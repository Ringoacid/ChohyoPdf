using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void HelloDocument()
        {
            // #example HelloDocument
            var doc = new Document(font, 10.5);          // 既定のフォントと文字サイズ
            var section = doc.AddSection();              // A4 縦・余白 20 mm

            section.AddParagraph("はじめての PDF").SetFontSize(18).SetBold().SetSpaceAfter(6);
            section.AddParagraph(
                "ChohyoPdf で作った文書です。長い文章は本文の幅に合わせて自動的に折り返され、" +
                "ページの終わりまで来ると続きが次のページに送られます。" +
                "句読点や閉じかっこが行の先頭に来ないように、禁則処理も行います。");

            doc.Save("hello.pdf");
            // #end
        }

        public static void HelloGraphics()
        {
            // #example HelloGraphics
            var pdf = new PdfDocument();
            var page = pdf.AddPage(PageSize.A6.Landscape);   // 148 × 105 mm
            var g = page.Graphics;

            g.DrawRectangle(new PdfPen(PdfColor.Navy, 1), Unit.Mm(8), Unit.Mm(8), Unit.Mm(132), Unit.Mm(89));
            g.DrawText("座標を指定して描く", Unit.Mm(14), Unit.Mm(14), new TextStyle(font, 16));
            g.DrawCircle(null, PdfColor.FromHex("#F4B183"), Unit.Mm(50), Unit.Mm(62), Unit.Mm(20));
            g.DrawRectangle(null, PdfColor.FromHex("#9DC3E6"), Unit.Mm(80), Unit.Mm(42), Unit.Mm(45), Unit.Mm(40));

            pdf.Save("hello-graphics.pdf");
            // #end
        }

        public static void TextDecoration()
        {
            // #example TextDecoration
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            s.AddParagraph().AddText("大きさ　")
                .AddText("8pt ", new TextStyle { FontSize = 8 })
                .AddText("12pt ", new TextStyle { FontSize = 12 })
                .AddText("18pt", new TextStyle { FontSize = 18 });
            s.AddParagraph().AddText("太字と斜体　")
                .AddBold("太字").AddText(" / ")
                .AddText("斜体", new TextStyle { Italic = true }).AddText(" / ")
                .AddText("太字の斜体", new TextStyle { Bold = true, Italic = true });
            s.AddParagraph().AddText("線　")
                .AddText("下線", new TextStyle { Underline = true }).AddText(" / ")
                .AddText("取り消し線", new TextStyle { Strikeout = true });
            s.AddParagraph().AddText("色　")
                .AddText("赤 ", new TextStyle { Color = PdfColor.Red })
                .AddText("#2E75B6 ", new TextStyle { Color = PdfColor.FromHex("#2E75B6") })
                .AddText("マーカー", new TextStyle { BackgroundColor = PdfColor.FromHex("#FFE699") });
            s.AddParagraph().AddText("字間　")
                .AddText("ひろげる", new TextStyle { CharacterSpacing = 5 }).AddText(" / ")
                .AddText("つめる", new TextStyle { CharacterSpacing = -1.5 });
            s.AddParagraph().AddText("フォント　")
                .AddText("游ゴシック / ").AddText("游明朝", new TextStyle { Font = mincho });
            s.AddParagraph().AddText("リンク　").AddLink("example.com を開く", "https://example.com/");

            // 段落全体にまとめて指定することもできる
            s.AddParagraph("段落全体を紺色の 14pt にする").SetFontSize(14).SetColor(PdfColor.Navy).SetSpaceBefore(6);

            doc.Save("text-decoration.pdf");
            // #end
        }

        public static void TextAlignments()
        {
            // #example TextAlignments
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            string text = "ChohyoPdf は、日本語の帳票を作るための PDF ライブラリです。" +
                          "Visual Studio や Visual Studio Code から、NuGet パッケージとして参照できます。";
            var frame = new PdfPen(PdfColor.LightGray, 0.5);

            foreach (var align in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right, TextAlignment.Justify })
            {
                s.AddParagraph(align.ToString()).SetFontSize(8).SetColor(PdfColor.Gray);
                s.AddParagraph(text).SetAlignment(align).SetBorder(frame, new Thickness(4))
                    .SetIndent(0, 0, Unit.Mm(50)).SetSpaceAfter(6);
            }

            // 均等割り付けは最終行も両端に揃える。短い項目名をそろえるときに使う
            s.AddParagraph("Distribute").SetFontSize(8).SetColor(PdfColor.Gray);
            foreach (string label in new[] { "氏名", "住所", "電話番号" })
                s.AddParagraph(label).SetAlignment(TextAlignment.Distribute).SetBorder(frame, new Thickness(4))
                    .SetIndent(0, 0, Unit.Mm(145));

            doc.Save("text-alignments.pdf");
            // #end
        }

        public static void LineSpacingAndIndent()
        {
            // #example LineSpacingAndIndent
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            string text = "行送りは文字の大きさに対する倍率で指定します。既定値は 1.5 倍です。" +
                          "固定の高さ（ポイント）で指定することもできます。";
            var frame = new PdfPen(PdfColor.LightGray, 0.5);

            s.AddParagraph("行送り 1.0").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph(text).SetLineSpacing(1.0).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
            s.AddParagraph("行送り 2.0").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph(text).SetLineSpacing(2.0).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
            s.AddParagraph("行送り 固定 14pt").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph(text).SetLineSpacing(14, LineSpacingRule.Exactly).SetBorder(frame, new Thickness(4)).SetSpaceAfter(12);

            s.AddParagraph("字下げ（1 行目だけ 1 文字下げる）").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph(text).SetIndent(0, firstLine: 10.5).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
            s.AddParagraph("左右のインデント（20 mm）").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph(text).SetIndent(Unit.Mm(20), 0, Unit.Mm(20)).SetBorder(frame, new Thickness(4)).SetSpaceAfter(6);
            s.AddParagraph("ぶら下げインデント（2 行目以降を下げる）").SetFontSize(8).SetColor(PdfColor.Gray);
            s.AddParagraph("※　" + text).SetIndent(21, firstLine: -21).SetBorder(frame, new Thickness(4));

            doc.Save("line-spacing.pdf");
            // #end
        }

        public static void LineBreaking()
        {
            // #example LineBreaking
            var pdf = new PdfDocument();
            var g = pdf.AddPage(new PageSize(Unit.Mm(170), Unit.Mm(48))).Graphics;
            var style = new TextStyle(font, 12);
            var options = new TextBoxOptions { LineSpacingRule = LineSpacingRule.Exactly, LineSpacing = 18 };
            var grid = new PdfPen(PdfColor.FromHex("#BDD7EE"), 0.5);

            // 1 行 7 文字のマス目に重ねて描き、行末が空く位置を見る
            string[] texts =
            {
                "吾輩は猫である。名前はまだ無い。",
                "長い文章です（注釈付き）。新しいコンピューターを買った。",
            };
            for (int i = 0; i < texts.Length; i++)
            {
                double x = Unit.Mm(8 + i * 45), y = Unit.Mm(8);
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 7; col++)
                        g.DrawRectangle(grid, x + col * 12, y + row * 18, 12, 18);
                g.DrawTextBox(texts[i], new PdfRect(x, y, 12 * 7 + 0.5, 18 * 5), style, options);
            }

            // 英単語は単語の途中で改行しない
            var box = new PdfRect(Unit.Mm(100), Unit.Mm(8), Unit.Mm(60), 18 * 5);
            g.DrawRectangle(grid, box.X, box.Y, box.Width, box.Height);
            g.DrawTextBox("Long words such as typography are never split. 日本語と English が混ざっても同じです。",
                box, style, options);

            pdf.Save("line-breaking.pdf");
            // #end
        }

        public static void SpecialCharacters()
        {
            // #example SpecialCharacters
            var doc = new Document(font, 12);
            // 游ゴシックにない文字（チェスの駒など）は、代替フォントで描く
            doc.DefaultTextStyle.FallbackFonts = new[] { PdfFont.FromSystem("Segoe UI Symbol") };
            var s = doc.AddSection();

            s.AddParagraph("サロゲートペアの漢字：𠮷野家の𠮷、𩸽（ほっけ）");
            s.AddParagraph("記号：①②③ ㈱ ℡ ※ 〒 ♪ ← → ↑ ↓");
            s.AddParagraph("代替フォント：♔ ♕ ♖ ♗ ♘ ♙");
            s.AddParagraph("半角カナ・全角英数：ｱｲｳｴｵ ＡＢＣ１２３");

            doc.Save("special-characters.pdf");
            // #end
        }

        public static void ParagraphBoxes()
        {
            // #example ParagraphBoxes
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            s.AddParagraph("ご注意").SetBold().SetColor(PdfColor.FromHex("#C55A11"));
            s.AddParagraph("背景色と枠線を付けた段落です。内側の余白は Thickness で指定します。" +
                           "段落がページをまたぐときも、背景と枠はページごとに描きます。")
                .SetBackground(PdfColor.FromHex("#FBE5D6"), new Thickness(8, 6))
                .SetBorder(new PdfPen(PdfColor.FromHex("#ED7D31"), 1))
                .SetSpaceAfter(12);

            s.AddParagraph("背景色だけの段落。コードや補足の囲みに使えます。")
                .SetBackground(PdfColor.FromGray(0.94), new Thickness(8, 6))
                .SetSpaceAfter(12);

            s.AddHorizontalLine(new PdfPen(PdfColor.Gray, 0.5));
            s.AddParagraph("水平線（AddHorizontalLine）で区切ることもできます。");

            doc.Save("paragraph-boxes.pdf");
            // #end
        }

        public static void NamedStyles()
        {
            // #example NamedStyles
            var doc = new Document(mincho, 10.5);                 // 本文は游明朝
            doc.DefaultParagraphFormat.Alignment = TextAlignment.Justify;
            doc.DefaultParagraphFormat.FirstLineIndent = 10.5;    // 全段落を 1 文字字下げ
            doc.DefaultParagraphFormat.SpaceAfter = 4;

            doc.Styles["見出し1"] = new ParagraphStyle(
                new TextStyle { Font = font, FontSize = 15, Bold = true, Color = PdfColor.Navy },
                new ParagraphFormat { FirstLineIndent = 0, SpaceBefore = 10, SpaceAfter = 6, KeepWithNext = true });
            doc.Styles["見出し2"] = new ParagraphStyle(
                new TextStyle { Font = font, FontSize = 12, Bold = true },
                new ParagraphFormat { FirstLineIndent = 0, SpaceBefore = 8, SpaceAfter = 4, KeepWithNext = true });
            doc.Styles["注記"] = new ParagraphStyle(
                new TextStyle { Font = font, FontSize = 8.5, Color = PdfColor.DarkGray },
                new ParagraphFormat { FirstLineIndent = 0, LeftIndent = 10.5 });

            var s = doc.AddSection();
            s.AddParagraph("1. はじめに", "見出し1");
            s.AddParagraph("名前付きスタイルを登録すると、段落ごとに書式を並べなくても、見出しや注記の見た目をそろえられます。" +
                           "スタイルを書き換えれば、そのスタイルを使うすべての段落に反映されます。");
            s.AddParagraph("1.1 本文の書式", "見出し2");
            s.AddParagraph("本文は游明朝の両端揃えで、1 文字字下げしています。見出しと注記は游ゴシックにしています。");
            s.AddParagraph("※ 注記スタイルの段落です。小さめの灰色の文字になります。", "注記");
            s.AddParagraph("※ 段落で直接指定した値は、スタイルより優先されます。", "注記").SetColor(PdfColor.Red);

            doc.Save("named-styles.pdf");
            // #end
        }
    }
}
