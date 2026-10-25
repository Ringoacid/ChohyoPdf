using System.Text;
using System.Windows.Automation;
using DesignerCapture;

// 説明書（docs/designer.md）の画面写真と動画を撮る。
// 使い方: dotnet run --project docs/tools/DesignerCapture -- <デザイナーの exe> <出力フォルダ> [撮るものの名前...]
// 撮影中はマウスとキーボードを動かすので、PC に触らないでください。
Native.UsePhysicalPixels();
Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 2)
{
    Console.WriteLine("使い方: DesignerCapture <ChohyoPdfDesigner.exe> <出力フォルダ> [名前...]  （名前: dump " + string.Join(" ", Scenes.All.Keys) + "）");
    return 1;
}
string exe = Path.GetFullPath(args[0]);
string outDir = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outDir);
var names = args.Skip(2).ToList();

// dump [請求書] [click:部品の名前 | at:x,y]... : 部品の名前と位置（ウィンドウの左上から）を一覧にする（シナリオを書くときの調査用）
if (names.Count >= 1 && names[0] == "dump")
{
    string? file = null;
    if (names.Contains("請求書"))
    {
        file = Path.Combine(Path.GetTempPath(), "請求書.chohyo");
        ChohyoPdf.Designer.Model.ReportSerializer.Save(ChohyoPdf.Designer.Model.SampleReports.CreateInvoice(), file);
    }
    using var s = new Session(exe, file);
    foreach (var step in names.Skip(1))
    {
        if (step.StartsWith("click:")) s.Click(step.Substring(6));
        else if (step.StartsWith("at:"))
        {
            var xy = step.Substring(3).Split(',').Select(int.Parse).ToArray();
            Session.Click(s.At(xy[0], xy[1]));
        }
        else continue;
        Thread.Sleep(1500);
    }
    var origin = s.WindowRect.Location;
    foreach (var w in s.TopWindows())
    {
        Console.WriteLine("# " + w.Current.Name);
        foreach (AutomationElement e in w.FindAll(TreeScope.Descendants, Condition.TrueCondition))
        {
            var c = e.Current;
            if (c.IsOffscreen || c.BoundingRectangle.IsEmpty) continue;
            var r = c.BoundingRectangle;
            Console.WriteLine($"{c.ControlType.ProgrammaticName.Replace("ControlType.", ""),-12} {(int)r.X - origin.X,5},{(int)r.Y - origin.Y,4} {(int)r.Width,4}x{(int)r.Height,-4} [{c.AutomationId}] {c.Name}");
        }
    }
    return 0;
}

var scenes = new Scenes(exe, outDir);
foreach (var name in names.Count > 0 ? names : Scenes.All.Keys.ToList())
{
    if (!Scenes.All.TryGetValue(name, out var scene))
    {
        Console.Error.WriteLine("不明な名前: " + name);
        return 1;
    }
    Console.WriteLine("撮影: " + name);
    try
    {
        scene(scenes);
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e);
        Session.Screenshot(Screen.PrimaryScreen!.Bounds, Path.Combine(outDir, "error-" + name + ".png"));
        return 1;
    }
    finally
    {
        scenes.CloseAll();
    }
}
return 0;
