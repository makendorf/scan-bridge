using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;

namespace ScanBridge.Services;

/// <summary>
/// Менеджер групп пост-скан действий.
/// Загружает и сохраняет группы из/в базу данных.
/// </summary>
public class GroupManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GroupManager> _logger;

    public GroupManager(IServiceScopeFactory scopeFactory, ILogger<GroupManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Загружает все группы из БД с действиями и привязками к сканерам.
    /// </summary>
    public List<PostScanActionGroupConfig> LoadGroups()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return db.PostScanActionGroups
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
            .AsSplitQuery()
            .ToList();
    }

    /// <summary>
    /// Сохраняет группы в БД (полная замена).
    /// </summary>
    public List<PostScanActionGroupConfig> SaveGroups(List<PostScanActionGroupConfig> groupConfigs)
    {
        using var scope = _scopeFactory.CreateScope();
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

        _logger.LogInformation("Группы пост-скан действий обновлены: {Count}", groupConfigs.Count);

        return LoadGroups();
    }
}
