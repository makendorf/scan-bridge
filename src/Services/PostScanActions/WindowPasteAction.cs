using System.Runtime.InteropServices;
using ScanBridge.Models;
using ScanBridge.Utils;

namespace ScanBridge.Services.PostScanActions;

/// <summary>
/// Действие вставки результата сканирования в выбранное окно по заголовку.
/// Находит окно по частичному совпадению заголовка, активирует его и вставляет текст.
/// Работает только на Windows.
/// </summary>
public class WindowPasteAction : IPostScanAction
{
    /// <summary>
    /// Тип действия.
    /// </summary>
    public string Type => "WindowPaste";

    private readonly ILogger<WindowPasteAction> _logger;
    private readonly string _windowTitle;
    private readonly bool _appendNewline;
    private readonly int _activationDelay;
    private readonly string _mode;

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Создаёт экземпляр действия вставки в выбранное окно.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: WindowTitle, AppendNewline, ActivationDelay, Mode (clipboard/keyboard).</param>
    public WindowPasteAction(ILogger<WindowPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _windowTitle = settings.TryGetValue("WindowTitle", out var title) ? title : "";
        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;
        _activationDelay = settings.TryGetValue("ActivationDelay", out var actStr)
            && int.TryParse(actStr, out var ad) ? ad : 200;
        _mode = (settings.TryGetValue("Mode", out var mode) ? mode : "clipboard").ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(_windowTitle))
            _logger.LogWarning("WindowPaste: WindowTitle не задан, действие не будет выполняться");
    }

    /// <summary>
    /// Вставляет текст в выбранное окно.
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

        if (string.IsNullOrWhiteSpace(_windowTitle))
            return;

        try
        {
            var targetHwnd = FindWindowByTitle(_windowTitle);
            if (targetHwnd == IntPtr.Zero)
            {
                _logger.LogWarning("WindowPaste: окно с заголовком «{Title}» не найдено", _windowTitle);
                return;
            }

            var prevWindow = Win32Clipboard.GetForegroundWindow();

            ActivateWindow(targetHwnd);
            await Task.Delay(_activationDelay, ct);

            var text = scan.ParsedData;
            if (_appendNewline)
                text += Environment.NewLine;

            if (_mode == "keyboard")
            {
                Win32Clipboard.SimulateTyping(text);
            }
            else
            {
                Win32Clipboard.SetClipboardText(text);
                await Task.Delay(50, ct);
                Win32Clipboard.SimulatePaste();
                await Task.Delay(50, ct);
            }

            if (prevWindow != IntPtr.Zero && prevWindow != targetHwnd)
                Win32Clipboard.SetForegroundWindow(prevWindow);

            _logger.LogInformation("Вставлено в «{Title}»: {Data}", _windowTitle, ControlCharDisplay.ForDisplay(text.TrimEnd()));
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Вставка отменена: {Data}", ControlCharDisplay.ForDisplay(scan.ParsedData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка вставки в «{Title}»: {Data}", _windowTitle, ControlCharDisplay.ForDisplay(scan.ParsedData));
        }
    }

    private static void ActivateWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, 9);
        BringWindowToTop(hWnd);

        var foregroundThreadId = GetWindowThreadProcessId(Win32Clipboard.GetForegroundWindow(), out _);
        var currentThreadId = GetCurrentThreadId();

        var attached = false;
        if (foregroundThreadId != currentThreadId)
        {
            attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        Win32Clipboard.SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);

        if (attached)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
    }

    private static IntPtr FindWindowByTitle(string titlePart)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new System.Text.StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);

            if (sb.ToString().Contains(titlePart, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
