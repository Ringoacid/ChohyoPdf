using System;
using System.Collections.Generic;

namespace ChohyoPdf
{
    /// <summary>文字のベースラインの位置（上付き・下付き）。</summary>
    public enum BaselineShift
    {
        /// <summary>通常の位置・大きさ。</summary>
        None,

        /// <summary>上付き文字。文字サイズを 0.65 倍にし、ベースラインを文字サイズの 1/3 ほど上げる。</summary>
        Superscript,

        /// <summary>下付き文字。文字サイズを 0.65 倍にし、ベースラインを文字サイズの 1/8 ほど下げる。</summary>
        Subscript,
    }

    /// <summary>
    /// 文字のスタイル。未設定（null）のプロパティは親（文書の既定値、表、セル、段落など）から引き継ぐ。
    /// </summary>
    public sealed class TextStyle
    {
        /// <summary>すべて未設定（親から引き継ぐ）のスタイルを作る。</summary>
        public TextStyle()
        {
        }

        /// <summary>フォントと文字サイズ（ポイント）を指定して作る。</summary>
        public TextStyle(PdfFont font, double fontSize)
        {
            Font = font;
            FontSize = fontSize;
        }

        /// <summary>フォント。</summary>
        public PdfFont? Font { get; set; }

        /// <summary>文字サイズ（ポイント）。</summary>
        public double? FontSize { get; set; }

        /// <summary>文字色。</summary>
        public PdfColor? Color { get; set; }

        /// <summary>太字。フォントに <see cref="PdfFont.BoldVariant"/> がなければ疑似太字になる。</summary>
        public bool? Bold { get; set; }

        /// <summary>斜体。フォントに <see cref="PdfFont.ItalicVariant"/> がなければ疑似斜体になる。</summary>
        public bool? Italic { get; set; }

        /// <summary>下線。</summary>
        public bool? Underline { get; set; }

        /// <summary>取り消し線。</summary>
        public bool? Strikeout { get; set; }

        /// <summary>字間（ポイント）。各文字の後ろに加える。</summary>
        public double? CharacterSpacing { get; set; }

        /// <summary>文字の背景色（マーカー）。</summary>
        public PdfColor? BackgroundColor { get; set; }

        /// <summary>
        /// 上付き・下付き。<see cref="BaselineShift.Superscript"/> / <see cref="BaselineShift.Subscript"/> にすると、
        /// 文字を 0.65 倍に縮小してベースラインを上下にずらして描く。行の高さと行の位置は、通常の文字と同じ（<see cref="FontSize"/> の文字）のまま変わらない。
        /// 親が上付きのとき、<see cref="BaselineShift.None"/> を指定すると通常の位置に戻せる。
        /// </summary>
        public BaselineShift? BaselineShift { get; set; }

        /// <summary>
        /// 代替フォント。<see cref="Font"/> にグリフがない文字は、ここに並べた順にグリフを持つフォントで描画する。
        /// </summary>
        public IList<PdfFont>? FallbackFonts { get; set; }

        /// <summary>複製を作る。</summary>
        public TextStyle Clone() => new TextStyle
        {
            Font = Font,
            FontSize = FontSize,
            Color = Color,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            Strikeout = Strikeout,
            CharacterSpacing = CharacterSpacing,
            BackgroundColor = BackgroundColor,
            BaselineShift = BaselineShift,
            FallbackFonts = FallbackFonts == null ? null : new List<PdfFont>(FallbackFonts),
        };

        /// <summary><paramref name="other"/> の設定済みプロパティで上書きした新しいスタイルを返す。</summary>
        public TextStyle Merge(TextStyle? other)
        {
            var result = Clone();
            if (other == null) return result;
            if (other.Font != null) result.Font = other.Font;
            if (other.FontSize.HasValue) result.FontSize = other.FontSize;
            if (other.Color.HasValue) result.Color = other.Color;
            if (other.Bold.HasValue) result.Bold = other.Bold;
            if (other.Italic.HasValue) result.Italic = other.Italic;
            if (other.Underline.HasValue) result.Underline = other.Underline;
            if (other.Strikeout.HasValue) result.Strikeout = other.Strikeout;
            if (other.CharacterSpacing.HasValue) result.CharacterSpacing = other.CharacterSpacing;
            if (other.BackgroundColor.HasValue) result.BackgroundColor = other.BackgroundColor;
            if (other.BaselineShift.HasValue) result.BaselineShift = other.BaselineShift;
            if (other.FallbackFonts != null) result.FallbackFonts = other.FallbackFonts;
            return result;
        }
    }
}

namespace ChohyoPdf.Text
{
    /// <summary>すべての値が確定したスタイル（内部用・不変）。</summary>
    internal sealed class ResolvedStyle
    {
        public PdfFont Font = null!;
        public double Size;
        public PdfColor Color;
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public bool Strikeout;
        public double CharacterSpacing;
        public PdfColor? Background;
        public BaselineShift Shift;
        public PdfFont[] Fallbacks = Array.Empty<PdfFont>();
        public string? Link;

        /// <summary>文書内リンクの飛び先のアンカー名（<see cref="Link"/> が優先）。</summary>
        public string? LinkAnchor;

        /// <summary>アンカーの印（空のテキストにこのスタイルを付けると、その位置にアンカーを置く）。</summary>
        public string? Marker;

        /// <summary>脚注の参照記号の文字に付ける、段落内の脚注の通し番号（1 始まり。0 なら脚注ではない）。参照記号のある行を調べるために使う。</summary>
        public int FootnoteId;

        /// <summary>この文字列に付けるルビの文字列（親文字の組。null ならルビなし）。<see cref="RubyStyle"/> とセットで指定する。</summary>
        public string? RubyText;

        /// <summary>ルビの文字スタイル（<see cref="RubyText"/> があるときだけ使う。ルビ自身は <see cref="RubyText"/> を持たない）。</summary>
        public ResolvedStyle? RubyStyle;

        public static ResolvedStyle From(TextStyle style)
        {
            if (style.Font == null)
                throw new InvalidOperationException("フォントが指定されていません。Document.DefaultTextStyle.Font などでフォントを設定してください。");
            double size = style.FontSize ?? 10.5;
            if (size <= 0) throw new InvalidOperationException("文字サイズは正の値で指定してください。");
            var fallbacks = new List<PdfFont>();
            if (style.FallbackFonts != null)
            {
                foreach (var f in style.FallbackFonts)
                {
                    if (f != null && f != style.Font) fallbacks.Add(f);
                }
            }
            return new ResolvedStyle
            {
                Font = style.Font,
                Size = size,
                Color = style.Color ?? PdfColor.Black,
                Bold = style.Bold ?? false,
                Italic = style.Italic ?? false,
                Underline = style.Underline ?? false,
                Strikeout = style.Strikeout ?? false,
                CharacterSpacing = style.CharacterSpacing ?? 0,
                Background = style.BackgroundColor,
                Shift = style.BaselineShift ?? BaselineShift.None,
                Fallbacks = fallbacks.ToArray(),
            };
        }

        /// <summary>上付き・下付きの文字サイズの倍率。</summary>
        public const double ShiftScale = 0.65;

        /// <summary>実際に描画する文字サイズ（上付き・下付きなら縮小後）。<see cref="Size"/> は行の高さの基準になる指定サイズ。</summary>
        public double DrawSize => Shift == BaselineShift.None ? Size : Size * ShiftScale;

        /// <summary>ベースラインのずれ（下向きが正）。上付きは負、下付きは正。</summary>
        public double BaselineOffset
            => Shift == BaselineShift.Superscript ? -Size * 0.34 : Shift == BaselineShift.Subscript ? Size * 0.12 : 0;

        public ResolvedStyle WithLink(string? link)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.Link = link;
            return copy;
        }

        public ResolvedStyle WithLinkAnchor(string? anchor)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.LinkAnchor = anchor;
            return copy;
        }

        public ResolvedStyle WithMarker(string? marker)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.Marker = marker;
            return copy;
        }

        public ResolvedStyle WithFootnote(int id)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.FootnoteId = id;
            return copy;
        }

        public ResolvedStyle WithRuby(string? rubyText, ResolvedStyle? rubyStyle)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.RubyText = rubyText;
            copy.RubyStyle = rubyStyle;
            return copy;
        }

        public ResolvedStyle WithSize(double size)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.Size = size;
            return copy;
        }
    }
}
