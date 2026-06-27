using System.Runtime.InteropServices;
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
    private readonly int _delayMs;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    /// <summary>
    /// Создаёт экземпляр действия вставки в буфер обмена.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: AppendNewline (добавлять перенос строки), DelayMs (задержка в мс).</param>
    public ClipboardPasteAction(ILogger<ClipboardPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;

        _delayMs = settings.TryGetValue("DelayMs", out var delayStr)
            && int.TryParse(delayStr, out var d) ? d : 50;
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

            var prevWindow = GetForegroundWindow();

            SetClipboardText(text);

            await Task.Delay(_delayMs, ct);

            SimulatePaste();

            await Task.Delay(_delayMs, ct);

            if (prevWindow != IntPtr.Zero)
                SetForegroundWindow(prevWindow);

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

    /// <summary>
    /// Устанавливает текст в буфер обмена Windows через Win32 API.
    /// </summary>
    /// <param name="text">Текст для копирования в буфер обмена.</param>
    private static void SetClipboardText(string text)
    {
        var opened = false;
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                opened = true;
                break;
            }
            Thread.Sleep(50);
        }

        if (!opened)
            throw new InvalidOperationException("Не удалось открыть буфер обмена после 10 попыток");

        try
        {
            EmptyClipboard();

            var hGlobal = Marshal.StringToHGlobalUni(text);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("Не удалось выделить память для буфера обмена");

            var result = SetClipboardData(CF_UNICODETEXT, hGlobal);

            if (result == IntPtr.Zero)
            {
                Marshal.FreeHGlobal(hGlobal);
                throw new InvalidOperationException("SetClipboardData вернул null");
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// Эмулирует нажатие Ctrl+V через Win32 API keybd_event.
    /// </summary>
    private static void SimulatePaste()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
