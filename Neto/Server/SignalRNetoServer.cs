using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MessagePack;
using Neto.Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Neto.Server
{
    public class SignalRNetoServer<CM> : NetObjectHandler<CM> where CM : ClientModel
    {
        private readonly ConcurrentDictionary<Guid, CM> _clients = new();
        private readonly ConcurrentDictionary<string, CM> _clientsByConnectionId = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Guid, string> _connectionIdByGuid = new();
        private readonly ConcurrentDictionary<string, string> _clientIpByConnectionId = new(StringComparer.Ordinal);
        private readonly ConstructorInfo _clientModelConstructor;
        private readonly string? _bindAddress;

        private readonly INetoSignalRBridge _bridge;
        private IHost? _host;
        private IHubContext<NetoSignalRHub>? _hubContext;

        public SignalRNetoServer(int port, string? bindAddress = null)
        {
            Port = port;
            _bindAddress = bindAddress;
            IPAddresses = Array.Empty<IPAddress>();

            var ctor = typeof(CM).GetConstructor(new[] { typeof(TcpClient) });
            if (ctor == null)
                throw new ApplicationException("No constructor with TcpClient as argument was found");
            _clientModelConstructor = ctor;

            CachedIdentities = new ConcurrentDictionary<string, ClientIdentity>();
            _bridge = new SignalRBridge(this);
        }

        public event EventHandler<ClientEventArgs<CM>>? OnClientConnected;
        public event EventHandler<ClientEventArgs<CM>>? OnClientDisconnected;

        public virtual string Version => "1";

        public IPAddress[] IPAddresses { get; private set; }
        public int Port { get; init; }
        protected bool Hosting { get; private set; }
        protected ConcurrentDictionary<string, ClientIdentity> CachedIdentities { get; set; }

        public void Host()
        {
            if (Hosting)
                throw new Exception("Already hosting");

            IPAddresses = ResolveAddressesToListenOn();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(IPAddresses.Select(ip => $"http://{ip}:{Port}").ToArray());
            builder.Services.AddSignalR();
            builder.Services.AddSingleton(_bridge);
            builder.Services.AddSingleton<INetoSignalRBridge>(_bridge);

            var app = builder.Build();
            app.MapHub<NetoSignalRHub>("/neto");
            _host = app;
            _hubContext = app.Services.GetRequiredService<IHubContext<NetoSignalRHub>>();
            _host.StartAsync().GetAwaiter().GetResult();

            Hosting = true;
            FireOnStatus($"Hosting server on {string.Join(", ", IPAddresses.Select(i => i.ToString()))}:{Port}");
        }

        public virtual async Task Stop()
        {
            if (!Hosting)
                throw new Exception("Not hosting");

            Hosting = false;
            await SendPacketToAllClients(new Packet(PacketTypes.ServerShutdown));
            foreach (var c in _clients.Values.ToArray())
                await DropClient(c);

            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
                _host = null;
            }
            _hubContext = null;
            FireOnStatus("Stopped server");
        }

        public async Task SendPacketToClient(Packet p, CM client)
        {
            if (_hubContext == null)
                return;
            if (_connectionIdByGuid.TryGetValue(client.ClientGuid, out var connectionId))
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceivePacket", ToTransportPacket(p));
        }

        protected virtual async Task DropClient(CM client)
        {
            if (_connectionIdByGuid.TryRemove(client.ClientGuid, out var connectionId))
            {
                _clientsByConnectionId.TryRemove(connectionId, out _);
                _clientIpByConnectionId.TryRemove(connectionId, out _);
            }

            if (client.IsRegistered)
            {
                OnClientDisconnected?.Invoke(this, new ClientEventArgs<CM>(client));
                client.IsRegistered = false;
            }

            client.Stop();
            _clients.TryRemove(client.ClientGuid, out _);
            await Task.CompletedTask;
        }

        protected async Task KickClient(CM client, string reason)
        {
            await SendPacketToClient(new Packet(PacketTypes.ServerClientDropped, new ServerKicked(reason)), client);
            await DropClient(client);
        }

        protected async Task SendPacketToAllClients(Packet p, bool onlyRegistered = false)
        {
            var clients = onlyRegistered ? _clients.Values.Where(c => c.IsRegistered) : _clients.Values;
            await SendPacketToClients(p, clients);
        }

        protected async Task SendPacketToClients(Packet p, IEnumerable<CM> clients)
        {
            if (_hubContext == null)
                return;

            var tp = ToTransportPacket(p);
            var tasks = new List<Task>();
            foreach (var c in clients)
            {
                if (_connectionIdByGuid.TryGetValue(c.ClientGuid, out var connectionId))
                    tasks.Add(_hubContext.Clients.Client(connectionId).SendAsync("ReceivePacket", tp));
            }
            await Task.WhenAll(tasks);
        }

        protected async Task SendPacketToAllClientsExcept(Packet p, Guid except, bool onlyRegistered = false)
        {
            var clients = onlyRegistered ? _clients.Values.Where(c => c.IsRegistered && c.ClientGuid != except) : _clients.Values.Where(c => c.ClientGuid != except);
            await SendPacketToClients(p, clients);
        }

        private async Task HandleIncomingPacket(CM client, Packet packet)
        {
            if (!client.IsRegistered && packet.PacketType != PacketTypes.ClientRegister)
            {
                await DropClient(client);
                return;
            }

            switch (packet.PacketType)
            {
                case PacketTypes.ClientRegister:
                    var reg = packet.GetObjectData<ClientRegister>();
                    if (reg?.Message != NetConstants.ClientRegisterString)
                    {
                        await DropClient(client);
                        return;
                    }
                    if (reg.Version != Version)
                    {
                        await SendPacketToClient(new Packet(PacketTypes.ServerRegisterDenied, new ServerRegisterDenied($"Incorrect version {reg.Version}. Server is running version {Version}")), client);
                        await DropClient(client);
                        return;
                    }

                    if (!client.IsRegistered)
                    {
                        client.IsRegistered = true;
                        if (!string.IsNullOrWhiteSpace(reg.IdentityToken))
                        {
                            var token = BuildClientToken(client, reg.IdentityToken);
                            if (!string.IsNullOrWhiteSpace(token))
                                CachedIdentities[token] = new ClientIdentity(token, client.ClientGuid);
                        }
                        await SendPacketToClient(new Packet(PacketTypes.ServerRegisterAccepted, new ServerRegisterAccepted(NetConstants.ServerRegisterString, client.ClientGuid)), client);
                        OnClientConnected?.Invoke(this, new ClientEventArgs<CM>(client));
                    }
                    break;

                case PacketTypes.ClientDisconnect:
                    await DropClient(client);
                    break;

                case PacketTypes.ObjectData:
                    DispatchObjects(client, packet.Objects);
                    break;

                case PacketTypes.KeepAlive:
                    break;
            }
        }

        private string BuildClientToken(CM client, string identityToken)
        {
            if (_connectionIdByGuid.TryGetValue(client.ClientGuid, out var connectionId) && _clientIpByConnectionId.TryGetValue(connectionId, out var ip))
                return $"{ip}:{identityToken}";
            return string.Empty;
        }

        private Packet? FromTransportPacket(SignalRTransportPacket transport)
        {
            try
            {
                return MessagePackSerializer.Deserialize<Packet>(transport.Payload, GetMessagePackOptions());
            }
            catch
            {
                return null;
            }
        }

        private SignalRTransportPacket ToTransportPacket(Packet packet)
        {
            var bytes = MessagePackSerializer.Serialize(packet, GetMessagePackOptions());
            return new SignalRTransportPacket(bytes);
        }

        private IPAddress[] ResolveAddressesToListenOn()
        {
            if (string.IsNullOrWhiteSpace(_bindAddress))
                return GetIpAddresses();

            if (IPAddress.TryParse(_bindAddress, out var parsed))
                return new[] { parsed };

            try
            {
                var resolved = Dns.GetHostAddresses(_bindAddress).Distinct().ToArray();
                return resolved.Length > 0 ? resolved : GetIpAddresses();
            }
            catch
            {
                return GetIpAddresses();
            }
        }

        private static IPAddress[] GetIpAddresses()
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            var local = IPAddress.Parse("127.0.0.1");
            return addresses.Any(a => a.Equals(local)) ? addresses : addresses.Concat(new[] { local }).ToArray();
        }

        private sealed class SignalRBridge : INetoSignalRBridge
        {
            private readonly SignalRNetoServer<CM> _server;

            public SignalRBridge(SignalRNetoServer<CM> server)
            {
                _server = server;
            }

            public Task OnConnectedAsync(string connectionId, string? remoteIp)
            {
                var client = (CM)_server._clientModelConstructor.Invoke(new object[] { new TcpClient() });
                _server._clients[client.ClientGuid] = client;
                _server._clientsByConnectionId[connectionId] = client;
                _server._connectionIdByGuid[client.ClientGuid] = connectionId;
                if (!string.IsNullOrWhiteSpace(remoteIp))
                    _server._clientIpByConnectionId[connectionId] = remoteIp;
                _server.FireOnStatus($"Client connected ({remoteIp ?? connectionId})");
                return Task.CompletedTask;
            }

            public async Task OnDisconnectedAsync(string connectionId)
            {
                if (_server._clientsByConnectionId.TryGetValue(connectionId, out var client))
                    await _server.DropClient(client);
            }

            public async Task OnPacketAsync(string connectionId, SignalRTransportPacket packet)
            {
                if (!_server._clientsByConnectionId.TryGetValue(connectionId, out var client))
                    return;

                var decoded = _server.FromTransportPacket(packet);
                if (decoded == null)
                {
                    if (++client.MalformedPackets >= 3)
                        await _server.KickClient(client, "Sent too many malformed packets");
                    return;
                }

                client.MalformedPackets = Math.Max(0, client.MalformedPackets - 1);
                client.LastActivity = DateTime.Now;
                await _server.HandleIncomingPacket(client, decoded);
            }
        }
    }
}
