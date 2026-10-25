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

        private int _columnCount = 1;
        private double _columnSpacing = Unit.Mm(8);

        /// <summary>
        /// 段数（既定 1 = 段組みなし）。2 以上にすると、本文は 1 段目の下端まで流れたら次の段の上へ、最後の段の下端で次のページへ進む。
        /// 段の幅は等分で、<see cref="ColumnWidth"/> で取得できる。ヘッダー/フッターと背景/前景は段組みの影響を受けず、ページ全体の幅のまま。
        /// 段数を変えられるのはセクションの区切りだけ（同じページの途中では変えられない）。
        /// </summary>
        public int ColumnCount
        {
            get => _columnCount;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "段数は 1 以上にしてください。");
                _columnCount = value;
            }
        }

        /// <summary>段と段の間隔（ポイント、既定 8 mm）。<see cref="ColumnCount"/> が 1 のときは使われない。</summary>
        public double ColumnSpacing
        {
            get => _columnSpacing;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "段間は 0 以上の有限の値にしてください。");
                _columnSpacing = value;
            }
        }

        /// <summary>
        /// 段と段の間に引く境界線（null なら引かない。既定 null）。線は段の間隔の中央に、本文領域の高さいっぱいに引く
        /// （内容のある段の間だけ。最後のページで内容のない段との間には引かない）。
        /// </summary>
        public PdfPen? ColumnSeparator { get; set; }

        /// <summary>1 つの段の幅（<see cref="ColumnCount"/> が 1 のときは <see cref="ContentWidth"/> と同じ）。</summary>
        public double ColumnWidth => (ContentWidth - ColumnSpacing * (ColumnCount - 1)) / ColumnCount;

        /// <summary>複製を作る。</summary>
        public PageSetup Clone() => new PageSetup
        {
            Size = Size,
            Margins = Margins.Clone(),
            HeaderDistance = HeaderDistance,
            FooterDistance = FooterDistance,
            ColumnCount = ColumnCount,
            ColumnSpacing = ColumnSpacing,
            ColumnSeparator = ColumnSeparator?.Clone(),
        };
    }

    /// <summary>ページの描画時に渡される情報。</summary>
    public sealed class PageContext
    {
        /// <summary>
        /// 文書全体での通しページ番号（1 始まり。物理的なページの順番）。
        /// 奇数・偶数のヘッダー/フッターの切り替えはこの番号で決まる。
        /// 印刷されるページ番号（開始番号や書式の指定を反映したもの）は <see cref="DisplayPageNumber"/> と <see cref="PageNumberText"/>。
        /// </summary>
        public int PageNumber { get; internal set; }

        /// <summary>
        /// 表示用のページ番号（数値）。<see cref="Section.PageNumberStart"/> を指定しなければ <see cref="PageNumber"/> と同じ。
        /// </summary>
        public int DisplayPageNumber { get; internal set; }

        /// <summary>表示用のページ番号を、セクションの書式（<see cref="Section.PageNumberFormat"/>）で文字列にしたもの。</summary>
        public string PageNumberText { get; internal set; } = "";

        /// <summary>このページに適用されるページ番号の書式。</summary>
        public PageNumberFormat PageNumberFormat { get; internal set; }

        /// <summary>通しページ番号（<see cref="PageNumber"/>）が奇数か。</summary>
        public bool IsOddPage => PageNumber % 2 != 0;

        /// <summary>通しページ番号（<see cref="PageNumber"/>）が偶数か。</summary>
        public bool IsEvenPage => PageNumber % 2 == 0;

        /// <summary>文書全体のページ数。</summary>
        public int PageCount { get; internal set; }

        /// <summary>セクション内でのページ番号（1 始まり）。</summary>
        public int SectionPageNumber { get; internal set; }

        /// <summary>セクションのページ数。</summary>
        public int SectionPageCount { get; internal set; }

        /// <summary>用紙サイズ。</summary>
        public PageSize PageSize { get; internal set; }

        /// <summary>
        /// 本文領域（ページ座標）。段組みのセクションでも、すべての段をまとめた領域のまま
        /// （段ごとの領域は <see cref="ColumnAreas"/>）。
        /// </summary>
        public PdfRect ContentArea { get; internal set; }

        /// <summary>このページの段数（<see cref="PageSetup.ColumnCount"/>）。</summary>
        public int ColumnCount { get; internal set; } = 1;

        /// <summary>段ごとの領域（ページ座標、左の段から順）。段組みでなければ <see cref="ContentArea"/> 1 つだけ。</summary>
        public IReadOnlyList<PdfRect> ColumnAreas { get; internal set; } = new PdfRect[0];

        /// <summary>このページの <see cref="PdfPage"/>。</summary>
        public PdfPage Page { get; internal set; } = null!;
    }

    /// <summary>ヘッダーまたはフッター。段落・表などを追加できる。</summary>
    public sealed class HeaderFooter : BlockContainer
    {
        internal HeaderFooter()
        {
        }

        /// <summary>
        /// セクションの 1 ページ目にも表示するか。
        /// <see cref="Section.FirstPageHeader"/> / <see cref="Section.FirstPageFooter"/> では使われない（それらは 1 ページ目専用）。
        /// </summary>
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

        /// <summary>このセクションのページ設定。</summary>
        public PageSetup PageSetup { get; set; }

        /// <summary>ヘッダー。<see cref="DifferentOddEven"/> が true のときは奇数ページ用になる。</summary>
        public HeaderFooter Header { get; } = new HeaderFooter();

        /// <summary>フッター。<see cref="DifferentOddEven"/> が true のときは奇数ページ用になる。</summary>
        public HeaderFooter Footer { get; } = new HeaderFooter();

        /// <summary>
        /// 偶数ページ用のヘッダー/フッター（<see cref="EvenHeader"/> / <see cref="EvenFooter"/>）を使うか。
        /// 奇数か偶数かは、書式や開始番号に関係なく文書全体の通しページ番号（<see cref="PageContext.PageNumber"/>）で決まる
        /// （両面印刷で左右のページが変わるのは物理的なページの順番のため）。
        /// 偶数ページ用に何も追加しなければ、偶数ページには何も表示しない。
        /// </summary>
        public bool DifferentOddEven { get; set; }

        /// <summary>偶数ページ用のヘッダー（<see cref="DifferentOddEven"/> が true のとき）。</summary>
        public HeaderFooter EvenHeader { get; } = new HeaderFooter();

        /// <summary>偶数ページ用のフッター（<see cref="DifferentOddEven"/> が true のとき）。</summary>
        public HeaderFooter EvenFooter { get; } = new HeaderFooter();

        /// <summary>
        /// セクションの 1 ページ目だけ別のヘッダー/フッター（<see cref="FirstPageHeader"/> / <see cref="FirstPageFooter"/>）を使うか。
        /// 奇数・偶数の切り替えよりも優先される。何も追加しなければ 1 ページ目には何も表示しない。
        /// <see cref="HeaderFooter.ShowOnFirstPage"/> の動作は変わらない。
        /// </summary>
        public bool DifferentFirstPage { get; set; }

        /// <summary>セクションの 1 ページ目用のヘッダー（<see cref="DifferentFirstPage"/> が true のとき）。</summary>
        public HeaderFooter FirstPageHeader { get; } = new HeaderFooter();

        /// <summary>セクションの 1 ページ目用のフッター（<see cref="DifferentFirstPage"/> が true のとき）。</summary>
        public HeaderFooter FirstPageFooter { get; } = new HeaderFooter();

        /// <summary>
        /// このセクションの最初のページの表示用ページ番号。null なら前のセクションの続きから数える（最初のセクションは 1）。
        /// 前付けをローマ数字、本文を 1 から、とするときは本文のセクションに 1 を指定する。
        /// </summary>
        public int? PageNumberStart { get; set; }

        /// <summary>
        /// ページ番号の書式。null なら前のセクションから引き継ぐ（最初のセクションはアラビア数字）。
        /// <see cref="FieldType.PageNumber"/> などのフィールドと <see cref="PageContext.PageNumberText"/> に使われる。
        /// </summary>
        public PageNumberFormat? PageNumberFormat { get; set; }

        /// <summary>
        /// 脚注の番号の振り方。<c>Continuous</c>（既定）なら前のセクションからの続き、
        /// <c>Restart</c> ならこのセクションの最初の脚注を 1 から振り直す（最初のセクションは常に 1 から）。
        /// </summary>
        public FootnoteNumbering FootnoteNumbering { get; set; }

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

        /// <summary>既定のフォントを指定せずに文書を作る。フォントは <see cref="DefaultTextStyle"/> などで指定する。</summary>
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

        /// <summary>
        /// 見出しレベル（<see cref="ParagraphFormat.OutlineLevel"/>）を持つ段落から、PDF のしおり（アウトライン）を作るか（既定 true）。
        /// 見出しレベルを指定した段落がなければ、出力は変わらない。
        /// 見出しはレベルの大小で入れ子になる（1 の次に 3 が来たら、3 は 1 の子になる）。
        /// </summary>
        public bool CreateBookmarks { get; set; } = true;

        /// <summary>
        /// PDF を開いたときにしおりの欄を表示するか（既定 false）。しおりが 1 つもない文書では何も変わらない。
        /// </summary>
        public bool ShowBookmarkPane { get; set; }

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

        /// <summary>
        /// 脚注の本文の文字スタイル。指定しない項目は、文書の既定（<see cref="DefaultTextStyle"/>）から決まる既定になる
        /// （文字サイズは本文の 0.85 倍）。表のセルなど、脚注を付けた場所のスタイルは引き継がない。
        /// </summary>
        public TextStyle? FootnoteStyle { get; set; }

        /// <summary>
        /// 脚注の本文の段落書式。指定しない項目は既定（行送りは 1.2 倍、字下げと段落間隔なし）になる。
        /// 脚注の本文の段落に <see cref="Paragraph.StyleName"/> や <see cref="Paragraph.Format"/> を指定すると、それが優先される。
        /// </summary>
        public ParagraphFormat? FootnoteFormat { get; set; }

        /// <summary>脚注の番号の書式（既定はアラビア数字）。<see cref="FootnoteSymbols"/> を指定したときは使われない。</summary>
        public PageNumberFormat FootnoteNumberFormat { get; set; } = PageNumberFormat.Arabic;

        /// <summary>
        /// 番号の代わりに使う記号の列（例: "*", "†", "‡"）。null または空なら <see cref="FootnoteNumberFormat"/> の番号。
        /// 記号を使い切ったら、同じ記号を重ねる（"*", "†", "‡", "**", "††", …）。
        /// </summary>
        public IList<string>? FootnoteSymbols { get; set; }

        /// <summary>
        /// 脚注の領域の上に引く区切り線のペン（既定は黒の 0.5 ポイント）。null なら線を引かない（空きは残る）。
        /// </summary>
        public PdfPen? FootnoteSeparator { get; set; } = new PdfPen(PdfColor.Black, 0.5);

        /// <summary>区切り線の長さ（ポイント）。null（既定）なら本文領域の幅の 3 分の 1。</summary>
        public double? FootnoteSeparatorLength { get; set; }

        /// <summary>脚注と脚注の間（最初の脚注では区切り線との間）の空き（ポイント、既定 2）。</summary>
        public double FootnoteSpacing { get; set; } = 2;

        /// <summary>名前付き段落スタイル（<see cref="Paragraph.StyleName"/> で参照）。</summary>
        public Dictionary<string, ParagraphStyle> Styles { get; } = new Dictionary<string, ParagraphStyle>();

        /// <summary>新しいセクションで使う既定のページ設定。</summary>
        public PageSetup DefaultPageSetup { get; set; } = new PageSetup();

        /// <summary>セクションの一覧。</summary>
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
        public PdfDocument Render() => new DocumentRenderer(this).Render(CreatePdf);

        private PdfDocument CreatePdf()
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
            pdf.ShowBookmarkPane = ShowBookmarkPane;
            return pdf;
        }

        /// <summary>レイアウト後のページ数を返す。</summary>
        public int CountPages() => Render().Pages.Count;

        /// <summary>レイアウトを行い、PDF をファイルに保存する。</summary>
        public void Save(string path) => Render().Save(path);

        /// <summary>レイアウトを行い、PDF をストリームに書き込む。</summary>
        public void Save(Stream stream) => Render().Save(stream);

        /// <summary>レイアウトを行い、PDF をバイト列で返す。</summary>
        public byte[] ToArray() => Render().ToArray();
    }
}
