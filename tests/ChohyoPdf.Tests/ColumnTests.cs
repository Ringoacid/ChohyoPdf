using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>段組み（PageSetup.ColumnCount など）のテスト。</summary>
    public class ColumnTests
    {
        // 用紙 400 x 600、余白 40。本文は 320 x 520。2 段（段間 20）なら段の幅は 150、3 段（段間 10）なら 100。
        // 行送りは 20 ポイントの固定、文字は 10 ポイントの等幅（全角 10、半角 5）なので、1 段に 26 行入る。
        private const double Margin = 40;
        private const double LinesPerColumn = 26;

        private sealed class TextRunInfo
        {
            public string Text = "";
            public double X;
            public double Y;
            public int Page;
        }

        private static List<TextRunInfo> Runs(byte[] bytes)
        {
            var inspector = new PdfInspector(bytes);
            var texts = inspector.PageRuns();
            var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            var result = new List<TextRunInfo>();
            for (int page = 0; page < contents.Count; page++)
            {
                var matches = Regex.Matches(contents[page], @"/F\d+ (\S+) Tf\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm\n<[0-9A-F]+> Tj");
                Assert.Equal(texts[page].Count, matches.Count);
                for (int i = 0; i < matches.Count; i++)
                {
                    result.Add(new TextRunInfo
                    {
                        Text = texts[page][i],
                        X = double.Parse(matches[i].Groups[2].Value, CultureInfo.InvariantCulture),
                        Y = double.Parse(matches[i].Groups[3].Value, CultureInfo.InvariantCulture),
                        Page = page,
                    });
                }
            }
            return result;
        }

        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        private static Document? NewDoc()
        {
            var font = Mono();
            if (font == null) return null;
            var doc = new Document(font, 10);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            doc.DefaultParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
            doc.DefaultParagraphFormat.LineSpacing = 20;
            doc.DefaultPageSetup = new PageSetup { Size = new PageSize(400, 600), Margins = new Thickness(Margin) };
            return doc;
        }

        private static Section Columns(Document doc, int count, double spacing = 20, PdfPen? separator = null)
        {
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = count;
            s.PageSetup.ColumnSpacing = spacing;
            s.PageSetup.ColumnSeparator = separator;
            return s;
        }

        private static double ColumnX(int column, int count = 2, double spacing = 20)
        {
            double width = (320 - spacing * (count - 1)) / count;
            return Margin + column * (width + spacing);
        }

        private static void Lines(BlockContainer s, int from, int to)
        {
            for (int i = from; i <= to; i++) s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
        }

        private static TextRunInfo Find(List<TextRunInfo> runs, string text)
        {
            var found = runs.Where(r => r.Text == text).ToList();
            Assert.True(found.Count == 1, "文字列 '" + text + "' が " + found.Count + " 件");
            return found[0];
        }

        // ---- 設定 ----

        [Fact]
        public void PageSetup_Defaults_Clone_Validation()
        {
            var setup = new PageSetup();
            Assert.Equal(1, setup.ColumnCount);
            Assert.Null(setup.ColumnSeparator);
            Assert.True(setup.ColumnSpacing > 0);
            Assert.Equal(setup.ContentWidth, setup.ColumnWidth, 6);

            setup.ColumnCount = 3;
            setup.ColumnSpacing = 10;
            setup.ColumnSeparator = new PdfPen(PdfColor.Black, 1);
            Assert.Equal((setup.ContentWidth - 20) / 3, setup.ColumnWidth, 6);

            var clone = setup.Clone();
            Assert.Equal(3, clone.ColumnCount);
            Assert.Equal(10, clone.ColumnSpacing);
            Assert.NotNull(clone.ColumnSeparator);
            Assert.NotSame(setup.ColumnSeparator, clone.ColumnSeparator);
            Assert.Equal(1, clone.ColumnSeparator!.Width);

            Assert.Throws<ArgumentOutOfRangeException>(() => setup.ColumnCount = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => setup.ColumnSpacing = -1);
        }

        [Fact]
        public void DefaultPageSetup_ColumnsAreInheritedByNewSections()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.DefaultPageSetup.ColumnCount = 2;
            doc.DefaultPageSetup.ColumnSpacing = 20;
            var s = doc.AddSection();
            Assert.Equal(2, s.PageSetup.ColumnCount);
            Lines(s, 1, 30);
            var runs = Runs(doc.ToArray());
            Assert.Equal(ColumnX(1), Find(runs, "行027").X, 3);
        }

        [Fact]
        public void ColumnCountOne_IsSameAsNoColumns()
        {
            byte[] Make(bool explicitColumns)
            {
                var doc = NewDoc()!;
                var s = doc.AddSection();
                if (explicitColumns)
                {
                    s.PageSetup.ColumnCount = 1;
                    s.PageSetup.ColumnSeparator = new PdfPen(PdfColor.FromRgb(255, 0, 0), 1);   // 1 段なら引かない
                }
                Lines(s, 1, 60);
                s.AddColumnBreak();
                s.AddParagraph("最後");
                return doc.ToArray();
            }
            if (Mono() == null) return;
            var a = Make(false);
            var b = Make(true);
            Assert.Equal(a, b);
            Assert.Equal(4, new PdfInspector(a).PageCount);    // 26 + 26 + 8 行、段区切りで改ページ、最後
        }

        // ---- 本文の流れ ----

        [Fact]
        public void TwoColumns_FlowFromFirstColumnToSecondThenNextPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            Lines(s, 1, 60);
            var runs = Runs(doc.ToArray());
            Assert.Equal(60, runs.Count);

            var page1 = runs.Where(r => r.Page == 0).ToList();
            Assert.Equal(52, page1.Count);
            Assert.Equal("行001", page1[0].Text);
            Assert.Equal("行027", page1[26].Text);
            foreach (var r in page1.Take(26)) Assert.Equal(ColumnX(0), r.X, 3);
            foreach (var r in page1.Skip(26)) Assert.Equal(ColumnX(1), r.X, 3);
            // 2 段目の最初の行は、1 段目の最初の行と同じ高さ
            Assert.Equal(page1[0].Y, page1[26].Y, 6);
            Assert.True(page1[25].Y > page1[0].Y);

            var page2 = runs.Where(r => r.Page == 1).ToList();
            Assert.Equal("行053", page2[0].Text);
            Assert.Equal(8, page2.Count);
            Assert.All(page2, r => Assert.Equal(ColumnX(0), r.X, 3));      // 最後のページは 1 段目だけ（段の高さはそろえない）
        }

        [Fact]
        public void ThreeColumns_FlowLeftToRight()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 3, 10);
            Lines(s, 1, 80);
            var runs = Runs(doc.ToArray());
            Assert.Equal(1, runs.Max(r => r.Page));    // 78 行で 1 ページが埋まる
            Assert.Equal(ColumnX(0, 3, 10), Find(runs, "行001").X, 3);
            Assert.Equal(ColumnX(1, 3, 10), Find(runs, "行027").X, 3);
            Assert.Equal(ColumnX(2, 3, 10), Find(runs, "行053").X, 3);
            Assert.Equal(1, Find(runs, "行079").Page);
            Assert.Equal(ColumnX(0, 3, 10), Find(runs, "行079").X, 3);
        }

        [Fact]
        public void LongParagraphIsSplitAcrossColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var p = s.AddParagraph();
            for (int i = 1; i <= 40; i++) p.AddText("行" + i.ToString("00", CultureInfo.InvariantCulture) + (i < 40 ? "\n" : ""));
            var runs = Runs(doc.ToArray());
            Assert.Equal(40, runs.Count);
            Assert.All(runs, r => Assert.Equal(0, r.Page));
            Assert.Equal(ColumnX(0), Find(runs, "行26").X, 3);
            Assert.Equal(ColumnX(1), Find(runs, "行27").X, 3);
            Assert.Equal(Find(runs, "行01").Y, Find(runs, "行27").Y, 6);
        }

        [Fact]
        public void ParagraphWrapsAtColumnWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.AddParagraph(new string('あ', 40));     // 全角 10 ポイント × 40 = 400。段の幅 150 なら 1 行 15 文字で 3 行
            var runs = Runs(doc.ToArray());
            Assert.Equal(3, runs.Count);
            Assert.Equal(new[] { 15, 15, 10 }, runs.Select(r => r.Text.Length));
        }

        // ---- 段区切り・改ページ ----

        [Fact]
        public void ColumnBreak_GoesToNextColumnThenNextPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.AddParagraph("あ");
            s.AddColumnBreak();
            s.AddParagraph("い");
            s.AddColumnBreak();
            s.AddParagraph("う");
            s.AddColumnBreak();
            s.AddColumnBreak();      // 連続した段区切りは、先頭では無視される
            s.AddParagraph("え");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "あ").Page);
            Assert.Equal(ColumnX(0), Find(runs, "あ").X, 3);
            Assert.Equal(0, Find(runs, "い").Page);
            Assert.Equal(ColumnX(1), Find(runs, "い").X, 3);
            Assert.Equal(Find(runs, "あ").Y, Find(runs, "い").Y, 6);
            Assert.Equal(1, Find(runs, "う").Page);
            Assert.Equal(ColumnX(0), Find(runs, "う").X, 3);
            Assert.Equal(1, Find(runs, "え").Page);
            Assert.Equal(ColumnX(1), Find(runs, "え").X, 3);
        }

        [Fact]
        public void ColumnBreakAtTop_IsIgnored()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.AddColumnBreak();
            s.AddParagraph("あ");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "あ").Page);
            Assert.Equal(ColumnX(0), Find(runs, "あ").X, 3);
        }

        [Fact]
        public void ColumnBreak_WithoutColumns_IsPageBreak()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("あ");
            s.AddColumnBreak();
            s.AddParagraph("い");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "あ").Page);
            Assert.Equal(1, Find(runs, "い").Page);
        }

        [Fact]
        public void PageBreak_InColumnsGoesToNextPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.AddParagraph("あ");
            s.AddPageBreak();
            s.AddParagraph("い");
            s.AddColumnBreak();
            s.AddPageBreak();     // 段の先頭の改ページ（前の段に内容がある）は、次のページへ進める
            s.AddParagraph("う");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "あ").Page);
            Assert.Equal(1, Find(runs, "い").Page);
            Assert.Equal(ColumnX(0), Find(runs, "い").X, 3);
            Assert.Equal(2, Find(runs, "う").Page);
            Assert.Equal(ColumnX(0), Find(runs, "う").X, 3);
        }

        [Fact]
        public void PageBreakBefore_InSecondColumnGoesToNextPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.AddParagraph("あ");
            s.AddColumnBreak();
            s.AddParagraph("い");
            s.AddParagraph("う").SetPageBreakBefore();
            var runs = Runs(doc.ToArray());
            Assert.Equal(ColumnX(1), Find(runs, "い").X, 3);
            Assert.Equal(1, Find(runs, "う").Page);
            Assert.Equal(ColumnX(0), Find(runs, "う").X, 3);
        }

        [Fact]
        public void KeepWithNext_MovesHeadingToNextColumn()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            Lines(s, 1, 25);
            s.AddParagraph("見出し").SetKeepWithNext();   // 26 行目（1 段目の最後）
            s.AddParagraph("本文");
            var runs = Runs(doc.ToArray());
            Assert.Equal(ColumnX(0), Find(runs, "行025").X, 3);
            Assert.Equal(ColumnX(1), Find(runs, "見出し").X, 3);
            Assert.Equal(ColumnX(1), Find(runs, "本文").X, 3);
            Assert.Equal(Find(runs, "行001").Y, Find(runs, "見出し").Y, 6);
        }

        [Fact]
        public void SectionBreak_CanChangeColumnCount()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var title = doc.AddSection();
            title.AddParagraph("表題");
            var body = Columns(doc, 2);
            Lines(body, 1, 30);
            var last = doc.AddSection();
            last.AddParagraph("結び");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "表題").Page);
            Assert.Equal(ColumnX(0), Find(runs, "表題").X, 3);
            Assert.Equal(1, Find(runs, "行001").Page);
            Assert.Equal(ColumnX(1), Find(runs, "行027").X, 3);
            Assert.Equal(2, Find(runs, "結び").Page);
            Assert.Equal(ColumnX(0), Find(runs, "結び").X, 3);
            Assert.Equal(3, new PdfInspector(doc.ToArray()).PageCount);
        }

        [Fact]
        public void TooNarrowColumns_Throw()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 5, 100);
            s.AddParagraph("あ");
            Assert.Throws<InvalidOperationException>(() => doc.ToArray());
        }

        // ---- 表・画像・図・リスト ----

        [Fact]
        public void Table_UsesColumnWidthAndRepeatsHeaderInEachColumn()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var table = s.AddTable();
            table.AddColumns(1, 1);
            table.Borders = null;
            table.CellPadding = new Thickness(0);
            table.AddHeaderRow("題", "値");
            for (int i = 1; i <= 40; i++) table.AddRow("名" + i.ToString("00", CultureInfo.InvariantCulture), "値" + i.ToString("00", CultureInfo.InvariantCulture));
            var runs = Runs(doc.ToArray());

            // 相対幅の列は、段の幅（150）を等分する
            Assert.Equal(ColumnX(0), Find(runs, "名01").X, 3);
            Assert.Equal(ColumnX(0) + 75, Find(runs, "値01").X, 3);
            // 表は 2 段目に続き、見出し行がそこでも繰り返される
            var headers = runs.Where(r => r.Text == "題").ToList();
            Assert.Equal(2, headers.Count);
            Assert.Equal(ColumnX(0), headers[0].X, 3);
            Assert.Equal(ColumnX(1), headers[1].X, 3);
            Assert.Equal(headers[0].Y, headers[1].Y, 6);
            Assert.Equal(ColumnX(1), Find(runs, "名40").X, 3);
        }

        [Fact]
        public void Table_FixedColumnsWiderThanColumnAreShrunk()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var table = s.AddTable();
            table.Borders = null;
            table.CellPadding = new Thickness(0);
            table.AddColumn(200);
            table.AddColumn(100);
            table.AddRow("左", "右");
            var runs = Runs(doc.ToArray());
            Assert.Equal(ColumnX(0) + 100, Find(runs, "右").X, 3);    // 300 → 150 に縮める（200:100 の比率のまま）

            // 段組みでなければ、従来どおり指定した幅のまま
            var plain = NewDoc()!;
            var ps = plain.AddSection();
            var pt = ps.AddTable();
            pt.Borders = null;
            pt.CellPadding = new Thickness(0);
            pt.AddColumn(200);
            pt.AddColumn(100);
            pt.AddRow("左", "右");
            Assert.Equal(Margin + 200, Find(Runs(plain.ToArray()), "右").X, 3);
        }

        [Fact]
        public void Image_IsScaledToColumnWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var image = PdfImage.FromBytes(TestData.Bytes("rgb8.png"));
            s.AddImage(image, width: 500);
            s.AddImage(image, width: 100);
            var content = string.Join("\n", PdfChecker.DecodedStreams(doc.ToArray()));
            var widths = Regex.Matches(content, @"(\S+) 0 0 -(\S+) (\S+) (\S+) cm\n/Im\d+ Do").Cast<Match>()
                .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(2, widths.Count);
            Assert.Equal(150, widths[0], 3);
            Assert.Equal(100, widths[1], 3);
        }

        [Fact]
        public void Drawing_WiderThanColumnIsClamped()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var widths = new List<double>();
            s.AddDrawing(500, 30, (g, r) => widths.Add(r.Width));
            s.AddDrawing(100, 30, (g, r) => widths.Add(r.Width));
            s.AddDrawing(30, (g, r) => widths.Add(r.Width));      // 幅の指定なし
            doc.ToArray();
            Assert.Equal(new[] { 150.0, 100.0, 150.0 }, widths);
        }

        [Fact]
        public void List_FlowsAcrossColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var list = s.AddNumberedList();
            for (int i = 1; i <= 30; i++) list.AddItem("項" + i.ToString("00", CultureInfo.InvariantCulture));
            var runs = Runs(doc.ToArray());
            double offset = Find(runs, "項01").X - ColumnX(0);
            Assert.True(offset > 0);
            Assert.Equal(ColumnX(1) + offset, Find(runs, "項27").X, 3);
            Assert.Equal(Find(runs, "項01").Y, Find(runs, "項27").Y, 6);
            // 2 段目の最初の項目の番号は、その項目の左の記号の列に付く
            Assert.Equal(ColumnX(1), Find(runs, "27.").X, 3);
        }

        [Fact]
        public void Ruby_InColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            var p = s.AddParagraph();
            p.AddRuby("漢字", "かんじ");
            p.AddText(new string('あ', 30));
            var runs = Runs(doc.ToArray());
            Assert.Contains(runs, r => r.Text == "かんじ");
            Assert.All(runs, r => Assert.InRange(r.X, ColumnX(0) - 1e-6, ColumnX(0) + 150));
            Assert.All(runs, r => Assert.Equal(0, r.Page));
        }

        // ---- ヘッダー/フッター・背景/前景 ----

        [Fact]
        public void HeaderFooterAndBackground_UseFullPageWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2);
            s.Header.AddParagraph("HH").SetAlignment(TextAlignment.Right);
            s.Footer.AddParagraph("FF").SetAlignment(TextAlignment.Center);
            Lines(s, 1, 30);
            var areas = new List<PdfRect>();
            var contentAreas = new List<PdfRect>();
            var columnCounts = new List<int>();
            s.PageBackground = (g, ctx) =>
            {
                if (ctx.PageNumber != 1) return;
                contentAreas.Add(ctx.ContentArea);
                columnCounts.Add(ctx.ColumnCount);
                areas.AddRange(ctx.ColumnAreas);
            };
            var runs = Runs(doc.ToArray());

            // ヘッダーは本文領域の右端に、フッターは本文領域の中央にそろう（段の影響を受けない）
            Assert.Equal(Margin + 320 - 10, Find(runs, "HH").X, 3);
            Assert.Equal(Margin + 160 - 5, Find(runs, "FF").X, 3);

            var area = Assert.Single(contentAreas);
            Assert.Equal(new PdfRect(Margin, Margin, 320, 520), area);
            Assert.Equal(2, Assert.Single(columnCounts));
            Assert.Equal(2, areas.Count);
            Assert.Equal(new PdfRect(Margin, Margin, 150, 520), areas[0]);
            Assert.Equal(new PdfRect(Margin + 170, Margin, 150, 520), areas[1]);
        }

        [Fact]
        public void PageContext_WithoutColumns_HasOneColumnArea()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("あ");
            PageContext? seen = null;
            s.PageBackground = (g, ctx) => seen = ctx;
            doc.ToArray();
            Assert.Equal(1, seen!.ColumnCount);
            var only = Assert.Single(seen.ColumnAreas);
            Assert.Equal(seen.ContentArea, only);
        }

        // ---- 境界線 ----

        private static readonly Regex RedLine = new Regex(@"1 0 0 RG\n[^\n]*\n(\S+) (\S+) m (\S+) (\S+) l\n", RegexOptions.Compiled);

        private static List<Match> Separators(byte[] bytes, int page)
        {
            var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            return RedLine.Matches(contents[page]).Cast<Match>().ToList();
        }

        [Fact]
        public void Separator_IsDrawnBetweenUsedColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 3, 10, new PdfPen(PdfColor.FromRgb(255, 0, 0), 1));
            Lines(s, 1, 60);      // 1 ページ目は 3 段、2 ページ目は 1 段目だけ使う（60 行 = 26 + 26 + 8）
            var bytes = doc.ToArray();
            var page1 = Separators(bytes, 0);
            Assert.Equal(2, page1.Count);
            // 境界線は段間の中央に、本文領域の高さいっぱい
            Assert.Equal(Margin + 100 + 5, double.Parse(page1[0].Groups[1].Value, CultureInfo.InvariantCulture), 3);
            Assert.Equal(Margin + 100 + 10 + 100 + 5, double.Parse(page1[1].Groups[1].Value, CultureInfo.InvariantCulture), 3);
            Assert.Equal(Margin, double.Parse(page1[0].Groups[2].Value, CultureInfo.InvariantCulture), 3);
            Assert.Equal(Margin + 520, double.Parse(page1[0].Groups[4].Value, CultureInfo.InvariantCulture), 3);
        }

        [Fact]
        public void Separator_NotDrawnWhenOnlyOneColumnHasContent()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = Columns(doc, 2, 20, new PdfPen(PdfColor.FromRgb(255, 0, 0), 1));
            Lines(s, 1, 30);
            s.AddPageBreak();
            Lines(s, 31, 33);     // 2 ページ目は 1 段目だけ
            var bytes = doc.ToArray();
            Assert.Single(Separators(bytes, 0));
            Assert.Empty(Separators(bytes, 1));
        }

        // ---- しおり・アンカー・目次 ----

        private static byte[] BuildOutlineDocument(out Document? built)
        {
            var doc = NewDoc();
            built = doc;
            if (doc == null) return new byte[0];
            var s = Columns(doc, 2);
            s.AddTableOfContents("目次", 2);
            s.AddParagraph("章A").SetOutline(1).SetAnchor("a");
            Lines(s, 1, 10);
            s.AddColumnBreak();
            s.AddParagraph("節B").SetOutline(2).SetAnchor("b");
            s.AddParagraph("参照").AddInternalLink("Bへ", "b").AddText(" p.").AddPageRef("b");
            Lines(s, 11, 14);
            s.AddColumnBreak();
            s.AddParagraph("章C").SetOutline(1).SetAnchor("c");
            s.AddParagraph("参照C").AddInternalLink("Cへ", "c").AddText(" p.").AddPageRef("c");
            return doc.ToArray();
        }

        [Fact]
        public void BookmarksAndLinks_PointToColumnPositions()
        {
            var bytes = BuildOutlineDocument(out var doc);
            if (doc == null) return;
            var pdf = new PdfInspector(bytes);
            var outline = pdf.Outlines();
            Assert.Equal(new[] { "章A", "節B", "章C" }, outline.Select(o => o.Title));
            // 章A は目次(1 + 項目 3 行)の後ろ。節B は 2 段目の先頭、章C は 2 ページ目の 1 段目の先頭
            Assert.Equal(0, outline[0].PageIndex);
            Assert.Equal(0, outline[1].PageIndex);
            Assert.Equal(1, outline[2].PageIndex);
            Assert.True(outline[0].Top < 600 - Margin - 20 * 3);       // 目次の下にある
            Assert.Equal(600 - Margin, outline[1].Top, 3);               // 2 段目の先頭
            Assert.Equal(600 - Margin, outline[2].Top, 3);

            // 文書内リンクも同じ位置を指す
            var links = pdf.Links();
            Assert.Equal(5, links.Count);       // 目次の 3 行 + 本文の 2 つ
            Assert.Equal(outline.Select(o => o.Top), links.Take(3).Select(l => l.DestTop));
            Assert.Equal(outline.Select(o => o.PageIndex), links.Take(3).Select(l => l.DestPageIndex));
            Assert.Equal(0, links[3].DestPageIndex);
            Assert.Equal(600 - Margin, links[3].DestTop, 3);
            Assert.Equal(1, links[4].DestPageIndex);
            Assert.Equal(600 - Margin, links[4].DestTop, 3);
        }

        [Fact]
        public void TableOfContents_PageNumbersAndWidthInColumns()
        {
            var bytes = BuildOutlineDocument(out var doc);
            if (doc == null) return;
            var runs = Runs(bytes);
            var page1 = runs.Where(r => r.Page == 0).ToList();
            // 目次の行は 1 段目に収まり、ページ番号は段の右端（ColumnX(0) + 150）にそろう（半角 1 文字 = 5 ポイント）
            var toc = page1.Where(r => r.X < ColumnX(1) && r.Y < Margin + 20 * 4).ToList();
            var numbers = toc.Where(r => r.Text == "1" || r.Text == "2").ToList();
            Assert.Equal(3, numbers.Count);
            Assert.Equal(new[] { "1", "1", "2" }, numbers.Select(r => r.Text));
            Assert.All(numbers, r => Assert.Equal(ColumnX(0) + 150 - 5, r.X, 3));
            // 本文中のページ参照
            Assert.Contains(page1, r => r.Text == " p." || r.Text == "1");
        }

        [Fact]
        public void Layout_IsDeterministicAcrossRenders()
        {
            var a = BuildOutlineDocument(out var doc);
            if (doc == null) return;
            var b = doc.ToArray();      // 同じ文書をもう一度出力しても同じ
            Assert.Equal(a, b);
            Assert.Equal(a, BuildOutlineDocument(out _));
        }
    }
}
