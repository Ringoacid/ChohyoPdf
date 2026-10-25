using System.Globalization;
using System.Text;

namespace ChohyoPdf
{
    /// <summary>ページ番号の書式。<see cref="Section.PageNumberFormat"/> や <see cref="FieldRun.Format"/> で指定する。</summary>
    public enum PageNumberFormat
    {
        /// <summary>アラビア数字（1, 2, 3, …）。</summary>
        Arabic,

        /// <summary>小文字のローマ数字（i, ii, iii, …）。1〜3999 の範囲。範囲外はアラビア数字。</summary>
        LowerRoman,

        /// <summary>大文字のローマ数字（I, II, III, …）。1〜3999 の範囲。範囲外はアラビア数字。</summary>
        UpperRoman,

        /// <summary>小文字の英字（a, b, …, z, aa, bb, …）。Word と同じく 27 以降は同じ文字を繰り返す。1 未満はアラビア数字。</summary>
        LowerLetter,

        /// <summary>大文字の英字（A, B, …, Z, AA, BB, …）。1 未満はアラビア数字。</summary>
        UpperLetter,

        /// <summary>全角数字（１, ２, ３, …）。</summary>
        FullWidthArabic,

        /// <summary>丸数字（①〜⑳）。1〜20 の範囲。範囲外はアラビア数字。</summary>
        CircledNumber,

        /// <summary>漢数字（一, 二, …, 十, 十一, …, 二十, 百, …）。0 は「〇」、1〜9999 の範囲。範囲外はアラビア数字。</summary>
        Kanji,
    }

    /// <summary>
    /// 番号を <see cref="PageNumberFormat"/> の書式の文字列にする。ページ番号のフィールド（<see cref="FieldRun"/>）、
    /// <see cref="PageContext.PageNumberText"/>、番号付きリストの番号、脚注番号は、すべてここで文字列にする。
    /// 低レベル層（<see cref="PdfGraphics"/>）でページ番号を描くときも、これを使えば同じ表記になる。
    /// </summary>
    public static class PageNumberFormatter
    {
        private static readonly string[] RomanUpper = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
        private static readonly string[] RomanLower = { "m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i" };
        private static readonly int[] RomanValues = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        private const string KanjiDigits = "〇一二三四五六七八九";

        /// <summary>
        /// 番号を指定の書式の文字列にする（例: 4 と <see cref="PageNumberFormat.UpperRoman"/> なら "IV"）。
        /// 書式で表せない番号（ローマ数字の 0 や 4000 以上、丸数字の 21 以上など）はアラビア数字にする。
        /// </summary>
        /// <param name="number">番号。</param>
        /// <param name="format">書式。</param>
        public static string Format(int number, PageNumberFormat format)
        {
            switch (format)
            {
                case PageNumberFormat.LowerRoman:
                    if (number >= 1 && number <= 3999) return Roman(number, RomanLower);
                    break;
                case PageNumberFormat.UpperRoman:
                    if (number >= 1 && number <= 3999) return Roman(number, RomanUpper);
                    break;
                case PageNumberFormat.LowerLetter:
                    if (number >= 1) return Letters(number, 'a');
                    break;
                case PageNumberFormat.UpperLetter:
                    if (number >= 1) return Letters(number, 'A');
                    break;
                case PageNumberFormat.FullWidthArabic:
                    return FullWidth(Arabic(number));
                case PageNumberFormat.CircledNumber:
                    if (number >= 1 && number <= 20) return ((char)(0x2460 + number - 1)).ToString();
                    break;
                case PageNumberFormat.Kanji:
                    if (number >= 0 && number <= 9999) return Kanji(number);
                    break;
            }
            return Arabic(number);
        }

        private static string Arabic(int number) => number.ToString(CultureInfo.InvariantCulture);

        private static string Roman(int number, string[] symbols)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < RomanValues.Length; i++)
            {
                while (number >= RomanValues[i])
                {
                    sb.Append(symbols[i]);
                    number -= RomanValues[i];
                }
            }
            return sb.ToString();
        }

        private static string Letters(int number, char first)
        {
            char c = (char)(first + (number - 1) % 26);
            return new string(c, (number - 1) / 26 + 1);
        }

        private static string FullWidth(string ascii)
        {
            var sb = new StringBuilder(ascii.Length);
            foreach (char c in ascii)
            {
                if (c >= '0' && c <= '9') sb.Append((char)(c - '0' + '０'));
                else if (c == '-') sb.Append('－');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string Kanji(int number)
        {
            if (number == 0) return KanjiDigits[0].ToString();
            var sb = new StringBuilder();
            AppendUnit(sb, number / 1000 % 10, '千');
            AppendUnit(sb, number / 100 % 10, '百');
            AppendUnit(sb, number / 10 % 10, '十');
            int ones = number % 10;
            if (ones > 0) sb.Append(KanjiDigits[ones]);
            return sb.ToString();
        }

        /// <summary>千・百・十の位。係数 1 は省略する（十, 百, 千）。</summary>
        private static void AppendUnit(StringBuilder sb, int digit, char unit)
        {
            if (digit == 0) return;
            if (digit > 1) sb.Append(KanjiDigits[digit]);
            sb.Append(unit);
        }
    }
}
