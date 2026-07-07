using NCrontab;
using ScanBridge.Models;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Сервис cron-расписаний для триггеров сценариев.
/// Проверяет расписание каждую минуту и запускает сценарии.
/// </summary>
public class ScheduleService : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceProvider _services;
    private readonly ILogger<ScheduleService> _logger;

    private readonly Dictionary<int, (CrontabSchedule Schedule, string Payload)> _schedules = new();

    public ScheduleService(IServiceProvider services, ILogger<ScheduleService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        ReloadSchedules();
        _timer = new Timer(CheckSchedules, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void ReloadSchedules()
    {
        _schedules.Clear();

        using var scope = _services.CreateScope();
        var scenarioService = scope.ServiceProvider.GetRequiredService<ScenarioService>();
        var scenarios = scenarioService.GetAll();

        foreach (var scenario in scenarios)
        {
            if (!scenario.Enabled) continue;
            if (scenario.TriggerType != TriggerType.Schedule) continue;
            if (scenario.TriggerSettings?.CronExpression == null) continue;

            try
            {
                var schedule = CrontabSchedule.Parse(scenario.TriggerSettings.CronExpression);
                var payload = scenario.TriggerSettings.SchedulePayload ?? "";
                _schedules[scenario.Id] = (schedule, payload);
                _logger.LogInformation("Расписание загружено: «{Name}» cron={Cron}", scenario.Name, scenario.TriggerSettings.CronExpression);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Неверное cron-выражение для сценария «{Name}»", scenario.Name);
            }
        }
    }

    private void CheckSchedules(object? state)
    {
        var now = DateTime.UtcNow;

        foreach (var (scenarioId, (sched, payload)) in _schedules)
        {
            try
            {
                var nextRun = sched.GetNextOccurrence(now.AddMinutes(-1));
                if (nextRun <= now && nextRun > now.AddMinutes(-1))
                {
                    _logger.LogDebug("Cron триггер: сценарий ID={Id}", scenarioId);
                    _ = ExecuteScheduleAsync(payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка cron проверки для сценария ID={Id}", scenarioId);
            }
        }
    }

    private async Task ExecuteScheduleAsync(string payload)
    {
        using var scope = _services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<TriggerDispatcher>();
        await dispatcher.DispatchScheduleTriggerAsync(payload, CancellationToken.None);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
