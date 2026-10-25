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

        /// <summary>内容に合わせる列か（<see cref="Column.AutoFit"/>、または <see cref="Table.AutoFitColumns"/> で幅を指定していない列）。</summary>
        private readonly bool[] _auto;
        private readonly bool _hasAuto;

        /// <summary>内容の幅の計測結果（初めて必要になったときに 1 度だけ計測する。このノードを作った文書のレイアウトの間は変わらない）。</summary>
        private readonly object _sync = new object();
        private IntrinsicWidths? _intrinsic;

        /// <summary>列ごとの内容の幅。固定幅の列は最小も最大もその幅。</summary>
        private sealed class IntrinsicWidths
        {
            public double[] Min = Array.Empty<double>();
            public double[] Max = Array.Empty<double>();
        }

        public TableNode(Table t, StyleScope scope)
        {
            _t = t;
            _rows = t.Rows.Count;
            _cols = t.Columns.Count;
            _auto = new bool[_cols];
            for (int i = 0; i < _cols; i++)
            {
                var column = t.Columns[i];
                _auto[i] = column.AutoFit || (t.AutoFitColumns && !column.Width.HasValue);
                if (_auto[i]) _hasAuto = true;
            }
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

        /// <summary>
        /// 内容の幅（列ごとの最小・最大）。セルの内容の最小幅（折り返せない最長の単位）と最大幅（折り返さないときの幅）に、
        /// セルの余白と罫線の太さの半分（左右）を加え、1 列のセルは列の最大値を採る。横に結合したセルは、
        /// 結合した列の合計が足りない分を、固定幅でない列に最大幅の比で振り分ける。固定幅の列は、その幅が最小でも最大でもある。
        /// 入れ子の表・リスト・画像は内容の幅を計測し、幅を指定していない図は 0 として扱う。
        /// </summary>
        private IntrinsicWidths GetIntrinsic(LayoutContext ctx)
        {
            lock (_sync)
            {
                return _intrinsic ?? (_intrinsic = ComputeIntrinsic(ctx));
            }
        }

        private IntrinsicWidths ComputeIntrinsic(LayoutContext ctx)
        {
            var min = new double[_cols];
            var max = new double[_cols];
            var isFixed = new bool[_cols];
            for (int i = 0; i < _cols; i++)
            {
                var column = _t.Columns[i];
                if (_auto[i] || !column.Width.HasValue) continue;
                isFixed[i] = true;
                min[i] = max[i] = Math.Max(0, column.Width.Value);
            }

            var spans = new List<KeyValuePair<CellInfo, double[]>>();
            foreach (var cells in _rowCells)
            {
                foreach (var cell in cells)
                {
                    cell.Content.MeasureWidths(ctx, out double cellMin, out double cellMax);
                    double extra = cell.Padding.Left + cell.Padding.Right + BorderHalf(cell.Left) + BorderHalf(cell.Right);
                    cellMin += extra;
                    cellMax += extra;
                    if (cell.ColSpan == 1)
                    {
                        if (isFixed[cell.Col]) continue;
                        min[cell.Col] = Math.Max(min[cell.Col], cellMin);
                        max[cell.Col] = Math.Max(max[cell.Col], cellMax);
                    }
                    else
                    {
                        spans.Add(new KeyValuePair<CellInfo, double[]>(cell, new[] { cellMin, cellMax }));
                    }
                }
            }
            for (int i = 0; i < _cols; i++) max[i] = Math.Max(max[i], min[i]);

            // 結合したセルは、結合の小さいものから順に、足りない幅を振り分ける
            spans.Sort((a, b) => a.Key.ColSpan.CompareTo(b.Key.ColSpan));
            foreach (var pair in spans)
            {
                var cell = pair.Key;
                SpreadSpan(cell, pair.Value[0], min, max, isFixed);
                SpreadSpan(cell, pair.Value[1], max, max, isFixed);
                for (int i = cell.Col; i < cell.Col + cell.ColSpan; i++) max[i] = Math.Max(max[i], min[i]);
            }
            return new IntrinsicWidths { Min = min, Max = max };
        }

        private static double BorderHalf(BorderLine? line) => line != null && line.IsVisible ? line.Width / 2 : 0;

        /// <summary>
        /// 結合したセルが必要とする幅 <paramref name="need"/> に対して、結合した列の <paramref name="target"/> の合計が足りない分を、
        /// 固定幅でない列に <paramref name="weights"/>（全部 0 なら均等）の比で足す。
        /// </summary>
        private static void SpreadSpan(CellInfo cell, double need, double[] target, double[] weights, bool[] isFixed)
        {
            double sum = 0, weightSum = 0;
            int free = 0;
            for (int i = cell.Col; i < cell.Col + cell.ColSpan; i++)
            {
                sum += target[i];
                if (isFixed[i]) continue;
                weightSum += weights[i];
                free++;
            }
            if (need <= sum || free == 0) return;
            double lack = need - sum;
            for (int i = cell.Col; i < cell.Col + cell.ColSpan; i++)
            {
                if (isFixed[i]) continue;
                target[i] += weightSum > 0 ? lack * weights[i] / weightSum : lack / free;
            }
        }

        /// <summary>表の幅。列の最小幅の合計と最大幅の合計（固定幅の列はその幅、比率の列は内容に合わせた幅として数える）。</summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            min = 0;
            max = 0;
            if (_rows == 0 || _cols == 0) return;
            var intrinsic = GetIntrinsic(ctx);
            for (int i = 0; i < _cols; i++)
            {
                min += intrinsic.Min[i];
                max += intrinsic.Max[i];
            }
        }

        /// <summary>
        /// 内容に合わせる列がある表の列幅。固定幅の列を除いた残りの幅で、内容に合わせる列（と、比率の列）に幅を配る。
        /// 内容に合わせる列だけなら、最大幅の合計が収まれば最大幅（表は内容の幅になる）、収まらなければ最大幅に比例して縮める（最小幅は下回らない）。
        /// 最小幅の合計も収まらないときは、最小幅を同じ比率で縮めて収める（長い単語は文字の途中で折り返す）。
        /// 比率の列があるときは、内容に合わせる列が最大幅を取り、残りを比率の列で比率に従って分ける
        /// （比率の列の最小幅に足りなければ、比率の列を最小幅にして、残りを内容に合わせる列に配る）。
        /// </summary>
        private void ComputeAutoColumns(LayoutContext ctx, double width, bool shrinkToFit, double[] ws)
        {
            var intrinsic = GetIntrinsic(ctx);
            double fixedSum = 0;
            var autoIdx = new List<int>();
            var relIdx = new List<int>();
            for (int i = 0; i < _cols; i++)
            {
                var c = _t.Columns[i];
                if (_auto[i]) autoIdx.Add(i);
                else if (c.Width.HasValue)
                {
                    ws[i] = Math.Max(0, c.Width.Value);
                    fixedSum += ws[i];
                }
                else relIdx.Add(i);
            }
            double scale = shrinkToFit && fixedSum > width && fixedSum > 0 ? Math.Max(0, width) / fixedSum : 1;
            for (int i = 0; i < _cols; i++)
            {
                if (!_auto[i] && _t.Columns[i].Width.HasValue) ws[i] *= scale;
            }
            double avail = width - fixedSum * scale;

            var weights = new double[_cols];
            if (avail <= 1e-6)
            {
                // 固定幅の列だけで使い切っている: 内容に合わせる列は最小幅にする（固定幅の列と同じく、収まらなければ右にはみ出す。段組みでは縮める）
                foreach (int i in autoIdx) ws[i] = intrinsic.Min[i];
                foreach (int i in relIdx) ws[i] = intrinsic.Min[i];
                if (shrinkToFit)
                {
                    double total = 0;
                    foreach (double w in ws) total += w;
                    if (total > width && total > 0)
                    {
                        double f = Math.Max(0, width) / total;
                        for (int i = 0; i < _cols; i++) ws[i] *= f;
                    }
                }
                return;
            }

            if (relIdx.Count == 0)
            {
                FillAuto(autoIdx, avail, intrinsic, ws);
                return;
            }

            double autoMax = 0, relMin = 0;
            foreach (int i in autoIdx) autoMax += intrinsic.Max[i];
            foreach (int i in relIdx) relMin += intrinsic.Min[i];
            double rest = avail - autoMax;
            if (rest >= relMin)
            {
                foreach (int i in autoIdx) ws[i] = intrinsic.Max[i];
                foreach (int i in relIdx) weights[i] = Math.Max(0, _t.Columns[i].RelativeWidth);
                Fill(relIdx, rest, weights, intrinsic.Min, ws);
            }
            else
            {
                foreach (int i in relIdx) ws[i] = intrinsic.Min[i];
                FillAuto(autoIdx, Math.Max(0, avail - relMin), intrinsic, ws);
            }
        }

        /// <summary>内容に合わせる列に <paramref name="total"/> の幅を配る（最大幅の合計が収まればそのまま、収まらなければ最大幅に比例して縮める）。</summary>
        private static void FillAuto(List<int> idx, double total, IntrinsicWidths intrinsic, double[] ws)
        {
            double maxSum = 0;
            foreach (int i in idx) maxSum += intrinsic.Max[i];
            if (maxSum <= total + 1e-9)
            {
                foreach (int i in idx) ws[i] = intrinsic.Max[i];
                return;
            }
            Fill(idx, total, intrinsic.Max, intrinsic.Min, ws);
        }

        /// <summary>
        /// 列 <paramref name="idx"/> に合計 <paramref name="total"/> の幅を、<paramref name="weights"/> に比例して配る。
        /// どの列も <paramref name="lower"/> を下回らない（下回る列は <paramref name="lower"/> にして、残りを残りの列で配り直す）。
        /// <paramref name="lower"/> の合計が <paramref name="total"/> 以上のときは、<paramref name="lower"/> の比で縮めて合計を <paramref name="total"/> にする。
        /// </summary>
        private static void Fill(List<int> idx, double total, double[] weights, double[] lower, double[] ws)
        {
            double lowerSum = 0;
            foreach (int i in idx) lowerSum += lower[i];
            if (lowerSum >= total)
            {
                foreach (int i in idx) ws[i] = lowerSum > 0 ? lower[i] * Math.Max(0, total) / lowerSum : 0;
                return;
            }
            var pinned = new bool[ws.Length];
            while (true)
            {
                double remaining = total, weightSum = 0;
                int free = 0;
                foreach (int i in idx)
                {
                    if (pinned[i]) remaining -= lower[i];
                    else
                    {
                        weightSum += weights[i];
                        free++;
                    }
                }
                bool changed = false;
                foreach (int i in idx)
                {
                    if (pinned[i]) continue;
                    double w = weightSum > 0 ? remaining * weights[i] / weightSum : remaining / free;
                    if (w < lower[i] - 1e-9)
                    {
                        pinned[i] = true;
                        changed = true;
                    }
                    else ws[i] = w;
                }
                if (!changed) break;
            }
            foreach (int i in idx)
            {
                if (pinned[i]) ws[i] = lower[i];
            }
        }

        // shrinkToFit: 固定幅の列の合計が width を超えるとき、比率を保って縮めて収める（段組みのセクションで、隣の段にはみ出さないため）。
        // false なら従来どおり、そのままの幅で右にはみ出す。内容に合わせる列がある表は、ComputeAutoColumns で幅を配る。
        private void ComputeColumns(LayoutContext ctx, double width, bool shrinkToFit, out double[] xs, out double[] ws, out double offset)
        {
            xs = new double[_cols + 1];
            ws = new double[_cols];
            if (_hasAuto)
            {
                ComputeAutoColumns(ctx, width, shrinkToFit, ws);
                for (int i = 0; i < _cols; i++) xs[i + 1] = xs[i] + ws[i];
            }
            else
            {
                double fixedSum = 0, relSum = 0;
                foreach (var c in _t.Columns)
                {
                    if (c.Width.HasValue) fixedSum += Math.Max(0, c.Width.Value);
                    else relSum += Math.Max(0, c.RelativeWidth);
                }
                double rest = Math.Max(0, width - fixedSum);
                double scale = shrinkToFit && fixedSum > width && fixedSum > 0 ? Math.Max(0, width) / fixedSum : 1;
                for (int i = 0; i < _cols; i++)
                {
                    var c = _t.Columns[i];
                    ws[i] = c.Width.HasValue ? Math.Max(0, c.Width.Value) * scale : (relSum > 0 ? rest * Math.Max(0, c.RelativeWidth) / relSum : 0);
                    xs[i + 1] = xs[i] + ws[i];
                }
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

            /// <summary>行の中のセルにある脚注の本文の高さの合計と、その脚注。</summary>
            public double FootnoteHeight;
            public List<FootnoteEntry>? Footnotes;

            public void AddFootnotes(Slice slice)
            {
                if (slice.Footnotes == null) return;
                Footnotes = Footnotes ?? new List<FootnoteEntry>();
                Footnotes.AddRange(slice.Footnotes);
                FootnoteHeight += slice.FootnoteHeight;
            }

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
                    g.AddFootnotes(slice);
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
            double footnoteBase = ctx.FootnoteItems;
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
                    // 同じ行の前のセルの脚注も、このページの脚注領域を使う
                    ctx.FootnoteItems = footnoteBase + g.FootnoteHeight;
                    double innerH = available - cell.Padding.Top - cell.Padding.Bottom - (ctx.FootnoteArea(ctx.FootnoteItems) - ctx.FootnoteArea(footnoteBase));
                    slice = cell.Content.Layout(ctx, innerW, innerH, cur, atTop);
                    ctx.FootnoteItems = footnoteBase;
                    if (slice.Empty) return null;
                    g.AddFootnotes(slice);
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
            double saved = ctx.FootnoteItems;
            try
            {
                return LayoutRows(ctx, width, available, cursor, atTop);
            }
            finally
            {
                ctx.FootnoteItems = saved;
            }
        }

        private Slice LayoutRows(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            if (_rows == 0 || _cols == 0) return new Slice { Height = 0 };
            ComputeColumns(ctx, width, ctx.ColumnCount > 1, out var xs, out var ws, out double offset);
            var c = cursor as Cursor;
            bool first = c == null;
            int row = c?.Row ?? 0;
            var cellCursors = c?.CellCursors;

            var placedGroups = new List<KeyValuePair<double, GroupLayout>>();
            double y = 0;

            // 脚注: 置いた行の脚注の本文の高さ（fnSum）は、後に置く行や中のセルの利用可能な高さから引く
            double fnBase = ctx.FootnoteItems;
            double fnSum = 0;
            var fnList = new List<FootnoteEntry>();
            double Reserved() => ctx.FootnoteArea(fnBase + fnSum) - ctx.FootnoteArea(fnBase);
            double ExtraFor(GroupLayout group) => ctx.FootnoteArea(fnBase + fnSum + group.FootnoteHeight) - ctx.FootnoteArea(fnBase);
            bool OverLimit(GroupLayout group) => group.FootnoteHeight > 0 && ctx.FootnoteArea(fnBase + fnSum + group.FootnoteHeight) > ctx.FootnoteLimit + 1e-6;
            void Commit(GroupLayout group)
            {
                if (group.Footnotes != null) fnList.AddRange(group.Footnotes);
                fnSum += group.FootnoteHeight;
            }

            // 見出し行
            if (_headerRows > 0 && (first || _t.RepeatHeaderRows))
            {
                int r = 0;
                while (r < _headerRows)
                {
                    int end = GroupEnd(r, _headerRows);
                    ctx.FootnoteItems = fnBase + fnSum;
                    var g = Measure(ctx, r, end, xs, null);
                    placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, g));
                    y += g.Total;
                    // 繰り返す見出し行の脚注は、最初の 1 回だけ数える（2 回目以降は同じ脚注をページごとに重ねない）
                    if (first) Commit(g);
                    else
                    {
                        g.Footnotes = null;
                        g.FootnoteHeight = 0;
                    }
                    r = end;
                }
                if (first) row = _headerRows;
            }

            int bodyPlaced = 0;
            object? next = null;
            while (row < _rows)
            {
                int end = cellCursors != null ? row + 1 : GroupEnd(row, _rows);
                ctx.FootnoteItems = fnBase + fnSum;
                var g = Measure(ctx, row, end, xs, cellCursors);
                if (y + g.Total + ExtraFor(g) <= available + 1e-6 && !OverLimit(g))
                {
                    placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, g));
                    y += g.Total;
                    Commit(g);
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
                    ctx.FootnoteItems = fnBase + fnSum;
                    var split = Split(ctx, row, xs, available - y - Reserved(), cellCursors, forced);
                    ctx.FootnoteItems = fnBase;
                    if (split != null && !forced && (y + split.Total + ExtraFor(split) > available + 1e-6 || OverLimit(split))) split = null;
                    if (split != null)
                    {
                        placedGroups.Add(new KeyValuePair<double, GroupLayout>(y, split));
                        y += split.Total;
                        Commit(split);
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
                Commit(g);
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
                FootnoteHeight = fnSum,
                Footnotes = fnList.Count > 0 ? fnList : null,
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
