using System;
using System.Collections.Generic;
using System.Linq;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    public class ModelTests
    {
        [Fact]
        public void Json_RoundTrip_KeepsEverything()
        {
            var report = CodeGenTests.AllElements();
            string json = ReportSerializer.ToJson(report);
            var loaded = ReportSerializer.FromJson(json);
            Assert.Equal(json, ReportSerializer.ToJson(loaded));

            var table = loaded.Pages[0].Elements.OfType<TableElement>().First();
            Assert.Equal(4, table.Columns.Count);
            Assert.Equal(33.3, table.Columns[1].Width);
            Assert.Equal(2, table.Rows[0].Cells[2].RowSpan);
            Assert.Equal("行", table.DetailSource);
            Assert.Contains("\"type\": \"table\"", json);
            // 計算で求める値（表の幅など）は保存しない
            Assert.DoesNotContain("\"bounds\"", json);
            Assert.DoesNotContain("\"kindName\"", json);
            // 日本語はエスケープしない
            Assert.Contains("山田 太郎", json);
        }

        [Fact]
        public void Json_Version1_MovesElementsToFirstPage()
        {
            string json = "{ \"formatVersion\": 1, \"name\": \"古い形式\", \"elements\": ["
                + "{ \"type\": \"text\", \"name\": \"見出し\", \"text\": \"{顧客名} 様\", \"x\": 10, \"y\": 20 },"
                + "{ \"type\": \"line\", \"x1\": 0, \"y1\": 0, \"x2\": 10, \"y2\": 0 } ] }";
            var report = ReportSerializer.FromJson(json);
            var page = Assert.Single(report.Pages);
            Assert.Equal(2, page.Elements.Count);
            Assert.Equal("見出し", page.Elements[0].Name);
            Assert.Equal(ReportDefinition.CurrentFormatVersion, report.FormatVersion);

            // 保存し直すと新しい形式になり、古い "elements" は書かない
            string saved = ReportSerializer.ToJson(report);
            Assert.Contains("\"pages\"", saved);
            using (var doc = System.Text.Json.JsonDocument.Parse(saved))
                Assert.False(doc.RootElement.TryGetProperty("elements", out _));
            Assert.Equal(saved, ReportSerializer.ToJson(ReportSerializer.FromJson(saved)));
        }

        [Fact]
        public void Json_RoundTrip_KeepsCommonElementsAndPaper()
        {
            var report = SampleReports.CreateDocument();
            report.Pages[1].Paper = new PaperSetting { PaperName = "B5", Landscape = true };
            report.Pages[2].HideCommon = true;
            string json = ReportSerializer.ToJson(report);
            var loaded = ReportSerializer.FromJson(json);
            Assert.Equal(json, ReportSerializer.ToJson(loaded));
            Assert.Equal(3, loaded.Common.Elements.Count);
            Assert.Equal(PageVisibility.ExceptFirstPage, loaded.Common.Elements[0].Visibility);
            Assert.Equal(257, loaded.PageWidthOf(loaded.Pages[1]));
            Assert.Equal(210, loaded.PageWidthOf(loaded.Pages[0]));
            Assert.True(loaded.Pages[2].HideCommon);
            // 既定の値（共通の部品を描く）は書かない
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "hideCommon"));
        }

        [Fact]
        public void Renderer_DesignLayers_DrawOnePagePerDesignPage()
        {
            var report = SampleReports.CreateDocument();
            if (!TestFonts.Apply(report)) return;
            report.Pages[1].Paper = new PaperSetting { PaperName = "A5", Landscape = true };
            foreach (var layer in new[] { DesignLayer.None, DesignLayer.Page, DesignLayer.Common })
            {
                var all = ReportRenderer.Render(report, RenderMode.Design, null, TestData.Folder, null, null, layer);
                Assert.Equal(3, all.Pages.Count);
                var one = ReportRenderer.Render(report, RenderMode.Preview, ReportData.FromSamples(report), TestData.Folder, null, 1, layer);
                Assert.Single(one.Pages);
                // 共通の部品を編集しているときは、文書の用紙に描く
                Assert.Equal(layer == DesignLayer.Common ? PageSize.A4.Width : PageSize.A5.Landscape.Width, one.Pages[0].Width, 3);
            }
        }

        [Fact]
        public void Validator_WarnsAboutRepeatingRowsInCommonElements()
        {
            var report = new ReportDefinition();
            report.Lists.Add(new ListDefinition { Name = "一覧" });
            var table = TableElement.Create(1, 1);
            table.DetailSource = "一覧";
            report.Common.Elements.Add(table);
            Assert.Contains(ReportValidator.Validate(report), m => m.StartsWith("全ページ共通の表") && m.Contains("くり返せません"));
        }

        [Fact]
        public void Json_WithoutPages_HasOneEmptyPage()
        {
            var report = ReportSerializer.FromJson("{ \"formatVersion\": 2, \"pages\": [] }");
            Assert.Single(report.Pages);
            Assert.Throws<System.IO.InvalidDataException>(() => ReportSerializer.FromJson("{ \"formatVersion\": 99 }"));
        }

        [Fact]
        public void Template_ParsesFieldsFormatsAndEscapes()
        {
            var s = TextTemplate.Parse("合計 {金額:#,0} 円 {{注}} {名前}{ 未完");
            Assert.Collection(s,
                x => { Assert.False(x.IsField); Assert.Equal("合計 ", x.Text); },
                x => { Assert.True(x.IsField); Assert.Equal("金額", x.Text); Assert.Equal("#,0", x.Format); },
                x => { Assert.False(x.IsField); Assert.Equal(" 円 {注} ", x.Text); },
                x => { Assert.True(x.IsField); Assert.Equal("名前", x.Text); Assert.Null(x.Format); },
                x => { Assert.False(x.IsField); Assert.Equal("{ 未完", x.Text); });
            Assert.False(TextTemplate.HasFields("{}"));
            Assert.Equal("{{a}}", TextTemplate.Escape("{a}"));
        }

        [Fact]
        public void Template_FormatsValuesInJapaneseCulture()
        {
            Assert.Equal("1,234,568", TextTemplate.FormatValue(1234567.6m, FieldKind.Number, "#,0"));
            // 書式がなければ、数値はそのまま（3 桁区切りにしない）
            Assert.Null(TextTemplate.EffectiveFormat(FieldKind.Number, null, null));
            Assert.Equal("1234", TextTemplate.FormatValue(1234m, FieldKind.Number, null));
            Assert.Equal("1234.5", TextTemplate.FormatValue(1234.5m, FieldKind.Number, null));
            Assert.True(TextTemplate.TryParseSample("2026/1/5", FieldKind.Date, out object date));
            Assert.Equal("2026年1月5日", TextTemplate.FormatValue(date, FieldKind.Date, "yyyy年M月d日"));
            Assert.True(TextTemplate.TryParseSample("¥1,200", FieldKind.Number, out object n));
            Assert.Equal(1200m, n);
            Assert.False(TextTemplate.TryParseSample("abc", FieldKind.Number, out _));
        }

        [Fact]
        public void Template_FormatsBuiltInsAndJapaneseEra()
        {
            var now = new DateTime(2026, 10, 21, 9, 5, 0);
            string Format(string name, string? format, int page = 3, int count = 12)
                => TextTemplate.FormatBuiltIn(TextTemplate.FindBuiltIn(name)!, format, now, page, count);
            Assert.Equal("003", Format("ページ番号", "000"));
            Assert.Equal("3", Format("ページ番号", null));
            Assert.Equal("12", Format("総ページ数", null));
            Assert.Equal("2026/10/21", Format("今日", null));
            Assert.Equal("2026年10月21日（水）", Format("今日", "yyyy年M月d日（ddd）"));
            Assert.Equal("2026/10/21 09:05", Format("現在日時", null));
            Assert.Equal("9:05", Format("現在時刻", null));
            Assert.Equal("令和8年10月21日", Format("和暦", null));
            Assert.Equal("令和8年", Format("和暦年", null));
            Assert.Equal("2026", Format("今年", null));
            Assert.Equal("10", Format("今月", null));
            Assert.Equal("10", Format("今月", "00"));
            Assert.Equal("03", Format("ページ番号", "00"));

            // 元号の変わり目と元年。明治より前（値のない日付を含む）は空文字列
            Assert.Equal("平成31年4月30日", TextTemplate.FormatDate(new DateTime(2019, 4, 30), "ggy年M月d日"));
            Assert.Equal("令和元年5月1日", TextTemplate.FormatDate(new DateTime(2019, 5, 1), "ggy年M月d日"));
            Assert.Equal("", TextTemplate.FormatDate(default, "ggy年"));
            Assert.Equal("0001/01/01", TextTemplate.FormatDate(default, "yyyy/MM/dd"));
            Assert.Equal("令和8年10月20日", FormatPresets.Example(FieldKind.Date, "ggy年M月d日", null));
        }

        [Fact]
        public void Table_OwnerMap_FollowsMerges()
        {
            var t = TableElement.Create(3, 3);
            t.Rows[0].Cells[0].ColumnSpan = 2;
            t.Rows[1].Cells[1].RowSpan = 5;   // 固定の行の範囲で切り詰める
            t.DetailSource = "x";
            t.DetailRow.Cells[1].ColumnSpan = 2;
            var map = t.GetOwnerMap();
            Assert.Equal((0, 0), map[0, 1]);
            Assert.Equal((0, 2), map[0, 2]);
            Assert.Equal((1, 1), map[2, 1]);
            Assert.Equal((3, 1), map[3, 2]);
            Assert.True(t.IsCovered(2, 1));
            Assert.False(t.IsCovered(2, 2));
        }

        [Fact]
        public void Table_SizeFollowsColumnsAndRows()
        {
            var t = TableElement.Create(2, 1, 10, 5);
            Assert.Equal(20, t.Width);
            Assert.Equal(5, t.Height);
            t.DetailSource = "x";
            t.DetailRowCount = 4;
            Assert.Equal(25, t.Height);
            t.Width = 40;
            Assert.Equal(20, t.Columns[0].Width);
        }

        [Fact]
        public void Identifiers_AreValidCSharp()
        {
            Assert.Equal("顧客名", Identifiers.From("顧客名", "x"));
            Assert.Equal("_1行目", Identifiers.From("1行目", "x"));
            Assert.Equal("@class", Identifiers.From("class", "x"));
            Assert.Equal("ご請求_金額", Identifiers.From("ご請求 金額", "x"));
            Assert.Equal("x", Identifiers.From("!!", "x"));
            Assert.Equal("YuGothicRegular", Identifiers.PascalAscii("Yu Gothic Regular", "x"));
        }

        [Fact]
        public void Renderer_DesignMode_ReportsProblemsWithoutThrowing()
        {
            var report = new ReportDefinition { DefaultFontName = "存在しないフォント" };
            report.Pages[0].Elements.Add(new TextElement { Text = "{x}" });
            report.Pages[0].Elements.Add(new ImageElement { Path = "存在しない.png" });
            var warnings = new List<string>();
            var pdf = ReportRenderer.Render(report, RenderMode.Design, null, TestData.Folder, warnings);
            Assert.Single(pdf.Pages);
            Assert.Contains(warnings, w => w.Contains("存在しないフォント"));
            Assert.Contains(warnings, w => w.Contains("存在しない.png"));
        }
    }
}
