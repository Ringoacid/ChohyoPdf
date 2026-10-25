using System.Drawing;
using System.Windows.Automation;
using ChohyoPdf.Designer.Model;

namespace DesignerCapture;

internal sealed partial class Scenes
{
    public static readonly Dictionary<string, Action<Scenes>> All = new()
    {
        ["overview"] = s => s.Overview(),
        ["tutorial"] = s => s.Tutorial(),
        ["place-text"] = s => s.PlaceText(),
        ["move-resize"] = s => s.MoveResize(),
        ["table-merge"] = s => s.TableMerge(),
        ["page-number"] = s => s.PageNumber(),
        ["merge-result"] = s => s.MergeResult(),
        ["list-table"] = s => s.ListTable(),
        ["merge-window"] = s => s.MergeWindow(),
        ["expression"] = s => s.Expression(),
        ["preview"] = s => s.Preview(),
        ["code-window"] = s => s.CodeWindow(),
    };

    /// <summary>用紙の上の部分（書式バーから下）。短い動画はこの範囲を切り取る。</summary>
    private static readonly Rectangle Canvas = new(0, 44, 1120, 560);

    /// <summary>画面の全体（請求書の見本で、表を選んだところ）。</summary>
    private void Overview()
    {
        Open(SampleReports.CreateInvoice(), "請求書");
        Session.Click(Mm(60, 110));
        Session.MoveTo(_s.At(1300, 860), 300);
        Shot("overview");
        Close();
    }

    /// <summary>はじめての文書：テキストと表を置き、差し込み項目を作って、プレビューする。</summary>
    private void Tutorial()
    {
        Open(null);
        Record("tutorial.mp4", () =>
        {
            Step("表題");
            _s.Click("テキストツール");
            Session.Drag(Mm(20, 20), Mm(190, 32));
            Session.DoubleClick(Mm(100, 26));
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("地域交流会のご案内");
            Session.Press(Keys.Enter, Keys.ControlKey);
            Session.Click(_s.At(266, 68));                  // 書式バーの文字サイズ
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("18");
            Session.Press(Keys.Enter);
            Session.Click(_s.At(589, 68));                  // 中央揃え
            Session.Click(_s.At(338, 68));                  // 太字
            Thread.Sleep(800);

            Step("表");
            // 3 列の表ができるので、1 列削除して 2 列にする
            _s.Click("表ツール");
            Session.Drag(Mm(30, 60), Mm(180, 81));
            Session.Click(Mm(160, 63.5));
            _s.Click("列を削除");
            Session.DoubleClick(Mm(50, 63.5));
            var cells = new[] { "日時", "11月14日（土）13:30～16:00", "会場", "市民会館 2 階 大会議室", "会費", "無料" };
            for (int i = 0; i < cells.Length; i++)
            {
                Session.Type(cells[i]);
                if (i == cells.Length - 1) break;
                Session.Press(Keys.Tab);                     // 確定して隣のセルを選ぶ
                Session.Press(Keys.F2);                      // 編集を始める
            }
            Session.Press(Keys.Enter, Keys.ControlKey);
            Session.Click(Mm(100, 140));
            Thread.Sleep(600);

            Step("差し込み項目");
            _s.Click("差し込みの設定");
            var merge = _s.FindWindow("差し込み");
            _s.Click("差し込み項目を追加", merge);
            var edits = Edits(merge);
            Session.Click(Session.Center(edits[0]));        // 名前
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("氏名");
            Session.Click(Session.Center(edits[1]));        // 確認用の値
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("山田 太郎");
            Session.Press(Keys.Tab);
            Thread.Sleep(800);
            _s.Click("閉じる", merge);

            Step("宛名");
            _s.Click("テキストツール");
            Session.Drag(Mm(20, 42), Mm(110, 50));
            Session.DoubleClick(Mm(60, 46));
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("{氏名} 様");
            Session.Press(Keys.Enter, Keys.ControlKey);
            Session.Click(Mm(100, 140));
            Thread.Sleep(800);

            Step("結果とプレビュー");
            _s.Click("差し込み結果を表示");
            Thread.Sleep(2000);
            _s.Click("プレビュー");
            Thread.Sleep(4000);
            _s.Click("閉じる", _s.FindWindow("プレビュー"));
        }, poster: "tutorial.png", posterAt: -8);
        Close();
    }

    /// <summary>テキストを置いて、文字を入力する。</summary>
    private void PlaceText()
    {
        Open(null);
        Record("place-text.webp", () =>
        {
            _s.Click("テキストツール");
            Session.Drag(Mm(20, 20), Mm(130, 34));
            Session.DoubleClick(Mm(60, 27));
            Session.Press(Keys.A, Keys.ControlKey);
            Session.Type("地域交流会のご案内");
            Session.Press(Keys.Enter, Keys.ControlKey);
        }, part: Canvas);
        Close();
    }

    /// <summary>部品をドラッグで動かし、ハンドルで大きさを変える。</summary>
    private void MoveResize()
    {
        var r = SampleReports.CreateBlank();
        r.Pages[0].Elements.Add(new RectangleElement { Name = "矩形", X = 30, Y = 30, Width = 50, Height = 25, FillColor = "#DBEAFE", StrokeColor = "#1F4E79" });
        Open(r);
        Record("move-resize.webp", () =>
        {
            Session.Click(Mm(55, 42));
            Session.Drag(Mm(55, 42), Mm(85, 57));             // 移動（グリッドに合わせて動く）
            Session.Drag(Mm(110, 70), Mm(140, 82));           // 右下のハンドルで大きさを変える
        }, part: Canvas);
        Close();
    }

    /// <summary>表のセルを 2 つ選んで結合する。</summary>
    private void TableMerge()
    {
        var r = SampleReports.CreateBlank();
        var table = TableElement.Create(4, 4, 35, 8);
        table.Name = "成績表";
        table.X = 20;
        table.Y = 30;
        string[][] text =
        {
            new[] { "氏名", "前期", "後期", "合計" },
            new[] { "", "国語", "数学", "" },
            new[] { "山田 太郎", "82", "75", "157" },
            new[] { "佐藤 花子", "91", "88", "179" },
        };
        for (int row = 0; row < 4; row++)
            for (int c = 0; c < 4; c++) table.Rows[row].Cells[c].Text = text[row][c];
        r.Pages[0].Elements.Add(table);
        Open(r);
        Point Cell(int row, int c) => Mm(20 + 35 * c + 17.5, 30 + 8 * row + 4);
        Record("table-merge.webp", () =>
        {
            Session.Click(Cell(2, 0));                        // 表を選ぶ
            Session.Click(Cell(0, 1));                        // セルを選ぶ
            Session.ClickWith(Cell(0, 2), Keys.ShiftKey);     // 範囲を広げる
            _s.Click("結合");
            Session.Click(Cell(0, 0));
            Session.ClickWith(Cell(1, 0), Keys.ShiftKey);
            _s.Click("結合");
            Session.Click(Mm(100, 140));
        }, part: Canvas);
        Close();
    }

    /// <summary>全ページ共通の部品として、ページ番号を置く。</summary>
    private void PageNumber()
    {
        var r = SampleReports.CreateBlank();
        r.Pages[0].Elements.Add(new TextElement { Name = "表題", Text = "活動報告書", X = 20, Y = 30, Width = 170, Height = 14, FontSize = 24, Bold = true, Alignment = ChohyoPdf.TextAlignment.Center });
        Open(r);
        // 1 ページ目の下端が見えるまでスクロールする
        for (int i = 0; i < 40 && Mm(105, 297).Y > _s.At(0, 840).Y; i++)
        {
            Session.MoveTo(_s.At(700, 400), 1);
            Native.Wheel(-120);
            Thread.Sleep(300);
            Calibrate();
        }
        Session.MoveTo(_s.At(700, 860), 1);
        Record("page-number.webp", () =>
        {
            _s.Click("ページ番号");
            Thread.Sleep(800);
            _s.Click("差し込み結果を表示");
            Thread.Sleep(800);
        }, part: new Rectangle(0, 44, 1120, 840));
        Close();
    }

    /// <summary>差し込み欄に、確認用の値を入れて表示する。</summary>
    private void MergeResult()
    {
        Open(SampleReports.CreateLetter(), "案内状");
        Session.MoveTo(Mm(150, 120), 1);
        Record("merge-result.webp", () =>
        {
            Thread.Sleep(600);
            _s.Click("差し込み結果を表示");
            Thread.Sleep(1500);
        }, part: Canvas);
        Close();
    }

    /// <summary>一覧の表を用紙に置き、確認用の行を並べる。</summary>
    private void ListTable()
    {
        var r = SampleReports.CreateBlank();
        var list = new ListDefinition { Name = "参加者" };
        list.Columns.Add(new FieldDefinition { Name = "No", Kind = FieldKind.Number, Format = "0" });
        list.Columns.Add(new FieldDefinition { Name = "氏名", Kind = FieldKind.Text });
        list.Columns.Add(new FieldDefinition { Name = "所属", Kind = FieldKind.Text });
        string[][] rows =
        {
            new[] { "1", "山田 太郎", "総務部" }, new[] { "2", "佐藤 花子", "営業部" }, new[] { "3", "鈴木 一郎", "開発部" },
            new[] { "4", "高橋 葵", "広報部" }, new[] { "5", "田中 健", "経理部" },
        };
        foreach (var row in rows) list.SampleRows.Add(row.ToList());
        r.Lists.Add(list);
        Open(r);
        Record("list-table.webp", () =>
        {
            _s.Click("差し込みの設定");
            var merge = _s.FindWindow("差し込み");
            _s.Click("一覧の章", merge);
            Thread.Sleep(1000);
            _s.Click("この一覧の表を用紙に置く", merge);
            Thread.Sleep(800);
            _s.Click("閉じる", merge);
            Thread.Sleep(800);
            _s.Click("差し込み結果を表示");
            Thread.Sleep(1500);
        }, width: 1080);
        Close();
    }

    /// <summary>差し込みのウィンドウ（差し込み項目と一覧）。</summary>
    private void MergeWindow()
    {
        Open(SampleReports.CreateInvoice(), "請求書");
        _s.Click("差し込みの設定");
        var merge = _s.FindWindow("差し込み");
        Session.Click(Session.Center(ListItems(merge, "FieldList")[2]));
        Session.MoveTo(_s.At(1300, 860), 300);
        Shot("merge-fields", window: merge);
        _s.Click("一覧の章", merge);
        Session.MoveTo(_s.At(1300, 860), 300);
        Shot("merge-lists", window: merge);
        Close();
    }

    /// <summary>C# の式で計算する差し込み項目。</summary>
    private void Expression()
    {
        var r = SampleReports.CreateInvoice();
        r.Fields.Add(new FieldDefinition { Name = "支払期限", Kind = FieldKind.Date, Format = "yyyy年M月d日", Script = "now.Date.AddDays(1 - now.Day).AddMonths(1).AddDays(-1)" });
        Open(r, "請求書", allowScripts: true);
        _s.Click("差し込みの設定");
        var merge = _s.FindWindow("差し込み");
        var items = ListItems(merge, "FieldList");
        Session.Click(Session.Center(items[items.Count - 1]));
        Session.MoveTo(_s.At(1300, 860), 300);
        Shot("expression", window: merge);
        Close();
    }

    /// <summary>プレビューのウィンドウ。</summary>
    private void Preview()
    {
        Open(SampleReports.CreateInvoice(), "請求書");
        _s.Click("プレビュー");
        var preview = _s.FindWindow("プレビュー");
        Place(preview, 300, 30, 900, 900);
        Thread.Sleep(1500);
        Session.MoveTo(_s.At(1300, 860), 1);
        Shot("preview", window: preview);
        Close();
    }

    /// <summary>C# コードを出力するウィンドウ。</summary>
    private void CodeWindow()
    {
        Open(SampleReports.CreateInvoice(), "請求書");
        Session.Press(Keys.G, Keys.ControlKey);
        var code = _s.FindWindow("C#");
        Thread.Sleep(1500);
        Session.MoveTo(_s.At(1300, 860), 1);
        Shot("code-window", window: code);
        Close();
    }

    private static List<AutomationElement> Edits(AutomationElement scope)
        => scope.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
            .Cast<AutomationElement>().Where(e => !e.Current.IsOffscreen).ToList();

    private static List<AutomationElement> ListItems(AutomationElement scope, string listId)
    {
        var list = scope.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, listId));
        return list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
    }
}
