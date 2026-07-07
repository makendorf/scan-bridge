using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие вставки результата сканирования в активное окно.
/// Поддерживает два режима: через буфер обмена (Ctrl+V) и эмуляцию клавиатуры.
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
    private readonly string _mode;

    /// <summary>
    /// Создаёт экземпляр действия вставки в буфер обмена.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: AppendNewline, Mode (clipboard/keyboard).</param>
    public ClipboardPasteAction(ILogger<ClipboardPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;

        _mode = (settings.TryGetValue("Mode", out var mode) ? mode : "clipboard").ToLowerInvariant();
    }

    /// <summary>
    /// Вставляет результат сканирования в активное окно.
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

            if (_mode == "keyboard")
            {
                Win32Clipboard.SimulateTyping(text);
            }
            else
            {
                var prevWindow = Win32Clipboard.GetForegroundWindow();

                Win32Clipboard.SetClipboardText(text);
                await Task.Delay(50, ct);
                Win32Clipboard.SimulatePaste();
                await Task.Delay(50, ct);

                if (prevWindow != IntPtr.Zero)
                    Win32Clipboard.SetForegroundWindow(prevWindow);
            }

            _logger.LogInformation("Вставлено: {Data}", ControlCharDisplay.ForDisplay(text.TrimEnd()));
            scan.Metadata["pasteSuccess"] = "true";
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Вставка отменена: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = "cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вставки: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = ex.Message;
        }
    }
}
