using System;
using System.Collections.Generic;
using System.Linq;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>よく使う書式（書式の選択肢）。</summary>
    public sealed class FormatPreset
    {
        public FormatPreset(string format, string description)
        {
            Format = format;
            Description = description;
        }

        /// <summary>書式指定文字列。"" は既定の書式。</summary>
        public string Format { get; }

        /// <summary>書式の説明。</summary>
        public string Description { get; }
    }

    /// <summary>
    /// 数値・日付の書式の選択肢。
    /// 生成したコードを .NET Framework で動かしても同じ文字列になるように、
    /// カルチャによって変わる標準の書式（C、N、d など）は使わず、記号を並べたカスタム書式だけにする。
    /// </summary>
    public static class FormatPresets
    {
        /// <summary>サンプルがないときに表示例に使う数値。</summary>
        public const decimal ExampleNumber = 1234567.5m;

        /// <summary>サンプルがないときに表示例に使う日時。</summary>
        public static DateTime ExampleDate { get; } = new DateTime(2026, 10, 20, 14, 30, 0);

        private static readonly FormatPreset[] Numbers =
        {
            new FormatPreset("", "書式なし（そのまま）"),
            new FormatPreset("#,0", "3 桁区切り"),
            new FormatPreset("#,0.00", "3 桁区切り・小数 2 桁"),
            new FormatPreset("¥#,0", "円記号を付ける"),
            new FormatPreset("#,0円", "「円」を付ける"),
            new FormatPreset("#,0;△#,0", "3 桁区切り・マイナスは △"),
            new FormatPreset("0", "整数（四捨五入）"),
            new FormatPreset("0.0", "小数 1 桁"),
            new FormatPreset("0.00", "小数 2 桁"),
            new FormatPreset("0%", "パーセント（0.1 → 10%）"),
            new FormatPreset("000", "3 桁まで 0 で埋める"),
        };

        private static readonly FormatPreset[] Dates =
        {
            new FormatPreset("", "既定（年/月/日）"),
            new FormatPreset("yyyy/M/d", "年/月/日（0 を付けない）"),
            new FormatPreset("yyyy年M月d日", "年月日"),
            new FormatPreset("yyyy年M月d日（ddd）", "年月日と曜日"),
            new FormatPreset("ggy年M月d日", "和暦（令和）の年月日"),
            new FormatPreset("ggy年M月", "和暦（令和）の年月"),
            new FormatPreset("yyyy年M月", "年月"),
            new FormatPreset("M月d日", "月日"),
            new FormatPreset("yyyy-MM-dd", "ハイフン区切り"),
            new FormatPreset("yyyy/MM/dd HH:mm", "日付と時刻"),
            new FormatPreset("H:mm", "時刻"),
        };

        /// <summary>種類ごとの選択肢。文字列には書式がない。</summary>
        public static IReadOnlyList<FormatPreset> For(FieldKind kind)
            => kind == FieldKind.Number ? Numbers : kind == FieldKind.Date ? Dates : Array.Empty<FormatPreset>();

        /// <summary>書式が選択肢のどれかなら、その選択肢。</summary>
        public static FormatPreset? Find(FieldKind kind, string? format)
            => For(kind).FirstOrDefault(p => p.Format == (format ?? ""));

        /// <summary>
        /// 書式を付けた表示例。サンプルの値が読めればその値を、読めなければ例の値を使う。
        /// 書式が正しくなければ null。
        /// </summary>
        public static string? Example(FieldKind kind, string? format, string? sample)
        {
            object value;
            if (kind == FieldKind.Text) return sample ?? "";
            if (string.IsNullOrWhiteSpace(sample) || !TextTemplate.TryParseSample(sample!, kind, out value))
                value = kind == FieldKind.Number ? ExampleNumber : (object)ExampleDate;
            try
            {
                return TextTemplate.FormatValue(value, kind, TextTemplate.EffectiveFormat(kind, null, format));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
