using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;
using ScanBridge.Services;
using Serilog;

namespace ScanBridge.Api;

public static class PostScanEndpoints
{
    public static WebApplication MapPostScanEndpoints(this WebApplication app, PostScanManager postScanManager)
    {
        app.MapGet("/api/postscan/groups", () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var groups = db.PostScanActionGroups
                .OrderBy(g => g.SortOrder)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Enabled,
                    ScannerNames = db.PostScanActionGroupScanners
                        .Where(s => s.GroupId == g.Id)
                        .Select(s => s.ScannerName)
                        .ToList(),
                    Actions = db.PostScanActions
                        .Where(a => a.GroupId == g.Id)
                        .OrderBy(a => a.SortOrder)
                        .Select(a => new
                        {
                            a.Id,
                            a.Type,
                            a.Enabled,
                            Settings = JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new Dictionary<string, string>()
                        })
                        .ToList()
                })
                .ToList();

            var enabled = postScanManager.GetEnabledActions();
            return Results.Ok(new { groups, enabled });
        });

        app.MapPut("/api/postscan/groups", (List<PostScanActionGroupConfig> groupConfigs) =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.PostScanActionGroupScanners.ExecuteDelete();
            db.PostScanActions.ExecuteDelete();
            db.PostScanActionGroups.ExecuteDelete();

            for (var gi = 0; gi < groupConfigs.Count; gi++)
            {
                var gc = groupConfigs[gi];
                var group = new PostScanActionGroup
                {
                    Name = gc.Name,
                    Enabled = gc.Enabled,
                    SortOrder = gi
                };
                db.PostScanActionGroups.Add(group);
                db.SaveChanges();

                foreach (var scannerName in gc.ScannerNames)
                {
                    db.PostScanActionGroupScanners.Add(new PostScanActionGroupScanner
                    {
                        GroupId = group.Id,
                        ScannerName = scannerName
                    });
                }

                for (var ai = 0; ai < gc.Actions.Count; ai++)
                {
                    var ac = gc.Actions[ai];
                    db.PostScanActions.Add(new PostScanAction
                    {
                        GroupId = group.Id,
                        Type = ac.Type,
                        Enabled = ac.Enabled,
                        SettingsJson = JsonSerializer.Serialize(ac.Settings ?? new()),
                        SortOrder = ai
                    });
                }
            }
            db.SaveChanges();

            var finalConfigs = db.PostScanActionGroups
                .OrderBy(g => g.SortOrder)
                .Select(g => new PostScanActionGroupConfig
                {
                    Id = g.Id,
                    Name = g.Name,
                    Enabled = g.Enabled,
                    ScannerNames = db.PostScanActionGroupScanners
                        .Where(s => s.GroupId == g.Id)
                        .Select(s => s.ScannerName)
                        .ToList(),
                    Actions = db.PostScanActions
                        .Where(a => a.GroupId == g.Id)
                        .OrderBy(a => a.SortOrder)
                        .Select(a => new PostScanActionConfig
                        {
                            Type = a.Type,
                            Enabled = a.Enabled,
                            Settings = JsonSerializer.Deserialize<Dictionary<string, string>>(a.SettingsJson) ?? new()
                        })
                        .ToList()
                })
                .ToList();

            postScanManager.Configure(finalConfigs);
            Log.Information("Группы пост-скан действий обновлены: {Count}", finalConfigs.Count);

            var enabled = postScanManager.GetEnabledActions();
            return Results.Ok(new { groups = finalConfigs, enabled });
        });

        return app;
    }
}
