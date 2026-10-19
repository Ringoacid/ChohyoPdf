using System;
using System.Collections.Generic;

namespace ChohyoPdf
{
    /// <summary>
    /// 文字のスタイル。未設定（null）のプロパティは親（文書の既定値、表、セル、段落など）から引き継ぐ。
    /// </summary>
    public sealed class TextStyle
    {
        public TextStyle()
        {
        }

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
        /// 代替フォント。<see cref="Font"/> にグリフがない文字は、ここに並べた順にグリフを持つフォントで描画する。
        /// </summary>
        public IList<PdfFont>? FallbackFonts { get; set; }

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
        public PdfFont[] Fallbacks = Array.Empty<PdfFont>();
        public string? Link;

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
                Fallbacks = fallbacks.ToArray(),
            };
        }

        public ResolvedStyle WithLink(string? link)
        {
            var copy = (ResolvedStyle)MemberwiseClone();
            copy.Link = link;
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
