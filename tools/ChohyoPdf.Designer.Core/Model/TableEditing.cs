using System;
using System.Collections.Generic;
using System.Linq;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>表のセルの範囲。行は 0 〜 Rows.Count - 1 が固定の行、Rows.Count がくり返し行。</summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        public CellRange(int row1, int column1, int row2, int column2)
        {
            Top = Math.Min(row1, row2);
            Bottom = Math.Max(row1, row2);
            Left = Math.Min(column1, column2);
            Right = Math.Max(column1, column2);
        }

        public int Top { get; }
        public int Left { get; }
        public int Bottom { get; }
        public int Right { get; }

        public bool IsSingle => Top == Bottom && Left == Right;

        public bool Contains(int row, int column) => row >= Top && row <= Bottom && column >= Left && column <= Right;

        public bool Equals(CellRange other) => Top == other.Top && Left == other.Left && Bottom == other.Bottom && Right == other.Right;

        public override bool Equals(object? obj) => obj is CellRange r && Equals(r);

        public override int GetHashCode() => HashCode.Combine(Top, Left, Bottom, Right);
    }

    /// <summary>表の行・列の追加と削除、セルの結合。</summary>
    public static class TableEditing
    {
        /// <summary>範囲に一部だけかかっている結合セルがあれば、それを含むように範囲を広げる。</summary>
        public static CellRange ExpandToMerges(TableElement t, CellRange range)
        {
            var owners = t.GetOwnerMap();
            int top = range.Top, left = range.Left, bottom = range.Bottom, right = range.Right;
            // くり返し行と固定の行はまたげない
            if (top < t.Rows.Count && bottom >= t.Rows.Count) bottom = t.Rows.Count - 1;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int r = top; r <= bottom; r++)
                {
                    for (int c = left; c <= right; c++)
                    {
                        var (or, oc) = owners[r, c];
                        var row = t.GetRow(or)!;
                        int rs = t.ClampRowSpan(row.Cells[oc].RowSpan, or), cs = t.ClampSpan(row.Cells[oc].ColumnSpan, oc);
                        if (or < top) { top = or; changed = true; }
                        if (oc < left) { left = oc; changed = true; }
                        if (or + rs - 1 > bottom) { bottom = or + rs - 1; changed = true; }
                        if (oc + cs - 1 > right) { right = oc + cs - 1; changed = true; }
                    }
                }
            }
            return new CellRange(top, left, bottom, right);
        }

        /// <summary>範囲のセルを 1 つに結合する。文字は左上のセルのものを残す。</summary>
        public static void Merge(TableElement t, CellRange range)
        {
            range = ExpandToMerges(t, range);
            for (int r = range.Top; r <= range.Bottom; r++)
            {
                var row = t.GetRow(r);
                if (row == null) continue;
                for (int c = range.Left; c <= range.Right; c++)
                {
                    row.Cells[c].ColumnSpan = 1;
                    row.Cells[c].RowSpan = 1;
                }
            }
            var owner = t.GetRow(range.Top)!.Cells[range.Left];
            owner.ColumnSpan = range.Right - range.Left + 1;
            owner.RowSpan = range.Top >= t.Rows.Count ? 1 : range.Bottom - range.Top + 1;
            t.OnSizeChanged();
        }

        /// <summary>範囲の結合を解除する。</summary>
        public static void Unmerge(TableElement t, CellRange range)
        {
            range = ExpandToMerges(t, range);
            for (int r = range.Top; r <= range.Bottom; r++)
            {
                var row = t.GetRow(r);
                if (row == null) continue;
                for (int c = range.Left; c <= range.Right; c++)
                {
                    row.Cells[c].ColumnSpan = 1;
                    row.Cells[c].RowSpan = 1;
                }
            }
            t.OnSizeChanged();
        }

        /// <summary>列を index の位置に挿入する。挿入位置をまたぐ結合セルは 1 列広げる。</summary>
        public static void InsertColumn(TableElement t, int index, double width)
        {
            index = Math.Max(0, Math.Min(index, t.Columns.Count));
            foreach (var row in AllRows(t))
            {
                for (int c = 0; c < index && c < row.Cells.Count; c++)
                {
                    if (c + row.Cells[c].ColumnSpan > index) row.Cells[c].ColumnSpan++;
                }
                row.Cells.Insert(Math.Min(index, row.Cells.Count), new TableCell());
            }
            t.Columns.Insert(index, new TableColumn { Width = width });
            t.OnSizeChanged();
        }

        /// <summary>列を削除する。その列をまたぐ結合セルは 1 列狭める。</summary>
        public static void DeleteColumn(TableElement t, int index)
        {
            if (t.Columns.Count <= 1 || index < 0 || index >= t.Columns.Count) return;
            foreach (var row in AllRows(t))
            {
                for (int c = 0; c < index && c < row.Cells.Count; c++)
                {
                    if (c + row.Cells[c].ColumnSpan > index) row.Cells[c].ColumnSpan--;
                }
                var removed = row.Cells[index];
                // 結合の左上のセルを消すときは、結合を右隣のセルに引き継ぐ
                if (removed.ColumnSpan > 1 && index + 1 < row.Cells.Count)
                {
                    row.Cells[index + 1].ColumnSpan = removed.ColumnSpan - 1;
                    row.Cells[index + 1].RowSpan = removed.RowSpan;
                    if (row.Cells[index + 1].Text.Length == 0) row.Cells[index + 1].Text = removed.Text;
                }
                row.Cells.RemoveAt(index);
            }
            t.Columns.RemoveAt(index);
            t.OnSizeChanged();
        }

        /// <summary>固定の行を index の位置に挿入する。挿入位置をまたぐ結合セルは 1 行広げる。</summary>
        public static void InsertRow(TableElement t, int index, double height)
        {
            index = Math.Max(0, Math.Min(index, t.Rows.Count));
            for (int r = 0; r < index; r++)
            {
                foreach (var cell in t.Rows[r].Cells)
                {
                    if (r + cell.RowSpan > index) cell.RowSpan++;
                }
            }
            t.Rows.Insert(index, t.NewRow(height));
            t.OnSizeChanged();
        }

        /// <summary>固定の行を削除する。その行をまたぐ結合セルは 1 行狭める。</summary>
        public static void DeleteRow(TableElement t, int index)
        {
            if (index < 0 || index >= t.Rows.Count) return;
            if (t.Rows.Count <= 1 && !t.HasDetail) return;
            for (int r = 0; r < index; r++)
            {
                foreach (var cell in t.Rows[r].Cells)
                {
                    if (r + cell.RowSpan > index) cell.RowSpan--;
                }
            }
            var removed = t.Rows[index];
            if (index + 1 < t.Rows.Count)
            {
                var next = t.Rows[index + 1];
                for (int c = 0; c < removed.Cells.Count && c < next.Cells.Count; c++)
                {
                    var cell = removed.Cells[c];
                    if (cell.RowSpan <= 1) continue;
                    next.Cells[c].RowSpan = cell.RowSpan - 1;
                    next.Cells[c].ColumnSpan = cell.ColumnSpan;
                    if (next.Cells[c].Text.Length == 0) next.Cells[c].Text = cell.Text;
                }
            }
            t.Rows.RemoveAt(index);
            t.OnSizeChanged();
        }

        private static IEnumerable<TableRow> AllRows(TableElement t) => t.Rows.Concat(new[] { t.DetailRow }).ToList();

        /// <summary>
        /// 一覧の列から一覧の表を作る。見出しの行に列の名前、くり返し行に {列名} を入れ、数値の列は右揃えにする。
        /// </summary>
        /// <param name="width">表の幅（mm）。列で等分する。</param>
        public static TableElement CreateDetailTable(ListDefinition list, double width, int rowsPerPage = 10, double rowHeight = 7)
        {
            int columns = Math.Max(1, list.Columns.Count);
            var table = TableElement.Create(columns, 1, Math.Round(width / columns, 1), rowHeight);
            for (int c = 0; c < list.Columns.Count; c++)
            {
                var column = list.Columns[c];
                var header = table.Rows[0].Cells[c];
                header.Text = TextTemplate.Escape(column.Name);
                header.Alignment = TextAlignment.Center;
                header.Bold = true;
                header.BackgroundColor = "#E8EEF6";
                table.DetailRow.Cells[c].Text = "{" + column.Name + "}";
                if (column.Kind == FieldKind.Number) table.Columns[c].Alignment = TextAlignment.Right;
            }
            table.DetailSource = list.Name;
            table.DetailRowCount = Math.Max(1, rowsPerPage);
            return table;
        }
    }
}
