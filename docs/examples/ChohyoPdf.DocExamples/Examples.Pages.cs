using System.Text;
using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        /// <summary>サンプル用の長い文章。</summary>
        private static string Filler(int repeat)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < repeat; i++)
                sb.Append("本文が長くなると、ページの終わりで自動的に次のページへ送られます。");
            return sb.ToString();
        }

        public static void PageSetupSample()
        {
            // #example PageSetupSample
            var doc = new Document(font, 12);
            var s = doc.AddSection(new PageSetup
            {
                Size = PageSize.A5.Landscape,                    // A5 横
                Margins = Thickness.FromMm(15, 12, 15, 12),      // 左, 上, 右, 下
            });

            // 本文の領域（ContentArea）を破線で示す
            s.PageBackground = (g, ctx) =>
            {
                var area = ctx.ContentArea;
                g.DrawRectangle(PdfPen.Dashed(PdfColor.Gray, 0.5, 3, 2), area.X, area.Y, area.Width, area.Height);
            };

            s.AddParagraph("A5 横・余白は左右 15 mm、上下 12 mm").SetBold();
            s.AddParagraph($"本文の幅 {Unit.ToMm(s.PageSetup.ContentWidth):0} mm × 高さ {Unit.ToMm(s.PageSetup.ContentHeight):0} mm");

            doc.Save("page-setup.pdf");
            // #end
        }

        public static void HeaderAndFooter()
        {
            // #example HeaderAndFooter
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.PageSetup.Margins = Thickness.FromMm(20, 25, 20, 22);   // ヘッダー/フッターの分だけ余白を広げる

            s.Header.AddParagraph("社内報告書　2026 年 10 月").SetFontSize(8).SetColor(PdfColor.Gray)
                .SetAlignment(TextAlignment.Right);
            s.Header.AddHorizontalLine(new PdfPen(PdfColor.Gray, 0.5));

            s.Footer.AddParagraph().SetAlignment(TextAlignment.Center).SetFontSize(9)
                .AddPageNumber().AddText(" / ").AddPageCount().AddText(" ページ");

            s.AddParagraph("ヘッダーとフッター").SetFontSize(16).SetBold().SetSpaceAfter(6);
            for (int i = 0; i < 12; i++)
                s.AddParagraph(Filler(8)).SetSpaceAfter(6);

            doc.Save("header-footer.pdf");
            // #end
        }

        public static void PageBreaks()
        {
            // #example PageBreaks
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            s.AddParagraph("第 1 章").SetFontSize(18).SetBold();
            s.AddParagraph("1 ページ目の本文です。");
            s.AddPageBreak();                                         // ここで改ページ

            s.AddParagraph("第 2 章").SetFontSize(18).SetBold();
            s.AddParagraph("2 ページ目の本文です。");

            s.AddParagraph("第 3 章").SetFontSize(18).SetBold().SetPageBreakBefore();   // 段落の前で改ページ
            s.AddParagraph("3 ページ目の本文です。");

            doc.Save("page-breaks.pdf");
            // #end
        }

        public static void KeepWithNextSample()
        {
            // #example KeepWithNextSample
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // ページの下のほうまで本文が詰まっている状態を、灰色の箱で表す
            s.AddDrawing(s.PageSetup.ContentHeight - 70, (g, r) =>
            {
                g.FillRectangle(PdfColor.FromGray(0.93), 0, 0, r.Width, r.Height);
                g.DrawText("（ここまで本文があるとします）", r.Width / 2, r.Height / 2, new TextStyle(font, 14), TextAlignment.Center);
            });

            // 見出しだけがページの最後に残らないよう、次の段落と同じページに置く
            s.AddParagraph("2. 次の章の見出し").SetFontSize(14).SetBold().SetKeepWithNext();
            // 段落を途中で分割しない
            s.AddParagraph(Filler(5)).SetKeepTogether();

            doc.Save("keep-with-next.pdf");
            // #end
        }

        public static void Sections()
        {
            // #example Sections
            var doc = new Document(font, 10.5);

            // 表紙（フッターなし）
            var cover = doc.AddSection();
            cover.AddSpacer(Unit.Mm(90));
            cover.AddParagraph("年次報告書").SetFontSize(28).SetBold().SetAlignment(TextAlignment.Center);
            cover.AddParagraph("2026 年度").SetFontSize(14).SetAlignment(TextAlignment.Center);

            // 本文（セクションごとのページ番号）
            var body = doc.AddSection();
            body.Footer.AddParagraph().SetAlignment(TextAlignment.Center)
                .AddText("- ").AddField(FieldType.SectionPageNumber).AddText(" -");
            body.AddParagraph("本文").SetFontSize(16).SetBold();
            body.AddParagraph(Filler(10));

            // 横向きのページ（幅の広い表など）
            var wide = doc.AddSection(new PageSetup { Size = PageSize.A4.Landscape });
            wide.AddParagraph("横向きのセクション").SetFontSize(16).SetBold();
            wide.AddParagraph("セクションごとに用紙の大きさ・向き・余白、ヘッダーとフッターを変えられます。");

            doc.Save("sections.pdf");
            // #end
        }

        public static void Watermark()
        {
            // #example Watermark
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // 本文の下に描く：ページ上端の帯
            s.PageBackground = (g, ctx) =>
            {
                g.FillRectangle(PdfColor.FromHex("#1F4E79"), 0, 0, ctx.PageSize.Width, Unit.Mm(10));
                g.DrawText("ACME 株式会社", Unit.Mm(20), Unit.Mm(3), new TextStyle(font, 9) { Color = PdfColor.White });
            };

            // 本文の上に描く：半透明の透かし
            s.PageForeground = (g, ctx) =>
            {
                double cx = ctx.PageSize.Width / 2, cy = ctx.PageSize.Height / 2;
                g.SaveState();
                g.RotateAt(-30, cx, cy);                         // 反時計回りに 30 度
                g.DrawText("社外秘", cx, cy - 45, new TextStyle(font, 90) { Color = PdfColor.Red.WithAlpha(0.2) },
                    TextAlignment.Center);
                g.RestoreState();
            };

            s.AddParagraph("透かしとページの装飾").SetFontSize(16).SetBold().SetSpaceAfter(6);
            for (int i = 0; i < 6; i++) s.AddParagraph(Filler(6)).SetSpaceAfter(6);

            doc.Save("watermark.pdf");
            // #end
        }
    }
}
