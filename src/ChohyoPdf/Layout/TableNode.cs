using System;
using System.Collections.Generic;

namespace ChohyoPdf.Layout
{
    /// <summary>表のレイアウト。行（結合でつながった行はまとめて）単位でページに配置する。</summary>
    internal sealed class TableNode : LayoutNode
    {
        private sealed class CellInfo
        {
            public int Row;
            public int Col;
            public int RowSpan;
            public int ColSpan;
            public ContainerNode Content = null!;
            public Thickness Padding = null!;
            public PdfColor? Background;
            public VerticalAlignment VAlign;
            public BorderLine? Top, Bottom, Left, Right;
        }

        private sealed class Cursor
        {
            public int Row;

            /// <summary>分割中の行のセルごとの続き位置（null なら行の先頭から）。</summary>
            public Dictionary<CellInfo, object?>? CellCursors;
        }

        /// <summary>セルの内容をすべて配置し終えたことを示す印。</summary>
        private static readonly object Done = new object();

        private readonly Table _t;
        private readonly int _rows;
        private readonly int _cols;
        private readonly List<CellInfo>[] _rowCells;
        private readonly int _headerRows;

        public TableNode(Table t, StyleScope scope)
        {
            _t = t;
            _rows = t.Rows.Count;
            _cols = t.Columns.Count;
            _headerRows = Math.Max(0, Math.Min(t.HeaderRowCount, _rows));
            _rowCells = new List<CellInfo>[_rows];
            var occupied = new bool[_rows, Math.Max(1, _cols)];
            var tableScope = scope.ForInnerContainer().With(t.Style);
            int bodyIndex = 0;
            for (int r = 0; r < _rows; r++)
            {
                _rowCells[r] = new List<CellInfo>();
                var row = t.Rows[r];
                bool header = r < _headerRows;
                var rowScope = tableScope.With(header ? t.HeaderStyle : null).With(row.Style);
                PdfColor? stripe = !header && t.AlternateRowBackground.HasValue && bodyIndex % 2 == 1 ? t.AlternateRowBackground : null;
                for (int c = 0; c < _cols && c < row.Cells.Count; c++)
                {
                    if (occupied[r, c]) continue;
                    var cell = row.Cells[c];
                    int cs = Math.Max(1, Math.Min(cell.ColumnSpan, _cols - c));
                    int maxRow = header ? _headerRows : _rows;
                    int rs = Math.Max(1, Math.Min(cell.RowSpan, maxRow - r));
                    for (int k = 1; k < cs; k++)
                    {
                        if (occupied[r, c + k])
                        {
                            cs = k;
                            break;
                        }
                    }
                    for (int rr = r; rr < r + rs; rr++)
                    {
                        for (int cc = c; cc < c + cs; cc++) occupied[rr, cc] = true;
                    }
                    var column = t.Columns[c];
                    var align = cell.Alignment ?? column.Alignment;
                    var cellScope = rowScope.With(cell.Style, align.HasValue ? new ParagraphFormat { Alignment = align } : null);
                    var defaultBorder = t.Borders;
                    _rowCells[r].Add(new CellInfo
                    {
                        Row = r,
                        Col = c,
                        RowSpan = rs,
                        ColSpan = cs,
                        Content = new ContainerNode(cell.Blocks, cellScope, allowPageBreaks: false),
                        Padding = cell.Padding ?? t.CellPadding,
                        Background = cell.Background ?? row.Background ?? (header ? t.HeaderBackground : stripe) ?? column.Background,
                        VAlign = cell.VerticalAlignment ?? row.VerticalAlignment ?? t.VerticalAlignment,
                        Top = cell.Borders.Top ?? defaultBorder,
                        Bottom = cell.Borders.Bottom ?? defaultBorder,
                        Left = cell.Borders.Left ?? defaultBorder,
                        Right = cell.Borders.Right ?? defaultBorder,
                    });
                }
                if (!header) bodyIndex++;
            }
        }

        public override double SpaceBefore => _t.SpaceBefore;
        public override double SpaceAfter => _t.SpaceAfter;
        public override bool KeepWithNext => _t.KeepWithNext;
        public override bool PageBreakBefore => _t.PageBreakBefore;

        private void ComputeColumns(double width, out double[] xs, out double[] ws, out double offset)
        {
            xs = new double[_cols + 1];
            ws = new double[_cols];
            double fixedSum = 0, relSum = 0;
            foreach (var c in _t.Columns)
            {
                if (c.Width.HasValue) fixedSum += Math.Max(0, c.Width.Value);
                else relSum += Math.Max(0, c.RelativeWidth);
            }
            double rest = Math.Max(0, width - fixedSum);
            for (int i = 0; i < _cols; i++)
            {
                var c = _t.Columns[i];
                ws[i] = c.Width.HasValue ? Math.Max(0, c.Width.Value) : (relSum > 0 ? rest * Math.Max(0, c.RelativeWidth) / relSum : 0);
                xs[i + 1] = xs[i] + ws[i];
            }
            double total = xs[_cols];
            offset = 0;
            if (total < width)
            {
                if (_t.Alignment == HorizontalAlignment.Center) offset = (width - total) / 2;
                else if (_t.Alignment == HorizontalAlignment.Right) offset = width - total;
            }
        }

        /// <summary>行 r から始まり、結合でつながっている行の範囲の終わり（排他）。</summary>
        private int GroupEnd(int r, int limit)
        {
            int end = r + 1;
            for (int i = r; i < end; i++)
            {
                foreach (var cell in _rowCells[i]) end = Math.Max(end, Math.Min(limit, cell.Row + cell.RowSpan));
                if (i == end - 1 && _t.Rows[i].KeepWithNext && end < limit) end++;
            }
            return end;
        }

        private sealed class GroupLayout
        {
            public int Start;
            public int End;
            public double[] Heights = Array.Empty<double>();
            public Dictionary<CellInfo, Slice> Slices = new Dictionary<CellInfo, Slice>();
            public Dictionary<CellInfo, object?> NextCursors = new Dictionary<CellInfo, object?>();
            public bool Complete = true;
            public double Total
            {
                get
                {
                    double t = 0;
                    foreach (double h in Heights) t += h;
                    return t;
                }
            }
        }

        private double CellWidth(CellInfo cell, double[] xs) => xs[cell.Col + cell.ColSpan] - xs[cell.Col];

        /// <summary>行グループを高さ無制限でレイアウトする（行が分割の途中なら続きから）。</summary>
        private GroupLayout Measure(LayoutContext ctx, int start, int end, double[] xs, Dictionary<CellInfo, object?>? cursors)
        {
            var g = new GroupLayout { Start = start, End = end, Heights = new double[end - start] };
            var spanning = new List<CellInfo>();
            for (int r = start; r < end; r++)
            {
                var row = _t.Rows[r];
                g.Heights[r - start] = row.Height ?? 0;
                foreach (var cell in _rowCells[r])
                {
                    object? cur = null;
                    if (cursors != null && cursors.TryGetValue(cell, out var cc)) cur = cc;
                    Slice slice;
                    if (cur == Done) slice = new Slice { Height = 0 };
                    else
                    {
                        double innerW = Math.Max(0, CellWidth(cell, xs) - cell.Padding.Left - cell.Padding.Right);
                        slice = cell.Content.Layout(ctx, innerW, double.PositiveInfinity, cur, true);
                    }
                    g.Slices[cell] = slice;
                    g.NextCursors[cell] = Done;
                    double need = slice.Height + cell.Padding.Top + cell.Padding.Bottom;
                    if (cell.RowSpan == 1)
                    {
                        if (row.HeightRule != RowHeightRule.Exactly || !row.Height.HasValue)
                            g.Heights[r - start] = Math.Max(g.Heights[r - start], need);
                    }
                    else
                    {
                        spanning.Add(cell);
                    }
                }
            }
            spanning.Sort((a, b) => a.RowSpan.CompareTo(b.RowSpan));
            foreach (var cell in spanning)
            {
                double need = g.Slices[cell].Height + cell.Padding.Top + cell.Padding.Bottom;
                double sum = 0;
                for (int r = cell.Row; r < cell.Row + cell.RowSpan; r++) sum += g.Heights[r - start];
                if (need > sum)
                {
                    // 固定高さでない最後の行に不足分を足す
                    for (int r = cell.Row + cell.RowSpan - 1; r >= cell.Row; r--)
                    {
                        var row = _t.Rows[r];
                        if (row.HeightRule != RowHeightRule.Exactly || !row.Height.HasValue)
                        {
                            g.Heights[r - start] += need - sum;
                            break;
                        }
                    }
                }
            }
            return g;
        }

        /// <summary>
        /// 1 行を利用できる高さで分割してレイアウトする。分割できなければ null。
        /// atTop が false のときは、内容が残っているセルがすべて何かを置ける場合だけ分割する
        /// （ラベルと内容が別のページに分かれたり、中身のない枠だけが残ったりしないようにする）。
        /// </summary>
        private GroupLayout? Split(LayoutContext ctx, int r, double[] xs, double available, Dictionary<CellInfo, object?>? cursors, bool atTop)
        {
            var row = _t.Rows[r];
            if (row.HeightRule == RowHeightRule.Exactly && row.Height.HasValue) return null;
            var g = new GroupLayout { Start = r, End = r + 1, Heights = new double[1], Complete = true };
            double height = 0;
            bool progressed = false;
            foreach (var cell in _rowCells[r])
            {
                object? cur = null;
                if (cursors != null && cursors.TryGetValue(cell, out var cc)) cur = cc;
                Slice slice;
                if (cur == Done || cell.Content.IsEmpty)
                {
                    slice = new Slice { Height = 0 };
                    g.NextCursors[cell] = Done;
                }
                else
                {
                    double innerW = Math.Max(0, CellWidth(cell, xs) - cell.Padding.Left - cell.Padding.Right);
                    double innerH = available - cell.Padding.Top - cell.Padding.Bottom;
                    slice = cell.Content.Layout(ctx, innerW, innerH, cur, atTop);
                    if (slice.Empty) return null;
                    progressed = true;
                    g.NextCursors[cell] = slice.Next ?? Done;
                    if (slice.Next != null) g.Complete = false;
                }
                g.Slices[cell] = slice;
                height = Math.Max(height, slice.Height + cell.Padding.Top + cell.Padding.Bottom);
            }
            if (!progressed) return null;
            g.Heights[0] = height;
            return g;
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            if (_rows == 0 || _cols == 0) return new Slice { Height = 0 };
            ComputeColumns(width, out var xs, out var ws, out double offset);
            var c = cursor as Cursor;
            bool first = c == null;
            int row = c?.Row ?? 0;
            var cellCursors = c?.CellCursors;

            var placedGroups = new List<KeyValuePair<double, GroupLayout>>();
            double y = 0;

            // 見出し行
            if (_headerRows > 0 && (first || _t.RepeatHeaderRows))
            {
                int r = 0;
                while (r < _headerRows)
                {
                    int end = GroupEnd(r, _headerRows);
                    var g = Measure(ctx, r, end, xs, null);
                    placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, g));
                    y += g.Total;
                    r = end;
                }
                if (first) row = _headerRows;
            }

            int bodyPlaced = 0;
            object? next = null;
            while (row < _rows)
            {
                int end = cellCursors != null ? row + 1 : GroupEnd(row, _rows);
                var g = Measure(ctx, row, end, xs, cellCursors);
                if (y + g.Total <= available + 1e-6)
                {
                    placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, g));
                    y += g.Total;
                    row = end;
                    cellCursors = null;
                    bodyPlaced++;
                    continue;
                }

                // 収まらない: ページの先頭で最初の行なら必ず分割する。そうでなければ、
                // AllowBreakAcrossPages の行だけ、残りの高さで分割を試みる
                bool singleRow = end == row + 1;
                bool forced = atTop && bodyPlaced == 0;
                if (singleRow && (forced || _t.Rows[row].AllowBreakAcrossPages))
                {
                    var split = Split(ctx, row, xs, available - y, cellCursors, forced);
                    if (split != null)
                    {
                        placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, split));
                        y += split.Total;
                        bodyPlaced++;
                        if (split.Complete)
                        {
                            row++;
                            cellCursors = null;
                            continue;
                        }
                        next = new Cursor { Row = row, CellCursors = split.NextCursors };
                        break;
                    }
                }

                if (bodyPlaced > 0)
                {
                    next = new Cursor { Row = row, CellCursors = cellCursors };
                    break;
                }
                if (!atTop) return Slice.Nothing();

                // ページの先頭でも収まらない（固定高さの行や結合行）: はみ出して配置する
                placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, g));
                y += g.Total;
                row = end;
                cellCursors = null;
                bodyPlaced++;
                if (row < _rows) next = new Cursor { Row = row };
                break;
            }

            var groups = placedGroups;
            double tableOffset = offset;
            var xsLocal = xs;
            return new Slice
            {
                Height = y,
                Next = next,
                Draw = (gr, x, top) => DrawGroups(gr, x + tableOffset, top, xsLocal, groups),
            };
        }

        private void DrawGroups(PdfGraphics g, double x, double top, double[] xs, List<KeyValuePair<double, GroupLayout>> groups)
        {
            var borders = new List<Action>();
            foreach (var kv in groups)
            {
                var layout = kv.Value;
                double groupTop = top + kv.Key;
                var rowTops = new double[layout.End - layout.Start + 1];
                rowTops[0] = groupTop;
                for (int i = 0; i < layout.Heights.Length; i++) rowTops[i + 1] = rowTops[i] + layout.Heights[i];

                foreach (var pair in layout.Slices)
                {
                    var cell = pair.Key;
                    var slice = pair.Value;
                    int r0 = cell.Row - layout.Start;
                    int r1 = Math.Min(layout.Heights.Length, r0 + cell.RowSpan);
                    double cx = x + xs[cell.Col];
                    double cw = xs[cell.Col + cell.ColSpan] - xs[cell.Col];
                    double cy = rowTops[r0];
                    double ch = rowTops[r1] - cy;

                    if (cell.Background.HasValue) g.FillRectangle(cell.Background.Value, cx, cy, cw, ch);

                    if (slice.Draw != null)
                    {
                        double innerH = ch - cell.Padding.Top - cell.Padding.Bottom;
                        double dy = 0;
                        if (cell.VAlign == VerticalAlignment.Middle) dy = Math.Max(0, (innerH - slice.Height) / 2);
                        else if (cell.VAlign == VerticalAlignment.Bottom) dy = Math.Max(0, innerH - slice.Height);
                        bool clip = slice.Height > innerH + 0.01;
                        if (clip)
                        {
                            g.SaveState();
                            g.SetClip(cx, cy, cw, ch);
                        }
                        slice.Draw(g, cx + cell.Padding.Left, cy + cell.Padding.Top + dy);
                        if (clip) g.RestoreState();
                    }

                    var cellCopy = cell;
                    borders.Add(() => DrawBorders(g, cellCopy, cx, cy, cw, ch));
                }
            }
            foreach (var b in borders) b();
        }

        private static void DrawBorders(PdfGraphics g, CellInfo cell, double x, double y, double w, double h)
        {
            if (cell.Top != null && cell.Top.IsVisible) g.DrawLine(cell.Top.ToPen(), x, y, x + w, y);
            if (cell.Bottom != null && cell.Bottom.IsVisible) g.DrawLine(cell.Bottom.ToPen(), x, y + h, x + w, y + h);
            if (cell.Left != null && cell.Left.IsVisible) g.DrawLine(cell.Left.ToPen(), x, y, x, y + h);
            if (cell.Right != null && cell.Right.IsVisible) g.DrawLine(cell.Right.ToPen(), x + w, y, x + w, y + h);
        }
    }
}
