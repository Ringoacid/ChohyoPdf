using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void TableBasic()
        {
            // #example TableBasic
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            var t = s.AddTable();
            t.AddColumns(1, 4, 1.2, 2);                          // 列幅の比率
            t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
            t.HeaderStyle = new TextStyle { Bold = true };
            t.AddHeaderRow("No.", "品名", "数量", "金額");
            t.AddRow("1", "ノートパソコン 14 インチ", "2", "396,000");
            t.AddRow("2", "液晶モニター 27 インチ（高さ調整スタンド付き、ケーブル類一式を含む）", "4", "168,000");
            t.AddRow("3", "USB-C ドック", "4", "52,800");

            doc.Save("table-basic.pdf");
            // #end
        }

        public static void TableColumns()
        {
            // #example TableColumns
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            var t = s.AddTable();
            t.AddColumn(Unit.Mm(15)).Alignment = TextAlignment.Center;     // 固定幅・中央揃え
            t.AddRelativeColumn(3);                                         // 残りを 3 : 1 で分ける
            var amount = t.AddRelativeColumn(1);
            amount.Alignment = TextAlignment.Right;                         // 金額は右揃え
            amount.Background = PdfColor.FromHex("#FFF2CC");                // 列の背景色
            t.AddHeaderRow("コード", "内容", "金額");
            t.AddRow("A01", "固定幅 15 mm の列", "1,200");
            t.AddRow("A02", "比率 3 の列", "34,500");
            t.AddRow("A03", "比率 1 の列は右揃え", "678,900");

            s.AddSpacer(12);

            // 列がすべて固定幅なら、表の幅は列幅の合計になる。Alignment で位置を決める
            var small = s.AddTable();
            small.Alignment = HorizontalAlignment.Center;
            small.AddColumn(Unit.Mm(30));
            small.AddColumn(Unit.Mm(30)).Alignment = TextAlignment.Right;
            small.AddHeaderRow("項目", "値");
            small.AddRow("中央に配置", "60 mm");

            doc.Save("table-columns.pdf");
            // #end
        }

        public static void TableMerge()
        {
            // #example TableMerge
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            var t = s.AddTable();
            t.AddColumns(1.2, 1.5, 1, 1, 1, 1);
            t.VerticalAlignment = VerticalAlignment.Middle;
            t.HeaderBackground = PdfColor.FromHex("#E2EFDA");

            // 見出し 2 行：結合されて隠れるセルには null を渡す
            var h1 = t.AddHeaderRow("地域", "店舗", "上期", null!, "下期", null!);
            h1[0].Merge(1, 2);                                   // 縦に 2 行
            h1[1].Merge(1, 2);
            h1[2].Merge(2);                                      // 横に 2 列
            h1[4].Merge(2);
            t.AddHeaderRow(null!, null!, "売上", "利益", "売上", "利益");
            foreach (var row in t.Rows) foreach (var c in row.Cells) c.Alignment = TextAlignment.Center;

            var r1 = t.AddRow("東日本", "札幌", "120", "18", "135", "21");
            r1[0].Merge(1, 2);
            t.AddRow(null!, "仙台", "98", "12", "101", "15");
            var r3 = t.AddRow("西日本", "大阪", "210", "35", "198", "30");
            r3[0].Merge(1, 3);
            t.AddRow(null!, "広島", "75", "9", "80", "11");
            t.AddRow(null!, "福岡", "130", "20", "142", "24");
            var total = t.AddRow("合計", null!, "633", "94", "656", "101");
            total[0].Merge(2).SetAlignment(TextAlignment.Center);
            total.Background = PdfColor.FromGray(0.92);
            total.Style = new TextStyle { Bold = true };

            doc.Save("table-merge.pdf");
            // #end
        }

        public static void TableBorders()
        {
            // #example TableBorders
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // 色付きの罫線と縞模様
            var t = s.AddTable();
            t.AddColumns(2, 1, 1);
            t.Borders = new BorderLine(PdfColor.FromHex("#8EA9DB"), 0.5);
            t.AlternateRowBackground = PdfColor.FromHex("#EEF3FB");
            t.HeaderBackground = PdfColor.FromHex("#4472C4");
            t.HeaderStyle = new TextStyle { Color = PdfColor.White, Bold = true };
            t.CellPadding = new Thickness(6, 4);
            t.AddHeaderRow("項目", "4 月", "5 月");
            for (int i = 1; i <= 5; i++) t.AddRow("項目 " + i, (i * 120).ToString(), (i * 135).ToString());
            var sum = t.AddRow("合計", "1,800", "2,025");
            foreach (var c in sum.Cells) c.Borders.Top = new BorderLine(PdfColor.FromHex("#1F3864"), 1.5);

            s.AddSpacer(16);

            // 横線だけの表（縦の罫線なし）
            var plain = s.AddTable();
            plain.AddColumns(1, 3);
            plain.Borders = null;                                // 既定の罫線をなくす
            var head = plain.AddHeaderRow("日付", "内容");
            foreach (var c in head.Cells) c.Borders.Bottom = BorderLine.Thick;
            foreach (var (date, text) in new[] { ("10/1", "キックオフ"), ("10/8", "設計レビュー"), ("10/15", "結合テスト") })
            {
                var row = plain.AddRow(date, text);
                foreach (var c in row.Cells) c.Borders.Bottom = BorderLine.Dashed(PdfColor.Gray);
            }

            doc.Save("table-borders.pdf");
            // #end
        }

        public static void TableLong()
        {
            // #example TableLong
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber().AddText(" / ").AddPageCount();

            var t = s.AddTable();
            t.AddColumns(1, 4, 2);
            t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
            t.AlternateRowBackground = PdfColor.FromGray(0.96);
            t.AddHeaderRow("No.", "品名", "金額");                // 改ページ後も各ページの先頭に表示
            for (int i = 1; i <= 70; i++)
                t.AddRow(i.ToString(), "商品 " + i, (i * 1280).ToString("#,0") + " 円").Cells[2].Alignment = TextAlignment.Right;

            doc.Save("table-long.pdf");
            // #end
        }

        public static void TableRichCells()
        {
            // #example TableRichCells
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            var logo = PdfImage.FromFile("logo.png");

            var t = s.AddTable();
            t.AddColumns(1, 2.2, 1.6);
            t.AddHeaderRow("画像", "段落を複数", "表の入れ子");

            var row = t.AddRow();
            row.VerticalAlignment = VerticalAlignment.Middle;
            row[0].AddImage(logo, width: Unit.Mm(22)).Alignment = HorizontalAlignment.Center;

            row[1].AddParagraph("ChohyoPdf").SetBold().SetFontSize(13);
            row[1].AddParagraph("セルには段落・画像・図・表を入れられます。").SetSpaceAfter(4);
            row[1].AddParagraph().AddText("文字の").AddText("装飾", new TextStyle { Color = PdfColor.Red, Bold = true }).AddText("もそのまま使えます。");

            var inner = row[2].AddTable();
            inner.AddColumns(1, 1);
            inner.Borders = new BorderLine(PdfColor.Gray, 0.3);
            inner.AddRow("A", "1");
            inner.AddRow("B", "2");

            var r2 = t.AddRow("上", "中央", "下");
            r2.Height = Unit.Mm(15);                             // 最小の高さ
            r2[0].VerticalAlignment = VerticalAlignment.Top;
            r2[1].VerticalAlignment = VerticalAlignment.Middle;
            r2[2].VerticalAlignment = VerticalAlignment.Bottom;
            r2[1].Background = PdfColor.FromHex("#FCE4D6");
            r2[2].Padding = new Thickness(12, 2);                // セルごとの内側の余白

            doc.Save("table-rich-cells.pdf");
            // #end
        }

        public static void TableRowSplit()
        {
            // #example TableRowSplit
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.AddDrawing(s.PageSetup.ContentHeight - 110, (g, r) =>
                g.FillRectangle(PdfColor.FromGray(0.93), 0, 0, r.Width, r.Height));

            var t = s.AddTable();
            t.AddColumns(1, 5);
            t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
            t.AddHeaderRow("No.", "内容");
            t.AddRow("1", "短い行");
            // 既定では、収まらない行は行ごと次のページへ送る。true にするとページ末尾で分割する
            t.AddRow("2", Filler(12)).AllowBreakAcrossPages = true;

            doc.Save("table-row-split.pdf");
            // #end
        }
    }
}
