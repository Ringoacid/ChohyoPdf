using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>生成した PDF の、ページ・しおり・リンク・ページ上の文字列を読み取る。</summary>
    internal sealed class PdfInspector
    {
        public sealed class OutlineItem
        {
            public int Id;
            public string Title = "";
            public int Parent;
            public int PageIndex;
            public double Top;
            public int Count;
            public bool HasCount;
        }

        public sealed class LinkItem
        {
            public int PageIndex;
            public int DestPageIndex = -1;
            public double DestTop;
            public string? Uri;
        }

        private readonly string _s;
        private readonly byte[] _bytes;

        public PdfInspector(byte[] bytes)
        {
            _bytes = bytes;
            _s = PdfChecker.Latin1(bytes);
            PdfChecker.AssertValidStructure(bytes);
            var kids = Regex.Match(_s, @"/Type /Pages /Kids \[([^\]]*)\]");
            Assert.True(kids.Success);
            PageIds = Regex.Matches(kids.Groups[1].Value, @"(\d+) 0 R").Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).ToList();
        }

        public List<int> PageIds { get; }

        public int PageCount => PageIds.Count;

        public bool Contains(string text) => _s.Contains(text);

        public string Catalog
        {
            get
            {
                var m = Regex.Match(_s, @"<< /Type /Catalog[^\n]*");
                Assert.True(m.Success);
                return m.Value;
            }
        }

        private string ObjectBody(int id)
        {
            string head = "\n" + id + " 0 obj\n";
            int start = _s.IndexOf(head, StringComparison.Ordinal);
            Assert.True(start >= 0, "object " + id);
            start += head.Length;
            int end = _s.IndexOf("\nendobj", start, StringComparison.Ordinal);
            return _s.Substring(start, end - start);
        }

        private static string DecodeTextString(string token)
        {
            if (token.StartsWith("<FEFF"))
            {
                string hex = token.Substring(5, token.Length - 6);
                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                return Encoding.BigEndianUnicode.GetString(bytes);
            }
            return token.Substring(1, token.Length - 2);
        }

        private int PageIndexOf(int objectId) => PageIds.IndexOf(objectId);

        /// <summary>しおりをたどる順（親、子、次の兄弟）で返す。</summary>
        public List<OutlineItem> Outlines()
        {
            var list = new List<OutlineItem>();
            var root = Regex.Match(_s, @"/Outlines (\d+) 0 R");
            if (!root.Success) return list;
            int rootId = int.Parse(root.Groups[1].Value);
            var rootBody = ObjectBody(rootId);
            Assert.Contains("/Type /Outlines", rootBody);
            var first = Regex.Match(rootBody, @"/First (\d+) 0 R");
            if (first.Success) Walk(int.Parse(first.Groups[1].Value), rootId, list);
            return list;
        }

        private void Walk(int id, int parent, List<OutlineItem> list)
        {
            while (true)
            {
                string body = ObjectBody(id);
                var item = new OutlineItem { Id = id, Parent = parent };
                item.Title = DecodeTextString(Regex.Match(body, @"/Title (\((?:\\.|[^)\\])*\)|<[0-9A-F]*>)").Groups[1].Value);
                Assert.Equal(parent, int.Parse(Regex.Match(body, @"/Parent (\d+) 0 R").Groups[1].Value));
                var dest = Regex.Match(body, @"/Dest \[(\d+) 0 R /XYZ null (\S+) null\]");
                Assert.True(dest.Success, body);
                item.PageIndex = PageIndexOf(int.Parse(dest.Groups[1].Value));
                item.Top = double.Parse(dest.Groups[2].Value, CultureInfo.InvariantCulture);
                var count = Regex.Match(body, @"/Count (-?\d+)");
                item.HasCount = count.Success;
                if (count.Success) item.Count = int.Parse(count.Groups[1].Value);
                list.Add(item);
                var child = Regex.Match(body, @"/First (\d+) 0 R");
                if (child.Success) Walk(int.Parse(child.Groups[1].Value), id, list);
                var next = Regex.Match(body, @"/Next (\d+) 0 R");
                if (!next.Success) return;
                id = int.Parse(next.Groups[1].Value);
            }
        }

        /// <summary>全ページのリンク注釈（ページ順、ページ内は追加順）。</summary>
        public List<LinkItem> Links()
        {
            var list = new List<LinkItem>();
            for (int p = 0; p < PageIds.Count; p++)
            {
                var annots = Regex.Match(ObjectBody(PageIds[p]), @"/Annots \[([^\]]*)\]");
                if (!annots.Success) continue;
                foreach (Match a in Regex.Matches(annots.Groups[1].Value, @"(\d+) 0 R"))
                {
                    string body = ObjectBody(int.Parse(a.Groups[1].Value));
                    var link = new LinkItem { PageIndex = p };
                    var dest = Regex.Match(body, @"/Dest \[(\d+) 0 R /XYZ null (\S+) null\]");
                    if (dest.Success)
                    {
                        link.DestPageIndex = PageIndexOf(int.Parse(dest.Groups[1].Value));
                        link.DestTop = double.Parse(dest.Groups[2].Value, CultureInfo.InvariantCulture);
                    }
                    var uri = Regex.Match(body, @"/URI \(([^)]*)\)");
                    if (uri.Success) link.Uri = uri.Groups[1].Value;
                    list.Add(link);
                }
            }
            return list;
        }

        /// <summary>ページごとの文字列（描画した順の、同じ書式の連なりごと）。ToUnicode で字形から文字に戻す。</summary>
        public List<List<string>> PageRuns()
        {
            var streams = PdfChecker.DecodedStreams(_bytes);
            var map = new Dictionary<int, string>();
            var cmap = streams.FirstOrDefault(c => c.Contains("beginbfchar"));
            if (cmap != null)
            {
                foreach (Match m in Regex.Matches(cmap, @"<([0-9A-F]{4})> <([0-9A-F]+)>"))
                {
                    var sb = new StringBuilder();
                    string hex = m.Groups[2].Value;
                    for (int i = 0; i < hex.Length; i += 4) sb.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));
                    map[Convert.ToInt32(m.Groups[1].Value, 16)] = sb.ToString();
                }
            }
            var pages = new List<List<string>>();
            foreach (string content in streams.Where(c => c.StartsWith("1 0 0 -1 0 ")))
            {
                var runs = new List<string>();
                foreach (Match m in Regex.Matches(content, @"<([0-9A-F]+)> Tj"))
                {
                    var sb = new StringBuilder();
                    string hex = m.Groups[1].Value;
                    for (int i = 0; i < hex.Length; i += 4) sb.Append(map.TryGetValue(Convert.ToInt32(hex.Substring(i, 4), 16), out string? t) ? t : "?");
                    runs.Add(sb.ToString());
                }
                pages.Add(runs);
            }
            Assert.Equal(PageIds.Count, pages.Count);
            return pages;
        }
    }

    /// <summary>しおり（アウトライン）・文書内リンク・ページ参照・目次のテスト。</summary>
    public class BookmarkAndTocTests
    {
        private static Document? NewDoc()
        {
            var font = TestFonts.Japanese;
            if (font == null) return null;
            var doc = new Document(font, 10.5);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return doc;
        }

        private static Paragraph Heading(BlockContainer c, string text, int level, bool newPage = true)
        {
            var p = c.AddParagraph(text).SetOutline(level).SetBold();
            if (newPage) p.SetPageBreakBefore();
            return p;
        }

        private static int PageOfRun(PdfInspector pdf, string text, int firstPage = 0)
        {
            var pages = pdf.PageRuns();
            for (int i = firstPage; i < pages.Count; i++)
            {
                if (pages[i].Contains(text)) return i;
            }
            Assert.Fail("文字列 '" + text + "' が見つかりません");
            return -1;
        }

        // ---- しおり ----

        [Fact]
        public void Bookmarks_NestByLevel()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            Heading(s, "A", 1);
            Heading(s, "A1", 2);
            Heading(s, "A1a", 3);
            Heading(s, "A2", 2);
            Heading(s, "B", 1);
            Heading(s, "B-deep", 3);     // レベルが飛んでも、直前の浅い見出しの子になる
            Heading(s, "B-shallow", 2);  // 3 の次に 2 が来たら、B の子（B-deep の兄弟）
            s.AddParagraph("見出しではない段落");

            var pdf = new PdfInspector(doc.ToArray());
            var items = pdf.Outlines();
            Assert.Equal(new[] { "A", "A1", "A1a", "A2", "B", "B-deep", "B-shallow" }, items.Select(i => i.Title));
            // 各見出しはそれぞれのページ（1 ページ 1 見出し）
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, items.Select(i => i.PageIndex));
            // Count: 開いた項目の子孫の数
            Assert.Equal(3, items.First(i => i.Title == "A").Count);
            Assert.Equal(1, items.First(i => i.Title == "A1").Count);
            Assert.Equal(2, items.First(i => i.Title == "B").Count);
            Assert.False(items.First(i => i.Title == "A2").HasCount);
        }

        [Fact]
        public void Bookmarks_ParentStructure()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            Heading(s, "A", 1, false);
            Heading(s, "A1", 2, false);
            Heading(s, "A1a", 3, false);
            Heading(s, "A2", 2, false);
            Heading(s, "B", 1, false);
            Heading(s, "B-deep", 3, false);
            var items = new PdfInspector(doc.ToArray()).Outlines();
            var byTitle = items.ToDictionary(i => i.Title);
            var rootParent = byTitle["A"].Parent;
            Assert.Equal(rootParent, byTitle["B"].Parent);
            Assert.Equal(byTitle["A"].Id, byTitle["A1"].Parent);
            Assert.Equal(byTitle["A1"].Id, byTitle["A1a"].Parent);
            Assert.Equal(byTitle["A"].Id, byTitle["A2"].Parent);
            Assert.Equal(byTitle["B"].Id, byTitle["B-deep"].Parent);
        }

        [Fact]
        public void Bookmarks_TitleIsUtf16BeAndDestinationHasPosition()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddSpacer(100);
            s.AddParagraph("第1章　はじめに").SetOutline(1);
            var bytes = doc.ToArray();
            var pdf = new PdfInspector(bytes);
            Assert.Contains("/Title <FEFF", PdfChecker.Latin1(bytes));
            var item = Assert.Single(pdf.Outlines());
            Assert.Equal("第1章　はじめに", item.Title);
            // 見出しは余白の上端から 100 ポイント下。PDF の座標は上向きなので「ページの高さ - 位置」
            var setup = s.PageSetup;
            Assert.Equal(setup.Size.Height - setup.Margins.Top - 100, item.Top, 2);
        }

        [Fact]
        public void Bookmarks_OutlineTitleAndStyleLevel()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.Styles["章"] = new ParagraphStyle(null, new ParagraphFormat { OutlineLevel = 1 });
            doc.Styles["節"] = new ParagraphStyle(null, new ParagraphFormat { OutlineLevel = 2 });
            var s = doc.AddSection();
            s.AddParagraph("第 1 章", "章");
            var p = s.AddParagraph("1.1", "節");
            p.OutlineTitle = "1.1 節の題";
            s.AddParagraph("改行を\n含む\t見出し", "節");
            s.AddParagraph("フィールド").AddPageNumber();   // フィールドは文字列に含めない
            s.Blocks[3] = new Paragraph("対象外");
            var items = new PdfInspector(doc.ToArray()).Outlines();
            Assert.Equal(new[] { "第 1 章", "1.1 節の題", "改行を 含む 見出し" }, items.Select(i => i.Title));
        }

        [Fact]
        public void Bookmarks_FieldsAreExcludedFromTitle()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("第").SetOutline(1).AddPageNumber().AddText("章");
            var item = Assert.Single(new PdfInspector(doc.ToArray()).Outlines());
            Assert.Equal("第章", item.Title);
        }

        [Fact]
        public void Bookmarks_NoHeadings_OutputIsUnchanged()
        {
            var a = NewDoc();
            var b = NewDoc();
            if (a == null || b == null) return;
            b.CreateBookmarks = false;
            b.ShowBookmarkPane = true;     // しおりがなければ何も変わらない
            foreach (var d in new[] { a, b })
            {
                var s = d.AddSection();
                s.AddParagraph("本文です。").AddPageNumber();
                s.AddParagraph("リンク").AddLink("x", "https://example.com/");
            }
            var bytes = a.ToArray();
            Assert.Equal(bytes, b.ToArray());
            Assert.DoesNotContain("/Outlines", PdfChecker.Latin1(bytes));
            Assert.DoesNotContain("/PageMode", PdfChecker.Latin1(bytes));
        }

        [Fact]
        public void Bookmarks_CanBeDisabled_AndPaneCanBeShown()
        {
            var doc = NewDoc();
            if (doc == null) return;
            Heading(doc.AddSection(), "見出し", 1, false);
            var pdf = new PdfInspector(doc.ToArray());
            Assert.Single(pdf.Outlines());
            Assert.DoesNotContain("/PageMode", pdf.Catalog);

            doc.ShowBookmarkPane = true;
            Assert.Contains("/PageMode /UseOutlines", new PdfInspector(doc.ToArray()).Catalog);

            doc.CreateBookmarks = false;
            var off = new PdfInspector(doc.ToArray());
            Assert.Empty(off.Outlines());
            Assert.DoesNotContain("/PageMode", off.Catalog);
        }

        [Fact]
        public void Bookmarks_HeadingInTableCell()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("前置き");
            var table = s.AddTable();
            table.AddColumn(200);
            table.AddRow().Cells[0].AddParagraph("セルの見出し").SetOutline(2);
            var items = new PdfInspector(doc.ToArray()).Outlines();
            Assert.Equal(new[] { "セルの見出し" }, items.Select(i => i.Title));
        }

        [Fact]
        public void OutlineLevelIsValidated()
        {
            var format = new ParagraphFormat();
            Assert.Throws<ArgumentOutOfRangeException>(() => format.OutlineLevel = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => format.OutlineLevel = 10);
            format.OutlineLevel = 9;
            Assert.Equal(9, format.Clone().OutlineLevel);
            Assert.Equal(3, new ParagraphFormat { OutlineLevel = 1 }.Merge(new ParagraphFormat { OutlineLevel = 3 }).OutlineLevel);
            Assert.Equal(1, new ParagraphFormat { OutlineLevel = 1 }.Merge(new ParagraphFormat()).OutlineLevel);
        }

        [Fact]
        public void LowLevel_BookmarksAndPageLinks()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            var pdf = new PdfDocument();
            var p0 = pdf.AddPage();
            var p1 = pdf.AddPage();
            var p2 = pdf.AddPage();
            var parent = p0.AddBookmark("日本語の章");
            parent.AddChild("English child", p2, 120).AddChild("孫", p1);
            pdf.AddBookmark("Last", p1, 30);
            pdf.ShowBookmarkPane = true;
            p0.AddLink(new PdfRect(10, 10, 100, 20), p2, 50);
            p0.Graphics.Translate(0, 100);
            p0.Graphics.AddLink(new PdfRect(10, 10, 100, 20), p1);   // 現在の座標系の矩形

            var bytes = pdf.ToArray();
            var info = new PdfInspector(bytes);
            Assert.Contains("/PageMode /UseOutlines", info.Catalog);
            var items = info.Outlines();
            Assert.Equal(new[] { "日本語の章", "English child", "孫", "Last" }, items.Select(i => i.Title));
            Assert.Equal(new[] { 0, 2, 1, 1 }, items.Select(i => i.PageIndex));
            double h = p0.Height;
            Assert.Equal(h, items[0].Top, 3);
            Assert.Equal(h - 120, items[1].Top, 3);
            Assert.Equal(h - 30, items[3].Top, 3);
            Assert.Equal(2, items[0].Count);

            var links = info.Links();
            Assert.Equal(2, links.Count);
            Assert.Equal(2, links[0].DestPageIndex);
            Assert.Equal(h - 50, links[0].DestTop, 3);
            Assert.Equal(1, links[1].DestPageIndex);
            string text = PdfChecker.Latin1(bytes);
            // 2 つ目のリンクの領域は、座標変換（+100）を反映する
            var rect = Regex.Matches(text, @"/Subtype /Link /Rect \[([^\]]*)\]")[1].Groups[1].Value.Split(' ');
            Assert.Equal(h - 130, double.Parse(rect[1], CultureInfo.InvariantCulture), 3);
        }

        [Fact]
        public void LowLevel_RejectsPagesOfAnotherDocument()
        {
            var a = new PdfDocument();
            var b = new PdfDocument();
            var pa = a.AddPage();
            var pb = b.AddPage();
            Assert.Throws<ArgumentException>(() => a.AddBookmark("x", pb));
            Assert.Throws<ArgumentException>(() => pa.AddLink(new PdfRect(0, 0, 10, 10), pb));
            Assert.Throws<ArgumentException>(() => pa.AddBookmark("x").AddChild("y", pb));
            Assert.Throws<ArgumentNullException>(() => a.AddBookmark(null!, pa));
        }

        // ---- 文書内リンク ----

        [Fact]
        public void InternalLinks_JumpToAnchorsOnLaterAndEarlierPages()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("先頭").SetAnchor("top");
            s.AddParagraph("前方へ: ").AddInternalLink("第 3 章へ", "ch3");
            s.AddPageBreak();
            s.AddParagraph("2 ページ目");
            s.AddPageBreak();
            s.AddSpacer(200);
            s.AddParagraph("第 3 章").SetAnchor("ch3");
            s.AddParagraph("後方へ: ").AddInternalLink("先頭へ", "top");

            var pdf = new PdfInspector(doc.ToArray());
            Assert.Equal(3, pdf.PageCount);
            var links = pdf.Links();
            Assert.Equal(2, links.Count);
            Assert.Equal(0, links[0].PageIndex);
            Assert.Equal(2, links[0].DestPageIndex);
            var setup = doc.Sections[0].PageSetup;
            Assert.Equal(setup.Size.Height - setup.Margins.Top - 200, links[0].DestTop, 2);
            Assert.Equal(2, links[1].PageIndex);
            Assert.Equal(0, links[1].DestPageIndex);
            Assert.Equal(setup.Size.Height - setup.Margins.Top, links[1].DestTop, 2);
        }

        [Fact]
        public void InternalLinks_InlineAnchorUsesItsLine()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.PageSetup.Margins = Thickness.FromMm(20);
            var p = s.AddParagraph().SetLineSpacing(1.5);
            // 3 行目に入るように、改行を入れてからアンカーを置く
            p.AddText("1 行目\n2 行目\n").AddAnchor("mid").AddText("3 行目");
            s.AddParagraph("戻る").AddInternalLink("リンク", "mid");
            var links = new PdfInspector(doc.ToArray()).Links();
            var link = Assert.Single(links);
            var setup = s.PageSetup;
            double lineHeight = 10.5 * 1.5;
            Assert.Equal(setup.Size.Height - setup.Margins.Top - 2 * lineHeight, link.DestTop, 2);
        }

        [Fact]
        public void InternalLinks_MissingAnchorThrows()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("x").SetAnchor("here");
            s.AddParagraph().AddInternalLink("リンク", "typo");
            var ex = Assert.Throws<InvalidOperationException>(() => doc.ToArray());
            Assert.Contains("typo", ex.Message);

            var doc2 = NewDoc()!;
            doc2.AddSection().AddParagraph("p").AddPageRef("nothing");
            Assert.Contains("nothing", Assert.Throws<InvalidOperationException>(() => doc2.ToArray()).Message);
        }

        [Fact]
        public void Anchors_DuplicateNamesThrow()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("a").SetAnchor("x");
            s.AddParagraph("b").AddAnchor("x");
            Assert.Contains("x", Assert.Throws<InvalidOperationException>(() => doc.ToArray()).Message);
            Assert.Throws<ArgumentException>(() => new Paragraph().SetAnchor(""));
            Assert.Throws<ArgumentException>(() => new Paragraph().AddInternalLink("a", ""));
        }

        [Fact]
        public void InternalLinks_UrlLinksAreStillWritten()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("a").SetAnchor("a");
            s.AddParagraph().AddLink("外部", "https://example.com/").AddInternalLink("内部", "a");
            var links = new PdfInspector(doc.ToArray()).Links();
            Assert.Equal(2, links.Count);
            Assert.Equal("https://example.com/", links[0].Uri);
            Assert.Equal(0, links[1].DestPageIndex);
        }

        // ---- ページ参照 ----

        [Fact]
        public void PageRef_ForwardAndBackwardReferences()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.AddParagraph("序").SetAnchor("intro").AddText("　前方参照: 第 ").AddPageRef("late").AddText(" 頁");
            for (int i = 0; i < 4; i++)
            {
                s.AddPageBreak();
                s.AddParagraph("本文 " + (i + 2));
            }
            s.AddPageBreak();
            s.AddParagraph("後ろの章").SetAnchor("late").AddText("　後方参照: ").AddPageRef("intro");
            var pdf = new PdfInspector(doc.ToArray());
            Assert.Equal(6, pdf.PageCount);
            var pages = pdf.PageRuns();
            Assert.Equal("序　前方参照: 第 6 頁", string.Concat(pages[0]));
            Assert.Equal("後ろの章　後方参照: 1", string.Concat(pages[5]));
        }

        [Fact]
        public void PageRef_UsesTheSectionFormatOfTheTargetPage()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var front = doc.AddSection();
            front.PageNumberFormat = PageNumberFormat.LowerRoman;
            front.AddParagraph("参照: ").AddPageRef("body").AddText(" / ").AddPageRef("body", PageNumberFormat.Kanji).AddText(" / ").AddPageRef("pref");
            front.AddPageBreak();
            front.AddParagraph("前付け 2").SetAnchor("pref");
            var body = doc.AddSection();
            body.PageNumberStart = 1;
            body.PageNumberFormat = PageNumberFormat.Arabic;
            body.AddParagraph("本文 1");
            body.AddPageBreak();
            body.AddParagraph("本文 2").SetAnchor("body");

            var pages = new PdfInspector(doc.ToArray()).PageRuns();
            // 本文 2 は、通しの 4 ページ目で表示番号は 2（セクションの書式はアラビア数字）。前付け 2 はローマ数字の ii
            Assert.Equal("参照: 2 / 二 / ii", string.Concat(pages[0]));
        }

        [Fact]
        public void PageRef_InHeaderAndFooter()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            s.Footer.AddParagraph("巻末は ").AddPageRef("end");
            s.AddParagraph("1");
            s.AddPageBreak();
            s.AddParagraph("2");
            s.AddPageBreak();
            s.AddParagraph("3").SetAnchor("end");
            var pages = new PdfInspector(doc.ToArray()).PageRuns();
            Assert.Equal(new[] { "1巻末は 3", "2巻末は 3", "3巻末は 3" }, pages.Select(p => string.Concat(p)));
        }

        // ---- 目次 ----

        /// <summary>
        /// 目次が 2 ページ以上になる文書。見出しの本文は「本文 NNN」、目次には OutlineTitle の「項目 NNN」が出る。
        /// 見出し NNN は 1 ページ 1 つで、目次の後ろにある。
        /// </summary>
        private static Document? BuildLongTocDocument(int headings, out Section? body)
        {
            var doc = NewDoc();
            body = null;
            if (doc == null) return null;
            var s = doc.AddSection();
            s.AddTableOfContents("目次");
            for (int i = 1; i <= headings; i++)
            {
                var p = s.AddParagraph("本文 " + i.ToString("000")).SetOutline(1);
                p.OutlineTitle = "項目 " + i.ToString("000");
                p.SetPageBreakBefore();
            }
            body = s;
            return doc;
        }

        private static List<string> TocPageNumbers(PdfInspector pdf, int count, string prefix)
        {
            var runs = pdf.PageRuns().SelectMany(p => p).ToList();
            var numbers = new List<string>();
            for (int i = 1; i <= count; i++)
            {
                int at = runs.IndexOf(prefix + i.ToString("000"));
                Assert.True(at >= 0, "目次に " + prefix + i + " がありません");
                numbers.Add(runs[at + 1]);
            }
            return numbers;
        }

        [Fact]
        public void Toc_MultiPage_NumbersMatchActualPages()
        {
            var doc = BuildLongTocDocument(70, out _);
            if (doc == null) return;
            var pdf = new PdfInspector(doc.ToArray());
            int firstBodyPage = PageOfRun(pdf, "本文 001");
            Assert.True(firstBodyPage >= 2, "目次が 2 ページ以上になるはず: " + firstBodyPage);
            Assert.Equal(firstBodyPage + 70, pdf.PageCount);

            var numbers = TocPageNumbers(pdf, 70, "項目 ");
            for (int i = 1; i <= 70; i++)
            {
                int actual = PageOfRun(pdf, "本文 " + i.ToString("000"), firstBodyPage) + 1;
                Assert.Equal(actual.ToString(CultureInfo.InvariantCulture), numbers[i - 1]);
            }
        }

        [Fact]
        public void Toc_EntriesLinkToHeadingsAndBookmarksAgree()
        {
            var doc = BuildLongTocDocument(60, out _);
            if (doc == null) return;
            var pdf = new PdfInspector(doc.ToArray());
            int firstBodyPage = PageOfRun(pdf, "本文 001");
            var links = pdf.Links();
            Assert.Equal(60, links.Count);
            Assert.All(links, l => Assert.True(l.PageIndex < firstBodyPage));
            Assert.Equal(Enumerable.Range(0, 60).Select(i => firstBodyPage + i), links.Select(l => l.DestPageIndex));
            // しおりも同じページを指す（目次の見出しは、レベルを指定していないのでしおりに入らない）
            var items = pdf.Outlines();
            Assert.Equal(60, items.Count);
            Assert.Equal(Enumerable.Range(0, 60).Select(i => firstBodyPage + i), items.Select(l => l.PageIndex));
            Assert.Equal("項目 001", items[0].Title);
        }

        [Fact]
        public void Toc_PagesStayConsistentWhenTheTocGrows()
        {
            // 見出しを増やすと目次が 3 ページになり、本文のページがさらにずれる
            var doc = BuildLongTocDocument(120, out _);
            if (doc == null) return;
            var pdf = new PdfInspector(doc.ToArray());
            int firstBodyPage = PageOfRun(pdf, "本文 001");
            Assert.True(firstBodyPage >= 3, firstBodyPage.ToString());
            var numbers = TocPageNumbers(pdf, 120, "項目 ");
            for (int i = 1; i <= 120; i++)
                Assert.Equal((firstBodyPage + i).ToString(CultureInfo.InvariantCulture), numbers[i - 1]);
        }

        [Fact]
        public void Toc_FrontMatterInRomanNumerals()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var front = doc.AddSection();
            front.PageNumberFormat = PageNumberFormat.LowerRoman;
            front.AddTableOfContents("目次", maxLevel: 2);
            Heading(front, "序文", 1);
            var body = doc.AddSection();
            body.PageNumberStart = 1;
            body.PageNumberFormat = PageNumberFormat.Arabic;
            for (int i = 1; i <= 50; i++)
            {
                Heading(body, "第" + i + "章", 1);
                if (i % 10 == 0) Heading(body, "節 " + i, 2, false);
            }
            var pdf = new PdfInspector(doc.ToArray());
            var runs = pdf.PageRuns();
            var all = runs.SelectMany(p => p).ToList();
            string TocNumber(string title) => all[all.IndexOf(title) + 1];
            // 見出しのページ（目次にも同じ文字列があるので、最後に現れるページ）
            int PageOf(string title) => Enumerable.Range(0, runs.Count).Last(k => runs[k].Contains(title));

            int prefacePage = PageOf("序文");
            Assert.True(prefacePage >= 2, "目次が 2 ページ以上、序文はその後ろ: " + prefacePage);
            int frontPages = prefacePage + 1;
            // 前付けのページ番号はローマ数字
            Assert.Equal(PageNumberFormatter.Format(frontPages, PageNumberFormat.LowerRoman), TocNumber("序文"));
            // 本文は 1 から数え直し、アラビア数字
            for (int i = 1; i <= 50; i++)
                Assert.Equal((PageOf("第" + i + "章") + 1 - frontPages).ToString(CultureInfo.InvariantCulture), TocNumber("第" + i + "章"));
            // レベル 2 の見出しも入る（maxLevel = 2）
            Assert.Equal((PageOf("節 10") + 1 - frontPages).ToString(CultureInfo.InvariantCulture), TocNumber("節 10"));
        }

        [Fact]
        public void Toc_LevelLimitIndentAndOptions()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var toc = s.AddTableOfContents("もくじ", maxLevel: 2);
            for (int level = 1; level <= 4; level++)
            {
                var p = s.AddParagraph("本文" + level).SetOutline(level);
                p.OutlineTitle = "T" + level;
            }
            var pdf = new PdfInspector(doc.ToArray());
            var runs = pdf.PageRuns().SelectMany(p => p).ToList();
            Assert.Contains("もくじ", runs);
            Assert.Contains("T1", runs);
            Assert.Contains("T2", runs);
            Assert.DoesNotContain("T3", runs);
            Assert.DoesNotContain("T4", runs);
            Assert.Equal(2, pdf.Links().Count);
            // しおりには、レベルの上限に関係なくすべての見出しが入る
            Assert.Equal(4, pdf.Outlines().Count);

            toc.Hyperlinks = false;
            toc.ShowPageNumbers = false;
            toc.MinLevel = 2;
            toc.Title = null;
            var plain = new PdfInspector(doc.ToArray());
            var plainRuns = plain.PageRuns().SelectMany(p => p).ToList();
            Assert.Empty(plain.Links());
            Assert.DoesNotContain("T1", plainRuns);
            Assert.Contains("T2", plainRuns);
            Assert.DoesNotContain("もくじ", plainRuns);
            // ページ番号を出さないので、T2 の次の文字列は本文（"本文1"）
            Assert.Equal("本文1", plainRuns[plainRuns.IndexOf("T2") + 1]);

            toc.MinLevel = 3;
            toc.MaxLevel = 2;
            Assert.Throws<InvalidOperationException>(() => doc.ToArray());
        }

        [Fact]
        public void Toc_TitleStyleAndEntryStyles()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.Styles["目次見出し"] = new ParagraphStyle(new TextStyle { FontSize = 23.45 }, null);
            doc.Styles["目次1"] = new ParagraphStyle(new TextStyle { FontSize = 11.11 }, null);
            doc.Styles["目次2"] = new ParagraphStyle(new TextStyle { FontSize = 12.22 }, null);
            var s = doc.AddSection();
            var toc = s.AddTableOfContents();
            toc.TitleStyleName = "目次見出し";
            toc.EntryStyleName = "目次1";
            toc.LevelStyleNames[2] = "目次2";
            toc.TitleOutlineLevel = 1;
            s.AddParagraph("章").SetOutline(1);
            s.AddParagraph("節").SetOutline(2);

            var bytes = doc.ToArray();
            var content = PdfChecker.DecodedStreams(bytes).First(c => c.StartsWith("1 0 0 -1 0 "));
            Assert.Contains("23.45 Tf", content);
            Assert.Contains("11.11 Tf", content);
            Assert.Contains("12.22 Tf", content);
            // 目次の見出しもしおりに入る（章、節は目次の見出しの子ではなく、同じ階層）
            var items = new PdfInspector(bytes).Outlines();
            Assert.Equal(new[] { "目次", "章", "節" }, items.Select(i => i.Title));
        }

        [Fact]
        public void Toc_NumberFormatOverride()
        {
            var doc = NewDoc();
            if (doc == null) return;
            var s = doc.AddSection();
            var toc = s.AddTableOfContents("目次");
            toc.PageNumberFormat = PageNumberFormat.Kanji;
            s.AddParagraph("章").SetOutline(1).SetPageBreakBefore();
            var runs = new PdfInspector(doc.ToArray()).PageRuns().SelectMany(p => p).ToList();
            Assert.Equal("二", runs[runs.IndexOf("章") + 1]);
        }

        [Fact]
        public void Toc_UserDrawingCallbacksRunOnce()
        {
            var doc = NewDoc();
            if (doc == null) return;
            int backgrounds = 0, drawings = 0, foregrounds = 0;
            var s = doc.AddSection();
            s.PageBackground = (g, pc) => backgrounds++;
            s.PageForeground = (g, pc) => foregrounds++;
            s.AddTableOfContents();
            s.AddDrawing(30, (g, r) => drawings++);
            s.AddParagraph("章").SetOutline(1).SetPageBreakBefore();
            s.AddDrawing(30, (g, r) => drawings++);
            var pdf = new PdfInspector(doc.ToArray());
            Assert.Equal(2, pdf.PageCount);
            Assert.Equal(2, backgrounds);
            Assert.Equal(2, foregrounds);
            Assert.Equal(2, drawings);
        }

        [Fact]
        public void Toc_EmptyDocumentHasOnlyTheTitle()
        {
            var doc = NewDoc();
            if (doc == null) return;
            doc.AddSection().AddTableOfContents("目次");
            var pdf = new PdfInspector(doc.ToArray());
            Assert.Equal(1, pdf.PageCount);
            Assert.Equal(new[] { "目次" }, pdf.PageRuns()[0]);
        }
    }
}
