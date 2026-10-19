namespace ChohyoPdf.Text
{
    /// <summary>改行位置の判定に使う文字クラス（UAX #14 を日本語向けに簡略化したもの）。</summary>
    internal enum BreakClass : byte
    {
        /// <summary>英数字など（単語の途中では改行しない）。</summary>
        Alphabetic,

        /// <summary>和文文字（前後どちらでも改行できる）。</summary>
        Ideographic,

        /// <summary>空白（直後で改行できる。行末ではぶら下げる）。</summary>
        Space,

        /// <summary>ゼロ幅空白（直後で改行できる）。</summary>
        ZeroWidthSpace,

        /// <summary>和文の始め括弧類（行末禁則: 直後で改行しない）。</summary>
        Open,

        /// <summary>欧文の始め括弧（直後で改行しない）。</summary>
        OpenLatin,

        /// <summary>和文の終わり括弧・句読点・小書き仮名など（行頭禁則: 直前で改行しない）。</summary>
        Close,

        /// <summary>欧文の句読点・終わり括弧（直前で改行しない）。</summary>
        CloseLatin,

        /// <summary>ハイフン（直後で改行できる）。</summary>
        Hyphen,

        /// <summary>強制改行。</summary>
        HardBreak,
    }

    internal static class TextUtil
    {
        /// <summary>描画しない文字（異体字セレクタ、結合子、BOM など）。</summary>
        public static bool IsIgnorable(int cp)
        {
            return (cp >= 0xFE00 && cp <= 0xFE0F)
                || (cp >= 0xE0100 && cp <= 0xE01EF)
                || cp == 0x200B || cp == 0x200C || cp == 0x200D || cp == 0x2060 || cp == 0xFEFF || cp == 0x00AD
                || cp == 0x180E
                || (cp < 0x20 && cp != '\t' && cp != '\n');
        }

        private const string CloseChars =
            "、。，．・：；？！゛゜ヽヾゝゞ々〻ー‐゠–〜～…‥" +
            "ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ" +
            "」』）］｝〉》】〕〙〗〟’”｣｡､･ｰﾞﾟｧｨｩｪｫｬｭｮｯ" +
            "％‰℃¢°′″";

        private const string OpenChars = "「『（［｛〈《【〔〘〖〝‘“｢＜￥＄£＃";

        private const string CloseLatinChars = ".,:;!?)]}%'\"";

        private const string OpenLatinChars = "([{";

        public static BreakClass Classify(int cp)
        {
            if (cp == '\n') return BreakClass.HardBreak;
            if (cp == ' ' || cp == '\t') return BreakClass.Space;
            if (cp == 0x200B) return BreakClass.ZeroWidthSpace;
            if (cp < 0x10000)
            {
                char c = (char)cp;
                if (CloseChars.IndexOf(c) >= 0) return BreakClass.Close;
                if (OpenChars.IndexOf(c) >= 0) return BreakClass.Open;
                if (cp < 0x80)
                {
                    if (CloseLatinChars.IndexOf(c) >= 0) return BreakClass.CloseLatin;
                    if (OpenLatinChars.IndexOf(c) >= 0) return BreakClass.OpenLatin;
                    if (c == '-') return BreakClass.Hyphen;
                    return BreakClass.Alphabetic;
                }
            }
            if (IsIdeographic(cp)) return BreakClass.Ideographic;
            return BreakClass.Alphabetic;
        }

        public static bool IsIdeographic(int cp)
        {
            return (cp >= 0x1100 && cp <= 0x11FF)
                || (cp >= 0x2E80 && cp <= 0x2FFF)
                || (cp >= 0x3000 && cp <= 0x303F)
                || (cp >= 0x3040 && cp <= 0x30FF)
                || (cp >= 0x3100 && cp <= 0x31FF)
                || (cp >= 0x3200 && cp <= 0x4DBF)
                || (cp >= 0x4E00 && cp <= 0x9FFF)
                || (cp >= 0xA000 && cp <= 0xA4CF)
                || (cp >= 0xAC00 && cp <= 0xD7AF)
                || (cp >= 0xF900 && cp <= 0xFAFF)
                || (cp >= 0xFE30 && cp <= 0xFE4F)
                || (cp >= 0xFF00 && cp <= 0xFFEF)
                || (cp >= 0x1F000 && cp <= 0x1FAFF)
                || (cp >= 0x20000 && cp <= 0x3FFFF)
                || (cp >= 0x2190 && cp <= 0x21FF)
                || (cp >= 0x2460 && cp <= 0x24FF)
                || (cp >= 0x25A0 && cp <= 0x27BF);
        }

        /// <summary><paramref name="a"/> と <paramref name="b"/> の間で改行してよいか。</summary>
        public static bool CanBreakBetween(BreakClass a, BreakClass b)
        {
            if (b == BreakClass.Space) return false;
            if (a == BreakClass.Space) return b != BreakClass.Close && b != BreakClass.CloseLatin;
            if (a == BreakClass.ZeroWidthSpace) return true;
            if (b == BreakClass.ZeroWidthSpace) return false;
            if (b == BreakClass.Close || b == BreakClass.CloseLatin) return false;
            if (a == BreakClass.Open || a == BreakClass.OpenLatin) return false;
            if (a == BreakClass.Ideographic || a == BreakClass.Close) return true;
            if (b == BreakClass.Ideographic || b == BreakClass.Open) return true;
            if (a == BreakClass.Hyphen && b == BreakClass.Alphabetic) return true;
            return false;
        }
    }
}
