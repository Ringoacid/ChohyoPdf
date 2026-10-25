using System;
using System.Collections.Generic;
using ChohyoPdf.Layout;

namespace ChohyoPdf
{
    /// <summary>
    /// 目次。見出しレベル（<see cref="ParagraphFormat.OutlineLevel"/>）を持つ段落から自動で作る。
    /// 1 行は「見出しの文字列、リーダー（点線）、ページ番号」で、行全体が見出しへの文書内リンクになる。
    /// 見出しはレベルごとにインデントされる。
    /// <para>
    /// ページ番号は、目次自体が複数ページになって後ろの見出しの位置がずれる場合も含め、確定するまでレイアウトを繰り返して求める
    /// （目次または <see cref="FieldType.PageRef"/> を使う文書は、レイアウトに通常の数倍の時間がかかる）。
    /// ページ番号は見出しのあるページが属するセクションの書式（<see cref="Section.PageNumberFormat"/> など）で表示する。
    /// </para>
    /// <para>
    /// 見出しとして数えるのは、セクションの本文（表のセルの中を含む）にある段落だけで、ヘッダー/フッターの段落は数えない。
    /// 目次より前にある見出しも、後ろにある見出しも対象になる。
    /// </para>
    /// </summary>
    public sealed class TableOfContents : BoxBlock
    {
        private Paragraph? _titleParagraph;

        /// <summary>目次の見出しの文字列。null または空なら見出しを置かない。</summary>
        public string? Title { get; set; }

        /// <summary>目次の見出しに使う名前付きスタイル（<see cref="Document.Styles"/>）。</summary>
        public string? TitleStyleName { get; set; }

        /// <summary>目次の見出しの文字スタイル。指定しなければ、名前付きスタイルと文書の既定に従う。</summary>
        public TextStyle? TitleStyle { get; set; }

        /// <summary>目次の見出しの段落書式。指定しなければ、名前付きスタイルと文書の既定に従う。</summary>
        public ParagraphFormat? TitleFormat { get; set; }

        /// <summary>
        /// 目次の見出し自体を見出し（しおりの項目）にするときのレベル（1〜9）。null（既定）ならしおりに入れない。
        /// 目次の項目にも入らない（目次に目次を載せないため、目次の対象は <see cref="MinLevel"/> 以上の他の見出し）。
        /// </summary>
        public int? TitleOutlineLevel { get; set; }

        /// <summary>目次に入れる最小の見出しレベル（既定 1）。</summary>
        public int MinLevel { get; set; } = 1;

        /// <summary>目次に入れる最大の見出しレベル（既定 3）。これより深いレベルの見出しは載せない。</summary>
        public int MaxLevel { get; set; } = 3;

        /// <summary>
        /// 見出しレベルが 1 つ深くなるごとの左インデント（ポイント、既定 14）。
        /// null にすると目次の行にインデントを設定せず、名前付きスタイル（<see cref="LevelStyleNames"/> など）の値に従う。
        /// </summary>
        public double? IndentPerLevel { get; set; } = 14;

        /// <summary>見出しとページ番号の間のリーダー（既定は点線）。</summary>
        public TabLeader Leader { get; set; } = TabLeader.Dots;

        /// <summary>ページ番号を表示するか（既定 true）。</summary>
        public bool ShowPageNumbers { get; set; } = true;

        /// <summary>各行を見出しへの文書内リンクにするか（既定 true）。</summary>
        public bool Hyperlinks { get; set; } = true;

        /// <summary>
        /// ページ番号の書式。null（既定）なら、見出しのあるページのセクションの書式で表示する
        /// （前付けをローマ数字にしたセクション構成なら、ローマ数字で表示される）。
        /// </summary>
        public PageNumberFormat? PageNumberFormat { get; set; }

        /// <summary>目次の各行に使う名前付きスタイル（すべてのレベル共通。<see cref="LevelStyleNames"/> が優先）。</summary>
        public string? EntryStyleName { get; set; }

        /// <summary>レベルごとの名前付きスタイル（キーは見出しレベル）。</summary>
        public Dictionary<int, string> LevelStyleNames { get; } = new Dictionary<int, string>();

        /// <summary>目次の各行の文字スタイル（名前付きスタイルの後に適用）。</summary>
        public TextStyle? EntryStyle { get; set; }

        /// <summary>目次の各行の段落書式（インデントとタブ位置を含め、最後に適用）。</summary>
        public ParagraphFormat? EntryFormat { get; set; }

        /// <summary>見出しの段落を作り直して返す（走査で見出しとして数えるため。見出しなしなら null）。</summary>
        internal Paragraph? PrepareTitle()
        {
            _titleParagraph = BuildTitle();
            return _titleParagraph;
        }

        private Paragraph? BuildTitle()
        {
            if (string.IsNullOrEmpty(Title)) return null;
            var p = new Paragraph(Title!) { StyleName = TitleStyleName, Style = TitleStyle };
            if (TitleFormat != null) p.Format = TitleFormat.Clone();
            else if (TitleStyleName == null && TitleStyle == null) p.Style = new TextStyle { Bold = true, FontSize = 14 };
            if (TitleOutlineLevel.HasValue) p.Format.OutlineLevel = TitleOutlineLevel;
            if (!p.Format.KeepWithNext.HasValue) p.Format.KeepWithNext = true;
            if (!p.Format.SpaceAfter.HasValue && TitleStyleName == null) p.Format.SpaceAfter = 6;
            return p;
        }

        internal override LayoutNode CreateNode(StyleScope scope)
        {
            if (MinLevel < 1 || MaxLevel > 9 || MinLevel > MaxLevel)
                throw new InvalidOperationException("目次のレベルは 1〜9 で、MinLevel が MaxLevel 以下になるように指定してください。");
            var blocks = new List<Block>();
            var title = _titleParagraph ?? BuildTitle();
            if (title != null) blocks.Add(title);

            var index = scope.Index;
            // 索引がないのは、文書の外で描くとき（PdfGraphics.DrawBlocks）だけ
            if (index == null) throw new InvalidOperationException("目次は Document の中でだけ使えます（PdfGraphics.DrawBlocks では使えません）。");
            if (ShowPageNumbers) index.AnchorsRead = true;
            foreach (var heading in index.Headings)
            {
                if (heading.Level < MinLevel || heading.Level > MaxLevel) continue;
                if (title != null && ReferenceEquals(heading.Paragraph, title)) continue;
                blocks.Add(BuildEntry(heading, index));
            }
            return new BoxNode(new ContainerNode(blocks, scope, allowPageBreaks: true), this);
        }

        private Paragraph BuildEntry(HeadingInfo heading, DocumentIndex index)
        {
            var p = new Paragraph();
            if (LevelStyleNames.TryGetValue(heading.Level, out string? levelStyle)) p.StyleName = levelStyle;
            else p.StyleName = EntryStyleName;
            p.Style = EntryStyle;

            var format = new ParagraphFormat();
            if (IndentPerLevel.HasValue) format.LeftIndent = IndentPerLevel.Value * (heading.Level - 1);
            if (ShowPageNumbers) format.AddTabStop(1e6, TabAlignment.Right, Leader);
            p.Format = format.Merge(EntryFormat);

            string? link = Hyperlinks ? heading.Key : null;
            p.Inlines.Add(new TextRun(heading.Title) { LinkAnchor = link });
            if (ShowPageNumbers)
            {
                string text = "00";
                if (index.Assumed.TryGet(heading.Key, out var info))
                    text = PageNumberFormat.HasValue ? PageNumberFormatter.Format(info.DisplayNumber, PageNumberFormat.Value) : info.Text;
                p.Inlines.Add(new TextRun("\t") { LinkAnchor = link });
                p.Inlines.Add(new TextRun(text) { LinkAnchor = link });
            }
            return p;
        }
    }
}

namespace ChohyoPdf.Layout
{
    /// <summary>別のノードを包み、ブロックの配置設定（前後の間隔など）を与える。</summary>
    internal sealed class BoxNode : LayoutNode
    {
        private readonly LayoutNode _inner;
        private readonly BoxBlock _block;

        public BoxNode(LayoutNode inner, BoxBlock block)
        {
            _inner = inner;
            _block = block;
        }

        public override double SpaceBefore => _block.SpaceBefore;
        public override double SpaceAfter => _block.SpaceAfter;
        public override bool KeepWithNext => _block.KeepWithNext;
        public override bool PageBreakBefore => _block.PageBreakBefore;

        public override void MeasureWidths(LayoutContext ctx, out double min, out double max)
            => _inner.MeasureWidths(ctx, out min, out max);

        public override Slice Layout(LayoutContext ctx, double width, double available, object? cursor, bool atTop)
            => _inner.Layout(ctx, width, available, cursor, atTop);
    }
}
