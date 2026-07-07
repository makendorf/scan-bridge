using System.IO.Ports;
using ScanBridge.Models;
using ScanBridge.Parsers;
using ScanBridge.Utils;

namespace ScanBridge.Services;

/// <summary>
/// Сервис работы с serial-портом для чтения данных от сканера штрихкодов.
/// Наследует BackgroundService для работы в фоновом режиме.
/// Реализует логику автоматического переподключения с экспоненциальной задержкой.
/// </summary>
public class SerialPortService : BackgroundService
{
    private readonly ILogger<SerialPortService> _logger;
    private readonly SerialPortConfig _config;
    private readonly IBarcodeParser _parser;
    private readonly ScanProcessorService _processor;
    private readonly ScanHistoryService _historyService;
    private readonly ReconnectConfig _reconnect;
    private SerialPort? _serialPort;

    /// <summary>
    /// Создаёт экземпляр сервиса serial-порта.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="config">Конфигурация serial-порта.</param>
    /// <param name="parser">Парсер штрихкодов.</param>
    /// <param name="processor">Сервис обработки результатов сканирования.</param>
    /// <param name="reconnect">Конфигурация переподключения (если null — используются значения по умолчанию).</param>
    public SerialPortService(
        ILogger<SerialPortService> logger,
        SerialPortConfig config,
        IBarcodeParser parser,
        ScanProcessorService processor,
        ScanHistoryService historyService,
        ReconnectConfig? reconnect = null)
    {
        _logger = logger;
        _config = config;
        _parser = parser;
        _processor = processor;
        _historyService = historyService;
        _reconnect = reconnect ?? new ReconnectConfig();
    }

    /// <summary>
    /// Основной цикл чтения данных из serial-порта.
    /// При ошибке переподключается с экспоненциальной задержкой.
    /// Останавливается при отмене токена или превышении лимита попыток.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены фоновой задачи.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = Math.Max(_reconnect.DelayMs, 100);
        var maxRetryDelay = Math.Max(retryDelay * 30, 30000);
        var maxRetries = _reconnect.Continuous ? int.MaxValue : _reconnect.MaxRetries;
        var retryCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                OpenPort();
                retryDelay = Math.Max(_reconnect.DelayMs, 100);
                retryCount = 0;
                _logger.LogInformation("[{Scanner}] Прослушивание порта {Port} ({Baud}/{DataBits}/{Parity}/{StopBits})",
                    _config.Name, _config.PortName, _config.BaudRate, _config.DataBits, _config.Parity, _config.StopBits);

                var buffer = new byte[1024];
                var stringBuffer = new System.Text.StringBuilder();

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var bytesRead = _serialPort!.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var chunk = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            stringBuffer.Append(chunk);

                            // Проверяем наличие символа-терминатора (\r, \n или \r\n)
                            var accumulated = stringBuffer.ToString();
                            var terminatorIndex = accumulated.IndexOfAny(new[] { '\r', '\n' });

                            if (terminatorIndex >= 0)
                            {
                                // Извлечь полную строку до терминатора
                                var rawData = accumulated.Substring(0, terminatorIndex).TrimEnd('\r', '\n');
                                stringBuffer.Clear();

                                // Убрать возможные лишние символы после терминатора
                                var remaining = accumulated.Substring(terminatorIndex + 1).TrimStart('\r', '\n');
                                if (remaining.Length > 0)
                                    stringBuffer.Append(remaining);

                                if (rawData.Length > 0)
                                {
                                    var result = _parser.Parse(rawData, _config.ControlCharMode);
                                    result.ScannerName = _config.Name;

                                    _logger.LogInformation("[{Scanner}] Сканирование: Исходные={Raw}, Формат={Format}, Тип={ContentType}, Валидно={Valid}",
                                        _config.Name, ControlCharDisplay.ForDisplay(result.RawData), result.Format, result.ContentType, result.IsValid);

                                    await _processor.ProcessAsync(result, stoppingToken);
                                }
                            }
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
                _historyService.RecordReconnect(_config.Name, ex.Message, retryCount);
                _logger.LogError(ex, "[{Scanner}] Ошибка COM-порта (попытка {Retry}), переподключение через {Delay} сек...",
                    _config.Name, retryCount, retryDelay / 1000);
                LogAvailablePorts();
                ClosePort();
                try { await Task.Delay(retryDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
                retryDelay = Math.Min(retryDelay * 2, maxRetryDelay);
                if (retryCount >= maxRetries)
                {
                    if (_reconnect.Continuous)
                    {
                        _logger.LogWarning("[{Scanner}] Непрерывное переподключение: попытка {Retry}, следующая через {Delay} сек...",
                            _config.Name, retryCount, retryDelay / 1000);
                    }
                    else
                    {
                        _logger.LogCritical("[{Scanner}] Превышен лимит попыток переподключения ({MaxRetries}). Остановка сканера.",
                            _config.Name, _reconnect.MaxRetries);
                        break;
                    }
                }
            }
        }

        ClosePort();
    }

    /// <summary>
    /// Открывает serial-порт с параметрами из конфигурации.
    /// Предварительно закрывает текущий порт, если он открыт.
    /// </summary>
    private void OpenPort()
    {
        ClosePort();

        if (!Enum.TryParse<Parity>(_config.Parity, true, out var parity))
            throw new ArgumentException($"Невалидное значение Parity: '{_config.Parity}'");
        if (!Enum.TryParse<StopBits>(_config.StopBits, true, out var stopBits))
            throw new ArgumentException($"Невалидное значение StopBits: '{_config.StopBits}'");
        if (!Enum.TryParse<Handshake>(_config.Handshake, true, out var handshake))
            throw new ArgumentException($"Невалидное значение Handshake: '{_config.Handshake}'");

        _serialPort = new SerialPort
        {
            PortName = _config.PortName,
            BaudRate = _config.BaudRate,
            DataBits = _config.DataBits,
            Parity = parity,
            StopBits = stopBits,
            Handshake = handshake,
            ReadTimeout = Math.Clamp(_config.ReadTimeout, 50, 5000),
            WriteTimeout = _config.WriteTimeout
        };

        _serialPort.Open();
    }

    /// <summary>
    /// Закрывает и освобождает serial-порт.
    /// Очищает входной и выходной буферы перед закрытием.
    /// </summary>
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[{Scanner}] Ошибка при закрытии порта", _config.Name);
        }
        finally
        {
            _serialPort = null;
        }
    }

    /// <summary>
    /// Логирует список доступных COM-портов для диагностики проблем подключения.
    /// </summary>
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

    /// <summary>
    /// Освобождает ресурсы: закрывает serial-порт.
    /// </summary>
    public override void Dispose()
    {
        ClosePort();
        base.Dispose();
    }
}
