using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>脚注（Paragraph.AddFootnote、Document.Footnote* など）のテスト。</summary>
    public class FootnoteTests
    {
        // 用紙 400 x 600、余白 40。本文は 320 x 520。本文は 10 ポイントの等幅フォント（全角 10）で行送りは 20 ポイントの固定、
        // 1 ページに 26 行入る。脚注は 8.5 ポイントの文字で、行送り 12 ポイントの固定、脚注の前の空き 2 ポイント、区切り線の分が 6.5 ポイント。
        // したがって、脚注 1 行の脚注領域は 6.5 + 2 + 12 = 20.5 ポイント。
        private const double Margin = 40;
        private const double ContentBottom = 560;

        private sealed class RunInfo
        {
            public string Text = "";
            public double Size;
            public double X;
            public double Y;
            public int Page;
        }

        private static List<RunInfo> Runs(byte[] bytes)
        {
            var inspector = new PdfInspector(bytes);
            var texts = inspector.PageRuns();
            var contents = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            var result = new List<RunInfo>();
            for (int page = 0; page < contents.Count; page++)
            {
                var matches = Regex.Matches(contents[page], @"/F\d+ (\S+) Tf\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm\n<[0-9A-F]+> Tj");
                Assert.Equal(texts[page].Count, matches.Count);
                for (int i = 0; i < matches.Count; i++)
                {
                    result.Add(new RunInfo
                    {
                        Text = texts[page][i],
                        Size = double.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture),
                        X = double.Parse(matches[i].Groups[2].Value, CultureInfo.InvariantCulture),
                        Y = double.Parse(matches[i].Groups[3].Value, CultureInfo.InvariantCulture),
                        Page = page,
                    });
                }
            }
            return result;
        }

        private static Document? NewDoc()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            if (path == null) return null;
            var doc = new Document(PdfFont.FromFile(path, "MS Gothic"), 10);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            doc.DefaultParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
            doc.DefaultParagraphFormat.LineSpacing = 20;
            doc.DefaultPageSetup = new PageSetup { Size = new PageSize(400, 600), Margins = new Thickness(Margin) };
            doc.FootnoteFormat = new ParagraphFormat { LineSpacingRule = LineSpacingRule.Exactly, LineSpacing = 12 };
            return doc;
        }

        private static void Lines(BlockContainer s, int from, int to)
        {
            for (int i = from; i <= to; i++) s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
        }

        private static RunInfo Find(List<RunInfo> runs, string text)
        {
            var found = runs.Where(r => r.Text == text).ToList();
            Assert.True(found.Count == 1, "文字列 '" + text + "' が " + found.Count + " 件");
            return found[0];
        }

        // ---- 設定 ----

        [Fact]
        public void Api_DefaultsAndOverloads()
        {
            var doc = new Document();
            Assert.Equal(PageNumberFormat.Arabic, doc.FootnoteNumberFormat);
            Assert.Null(doc.FootnoteStyle);
            Assert.Null(doc.FootnoteFormat);
            Assert.Null(doc.FootnoteSymbols);
            Assert.NotNull(doc.FootnoteSeparator);
            Assert.Null(doc.FootnoteSeparatorLength);
            Assert.True(doc.FootnoteSpacing > 0);
            Assert.Equal(FootnoteNumbering.Continuous, doc.AddSection().FootnoteNumbering);

            var p = new Paragraph("本文");
            Assert.Same(p, p.AddFootnote("脚注"));
            var body = p.AddFootnote();
            body.AddParagraph("二つ目");
            Assert.Equal(3, p.Inlines.Count);
            Assert.All(p.Inlines.Skip(1), i => Assert.IsType<FootnoteRun>(i));
            Assert.Same(body, ((FootnoteRun)p.Inlines[2]).Footnote);
            Assert.Single(((FootnoteRun)p.Inlines[1]).Footnote.Blocks);
            Assert.Throws<ArgumentNullException>(() => new FootnoteRun(null!));
        }

        // ---- 配置 ----

        [Fact]
        public void Footnote_IsPlacedAtTheBottomOfThePageWithSeparatorLine()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("本文一行目").AddFootnote("これは脚注です");
            s.AddParagraph("本文二行目");
            byte[] bytes = doc.ToArray();
            var runs = Runs(bytes);

            var body = Find(runs, "本文一行目");
            var reference = runs.First(r => r.Text == "1" && r.Y < 100);
            var marker = runs.First(r => r.Text == "1" && r.Y > 400);
            var text = Find(runs, "これは脚注です");

            // 参照記号は本文より小さい上付き
            Assert.True(reference.Size < body.Size);
            Assert.True(reference.Y < body.Y);
            Assert.Equal(Margin + 50, reference.X, 3);
            // 脚注: 番号が左端、本文はその右。脚注領域は 20.5 ポイントで、本文領域の下端にそろう
            Assert.Equal(Margin, marker.X, 3);
            Assert.True(text.X > marker.X);
            Assert.Equal(marker.Y, text.Y, 3);
            Assert.True(text.Y <= ContentBottom && text.Y > ContentBottom - 20.5);
            Assert.True(Math.Abs(text.Size - 8.5) < 1e-6);
            Assert.Equal(1, new PdfInspector(bytes).PageCount);

            // 区切り線: 本文領域の幅の 3 分の 1 の短い横線が、脚注領域の上端の下に引かれる
            string content = PdfChecker.DecodedStreams(bytes).First(c => c.StartsWith("1 0 0 -1 0 "));
            double lineY = ContentBottom - 20.5 + 6 + 0.25;
            var line = Regex.Match(content, @"(\S+) (\S+) m (\S+) (\S+) l\n");
            Assert.True(line.Success);
            Assert.Equal(Margin, double.Parse(line.Groups[1].Value, CultureInfo.InvariantCulture), 3);
            Assert.Equal(lineY, double.Parse(line.Groups[2].Value, CultureInfo.InvariantCulture), 3);
            Assert.Equal(Margin + 320 / 3.0, double.Parse(line.Groups[3].Value, CultureInfo.InvariantCulture), 2);
        }

        [Fact]
        public void Separator_CanBeChangedOrRemoved()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("本文").AddFootnote("脚注");
            doc.FootnoteSeparator = new PdfPen(PdfColor.FromRgb(255, 0, 0), 2);
            doc.FootnoteSeparatorLength = 50;
            string content = PdfChecker.DecodedStreams(doc.ToArray()).First(c => c.StartsWith("1 0 0 -1 0 "));
            Assert.Contains("1 0 0 RG", content);
            Assert.Contains("2 w", content);
            var line = Regex.Match(content, @"(\S+) (\S+) m (\S+) (\S+) l\n");
            Assert.Equal(Margin + 50, double.Parse(line.Groups[3].Value, CultureInfo.InvariantCulture), 3);

            doc.FootnoteSeparator = null;
            string without = PdfChecker.DecodedStreams(doc.ToArray()).First(c => c.StartsWith("1 0 0 -1 0 "));
            Assert.DoesNotMatch(@" m \S+ \S+ l\n", without);
        }

        [Fact]
        public void MultipleFootnotes_OnOnePageAreStackedInOrder()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("甲").AddFootnote("脚注あ");
            s.AddParagraph("乙").AddFootnote("脚注い");
            s.AddParagraph("丙").AddFootnote("脚注う");
            var runs = Runs(doc.ToArray());
            var a = Find(runs, "脚注あ");
            var b = Find(runs, "脚注い");
            var c = Find(runs, "脚注う");
            Assert.Equal(14, b.Y - a.Y, 3);
            Assert.Equal(14, c.Y - b.Y, 3);
            // 3 つ分の領域（6.5 + 3 * 14）に収まり、最後の脚注が本文領域の下端にそろう
            Assert.True(c.Y <= ContentBottom && c.Y > ContentBottom - 12);
            Assert.True(a.Y > ContentBottom - (6.5 + 3 * 14));
            var markers = runs.Where(r => r.Y > 400 && r.X == Margin).Select(r => r.Text).ToList();
            Assert.Equal(new[] { "1", "2", "3" }, markers);
            // 本文は脚注があっても元の位置のまま
            Assert.Equal(Find(runs, "甲").Y + 20, Find(runs, "乙").Y, 3);
        }

        [Fact]
        public void BodyMovesToTheNextPageByTheHeightOfFootnotes()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            for (int i = 1; i <= 30; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i == 24) p.AddFootnote("二十四行目の脚注");
            }
            var runs = Runs(doc.ToArray());
            // 24 行 + 脚注領域 20.5 = 500.5 なので 25 行目は入らず、脚注なしなら入る 25・26 行目も次のページへ
            Assert.Equal(0, Find(runs, "行024").Page);
            Assert.Equal(1, Find(runs, "行025").Page);
            Assert.Equal(0, Find(runs, "二十四行目の脚注").Page);
            Assert.Equal(1, runs.Max(r => r.Page));
        }

        [Fact]
        public void ReferenceLineMovesToTheNextPageTogetherWithItsFootnote()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            for (int i = 1; i <= 30; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i == 26) p.AddFootnote("二十六行目の脚注");
            }
            var runs = Runs(doc.ToArray());
            // 26 行目は本文だけなら入るが、脚注が入らないので、参照のある行ごと次のページへ
            Assert.Equal(0, Find(runs, "行025").Page);
            Assert.Equal(1, Find(runs, "行026").Page);
            Assert.Equal(1, Find(runs, "二十六行目の脚注").Page);
            Assert.DoesNotContain(runs, r => r.Page == 0 && r.Text == "二十六行目の脚注");
        }

        [Fact]
        public void ParagraphSpanningPages_PutsTheFootnoteOnThePageOfTheReference()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var lines = string.Join("\n", Enumerable.Range(1, 30).Select(i => "段" + i.ToString("00", CultureInfo.InvariantCulture)));
            s.AddParagraph(lines).AddFootnote("段落の最後の脚注");
            var runs = Runs(doc.ToArray());
            Assert.Equal(1, runs.Max(r => r.Page));
            Assert.Equal(1, Find(runs, "段落の最後の脚注").Page);
            Assert.Equal(1, runs.First(r => r.Text == "1" && r.Y < 200 && r.X > Margin).Page);
        }

        [Fact]
        public void FootnoteInTheMiddleOfAParagraph_ReservesItsSpaceOnlyFromTheReferenceLine()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            // 参照記号は 20 行目の行。1 ページに 26 行入る余裕のうち、24 行までは脚注があっても入る
            var p = s.AddParagraph(string.Join("\n", Enumerable.Range(1, 40).Select(i => "段" + i.ToString("00", CultureInfo.InvariantCulture))));
            p.Inlines.Clear();
            for (int i = 1; i <= 40; i++)
            {
                p.AddText("段" + i.ToString("00", CultureInfo.InvariantCulture) + (i < 40 ? "\n" : ""));
                if (i == 20) p.AddFootnote("途中の脚注");
            }
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, Find(runs, "途中の脚注").Page);
            // 1 ページ目は 24 行（480 + 20.5 <= 520 だが 25 行では入らない）
            Assert.Equal(0, Find(runs, "段24").Page);
            Assert.Equal(1, Find(runs, "段25").Page);
        }

        [Fact]
        public void LongFootnote_DoesNotHangAndOverflowsInsteadOfBeingLost()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("本文").AddFootnote(string.Concat(Enumerable.Repeat("とても長い脚注の文章です。", 400)));
            s.AddParagraph("次の本文");
            byte[] bytes = doc.ToArray();
            var runs = Runs(bytes);
            Assert.Contains(runs, r => r.Text == "本文");
            Assert.Contains(runs, r => r.Text == "次の本文");
            Assert.Contains(runs, r => r.Text.StartsWith("とても長い"));
        }

        // ---- リスト・表 ----

        [Fact]
        public void ListItemFootnote_IsPlacedAtTheBottom()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddBulletList();
            list.AddItem("項目一").AddFootnote("項目の脚注");
            list.AddItem("項目二");
            var runs = Runs(doc.ToArray());
            var note = Find(runs, "項目の脚注");
            Assert.True(note.Y <= ContentBottom && note.Y > ContentBottom - 20.5);
            Assert.Single(runs, r => r.Text == "項目一");
        }

        [Fact]
        public void TableCellFootnote_IsPlacedAtTheBottomAndRowMovesWithIt()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            Lines(s, 1, 24);                       // 480 ポイント
            var table = s.AddTable();
            table.CellPadding = new Thickness(0);
            table.AddColumns(1, 1);
            var row = table.AddRow("表の一行目", "");
            row.Cells[1].AddParagraph("セル").AddFootnote("セルの脚注");
            table.AddRow("表の二行目", "右");
            var runs = Runs(doc.ToArray());
            // 表の一行目（20 ポイント、500）に脚注領域 20.5 を足すと 520.5 で入らないので、行ごと次のページへ
            Assert.Equal(1, Find(runs, "表の一行目").Page);
            Assert.Equal(1, Find(runs, "セルの脚注").Page);
            Assert.Equal(0, Find(runs, "行024").Page);
        }

        [Fact]
        public void TableCellFootnote_FitsWhenThereIsRoom()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            Lines(s, 1, 10);
            var table = s.AddTable();
            table.AddColumns(1, 1);
            table.AddRow("左", "").Cells[1].AddParagraph("右").AddFootnote("セルの脚注です");
            table.AddRow("左二", "").Cells[1].AddParagraph("右二").AddFootnote("二つ目のセルの脚注");
            var runs = Runs(doc.ToArray());
            Assert.Equal(0, runs.Max(r => r.Page));
            var first = Find(runs, "セルの脚注です");
            var second = Find(runs, "二つ目のセルの脚注");
            Assert.True(second.Y > first.Y);
            Assert.True(second.Y <= ContentBottom);
            Assert.Equal(new[] { "1", "2" }, runs.Where(r => r.Y > 400 && r.X == Margin).Select(r => r.Text).ToArray());
        }

        // ---- 段組み ----

        [Fact]
        public void TwoColumns_FootnoteSpansTheFullWidthAtTheBottom()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 2;
            s.PageSetup.ColumnSpacing = 20;
            for (int i = 1; i <= 60; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i == 5) p.AddFootnote("段をまたいで全幅に置く、二十五文字以上の長い脚注の文章です");
            }
            var runs = Runs(doc.ToArray());
            // 脚注は 1 行（全幅 320 に収まる）。段ごとの高さは 520 - 20.5 = 499.5 なので 24 行
            var note = Find(runs, "段をまたいで全幅に置く、二十五文字以上の長い脚注の文章です");
            Assert.True(note.Y <= ContentBottom && note.Y > ContentBottom - 20.5);
            Assert.Equal(0, Find(runs, "行024").Page);
            Assert.Equal(0, Find(runs, "行048").Page);
            Assert.Equal(1, Find(runs, "行049").Page);
            Assert.Equal(Margin, Find(runs, "行001").X, 3);
            Assert.Equal(Margin + 150 + 20, Find(runs, "行025").X, 3);
            // 2 段目の末尾の行も、脚注の上に収まる
            Assert.True(Find(runs, "行048").Y < note.Y - 12);
        }

        [Fact]
        public void TwoColumns_FootnoteInTheSecondColumnCannotOverlapAFullFirstColumn()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 2;
            s.PageSetup.ColumnSpacing = 20;
            for (int i = 1; i <= 60; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i == 30) p.AddFootnote("二段目の脚注");
            }
            var runs = Runs(doc.ToArray());
            // 1 段目が 26 行で埋まっているので、2 段目に脚注は置けない。脚注のある行は次のページへ
            Assert.Equal(0, Find(runs, "行029").Page);
            Assert.Equal(1, Find(runs, "行030").Page);
            Assert.Equal(1, Find(runs, "二段目の脚注").Page);
            Assert.Equal(Margin, Find(runs, "行030").X, 3);
        }

        [Fact]
        public void TwoColumns_FootnotesInBothColumnsGatherAtTheBottom()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 2;
            s.PageSetup.ColumnSpacing = 20;
            s.PageSetup.ColumnSeparator = new PdfPen(PdfColor.Black, 0.5);
            for (int i = 1; i <= 50; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i == 3) p.AddFootnote("左の段の脚注");
                if (i == 30) p.AddFootnote("右の段の脚注");
            }
            byte[] bytes = doc.ToArray();
            var runs = Runs(bytes);
            var left = Find(runs, "左の段の脚注");
            var right = Find(runs, "右の段の脚注");
            Assert.Equal(14, right.Y - left.Y, 3);
            Assert.Equal(left.X, right.X, 3);
            Assert.True(right.Y <= ContentBottom);
            Assert.Equal(new[] { "1", "2" }, runs.Where(r => r.Y > 525 && r.X == Margin).Select(r => r.Text).ToArray());

            // 2 つの脚注（6.5 + 28）の分、段の境界線は短くなる
            string content = PdfChecker.DecodedStreams(bytes).First(c => c.StartsWith("1 0 0 -1 0 "));
            var vertical = Regex.Matches(content, @"(\S+) (\S+) m (\S+) (\S+) l\n").Cast<Match>()
                .First(m => m.Groups[1].Value == m.Groups[3].Value);
            double top = double.Parse(vertical.Groups[2].Value, CultureInfo.InvariantCulture);
            double bottom = double.Parse(vertical.Groups[4].Value, CultureInfo.InvariantCulture);
            Assert.Equal(520 - (6.5 + 28), bottom - top, 3);
        }

        // ---- 番号 ----

        [Fact]
        public void Numbering_ContinuesAcrossSectionsOrRestarts()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.AddSection().AddParagraph("第一").AddFootnote("一の脚注");
            var second = doc.AddSection();
            second.AddParagraph("第二").AddFootnote("二の脚注");
            var third = doc.AddSection();
            third.FootnoteNumbering = FootnoteNumbering.Restart;
            third.AddParagraph("第三").AddFootnote("三の脚注");
            third.AddParagraph("第四").AddFootnote("四の脚注");
            var runs = Runs(doc.ToArray());
            string MarkerOf(string note) => runs.Last(r => r.Page == Find(runs, note).Page && r.Y == Find(runs, note).Y && r.X == Margin).Text;
            Assert.Equal("1", MarkerOf("一の脚注"));
            Assert.Equal("2", MarkerOf("二の脚注"));
            Assert.Equal("1", MarkerOf("三の脚注"));
            Assert.Equal("2", MarkerOf("四の脚注"));
        }

        [Fact]
        public void NumberFormat_AndSymbols()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.FootnoteNumberFormat = PageNumberFormat.LowerRoman;
            var s = doc.AddSection();
            for (int i = 0; i < 4; i++) s.AddParagraph("文" + i).AddFootnote("脚注" + i);
            var runs = Runs(doc.ToArray());
            Assert.Equal(new[] { "i", "ii", "iii", "iv" }, runs.Where(r => r.Y > 400 && r.X == Margin).Select(r => r.Text).ToArray());

            doc.FootnoteSymbols = new[] { "*", "†" };
            runs = Runs(doc.ToArray());
            Assert.Equal(new[] { "*", "†", "**", "††" }, runs.Where(r => r.Y > 400 && r.X == Margin).Select(r => r.Text).ToArray());
        }

        [Fact]
        public void MultiParagraphFootnote_AndListInsideFootnote()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var note = s.AddParagraph("本文").AddFootnote();
            note.AddParagraph("脚注の一段落目");
            note.AddParagraph("脚注の二段落目");
            note.AddBulletList().AddItem("脚注の中の項目");
            var runs = Runs(doc.ToArray());
            var one = Find(runs, "脚注の一段落目");
            var two = Find(runs, "脚注の二段落目");
            var item = Find(runs, "脚注の中の項目");
            Assert.Equal(12, two.Y - one.Y, 3);
            Assert.Equal(12, item.Y - two.Y, 3);
            Assert.Equal(one.X, two.X, 3);
            Assert.True(item.Y <= ContentBottom);
        }

        // ---- スタイル ----

        [Fact]
        public void Style_DefaultIsSmallerThanBody_AndCanBeChanged()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("本文").AddFootnote("脚注の文字");
            var runs = Runs(doc.ToArray());
            Assert.True(Find(runs, "脚注の文字").Size < Find(runs, "本文").Size);

            doc.FootnoteStyle = new TextStyle { FontSize = 7, Color = PdfColor.FromRgb(255, 0, 0) };
            byte[] bytes = doc.ToArray();
            runs = Runs(bytes);
            Assert.Equal(7, Find(runs, "脚注の文字").Size, 6);
            Assert.Contains("1 0 0 rg", PdfChecker.DecodedStreams(bytes).First(c => c.StartsWith("1 0 0 -1 0 ")));

            // 段落書式（ここでは行送り）と、脚注の段落自身のスタイルの指定
            doc.FootnoteFormat = new ParagraphFormat { LineSpacingRule = LineSpacingRule.Exactly, LineSpacing = 30 };
            runs = Runs(doc.ToArray());
            var note = Find(runs, "脚注の文字");
            Assert.True(note.Y > ContentBottom - 30 - 6.5 - 2 && note.Y <= ContentBottom);
        }

        [Fact]
        public void ReferenceMarkStyleAndFootnoteTextStyleOverrides()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var p = s.AddParagraph("本文");
            p.AddFootnote("太字の脚注", new TextStyle { FontSize = 6 });
            var runs = Runs(doc.ToArray());
            Assert.Equal(6, Find(runs, "太字の脚注").Size, 6);
        }

        // ---- 使えない場所 ----

        [Fact]
        public void FootnoteInHeaderOrFooter_Throws()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("本文");
            s.Footer.AddParagraph("フッター").AddFootnote("だめ");
            var ex = Assert.Throws<InvalidOperationException>(() => doc.ToArray());
            Assert.Contains("ヘッダー/フッター", ex.Message);
        }

        [Fact]
        public void FootnoteInsideAFootnote_Throws()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var note = s.AddParagraph("本文").AddFootnote();
            note.AddParagraph("脚注").AddFootnote("入れ子");
            var ex = Assert.Throws<InvalidOperationException>(() => doc.ToArray());
            Assert.Contains("入れ子", ex.Message);
        }

        // ---- 目次・再レイアウト ----

        [Fact]
        public void Layout_IsRepeatable_AndNoFootnotesLeavesOutputUnchanged()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            for (int i = 1; i <= 60; i++)
            {
                var p = s.AddParagraph("行" + i.ToString("000", CultureInfo.InvariantCulture));
                if (i % 7 == 0) p.AddFootnote("脚注" + i);
            }
            Assert.Equal(doc.ToArray(), doc.ToArray());

            var plain = NewDoc()!;
            Lines(plain.AddSection(), 1, 60);
            var withDefaults = NewDoc()!;
            withDefaults.FootnoteStyle = new TextStyle { FontSize = 20 };
            withDefaults.FootnoteSeparator = null;
            Lines(withDefaults.AddSection(), 1, 60);
            Assert.Equal(plain.ToArray(), withDefaults.ToArray());
        }

        [Fact]
        public void TableOfContents_ConvergesWithFootnotesAndNumbersAgree()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddTableOfContents("目次", 2);
            string[] chapters = { "第一章", "第二章", "第三章", "第四章" };
            int n = 0;
            foreach (string chapter in chapters)
            {
                var h = s.AddParagraph(chapter).SetOutline(1).SetBold().SetPageBreakBefore();
                h.AddFootnote(chapter + "の脚注");
                n++;
                // 脚注の分だけ本文が次のページへ送られ、ページ数が変わる
                for (int i = 1; i <= 23; i++)
                {
                    var p = s.AddParagraph(chapter + "の" + i.ToString("00", CultureInfo.InvariantCulture) + "行");
                    if (i % 8 == 0) p.AddFootnote(chapter + "-" + i + "の脚注");
                }
            }
            byte[] bytes = doc.ToArray();
            Assert.Equal(bytes, doc.ToArray());
            var pdf = new PdfInspector(bytes);
            var pages = pdf.PageRuns();
            var runs = Runs(bytes);

            // 目次のページ番号は、見出しの実際のページと一致する
            var toc = runs.Where(r => r.Page == 0).Select(r => r.Text).ToList();
            foreach (string chapter in chapters)
            {
                int actual = pages.FindIndex(1, p => p.Contains(chapter) && p.Count > 0 && p[0] == chapter) + 1;
                Assert.True(actual > 1, chapter + " の見出しが見つかりません");
                int at = toc.IndexOf(chapter);
                Assert.True(at >= 0, "目次に " + chapter + " がありません");
                Assert.Equal(actual.ToString(CultureInfo.InvariantCulture), toc[at + 1]);
            }

            // しおりと目次の見出しに、脚注の番号は混ざらない
            Assert.Equal(chapters, pdf.Outlines().Select(o => o.Title).ToArray());

            // 脚注の番号は文書の並びで通しになる（ページ数が変わっても同じ）
            var notes = runs.Where(r => r.X == Margin && r.Y > 400 && r.Page > 0 && r.Text.Length > 0 && char.IsDigit(r.Text[0])).Select(r => int.Parse(r.Text)).ToList();
            Assert.Equal(Enumerable.Range(1, notes.Count), notes);
            Assert.True(notes.Count >= 8);
            Assert.Equal(chapters.Length * (1 + 2), notes.Count);
        }

        [Fact]
        public void FootnotePageRef_InsideFootnoteWorks()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var note = s.AddParagraph("本文").AddFootnote();
            note.AddParagraph("参照: ").AddPageRef("end");
            s.AddPageBreak();
            s.AddParagraph("最後").SetAnchor("end");
            var pages = new PdfInspector(doc.ToArray()).PageRuns();
            Assert.Contains("参照: 2", pages[0]);
        }
    }
}
