using System;
using System.Collections.Generic;
using System.Linq;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    /// <summary>データの設定画面（書式の選択肢・データの渡し方・明細表の作成）で使う機能。</summary>
    public class DataUiTests
    {
        [Fact]
        public void FormatPresets_FormatInJapaneseCulture()
        {
            var numbers = FormatPresets.For(FieldKind.Number).ToDictionary(p => p.Format, p => FormatPresets.Example(FieldKind.Number, p.Format, "1234567.5"));
            Assert.Equal("1234567.5", numbers[""]);
            Assert.Equal("1,234,568", numbers["#,0"]);
            Assert.Equal("1,234,567.50", numbers["#,0.00"]);
            Assert.Equal("¥1,234,568", numbers["¥#,0"]);
            Assert.Equal("1,234,568円", numbers["#,0円"]);
            Assert.Equal("△1,235", FormatPresets.Example(FieldKind.Number, "#,0;△#,0", "-1234.5"));
            Assert.Equal("10%", FormatPresets.Example(FieldKind.Number, "0%", "0.1"));
            Assert.Equal("007", FormatPresets.Example(FieldKind.Number, "000", "7"));

            var dates = FormatPresets.For(FieldKind.Date).ToDictionary(p => p.Format, p => FormatPresets.Example(FieldKind.Date, p.Format, "2026/1/5 9:05"));
            Assert.Equal("2026/01/05", dates[""]);
            Assert.Equal("2026/1/5", dates["yyyy/M/d"]);
            Assert.Equal("2026年1月5日（月）", dates["yyyy年M月d日（ddd）"]);
            Assert.Equal("2026/01/05 09:05", dates["yyyy/MM/dd HH:mm"]);

            // サンプルが読めなければ例の値を使う。書式が正しくなければ null
            Assert.Equal("1,234,568", FormatPresets.Example(FieldKind.Number, "#,0", "abc"));
            Assert.Null(FormatPresets.Example(FieldKind.Date, "%", "2026/1/5"));
            Assert.Empty(FormatPresets.For(FieldKind.Text));
        }

        [Fact]
        public void DataExample_CompilesAgainstGeneratedCode()
        {
            var report = SampleReports.CreateInvoice();
            var shape = CSharpGenerator.DescribeData(report);
            Assert.Equal("請求書Data", shape.DataClass);
            Assert.Equal("顧客名", shape.Fields[report.FindField("顧客名")!].Property);
            Assert.Equal("decimal", shape.Fields[report.FindField("合計")!].Type);
            Assert.Equal("明細Item", shape.Lists[report.Lists[0]].ItemClass);

            string example = CSharpGenerator.DataExample(report);
            Assert.Contains("data.明細.Add(new 明細Item {", example);
            Assert.Contains("請求書Report.Save(data, \"請求書.pdf\");", example);

            string code = CSharpGenerator.Generate(report, new CodeGenOptions { Namespace = "Generated.Example" })
                + "\nnamespace Generated.Example\n{\n    public static class ExampleRunner\n    {\n        public static void Run()\n        {\n"
                + example + "\n        }\n    }\n}\n";
            var warnings = new List<string>();
            Compiler.Compile(code, LanguageVersion.CSharp7_3, false, warnings);
            Assert.Empty(warnings);
        }

        [Fact]
        public void CreateDetailTable_UsesListColumns()
        {
            var report = SampleReports.CreateInvoice();
            var list = report.Lists[0];
            var table = TableEditing.CreateDetailTable(list, 180, 12);
            Assert.Equal(list.Columns.Count, table.Columns.Count);
            Assert.Single(table.Rows);
            Assert.Equal("明細", table.DetailSource);
            Assert.Equal(12, table.DetailRowCount);
            Assert.Equal("品名", table.Rows[0].Cells[1].Text);
            Assert.Equal("{品名}", table.DetailRow.Cells[1].Text);
            Assert.Equal(TextAlignment.Right, table.Columns[2].Alignment);
            Assert.Equal(TextAlignment.Left, table.Columns[1].Alignment);
            Assert.Equal(180, table.Width, 1);
        }

        [Fact]
        public void PreviewMode_DrawsOnlyFirstPageAndToleratesMissingFiles()
        {
            var report = CodeGenTests.AllElements();
            if (!TestFonts.Apply(report)) return;
            report.Pages[0].Elements.OfType<ImageElement>().First().Path = "存在しない画像.png";
            report.Lists[0].SampleRows.AddRange(Enumerable.Range(0, 20).Select(i => new List<string> { "追加" + i, "1" }));
            var warnings = new List<string>();
            var pdf = ReportRenderer.Render(report, RenderMode.Preview, ReportData.FromSamples(report), TestData.Folder, warnings);
            Assert.Single(pdf.Pages);
            Assert.Contains(warnings, w => w.Contains("存在しない画像.png"));
        }
    }
}
