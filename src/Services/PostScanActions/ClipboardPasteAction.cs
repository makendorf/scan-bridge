using ScanBridge.IPC;
using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие вставки результата сканирования в активное окно.
/// Поддерживает два режима: через буфер обмена (Ctrl+V) и эмуляцию клавиатуры.
/// Работает только на Windows. При ошибке доступа — фоллбэк через tray-приложение.
/// </summary>
public class ClipboardPasteAction : IPostScanAction
{
    public string Type => "ClipboardPaste";

    private readonly ILogger<ClipboardPasteAction> _logger;
    private readonly bool _appendNewline;
    private readonly string _mode;

    public ClipboardPasteAction(ILogger<ClipboardPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;

        _mode = (settings.TryGetValue("Mode", out var mode) ? mode : "clipboard").ToLowerInvariant();
    }

    public async Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogDebug("Пропуск вставки: невалидный штрихкод {Raw}", scan.RawData);
            return;
        }

        var text = scan.ParsedData;
        if (_appendNewline)
            text += Environment.NewLine;

        // Попытка 1: прямой вызов (работает при запуске от имени пользователя)
        try
        {
            await ExecuteDirectAsync(text, ct);
            _logger.LogInformation("Вставлено: {Data}", ControlCharDisplay.ForDisplay(text.TrimEnd()));
            scan.Metadata["pasteSuccess"] = "true";
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Вставка отменена: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = "cancelled";
            return;
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            _logger.LogDebug(ex, "Прямая вставка не удалась (session 0?), попытка через tray-приложение");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вставки: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = ex.Message;
            return;
        }

        // Попытка 2: через tray-приложение (Named Pipe)
        try
        {
            await ExecuteViaTrayAsync(text, ct);
            _logger.LogInformation("Вставлено через tray-приложение: {Data}", ControlCharDisplay.ForDisplay(text.TrimEnd()));
            scan.Metadata["pasteSuccess"] = "true";
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Вставка отменена: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = "cancelled";
        }
        catch (Exception fallbackEx)
        {
            _logger.LogError(fallbackEx, "Ошибка вставки (прямая + fallback): {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
            scan.Metadata["pasteSuccess"] = "false";
            scan.Metadata["pasteError"] = fallbackEx.Message;
        }
    }

    private async Task ExecuteDirectAsync(string text, CancellationToken ct)
    {
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
    }

    private async Task ExecuteViaTrayAsync(string text, CancellationToken ct)
    {
        if (_mode == "keyboard")
        {
            var response = await IpcPipeClient.SimulateTypingAsync(text, ct);
            if (!response.Success)
                throw new InvalidOperationException(response.Error);
        }
        else
        {
            var prevWindow = Win32Clipboard.GetForegroundWindow();

            var clipResponse = await IpcPipeClient.SetClipboardTextAsync(text, ct);
            if (!clipResponse.Success)
                throw new InvalidOperationException(clipResponse.Error);

            await Task.Delay(50, ct);

            var pasteResponse = await IpcPipeClient.SimulatePasteAsync(ct);
            if (!pasteResponse.Success)
                throw new InvalidOperationException(pasteResponse.Error);

            await Task.Delay(50, ct);

            if (prevWindow != IntPtr.Zero)
            {
                var fgResponse = await IpcPipeClient.SetForegroundWindowAsync(prevWindow.ToInt64(), ct);
                if (!fgResponse.Success)
                    _logger.LogDebug("Не удалось восстановить фокус: {Error}", fgResponse.Error);
            }
        }
    }

    private static bool IsAccessError(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("Код ошибки Windows: 5")
            || msg.Contains("SendInput вернул 0")
            || msg.Contains("Неверный размер структуры INPUT")
            || (ex is InvalidOperationException && msg.Contains("SendInput"));
    }
}
