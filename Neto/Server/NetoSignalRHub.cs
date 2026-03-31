using Microsoft.AspNetCore.SignalR;
using Neto.Shared;

namespace Neto.Server
{
    public interface INetoSignalRBridge
    {
        Task OnConnectedAsync(string connectionId, string? remoteIp);
        Task OnDisconnectedAsync(string connectionId);
        Task OnPacketAsync(string connectionId, SignalRTransportPacket packet);
    }

    public class NetoSignalRHub : Hub
    {
        private readonly INetoSignalRBridge _bridge;

        public NetoSignalRHub(INetoSignalRBridge bridge)
        {
            _bridge = bridge;
        }

        public override async Task OnConnectedAsync()
        {
            var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            await _bridge.OnConnectedAsync(Context.ConnectionId, remoteIp);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _bridge.OnDisconnectedAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendPacket(SignalRTransportPacket packet)
        {
            await _bridge.OnPacketAsync(Context.ConnectionId, packet);
        }
    }
}
