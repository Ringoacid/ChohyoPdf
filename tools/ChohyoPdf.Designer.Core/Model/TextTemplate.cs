using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>差し込み欄を含む文字列の一部分。</summary>
    public sealed class TemplateSegment
    {
        private TemplateSegment(bool isField, string text, string? format)
        {
            IsField = isField;
            Text = text;
            Format = format;
        }

        /// <summary>差し込み欄なら true、そのまま出す文字列なら false。</summary>
        public bool IsField { get; }

        /// <summary>文字列、または差し込む項目の名前。</summary>
        public string Text { get; }

        /// <summary>差し込み欄に書いた書式（{項目:書式} の「書式」）。</summary>
        public string? Format { get; }

        public static TemplateSegment Literal(string text) => new TemplateSegment(false, text, null);

        public static TemplateSegment Field(string name, string? format) => new TemplateSegment(true, name, format);
    }

    /// <summary>組み込みの差し込み欄の値のもと。</summary>
    public enum BuiltInSource
    {
        /// <summary>文書全体でのページ番号（1 から）。</summary>
        PageNumber,

        /// <summary>総ページ数。</summary>
        PageCount,

        /// <summary>PDF を作る日（時刻は 0:00）。</summary>
        Today,

        /// <summary>PDF を作る日時。</summary>
        Now,

        /// <summary>PDF を作る年（数値）。</summary>
        Year,

        /// <summary>PDF を作る月（数値）。</summary>
        Month,
    }

    /// <summary>値を渡さなくても使える差し込み欄（{ページ番号} {今日} など）。</summary>
    public sealed class BuiltInField
    {
        public BuiltInField(string name, BuiltInSource source, string? defaultFormat, string description)
        {
            Name = name;
            Source = source;
            DefaultFormat = defaultFormat;
            Description = description;
        }

        public string Name { get; }

        public BuiltInSource Source { get; }

        /// <summary>種類。ページ番号・年・月は数値、そのほかは日付。</summary>
        public FieldKind Kind => Source == BuiltInSource.Today || Source == BuiltInSource.Now ? FieldKind.Date : FieldKind.Number;

        /// <summary>差し込み欄で書式を指定しないときの書式（null なら書式なし）。</summary>
        public string? DefaultFormat { get; }

        public string Description { get; }

        /// <summary>PDF を作る日時を使う。</summary>
        public bool UsesNow => Source != BuiltInSource.PageNumber && Source != BuiltInSource.PageCount;

        /// <summary>生成するコードでの、日時からの値の式（日時を使わないものは null）。</summary>
        public string? NowExpression
            => Source switch
            {
                BuiltInSource.Today => "now.Date",
                BuiltInSource.Now => "now",
                BuiltInSource.Year => "now.Year",
                BuiltInSource.Month => "now.Month",
                _ => null,
            };

        /// <summary>値（ページ番号・年・月は int、日付は DateTime）。</summary>
        public object Value(DateTime now, int pageNumber, int pageCount)
            => Source switch
            {
                BuiltInSource.PageNumber => pageNumber,
                BuiltInSource.PageCount => pageCount,
                BuiltInSource.Today => now.Date,
                BuiltInSource.Now => now,
                BuiltInSource.Year => now.Year,
                _ => (object)now.Month,
            };
    }

    /// <summary>
    /// 差し込み欄の書式: {項目名} または {項目名:書式}。"{" と "}" そのものは "{{" と "}}" と書く。
    /// </summary>
    public static class TextTemplate
    {
        /// <summary>ページ番号を差し込む名前。</summary>
        public const string PageNumber = "ページ番号";

        /// <summary>総ページ数を差し込む名前。</summary>
        public const string PageCount = "総ページ数";

        /// <summary>日付の既定の書式。</summary>
        public const string DefaultDateFormat = "yyyy/MM/dd";

        /// <summary>書式に使うカルチャ。</summary>
        public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("ja-JP");

        /// <summary>和暦（元号）で日付を書くカルチャ。書式に "gg" があるときに使う。</summary>
        public static CultureInfo JapaneseCulture { get; } = CreateJapaneseCulture();

        private static CultureInfo CreateJapaneseCulture()
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("ja-JP").Clone();
            culture.DateTimeFormat.Calendar = new JapaneseCalendar();
            return culture;
        }

        /// <summary>
        /// 組み込みの差し込み欄。値を渡さなくても使える。項目・一覧の列に同じ名前があれば、そちらを優先する。
        /// ReportRenderer と CSharpGenerator は、この表から値と書式を決める。
        /// </summary>
        public static IReadOnlyList<BuiltInField> BuiltIns { get; } = new[]
        {
            new BuiltInField(PageNumber, BuiltInSource.PageNumber, null, "ページ番号（文書全体で 1 から）"),
            new BuiltInField(PageCount, BuiltInSource.PageCount, null, "総ページ数"),
            new BuiltInField("今日", BuiltInSource.Today, DefaultDateFormat, "PDF を作った日"),
            new BuiltInField("現在日時", BuiltInSource.Now, "yyyy/MM/dd HH:mm", "PDF を作った日時"),
            new BuiltInField("現在時刻", BuiltInSource.Now, "H:mm", "PDF を作った時刻"),
            new BuiltInField("和暦", BuiltInSource.Today, "ggy年M月d日", "PDF を作った日を和暦（令和）で"),
            new BuiltInField("和暦年", BuiltInSource.Today, "ggy年", "PDF を作った年を和暦（令和）で"),
            new BuiltInField("今年", BuiltInSource.Year, null, "PDF を作った年（数値）"),
            new BuiltInField("今月", BuiltInSource.Month, null, "PDF を作った月（数値）"),
        };

        public static BuiltInField? FindBuiltIn(string name)
        {
            foreach (var b in BuiltIns)
            {
                if (b.Name == name) return b;
            }
            return null;
        }

        public static bool IsBuiltIn(string name) => FindBuiltIn(name) != null;

        /// <summary>組み込みの差し込み欄の値を文字列にする（生成する C# コードと同じ規則）。</summary>
        public static string FormatBuiltIn(BuiltInField field, string? segmentFormat, DateTime now, int pageNumber, int pageCount)
        {
            string? format = string.IsNullOrEmpty(segmentFormat) ? field.DefaultFormat : segmentFormat;
            object value = field.Value(now, pageNumber, pageCount);
            if (value is DateTime date) return FormatDate(date, format);
            int n = (int)value;
            if (NumberStyleOf(format) is PageNumberFormat style) return PageNumberFormatter.Format(n, style);
            return format == null ? n.ToString(Culture) : n.ToString(format, Culture);
        }

        /// <summary>
        /// ページ番号などの数値の組み込みの差し込み欄に使える、ローマ数字・漢数字などの書式の名前（{ページ番号:ローマ数字} のように書く）。
        /// 書式全体がこの名前と同じときだけ使う（.NET の数値の書式では、これらの名前はそのままの文字になるだけなので、区別できる）。
        /// </summary>
        public static IReadOnlyList<(string Name, PageNumberFormat Format)> NumberStyleNames { get; } = new[]
        {
            ("ローマ数字", PageNumberFormat.UpperRoman),
            ("ローマ数字小文字", PageNumberFormat.LowerRoman),
            ("英字", PageNumberFormat.UpperLetter),
            ("英字小文字", PageNumberFormat.LowerLetter),
            ("全角", PageNumberFormat.FullWidthArabic),
            ("丸数字", PageNumberFormat.CircledNumber),
            ("漢数字", PageNumberFormat.Kanji),
        };

        /// <summary>書式がローマ数字・漢数字などの名前（<see cref="NumberStyleNames"/>）なら、その書式。</summary>
        public static PageNumberFormat? NumberStyleOf(string? format)
        {
            foreach (var (name, style) in NumberStyleNames)
            {
                if (name == format) return style;
            }
            return null;
        }

        /// <summary>書式に元号（"gg"）があるか。あれば和暦で書く。</summary>
        public static bool IsJapaneseEraFormat(string? format) => format != null && format.Contains("gg");

        /// <summary>
        /// 日付を書式化する。書式に "gg" があれば和暦で書く（明治より前の日付は空文字列。生成するコードの FormatJapaneseDate と同じ）。
        /// </summary>
        public static string FormatDate(DateTime date, string? format)
        {
            if (!IsJapaneseEraFormat(format)) return date.ToString(format, Culture);
            return date < JapaneseCulture.DateTimeFormat.Calendar.MinSupportedDateTime ? "" : date.ToString(format, JapaneseCulture);
        }

        public static List<TemplateSegment> Parse(string text)
        {
            var result = new List<TemplateSegment>();
            var literal = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '{' && i + 1 < text.Length && text[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }
                if (ch == '}' && i + 1 < text.Length && text[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }
                if (ch == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        string inner = text.Substring(i + 1, close - i - 1);
                        int colon = inner.IndexOf(':');
                        string name = (colon >= 0 ? inner.Substring(0, colon) : inner).Trim();
                        string? format = colon >= 0 ? inner.Substring(colon + 1) : null;
                        if (name.Length > 0 && name.IndexOf('{') < 0)
                        {
                            if (literal.Length > 0)
                            {
                                result.Add(TemplateSegment.Literal(literal.ToString()));
                                literal.Clear();
                            }
                            result.Add(TemplateSegment.Field(name, string.IsNullOrEmpty(format) ? null : format));
                            i = close + 1;
                            continue;
                        }
                    }
                }
                literal.Append(ch);
                i++;
            }
            if (literal.Length > 0) result.Add(TemplateSegment.Literal(literal.ToString()));
            return result;
        }

        /// <summary>差し込み欄を含むか。</summary>
        public static bool HasFields(string text)
        {
            foreach (var s in Parse(text))
            {
                if (s.IsField) return true;
            }
            return false;
        }

        /// <summary>差し込み欄を使わない文字列を、差し込み欄の書式にする（"{" を "{{" にする）。</summary>
        public static string Escape(string text) => text.Replace("{", "{{").Replace("}", "}}");

        /// <summary>差し込み欄を元の文字列のまま戻す（デザイン画面での表示用）。</summary>
        public static string ToDisplay(TemplateSegment segment)
            => segment.IsField ? "{" + segment.Text + (segment.Format != null ? ":" + segment.Format : "") + "}" : segment.Text;

        /// <summary>
        /// 実際に使う書式。差し込み欄の書式、項目の書式、種類ごとの既定の書式の順に探す。
        /// 数値の既定は null（書式を付けず、1234 は 1234 のまま）。
        /// </summary>
        public static string? EffectiveFormat(FieldKind kind, string? segmentFormat, string? fieldFormat)
        {
            if (kind == FieldKind.Text) return null;
            if (!string.IsNullOrEmpty(segmentFormat)) return segmentFormat;
            if (!string.IsNullOrEmpty(fieldFormat)) return fieldFormat;
            return kind == FieldKind.Number ? null : DefaultDateFormat;
        }

        /// <summary>値を文字列にする。生成する C# コードと同じ規則で書式化する。</summary>
        public static string FormatValue(object? value, FieldKind kind, string? format)
        {
            switch (kind)
            {
                case FieldKind.Number:
                    return (value is decimal d ? d : 0m).ToString(format, Culture);
                case FieldKind.Date:
                    return FormatDate(value is DateTime t ? t : default, format);
                default:
                    return value as string ?? "";
            }
        }

        /// <summary>サンプルの文字列を値にする。読めなければ false（値は 0 や 0001/01/01 になる）。</summary>
        public static bool TryParseSample(string text, FieldKind kind, out object value)
        {
            switch (kind)
            {
                case FieldKind.Number:
                    {
                        string t = text.Trim().Replace(",", "").Replace("，", "").Replace("¥", "").Replace("￥", "");
                        bool ok = decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d);
                        value = ok ? d : 0m;
                        return ok || t.Length == 0;
                    }
                case FieldKind.Date:
                    {
                        string t = text.Trim();
                        bool ok = DateTime.TryParseExact(t, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt);
                        value = ok ? dt : default(DateTime);
                        return ok || t.Length == 0;
                    }
                default:
                    value = text;
                    return true;
            }
        }

        private static readonly string[] DateFormats =
        {
            "yyyy/M/d", "yyyy-M-d", "yyyy/M/d H:m", "yyyy-M-d H:m", "yyyy/M/d H:m:s", "yyyy-M-d H:m:s",
            "yyyy-MM-ddTHH:mm:ss", "yyyy年M月d日",
        };
    }
}
