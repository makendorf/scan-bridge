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
    private readonly IServiceProvider _services;
    private readonly ILogger<ScannerManager> _logger;
    private readonly IBarcodeParser _parser;
    private readonly ScanProcessorService _processor;
    private readonly Dictionary<string, ScannerInstance> _instances = new();
    private readonly object _lock = new();

    /// <summary>
    /// Создаёт экземпляр менеджера сканеров.
    /// </summary>
    /// <param name="services">Провайдер зависимостей для создания сервисов портов.</param>
    /// <param name="logger">Логгер.</param>
    /// <param name="parser">Парсер штрихкодов.</param>
    /// <param name="processor">Сервис обработки результатов сканирования.</param>
    public ScannerManager(
        IServiceProvider services,
        ILogger<ScannerManager> logger,
        IBarcodeParser parser,
        ScanProcessorService processor)
    {
        _services = services;
        _logger = logger;
        _parser = parser;
        _processor = processor;
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
        lock (_lock)
        {
            var conflict = FindByPort(config.PortName, config.Name);
            if (conflict != null)
            {
                _logger.LogWarning("[{Scanner}] Порт {Port} используется сканером '{Conflict}', останавливаем его",
                    config.Name, config.PortName, conflict);
                StopScannerInternal(conflict);
            }

            StopScannerInternal(config.Name);

            var cts = new CancellationTokenSource();
            var service = new SerialPortService(
                _services.GetRequiredService<ILogger<SerialPortService>>(),
                config, _parser, _processor, config.Reconnect);

            var task = Task.Run(() => service.StartAsync(cts.Token));
            _instances[config.Name] = new ScannerInstance(config, cts, service, task);

            _logger.LogInformation("[{Scanner}] Запущен на порту {Port}", config.Name, config.PortName);
            return conflict;
        }
    }

    /// <summary>
    /// Останавливает сканер по имени.
    /// </summary>
    /// <param name="name">Имя сканера.</param>
    public void StopScanner(string name)
    {
        lock (_lock)
        {
            StopScannerInternal(name);
        }
    }

    /// <summary>
    /// Внутренний метод остановки сканера (без блокировки).
    /// Отменяет токен, останавливает сервис, освобождает ресурсы.
    /// </summary>
    /// <param name="name">Имя сканера.</param>
    private void StopScannerInternal(string name)
    {
        if (!_instances.TryGetValue(name, out var instance)) return;

        _logger.LogInformation("[{Scanner}] Остановка...", name);

        instance.Cts.Cancel();

        try { instance.Service.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при остановке сервиса", name); }

        try
        {
            if (!instance.Task.IsCompleted)
                instance.Task.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }

        try { instance.Service.Dispose(); }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при Dispose сервиса", name); }

        try { instance.Cts.Dispose(); }
        catch (Exception ex) { _logger.LogDebug(ex, "[{Scanner}] Ошибка при Dispose CTS", name); }

        _instances.Remove(name);

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

    /// <summary>
    /// Останавливает все запущенные сканеры.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var name in _instances.Keys.ToList())
                StopScannerInternal(name);
        }
    }

    /// <summary>
    /// Перезапускает все сканеры: останавливает текущие и запускает новые.
    /// </summary>
    /// <param name="scanners">Список конфигураций для запуска.</param>
    public void RestartAll(List<SerialPortConfig> scanners)
    {
        StopAll();
        StartAll(scanners);
    }

    /// <summary>
    /// Перезапускает один сканер.
    /// </summary>
    /// <param name="config">Конфигурация сканера.</param>
    public void RestartScanner(SerialPortConfig config)
    {
        StopScanner(config.Name);
        StartScanner(config);
    }

    /// <summary>
    /// Возвращает список имён запущенных сканеров.
    /// </summary>
    /// <returns>Список имён активных сканеров.</returns>
    public List<string> GetRunning() { lock (_lock) return [.. _instances.Keys]; }

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
internal record ScannerInstance(SerialPortConfig Config, CancellationTokenSource Cts, SerialPortService Service, Task Task);
