using System;
using System.Collections.Generic;

namespace ChohyoPdf.Layout
{
    /// <summary>文書をページに割り付け、PDF に描画する。</summary>
    internal sealed class DocumentRenderer
    {
        private sealed class PageRecord
        {
            public Section Section = null!;
            public int SectionIndex;
            public int SectionPage;

            /// <summary>表示用のページ番号（数値）。</summary>
            public int DisplayNumber;

            /// <summary>ページ番号の書式。</summary>
            public PageNumberFormat NumberFormat;

            public DrawAction? Body;

            /// <summary>表示用のページ番号の文字列（見出しのページ参照などから再利用できる）。</summary>
            public string DisplayText => PageNumberFormatter.Format(DisplayNumber, NumberFormat);
        }

        private readonly Document _doc;

        public DocumentRenderer(Document doc)
        {
            _doc = doc;
        }

        /// <summary>ページ番号を確定するためのレイアウトの繰り返しの上限。</summary>
        private const int MaxPasses = 10;

        private StyleScope BaseScope(DocumentIndex index) => StyleScope.Root(_doc, index);

        /// <summary>
        /// 文書をレイアウトして PDF にする。ページ参照（<see cref="FieldType.PageRef"/>）や目次があるときは、
        /// 見出しのページが変わらなくなるまで、描画命令を作らない予備のレイアウトを繰り返してから、最後に本番の描画を行う
        /// （利用者の描画処理は本番の 1 回だけ呼ばれる）。
        /// </summary>
        public PdfDocument Render(Func<PdfDocument> create)
        {
            var sections = new List<Section>(_doc.Sections);
            if (sections.Count == 0) sections.Add(_doc.AddSection());

            var scan = DocumentScanner.Scan(_doc, sections);
            var assumed = new AnchorTable();
            if (scan.UsesReferences)
            {
                bool stable = false;
                for (int pass = 1; pass <= MaxPasses && !stable; pass++)
                {
                    var measured = RunPass(create(), sections, scan, assumed, true);
                    stable = measured.Anchors.SameAs(assumed);
                    assumed = measured.Anchors;
                }
                if (!stable)
                    throw new InvalidOperationException("目次とページ参照のページ番号が確定しません（" + MaxPasses + " 回レイアウトを繰り返しても、見出しのページが変わり続けます）。");
            }

            var pdf = create();
            var final = RunPass(pdf, sections, scan, assumed, false);
            foreach (string name in final.Index.Referenced)
            {
                if (!final.Anchors.Contains(name))
                    throw new InvalidOperationException("アンカー '" + name + "' がありません。Paragraph.SetAnchor / AddAnchor で本文の段落に付けた名前を指定してください。");
            }
            AddBookmarks(pdf, final.Hits);
            return pdf;
        }

        private sealed class PassResult
        {
            public AnchorTable Anchors = null!;
            public List<HeadingHit> Hits = null!;
            public DocumentIndex Index = null!;
        }

        /// <summary>見出しの位置から、PDF のしおりを作る。見出しレベルの大小で入れ子にする。</summary>
        private void AddBookmarks(PdfDocument pdf, List<HeadingHit> hits)
        {
            if (!_doc.CreateBookmarks || hits.Count == 0) return;
            hits.Sort((a, b) => a.Heading.Index.CompareTo(b.Heading.Index));
            var levels = new List<int>();
            var parents = new List<PdfBookmark>();
            foreach (var hit in hits)
            {
                while (levels.Count > 0 && levels[levels.Count - 1] >= hit.Heading.Level)
                {
                    levels.RemoveAt(levels.Count - 1);
                    parents.RemoveAt(parents.Count - 1);
                }
                var bookmark = parents.Count == 0
                    ? pdf.AddBookmark(hit.Heading.Title, hit.Page, hit.Y)
                    : parents[parents.Count - 1].AddChild(hit.Heading.Title, hit.Page, hit.Y);
                levels.Add(hit.Heading.Level);
                parents.Add(bookmark);
            }
        }

        /// <summary>
        /// 1 回分のレイアウトと描画。<paramref name="measuring"/> が true なら、ページ番号を調べるだけの予備の描画で、
        /// ヘッダー/フッター・背景/前景・図形ブロックの利用者の描画処理は呼ばない。
        /// </summary>
        private PassResult RunPass(PdfDocument pdf, List<Section> sections, DocumentScan scan, AnchorTable assumed, bool measuring)
        {
            var index = new DocumentIndex(scan, assumed);
            var collector = new PassCollector(pdf);
            var records = Paginate(sections, 0, null, index, collector, out bool countUsed);
            if (countUsed)
            {
                var sectionCounts = CountSectionPages(records, sections.Count);
                records = Paginate(sections, records.Count, sectionCounts, index, collector, out _);
            }
            var counts = CountSectionPages(records, sections.Count);
            DrawPages(pdf, records, counts, index, collector, measuring);
            return new PassResult { Anchors = collector.Anchors, Hits = collector.Hits, Index = index };
        }

        private void DrawPages(PdfDocument pdf, List<PageRecord> records, int[] counts, DocumentIndex index, PassCollector collector, bool measuring)
        {
            var scope = BaseScope(index).ForInnerContainer().WithoutFootnotes();
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var setup = rec.Section.PageSetup;
                var page = pdf.AddPage(setup.Size);
                var g = page.Graphics;
                g.IsMeasuring = measuring;
                var m = setup.Margins;
                var pc = new PageContext
                {
                    PageNumber = i + 1,
                    PageCount = records.Count,
                    SectionPageNumber = rec.SectionPage,
                    SectionPageCount = counts[rec.SectionIndex],
                    DisplayPageNumber = rec.DisplayNumber,
                    PageNumberText = rec.DisplayText,
                    PageNumberFormat = rec.NumberFormat,
                    PageSize = setup.Size,
                    ContentArea = new PdfRect(m.Left, m.Top, setup.ContentWidth, setup.ContentHeight),
                    ColumnCount = setup.ColumnCount,
                    ColumnAreas = ColumnAreas(setup),
                    Page = page,
                };
                var ctx = new LayoutContext { Page = pc };
                collector.BeginPage(i, page, rec.DisplayNumber, rec.NumberFormat, rec.DisplayText);

                if (!measuring) rec.Section.PageBackground?.Invoke(g, pc);

                var header = measuring ? null : SelectHeaderFooter(rec.Section, false, pc.PageNumber, rec.SectionPage);
                if (header != null && header.Blocks.Count > 0)
                {
                    var node = new ContainerNode(header.Blocks, scope, allowPageBreaks: false);
                    var slice = node.Layout(ctx, setup.ContentWidth, double.PositiveInfinity, null, true);
                    slice.Draw?.Invoke(g, m.Left, setup.HeaderDistance);
                }

                rec.Body?.Invoke(g, m.Left, m.Top);

                var footer = measuring ? null : SelectHeaderFooter(rec.Section, true, pc.PageNumber, rec.SectionPage);
                if (footer != null && footer.Blocks.Count > 0)
                {
                    var node = new ContainerNode(footer.Blocks, scope, allowPageBreaks: false);
                    var slice = node.Layout(ctx, setup.ContentWidth, double.PositiveInfinity, null, true);
                    slice.Draw?.Invoke(g, m.Left, setup.Size.Height - setup.FooterDistance - slice.Height);
                }

                if (!measuring) rec.Section.PageForeground?.Invoke(g, pc);
            }
        }

        /// <summary>
        /// ページに表示するヘッダー/フッターを選ぶ。セクションの 1 ページ目専用が最優先、
        /// 次に偶数ページ用（通しページ番号で判定）、それ以外は通常のもの。表示しないときは null。
        /// </summary>
        private static HeaderFooter? SelectHeaderFooter(Section section, bool footer, int pageNumber, int sectionPage)
        {
            if (section.DifferentFirstPage && sectionPage == 1)
                return footer ? section.FirstPageFooter : section.FirstPageHeader;
            bool even = section.DifferentOddEven && pageNumber % 2 == 0;
            var hf = even
                ? (footer ? section.EvenFooter : section.EvenHeader)
                : (footer ? section.Footer : section.Header);
            return hf.ShowOnFirstPage || sectionPage > 1 ? hf : null;
        }

        /// <summary>段ごとの領域（ページ座標、左の段から順）。</summary>
        private static PdfRect[] ColumnAreas(PageSetup setup)
        {
            var areas = new PdfRect[setup.ColumnCount];
            double width = setup.ColumnWidth;
            for (int i = 0; i < areas.Length; i++)
                areas[i] = new PdfRect(setup.Margins.Left + i * (width + setup.ColumnSpacing), setup.Margins.Top, width, setup.ContentHeight);
            return areas;
        }

        /// <summary>
        /// 1 ページ分の本文を、段の数だけ左の段から順に配置する。段が下端まで埋まるか段区切りに達したら次の段へ、
        /// 最後の段が埋まるか改ページに達したらページを終える。<paramref name="cursor"/> は次のページ（段）の続きの位置で、null なら本文の終わり。
        /// ページと段の進め方はここだけで決まる（ノードは「この幅・この高さに収まる分」を返すだけ）。
        /// </summary>
        private DrawAction LayoutPageColumns(ContainerNode body, LayoutContext ctx, PageSetup setup, double height, ref object? cursor)
        {
            int columns = setup.ColumnCount;
            double width = setup.ColumnWidth;
            double spacing = setup.ColumnSpacing;
            var draws = new List<KeyValuePair<double, DrawAction>>();
            int used = 0;
            ctx.PriorColumnContent = false;

            // 脚注: ページの下端に全段をまたいで置く。前の段までの脚注の領域は、後の段の高さから引く。
            // 後の段の脚注が増えても前の段の内容に重ならないよう、脚注の領域の上限を（高さ - 前の段の最大の高さ）にする
            double footnoteItems = 0;
            double tallest = 0;
            var footnotes = new List<FootnoteEntry>();
            for (int col = 0; col < columns; col++)
            {
                ctx.FootnoteItems = footnoteItems;
                ctx.FootnoteLimit = height - tallest;
                var slice = body.Layout(ctx, width, height - ctx.FootnoteArea(footnoteItems), cursor, true);
                // 2 段目以降で、脚注が前の段の内容に重なってしまうときは、この段に置かずに次のページへ送る
                if (col > 0 && slice.FootnoteHeight > 0 && ctx.FootnoteArea(footnoteItems + slice.FootnoteHeight) > ctx.FootnoteLimit + 1e-6) break;
                cursor = slice.Next;
                if (slice.Draw != null) draws.Add(new KeyValuePair<double, DrawAction>(col * (width + spacing), slice.Draw));
                if (slice.Footnotes != null) footnotes.AddRange(slice.Footnotes);
                footnoteItems += slice.FootnoteHeight;
                tallest = Math.Max(tallest, slice.Height);
                if (slice.Height > 0)
                {
                    used = col + 1;
                    ctx.PriorColumnContent = true;
                }
                if (cursor == null) break;
                if (slice.ForceBreak && !slice.ColumnBreak) break;
            }
            ctx.PriorColumnContent = false;
            ctx.FootnoteItems = 0;
            ctx.FootnoteLimit = double.PositiveInfinity;

            double footnoteArea = ctx.FootnoteArea(footnoteItems);
            double bodyHeight = height - footnoteArea;
            var separator = setup.ColumnSeparator;
            int separators = separator != null ? used - 1 : 0;
            var footnotePen = _doc.FootnoteSeparator;
            double footnoteLineLength = Math.Min(setup.ContentWidth, _doc.FootnoteSeparatorLength ?? setup.ContentWidth / 3);
            double footnoteLineGap = FootnoteSeparatorGap;
            double footnoteTop = bodyHeight;
            return (g, x, y) =>
            {
                for (int i = 0; i < separators; i++)
                {
                    double sx = x + (i + 1) * width + i * spacing + spacing / 2;
                    g.DrawLine(separator!, sx, y, sx, y + bodyHeight);
                }
                foreach (var d in draws) d.Value(g, x + d.Key, y);
                if (footnotes.Count == 0) return;
                double top = y + footnoteTop;
                if (footnotePen != null)
                {
                    double ly = top + footnoteLineGap + footnotePen.Width / 2;
                    g.DrawLine(footnotePen, x, ly, x + footnoteLineLength, ly);
                }
                double fy = top + footnoteLineGap + (footnotePen?.Width ?? 0);
                foreach (var f in footnotes)
                {
                    f.Draw(g, x, fy);
                    fy += f.Height;
                }
            };
        }

        /// <summary>脚注の区切り線の上の空き（本文の最後の行と区切り線の間）。</summary>
        private const double FootnoteSeparatorGap = 6;

        /// <summary>脚注の区切り線の分の高さ（空きと線幅）。</summary>
        private double FootnoteSeparatorHeight => FootnoteSeparatorGap + (_doc.FootnoteSeparator?.Width ?? 0);

        private static int[] CountSectionPages(List<PageRecord> records, int sectionCount)
        {
            var counts = new int[sectionCount];
            foreach (var r in records) counts[r.SectionIndex]++;
            return counts;
        }

        private List<PageRecord> Paginate(List<Section> sections, int pageCount, int[]? sectionCounts, DocumentIndex index, PassCollector collector, out bool pageCountUsed)
        {
            var records = new List<PageRecord>();
            var scope = BaseScope(index);
            var ctx = new LayoutContext { Collector = collector, FootnoteSeparatorHeight = FootnoteSeparatorHeight };
            int pageNumber = 0;
            int nextDisplay = 1;
            var numberFormat = PageNumberFormat.Arabic;
            for (int si = 0; si < sections.Count; si++)
            {
                var section = sections[si];
                if (section.PageNumberStart.HasValue) nextDisplay = section.PageNumberStart.Value;
                if (section.PageNumberFormat.HasValue) numberFormat = section.PageNumberFormat.Value;
                var setup = section.PageSetup;
                double width = setup.ContentWidth;
                double height = setup.ContentHeight;
                if (width <= 0 || height <= 0)
                    throw new InvalidOperationException("余白が大きすぎて本文領域がありません。");
                if (setup.ColumnWidth <= 0)
                    throw new InvalidOperationException("段の幅がありません。PageSetup.ColumnCount を減らすか、ColumnSpacing を小さくしてください。");
                ctx.ColumnCount = setup.ColumnCount;
                var columnAreas = ColumnAreas(setup);
                var body = new ContainerNode(section.Blocks, scope, allowPageBreaks: true);
                object? cursor = null;
                int sectionPage = 0;
                do
                {
                    pageNumber++;
                    sectionPage++;
                    int display = nextDisplay++;
                    ctx.Page = new PageContext
                    {
                        PageNumber = pageNumber,
                        DisplayPageNumber = display,
                        PageNumberText = PageNumberFormatter.Format(display, numberFormat),
                        PageNumberFormat = numberFormat,
                        PageCount = pageCount,
                        SectionPageNumber = sectionPage,
                        SectionPageCount = sectionCounts != null ? sectionCounts[si] : 0,
                        PageSize = setup.Size,
                        ContentArea = new PdfRect(setup.Margins.Left, setup.Margins.Top, width, height),
                        ColumnCount = setup.ColumnCount,
                        ColumnAreas = columnAreas,
                    };
                    var draw = LayoutPageColumns(body, ctx, setup, height, ref cursor);
                    records.Add(new PageRecord { Section = section, SectionIndex = si, SectionPage = sectionPage, DisplayNumber = display, NumberFormat = numberFormat, Body = draw });
                    if (records.Count > 100000) throw new InvalidOperationException("ページ数が多すぎます（レイアウトが終了しません）。");
                }
                while (cursor != null);
            }
            pageCountUsed = ctx.PageCountUsed;
            return records;
        }
    }
}
