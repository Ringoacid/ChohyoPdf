namespace ChohyoPdf
{
    /// <summary>
    /// 段落の書式。未設定（null）のプロパティは親（文書の既定値、名前付きスタイル、セルなど）から引き継ぐ。
    /// </summary>
    public sealed class ParagraphFormat
    {
        /// <summary>水平方向の揃え。</summary>
        public TextAlignment? Alignment { get; set; }

        /// <summary>行送りの指定方法。</summary>
        public LineSpacingRule? LineSpacingRule { get; set; }

        /// <summary>行送り（Multiple なら文字サイズの倍数、それ以外はポイント）。</summary>
        public double? LineSpacing { get; set; }

        /// <summary>1 行目の字下げ（ポイント）。負の値ならぶら下げインデント。</summary>
        public double? FirstLineIndent { get; set; }

        /// <summary>左インデント（ポイント）。</summary>
        public double? LeftIndent { get; set; }

        /// <summary>右インデント（ポイント）。</summary>
        public double? RightIndent { get; set; }

        /// <summary>段落前の間隔（ポイント）。ページやセルの先頭では無視される。</summary>
        public double? SpaceBefore { get; set; }

        /// <summary>段落後の間隔（ポイント）。</summary>
        public double? SpaceAfter { get; set; }

        /// <summary>段落をページで分割しない。</summary>
        public bool? KeepTogether { get; set; }

        /// <summary>次のブロックと同じページに置く（見出しなど）。</summary>
        public bool? KeepWithNext { get; set; }

        /// <summary>段落の前で改ページする。</summary>
        public bool? PageBreakBefore { get; set; }

        /// <summary>
        /// 改ページ位置の調整（段落の 1 行目だけがページ末尾に残ったり、最終行だけが次ページに送られたりしないようにする）。
        /// </summary>
        public bool? WidowControl { get; set; }

        /// <summary>段落の背景色。</summary>
        public PdfColor? Background { get; set; }

        /// <summary>段落の枠線。</summary>
        public PdfPen? Border { get; set; }

        /// <summary>段落の内側の余白（背景・枠線とテキストの間）。</summary>
        public Thickness? Padding { get; set; }

        public ParagraphFormat Clone() => new ParagraphFormat().Merge(this);

        /// <summary><paramref name="other"/> の設定済みプロパティで上書きした新しい書式を返す。</summary>
        public ParagraphFormat Merge(ParagraphFormat? other)
        {
            var r = new ParagraphFormat
            {
                Alignment = Alignment,
                LineSpacingRule = LineSpacingRule,
                LineSpacing = LineSpacing,
                FirstLineIndent = FirstLineIndent,
                LeftIndent = LeftIndent,
                RightIndent = RightIndent,
                SpaceBefore = SpaceBefore,
                SpaceAfter = SpaceAfter,
                KeepTogether = KeepTogether,
                KeepWithNext = KeepWithNext,
                PageBreakBefore = PageBreakBefore,
                WidowControl = WidowControl,
                Background = Background,
                Border = Border,
                Padding = Padding,
            };
            if (other == null) return r;
            if (other.Alignment.HasValue) r.Alignment = other.Alignment;
            if (other.LineSpacingRule.HasValue) r.LineSpacingRule = other.LineSpacingRule;
            if (other.LineSpacing.HasValue) r.LineSpacing = other.LineSpacing;
            if (other.FirstLineIndent.HasValue) r.FirstLineIndent = other.FirstLineIndent;
            if (other.LeftIndent.HasValue) r.LeftIndent = other.LeftIndent;
            if (other.RightIndent.HasValue) r.RightIndent = other.RightIndent;
            if (other.SpaceBefore.HasValue) r.SpaceBefore = other.SpaceBefore;
            if (other.SpaceAfter.HasValue) r.SpaceAfter = other.SpaceAfter;
            if (other.KeepTogether.HasValue) r.KeepTogether = other.KeepTogether;
            if (other.KeepWithNext.HasValue) r.KeepWithNext = other.KeepWithNext;
            if (other.PageBreakBefore.HasValue) r.PageBreakBefore = other.PageBreakBefore;
            if (other.WidowControl.HasValue) r.WidowControl = other.WidowControl;
            if (other.Background.HasValue) r.Background = other.Background;
            if (other.Border != null) r.Border = other.Border;
            if (other.Padding != null) r.Padding = other.Padding;
            return r;
        }
    }

    /// <summary>名前付き段落スタイル（文字スタイルと段落書式の組）。</summary>
    public sealed class ParagraphStyle
    {
        public ParagraphStyle()
        {
        }

        public ParagraphStyle(TextStyle? text, ParagraphFormat? format)
        {
            Text = text;
            Format = format;
        }

        public TextStyle? Text { get; set; }

        public ParagraphFormat? Format { get; set; }
    }
}
