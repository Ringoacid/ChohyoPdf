using System.Collections.Generic;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>箇条書きの項目 1 つの、入れ子の深さと記号。</summary>
    public readonly struct ListMarker
    {
        public ListMarker(int depth, string text)
        {
            Depth = depth;
            Text = text;
        }

        /// <summary>入れ子の深さ（いちばん外側が 0）。</summary>
        public int Depth { get; }

        /// <summary>記号（「・」や「1.」など）。</summary>
        public string Text { get; }
    }

    /// <summary>
    /// 箇条書きのテキストの、項目ごとの記号。デザイン画面で文字を編集している間に、行の左に記号を表示するのに使う。
    /// PDF に描く箇条書き（<see cref="RichText.Build"/> で作る ListBlock）と同じ記号・番号にする。
    /// </summary>
    public static class ListMarkers
    {
        // ChohyoPdf の ListBlock の、深さごとの既定の記号
        private static readonly string[] DefaultBullets = { "・", "○", "■" };

        /// <summary>
        /// テキストの行ごとの記号（1 行が 1 項目。空の行も項目になる）。箇条書きにしていなければ空。
        /// </summary>
        public static List<ListMarker> For(TextElement e) => For(e.Text, e.ListStyle, e.Bullet, e.NumberStyle);

        /// <summary>
        /// テキスト text を、style・bullet・numberStyle（<see cref="TextElement"/> の同じ名前のプロパティ）で箇条書きにしたときの、行ごとの記号。
        /// </summary>
        public static List<ListMarker> For(string text, TextListStyle style, string? bullet, ListNumberStyle numberStyle)
        {
            var result = new List<ListMarker>();
            if (style == TextListStyle.None) return result;
            // counters[d] は、深さ d のいま開いているリストで最後に付けた番号
            var counters = new List<int>();
            foreach (var item in RichText.Items(text))
            {
                // 新しい子リストは 1 から数え直す。浅い項目に戻ったら、その深さのリストの続きの番号にする
                int keep = item.NewSubList ? item.Depth : item.Depth + 1;
                if (counters.Count > keep) counters.RemoveRange(keep, counters.Count - keep);
                while (counters.Count < item.Depth + 1) counters.Add(0);
                counters[item.Depth]++;
                result.Add(new ListMarker(item.Depth, Text(style, bullet, numberStyle, item.Depth, counters[item.Depth])));
            }
            return result;
        }

        /// <summary>
        /// 深さ depth の number 番目の項目の記号。指定した記号・番号の形を使うのはいちばん外側だけで、
        /// 入れ子の項目は深さごとの既定（・ → ○ → ■、1. → (1) → ①）にする。
        /// </summary>
        private static string Text(TextListStyle style, string? bullet, ListNumberStyle numberStyle, int depth, int number)
        {
            if (style == TextListStyle.Bullet)
                return depth == 0 && bullet != null ? bullet : DefaultBullets[depth % DefaultBullets.Length];
            if (depth == 0)
            {
                var (format, prefix, suffix) = RichText.NumberFormat(numberStyle);
                if (format is PageNumberFormat f)
                    return (prefix ?? "") + PageNumberFormatter.Format(number, f) + (suffix ?? (f == PageNumberFormat.CircledNumber ? "" : "."));
            }
            switch (depth % 3)
            {
                case 0: return PageNumberFormatter.Format(number, PageNumberFormat.Arabic) + ".";
                case 1: return "(" + PageNumberFormatter.Format(number, PageNumberFormat.Arabic) + ")";
                default: return PageNumberFormatter.Format(number, PageNumberFormat.CircledNumber);
            }
        }
    }
}
