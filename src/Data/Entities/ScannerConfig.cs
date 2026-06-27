namespace ScanBridge.Data.Entities;

/// <summary>
/// Сущность конфигурации сканера в базе данных SQLite.
/// Хранит параметры serial-порта и настройки переподключения.
/// </summary>
public class ScannerConfig
{
    /// <summary>
    /// Уникальный идентификатор записи.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Уникальное имя сканера. Ограничение: до 100 символов.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Имя COM-порта. Ограничение: до 20 символов.
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// Скорость передачи данных в бодах.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Количество бит данных в байте.
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Тип контроля чётности. Ограничение: до 20 символов.
    /// </summary>
    public string Parity { get; set; } = "None";

    /// <summary>
    /// Количество стоп-битов. Ограничение: до 20 символов.
    /// </summary>
    public string StopBits { get; set; } = "One";

    /// <summary>
    /// Управление потоком. Ограничение: до 30 символов.
    /// </summary>
    public string Handshake { get; set; } = "RequestToSend";

    /// <summary>
    /// Таймаут чтения из порта в миллисекундах.
    /// </summary>
    public int ReadTimeout { get; set; } = 5000;

    /// <summary>
    /// Таймаут записи в порт в миллисекундах.
    /// </summary>
    public int WriteTimeout { get; set; } = 5000;

    /// <summary>
    /// Порядок сортировки сканера в интерфейсе.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Задержка между попытками переподключения в миллисекундах.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 1000;

    /// <summary>
    /// Максимальное количество попыток переподключения.
    /// </summary>
    public int ReconnectMaxRetries { get; set; } = 10;

    /// <summary>
    /// Режим непрерывного переподключения (бесконечные попытки).
    /// </summary>
    public bool ReconnectContinuous { get; set; }

    /// <summary>
    /// Режим обработки управляющих символов: 0 — удалять, 1 — оставлять (валидация пройдёт), 2 — оставлять (обычная валидация).
    /// </summary>
    public int ControlCharMode { get; set; }
}
