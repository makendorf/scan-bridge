using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Менеджер пост-скан действий.
/// Управляет группами действий и визуальными сценариями, привязанными к сканерам.
/// Группы и сценарии выполняются параллельно.
/// Действия внутри группы выполняются последовательно.
/// </summary>
public class PostScanManager
{
    private readonly IPostScanActionFactory _factory;
    private readonly ILogger<PostScanManager> _logger;
    private volatile IReadOnlyList<CompiledGroup> _groups = Array.Empty<CompiledGroup>();
    private volatile IReadOnlyList<CompiledScenario> _scenarios = Array.Empty<CompiledScenario>();
    private ScenarioExecutor? _scenarioExecutor;

    /// <summary>
    /// Создаёт экземпляр менеджера пост-скан действий.
    /// </summary>
    /// <param name="factory">Фабрика для создания экшенов.</param>
    /// <param name="logger">Логгер.</param>
    public PostScanManager(IPostScanActionFactory factory, ILogger<PostScanManager> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Настраивает группы действий из конфигурации.
    /// Атомарно заменяет текущий список групп на новый.
    /// </summary>
    /// <param name="groupConfigs">Список конфигураций групп.</param>
    public virtual void Configure(List<PostScanActionGroupConfig> groupConfigs)
    {
        var newGroups = new List<CompiledGroup>();

        foreach (var groupConfig in groupConfigs)
        {
            if (!groupConfig.Enabled) continue;

            var compiledActions = new List<CompiledAction>();
            foreach (var actionConfig in groupConfig.Actions)
            {
                if (!actionConfig.Enabled) continue;

                var action = CreateAction(actionConfig);
                if (action != null)
                {
                    compiledActions.Add(new CompiledAction(action, actionConfig));
                }
                else
                {
                    _logger.LogWarning("Неизвестный тип пост-скан действия: {Type}", actionConfig.Type);
                }
            }

            if (compiledActions.Count > 0)
            {
                newGroups.Add(new CompiledGroup(groupConfig, compiledActions));
                _logger.LogInformation("Группа «{Name}»: {Count} действий, сканеры: {Scanners}",
                    groupConfig.Name, compiledActions.Count,
                    groupConfig.ScannerNames.Count > 0 ? string.Join(", ", groupConfig.ScannerNames) : "все");
            }
        }

        _groups = newGroups.AsReadOnly();
    }

    /// <summary>
    /// Настраивает визуальные сценарии из конфигурации.
    /// Атомарно заменяет текущий список сценариев на новый.
    /// </summary>
    /// <param name="scenarioConfigs">Список конфигураций сценариев.</param>
    /// <param name="executor">Executor для компиляции сценариев.</param>
    public virtual void ConfigureScenarios(List<ScenarioConfig> scenarioConfigs, ScenarioExecutor executor)
    {
        _scenarioExecutor = executor;
        var newScenarios = new List<CompiledScenario>();

        foreach (var config in scenarioConfigs)
        {
            if (!config.Enabled) continue;

            var compiled = executor.Compile(config);
            if (compiled != null)
            {
                newScenarios.Add(compiled);
                _logger.LogInformation("Сценарий «{Name}»: {NodeCount} узлов, сканеры: {Scanners}",
                    config.Name, config.Nodes.Count,
                    config.ScannerNames.Count > 0 ? string.Join(", ", config.ScannerNames) : "все");
            }
        }

        _scenarios = newScenarios.AsReadOnly();
    }

    /// <summary>
    /// Перезагружает сценарии из базы данных.
    /// </summary>
    public virtual void ReloadScenarios(ScenarioService scenarioService, ScenarioExecutor executor)
    {
        var configs = scenarioService.GetAllWithGraph();
        ConfigureScenarios(configs, executor);
    }

    /// <summary>
    /// Выполняет все подходящие группы и сценарии для результата сканирования.
    /// Группы и сценарии выполняются параллельно.
    /// Действия внутри группы — последовательно.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public virtual async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
    {
        var groups = _groups;
        var scenarios = _scenarios;

        var matchingGroups = groups
            .Where(g => MatchesScanner(g.Config, scan.ScannerName))
            .ToList();

        var matchingScenarios = scenarios
            .Where(s => MatchesScanner(s.Config, scan.ScannerName))
            .ToList();

        if (matchingGroups.Count == 0 && matchingScenarios.Count == 0) return;

        var tasks = new List<Task>();

        foreach (var group in matchingGroups)
        {
            tasks.Add(ExecuteGroupAsync(group, scan, ct));
        }

        foreach (var scenario in matchingScenarios)
        {
            tasks.Add(ExecuteScenarioAsync(scenario, scan, ct));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Возвращает список типов активных пост-скан действий.
    /// </summary>
    /// <returns>Только для чтения список строк с типами действий.</returns>
    public virtual IReadOnlyList<string> GetEnabledActions()
    {
        return _groups
            .SelectMany(g => g.Actions)
            .Select(a => a.Action.Type)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Получить количество активных сценариев.
    /// </summary>
    public int GetScenarioCount() => _scenarios.Count;

    /// <summary>
    /// Проверяет, подходит ли группа для данного сканера.
    /// Пустой список сканеров группы означает привязку ко всем.
    /// </summary>
    private static bool MatchesScanner(PostScanActionGroupConfig groupConfig, string scannerName)
    {
        if (groupConfig.ScannerNames.Count == 0)
            return true;

        return groupConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Проверяет, подходит ли сценарий для данного сканера.
    /// Пустой список сканеров сценария означает привязку ко всем.
    /// </summary>
    private static bool MatchesScanner(ScenarioConfig scenarioConfig, string scannerName)
    {
        if (scenarioConfig.ScannerNames.Count == 0)
            return true;

        return scenarioConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Выполняет все действия группы последовательно.
    /// </summary>
    private async Task ExecuteGroupAsync(CompiledGroup group, ScanResult scan, CancellationToken ct)
    {
        foreach (var compiledAction in group.Actions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await compiledAction.Action.ExecuteAsync(scan, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в пост-скан действии {Type} (группа «{Group}»)",
                    compiledAction.Action.Type, group.Config.Name);
            }
        }
    }

    /// <summary>
    /// Создаёт экземпляр пост-скан действия по конфигурации.
    /// </summary>
    /// <param name="config">Конфигурация действия.</param>
    /// <returns>Экземпляр действия или null, если тип неизвестен.</returns>
    private IPostScanAction? CreateAction(PostScanActionConfig config)
    {
        var settings = config.Settings ?? new();
        return _factory.Create(config.Type, settings);
    }

    /// <summary>
    /// Выполняет скомпилированный сценарий.
    /// </summary>
    private async Task ExecuteScenarioAsync(CompiledScenario scenario, ScanResult scan, CancellationToken ct)
    {
        try
        {
            var context = new ScenarioContext
            {
                Scan = scan,
                CancellationToken = ct
            };

            await ExecuteNodeAsync(scenario.StartNode, context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в сценарии «{Name}»", scenario.Config.Name);
        }
    }

    /// <summary>
    /// Рекурсивно выполняет узел сценария.
    /// </summary>
    private async Task ExecuteNodeAsync(CompiledNode node, ScenarioContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            switch (node.Config.Type)
            {
                case "Start":
                    break;

                case "Condition":
                    var settings = node.Config.Settings ?? new Dictionary<string, string>();
                    var result = ConditionEvaluator.Evaluate(settings, context.Scan);
                    context.Variables["lastCondition"] = result;
                    break;

                case "End":
                    return;

                default:
                    // All action types: Log, Replacement, ClipboardPaste, etc.
                    if (node.Action != null)
                    {
                        await node.Action.ExecuteAsync(context.Scan, context.CancellationToken);
                    }
                    break;
            }

            // Определить следующие узлы
            List<CompiledNode> nextNodes;
            if (node.Config.Type == "Condition")
            {
                var conditionResult = context.Variables.TryGetValue("lastCondition", out var val) && val is bool b && b;
                var portName = conditionResult ? "output_1" : "output_2";

                if (node.PortConnections.TryGetValue(portName, out var portNodes) && portNodes.Count > 0)
                {
                    nextNodes = portNodes;
                }
                else
                {
                    nextNodes = node.NextNodes.Take(1).ToList();
                }
            }
            else
            {
                nextNodes = node.NextNodes;
            }

            foreach (var next in nextNodes)
            {
                await ExecuteNodeAsync(next, context);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в узле {NodeId} ({Type})", node.Config.NodeId, node.Config.Type);
            foreach (var next in node.NextNodes)
            {
                await ExecuteNodeAsync(next, context);
            }
        }
    }

    private record CompiledAction(IPostScanAction Action, PostScanActionConfig Config);
    private record CompiledGroup(PostScanActionGroupConfig Config, List<CompiledAction> Actions);
}
