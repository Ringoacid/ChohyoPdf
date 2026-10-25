using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ChohyoPdf.Text;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>表の列幅を内容に合わせる機能（Column.AutoFit、Table.AddAutoColumn、Table.AutoFitColumns）のテスト。</summary>
    public class TableAutoWidthTests
    {
        // 用紙 400 x 600、余白 40。本文の幅は 320。
        // 文字は 10 ポイントの等幅（全角 10、半角 5）。行送りは 20 ポイントの固定。
        private const double Margin = 40;
        private const double Content = 320;

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

        private static TextRunInfo Find(List<TextRunInfo> runs, string text)
        {
            var found = runs.Where(r => r.Text == text).ToList();
            Assert.True(found.Count == 1, "文字列 '" + text + "' が " + found.Count + " 件");
            return found[0];
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

        /// <summary>罫線と余白のない表（文字の位置が列の幅そのままになる）。</summary>
        private static Table PlainTable(Document doc)
        {
            var t = doc.AddSection().AddTable();
            t.Borders = null;
            t.CellPadding = new Thickness(0);
            return t;
        }

        // ---- 設定 ----

        [Fact]
        public void Column_WidthModeFollowsSettings()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = doc.AddSection().AddTable();
            var fixedColumn = t.AddColumn(50);
            var relative = t.AddRelativeColumn(2);
            var auto = t.AddAutoColumn();
            Assert.Equal(ColumnWidthMode.Fixed, fixedColumn.WidthMode);
            Assert.Equal(ColumnWidthMode.Relative, relative.WidthMode);
            Assert.Equal(ColumnWidthMode.Auto, auto.WidthMode);
            Assert.True(auto.AutoFit);
            Assert.False(fixedColumn.AutoFit);

            // 既存の設定の意味は変わらない
            Assert.Equal(50, fixedColumn.Width);
            Assert.Null(relative.Width);
            Assert.Equal(2, relative.RelativeWidth);
            Assert.Null(auto.Width);

            relative.AutoFit = true;
            Assert.Equal(ColumnWidthMode.Auto, relative.WidthMode);
            Assert.False(t.AutoFitColumns);
        }

        [Fact]
        public void AddAutoColumns_AddsColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = doc.AddSection().AddTable();
            Assert.Same(t, t.AddAutoColumns(3));
            Assert.Equal(3, t.Columns.Count);
            Assert.All(t.Columns, c => Assert.True(c.AutoFit));
            Assert.Throws<ArgumentOutOfRangeException>(() => t.AddAutoColumns(-1));
        }

        // ---- 幅の配分 ----

        [Fact]
        public void AllAuto_FitsContent_UsesMaxWidths()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(3);
            t.AddRow("あい", "うえおか", "AB");     // 20, 40, 10
            t.AddRow("あ", "う", "ABCD");           // 10, 10, 20
            var runs = Runs(doc.ToArray());

            // 表は内容の幅（20 + 40 + 20 = 80）になる
            Assert.Equal(Margin, Find(runs, "あい").X, 3);
            Assert.Equal(Margin + 20, Find(runs, "うえおか").X, 3);
            Assert.Equal(Margin + 60, Find(runs, "AB").X, 3);
            Assert.Equal(Margin, Find(runs, "あ").X, 3);
            Assert.Equal(Margin + 60, Find(runs, "ABCD").X, 3);
        }

        [Fact]
        public void AllAuto_NarrowTable_FollowsAlignment()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.Alignment = HorizontalAlignment.Right;
            t.AddAutoColumns(2);
            t.AddRow("あい", "うえ");               // 20 + 20 = 40
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + Content - 40, Find(runs, "あい").X, 3);
            Assert.Equal(Margin + Content - 20, Find(runs, "うえ").X, 3);
        }

        [Fact]
        public void AllAuto_OverflowShrinksProportionallyToMaxWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            // 最大幅は 600 と 600（合計 1200 > 320）。最小幅は 10 と 10。比例配分で 160 ずつ
            string a = new string('あ', 60);
            string b = new string('い', 60);
            t.AddRow(a, b);
            var runs = Runs(doc.ToArray());
            var second = runs.First(r => r.Text.StartsWith("い", StringComparison.Ordinal));
            Assert.Equal(Margin + 160, second.X, 3);
            // 160 の幅には 16 文字入るので、60 文字は 4 行になる
            Assert.Equal(4, runs.Count(r => r.Text.StartsWith("あ", StringComparison.Ordinal)));
        }

        [Fact]
        public void AllAuto_WidthsAreProportionalToMax_ClampedToMin()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            // 1 列目は折り返せない英単語（最小 = 最大 = 100）、2 列目は長い和文（最大 1000、最小 10）
            t.AddRow(new string('A', 20), new string('か', 100));
            var runs = Runs(doc.ToArray());
            // 最大幅の比例配分（100 : 1000 → 320 を 29 : 291）では 1 列目が最小幅 100 を下回るので、100 にして残り 220 を 2 列目に配る
            Assert.Equal(Margin + 100, runs.First(r => r.Text.StartsWith("か", StringComparison.Ordinal)).X, 3);
            Assert.Equal(Margin, Find(runs, new string('A', 20)).X, 3);
            // 2 列目は 220 の幅に 22 文字入る
            Assert.Equal(5, runs.Count(r => r.Text.StartsWith("か", StringComparison.Ordinal)));
        }

        [Fact]
        public void AllAuto_MinWidthsDoNotFit_ScaledToTable()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            // 折り返せない英単語が 2 つ（各 200）。最小幅の合計 400 が本文の幅 320 を超えるので、同じ比率で縮めて 160 ずつにする
            t.AddRow(new string('A', 40), new string('B', 40));
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 160, runs.First(r => r.Text.StartsWith("B", StringComparison.Ordinal)).X, 3);
        }

        [Fact]
        public void MinWidth_FollowsLineBreakingRules()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            // 1 列目: 英単語 "abc defgh"（最小は最長の単語 "defgh" = 25、最大は 45）。2 列目: 長い和文
            // 2 列目の最大幅は 1000 なので、1 列目は最大幅の比例配分（45 : 1000）では 14 になり、最小幅 25 で止まる
            t.AddRow("abc defgh", new string('あ', 100));
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 25, runs.First(r => r.Text.StartsWith("あ", StringComparison.Ordinal)).X, 3);
            // "abc" と "defgh" は 2 行に分かれる
            Assert.Equal(Margin, Find(runs, "abc").X, 3);
            Assert.Equal(Margin, Find(runs, "defgh").X, 3);
        }

        [Fact]
        public void MinWidth_KeepsLineStartProhibitedCharactersWithPreviousCharacter()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            // 1 列目の「い。」は行頭禁則で分けられない（最小 20）。「あ」と「い。」の間は改行できる
            t.AddRow("あい。", new string('か', 100));
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 20, runs.First(r => r.Text.StartsWith("か", StringComparison.Ordinal)).X, 3);
            Assert.Equal(Margin, Find(runs, "あ").X, 3);
            Assert.Equal(Margin, Find(runs, "い。").X, 3);
        }

        [Fact]
        public void MinWidth_UsesLongestHardLineForMax()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            t.AddRow("あ\nいうえ", "X");           // 最大 = 30（改行で区切った長い方）
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 30, Find(runs, "X").X, 3);
        }

        // ---- 余白・罫線・結合 ----

        [Fact]
        public void PaddingAndBorders_AreIncluded()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = doc.AddSection().AddTable();
            t.Borders = new BorderLine(PdfColor.Black, 2);       // 左右の罫線は幅の半分（1 + 1）ずつ
            t.CellPadding = new Thickness(5, 0, 7, 0);
            t.AddAutoColumns(2);
            t.AddRow("あい", "うえ");
            var runs = Runs(doc.ToArray());
            // 1 列目の幅 = 20 + 5 + 7 + (1 + 1) = 34。2 列目の文字は、その右の余白 5 だけ内側
            Assert.Equal(Margin + 5, Find(runs, "あい").X, 3);
            Assert.Equal(Margin + 34 + 5, Find(runs, "うえ").X, 3);
        }

        [Fact]
        public void ColumnSpan_SharesLackAmongAutoColumns()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(3);
            var wide = t.AddRow("あいうえおかきくけこ", null!, "Z");      // 100 を 2 列にまたがる
            wide[0].Merge(2);
            t.AddRow("a", "b", "c");                                       // 1 列 5、5、5
            var runs = Runs(doc.ToArray());
            // 結合したセルに必要な 100 に対し、1・2 列目の合計が 10 なので、足りない 90 を最大幅の比（1 : 1）で配る → 50 ずつ
            Assert.Equal(Margin + 50, Find(runs, "b").X, 3);
            Assert.Equal(Margin + 100, Find(runs, "Z").X, 3);
        }

        [Fact]
        public void ColumnSpan_OverFixedAndAutoColumns_OnlyAutoGrows()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddColumn(30);
            t.AddAutoColumn();
            t.AddColumn(10);
            var wide = t.AddRow("あいうえおかきくけこ", null!, null!);    // 100 を 3 列にまたがる
            wide[0].Merge(3);
            t.AddRow("a", "b", "c");
            var runs = Runs(doc.ToArray());
            // 30 + 自動の列 + 10 が 100 になる → 自動の列は 60
            Assert.Equal(Margin + 30, Find(runs, "b").X, 3);
            Assert.Equal(Margin + 90, Find(runs, "c").X, 3);
        }

        // ---- 固定幅・比率との混在 ----

        [Fact]
        public void FixedAndAuto_AutoUsesRemainingWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddColumn(50);
            t.AddAutoColumn();
            t.AddAutoColumn();
            t.AddRow("固定", "あい", "うえ");
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 50, Find(runs, "あい").X, 3);
            Assert.Equal(Margin + 70, Find(runs, "うえ").X, 3);

            // 内容が残りの幅に収まらないときは、固定幅の列を除いた残り（270）で縮める
            var doc2 = NewDoc()!;
            var t2 = PlainTable(doc2);
            t2.AddColumn(50);
            t2.AddAutoColumn();
            t2.AddAutoColumn();
            t2.AddRow("固定", new string('あ', 100), new string('い', 100));
            var second = Runs(doc2.ToArray()).First(r => r.Text.StartsWith("い", StringComparison.Ordinal));
            Assert.Equal(Margin + 50 + 135, second.X, 3);
        }

        [Fact]
        public void AutoAndRelative_RelativeSharesWhatAutoLeaves()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumn();
            t.AddRelativeColumn(1);
            t.AddRelativeColumn(3);
            t.AddRow("あいうえ", "B", "C");           // 自動の列は 40。残り 280 を 1 : 3 で分ける → 70, 210
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 40, Find(runs, "B").X, 3);
            Assert.Equal(Margin + 40 + 70, Find(runs, "C").X, 3);
        }

        [Fact]
        public void AutoAndRelative_RelativeKeepsItsMinWidthWhenAutoWantsTooMuch()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumn();
            t.AddRelativeColumn(1);
            // 自動の列は最大 1000。比率の列の最小幅は「ABCDEFGHIJ」の 50
            t.AddRow(new string('あ', 100), "ABCDEFGHIJ");
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + Content - 50, Find(runs, "ABCDEFGHIJ").X, 3);
        }

        [Fact]
        public void AutoFitColumns_TreatsRelativeColumnsAsAuto_KeepsFixed()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AutoFitColumns = true;
            t.AddColumn(100);
            t.AddRelativeColumn(5);            // 比率は使わず、内容に合わせる
            t.AddRelativeColumn(1);
            t.AddRow("固定", "あい", "うえ");
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 100, Find(runs, "あい").X, 3);
            Assert.Equal(Margin + 120, Find(runs, "うえ").X, 3);
        }

        [Fact]
        public void WithoutAutoColumns_LayoutIsUnchanged()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddColumn(50);
            t.AddRelativeColumn(1);
            t.AddRelativeColumn(3);
            t.AddRow("固定", "B", "C");
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 50, Find(runs, "B").X, 3);
            Assert.Equal(Margin + 50 + 67.5, Find(runs, "C").X, 3);
        }

        // ---- 内容の種類 ----

        [Fact]
        public void NestedTable_And_List_AreMeasured()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            var row = t.AddRow("", "X");
            var inner = row[0].AddTable();
            inner.Borders = null;
            inner.CellPadding = new Thickness(0);
            inner.AddColumn(30);
            inner.AddAutoColumn();
            inner.AddRow("a", "あいうえ");            // 30 + 40 = 70
            var runs = Runs(doc.ToArray());
            Assert.Equal(Margin + 70, Find(runs, "X").X, 3);

            var doc2 = NewDoc()!;
            var t2 = PlainTable(doc2);
            t2.AddAutoColumns(2);
            var row2 = t2.AddRow("", "Y");
            var list = row2[0].AddBulletList("-");
            list.MarkerWidth = 10;
            list.MarkerGap = 5;
            list.AddItem("あいうえお");                // 記号の列 15 + 50 = 65
            Assert.Equal(Margin + 65, Find(Runs(doc2.ToArray()), "Y").X, 3);
        }

        [Fact]
        public void Image_UsesNaturalWidth_AndRuby_IsMeasured()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            var row = t.AddRow("", "X");
            row[0].AddImage(PdfImage.FromBytes(TestData.Bytes("rgb8.png")), width: 60);
            Assert.Equal(Margin + 60, Find(Runs(doc.ToArray()), "X").X, 3);

            // ルビで広げた親文字の幅も数える（親文字 1 文字 + ルビ 4 文字: ルビは文字サイズの半分で 4 × 5 = 20）
            var doc2 = NewDoc()!;
            var t2 = PlainTable(doc2);
            t2.AddAutoColumns(2);
            var row2 = t2.AddRow("", "Z");
            row2[0].AddParagraph().AddRuby("漢", "かんじよ");
            var z = Find(Runs(doc2.ToArray()), "Z");
            Assert.Equal(Margin + 20, z.X, 3);
        }

        [Fact]
        public void IndentsAndParagraphPadding_AreIncluded()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            var row = t.AddRow("", "X");
            row[0].AddParagraph("あい").SetIndent(10, 0, 5);          // 20 + 左 10 + 右 5
            Assert.Equal(Margin + 35, Find(Runs(doc.ToArray()), "X").X, 3);
        }

        [Fact]
        public void Tabs_AreCountedAsDefaultWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            var row = t.AddRow("", "X");
            row[0].AddParagraph("A\tB");                                // 5 + 20（空白 4 文字分）+ 5 = 30
            Assert.Equal(Margin + 30, Find(Runs(doc.ToArray()), "X").X, 3);
        }

        // ---- 安定性 ----

        [Fact]
        public void LayoutIsDeterministic_AndHeaderRowsRepeatWithSameWidths()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            t.AddHeaderRow("見出し一", "見出し二");
            for (int i = 0; i < 60; i++) t.AddRow("行" + i.ToString("000", CultureInfo.InvariantCulture), "値" + i.ToString("000", CultureInfo.InvariantCulture));
            var first = doc.ToArray();
            var second = doc.ToArray();
            Assert.Equal(first, second);

            var runs = Runs(first);
            Assert.True(PdfChecker.CountPages(first) >= 2);
            var headers = runs.Where(r => r.Text == "見出し二").ToList();
            Assert.True(headers.Count >= 2);
            Assert.All(headers, h => Assert.Equal(headers[0].X, h.X, 6));
            // ページが変わっても、列の位置は同じ
            var values = runs.Where(r => r.Text.StartsWith("値", StringComparison.Ordinal)).ToList();
            Assert.All(values, v => Assert.Equal(headers[0].X, v.X, 6));
        }

        [Fact]
        public void MeasuringDoesNotDisturbTextBlockLayout()
        {
            var font = Mono();
            if (font == null) return;
            var block = TextBlock.Build("abc defgh\nあいう", ResolvedStyle.From(new TextStyle(font, 10)));
            block.Layout(20, 0, LineSpacingRule.Multiple, 1.5);
            int lines = block.Lines.Count;
            var widths = block.Lines.Select(l => l.Width).ToList();

            block.MeasureWidths(0, out double min, out double max);
            Assert.Equal(25, min, 6);          // defgh
            Assert.Equal(45, max, 6);          // abc defgh
            Assert.Equal(lines, block.Lines.Count);
            Assert.Equal(widths, block.Lines.Select(l => l.Width).ToList());

            // 1 行目のインデントは、最大幅と、1 行目の最初の単位（"abc"）に加わる
            block.MeasureWidths(8, out double minIndented, out double maxIndented);
            Assert.Equal(25, minIndented, 6);   // max(15 + 8, 25)
            Assert.Equal(53, maxIndented, 6);
            block.MeasureWidths(12, out minIndented, out _);
            Assert.Equal(27, minIndented, 6);   // 15 + 12
            block.MeasureWidths(0, out double again, out _);
            Assert.Equal(min, again, 6);
        }

        [Fact]
        public void EmptyAndHardBreakOnlyText_AreMeasuredAsZero()
        {
            var font = Mono();
            if (font == null) return;
            var empty = TextBlock.Build("", ResolvedStyle.From(new TextStyle(font, 10)));
            empty.MeasureWidths(0, out double min, out double max);
            Assert.Equal(0, min);
            Assert.Equal(0, max);
            var breaks = TextBlock.Build("\n\n", ResolvedStyle.From(new TextStyle(font, 10)));
            breaks.MeasureWidths(0, out min, out max);
            Assert.Equal(0, min);
            Assert.Equal(0, max);
        }

        [Fact]
        public void Footnotes_InAutoWidthTable_StillWork()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = PlainTable(doc);
            t.AddAutoColumns(2);
            var row = t.AddRow("", "本文");
            row[0].AddParagraph("あい").AddFootnote("脚注の本文");
            var runs = Runs(doc.ToArray());
            Assert.Contains(runs, r => r.Text == "脚注の本文");
            Assert.True(Find(runs, "脚注の本文").Y > Find(runs, "本文").Y);
        }

        [Fact]
        public void AutoWidthTable_InColumnSection_FitsColumn()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.PageSetup.ColumnCount = 2;
            s.PageSetup.ColumnSpacing = 20;
            var t = s.AddTable();
            t.Borders = null;
            t.CellPadding = new Thickness(0);
            t.AddAutoColumns(2);
            t.AddRow(new string('あ', 40), new string('い', 40));       // 最大幅 400 ずつ。段の幅は 150 なので 75 ずつ
            var runs = Runs(doc.ToArray());
            var second = runs.First(r => r.Text.StartsWith("い", StringComparison.Ordinal));
            Assert.Equal(Margin + 75, second.X, 3);
        }

        [Fact]
        public void EmptyColumn_GetsOnlyPadding()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var t = doc.AddSection().AddTable();
            t.Borders = null;
            t.CellPadding = new Thickness(4, 0, 4, 0);
            t.AddAutoColumns(3);
            t.AddRow("あ", null!, "い");
            var runs = Runs(doc.ToArray());
            // 幅は 10 + 8、0 + 8、10 + 8
            Assert.Equal(Margin + 4, Find(runs, "あ").X, 3);
            Assert.Equal(Margin + 18 + 8 + 4, Find(runs, "い").X, 3);
        }
    }
}
