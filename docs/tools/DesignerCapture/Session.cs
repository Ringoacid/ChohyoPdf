using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Automation;

namespace DesignerCapture;

/// <summary>
/// デザイナーを起動して、実際のマウスとキーボードで操作する。
/// 座標は画面の物理ピクセル。部品は UI Automation の名前で探す。
/// </summary>
internal sealed class Session : IDisposable
{
    public const int WindowX = 40, WindowY = 30, WindowWidth = 1440, WindowHeight = 900;

    private readonly Process _process;

    public AutomationElement Window { get; private set; }

    public Session(string exe, string? file)
    {
        _process = Process.Start(new ProcessStartInfo(exe) { Arguments = file == null ? "" : "\"" + file + "\"", UseShellExecute = false })!;
        Window = WaitFor(() =>
        {
            _process.Refresh();
            return _process.MainWindowHandle == IntPtr.Zero ? null : AutomationElement.FromHandle(_process.MainWindowHandle);
        }, 30000, "デザイナーのウィンドウ");
        var h = new IntPtr(Window.Current.NativeWindowHandle);
        Native.ShowWindow(h, 1);
        Native.MoveWindow(h, WindowX, WindowY, WindowWidth, WindowHeight, true);
        Thread.Sleep(500);
        if (!Native.BringToFront(h)) throw new InvalidOperationException("デザイナーのウィンドウを前面に出せません");
        Thread.Sleep(1000);
    }

    public Rectangle WindowRect
    {
        get
        {
            Native.GetWindowRect(new IntPtr(Window.Current.NativeWindowHandle), out var r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    public int ProcessId => _process.Id;

    public static T WaitFor<T>(Func<T?> get, int timeoutMs, string what) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var v = get();
                if (v != null) return v;
            }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(200);
        }
        throw new TimeoutException(what + " が見つかりません");
    }

    // ---- 部品を探す ----

    /// <summary>このプロセスのトップレベルのウィンドウ（ダイアログや別のウィンドウを含む）。</summary>
    public IEnumerable<AutomationElement> TopWindows()
    {
        var cond = new PropertyCondition(AutomationElement.ProcessIdProperty, _process.Id);
        return AutomationElement.RootElement.FindAll(TreeScope.Children, cond).Cast<AutomationElement>();
    }

    /// <summary>ウィンドウを探す。持ち主のあるウィンドウ（差し込みやプレビュー）は、持ち主のウィンドウの子になっている。</summary>
    public AutomationElement FindWindow(string titlePart, int timeoutMs = 10000)
    {
        var isWindow = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
        return WaitFor(() => TopWindows()
            .SelectMany(w => new[] { w }.Concat(w.FindAll(TreeScope.Children, isWindow).Cast<AutomationElement>()))
            .FirstOrDefault(w => w.Current.Name.Contains(titlePart) && w.Current.NativeWindowHandle != Window.Current.NativeWindowHandle), timeoutMs, "ウィンドウ「" + titlePart + "」");
    }

    /// <summary>名前が一致する部品を探す。見えていない部品（画面外）は除く。</summary>
    public AutomationElement Find(string name, AutomationElement? scope = null, ControlType? type = null, int timeoutMs = 10000)
    {
        return WaitFor(() =>
        {
            Condition cond = new PropertyCondition(AutomationElement.NameProperty, name);
            if (type != null) cond = new AndCondition(cond, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            foreach (var root in scope != null ? new[] { scope } : TopWindows())
            {
                foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, cond))
                {
                    if (!e.Current.IsOffscreen && !e.Current.BoundingRectangle.IsEmpty) return e;
                }
            }
            return null;
        }, timeoutMs, "「" + name + "」");
    }

    /// <summary>名前の部品を含むボタンなど（クリックできる親）を探す。</summary>
    public AutomationElement FindClickable(string name, AutomationElement? scope = null, int timeoutMs = 10000)
    {
        var e = Find(name, scope, null, timeoutMs);
        var walker = TreeWalker.ControlViewWalker;
        for (var p = e; p != null && p != AutomationElement.RootElement; p = walker.GetParent(p))
        {
            var t = p.Current.ControlType;
            if (t == ControlType.Button || t == ControlType.MenuItem || t == ControlType.ListItem || t == ControlType.TabItem
                || t == ControlType.CheckBox || t == ControlType.RadioButton || t == ControlType.ComboBox || t == ControlType.TreeItem)
                return p;
        }
        return e;
    }

    public static Point Center(AutomationElement e)
    {
        var r = e.Current.BoundingRectangle;
        return new Point((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2));
    }

    /// <summary>ウィンドウの左上からの位置を、画面の座標にする。</summary>
    public Point At(int x, int y) => new(WindowRect.X + x, WindowRect.Y + y);

    // ---- マウス ----

    /// <summary>人の操作に見えるよう、なめらかに動かす。</summary>
    public static void MoveTo(Point to, int ms = 450)
    {
        var (fx, fy) = Native.Cursor;
        int steps = Math.Max(1, ms / 12);
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            Native.SetCursorPos((int)Math.Round(fx + (to.X - fx) * e), (int)Math.Round(fy + (to.Y - fy) * e));
            Thread.Sleep(12);
        }
    }

    public static void Click(Point p, int moveMs = 450, int after = 350)
    {
        MoveTo(p, moveMs);
        Thread.Sleep(120);
        Native.LeftDown();
        Thread.Sleep(70);
        Native.LeftUp();
        Thread.Sleep(after);
    }

    /// <summary>Shift などを押しながらクリックする。</summary>
    public static void ClickWith(Point p, Keys modifier, int moveMs = 450, int after = 350)
    {
        MoveTo(p, moveMs);
        Native.KeyDown(modifier);
        Thread.Sleep(120);
        Native.LeftDown();
        Thread.Sleep(70);
        Native.LeftUp();
        Thread.Sleep(80);
        Native.KeyUp(modifier);
        Thread.Sleep(after);
    }

    public static void DoubleClick(Point p, int moveMs = 450, int after = 400)
    {
        MoveTo(p, moveMs);
        Thread.Sleep(120);
        Native.LeftDown(); Thread.Sleep(40); Native.LeftUp();
        Thread.Sleep(60);
        Native.LeftDown(); Thread.Sleep(40); Native.LeftUp();
        Thread.Sleep(after);
    }

    public static void RightClick(Point p, int moveMs = 450, int after = 500)
    {
        MoveTo(p, moveMs);
        Thread.Sleep(120);
        Native.RightDown(); Thread.Sleep(70); Native.RightUp();
        Thread.Sleep(after);
    }

    public static void Drag(Point from, Point to, int ms = 800, int after = 400)
    {
        MoveTo(from);
        Thread.Sleep(150);
        Native.LeftDown();
        Thread.Sleep(120);
        MoveTo(to, ms);
        Thread.Sleep(150);
        Native.LeftUp();
        Thread.Sleep(after);
    }

    public void Click(string name, AutomationElement? scope = null, int after = 400) => Click(Center(FindClickable(name, scope)), after: after);

    // ---- キーボード ----

    public static void Type(string text, int interval = 70)
    {
        foreach (char c in text)
        {
            if (c == '\n') Press(Keys.Enter);
            else Native.TypeChar(c);
            Thread.Sleep(interval);
        }
    }

    public static void Press(Keys key, params Keys[] modifiers)
    {
        foreach (var m in modifiers) Native.KeyDown(m);
        Native.KeyDown(key);
        Thread.Sleep(30);
        Native.KeyUp(key);
        foreach (var m in modifiers.Reverse()) Native.KeyUp(m);
        Thread.Sleep(150);
    }

    // ---- 撮影 ----

    /// <summary>画面の範囲を PNG に保存する。</summary>
    public static void Screenshot(Rectangle area, string path)
    {
        using var bmp = new Bitmap(area.Width, area.Height);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
        bmp.Save(path, ImageFormat.Png);
    }

    public void Close()
    {
        if (_process.HasExited) return;
        _process.Kill();
        _process.WaitForExit(5000);
    }

    public void Dispose() => Close();
}
