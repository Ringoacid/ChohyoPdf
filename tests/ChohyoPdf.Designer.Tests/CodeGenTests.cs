using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    /// <summary>生成した C# コードの PDF が、デザイナーの PDF 出力とバイト単位で一致することを確かめる。</summary>
    public class CodeGenTests
    {
        private static readonly DateTimeOffset Fixed = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(9));

        /// <summary>{今日} などに使う日時（生成コードとデザイナーの両方に渡す）。</summary>
        private static readonly DateTime FixedNow = new DateTime(2019, 5, 1, 9, 5, 30);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(18)]
        [InlineData(46)]
        public void Invoice_MatchesRenderer(int rows)
        {
            var report = SampleReports.CreateInvoice();
            if (!TestFonts.Apply(report)) return;
            var list = report.Lists[0];
            while (list.SampleRows.Count > rows) list.SampleRows.RemoveAt(list.SampleRows.Count - 1);
            while (list.SampleRows.Count < rows) list.SampleRows.Add(new List<string> { (list.SampleRows.Count + 1).ToString(), "追加の品目", "1", "100", "100" });

            var pdf = AssertSameAsRenderer(report);
            Assert.Equal(Math.Max(1, (rows + 14) / 15), pdf.Pages.Count);
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(30, 3)]
        [InlineData(31, 4)]
        [InlineData(95, 6)]
        public void MultiPageDocument_MatchesRenderer(int rows, int expectedPages)
        {
            var report = SampleReports.CreateDocument();
            if (!TestFonts.Apply(report)) return;
            var list = report.Lists[0];
            while (list.SampleRows.Count > rows) list.SampleRows.RemoveAt(list.SampleRows.Count - 1);
            while (list.SampleRows.Count < rows) list.SampleRows.Add(new List<string> { (list.SampleRows.Count + 1).ToString(), "追加の参加者", "総務部" });

            var pdf = AssertSameAsRenderer(report);
            Assert.Equal(expectedPages, pdf.Pages.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(13)]
        public void CommonElementsAndPaperPerPage_MatchRenderer(int rows)
        {
            var report = CommonAndPaper(rows);
            if (!TestFonts.Apply(report)) return;
            var pdf = AssertSameAsRenderer(report);
            int repeated = Math.Max(1, (rows + 3) / 4);
            Assert.Equal(2 + repeated, pdf.Pages.Count);
            // 2 ページ目だけ A5 横
            Assert.Equal(PageSize.A5.Landscape.Width, pdf.Pages[1].Width, 3);
            Assert.Equal(PageSize.A4.Width, pdf.Pages[0].Width, 3);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(18)]
        [InlineData(46)]
        public void SinglePageWithCommonFooter_MatchesRenderer(int rows)
        {
            var report = SampleReports.CreateInvoice();
            report.Common.Elements.Add(new TextElement { Text = "{ページ番号} / {総ページ数}", X = 20, Y = 288, Width = 170, Height = 5, Alignment = TextAlignment.Right });
            report.Common.Elements.Add(new TextElement { Text = "（続き）", X = 20, Y = 8, Width = 40, Height = 5, Visibility = PageVisibility.ExceptFirstPage });
            if (!TestFonts.Apply(report)) return;
            var list = report.Lists[0];
            while (list.SampleRows.Count > rows) list.SampleRows.RemoveAt(list.SampleRows.Count - 1);
            while (list.SampleRows.Count < rows) list.SampleRows.Add(new List<string> { (list.SampleRows.Count + 1).ToString(), "追加の品目", "1", "100", "100" });
            var pdf = AssertSameAsRenderer(report);
            Assert.Equal(Math.Max(1, (rows + 14) / 15), pdf.Pages.Count);
            Assert.Contains("DrawCommon(g, data, pageIndex + 1, pageCount);", CSharpGenerator.Generate(report));
        }

        [Fact]
        public void CommonElements_WithoutRepeatingRows_PassFixedPageNumbers()
        {
            var report = CommonAndPaper(0);
            report.Pages.RemoveAt(2);
            if (!TestFonts.Apply(report)) return;
            AssertSameAsRenderer(report);
            string code = CSharpGenerator.Generate(report);
            Assert.Contains("DrawCommon(g, data, 1, 2);", code);
            // 2 ページ目は共通の部品を描かない
            Assert.DoesNotContain("DrawCommon(g, data, 2, 2);", code);
        }

        /// <summary>全ページ共通の部品と、ページごとの用紙・共通の部品を描かないページ・くり返し行を使うデザイン。</summary>
        private static ReportDefinition CommonAndPaper(int rows)
        {
            var r = new ReportDefinition { Name = "共通の部品" };
            r.Fields.Add(new FieldDefinition { Name = "題", SampleValue = "見出し" });
            var list = new ListDefinition { Name = "行" };
            list.Columns.Add(new FieldDefinition { Name = "名前" });
            for (int i = 0; i < rows; i++) list.SampleRows.Add(new List<string> { "名前" + i });
            r.Lists.Add(list);

            r.Common.Elements.Add(new TextElement { Text = "{題} {ページ番号}/{総ページ数}", X = 10, Y = 5, Width = 80, Height = 6 });
            r.Common.Elements.Add(new LineElement { X1 = 10, Y1 = 12, X2 = 100, Y2 = 12, Visibility = PageVisibility.ExceptFirstPage });
            r.Common.Elements.Add(new TextElement { Text = "最初", X = 10, Y = 14, Width = 30, Height = 6, Visibility = PageVisibility.FirstPage });
            r.Common.Elements.Add(new TextElement { Text = "最後", X = 50, Y = 14, Width = 30, Height = 6, Visibility = PageVisibility.LastPage });
            // 共通の部品の表は、行をくり返さない
            var commonTable = TableElement.Create(1, 1, 30, 6);
            commonTable.X = 120;
            commonTable.Y = 5;
            commonTable.DetailSource = "行";
            commonTable.DetailRowCount = 2;
            commonTable.DetailRow.Cells[0].Text = "{名前}";
            r.Common.Elements.Add(commonTable);

            r.Pages[0].Elements.Add(new TextElement { Text = "1 ページ目 {ページ番号}", X = 10, Y = 30, Width = 80, Height = 8 });

            var landscape = new ReportPage { Name = "横", Paper = new PaperSetting { PaperName = "A5", Landscape = true }, HideCommon = true };
            landscape.Elements.Add(new TextElement { Text = "横向き {ページ番号}/{総ページ数}", X = 10, Y = 10, Width = 120, Height = 8 });
            r.Pages.Add(landscape);

            var repeat = new ReportPage { Name = "くり返し", Paper = new PaperSetting { PaperName = PaperSizes.Custom, CustomWidth = 150, CustomHeight = 120 } };
            var table = TableElement.Create(1, 1, 60, 6);
            table.X = 10;
            table.Y = 30;
            table.DetailSource = "行";
            table.DetailRowCount = 4;
            table.DetailRow.Cells[0].Text = "{名前}";
            repeat.Elements.Add(table);
            repeat.Elements.Add(new TextElement { Text = "（続き）", X = 80, Y = 30, Width = 30, Height = 6, Visibility = PageVisibility.ExceptFirstPage });
            r.Pages.Add(repeat);
            return r;
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, 9)]
        [InlineData(3, 0)]
        [InlineData(3, 9)]
        public void BuiltInFields_MatchRenderer(int pages, int rows)
        {
            const string all = "{ページ番号:000}/{総ページ数:0}|{今日}|{今日:yyyy年M月d日（ddd）}|{今日:ggy年M月d日}|{和暦}|{和暦年}"
                + "|{現在日時}|{現在時刻}|{現在時刻:HH時mm分}|{今年}|{今年:0000}|{今月:00}|{期限:ggy年M月}|{空の日付:ggy年}";
            var r = new ReportDefinition { Name = "組み込み" };
            r.Fields.Add(new FieldDefinition { Name = "期限", Kind = FieldKind.Date, SampleValue = "1989/1/7" });
            r.Fields.Add(new FieldDefinition { Name = "空の日付", Kind = FieldKind.Date, SampleValue = "" });
            var list = new ListDefinition { Name = "行" };
            list.Columns.Add(new FieldDefinition { Name = "今日" });
            for (int i = 0; i < rows; i++) list.SampleRows.Add(new List<string> { "列の値" + i });
            r.Lists.Add(list);
            r.Common.Elements.Add(new TextElement { Text = "共通 " + all, X = 5, Y = 5, Width = 200, Height = 12, FontSize = 7 });
            for (int p = 0; p < pages; p++)
            {
                if (p > 0) r.Pages.Add(new ReportPage());
                r.Pages[p].Elements.Add(new TextElement { Text = all, X = 5, Y = 20, Width = 200, Height = 12, FontSize = 7 });
            }
            // 一覧の列と同じ名前なら、くり返し行では列の値を使う
            var table = TableElement.Create(2, 1, 60, 6);
            table.X = 5;
            table.Y = 40;
            table.Rows[0].Cells[0].Text = "{今日}";
            table.DetailSource = "行";
            table.DetailRowCount = 4;
            table.DetailRow.Cells[0].Text = "{今日}";
            table.DetailRow.Cells[1].Text = "{ページ番号:00}";
            r.Pages[pages - 1].Elements.Add(table);
            if (!TestFonts.Apply(r)) return;
            var pdf = AssertSameAsRenderer(r);
            Assert.Equal(pages - 1 + Math.Max(1, (rows + 3) / 4), pdf.Pages.Count);
            string code = CSharpGenerator.Generate(r);
            Assert.Contains("public static PdfDocument Create(組み込みData data, DateTime now)", code);
            Assert.Contains("FormatJapaneseDate(now.Date, \"ggy年M月d日\")", code);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        public void ComputedFields_MatchRenderer(int rows)
        {
            var r = new ReportDefinition { Name = "計算" };
            r.Fields.Add(new FieldDefinition { Name = "名前", SampleValue = "山田 太郎" });
            r.Fields.Add(new FieldDefinition { Name = "締め日", Kind = FieldKind.Date, SampleValue = "2026/3/31" });
            r.Fields.Add(new FieldDefinition { Name = "先月", Kind = FieldKind.Number, Format = "00", Script = "now.AddMonths(-1).Month" });
            r.Fields.Add(new FieldDefinition { Name = "合計", Kind = FieldKind.Number, Format = "#,0", Script = "decimal sum = 0;\r\nforeach (var x in data.行)\r\n    sum += x.値;\r\nreturn sum;" });
            r.Fields.Add(new FieldDefinition { Name = "期限", Kind = FieldKind.Date, Format = "ggy年M月d日", Script = "data.締め日.AddDays(30);" });
            r.Fields.Add(new FieldDefinition { Name = "宛名", Kind = FieldKind.Text, Script = "data.名前 + \" 様\"" });
            r.Fields.Add(new FieldDefinition { Name = "件数", Kind = FieldKind.Text, Script = "data.行.Count" });
            r.Fields.Add(new FieldDefinition { Name = "空", Kind = FieldKind.Number, Script = "" });
            var list = new ListDefinition { Name = "行" };
            list.Columns.Add(new FieldDefinition { Name = "値", Kind = FieldKind.Number });
            for (int i = 0; i < rows; i++) list.SampleRows.Add(new List<string> { (i * 1000 + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            r.Lists.Add(list);
            const string text = "{宛名}|{先月}月|{合計}|{合計:0.0}|{期限}|{件数}|{空}";
            r.Common.Elements.Add(new TextElement { Text = "共通 " + text, X = 5, Y = 5, Width = 200, Height = 8 });
            r.Pages[0].Elements.Add(new TextElement { Text = text, X = 5, Y = 20, Width = 200, Height = 8 });
            var table = TableElement.Create(1, 1, 60, 6);
            table.X = 5;
            table.Y = 40;
            table.Rows[0].Cells[0].Text = "{合計}";
            table.DetailSource = "行";
            table.DetailRowCount = 3;
            table.DetailRow.Cells[0].Text = "{値} / {合計}";
            r.Pages[0].Elements.Add(table);
            if (!TestFonts.Apply(r)) return;

            var warnings = new List<string>();
            var data = ReportData.FromSamples(r, warnings, FixedNow, runScripts: true);
            Assert.Empty(warnings);
            Assert.Equal(4m, data.Values["先月"]);
            Assert.Equal(new DateTime(2026, 4, 30), data.Values["期限"]);
            Assert.Equal("山田 太郎 様", data.Values["宛名"]);
            Assert.Equal(rows.ToString(), data.Values["件数"]);

            AssertSameAsRenderer(r);
            string code = CSharpGenerator.Generate(r);
            Assert.Contains("private static decimal Compute先月(計算Data data, DateTime now)", code);
            // 計算する項目は、データのクラスに入れない
            Assert.DoesNotContain("public decimal 先月", code);
            Assert.DoesNotContain("先月", CSharpGenerator.DataExample(r));
        }

        [Fact]
        public void ComputedFields_ReportErrorsWithoutThrowing()
        {
            var r = new ReportDefinition();
            r.Fields.Add(new FieldDefinition { Name = "間違い", Kind = FieldKind.Number, Script = "1 +\r\nnow.ないメソッド()" });
            r.Fields.Add(new FieldDefinition { Name = "例外", Kind = FieldKind.Number, Script = "int.Parse(\"abc\")" });
            var warnings = new List<string>();
            var data = ReportData.FromSamples(r, warnings, FixedNow, runScripts: true);
            Assert.Contains(warnings, w => w.StartsWith("差し込み項目「間違い」の C# の式（2 行目）"));
            Assert.False(data.Values.ContainsKey("間違い"));

            // コンパイルできれば、実行時の例外も問題として報告する
            r.Fields.RemoveAt(0);
            warnings.Clear();
            ReportData.FromSamples(r, warnings, FixedNow, runScripts: true);
            Assert.Contains(warnings, w => w.Contains("「例外」") && w.Contains("FormatException"));
        }

        [Fact]
        public void ComputedFields_DoNotRunUnlessAllowed()
        {
            string marker = Path.Combine(Path.GetTempPath(), "ChohyoPdfDesignerScript-" + Guid.NewGuid().ToString("N") + ".txt");
            var r = new ReportDefinition();
            r.Fields.Add(new FieldDefinition { Name = "書く", Kind = FieldKind.Text, Script = "System.IO.File.WriteAllText(@\"" + marker + "\", \"x\"); return \"書いた\";" });
            var data = ReportData.FromSamples(r, null, FixedNow);
            Assert.False(File.Exists(marker));
            Assert.False(data.Values.ContainsKey("書く"));
            data = ReportData.FromSamples(r, null, FixedNow, runScripts: true);
            Assert.True(File.Exists(marker));
            Assert.Equal("書いた", data.Values["書く"]);
            File.Delete(marker);
        }

        [Fact]
        public void DesignWithoutDateFields_HasNoNowParameter()
        {
            string code = CSharpGenerator.Generate(SampleReports.CreateInvoice());
            Assert.DoesNotContain("DateTime now", code);
            Assert.DoesNotContain("JapaneseCulture", code);
        }

        [Fact]
        public void PagesWithoutRepeatingRows_UseFixedPageNumbers()
        {
            var report = new ReportDefinition { Name = "3 ページ" };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) report.Pages.Add(new ReportPage { Name = "ページ" + i });
                report.Pages[i].Elements.Add(new TextElement { Text = "{ページ番号} / {総ページ数}", X = 10, Y = 10 + i * 10, Width = 50, Height = 8 });
            }
            if (!TestFonts.Apply(report)) return;
            var pdf = AssertSameAsRenderer(report);
            Assert.Equal(3, pdf.Pages.Count);
            string code = CSharpGenerator.Generate(report);
            // ページが増えないので、ページ番号は決まった文字列になる
            Assert.Contains("\"2\" + \" / \" + \"3\"", code);
            Assert.DoesNotContain("pageNumber", code);
        }

        [Fact]
        public void SameListOnTwoPages_EachTableStartsFromFirstItem()
        {
            var report = SampleReports.CreateInvoice();
            if (!TestFonts.Apply(report)) return;
            var copy = new ReportPage { Name = "控え" };
            foreach (var e in ReportSerializer.ElementsFromJson(ReportSerializer.ElementsToJson(report.Pages[0].Elements))) copy.Elements.Add(e);
            report.Pages.Add(copy);
            var pdf = AssertSameAsRenderer(report);
            // 18 件を 1 ページ 15 行で描くので、どちらのページも 2 ページになる
            Assert.Equal(4, pdf.Pages.Count);
        }

        [Fact]
        public void AllElements_MatchesRenderer()
        {
            var report = AllElements();
            if (!TestFonts.Apply(report)) return;
            AssertSameAsRenderer(report);
        }

        [Fact]
        public void UnusualNames_MatchRenderer()
        {
            var report = new ReportDefinition { Name = "1 帳票 (テスト)", PaperName = PaperSizes.Custom, CustomWidth = 150, CustomHeight = 100 };
            report.Fields.Add(new FieldDefinition { Name = "class", SampleValue = "予約語" });
            report.Fields.Add(new FieldDefinition { Name = "2行目 の値", SampleValue = "数字で始まる\n改行と\"引用符\"と\\" });
            report.Fields.Add(new FieldDefinition { Name = "2行目_の値", SampleValue = "識別子が重なる" });
            report.Fields.Add(new FieldDefinition { Name = "CreateSample", Kind = FieldKind.Number, SampleValue = "-1234.5" });
            report.Fields.Add(new FieldDefinition { Name = "ページ番号", SampleValue = "組み込みの名前より項目を優先" });
            report.Fields.Add(new FieldDefinition { Name = "日時", Kind = FieldKind.Date, SampleValue = "2026-10-20 13:45:06" });
            var list = new ListDefinition { Name = "list" };
            list.Columns.Add(new FieldDefinition { Name = "Item", Kind = FieldKind.Text });
            list.Columns.Add(new FieldDefinition { Name = "listItem", Kind = FieldKind.Number });
            list.SampleRows.Add(new List<string> { "a", "1" });
            list.SampleRows.Add(new List<string> { "b" });
            report.Lists.Add(list);
            report.Pages[0].Elements.Add(new TextElement
            {
                X = 5, Y = 5, Width = 140, Height = 40,
                Text = "{class}/{2行目 の値}/{2行目_の値}/{CreateSample}/{CreateSample:0.000}/{ページ番号}/{総ページ数}/{日時:yyyy-MM-dd HH:mm:ss}/{{括弧}}/{不明な項目}/{ /{閉じていない",
            });
            var table = TableElement.Create(2, 1);
            table.X = 5;
            table.Y = 50;
            table.Rows[0].Cells[0].Text = "{Item}";
            table.DetailSource = "list";
            table.DetailRowCount = 3;
            table.DetailRow.Cells[0].Text = "{Item}{class}";
            table.DetailRow.Cells[1].Text = "{listItem}";
            report.Pages[0].Elements.Add(table);
            if (!TestFonts.Apply(report)) return;
            AssertSameAsRenderer(report);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void RichTextAndLists_MatchRenderer(int rows)
        {
            var report = RichText(rows);
            if (!TestFonts.Apply(report)) return;
            string code = CSharpGenerator.Generate(report, new CodeGenOptions { Namespace = "Generated.Test" });
            Assert.Contains("AddSuperscript(\"2\")", code);
            Assert.Contains("AddRubyText(", code);
            Assert.Contains(".AddSubList();", code);
            AssertSameAsRenderer(report);
        }

        /// <summary>上付き・下付き・ルビの記法と、箇条書きを使ったデザイン。</summary>
        internal static ReportDefinition RichText(int rows)
        {
            var r = new ReportDefinition { Name = "記法" };
            r.Fields.Add(new FieldDefinition { Name = "氏名", SampleValue = "山田 太郎" });
            r.Fields.Add(new FieldDefinition { Name = "フリガナ", SampleValue = "やまだ たろう" });
            r.Fields.Add(new FieldDefinition { Name = "空", SampleValue = "" });
            var list = new ListDefinition { Name = "明細" };
            list.Columns.Add(new FieldDefinition { Name = "品名" });
            list.Columns.Add(new FieldDefinition { Name = "面積", Kind = FieldKind.Number });
            for (int i = 0; i < rows; i++) list.SampleRows.Add(new List<string> { "品目" + i, (i + 10).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            r.Lists.Add(list);

            var elements = r.Pages[0].Elements;
            elements.Add(new TextElement { Text = "面積 10 m<sup>2</sup>、水は H<sub>2</sub>O。\n｜東京《とうきょう》と｜{氏名}《{フリガナ}》｜{空}《から》<sup></sup>", X = 10, Y = 10, Width = 90, Height = 20, LineSpacing = 1.4, Alignment = TextAlignment.Center });
            elements.Add(new TextElement { Text = "閉じていない <sup>2 と《ルビ》は、そのまま", X = 10, Y = 32, Width = 90, Height = 8 });
            elements.Add(new TextElement
            {
                Text = "一つ目の項目\n\t入れ子の項目 x<sup>2</sup>\n\t\t\tさらに深い項目\n\t同じ深さ\n<sub>下付きで始まる</sub>項目\n\n最後",
                X = 110, Y = 10, Width = 80, Height = 50, ListStyle = TextListStyle.Bullet, Bullet = "●", ShrinkToFit = true,
            });
            elements.Add(new TextElement { Text = "第一条\n第二条\n\t細目", X = 110, Y = 65, Width = 80, Height = 25, ListStyle = TextListStyle.Numbered, NumberStyle = ListNumberStyle.KanjiParenthesized, Wrap = false, VerticalAlignment = VerticalAlignment.Middle, BorderColor = "#000" });
            elements.Add(new TextElement { Text = "1 行目\n2 行目", X = 10, Y = 45, Width = 40, Height = 20, ListStyle = TextListStyle.Numbered, NumberStyle = ListNumberStyle.UpperRoman, Padding = 2 });

            var t = TableElement.Create(2, 1, 40, 8);
            t.X = 10;
            t.Y = 100;
            t.Rows[0].Cells[0].Text = "面積（m<sup>2</sup>）";
            t.Rows[0].Cells[1].Text = "普通の見出し";
            t.DetailSource = "明細";
            t.DetailRowCount = 4;
            t.DetailRow.Cells[0].Text = "｜{品名}《ひんめい》";
            t.DetailRow.Cells[1].Text = "{面積} m<sup>2</sup>";
            elements.Add(t);

            var t2 = TableElement.Create(1, 1, 40, 8);
            t2.X = 110;
            t2.Y = 100;
            t2.BorderColor = null;
            t2.Rows[0].Cells[0].Text = "CO<sub>2</sub>";
            elements.Add(t2);
            return r;
        }

        [Fact]
        public void GeneratedCode_WithoutNamespace_Compiles()
        {
            var report = SampleReports.CreateInvoice();
            string code = CSharpGenerator.Generate(report, new CodeGenOptions { Namespace = "", IncludeSampleData = false });
            var warnings = new List<string>();
            Compiler.Compile(code, LanguageVersion.CSharp7_3, false, warnings);
            Assert.Empty(warnings);
            Assert.DoesNotContain("CreateSample", code);
        }

        /// <summary>すべての種類の部品と、主な設定を使ったデザイン。</summary>
        internal static ReportDefinition AllElements()
        {
            var r = new ReportDefinition { Name = "全部品", PaperName = "B5", Landscape = true };
            r.Fields.Add(new FieldDefinition { Name = "名前", SampleValue = "山田 太郎" });
            r.Fields.Add(new FieldDefinition { Name = "金額", Kind = FieldKind.Number, SampleValue = "1234567.891" });
            r.Fields.Add(new FieldDefinition { Name = "件数", Kind = FieldKind.Number, SampleValue = "1234" });
            var list = new ListDefinition { Name = "行" };
            list.Columns.Add(new FieldDefinition { Name = "名前" });
            list.Columns.Add(new FieldDefinition { Name = "値", Kind = FieldKind.Number, Format = "0.0" });
            for (int i = 0; i < 7; i++) list.SampleRows.Add(new List<string> { "行" + i, (i * 1.25).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            r.Lists.Add(list);

            r.Pages[0].Elements.Add(new TextElement
            {
                Text = "{名前} 様　金額 {金額} 円（{金額:#,0.0000}）{件数} 件", X = 10.25, Y = 10.5, Width = 80, Height = 20,
                FontSize = 12.5, Bold = true, Italic = true, Underline = true, Strikeout = true, Color = "#336699",
                BackgroundColor = "#FFF0F0", Alignment = TextAlignment.Justify, VerticalAlignment = VerticalAlignment.Bottom,
                LineSpacing = 1.5, CharacterSpacing = 0.3, BorderColor = "#FF0000", BorderWidth = 1.2, BorderDash = DashStyle.Dash, Padding = 1.5,
            });
            r.Pages[0].Elements.Add(new TextElement { Text = "均等割り付け", X = 100, Y = 10, Width = 50, Height = 8, Alignment = TextAlignment.Distribute, BackgroundColor = "#EEEEEE" });
            r.Pages[0].Elements.Add(new TextElement { Text = "縮小して全体を表示する長い長い文字列", X = 100, Y = 20, Width = 30, Height = 6, ShrinkToFit = true, Wrap = false, BorderColor = "#000" });
            r.Pages[0].Elements.Add(new TextElement { Text = "", X = 100, Y = 30, Width = 30, Height = 6, BorderColor = "#000" });
            r.Pages[0].Elements.Add(new TextElement { Text = "余白が大きすぎる", X = 100, Y = 40, Width = 4, Height = 4, Padding = 3 });
            r.Pages[0].Elements.Add(new TextElement { Text = "{名前}", X = 160, Y = 10, Width = 40, Height = 8, Visibility = PageVisibility.LastPage });
            r.Pages[0].Elements.Add(new LineElement { X1 = 10, Y1 = 40, X2 = 90, Y2 = 45.5, Dash = DashStyle.Dot, StrokeWidth = 2, StrokeColor = "#00AA00" });
            r.Pages[0].Elements.Add(new LineElement { X1 = 10, Y1 = 48, X2 = 90, Y2 = 48, StrokeWidth = 0 });
            r.Pages[0].Elements.Add(new RectangleElement { X = 10, Y = 50, Width = 30, Height = 15, FillColor = "#DDEEFF", CornerRadius = 3, StrokeColor = null });
            r.Pages[0].Elements.Add(new RectangleElement { X = 45, Y = 50, Width = 30, Height = 15, Dash = DashStyle.Dash });
            r.Pages[0].Elements.Add(new RectangleElement { X = 80, Y = 50, Width = 30, Height = 15, FillColor = "#FFEECC80", StrokeColor = null });
            r.Pages[0].Elements.Add(new RectangleElement { X = 80, Y = 50, Width = 30, Height = 15, StrokeColor = null });
            r.Pages[0].Elements.Add(new EllipseElement { X = 115, Y = 50, Width = 20, Height = 15, FillColor = "#FF000040" });
            r.Pages[0].Elements.Add(new ImageElement { Path = "logo.png", X = 140, Y = 50, Width = 30, Height = 15 });
            r.Pages[0].Elements.Add(new ImageElement { Path = "logo.png", X = 175, Y = 50, Width = 30, Height = 15, Fit = ImageFit.Stretch, Visibility = PageVisibility.FirstPage });
            r.Pages[0].Elements.Add(new ImageElement { Path = "photo.jpg", X = 210, Y = 50, Width = 30, Height = 15 });
            r.Pages[0].Elements.Add(new ImageElement { Path = "", X = 210, Y = 70, Width = 30, Height = 15 });

            var t = TableElement.Create(4, 3, 20, 6.5);
            t.X = 10;
            t.Y = 75.3;
            t.BorderColor = null;
            t.Wrap = true;
            t.ShrinkToFit = false;
            t.CellPadding = 0.7;
            t.VerticalAlignment = VerticalAlignment.Top;
            t.Columns[1].Width = 33.3;
            t.Columns[3].Alignment = TextAlignment.Right;
            t.Rows[0].Cells[0].Text = "横に 2 列結合";
            t.Rows[0].Cells[0].ColumnSpan = 2;
            t.Rows[0].Cells[2].Text = "縦横に結合";
            t.Rows[0].Cells[2].ColumnSpan = 2;
            t.Rows[0].Cells[2].RowSpan = 2;
            t.Rows[0].Cells[2].BackgroundColor = "#E0E0E0";
            t.Rows[0].Cells[2].VerticalAlignment = VerticalAlignment.Middle;
            t.Rows[0].Cells[2].Alignment = TextAlignment.Center;
            t.Rows[1].Cells[0].Text = "{名前}";
            t.Rows[1].Cells[0].Color = "#0000FF";
            t.Rows[1].Cells[0].FontSize = 7;
            t.Rows[1].Cells[1].Text = "隠れていない";
            t.Rows[1].Cells[1].Bold = true;
            t.Rows[2].Cells[0].Text = "結合しすぎ";
            t.Rows[2].Cells[0].ColumnSpan = 9;
            t.Rows[2].Cells[0].RowSpan = 9;
            t.DetailSource = "行";
            t.DetailRowCount = 4;
            t.AlternateRowColor = "#F0F8FF";
            t.DetailRow.Cells[0].Text = "{名前} / {名前}";
            t.DetailRow.Cells[0].ColumnSpan = 3;
            t.DetailRow.Cells[3].Text = "{値}";
            t.DetailRow.Cells[3].BackgroundColor = "#FFFFE0";
            r.Pages[0].Elements.Add(t);

            var t2 = TableElement.Create(2, 0);
            t2.X = 150;
            t2.Y = 75;
            t2.DetailSource = "存在しない一覧";
            t2.DetailRowCount = 2;
            t2.DetailRow.Cells[0].Text = "{名前}";
            r.Pages[0].Elements.Add(t2);
            return r;
        }

        /// <summary>生成コードをコンパイル・実行し、デザイナーの PDF 出力と比べる。</summary>
        private static PdfDocument AssertSameAsRenderer(ReportDefinition report)
        {
            string code = CSharpGenerator.Generate(report, new CodeGenOptions { Namespace = "Generated.Test" });

            // C# 7.3（.NET Framework の既定）と、null 許容参照型を有効にした最新の C# のどちらでも警告が出ないこと
            var warnings = new List<string>();
            Compiler.Compile(code, LanguageVersion.CSharp7_3, false, warnings);
            var assembly = Compiler.Compile(code, LanguageVersion.Latest, true, warnings);
            Assert.True(warnings.Count == 0, string.Join("\n", warnings) + "\n\n" + code);

            var reportType = assembly.GetTypes().Single(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name == "Create"));
            var dataType = assembly.GetTypes().Single(t => t.GetMethod("CreateSample", BindingFlags.Public | BindingFlags.Static) != null);
            reportType.GetProperty("ImageFolder")?.SetValue(null, TestData.Folder);
            object data = dataType.GetMethod("CreateSample")!.Invoke(null, null)!;
            // {今日} などを使うときは、日時を受け取る Create がある。同じ日時をデザイナーにも渡す
            var withNow = reportType.GetMethod("Create", new[] { dataType, typeof(DateTime) });
            var actual = (PdfDocument)(withNow != null
                ? withNow.Invoke(null, new object[] { data, FixedNow })!
                : reportType.GetMethod("Create", new[] { dataType })!.Invoke(null, new[] { data })!);
            actual.Info.CreationDate = Fixed;

            var warningsFromRenderer = new List<string>();
            var expected = ReportRenderer.RenderSample(report, TestData.Folder, warningsFromRenderer, FixedNow, runScripts: true);
            expected.Info.CreationDate = Fixed;

            byte[] a = actual.ToArray(), e = expected.ToArray();
            if (!a.SequenceEqual(e))
            {
                string dir = Path.Combine(Path.GetTempPath(), "ChohyoPdf.Designer.Tests");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "expected.pdf"), e);
                File.WriteAllBytes(Path.Combine(dir, "actual.pdf"), a);
                File.WriteAllText(Path.Combine(dir, "generated.cs"), code);
                Assert.Fail("生成コードの PDF がデザイナーの出力と一致しません（" + dir + " に保存しました）。");
            }
            return actual;
        }
    }
}
