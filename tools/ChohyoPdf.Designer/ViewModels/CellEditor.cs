using System;
using System.Collections.Generic;
using System.Linq;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.ViewModels
{
    /// <summary>
    /// 表の中で選択したセルを編集する。文字は範囲の左上のセル、書式は範囲のすべてのセルに設定する。
    /// </summary>
    public sealed class CellEditor : ObservableObject
    {
        public CellEditor(TableElement table, CellRange range)
        {
            Table = table;
            Range = range;
        }

        public TableElement Table { get; }

        public CellRange Range { get; }

        /// <summary>くり返し行の見本を選んでいる。</summary>
        public bool IsDetailRow => Range.Top >= Table.Rows.Count;

        public string Title
        {
            get
            {
                string rows = IsDetailRow ? "くり返し行" : Range.Top == Range.Bottom ? (Range.Top + 1) + " 行目" : (Range.Top + 1) + "〜" + (Range.Bottom + 1) + " 行目";
                string cols = Range.Left == Range.Right ? (Range.Left + 1) + " 列目" : (Range.Left + 1) + "〜" + (Range.Right + 1) + " 列目";
                return rows + "・" + cols;
            }
        }

        private TableCell Anchor
        {
            get
            {
                var (r, c) = Table.GetOwner(Range.Top, Range.Left);
                return Table.GetRow(r)!.Cells[c];
            }
        }

        private IEnumerable<TableCell> Cells
        {
            get
            {
                for (int r = Range.Top; r <= Range.Bottom; r++)
                {
                    var row = Table.GetRow(r);
                    if (row == null) continue;
                    for (int c = Range.Left; c <= Range.Right && c < row.Cells.Count; c++) yield return row.Cells[c];
                }
            }
        }

        private IEnumerable<TableRow> Rows
        {
            get
            {
                for (int r = Range.Top; r <= Range.Bottom; r++)
                {
                    var row = Table.GetRow(r);
                    if (row != null) yield return row;
                }
            }
        }

        private IEnumerable<TableColumn> Columns
        {
            get
            {
                for (int c = Range.Left; c <= Range.Right && c < Table.Columns.Count; c++) yield return Table.Columns[c];
            }
        }

        public string Text
        {
            get => Anchor.Text;
            set
            {
                Anchor.Text = value;
                OnPropertyChanged();
            }
        }

        /// <summary>横の揃え。null なら列の設定に従う。</summary>
        public TextAlignment? Alignment
        {
            get => Anchor.Alignment;
            set
            {
                foreach (var cell in Cells) cell.Alignment = value;
                OnPropertyChanged();
            }
        }

        /// <summary>縦の揃え。null なら表の設定に従う。</summary>
        public VerticalAlignment? VerticalAlignment
        {
            get => Anchor.VerticalAlignment;
            set
            {
                foreach (var cell in Cells) cell.VerticalAlignment = value;
                OnPropertyChanged();
            }
        }

        public bool? Bold
        {
            get => Anchor.Bold;
            set
            {
                foreach (var cell in Cells) cell.Bold = value;
                OnPropertyChanged();
            }
        }

        public double? FontSize
        {
            get => Anchor.FontSize;
            set
            {
                double? v = value is double d && d > 0 ? d : null;
                foreach (var cell in Cells) cell.FontSize = v;
                OnPropertyChanged();
            }
        }

        public string? Color
        {
            get => Anchor.Color;
            set
            {
                foreach (var cell in Cells) cell.Color = value;
                OnPropertyChanged();
            }
        }

        public string? BackgroundColor
        {
            get => Anchor.BackgroundColor;
            set
            {
                foreach (var cell in Cells) cell.BackgroundColor = value;
                OnPropertyChanged();
            }
        }

        /// <summary>行の高さ（mm）。範囲のすべての行に設定する。</summary>
        public double RowHeight
        {
            get => Rows.FirstOrDefault()?.Height ?? 0;
            set
            {
                if (value <= 0) return;
                foreach (var row in Rows) row.Height = value;
                Table.OnSizeChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>列の幅（mm）。範囲のすべての列に設定する。</summary>
        public double ColumnWidth
        {
            get => Columns.FirstOrDefault()?.Width ?? 0;
            set
            {
                if (value <= 0) return;
                foreach (var column in Columns) column.Width = value;
                Table.OnSizeChanged();
                OnPropertyChanged();
            }
        }

        /// <summary>列の横揃え（セルで指定がなければ使う）。</summary>
        public TextAlignment ColumnAlignment
        {
            get => Columns.FirstOrDefault()?.Alignment ?? TextAlignment.Left;
            set
            {
                foreach (var column in Columns) column.Alignment = value;
                OnPropertyChanged();
            }
        }

        /// <summary>モデルが変わったときに、表示を更新する。</summary>
        public void Refresh() => OnPropertyChanged(string.Empty);
    }
}
