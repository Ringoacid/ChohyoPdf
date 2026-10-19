using System;
using System.Linq;
using System.Text;
using Xunit;

namespace ChohyoPdf.Tests
{
    public class LayoutTests
    {
        private static Document NewDoc(out bool ok)
        {
            var font = TestFonts.Japanese;
            ok = font != null;
            var doc = font != null ? new Document(font, 10.5) : new Document();
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return doc;
        }

        private static byte[] Render(Document doc, out int pages)
        {
            var bytes = doc.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            pages = PdfChecker.CountPages(bytes);
            return bytes;
        }

        private static string LongText(int repeat)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < repeat; i++) sb.Append("吾輩は猫である。名前はまだ無い。どこで生れたかとんと見当がつかぬ。");
            return sb.ToString();
        }

        [Fact]
        public void MissingFontGivesClearError()
        {
            var doc = new Document();
            doc.AddSection().AddParagraph("abc");
            var ex = Assert.Throws<InvalidOperationException>(() => doc.ToArray());
            Assert.Contains("フォント", ex.Message);
        }

        [Fact]
        public void LongParagraphFlowsOverPages()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            doc.AddSection().AddParagraph(LongText(200));
            Render(doc, out int pages);
            Assert.True(pages >= 3, "pages=" + pages);
        }

        [Fact]
        public void PageBreaks()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();
            s.AddPageBreak(); // ページ先頭の改ページは無視される
            s.AddParagraph("1");
            s.AddPageBreak();
            s.AddParagraph("2");
            s.AddParagraph("3").SetPageBreakBefore();
            Render(doc, out int pages);
            Assert.Equal(3, pages);
        }

        [Fact]
        public void MultipleSectionsStartNewPages()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            doc.AddSection().AddParagraph("A4");
            doc.AddSection(new PageSetup { Size = PageSize.A5.Landscape }).AddParagraph("A5");
            var bytes = Render(doc, out int pages);
            Assert.Equal(2, pages);
            Assert.Contains("/MediaBox [0 0 595.2756 419.5276]", PdfChecker.Latin1(bytes));
        }

        [Fact]
        public void HugeImageTerminates()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var img = PdfImage.FromBytes(TestData.Bytes("rgb8.png"));
            var s = doc.AddSection();
            s.AddParagraph("前");
            s.AddImage(img, height: 5000);
            s.AddImage(img, height: 5000).ScaleToFit = false;
            s.AddParagraph("後");
            Render(doc, out int pages);
            Assert.Equal(3, pages);
        }

        [Fact]
        public void TableRowTallerThanPageIsSplit()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var t = doc.AddSection().AddTable();
            t.AddColumns(1, 3);
            t.AddHeaderRow("見出し", "内容");
            t.AddRow("1", LongText(150));
            t.AddRow("2", "短い");
            Render(doc, out int pages);
            Assert.True(pages >= 2, "pages=" + pages);
        }

        [Fact]
        public void PathologicalTablesTerminate()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();

            // 固定高さでページより高い行
            var t1 = s.AddTable();
            t1.AddColumns(1);
            var r = t1.AddRow("固定");
            r.Height = 2000;
            r.HeightRule = RowHeightRule.Exactly;

            // ページより高い縦結合
            var t2 = s.AddTable();
            t2.AddColumns(1, 1);
            var r1 = t2.AddRow("結合", LongText(100));
            r1[0].RowSpan = 2;
            t2.AddRow(null!, LongText(100));

            // ページより高い見出し行
            var t3 = s.AddTable();
            t3.AddColumns(1);
            t3.AddHeaderRow(LongText(150));
            t3.AddRow("本文1");
            t3.AddRow("本文2");

            // 極端に狭い列
            var t4 = s.AddTable();
            t4.AddColumn(1);
            t4.AddColumn(400);
            t4.AddRow("狭い列のテキスト", "広い列");

            // 全段落が KeepWithNext
            for (int i = 0; i < 80; i++) s.AddParagraph("見出し " + i).SetKeepWithNext();

            Render(doc, out int pages);
            Assert.InRange(pages, 2, 500);
        }

        [Fact]
        public void HeaderRowsAreRepeatedOnEveryPage()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var t = doc.AddSection().AddTable();
            t.AddColumns(1, 1);
            t.HeaderStyle = new TextStyle { FontSize = 13.37 };
            t.AddHeaderRow("見出しA", "見出しB");
            for (int i = 0; i < 200; i++) t.AddRow("行 " + i, "値");
            var bytes = Render(doc, out int pages);
            Assert.True(pages >= 3);
            var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            Assert.Equal(pages, contents.Count);
            Assert.All(contents, c => Assert.Contains("13.37 Tf", c));
        }

        /// <summary>ページ末尾に少しだけ余白があるとき、見出し行と空の枠だけを残さず、表ごと次のページへ送る。</summary>
        [Fact]
        public void TableDoesNotLeaveEmptyFrameAtPageBottom()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();
            s.AddSpacer(s.PageSetup.ContentHeight - 40);
            var t = s.AddTable();
            t.AddColumns(1, 4);
            t.HeaderStyle = new TextStyle { FontSize = 13.37 };
            t.AddHeaderRow("番号", "内容");
            t.AddRow("", LongText(4));
            t.AddRow("1", LongText(3));
            var bytes = Render(doc, out int pages);
            var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            Assert.Equal(2, pages);
            Assert.DoesNotContain("13.37 Tf", contents[0]);
            Assert.Contains("13.37 Tf", contents[1]);
        }

        [Fact]
        public void AllowBreakAcrossPagesUsesRemainingSpace()
        {
            foreach (bool allow in new[] { false, true })
            {
                var doc = NewDoc(out bool ok);
                if (!ok) return;
                var s = doc.AddSection();
                s.AddSpacer(s.PageSetup.ContentHeight - 120);
                var t = s.AddTable();
                t.AddColumns(1, 4);
                t.HeaderStyle = new TextStyle { FontSize = 13.37 };
                t.AddHeaderRow("番号", "内容");
                t.AddRow("1", "短い行");
                t.AddRow("2", LongText(8)).AllowBreakAcrossPages = allow;
                var bytes = Render(doc, out int pages);
                var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
                Assert.Equal(2, pages);
                // 分割しないなら 2 行目の本文は 2 ページ目にだけある（1 ページ目は見出しと 1 行目のみ）
                int tjOnFirst = contents[0].Split(new[] { " Tj" }, StringSplitOptions.None).Length - 1;
                if (allow) Assert.True(tjOnFirst > 6, "tj=" + tjOnFirst);
                else Assert.True(tjOnFirst <= 6, "tj=" + tjOnFirst);
            }
        }

        [Fact]
        public void PageNumberFieldsInHeaderFooterAndBody()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();
            s.Footer.AddParagraph().AddPageNumber().AddText("/").AddPageCount();
            s.AddParagraph().AddText("全").AddPageCount().AddText("ページ");
            s.AddParagraph(LongText(150));
            Render(doc, out int pages);
            Assert.True(pages >= 2);
        }

        [Fact]
        public void KeepWithNextMovesHeadingToNextPage()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();
            s.PageSetup.Margins = new Thickness(50);
            double contentHeight = s.PageSetup.ContentHeight;
            s.AddSpacer(contentHeight - 20);
            s.AddParagraph("見出し").SetKeepWithNext();
            s.AddParagraph(LongText(3)).SetKeepTogether();
            Render(doc, out int pages);
            Assert.Equal(2, pages);
        }

        [Fact]
        public void DrawingBlockReceivesLocalCoordinates()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            PdfRect? received = null;
            var s = doc.AddSection();
            s.AddDrawing(100, 50, (g, rect) =>
            {
                received = rect;
                g.DrawRectangle(new PdfPen(PdfColor.Black), 0, 0, rect.Width, rect.Height);
            });
            Render(doc, out _);
            Assert.NotNull(received);
            Assert.Equal(100, received!.Value.Width);
            Assert.Equal(50, received.Value.Height);
        }

        [Fact]
        public void NamedStylesAreApplied()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            doc.Styles["H"] = new ParagraphStyle(new TextStyle { FontSize = 23.5 }, null);
            doc.AddSection().AddParagraph("見出し", "H");
            var bytes = Render(doc, out _);
            Assert.Contains(PdfChecker.DecodedStreams(bytes), c => c.Contains("23.5 Tf"));
            var bad = NewDoc(out _);
            bad.AddSection().AddParagraph("x", "未登録");
            Assert.Throws<InvalidOperationException>(() => bad.ToArray());
        }

        /// <summary>ぶら下げインデントでは、枠が 1 行目の左端（左余白の位置）から始まる。</summary>
        [Fact]
        public void HangingIndentBorderIncludesFirstLine()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            var s = doc.AddSection();
            s.PageSetup.Margins = new Thickness(50);
            s.AddParagraph("※　" + LongText(2)).SetIndent(21, firstLine: -21).SetBorder(new PdfPen(PdfColor.Black, 0.5));
            var bytes = Render(doc, out _);
            var content = PdfChecker.DecodedStreams(bytes).Single(c => c.StartsWith("1 0 0 -1 0 "));
            // 枠は x = 50、幅は本文の幅いっぱい
            double width = s.PageSetup.ContentWidth;
            Assert.Contains("50 50 " + width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " ", content);
        }

        [Fact]
        public void HyperlinkInParagraphCreatesAnnotation()
        {
            var doc = NewDoc(out bool ok);
            if (!ok) return;
            doc.AddSection().AddParagraph("詳細は").AddLink("こちら", "https://example.com/a");
            var bytes = Render(doc, out _);
            Assert.Contains("/URI (https://example.com/a)", PdfChecker.Latin1(bytes));
        }
    }
}
