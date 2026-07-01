using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Data.Entities;
using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Сервис управления сценариями: CRUD + компиляция + валидация.
/// </summary>
public class ScenarioService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScenarioService> _logger;

    public ScenarioService(IServiceProvider serviceProvider, ILogger<ScenarioService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Получить все сценарии (без нод/connections для списка).
    /// </summary>
    public List<ScenarioConfig> GetAll()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return db.Scenarios
            .OrderBy(s => s.SortOrder)
            .Select(s => new ScenarioConfig
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Enabled = s.Enabled,
                ScannerNames = JsonSerializer.Deserialize<List<string>>(s.ScannerNamesJson) ?? new(),
                Nodes = new(),
                Connections = new()
            })
            .ToList();
    }

    /// <summary>
    /// Получить все включённые сценарии с полным графом (для выполнения).
    /// </summary>
    public List<ScenarioConfig> GetAllWithGraph()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var scenarios = db.Scenarios
            .Where(s => s.Enabled)
            .OrderBy(s => s.SortOrder)
            .ToList();

        return scenarios.Select(s => new ScenarioConfig
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Enabled = s.Enabled,
            ScannerNames = JsonSerializer.Deserialize<List<string>>(s.ScannerNamesJson) ?? new(),
            Nodes = db.ScenarioNodes
                .Where(n => n.ScenarioId == s.Id)
                .Select(n => new ScenarioNodeConfig
                {
                    NodeId = n.NodeId,
                    Type = n.Type,
                    PositionX = n.PositionX,
                    PositionY = n.PositionY,
                    Settings = JsonSerializer.Deserialize<Dictionary<string, string>>(n.SettingsJson) ?? new(),
                    ActionType = n.ActionType
                })
                .ToList(),
            Connections = db.ScenarioConnections
                .Where(c => c.ScenarioId == s.Id)
                .Select(c => new ScenarioConnectionConfig
                {
                    SourceNodeId = c.SourceNodeId,
                    TargetNodeId = c.TargetNodeId,
                    SourcePort = c.SourcePort,
                    TargetPort = c.TargetPort
                })
                .ToList()
        }).ToList();
    }

    /// <summary>
    /// Получить сценарий по ID с графом.
    /// </summary>
    public ScenarioConfig? GetById(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var scenario = db.Scenarios.Find(id);
        if (scenario == null) return null;

        var nodes = db.ScenarioNodes
            .Where(n => n.ScenarioId == id)
            .Select(n => new ScenarioNodeConfig
            {
                NodeId = n.NodeId,
                Type = n.Type,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
                Settings = JsonSerializer.Deserialize<Dictionary<string, string>>(n.SettingsJson) ?? new(),
                ActionType = n.ActionType
            })
            .ToList();

        var connections = db.ScenarioConnections
            .Where(c => c.ScenarioId == id)
            .Select(c => new ScenarioConnectionConfig
            {
                SourceNodeId = c.SourceNodeId,
                TargetNodeId = c.TargetNodeId,
                SourcePort = c.SourcePort,
                TargetPort = c.TargetPort
            })
            .ToList();

        return new ScenarioConfig
        {
            Id = scenario.Id,
            Name = scenario.Name,
            Description = scenario.Description,
            Enabled = scenario.Enabled,
            ScannerNames = JsonSerializer.Deserialize<List<string>>(scenario.ScannerNamesJson) ?? new(),
            Nodes = nodes,
            Connections = connections
        };
    }

    /// <summary>
    /// Создать сценарий.
    /// </summary>
    public int Create(ScenarioConfig config)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var scenario = new Scenario
        {
            Name = config.Name,
            Description = config.Description,
            Enabled = config.Enabled,
            ScannerNamesJson = JsonSerializer.Serialize(config.ScannerNames),
            SortOrder = db.Scenarios.Count(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Scenarios.Add(scenario);
        db.SaveChanges();

        // Сохранить узлы и связи
        SaveNodesAndConnections(db, scenario.Id, config);

        _logger.LogInformation("Создан сценарий «{Name}» (ID: {Id})", config.Name, scenario.Id);
        return scenario.Id;
    }

    /// <summary>
    /// Обновить сценарий.
    /// </summary>
    public void Update(int id, ScenarioConfig config)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var scenario = db.Scenarios.Find(id);
        if (scenario == null)
            throw new InvalidOperationException($"Сценарий с ID {id} не найден");

        scenario.Name = config.Name;
        scenario.Description = config.Description;
        scenario.Enabled = config.Enabled;
        scenario.ScannerNamesJson = JsonSerializer.Serialize(config.ScannerNames);
        scenario.UpdatedAt = DateTime.UtcNow;

        // Удалить старые узлы и связи
        db.ScenarioNodes.RemoveRange(db.ScenarioNodes.Where(n => n.ScenarioId == id));
        db.ScenarioConnections.RemoveRange(db.ScenarioConnections.Where(c => c.ScenarioId == id));

        // Сохранить новые
        SaveNodesAndConnections(db, id, config);

        db.SaveChanges();
        _logger.LogInformation("Обновлён сценарий «{Name}» (ID: {Id})", config.Name, id);
    }

    /// <summary>
    /// Удалить сценарий.
    /// </summary>
    public void Delete(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var scenario = db.Scenarios.Find(id);
        if (scenario == null)
            throw new InvalidOperationException($"Сценарий с ID {id} не найден");

        db.ScenarioNodes.RemoveRange(db.ScenarioNodes.Where(n => n.ScenarioId == id));
        db.ScenarioConnections.RemoveRange(db.ScenarioConnections.Where(c => c.ScenarioId == id));
        db.Scenarios.Remove(scenario);
        db.SaveChanges();

        _logger.LogInformation("Удалён сценарий «{Name}» (ID: {Id})", scenario.Name, id);
    }

    /// <summary>
    /// Валидация графа сценария.
    /// </summary>
    public ValidationResult Validate(ScenarioConfig config)
    {
        var errors = new List<string>();

        if (config.Nodes.Count == 0)
        {
            errors.Add("Сценарий не содержит узлов");
            return new ValidationResult(false, errors);
        }

        // Проверить наличие точки входа: Scanner или Start (legacy)
        var hasScanner = config.Nodes.Any(n => n.Type == "Scanner");
        var hasStart = config.Nodes.Any(n => n.Type == "Start");
        var hasEnd = config.Nodes.Any(n => n.Type == "End");

        if (!hasScanner && !hasStart)
            errors.Add("Отсутствует узел Scanner (точка входа данных)");
        if (!hasEnd)
            errors.Add("Отсутствует узел End");

        // Проверить, что все узлы связаны
        var nodeIds = config.Nodes.Select(n => n.NodeId).ToHashSet();
        var connectedNodes = new HashSet<string>();

        foreach (var conn in config.Connections)
        {
            connectedNodes.Add(conn.SourceNodeId);
            connectedNodes.Add(conn.TargetNodeId);
        }

        var disconnected = nodeIds.Except(connectedNodes);
        foreach (var nodeId in disconnected)
        {
            var node = config.Nodes.First(n => n.NodeId == nodeId);
            // Entry/exit points (Start, Scanner, End) can be disconnected
            if (node.Type != "Start" && node.Type != "Scanner" && node.Type != "End")
            {
                errors.Add($"Узел «{nodeId}» не связан с другими узлами");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Миграция из группы пост-скан действий.
    /// </summary>
    public ScenarioConfig MigrateGroup(PostScanActionGroupConfig group)
    {
        var scenario = new ScenarioConfig
        {
            Name = group.Name,
            Enabled = group.Enabled,
            ScannerNames = new List<string>(group.ScannerNames),
            Nodes = new List<ScenarioNodeConfig>(),
            Connections = new List<ScenarioConnectionConfig>()
        };

        // Создать Scanner узел (точка входа)
        var startNodeId = "1";
        scenario.Nodes.Add(new ScenarioNodeConfig
        {
            NodeId = startNodeId,
            Type = "Scanner",
            PositionX = 50,
            PositionY = 200,
            Settings = new Dictionary<string, string> { ["scannerName"] = "" }
        });

        // Создать Action узлы
        string prevNodeId = startNodeId;
        int nodeCounter = 2;

        foreach (var action in group.Actions)
        {
            var nodeId = nodeCounter++.ToString();
            scenario.Nodes.Add(new ScenarioNodeConfig
            {
                NodeId = nodeId,
                Type = "Action",
                PositionX = nodeCounter * 200,
                PositionY = 200,
                ActionType = action.Type,
                Settings = action.Settings != null ? new Dictionary<string, string>(action.Settings) : new()
            });

            scenario.Connections.Add(new ScenarioConnectionConfig
            {
                SourceNodeId = prevNodeId,
                TargetNodeId = nodeId,
                SourcePort = "output_1",
                TargetPort = "input_1"
            });

            prevNodeId = nodeId;
        }

        // Создать End узел
        var endNodeId = nodeCounter.ToString();
        scenario.Nodes.Add(new ScenarioNodeConfig
        {
            NodeId = endNodeId,
            Type = "End",
            PositionX = (nodeCounter + 1) * 200,
            PositionY = 200
        });

        scenario.Connections.Add(new ScenarioConnectionConfig
        {
            SourceNodeId = prevNodeId,
            TargetNodeId = endNodeId,
            SourcePort = "output_1",
            TargetPort = "input_1"
        });

        return scenario;
    }

    private void SaveNodesAndConnections(AppDbContext db, int scenarioId, ScenarioConfig config)
    {
        foreach (var nodeConfig in config.Nodes)
        {
            db.ScenarioNodes.Add(new ScenarioNode
            {
                ScenarioId = scenarioId,
                NodeId = nodeConfig.NodeId,
                Type = nodeConfig.Type,
                PositionX = nodeConfig.PositionX,
                PositionY = nodeConfig.PositionY,
                SettingsJson = JsonSerializer.Serialize(nodeConfig.Settings ?? new()),
                ActionType = nodeConfig.ActionType
            });
        }

        foreach (var connConfig in config.Connections)
        {
            db.ScenarioConnections.Add(new ScenarioConnection
            {
                ScenarioId = scenarioId,
                SourceNodeId = connConfig.SourceNodeId,
                TargetNodeId = connConfig.TargetNodeId,
                SourcePort = connConfig.SourcePort,
                TargetPort = connConfig.TargetPort
            });
        }

        db.SaveChanges();
    }
}

/// <summary>
/// Результат валидации.
/// </summary>
public record ValidationResult(bool IsValid, List<string> Errors);
