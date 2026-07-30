using Microsoft.Extensions.DependencyInjection;
using ScanBridge.Models;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие «В сценарий» — вызывает другой сценарий, передавая текущие данные и метаданные.
/// Результат выполнения дочернего сценария (из узла End) записывается в метаданные текущего скана.
/// </summary>
public class ToScenarioAction : IPostScanAction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _targetScenarioId;

    public string Type => "ToScenario";

    public ToScenarioAction(ILogger<ToScenarioAction> logger, Dictionary<string, string> settings, IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory!;

        if (settings.TryGetValue("TargetScenarioId", out var idStr) && int.TryParse(idStr, out var id))
            _targetScenarioId = id;
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (_targetScenarioId <= 0)
        {
            scan.Metadata["scenario_error"] = "invalid_target_id";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<ScenarioExecutor>();
        var scenarioService = scope.ServiceProvider.GetRequiredService<ScenarioService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ToScenarioAction>>();

        var config = scenarioService.GetById(_targetScenarioId);
        if (config == null || !config.Enabled)
        {
            logger.LogWarning("Целевой сценарий (ID={Id}) не найден или отключён", _targetScenarioId);
            scan.Metadata["scenario_error"] = "target_not_found";
            return;
        }

        var compiled = executor.Compile(config);
        if (compiled == null)
        {
            logger.LogWarning("Не удалось скомпилировать сценарий «{Name}» (ID={Id})", config.Name, _targetScenarioId);
            scan.Metadata["scenario_error"] = "compile_failed";
            return;
        }

        // Клонируем скан для дочернего сценария и устанавливаем триггер "Scenario"
        var childScan = CloneScan(scan);
        childScan.TriggerType = "Scenario";
        childScan.TriggerSource = _targetScenarioId.ToString();

        var result = await executor.ExecuteWithResultAsync(compiled, childScan, ct, callDepth: 1);

        if (result != null)
        {
            // Копируем результат из End-узла в метаданные текущего скана
            scan.Metadata["scenario_result_data"] = result.ParsedData;
            scan.Metadata["scenario_result_raw"] = result.RawData;
            scan.Metadata["scenario_result_format"] = result.Format;
            scan.Metadata["scenario_result_valid"] = result.IsValid.ToString();
            scan.Metadata["scenario_result_timestamp"] = result.Timestamp.ToString("O");

            // Копируем все метаданные дочернего сценария с префиксом scenario_
            foreach (var kv in result.Metadata)
            {
                if (!kv.Key.StartsWith("scenario_"))
                    scan.Metadata[$"scenario_{kv.Key}"] = kv.Value;
            }
        }
    }

    private static ScanResult CloneScan(ScanResult scan) => new()
    {
        RawData = scan.RawData,
        ParsedData = scan.ParsedData,
        Format = scan.Format,
        IsValid = scan.IsValid,
        ScannerName = scan.ScannerName,
        Timestamp = scan.Timestamp,
        ContentType = scan.ContentType,
        ParsedContent = scan.ParsedContent,
        Metadata = new Dictionary<string, string>(scan.Metadata),
        TriggerSource = scan.TriggerSource,
        TriggerType = scan.TriggerType
    };
}
