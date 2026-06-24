using System.IO.Ports;
using ScanBridge.Models;
using ScanBridge.Parsers;

namespace ScanBridge.Services;

public class SerialPortService : BackgroundService
{
    private readonly ILogger<SerialPortService> _logger;
    private readonly SerialPortConfig _config;
    private readonly IBarcodeParser _parser;
    private readonly ScanProcessorService _processor;
    private SerialPort? _serialPort;

    public SerialPortService(
        ILogger<SerialPortService> logger,
        SerialPortConfig config,
        IBarcodeParser parser,
        ScanProcessorService processor)
    {
        _logger = logger;
        _config = config;
        _parser = parser;
        _processor = processor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = 1000;
        const int maxRetryDelay = 30000;
        const int maxRetries = 10;
        var retryCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                OpenPort();
                retryDelay = 1000;
                retryCount = 0;
                _logger.LogInformation("[{Scanner}] Прослушивание порта {Port} ({Baud}/{DataBits}/{Parity}/{StopBits})",
                    _config.Name, _config.PortName, _config.BaudRate, _config.DataBits, _config.Parity, _config.StopBits);

                var buffer = new byte[1024];

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var bytesRead = _serialPort!.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var raw = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            var result = _parser.Parse(raw);
                            result.ScannerName = _config.Name;

                            _logger.LogInformation("[{Scanner}] Сканирование: Исходные={Raw}, Формат={Format}, Тип={ContentType}, Валидно={Valid}",
                                _config.Name, result.RawData, result.Format, result.ContentType, result.IsValid);

                            await _processor.ProcessAsync(result, stoppingToken);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested) break;
                retryCount++;
                _logger.LogError(ex, "[{Scanner}] Ошибка COM-порта (попытка {Retry}/{MaxRetries}), переподключение через {Delay} сек...",
                    _config.Name, retryCount, maxRetries, retryDelay / 1000);
                LogAvailablePorts();
                ClosePort();
                try { await Task.Delay(retryDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
                retryDelay = Math.Min(retryDelay * 2, maxRetryDelay);
                if (retryCount >= maxRetries)
                {
                    _logger.LogCritical("[{Scanner}] Превышен лимит попыток переподключения ({MaxRetries}). Остановка сканера.",
                        _config.Name, maxRetries);
                    break;
                }
            }
        }

        ClosePort();
    }

    private void OpenPort()
    {
        ClosePort();

        _serialPort = new SerialPort
        {
            PortName = _config.PortName,
            BaudRate = _config.BaudRate,
            DataBits = _config.DataBits,
            Parity = Enum.Parse<Parity>(_config.Parity),
            StopBits = Enum.Parse<StopBits>(_config.StopBits),
            Handshake = Enum.Parse<Handshake>(_config.Handshake),
            ReadTimeout = Math.Clamp(_config.ReadTimeout, 50, 5000),
            WriteTimeout = _config.WriteTimeout
        };

        _serialPort.Open();
    }

    private void ClosePort()
    {
        try
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }
                _serialPort.Dispose();
            }
        }
        catch { }
        finally
        {
            _serialPort = null;
        }
    }

    private void LogAvailablePorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
                _logger.LogWarning("[{Scanner}] COM-порты не найдены в системе", _config.Name);
            else
                _logger.LogInformation("[{Scanner}] Доступные COM-порты: {Ports}", _config.Name, string.Join(", ", ports));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Scanner}] Не удалось получить список COM-портов", _config.Name);
        }
    }

    public override void Dispose()
    {
        ClosePort();
        base.Dispose();
    }
}
