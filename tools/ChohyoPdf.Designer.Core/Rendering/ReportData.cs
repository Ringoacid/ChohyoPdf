using System;
using System.Collections.Generic;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>
    /// 文書に差し込むデータ。値は種類に応じて string / decimal / DateTime。
    /// </summary>
    public sealed class ReportData
    {
        /// <summary>項目の値。</summary>
        public Dictionary<string, object?> Values { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

        /// <summary>{今日} {現在時刻} などに使う、PDF を作る日時。</summary>
        public DateTime Now { get; set; } = DateTime.Now;

        /// <summary>一覧のデータ。1 行は列の名前から値を引く辞書。</summary>
        public Dictionary<string, List<Dictionary<string, object?>>> Lists { get; } = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);

        /// <summary>デザインに入力したサンプルの値からデータを作る。</summary>
        /// <param name="now">{今日} などに使う日時。null なら今の日時。</param>
        /// <param name="runScripts">
        /// 差し込み項目の C# の式を実行して値を求める。式は利用者が書いたコードなので、信頼できるデザインのときだけ true にする。
        /// false なら、計算する差し込み項目には値を入れない。
        /// </param>
        public static ReportData FromSamples(ReportDefinition report, ICollection<string>? warnings = null, DateTime? now = null, bool runScripts = false)
        {
            var data = new ReportData();
            if (now != null) data.Now = now.Value;
            foreach (var field in report.Fields)
            {
                if (field.IsComputed) continue;
                if (!TextTemplate.TryParseSample(field.SampleValue, field.Kind, out object value))
                    warnings?.Add("項目「" + field.Name + "」のサンプル「" + field.SampleValue + "」を" + KindLabel(field.Kind) + "として読めません。");
                data.Values[field.Name] = value;
            }
            foreach (var list in report.Lists)
            {
                var rows = new List<Dictionary<string, object?>>();
                foreach (var sample in list.SampleRows)
                {
                    var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (int c = 0; c < list.Columns.Count; c++)
                    {
                        var column = list.Columns[c];
                        string text = c < sample.Count ? sample[c] : "";
                        if (!TextTemplate.TryParseSample(text, column.Kind, out object value))
                            warnings?.Add("一覧「" + list.Name + "」の列「" + column.Name + "」のサンプル「" + text + "」を" + KindLabel(column.Kind) + "として読めません。");
                        row[column.Name] = value;
                    }
                    rows.Add(row);
                }
                data.Lists[list.Name] = rows;
            }
            if (runScripts)
            {
                foreach (var pair in ScriptEvaluator.Evaluate(report, data.Now, warnings))
                {
                    // 同じ名前の項目が先にあれば、そちらを使う
                    if (report.FindField(pair.Key)?.IsComputed == true) data.Values[pair.Key] = pair.Value;
                }
            }
            return data;
        }

        internal static string KindLabel(FieldKind kind)
            => kind == FieldKind.Number ? "数値" : kind == FieldKind.Date ? "日付" : "文字列";
    }
}
