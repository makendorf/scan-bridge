using ScanBridge.Models;
using ScanBridge.Services.VisualScripting;

namespace ScanBridge.Services;

/// <summary>
/// Сервис файловых watcher'ов для триггеров сценариев.
/// Следит за изменениями файлов и запускает сценарии.
/// </summary>
public class FileWatcherService : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();

    public FileWatcherService(IServiceProvider services, ILogger<FileWatcherService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        ReloadWatchers();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        return Task.CompletedTask;
    }

    public void ReloadWatchers()
    {
        // Остановить существующие watcher'ы
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        using var scope = _services.CreateScope();
        var scenarioService = scope.ServiceProvider.GetRequiredService<ScenarioService>();
        var scenarios = scenarioService.GetAll();

        foreach (var scenario in scenarios)
        {
            if (scenario.TriggerType != TriggerType.FileWatcher) continue;
            if (string.IsNullOrWhiteSpace(scenario.TriggerSettings?.WatchPath)) continue;

            try
            {
                var watchPath = scenario.TriggerSettings.WatchPath;
                var filter = scenario.TriggerSettings.WatchFilter ?? "*.*";
                var changeTypesStr = scenario.TriggerSettings.WatchChangeTypes ?? "Created,Changed";
                var changeTypes = Enum.Parse<NotifyFilters>(changeTypesStr, true);

                var dir = Directory.Exists(watchPath) ? watchPath : Path.GetDirectoryName(watchPath) ?? watchPath;
                var fileFilter = Directory.Exists(watchPath) ? filter : Path.GetFileName(watchPath);

                var watcher = new FileSystemWatcher(dir, fileFilter)
                {
                    NotifyFilter = changeTypes,
                    EnableRaisingEvents = true
                };

                var scenarioId = scenario.Id;
                watcher.Created += (sender, e) => OnFileChanged(scenarioId, e.FullPath);
                watcher.Changed += (sender, e) => OnFileChanged(scenarioId, e.FullPath);

                _watchers.Add(watcher);
                _logger.LogInformation("FileWatcher: «{Name}» следит за {Path}/{Filter}", scenario.Name, dir, fileFilter);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось создать FileWatcher для «{Name}»", scenario.Name);
            }
        }
    }

    private async void OnFileChanged(int scenarioId, string filePath)
    {
        try
        {
            // Небольшая задержка чтобы файл успел записаться
            await Task.Delay(500);

            if (!File.Exists(filePath)) return;

            var content = await File.ReadAllTextAsync(filePath);

            using var scope = _services.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<TriggerDispatcher>();
            await dispatcher.DispatchFileTriggerAsync(filePath, content, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки изменения файла {Path}", filePath);
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }
}
