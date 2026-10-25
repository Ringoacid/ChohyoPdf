using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>ページ番号の書式・開始番号と、奇数/偶数・先頭ページ別のヘッダー/フッターのテスト。</summary>
    public class PageNumberAndHeaderTests
    {
        private static Document? NewDoc()
        {
            var font = TestFonts.Japanese;
            if (font == null) return null;
            var doc = new Document(font, 10.5);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return doc;
        }

        /// <summary>ページ数を指定して、そのページ数になるセクション（改ページで区切る）を作る。</summary>
        private static Section AddPages(Document doc, int pages, Action<Section>? setup = null)
        {
            var s = doc.AddSection();
            setup?.Invoke(s);
            for (int i = 0; i < pages; i++)
            {
                if (i > 0) s.AddPageBreak();
                s.AddParagraph("本文");
            }
            return s;
        }

        /// <summary>ページごとの内容ストリーム（描画命令）を返す。</summary>
        private static List<string> PageContents(Document doc, out int pages)
        {
            var bytes = doc.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            pages = PdfChecker.CountPages(bytes);
            var list = PdfChecker.DecodedStreams(bytes).Where(c => c.StartsWith("1 0 0 -1 0 ")).ToList();
            Assert.Equal(pages, list.Count);
            return list;
        }

        private static TextStyle Size(double size) => new TextStyle { FontSize = size };

        // ---- 書式 ----

        [Theory]
        [InlineData(1, "i")]
        [InlineData(4, "iv")]
        [InlineData(9, "ix")]
        [InlineData(14, "xiv")]
        [InlineData(1994, "mcmxciv")]
        [InlineData(3999, "mmmcmxcix")]
        [InlineData(0, "0")]
        [InlineData(4000, "4000")]
        [InlineData(-3, "-3")]
        public void Format_LowerRoman(int n, string expected)
            => Assert.Equal(expected, PageNumberFormatter.Format(n, PageNumberFormat.LowerRoman));

        [Fact]
        public void Format_UpperRoman()
        {
            Assert.Equal("XLII", PageNumberFormatter.Format(42, PageNumberFormat.UpperRoman));
            Assert.Equal("MMXXVI", PageNumberFormatter.Format(2026, PageNumberFormat.UpperRoman));
            Assert.Equal("4000", PageNumberFormatter.Format(4000, PageNumberFormat.UpperRoman));
        }

        [Theory]
        [InlineData(1, "a")]
        [InlineData(26, "z")]
        [InlineData(27, "aa")]
        [InlineData(28, "bb")]
        [InlineData(52, "zz")]
        [InlineData(53, "aaa")]
        [InlineData(0, "0")]
        [InlineData(-1, "-1")]
        public void Format_LowerLetter(int n, string expected)
            => Assert.Equal(expected, PageNumberFormatter.Format(n, PageNumberFormat.LowerLetter));

        [Fact]
        public void Format_UpperLetter()
        {
            Assert.Equal("A", PageNumberFormatter.Format(1, PageNumberFormat.UpperLetter));
            Assert.Equal("CC", PageNumberFormatter.Format(29, PageNumberFormat.UpperLetter));
        }

        [Fact]
        public void Format_FullWidthAndArabic()
        {
            Assert.Equal("１２３", PageNumberFormatter.Format(123, PageNumberFormat.FullWidthArabic));
            Assert.Equal("０", PageNumberFormatter.Format(0, PageNumberFormat.FullWidthArabic));
            Assert.Equal("－５", PageNumberFormatter.Format(-5, PageNumberFormat.FullWidthArabic));
            Assert.Equal("123", PageNumberFormatter.Format(123, PageNumberFormat.Arabic));
            Assert.Equal("-5", PageNumberFormatter.Format(-5, PageNumberFormat.Arabic));
        }

        [Fact]
        public void Format_CircledNumber()
        {
            Assert.Equal("①", PageNumberFormatter.Format(1, PageNumberFormat.CircledNumber));
            Assert.Equal("⑩", PageNumberFormatter.Format(10, PageNumberFormat.CircledNumber));
            Assert.Equal("⑳", PageNumberFormatter.Format(20, PageNumberFormat.CircledNumber));
            // 範囲外はアラビア数字
            Assert.Equal("21", PageNumberFormatter.Format(21, PageNumberFormat.CircledNumber));
            Assert.Equal("0", PageNumberFormatter.Format(0, PageNumberFormat.CircledNumber));
        }

        [Theory]
        [InlineData(0, "〇")]
        [InlineData(1, "一")]
        [InlineData(9, "九")]
        [InlineData(10, "十")]
        [InlineData(11, "十一")]
        [InlineData(20, "二十")]
        [InlineData(21, "二十一")]
        [InlineData(100, "百")]
        [InlineData(101, "百一")]
        [InlineData(110, "百十")]
        [InlineData(123, "百二十三")]
        [InlineData(1000, "千")]
        [InlineData(2026, "二千二十六")]
        [InlineData(9999, "九千九百九十九")]
        [InlineData(10000, "10000")]
        [InlineData(-1, "-1")]
        public void Format_Kanji(int n, string expected)
            => Assert.Equal(expected, PageNumberFormatter.Format(n, PageNumberFormat.Kanji));

        // ---- 開始番号と書式（PageContext） ----

        [Fact]
        public void PageContext_DefaultsAreUnchanged()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var seen = new List<PageContext>();
            var s = AddPages(doc, 3);
            s.PageBackground = (g, pc) => seen.Add(Snapshot(pc));
            doc.ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, seen.Select(p => p.DisplayPageNumber));
            Assert.Equal(new[] { "1", "2", "3" }, seen.Select(p => p.PageNumberText));
            Assert.All(seen, p => Assert.Equal(PageNumberFormat.Arabic, p.PageNumberFormat));
            Assert.Equal(new[] { true, false, true }, seen.Select(p => p.IsOddPage));
            Assert.Equal(new[] { false, true, false }, seen.Select(p => p.IsEvenPage));
        }

        private static PageContext Snapshot(PageContext pc) => new PageContext
        {
            PageNumber = pc.PageNumber,
            DisplayPageNumber = pc.DisplayPageNumber,
            PageNumberText = pc.PageNumberText,
            PageNumberFormat = pc.PageNumberFormat,
            SectionPageNumber = pc.SectionPageNumber,
        };

        [Fact]
        public void FrontMatterRomanThenBodyFromOne()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var seen = new List<PageContext>();
            Action<PdfGraphics, PageContext> record = (g, pc) => seen.Add(Snapshot(pc));

            AddPages(doc, 2, s => { s.PageNumberFormat = PageNumberFormat.LowerRoman; s.PageBackground = record; });
            AddPages(doc, 3, s => { s.PageNumberStart = 1; s.PageNumberFormat = PageNumberFormat.Arabic; s.PageBackground = record; });
            // 開始番号も書式も指定しない: 前のセクションの続き
            AddPages(doc, 1, s => s.PageBackground = record);
            // 開始番号だけ指定: 書式は引き継ぐ
            AddPages(doc, 1, s => { s.PageNumberStart = 10; s.PageBackground = record; });
            doc.ToArray();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, seen.Select(p => p.PageNumber));
            Assert.Equal(new[] { 1, 2, 1, 2, 3, 4, 10 }, seen.Select(p => p.DisplayPageNumber));
            Assert.Equal(new[] { "i", "ii", "1", "2", "3", "4", "10" }, seen.Select(p => p.PageNumberText));
            Assert.Equal(PageNumberFormat.LowerRoman, seen[1].PageNumberFormat);
            Assert.Equal(PageNumberFormat.Arabic, seen[6].PageNumberFormat);
            Assert.Equal(new[] { 1, 2, 1, 2, 3, 1, 1 }, seen.Select(p => p.SectionPageNumber));
        }

        [Fact]
        public void FormatIsInheritedFromPreviousSection()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var seen = new List<PageContext>();
            Action<PdfGraphics, PageContext> record = (g, pc) => seen.Add(Snapshot(pc));
            AddPages(doc, 1, s => { s.PageNumberFormat = PageNumberFormat.UpperRoman; s.PageBackground = record; });
            AddPages(doc, 2, s => { s.PageNumberStart = 5; s.PageBackground = record; });
            doc.ToArray();
            Assert.Equal(new[] { "I", "V", "VI" }, seen.Select(p => p.PageNumberText));
        }

        [Fact]
        public void NegativeAndZeroStart()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var seen = new List<PageContext>();
            AddPages(doc, 3, s => { s.PageNumberStart = -1; s.PageNumberFormat = PageNumberFormat.LowerRoman; s.PageBackground = (g, pc) => seen.Add(Snapshot(pc)); });
            doc.ToArray();
            Assert.Equal(new[] { "-1", "0", "i" }, seen.Select(p => p.PageNumberText));
        }

        // ---- フィールドの表示 ----

        /// <summary>
        /// フッターのフィールドを使った文書と、期待する文字列を直接書いた文書（1 ページ 1 セクション）で、
        /// 全ページの内容が一致することを確かめる。
        /// </summary>
        private static void AssertFooterText(Action<Paragraph> fields, Action<Section> sectionSetup, string[] expectedPerPage)
        {
            var a = NewDoc();
            var b = NewDoc();
            if (a == null || b == null) return;
            AddPages(a, expectedPerPage.Length, s => { sectionSetup(s); fields(s.Footer.AddParagraph()); });
            foreach (string expected in expectedPerPage)
                AddPages(b, 1, s => s.Footer.AddParagraph().AddText(expected));
            Assert.Equal(PageContents(b, out _), PageContents(a, out _));
        }

        [Fact]
        public void PageNumberField_FollowsSectionFormat()
        {
            AssertFooterText(
                p => p.AddPageNumber(),
                s => { s.PageNumberStart = 3; s.PageNumberFormat = PageNumberFormat.LowerRoman; },
                new[] { "iii", "iv", "v" });
        }

        [Fact]
        public void PageNumberField_FormatOverride()
        {
            AssertFooterText(
                p => p.AddPageNumber(PageNumberFormat.Kanji),
                s => s.PageNumberFormat = PageNumberFormat.LowerRoman,
                new[] { "一", "二" });
            AssertFooterText(
                p => p.AddPageNumber(PageNumberFormat.CircledNumber).AddText("/").AddPageCount(),
                s => { },
                new[] { "①/2", "②/2" });
        }

        [Fact]
        public void PhysicalPageNumberAndPageCountFields()
        {
            // 開始番号を 10 にしても、PhysicalPageNumber は物理的な順番、PageCount は既定でアラビア数字
            AssertFooterText(
                p => p.AddField(FieldType.PhysicalPageNumber).AddText(":").AddPageNumber().AddText(":").AddPageCount(),
                s => { s.PageNumberStart = 10; s.PageNumberFormat = PageNumberFormat.FullWidthArabic; },
                new[] { "１:１０:2", "２:１１:2" });
            AssertFooterText(
                p => p.AddField(FieldType.PageCount, PageNumberFormat.UpperRoman),
                s => { },
                new[] { "III", "III", "III" });
        }

        [Fact]
        public void SectionFieldsFollowSectionFormat()
        {
            AssertFooterText(
                p => p.AddField(FieldType.SectionPageNumber).AddText("/").AddField(FieldType.SectionPageCount),
                s => { s.PageNumberStart = 7; s.PageNumberFormat = PageNumberFormat.UpperLetter; },
                new[] { "A/B", "B/B" });
        }

        [Fact]
        public void PageNumberInBodyUsesDisplayNumber()
        {
            var a = NewDoc();
            var b = NewDoc();
            if (a == null || b == null) return;
            var sa = a.AddSection();
            sa.PageNumberStart = 4;
            sa.PageNumberFormat = PageNumberFormat.UpperRoman;
            sa.AddParagraph("頁").AddPageNumber();
            var sb = b.AddSection();
            sb.AddParagraph("頁").AddText("IV");
            Assert.Equal(PageContents(b, out _)[0], PageContents(a, out _)[0]);
        }

        // ---- 奇数・偶数・先頭ページのヘッダー/フッター ----

        [Fact]
        public void OddEvenHeadersAndFooters()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 4, s =>
            {
                s.DifferentOddEven = true;
                s.Header.AddParagraph("奇数ヘッダー", Size(11.11));
                s.EvenHeader.AddParagraph("偶数ヘッダー", Size(12.22));
                s.Footer.AddParagraph("奇数フッター", Size(13.33));
                s.EvenFooter.AddParagraph("偶数フッター", Size(14.44));
            });
            var pages = PageContents(doc, out int n);
            Assert.Equal(4, n);
            for (int i = 0; i < 4; i++)
            {
                bool odd = i % 2 == 0;
                Assert.Equal(odd, pages[i].Contains("11.11 Tf"));
                Assert.Equal(!odd, pages[i].Contains("12.22 Tf"));
                Assert.Equal(odd, pages[i].Contains("13.33 Tf"));
                Assert.Equal(!odd, pages[i].Contains("14.44 Tf"));
            }
        }

        [Fact]
        public void OddEvenOff_UsesHeaderOnEveryPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 2, s =>
            {
                s.Header.AddParagraph("ヘッダー", Size(11.11));
                s.EvenHeader.AddParagraph("偶数", Size(12.22));   // DifferentOddEven が false なら使われない
            });
            var pages = PageContents(doc, out _);
            Assert.All(pages, p => Assert.Contains("11.11 Tf", p));
            Assert.All(pages, p => Assert.DoesNotContain("12.22 Tf", p));
        }

        [Fact]
        public void EmptyEvenHeaderShowsNothing()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 2, s =>
            {
                s.DifferentOddEven = true;
                s.Header.AddParagraph("ヘッダー", Size(11.11));
            });
            var pages = PageContents(doc, out _);
            Assert.Contains("11.11 Tf", pages[0]);
            Assert.DoesNotContain("11.11 Tf", pages[1]);
        }

        [Fact]
        public void OddEvenUsesPhysicalPageNumber_AcrossSectionsAndRestartedNumbers()
        {
            var doc = NewDoc();
            if (doc == null) return;
            Action<Section> headers = s =>
            {
                s.DifferentOddEven = true;
                s.Header.AddParagraph("奇", Size(11.11));
                s.EvenHeader.AddParagraph("偶", Size(12.22));
            };
            AddPages(doc, 1, headers);                                       // 物理 1（奇）
            AddPages(doc, 2, s => { headers(s); s.PageNumberStart = 1; });   // 物理 2, 3。表示は 1, 2 だが物理で判定する
            var pages = PageContents(doc, out _);
            Assert.Contains("11.11 Tf", pages[0]);
            Assert.Contains("12.22 Tf", pages[1]);
            Assert.Contains("11.11 Tf", pages[2]);
        }

        [Fact]
        public void FirstPageHeaderFooter_TakesPrecedencePerSection()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 3, s =>
            {
                s.DifferentFirstPage = true;
                s.DifferentOddEven = true;
                s.FirstPageHeader.AddParagraph("先頭", Size(15.55));
                s.FirstPageFooter.AddParagraph("先頭フッター", Size(16.66));
                s.Header.AddParagraph("奇数", Size(11.11));
                s.EvenHeader.AddParagraph("偶数", Size(12.22));
                s.Footer.AddParagraph("通常フッター", Size(13.33));
            });
            // 2 つ目のセクション: 物理 4 ページ目（偶数）から始まる。先頭ページ用が偶数用に優先する
            AddPages(doc, 2, s =>
            {
                s.DifferentFirstPage = true;
                s.DifferentOddEven = true;
                s.FirstPageHeader.AddParagraph("先頭2", Size(17.77));
                s.Header.AddParagraph("奇数", Size(11.11));
                s.EvenHeader.AddParagraph("偶数", Size(12.22));
            });
            var pages = PageContents(doc, out int n);
            Assert.Equal(5, n);
            Assert.Contains("15.55 Tf", pages[0]);
            Assert.Contains("16.66 Tf", pages[0]);
            Assert.DoesNotContain("11.11 Tf", pages[0]);
            Assert.DoesNotContain("13.33 Tf", pages[0]);
            Assert.Contains("12.22 Tf", pages[1]);       // 物理 2: 偶数
            Assert.DoesNotContain("13.33 Tf", pages[1]);   // 偶数ページ用のフッターは空
            Assert.Contains("11.11 Tf", pages[2]);       // 物理 3: 奇数
            Assert.Contains("13.33 Tf", pages[2]);
            Assert.Contains("17.77 Tf", pages[3]);       // 物理 4: セクション先頭
            Assert.DoesNotContain("12.22 Tf", pages[3]);
            Assert.Contains("11.11 Tf", pages[4]);       // 物理 5: 奇数
        }

        [Fact]
        public void DifferentFirstPage_EmptyFirstPageShowsNothing()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 2, s =>
            {
                s.DifferentFirstPage = true;
                s.Header.AddParagraph("ヘッダー", Size(11.11));
            });
            var pages = PageContents(doc, out _);
            Assert.DoesNotContain("11.11 Tf", pages[0]);
            Assert.Contains("11.11 Tf", pages[1]);
        }

        [Fact]
        public void ShowOnFirstPage_BehaviourIsUnchanged()
        {
            var doc = NewDoc();
            if (doc == null) return;
            AddPages(doc, 2, s =>
            {
                s.Header.ShowOnFirstPage = false;
                s.Header.AddParagraph("ヘッダー", Size(11.11));
                s.Footer.AddParagraph("フッター", Size(13.33));
            });
            var pages = PageContents(doc, out _);
            Assert.DoesNotContain("11.11 Tf", pages[0]);
            Assert.Contains("13.33 Tf", pages[0]);
            Assert.Contains("11.11 Tf", pages[1]);

            // 偶数ページ用でも同じ（セクションの 1 ページ目が偶数ページだったとき）
            var doc2 = NewDoc()!;
            AddPages(doc2, 1);
            AddPages(doc2, 1, s =>
            {
                s.DifferentOddEven = true;
                s.EvenHeader.ShowOnFirstPage = false;
                s.EvenHeader.AddParagraph("偶数", Size(12.22));
            });
            var pages2 = PageContents(doc2, out _);
            Assert.DoesNotContain("12.22 Tf", pages2[1]);
        }
    }
}
