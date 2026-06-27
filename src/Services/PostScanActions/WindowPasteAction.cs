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
    private readonly int _delayMs;
    private readonly int _activationDelay;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    /// <summary>
    /// Создаёт экземпляр действия вставки в выбранное окно.
    /// </summary>
    /// <param name="logger">Логгер.</param>
    /// <param name="settings">Параметры: WindowTitle (заголовок окна), AppendNewline, DelayMs.</param>
    public WindowPasteAction(ILogger<WindowPasteAction> logger, Dictionary<string, string> settings)
    {
        _logger = logger;

        _windowTitle = settings.TryGetValue("WindowTitle", out var title) ? title : "";
        _appendNewline = settings.TryGetValue("AppendNewline", out var val)
            && bool.TryParse(val, out var b) && b;
        _delayMs = settings.TryGetValue("DelayMs", out var delayStr)
            && int.TryParse(delayStr, out var d) ? d : 50;
        _activationDelay = settings.TryGetValue("ActivationDelay", out var actStr)
            && int.TryParse(actStr, out var ad) ? ad : 200;

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

            var prevWindow = GetForegroundWindow();

            ActivateWindow(targetHwnd);
            await Task.Delay(_activationDelay, ct);

            var text = scan.ParsedData;
            if (_appendNewline)
                text += Environment.NewLine;

            SetClipboardText(text);
            await Task.Delay(_delayMs, ct);

            SimulatePaste();
            await Task.Delay(_delayMs, ct);

            if (prevWindow != IntPtr.Zero && prevWindow != targetHwnd)
                SetForegroundWindow(prevWindow);

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

    /// <summary>
    /// Активирует окно через AttachThreadInput + SetForegroundWindow.
    /// Обходит ограничение Windows, которое не позволяет фоновому процессу менять фокус.
    /// </summary>
    private static void ActivateWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, 9); // SW_RESTORE
        BringWindowToTop(hWnd);

        var foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThreadId = GetCurrentThreadId();

        var attached = false;
        if (foregroundThreadId != currentThreadId)
        {
            attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);

        if (attached)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
    }

    /// <summary>
    /// Находит окно по частичному совпадению заголовка.
    /// </summary>
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

    /// <summary>
    /// Устанавливает текст в буфер обмена Windows через Win32 API.
    /// </summary>
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
