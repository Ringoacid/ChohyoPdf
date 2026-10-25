using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ChohyoPdf.Designer
{
    /// <summary>double? と文字列の変換（空文字列は null）。</summary>
    public sealed class NullableDoubleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is double d ? d.ToString("0.###", CultureInfo.CurrentCulture) : "";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string s = (value as string ?? "").Trim();
            if (s.Length == 0) return null;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out double d) ? d : DependencyProperty.UnsetValue;
        }
    }

    /// <summary>サンプルの行と、タブ区切りの文字列（Excel からの貼り付け）の変換。</summary>
    public sealed class SampleRowsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is List<List<string>> rows ? string.Join("\r\n", rows.Select(r => string.Join("\t", r))) : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var rows = new List<List<string>>();
            foreach (string line in (value as string ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                rows.Add(line.Split('\t').ToList());
            }
            return rows;
        }
    }

    /// <summary>列挙値が引数と等しいか（ツールのボタン用）。</summary>
    public sealed class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value != null && parameter != null && value.ToString() == parameter.ToString();

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true && parameter != null ? Enum.Parse(targetType, parameter.ToString()!) : Binding.DoNothing;
    }

    /// <summary>null（または空文字列）なら Collapsed。引数が "Inverse" なら逆（null のときだけ Visible）。</summary>
    public sealed class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool empty = value == null || (value is string s && s.Length == 0);
            if (parameter as string == "Inverse") empty = !empty;
            return empty ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>false なら Visible（BooleanToVisibilityConverter の逆）。</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>数が 0 なら Visible（一覧が空のときの案内用）。引数が "Inverse" なら逆（1 以上のときだけ Visible）。</summary>
    public sealed class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is int n && n == 0) != (parameter as string == "Inverse") ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>用紙が「カスタム」なら Visible。</summary>
    public sealed class CustomPaperVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value as string == Model.PaperSizes.Custom ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>項目の種類・書式・サンプルから、表示例を作る（[0] 種類、[1] 書式、[2] サンプル）。</summary>
    public sealed class FieldExampleConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not Model.FieldKind kind) return "";
            // [3] は計算する項目か（C# の式）
            if (values.Length > 3 && values[3] is true) return "＝ C# の式で計算";
            string? sample = values.Length > 2 ? values[2] as string : null;
            if (kind == Model.FieldKind.Text) return sample ?? "";
            return Model.FormatPresets.Example(kind, values[1] as string, sample) ?? "（書式が正しくありません）";
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>項目の種類の表示名。</summary>
    public sealed class FieldKindLabelConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Model.FieldKind kind ? kind == Model.FieldKind.Number ? "数値" : kind == Model.FieldKind.Date ? "日付" : "文字列" : "";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>書式を使う種類（数値・日付）なら Visible。</summary>
    public sealed class HasFormatVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Model.FieldKind kind && kind != Model.FieldKind.Text ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>列挙値が引数と等しければ Visible（差し込みのウィンドウの章の中身用）。</summary>
    public sealed class EnumVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value != null && parameter != null && value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>項目・列の名前を、差し込み欄の書き方 {名前} にする。</summary>
    public sealed class BracesConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => "{" + value + "}";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>項目・一覧の用紙での使用数（[0] 項目か一覧、[1] MainViewModel.UsageCounts）。</summary>
    public sealed class UsageCountConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
            => values.Length > 1 && values[0] != null && values[1] is IReadOnlyDictionary<object, int> counts && counts.TryGetValue(values[0]!, out int n) ? n : 0;

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>2 つの値が同じオブジェクトか（選んでいる列の見出しの強調用）。</summary>
    public sealed class SameObjectConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
            => values.Length > 1 && values[0] != null && ReferenceEquals(values[0], values[1]);

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>一覧の列の見出しに出す、種類と書式の例（[0] 種類、[1] 書式）。例: 「数値・1,234,567.5」。</summary>
    public sealed class ColumnKindSummaryConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not Model.FieldKind kind) return "";
            string label = (string)new FieldKindLabelConverter().Convert(kind, typeof(string), null, culture);
            if (kind == Model.FieldKind.Text) return label;
            return label + "・" + (Model.FormatPresets.Example(kind, values[1] as string, null) ?? "（書式が正しくありません）");
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>null でなければ true（選んでいるものがあるときだけ使える欄の IsEnabled 用）。</summary>
    public sealed class NotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value != null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>
    /// 表示倍率とズームのスライダーの位置の変換。Word のスライダーのように、中央が 100% で、左右の端が 25% と 400%（-1 ～ 1）。
    /// </summary>
    public sealed class ZoomSliderConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is double zoom && zoom > 0 ? Math.Log(zoom, 4) : 0.0;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double v = value is double d ? d : 0;
            // 中央の近くでは、ちょうど 100% にする
            if (Math.Abs(v) < 0.02) return 1.0;
            return Math.Round(Math.Pow(4, v), 2);
        }
    }

    /// <summary>bool を反転する（「1 ページ」のように、オフの状態を選ぶボタン用）。</summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    }
}
