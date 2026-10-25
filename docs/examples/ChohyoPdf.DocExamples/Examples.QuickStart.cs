using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        // README に載せるサンプル。フォントの読み込みから保存までを、この中だけで完結させる
        public static void QuickStart()
        {
            // #example QuickStart
            var font = PdfFont.FromSystem("Yu Gothic Regular");      // 游ゴシック
            font.BoldVariant = PdfFont.FromSystem("Yu Gothic Bold");

            var doc = new Document(font, 10.5);
            var section = doc.AddSection();                          // A4 縦・余白 20 mm

            section.AddParagraph("御見積書").SetFontSize(18).SetBold().SetAlignment(TextAlignment.Center);
            section.AddParagraph("株式会社サンプル 御中").SetFontSize(12).SetSpaceBefore(12);
            section.AddParagraph("下記のとおり御見積り申し上げます。").SetSpaceAfter(8);

            var table = section.AddTable();
            table.AddColumns(1, 5, 1.2, 2);                          // 列幅の比率
            table.Columns[2].Alignment = TextAlignment.Right;
            table.Columns[3].Alignment = TextAlignment.Right;
            table.HeaderBackground = PdfColor.FromHex("#DDEBF7");
            table.AddHeaderRow("No.", "品名", "数量", "金額");     // 次のページにも繰り返す
            table.AddRow("1", "ノートパソコン 14 インチ", "2", "396,000");
            table.AddRow("2", "液晶モニター 27 インチ（高さ調整スタンド付き、ケーブル類一式を含む）", "4", "168,000");
            table.AddRow("3", "USB-C ドック", "4", "52,800");

            doc.Save("quick-start.pdf");
            // #end
        }
    }
}
