using System.Runtime.InteropServices;

namespace DesignerCapture;

/// <summary>マウス・キーボードの入力と、ウィンドウの操作（Win32）。</summary>
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public int type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public short wVk, wScan; public int dwFlags, time; public IntPtr dwExtraInfo; }

    private const int INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    private const int MOUSEEVENTF_LEFTDOWN = 0x02, MOUSEEVENTF_LEFTUP = 0x04, MOUSEEVENTF_RIGHTDOWN = 0x08, MOUSEEVENTF_RIGHTUP = 0x10, MOUSEEVENTF_WHEEL = 0x800;
    private const int KEYEVENTF_KEYUP = 0x02, KEYEVENTF_UNICODE = 0x04;

    [DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hgt, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool on);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr h);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    /// <summary>
    /// ウィンドウを前面に出す。ほかのアプリが前面にあると SetForegroundWindow だけでは前面に出せないので、
    /// 前面のウィンドウのスレッドに入力をつないでから行う。前面に出せたら true。
    /// </summary>
    public static bool BringToFront(IntPtr h)
    {
        var foreground = GetForegroundWindow();
        if (foreground == h) return true;
        uint current = GetCurrentThreadId(), other = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        bool attached = other != 0 && other != current && AttachThreadInput(current, other, true);
        try
        {
            BringWindowToTop(h);
            SetForegroundWindow(h);
        }
        finally
        {
            if (attached) AttachThreadInput(current, other, false);
        }
        return GetForegroundWindow() == h;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out RECT value, int size);

    /// <summary>ウィンドウの見えている範囲（Windows 11 の見えない枠や影を除く）。</summary>
    public static RECT VisibleBounds(IntPtr h)
    {
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<RECT>()) != 0) GetWindowRect(h, out r);
        return r;
    }

    public static void UsePhysicalPixels() => SetProcessDpiAwarenessContext(new IntPtr(-4));   // PER_MONITOR_AWARE_V2

    public static (int X, int Y) Cursor
    {
        get { GetCursorPos(out var p); return (p.X, p.Y); }
    }

    private static void Send(params INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

    private static INPUT Mouse(int flags, int data = 0) => new() { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } } };

    public static void LeftDown() => Send(Mouse(MOUSEEVENTF_LEFTDOWN));
    public static void LeftUp() => Send(Mouse(MOUSEEVENTF_LEFTUP));
    public static void RightDown() => Send(Mouse(MOUSEEVENTF_RIGHTDOWN));
    public static void RightUp() => Send(Mouse(MOUSEEVENTF_RIGHTUP));
    public static void Wheel(int delta) => Send(Mouse(MOUSEEVENTF_WHEEL, delta));

    public static void KeyDown(Keys key) => Send(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = (short)key } } });
    public static void KeyUp(Keys key) => Send(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = (short)key, dwFlags = KEYEVENTF_KEYUP } } });

    /// <summary>文字を 1 文字ずつ送る（IME を通さずに、日本語もそのまま入る）。</summary>
    public static void TypeChar(char c)
    {
        Send(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wScan = (short)c, dwFlags = KEYEVENTF_UNICODE } } },
             new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wScan = (short)c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } });
    }
}
