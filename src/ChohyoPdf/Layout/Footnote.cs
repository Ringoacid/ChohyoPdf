using System;
using System.Collections.Generic;
using System.Text;
using ChohyoPdf.Layout;
using ChohyoPdf.Text;

namespace ChohyoPdf
{
    /// <summary>脚注の番号の振り方。<see cref="Section.FootnoteNumbering"/> で指定する。</summary>
    public enum FootnoteNumbering
    {
        /// <summary>前のセクションからの続き番号（文書を通して振る。最初のセクションは 1 から）。</summary>
        Continuous,

        /// <summary>このセクションの最初の脚注を 1 に戻して振り直す。</summary>
        Restart,
    }

    /// <summary>
    /// 脚注の本文。段落とリストなどのブロックを追加できる（<see cref="Paragraph.AddFootnote()"/> で作る）。
    /// 本文には脚注を入れ子にできない（脚注の中の <see cref="Paragraph.AddFootnote()"/> は、レイアウト時に <see cref="InvalidOperationException"/> になる）。
    /// </summary>
    public sealed class Footnote : BlockContainer
    {
        /// <summary>空の脚注を作る。段落に付けるには <see cref="Paragraph.AddFootnote()"/> を使う。</summary>
        public Footnote()
        {
        }
    }

    /// <summary>
    /// 段落の中の脚注の参照記号。本文には上付きの番号が入り、同じ番号の脚注が、参照のあるページの本文領域の下端に置かれる。
    /// 番号は文書の並びの順に自動で振られる（<see cref="Section.FootnoteNumbering"/>、<see cref="Document.FootnoteNumberFormat"/>）。
    /// <see cref="Inline.Style"/> は参照記号の文字スタイル。
    /// </summary>
    public sealed class FootnoteRun : Inline
    {
        /// <summary>脚注の参照記号を作る。</summary>
        public FootnoteRun(Footnote footnote, TextStyle? style = null)
        {
            Footnote = footnote ?? throw new ArgumentNullException(nameof(footnote));
            Style = style;
        }

        /// <summary>脚注の本文。</summary>
        public Footnote Footnote { get; }
    }
}

namespace ChohyoPdf.Layout
{
    /// <summary>脚注 1 つの配置結果（ページの脚注領域に置く高さと描画）。</summary>
    internal sealed class FootnoteEntry
    {
        public FootnoteEntry(double height, DrawAction draw)
        {
            Height = height;
            Draw = draw;
        }

        /// <summary>脚注の前の間隔を含む高さ。</summary>
        public double Height { get; }

        public DrawAction Draw { get; }
    }

    /// <summary>脚注の番号と記号の文字列。</summary>
    internal static class FootnoteText
    {
        /// <summary>脚注の番号（1 始まり）を、文書の指定（記号列または数字の書式）で文字列にする。</summary>
        public static string Format(Document doc, int number)
        {
            var symbols = doc.FootnoteSymbols;
            if (symbols != null && symbols.Count > 0 && number >= 1)
            {
                string symbol = symbols[(number - 1) % symbols.Count] ?? "";
                if (symbol.Length > 0)
                {
                    int repeat = (number - 1) / symbols.Count + 1;
                    var sb = new StringBuilder(symbol.Length * repeat);
                    for (int i = 0; i < repeat; i++) sb.Append(symbol);
                    return sb.ToString();
                }
            }
            return PageNumberFormatter.Format(number, doc.FootnoteNumberFormat);
        }
    }

    /// <summary>
    /// 脚注 1 つ。本文を、番号の記号の分だけ右にずらして配置する（番号は 1 行目に描く）。
    /// 幅はページの本文領域の全幅（段組みでも全段をまたぐ）。
    /// </summary>
    internal sealed class FootnoteItem
    {
        private readonly ListItemNode _node;
        private readonly double _spacing;

        public FootnoteItem(Footnote footnote, string markerText, StyleScope scope, double spacing)
        {
            _spacing = spacing;
            var footnoteScope = scope.ForFootnote();
            var blocks = footnote.Blocks.Count > 0 ? (IEnumerable<Block>)footnote.Blocks : new Block[] { new Paragraph() };
            var content = new ContainerNode(blocks, footnoteScope, allowPageBreaks: false);

            var first = content.FirstChild as ParagraphNode;
            var baseStyle = first != null ? first.ResolvedTextStyle : footnoteScope.Text;
            var block = TextBlock.Build(markerText, ListNode.ResolveMarkerStyle(baseStyle, null));
            block.Layout(1e6, 0, LineSpacingRule.Multiple, 1.0);
            double markerWidth = block.NaturalWidth();
            double gap = (footnoteScope.Text.FontSize ?? 10.5) * 0.5;
            var marker = new ListMarkerDraw(block, markerWidth, markerWidth + gap, TextAlignment.Left);
            ListMarkerDraw? own = marker;
            if (first != null)
            {
                first.ListMarker = marker;
                own = null;
            }
            _node = new ListItemNode(content, markerWidth + gap, 0, own);
        }

        /// <summary>
        /// 幅 <paramref name="width"/> に配置する。呼び出し元の脚注の集計（<see cref="LayoutContext.FootnoteItems"/> など）は
        /// 脚注の本文のレイアウトに影響させない（呼び出しの前後で変わらない）。
        /// </summary>
        public FootnoteEntry Measure(LayoutContext ctx, double width)
        {
            double items = ctx.FootnoteItems, limit = ctx.FootnoteLimit;
            ctx.FootnoteItems = 0;
            ctx.FootnoteLimit = double.PositiveInfinity;
            Slice slice;
            try
            {
                slice = _node.Layout(ctx, Math.Max(0, width), double.PositiveInfinity, null, true);
            }
            finally
            {
                ctx.FootnoteItems = items;
                ctx.FootnoteLimit = limit;
            }
            var draw = slice.Draw;
            double spacing = _spacing;
            return new FootnoteEntry(spacing + slice.Height, (g, x, y) => draw?.Invoke(g, x, y + spacing));
        }
    }
}
