using System.Linq;
using ChohyoPdf.Designer.Model;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    public class TableEditingTests
    {
        private static TableElement Table(int columns, int rows)
        {
            var t = TableElement.Create(columns, rows);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++) t.Rows[r].Cells[c].Text = r + "-" + c;
            }
            return t;
        }

        [Fact]
        public void Merge_KeepsTopLeftTextAndExpandsToOverlappingMerges()
        {
            var t = Table(4, 4);
            TableEditing.Merge(t, new CellRange(1, 1, 2, 2));
            Assert.Equal(2, t.Rows[1].Cells[1].ColumnSpan);
            Assert.Equal(2, t.Rows[1].Cells[1].RowSpan);
            Assert.Equal("1-1", t.Rows[1].Cells[1].Text);

            // 結合に一部だけかかる範囲は、結合全体を含むように広がる
            var expanded = TableEditing.ExpandToMerges(t, new CellRange(2, 2, 2, 3));
            Assert.Equal(new CellRange(1, 1, 2, 3), expanded);
            TableEditing.Merge(t, new CellRange(2, 2, 2, 3));
            Assert.Equal(3, t.Rows[1].Cells[1].ColumnSpan);
            Assert.True(t.IsCovered(2, 3));

            TableEditing.Unmerge(t, new CellRange(2, 2, 2, 2));
            Assert.All(t.Rows.SelectMany(r => r.Cells), c => Assert.Equal(1, c.ColumnSpan * c.RowSpan));
        }

        [Fact]
        public void Merge_InDetailRowIsHorizontalOnly()
        {
            var t = Table(3, 1);
            t.DetailSource = "x";
            TableEditing.Merge(t, new CellRange(1, 0, 1, 1));
            Assert.Equal(2, t.DetailRow.Cells[0].ColumnSpan);
            Assert.Equal(1, t.DetailRow.Cells[0].RowSpan);
            // 固定の行と明細行はまたげない
            Assert.Equal(new CellRange(0, 0, 0, 0), TableEditing.ExpandToMerges(t, new CellRange(0, 0, 1, 0)));
        }

        [Fact]
        public void InsertAndDeleteColumn_AdjustSpans()
        {
            var t = Table(3, 2);
            t.Rows[0].Cells[0].ColumnSpan = 2;
            TableEditing.InsertColumn(t, 1, 15);
            Assert.Equal(4, t.Columns.Count);
            Assert.Equal(15, t.Columns[1].Width);
            Assert.Equal(3, t.Rows[0].Cells[0].ColumnSpan);
            Assert.All(t.Rows.Concat(new[] { t.DetailRow }), r => Assert.Equal(4, r.Cells.Count));

            // 結合の左上のセルを消すと、結合と文字は右隣のセルに引き継ぐ
            TableEditing.DeleteColumn(t, 0);
            Assert.Equal(3, t.Columns.Count);
            Assert.Equal(2, t.Rows[0].Cells[0].ColumnSpan);
            Assert.Equal("0-0", t.Rows[0].Cells[0].Text);
        }

        [Fact]
        public void InsertAndDeleteRow_AdjustSpans()
        {
            var t = Table(2, 3);
            t.Rows[0].Cells[0].RowSpan = 2;
            TableEditing.InsertRow(t, 1, 9);
            Assert.Equal(4, t.Rows.Count);
            Assert.Equal(9, t.Rows[1].Height);
            Assert.Equal(3, t.Rows[0].Cells[0].RowSpan);

            TableEditing.DeleteRow(t, 0);
            Assert.Equal(3, t.Rows.Count);
            Assert.Equal(2, t.Rows[0].Cells[0].RowSpan);
            Assert.Equal("0-0", t.Rows[0].Cells[0].Text);

            // 最後の 1 行は、明細行がなければ消さない
            TableEditing.DeleteRow(t, 0);
            TableEditing.DeleteRow(t, 0);
            TableEditing.DeleteRow(t, 0);
            Assert.Single(t.Rows);
        }
    }
}
