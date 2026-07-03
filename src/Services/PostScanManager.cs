using ScanBridge.Models;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Менеджер пост-скан действий.
/// Thin facade над ScanDispatcher.
/// </summary>
public class PostScanManager
{
    private readonly ScanDispatcher _dispatcher;

    public PostScanManager(ScanDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public virtual void ConfigureScenarios(List<ScenarioConfig> scenarioConfigs, ScenarioExecutor executor)
        => _dispatcher.ConfigureScenarios(scenarioConfigs, executor);

    public virtual void ReloadScenarios(ScenarioService scenarioService, ScenarioExecutor executor)
        => _dispatcher.ReloadScenarios(scenarioService, executor);

    public virtual Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
        => _dispatcher.ExecuteAllAsync(scan, ct);

    public int GetScenarioCount() => _dispatcher.GetScenarioCount();
}
