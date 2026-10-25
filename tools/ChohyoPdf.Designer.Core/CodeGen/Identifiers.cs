using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChohyoPdf.Designer.CodeGen
{
    /// <summary>項目名などから C# の識別子を作る。</summary>
    public static class Identifiers
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        /// <summary>
        /// 名前を識別子にする。使えない文字は "_" に置き換え、数字で始まるなら "_" を付け、予約語なら "@" を付ける。
        /// </summary>
        public static string From(string name, string fallback)
        {
            var sb = new StringBuilder();
            foreach (char ch in name.Trim())
            {
                bool ok = sb.Length == 0 ? IsStart(ch) || char.IsDigit(ch) : IsPart(ch);
                sb.Append(ok ? ch : '_');
            }
            string id = sb.ToString().Trim('_');
            if (id.Length == 0) return fallback;
            if (char.IsDigit(id[0])) id = "_" + id;
            if (Keywords.Contains(id)) id = "@" + id;
            return id;
        }

        /// <summary>ASCII の英数字だけを使い、単語の先頭を大文字にした名前（例: "Yu Gothic Regular" → "YuGothicRegular"）。</summary>
        public static string PascalAscii(string name, string fallback)
        {
            var sb = new StringBuilder();
            bool upper = true;
            foreach (char ch in name)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(upper ? char.ToUpperInvariant(ch) : ch);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }
            if (sb.Length == 0) return fallback;
            if (char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }

        private static bool IsStart(char ch)
        {
            if (ch == '_') return true;
            switch (CharUnicodeInfo.GetUnicodeCategory(ch))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPart(char ch)
        {
            if (IsStart(ch)) return true;
            switch (CharUnicodeInfo.GetUnicodeCategory(ch))
            {
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>同じ範囲の中で重ならないように識別子を割り当てる。</summary>
    internal sealed class NameScope
    {
        private readonly HashSet<string> _used = new HashSet<string>();

        public NameScope(params string[] reserved)
        {
            foreach (string r in reserved) _used.Add(r);
        }

        public string Reserve(string id)
        {
            string candidate = id;
            for (int n = 2; !_used.Add(candidate.TrimStart('@')); n++) candidate = id.TrimStart('@') + n;
            return candidate;
        }
    }
}
