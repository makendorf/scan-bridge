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

    private static readonly HashSet<string> StructuralTypes = new() { "Start", "Scanner", "Condition", "End", "Fork", "While" };

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

        // Найти Scanner узлы (новый стиль) или Start узлы (legacy)
        var scannerNodeConfigs = scenario.Nodes.Where(n => n.Type == "Scanner").ToList();
        var startNodeConfigs = scenario.Nodes.Where(n => n.Type == "Start").ToList();

        // Если есть Scanner узлы — используем их
        // Если только Start — мигрируем в Scanner с пустым именем (все сканеры)
        if (scannerNodeConfigs.Count == 0 && startNodeConfigs.Count == 0)
        {
            _logger.LogWarning("Сценарий «{Name}» не содержит Start/Scanner узел", scenario.Name);
            return null;
        }

        // Если только Start (legacy) — создаём виртуальные Scanner узлы
        if (scannerNodeConfigs.Count == 0)
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
        foreach (var scannerConfig in scannerNodeConfigs)
        {
            if (compiledNodes.TryGetValue(scannerConfig.NodeId, out var compiledScannerNode))
            {
                var scannerName = scannerConfig.Settings?.GetValueOrDefault("scannerName") ?? "";
                scannerNodes.Add((scannerName, compiledScannerNode));
            }
        }

        var startNode = scannerNodes.Count > 0 ? scannerNodes[0].Node : compiledNodes.Values.First();
        return new CompiledScenario(scenario, startNode, scannerNodes, compiledNodes);
    }

    /// <summary>
    /// Выполняет скомпилированный сценарий для результата сканирования.
    /// Находит Scanner узлы, совпадающие с именем сканера, и выполняет их.
    /// </summary>
    public async Task ExecuteAsync(CompiledScenario compiled, ScanResult scan, CancellationToken ct)
    {
        var context = new ScenarioContext
        {
            Scan = scan,
            CancellationToken = ct
        };

        // Найти Scanner узлы, совпадающие с именем сканера
        var matchingEntries = compiled.ScannerNodes
            .Where(s => string.IsNullOrEmpty(s.ScannerName) ||
                        string.Equals(s.ScannerName, scan.ScannerName, StringComparison.Ordinal))
            .ToList();

        // Fallback: если есть только legacy Start узлы (все с пустым именем) — выполняем все
        if (matchingEntries.Count == 0 && compiled.ScannerNodes.All(s => string.IsNullOrEmpty(s.ScannerName)))
        {
            matchingEntries = compiled.ScannerNodes;
        }

        foreach (var (_, node) in matchingEntries)
        {
            await ExecuteNodeAsync(node, context);
        }
    }

    private async Task ExecuteNodeAsync(CompiledNode node, ScenarioContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            switch (node.Config.Type)
            {
                case "Start":
                case "Scanner":
                    // Pass-through: Scanner — точка входа, ничего не делаем
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
                    return;

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
            foreach (var next in nextNodes)
            {
                await ExecuteNodeAsync(next, context);
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
                                await ExecuteNodeAsync(loopNode, context);
                            }
                        }
                    }
                }
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
}
