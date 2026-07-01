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

    public ScenarioExecutor(IPostScanActionFactory factory, ILogger<ScenarioExecutor> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Компилирует сценарий: проверяет граф, строит adjacency, проверяет на циклы.
    /// </summary>
    /// <param name="scenario">Конфигурация сценария.</param>
    /// <returns>Скомпилированный сценарий или null при ошибке.</returns>
    public CompiledScenario? Compile(ScenarioConfig scenario)
    {
        if (scenario.Nodes.Count == 0)
        {
            _logger.LogWarning("Сценарий «{Name}» не содержит узлов", scenario.Name);
            return null;
        }

        // Найти Start узел
        var startNodeConfig = scenario.Nodes.FirstOrDefault(n => n.Type == "Start");
        if (startNodeConfig == null)
        {
            _logger.LogWarning("Сценарий «{Name}» не содержит Start узел", scenario.Name);
            return null;
        }

        // Создать скомпилированные узлы
        var compiledNodes = new Dictionary<string, CompiledNode>();
        foreach (var nodeConfig in scenario.Nodes)
        {
            IPostScanAction? action = null;
            var nodeType = nodeConfig.Type;

            // Action nodes: type is either "Action" with ActionType, or the action type directly (e.g. "Log")
            var actionType = nodeConfig.ActionType ?? (nodeType != "Start" && nodeType != "Condition" && nodeType != "End" && nodeType != "Fork" && nodeType != "While" ? nodeType : null);

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

        var startNode = compiledNodes[startNodeConfig.NodeId];
        return new CompiledScenario(scenario, startNode, compiledNodes);
    }

    /// <summary>
    /// Выполняет скомпилированный сценарий.
    /// </summary>
    /// <param name="compiled">Скомпилированный сценарий.</param>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task ExecuteAsync(CompiledScenario compiled, ScanResult scan, CancellationToken ct)
    {
        var context = new ScenarioContext
        {
            Scan = scan,
            CancellationToken = ct
        };

        await ExecuteNodeAsync(compiled.StartNode, context);
    }

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

                case "While":
                    var whileSettings = node.Config.Settings ?? new Dictionary<string, string>();
                    var conditionsJson = whileSettings.GetValueOrDefault("conditions", "[]");
                    var whileResult = ConditionEvaluator.EvaluateMultiple(conditionsJson, context.Scan);
                    context.Variables["lastCondition"] = whileResult;
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
                    // Найти выход output_1 (тело цикла) и выполнить его снова
                    if (node.PortConnections.TryGetValue("output_1", out var loopNodes) && loopNodes.Count > 0)
                    {
                        // Защита от бесконечного цикла — максимум 100 итераций
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
            // Продолжаем выполнение с другими узлами
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
