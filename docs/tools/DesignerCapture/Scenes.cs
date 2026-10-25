using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using ChohyoPdf.Designer.Model;

namespace DesignerCapture;

/// <summary>撮影する場面。名前は出力するファイルの名前と同じ。</summary>
internal sealed partial class Scenes
{
    private readonly string _exe;
    private readonly string _outDir;
    private readonly string _workDir;

    private Session _s = null!;
    private double _originX, _originY, _scale;   // 用紙の (0, 0) mm の画面の座標と、1 mm のピクセル数

    public Scenes(string exe, string outDir)
    {
        _exe = exe;
        _outDir = outDir;
        _workDir = Path.Combine(Path.GetTempPath(), "ChohyoPdfDesignerCapture");
        Directory.CreateDirectory(_workDir);
    }

    // ---- 準備 ----

    /// <summary>デザインをファイルに保存して、デザイナーで開く。<paramref name="report"/> が null なら新しい文書。</summary>
    private Session Open(ReportDefinition? report, string fileName = "文書", bool allowScripts = false)
    {
        string? path = null;
        if (report != null)
        {
            path = Path.Combine(_workDir, fileName + ".chohyo");
            ReportSerializer.Save(report, path);
        }
        _s = new Session(_exe, path);
        if (allowScripts)
        {
            // C# の式のあるファイルを開くと、実行するかを尋ねられる
            _s.Click("はい(Y)", _s.FindWindow("C# の式の実行"));
            Thread.Sleep(1000);
        }
        Calibrate();
        Session.MoveTo(_s.At(700, 500), 1);
        return _s;
    }

    /// <summary>ステータスバーに出るカーソルの位置（mm）から、用紙の座標と画面の座標の対応を求める。</summary>
    private void Calibrate()
    {
        var a = _s.At(400, 200);
        var b = _s.At(800, 600);
        var (ax, ay) = ReadCursorMm(a);
        var (bx, by) = ReadCursorMm(b);
        _scale = (b.X - a.X) / (bx - ax);
        _originX = a.X - ax * _scale;
        _originY = a.Y - ay * _scale;
    }

    private (double X, double Y) ReadCursorMm(Point p)
    {
        Native.SetCursorPos(p.X - 3, p.Y - 3);
        Thread.Sleep(100);
        Native.SetCursorPos(p.X, p.Y);
        Thread.Sleep(400);
        var text = _s.TopWindows().First().FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>().Select(e => e.Current.Name).First(n => n.StartsWith("X ") && n.Contains("mm"));
        var m = Regex.Match(text, @"X (-?[\d.]+) mm, Y (-?[\d.]+) mm");
        return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>用紙の位置（mm）の、画面の座標。</summary>
    private Point Mm(double x, double y) => new((int)Math.Round(_originX + x * _scale), (int)Math.Round(_originY + y * _scale));

    // ---- 出力 ----

    private string Out(string file) => Path.Combine(_outDir, file);

    /// <summary>ウィンドウ全体か、その一部（ウィンドウの左上からの範囲）を PNG にする。</summary>
    private void Shot(string name, Rectangle? part = null, AutomationElement? window = null)
    {
        Thread.Sleep(500);
        var area = window != null ? Bounds(window) : _s.WindowRect;
        if (part is { } r) area = new Rectangle(area.X + r.X, area.Y + r.Y, r.Width, r.Height);
        Session.Screenshot(area, Out(name + ".png"));
    }

    private static Rectangle Bounds(AutomationElement window)
    {
        var r = Native.VisibleBounds(new IntPtr(window.Current.NativeWindowHandle));
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>ウィンドウの位置と大きさを変える（画面に収めて撮るため）。</summary>
    private static void Place(AutomationElement window, int x, int y, int width, int height)
    {
        Native.MoveWindow(new IntPtr(window.Current.NativeWindowHandle), x, y, width, height, true);
        Thread.Sleep(1200);
    }

    /// <summary>
    /// ウィンドウを録画しながら <paramref name="act"/> を行い、.webp か .mp4 にする。
    /// <paramref name="part"/> はウィンドウの左上からの切り取る範囲、<paramref name="width"/> は縮小後の幅。
    /// </summary>
    private void Record(string file, Action act, Rectangle? part = null, int? width = null, string? poster = null, double posterAt = 1)
    {
        string raw = Path.Combine(_workDir, Path.GetFileNameWithoutExtension(file) + ".mkv");
        var win = _s.WindowRect;
        using (var rec = new Recorder(win, raw))
        {
            Thread.Sleep(500);
            act();
            Thread.Sleep(1200);
            rec.Stop();
        }
        Recorder.Encode(raw, Out(file), width, part);
        if (poster != null) Recorder.Frame(raw, Out(poster), posterAt, width, part);
        Console.WriteLine($"  {file}: {new FileInfo(Out(file)).Length / 1024} KB");
    }

    private void Close()
    {
        _s.Close();
        Thread.Sleep(800);
    }

    /// <summary>撮影に失敗したときも、デザイナーを残さない。</summary>
    public void CloseAll()
    {
        _s?.Close();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_exe))) p.Kill();
    }

    private static void Step(string what) => Console.WriteLine($"  {DateTime.Now:HH:mm:ss} {what}");
}
