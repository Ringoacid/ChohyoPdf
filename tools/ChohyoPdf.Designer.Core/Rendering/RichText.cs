using System;
using System.Collections.Generic;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>箇条書きの項目 1 つ。</summary>
    internal sealed class RichItem
    {
        public RichItem(int depth, bool newSubList, List<MarkupRun> runs)
        {
            Depth = depth;
            NewSubList = newSubList;
            Runs = runs;
        }

        /// <summary>入れ子の深さ（いちばん外側が 0）。</summary>
        public int Depth { get; }

        /// <summary>1 つ浅いリストの最後の項目に、子リストを作ってから項目を足す（ListBlock.AddSubList）。</summary>
        public bool NewSubList { get; }

        public List<MarkupRun> Runs { get; }
    }

    /// <summary>
    /// 記法や箇条書きのあるテキストを、段落と箇条書き（ChohyoPdf の Paragraph・ListBlock）にする。
    /// ReportRenderer はここで作ったものを描き、CSharpGenerator は同じ順序で同じものを作るコードを書く（変えるときは両方を合わせること）。
    /// </summary>
    internal static class RichText
    {
        /// <summary>
        /// 箇条書きの項目。1 行が 1 項目で、行頭のタブの数が入れ子の深さ。深さは 1 つ前の項目より 1 だけ深くでき、
        /// それより深いタブは 1 つ深いものとして扱う（最初の項目は深さ 0）。
        /// </summary>
        public static List<RichItem> Items(string text)
        {
            var result = new List<RichItem>();
            int open = 0; // いま開いているリストの数 - 1（項目を足せる最も深い深さ）
            foreach (var (level, line) in TextMarkup.SplitItems(text))
            {
                int depth = Math.Min(level, result.Count == 0 ? 0 : open + 1);
                bool sub = depth > open;
                open = depth;
                result.Add(new RichItem(depth, sub, TextMarkup.Parse(line)));
            }
            return result;
        }

        /// <summary>段落に、記法の部分ごとの文字を足す（中身が空の部分は飛ばす）。expand は差し込み欄を値にする。</summary>
        public static Paragraph AddRuns(Paragraph p, IEnumerable<MarkupRun> runs, Func<string, string> expand)
        {
            foreach (var run in runs)
            {
                if (run.Text.Length == 0) continue;
                switch (run.Kind)
                {
                    case MarkupKind.Superscript: p.AddSuperscript(expand(run.Text)); break;
                    case MarkupKind.Subscript: p.AddSubscript(expand(run.Text)); break;
                    case MarkupKind.Ruby:
                        {
                            // 差し込んだ値が空なら何も足さない（生成コードの AddRubyText と同じ）
                            string baseText = TextMarkup.RubySafe(expand(run.Text));
                            if (baseText.Length > 0) p.AddRuby(baseText, TextMarkup.RubySafe(expand(run.Ruby)));
                            break;
                        }
                    default: p.AddText(expand(run.Text)); break;
                }
            }
            return p;
        }

        /// <summary>テキストの部品の中身（箇条書きなら ListBlock、そうでなければ改行を含む 1 つの Paragraph）。</summary>
        public static Block Build(TextElement e, Func<string, string> expand)
        {
            if (e.ListStyle == TextListStyle.None) return AddRuns(new Paragraph(), TextMarkup.Parse(e.Text), expand);
            var root = NewList(e);
            var stack = new List<ListBlock> { root };
            foreach (var item in Items(e.Text))
            {
                if (item.NewSubList) stack.Add(stack[item.Depth - 1].AddSubList());
                else if (stack.Count > item.Depth + 1) stack.RemoveRange(item.Depth + 1, stack.Count - item.Depth - 1);
                AddRuns(stack[item.Depth].AddItem().AddParagraph(), item.Runs, expand);
            }
            return root;
        }

        /// <summary>いちばん外側のリスト（入れ子のリストは、深さごとの既定の記号・番号にする）。</summary>
        public static ListBlock NewList(TextElement e)
        {
            var list = new ListBlock { Kind = e.ListStyle == TextListStyle.Numbered ? ListKind.Numbered : ListKind.Bullet };
            if (e.ListStyle == TextListStyle.Bullet)
            {
                if (e.Bullet != null) list.Bullet = e.Bullet;
                return list;
            }
            var (format, prefix, suffix) = NumberFormat(e.NumberStyle);
            if (format != null) list.NumberFormat = format;
            if (prefix != null) list.Prefix = prefix;
            if (suffix != null) list.Suffix = suffix;
            return list;
        }

        /// <summary>NewList と同じリストを作る C# の式。</summary>
        public static string NewListCode(TextElement e)
        {
            var init = new List<string> { "Kind = ListKind." + (e.ListStyle == TextListStyle.Numbered ? "Numbered" : "Bullet") };
            if (e.ListStyle == TextListStyle.Bullet)
            {
                if (e.Bullet != null) init.Add("Bullet = " + Code.String(e.Bullet));
            }
            else
            {
                var (format, prefix, suffix) = NumberFormat(e.NumberStyle);
                if (format != null) init.Add("NumberFormat = PageNumberFormat." + format);
                if (prefix != null) init.Add("Prefix = " + Code.String(prefix));
                if (suffix != null) init.Add("Suffix = " + Code.String(suffix));
            }
            return "new ListBlock" + Code.Initializer(init);
        }

        /// <summary>番号の形の、ListBlock の NumberFormat・Prefix・Suffix（null は既定のまま）。</summary>
        internal static (PageNumberFormat? Format, string? Prefix, string? Suffix) NumberFormat(ListNumberStyle style) => style switch
        {
            ListNumberStyle.ArabicParenthesis => (PageNumberFormat.Arabic, null, ")"),
            ListNumberStyle.Parenthesized => (PageNumberFormat.Arabic, "(", ")"),
            ListNumberStyle.Circled => (PageNumberFormat.CircledNumber, null, null),
            ListNumberStyle.Kanji => (PageNumberFormat.Kanji, null, "、"),
            ListNumberStyle.KanjiParenthesized => (PageNumberFormat.Kanji, "（", "）"),
            ListNumberStyle.LowerLetter => (PageNumberFormat.LowerLetter, null, null),
            ListNumberStyle.UpperLetter => (PageNumberFormat.UpperLetter, null, null),
            ListNumberStyle.LowerRoman => (PageNumberFormat.LowerRoman, null, null),
            ListNumberStyle.UpperRoman => (PageNumberFormat.UpperRoman, null, null),
            _ => (null, null, null),
        };
    }
}
