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
        const int INPUT_SIZE = 40;

        var inputSize = Marshal.SizeOf<UIntPtr>() == 8 ? INPUT_SIZE : 28;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var hInput = Marshal.AllocHGlobal(inputSize);

            try
            {
                Marshal.WriteInt32(hInput, 0, (int)INPUT_KEYBOARD);

                var kiOffset = Marshal.SizeOf<uint>();
                Marshal.WriteInt16(hInput, kiOffset, 0);
                Marshal.WriteInt16(hInput, kiOffset + 2, (short)ch);
                Marshal.WriteInt32(hInput, kiOffset + 4, (int)KEYEVENTF_UNICODE);
                Marshal.WriteInt32(hInput, kiOffset + 8, 0);
                Marshal.WriteInt64(hInput, kiOffset + 12, 0);

                SendInput(1, hInput, inputSize);

                Marshal.WriteInt32(hInput, kiOffset + 4, (int)(KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
                SendInput(1, hInput, inputSize);
            }
            finally
            {
                Marshal.FreeHGlobal(hInput);
            }

            if (delayMs > 0 && i + 1 < text.Length)
                Thread.Sleep(delayMs);
        }
    }
}
