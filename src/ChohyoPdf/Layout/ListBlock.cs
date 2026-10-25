using System;
using System.Collections.Generic;
using ChohyoPdf.Layout;
using ChohyoPdf.Text;

namespace ChohyoPdf
{
    /// <summary>リストの種類。</summary>
    public enum ListKind
    {
        /// <summary>箇条書き（「・」「●」などの記号）。</summary>
        Bullet,

        /// <summary>番号付きリスト（1. / (1) / ① / 一、 など）。</summary>
        Numbered,
    }

    /// <summary>
    /// リストの項目。段落・表・画像・子リストなど、複数のブロックを入れられる（<see cref="BlockContainer"/>）。
    /// 記号は項目の最初のブロックが段落ならその 1 行目に付く（段落でなければ項目の上端に付く）。
    /// </summary>
    public sealed class ListBlockItem : BlockContainer
    {
        /// <summary>この項目の文字スタイル（リストのスタイルの後に適用）。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>この項目の段落書式（リストの書式の後に適用）。</summary>
        public ParagraphFormat? Format { get; set; }

        /// <summary>
        /// この項目の番号（番号付きリストのとき）。指定すると以降の項目はこの番号に続く。
        /// null なら前の項目の次の番号（最初の項目は <see cref="ListBlock.StartNumber"/>）。
        /// </summary>
        public int? Number { get; set; }
    }

    /// <summary>
    /// リスト（箇条書き・番号付きリスト）。項目は 1 行目に記号が付き、2 行目以降の行頭は本文の開始位置にそろう（ぶら下げインデント）。
    /// 項目は段落と同じように折り返され、ページをまたいで分割される（記号は項目の 1 行目にだけ付き、番号はページをまたいでも連続する）。
    /// <para>
    /// 項目の中に子リストを入れると入れ子になる。記号を指定しなければ、レベルごとの既定が使われる
    /// （箇条書きは ・ → ○ → ■、番号付きは 1. → (1) → ①。4 レベル目以降は繰り返す）。
    /// </para>
    /// <para>
    /// 項目の文字スタイルと段落書式は、文書の既定値 → 表 → 行 → セル → リスト（<see cref="Style"/> / <see cref="Format"/>） → 項目（<see cref="ListBlockItem.Style"/> / <see cref="ListBlockItem.Format"/>）
    /// → 名前付きスタイル → 段落の順に上書きされる。項目の位置はリストが決めるので、本文用の字下げ（1 行目・左インデント）と段落前の改ページは引き継がない。
    /// リストの中の改ページ（<see cref="PageBreak"/>）は無視される。
    /// </para>
    /// </summary>
    public sealed class ListBlock : BoxBlock
    {
        /// <summary>リストの種類。</summary>
        public ListKind Kind { get; set; } = ListKind.Bullet;

        /// <summary>項目。</summary>
        public List<ListBlockItem> Items { get; } = new List<ListBlockItem>();

        /// <summary>
        /// 箇条書きの記号（任意の文字列）。null なら入れ子のレベルごとの既定（・ → ○ → ■）。
        /// 番号付きリスト（<see cref="Kind"/> が <see cref="ListKind.Numbered"/>）では使われない。
        /// </summary>
        public string? Bullet { get; set; }

        /// <summary>
        /// 番号の書式（<see cref="PageNumberFormat"/> を再利用する）。null なら入れ子のレベルごとの既定（1. → (1) → ①）。
        /// 箇条書きでは使われない。
        /// </summary>
        public PageNumberFormat? NumberFormat { get; set; }

        /// <summary>番号の前に付ける文字列（例: "("）。null なら既定（レベルごとの既定の書式なら "(" か空、書式を指定したときは空）。</summary>
        public string? Prefix { get; set; }

        /// <summary>
        /// 番号の後ろに付ける文字列（例: ")" や "、"）。null なら既定（レベルごとの既定の書式ならそれに従い、
        /// 書式を指定したときは丸数字なら付けず、それ以外は "."）。空文字列にすると何も付けない。
        /// </summary>
        public string? Suffix { get; set; }

        /// <summary>最初の項目の番号（既定 1）。</summary>
        public int StartNumber { get; set; } = 1;

        /// <summary>
        /// 記号を置く列の幅（ポイント）。null なら、このリストの記号のうち一番幅の広いものに合わせる
        /// （番号が 9. から 10. になっても、本文の開始位置は全項目でそろう）。
        /// 記号がこの幅より広いと、はみ出す（左揃えなら本文側へ、右揃えなら左側へ）。
        /// </summary>
        public double? MarkerWidth { get; set; }

        /// <summary>記号の列と本文の間隔（ポイント）。null なら文字サイズの 0.5 倍。</summary>
        public double? MarkerGap { get; set; }

        /// <summary>
        /// 記号の列の中での位置（既定は左揃え）。<see cref="HorizontalAlignment.Right"/> にすると、
        /// 9. → 10. のように桁が増えても、記号の右端（本文の手前）がそろう。
        /// </summary>
        public HorizontalAlignment MarkerAlignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>
        /// 記号の文字スタイル（色・フォント・太字など）。指定しなかった項目は、項目の最初の段落の文字スタイルに従う
        /// （下線・取り消し線・マーカー・上付き下付きは引き継がない）。
        /// </summary>
        public TextStyle? MarkerStyle { get; set; }

        /// <summary>リスト全体の左端からの字下げ（ポイント）。記号の列はここから始まる。</summary>
        public double Indent { get; set; }

        /// <summary>項目と項目の間の間隔（ポイント）。ページの先頭では無視される。</summary>
        public double ItemSpacing { get; set; }

        /// <summary>リスト全体の文字スタイル（項目の段落が引き継ぐ）。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>リスト全体の段落書式（項目の段落が引き継ぐ）。</summary>
        public ParagraphFormat? Format { get; set; }

        /// <summary>空の項目を追加する。返した項目に段落・表・子リストなどを追加する。</summary>
        public ListBlockItem AddItem()
        {
            var item = new ListBlockItem();
            Items.Add(item);
            return item;
        }

        /// <summary>段落 1 つだけの項目を追加し、その段落を返す（続けて <see cref="Paragraph.AddText"/> などで書式を付けられる）。</summary>
        public Paragraph AddItem(string text, TextStyle? style = null)
            => AddItem().AddParagraph(text, style);

        /// <summary>名前付きスタイルを指定して、段落 1 つだけの項目を追加する。</summary>
        public Paragraph AddItem(string text, string styleName)
            => AddItem().AddParagraph(text, styleName);

        /// <summary>
        /// 最後の項目（なければ空の項目を追加する）に子リストを追加する。種類を省略すると、このリストと同じ種類になる。
        /// </summary>
        public ListBlock AddSubList(ListKind? kind = null)
        {
            var item = Items.Count > 0 ? Items[Items.Count - 1] : AddItem();
            return item.AddList(kind ?? Kind);
        }

        // ---- 書式設定（メソッドチェーン用） ----

        /// <summary>記号の列の幅と、本文との間隔を指定する（ポイント）。</summary>
        public ListBlock SetMarkerBox(double? width, double? gap = null)
        {
            MarkerWidth = width;
            MarkerGap = gap;
            return this;
        }

        /// <summary>記号の文字スタイルを指定する。</summary>
        public ListBlock SetMarkerStyle(TextStyle style)
        {
            MarkerStyle = style;
            return this;
        }

        /// <summary>記号の列の中での位置を指定する。</summary>
        public ListBlock SetMarkerAlignment(HorizontalAlignment alignment)
        {
            MarkerAlignment = alignment;
            return this;
        }

        internal override LayoutNode CreateNode(StyleScope scope) => new ListNode(this, scope);
    }
}

namespace ChohyoPdf.Layout
{
    /// <summary>項目の 1 行目に描く記号。</summary>
    internal sealed class ListMarkerDraw
    {
        private readonly TextBlock _block;
        private readonly TextAlignment _alignment;

        public ListMarkerDraw(TextBlock block, double width, double columnOffset, TextAlignment alignment)
        {
            _block = block;
            Width = width;
            ColumnOffset = columnOffset;
            _alignment = alignment;
        }

        /// <summary>記号の列の幅。</summary>
        public double Width { get; }

        /// <summary>記号の列の左端から本文の左端までの距離。</summary>
        public double ColumnOffset { get; }

        /// <summary>記号の行の上端からベースラインまでの距離。</summary>
        public double Baseline => _block.Lines[0].Baseline;

        /// <summary>記号の列の左端が <paramref name="columnX"/>、記号の行の上端が <paramref name="top"/> になるように描く。</summary>
        public void Draw(PdfGraphics g, double columnX, double top)
            => _block.Draw(g, columnX, top, Width, _alignment, 0, 1);
    }

    /// <summary>リストの項目 1 つ。本文を記号の分だけ右にずらして配置し、記号を描く。</summary>
    internal sealed class ListItemNode : LayoutNode
    {
        private readonly ContainerNode _content;
        private readonly double _offset;
        private readonly double _spaceBefore;
        private readonly ListMarkerDraw? _ownMarker;

        // ownMarker: 項目が自分で描く記号（最初のブロックが段落でないとき。段落なら段落が描く）
        public ListItemNode(ContainerNode content, double offset, double spaceBefore, ListMarkerDraw? ownMarker)
        {
            _content = content;
            _offset = offset;
            _spaceBefore = spaceBefore;
            _ownMarker = ownMarker;
        }

        public override double SpaceBefore => _spaceBefore;

        /// <summary>本文の幅に、記号の列の幅を加える。</summary>
        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
        {
            _content.MeasureWidths(ctx, out min, out max);
            min += _offset;
            max += _offset;
        }

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
        {
            var inner = _content.Layout(ctx, Math.Max(0, width - _offset), available, cursor, atTop);
            if (inner.Empty) return inner;
            var marker = cursor == null ? _ownMarker : null;
            var draw = inner.Draw;
            double offset = _offset;
            return new Slice
            {
                Height = inner.Height,
                Next = inner.Next,
                FootnoteHeight = inner.FootnoteHeight,
                Footnotes = inner.Footnotes,
                ForceBreak = inner.ForceBreak,
                ColumnBreak = inner.ColumnBreak,
                Draw = (g, x, y) =>
                {
                    marker?.Draw(g, x + offset - marker.ColumnOffset, y);
                    draw?.Invoke(g, x + offset, y);
                },
            };
        }
    }

    /// <summary>リスト。項目の記号と番号を決め、項目を縦に積む。</summary>
    internal sealed class ListNode : LayoutNode
    {
        private static readonly string[] DefaultBullets = { "・", "○", "■" };

        private readonly ListBlock _list;
        private readonly ContainerNode _items;

        public ListNode(ListBlock list, StyleScope scope)
        {
            _list = list;
            int level = scope.ListLevel;
            var listScope = scope.ForList(list.Style, list.Format);

            // 項目の本文を作る（空の項目は、記号を付ける空の段落を 1 つ置く）
            var contents = new List<ContainerNode>();
            var itemScopes = new List<StyleScope>();
            foreach (var item in list.Items)
            {
                var itemScope = listScope.With(item.Style, item.Format);
                var blocks = item.Blocks.Count > 0 ? (IEnumerable<Block>)item.Blocks : new Block[] { new Paragraph() };
                contents.Add(new ContainerNode(blocks, itemScope, allowPageBreaks: false));
                itemScopes.Add(itemScope);
            }

            // 記号の文字列と TextBlock
            var markerBlocks = new List<TextBlock>();
            double widest = 0;
            int number = list.StartNumber;
            for (int i = 0; i < contents.Count; i++)
            {
                if (i > 0) number++;
                if (list.Items[i].Number.HasValue) number = list.Items[i].Number!.Value;
                string text = MarkerText(level, number);

                var first = contents[i].FirstChild as ParagraphNode;
                var baseStyle = first != null ? first.ResolvedTextStyle : itemScopes[i].Text;
                var resolved = ResolveMarkerStyle(baseStyle, list.MarkerStyle);
                var block = TextBlock.Build(text, resolved);
                block.Layout(1e6, 0, LineSpacingRule.Multiple, 1.0);
                markerBlocks.Add(block);
                widest = Math.Max(widest, block.NaturalWidth());
            }

            double fontSize = listScope.Text.FontSize ?? 10.5;
            double markerWidth = list.MarkerWidth ?? widest;
            double gap = list.MarkerGap ?? fontSize * 0.5;
            double columnOffset = markerWidth + gap;
            var alignment = list.MarkerAlignment == HorizontalAlignment.Right ? TextAlignment.Right
                : list.MarkerAlignment == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Left;

            var nodes = new List<LayoutNode>();
            for (int i = 0; i < contents.Count; i++)
            {
                var marker = new ListMarkerDraw(markerBlocks[i], markerWidth, columnOffset, alignment);
                ListMarkerDraw? own = marker;
                if (contents[i].FirstChild is ParagraphNode paragraph)
                {
                    paragraph.ListMarker = marker;
                    own = null;
                }
                nodes.Add(new ListItemNode(contents[i], list.Indent + columnOffset, i > 0 ? list.ItemSpacing : 0, own));
            }
            _items = new ContainerNode(nodes, allowPageBreaks: false);
        }

        public override double SpaceBefore => _list.SpaceBefore;
        public override double SpaceAfter => _list.SpaceAfter;
        public override bool KeepWithNext => _list.KeepWithNext;
        public override bool PageBreakBefore => _list.PageBreakBefore;

        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
            => _items.MeasureWidths(ctx, out min, out max);

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
            => _items.Layout(ctx, width, available, cursor, atTop);

        private string MarkerText(int level, int number)
        {
            if (_list.Kind == ListKind.Bullet)
                return _list.Bullet ?? DefaultBullets[level % DefaultBullets.Length];

            PageNumberFormat format;
            string prefix, suffix;
            if (_list.NumberFormat.HasValue)
            {
                format = _list.NumberFormat.Value;
                prefix = "";
                suffix = format == PageNumberFormat.CircledNumber ? "" : ".";
            }
            else
            {
                // 既定: 1. → (1) → ①（4 レベル目以降は繰り返す）
                switch (level % 3)
                {
                    case 0:
                        format = PageNumberFormat.Arabic;
                        prefix = "";
                        suffix = ".";
                        break;
                    case 1:
                        format = PageNumberFormat.Arabic;
                        prefix = "(";
                        suffix = ")";
                        break;
                    default:
                        format = PageNumberFormat.CircledNumber;
                        prefix = "";
                        suffix = "";
                        break;
                }
            }
            return (_list.Prefix ?? prefix) + PageNumberFormatter.Format(number, format) + (_list.Suffix ?? suffix);
        }

        /// <summary>記号の文字スタイル。基準の文字スタイルに <paramref name="marker"/> を重ねる（装飾の下線・取り消し線・背景・上付き下付きは引き継がない）。</summary>
        internal static ResolvedStyle ResolveMarkerStyle(TextStyle baseStyle, TextStyle? marker)
        {
            var resolved = ResolvedStyle.From(baseStyle.Merge(marker));
            resolved.Underline = marker?.Underline ?? false;
            resolved.Strikeout = marker?.Strikeout ?? false;
            resolved.Background = marker?.BackgroundColor;
            resolved.Shift = marker?.BaselineShift ?? BaselineShift.None;
            return resolved;
        }
    }
}
