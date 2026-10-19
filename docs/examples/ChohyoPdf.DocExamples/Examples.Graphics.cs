using System;
using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void ImageBasic()
        {
            // #example ImageBasic
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            var photo = PdfImage.FromFile("photo.jpg");          // JPEG
            var logo = PdfImage.FromFile("logo.png");            // 透過 PNG

            s.AddParagraph($"photo.jpg：{photo.PixelWidth} × {photo.PixelHeight} px").SetFontSize(9);
            s.AddImage(photo, width: Unit.Mm(80));               // 幅を指定すると高さは縦横比から決まる
            s.AddParagraph("中央・右寄せ").SetFontSize(9).SetSpaceBefore(8);
            s.AddImage(logo, width: Unit.Mm(25)).Alignment = HorizontalAlignment.Center;
            s.AddImage(logo, height: Unit.Mm(12)).Alignment = HorizontalAlignment.Right;

            doc.Save("image-basic.pdf");
            // #end
        }

        public static void ImageTransparency()
        {
            // #example ImageTransparency
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(150), Unit.Mm(60))).Graphics;
            var logo = PdfImage.FromFile("logo.png");
            var photo = PdfImage.FromFile("photo.jpg");

            // 市松模様の上に置くと、透明な部分と影の半透明が分かる
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 30; x++)
                    if ((x + y) % 2 == 0) g.FillRectangle(PdfColor.FromGray(0.85), x * Unit.Mm(5), y * Unit.Mm(5), Unit.Mm(5), Unit.Mm(5));

            g.DrawImage(logo, Unit.Mm(10), Unit.Mm(10), Unit.Mm(40), Unit.Mm(40));
            g.DrawImage(photo, Unit.Mm(60), Unit.Mm(10), Unit.Mm(64), Unit.Mm(40));
            g.DrawImage(logo, Unit.Mm(105), Unit.Mm(25), Unit.Mm(30), Unit.Mm(30));   // 写真に重ねる

            pdf.Save("image-transparency.pdf");
            // #end
        }

        public static void ImageFit()
        {
            // #example ImageFit
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(62))).Graphics;
            var photo = PdfImage.FromFile("photo.jpg");
            var pen = PdfPen.Dashed(PdfColor.Red, 0.5, 2, 2);
            var label = new TextStyle(font, 8);
            double w = Unit.Mm(45), h = Unit.Mm(40), top = Unit.Mm(14);

            g.DrawText("DrawImage（枠いっぱいに伸ばす）", Unit.Mm(8), Unit.Mm(6), label);
            g.DrawImage(photo, Unit.Mm(8), top, w, h);
            g.DrawRectangle(pen, Unit.Mm(8), top, w, h);

            g.DrawText("DrawImageFit（縦横比を保って中央に）", Unit.Mm(62), Unit.Mm(6), label);
            g.DrawImageFit(photo, Unit.Mm(62), top, w, h);
            g.DrawRectangle(pen, Unit.Mm(62), top, w, h);

            g.DrawText("DrawImageFit（縦長の枠）", Unit.Mm(116), Unit.Mm(6), label);
            g.DrawImageFit(photo, Unit.Mm(126), top, Unit.Mm(25), h);
            g.DrawRectangle(pen, Unit.Mm(126), top, Unit.Mm(25), h);

            pdf.Save("image-fit.pdf");
            // #end
        }

        public static void Shapes()
        {
            // #example Shapes
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(85))).Graphics;
            var pen = new PdfPen(PdfColor.FromHex("#1F4E79"), 1.5);
            var fill = PdfColor.FromHex("#BDD7EE");
            var label = new TextStyle(font, 8);
            double c = Unit.Mm(40);                              // 1 マスの大きさ

            void Label(int col, int row, string text) =>
                g.DrawText(text, Unit.Mm(10) + col * c + Unit.Mm(15), Unit.Mm(5) + row * c + Unit.Mm(33), label, TextAlignment.Center);

            double X(int col) => Unit.Mm(10) + col * c;
            double Y(int row) => Unit.Mm(5) + row * c;

            g.DrawLine(pen, X(0), Y(0) + Unit.Mm(25), X(0) + Unit.Mm(30), Y(0) + Unit.Mm(5));
            g.DrawLine(PdfPen.Dashed(PdfColor.Red, 1, 4, 2), X(0), Y(0) + Unit.Mm(28), X(0) + Unit.Mm(30), Y(0) + Unit.Mm(28));
            Label(0, 0, "DrawLine / 破線");
            g.DrawRectangle(pen, fill, X(1), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22));
            Label(1, 0, "DrawRectangle");
            g.DrawRoundedRectangle(pen, fill, X(2), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22), Unit.Mm(5));
            Label(2, 0, "DrawRoundedRectangle");
            g.DrawEllipse(pen, fill, X(3), Y(0) + Unit.Mm(5), Unit.Mm(30), Unit.Mm(22));
            Label(3, 0, "DrawEllipse");

            g.DrawPie(pen, fill, X(0) + Unit.Mm(15), Y(1) + Unit.Mm(15), Unit.Mm(13), -90, 270);   // 真上から時計回りに 270 度
            Label(0, 1, "DrawPie");
            g.DrawPolygon(pen, fill, X(1) + Unit.Mm(15), Y(1) + Unit.Mm(2), X(1) + Unit.Mm(30), Y(1) + Unit.Mm(28), X(1), Y(1) + Unit.Mm(28));
            Label(1, 1, "DrawPolygon");
            var path = new PdfPath()
                .MoveTo(X(2), Y(1) + Unit.Mm(25))
                .CurveTo(X(2) + Unit.Mm(5), Y(1) - Unit.Mm(5), X(2) + Unit.Mm(25), Y(1) + Unit.Mm(40), X(2) + Unit.Mm(30), Y(1) + Unit.Mm(5));
            g.DrawPath(path, pen, null);
            Label(2, 1, "PdfPath（ベジェ曲線）");
            g.DrawPolyline(PdfPen.Dotted(PdfColor.FromHex("#C55A11"), 2), X(3), Y(1) + Unit.Mm(25), X(3) + Unit.Mm(10), Y(1) + Unit.Mm(5),
                X(3) + Unit.Mm(20), Y(1) + Unit.Mm(20), X(3) + Unit.Mm(30), Y(1) + Unit.Mm(3));
            Label(3, 1, "DrawPolyline / 点線");

            pdf.Save("shapes.pdf");
            // #end
        }

        public static void Colors()
        {
            // #example Colors
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(62))).Graphics;
            var label = new TextStyle(font, 8);

            // 半透明（WithAlpha）の円を重ねる
            g.DrawCircle(null, PdfColor.FromRgb(255, 0, 0).WithAlpha(0.5), Unit.Mm(25), Unit.Mm(22), Unit.Mm(14));
            g.DrawCircle(null, PdfColor.FromRgb(0, 176, 80).WithAlpha(0.5), Unit.Mm(40), Unit.Mm(22), Unit.Mm(14));
            g.DrawCircle(null, PdfColor.FromRgb(0, 112, 192).WithAlpha(0.5), Unit.Mm(32.5), Unit.Mm(35), Unit.Mm(14));
            g.DrawText("WithAlpha(0.5)", Unit.Mm(32.5), Unit.Mm(53), label, TextAlignment.Center);

            // グレー・RGB・CMYK
            for (int i = 0; i <= 10; i++)
                g.FillRectangle(PdfColor.FromGray(i / 10.0), Unit.Mm(70 + i * 8), Unit.Mm(8), Unit.Mm(8), Unit.Mm(10));
            g.DrawText("FromGray(0.0) … FromGray(1.0)", Unit.Mm(70), Unit.Mm(19.5), label);

            string[] hex = { "#C00000", "#ED7D31", "#FFC000", "#70AD47", "#4472C4", "#7030A0" };
            for (int i = 0; i < hex.Length; i++)
                g.FillRectangle(PdfColor.FromHex(hex[i]), Unit.Mm(70 + i * 14), Unit.Mm(27), Unit.Mm(12), Unit.Mm(8));
            g.DrawText("FromHex(\"#C00000\") など", Unit.Mm(70), Unit.Mm(36.5), label);

            g.FillRectangle(PdfColor.FromCmyk(1, 0, 0, 0), Unit.Mm(70), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
            g.FillRectangle(PdfColor.FromCmyk(0, 1, 0, 0), Unit.Mm(84), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
            g.FillRectangle(PdfColor.FromCmyk(0, 0, 1, 0), Unit.Mm(98), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
            g.FillRectangle(PdfColor.FromCmyk(0, 0, 0, 1), Unit.Mm(112), Unit.Mm(44), Unit.Mm(12), Unit.Mm(8));
            g.DrawText("FromCmyk（印刷向け）", Unit.Mm(70), Unit.Mm(53.5), label);

            pdf.Save("colors.pdf");
            // #end
        }

        public static void Transforms()
        {
            // #example Transforms
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(70))).Graphics;
            var style = new TextStyle(font, 10);

            // 回転：点 (cx, cy) を中心に、時計回りを正とする角度で回す
            double cx = Unit.Mm(35), cy = Unit.Mm(35);
            for (int angle = 0; angle < 360; angle += 45)
            {
                g.SaveState();
                g.RotateAt(angle, cx, cy);
                g.DrawText($"{angle}°", cx + Unit.Mm(6), cy - 5, style);
                g.RestoreState();
            }
            g.DrawCircle(null, PdfColor.Red, cx, cy, 2);

            // 拡大縮小：Translate で原点を移してから Scale
            g.SaveState();
            g.Translate(Unit.Mm(75), Unit.Mm(12));
            g.Scale(2, 1);                                       // 横に 2 倍
            g.DrawText("横 2 倍", 0, 0, style);
            g.RestoreState();

            // クリップ：円の中だけに画像を描く
            var photo = PdfImage.FromFile("photo.jpg");
            g.SaveState();
            g.SetClip(new PdfPath().AddEllipse(Unit.Mm(120), Unit.Mm(10), Unit.Mm(45), Unit.Mm(45)));
            g.DrawImage(photo, Unit.Mm(106.5), Unit.Mm(10), Unit.Mm(72), Unit.Mm(45));
            g.RestoreState();
            g.DrawText("SetClip", Unit.Mm(142.5), Unit.Mm(58), style, TextAlignment.Center);

            pdf.Save("transforms.pdf");
            // #end
        }

        public static void Charts()
        {
            // #example Charts
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.AddParagraph("月別売上と構成比").SetFontSize(14).SetBold().SetSpaceAfter(6);

            string[] months = { "4月", "5月", "6月", "7月", "8月", "9月" };
            double[] sales = { 120, 145, 98, 170, 160, 190 };
            var labelStyle = new TextStyle(font, 8);

            // 棒グラフ（AddDrawing の (0, 0) は図の左上）
            s.AddDrawing(Unit.Mm(55), (g, r) =>
            {
                double baseY = r.Height - 14, maxH = r.Height - 30, step = r.Width / months.Length;
                g.DrawLine(new PdfPen(PdfColor.Gray, 0.5), 0, baseY, r.Width, baseY);
                for (int i = 0; i < months.Length; i++)
                {
                    double h = sales[i] / 200 * maxH, x = i * step + step * 0.25;
                    g.FillRectangle(PdfColor.FromHex("#5B9BD5"), x, baseY - h, step * 0.5, h);
                    g.DrawText(sales[i].ToString(), x + step * 0.25, baseY - h - 11, labelStyle, TextAlignment.Center);
                    g.DrawText(months[i], x + step * 0.25, baseY + 3, labelStyle, TextAlignment.Center);
                }
            });

            // 円グラフ（DrawPie の角度は 0 度が右、時計回りが正）
            string[] names = { "製品A", "製品B", "製品C", "その他" };
            double[] share = { 45, 25, 20, 10 };
            string[] colors = { "#5B9BD5", "#ED7D31", "#A5A5A5", "#FFC000" };
            s.AddDrawing(Unit.Mm(55), (g, r) =>
            {
                double cx = Unit.Mm(30), cy = r.Height / 2, radius = Unit.Mm(24), start = -90;
                for (int i = 0; i < share.Length; i++)
                {
                    double sweep = share[i] / 100 * 360;
                    g.DrawPie(new PdfPen(PdfColor.White, 1), PdfColor.FromHex(colors[i]), cx, cy, radius, start, sweep);
                    double mid = (start + sweep / 2) * Math.PI / 180;
                    g.DrawText($"{share[i]}%", cx + Math.Cos(mid) * radius * 0.65, cy + Math.Sin(mid) * radius * 0.65 - 4,
                        labelStyle, TextAlignment.Center);
                    g.FillRectangle(PdfColor.FromHex(colors[i]), Unit.Mm(65), Unit.Mm(12 + i * 8), Unit.Mm(4), Unit.Mm(4));
                    g.DrawText(names[i], Unit.Mm(71), Unit.Mm(11.5 + i * 8), labelStyle);
                    start += sweep;
                }
            });

            doc.Save("charts.pdf");
            // #end
        }
    }
}
