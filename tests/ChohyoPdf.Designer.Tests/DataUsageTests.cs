using System.Linq;
using ChohyoPdf.Designer.Model;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    /// <summary>差し込み項目・一覧を用紙で使っている数（「差し込み」タブの使用数と「用紙から探す」）。</summary>
    public class DataUsageTests
    {
        private static ReportDefinition Report()
        {
            var report = new ReportDefinition();
            report.Fields.Add(new FieldDefinition { Name = "氏名" });
            report.Fields.Add(new FieldDefinition { Name = "金額" });
            var list = new ListDefinition { Name = "明細" };
            list.Columns.Add(new FieldDefinition { Name = "品名" });
            list.Columns.Add(new FieldDefinition { Name = "金額" });
            report.Lists.Add(list);
            report.Lists.Add(new ListDefinition { Name = "名簿" });
            return report;
        }

        [Fact]
        public void CountField_CountsTextAndFormats()
        {
            var report = Report();
            report.Pages[0].Elements.Add(new TextElement { Text = "{氏名} 様（{氏名:}）{{氏名}} {氏名さん}" });
            report.Pages[0].Elements.Add(new TextElement { Text = "合計 {金額:#,0} 円" });
            Assert.Equal(2, DataUsage.CountField(report, report.Fields[0]));
            Assert.Equal(1, DataUsage.CountField(report, report.Fields[1]));
        }

        [Fact]
        public void CountField_TablesAndCommonElements()
        {
            var report = Report();
            report.Pages.Add(new ReportPage());
            var table = TableElement.Create(2, 2);
            table.Rows[0].Cells[0].Text = "{氏名}";
            table.Rows[1].Cells[1].Text = "{金額}";
            // くり返し行では、一覧の列「金額」が先に使われる
            table.DetailSource = "明細";
            table.DetailRow.Cells[0].Text = "{氏名}";
            table.DetailRow.Cells[1].Text = "{金額}";
            report.Pages[1].Elements.Add(table);
            report.Common.Elements.Add(new TextElement { Text = "{氏名}" });

            Assert.Equal(3, DataUsage.CountField(report, report.Fields[0]));
            Assert.Equal(1, DataUsage.CountField(report, report.Fields[1]));
            var locations = DataUsage.FindField(report, report.Fields[0]);
            Assert.Equal(new[] { -1, 1 }, locations.Select(x => x.PageIndex));
            Assert.True(locations[0].IsCommon);
            Assert.Equal(2, locations[1].Count);

            // くり返さない表のくり返し行は使わない
            table.DetailSource = null;
            Assert.Equal(2, DataUsage.CountField(report, report.Fields[0]));
        }

        [Fact]
        public void CountField_DuplicateNameUsesFirstField()
        {
            var report = Report();
            report.Fields.Add(new FieldDefinition { Name = "氏名" });
            report.Pages[0].Elements.Add(new TextElement { Text = "{氏名}" });
            Assert.Equal(1, DataUsage.CountField(report, report.Fields[0]));
            Assert.Equal(0, DataUsage.CountField(report, report.Fields[2]));
        }

        [Fact]
        public void CountList_CountsRepeatingTablesOnPages()
        {
            var report = Report();
            var a = TableElement.Create(2, 1);
            a.DetailSource = "明細";
            var b = TableElement.Create(2, 1);
            b.DetailSource = "明細";
            var common = TableElement.Create(2, 1);
            common.DetailSource = "明細";
            report.Pages[0].Elements.Add(a);
            report.Pages[0].Elements.Add(b);
            report.Common.Elements.Add(common);

            var counts = DataUsage.CountAll(report);
            Assert.Equal(2, counts[report.Lists[0]]);
            Assert.Equal(0, counts[report.Lists[1]]);
            Assert.Equal(0, counts[report.Fields[0]]);
            Assert.All(DataUsage.FindList(report, report.Lists[0]), x => Assert.Equal(0, x.PageIndex));
        }
    }
}
