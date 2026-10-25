using System;
using System.Collections.Generic;
using System.Globalization;
using ChohyoPdf.Text;

namespace ChohyoPdf.Layout
{
    /// <summary>親から引き継ぐ文字スタイルと段落書式。</summary>
    internal sealed class StyleScope
    {
        public StyleScope(Document doc, TextStyle text, ParagraphFormat format, DocumentIndex? index = null, int listLevel = 0, bool noFootnotes = false, bool noWrap = false)
        {
            Doc = doc;
            Text = text;
            Format = format;
            Index = index;
            ListLevel = listLevel;
            NoFootnotes = noFootnotes;
            NoWrap = noWrap;
        }

        /// <summary>
        /// 段落を折り返さない範囲（<see cref="PdfGraphics.DrawBlocks(System.Collections.Generic.IEnumerable{Block}, PdfRect, TextStyle, TextBoxOptions)"/> で <see cref="TextBoxOptions.Wrap"/> が false のとき）。
        /// 表のセルの中には引き継がない。
        /// </summary>
        public bool NoWrap { get; }

        /// <summary>脚注を置けない範囲（ヘッダー/フッター、脚注の本文）か。ここにある脚注の参照記号は例外になる。</summary>
        public bool NoFootnotes { get; }

        /// <summary>文書の既定の文字スタイルと段落書式だけから作った、いちばん外側の範囲。</summary>
        public static StyleScope Root(Document doc, DocumentIndex? index)
        {
            var text = new TextStyle { FontSize = 10.5, Color = PdfColor.Black }.Merge(doc.DefaultTextStyle);
            var format = new ParagraphFormat
            {
                Alignment = TextAlignment.Left,
                LineSpacingRule = LineSpacingRule.Multiple,
                LineSpacing = 1.5,
                WidowControl = true,
            }.Merge(doc.DefaultParagraphFormat);
            return new StyleScope(doc, text, format, index);
        }

        /// <summary>ヘッダー/フッターなど、脚注を置けない範囲にする。</summary>
        public StyleScope WithoutFootnotes() => new StyleScope(Doc, Text, Format, Index, ListLevel, noFootnotes: true, noWrap: NoWrap);

        /// <summary>段落を折り返さない範囲にする。</summary>
        public StyleScope WithoutWrap() => new StyleScope(Doc, Text, Format, Index, ListLevel, NoFootnotes, noWrap: true);

        /// <summary>
        /// 脚注の本文で使う範囲。本文の位置（表のセルやリストなど）に関係なく、文書の既定から始め、
        /// 文字サイズを本文の 0.85 倍（0.5 ポイント単位）、行送りを 1.2 倍にし、字下げと段落間隔をなくしたものに、
        /// <see cref="Document.FootnoteStyle"/> と <see cref="Document.FootnoteFormat"/> を重ねる。
        /// </summary>
        public StyleScope ForFootnote()
        {
            var root = Root(Doc, Index);
            double size = Math.Round((root.Text.FontSize ?? 10.5) * 0.85 * 2, MidpointRounding.AwayFromZero) / 2;
            var text = root.Text.Merge(new TextStyle { FontSize = size }).Merge(Doc.FootnoteStyle);
            var format = root.Format.Merge(new ParagraphFormat
            {
                LineSpacingRule = LineSpacingRule.Multiple,
                LineSpacing = 1.2,
                FirstLineIndent = 0,
                LeftIndent = 0,
                RightIndent = 0,
                SpaceBefore = 0,
                SpaceAfter = 0,
                KeepWithNext = false,
                KeepTogether = false,
                PageBreakBefore = false,
            }).Merge(Doc.FootnoteFormat);
            return new StyleScope(Doc, text, format, Index, 0, noFootnotes: true);
        }

        /// <summary>入れ子になっているリストの深さ（リストの外は 0。最初のリストの項目の中は 1）。記号の既定をレベルごとに変えるために使う。</summary>
        public int ListLevel { get; }

        public Document Doc { get; }

        /// <summary>見出し・アンカーの索引（文書をレイアウトするときだけ設定される）。</summary>
        public DocumentIndex? Index { get; }
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
        }), Index, 0, NoFootnotes);

        /// <summary>
        /// リストの項目の中で使う範囲。本文用の字下げ（1 行目・左）と段落前の改ページは引き継がない（項目の位置はリストが決める）。
        /// そのほかの書式（揃え・行送り・段落間隔など）は引き継ぎ、リスト自身の指定を重ねる。
        /// </summary>
        public StyleScope ForList(TextStyle? text, ParagraphFormat? format)
            => new StyleScope(Doc, Text.Merge(text), Format.Merge(new ParagraphFormat
            {
                FirstLineIndent = 0,
                LeftIndent = 0,
                PageBreakBefore = false,
            }).Merge(format), Index, ListLevel + 1, NoFootnotes, NoWrap);

        public StyleScope With(TextStyle? text, ParagraphFormat? format = null)
        {
            if (text == null && format == null) return this;
            return new StyleScope(Doc, Text.Merge(text), Format.Merge(format), Index, ListLevel, NoFootnotes, NoWrap);
        }
    }

    internal sealed class LayoutContext
    {
        public PageContext Page = new PageContext();

        /// <summary>本文で総ページ数フィールドが使われたか（使われたら 2 回目のレイアウトを行う）。</summary>
        public bool PageCountUsed;

        /// <summary>
        /// 描画時にアンカーと見出しの位置を記録する先。本文のレイアウトだけに設定され、
        /// ヘッダー/フッターでは null（そこにある見出しやアンカーは記録しない）。
        /// </summary>
        public PassCollector? Collector;

        /// <summary>本文の段数（1 なら段組みなし）。段の幅に収まらない固定幅の表などを縮めるかの判定に使う。</summary>
        public int ColumnCount = 1;

        /// <summary>
        /// いま配置している段より前の段に、このページの内容があるか。
        /// 段の先頭では「ページの先頭」と同じに扱うが、改ページだけは前の段に内容があれば次のページへ進める。
        /// </summary>
        public bool PriorColumnContent;

        /// <summary>
        /// このページに、いまレイアウトしているノードより前で確定した脚注の本文の高さの合計（区切り線は含まない）。
        /// ノードに渡される利用可能な高さは、この脚注が使う領域を引いたもの。ContainerNode と、ページと段を進める処理（DocumentRenderer）だけが
        /// 一時的に変えて、レイアウトから戻るときは必ず元に戻す（レイアウトは何度試しても結果が同じになる）。
        /// </summary>
        public double FootnoteItems;

        /// <summary>脚注の領域（区切り線を含む）の高さの上限。段組みで、前の段の内容に脚注が重ならないようにする。</summary>
        public double FootnoteLimit = double.PositiveInfinity;

        /// <summary>脚注の区切り線の分の高さ（最初の脚注を置くときだけ使う）。</summary>
        public double FootnoteSeparatorHeight;

        /// <summary>脚注の本文の高さの合計が <paramref name="items"/> のときの、脚注の領域の高さ（脚注がなければ 0）。</summary>
        public double FootnoteArea(double items) => items > 0 ? items + FootnoteSeparatorHeight : 0;

        /// <summary>これまでの脚注に <paramref name="add"/> を足したときに増える領域の高さ（最初の脚注なら区切り線の分を含む）。</summary>
        public double FootnoteExtra(double add) => add > 0 ? FootnoteArea(FootnoteItems + add) - FootnoteArea(FootnoteItems) : 0;
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

        /// <summary>この片の後で改ページ（段組みでは、次の段へ）する。</summary>
        public bool ForceBreak;

        /// <summary><see cref="ForceBreak"/> が段区切りによるもの（段組みでは次の段へ進み、改ページとは違い次のページにはならない）。</summary>
        public bool ColumnBreak;

        /// <summary>この片に含まれる参照記号の脚注の本文の高さの合計（前の間隔を含み、区切り線は含まない）。</summary>
        public double FootnoteHeight;

        /// <summary>この片に含まれる参照記号の脚注（本文の並びの順）。</summary>
        public List<FootnoteEntry>? Footnotes;

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

        /// <summary>
        /// 内容の幅の計測（表の列幅を内容に合わせるときに使う）。<paramref name="min"/> は、折り返しや縮小を限度いっぱいまで行ったときの幅、
        /// <paramref name="max"/> は、折り返さないときの幅（<paramref name="min"/> 以上）。
        /// 計測は副作用を持たず、レイアウトの状態を変えない（予備のレイアウトで何度呼ばれても、レイアウトの前後でも同じ結果になる）。
        /// 既定は 0 と 0（利用できる幅いっぱいに広がるもの、幅を持たないものは、幅の決定に関わらない）。
        /// </summary>
        public virtual void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            min = 0;
            max = 0;
        }
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

        /// <summary>配置済みのノードを積む（リストなど、ブロックから作らないノードを並べるとき）。</summary>
        public ContainerNode(IEnumerable<LayoutNode> nodes, bool allowPageBreaks)
        {
            _children = new List<LayoutNode>(nodes);
            _allowPageBreaks = allowPageBreaks;
        }

        public bool IsEmpty => _children.Count == 0;

        /// <summary>子の幅の最大値。</summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            min = 0;
            max = 0;
            foreach (var child in _children)
            {
                child.MeasureWidths(ctx, out double childMin, out double childMax);
                min = Math.Max(min, childMin);
                max = Math.Max(max, childMax);
            }
        }

        /// <summary>最初の子（なければ null）。</summary>
        public LayoutNode? FirstChild => _children.Count > 0 ? _children[0] : null;

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var c = cursor as Cursor;
            int index = c?.Index ?? 0;
            object? childCursor = c?.Child;
            double y = 0;
            bool placed = false;
            var draws = new List<KeyValuePair<double, DrawAction>>();

            // 脚注: 子が確定するごとに、その脚注を集計する。ctx.FootnoteItems は子をレイアウトする間だけ変え、戻るときに必ず戻す
            double footnoteBase = ctx.FootnoteItems;
            double footnoteSum = 0;
            List<FootnoteEntry>? footnotes = null;

            Slice Result(object? next, bool forceBreak = false, bool columnBreak = false)
            {
                ctx.FootnoteItems = footnoteBase;
                if (!placed && !atTop && next != null) return Slice.Nothing();
                var list = draws;
                return new Slice
                {
                    Height = y,
                    Next = next,
                    FootnoteHeight = footnoteSum,
                    Footnotes = footnotes,
                    ForceBreak = forceBreak,
                    ColumnBreak = forceBreak && columnBreak,
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
                    if (child is PageBreakNode breakNode)
                    {
                        index++;
                        // 段（ページ）の先頭の区切りは無視する。ただし改ページは、前の段に内容があれば次のページへ進める
                        if (!_allowPageBreaks || (top && y == 0 && (breakNode.IsColumnBreak || !ctx.PriorColumnContent))) continue;
                        return Result(new Cursor(index, null), forceBreak: true, columnBreak: breakNode.IsColumnBreak);
                    }
                    if (child.PageBreakBefore && _allowPageBreaks && (placed || ctx.PriorColumnContent))
                        return Result(new Cursor(index, null), forceBreak: true);
                }

                double spaceBefore = childCursor == null && !(top && y == 0) ? child.SpaceBefore : 0;
                ctx.FootnoteItems = footnoteBase + footnoteSum;
                double reserved = ctx.FootnoteArea(footnoteBase + footnoteSum) - ctx.FootnoteArea(footnoteBase);
                var slice = child.Layout(ctx, width, available - y - spaceBefore - reserved, childCursor, top);
                ctx.FootnoteItems = footnoteBase;
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
                        ctx.FootnoteItems = footnoteBase + footnoteSum + slice.FootnoteHeight;
                        double probeReserved = ctx.FootnoteArea(ctx.FootnoteItems) - ctx.FootnoteArea(footnoteBase);
                        var probe = nextChild.Layout(ctx, width, available - after - nextChild.SpaceBefore - probeReserved, null, false);
                        ctx.FootnoteItems = footnoteBase;
                        if (probe.Empty) return Result(new Cursor(index, childCursor));
                    }
                }

                double offset = y + spaceBefore;
                if (slice.Draw != null) draws.Add(new KeyValuePair<double, DrawAction>(offset, slice.Draw));
                y = offset + slice.Height;
                placed = true;
                if (slice.Footnotes != null)
                {
                    footnotes = footnotes ?? new List<FootnoteEntry>();
                    footnotes.AddRange(slice.Footnotes);
                    footnoteSum += slice.FootnoteHeight;
                }

                if (slice.Next != null) return Result(new Cursor(index, slice.Next), slice.ForceBreak, slice.ColumnBreak);
                y += child.SpaceAfter;
                if (!double.IsInfinity(available) && y > available) y = Math.Max(available, offset + slice.Height);
                index++;
                childCursor = null;
                if (slice.ForceBreak && index < _children.Count) return Result(new Cursor(index, null), forceBreak: true, columnBreak: slice.ColumnBreak);
            }
            return Result(null);
        }
    }

    /// <summary>改ページ（<see cref="IsColumnBreak"/> が true なら段区切り）。</summary>
    internal sealed class PageBreakNode : LayoutNode
    {
        public PageBreakNode(bool column = false)
        {
            IsColumnBreak = column;
        }

        public bool IsColumnBreak { get; }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
            => new Slice { Height = 0, ForceBreak = true, ColumnBreak = IsColumnBreak };
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
        private readonly TabLayout? _tabs;
        private readonly DocumentIndex? _index;
        private readonly HeadingInfo? _heading;
        private readonly bool _noWrap;
        private TextBlock? _block;
        private int _blockPage = -1;

        /// <summary>段落の脚注（参照記号の並びの順）。脚注がなければ null。</summary>
        private readonly List<FootnoteItem>? _footnotes;
        private readonly List<string>? _footnoteMarks;

        /// <summary>段落の確定した文字スタイル（リストの記号のスタイルの元になる）。</summary>
        public TextStyle ResolvedTextStyle => _style;

        /// <summary>リストの項目の最初の段落なら、1 行目に描く記号（それ以外は null）。</summary>
        public ListMarkerDraw? ListMarker { get; set; }

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
            _tabs = TabLayout.Create(_format.TabStops, _format.DefaultTabStop);
            _noWrap = scope.NoWrap;
            _index = scope.Index;
            if (_index != null) _index.ByParagraph.TryGetValue(p, out _heading);
            foreach (var inline in p.Inlines)
            {
                if (inline is FootnoteRun footnote)
                {
                    if (scope.NoFootnotes)
                        throw new InvalidOperationException("この場所には脚注を置けません（脚注を使えるのは、セクションの本文、リストの項目、表のセルの段落です）。");
                    if (_index == null || !_index.Scan.FootnoteNumbers.TryGetValue(footnote, out int number))
                        throw new InvalidOperationException("脚注の番号が決まっていません（内部エラー）。");
                    string mark = FootnoteText.Format(scope.Doc, number);
                    _footnotes = _footnotes ?? new List<FootnoteItem>();
                    _footnoteMarks = _footnoteMarks ?? new List<string>();
                    _footnotes.Add(new FootnoteItem(footnote.Footnote, mark, scope, scope.Doc.FootnoteSpacing));
                    _footnoteMarks.Add(mark);
                }
                else if (inline is FieldRun field)
                {
                    _hasFields = true;
                    if (field.Field == FieldType.PageRef)
                    {
                        if (string.IsNullOrEmpty(field.Anchor))
                            throw new InvalidOperationException("ページ参照（FieldType.PageRef）にはアンカー名（FieldRun.Anchor）が必要です。");
                        // 索引がないのは、文書の外で描くとき（PdfGraphics.DrawBlocks）だけ。ページを確定するレイアウトの繰り返しができない
                        if (_index == null) throw new InvalidOperationException("ページ参照は Document の中でだけ使えます（PdfGraphics.DrawBlocks では使えません）。");
                        _index.Referenced.Add(field.Anchor!);
                    }
                }
                else if (inline is TextRun run && run.Link == null && run.LinkAnchor != null)
                {
                    _index?.Referenced.Add(run.LinkAnchor);
                }
            }
        }

        public override double SpaceBefore => _format.SpaceBefore ?? 0;
        public override double SpaceAfter => _format.SpaceAfter ?? 0;
        public override bool KeepWithNext => _format.KeepWithNext ?? false;
        public override bool PageBreakBefore => _format.PageBreakBefore ?? false;

        /// <summary>
        /// 段落の幅。文字列の幅（<see cref="TextBlock.MeasureWidths"/>）に、左右のインデントと余白を加える。
        /// ページ番号などのフィールドは、いまのページでの表示の文字列で数える。
        /// </summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            var block = GetBlock(ctx);
            var pad = _format.Padding ?? new Thickness(0);
            double extra = Math.Max(0, _format.LeftIndent ?? 0) + Math.Max(0, _format.RightIndent ?? 0) + pad.Left + pad.Right;
            block.MeasureWidths(_format.FirstLineIndent ?? 0, out min, out max);
            min += extra;
            max += extra;
        }

        private TextBlock GetBlock(LayoutContext ctx)
        {
            if (_block != null && (!_hasFields || _blockPage == ctx.Page.PageNumber)) return _block;
            var baseStyle = ResolvedStyle.From(_style);
            var segments = new List<KeyValuePair<string, ResolvedStyle>>();
            int footnoteIndex = 0;
            foreach (var inline in _p.Inlines)
            {
                var style = inline.Style == null ? baseStyle : ResolvedStyle.From(_style.Merge(inline.Style));
                if (inline is FootnoteRun)
                {
                    // 参照記号は上付きの番号。行を調べるために、番号の文字に脚注の通し番号（1 始まり）を付ける
                    var mark = ResolvedStyle.From(inline.Style == null ? _style : _style.Merge(inline.Style));
                    mark.Shift = BaselineShift.Superscript;
                    footnoteIndex++;
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(_footnoteMarks![footnoteIndex - 1], mark.WithFootnote(footnoteIndex)));
                }
                else if (inline is TextRun run)
                {
                    if (run.Link != null) style = style.WithLink(run.Link);
                    else if (run.LinkAnchor != null) style = style.WithLinkAnchor(run.LinkAnchor);
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(run.Text, style));
                }
                else if (inline is RubyRun ruby)
                {
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(ruby.BaseText, WithRuby(style, inline.Style, ruby)));
                }
                else if (inline is AnchorRun anchor)
                {
                    segments.Add(new KeyValuePair<string, ResolvedStyle>("", style.WithMarker(anchor.Name)));
                }
                else if (inline is FieldRun field)
                {
                    segments.Add(new KeyValuePair<string, ResolvedStyle>(FieldText(ctx, field), style));
                }
            }
            _block = TextBlock.Build(segments, baseStyle);
            _blockPage = ctx.Page.PageNumber;
            return _block;
        }

        /// <summary>
        /// 親文字のスタイルにルビを付ける。ルビのスタイルは親文字のスタイルを引き継ぎ、文字サイズは親文字の半分、
        /// 下線・取り消し線・マーカー・上付き下付き・字間は引き継がない。段落の <see cref="Paragraph.RubyStyle"/>、<see cref="RubyRun.RubyStyle"/> の順に重ねる。
        /// </summary>
        private ResolvedStyle WithRuby(ResolvedStyle parent, TextStyle? inlineStyle, RubyRun ruby)
        {
            if (ruby.RubyText.Length == 0) return parent;
            var rubyStyle = (inlineStyle == null ? _style : _style.Merge(inlineStyle)).Clone();
            rubyStyle.Underline = false;
            rubyStyle.Strikeout = false;
            rubyStyle.BackgroundColor = null;
            rubyStyle.BaselineShift = BaselineShift.None;
            rubyStyle.CharacterSpacing = 0;
            rubyStyle.FontSize = parent.DrawSize / 2;
            rubyStyle = rubyStyle.Merge(_p.RubyStyle).Merge(ruby.RubyStyle);
            return parent.WithRuby(ruby.RubyText, ResolvedStyle.From(rubyStyle));
        }

        private string FieldText(LayoutContext ctx, FieldRun field)
        {
            var page = ctx.Page;
            var format = field.Format ?? page.PageNumberFormat;
            switch (field.Field)
            {
                case FieldType.PageRef:
                {
                    // 前回までのレイアウトで分かったページを使う（分かるまでは仮の文字列。確定は DocumentRenderer の繰り返しで行う）
                    if (_index != null) _index.AnchorsRead = true;
                    if (_index != null && _index.Assumed.TryGet(field.Anchor ?? "", out var info))
                        return field.Format.HasValue ? PageNumberFormatter.Format(info.DisplayNumber, field.Format.Value) : info.Text;
                    return "00";
                }
                case FieldType.PageNumber:
                    return PageNumberFormatter.Format(page.DisplayPageNumber, format);
                case FieldType.PhysicalPageNumber:
                    return PageNumberFormatter.Format(page.PageNumber, format);
                case FieldType.SectionPageNumber:
                    return PageNumberFormatter.Format(page.SectionPageNumber, format);
                case FieldType.PageCount:
                    if (page.PageCount <= 0)
                    {
                        ctx.PageCountUsed = true;
                        return "00";
                    }
                    return PageNumberFormatter.Format(page.PageCount, field.Format ?? PageNumberFormat.Arabic);
                default:
                    if (page.SectionPageCount <= 0)
                    {
                        ctx.PageCountUsed = true;
                        return "00";
                    }
                    return PageNumberFormatter.Format(page.SectionPageCount, format);
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
            // 折り返さないときも、揃えは本文の幅の中で行う（DrawTextBox と同じ）
            block.Layout(_noWrap ? double.MaxValue / 4 : innerWidth, firstIndent, _format.LineSpacingRule ?? LineSpacingRule.Multiple, _format.LineSpacing ?? 1.5, _tabs);
            var lines = block.Lines;
            int n = lines.Count;
            int start = cursor is int s ? s : 0;
            double topPad = start == 0 ? pad.Top : 0;

            // 脚注: 参照記号のある行を置くと、その脚注もこのページの脚注領域に入れる必要がある（入らなければ行ごと次へ送る）
            var footnoteLines = _footnotes != null ? FootnoteLines(block) : null;
            double footnoteWidth = ctx.Page.ContentArea.Width;
            var measured = footnoteLines != null ? new FootnoteEntry?[_footnotes!.Count] : null;
            double LineFootnotes(int line)
            {
                double sum = 0;
                if (footnoteLines == null) return sum;
                foreach (int id in footnoteLines[line] ?? NoFootnotes)
                {
                    var entry = measured![id] ?? (measured[id] = _footnotes![id].Measure(ctx, footnoteWidth));
                    sum += entry.Height;
                }
                return sum;
            }

            int end = start;
            double h = topPad;
            double footnoteAdd = 0;
            while (end < n)
            {
                double need = lines[end].Height + (end == n - 1 ? pad.Bottom : 0);
                double lineFootnotes = LineFootnotes(end);
                if (h + need + ctx.FootnoteExtra(footnoteAdd + lineFootnotes) > available + 1e-6) break;
                if (lineFootnotes > 0 && ctx.FootnoteArea(ctx.FootnoteItems + footnoteAdd + lineFootnotes) > ctx.FootnoteLimit + 1e-6) break;
                h += lines[end].Height;
                footnoteAdd += lineFootnotes;
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

            // 置く行（[start, end)）の脚注。折り返しの調整で行数が変わった後の範囲で集計する
            List<FootnoteEntry>? placedFootnotes = null;
            double placedFootnoteHeight = 0;
            if (footnoteLines != null)
            {
                for (int i = start; i < end; i++)
                {
                    foreach (int id in footnoteLines[i] ?? NoFootnotes)
                    {
                        var entry = measured![id] ?? (measured[id] = _footnotes![id].Measure(ctx, footnoteWidth));
                        placedFootnotes = placedFootnotes ?? new List<FootnoteEntry>();
                        placedFootnotes.Add(entry);
                        placedFootnoteHeight += entry.Height;
                    }
                }
            }

            int from = start, to = end;
            var align = _format.Alignment ?? TextAlignment.Left;
            var collector = ctx.Collector;
            var heading = _heading;
            string? anchor = _p.Anchor;
            var background = _format.Background;
            var border = _format.Border;
            double sliceHeight = height;
            var listMarker = ListMarker;
            double firstBaseline = lines[0].Baseline;
            DrawAction draw = (g, x, y) =>
            {
                // リストの記号は項目の 1 行目にだけ付ける（ベースラインを 1 行目に合わせる）
                if (listMarker != null && from == 0)
                    listMarker.Draw(g, x - listMarker.ColumnOffset, y + topPad + firstBaseline - listMarker.Baseline);
                double bx = x + boxLeft;
                if (background.HasValue) g.FillRectangle(background.Value, bx, y, boxWidth, sliceHeight);
                Action<string, double, double>? onMarker = null;
                if (collector != null)
                {
                    // アンカーと見出しは、段落の最初の行を含むページに記録する。段落の途中のアンカーはその行の位置に記録する
                    if (from == 0)
                    {
                        if (anchor != null) collector.Register(anchor, g, bx, y);
                        if (heading != null) collector.RegisterHeading(heading, g, bx, y);
                    }
                    onMarker = (name, mx, my) => collector.Register(name, g, mx, my);
                }
                block.Draw(g, x + left + pad.Left, y + topPad, innerWidth, align, from, to, onMarker);
                if (border != null) g.DrawRectangle(border, bx, y, boxWidth, sliceHeight);
            };
            return new Slice
            {
                Height = height,
                Draw = draw,
                Next = end < n ? (object)end : null,
                FootnoteHeight = placedFootnoteHeight,
                Footnotes = placedFootnotes,
            };
        }

        private static readonly int[] NoFootnotes = new int[0];

        /// <summary>行ごとの、その行にある参照記号の脚注の番号（脚注の並びの順の 0 始まり。ない行は null）。</summary>
        private int[]?[] FootnoteLines(TextBlock block)
        {
            var result = new int[]?[block.Lines.Count];
            var refs = block.FootnoteRefs();
            foreach (var r in refs)
            {
                var existing = result[r.Key];
                if (existing == null) result[r.Key] = new[] { r.Value - 1 };
                else
                {
                    var grown = new int[existing.Length + 1];
                    Array.Copy(existing, grown, existing.Length);
                    grown[existing.Length] = r.Value - 1;
                    result[r.Key] = grown;
                }
            }
            return result;
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

        /// <summary>
        /// 画像の幅。縮小できる（<see cref="ImageBlock.ScaleToFit"/>）画像の最小は 0、そうでなければ指定または自然な大きさ。
        /// </summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            Size(out double w, out _);
            max = w;
            min = _b.ScaleToFit ? 0 : w;
        }

        /// <summary>指定（幅・高さ・自然な大きさ）から決まる画像の大きさ。</summary>
        private void Size(out double w, out double h)
        {
            var img = _b.Image;
            double ratio = img.Height / img.Width;
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
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var img = _b.Image;
            Size(out double w, out double h);
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

        /// <summary>幅を指定した図はその幅。指定のない図（幅いっぱいに広がる）は幅の決定に関わらない（0）。</summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            min = max = Math.Max(0, _b.Width ?? 0);
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            double w = _b.Width ?? width;
            // 段組みでは、段の幅より広い図は段の幅に縮める（隣の段にはみ出さない）
            if (ctx.ColumnCount > 1 && w > width) w = Math.Max(0, width);
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
                    // ページ番号を確定するための予備の描画では、利用者の描画処理を呼ばない
                    if (g.IsMeasuring) return;
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
