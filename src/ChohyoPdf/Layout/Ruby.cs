using System;
using System.Collections.Generic;
using System.Text;

namespace ChohyoPdf
{
    /// <summary>
    /// ルビ（振り仮名）付きの文字列。親文字の上に、ルビを中央そろえで小さく描く（グループルビ）。
    /// 親文字とルビの組は、途中で改行しない。
    /// </summary>
    public sealed class RubyRun : Inline
    {
        /// <param name="baseText">親文字（空にできない。改行・タブを含められない）。</param>
        /// <param name="rubyText">ルビ（改行・タブを含められない）。null や空なら、ルビのない普通の文字列として扱う。</param>
        /// <param name="style">親文字の文字スタイル（<see cref="Inline.Style"/>）。</param>
        /// <param name="rubyStyle">ルビの文字スタイル（<see cref="RubyStyle"/>）。</param>
        public RubyRun(string baseText, string rubyText, TextStyle? style = null, TextStyle? rubyStyle = null)
        {
            if (string.IsNullOrEmpty(baseText)) throw new ArgumentException("親文字を指定してください。", nameof(baseText));
            if (HasLineBreakOrTab(baseText)) throw new ArgumentException("親文字に改行やタブは含められません。", nameof(baseText));
            rubyText = rubyText ?? "";
            if (HasLineBreakOrTab(rubyText)) throw new ArgumentException("ルビに改行やタブは含められません。", nameof(rubyText));
            BaseText = baseText;
            RubyText = rubyText;
            Style = style;
            RubyStyle = rubyStyle;
        }

        /// <summary>親文字。</summary>
        public string BaseText { get; }

        /// <summary>ルビ。</summary>
        public string RubyText { get; }

        /// <summary>
        /// ルビの文字スタイル。親文字のスタイルを引き継ぎ、文字サイズは親文字の半分になる。
        /// 下線・取り消し線・マーカー・上付き下付き・字間は引き継がない（ここで指定した場合も、ルビには下線などを描かない）。
        /// 段落全体の既定は <see cref="Paragraph.RubyStyle"/>。この指定のほうが優先される。
        /// </summary>
        public TextStyle? RubyStyle { get; set; }

        private static bool HasLineBreakOrTab(string text)
        {
            foreach (char c in text)
            {
                if (c == (char)0x0A || c == (char)0x0D || c == (char)0x09 || c == (char)0x2028 || c == (char)0x2029) return true;
            }
            return false;
        }
    }
}

namespace ChohyoPdf.Layout
{
    /// <summary>ルビ記法（<c>｜親文字《ルビ》</c>）の解析。</summary>
    internal static class RubyMarkup
    {
        /// <summary>記法の 1 つの要素。<see cref="Ruby"/> が null なら普通の文字列。</summary>
        public sealed class Part
        {
            public Part(string text, string[]? ruby)
            {
                Text = text;
                Ruby = ruby;
            }

            /// <summary>文字列（ルビがあれば親文字）。</summary>
            public string Text { get; }

            /// <summary>ルビ。要素が 1 つならグループルビ、親文字の文字数と同じ数ならモノルビ。</summary>
            public string[]? Ruby { get; }
        }

        private const char FullWidthBar = '｜';
        private const char Open = '《';
        private const char Close = '》';

        public static List<Part> Parse(string markup)
        {
            if (markup == null) throw new ArgumentNullException(nameof(markup));
            var parts = new List<Part>();
            var plain = new StringBuilder();
            int i = 0;
            while (i < markup.Length)
            {
                char c = markup[i];
                if (c == FullWidthBar || c == '|')
                {
                    int open = markup.IndexOf(Open, i + 1);
                    if (open < 0) throw Error(i, "「" + c + "」の後ろに「《」がありません。");
                    string baseText = markup.Substring(i + 1, open - i - 1);
                    if (baseText.Length == 0) throw Error(i, "「" + c + "」と「《」の間に親文字がありません。");
                    i = ReadRuby(markup, open, plain, baseText, parts);
                }
                else if (c == Open)
                {
                    string baseText = TakeTrailingIdeographs(plain);
                    if (baseText.Length == 0) throw Error(i, "「《」の前に親文字（漢字）がありません。親文字が漢字でないときは「｜」で始まりを示してください。");
                    i = ReadRuby(markup, i, plain, baseText, parts);
                }
                else if (c == Close)
                {
                    throw Error(i, "「》」に対応する「《」がありません。");
                }
                else
                {
                    plain.Append(c);
                    i++;
                }
            }
            Flush(plain, parts);
            return parts;
        }

        /// <summary><paramref name="open"/> の位置の「《」から「》」までを読み、要素を追加して「》」の次の位置を返す。</summary>
        private static int ReadRuby(string markup, int open, StringBuilder plain, string baseText, List<Part> parts)
        {
            int close = markup.IndexOf(Close, open + 1);
            if (close < 0) throw Error(open, "「《」に対応する「》」がありません。");
            string rubyText = markup.Substring(open + 1, close - open - 1);
            Flush(plain, parts);
            if (rubyText.Length == 0)
            {
                parts.Add(new Part(baseText, null));
                return close + 1;
            }
            var rubies = rubyText.Split('|', FullWidthBar);
            if (rubies.Length > 1)
            {
                int count = SplitElements(baseText).Count;
                if (rubies.Length != count)
                    throw Error(open, "モノルビの区切り（「|」）の数が合いません。親文字「" + baseText + "」は " + count + " 文字ですが、ルビは " + rubies.Length + " 個あります。");
            }
            parts.Add(new Part(baseText, rubies));
            return close + 1;
        }

        private static void Flush(StringBuilder plain, List<Part> parts)
        {
            if (plain.Length == 0) return;
            parts.Add(new Part(plain.ToString(), null));
            plain.Length = 0;
        }

        private static ArgumentException Error(int index, string message)
            => new ArgumentException("ルビ記法の " + (index + 1) + " 文字目: " + message, "markup");

        /// <summary>文字列を 1 文字ずつに分ける（サロゲートペアは 1 文字）。</summary>
        public static List<string> SplitElements(string text)
        {
            var list = new List<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    list.Add(text.Substring(i, 2));
                    i++;
                }
                else
                {
                    list.Add(text[i].ToString());
                }
            }
            return list;
        }

        /// <summary>バッファの末尾に続く漢字（々・〆・〇を含む）を取り出す。</summary>
        private static string TakeTrailingIdeographs(StringBuilder sb)
        {
            int start = sb.Length;
            while (start > 0)
            {
                int cp = sb[start - 1];
                int width = 1;
                if (char.IsLowSurrogate(sb[start - 1]) && start >= 2 && char.IsHighSurrogate(sb[start - 2]))
                {
                    cp = char.ConvertToUtf32(sb[start - 2], sb[start - 1]);
                    width = 2;
                }
                if (!IsIdeograph(cp)) break;
                start -= width;
            }
            string result = sb.ToString(start, sb.Length - start);
            sb.Length = start;
            return result;
        }

        private static bool IsIdeograph(int cp)
            => (cp >= 0x4E00 && cp <= 0x9FFF) || (cp >= 0x3400 && cp <= 0x4DBF) || (cp >= 0xF900 && cp <= 0xFAFF)
            || (cp >= 0x20000 && cp <= 0x3FFFF) || cp == 0x3005 || cp == 0x3006 || cp == 0x3007;
    }
}
