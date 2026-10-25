using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ChohyoPdf.Text;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>上付き・下付き文字と、タブ位置・リーダーのテスト。</summary>
    public class SuperscriptAndTabTests
    {
        /// <summary>全角 1em・半角 0.5em の等幅フォント（MS ゴシック）。</summary>
        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        /// <summary>文字列を描いた位置（Tm の x, ベースラインの y）と文字サイズ（Tf）。</summary>
        private sealed class Run
        {
            public double X;
            public double Y;
            public double Size;
        }

        private static List<Run> Runs(PdfDocument pdf)
        {
            pdf.CompressContent = false;
            string s = PdfChecker.Latin1(pdf.ToArray());
            var list = new List<Run>();
            foreach (Match m in Regex.Matches(s, @"/F\d+ (\S+) Tf\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm"))
            {
                list.Add(new Run
                {
                    Size = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    X = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                    Y = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                });
            }
            return list;
        }

        private static string Content(PdfDocument pdf)
        {
            pdf.CompressContent = false;
            return PdfChecker.Latin1(pdf.ToArray());
        }

        /// <summary>ブロックを (0, 0) に描いたときの文字列の位置を返す。</summary>
        private static List<Run> DrawBlock(PdfFont font, string text, double width, double firstIndent, TabLayout? tabs,
            TextAlignment align = TextAlignment.Left, double size = 10)
        {
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var block = TextBlock.Build(text, ResolvedStyle.From(new TextStyle(font, size)));
            block.Layout(width, firstIndent, LineSpacingRule.Multiple, 1.0, tabs);
            block.Draw(g, 0, 0, width, align, 0, block.Lines.Count);
            return Runs(pdf);
        }

        // ---- 上付き・下付き ----

        [Fact]
        public void BaselineShift_CloneAndMerge()
        {
            var style = new TextStyle { BaselineShift = BaselineShift.Superscript };
            Assert.Equal(BaselineShift.Superscript, style.Clone().BaselineShift);
            Assert.Equal(BaselineShift.Superscript, new TextStyle { Bold = true }.Merge(style).BaselineShift);
            // 未指定は親を引き継ぎ、None は親の上付きを打ち消す
            Assert.Equal(BaselineShift.Superscript, style.Merge(new TextStyle { Bold = true }).BaselineShift);
            Assert.Equal(BaselineShift.None, style.Merge(new TextStyle { BaselineShift = BaselineShift.None }).BaselineShift);
        }

        [Fact]
        public void Superscript_ShrinksAndRaisesBaseline()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            g.DrawText("A", 0, 0, new TextStyle(font, 10));
            g.DrawText("A", 0, 0, new TextStyle(font, 10) { BaselineShift = BaselineShift.Superscript });
            g.DrawText("A", 0, 0, new TextStyle(font, 10) { BaselineShift = BaselineShift.Subscript });
            var runs = Runs(pdf);
            Assert.Equal(3, runs.Count);
            Assert.Equal(10, runs[0].Size, 3);
            Assert.Equal(6.5, runs[1].Size, 3);
            Assert.Equal(6.5, runs[2].Size, 3);
            Assert.Equal(runs[0].Y - 3.4, runs[1].Y, 2);   // 上付きは上へ（y は下向きが正）
            Assert.Equal(runs[0].Y + 1.2, runs[2].Y, 2);   // 下付きは下へ
        }

        [Fact]
        public void Superscript_WidthIsScaled()
        {
            var font = Mono();
            if (font == null) return;
            double normal = PdfGraphics.MeasureText("AB", new TextStyle(font, 10));
            double sup = PdfGraphics.MeasureText("AB", new TextStyle(font, 10) { BaselineShift = BaselineShift.Superscript });
            Assert.Equal(10, normal, 3);
            Assert.Equal(6.5, sup, 3);
        }

        [Fact]
        public void Superscript_DoesNotChangeLineHeight()
        {
            var font = Mono();
            if (font == null) return;
            var normalStyle = new TextStyle(font, 10);
            var supStyle = new TextStyle(font, 10) { BaselineShift = BaselineShift.Superscript };
            var subStyle = new TextStyle(font, 10) { BaselineShift = BaselineShift.Subscript };
            double h = PdfGraphics.MeasureTextHeight("m2", normalStyle, 100, 1.5);
            var segments = new List<KeyValuePair<string, ResolvedStyle>>
            {
                new KeyValuePair<string, ResolvedStyle>("m", ResolvedStyle.From(normalStyle)),
                new KeyValuePair<string, ResolvedStyle>("2", ResolvedStyle.From(supStyle)),
                new KeyValuePair<string, ResolvedStyle>("2", ResolvedStyle.From(subStyle)),
            };
            var block = TextBlock.Build(segments, ResolvedStyle.From(normalStyle));
            block.Layout(100, 0, LineSpacingRule.Multiple, 1.5);
            Assert.Equal(h, block.TotalHeight, 6);
            Assert.Single(block.Lines);

            // 上付きだけの行も、通常の行と同じ高さ・同じベースライン位置
            var onlySup = TextBlock.Build("2", ResolvedStyle.From(supStyle));
            onlySup.Layout(100, 0, LineSpacingRule.Multiple, 1.5);
            Assert.Equal(h, onlySup.TotalHeight, 6);
            var normal = TextBlock.Build("2", ResolvedStyle.From(normalStyle));
            normal.Layout(100, 0, LineSpacingRule.Multiple, 1.5);
            Assert.Equal(normal.Lines[0].Baseline, onlySup.Lines[0].Baseline, 6);
        }

        [Fact]
        public void Superscript_InParagraph()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var p = doc.AddSection().AddParagraph("E = mc");
            p.AddSuperscript("2");
            p.AddText(" H");
            p.AddSubscript("2", new TextStyle { Color = PdfColor.Red });
            p.AddText("O");
            var runs = Runs(doc.Render());
            Assert.Contains(runs, r => Math.Abs(r.Size - 10) < 1e-6);
            Assert.Equal(2, runs.Count(r => Math.Abs(r.Size - 6.5) < 1e-6));
            var normalRun = runs.First(r => Math.Abs(r.Size - 10) < 1e-6);
            var sup = runs.First(r => Math.Abs(r.Size - 6.5) < 1e-6);
            Assert.True(sup.Y < normalRun.Y);
            Assert.Equal(BaselineShift.Superscript, ((TextRun)p.Inlines[1]).Style!.BaselineShift);
            Assert.Equal(BaselineShift.Subscript, ((TextRun)p.Inlines[3]).Style!.BaselineShift);
        }

        [Fact]
        public void Superscript_UnderlineFollowsShiftedBaseline()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            g.DrawText("A", 0, 0, new TextStyle(font, 10) { Underline = true });
            g.DrawText("A", 0, 0, new TextStyle(font, 10) { Underline = true, BaselineShift = BaselineShift.Superscript });
            string s = Content(pdf);
            var rects = Regex.Matches(s, @"(\S+) (\S+) (\S+) (\S+) re").Cast<Match>()
                .Select(m => new[] { 1, 2, 3, 4 }.Select(i => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray())
                .ToList();
            Assert.Equal(2, rects.Count);
            Assert.Equal(5, rects[0][2], 3);        // 通常の下線の幅
            Assert.Equal(3.25, rects[1][2], 3);     // 縮小した文字の幅
            Assert.True(rects[1][1] < rects[0][1]); // 上付きの下線は上にずれる
        }

        [Fact]
        public void Superscript_WorksInTextBoxWithShrink()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var result = g.DrawTextBox("あいうえおかきくけこ", new PdfRect(0, 0, 50, 12),
                new TextStyle(font, 10) { BaselineShift = BaselineShift.Superscript },
                new TextBoxOptions { ShrinkToFit = true, LineSpacing = 1.0 });
            Assert.False(result.Overflowed);
            Assert.Equal(1, PdfChecker.CountPages(pdf.ToArray()));
        }

        // ---- タブ ----

        [Fact]
        public void Tab_WithoutTabStopsKeepsLegacyWidth()
        {
            var font = Mono();
            if (font == null) return;
            // 従来どおり、空白 4 文字分（半角 0.5em × 4 = 20）
            var runs = DrawBlock(font, "あ\tい", 200, 0, null);
            Assert.Equal(2, runs.Count);
            Assert.Equal(10 + 20, runs[1].X, 3);
        }

        [Fact]
        public void Tab_LeftStop()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(50), new TabStop(120) }, null);
            var runs = DrawBlock(font, "あ\tい\tう", 200, 0, tabs);
            Assert.Equal(3, runs.Count);
            Assert.Equal(0, runs[0].X, 3);
            Assert.Equal(50, runs[1].X, 3);
            Assert.Equal(120, runs[2].X, 3);
        }

        [Fact]
        public void Tab_PassingTheStopUsesNextStop()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(15), new TabStop(50) }, null);
            // 「ああ」は 20 の位置で終わる。15 のタブ位置は通り過ぎているので 50 へ
            var runs = DrawBlock(font, "ああ\tい", 200, 0, tabs);
            Assert.Equal(50, runs[1].X, 3);
        }

        [Fact]
        public void Tab_DefaultInterval()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(null, 30);
            var runs = DrawBlock(font, "あ\tい\tう\t\tえ", 300, 0, tabs);
            Assert.Equal(30, runs[1].X, 3);   // あ(0-10) → 30
            Assert.Equal(60, runs[2].X, 3);   // い(30-40) → 60
            Assert.Equal(120, runs[3].X, 3);  // う(60-70) → 90 → 120

            // ちょうどタブ位置にいるときは次のタブ位置へ進む
            runs = DrawBlock(font, "あああ\tい", 300, 0, tabs);
            Assert.Equal(60, runs[1].X, 3);
        }

        [Fact]
        public void Tab_DefaultIntervalFollowsCustomStops()
        {
            var font = Mono();
            if (font == null) return;
            // 最後のタブ位置（25）より右は、既定の間隔（36）の整数倍
            var tabs = TabLayout.Create(new[] { new TabStop(25) }, null);
            var runs = DrawBlock(font, "あ\tい\tう", 300, 0, tabs);
            Assert.Equal(25, runs[1].X, 3);
            Assert.Equal(36, runs[2].X, 3);
        }

        [Fact]
        public void Tab_RightStop()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(100, TabAlignment.Right) }, null);
            var runs = DrawBlock(font, "項目\t12", 200, 0, tabs);
            // "12" は半角 2 文字（10）なので、右端が 100 になる位置
            Assert.Equal(90, runs[1].X, 3);

            // 末尾の空白は右揃えの幅に含めない
            runs = DrawBlock(font, "項目\t12  ", 200, 0, tabs);
            Assert.Equal(90, runs[1].X, 3);
        }

        [Fact]
        public void Tab_CenterStop()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(100, TabAlignment.Center) }, null);
            var runs = DrawBlock(font, "あ\tいう", 200, 0, tabs);
            Assert.Equal(90, runs[1].X, 3);   // 幅 20 の文字列（90〜110）の中心が 100
        }

        [Fact]
        public void Tab_DecimalStop()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(100, TabAlignment.Decimal) }, null);
            var runs = DrawBlock(font, "計\t123.45", 200, 0, tabs);
            // 小数点の手前の「123」（幅 15）の右端が 100
            Assert.Equal(85, runs[1].X, 3);
            // 小数点がなければ右揃えと同じ
            runs = DrawBlock(font, "計\t123", 200, 0, tabs);
            Assert.Equal(85, runs[1].X, 3);
        }

        [Fact]
        public void Tab_RightStopTooNarrowGivesZeroWidth()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(50, TabAlignment.Right) }, null);
            // 「ああああ」で 40 あり、残り 10 に「123」（15）は入らない。タブの幅は 0 で、文字は続けて並ぶ
            var runs = DrawBlock(font, "ああああ\t123", 200, 0, tabs);
            Assert.Equal(40, runs[1].X, 3);
        }

        [Fact]
        public void Tab_LeftStopBeyondLineWidthWrapsToNextLine()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(100) }, null);
            var block = TextBlock.Build("あ\tい", ResolvedStyle.From(new TextStyle(font, 10)));
            block.Layout(60, 0, LineSpacingRule.Multiple, 1.0, tabs);
            Assert.Equal(2, block.Lines.Count);
            Assert.Equal("あ", block.LineText(0));
            Assert.Equal("い", block.LineText(1));
        }

        [Fact]
        public void Tab_RightStopBeyondLineWidthIsMovedToTheRightEdge()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(100, TabAlignment.Right) }, null);
            var block = TextBlock.Build("あ\tい", ResolvedStyle.From(new TextStyle(font, 10)));
            block.Layout(60, 0, LineSpacingRule.Multiple, 1.0, tabs);
            Assert.Single(block.Lines);
            var runs = DrawBlock(font, "あ\tい", 60, 0, tabs);
            Assert.Equal(50, runs[1].X, 3);   // 行の右端（60）に「い」の右端がそろう
        }

        [Fact]
        public void Tab_HangingIndentHasImplicitStopAtLeftIndent()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(null, 36);
            // 1 行目が 20 ポイント左に出ている。「1.」(10) のあと、本文の左端（0）まで進む
            var runs = DrawBlock(font, "1.\t項目", 200, -20, tabs);
            Assert.Equal(-20, runs[0].X, 3);
            Assert.Equal(0, runs[1].X, 3);
        }

        [Fact]
        public void Tab_FirstLineIndentCountsFromTheLeftEdge()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(50) }, null);
            // 位置は本文領域の左端が基準。1 行目の字下げ（10）は位置に含まれる
            var runs = DrawBlock(font, "あ\tい", 200, 10, tabs);
            Assert.Equal(10, runs[0].X, 3);
            Assert.Equal(50, runs[1].X, 3);
        }

        [Fact]
        public void Tab_LinesWithTabsAreNotJustified()
        {
            var font = Mono();
            if (font == null) return;
            var tabs = TabLayout.Create(new[] { new TabStop(30) }, null);
            var runs = DrawBlock(font, "あ\tい\nうえお かきく", 100, 0, tabs, TextAlignment.Distribute);
            Assert.Equal(30, runs[1].X, 3);
        }

        [Fact]
        public void Tab_InvalidDefaultIntervalIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => TabLayout.Create(null, 0));
            Assert.Throws<InvalidOperationException>(() => TabLayout.Create(null, -3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabStop(double.NaN));
        }

        // ---- リーダー ----

        [Fact]
        public void TabLeader_DotsAreDrawnAsDashedRoundLine()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            g.DrawTextBox("項目\t12", new PdfRect(0, 0, 200, 20), new TextStyle(font, 10),
                new TextBoxOptions
                {
                    TabStops = new[] { new TabStop(150, TabAlignment.Right, TabLeader.Dots) },
                });
            string s = Content(pdf);
            Assert.Contains("1 J", s);               // 丸い端
            Assert.Contains("[0 3] 0 d", s);         // 長さ 0 の線を 3 ポイント間隔で
            Assert.Single(Regex.Matches(s, @"\[0 3\] 0 d"));
        }

        [Fact]
        public void TabLeader_NoneDrawsNothing()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            g.DrawTextBox("項目\t12", new PdfRect(0, 0, 200, 20), new TextStyle(font, 10),
                new TextBoxOptions { TabStops = new[] { new TabStop(150, TabAlignment.Right) } });
            string s = Content(pdf);
            Assert.DoesNotContain(" d\n", s);
            Assert.DoesNotContain(" re", s);
        }

        [Fact]
        public void TabLeader_DashesAndLine()
        {
            var font = Mono();
            if (font == null) return;
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var style = new TextStyle(font, 10);
            g.DrawTextBox("項目\t12", new PdfRect(0, 0, 200, 20), style,
                new TextBoxOptions { TabStops = new[] { new TabStop(150, TabAlignment.Right, TabLeader.Dashes) } });
            g.DrawTextBox("項目\t12", new PdfRect(0, 30, 200, 20), style,
                new TextBoxOptions { TabStops = new[] { new TabStop(150, TabAlignment.Right, TabLeader.Line) } });
            string s = Content(pdf);
            Assert.Contains("[3.5 2.5] 0 d", s);
            Assert.Single(Regex.Matches(s, @" re\n"));
        }

        [Fact]
        public void TabLeader_DotsAlignAcrossLines()
        {
            var font = Mono();
            if (font == null) return;
            // 前の文字列の長さが違っても、点はタブ位置を基準にした同じ間隔で並ぶ（縦にそろう）
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            var options = new TextBoxOptions { TabStops = new[] { new TabStop(150, TabAlignment.Right, TabLeader.Dots) } };
            g.DrawTextBox("あ\t1", new PdfRect(0, 0, 200, 20), new TextStyle(font, 10), options);
            g.DrawTextBox("あいう\t1", new PdfRect(0, 30, 200, 20), new TextStyle(font, 10), options);
            string s = Content(pdf);
            var starts = Regex.Matches(s, @"(\S+) (\S+) m (\S+) \S+ l").Cast<Match>()
                .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(2, starts.Count);
            double diff = (starts[1] - starts[0]) / 3;
            Assert.Equal(Math.Round(diff), diff, 3);   // 開始位置の差は点の間隔（3）の整数倍
        }

        // ---- ParagraphFormat / Paragraph ----

        [Fact]
        public void ParagraphFormat_TabStopsCloneAndMerge()
        {
            var a = new ParagraphFormat();
            a.AddTabStop(100, TabAlignment.Right, TabLeader.Dots).AddTabStop(40);
            Assert.Equal(new[] { 40.0, 100.0 }, a.TabStops!.Select(t => t.Position).ToArray());
            Assert.Equal(TabAlignment.Right, a.TabStops![1].Alignment);
            Assert.Equal(TabLeader.Dots, a.TabStops![1].Leader);

            var clone = a.Clone();
            clone.AddTabStop(200);
            Assert.Equal(2, a.TabStops.Count);       // 複製の変更は元に影響しない
            Assert.Equal(3, clone.TabStops!.Count);

            var parent = new ParagraphFormat { DefaultTabStop = 24 };
            parent.AddTabStop(10);
            var merged = parent.Merge(new ParagraphFormat { Alignment = TextAlignment.Right });
            Assert.Equal(24, merged.DefaultTabStop);
            Assert.Single(merged.TabStops!);                     // 未指定なら親を引き継ぐ
            merged = parent.Merge(a);
            Assert.Equal(2, merged.TabStops!.Count);             // 指定すると置き換える
            Assert.Equal(24, merged.DefaultTabStop);
        }

        [Fact]
        public void Paragraph_TableOfContentsLine()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            var doc = new Document(font, 10.5);
            var section = doc.AddSection();
            double width = section.PageSetup.ContentWidth;
            double left = section.PageSetup.Margins.Left;
            for (int i = 1; i <= 3; i++)
            {
                section.AddParagraph("第 " + i + " 章 はじめに")
                    .AddTabStop(width, TabAlignment.Right, TabLeader.Dots)
                    .AddTab()
                    .AddText((i * 10).ToString(CultureInfo.InvariantCulture));
            }
            var pdf = doc.Render();
            var runs = Runs(pdf);
            Assert.Equal(3, PdfChecker.Latin1(pdf.ToArray()).Split(new[] { "[0 3.15] 0 d" }, StringSplitOptions.None).Length - 1);
            double numberWidth = font.MeasureText("10", 10.5);
            // 3 行ぶんの「見出し」と「ページ番号」が 1 ページに並び、ページ番号の右端が本文の右端にそろう
            Assert.Equal(1, PdfChecker.CountPages(pdf.ToArray()));
            var numberRun = runs.Where(r => Math.Abs(r.X - (left + width - numberWidth)) < 0.01).ToList();
            Assert.Equal(3, numberRun.Count);
        }

        [Fact]
        public void Paragraph_TabStopsAreInheritedFromDocumentDefault()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            doc.DefaultParagraphFormat.AddTabStop(100);
            doc.AddSection().AddParagraph("あ\tい");
            var runs = Runs(doc.Render());
            Assert.Equal(2, runs.Count);
            Assert.Equal(100, runs[1].X - runs[0].X, 3);
        }

        [Fact]
        public void Paragraph_InsideTableCell()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var t = doc.AddSection().AddTable();
            t.AddColumns(1);
            var row = t.AddRow("");
            var p = row.Cells[0].AddParagraph();
            p.AddText("あ\tい").AddTabStop(40);
            var bytes = doc.ToArray();
            Assert.Equal(1, PdfChecker.CountPages(bytes));
        }

        [Fact]
        public void Tab_HeavyTextTerminates()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var p = doc.AddSection().AddParagraph();
            p.AddTabStop(500, TabAlignment.Right, TabLeader.Dots).AddTabStop(30, TabAlignment.Center, TabLeader.Line);
            for (int i = 0; i < 200; i++) p.AddText("項目" + i + "\t" + i + "\t");
            var bytes = doc.ToArray();
            PdfChecker.AssertValidStructure(bytes);
        }
    }
}
