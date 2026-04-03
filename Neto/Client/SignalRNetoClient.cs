using Microsoft.AspNetCore.SignalR.Client;
using MessagePack;
using Neto.Shared;
using System.Net;
using System.Net.Sockets;

namespace Neto.Client
{
    public class SignalRNetoClient : NetObjectHandler<ClientModel>
    {
        private readonly string _clientUniqueToken;
        private HubConnection? _connection;
        private bool _disconnectRaised;

        public SignalRNetoClient(string? clientUniqueToken = null)
        {
            CancellationToken = new CancellationTokenSource();
            _clientUniqueToken = clientUniqueToken ?? string.Empty;
        }

        ~SignalRNetoClient()
        {
            CancellationToken.Dispose();
        }

        public event EventHandler? Connected;
        public event EventHandler<StringEventArgs>? Disconnected;
        public event EventHandler<StringEventArgs>? Kicked;

        public virtual string Version => "1";

        public Guid ClientGuid { get; private set; }
        public bool IsConnected => _connection?.State == HubConnectionState.Connected;
        protected CancellationTokenSource CancellationToken { get; private set; }

        public static IPEndPoint? EndPointFromAddress(string address, int port, out string error)
        {
            error = string.Empty;
            if (port < 1 || port > 65535)
            {
                error = "Invalid port";
                return null;
            }
            if (IPAddress.TryParse(address, out var ipAddress))
            {
                return new IPEndPoint(ipAddress, port);
            }
            try
            {
                var addresses = Dns.GetHostAddresses(address);
                foreach (var ip in addresses)
                {
                    if (ip.ToString() == "::1")
                        continue;
                    return new IPEndPoint(ip, port);
                }
                error = $"Unable to resolve hostname {address}";
                return null;
            }
            catch (Exception e)
            {
                error = $"Unable to resolve hostname {address}: {e.Message}";
                return null;
            }
        }

        public virtual string GetConnectionStatusString()
        {
            if (!IsConnected)
                return "Not connected";
            if (CancellationToken.IsCancellationRequested)
                return "Stopping...";
            return "Connected";
        }

        public async Task<ConnectionResult> Connect(string address, int port)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                FireOnError("Invalid address");
                return ConnectionResult.Denied;
            }

            Uri hubUri;
            try
            {
                var raw = address.Trim();
                if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute) &&
                    (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                {
                    var builder = new UriBuilder(absolute)
                    {
                        Path = "/neto"
                    };
                    if (builder.Port <= 0)
                        builder.Port = port;
                    hubUri = builder.Uri;
                }
                else
                {
                    var scheme = port == 443 ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
                    hubUri = new UriBuilder(scheme, raw, port, "neto").Uri;
                }
            }
            catch (Exception e)
            {
                FireOnError($"Invalid address: {e.Message}");
                return ConnectionResult.Denied;
            }

            return await Connect(hubUri);
        }

        public async Task<ConnectionResult> Connect(IPEndPoint ipEndpoint)
        {
            var uri = new UriBuilder(Uri.UriSchemeHttp, ipEndpoint.Address.ToString(), ipEndpoint.Port, "neto").Uri;
            return await Connect(uri);
        }

        private async Task<ConnectionResult> Connect(Uri hubUri)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                FireOnError("Already connected");
                return ConnectionResult.Denied;
            }

            CancellationToken = new CancellationTokenSource();
            _disconnectRaised = false;

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUri)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<SignalRTransportPacket>("ReceivePacket", async transport =>
            {
                var packet = FromTransportPacket(transport);
                if (packet != null)
                    await handleIncomingPacket(packet);
            });

            _connection.Closed += async _ =>
            {
                RaiseDisconnected("Disconnected");
                await Task.CompletedTask;
            };

            try
            {
                FireOnStatus($"Connecting to {hubUri.Host}:{hubUri.Port}...");
                await _connection.StartAsync(CancellationToken.Token);
                FireOnStatus("Connected to server");
                await SendPacketToServer(new Packet(PacketTypes.ClientRegister, new ClientRegister(NetConstants.ClientRegisterString, Version, _clientUniqueToken)));
                return ConnectionResult.Connected;
            }
            catch (Exception e)
            {
                FireOnError($"Connect Error: {e.Message}");
                return ConnectionResult.Exception;
            }
        }

        public async Task Disconnect()
        {
            if (_connection == null)
                return;
            await SendPacketToServer(new Packet(PacketTypes.ClientDisconnect));
            CancellationToken.Cancel();
            await _connection.StopAsync();
            RaiseDisconnected("Disconnected");
        }

        public async Task SendPacketToServer(Packet p)
        {
            if (CancellationToken.IsCancellationRequested || _connection?.State != HubConnectionState.Connected)
            {
                FireOnError("Error sending message to server: Not connected");
                return;
            }

            try
            {
                await _connection.InvokeAsync("SendPacket", ToTransportPacket(p), CancellationToken.Token);
            }
            catch (Exception e)
            {
                CancellationToken.Cancel();
                FireOnError($"Error sending message to server: {e.Message}");
            }
        }

        private SignalRTransportPacket ToTransportPacket(Packet packet)
        {
            var bytes = MessagePackSerializer.Serialize(packet, GetMessagePackOptions());
            return new SignalRTransportPacket(bytes);
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

        private async Task handleIncomingPacket(Packet packet)
        {
            switch (packet.PacketType)
            {
                case PacketTypes.ServerRegisterAccepted:
                    var accept = packet.GetObjectData<ServerRegisterAccepted>();
                    if (accept?.Message == NetConstants.ServerRegisterString)
                    {
                        ClientGuid = accept.ClientGuid;
                        Connected?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        RaiseDisconnected("Invalid server response");
                        await Disconnect();
                    }
                    break;

                case PacketTypes.ServerRegisterDenied:
                    var denied = packet.GetObjectData<ServerRegisterDenied>();
                    Kicked?.Invoke(this, new StringEventArgs($"Registration denied: {denied?.Message ?? "Unknown reason"}"));
                    await Disconnect();
                    break;

                case PacketTypes.ServerClientDropped:
                    CancellationToken.Cancel();
                    var kicked = packet.GetObjectData<ServerKicked>();
                    Kicked?.Invoke(this, new StringEventArgs($"Kicked from server: {kicked?.Reason ?? "Unknown reason"}"));
                    RaiseDisconnected("Disconnected");
                    break;

                case PacketTypes.ServerShutdown:
                    CancellationToken.Cancel();
                    RaiseDisconnected("Server shutting down");
                    break;

                case PacketTypes.ObjectData:
                    DispatchObjects(null, packet.Objects);
                    break;

                case PacketTypes.KeepAlive:
                    await SendPacketToServer(new Packet(PacketTypes.KeepAlive, new KeepAlive()));
                    break;
            }
        }

        private void RaiseDisconnected(string message)
        {
            if (_disconnectRaised)
                return;
            _disconnectRaised = true;
            Disconnected?.Invoke(this, new StringEventArgs(message));
        }
    }
}
