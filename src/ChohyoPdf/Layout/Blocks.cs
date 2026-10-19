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

        /// <summary>縦方向の空白を追加する。</summary>
        public void AddSpacer(double height) => Blocks.Add(new Spacer(height));

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
        public TextRun(string text, TextStyle? style = null)
        {
            Text = text ?? "";
            Style = style;
        }

        public string Text { get; set; }

        /// <summary>リンク先 URL。</summary>
        public string? Link { get; set; }
    }

    /// <summary>フィールドの種類。</summary>
    public enum FieldType
    {
        /// <summary>通しページ番号。</summary>
        PageNumber,

        /// <summary>総ページ数。</summary>
        PageCount,

        /// <summary>セクション内のページ番号。</summary>
        SectionPageNumber,

        /// <summary>セクションのページ数。</summary>
        SectionPageCount,
    }

    /// <summary>ページ番号などのフィールド。</summary>
    public sealed class FieldRun : Inline
    {
        public FieldRun(FieldType field, TextStyle? style = null)
        {
            Field = field;
            Style = style;
        }

        public FieldType Field { get; set; }
    }

    /// <summary>段落。文字列は自動で折り返され（禁則処理あり）、必要ならページをまたいで分割される。</summary>
    public sealed class Paragraph : Block
    {
        public Paragraph()
        {
        }

        public Paragraph(string text, TextStyle? style = null)
        {
            AddText(text);
            Style = style;
        }

        /// <summary>段落内の要素。</summary>
        public List<Inline> Inlines { get; } = new List<Inline>();

        /// <summary>段落全体の文字スタイル。</summary>
        public TextStyle? Style { get; set; }

        /// <summary>段落の書式。</summary>
        public ParagraphFormat Format { get; set; } = new ParagraphFormat();

        /// <summary><see cref="Document.Styles"/> に登録した名前付きスタイル。</summary>
        public string? StyleName { get; set; }

        /// <summary>テキストを追加する。改行文字（\n）で改行できる。</summary>
        public Paragraph AddText(string text, TextStyle? style = null)
        {
            Inlines.Add(new TextRun(text, style));
            return this;
        }

        /// <summary>太字のテキストを追加する。</summary>
        public Paragraph AddBold(string text) => AddText(text, new TextStyle { Bold = true });

        /// <summary>リンク付きのテキストを追加する。</summary>
        public Paragraph AddLink(string text, string uri, TextStyle? style = null)
        {
            Inlines.Add(new TextRun(text, style ?? new TextStyle { Color = PdfColor.FromRgb(0, 0, 238), Underline = true }) { Link = uri });
            return this;
        }

        /// <summary>改行を追加する。</summary>
        public Paragraph AddLineBreak() => AddText("\n");

        /// <summary>通しページ番号を追加する。</summary>
        public Paragraph AddPageNumber(TextStyle? style = null)
        {
            Inlines.Add(new FieldRun(FieldType.PageNumber, style));
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

        private TextStyle EnsureStyle() => Style ?? (Style = new TextStyle());

        // ---- 書式設定（メソッドチェーン用） ----

        public Paragraph SetFont(PdfFont font)
        {
            EnsureStyle().Font = font;
            return this;
        }

        public Paragraph SetFontSize(double size)
        {
            EnsureStyle().FontSize = size;
            return this;
        }

        public Paragraph SetBold(bool bold = true)
        {
            EnsureStyle().Bold = bold;
            return this;
        }

        public Paragraph SetItalic(bool italic = true)
        {
            EnsureStyle().Italic = italic;
            return this;
        }

        public Paragraph SetUnderline(bool underline = true)
        {
            EnsureStyle().Underline = underline;
            return this;
        }

        public Paragraph SetColor(PdfColor color)
        {
            EnsureStyle().Color = color;
            return this;
        }

        public Paragraph SetAlignment(TextAlignment alignment)
        {
            Format.Alignment = alignment;
            return this;
        }

        public Paragraph SetLineSpacing(double spacing, LineSpacingRule rule = LineSpacingRule.Multiple)
        {
            Format.LineSpacing = spacing;
            Format.LineSpacingRule = rule;
            return this;
        }

        public Paragraph SetSpaceBefore(double points)
        {
            Format.SpaceBefore = points;
            return this;
        }

        public Paragraph SetSpaceAfter(double points)
        {
            Format.SpaceAfter = points;
            return this;
        }

        public Paragraph SetIndent(double left, double firstLine = 0, double right = 0)
        {
            Format.LeftIndent = left;
            Format.FirstLineIndent = firstLine;
            Format.RightIndent = right;
            return this;
        }

        public Paragraph SetKeepWithNext(bool keep = true)
        {
            Format.KeepWithNext = keep;
            return this;
        }

        public Paragraph SetKeepTogether(bool keep = true)
        {
            Format.KeepTogether = keep;
            return this;
        }

        public Paragraph SetPageBreakBefore(bool value = true)
        {
            Format.PageBreakBefore = value;
            return this;
        }

        public Paragraph SetBackground(PdfColor color, Thickness? padding = null)
        {
            Format.Background = color;
            if (padding != null) Format.Padding = padding;
            return this;
        }

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
        public ImageBlock(PdfImage image)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
        }

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

    /// <summary>縦方向の空白。</summary>
    public sealed class Spacer : Block
    {
        public Spacer(double height)
        {
            Height = height;
        }

        public double Height { get; set; }

        internal override LayoutNode CreateNode(StyleScope scope) => new SpacerNode(Height);
    }

    /// <summary>水平線。</summary>
    public sealed class HorizontalLine : BoxBlock
    {
        public HorizontalLine()
        {
            SpaceBefore = 3;
            SpaceAfter = 3;
        }

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
