using System.Runtime.InteropServices;

namespace ScanBridge.Utils;

internal static class Win32Clipboard
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

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

    private const uint CF_UNICODETEXT = 13;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
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
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
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
        // 1. КРИТИЧЕСКАЯ ПРОВЕРКА: Размер структуры должен быть строго 40 (x64) или 28 (x86)
        int size = Marshal.SizeOf<INPUT>();
        if (size != 40 && size != 28)
        {
            throw new InvalidOperationException($"Неверный размер структуры INPUT: {size}. SendInput не сработает.");
        }

        var inputs = new INPUT[4];

        // ИСПРАВЛЕНИЕ: Убираем KEYEVENTF_EXTENDEDKEY для левого Ctrl. 
        inputs[0] = CreateKeyInput(0x11, 0); // Ctrl Down
        inputs[1] = CreateKeyInput(0x56, 0); // V Down
        inputs[2] = CreateKeyInput(0x56, KEYEVENTF_KEYUP); // V Up
        inputs[3] = CreateKeyInput(0x11, KEYEVENTF_KEYUP); // Ctrl Up

        // 2. ПРОВЕРКА РЕЗУЛЬТАТА: SendInput возвращает количество успешно отправленных событий
        uint sent = SendInput((uint)inputs.Length, inputs, size);

        if (sent == 0)
        {
            int error = Marshal.GetLastWin32Error();
            // Если вылетит здесь, смотрите код ошибки ниже в чеклисте
            throw new InvalidOperationException($"SendInput вернул 0. Код ошибки Windows: {error}");
        }

        // 3. Увеличиваем задержку, чтобы "тяжелые" приложения успели обработать вставку
        Thread.Sleep(100);
    }

    internal static void SimulateTyping(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var prevWindow = GetForegroundWindow();

        var inputs = new INPUT[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            inputs[i * 2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            inputs[i * 2 + 1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SendInput failed with error code: {error}");
        }

        Thread.Sleep(50);

        if (prevWindow != IntPtr.Zero)
            SetForegroundWindow(prevWindow);
    }

    private static INPUT CreateKeyInput(byte vk, uint flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
