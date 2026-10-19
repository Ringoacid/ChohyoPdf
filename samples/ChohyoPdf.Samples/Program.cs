using System;
using System.IO;
using ChohyoPdf;

namespace ChohyoPdf.Samples
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "output");
            Directory.CreateDirectory(outDir);

            // Windows なら游ゴシック、なければ他の日本語フォントを探す
            if (!PdfFont.TryFromSystem(out var gothic, "Yu Gothic Regular", "游ゴシック", "Meiryo", "MS Gothic", "Noto Sans JP", "IPAexGothic"))
            {
                Console.Error.WriteLine("日本語フォントが見つかりません。");
                return 1;
            }
            PdfFont.TryFromSystem(out var gothicBold, "Yu Gothic Bold", "Meiryo Bold");
            if (gothicBold != null) gothic!.BoldVariant = gothicBold;
            PdfFont.TryFromSystem(out var mincho, "Yu Mincho Regular", "游明朝", "MS Mincho", "Noto Serif JP", "IPAexMincho");

            Invoice.Create(gothic!, Path.Combine(outDir, "invoice.pdf"));
            Report.Create(gothic!, mincho ?? gothic!, Path.Combine(outDir, "report.pdf"));
            LowLevel.Create(gothic!, Path.Combine(outDir, "lowlevel.pdf"));

            Console.WriteLine("出力先: " + outDir);
            return 0;
        }
    }

    /// <summary>請求書（表・セル結合・右寄せ・ヘッダー/フッター）。</summary>
    internal static class Invoice
    {
        public static void Create(PdfFont font, string path)
        {
            var doc = new Document(font, 10);
            doc.Info.Title = "請求書";
            doc.Info.Author = "株式会社サンプル";
            doc.Info.CreationDate = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(9));
            doc.DefaultParagraphFormat.LineSpacing = 1.3;

            var section = doc.AddSection();
            section.PageSetup.Margins = Thickness.FromMm(18, 20, 18, 20);
            section.Footer.AddParagraph().SetAlignment(TextAlignment.Center).SetFontSize(8)
                .AddPageNumber().AddText(" / ").AddPageCount();

            section.AddParagraph("請 求 書").SetFontSize(22).SetBold().SetAlignment(TextAlignment.Center).SetSpaceAfter(12);

            // 宛先と発行元を 2 列の罫線なしの表で並べる
            var top = section.AddTable();
            top.Borders = null;
            top.AddColumns(1, 1);
            var r = top.AddRow();
            r[0].AddParagraph("株式会社 取引先 御中").SetFontSize(14).SetUnderline();
            r[0].AddParagraph("〒100-0001 東京都千代田区千代田1-1").SetSpaceBefore(4);
            r[0].AddParagraph("下記のとおりご請求申し上げます。").SetSpaceBefore(10);
            r[1].Alignment = TextAlignment.Right;
            r[1].AddParagraph("請求日: 2026年10月19日");
            r[1].AddParagraph("請求番号: INV-2026-0042");
            r[1].AddParagraph("株式会社サンプル").SetBold().SetSpaceBefore(8);
            r[1].AddParagraph("〒530-0001 大阪府大阪市北区梅田1-2-3\nTEL 06-0000-0000");
            r[1].AddDrawing(Unit.Mm(18), Unit.Mm(18), (g, rect) =>
            {
                // 角印のイメージ
                var red = PdfColor.FromRgb(200, 30, 30);
                g.DrawRectangle(new PdfPen(red, 1.5), 0, 0, rect.Width, rect.Height);
                g.DrawTextBox("サンプル\n之印", rect, new TextStyle(font, 9) { Color = red },
                    new TextBoxOptions { Alignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Middle });
            }).Alignment = HorizontalAlignment.Right;

            section.AddSpacer(10);

            var total = section.AddTable();
            total.AddColumn(Unit.Mm(40));
            total.AddColumn(Unit.Mm(60));
            var tr = total.AddRow("ご請求金額");                 // 金額は明細を集計してから入れる
            tr.Height = 28;
            tr.VerticalAlignment = VerticalAlignment.Middle;
            tr[0].SetBackground(PdfColor.FromHex("#E8EEF7")).SetAlignment(TextAlignment.Distribute);
            tr[0].Padding = new Thickness(8, 2, 8, 2);
            tr[1].Style = new TextStyle { FontSize = 16, Bold = true };
            tr[1].Alignment = TextAlignment.Right;
            total.Borders = new BorderLine(PdfColor.Black, 1);
            total.SpaceAfter = 12;

            // 明細
            var items = section.AddTable();
            items.AddColumn(Unit.Mm(12)).Alignment = TextAlignment.Center;
            items.AddRelativeColumn(1);
            items.AddColumn(Unit.Mm(18)).Alignment = TextAlignment.Right;
            items.AddColumn(Unit.Mm(14)).Alignment = TextAlignment.Center;
            items.AddColumn(Unit.Mm(26)).Alignment = TextAlignment.Right;
            items.AddColumn(Unit.Mm(28)).Alignment = TextAlignment.Right;
            items.HeaderBackground = PdfColor.FromHex("#2F4F7F");
            items.HeaderStyle = new TextStyle { Color = PdfColor.White, Bold = true };
            items.AlternateRowBackground = PdfColor.FromHex("#F4F6FA");
            var h = items.AddHeaderRow("No.", "品名・摘要", "数量", "単位", "単価", "金額");
            foreach (var cell in h.Cells) cell.Alignment = TextAlignment.Center;

            var products = new[]
            {
                "システム保守サービス（10月分）",
                "クラウドサーバー利用料（スタンダードプラン・月額）",
                "帳票レイアウト設計および PDF 出力機能の追加開発。禁則処理・自動改ページ・表の見出し繰り返しを含む。",
                "ノートパソコン",
                "USB-C ドック",
                "現地作業費（交通費込み）",
            };
            long sum = 0;
            for (int i = 0; i < 36; i++)
            {
                string name = products[i % products.Length];
                int qty = (i % 5) + 1;
                long price = 1200 + (i * 7919 % 50000);
                long amount = qty * price;
                sum += amount;
                items.AddRow((i + 1).ToString(), name, qty.ToString(), i % 3 == 0 ? "式" : "個", price.ToString("#,0"), amount.ToString("#,0"));
            }
            long tax = sum / 10;
            var label = new[] { "小計", "消費税 10%", "合計" };
            var values = new[] { sum, tax, sum + tax };
            tr[1].SetText("¥" + (sum + tax).ToString("#,0") + " -");
            for (int i = 0; i < 3; i++)
            {
                var row = items.AddRow();
                row.KeepWithNext = i < 2;
                row.Background = PdfColor.White;
                row[0].Merge(4).Borders.Left = BorderLine.None;
                row[0].Borders.Bottom = BorderLine.None;
                row[0].Borders.Top = i == 0 ? null : BorderLine.None;
                row[4].SetText(label[i]).SetAlignment(TextAlignment.Center).SetBackground(PdfColor.FromHex("#E8EEF7"));
                row[5].SetText(values[i].ToString("#,0"), i == 2 ? new TextStyle { Bold = true } : null);
            }

            section.AddParagraph("備考").SetBold().SetSpaceBefore(14).SetKeepWithNext();
            var note = section.AddParagraph(
                "お振込手数料は貴社にてご負担ください。お支払期限は2026年11月30日（月）です。" +
                "なお、本請求書に関するお問い合わせは担当（営業部・山田）までご連絡ください。");
            note.SetBorder(new PdfPen(PdfColor.Gray, 0.5), new Thickness(6));

            doc.Save(path);
        }
    }

    /// <summary>長文レポート（見出し・禁則・両端揃え・画像・図・グラフ・結合セル）。</summary>
    internal static class Report
    {
        public static void Create(PdfFont gothic, PdfFont mincho, string path)
        {
            var doc = new Document(mincho, 10.5);
            doc.Info.Title = "ChohyoPdf 機能サンプル";
            doc.Info.CreationDate = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(9));
            doc.DefaultTextStyle.FallbackFonts = new[] { gothic };
            doc.DefaultParagraphFormat.Alignment = TextAlignment.Justify;
            doc.DefaultParagraphFormat.FirstLineIndent = 10.5;
            doc.DefaultParagraphFormat.SpaceAfter = 4;
            doc.Styles["見出し1"] = new ParagraphStyle(
                new TextStyle { Font = gothic, FontSize = 16, Bold = true, Color = PdfColor.FromHex("#1F3A5F") },
                new ParagraphFormat { SpaceBefore = 14, SpaceAfter = 8, KeepWithNext = true, FirstLineIndent = 0, Alignment = TextAlignment.Left, Border = null });
            doc.Styles["見出し2"] = new ParagraphStyle(
                new TextStyle { Font = gothic, FontSize = 12, Bold = true },
                new ParagraphFormat { SpaceBefore = 10, SpaceAfter = 4, KeepWithNext = true, FirstLineIndent = 0, Background = PdfColor.FromHex("#EEF2F7"), Padding = new Thickness(4, 2, 4, 2) });

            var section = doc.AddSection();
            section.Header.AddParagraph("ChohyoPdf 機能サンプル", new TextStyle { Font = gothic, FontSize = 8, Color = PdfColor.Gray })
                .SetAlignment(TextAlignment.Right);
            section.Header.ShowOnFirstPage = false;
            section.Footer.AddParagraph().SetAlignment(TextAlignment.Center).SetFont(gothic).SetFontSize(8)
                .AddText("- ").AddPageNumber().AddText(" -");
            section.PageBackground = (g, pc) =>
            {
                if (pc.PageNumber == 1) g.FillRectangle(PdfColor.FromHex("#1F3A5F"), 0, 0, pc.PageSize.Width, Unit.Mm(12));
            };

            section.AddParagraph("ChohyoPdf 機能サンプル", "見出し1").SetFontSize(22).SetAlignment(TextAlignment.Center).SetSpaceBefore(20);
            section.AddParagraph("MIT ライセンスの PDF 生成ライブラリ", new TextStyle { Font = gothic, FontSize = 11, Color = PdfColor.Gray })
                .SetAlignment(TextAlignment.Center).SetSpaceAfter(16).Format.FirstLineIndent = 0;

            section.AddParagraph("1. 日本語の組版", "見出し1");
            section.AddParagraph("1.1 禁則処理と両端揃え", "見出し2");
            string text = "吾輩は猫である。名前はまだ無い。どこで生れたかとんと見当がつかぬ。何でも薄暗いじめじめした所でニャーニャー泣いていた事だけは記憶している。" +
                "吾輩はここで始めて人間というものを見た。しかもあとで聞くとそれは書生という人間中で一番獰悪な種族であったそうだ。" +
                "この書生というのは時々我々を捕えて煮て食うという話である。（しかしその当時は何という考もなかったから別段恐しいとも思わなかった。）" +
                "ただ彼の掌に載せられてスーッと持ち上げられた時何だかフワフワした感じがあったばかりである。";
            for (int i = 0; i < 3; i++) section.AddParagraph(text);

            section.AddParagraph("1.2 文字の装飾と欧文の混在", "見出し2");
            var p = section.AddParagraph();
            p.AddText("段落の中で");
            p.AddText("太字", new TextStyle { Bold = true });
            p.AddText("、");
            p.AddText("斜体", new TextStyle { Italic = true });
            p.AddText("、");
            p.AddText("下線", new TextStyle { Underline = true });
            p.AddText("、");
            p.AddText("取り消し線", new TextStyle { Strikeout = true });
            p.AddText("、");
            p.AddText("色", new TextStyle { Color = PdfColor.FromRgb(200, 0, 0) });
            p.AddText("、");
            p.AddText("マーカー", new TextStyle { BackgroundColor = PdfColor.FromHex("#FFF59D") });
            p.AddText("、");
            p.AddText("大きな文字", new TextStyle { FontSize = 16 });
            p.AddText("を混在できます。English words are wrapped at word boundaries, e.g. internationalization and localization. ");
            p.AddText("サロゲートペアの文字（𠮷野家、𩸽）も扱えます。リンク: ");
            p.AddLink("https://example.com/", "https://example.com/");

            section.AddParagraph("2. 表", "見出し1");
            section.AddParagraph("セルの結合（横・縦）、背景色、罫線の種類、垂直方向の揃えを指定できます。見出し行はページをまたぐと繰り返されます。");
            var table = section.AddTable();
            table.Style = new TextStyle { Font = gothic, FontSize = 9 };
            table.AddColumns(1.2, 1, 1, 1, 1);
            table.HeaderBackground = PdfColor.FromHex("#DCE6F2");
            var h1 = table.AddHeaderRow("部門", "上期", null!, "下期", null!);
            h1[0].Merge(1, 2).SetAlignment(TextAlignment.Center, VerticalAlignment.Middle);
            h1[1].Merge(2).SetAlignment(TextAlignment.Center);
            h1[3].Merge(2).SetAlignment(TextAlignment.Center);
            var h2 = table.AddHeaderRow(null!, "計画", "実績", "計画", "実績");
            for (int i = 1; i < 5; i++) h2[i].Alignment = TextAlignment.Center;
            string[] depts = { "営業第一部", "営業第二部", "開発部", "管理部" };
            var rnd = new Random(42);
            foreach (var d in depts)
            {
                var row = table.AddRow(d);
                for (int i = 1; i < 5; i++)
                {
                    int v = rnd.Next(800, 2000);
                    row[i].SetText(v.ToString("#,0")).Alignment = TextAlignment.Right;
                    if (i % 2 == 0 && v > 1500) row[i].Background = PdfColor.FromHex("#E2F0D9");
                }
            }
            var note = table.AddRow("備考");
            note[1].Merge(4).SetText("実績は速報値です。下期は 3 月末時点の見込みを含みます。セル内の文章も自動で折り返されます。");
            note[1].Borders.Bottom = new BorderLine(PdfColor.Black, 1.5);
            note[0].Borders.Bottom = new BorderLine(PdfColor.Black, 1.5);

            section.AddParagraph("3. 図", "見出し1");
            section.AddParagraph("画像（PNG / JPEG）とベクター図形を配置できます。下はプログラムで生成した PNG 相当の画像と、図形描画 API で描いた棒グラフと円グラフです。");
            section.AddImage(CreateGradientImage(), Unit.Mm(60)).Alignment = HorizontalAlignment.Center;
            section.AddParagraph("図 1　プログラムで生成した画像（透明度付き）", new TextStyle { Font = gothic, FontSize = 9 })
                .SetAlignment(TextAlignment.Center).Format.FirstLineIndent = 0;

            double[] values = { 120, 180, 90, 210, 160 };
            string[] labels = { "4月", "5月", "6月", "7月", "8月" };
            section.AddDrawing(170, (g, rect) =>
            {
                var axis = new PdfPen(PdfColor.Black, 0.8);
                double left = 30, bottom = rect.Height - 20, chartW = 250, chartH = bottom - 10;
                g.DrawLine(axis, left, 10, left, bottom);
                g.DrawLine(axis, left, bottom, left + chartW, bottom);
                var small = new TextStyle(gothic, 8);
                for (int i = 0; i <= 4; i++)
                {
                    double yy = bottom - chartH * i / 4;
                    g.DrawLine(PdfPen.Dashed(PdfColor.LightGray, 0.5, 2, 2), left, yy, left + chartW, yy);
                    g.DrawText((i * 60).ToString(), left - 4, yy - 5, small, TextAlignment.Right);
                }
                double bw = chartW / values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    double bh = chartH * values[i] / 240;
                    g.FillRectangle(PdfColor.FromHex("#4472C4"), left + bw * i + bw * 0.2, bottom - bh, bw * 0.6, bh);
                    g.DrawText(labels[i], left + bw * i + bw / 2, bottom + 4, small, TextAlignment.Center);
                }
                double cx = 400, cy = rect.Height / 2, radius = 65, angle = -90, totalValue = 0;
                foreach (double v in values) totalValue += v;
                string[] colors = { "#4472C4", "#ED7D31", "#A5A5A5", "#FFC000", "#5B9BD5" };
                for (int i = 0; i < values.Length; i++)
                {
                    double sweep = 360 * values[i] / totalValue;
                    g.DrawPie(new PdfPen(PdfColor.White, 1), PdfColor.FromHex(colors[i]), cx, cy, radius, angle, sweep);
                    double mid = (angle + sweep / 2) * Math.PI / 180;
                    g.DrawText(labels[i], cx + Math.Cos(mid) * radius * 0.65, cy + Math.Sin(mid) * radius * 0.65 - 5,
                        new TextStyle(gothic, 8) { Color = PdfColor.White, Bold = true }, TextAlignment.Center);
                    angle += sweep;
                }
            }).SpaceBefore = 10;
            section.AddParagraph("図 2　図形描画 API で描いたグラフ", new TextStyle { Font = gothic, FontSize = 9 })
                .SetAlignment(TextAlignment.Center).Format.FirstLineIndent = 0;

            section.AddParagraph("4. 長い表と自動改ページ", "見出し1").SetPageBreakBefore();
            var big = section.AddTable();
            big.Style = new TextStyle { Font = gothic, FontSize = 9 };
            big.AddColumn(Unit.Mm(15)).Alignment = TextAlignment.Right;
            big.AddRelativeColumn(2);
            big.AddRelativeColumn(3);
            big.HeaderBackground = PdfColor.FromHex("#DCE6F2");
            big.AddHeaderRow("No.", "項目", "説明");
            for (int i = 1; i <= 60; i++)
            {
                string desc = i % 7 == 0
                    ? "この行は説明が長いため、セル内で複数行に折り返されます。行の高さは内容に合わせて自動的に広がり、ページ末尾に収まらない行は次のページへ送られます。"
                    : "説明 " + i;
                big.AddRow(i.ToString(), "項目 " + i, desc);
            }

            doc.Save(path);
        }

        /// <summary>透明度付きのグラデーション画像を生成する。</summary>
        private static PdfImage CreateGradientImage()
        {
            int w = 240, h = 120;
            var rgb = new byte[w * h * 3];
            var alpha = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    rgb[i * 3] = (byte)(255 * x / w);
                    rgb[i * 3 + 1] = (byte)(255 * y / h);
                    rgb[i * 3 + 2] = 200;
                    double dx = (x - w / 2.0) / (w / 2.0), dy = (y - h / 2.0) / (h / 2.0);
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    alpha[i] = (byte)Math.Max(0, Math.Min(255, (1.2 - d) * 255));
                }
            }
            return PdfImage.FromRawPixels(w, h, 3, rgb, alpha);
        }
    }

    /// <summary>座標指定の描画（定型帳票向け）。</summary>
    internal static class LowLevel
    {
        public static void Create(PdfFont font, string path)
        {
            var pdf = new PdfDocument();
            pdf.Info.Title = "座標指定の帳票";
            pdf.Info.CreationDate = new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(9));
            var page = pdf.AddPage(PageSize.A5.Landscape);
            var g = page.Graphics;
            var title = new TextStyle(font, 18) { Bold = true };
            var label = new TextStyle(font, 9);
            var value = new TextStyle(font, 11);
            var pen = new PdfPen(PdfColor.Black, 0.5);

            g.DrawText("受　領　書", page.Width / 2, Unit.Mm(10), title, TextAlignment.Center);
            double x = Unit.Mm(15), y = Unit.Mm(25), w = page.Width - Unit.Mm(30);
            string[] labels = { "お名前", "ご住所", "品名", "金額", "備考" };
            string[] values =
            {
                "山田 太郎 様",
                "東京都港区芝公園4-2-8",
                "帳票作成サービス一式",
                "¥ 55,000 -（税込）",
                "長い備考はセル幅に合わせて自動で縮小されます。この欄は縮小して全体を表示する設定になっています。",
            };
            double rowH = Unit.Mm(12), labelW = Unit.Mm(30);
            for (int i = 0; i < labels.Length; i++)
            {
                double ry = y + rowH * i;
                g.FillRectangle(PdfColor.FromHex("#F2F2F2"), x, ry, labelW, rowH);
                g.DrawRectangle(pen, x, ry, w, rowH);
                g.DrawLine(pen, x + labelW, ry, x + labelW, ry + rowH);
                g.DrawTextBox(labels[i], new PdfRect(x + 4, ry, labelW - 8, rowH), label,
                    new TextBoxOptions { Alignment = TextAlignment.Distribute, VerticalAlignment = VerticalAlignment.Middle });
                g.DrawTextBox(values[i], new PdfRect(x + labelW + 6, ry + 2, w - labelW - 12, rowH - 4), value,
                    new TextBoxOptions { VerticalAlignment = VerticalAlignment.Middle, ShrinkToFit = true });
            }
            g.SaveState();
            g.RotateAt(-15, page.Width - Unit.Mm(40), page.Height - Unit.Mm(30));
            g.DrawCircle(new PdfPen(PdfColor.FromRgb(200, 30, 30), 1.5), null, page.Width - Unit.Mm(40), page.Height - Unit.Mm(30), Unit.Mm(10));
            g.DrawText("受領", page.Width - Unit.Mm(40), page.Height - Unit.Mm(30) - 7, new TextStyle(font, 12) { Color = PdfColor.FromRgb(200, 30, 30) }, TextAlignment.Center);
            g.RestoreState();
            pdf.Save(path);
        }
    }
}
