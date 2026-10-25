using System.Linq;
using System.Text;
using System.Windows.Controls;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Controls
{
    /// <summary>上付き・下付き・ルビの記法（<see cref="TextMarkup"/>）を、入力欄の文字に付けたり外したりする。</summary>
    public static class MarkupEditing
    {
        /// <summary>
        /// 入力欄で選んでいる文字を open と close で囲む。すでに囲まれていれば外す。
        /// 何も選んでいなければ、空の記法を入れて、その中にカーソルを置く。
        /// </summary>
        public static void Toggle(TextBox box, string open, string close)
        {
            string text = box.Text;
            int start = box.SelectionStart, length = box.SelectionLength;
            // 選んだ範囲のすぐ外に記法があれば、それを外す
            if (start >= open.Length && string.CompareOrdinal(text, start - open.Length, open, 0, open.Length) == 0
                && string.CompareOrdinal(text, start + length, close, 0, close.Length) == 0)
            {
                box.Text = text.Substring(0, start - open.Length) + text.Substring(start, length) + text.Substring(start + length + close.Length);
                box.Select(start - open.Length, length);
                box.Focus();
                return;
            }
            string selected = box.SelectedText;
            string replaced = ToggleLines(selected, open, close);
            box.SelectedText = replaced;
            if (length == 0) box.Select(start + open.Length, 0);
            else if (replaced.StartsWith(open) && replaced.EndsWith(close) && !replaced.Contains('\n')) box.Select(start + open.Length, replaced.Length - open.Length - close.Length);
            else box.Select(start, replaced.Length);
            box.Focus();
        }

        /// <summary>
        /// 文字列の各行を open と close で囲む（すべての行が囲まれていれば外す）。行頭のタブ（箇条書きの入れ子）は囲まない。
        /// 記法は行をまたげない（箇条書きでは 1 行が 1 項目）ので、行ごとに囲む。
        /// </summary>
        public static string ToggleLines(string text, string open, string close)
        {
            if (text.Length == 0) return open + close;
            var lines = text.Split('\n').Select(Split).ToList();
            bool wrapped = lines.Where(l => l.Body.Length > 0).All(l => l.Body.Length >= open.Length + close.Length && l.Body.StartsWith(open) && l.Body.EndsWith(close));
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                var (tabs, body, cr) = lines[i];
                if (i > 0) sb.Append('\n');
                sb.Append(tabs);
                if (body.Length == 0) sb.Append(body);
                else if (wrapped) sb.Append(body, open.Length, body.Length - open.Length - close.Length);
                else sb.Append(open).Append(body).Append(close);
                sb.Append(cr);
            }
            return sb.ToString();
        }

        /// <summary>行を、行頭のタブ・本文・行末の CR に分ける。</summary>
        private static (string Tabs, string Body, string Cr) Split(string line)
        {
            int tabs = 0;
            while (tabs < line.Length && line[tabs] == '\t') tabs++;
            int end = line.EndsWith("\r") ? line.Length - 1 : line.Length;
            return (line.Substring(0, tabs), line.Substring(tabs, end - tabs), line.Substring(end));
        }

        /// <summary>
        /// 選んでいる文字を ｜親文字《》 にして、ルビを入力する位置（《 と 》 の間）にカーソルを置く。
        /// 何も選んでいなければ ｜《》 を入れて、親文字を入力する位置（｜ の後ろ）にカーソルを置く。
        /// </summary>
        public static void AddRuby(TextBox box)
        {
            int start = box.SelectionStart;
            string baseText = box.SelectedText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            box.SelectedText = "｜" + baseText + "《》";
            box.Select(baseText.Length == 0 ? start + 1 : start + baseText.Length + 2, 0);
            box.Focus();
        }
    }
}
