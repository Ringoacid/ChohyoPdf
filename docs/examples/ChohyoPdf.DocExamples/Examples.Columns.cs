using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void Columns()
        {
            // #example Columns
            var doc = new Document(font, 10.5);
            doc.Styles["見出し"] = new ParagraphStyle(
                new TextStyle { FontSize = 12, Bold = true },
                new ParagraphFormat { OutlineLevel = 1, SpaceBefore = 8, SpaceAfter = 3, KeepWithNext = true });

            // 1 段の表題。段数を変えるときはセクションを分ける（セクションは新しいページから始まる）
            var title = doc.AddSection();
            title.AddParagraph("社内報 2026 年秋号").SetFontSize(22).SetBold().SetAlignment(TextAlignment.Center).SetSpaceAfter(6);
            title.AddParagraph("総務部").SetAlignment(TextAlignment.Center);

            // 2 段組みの本文。段の幅は等分で、段の間に境界線を引ける
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 2;
            s.PageSetup.ColumnSpacing = Unit.Mm(10);                                  // 段間（既定は 8 mm）
            s.PageSetup.ColumnSeparator = new PdfPen(PdfColor.Gray, 0.5);             // 境界線（null なら引かない）
            s.Header.AddParagraph("社内報 2026 年秋号").SetFontSize(8).SetColor(PdfColor.Gray).SetAlignment(TextAlignment.Right);
            s.Footer.AddParagraph().SetFontSize(9).SetAlignment(TextAlignment.Center).AddPageNumber();   // ヘッダー/フッターは用紙の幅のまま

            for (int i = 1; i <= 4; i++)
            {
                s.AddParagraph("記事 " + i, "見出し");
                s.AddParagraph(Filler(5)).SetAlignment(TextAlignment.Justify).SetSpaceAfter(4);
            }

            // 表やリストも段の幅で組まれる
            var table = s.AddTable();
            table.AddColumns(2, 1);
            table.AddHeaderRow("行事", "日付");
            table.AddRow("運動会", "10/12");
            table.AddRow("文化祭", "11/03");
            table.AddRow("忘年会", "12/20");

            s.AddParagraph("お知らせ", "見出し");
            var list = s.AddBulletList();
            list.AddItem("駐車場の利用は事前に申請してください。");
            list.AddItem("会議室の予約は総務部まで。");

            s.AddColumnBreak();      // ここから次の段（最後の段なら次のページ）
            s.AddParagraph("編集後記", "見出し");
            s.AddParagraph(Filler(4)).SetAlignment(TextAlignment.Justify);

            doc.Save("columns.pdf");
            // #end
        }

        public static void ColumnAreas()
        {
            // #example ColumnAreas
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 3;
            s.PageSetup.ColumnSpacing = Unit.Mm(6);

            // PageContext.ContentArea は段組みでもすべての段をまとめた領域のまま。段ごとの領域は ColumnAreas に入る
            s.PageBackground = (g, ctx) =>
            {
                var frame = PdfPen.Dashed(PdfColor.Gray, 0.5);
                foreach (var area in ctx.ColumnAreas)
                    g.DrawRectangle(frame, area.X, area.Y, area.Width, area.Height);
            };

            s.AddParagraph($"段の幅は {Unit.ToMm(s.PageSetup.ColumnWidth):0.0} mm").SetBold();
            s.AddParagraph(Filler(14)).SetAlignment(TextAlignment.Justify);
            s.AddColumnBreak();
            s.AddParagraph("3 つ目の段へ").SetBold();
            s.AddParagraph(Filler(10)).SetAlignment(TextAlignment.Justify);

            doc.Save("column-areas.pdf");
            // #end
        }
    }
}
