using System.Collections.Generic;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.ViewModels
{
    /// <summary>コンボボックスの選択肢（値と表示名）。</summary>
    public sealed class Choice
    {
        public Choice(object? value, string label)
        {
            Value = value;
            Label = label;
        }

        public object? Value { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }

    /// <summary>組み込みの差し込み欄の説明（書き方・表示例・内容）。</summary>
    public sealed class BuiltInExample
    {
        public BuiltInExample(string text, string example, string description)
        {
            Text = text;
            Example = example;
            Description = description;
        }

        public string Text { get; }
        public string Example { get; }
        public string Description { get; }
    }

    public static class Choices
    {
        public static IReadOnlyList<Choice> Alignments { get; } = new[]
        {
            new Choice(TextAlignment.Left, "左揃え"),
            new Choice(TextAlignment.Center, "中央揃え"),
            new Choice(TextAlignment.Right, "右揃え"),
            new Choice(TextAlignment.Justify, "両端揃え"),
            new Choice(TextAlignment.Distribute, "均等割り付け"),
        };

        public static IReadOnlyList<Choice> VerticalAlignments { get; } = new[]
        {
            new Choice(VerticalAlignment.Top, "上"),
            new Choice(VerticalAlignment.Middle, "中央"),
            new Choice(VerticalAlignment.Bottom, "下"),
        };

        public static IReadOnlyList<Choice> DashStyles { get; } = new[]
        {
            new Choice(DashStyle.Solid, "実線"),
            new Choice(DashStyle.Dash, "破線"),
            new Choice(DashStyle.Dot, "点線"),
        };

        public static IReadOnlyList<Choice> Visibilities { get; } = new[]
        {
            new Choice(PageVisibility.AllPages, "増えたすべてのページに描く"),
            new Choice(PageVisibility.FirstPage, "最初のページだけに描く"),
            new Choice(PageVisibility.LastPage, "最後のページだけに描く"),
            new Choice(PageVisibility.ExceptFirstPage, "最初のページ以外に描く"),
        };

        /// <summary>全ページ共通の部品を描くページ（文書全体での位置）。</summary>
        public static IReadOnlyList<Choice> CommonVisibilities { get; } = new[]
        {
            new Choice(PageVisibility.AllPages, "すべてのページに描く"),
            new Choice(PageVisibility.FirstPage, "最初のページだけに描く"),
            new Choice(PageVisibility.LastPage, "最後のページだけに描く"),
            new Choice(PageVisibility.ExceptFirstPage, "最初のページ以外に描く（表紙を除く）"),
        };

        public static IReadOnlyList<Choice> ImageFits { get; } = new[]
        {
            new Choice(ImageFit.Uniform, "縦横比を保つ"),
            new Choice(ImageFit.Stretch, "枠に合わせて伸縮"),
        };

        public static IReadOnlyList<Choice> ListStyles { get; } = new[]
        {
            new Choice(TextListStyle.None, "なし"),
            new Choice(TextListStyle.Bullet, "箇条書き（・）"),
            new Choice(TextListStyle.Numbered, "番号付き（1.）"),
        };

        /// <summary>箇条書きの記号（空文字列は既定の「・」。TextElement.Bullet では null）。</summary>
        public static IReadOnlyList<Choice> Bullets { get; } = new[]
        {
            new Choice("", "・（既定）"),
            new Choice("●", "●"),
            new Choice("○", "○"),
            new Choice("■", "■"),
            new Choice("□", "□"),
            new Choice("◆", "◆"),
            new Choice("※", "※"),
            new Choice("－", "－"),
            new Choice("✓", "✓"),
        };

        public static IReadOnlyList<Choice> NumberStyles { get; } = new[]
        {
            new Choice(ListNumberStyle.Default, "1.（入れ子は (1) → ①）"),
            new Choice(ListNumberStyle.ArabicParenthesis, "1)"),
            new Choice(ListNumberStyle.Parenthesized, "(1)"),
            new Choice(ListNumberStyle.Circled, "①"),
            new Choice(ListNumberStyle.Kanji, "一、"),
            new Choice(ListNumberStyle.KanjiParenthesized, "（一）"),
            new Choice(ListNumberStyle.LowerLetter, "a."),
            new Choice(ListNumberStyle.UpperLetter, "A."),
            new Choice(ListNumberStyle.LowerRoman, "i."),
            new Choice(ListNumberStyle.UpperRoman, "I."),
        };

        public static IReadOnlyList<Choice> TabAlignments { get; } = new[]
        {
            new Choice(TabAlignment.Left, "左揃え"),
            new Choice(TabAlignment.Center, "中央揃え"),
            new Choice(TabAlignment.Right, "右揃え"),
            new Choice(TabAlignment.Decimal, "小数点揃え"),
        };

        public static IReadOnlyList<Choice> TabLeaders { get; } = new[]
        {
            new Choice(TabLeader.None, "なし"),
            new Choice(TabLeader.Dots, "点線（……）"),
            new Choice(TabLeader.Dashes, "破線（- - -）"),
            new Choice(TabLeader.Line, "実線（＿＿）"),
        };

        public static IReadOnlyList<Choice> FieldKinds { get; } = new[]
        {
            new Choice(FieldKind.Text, "文字列"),
            new Choice(FieldKind.Number, "数値"),
            new Choice(FieldKind.Date, "日付"),
        };

        public static IReadOnlyList<string> PaperNames => PaperSizes.Names;

        public static IReadOnlyList<double> GridSizes { get; } = new[] { 0.5, 1.0, 2.0, 2.5, 5.0, 10.0 };
    }
}
