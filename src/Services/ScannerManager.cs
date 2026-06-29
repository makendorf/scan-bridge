using System.IO.Ports;
using Microsoft.EntityFrameworkCore;
using ScanBridge.Data;
using ScanBridge.Models;
using ScanBridge.Parsers;

namespace ScanBridge.Services;

/// <summary>
/// Менеджер сканеров — управляет жизненным циклом всех подключённых сканеров.
/// Отвечает за запуск, остановку, перезапуск и разрешение конфликтов портов.
/// Потокобезопасен через внутренний объект блокировки.
/// </summary>
public class ScannerManager : IDisposable
{
    private readonly Func<SerialPortConfig, ReconnectConfig?, SerialPortService> _serviceFactory;
    private readonly ILogger<ScannerManager> _logger;
    private readonly Dictionary<string, ScannerInstance> _instances = new();
    private readonly object _lock = new();

    /// <summary>
    /// Создаёт экземпляр менеджера сканеров.
    /// </summary>
    /// <param name="serviceFactory">Фабрика для создания экземпляров SerialPortService.</param>
    /// <param name="logger">Логгер.</param>
    public ScannerManager(
        Func<SerialPortConfig, ReconnectConfig?, SerialPortService> serviceFactory,
        ILogger<ScannerManager> logger)
    {
        _serviceFactory = serviceFactory;
        _logger = logger;
    }

    /// <summary>
    /// Запускает все сканеры из списка конфигураций.
    /// </summary>
    /// <param name="scanners">Список конфигураций сканеров для запуска.</param>
    public void StartAll(List<SerialPortConfig> scanners)
    {
        foreach (var scanner in scanners)
            StartScanner(scanner);
    }

    /// <summary>
    /// Запускает один сканер. Если порт уже занят другим сканером — останавливает его.
    /// </summary>
    /// <param name="config">Конфигурация сканера.</param>
    /// <returns>Имя конфликтующего сканера или null, если конфликтов нет.</returns>
    public string? StartScanner(SerialPortConfig config)
    {
        return StartScannerAsync(config).GetAwaiter().GetResult();
    }

    public async Task<string?> StartScannerAsync(SerialPortConfig config)
    {
        List<ScannerInstance> toStop;
        string? conflict;
        lock (_lock)
        {
            toStop = new List<ScannerInstance>();
            conflict = FindByPort(config.PortName, config.Name);
            if (conflict != null)
            {
                _logger.LogWarning("[{Scanner}] Порт {Port} используется сканером '{Conflict}', останавливаем его",
                    config.Name, config.PortName, conflict);
                CollectInstanceToStop(conflict, toStop);
                _instances.Remove(conflict);
            }

            CollectInstanceToStop(config.Name, toStop);
            _instances.Remove(config.Name);

            var cts = new CancellationTokenSource();
            var service = _serviceFactory(config, config.Reconnect);

            var task = Task.Run(() => service.StartAsync(cts.Token));
            _instances[config.Name] = new ScannerInstance(config, cts, service, task, DateTime.UtcNow);

            _logger.LogInformation("[{Scanner}] Запущен на порту {Port}", config.Name, config.PortName);
        }

        foreach (var instance in toStop)
            await StopInstanceAsync(instance).ConfigureAwait(false);

        return conflict;
    }

    /// <summary>
    /// Останавливает сканер по имени.
    /// </summary>
    /// <param name="name">Имя сканера.</param>
    public void StopScanner(string name)
    {
        StopScannerAsync(name).GetAwaiter().GetResult();
    }

    public async Task StopScannerAsync(string name)
    {
        ScannerInstance? instance;
        lock (_lock)
        {
            if (!_instances.Remove(name, out instance!))
                return;
        }
        await StopInstanceAsync(instance).ConfigureAwait(false);
    }

    private void CollectInstanceToStop(string name, List<ScannerInstance> list)
    {
        if (_instances.TryGetValue(name, out var instance))
            list.Add(instance);
    }

    private async Task StopInstanceAsync(ScannerInstance instance)
    {
        var name = instance.Config.Name;
        _logger.LogInformation("[{Scanner}] Остановка...", name);

        instance.Cts.Cancel();

        try { await instance.Service.StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при остановке сервиса", name); }

        try
        {
            if (!instance.Task.IsCompleted)
                await instance.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { }

        try { instance.Service.Dispose(); }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при Dispose сервиса", name); }

        try { instance.Cts.Dispose(); }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при Dispose CTS", name); }

        _logger.LogInformation("[{Scanner}] Остановлен", name);
    }

    /// <summary>
    /// Ищет сканер, использующий указанный порт (за исключением указанного сканера).
    /// </summary>
    /// <param name="portName">Имя порта для поиска.</param>
    /// <param name="exceptName">Имя сканера-исключения.</param>
    /// <returns>Имя найденного сканера или null.</returns>
    private string? FindByPort(string portName, string exceptName)
    {
        foreach (var kv in _instances)
        {
            if (kv.Key != exceptName && kv.Value.Config.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        }
        return null;
    }

    /// <summary>
    /// Проверяет, используется ли указанный порт другим сканером.
    /// </summary>
    /// <param name="portName">Имя порта.</param>
    /// <param name="exceptName">Имя сканера-исключения.</param>
    /// <returns>Имя конфликтующего сканера или null.</returns>
    public string? CheckPortConflict(string portName, string exceptName)
    {
        lock (_lock)
        {
            return FindByPort(portName, exceptName);
        }
    }

    public void StopAll()
    {
        StopAllAsync().GetAwaiter().GetResult();
    }

    public async Task StopAllAsync()
    {
        List<ScannerInstance> toStop;
        lock (_lock)
        {
            toStop = new List<ScannerInstance>(_instances.Values);
            _instances.Clear();
        }

        foreach (var instance in toStop)
            await StopInstanceAsync(instance).ConfigureAwait(false);
    }

    public void RestartAll(List<SerialPortConfig> scanners)
    {
        RestartAllAsync(scanners).GetAwaiter().GetResult();
    }

    public async Task RestartAllAsync(List<SerialPortConfig> scanners)
    {
        await StopAllAsync().ConfigureAwait(false);
        StartAll(scanners);
    }

    public void RestartScanner(SerialPortConfig config)
    {
        RestartScannerAsync(config).GetAwaiter().GetResult();
    }

    public async Task RestartScannerAsync(SerialPortConfig config)
    {
        await StopScannerAsync(config.Name).ConfigureAwait(false);
        await StartScannerAsync(config).ConfigureAwait(false);
    }

    /// <summary>
    /// Возвращает список имён запущенных сканеров.
    /// </summary>
    /// <returns>Список имён активных сканеров.</returns>
    public List<string> GetRunning() { lock (_lock) return [.. _instances.Keys]; }

    /// <summary>
    /// Возвращает время работы каждого запущенного сканера.
    /// </summary>
    /// <returns>Словарь: имя сканера → время работы.</returns>
    public Dictionary<string, TimeSpan> GetUptime()
    {
        lock (_lock)
        {
            return _instances.ToDictionary(kv => kv.Key, kv => DateTime.UtcNow - kv.Value.StartedAt);
        }
    }

    /// <summary>
    /// Останавливает все сканеры и освобождает ресурсы.
    /// </summary>
    public void Dispose()
    {
        StopAll();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Внутренняя запись, хранящая экземпляр сканера с его конфигурацией и управляющими объектами.
/// </summary>
internal record ScannerInstance(SerialPortConfig Config, CancellationTokenSource Cts, SerialPortService Service, Task Task, DateTime StartedAt);
