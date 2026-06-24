using System.IO.Ports;
using ScanBridge.Models;
using ScanBridge.Parsers;

namespace ScanBridge.Services;

public class ScannerManager : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ScannerManager> _logger;
    private readonly IBarcodeParser _parser;
    private readonly ScanProcessorService _processor;
    private readonly Dictionary<string, ScannerInstance> _instances = new();
    private readonly object _lock = new();

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

    public void StartAll(List<SerialPortConfig> scanners)
    {
        foreach (var scanner in scanners)
            StartScanner(scanner);
    }

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
                config, _parser, _processor);

            var task = Task.Run(() => service.StartAsync(cts.Token));
            _instances[config.Name] = new ScannerInstance(config, cts, service, task);

            _logger.LogInformation("[{Scanner}] Запущен на порту {Port}", config.Name, config.PortName);
            return conflict;
        }
    }

    public void StopScanner(string name)
    {
        lock (_lock)
        {
            StopScannerInternal(name);
        }
    }

    private void StopScannerInternal(string name)
    {
        if (!_instances.TryGetValue(name, out var instance)) return;

        _logger.LogInformation("[{Scanner}] Остановка...", name);

        instance.Cts.Cancel();

        try { instance.Service.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        catch { }

        try
        {
            if (!instance.Task.IsCompleted)
                instance.Task.Wait(TimeSpan.FromSeconds(3));
        }
        catch { }

        try { instance.Service.Dispose(); }
        catch { }

        try { instance.Cts.Dispose(); }
        catch { }

        _instances.Remove(name);
        Thread.Sleep(200);

        _logger.LogInformation("[{Scanner}] Остановлен", name);
    }

    private string? FindByPort(string portName, string exceptName)
    {
        foreach (var kv in _instances)
        {
            if (kv.Key != exceptName && kv.Value.Config.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        }
        return null;
    }

    public string? CheckPortConflict(string portName, string exceptName)
    {
        lock (_lock)
        {
            return FindByPort(portName, exceptName);
        }
    }

    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var name in _instances.Keys.ToList())
                StopScannerInternal(name);
        }
    }

    public void RestartAll(List<SerialPortConfig> scanners)
    {
        StopAll();
        StartAll(scanners);
    }

    public void RestartScanner(SerialPortConfig config)
    {
        StopScanner(config.Name);
        StartScanner(config);
    }

    public List<string> GetRunning() { lock (_lock) return [.. _instances.Keys]; }

    public void Dispose()
    {
        StopAll();
        GC.SuppressFinalize(this);
    }
}

internal record ScannerInstance(SerialPortConfig Config, CancellationTokenSource Cts, SerialPortService Service, Task Task);
