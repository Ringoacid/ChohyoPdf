using System;
using System.Globalization;
using System.Text;

namespace ChohyoPdf.Internal
{
    /// <summary>PDF の字句（数値・名前・文字列）を書式化するヘルパー。</summary>
    internal static class PdfFormat
    {
        /// <summary>指数表記を使わず、小数点以下最大 4 桁で数値を書式化する。</summary>
        public static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("PDF に NaN / Infinity は出力できません。", nameof(value));
            double rounded = Math.Round(value, 4, MidpointRounding.AwayFromZero);
            if (rounded == 0) return "0";
            if (Math.Abs(rounded) >= 1e15)
                throw new ArgumentOutOfRangeException(nameof(value), "数値が大きすぎます。");
            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>PDF 名前オブジェクト（先頭の / を含む）。</summary>
        public static string Name(string name)
        {
            var sb = new StringBuilder(name.Length + 1);
            sb.Append('/');
            foreach (byte b in Encoding.UTF8.GetBytes(name))
            {
                if (b < 0x21 || b > 0x7E || b == '#' || "()<>[]{}/%".IndexOf((char)b) >= 0)
                    sb.Append('#').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                else
                    sb.Append((char)b);
            }
            return sb.ToString();
        }

        /// <summary>
        /// テキスト文字列。ASCII だけならリテラル文字列、そうでなければ BOM 付き UTF-16BE の16進文字列にする。
        /// </summary>
        public static string TextString(string text)
        {
            bool ascii = true;
            foreach (char c in text)
            {
                if (c < 0x20 || c > 0x7E) { ascii = false; break; }
            }
            if (ascii)
            {
                var sb = new StringBuilder(text.Length + 2);
                sb.Append('(');
                foreach (char c in text)
                {
                    if (c == '(' || c == ')' || c == '\\') sb.Append('\\');
                    sb.Append(c);
                }
                sb.Append(')');
                return sb.ToString();
            }
            var bytes = Encoding.BigEndianUnicode.GetBytes(text);
            var hex = new StringBuilder(bytes.Length * 2 + 6);
            hex.Append("<FEFF");
            foreach (byte b in bytes) hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            hex.Append('>');
            return hex.ToString();
        }

        public static string Date(DateTimeOffset date)
        {
            var offset = date.Offset;
            string sign = offset < TimeSpan.Zero ? "-" : "+";
            var abs = offset.Duration();
            return "(D:" + date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + sign
                + abs.Hours.ToString("00", CultureInfo.InvariantCulture) + "'"
                + abs.Minutes.ToString("00", CultureInfo.InvariantCulture) + "')";
        }

        public static string Hex(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2 + 2);
            sb.Append('<');
            foreach (byte b in data) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            sb.Append('>');
            return sb.ToString();
        }
    }
}
