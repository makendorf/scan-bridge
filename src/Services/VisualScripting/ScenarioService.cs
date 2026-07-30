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
            .ToList()
            .Select(s => new ScenarioConfig
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Enabled = s.Enabled,
                ScannerNames = JsonSerializer.Deserialize<List<string>>(s.ScannerNamesJson) ?? new(),
                TriggerType = Enum.TryParse<TriggerType>(s.TriggerType, out var tt) ? tt : TriggerType.Scanner,
                TriggerSettings = s.TriggerSettingsJson != null
                    ? JsonSerializer.Deserialize<TriggerSettingsConfig>(s.TriggerSettingsJson)
                    : null,
                Nodes = new(),
                Connections = new()
            })
            .ToList();
    }

    /// <summary>
    /// Получить все включённые сценарии, содержащие узел "Из сценария" (FromScenario).
    /// Используется для выпадающего списка в узле "В сценарий".
    /// </summary>
    public List<ScenarioConfig> GetAllWithFromScenarioNode()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Найти ID сценариев, у которых есть узел FromScenario
        var scenarioIdsWithFromNode = db.ScenarioNodes
            .Where(n => n.Type == "FromScenario")
            .Select(n => n.ScenarioId)
            .Distinct()
            .ToList();

        return db.Scenarios
            .Where(s => s.Enabled && scenarioIdsWithFromNode.Contains(s.Id))
            .OrderBy(s => s.SortOrder)
            .ToList()
            .Select(s => new ScenarioConfig
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Enabled = s.Enabled,
                Nodes = new(),
                Connections = new()
            })
            .ToList();
    }

    /// <summary>
    /// Получить сценарии, которые ссылаются на указанный сценарий через узел "В сценарий" (ToScenario).
    /// Используется для отображения в настройках узла "Из сценария".
    /// </summary>
    public List<ScenarioConfig> GetScenariosThatReference(int targetScenarioId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Найти ID сценариев, у которых есть узел ToScenario с TargetScenarioId = targetScenarioId
        var referencingScenarioIds = db.ScenarioNodes
            .Where(n => n.Type == "ToScenario" && n.SettingsJson != null && n.SettingsJson.Contains($"\"TargetScenarioId\":\"{targetScenarioId}\""))
            .Select(n => n.ScenarioId)
            .Distinct()
            .ToList();

        return db.Scenarios
            .Where(s => referencingScenarioIds.Contains(s.Id))
            .OrderBy(s => s.SortOrder)
            .ToList()
            .Select(s => new ScenarioConfig
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Enabled = s.Enabled,
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
            TriggerType = Enum.TryParse<TriggerType>(s.TriggerType, out var tt) ? tt : TriggerType.Scanner,
            TriggerSettings = s.TriggerSettingsJson != null
                ? JsonSerializer.Deserialize<TriggerSettingsConfig>(s.TriggerSettingsJson)
                : null,
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
            TriggerType = Enum.TryParse<TriggerType>(scenario.TriggerType, out var tt) ? tt : TriggerType.Scanner,
            TriggerSettings = scenario.TriggerSettingsJson != null
                ? JsonSerializer.Deserialize<TriggerSettingsConfig>(scenario.TriggerSettingsJson)
                : null,
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
            TriggerType = config.TriggerType.ToString(),
            TriggerSettingsJson = config.TriggerSettings != null
                ? JsonSerializer.Serialize(config.TriggerSettings)
                : null,
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
        scenario.TriggerType = config.TriggerType.ToString();
        scenario.TriggerSettingsJson = config.TriggerSettings != null
            ? JsonSerializer.Serialize(config.TriggerSettings)
            : null;
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

        // Проверить наличие точки входа: Scanner, Start (legacy), HttpTrigger, ScheduleTrigger, FileTrigger
        var entryTypes = new HashSet<string> { "Scanner", "Start", "HttpTrigger", "ScheduleTrigger", "FileTrigger", "FromScenario" };
        var hasEntryNode = config.Nodes.Any(n => entryTypes.Contains(n.Type));
        var hasEnd = config.Nodes.Any(n => n.Type == "End");

        if (!hasEntryNode)
            errors.Add("Отсутствует узел-триггер (Сканер, HTTP, Расписание, Файл или Из сценария)");
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
            // Entry/exit points can be disconnected
            if (!entryTypes.Contains(node.Type) && node.Type != "End")
            {
                errors.Add($"Узел «{nodeId}» не связан с другими узлами");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
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
