using ScanBridge.Models;
using ScanBridge.Services;
using Serilog;

namespace ScanBridge.Api;

public static class PostScanEndpoints
{
    public static WebApplication MapPostScanEndpoints(this WebApplication app, PostScanManager postScanManager, GroupManager groupManager)
    {
        app.MapGet("/api/postscan/groups", () =>
        {
            var groups = groupManager.LoadGroups();
            var enabled = postScanManager.GetEnabledActions();
            return Results.Ok(new { groups, enabled });
        });

        app.MapPut("/api/postscan/groups", (List<PostScanActionGroupConfig> groupConfigs) =>
        {
            var finalConfigs = groupManager.SaveGroups(groupConfigs);
            postScanManager.Configure(finalConfigs);

            var enabled = postScanManager.GetEnabledActions();
            return Results.Ok(new { groups = finalConfigs, enabled });
        });

        return app;
    }
}
