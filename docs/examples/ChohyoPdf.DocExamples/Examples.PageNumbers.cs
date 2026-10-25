using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void OddEvenHeaders()
        {
            // #example OddEvenHeaders
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.PageSetup.Margins = Thickness.FromMm(20, 25, 20, 22);

            s.DifferentFirstPage = true;     // セクションの 1 ページ目だけ別のヘッダー/フッター
            s.DifferentOddEven = true;       // 偶数ページは EvenHeader / EvenFooter を使う

            // 1 ページ目: 表題のみ
            s.FirstPageHeader.AddParagraph("社内報告書").SetFontSize(14).SetBold().SetAlignment(TextAlignment.Center);

            // 奇数ページ（ヘッダー/フッターは右寄せ）
            s.Header.AddParagraph("2026 年度 年次報告書").SetFontSize(8).SetColor(PdfColor.Gray)
                .SetAlignment(TextAlignment.Right);
            s.Footer.AddParagraph().SetFontSize(9).SetAlignment(TextAlignment.Right).AddPageNumber();

            // 偶数ページ（左寄せ）
            s.EvenHeader.AddParagraph("株式会社サンプル").SetFontSize(8).SetColor(PdfColor.Gray)
                .SetAlignment(TextAlignment.Left);
            s.EvenFooter.AddParagraph().SetFontSize(9).SetAlignment(TextAlignment.Left).AddPageNumber();

            s.AddParagraph("両面印刷向けのヘッダーとフッター").SetFontSize(16).SetBold().SetSpaceAfter(6);
            for (int i = 0; i < 30; i++)
                s.AddParagraph(Filler(8)).SetSpaceAfter(6);

            doc.Save("odd-even-headers.pdf");
            // #end
        }

        public static void PageNumberFormats()
        {
            // #example PageNumberFormats
            var doc = new Document(font, 10.5);

            // 前付け（目次）: ローマ数字の小文字
            var front = doc.AddSection();
            front.PageNumberFormat = PageNumberFormat.LowerRoman;
            front.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber();
            front.AddParagraph("目次").SetFontSize(16).SetBold();
            front.AddParagraph("（前付けのページ番号は i, ii, … で表示します）");

            // 本文: 1 から数え直し、アラビア数字。総ページ数は物理的なページ数（4）のまま
            var body = doc.AddSection();
            body.PageNumberStart = 1;
            body.PageNumberFormat = PageNumberFormat.Arabic;
            body.Footer.AddParagraph().SetAlignment(TextAlignment.Center)
                .AddPageNumber().AddText(" / 全 ").AddPageCount().AddText(" ページ");
            body.AddParagraph("第 1 章").SetFontSize(16).SetBold();
            body.AddParagraph("本文は 1 ページ目から数えます。");
            body.AddPageBreak();
            body.AddParagraph("第 2 章").SetFontSize(16).SetBold();
            body.AddParagraph("開始番号を指定しなかったセクションは、前のセクションの続きから数えます。");

            // 付録: 書式だけ変える（番号は続き）。フィールドごとの書式指定や、通しページ番号も使える
            var appendix = doc.AddSection();
            appendix.PageNumberFormat = PageNumberFormat.CircledNumber;
            appendix.Footer.AddParagraph().SetAlignment(TextAlignment.Center)
                .AddPageNumber().AddText("　（通しページ ").AddField(FieldType.PhysicalPageNumber, PageNumberFormat.Kanji).AddText("）");
            appendix.AddParagraph("付録").SetFontSize(16).SetBold();
            appendix.AddParagraph("丸数字、漢数字、全角数字、英字などの書式を選べます。");

            doc.Save("page-number-formats.pdf");
            // #end
        }
    }
}
