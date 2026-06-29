using System.Runtime.InteropServices;

namespace ScanBridge.Utils;

internal static class Win32Clipboard
{
    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint CF_UNICODETEXT = 13;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    internal static void SetClipboardText(string text)
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

    internal static void SimulatePaste()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    internal static void SimulateTyping(string text, int delayMs = 10)
    {
        var prevWindow = GetForegroundWindow();

        var size = Marshal.SizeOf<INPUT>();
        var hInput = Marshal.AllocHGlobal(size * 2);

        try
        {
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                var down = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE }
                    }
                };
                var up = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP }
                    }
                };

                Marshal.StructureToPtr(down, hInput, false);
                Marshal.StructureToPtr(up, hInput + size, false);
                SendInput(2, hInput, size);

                if (delayMs > 0 && i + 1 < text.Length)
                    Thread.Sleep(delayMs);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(hInput);
        }

        if (prevWindow != IntPtr.Zero)
            SetForegroundWindow(prevWindow);
    }
}
