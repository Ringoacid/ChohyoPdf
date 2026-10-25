using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void TableAutoWidth()
        {
            // #example TableAutoWidth
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // 内容に合わせる列（AddAutoColumn）だけの表は、表の幅も内容に合わせて狭くなる
            s.AddParagraph("列の幅を内容に合わせる").SetBold().SetSpaceAfter(4);
            var t = s.AddTable();
            t.AddAutoColumns(3);                                   // AddAutoColumn() を 3 回呼ぶのと同じ
            t.Columns[2].Alignment = TextAlignment.Right;
            t.HeaderBackground = PdfColor.FromHex("#DDEBF7");
            t.AddHeaderRow("コード", "品名", "金額");
            t.AddRow("A-01", "ノートパソコン", "198,000");
            t.AddRow("A-02", "液晶モニター", "84,000");
            t.AddRow("B-10", "USB-C ドック", "13,200");

            // 内容が表の幅に収まらないときは、最大の幅に比例して縮める。
            // 折り返せない単位（欧文の単語、禁則処理で分けられない文字）より狭くはならない
            s.AddParagraph("収まらないときは縮めて折り返す").SetBold().SetSpaceBefore(14).SetSpaceAfter(4);
            var wide = s.AddTable();
            wide.AddAutoColumns(3);
            wide.AddHeaderRow("No.", "概要", "備考");
            wide.AddRow("1", "日本語の長い説明文は、表の幅に収まらなくなるとセルの中で折り返されます。短い列は、折り返せる限界の幅を下回りません。", "ok");
            wide.AddRow("2", "見積書の備考欄のように、行によって文字数が大きく違う列にも使えます。", "要確認：納期と数量");

            // 固定幅や比率の列と混ぜられる。固定幅を除いた残りから、内容に合わせる列が先に幅を取り、残りを比率の列で分ける
            s.AddParagraph("固定幅・比率の列との組み合わせ").SetBold().SetSpaceBefore(14).SetSpaceAfter(4);
            var mixed = s.AddTable();
            mixed.AddColumn(Unit.Mm(12)).Alignment = TextAlignment.Center;    // 固定幅
            mixed.AddAutoColumn();                                             // 内容に合わせる
            mixed.AddRelativeColumn(1);                                        // 残りを比率で分ける
            mixed.AddHeaderRow("No.", "区分", "メモ");
            mixed.AddRow("1", "見積", "残りの幅いっぱいに広がる列。内容に合わせる列は、この列の最小の幅を残して幅を取る。");
            mixed.AddRow("2", "請求", "短いメモ");

            // 表全体を内容に合わせるなら AutoFitColumns（固定幅を指定した列は、そのまま）
            s.AddParagraph("表全体を内容に合わせる").SetBold().SetSpaceBefore(14).SetSpaceAfter(4);
            var all = s.AddTable();
            all.AutoFitColumns = true;
            all.Alignment = HorizontalAlignment.Center;
            all.AddRelativeColumn();                                           // 比率は使われず、内容に合わせる
            all.AddRelativeColumn();
            all.AddHeaderRow("項目", "値");
            all.AddRow("用紙", "A4 縦");
            all.AddRow("余白", "20 mm");

            doc.Save("table-auto-width.pdf");
            // #end
        }
    }
}
