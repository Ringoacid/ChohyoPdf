using System.Collections.Generic;
using System.Linq;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>差し込み項目・一覧を使っている部品の場所。</summary>
    public sealed class DataUsageLocation
    {
        public DataUsageLocation(int pageIndex, ReportElement element, int count)
        {
            PageIndex = pageIndex;
            Element = element;
            Count = count;
        }

        /// <summary>ページの番号（0 から）。全ページ共通の部品なら -1。</summary>
        public int PageIndex { get; }

        public bool IsCommon => PageIndex < 0;

        public ReportElement Element { get; }

        /// <summary>この部品の中で使っている数（一覧は常に 1）。</summary>
        public int Count { get; }
    }

    /// <summary>
    /// 差し込み項目・一覧を、用紙のどこで使っているかを調べる（「差し込み」タブの使用数と「用紙から探す」）。
    /// 名前は ReportValidator・ReportRenderer と同じ順に探す（くり返し行では一覧の列が先、同じ名前の項目は最初のもの）。
    /// </summary>
    public static class DataUsage
    {
        /// <summary>差し込み項目を使っている部品（全ページ共通の部品、ページの順）。</summary>
        public static List<DataUsageLocation> FindField(ReportDefinition report, FieldDefinition field)
        {
            var result = new List<DataUsageLocation>();
            if (report.FindField(field.Name) != field) return result;
            ForEachElement(report, (page, element) =>
            {
                int count = CountInElement(report, element, page < 0, field.Name);
                if (count > 0) result.Add(new DataUsageLocation(page, element, count));
            });
            return result;
        }

        /// <summary>一覧をくり返している表（ページの表だけ。全ページ共通の部品の表では行をくり返せない）。</summary>
        public static List<DataUsageLocation> FindList(ReportDefinition report, ListDefinition list)
        {
            var result = new List<DataUsageLocation>();
            if (report.FindList(list.Name) != list) return result;
            ForEachElement(report, (page, element) =>
            {
                if (page >= 0 && element is TableElement table && table.HasDetail && table.DetailSource == list.Name)
                    result.Add(new DataUsageLocation(page, element, 1));
            });
            return result;
        }

        /// <summary>差し込み項目を用紙で使っている数（{名前} と {名前:書式} の数の合計）。</summary>
        public static int CountField(ReportDefinition report, FieldDefinition field) => FindField(report, field).Sum(x => x.Count);

        /// <summary>一覧をくり返している表の数。</summary>
        public static int CountList(ReportDefinition report, ListDefinition list) => FindList(report, list).Count;

        /// <summary>すべての差し込み項目と一覧の使用数。</summary>
        public static Dictionary<object, int> CountAll(ReportDefinition report)
        {
            var counts = new Dictionary<object, int>();
            foreach (var field in report.Fields) counts[field] = CountField(report, field);
            foreach (var list in report.Lists) counts[list] = CountList(report, list);
            return counts;
        }

        private static void ForEachElement(ReportDefinition report, System.Action<int, ReportElement> action)
        {
            foreach (var element in report.Common.Elements) action(-1, element);
            for (int p = 0; p < report.Pages.Count; p++)
            {
                foreach (var element in report.Pages[p].Elements) action(p, element);
            }
        }

        private static int CountInElement(ReportDefinition report, ReportElement element, bool common, string name)
        {
            switch (element)
            {
                case TextElement text:
                    return CountInText(text.Text, name, null);
                case TableElement table:
                    {
                        int count = table.Rows.Sum(row => row.Cells.Sum(cell => CountInText(cell.Text, name, null)));
                        // 全ページ共通の部品の表では、くり返し行は使われない
                        if (table.HasDetail && !common)
                        {
                            var list = report.FindList(table.DetailSource);
                            count += table.DetailRow.Cells.Sum(cell => CountInText(cell.Text, name, list));
                        }
                        return count;
                    }
                default:
                    return 0;
            }
        }

        private static int CountInText(string text, string name, ListDefinition? list)
        {
            if (text.IndexOf(name, System.StringComparison.Ordinal) < 0) return 0;
            // くり返し行では、同じ名前の一覧の列が先に使われる
            if (list?.FindColumn(name) != null) return 0;
            return TextTemplate.Parse(text).Count(s => s.IsField && s.Text == name);
        }
    }
}
