using System.Collections.Generic;
using System.Linq;
using ChohyoPdf.Designer.Rendering;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>デザインの問題（差し込み欄の名前の間違いなど）を調べる。</summary>
    public static class ReportValidator
    {
        public static List<string> Validate(ReportDefinition report)
        {
            var messages = new List<string>();
            CheckNames(report.Fields.Select(f => f.Name), "差し込み項目", messages);
            CheckNames(report.Lists.Select(l => l.Name), "一覧", messages);
            foreach (var list in report.Lists) CheckNames(list.Columns.Select(c => c.Name), "一覧「" + list.Name + "」の列", messages);
            foreach (var field in report.Fields)
            {
                if (field.IsComputed && string.IsNullOrWhiteSpace(field.Script))
                    messages.Add("差し込み項目「" + field.Name + "」: 値を計算する C# の式を入力してください。");
                if (TextTemplate.IsBuiltIn(field.Name))
                    messages.Add("差し込み項目「" + field.Name + "」は組み込みの差し込み欄と同じ名前です。差し込み項目の値が優先されます。");
            }
            ReportData.FromSamples(report, messages);

            // 全ページ共通の部品は -1 番目として調べる
            for (int p = -1; p < report.Pages.Count; p++)
            {
                bool common = p < 0;
                var elements = common ? report.Common.Elements : report.Pages[p].Elements;
                // ページが複数あるときは、どのページの部品かを書く
                string pageLabel = common ? "全ページ共通の" : report.Pages.Count > 1 ? (p + 1) + " ページ目の" : "";
                foreach (var element in elements)
                {
                    string label = pageLabel + element.KindName + "「" + (element.Name.Length > 0 ? element.Name : "名前なし") + "」";
                    switch (element)
                    {
                        case TextElement text:
                            CheckTemplate(report, text.Text, null, label, messages);
                            break;
                        case ImageElement image when image.Path.Length == 0:
                            messages.Add(label + ": 画像ファイルを指定してください。");
                            break;
                        case TableElement table:
                            foreach (var row in table.Rows)
                            {
                                foreach (var cell in row.Cells) CheckTemplate(report, cell.Text, null, label, messages);
                            }
                            if (table.HasDetail && common)
                            {
                                messages.Add(label + ": 全ページ共通の部品の表では、行をくり返せません（くり返し行は空の行になります）。");
                            }
                            else if (table.HasDetail)
                            {
                                var list = report.FindList(table.DetailSource);
                                if (list == null) messages.Add(label + ": くり返す一覧「" + table.DetailSource + "」がありません。");
                                foreach (var cell in table.DetailRow.Cells) CheckTemplate(report, cell.Text, list, label, messages);
                            }
                            break;
                    }
                }
            }
            return messages.Distinct().ToList();
        }

        private static void CheckNames(IEnumerable<string> names, string kind, List<string> messages)
        {
            var seen = new HashSet<string>();
            foreach (string name in names)
            {
                if (name.Trim().Length == 0) messages.Add(kind + "の名前が空です。");
                else if (name.IndexOfAny(new[] { '{', '}', ':' }) >= 0) messages.Add(kind + "「" + name + "」: 名前に { } : は使えません。");
                else if (!seen.Add(name)) messages.Add(kind + "「" + name + "」が重複しています。最初のものだけが使われます。");
            }
        }

        private static void CheckTemplate(ReportDefinition report, string text, ListDefinition? list, string label, List<string> messages)
        {
            foreach (var segment in TextTemplate.Parse(text))
            {
                if (!segment.IsField) continue;
                string name = segment.Text;
                if (list?.FindColumn(name) != null || report.FindField(name) != null || TextTemplate.IsBuiltIn(name)) continue;
                bool inOtherList = report.Lists.Any(l => l.FindColumn(name) != null);
                messages.Add(label + ": 差し込み欄 {" + name + "} に対応する差し込み項目がありません。"
                    + (inOtherList ? "一覧の列は、その一覧をくり返す表の、くり返し行でだけ使えます。" : ""));
            }
        }
    }
}
