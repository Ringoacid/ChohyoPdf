using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ChohyoPdf.Text;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>ルビ（振り仮名）のテスト。</summary>
    public class RubyTests
    {
        /// <summary>全角 1em・半角 0.5em の等幅フォント（MS ゴシック）。</summary>
        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        private sealed class Run
        {
            public double X;
            public double Y;
            public double Size;
        }

        private static string Content(PdfDocument pdf)
        {
            pdf.CompressContent = false;
            return PdfChecker.Latin1(pdf.ToArray());
        }

        private static List<Run> Runs(PdfDocument pdf) => Runs(Content(pdf));

        private static List<Run> Runs(string content)
        {
            var list = new List<Run>();
            foreach (Match m in Regex.Matches(content, @"/F\d+ (\S+) Tf\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm"))
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

        private static ResolvedStyle Style(PdfFont font, double size) => ResolvedStyle.From(new TextStyle(font, size));

        private static KeyValuePair<string, ResolvedStyle> Plain(string text, ResolvedStyle style)
            => new KeyValuePair<string, ResolvedStyle>(text, style);

        /// <summary>ルビ付きの要素（ルビの文字サイズは親文字の半分）。</summary>
        private static KeyValuePair<string, ResolvedStyle> Ruby(PdfFont font, string text, string ruby, ResolvedStyle style)
            => new KeyValuePair<string, ResolvedStyle>(text, style.WithRuby(ruby, Style(font, style.Size / 2)));

        private static TextBlock Block(ResolvedStyle style, params KeyValuePair<string, ResolvedStyle>[] segments)
            => TextBlock.Build(segments, style);

        private static string Draw(TextBlock block, double width, TextAlignment align = TextAlignment.Left)
        {
            var pdf = new PdfDocument();
            var g = pdf.AddPage().Graphics;
            block.Draw(g, 0, 0, width, align, 0, block.Lines.Count);
            return Content(pdf);
        }

        // ---- 配置 ----

        [Fact]
        public void Ruby_IsHalfSizeCenteredAboveBase()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var block = Block(style, Ruby(font, "漢字", "かん", style));
            block.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            var runs = Runs(Draw(block, 200));
            Assert.Equal(2, runs.Count);
            Assert.Equal(10, runs[0].Size, 3);
            Assert.Equal(5, runs[1].Size, 3);
            // ルビ（5 pt の全角 2 文字 = 10）は、親文字の幅（20）の中央にそろう
            Assert.Equal(0, runs[0].X, 3);
            Assert.Equal(5, runs[1].X, 3);
            // ルビの下端が親文字の上端（ベースライン − アセント）に付く
            Assert.Equal(font.Ascent * 10 + font.Descent * 5, runs[0].Y - runs[1].Y, 3);
        }

        [Fact]
        public void Ruby_ShorterThanBase_IsCenteredWithoutSpreading()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var block = Block(style, Ruby(font, "東京都", "と", style));
            block.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            var runs = Runs(Draw(block, 200));
            Assert.Equal(2, runs.Count);
            Assert.Equal(30, block.Lines[0].Width, 3);          // 親文字の幅のまま
            Assert.Equal((30 - 5) / 2.0, runs[1].X, 3);
        }

        [Fact]
        public void Ruby_LongerThanBase_SpreadsBaseCharactersEvenly()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            // ルビ 6 文字 = 30、親文字 1 文字 = 10。親文字の幅が 30 になり、後ろの文字がその分だけ右にずれる
            var block = Block(style, Ruby(font, "漢", "かんじかんじ", style), Plain("あ", style));
            block.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal(40, block.Lines[0].Width, 3);
            var runs = Runs(Draw(block, 200));
            Assert.Equal(3, runs.Count);
            Assert.Equal(10, runs[0].X, 3);                      // 余白 20 の半分だけ右に寄る（前後に同じ余白）
            Assert.Equal(0, runs[1].X, 3);                       // ルビは組の左端から
            Assert.Equal(30, runs[2].X, 3);

            // 2 文字の親文字は、文字の間と前後に同じ量の余白が入る（余白 = (30 - 20) / 2 = 5 ずつ）
            var two = Block(style, Ruby(font, "漢字", "かんじかんじ", style));
            two.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal(30, two.Lines[0].Width, 3);
            var runs2 = Runs(Draw(two, 200));
            Assert.Equal(3, runs2.Count);
            Assert.Equal(2.5, runs2[0].X, 3);
            Assert.Equal(15 + 2.5, runs2[1].X, 3);
            Assert.Equal(0, runs2[2].X, 3);
        }

        // ---- 改行 ----

        [Fact]
        public void Ruby_GroupIsNeverSplitAcrossLines()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var block = Block(style, Plain("あいう", style), Ruby(font, "漢字", "かんじ", style), Plain("え", style));
            block.Layout(40, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal("あいう", block.LineText(0));
            Assert.Equal("漢字え", block.LineText(1));

            // 組が 1 行に収まらないときも、途中では改行しない
            var narrow = Block(style, Ruby(font, "漢字", "かんじ", style), Plain("あ", style));
            narrow.Layout(10, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal("漢字", narrow.LineText(0));
            Assert.Equal("あ", narrow.LineText(1));

            // 組と組の間では改行できる
            var mono = Block(style, Ruby(font, "東", "と", style), Ruby(font, "京", "きょう", style));
            mono.Layout(20, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal(2, mono.Lines.Count);
        }

        [Fact]
        public void Ruby_KinsokuStillApplies()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            // 行頭禁則の句点は、ルビの組ごと次の行へ送られる（句点だけが行頭に来ない）
            var block = Block(style, Plain("あ", style), Ruby(font, "漢字", "かんじ", style), Plain("。", style));
            block.Layout(30, 0, LineSpacingRule.Multiple, 1.0);
            Assert.Equal("あ", block.LineText(0));
            Assert.Equal("漢字。", block.LineText(1));
        }

        // ---- 行の高さ ----

        [Fact]
        public void Ruby_RaisesLineHeight_OnlyWhenNeeded()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var plain = Block(style, Plain("漢字", style));
            plain.Layout(200, 0, LineSpacingRule.Multiple, 1.2);
            var withRuby = Block(style, Ruby(font, "漢字", "かんじ", style));
            withRuby.Layout(200, 0, LineSpacingRule.Multiple, 1.2);

            double reach = font.Ascent * 10 + (font.Ascent + font.Descent) * 5;
            double baseline0 = plain.Lines[0].Baseline;
            Assert.Equal(12, plain.Lines[0].Height, 3);
            Assert.True(reach > baseline0);
            Assert.Equal(12 + (reach - baseline0), withRuby.Lines[0].Height, 3);
            Assert.Equal(reach, withRuby.Lines[0].Baseline, 3);

            // ルビがない行の高さは変わらない
            var two = Block(style, Ruby(font, "漢字", "かんじ", style), Plain("\n漢字", style));
            two.Layout(200, 0, LineSpacingRule.Multiple, 1.2);
            Assert.Equal(withRuby.Lines[0].Height, two.Lines[0].Height, 3);
            Assert.Equal(12, two.Lines[1].Height, 3);
            Assert.Equal(baseline0, two.Lines[1].Baseline, 3);

            // 行間に余裕があれば、行の高さは広げない（ルビは行間の余白に収まる）
            var loose = Block(style, Ruby(font, "漢字", "かんじ", style));
            loose.Layout(200, 0, LineSpacingRule.Multiple, 3.0);
            var loosePlain = Block(style, Plain("漢字", style));
            loosePlain.Layout(200, 0, LineSpacingRule.Multiple, 3.0);
            Assert.Equal(30, loose.Lines[0].Height, 3);
            Assert.Equal(loosePlain.Lines[0].Baseline, loose.Lines[0].Baseline, 3);
        }

        [Fact]
        public void Ruby_AtLeastGrowsAndExactlyKeepsHeight()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var atLeast = Block(style, Ruby(font, "漢字", "かんじ", style));
            atLeast.Layout(200, 0, LineSpacingRule.AtLeast, 12);
            var atLeastPlain = Block(style, Plain("漢字", style));
            atLeastPlain.Layout(200, 0, LineSpacingRule.AtLeast, 12);
            Assert.True(atLeast.Lines[0].Height > atLeastPlain.Lines[0].Height);

            var exactly = Block(style, Ruby(font, "漢字", "かんじ", style));
            exactly.Layout(200, 0, LineSpacingRule.Exactly, 14);
            var exactlyPlain = Block(style, Plain("漢字", style));
            exactlyPlain.Layout(200, 0, LineSpacingRule.Exactly, 14);
            Assert.Equal(14, exactly.Lines[0].Height, 3);
            Assert.Equal(exactlyPlain.Lines[0].Baseline, exactly.Lines[0].Baseline, 3);
            // 行の高さを変えなくても、ルビは描かれる
            Assert.Equal(2, Runs(Draw(exactly, 200)).Count);
        }

        // ---- テキスト抽出 ----

        [Fact]
        public void Ruby_IsMarkedAsIgnoredTextAndDrawnAfterBase()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var block = Block(style, Plain("私は", style), Ruby(font, "東京", "とうき", style), Plain("へ", style));
            block.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            string content = Draw(block, 200);

            const string Begin = "/Span <</ActualText <FEFF>>> BDC";
            Assert.Single(Regex.Matches(content, Regex.Escape(Begin)));
            Assert.Single(Regex.Matches(content, @"\nEMC\n"));
            int bdc = content.IndexOf(Begin, StringComparison.Ordinal);
            int emc = content.IndexOf("\nEMC\n", StringComparison.Ordinal);
            var tj = Regex.Matches(content, @"> Tj").Cast<Match>().Select(m => m.Index).ToList();
            // 描く順序: 私は → 東京 → ルビ（印の中）→ へ
            Assert.Equal(4, tj.Count);
            Assert.True(tj[1] < bdc);
            Assert.True(tj[2] > bdc && tj[2] < emc);
            Assert.True(tj[3] > emc);
            // 印は BT/ET の外で開閉する
            Assert.DoesNotContain("BT\n" + Begin, content);
        }

        // ---- 何度レイアウトしても同じ結果 ----

        [Fact]
        public void Ruby_LayoutIsRepeatable()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var tabs = TabLayout.Create(new[] { new TabStop(100, TabAlignment.Right, TabLeader.Dots) }, null);
            var block = Block(style, Plain("あ", style), Ruby(font, "漢", "かんじかんじ", style), Plain("\t", style),
                Ruby(font, "東京都", "と", style), Plain("いうえおかきくけこ", style));
            block.Layout(60, 0, LineSpacingRule.Multiple, 1.5, tabs);
            string first = Draw(block, 60);
            int lines = block.Lines.Count;
            double height = block.TotalHeight;
            for (int i = 0; i < 3; i++)
            {
                block.Layout(200, 0, LineSpacingRule.Multiple, 1.5, tabs);
                block.Layout(30, 0, LineSpacingRule.AtLeast, 20, tabs);
                block.Layout(60, 0, LineSpacingRule.Multiple, 1.5, tabs);
                Assert.Equal(lines, block.Lines.Count);
                Assert.Equal(height, block.TotalHeight, 9);
                Assert.Equal(first, Draw(block, 60));
            }
        }

        [Fact]
        public void Ruby_ForwardPageRefConvergesToTheSameLayout()
        {
            var font = Mono();
            if (font == null) return;
            Document Make(bool withRef)
            {
                var doc = new Document(font, 10);
                var s = doc.AddSection();
                var p = s.AddParagraph("本文");
                p.AddRuby("漢字", "かんじ");
                if (withRef) p.AddText("（").AddPageRef("end").AddText("）");
                s.AddParagraph("前置き").AddRubyMarkup("｜東京《とうきょう》");
                s.AddPageBreak();
                s.AddParagraph("終わり").SetAnchor("end").AddRuby("漢字", "かんじ");
                return doc;
            }
            // 同じ Document を 2 回レンダリングしても同じ（モデルに状態を残さない）
            var withRef = Make(true);
            var a = Runs(withRef.Render());
            var b = Runs(withRef.Render());
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].X, b[i].X, 3);
                Assert.Equal(a[i].Y, b[i].Y, 3);
                Assert.Equal(a[i].Size, b[i].Size, 3);
            }
            // ページ参照のために何度かレイアウトし直しても、ルビの位置は 1 回で済む場合と同じ
            var once = Runs(Make(false).Render());
            var rubyA = a.Where(r => Math.Abs(r.Size - 5) < 1e-6).ToList();
            var rubyOnce = once.Where(r => Math.Abs(r.Size - 5) < 1e-6).ToList();
            Assert.Equal(3, rubyA.Count);
            Assert.Equal(rubyOnce.Count, rubyA.Count);
            for (int i = 0; i < rubyA.Count; i++)
            {
                Assert.Equal(rubyOnce[i].X, rubyA[i].X, 3);
                Assert.Equal(rubyOnce[i].Y, rubyA[i].Y, 3);
            }
        }

        // ---- ほかの機能との組み合わせ ----

        [Fact]
        public void Ruby_JustifyDoesNotSeparateTheGroup()
        {
            var font = Mono();
            if (font == null) return;
            var style = Style(font, 10);
            var block = Block(style, Ruby(font, "漢字", "かんじ", style), Plain("あい", style));
            block.Layout(200, 0, LineSpacingRule.Multiple, 1.0);
            var runs = Runs(Draw(block, 200, TextAlignment.Distribute));
            // 親文字の 2 文字は離れず、組と「あい」の間にだけ余白が入る。ルビは親文字の中央
            var bases = runs.Where(r => Math.Abs(r.Size - 10) < 1e-6).OrderBy(r => r.X).ToList();
            var ruby = runs.Single(r => Math.Abs(r.Size - 5) < 1e-6);
            Assert.Equal(4, bases.Count);
            Assert.Equal(bases[0].X + 10, bases[1].X, 3);
            Assert.True(bases[2].X - bases[1].X > 10 + 1);
            Assert.Equal(bases[0].X + 10 - 7.5, ruby.X, 3);
        }

        [Fact]
        public void Ruby_WithDecorationsTabsAndSuperscript_InParagraph()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var s = doc.AddSection();
            var p = s.AddParagraph().SetAlignment(TextAlignment.Justify).AddTabStop(150, TabAlignment.Right, TabLeader.Dots);
            p.AddText("目次");
            p.AddRuby("第一章", "だいいっしょう", new TextStyle { Bold = true, Underline = true });
            p.AddSuperscript("注");
            p.AddRuby("漢字", "かんじ", new TextStyle { Strikeout = true, Color = PdfColor.Red }, new TextStyle { Color = PdfColor.Blue });
            p.AddTab().AddText("12");
            s.AddParagraph("長い文章の途中に").AddRuby("振り仮名", "ふりがな").AddText("が入っても、折り返しや両端揃えが崩れないことを確認します。繰り返し繰り返し。")
                .SetAlignment(TextAlignment.Justify);
            var pdf = doc.Render();
            Assert.Single(pdf.Pages);
            string content = Content(pdf);
            Assert.Equal(3, Regex.Matches(content, @"/ActualText <FEFF>").Count);
            Assert.Equal(Regex.Matches(content, @"/ActualText <FEFF>").Count, Regex.Matches(content, @"\nEMC\n").Count);
        }

        [Fact]
        public void Ruby_UnderlineCoversWidenedBase_AndRubyHasNone()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            doc.AddSection().AddParagraph().AddRuby("漢", "かんじかんじ", new TextStyle { Underline = true });
            string content = Content(doc.Render());
            var rects = Regex.Matches(content, @"(\S+) (\S+) (\S+) (\S+) re").Cast<Match>()
                .Select(m => double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)).ToList();
            Assert.Single(rects);
            Assert.Equal(30, rects[0], 3);
        }

        // ---- スタイル ----

        [Fact]
        public void RubyStyle_ParagraphAndRunLevelAreApplied()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var p = doc.AddSection().AddParagraph();
            p.SetRubyStyle(new TextStyle { Color = PdfColor.Red });
            p.AddRuby("漢字", "かんじ");
            p.AddRuby("東京", "とうきょう", null, new TextStyle { Color = PdfColor.Blue, FontSize = 4 });
            string content = Content(doc.Render());
            var begins = Regex.Matches(content, @"/Span <</ActualText <FEFF>>> BDC\nq\nBT\n/F\d+ (\S+) Tf\n([^\n]+)\n").Cast<Match>().ToList();
            Assert.Equal(2, begins.Count);
            Assert.Equal("5", begins[0].Groups[1].Value);
            Assert.Equal(PdfColor.Red.FillOperator(), begins[0].Groups[2].Value);
            Assert.Equal("4", begins[1].Groups[1].Value);
            Assert.Equal(PdfColor.Blue.FillOperator(), begins[1].Groups[2].Value);
        }

        [Fact]
        public void RubyStyle_DecorationsAreNotInherited()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var p = doc.AddSection().AddParagraph();
            p.AddRuby("漢字", "かんじ", new TextStyle { Strikeout = true, BackgroundColor = PdfColor.Yellow, CharacterSpacing = 1, Underline = true });
            string content = Content(doc.Render());
            // 背景・下線・取り消し線は親文字の分だけ（3 つ）。ルビには引かない
            Assert.Equal(3, Regex.Matches(content, @" re\n").Count);
        }

        // ---- API ----

        [Fact]
        public void RubyRun_ValidatesArguments()
        {
            Assert.Throws<ArgumentException>(() => new RubyRun("", "かんじ"));
            Assert.Throws<ArgumentException>(() => new RubyRun("漢\n字", "かんじ"));
            Assert.Throws<ArgumentException>(() => new RubyRun("漢字", "かん\tじ"));
            var plain = new RubyRun("漢字", null!);
            Assert.Equal("", plain.RubyText);
        }

        [Fact]
        public void EmptyRuby_IsPlainText()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            doc.AddSection().AddParagraph().AddRuby("漢字", "").AddText("あ");
            var runs = Runs(doc.Render());
            Assert.Single(runs);
            Assert.Equal(10, runs[0].Size, 3);
        }

        [Fact]
        public void AddMonoRuby_MakesOneRunPerCharacter()
        {
            var p = new Paragraph();
            p.AddMonoRuby("東京", new[] { "とう", "きょう" });
            Assert.Equal(2, p.Inlines.Count);
            Assert.Equal("東", ((RubyRun)p.Inlines[0]).BaseText);
            Assert.Equal("とう", ((RubyRun)p.Inlines[0]).RubyText);
            Assert.Equal("京", ((RubyRun)p.Inlines[1]).BaseText);
            Assert.Equal("きょう", ((RubyRun)p.Inlines[1]).RubyText);
            Assert.Throws<ArgumentException>(() => new Paragraph().AddMonoRuby("東京", new[] { "とうきょう" }));
            // サロゲートペアは 1 文字
            var q = new Paragraph().AddMonoRuby("\U00020BB7野", new[] { "よし", "の" });
            Assert.Equal(2, q.Inlines.Count);
            Assert.Equal("\U00020BB7", ((RubyRun)q.Inlines[0]).BaseText);
        }

        [Fact]
        public void AddRuby_WithArraysAddsPairsInOrder()
        {
            var p = new Paragraph().AddRuby(new[] { "漢字", "仮名" }, new[] { "かんじ", "かな" });
            Assert.Equal(2, p.Inlines.Count);
            Assert.Equal("かな", ((RubyRun)p.Inlines[1]).RubyText);
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRuby(new[] { "漢字" }, new[] { "かんじ", "かな" }));
        }

        [Fact]
        public void AddRubyMarkup_ParsesAozoraStyle()
        {
            var p = new Paragraph().AddRubyMarkup("私は東京《とうきょう》へ、｜Tokyo《とうきょう》と|大阪府《おおさか|ふ|》。");
            var texts = p.Inlines.Select(i => i is RubyRun r ? "[" + r.BaseText + "/" + r.RubyText + "]" : ((TextRun)i).Text).ToList();
            // 「大阪府」は 3 文字なので 3 つのルビ（最後は空）
            Assert.Equal(new[] { "私は", "[東京/とうきょう]", "へ、", "[Tokyo/とうきょう]", "と", "[大/おおさか]", "[阪/ふ]", "[府/]", "。" }, texts);
        }

        [Fact]
        public void AddRubyMarkup_RejectsMalformedMarkup()
        {
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRubyMarkup("あ《あ》"));          // 親文字（漢字）がない
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRubyMarkup("漢字《かんじ"));       // 閉じていない
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRubyMarkup("漢字》"));            // 対応する開きがない
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRubyMarkup("|漢字"));             // 「《」がない
            Assert.Throws<ArgumentException>(() => new Paragraph().AddRubyMarkup("東京《と|う|きょう》")); // モノルビの数が合わない
            Assert.Empty(new Paragraph().AddRubyMarkup("").Inlines);
            // ルビが空なら普通の文字列
            var p = new Paragraph().AddRubyMarkup("漢字《》");
            Assert.IsType<TextRun>(p.Inlines.Single());
        }

        [Fact]
        public void Heading_OutlineTitleUsesBaseText()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var s = doc.AddSection();
            var p = s.AddParagraph("第").SetOutline(1);
            p.AddRuby("一章", "いちしょう");
            var pdf = doc.Render();
            Assert.Equal("第一章", pdf.Bookmarks[0].Title);
        }

        [Fact]
        public void Ruby_WorksInTableCellAndMultiplePages()
        {
            var font = Mono();
            if (font == null) return;
            var doc = new Document(font, 10);
            var s = doc.AddSection();
            var table = s.AddTable();
            table.AddColumn(80);
            var row = table.AddRow("");
            row.Cells[0].AddParagraph().AddRuby("漢字", "かんじ");
            for (int i = 0; i < 80; i++) s.AddParagraph("行 " + i).AddRuby("漢字", "かんじ").AddText("の行です");
            var pdf = doc.Render();
            Assert.True(pdf.Pages.Count > 1);
            PdfChecker.AssertValidStructure(pdf.ToArray());
        }
    }
}
