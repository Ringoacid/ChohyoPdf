using System;
using System.Collections.Generic;
using System.Globalization;
using ChohyoPdf.Text;

namespace ChohyoPdf.Layout
{
    /// <summary>親から引き継ぐ文字スタイルと段落書式。</summary>
    internal sealed class StyleScope
    {
        public StyleScope(Document doc, TextStyle text, ParagraphFormat format)
        {
            Doc = doc;
            Text = text;
            Format = format;
        }

        public Document Doc { get; }
        public TextStyle Text { get; }
        public ParagraphFormat Format { get; }

        /// <summary>
        /// 表のセルやヘッダー/フッターの中で使う範囲。本文用のインデント・段落間隔・揃えを引き継がない（行送りと文字スタイルは引き継ぐ）。
        /// </summary>
        public StyleScope ForInnerContainer() => new StyleScope(Doc, Text, Format.Merge(new ParagraphFormat
        {
            Alignment = TextAlignment.Left,
            FirstLineIndent = 0,
            LeftIndent = 0,
            RightIndent = 0,
            SpaceBefore = 0,
            SpaceAfter = 0,
            KeepWithNext = false,
            KeepTogether = false,
            PageBreakBefore = false,
        }));

        public StyleScope With(TextStyle? text, ParagraphFormat? format = null)
        {
            if (text == null && format == null) return this;
            return new StyleScope(Doc, Text.Merge(text), Format.Merge(format));
        }
    }

    internal sealed class LayoutContext
    {
        public PageContext Page = new PageContext();

        /// <summary>本文で総ページ数フィールドが使われたか（使われたら 2 回目のレイアウトを行う）。</summary>
        public bool PageCountUsed;
    }

    internal delegate void DrawAction(PdfGraphics g, double x, double y);

    /// <summary>レイアウト結果の 1 片（1 ページ分）。</summary>
    internal sealed class Slice
    {
        public double Height;
        public DrawAction? Draw;

        /// <summary>続きの位置。null なら完了。</summary>
        public object? Next;

        /// <summary>何も配置できなかった（次のページで再試行する）。</summary>
        public bool Empty;

        /// <summary>この片の後で改ページする。</summary>
        public bool ForceBreak;

        public static Slice Nothing() => new Slice { Empty = true };
    }

    /// <summary>
    /// レイアウトの単位。カーソル（続きの位置）を受け取り、利用できる高さに収まる分だけを返す。
    /// atTop が true のとき（ページや領域の先頭）は、収まらなくても必ず何かを配置して処理を進める。
    /// </summary>
    internal abstract class LayoutNode
    {
        public virtual double SpaceBefore => 0;
        public virtual double SpaceAfter => 0;
        public virtual bool KeepWithNext => false;
        public virtual bool PageBreakBefore => false;

        public abstract Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop);
    }

    /// <summary>ブロックを縦に積む。</summary>
    internal sealed class ContainerNode : LayoutNode
    {
        private sealed class Cursor
        {
            public Cursor(int index, object? child)
            {
                Index = index;
                Child = child;
            }

            public int Index { get; }
            public object? Child { get; }
        }

        private readonly List<LayoutNode> _children;
        private readonly bool _allowPageBreaks;

        public ContainerNode(IEnumerable<Block> blocks, StyleScope scope, bool allowPageBreaks)
        {
            _children = new List<LayoutNode>();
            foreach (var b in blocks) _children.Add(b.CreateNode(scope));
            _allowPageBreaks = allowPageBreaks;
        }

        public bool IsEmpty => _children.Count == 0;

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var c = cursor as Cursor;
            int index = c?.Index ?? 0;
            object? childCursor = c?.Child;
            double y = 0;
            bool placed = false;
            var draws = new List<KeyValuePair<double, DrawAction>>();

            Slice Result(object? next, bool forceBreak = false)
            {
                if (!placed && !atTop && next != null) return Slice.Nothing();
                var list = draws;
                return new Slice
                {
                    Height = y,
                    Next = next,
                    ForceBreak = forceBreak,
                    Draw = (g, x, top) =>
                    {
                        foreach (var d in list) d.Value(g, x, top + d.Key);
                    },
                };
            }

            while (index < _children.Count)
            {
                var child = _children[index];
                bool top = atTop && !placed;

                if (childCursor == null)
                {
                    if (child is PageBreakNode)
                    {
                        index++;
                        if (!_allowPageBreaks || (top && y == 0)) continue;
                        return Result(new Cursor(index, null), forceBreak: true);
                    }
                    if (child.PageBreakBefore && _allowPageBreaks && placed)
                        return Result(new Cursor(index, null), forceBreak: true);
                }

                double spaceBefore = childCursor == null && !(top && y == 0) ? child.SpaceBefore : 0;
                var slice = child.Layout(ctx, width, available - y - spaceBefore, childCursor, top);
                if (slice.Empty)
                {
                    if (top) throw new InvalidOperationException("レイアウトが進みません（内部エラー）。");
                    return Result(new Cursor(index, childCursor));
                }

                if (slice.Next == null && child.KeepWithNext && !top && index + 1 < _children.Count)
                {
                    var nextChild = _children[index + 1];
                    if (!(nextChild is PageBreakNode))
                    {
                        double after = y + spaceBefore + slice.Height + child.SpaceAfter;
                        var probe = nextChild.Layout(ctx, width, available - after - nextChild.SpaceBefore, null, false);
                        if (probe.Empty) return Result(new Cursor(index, childCursor));
                    }
                }

                double offset = y + spaceBefore;
                if (slice.Draw != null) draws.Add(new KeyValuePair<double, DrawAction>(offset, slice.Draw));
                y = offset + slice.Height;
                placed = true;

                if (slice.Next != null) return Result(new Cursor(index, slice.Next), slice.ForceBreak);
                y += child.SpaceAfter;
                if (!double.IsInfinity(available) && y > available) y = Math.Max(available, offset + slice.Height);
                index++;
                childCursor = null;
                if (slice.ForceBreak && index < _children.Count) return Result(new Cursor(index, null), forceBreak: true);
            }
            return Result(null);
        }
    }

    internal sealed class PageBreakNode : LayoutNode
    {
        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
            => new Slice { Height = 0, ForceBreak = true };
    }

    internal sealed class SpacerNode : LayoutNode
    {
        private readonly double _height;

        public SpacerNode(double height)
        {
            _height = Math.Max(0, height);
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
            => new Slice { Height = Math.Max(0, Math.Min(_height, available)) };
    }

    internal sealed class ParagraphNode : LayoutNode
    {
        private readonly Paragraph _p;
        private readonly TextStyle _style;
        private readonly ParagraphFormat _format;
        private readonly bool _hasFields;
        private TextBlock? _block;
        private int _blockPage = -1;

        public ParagraphNode(Paragraph p, StyleScope scope)
        {
            _p = p;
            var text = scope.Text;
            var format = scope.Format;
            if (p.StyleName != null)
            {
                if (!scope.Doc.Styles.TryGetValue(p.StyleName, out var named))
                    throw new InvalidOperationException("スタイル '" + p.StyleName + "' は Document.Styles に登録されていません。");
                text = text.Merge(named.Text);
                format = format.Merge(named.Format);
            }
            _style = text.Merge(p.Style);
            _format = format.Merge(p.Format);
            foreach (var inline in p.Inlines)
            {
                if (inline is FieldRun) _hasFields = true;
            }
        }

        public override double SpaceBefore => _format.SpaceBefore ?? 0;
        public override double SpaceAfter => _format.SpaceAfter ?? 0;
        public override bool KeepWithNext => _format.KeepWithNext ?? false;
        public override bool PageBreakBefore => _format.PageBreakBefore ?? false;

        private TextBlock GetBlock(LayoutContext ctx)
        {
            if (_block != null && (!_hasFields || _blockPage == ctx.Page.PageNumber)) return _block;
            var baseStyle = ResolvedStyle.From(_style);
            var segments = new List<KeyValuePair<string, ResolvedStyle>>();
            foreach (var inline in _p.Inlines)
            {
                var style = inline.Style == null ? baseStyle : ResolvedStyle.From(_style.Merge(inline.Style));
                if (inline is TextRun run)
                {
                    if (run.Link != null) style = style.WithLink(run.Link);
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(run.Text, style));
                }
                else if (inline is FieldRun field)
                {
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(FieldText(ctx, field.Field), style));
                }
            }
            _block = TextBlock.Build(segments, baseStyle);
            _blockPage = ctx.Page.PageNumber;
            return _block;
        }

        private static string FieldText(LayoutContext ctx, FieldType field)
        {
            var page = ctx.Page;
            switch (field)
            {
                case FieldType.PageNumber: return page.PageNumber.ToString(CultureInfo.InvariantCulture);
                case FieldType.SectionPageNumber: return page.SectionPageNumber.ToString(CultureInfo.InvariantCulture);
                case FieldType.PageCount:
                    if (page.PageCount <= 0)
                    {
                        ctx.PageCountUsed = true;
                        return "00";
                    }
                    return page.PageCount.ToString(CultureInfo.InvariantCulture);
                default:
                    if (page.SectionPageCount <= 0)
                    {
                        ctx.PageCountUsed = true;
                        return "00";
                    }
                    return page.SectionPageCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var block = GetBlock(ctx);
            var pad = _format.Padding ?? new Thickness(0);
            double left = _format.LeftIndent ?? 0, right = _format.RightIndent ?? 0;
            double firstIndent = _format.FirstLineIndent ?? 0;
            // ぶら下げインデント（1 行目が左に出る）のときは、背景と枠を 1 行目の左端まで広げる
            double hang = Math.Min(0, firstIndent);
            double boxLeft = left + hang;
            double boxWidth = Math.Max(0, width - boxLeft - right);
            double innerWidth = Math.Max(0, boxWidth - pad.Left - pad.Right + hang);
            block.Layout(innerWidth, firstIndent, _format.LineSpacingRule ?? LineSpacingRule.Multiple, _format.LineSpacing ?? 1.5);
            var lines = block.Lines;
            int n = lines.Count;
            int start = cursor is int s ? s : 0;
            double topPad = start == 0 ? pad.Top : 0;

            int end = start;
            double h = topPad;
            while (end < n)
            {
                double need = lines[end].Height + (end == n - 1 ? pad.Bottom : 0);
                if (h + need > available + 1e-6) break;
                h += lines[end].Height;
                end++;
            }
            if (end == start)
            {
                if (!atTop) return Slice.Nothing();
                end = start + 1;
            }
            if (end < n && !atTop)
            {
                if (_format.KeepTogether == true) return Slice.Nothing();
                if (_format.WidowControl ?? true)
                {
                    // 段落の最初の 1 行だけがページ末尾に残らないようにする
                    if (start == 0 && end - start == 1 && n > 1) return Slice.Nothing();
                }
            }
            if (end < n && (_format.WidowControl ?? true) && n - end == 1 && end - start > 2)
            {
                // 最終行だけが次のページに送られないよう、1 行多く送る
                end--;
            }

            double height = topPad;
            for (int i = start; i < end; i++) height += lines[i].Height;
            if (end == n) height += pad.Bottom;

            int from = start, to = end;
            var align = _format.Alignment ?? TextAlignment.Left;
            var background = _format.Background;
            var border = _format.Border;
            double sliceHeight = height;
            DrawAction draw = (g, x, y) =>
            {
                double bx = x + boxLeft;
                if (background.HasValue) g.FillRectangle(background.Value, bx, y, boxWidth, sliceHeight);
                block.Draw(g, x + left + pad.Left, y + topPad, innerWidth, align, from, to);
                if (border != null) g.DrawRectangle(border, bx, y, boxWidth, sliceHeight);
            };
            return new Slice { Height = height, Draw = draw, Next = end < n ? (object)end : null };
        }
    }

    internal sealed class ImageNode : LayoutNode
    {
        private readonly ImageBlock _b;

        public ImageNode(ImageBlock b)
        {
            _b = b;
        }

        public override double SpaceBefore => _b.SpaceBefore;
        public override double SpaceAfter => _b.SpaceAfter;
        public override bool KeepWithNext => _b.KeepWithNext;
        public override bool PageBreakBefore => _b.PageBreakBefore;

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var img = _b.Image;
            double ratio = img.Height / img.Width;
            double w, h;
            if (_b.Width.HasValue && _b.Height.HasValue)
            {
                w = _b.Width.Value;
                h = _b.Height.Value;
            }
            else if (_b.Width.HasValue)
            {
                w = _b.Width.Value;
                h = w * ratio;
            }
            else if (_b.Height.HasValue)
            {
                h = _b.Height.Value;
                w = h / ratio;
            }
            else
            {
                w = img.Width;
                h = img.Height;
            }
            if (_b.ScaleToFit && w > width && width > 0)
            {
                double s = width / w;
                w *= s;
                h *= s;
            }
            if (h > available + 1e-6)
            {
                if (!atTop) return Slice.Nothing();
                if (_b.ScaleToFit && available > 0)
                {
                    double s = available / h;
                    w *= s;
                    h *= s;
                }
            }
            double off = _b.Alignment == HorizontalAlignment.Center ? (width - w) / 2 : _b.Alignment == HorizontalAlignment.Right ? width - w : 0;
            return new Slice { Height = h, Draw = (g, x, y) => g.DrawImage(img, x + off, y, w, h) };
        }
    }

    internal sealed class DrawingNode : LayoutNode
    {
        private readonly DrawingBlock _b;

        public DrawingNode(DrawingBlock b)
        {
            _b = b;
        }

        public override double SpaceBefore => _b.SpaceBefore;
        public override double SpaceAfter => _b.SpaceAfter;
        public override bool KeepWithNext => _b.KeepWithNext;
        public override bool PageBreakBefore => _b.PageBreakBefore;

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            double w = _b.Width ?? width;
            double h = _b.Height;
            if (h > available + 1e-6 && !atTop) return Slice.Nothing();
            double off = _b.Alignment == HorizontalAlignment.Center ? (width - w) / 2 : _b.Alignment == HorizontalAlignment.Right ? width - w : 0;
            var draw = _b.Draw;
            bool clip = _b.Clip;
            return new Slice
            {
                Height = h,
                Draw = (g, x, y) =>
                {
                    g.SaveState();
                    g.Translate(x + off, y);
                    if (clip) g.SetClip(0, 0, w, h);
                    draw(g, new PdfRect(0, 0, w, h));
                    g.RestoreState();
                },
            };
        }
    }
}
