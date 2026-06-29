using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие вставки результата сканирования в активное окно через буфер обмена.
/// Использует Win32 API для эмуляции нажатия Ctrl+V.
/// Работает только на Windows.
/// </summary>
public class ClipboardPasteAction : IPostScanAction
{
    /// <summary>
    /// Тип действия.
    /// </summary>
    public string Type => "ClipboardPaste";

    private readonly ILogger<ClipboardPasteAction> _logger;
    private readonly bool _appendNewline;

    /// <summary>
    /// Создаёт экземпляр действия вставки в буфер обмена.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: AppendNewline (добавлять перенос строки).</param>
    public ClipboardPasteAction(ILogger<ClipboardPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;
    }

    /// <summary>
    /// Вставляет результат сканирования в активное окно.
    /// Сохраняет предыдущее активное окно, вставляет текст и восстанавливает фокус.
    /// </summary>
    /// <param name="scan">Результат сканирования.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogDebug("Пропуск вставки: невалидный штрихкод {Raw}", scan.RawData);
            return;
        }

        try
        {
            var text = scan.ParsedData;

            if (_appendNewline)
                text += Environment.NewLine;

            var prevWindow = Win32Clipboard.GetForegroundWindow();

            Win32Clipboard.SetClipboardText(text);

            await Task.Delay(50, ct);

            Win32Clipboard.SimulatePaste();

            await Task.Delay(50, ct);

            if (prevWindow != IntPtr.Zero)
                Win32Clipboard.SetForegroundWindow(prevWindow);

            _logger.LogInformation("Вставлено: {Data}", ControlCharDisplay.ForDisplay(text.TrimEnd()));
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Вставка отменена: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вставки: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
        }
    }
}
