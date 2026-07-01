using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Диспетчер пост-скан действий.
/// Определяет какие группы и сценарии выполнять для результата сканирования.
/// Группы и сценарии выполняются параллельно.
/// Действия внутри группы выполняются последовательно.
/// </summary>
public class ScanDispatcher
{
    private readonly IPostScanActionFactory _factory;
    private readonly ILogger<ScanDispatcher> _logger;
    private volatile IReadOnlyList<CompiledGroup> _groups = Array.Empty<CompiledGroup>();
    private volatile IReadOnlyList<CompiledScenario> _scenarios = Array.Empty<CompiledScenario>();
    private ScenarioExecutor? _scenarioExecutor;

    public ScanDispatcher(IPostScanActionFactory factory, ILogger<ScanDispatcher> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Настраивает группы действий из конфигурации.
    /// </summary>
    public void ConfigureGroups(List<PostScanActionGroupConfig> groupConfigs)
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
    /// </summary>
    public void ConfigureScenarios(List<ScenarioConfig> scenarioConfigs, ScenarioExecutor executor)
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
    public void ReloadScenarios(ScenarioService scenarioService, ScenarioExecutor executor)
    {
        var configs = scenarioService.GetAllWithGraph();
        ConfigureScenarios(configs, executor);
    }

    /// <summary>
    /// Выполняет все подходящие группы и сценарии для результата сканирования.
    /// </summary>
    public async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
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
            tasks.Add(ExecuteGroupAsync(group, scan, ct));

        foreach (var scenario in matchingScenarios)
            tasks.Add(ExecuteScenarioAsync(scenario, scan, ct));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Возвращает список типов активных пост-скан действий.
    /// </summary>
    public IReadOnlyList<string> GetEnabledActions()
    {
        return _groups
            .SelectMany(g => g.Actions)
            .Select(a => a.Action.Type)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Количество активных сценариев.
    /// </summary>
    public int GetScenarioCount() => _scenarios.Count;

    private static bool MatchesScanner(PostScanActionGroupConfig groupConfig, string scannerName)
    {
        if (groupConfig.ScannerNames.Count == 0) return true;
        return groupConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

    private static bool MatchesScanner(ScenarioConfig scenarioConfig, string scannerName)
    {
        if (scenarioConfig.ScannerNames.Count == 0) return true;
        return scenarioConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

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

    private IPostScanAction? CreateAction(PostScanActionConfig config)
    {
        var settings = config.Settings ?? new();
        return _factory.Create(config.Type, settings);
    }

    private async Task ExecuteScenarioAsync(CompiledScenario scenario, ScanResult scan, CancellationToken ct)
    {
        try
        {
            var context = new ScenarioContext { Scan = scan, CancellationToken = ct };
            await ExecuteNodeAsync(scenario.StartNode, context);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в сценарии «{Name}»", scenario.Config.Name);
        }
    }

    private async Task ExecuteNodeAsync(CompiledNode node, ScenarioContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            switch (node.Config.Type)
            {
                case "Start": break;
                case "Condition":
                    var settings = node.Config.Settings ?? new Dictionary<string, string>();
                    var result = ConditionEvaluator.Evaluate(settings, context.Scan);
                    context.Variables["lastCondition"] = result;
                    break;
                case "End": return;
                default:
                    if (node.Action != null)
                        await node.Action.ExecuteAsync(context.Scan, context.CancellationToken);
                    break;
            }

            List<CompiledNode> nextNodes;
            if (node.Config.Type == "Condition")
            {
                var conditionResult = context.Variables.TryGetValue("lastCondition", out var val) && val is bool b && b;
                var portName = conditionResult ? "output_1" : "output_2";
                nextNodes = node.PortConnections.TryGetValue(portName, out var portNodes) && portNodes.Count > 0
                    ? portNodes
                    : node.NextNodes.Take(1).ToList();
            }
            else
            {
                nextNodes = node.NextNodes;
            }

            foreach (var next in nextNodes)
                await ExecuteNodeAsync(next, context);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в узле {NodeId} ({Type})", node.Config.NodeId, node.Config.Type);
            foreach (var next in node.NextNodes)
                await ExecuteNodeAsync(next, context);
        }
    }

    internal record CompiledAction(IPostScanAction Action, PostScanActionConfig Config);
    internal record CompiledGroup(PostScanActionGroupConfig Config, List<CompiledAction> Actions);
}
