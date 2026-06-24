using System.Runtime.InteropServices;
using ScanBridge.Models;

namespace ScanBridge.Services.PostScanActions;

public class ClipboardPasteAction : IPostScanAction
{
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

    public ClipboardPasteAction(ILogger<ClipboardPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;

        _delayMs = settings.TryGetValue("DelayMs", out var delayStr)
            && int.TryParse(delayStr, out var d) ? d : 50;
    }

    public Task ExecuteAsync(ScanResult scan, CancellationToken ct)
    {
        if (!scan.IsValid)
        {
            _logger.LogDebug("Пропуск вставки: невалидный штрихкод {Raw}", scan.RawData);
            return Task.CompletedTask;
        }

        try
        {
            var text = scan.ParsedData;

            if (_appendNewline)
                text += Environment.NewLine;

            var prevWindow = GetForegroundWindow();

            SetClipboardText(text);

            Thread.Sleep(_delayMs);

            SimulatePaste();

            Thread.Sleep(_delayMs);

            if (prevWindow != IntPtr.Zero)
                SetForegroundWindow(prevWindow);

            _logger.LogInformation("Вставлено: {Data}", text.TrimEnd());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вставки: {Data}", scan.ParsedData);
        }

        return Task.CompletedTask;
    }

    private static void SetClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            throw new InvalidOperationException("Не удалось открыть буфер обмена");

        try
        {
            EmptyClipboard();

            var hGlobal = Marshal.StringToHGlobalUni(text);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("Не удалось выделить память для буфера обмена");

            SetClipboardData(CF_UNICODETEXT, hGlobal);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void SimulatePaste()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
