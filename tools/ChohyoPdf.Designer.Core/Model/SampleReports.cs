using System.Collections.Generic;
using System.Globalization;

namespace ChohyoPdf.Designer.Model
{
    /// <summary>新規作成で選べる見本のデザイン。</summary>
    public static class SampleReports
    {
        /// <summary>白紙（A4 縦）。</summary>
        public static ReportDefinition CreateBlank() => new ReportDefinition { Name = "文書" };

        /// <summary>
        /// 複数ページの文書の見本。表紙・本文・名簿でページごとにレイアウトが違い、
        /// 名簿のページは参加者の人数に応じて増える（見出しは増えたページにも、「以上」は最後のページだけに描く）。
        /// 上の文書名と下のページ番号は全ページ共通の部品で、表紙には描かない。
        /// </summary>
        public static ReportDefinition CreateDocument()
        {
            var r = new ReportDefinition { Name = "活動報告書" };
            r.Fonts.Add(new FontDefinition { Name = "Yu Gothic Regular", BoldName = "Yu Gothic Bold" });
            r.Fields.Add(new FieldDefinition { Name = "作成日", Kind = FieldKind.Date, Format = "yyyy年M月d日", SampleValue = "2026/10/21" });
            r.Fields.Add(new FieldDefinition { Name = "作成者", Kind = FieldKind.Text, SampleValue = "地域交流委員会" });

            var list = new ListDefinition { Name = "参加者" };
            list.Columns.Add(new FieldDefinition { Name = "No", Kind = FieldKind.Number, Format = "0" });
            list.Columns.Add(new FieldDefinition { Name = "氏名", Kind = FieldKind.Text });
            list.Columns.Add(new FieldDefinition { Name = "所属", Kind = FieldKind.Text });
            string[] family = { "佐藤", "鈴木", "高橋", "田中", "伊藤", "渡辺", "山本", "中村" };
            string[] given = { "翔", "葵", "蓮", "陽菜" };
            string[] groups = { "総務部", "営業部", "開発部", "広報部", "製造部" };
            for (int i = 0; i < 32; i++)
            {
                list.SampleRows.Add(new List<string>
                {
                    (i + 1).ToString(CultureInfo.InvariantCulture), family[i % family.Length] + " " + given[i / family.Length % given.Length], groups[i % groups.Length],
                });
            }
            r.Lists.Add(list);

            // 全ページ共通の部品（表紙には描かない）
            r.Common.Elements.Add(new TextElement
            {
                Name = "ヘッダー", Text = "活動報告書", X = 20, Y = 12, Width = 170, Height = 6, FontSize = 8, Color = "#606060",
                Alignment = TextAlignment.Right, Visibility = PageVisibility.ExceptFirstPage,
            });
            r.Common.Elements.Add(new LineElement
            {
                Name = "ヘッダーの線", X1 = 20, Y1 = 19, X2 = 190, Y2 = 19, StrokeColor = "#A0A0A0", StrokeWidth = 0.5, Visibility = PageVisibility.ExceptFirstPage,
            });
            r.Common.Elements.Add(new TextElement
            {
                Name = "ページ番号", Text = "{ページ番号} / {総ページ数}", X = 20, Y = 282, Width = 170, Height = 6,
                FontSize = 8, Color = "#606060", Alignment = TextAlignment.Center, Visibility = PageVisibility.ExceptFirstPage,
            });

            // 1 ページ目: 表紙
            var cover = r.Pages[0];
            cover.Name = "表紙";
            cover.Elements.Add(new RectangleElement { Name = "上の帯", X = 0, Y = 0, Width = 210, Height = 10, StrokeColor = null, FillColor = "#1F4E79" });
            cover.Elements.Add(new TextElement
            {
                Name = "表題", Text = "活動報告書", X = 20, Y = 95, Width = 170, Height = 20,
                FontSize = 32, Bold = true, Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle,
            });
            cover.Elements.Add(new TextElement
            {
                Name = "副題", Text = "2026 年度 上期", X = 20, Y = 118, Width = 170, Height = 10,
                FontSize = 14, Color = "#404040", Alignment = TextAlignment.Center,
            });
            cover.Elements.Add(new LineElement { Name = "飾りの線", X1 = 60, Y1 = 132, X2 = 150, Y2 = 132, StrokeColor = "#1F4E79", StrokeWidth = 1 });
            cover.Elements.Add(new TextElement
            {
                Name = "作成日と作成者", Text = "{作成日}\n{作成者}", X = 20, Y = 232, Width = 170, Height = 18,
                FontSize = 12, Alignment = TextAlignment.Center, LineSpacing = 1.6,
            });

            // 2 ページ目: 本文
            var body = new ReportPage { Name = "本文" };
            r.Pages.Add(body);
            body.Elements.Add(Heading("見出し 1", "1. 活動の概要", 28));
            body.Elements.Add(new TextElement
            {
                Name = "本文 1", X = 20, Y = 40, Width = 170, Height = 36, LineSpacing = 1.7, Alignment = TextAlignment.Justify,
                Text = "本報告書は、2026 年度上期に行った地域交流活動の内容と成果をまとめたものです。"
                    + "説明会を 4 回開き、延べ 128 人が参加しました。参加者へのアンケートでは、86% の方から回答を得ました。"
                    + "次の期は、オンラインでの参加の受付を始め、より多くの方に参加していただけるようにします。",
            });
            body.Elements.Add(Heading("見出し 2", "2. 主な成果", 84));
            var results = TableElement.Create(3, 4, 0, 8);
            results.Name = "成果の表";
            results.X = 20;
            results.Y = 96;
            results.Columns[0].Width = 90;
            results.Columns[1].Width = 40;
            results.Columns[2].Width = 40;
            results.Columns[1].Alignment = TextAlignment.Right;
            results.Columns[2].Alignment = TextAlignment.Right;
            string[,] cells =
            {
                { "項目", "目標", "実績" },
                { "説明会の開催", "3 回", "4 回" },
                { "参加者数（延べ）", "100 人", "128 人" },
                { "アンケートの回答率", "80%", "86%" },
            };
            for (int row = 0; row < 4; row++)
            {
                for (int c = 0; c < 3; c++) results.Rows[row].Cells[c].Text = cells[row, c];
            }
            for (int c = 0; c < 3; c++)
            {
                results.Rows[0].Cells[c].Bold = true;
                results.Rows[0].Cells[c].Alignment = TextAlignment.Center;
                results.Rows[0].Cells[c].BackgroundColor = "#DCE6F2";
            }
            body.Elements.Add(results);
            body.Elements.Add(new TextElement
            {
                Name = "本文 2", X = 20, Y = 134, Width = 170, Height = 20, LineSpacing = 1.7, Alignment = TextAlignment.Justify,
                Text = "参加者の名簿は次のページにあります。名簿は、参加者の人数に応じてページが増えます。",
            });

            // 3 ページ目: 名簿（人数が多ければ、このページが増える）
            var roster = new ReportPage { Name = "名簿" };
            r.Pages.Add(roster);
            roster.Elements.Add(Heading("見出し 3", "3. 参加者名簿", 28));
            var table = TableEditing.CreateDetailTable(list, 170, 30);
            table.Name = "参加者の表";
            table.X = 20;
            table.Y = 40;
            table.Columns[0].Width = 15;
            table.Columns[1].Width = 75;
            table.Columns[2].Width = 80;
            table.Columns[0].Alignment = TextAlignment.Center;
            table.AlternateRowColor = "#F5F8FC";
            roster.Elements.Add(table);
            roster.Elements.Add(new TextElement
            {
                Name = "以上", Text = "以上", X = 150, Y = 260, Width = 40, Height = 6, Alignment = TextAlignment.Right,
                Visibility = PageVisibility.LastPage,
            });
            return r;
        }

        /// <summary>
        /// 案内状の見本（1 ページの手紙）。宛名・日時・会場を差し込むので、同じ文面で宛名だけを変えた案内状を何通も作れる。
        /// </summary>
        public static ReportDefinition CreateLetter()
        {
            var r = new ReportDefinition { Name = "案内状" };
            r.Fonts.Add(new FontDefinition { Name = "Yu Gothic Regular", BoldName = "Yu Gothic Bold" });
            r.Fields.Add(new FieldDefinition { Name = "氏名", Kind = FieldKind.Text, SampleValue = "山田 太郎" });
            r.Fields.Add(new FieldDefinition { Name = "発行日", Kind = FieldKind.Date, Format = "yyyy年M月d日", SampleValue = "2026/10/23" });
            r.Fields.Add(new FieldDefinition { Name = "日時", Kind = FieldKind.Text, SampleValue = "2026年11月14日（土）13:30～16:00" });
            r.Fields.Add(new FieldDefinition { Name = "会場", Kind = FieldKind.Text, SampleValue = "市民会館 2 階 大会議室" });

            var page = r.Pages[0];
            page.Name = "案内状";
            page.Elements.Add(new TextElement { Name = "発行日", Text = "{発行日}", X = 120, Y = 25, Width = 70, Height = 7, Alignment = TextAlignment.Right });
            page.Elements.Add(new TextElement { Name = "宛名", Text = "{氏名} 様", X = 20, Y = 38, Width = 100, Height = 9, FontSize = 14 });
            page.Elements.Add(new LineElement { Name = "宛名の線", X1 = 20, Y1 = 48, X2 = 110, Y2 = 48, StrokeColor = "#404040", StrokeWidth = 0.5 });
            page.Elements.Add(new TextElement
            {
                Name = "差出人", Text = "地域交流委員会\n委員長　佐藤 葵", X = 120, Y = 56, Width = 70, Height = 14, Alignment = TextAlignment.Right, LineSpacing = 1.5,
            });
            page.Elements.Add(new TextElement
            {
                Name = "表題", Text = "地域交流会のご案内", X = 20, Y = 82, Width = 170, Height = 12, FontSize = 18, Bold = true,
                Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle,
            });
            page.Elements.Add(new TextElement
            {
                Name = "本文", X = 20, Y = 104, Width = 170, Height = 36, LineSpacing = 1.8, Alignment = TextAlignment.Justify,
                Text = "拝啓　時下ますますご清栄のこととお喜び申し上げます。\n"
                    + "このたび、地域の皆さまの交流を深めることを目的に、下記のとおり交流会を開きます。"
                    + "ご多用中とは存じますが、ぜひご参加くださいますよう、ご案内申し上げます。",
            });
            page.Elements.Add(new TextElement { Name = "敬具", Text = "敬具", X = 150, Y = 142, Width = 40, Height = 7, Alignment = TextAlignment.Right });
            page.Elements.Add(new TextElement { Name = "記", Text = "記", X = 20, Y = 156, Width = 170, Height = 8, Alignment = TextAlignment.Center });
            var details = TableElement.Create(2, 3, 0, 9);
            details.Name = "日時と会場";
            details.X = 30;
            details.Y = 170;
            details.Columns[0].Width = 30;
            details.Columns[1].Width = 120;
            details.BorderColor = null;
            string[,] cells = { { "日時", "{日時}" }, { "会場", "{会場}" }, { "会費", "無料" } };
            for (int row = 0; row < 3; row++)
            {
                for (int c = 0; c < 2; c++) details.Rows[row].Cells[c].Text = cells[row, c];
                details.Rows[row].Cells[0].Bold = true;
            }
            page.Elements.Add(details);
            page.Elements.Add(new TextElement { Name = "以上", Text = "以上", X = 150, Y = 205, Width = 40, Height = 7, Alignment = TextAlignment.Right });
            return r;
        }

        private static TextElement Heading(string name, string text, double y)
            => new TextElement { Name = name, Text = text, X = 20, Y = y, Width = 170, Height = 9, FontSize = 14, Bold = true, Color = "#1F4E79" };

        /// <summary>請求書の見本。差し込み、品目の一覧の表（件数に応じてページが増える）、セル結合、最後のページだけの合計欄を含む。</summary>
        public static ReportDefinition CreateInvoice()
        {
            var r = new ReportDefinition { Name = "請求書" };
            var page = r.Pages[0].Elements;
            r.Fonts.Add(new FontDefinition { Name = "Yu Gothic Regular", BoldName = "Yu Gothic Bold" });

            r.Fields.Add(new FieldDefinition { Name = "請求書番号", Kind = FieldKind.Text, SampleValue = "INV-2026-0123" });
            r.Fields.Add(new FieldDefinition { Name = "発行日", Kind = FieldKind.Date, Format = "yyyy年M月d日", SampleValue = "2026/10/20" });
            r.Fields.Add(new FieldDefinition { Name = "顧客名", Kind = FieldKind.Text, SampleValue = "株式会社サンプル商会" });
            r.Fields.Add(new FieldDefinition { Name = "件名", Kind = FieldKind.Text, SampleValue = "事務用品一式" });
            r.Fields.Add(new FieldDefinition { Name = "小計", Kind = FieldKind.Number, Format = "#,0", SampleValue = "0" });
            r.Fields.Add(new FieldDefinition { Name = "消費税", Kind = FieldKind.Number, Format = "#,0", SampleValue = "0" });
            r.Fields.Add(new FieldDefinition { Name = "合計", Kind = FieldKind.Number, Format = "#,0", SampleValue = "0" });

            var list = new ListDefinition { Name = "明細" };
            list.Columns.Add(new FieldDefinition { Name = "No", Kind = FieldKind.Number, Format = "0" });
            list.Columns.Add(new FieldDefinition { Name = "品名", Kind = FieldKind.Text });
            list.Columns.Add(new FieldDefinition { Name = "数量", Kind = FieldKind.Number, Format = "#,0" });
            list.Columns.Add(new FieldDefinition { Name = "単価", Kind = FieldKind.Number, Format = "#,0" });
            list.Columns.Add(new FieldDefinition { Name = "金額", Kind = FieldKind.Number, Format = "#,0" });
            string[] items =
            {
                "コピー用紙 A4 500枚", "ボールペン 黒 10本入", "クリアファイル 20枚", "ホッチキス", "ホッチキス針 1000本",
                "付箋 75mm", "修正テープ", "蛍光ペン 5色", "ファイルボックス", "電卓", "ガムテープ", "はさみ",
                "セロハンテープ", "ノート B5", "封筒 長形3号 100枚", "ゴム印", "朱肉", "名刺ケース",
            };
            decimal subtotal = 0;
            for (int i = 0; i < items.Length; i++)
            {
                int qty = i % 4 + 1;
                int price = 180 + i * 75;
                subtotal += qty * price;
                list.SampleRows.Add(new List<string>
                {
                    (i + 1).ToString(CultureInfo.InvariantCulture), items[i], qty.ToString(CultureInfo.InvariantCulture),
                    price.ToString(CultureInfo.InvariantCulture), (qty * price).ToString(CultureInfo.InvariantCulture),
                });
            }
            r.Lists.Add(list);
            decimal tax = decimal.Floor(subtotal * 0.1m);
            r.FindField("小計")!.SampleValue = subtotal.ToString(CultureInfo.InvariantCulture);
            r.FindField("消費税")!.SampleValue = tax.ToString(CultureInfo.InvariantCulture);
            r.FindField("合計")!.SampleValue = (subtotal + tax).ToString(CultureInfo.InvariantCulture);

            page.Add(new TextElement
            {
                Name = "タイトル", Text = "請　求　書", X = 20, Y = 15, Width = 170, Height = 12,
                FontSize = 20, Bold = true, Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle,
            });
            page.Add(new TextElement
            {
                Name = "番号と日付", Text = "No. {請求書番号}\n発行日 {発行日}", X = 130, Y = 30, Width = 60, Height = 10,
                FontSize = 9, Alignment = TextAlignment.Right,
            });
            page.Add(new TextElement
            {
                Name = "宛名", Text = "{顧客名}　御中", X = 20, Y = 42, Width = 95, Height = 9,
                FontSize = 14, VerticalAlignment = VerticalAlignment.Bottom, ShrinkToFit = true, Wrap = false,
                Visibility = PageVisibility.FirstPage,
            });
            page.Add(new LineElement { Name = "宛名の下線", X1 = 20, Y1 = 52, X2 = 115, Y2 = 52, StrokeWidth = 0.8, Visibility = PageVisibility.FirstPage });
            page.Add(new TextElement
            {
                Name = "件名", Text = "件名：{件名}\n下記のとおりご請求申し上げます。", X = 20, Y = 55, Width = 95, Height = 10,
                FontSize = 9, Visibility = PageVisibility.FirstPage,
            });
            page.Add(new TextElement
            {
                Name = "発行元", Text = "株式会社ちょうひょう商事\n〒100-0001 東京都千代田区千代田1-1\nTEL 03-0000-0000\n登録番号 T1234567890123",
                X = 117, Y = 44, Width = 56, Height = 20, FontSize = 8.5, LineSpacing = 1.4,
            });
            page.Add(new RectangleElement
            {
                Name = "社印", X = 174, Y = 44, Width = 16, Height = 16, StrokeColor = "#C00000", StrokeWidth = 1, CornerRadius = 1,
                Visibility = PageVisibility.FirstPage,
            });
            page.Add(new TextElement
            {
                Name = "社印の文字", Text = "ちょう\nひょう", X = 172, Y = 44, Width = 16, Height = 16, FontSize = 8, Color = "#C00000",
                Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle, Visibility = PageVisibility.FirstPage,
            });

            var amount = TableElement.Create(2, 1, 0, 11);
            amount.Name = "請求金額";
            amount.X = 20;
            amount.Y = 70;
            amount.Visibility = PageVisibility.FirstPage;
            amount.Columns[0].Width = 40;
            amount.Columns[1].Width = 55;
            amount.FontSize = 14;
            amount.BorderWidth = 1;
            amount.Rows[0].Cells[0].Text = "ご請求金額";
            amount.Rows[0].Cells[0].BackgroundColor = "#DCE6F2";
            amount.Rows[0].Cells[0].Alignment = TextAlignment.Center;
            amount.Rows[0].Cells[0].FontSize = 11;
            amount.Rows[0].Cells[1].Text = "¥{合計}-";
            amount.Rows[0].Cells[1].Bold = true;
            amount.Rows[0].Cells[1].Alignment = TextAlignment.Right;
            page.Add(amount);

            var detail = TableElement.Create(5, 1, 0, 7);
            detail.Name = "明細表";
            detail.X = 20;
            detail.Y = 88;
            double[] widths = { 12, 83, 20, 25, 30 };
            string[] heads = { "No.", "品名", "数量", "単価", "金額" };
            string[] cells = { "{No}", "{品名}", "{数量}", "{単価}", "{金額}" };
            for (int c = 0; c < 5; c++)
            {
                detail.Columns[c].Width = widths[c];
                detail.Columns[c].Alignment = c == 1 ? TextAlignment.Left : c == 0 ? TextAlignment.Center : TextAlignment.Right;
                var head = detail.Rows[0].Cells[c];
                head.Text = heads[c];
                head.Bold = true;
                head.Alignment = TextAlignment.Center;
                head.BackgroundColor = "#DCE6F2";
                detail.DetailRow.Cells[c].Text = cells[c];
            }
            detail.DetailSource = "明細";
            detail.DetailRowCount = 15;
            detail.AlternateRowColor = "#F5F8FC";
            page.Add(detail);

            var totals = TableElement.Create(2, 3, 0, 7);
            totals.Name = "合計欄";
            totals.X = 130;
            totals.Y = 207;
            totals.Visibility = PageVisibility.LastPage;
            totals.Columns[0].Width = 30;
            totals.Columns[1].Width = 30;
            totals.Columns[1].Alignment = TextAlignment.Right;
            string[] labels = { "小計", "消費税（10%）", "合計" };
            string[] values = { "{小計}", "{消費税}", "{合計}" };
            for (int i = 0; i < 3; i++)
            {
                totals.Rows[i].Cells[0].Text = labels[i];
                totals.Rows[i].Cells[0].BackgroundColor = "#DCE6F2";
                totals.Rows[i].Cells[0].Alignment = TextAlignment.Center;
                totals.Rows[i].Cells[1].Text = values[i];
            }
            totals.Rows[2].Cells[1].Bold = true;
            page.Add(totals);

            // セル結合の例: 左の列を縦に 2 行結合する
            var bank = TableElement.Create(2, 2, 0, 7);
            bank.Name = "振込先";
            bank.X = 20;
            bank.Y = 207;
            bank.Visibility = PageVisibility.LastPage;
            bank.Columns[0].Width = 20;
            bank.Columns[1].Width = 75;
            bank.Rows[0].Cells[0].Text = "お振込先";
            bank.Rows[0].Cells[0].RowSpan = 2;
            bank.Rows[0].Cells[0].BackgroundColor = "#DCE6F2";
            bank.Rows[0].Cells[0].Alignment = TextAlignment.Center;
            bank.Rows[0].Cells[1].Text = "ちょうひょう銀行 本店営業部 普通 1234567";
            bank.Rows[1].Cells[1].Text = "カ）チョウヒョウショウジ";
            page.Add(bank);

            page.Add(new TextElement
            {
                Name = "備考", Text = "備考：お支払期限は発行日の翌月末日です。振込手数料は貴社にてご負担ください。",
                X = 20, Y = 232, Width = 170, Height = 14, FontSize = 8.5, BorderColor = "#808080", BorderWidth = 0.5, Padding = 2,
                Visibility = PageVisibility.LastPage,
            });
            page.Add(new TextElement
            {
                Name = "ページ番号", Text = "{ページ番号} / {総ページ数}", X = 20, Y = 280, Width = 170, Height = 6,
                FontSize = 8, Color = "#606060", Alignment = TextAlignment.Center,
            });
            return r;
        }
    }
}
