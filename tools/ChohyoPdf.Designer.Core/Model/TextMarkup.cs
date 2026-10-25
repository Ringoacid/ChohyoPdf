using System;
using System.Collections.Generic;
using System.Text;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>文字列の一部の書式。</summary>
    public enum MarkupKind
    {
        /// <summary>普通の文字。</summary>
        Text,

        /// <summary>上付き（&lt;sup&gt;…&lt;/sup&gt;）。</summary>
        Superscript,

        /// <summary>下付き（&lt;sub&gt;…&lt;/sub&gt;）。</summary>
        Subscript,

        /// <summary>ルビ付き（｜親文字《ルビ》）。</summary>
        Ruby,
    }

    /// <summary>書式をそろえた文字列の一部。<see cref="Text"/> と <see cref="Ruby"/> は差し込み欄 {名前} を含んでよい。</summary>
    public sealed class MarkupRun
    {
        public MarkupRun(MarkupKind kind, string text, string ruby = "")
        {
            Kind = kind;
            Text = text;
            Ruby = ruby;
        }

        public MarkupKind Kind { get; }

        /// <summary>文字列（ルビ付きなら親文字）。</summary>
        public string Text { get; }

        /// <summary>ルビ（<see cref="MarkupKind.Ruby"/> のときだけ）。</summary>
        public string Ruby { get; }
    }

    /// <summary>
    /// テキストの記法。文字の一部だけを上付き・下付きにしたり、ルビを付けたりする。
    /// <list type="bullet">
    /// <item>&lt;sup&gt;2&lt;/sup&gt; で上付き、&lt;sub&gt;2&lt;/sub&gt; で下付き（大文字・小文字は区別しない。入れ子にはできない）。</item>
    /// <item>｜東京《とうきょう》 でルビ（｜ は全角か半角の縦線。｜ のない「東京《とうきょう》」はルビにしない）。</item>
    /// </list>
    /// 閉じていない記法は、そのままの文字として扱う（以前のデザインの文字の見た目を変えないため、記法の形がそろったときだけ書式にする）。
    /// 差し込み欄 {名前} は、記法を読んだあとで値に置き換えるので、差し込んだ値の中の記法は書式にならない。
    /// </summary>
    public static class TextMarkup
    {
        public const string SuperscriptOpen = "<sup>";
        public const string SuperscriptClose = "</sup>";
        public const string SubscriptOpen = "<sub>";
        public const string SubscriptClose = "</sub>";

        /// <summary>書式を指定した部分があるか（記法がなければ、これまでどおり 1 つの書式で描く）。</summary>
        public static bool HasMarkup(string text)
        {
            if (text.IndexOf('<') < 0 && text.IndexOf('《') < 0) return false;
            foreach (var run in Parse(text))
            {
                if (run.Kind != MarkupKind.Text) return true;
            }
            return false;
        }

        /// <summary>文字列を、書式ごとの部分に分ける。改行やタブは普通の文字として残す。</summary>
        public static List<MarkupRun> Parse(string text)
        {
            var runs = new List<MarkupRun>();
            var plain = new StringBuilder();
            void Flush()
            {
                if (plain.Length == 0) return;
                runs.Add(new MarkupRun(MarkupKind.Text, plain.ToString()));
                plain.Clear();
            }

            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '<' && TryTag(text, i, SuperscriptOpen, SuperscriptClose, out string inner, out int next)
                    || ch == '<' && TryTag(text, i, SubscriptOpen, SubscriptClose, out inner, out next))
                {
                    Flush();
                    var kind = string.Compare(text, i, SuperscriptOpen, 0, SuperscriptOpen.Length, StringComparison.OrdinalIgnoreCase) == 0 ? MarkupKind.Superscript : MarkupKind.Subscript;
                    // 中身が空でも記法として扱う（描くときは飛ばす）
                    runs.Add(new MarkupRun(kind, inner));
                    i = next;
                    continue;
                }
                if ((ch == '｜' || ch == '|') && TryRuby(text, i, out string baseText, out string ruby, out next))
                {
                    Flush();
                    runs.Add(new MarkupRun(MarkupKind.Ruby, baseText, ruby));
                    i = next;
                    continue;
                }
                plain.Append(ch);
                i++;
            }
            Flush();
            return runs;
        }

        /// <summary>start から open … close の形なら、その中身と、close の次の位置。</summary>
        private static bool TryTag(string text, int start, string open, string close, out string inner, out int next)
        {
            inner = "";
            next = start;
            if (string.Compare(text, start, open, 0, open.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            int from = start + open.Length;
            int end = text.IndexOf(close, from, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return false;
            inner = text.Substring(from, end - from);
            next = end + close.Length;
            return true;
        }

        private static readonly char[] LineBreakOrTab = { '\r', '\n', '\t' };

        /// <summary>差し込んだ値をルビの親文字やルビにするときに、改行とタブを空白にする（ルビには改行もタブも含められない）。</summary>
        public static string RubySafe(string text) => text.IndexOfAny(LineBreakOrTab) < 0 ? text : text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

        /// <summary>start（｜ の位置）から ｜親文字《ルビ》 の形なら、親文字・ルビと、》 の次の位置。親文字は改行・タブを含まない 1 文字以上。</summary>
        private static bool TryRuby(string text, int start, out string baseText, out string ruby, out int next)
        {
            baseText = ruby = "";
            next = start;
            int open = text.IndexOf('《', start + 1);
            if (open <= start + 1) return false;
            int close = text.IndexOf('》', open + 1);
            if (close < 0) return false;
            baseText = text.Substring(start + 1, open - start - 1);
            ruby = text.Substring(open + 1, close - open - 1);
            if (baseText.IndexOfAny(LineBreakOrTab) >= 0 || ruby.IndexOfAny(LineBreakOrTab) >= 0 || baseText.IndexOf('《') >= 0) return false;
            next = close + 1;
            return true;
        }

        /// <summary>
        /// 箇条書きの項目に分ける。1 行が 1 項目で、行頭のタブの数が入れ子の深さ（0 から）。タブは項目の文字に含めない。
        /// </summary>
        public static List<(int Level, string Text)> SplitItems(string text)
        {
            var items = new List<(int, string)>();
            foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                int level = 0;
                while (level < raw.Length && raw[level] == '\t') level++;
                items.Add((level, raw.Substring(level)));
            }
            return items;
        }

        /// <summary>記法を取り除いた文字列（ルビは親文字だけ残す）。</summary>
        public static string ToPlainText(string text)
        {
            var sb = new StringBuilder();
            foreach (var run in Parse(text)) sb.Append(run.Text);
            return sb.ToString();
        }
    }
}
