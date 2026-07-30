using System.Text.Json;
using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Runtime engine для выполнения графовых сценариев.
/// </summary>
public class ScenarioExecutor
{
    private readonly IPostScanActionFactory _factory;
    private readonly ILogger<ScenarioExecutor> _logger;
    private readonly ConditionEvaluator _conditionEvaluator;

    private static readonly HashSet<string> StructuralTypes = new() { "Start", "Scanner", "HttpTrigger", "ScheduleTrigger", "FileTrigger", "FromScenario", "Condition", "End", "Fork", "While" };

    public ScenarioExecutor(IPostScanActionFactory factory, ILogger<ScenarioExecutor> logger, ConditionEvaluator conditionEvaluator)
    {
        _factory = factory;
        _logger = logger;
        _conditionEvaluator = conditionEvaluator;
    }

    /// <summary>
    /// Компилирует сценарий: проверяет граф, строит adjacency, проверяет на циклы.
    /// </summary>
    public CompiledScenario? Compile(ScenarioConfig scenario)
    {
        if (scenario.Nodes.Count == 0)
        {
            _logger.LogWarning("Сценарий «{Name}» не содержит узлов", scenario.Name);
            return null;
        }

        // Найти триггерные узлы (новый стиль) или Start узлы (legacy)
        var scannerNodeConfigs = scenario.Nodes.Where(n => n.Type == "Scanner").ToList();
        var httpTriggerConfigs = scenario.Nodes.Where(n => n.Type == "HttpTrigger").ToList();
        var scheduleTriggerConfigs = scenario.Nodes.Where(n => n.Type == "ScheduleTrigger").ToList();
        var fileTriggerConfigs = scenario.Nodes.Where(n => n.Type == "FileTrigger").ToList();
        var fromScenarioConfigs = scenario.Nodes.Where(n => n.Type == "FromScenario").ToList();
        var startNodeConfigs = scenario.Nodes.Where(n => n.Type == "Start").ToList();

        // Если есть триггерные узлы — используем их
        // Если только Start — мигрируем в Scanner с пустым именем (все сканеры)
        var allTriggers = scannerNodeConfigs.Concat(httpTriggerConfigs).Concat(scheduleTriggerConfigs).Concat(fileTriggerConfigs).Concat(fromScenarioConfigs).ToList();
        if (allTriggers.Count == 0 && startNodeConfigs.Count == 0)
        {
            _logger.LogWarning("Сценарий «{Name}» не содержит узлов-триггеров", scenario.Name);
            return null;
        }

        // Если только Start (legacy) — создаём виртуальные Scanner узлы
        if (scannerNodeConfigs.Count == 0 && fromScenarioConfigs.Count == 0)
        {
            scannerNodeConfigs = startNodeConfigs.Select(s => new ScenarioNodeConfig
            {
                NodeId = s.NodeId,
                Type = "Scanner",
                PositionX = s.PositionX,
                PositionY = s.PositionY,
                Settings = new Dictionary<string, string> { ["scannerName"] = "" }
            }).ToList();
        }

        // Создать скомпилированные узлы
        var compiledNodes = new Dictionary<string, CompiledNode>();
        foreach (var nodeConfig in scenario.Nodes)
        {
            IPostScanAction? action = null;
            var nodeType = nodeConfig.Type;

            // Определить ActionType для action-узлов
            var actionType = nodeConfig.ActionType
                ?? (nodeType != "Start" && !StructuralTypes.Contains(nodeType) ? nodeType : null);

            if (actionType != null)
            {
                var settings = nodeConfig.Settings ?? new Dictionary<string, string>();
                action = _factory.Create(actionType, settings);
                if (action == null)
                {
                    _logger.LogWarning("Неизвестный тип действия: {ActionType}", actionType);
                }
            }
            compiledNodes[nodeConfig.NodeId] = new CompiledNode(nodeConfig, action);
        }

        // Построить связи
        foreach (var connection in scenario.Connections)
        {
            if (compiledNodes.TryGetValue(connection.SourceNodeId, out var sourceNode) &&
                compiledNodes.TryGetValue(connection.TargetNodeId, out var targetNode))
            {
                sourceNode.NextNodes.Add(targetNode);

                if (!sourceNode.PortConnections.ContainsKey(connection.SourcePort))
                {
                    sourceNode.PortConnections[connection.SourcePort] = new List<CompiledNode>();
                }
                sourceNode.PortConnections[connection.SourcePort].Add(targetNode);
            }
        }

        // Проверить на циклы
        if (HasCycle(compiledNodes))
        {
            _logger.LogWarning("Сценарий «{Name}» содержит цикл", scenario.Name);
            return null;
        }

        // Собрать Scanner узлы с именами сканеров
        var scannerNodes = new List<(string ScannerName, CompiledNode Node)>();
        var triggerNodes = new List<(string TriggerType, string TriggerKey, CompiledNode Node)>();

        foreach (var scannerConfig in scannerNodeConfigs)
        {
            if (compiledNodes.TryGetValue(scannerConfig.NodeId, out var compiledScannerNode))
            {
                var scannerName = scannerConfig.Settings?.GetValueOrDefault("scannerName") ?? "";
                scannerNodes.Add((scannerName, compiledScannerNode));
                triggerNodes.Add(("Scanner", scannerName, compiledScannerNode));
            }
        }

        // Собрать FromScenario узлы
        var fromScenarioNodes = new List<(int ScenarioId, CompiledNode Node)>();
        foreach (var fsConfig in fromScenarioConfigs)
        {
            if (compiledNodes.TryGetValue(fsConfig.NodeId, out var compiledFsNode))
            {
                var callingScenarioId = scenario.Id;
                fromScenarioNodes.Add((callingScenarioId, compiledFsNode));
                triggerNodes.Add(("Scenario", callingScenarioId.ToString(), compiledFsNode));
            }
        }

        // Собрать другие триггерные узлы
        // Маппинг: имя узла графа → строковый тип триггера ScanResult
        var nodeTypeToTriggerType = new Dictionary<string, string>
        {
            ["HttpTrigger"] = "Http",
            ["ScheduleTrigger"] = "Schedule",
            ["FileTrigger"] = "FileWatcher"
        };
        foreach (var nodeConfig in scenario.Nodes.Where(n => n.Type is "HttpTrigger" or "ScheduleTrigger" or "FileTrigger"))
        {
            if (compiledNodes.TryGetValue(nodeConfig.NodeId, out var compiledNode))
            {
                var triggerType = nodeTypeToTriggerType[nodeConfig.Type];
                var triggerKey = nodeConfig.Settings?.GetValueOrDefault("routePath")
                    ?? nodeConfig.Settings?.GetValueOrDefault("cronExpression")
                    ?? nodeConfig.Settings?.GetValueOrDefault("watchPath")
                    ?? "";
                triggerNodes.Add((triggerType, triggerKey, compiledNode));
            }
        }

        var startNode = scannerNodes.Count > 0
            ? scannerNodes[0].Node
            : (fromScenarioNodes.Count > 0 ? fromScenarioNodes[0].Node : compiledNodes.Values.First());
        return new CompiledScenario(scenario, startNode, scannerNodes, triggerNodes, fromScenarioNodes, compiledNodes);
    }

    /// <summary>
    /// Выполняет скомпилированный сценарий для результата сканирования.
    /// Находит подходящие триггерные узлы и выполняет их.
    /// </summary>
    public async Task ExecuteAsync(CompiledScenario compiled, ScanResult scan, CancellationToken ct)
    {
        var context = new ScenarioContext
        {
            Scan = scan,
            CancellationToken = ct
        };

        await ExecuteCoreAsync(compiled, context);
    }

    /// <summary>
    /// Выполняет сценарий и возвращает результат из узла End.
    /// Используется для вызова сценариев из других сценариев (ToScenario).
    /// </summary>
    public async Task<ScanResult?> ExecuteWithResultAsync(CompiledScenario compiled, ScanResult scan, CancellationToken ct, int callDepth = 0)
    {
        if (callDepth > 10)
        {
            _logger.LogWarning("Превышена максимальная глубина вложенности сценариев (10). Сценарий: «{Name}»", compiled.Config.Name);
            scan.Metadata["scenario_error"] = "max_depth_exceeded";
            return null;
        }

        var context = new ScenarioContext
        {
            Scan = scan,
            CancellationToken = ct,
            CallDepth = callDepth
        };

        var result = await ExecuteCoreAsync(compiled, context);
        return result;
    }

    private async Task<ScanResult?> ExecuteCoreAsync(CompiledScenario compiled, ScenarioContext context)
    {
        var triggerType = context.Scan.TriggerType;
        var triggerKey = triggerType switch
        {
            "Scanner" => context.Scan.ScannerName,
            "Http" => context.Scan.TriggerSource,
            "Schedule" => context.Scan.TriggerSource,
            "FileWatcher" => context.Scan.TriggerSource,
            "Scenario" => context.Scan.TriggerSource,
            _ => context.Scan.ScannerName
        };

        // Найти триггерные узлы, совпадающие с типом и ключом
        var matchingEntries = compiled.TriggerNodes
            .Where(t => MatchesTriggerNode(t, triggerType, triggerKey))
            .ToList();

        // Fallback: legacy Start/Scanner узлы
        if (matchingEntries.Count == 0)
        {
            matchingEntries = compiled.ScannerNodes
                .Where(s => string.IsNullOrEmpty(s.ScannerName) ||
                            string.Equals(s.ScannerName, context.Scan.ScannerName, StringComparison.Ordinal))
                .Select(s => ("Scanner", s.ScannerName, s.Node))
                .ToList();

            if (matchingEntries.Count == 0 && compiled.ScannerNodes.All(s => string.IsNullOrEmpty(s.ScannerName)))
            {
                matchingEntries = compiled.ScannerNodes
                    .Select(s => ("Scanner", s.ScannerName, s.Node))
                    .ToList();
            }
        }

        ScanResult? endResult = null;
        foreach (var (_, _, node) in matchingEntries)
        {
            var nodeResult = await ExecuteNodeAsync(node, context);
            if (nodeResult != null)
                endResult = nodeResult;
        }

        return endResult;
    }

    private static bool MatchesTriggerNode((string TriggerType, string TriggerKey, CompiledNode Node) entry, string triggerType, string triggerKey)
    {
        if (entry.TriggerType != triggerType) return false;

        // Scanner: match by scanner name
        if (triggerType == "Scanner")
        {
            return string.IsNullOrEmpty(entry.TriggerKey) ||
                   string.Equals(entry.TriggerKey, triggerKey, StringComparison.Ordinal);
        }

        // Scenario: match by scenario ID
        if (triggerType == "Scenario")
        {
            return string.Equals(entry.TriggerKey, triggerKey, StringComparison.Ordinal);
        }

        // HTTP: match by route path
        if (triggerType == "Http")
        {
            return string.Equals(entry.TriggerKey, triggerKey, StringComparison.OrdinalIgnoreCase);
        }

        // Schedule и File: все узлы этого типа совпадают
        return true;
    }

    private async Task<ScanResult?> ExecuteNodeAsync(CompiledNode node, ScenarioContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            switch (node.Config.Type)
            {
                case "Start":
                case "Scanner":
                case "HttpTrigger":
                case "ScheduleTrigger":
                case "FileTrigger":
                case "FromScenario":
                    // Pass-through: триггерные узлы — точки входа, ничего не делаем
                    break;

                case "Condition":
                    var settings = node.Config.Settings ?? new Dictionary<string, string>();
                    var result = _conditionEvaluator.Evaluate(settings, context.Scan);
                    context.Variables["lastCondition"] = result;
                    break;

                case "While":
                    var whileSettings = node.Config.Settings ?? new Dictionary<string, string>();
                    var conditionsJson = whileSettings.GetValueOrDefault("conditions", "[]");
                    var logic = whileSettings.GetValueOrDefault("logic", "and");
                    var whileResult = _conditionEvaluator.EvaluateMultiple(conditionsJson, context.Scan, logic);
                    context.Variables["lastCondition"] = whileResult;
                    break;

                case "End":
                    return context.Scan;

                default:
                    // Все action типы: Log, Replacement, ClipboardPaste и т.д.
                    if (node.Action != null)
                    {
                        await node.Action.ExecuteAsync(context.Scan, context.CancellationToken);
                    }
                    break;
            }

            // Определить следующие узлы
            List<CompiledNode> nextNodes;
            if (node.Config.Type == "Condition" || node.Config.Type == "While")
            {
                var conditionResult = context.Variables.TryGetValue("lastCondition", out var val) && val is bool b && b;
                var portName = node.Config.Type == "While"
                    ? (conditionResult ? "output_1" : "output_2")
                    : (conditionResult ? "output_1" : "output_2");

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

            // Выполнить следующие узлы
            ScanResult? endResult = null;
            foreach (var next in nextNodes)
            {
                var nextResult = await ExecuteNodeAsync(next, context);
                if (nextResult != null)
                    endResult = nextResult;
            }

            // Для While: если условие истинно, вернуться к телу цикла
            if (node.Config.Type == "While")
            {
                var whileCondition = context.Variables.TryGetValue("lastCondition", out var wv) && wv is bool wb && wb;
                if (whileCondition)
                {
                    if (node.PortConnections.TryGetValue("output_1", out var loopNodes) && loopNodes.Count > 0)
                    {
                        var whileKey = $"while_{node.Config.NodeId}";
                        var iterations = context.Variables.TryGetValue(whileKey, out var iv) && iv is int intVal ? intVal : 0;
                        if (iterations < 100)
                        {
                            context.Variables[whileKey] = iterations + 1;
                            foreach (var loopNode in loopNodes)
                            {
                                var loopResult = await ExecuteNodeAsync(loopNode, context);
                                if (loopResult != null)
                                    endResult = loopResult;
                            }
                        }
                    }
                }
            }

            return endResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в узле {NodeId} ({Type})", node.Config.NodeId, node.Config.Type);
            ScanResult? endResult = null;
            foreach (var next in node.NextNodes)
            {
                var nextResult = await ExecuteNodeAsync(next, context);
                if (nextResult != null)
                    endResult = nextResult;
            }
            return endResult;
        }
    }

    private static bool HasCycle(Dictionary<string, CompiledNode> nodes)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var nodeId in nodes.Keys)
        {
            if (HasCycleUtil(nodeId, nodes, visited, recursionStack))
                return true;
        }

        return false;
    }

    private static bool HasCycleUtil(string nodeId, Dictionary<string, CompiledNode> nodes,
        HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
            return true;

        if (visited.Contains(nodeId))
            return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (nodes.TryGetValue(nodeId, out var node))
        {
            foreach (var next in node.NextNodes)
            {
                if (HasCycleUtil(next.Config.NodeId, nodes, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}

/// <summary>
/// Контекст выполнения сценария.
/// </summary>
public class ScenarioContext
{
    public ScanResult Scan { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public CancellationToken CancellationToken { get; set; }
    public int CallDepth { get; set; }
}
