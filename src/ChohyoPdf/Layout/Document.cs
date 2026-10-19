using System;
using System.Collections.Generic;
using System.IO;
using ChohyoPdf.Layout;

namespace ChohyoPdf
{
    /// <summary>ページの設定（用紙・余白・ヘッダー/フッターの位置）。</summary>
    public sealed class PageSetup
    {
        /// <summary>用紙サイズ。</summary>
        public PageSize Size { get; set; } = PageSize.A4;

        /// <summary>
        /// 余白（本文領域の外側）。ヘッダーとフッターはこの余白の中に置かれるので、
        /// 上下の余白はヘッダー/フッターの高さを考慮して設定する。
        /// </summary>
        public Thickness Margins { get; set; } = Thickness.FromMm(20);

        /// <summary>用紙上端からヘッダーの上端までの距離。</summary>
        public double HeaderDistance { get; set; } = Unit.Mm(8);

        /// <summary>用紙下端からフッターの下端までの距離。</summary>
        public double FooterDistance { get; set; } = Unit.Mm(8);

        /// <summary>本文領域の幅。</summary>
        public double ContentWidth => Size.Width - Margins.Left - Margins.Right;

        /// <summary>本文領域の高さ。</summary>
        public double ContentHeight => Size.Height - Margins.Top - Margins.Bottom;

        public PageSetup Clone() => new PageSetup
        {
            Size = Size,
            Margins = Margins.Clone(),
            HeaderDistance = HeaderDistance,
            FooterDistance = FooterDistance,
        };
    }

    /// <summary>ページの描画時に渡される情報。</summary>
    public sealed class PageContext
    {
        /// <summary>文書全体での通しページ番号（1 始まり）。</summary>
        public int PageNumber { get; internal set; }

        /// <summary>文書全体のページ数。</summary>
        public int PageCount { get; internal set; }

        /// <summary>セクション内でのページ番号（1 始まり）。</summary>
        public int SectionPageNumber { get; internal set; }

        /// <summary>セクションのページ数。</summary>
        public int SectionPageCount { get; internal set; }

        /// <summary>用紙サイズ。</summary>
        public PageSize PageSize { get; internal set; }

        /// <summary>本文領域（ページ座標）。</summary>
        public PdfRect ContentArea { get; internal set; }

        /// <summary>このページの <see cref="PdfPage"/>。</summary>
        public PdfPage Page { get; internal set; } = null!;
    }

    /// <summary>ヘッダーまたはフッター。段落・表などを追加できる。</summary>
    public sealed class HeaderFooter : BlockContainer
    {
        internal HeaderFooter()
        {
        }

        /// <summary>1 ページ目にも表示するか。</summary>
        public bool ShowOnFirstPage { get; set; } = true;
    }

    /// <summary>
    /// セクション。用紙設定・ヘッダー・フッターを共有するページのまとまり。
    /// 本文のブロックは自動で折り返し・改ページされる。
    /// </summary>
    public sealed class Section : BlockContainer
    {
        internal Section(PageSetup setup)
        {
            PageSetup = setup;
        }

        public PageSetup PageSetup { get; set; }

        public HeaderFooter Header { get; } = new HeaderFooter();

        public HeaderFooter Footer { get; } = new HeaderFooter();

        /// <summary>本文の前（背景）に描画する処理。透かしや帳票の枠などに使う。</summary>
        public Action<PdfGraphics, PageContext>? PageBackground { get; set; }

        /// <summary>本文の後（前景）に描画する処理。</summary>
        public Action<PdfGraphics, PageContext>? PageForeground { get; set; }
    }

    /// <summary>
    /// 自動レイアウトする文書。セクションに段落・表・画像・図を追加すると、
    /// 折り返しと改ページを自動で行って PDF を生成する。
    /// </summary>
    public sealed class Document
    {
        private readonly List<Section> _sections = new List<Section>();

        public Document()
        {
        }

        /// <summary>既定のフォントを指定して文書を作る。</summary>
        public Document(PdfFont defaultFont, double defaultFontSize = 10.5)
        {
            DefaultTextStyle.Font = defaultFont;
            DefaultTextStyle.FontSize = defaultFontSize;
        }

        /// <summary>文書情報（タイトル・作成者など）。</summary>
        public PdfDocumentInfo Info { get; } = new PdfDocumentInfo();

        /// <summary>文書の言語。</summary>
        public string? Language { get; set; } = "ja-JP";

        /// <summary>既定の文字スタイル。少なくとも <see cref="TextStyle.Font"/> を設定する。</summary>
        public TextStyle DefaultTextStyle { get; set; } = new TextStyle { FontSize = 10.5, Color = PdfColor.Black };

        /// <summary>既定の段落書式。</summary>
        public ParagraphFormat DefaultParagraphFormat { get; set; } = new ParagraphFormat
        {
            Alignment = TextAlignment.Left,
            LineSpacingRule = ChohyoPdf.LineSpacingRule.Multiple,
            LineSpacing = 1.5,
            FirstLineIndent = 0,
            LeftIndent = 0,
            RightIndent = 0,
            SpaceBefore = 0,
            SpaceAfter = 0,
            KeepTogether = false,
            KeepWithNext = false,
            PageBreakBefore = false,
            WidowControl = true,
        };

        /// <summary>名前付き段落スタイル（<see cref="Paragraph.StyleName"/> で参照）。</summary>
        public Dictionary<string, ParagraphStyle> Styles { get; } = new Dictionary<string, ParagraphStyle>();

        /// <summary>新しいセクションで使う既定のページ設定。</summary>
        public PageSetup DefaultPageSetup { get; set; } = new PageSetup();

        public IReadOnlyList<Section> Sections => _sections;

        /// <summary>
        /// セクションを追加する。<paramref name="setup"/> を省略すると <see cref="DefaultPageSetup"/> の複製を使う。
        /// </summary>
        public Section AddSection(PageSetup? setup = null)
        {
            var section = new Section(setup ?? DefaultPageSetup.Clone());
            _sections.Add(section);
            return section;
        }

        /// <summary>最後のセクション（なければ追加する）。</summary>
        public Section LastSection => _sections.Count > 0 ? _sections[_sections.Count - 1] : AddSection();

        /// <summary>レイアウトを行い、低レベルの <see cref="PdfDocument"/> を生成する（追加で描画したい場合に使う）。</summary>
        public PdfDocument Render()
        {
            var pdf = new PdfDocument();
            pdf.Info.Title = Info.Title;
            pdf.Info.Author = Info.Author;
            pdf.Info.Subject = Info.Subject;
            pdf.Info.Keywords = Info.Keywords;
            pdf.Info.Creator = Info.Creator;
            pdf.Info.Producer = Info.Producer;
            pdf.Info.CreationDate = Info.CreationDate;
            pdf.Language = Language;
            new DocumentRenderer(this).Render(pdf);
            return pdf;
        }

        /// <summary>レイアウト後のページ数を返す。</summary>
        public int CountPages() => Render().Pages.Count;

        public void Save(string path) => Render().Save(path);

        public void Save(Stream stream) => Render().Save(stream);

        public byte[] ToArray() => Render().ToArray();
    }
}
