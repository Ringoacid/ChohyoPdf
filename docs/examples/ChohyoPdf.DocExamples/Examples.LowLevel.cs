using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void Coordinates()
        {
            // #example Coordinates
            var pdf = new PdfDocument();
            var page = pdf.AddPage(new PageSize(Unit.Mm(120), Unit.Mm(80)));
            var g = page.Graphics;
            var small = new TextStyle(font, 7) { Color = PdfColor.Gray };

            // 10 mm ごとの方眼
            for (int mm = 0; mm <= 120; mm += 10)
            {
                g.DrawLine(new PdfPen(PdfColor.LightGray, 0.3), Unit.Mm(mm), 0, Unit.Mm(mm), page.Height);
                if (mm > 0) g.DrawText(mm.ToString(), Unit.Mm(mm) + 1, 1, small);
            }
            for (int mm = 10; mm <= 80; mm += 10)
            {
                g.DrawLine(new PdfPen(PdfColor.LightGray, 0.3), 0, Unit.Mm(mm), page.Width, Unit.Mm(mm));
                g.DrawText(mm.ToString(), 1, Unit.Mm(mm) + 1, small);
            }

            var red = new TextStyle(font, 9) { Color = PdfColor.Red };
            g.DrawText("(0, 0)", Unit.Mm(2), Unit.Mm(3), red);
            g.DrawText("x →", Unit.Mm(12), Unit.Mm(6), red);
            g.DrawText("y ↓", Unit.Mm(2), Unit.Mm(13), red);

            // (40 mm, 30 mm) に幅 50 mm・高さ 25 mm の矩形
            g.DrawRectangle(new PdfPen(PdfColor.Blue, 1), PdfColor.Blue.WithAlpha(0.1), Unit.Mm(40), Unit.Mm(30), Unit.Mm(50), Unit.Mm(25));
            g.DrawCircle(null, PdfColor.Blue, Unit.Mm(40), Unit.Mm(30), 2);
            g.DrawText("(Unit.Mm(40), Unit.Mm(30))", Unit.Mm(42), Unit.Mm(32), new TextStyle(font, 8) { Color = PdfColor.Blue });

            pdf.Save("coordinates.pdf");
            // #end
        }

        public static void DrawTextSample()
        {
            // #example DrawTextSample
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(160), Unit.Mm(45))).Graphics;
            var style = new TextStyle(font, 12);
            var mark = new PdfPen(PdfColor.Red, 0.5);
            double y = Unit.Mm(10);

            void Cross(double x) { g.DrawLine(mark, x - 4, y, x + 4, y); g.DrawLine(mark, x, y - 4, x, y + 4); }

            // y は文字の上端。揃えによって x の意味が変わる（赤い十字が (x, y)）
            Cross(Unit.Mm(10));
            g.DrawText("Left：x が左端", Unit.Mm(10), y, style);
            Cross(Unit.Mm(80));
            g.DrawText("Center：x が中央", Unit.Mm(80), y, style, TextAlignment.Center);
            Cross(Unit.Mm(150));
            g.DrawText("Right：x が右端", Unit.Mm(150), y, style, TextAlignment.Right);

            // 戻り値は描いた幅。続けて書くときに使う
            double width = g.DrawText("金額：", Unit.Mm(10), Unit.Mm(25), style);
            g.DrawText("12,345 円", Unit.Mm(10) + width, Unit.Mm(25), new TextStyle(font, 12) { Bold = true, Underline = true });

            pdf.Save("draw-text.pdf");
            // #end
        }

        public static void TextBoxes()
        {
            // #example TextBoxes
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(48))).Graphics;
            var style = new TextStyle(font, 10);
            var frame = new PdfPen(PdfColor.Gray, 0.5);
            string text = "DrawTextBox は矩形の中で折り返して描きます。";

            var aligns = new[] { VerticalAlignment.Top, VerticalAlignment.Middle, VerticalAlignment.Bottom };
            for (int i = 0; i < aligns.Length; i++)
            {
                var rect = new PdfRect(Unit.Mm(8 + i * 54), Unit.Mm(10), Unit.Mm(48), Unit.Mm(30));
                g.DrawRectangle(frame, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawText(aligns[i].ToString(), rect.X, rect.Y - 11, new TextStyle(font, 8) { Color = PdfColor.Gray });
                g.DrawTextBox(text, rect.Deflate(3, 3, 3, 3), style, new TextBoxOptions
                {
                    VerticalAlignment = aligns[i],
                    Alignment = TextAlignment.Center,
                });
            }

            pdf.Save("text-boxes.pdf");
            // #end
        }

        public static void ShrinkToFit()
        {
            // #example ShrinkToFit
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(30))).Graphics;
            var style = new TextStyle(font, 11);
            var frame = new PdfPen(PdfColor.Gray, 0.5);
            var note = new TextStyle(font, 7.5) { Color = PdfColor.Red };
            string text = "株式会社サンプルホールディングス 営業企画部";

            var options = new[]
            {
                new TextBoxOptions(),                                        // そのまま（入りきらない行は描かない）
                new TextBoxOptions { ShrinkToFit = true },                   // 縮小して全体を収める
                new TextBoxOptions { ShrinkToFit = true, Wrap = false },     // 折り返さず 1 行に縮小
            };
            for (int i = 0; i < options.Length; i++)
            {
                var rect = new PdfRect(Unit.Mm(8 + i * 54), Unit.Mm(8), Unit.Mm(48), Unit.Mm(10));
                g.DrawRectangle(frame, rect.X, rect.Y, rect.Width, rect.Height);
                options[i].VerticalAlignment = VerticalAlignment.Middle;
                TextBoxResult result = g.DrawTextBox(text, rect.Deflate(2, 1, 2, 1), style, options[i]);
                g.DrawText($"{result.FontSize:0.#}pt / {result.LinesDrawn}/{result.TotalLines} 行 / Overflowed={result.Overflowed}",
                    rect.X, rect.Bottom + 3, note);
            }

            pdf.Save("shrink-to-fit.pdf");
            // #end
        }

        public static void FormSample()
        {
            // #example FormSample
            var pdf = new PdfDocument();
            var page = pdf.AddPage(PageSize.A5.Landscape);
            var g = page.Graphics;
            var label = new TextStyle(font, 10);
            var value = new TextStyle(font, 12);
            var thin = new PdfPen(PdfColor.Black, 0.5);
            var gray = PdfColor.FromGray(0.92);

            g.DrawText("申 込 書", page.Width / 2, Unit.Mm(12), new TextStyle(font, 20) { Bold = true }, TextAlignment.Center);
            g.DrawText("2026 年 10 月 19 日", page.Width - Unit.Mm(15), Unit.Mm(24), label, TextAlignment.Right);

            string[] labels = { "氏名", "フリガナ", "住所", "電話番号" };
            string[] values = { "山田 太郎", "ヤマダ タロウ", "東京都千代田区千代田 1-1-1 サンプルマンション 1001 号室", "03-1234-5678" };
            double x = Unit.Mm(15), y = Unit.Mm(32), labelW = Unit.Mm(30), w = page.Width - Unit.Mm(30), h = Unit.Mm(12);
            for (int i = 0; i < labels.Length; i++)
            {
                double top = y + i * h;
                g.DrawRectangle(thin, gray, x, top, labelW, h);
                g.DrawRectangle(thin, x + labelW, top, w - labelW, h);
                // 項目名は均等割り付け、記入欄は収まらなければ縮小
                g.DrawTextBox(labels[i], new PdfRect(x + Unit.Mm(4), top, labelW - Unit.Mm(8), h), label,
                    new TextBoxOptions { Alignment = TextAlignment.Distribute, VerticalAlignment = VerticalAlignment.Middle });
                g.DrawTextBox(values[i], new PdfRect(x + labelW + Unit.Mm(3), top, w - labelW - Unit.Mm(6), h), value,
                    new TextBoxOptions { VerticalAlignment = VerticalAlignment.Middle, ShrinkToFit = true, Wrap = false });
            }

            // チェックボックス
            double cy = y + labels.Length * h + Unit.Mm(8);
            string[] plans = { "ライト", "スタンダード", "プレミアム" };
            for (int i = 0; i < plans.Length; i++)
            {
                double bx = x + i * Unit.Mm(40);
                g.DrawRectangle(thin, bx, cy, Unit.Mm(4), Unit.Mm(4));
                if (i == 1) g.DrawPolyline(new PdfPen(PdfColor.Red, 1.5), bx + 2, cy + 6, bx + 5, cy + 10, bx + 11, cy + 1);
                g.DrawText(plans[i], bx + Unit.Mm(6), cy - 1, label);
            }

            pdf.Save("form.pdf");
            // #end
        }

        public static void RenderThenDraw()
        {
            // #example RenderThenDraw
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.AddParagraph("稟議書").SetFontSize(20).SetBold().SetAlignment(TextAlignment.Center).SetSpaceAfter(10);
            s.AddParagraph("件名：新しい会計システムの導入について");
            s.AddParagraph(Filler(4));

            // 自動レイアウトの結果を PdfDocument として受け取り、座標指定で描き足す
            PdfDocument pdf = doc.Render();
            var g = pdf.Pages[0].Graphics;
            double cx = pdf.Pages[0].Width - Unit.Mm(35), cy = Unit.Mm(17);
            var red = PdfColor.FromHex("#D00000");
            g.DrawCircle(new PdfPen(red, 1.5), null, cx, cy, Unit.Mm(10));
            g.DrawLine(new PdfPen(red, 0.8), cx - Unit.Mm(9.5), cy - 2, cx + Unit.Mm(9.5), cy - 2);
            g.DrawText("承認", cx, cy - Unit.Mm(7), new TextStyle(font, 9) { Color = red }, TextAlignment.Center);
            g.DrawText("10.19", cx, cy + 1, new TextStyle(font, 8) { Color = red }, TextAlignment.Center);
            pdf.Save("render-then-draw.pdf");
            // #end
        }
    }
}
