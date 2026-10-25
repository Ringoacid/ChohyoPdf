using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>箇条書き・番号付きリストのテスト。</summary>
    public class ListTests
    {
        /// <summary>ページ上の文字列 1 つ（同じ書式の連なり）。(X, Y) はベースラインの位置（左上原点・下向きが正）。</summary>
        private sealed class TextRunInfo
        {
            public string Text = "";
            public double X;
            public double Y;
            public double Size;
            public string Color = "";
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
                var matches = Regex.Matches(contents[page], @"/F\d+ (\S+) Tf\n([^\n]*)\n(?:[^\n]*\n)*?1 0 [\d.]+ -1 (\S+) (\S+) Tm\n<[0-9A-F]+> Tj");
                Assert.Equal(texts[page].Count, matches.Count);
                for (int i = 0; i < matches.Count; i++)
                {
                    var m = matches[i];
                    result.Add(new TextRunInfo
                    {
                        Text = texts[page][i],
                        Size = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                        Color = m.Groups[2].Value,
                        X = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                        Y = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                        Page = page,
                    });
                }
            }
            return result;
        }

        /// <summary>全角 1em・半角 0.5em の等幅フォント（MS ゴシック）。見つからない環境では null。</summary>
        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        private static Document? NewDoc(double size = 10)
        {
            var font = Mono();
            if (font == null) return null;
            var doc = new Document(font, size);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return doc;
        }

        private static TextRunInfo Find(List<TextRunInfo> runs, string text, int occurrence = 0)
        {
            var found = runs.Where(r => r.Text == text).ToList();
            Assert.True(found.Count > occurrence, "文字列 '" + text + "' が見つかりません（" + found.Count + " 件）");
            return found[occurrence];
        }

        private const double Left = 56.692913385826771;   // 余白 20 mm

        // ---- 箇条書き ----

        [Fact]
        public void Bullet_DefaultMarkerAndHangingIndent()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("前");
            var list = s.AddBulletList();
            list.AddItem("りんご");
            list.AddItem("みかん");
            var runs = Runs(doc.ToArray());

            var before = Find(runs, "前");
            var bullet1 = Find(runs, "・", 0);
            var bullet2 = Find(runs, "・", 1);
            var item1 = Find(runs, "りんご");
            var item2 = Find(runs, "みかん");
            Assert.Equal(Left, bullet1.X, 3);
            // 記号の列の幅は記号に合わせる（「・」は 10 ポイント）。本文は 10 + 間隔（文字サイズの半分）の位置
            Assert.Equal(Left + 15, item1.X, 3);
            Assert.Equal(item1.X, item2.X, 6);
            // 記号と本文のベースラインがそろう
            Assert.Equal(item1.Y, bullet1.Y, 6);
            Assert.Equal(item2.Y, bullet2.Y, 6);
            Assert.True(item2.Y > item1.Y);
            Assert.True(item1.Y > before.Y);
        }

        [Fact]
        public void Bullet_WrappedLinesAlignWithTextStart()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection(new PageSetup { Size = new PageSize(200, 300), Margins = new Thickness(20) });
            var list = s.AddBulletList("●");
            list.AddItem(new string('あ', 40));
            var runs = Runs(doc.ToArray());

            var bullet = Find(runs, "●");
            var lines = runs.Where(r => r.Text.StartsWith("あ")).OrderBy(r => r.Y).ToList();
            Assert.True(lines.Count >= 3);
            // 全行の行頭が本文の開始位置にそろい、記号は 1 行目だけ
            Assert.All(lines, l => Assert.Equal(lines[0].X, l.X, 6));
            Assert.Equal(20 + 15, lines[0].X, 3);
            Assert.Single(runs, r => r.Text == "●");
            Assert.Equal(20, bullet.X, 3);
            Assert.Equal(lines[0].Y, bullet.Y, 6);
        }

        [Fact]
        public void Bullet_DefaultsChangePerLevel()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var l1 = s.AddBulletList();
            l1.AddItem("レベル1");
            var l2 = l1.AddSubList();
            l2.AddItem("レベル2");
            var l3 = l2.AddSubList();
            l3.AddItem("レベル3");
            var l4 = l3.AddSubList();
            l4.AddItem("レベル4");
            var runs = Runs(doc.ToArray());

            Assert.Equal("・", Find(runs, "・").Text);
            Assert.NotNull(Find(runs, "○"));
            Assert.NotNull(Find(runs, "■"));
            // 4 レベル目は 1 レベル目の記号に戻る
            Assert.Equal(2, runs.Count(r => r.Text == "・"));

            // 入れ子は親の本文の開始位置から始まる
            double x1 = Find(runs, "レベル1").X;
            Assert.Equal(x1, Find(runs, "○").X, 6);
            double x2 = Find(runs, "レベル2").X;
            Assert.Equal(x2, Find(runs, "■").X, 6);
            Assert.True(x2 > x1);
            Assert.Equal(x2 + 15, Find(runs, "レベル3").X, 3);
        }

        [Fact]
        public void Bullet_CustomStringAndMarkerWidth()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddBulletList("–");
            list.SetMarkerBox(30, 6);
            list.AddItem("項目");
            var runs = Runs(doc.ToArray());
            Assert.Equal(Left, Find(runs, "–").X, 3);
            Assert.Equal(Left + 36, Find(runs, "項目").X, 3);
        }

        [Fact]
        public void List_IndentAndItemSpacing()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var plain = s.AddBulletList();
            plain.AddItem("A");
            plain.AddItem("B");
            var spaced = s.AddBulletList();
            spaced.Indent = 20;
            spaced.ItemSpacing = 8;
            spaced.AddItem("C");
            spaced.AddItem("D");
            var runs = Runs(doc.ToArray());
            double step = Find(runs, "B").Y - Find(runs, "A").Y;
            Assert.Equal(step + 8, Find(runs, "D").Y - Find(runs, "C").Y, 6);
            Assert.Equal(Find(runs, "A").X + 20, Find(runs, "C").X, 6);
            Assert.Equal(Left + 20, Find(runs, "・", 2).X, 3);
        }

        // ---- 番号付きリスト ----

        [Fact]
        public void Numbered_DefaultFormatsPerLevel()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddNumberedList();
            list.AddItem("第一");
            list.AddItem("第二");
            var sub = list.AddSubList();
            sub.AddItem("子一");
            sub.AddItem("子二");
            var subsub = sub.AddSubList();
            subsub.AddItem("孫一");
            subsub.AddItem("孫二");
            list.AddItem("第三");
            var runs = Runs(doc.ToArray());

            string[] markers = runs.Select(r => r.Text).Where(t => t.Length > 0 && !t.StartsWith("第") && !t.StartsWith("子") && !t.StartsWith("孫")).ToArray();
            Assert.Equal(new[] { "1.", "2.", "(1)", "(2)", "①", "②", "3." }, markers);
        }

        [Fact]
        public void Numbered_FormatsPrefixSuffixAndStart()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var roman = s.AddNumberedList(PageNumberFormat.LowerRoman);
            roman.AddItem("x");
            roman.AddItem("x");
            var kanji = s.AddNumberedList(PageNumberFormat.Kanji, 1, null, "、");
            kanji.AddItem("y");
            kanji.AddItem("y");
            var wide = s.AddNumberedList(PageNumberFormat.Kanji, 3, "（", "）");
            wide.AddItem("z");
            var letters = s.AddNumberedList(PageNumberFormat.UpperLetter, 2);
            letters.AddItem("w");
            var circled = s.AddNumberedList(PageNumberFormat.CircledNumber);
            circled.AddItem("v");
            var paren = s.AddNumberedList(PageNumberFormat.Arabic, 9, null, ")");
            paren.AddItem("u");
            paren.AddItem("u");
            var runs = Runs(doc.ToArray());
            var markers = runs.Select(r => r.Text).Where(t => t.Length > 0 && !"xyzwvu".Contains(t)).ToArray();
            Assert.Equal(new[] { "i.", "ii.", "一、", "二、", "（三）", "B.", "①", "9)", "10)" }, markers);
        }

        [Fact]
        public void Numbered_ItemNumberOverrideContinues()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddNumberedList();
            list.AddItem("a");
            var item = list.AddItem();
            item.Number = 10;
            item.AddParagraph("b");
            list.AddItem("c");
            var runs = Runs(doc.ToArray());
            var markers = runs.Select(r => r.Text).Where(t => t.EndsWith(".")).ToArray();
            Assert.Equal(new[] { "1.", "10.", "11." }, markers);
        }

        [Fact]
        public void Numbered_TextStartIsSharedAndRightAlignmentAlignsDigits()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var left = s.AddNumberedList(startNumber: 9);
            left.AddItem("a");
            left.AddItem("b");
            var right = s.AddNumberedList(startNumber: 9);
            right.MarkerAlignment = HorizontalAlignment.Right;
            right.AddItem("c");
            right.AddItem("d");
            var runs = Runs(doc.ToArray());

            // 本文の開始位置は、桁数の違う記号でもそろう
            Assert.Equal(Find(runs, "a").X, Find(runs, "b").X, 6);
            Assert.Equal(Find(runs, "c").X, Find(runs, "d").X, 6);
            Assert.Equal(Find(runs, "a").X, Find(runs, "c").X, 6);

            // 左揃えは左端がそろい、右揃えは右端（本文の手前）がそろう。半角の幅は 5 ポイント
            Assert.Equal(Find(runs, "9.", 0).X, Find(runs, "10.", 0).X, 6);
            double nine = Find(runs, "9.", 1).X + 10;
            double ten = Find(runs, "10.", 1).X + 15;
            Assert.Equal(nine, ten, 6);
            Assert.Equal(Left + 5, Find(runs, "9.", 1).X, 3);
        }

        // ---- 記号のスタイル ----

        [Fact]
        public void MarkerStyle_ColorSizeAndBold()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddBulletList("●");
            list.MarkerStyle = new TextStyle { Color = PdfColor.Red, FontSize = 20 };
            list.AddItem("本文");
            var runs = Runs(doc.ToArray());
            var marker = Find(runs, "●");
            var text = Find(runs, "本文");
            Assert.Equal(20, marker.Size, 6);
            Assert.Equal(10, text.Size, 6);
            Assert.StartsWith("1 0 0", marker.Color);
            Assert.DoesNotContain("1 0 0 rg", text.Color);
            // 大きさが違ってもベースラインはそろう
            Assert.Equal(text.Y, marker.Y, 6);
            // 記号の列の幅は記号に合わせて広がる（20 ポイント + 間隔 5）
            Assert.Equal(Left + 25, text.X, 3);
        }

        [Fact]
        public void MarkerStyle_FollowsParagraphStyleAndDropsDecoration()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddNumberedList();
            list.AddItem("赤", new TextStyle { Color = PdfColor.Red, FontSize = 14, Underline = true });
            list.AddItem("標準");
            var runs = Runs(doc.ToArray());
            var m1 = Find(runs, "1.");
            Assert.Equal(14, m1.Size, 6);
            Assert.StartsWith("1 0 0", m1.Color);
            Assert.Equal(10, Find(runs, "2.").Size, 6);
            // 下線は本文にだけ引かれる（塗りつぶしの矩形 1 つ）
            string content = PdfChecker.DecodedStreams(doc.ToArray()).First(c => c.StartsWith("1 0 0 -1 0 "));
            Assert.Single(Regex.Matches(content, @" re\n"));
        }

        // ---- スタイルの継承 ----

        [Fact]
        public void Style_InheritanceListAndItem()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.DefaultParagraphFormat.FirstLineIndent = 21;      // 本文用の字下げ
            doc.DefaultParagraphFormat.LeftIndent = 30;
            doc.Styles["強調"] = new ParagraphStyle(new TextStyle { FontSize = 16 }, null);
            var s = doc.AddSection();
            var list = s.AddBulletList();
            list.Style = new TextStyle { FontSize = 12, Color = PdfColor.Blue };
            list.AddItem("リスト");
            var item = list.AddItem();
            item.Style = new TextStyle { FontSize = 14 };
            item.AddParagraph("項目");
            item.AddParagraph("段落", new TextStyle { FontSize = 9 });
            list.AddItem("名前", "強調");
            var runs = Runs(doc.ToArray());

            // 本文の字下げは引き継がない
            Assert.Equal(Left, Find(runs, "・").X, 3);
            Assert.Equal(12, Find(runs, "リスト").Size, 6);
            Assert.Equal(14, Find(runs, "項目").Size, 6);
            Assert.Equal(9, Find(runs, "段落").Size, 6);
            Assert.Equal(16, Find(runs, "名前").Size, 6);
            Assert.StartsWith("0 0 1", Find(runs, "リスト").Color);
            Assert.StartsWith("0 0 1", Find(runs, "項目").Color);
            // 記号は最初の段落の大きさに従う
            Assert.Equal(12, Find(runs, "・", 0).Size, 6);
            Assert.Equal(14, Find(runs, "・", 1).Size, 6);
            Assert.Equal(16, Find(runs, "・", 2).Size, 6);
            // 本文の開始位置は、リスト内で一番幅の広い記号（16 ポイントの「・」）に合わせて全項目でそろう
            Assert.Equal(Find(runs, "リスト").X, Find(runs, "名前").X, 6);
        }

        // ---- 複数のブロック・入れ子・コンテナ ----

        [Fact]
        public void Item_MultipleBlocksHaveASingleMarker()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddNumberedList();
            var item = list.AddItem();
            item.AddParagraph("一段落目");
            item.AddParagraph("二段落目");
            var sub = item.AddBulletList();
            sub.AddItem("子");
            list.AddItem("次");
            var runs = Runs(doc.ToArray());
            Assert.Single(runs, r => r.Text == "1.");
            Assert.Single(runs, r => r.Text == "2.");
            Assert.Equal(Find(runs, "一段落目").X, Find(runs, "二段落目").X, 6);
            Assert.Equal(Find(runs, "一段落目").X, Find(runs, "○").X, 6);   // 子リストの 1 レベル目が親の入れ子 = 2 レベル目
            Assert.Equal(Find(runs, "一段落目").Y, Find(runs, "1.").Y, 6);
        }

        [Fact]
        public void Item_StartingWithATableAndEmptyItem()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var list = s.AddBulletList();
            var item = list.AddItem();
            var table = item.AddTable();
            table.AddColumn(100);
            table.AddRow("セル");
            list.AddItem();                     // 空の項目にも記号が付く
            list.AddItem("最後");
            var runs = Runs(doc.ToArray());
            Assert.Equal(3, runs.Count(r => r.Text == "・"));
            Assert.True(Find(runs, "セル").X > Find(runs, "・").X);
            Assert.True(Find(runs, "・", 1).Y > Find(runs, "セル").Y);
        }

        [Fact]
        public void List_InTableCellAndHeaderFooter()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var table = s.AddTable();
            table.AddColumn(Unit.Mm(80));
            table.AddColumn(Unit.Mm(80));
            var row = table.AddRow("左");
            var list = row.Cells[1].AddBulletList();
            list.AddItem("セル内 A");
            list.AddItem("セル内 B");
            s.Footer.AddNumberedList().AddItem("フッター");
            var runs = Runs(doc.ToArray());
            Assert.Equal(2, runs.Count(r => r.Text == "・"));
            Assert.Equal(Find(runs, "セル内 A").Y, Find(runs, "・", 0).Y, 6);
            Assert.Equal(Find(runs, "セル内 A").X, Find(runs, "セル内 B").X, 6);
            Assert.Equal("1.", Find(runs, "1.").Text);
            Assert.Equal(Find(runs, "フッター").Y, Find(runs, "1.").Y, 6);
        }

        // ---- 改ページ ----

        [Fact]
        public void PageBreaks_NumbersContinueAndMarkerOnlyOnFirstLine()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection(new PageSetup { Size = new PageSize(200, 200), Margins = new Thickness(20) });
            var list = s.AddNumberedList();
            for (int i = 1; i <= 40; i++) list.AddItem("項目" + i);
            list.AddItem(string.Concat(Enumerable.Repeat("長い項目の文章。", 60)));      // ページをまたぐ項目
            list.AddItem("最後");
            var runs = Runs(doc.ToArray());
            int pages = runs.Max(r => r.Page) + 1;
            Assert.True(pages >= 4, "ページ数: " + pages);

            var markers = runs.Where(r => Regex.IsMatch(r.Text, @"^\d+\.$")).ToList();
            Assert.Equal(Enumerable.Range(1, 42).Select(n => n + ".").ToArray(), markers.Select(m => m.Text).ToArray());
            // 2 ページ目以降の最初の記号は、前のページの続きの番号（項目 1 から連続）
            var firstOnSecond = markers.First(m => m.Page == 1);
            int lastOnFirst = int.Parse(markers.Last(m => m.Page == 0).Text.TrimEnd('.'));
            Assert.Equal(lastOnFirst + 1, int.Parse(firstOnSecond.Text.TrimEnd('.')));

            // 長い項目は複数のページに分割され、記号は最初の行にだけ付く
            var longLines = runs.Where(r => Regex.IsMatch(r.Text, "^[長い項目の文章。]+$")).ToList();
            Assert.True(longLines.Select(r => r.Page).Distinct().Count() >= 2);
            Assert.All(longLines, l => Assert.Equal(longLines[0].X, l.X, 6));
        }

        [Fact]
        public void PageBreaks_ListPageBreakBefore()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection(new PageSetup { Size = new PageSize(200, 200), Margins = new Thickness(20) });
            for (int i = 0; i < 9; i++) s.AddParagraph("行" + i);
            var list = s.AddBulletList();
            list.PageBreakBefore = true;
            list.AddItem("次ページ");
            var runs = Runs(doc.ToArray());
            Assert.Equal(1, Find(runs, "次ページ").Page);
            Assert.Equal(1, Find(runs, "・").Page);
        }

        // ---- 見出し・リンクとの連携、予備レイアウト ----

        [Fact]
        public void Headings_InListItemsReachTheTocAndBookmarks()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddTableOfContents();
            s.AddPageBreak();
            var list = s.AddNumberedList();
            var item = list.AddItem();
            var h = item.AddParagraph("リスト内の見出し");
            h.SetOutline(1).SetAnchor("h1");
            item.AddParagraph("本文");
            var second = list.AddItem();
            second.AddParagraph().AddInternalLink("見出しへ", "h1").AddText("（p.").AddPageRef("h1").AddText("）");
            var bytes = doc.ToArray();
            var inspector = new PdfInspector(bytes);
            Assert.Equal(2, inspector.PageCount);
            Assert.Contains(inspector.Outlines(), o => o.Title == "リスト内の見出し");
            var runs = Runs(bytes);
            // 目次の行（見出し + ページ番号 2）
            Assert.Equal(2, runs.Count(r => r.Text == "リスト内の見出し"));
            Assert.Contains(runs, r => r.Text == "2");
            Assert.Contains(runs, r => r.Text.Contains("p.2"));
            Assert.NotEmpty(inspector.Links());
        }

        [Fact]
        public void MultiPassLayout_IsRepeatable()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection(new PageSetup { Size = new PageSize(200, 200), Margins = new Thickness(20) });
            s.AddTableOfContents();
            var list = s.AddNumberedList();
            for (int i = 1; i <= 30; i++)
            {
                var item = list.AddItem();
                item.AddParagraph("見出し" + i).SetOutline(1);
                var sub = item.AddBulletList();
                sub.AddItem("子" + i);
            }
            byte[] a = doc.ToArray();
            byte[] b = doc.ToArray();
            Assert.Equal(a, b);
            // 記号は項目ごとに 1 つ（番号は 1〜30 が順に並ぶ）
            var runs = Runs(a);
            var numbers = runs.Where(r => Regex.IsMatch(r.Text, @"^\d+\.$")).Select(r => r.Text).ToArray();
            Assert.Equal(Enumerable.Range(1, 30).Select(n => n + ".").ToArray(), numbers);
            Assert.Equal(30, runs.Count(r => r.Text == "○"));
        }
    }
}
