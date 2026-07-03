using ScanBridge.Models;
using ScanBridge.Services.PostScanActions;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Диспетчер пост-скан действий.
/// Выполняет подходящие сценарии для результата сканирования.
/// </summary>
public class ScanDispatcher
{
    private readonly IPostScanActionFactory _factory;
    private readonly ILogger<ScanDispatcher> _logger;
    private volatile IReadOnlyList<CompiledScenario> _scenarios = Array.Empty<CompiledScenario>();
    private ScenarioExecutor? _scenarioExecutor;

    public ScanDispatcher(IPostScanActionFactory factory, ILogger<ScanDispatcher> logger)
    {
        _factory = factory;
        _logger = logger;
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
    /// Выполняет все подходящие сценарии для результата сканирования.
    /// </summary>
    public async Task ExecuteAllAsync(ScanResult scan, CancellationToken ct)
    {
        var matchingScenarios = _scenarios
            .Where(s => MatchesScanner(s.Config, scan.ScannerName))
            .ToList();

        if (matchingScenarios.Count == 0) return;

        var tasks = new List<Task>();
        foreach (var scenario in matchingScenarios)
            tasks.Add(ExecuteScenarioAsync(scenario, scan, ct));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Количество активных сценариев.
    /// </summary>
    public int GetScenarioCount() => _scenarios.Count;

    private static bool MatchesScanner(ScenarioConfig scenarioConfig, string scannerName)
    {
        if (scenarioConfig.ScannerNames.Count == 0) return true;
        return scenarioConfig.ScannerNames.Contains(scannerName, StringComparer.Ordinal);
    }

    private async Task ExecuteScenarioAsync(CompiledScenario scenario, ScanResult scan, CancellationToken ct)
    {
        try
        {
            var context = new ScenarioContext { Scan = scan, CancellationToken = ct };
            await _scenarioExecutor!.ExecuteAsync(scenario, scan, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в сценарии «{Name}»", scenario.Config.Name);
        }
    }
}
