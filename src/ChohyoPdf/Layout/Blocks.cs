using System;
using System.Collections.Generic;
using ChohyoPdf.Layout;

namespace ChohyoPdf
{
    /// <summary>文書に配置するブロック要素（段落・表・画像・図など）の基底クラス。</summary>
    public abstract class Block
    {
        internal abstract LayoutNode CreateNode(StyleScope scope);
    }

    /// <summary>段落以外のブロック（表・画像・図）に共通の配置設定。</summary>
    public abstract class BoxBlock : Block
    {
        /// <summary>前の間隔（ポイント）。ページやセルの先頭では無視される。</summary>
        public double SpaceBefore { get; set; }

        /// <summary>後の間隔（ポイント）。</summary>
        public double SpaceAfter { get; set; }

        /// <summary>次のブロックと同じページに置く。</summary>
        public bool KeepWithNext { get; set; }

        /// <summary>このブロックの前で改ページする。</summary>
        public bool PageBreakBefore { get; set; }
    }

    /// <summary>ブロックを縦に並べる入れ物（セクション、ヘッダー/フッター、表のセル）。</summary>
    public abstract class BlockContainer
    {
        /// <summary>含まれるブロック。</summary>
        public List<Block> Blocks { get; } = new List<Block>();

        /// <summary>ブロックを追加する。</summary>
        public T Add<T>(T block) where T : Block
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            Blocks.Add(block);
            return block;
        }

        /// <summary>段落を追加する。</summary>
        public Paragraph AddParagraph(string? text = null, TextStyle? style = null)
        {
            var p = new Paragraph();
            if (text != null) p.AddText(text);
            if (style != null) p.Style = style;
            return Add(p);
        }

        /// <summary>名前付きスタイルを指定して段落を追加する。</summary>
        public Paragraph AddParagraph(string text, string styleName)
        {
            var p = new Paragraph { StyleName = styleName };
            p.AddText(text);
            return Add(p);
        }

        /// <summary>表を追加する。</summary>
        public Table AddTable() => Add(new Table());

        /// <summary>画像を追加する。幅・高さの片方だけを指定すると縦横比を保つ。</summary>
        public ImageBlock AddImage(PdfImage image, double? width = null, double? height = null)
            => Add(new ImageBlock(image) { Width = width, Height = height });

        /// <summary>
        /// 図（ベクター描画領域）を追加する。<paramref name="draw"/> には左上が (0, 0) の座標系と領域の矩形が渡される。
        /// </summary>
        public DrawingBlock AddDrawing(double width, double height, Action<PdfGraphics, PdfRect> draw)
            => Add(new DrawingBlock(height, draw) { Width = width });

        /// <summary>本文幅いっぱいの図を追加する。</summary>
        public DrawingBlock AddDrawing(double height, Action<PdfGraphics, PdfRect> draw)
            => Add(new DrawingBlock(height, draw));

        /// <summary>改ページを追加する。</summary>
        public void AddPageBreak() => Blocks.Add(new PageBreak());

        /// <summary>
        /// 段区切りを追加する。段組みのセクション（<see cref="PageSetup.ColumnCount"/> が 2 以上）では、これ以降の内容を次の段の上から始める
        /// （最後の段なら次のページの 1 段目）。段組みでないセクションでは改ページと同じ。
        /// 段の先頭にある段区切りは無視される。
        /// </summary>
        public void AddColumnBreak() => Blocks.Add(new ColumnBreak());

        /// <summary>縦方向の空白を追加する。</summary>
        public void AddSpacer(double height) => Blocks.Add(new Spacer(height));

        /// <summary>
        /// 目次を追加する。見出しレベル（<see cref="ParagraphFormat.OutlineLevel"/>）を持つ段落から自動で作り、
        /// 各行はその見出しへの文書内リンクになる。
        /// </summary>
        /// <param name="title">目次の見出し（null で見出しなし）。</param>
        /// <param name="maxLevel">目次に入れる見出しレベルの上限（1〜9）。</param>
        public TableOfContents AddTableOfContents(string? title = "目次", int maxLevel = 3)
            => Add(new TableOfContents { Title = title, MaxLevel = maxLevel });

        /// <summary>
        /// リスト（箇条書き・番号付きリスト）を追加する。項目は <see cref="ListBlock.AddItem(string, TextStyle?)"/> で追加する。
        /// 記号を指定しなければ、レベルごとの既定の記号になる。
        /// </summary>
        public ListBlock AddList(ListKind kind = ListKind.Bullet)
            => Add(new ListBlock { Kind = kind });

        /// <summary>箇条書きを追加する。</summary>
        /// <param name="bullet">記号（「・」「●」「–」など任意の文字列）。null なら入れ子のレベルごとの既定（・ → ○ → ■）。</param>
        public ListBlock AddBulletList(string? bullet = null)
            => Add(new ListBlock { Kind = ListKind.Bullet, Bullet = bullet });

        /// <summary>番号付きリストを追加する。</summary>
        /// <param name="format">番号の書式。null なら入れ子のレベルごとの既定（1. → (1) → ①）。</param>
        /// <param name="startNumber">最初の項目の番号。</param>
        /// <param name="prefix">番号の前に付ける文字列（例: "("）。null なら既定。</param>
        /// <param name="suffix">番号の後ろに付ける文字列（例: ")" や "、"）。null なら既定（書式を指定したときは "."、丸数字は付けない）。</param>
        public ListBlock AddNumberedList(PageNumberFormat? format = null, int startNumber = 1, string? prefix = null, string? suffix = null)
            => Add(new ListBlock { Kind = ListKind.Numbered, NumberFormat = format, StartNumber = startNumber, Prefix = prefix, Suffix = suffix });

        /// <summary>水平線を追加する。</summary>
        public HorizontalLine AddHorizontalLine(PdfPen? pen = null)
            => Add(new HorizontalLine { Pen = pen ?? new PdfPen(PdfColor.Black, 0.5) });
    }

    /// <summary>段落内の要素。</summary>
    public abstract class Inline
    {
        /// <summary>この要素の文字スタイル（段落のスタイルを上書き）。</summary>
        public TextStyle? Style { get; set; }
    }

    /// <summary>テキスト。</summary>
    public sealed class TextRun : Inline
    {
        /// <summary>文字スタイルを指定してテキストを作る。</summary>
        public TextRun(string text, TextStyle? style = null)
        {
            Text = text ?? "";
            Style = style;
        }

        /// <summary>テキスト。</summary>
        public string Text { get; set; }

        /// <summary>リンク先 URL。</summary>
        public string? Link { get; set; }

        /// <summary>
        /// 文書内リンクの飛び先のアンカー名（<see cref="Paragraph.SetAnchor"/> / <see cref="Paragraph.AddAnchor"/> で付けた名前）。
        /// <see cref="Link"/> も指定したときは <see cref="Link"/> が優先される。
        /// 存在しないアンカー名を指定すると、レイアウト時（<see cref="Document.Render"/> など）に <see cref="InvalidOperationException"/> になる。
        /// </summary>
        public string? LinkAnchor { get; set; }
    }

    /// <summary>
    /// 段落の中の任意の位置に置くアンカー（飛び先の名前）。文字は描かない。
    /// <see cref="TextRun.LinkAnchor"/> や <see cref="FieldType.PageRef"/> から参照できる。
    /// </summary>
    public sealed class AnchorRun : Inline
    {
        /// <summary>アンカー名を指定して作る。</summary>
        public AnchorRun(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("アンカー名を指定してください。", nameof(name));
            Name = name;
        }

        /// <summary>アンカー名。</summary>
        public string Name { get; }
    }

    /// <summary>フィールドの種類。</summary>
    public enum FieldType
    {
        /// <summary>
        /// ページ番号（表示用）。セクションの開始番号と書式（<see cref="Section.PageNumberStart"/> / <see cref="Section.PageNumberFormat"/>）に従う。
        /// どちらも指定しなければ通しページ番号と同じ。
        /// </summary>
        PageNumber,

        /// <summary>総ページ数（物理的なページ数）。<see cref="FieldRun.Format"/> を指定しない限りアラビア数字。</summary>
        PageCount,

        /// <summary>セクション内のページ番号（セクションの書式に従う）。</summary>
        SectionPageNumber,

        /// <summary>セクションのページ数（セクションの書式に従う）。</summary>
        SectionPageCount,

        /// <summary>開始番号の指定に関係ない、文書全体での通しページ番号（物理的なページの順番）。書式はセクションに従う。</summary>
        PhysicalPageNumber,

        /// <summary>
        /// アンカー（<see cref="FieldRun.Anchor"/>）がある位置のページ番号。後ろのページにあるアンカー（前方参照）も参照できる。
        /// 表示は、そのページが属するセクションの書式（<see cref="Section.PageNumberFormat"/> と開始番号を反映したもの）で、
        /// <see cref="FieldRun.Format"/> を指定すると書式だけ変える。
        /// </summary>
        PageRef,
    }

    /// <summary>ページ番号などのフィールド。</summary>
    public sealed class FieldRun : Inline
    {
        /// <summary>フィールドの種類と文字スタイルを指定して作る。</summary>
        public FieldRun(FieldType field, TextStyle? style = null)
        {
            Field = field;
            Style = style;
        }

        /// <summary>フィールドの種類。</summary>
        public FieldType Field { get; set; }

        /// <summary><see cref="FieldType.PageRef"/> が参照するアンカー名。</summary>
        public string? Anchor { get; set; }

        /// <summary>数字の書式。null ならフィールドの種類ごとの既定（<see cref="FieldType"/> を参照）に従う。</summary>
        public PageNumberFormat? Format { get; set; }
    }

    /// <summary>段落。文字列は自動で折り返され（禁則処理あり）、必要ならページをまたいで分割される。</summary>
    public sealed class Paragraph : Block
    {
        /// <summary>空の段落を作る。</summary>
        public Paragraph()
        {
        }

        /// <summary>テキストと文字スタイルを指定して段落を作る。</summary>
        public Paragraph(string text, TextStyle? style = null)
        {
            AddText(text);
            Style = style;
        }

        /// <summary>段落内の要素。</summary>
        public List<Inline> Inlines { get; } = new List<Inline>();

        /// <summary>段落全体の文字スタイル。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>
        /// この段落のルビの既定の文字スタイル（色・フォント・文字サイズなど）。<see cref="RubyRun.RubyStyle"/> や
        /// <see cref="AddRuby(string, string, TextStyle?, TextStyle?)"/> の <c>rubyStyle</c> で上書きできる。文字サイズを指定しなければ親文字の半分になる。
        /// </summary>
        public TextStyle? RubyStyle { get; set; }

        /// <summary>段落の書式。</summary>
        public ParagraphFormat Format { get; set; } = new ParagraphFormat();

        /// <summary><see cref="Document.Styles"/> に登録した名前付きスタイル。</summary>
        public string? StyleName { get; set; }

        /// <summary>
        /// 段落の先頭（段落が複数ページにまたがるときは最初のページの先頭）にアンカーを付ける。
        /// 文書内リンク（<see cref="AddInternalLink"/>）やページ参照（<see cref="AddPageRef(string, TextStyle?)"/>）の飛び先になる。
        /// アンカー名は文書の本文内で一意にする（重複するとレイアウト時に例外になる）。
        /// </summary>
        public string? Anchor { get; set; }

        /// <summary>
        /// しおりと目次に出す文字列。null なら段落の文字列（フィールドを除く。改行とタブは空白 1 つにまとめる）。
        /// 見出しレベル（<see cref="ParagraphFormat.OutlineLevel"/>）がある段落だけに使われる。
        /// </summary>
        public string? OutlineTitle { get; set; }

        /// <summary>テキストを追加する。改行文字（\n）で改行できる。</summary>
        public Paragraph AddText(string text, TextStyle? style = null)
        {
            Inlines.Add(new TextRun(text, style));
            return this;
        }

        /// <summary>太字のテキストを追加する。</summary>
        public Paragraph AddBold(string text) => AddText(text, new TextStyle { Bold = true });

        /// <summary>上付き文字を追加する（文字を 0.65 倍に縮小し、ベースラインを上げて描く）。</summary>
        public Paragraph AddSuperscript(string text, TextStyle? style = null)
            => AddText(text, WithBaselineShift(style, BaselineShift.Superscript));

        /// <summary>下付き文字を追加する（文字を 0.65 倍に縮小し、ベースラインを下げて描く）。</summary>
        public Paragraph AddSubscript(string text, TextStyle? style = null)
            => AddText(text, WithBaselineShift(style, BaselineShift.Subscript));

        private static TextStyle WithBaselineShift(TextStyle? style, BaselineShift shift)
        {
            var result = style == null ? new TextStyle() : style.Clone();
            result.BaselineShift = shift;
            return result;
        }

        /// <summary>リンク付きのテキストを追加する。</summary>
        public Paragraph AddLink(string text, string uri, TextStyle? style = null)
        {
            Inlines.Add(new TextRun(text, style ?? new TextStyle { Color = PdfColor.FromRgb(0, 0, 238), Underline = true }) { Link = uri });
            return this;
        }

        /// <summary>段落を指定のアンカー名で参照できるようにする（<see cref="Anchor"/>）。</summary>
        public Paragraph SetAnchor(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("アンカー名を指定してください。", nameof(name));
            Anchor = name;
            return this;
        }

        /// <summary>段落の途中の、この位置にアンカーを置く（文字は描かない）。</summary>
        public Paragraph AddAnchor(string name)
        {
            Inlines.Add(new AnchorRun(name));
            return this;
        }

        /// <summary>
        /// 文書内のアンカーへ移動するリンク付きのテキストを追加する（<see cref="SetAnchor"/> / <see cref="AddAnchor"/> で付けた位置へ移動する）。
        /// <paramref name="style"/> を省略すると、青い下線付きで表示する。存在しないアンカー名は、レイアウト時に例外になる。
        /// </summary>
        public Paragraph AddInternalLink(string text, string anchorName, TextStyle? style = null)
        {
            if (string.IsNullOrEmpty(anchorName)) throw new ArgumentException("アンカー名を指定してください。", nameof(anchorName));
            Inlines.Add(new TextRun(text, style ?? new TextStyle { Color = PdfColor.FromRgb(0, 0, 238), Underline = true }) { LinkAnchor = anchorName });
            return this;
        }

        /// <summary>
        /// アンカーがあるページのページ番号を追加する（そのページのセクションの書式で表示する。後ろのページでもよい）。
        /// 存在しないアンカー名は、レイアウト時に <see cref="InvalidOperationException"/> になる。
        /// </summary>
        public Paragraph AddPageRef(string anchorName, TextStyle? style = null)
        {
            if (string.IsNullOrEmpty(anchorName)) throw new ArgumentException("アンカー名を指定してください。", nameof(anchorName));
            Inlines.Add(new FieldRun(FieldType.PageRef, style) { Anchor = anchorName });
            return this;
        }

        /// <summary>書式を指定して、アンカーがあるページのページ番号を追加する。</summary>
        public Paragraph AddPageRef(string anchorName, PageNumberFormat format, TextStyle? style = null)
        {
            AddPageRef(anchorName, style);
            ((FieldRun)Inlines[Inlines.Count - 1]).Format = format;
            return this;
        }

        /// <summary>
        /// この段落を見出しにする（しおりと目次の対象）。<paramref name="level"/> は 1〜9。
        /// 名前付きスタイルの <see cref="ParagraphFormat.OutlineLevel"/> でも指定できる。
        /// </summary>
        /// <param name="level">見出しレベル（1 が最上位）。</param>
        /// <param name="title">しおりと目次に出す文字列（省略すると段落の文字列）。</param>
        public Paragraph SetOutline(int level, string? title = null)
        {
            Format.OutlineLevel = level;
            if (title != null) OutlineTitle = title;
            return this;
        }

        /// <summary>
        /// ルビ（振り仮名）付きの文字列を追加する。ルビは親文字の中央にそろえ、文字サイズは親文字の半分になる。
        /// ルビが親文字より長いときは、親文字の文字の間を均等に空けて、ルビの幅に合わせる。親文字とルビの組は途中で改行しない。
        /// ルビがある行は、ルビの分だけ行が高くなる（<see cref="LineSpacingRule.Exactly"/> の行送りでは行の高さを変えない）。
        /// </summary>
        /// <param name="baseText">親文字（改行・タブは含められない）。</param>
        /// <param name="rubyText">ルビ。null や空なら普通の文字列として追加する。</param>
        /// <param name="style">親文字の文字スタイル。</param>
        /// <param name="rubyStyle">ルビの文字スタイル（色・フォント・文字サイズなど。親文字のスタイルを引き継ぐ。<see cref="RubyStyle"/> より優先）。</param>
        public Paragraph AddRuby(string baseText, string rubyText, TextStyle? style = null, TextStyle? rubyStyle = null)
        {
            Inlines.Add(new RubyRun(baseText, rubyText, style, rubyStyle));
            return this;
        }

        /// <summary>
        /// モノルビを追加する。親文字を 1 文字ずつ区切り、<paramref name="rubies"/> の同じ順番のルビを付ける
        /// （例: <c>AddMonoRuby("東京", new[] { "とう", "きょう" })</c>）。ルビを付けない文字は空文字列にする。
        /// 組ごとに改行できる。
        /// </summary>
        /// <param name="baseText">親文字。ルビの数と文字数（サロゲートペアは 1 文字）が同じであること。</param>
        /// <param name="rubies">親文字の 1 文字ごとのルビ。</param>
        /// <param name="style">親文字の文字スタイル。</param>
        /// <param name="rubyStyle">ルビの文字スタイル。</param>
        public Paragraph AddMonoRuby(string baseText, IList<string> rubies, TextStyle? style = null, TextStyle? rubyStyle = null)
        {
            if (string.IsNullOrEmpty(baseText)) throw new ArgumentException("親文字を指定してください。", nameof(baseText));
            if (rubies == null) throw new ArgumentNullException(nameof(rubies));
            var chars = RubyMarkup.SplitElements(baseText);
            if (chars.Count != rubies.Count)
                throw new ArgumentException("ルビの数(" + rubies.Count + ")が親文字の文字数(" + chars.Count + ")と合いません。", nameof(rubies));
            for (int i = 0; i < chars.Count; i++) AddRuby(chars[i], rubies[i], style, rubyStyle);
            return this;
        }

        /// <summary>
        /// 親文字とルビの組を、配列で順番に追加する（<paramref name="baseTexts"/>[i] にルビ <paramref name="rubies"/>[i] を付ける）。
        /// 組の間に普通の文字列を挟みたいときは、<see cref="AddRuby(string, string, TextStyle?, TextStyle?)"/> と <see cref="AddText"/> を交互に呼ぶか、<see cref="AddRubyMarkup"/> を使う。
        /// </summary>
        public Paragraph AddRuby(IList<string> baseTexts, IList<string> rubies, TextStyle? style = null, TextStyle? rubyStyle = null)
        {
            if (baseTexts == null) throw new ArgumentNullException(nameof(baseTexts));
            if (rubies == null) throw new ArgumentNullException(nameof(rubies));
            if (baseTexts.Count != rubies.Count)
                throw new ArgumentException("ルビの数(" + rubies.Count + ")が親文字の数(" + baseTexts.Count + ")と合いません。", nameof(rubies));
            for (int i = 0; i < baseTexts.Count; i++) AddRuby(baseTexts[i], rubies[i], style, rubyStyle);
            return this;
        }

        /// <summary>
        /// ルビ記法の文字列を解析して追加する。記法は次のとおり（青空文庫と同じ）。
        /// <list type="bullet">
        /// <item><description><c>｜親文字《るび》</c>: 「｜」（全角または半角の縦線）から「《」までが親文字、「《」から「》」までがルビ。</description></item>
        /// <item><description><c>東京《とうきょう》</c>: 「｜」がなければ、「《」の直前に続く漢字（々・〆・〇を含む）が親文字。</description></item>
        /// <item><description><c>東京《とう|きょう》</c>: ルビの中を「|」で区切り、区切りの数が親文字の文字数と同じなら、モノルビ（1 文字ずつ）。</description></item>
        /// <item><description>「《」「》」「｜」「|」は記号として扱う。文字そのものを入れるときは、<see cref="AddText"/> で追加する。</description></item>
        /// </list>
        /// 記法が正しくないとき（「》」がない、親文字がない、モノルビの数が合わないなど）は <see cref="ArgumentException"/> になる。
        /// </summary>
        public Paragraph AddRubyMarkup(string markup, TextStyle? style = null, TextStyle? rubyStyle = null)
        {
            foreach (var part in RubyMarkup.Parse(markup))
            {
                if (part.Ruby == null) AddText(part.Text, style);
                else if (part.Ruby.Length == 1) AddRuby(part.Text, part.Ruby[0], style, rubyStyle);
                else AddMonoRuby(part.Text, part.Ruby, style, rubyStyle);
            }
            return this;
        }

        /// <summary>ルビの既定の文字スタイルを指定する（<see cref="RubyStyle"/>）。</summary>
        public Paragraph SetRubyStyle(TextStyle style)
        {
            RubyStyle = style;
            return this;
        }

        /// <summary>
        /// この位置に脚注を付ける。本文には上付きの番号が入り、同じ番号と <paramref name="text"/> の脚注が、
        /// この行があるページの本文領域の下端（段組みでは全段をまたぐ下端）に、区切り線の下に置かれる。
        /// 脚注を複数の段落やリストにしたいときは、引数なしの <see cref="AddFootnote()"/> を使う。
        /// 脚注を付けられるのは、セクションの本文、リストの項目、表のセルの段落。ヘッダー/フッターと脚注の本文の中では、レイアウト時に <see cref="InvalidOperationException"/> になる。
        /// </summary>
        /// <param name="text">脚注の文字列。</param>
        /// <param name="style">脚注の文字スタイル（<see cref="Document.FootnoteStyle"/> に重ねる）。</param>
        public Paragraph AddFootnote(string text, TextStyle? style = null)
        {
            AddFootnote().AddParagraph(text, style);
            return this;
        }

        /// <summary>
        /// この位置に脚注を付けて、脚注の本文を返す。返された <see cref="Footnote"/> に段落やリストを追加する。
        /// 参照記号の番号は <see cref="Section.FootnoteNumbering"/> と <see cref="Document.FootnoteNumberFormat"/> に従う。
        /// </summary>
        public Footnote AddFootnote()
        {
            var footnote = new Footnote();
            Inlines.Add(new FootnoteRun(footnote));
            return footnote;
        }

        /// <summary>改行を追加する。</summary>
        public Paragraph AddLineBreak() => AddText("\n");

        /// <summary>
        /// タブ（次のタブ位置まで進む）を追加する。文字列中の \t と同じ。
        /// <paramref name="style"/> の色や文字サイズは、リーダー（導線）の描画にも使われる。
        /// </summary>
        public Paragraph AddTab(TextStyle? style = null) => AddText("\t", style);

        /// <summary>ページ番号を追加する（セクションの開始番号と書式に従う）。</summary>
        public Paragraph AddPageNumber(TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(FieldType.PageNumber, style));
            return this;
        }

        /// <summary>書式を指定してページ番号を追加する（セクションの書式は使わない）。</summary>
        public Paragraph AddPageNumber(PageNumberFormat format, TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(FieldType.PageNumber, style) { Format = format });
            return this;
        }

        /// <summary>総ページ数を追加する。</summary>
        public Paragraph AddPageCount(TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(FieldType.PageCount, style));
            return this;
        }

        /// <summary>フィールドを追加する。</summary>
        public Paragraph AddField(FieldType field, TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(field, style));
            return this;
        }

        /// <summary>書式を指定してフィールドを追加する。</summary>
        public Paragraph AddField(FieldType field, PageNumberFormat format, TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(field, style) { Format = format });
            return this;
        }

        private TextStyle EnsureStyle() => Style ?? (Style = new TextStyle());

        // ---- 書式設定（メソッドチェーン用） ----

        /// <summary>段落のフォントを指定する。</summary>
        public Paragraph SetFont(PdfFont font)
        {
            EnsureStyle().Font = font;
            return this;
        }

        /// <summary>段落の文字サイズ（ポイント）を指定する。</summary>
        public Paragraph SetFontSize(double size)
        {
            EnsureStyle().FontSize = size;
            return this;
        }

        /// <summary>段落を太字にする。</summary>
        public Paragraph SetBold(bool bold = true)
        {
            EnsureStyle().Bold = bold;
            return this;
        }

        /// <summary>段落を斜体にする。</summary>
        public Paragraph SetItalic(bool italic = true)
        {
            EnsureStyle().Italic = italic;
            return this;
        }

        /// <summary>段落に下線を引く。</summary>
        public Paragraph SetUnderline(bool underline = true)
        {
            EnsureStyle().Underline = underline;
            return this;
        }

        /// <summary>段落の文字色を指定する。</summary>
        public Paragraph SetColor(PdfColor color)
        {
            EnsureStyle().Color = color;
            return this;
        }

        /// <summary>段落の水平方向の揃えを指定する。</summary>
        public Paragraph SetAlignment(TextAlignment alignment)
        {
            Format.Alignment = alignment;
            return this;
        }

        /// <summary>行送りを指定する（<paramref name="rule"/> が Multiple なら文字サイズの倍数、それ以外はポイント）。</summary>
        public Paragraph SetLineSpacing(double spacing, LineSpacingRule rule = LineSpacingRule.Multiple)
        {
            Format.LineSpacing = spacing;
            Format.LineSpacingRule = rule;
            return this;
        }

        /// <summary>段落前の間隔（ポイント）を指定する。ページやセルの先頭では無視される。</summary>
        public Paragraph SetSpaceBefore(double points)
        {
            Format.SpaceBefore = points;
            return this;
        }

        /// <summary>段落後の間隔（ポイント）を指定する。</summary>
        public Paragraph SetSpaceAfter(double points)
        {
            Format.SpaceAfter = points;
            return this;
        }

        /// <summary>インデント（ポイント）を指定する。<paramref name="firstLine"/> は 1 行目だけに加える字下げ（負の値ならぶら下げ）。</summary>
        public Paragraph SetIndent(double left, double firstLine = 0, double right = 0)
        {
            Format.LeftIndent = left;
            Format.FirstLineIndent = firstLine;
            Format.RightIndent = right;
            return this;
        }

        /// <summary>タブ位置を追加する（<see cref="ParagraphFormat.AddTabStop"/> と同じ）。</summary>
        /// <param name="position">段落の本文領域の左端からの位置（ポイント）。</param>
        /// <param name="alignment">タブ位置での文字の揃え。</param>
        /// <param name="leader">タブで空いた部分を埋めるリーダー。</param>
        public Paragraph AddTabStop(double position, TabAlignment alignment = TabAlignment.Left, TabLeader leader = TabLeader.None)
        {
            Format.AddTabStop(position, alignment, leader);
            return this;
        }

        /// <summary>既定のタブ間隔（ポイント）を指定する。</summary>
        public Paragraph SetDefaultTabStop(double points)
        {
            Format.DefaultTabStop = points;
            return this;
        }

        /// <summary>次のブロックと同じページに置く（見出しなど）。</summary>
        public Paragraph SetKeepWithNext(bool keep = true)
        {
            Format.KeepWithNext = keep;
            return this;
        }

        /// <summary>段落をページで分割しない。</summary>
        public Paragraph SetKeepTogether(bool keep = true)
        {
            Format.KeepTogether = keep;
            return this;
        }

        /// <summary>段落の前で改ページする。</summary>
        public Paragraph SetPageBreakBefore(bool value = true)
        {
            Format.PageBreakBefore = value;
            return this;
        }

        /// <summary>段落の背景色と、内側の余白を指定する。</summary>
        public Paragraph SetBackground(PdfColor color, Thickness? padding = null)
        {
            Format.Background = color;
            if (padding != null) Format.Padding = padding;
            return this;
        }

        /// <summary>段落の枠線と、内側の余白を指定する。</summary>
        public Paragraph SetBorder(PdfPen pen, Thickness? padding = null)
        {
            Format.Border = pen;
            if (padding != null) Format.Padding = padding;
            return this;
        }

        internal override LayoutNode CreateNode(StyleScope scope) => new ParagraphNode(this, scope);
    }

    /// <summary>画像ブロック。</summary>
    public sealed class ImageBlock : BoxBlock
    {
        /// <summary>画像を指定して作る。</summary>
        public ImageBlock(PdfImage image)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
        }

        /// <summary>描画する画像。</summary>
        public PdfImage Image { get; set; }

        /// <summary>幅（ポイント）。null なら高さから縦横比で決める（両方 null なら自然なサイズ）。</summary>
        public double? Width { get; set; }

        /// <summary>高さ（ポイント）。</summary>
        public double? Height { get; set; }

        /// <summary>水平位置。</summary>
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>はみ出すとき、利用できる領域に収まるよう縮小するか（既定 true）。</summary>
        public bool ScaleToFit { get; set; } = true;

        internal override LayoutNode CreateNode(StyleScope scope) => new ImageNode(this);
    }

    /// <summary>図（ベクター描画領域）。</summary>
    public sealed class DrawingBlock : BoxBlock
    {
        /// <summary>高さ（ポイント）と描画処理を指定して作る。</summary>
        public DrawingBlock(double height, Action<PdfGraphics, PdfRect> draw)
        {
            Height = height;
            Draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        /// <summary>幅（ポイント）。null なら利用できる幅いっぱい。</summary>
        public double? Width { get; set; }

        /// <summary>高さ（ポイント）。</summary>
        public double Height { get; set; }

        /// <summary>水平位置。</summary>
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>描画処理。左上が (0, 0) になるよう座標が移動された状態で呼ばれる。</summary>
        public Action<PdfGraphics, PdfRect> Draw { get; set; }

        /// <summary>描画を領域内に制限するか。</summary>
        public bool Clip { get; set; }

        internal override LayoutNode CreateNode(StyleScope scope) => new DrawingNode(this);
    }

    /// <summary>改ページ。</summary>
    public sealed class PageBreak : Block
    {
        internal override LayoutNode CreateNode(StyleScope scope) => new PageBreakNode();
    }

    /// <summary>段区切り。段組みのセクションでは次の段へ、そうでなければ次のページへ進む。</summary>
    public sealed class ColumnBreak : Block
    {
        internal override LayoutNode CreateNode(StyleScope scope) => new PageBreakNode(column: true);
    }

    /// <summary>縦方向の空白。</summary>
    public sealed class Spacer : Block
    {
        /// <summary>高さ（ポイント）を指定して作る。</summary>
        public Spacer(double height)
        {
            Height = height;
        }

        /// <summary>空白の高さ（ポイント）。</summary>
        public double Height { get; set; }

        internal override LayoutNode CreateNode(StyleScope scope) => new SpacerNode(Height);
    }

    /// <summary>水平線。</summary>
    public sealed class HorizontalLine : BoxBlock
    {
        /// <summary>水平線を作る（前後の間隔は 3 ポイント）。</summary>
        public HorizontalLine()
        {
            SpaceBefore = 3;
            SpaceAfter = 3;
        }

        /// <summary>線のスタイル（既定は黒の 0.5 ポイント）。</summary>
        public PdfPen Pen { get; set; } = new PdfPen(PdfColor.Black, 0.5);

        internal override LayoutNode CreateNode(StyleScope scope) => new DrawingNode(new DrawingBlock(Pen.Width, (g, r) => g.DrawLine(Pen, 0, r.Height / 2, r.Width, r.Height / 2))
        {
            SpaceBefore = SpaceBefore,
            SpaceAfter = SpaceAfter,
            KeepWithNext = KeepWithNext,
            PageBreakBefore = PageBreakBefore,
        });
    }
}
