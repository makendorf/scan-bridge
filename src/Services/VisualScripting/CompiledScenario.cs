using ScanBridge.Models;

namespace ScanBridge.Services.VisualScripting;

/// <summary>
/// Скомпилированный сценарий (предкомпилированная графовая структура).
/// </summary>
public class CompiledScenario
{
    public ScenarioConfig Config { get; }
    public CompiledNode StartNode { get; }
    public List<(string ScannerName, CompiledNode Node)> ScannerNodes { get; }
    public List<(string TriggerType, string TriggerKey, CompiledNode Node)> TriggerNodes { get; }
    public List<(int ScenarioId, CompiledNode Node)> FromScenarioNodes { get; }
    public Dictionary<string, CompiledNode> Nodes { get; }

    public CompiledScenario(ScenarioConfig config, CompiledNode startNode,
        List<(string ScannerName, CompiledNode Node)> scannerNodes,
        List<(string TriggerType, string TriggerKey, CompiledNode Node)> triggerNodes,
        List<(int ScenarioId, CompiledNode Node)> fromScenarioNodes,
        Dictionary<string, CompiledNode> nodes)
    {
        Config = config;
        StartNode = startNode;
        ScannerNodes = scannerNodes;
        TriggerNodes = triggerNodes;
        FromScenarioNodes = fromScenarioNodes;
        Nodes = nodes;
    }
}

/// <summary>
/// Скомпилированный узел с предсозданным экшеном.
/// </summary>
public class CompiledNode
{
    public ScenarioNodeConfig Config { get; }
    public IPostScanAction? Action { get; }
    public List<CompiledNode> NextNodes { get; } = new();
    public Dictionary<string, List<CompiledNode>> PortConnections { get; } = new();

    public CompiledNode(ScenarioNodeConfig config, IPostScanAction? action)
    {
        Config = config;
        Action = action;
    }
}
