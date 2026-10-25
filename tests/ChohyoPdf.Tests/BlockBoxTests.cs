using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>座標を指定して段落・箇条書きを描く（PdfGraphics.DrawBlocks）のテスト。</summary>
    public class BlockBoxTests
    {
        /// <summary>ページ上の文字列 1 つ。(X, Y) は DrawBlocks の中の座標でのベースラインの位置。</summary>
        private sealed class Run
        {
            public string Text = "";
            public double X;
            public double Y;
            public double Size;
        }

        private static List<Run> Runs(byte[] bytes, int page = 0)
        {
            var texts = new PdfInspector(bytes).PageRuns()[page];
            var content = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList()[page];
            var matches = Regex.Matches(content, @"/F\d+ (\S+) Tf\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm\n<[0-9A-F]+> Tj");
            Assert.Equal(texts.Count, matches.Count);
            return matches.Cast<Match>().Select((m, i) => new Run
            {
                Text = texts[i],
                Size = Num(m.Groups[1].Value),
                X = Num(m.Groups[2].Value),
                Y = Num(m.Groups[3].Value),
            }).ToList();
        }

        private static double Num(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        private static string Content(byte[] bytes, int page = 0) => PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList()[page];

        /// <summary>全角 1em・半角 0.5em の等幅フォント（MS ゴシック）。見つからない環境では null。</summary>
        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        [Fact]
        public void BulletList_IsPlacedInsideTheRect()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var list = new ListBlock();
            list.AddItem("一つ目");
            list.AddItem("二つ目");
            var result = g.DrawBlocks(list, new PdfRect(100, 200, 200, 100), new TextStyle(font, 10), new TextBoxOptions { LineSpacing = 1.5 });

            var bytes = pdf.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            Assert.Contains("1 0 0 1 100 200 cm", Content(bytes));
            var runs = Runs(bytes);
            Assert.Equal(new[] { "・", "一つ目", "・", "二つ目" }, runs.Select(r => r.Text));
            // 記号の列の幅は「・」の 10、本文との間隔は文字サイズの半分の 5
            Assert.Equal(0, runs[0].X, 3);
            Assert.Equal(15, runs[1].X, 3);
            Assert.Equal(runs[0].Y, runs[1].Y, 3);
            Assert.Equal(15, runs[3].Y - runs[1].Y, 3);
            Assert.Equal(30, result.Height, 3);
            Assert.False(result.Overflowed);
            Assert.Equal(1, result.Scale);
        }

        [Fact]
        public void Superscript_InsideParagraph()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var p = new Paragraph("面積 10 m");
            p.AddSuperscript("2");
            p.AddText(" と H");
            p.AddSubscript("2");
            p.AddText("O");
            g.DrawBlocks(p, new PdfRect(0, 0, 300, 50), new TextStyle(font, 10));

            var runs = Runs(pdf.ToArray());
            var sup = runs.Where(r => r.Text == "2").ToList();
            Assert.Equal(2, sup.Count);
            Assert.Equal(6.5, sup[0].Size, 3);
            Assert.True(sup[0].Y < runs[0].Y);
            Assert.True(sup[1].Y > runs[0].Y);
        }

        [Fact]
        public void ShrinkToFit_ScalesEverything()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var list = new ListBlock { Kind = ListKind.Numbered };
            for (int i = 0; i < 6; i++) list.AddItem("項目の文字列です");
            var options = new TextBoxOptions { ShrinkToFit = true, LineSpacing = 1.0 };
            var result = g.DrawBlocks(list, new PdfRect(0, 0, 100, 30), new TextStyle(font, 10), options);
            Assert.True(result.Scale < 1);
            Assert.False(result.Overflowed);
            Assert.True(result.Height <= 30 + 1e-6);
            Assert.Equal(10 * result.Scale, result.FontSize, 6);
            Assert.Contains(" 0 0 " + result.Scale.ToString("0.####", CultureInfo.InvariantCulture), Content(pdf.ToArray()));
        }

        [Fact]
        public void Overflow_DrawsOnlyWhatFits()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var blocks = new List<Block> { new Paragraph("一行目"), new Paragraph("二行目"), new Paragraph("三行目") };
            var result = g.DrawBlocks(blocks, new PdfRect(0, 0, 100, 25), new TextStyle(font, 10), new TextBoxOptions { LineSpacing = 1.0 });
            Assert.True(result.Overflowed);
            Assert.Equal(20, result.Height, 3);
            Assert.Equal(30, result.TotalHeight, 3);
            Assert.Equal(new[] { "一行目", "二行目" }, Runs(pdf.ToArray()).Select(r => r.Text));

            // はみ出す分も描く
            var pdf2 = new PdfDocument();
            var all = pdf2.AddPage().Graphics.DrawBlocks(blocks, new PdfRect(0, 0, 100, 25), new TextStyle(font, 10),
                new TextBoxOptions { LineSpacing = 1.0, DrawOverflow = true });
            Assert.Equal(30, all.Height, 3);
            Assert.Equal(3, Runs(pdf2.ToArray()).Count);
        }

        [Fact]
        public void VerticalAlignmentAndDefaultParagraphFormat()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var options = new TextBoxOptions { LineSpacing = 1.0, VerticalAlignment = VerticalAlignment.Bottom, Alignment = TextAlignment.Right };
            var right = new Paragraph("右");
            var left = new Paragraph("左") { Format = new ParagraphFormat { Alignment = TextAlignment.Left } };
            g.DrawBlocks(new Block[] { right, left }, new PdfRect(10, 20, 100, 50), new TextStyle(font, 10), options);
            var bytes = pdf.ToArray();
            // 内容の高さ 20 を下に揃える
            Assert.Contains("1 0 0 1 10 50 cm", Content(bytes));
            var runs = Runs(bytes);
            Assert.Equal(90, runs[0].X, 3);
            Assert.Equal(0, runs[1].X, 3);
        }

        [Fact]
        public void NoWrap_KeepsOneLineAndAligns()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var options = new TextBoxOptions { Wrap = false, Alignment = TextAlignment.Right };
            var result = g.DrawBlocks(new Paragraph("あいうえおかきくけこ"), new PdfRect(0, 0, 50, 50), new TextStyle(font, 10), options);
            var runs = Runs(pdf.ToArray());
            Assert.Single(runs);
            Assert.Equal(-50, runs[0].X, 3);
            Assert.True(result.Overflowed);

            // 縮小すると幅に収まる
            var pdf2 = new PdfDocument();
            options.ShrinkToFit = true;
            var shrunk = pdf2.AddPage().Graphics.DrawBlocks(new Paragraph("あいうえおかきくけこ"), new PdfRect(0, 0, 50, 50), new TextStyle(font, 10), options);
            Assert.False(shrunk.Overflowed);
            Assert.True(shrunk.Scale <= 0.5 + 1e-6);
        }

        [Fact]
        public void PageNumber_IsThePagePosition()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            pdf.AddPage();
            var g = pdf.AddPage().Graphics;
            var p = new Paragraph("ページ ");
            p.AddPageNumber();
            g.DrawBlocks(p, new PdfRect(0, 0, 200, 20), new TextStyle(font, 10));
            Assert.Equal("ページ 2", Runs(pdf.ToArray(), 1).Single().Text);
        }

        private static Table SampleTable(int rows)
        {
            var table = new Table();
            table.AddColumn(50);
            table.AddColumn(50);
            for (int i = 0; i < rows; i++)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph("行" + i);
                row.Cells[1].AddParagraph("値" + i);
            }
            return table;
        }

        [Fact]
        public void Table_ShrinksAndOverflows()
        {
            var font = Mono();
            if (font == null) return;
            // 縮小すると、表の全体が矩形に収まる
            var pdf = new PdfDocument();
            var shrunk = pdf.AddPage().Graphics.DrawBlocks(SampleTable(10), new PdfRect(0, 0, 100, 60), new TextStyle(font, 10),
                new TextBoxOptions { ShrinkToFit = true, MinFontSize = 1 });
            Assert.False(shrunk.Overflowed);
            Assert.True(shrunk.Scale < 1);
            Assert.True(shrunk.Height <= 60 + 1e-6);
            Assert.Equal(20, Runs(pdf.ToArray()).Count);

            // 縮小しなければ、入る行だけを描く
            var pdf2 = new PdfDocument();
            var clipped = pdf2.AddPage().Graphics.DrawBlocks(SampleTable(10), new PdfRect(0, 0, 100, 60), new TextStyle(font, 10));
            Assert.True(clipped.Overflowed);
            Assert.True(clipped.Height <= 60 + 1e-6);
            Assert.True(clipped.TotalHeight > 60);
            int drawn = Runs(pdf2.ToArray()).Count;
            Assert.InRange(drawn, 2, 18);
            Assert.Equal(0, drawn % 2);
        }

        [Fact]
        public void Anchors_BecomeNamedDestinations()
        {
            var font = Mono();
            if (font == null) return;
            // 1 ページ目のリンクから、あとで 2 ページ目に描いたアンカーへ飛ぶ
            var pdf = new PdfDocument();
            var first = pdf.AddPage().Graphics;
            var second = pdf.AddPage().Graphics;
            first.DrawBlocks(new Paragraph().AddInternalLink("2 ページ目へ", "target"), new PdfRect(0, 0, 200, 20), new TextStyle(font, 10));
            second.DrawBlocks(new Paragraph("飛び先").SetAnchor("target"), new PdfRect(0, 100, 200, 20), new TextStyle(font, 10));
            var bytes = pdf.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            Assert.Matches(@"/Dest \[\d+ 0 R", PdfChecker.Latin1(bytes));
        }

        [Fact]
        public void TableOfContentsAndPageRef_AreRejected()
        {
            var font = Mono();
            if (font == null) return;
            var g = new PdfDocument().AddPage().Graphics;
            var toc = Assert.Throws<InvalidOperationException>(() => g.DrawBlocks(new TableOfContents(), new PdfRect(0, 0, 200, 100), new TextStyle(font, 10)));
            Assert.Contains("DrawBlocks", toc.Message);
            var pageRef = Assert.Throws<InvalidOperationException>(() => g.DrawBlocks(new Paragraph().AddPageRef("a"), new PdfRect(0, 0, 200, 100), new TextStyle(font, 10)));
            Assert.Contains("DrawBlocks", pageRef.Message);
        }

        [Fact]
        public void Footnote_IsRejected()
        {
            var font = Mono();
            if (font == null) return;
            var g = new PdfDocument().AddPage().Graphics;
            var p = new Paragraph("本文");
            p.AddFootnote("脚注");
            Assert.Throws<InvalidOperationException>(() => g.DrawBlocks(p, new PdfRect(0, 0, 200, 20), new TextStyle(font, 10)));
        }
    }
}
