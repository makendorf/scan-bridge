namespace ScanBridge.Models;

/// <summary>
/// Конфигурация.serial-порта для подключения сканера.
/// Определяет параметры COM-порта и логику переподключения.
/// </summary>
public class SerialPortConfig
{
    /// <summary>
    /// Уникальное имя сканера (например, "Сканер на кассе 1").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Имя COM-порта (например, "COM2", "COM10").
    /// </summary>
    public string PortName { get; set; } = "COM2";

    /// <summary>
    /// Скорость передачи данных в бодах (по умолчанию 9600).
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Количество бит данных в байте (по умолчанию 8).
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Тип контроля чётности: None, Odd, Even, Mark, Space.
    /// </summary>
    public string Parity { get; set; } = "None";

    /// <summary>
    /// Количество стоп-битов: One, Two, OnePointFive.
    /// </summary>
    public string StopBits { get; set; } = "One";

    /// <summary>
    /// Управление потоком: None, XOnXOff, RequestToSend, RequestToSendXOnXOff.
    /// </summary>
    public string Handshake { get; set; } = "RequestToSend";

    /// <summary>
    /// Таймаут чтения из порта в миллисекундах (ограничен диапазоном 50-5000).
    /// </summary>
    public int ReadTimeout { get; set; } = 5000;

    /// <summary>
    /// Таймаут записи в порт в миллисекундах.
    /// </summary>
    public int WriteTimeout { get; set; } = 5000;

    /// <summary>
    /// Конфигурация автоматического переподключения при разрыве связи.
    /// </summary>
    public ReconnectConfig Reconnect { get; set; } = new();

    /// <summary>
    /// Режим обработки управляющих символов: 0 — удалять, 1 — оставлять (валидация пройдёт), 2 — оставлять (обычная валидация).
    /// </summary>
    public int ControlCharMode { get; set; }
}
