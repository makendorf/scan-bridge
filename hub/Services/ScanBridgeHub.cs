using Microsoft.AspNetCore.SignalR;

namespace ScanBridgeHub.Services;

public class StatsHub : Hub
{
    public async Task SendStatsUpdate(object stats)
    {
        await Clients.All.SendAsync("StatsUpdate", stats);
    }
}
