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
            public DrawAction? Body;
        }

        private readonly Document _doc;

        public DocumentRenderer(Document doc)
        {
            _doc = doc;
        }

        private StyleScope BaseScope()
        {
            var text = new TextStyle { FontSize = 10.5, Color = PdfColor.Black }.Merge(_doc.DefaultTextStyle);
            var format = new ParagraphFormat
            {
                Alignment = TextAlignment.Left,
                LineSpacingRule = LineSpacingRule.Multiple,
                LineSpacing = 1.5,
                WidowControl = true,
            }.Merge(_doc.DefaultParagraphFormat);
            return new StyleScope(_doc, text, format);
        }

        public void Render(PdfDocument pdf)
        {
            var sections = new List<Section>(_doc.Sections);
            if (sections.Count == 0) sections.Add(_doc.AddSection());

            var records = Paginate(sections, 0, null, out bool countUsed);
            if (countUsed)
            {
                var sectionCounts = CountSectionPages(records, sections.Count);
                records = Paginate(sections, records.Count, sectionCounts, out _);
            }
            var counts = CountSectionPages(records, sections.Count);

            var scope = BaseScope().ForInnerContainer();
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                var setup = rec.Section.PageSetup;
                var page = pdf.AddPage(setup.Size);
                var g = page.Graphics;
                var m = setup.Margins;
                var pc = new PageContext
                {
                    PageNumber = i + 1,
                    PageCount = records.Count,
                    SectionPageNumber = rec.SectionPage,
                    SectionPageCount = counts[rec.SectionIndex],
                    PageSize = setup.Size,
                    ContentArea = new PdfRect(m.Left, m.Top, setup.ContentWidth, setup.ContentHeight),
                    Page = page,
                };
                var ctx = new LayoutContext { Page = pc };

                rec.Section.PageBackground?.Invoke(g, pc);

                var header = rec.Section.Header;
                if (header.Blocks.Count > 0 && (header.ShowOnFirstPage || rec.SectionPage > 1))
                {
                    var node = new ContainerNode(header.Blocks, scope, allowPageBreaks: false);
                    var slice = node.Layout(ctx, setup.ContentWidth, double.PositiveInfinity, null, true);
                    slice.Draw?.Invoke(g, m.Left, setup.HeaderDistance);
                }

                rec.Body?.Invoke(g, m.Left, m.Top);

                var footer = rec.Section.Footer;
                if (footer.Blocks.Count > 0 && (footer.ShowOnFirstPage || rec.SectionPage > 1))
                {
                    var node = new ContainerNode(footer.Blocks, scope, allowPageBreaks: false);
                    var slice = node.Layout(ctx, setup.ContentWidth, double.PositiveInfinity, null, true);
                    slice.Draw?.Invoke(g, m.Left, setup.Size.Height - setup.FooterDistance - slice.Height);
                }

                rec.Section.PageForeground?.Invoke(g, pc);
            }
        }

        private static int[] CountSectionPages(List<PageRecord> records, int sectionCount)
        {
            var counts = new int[sectionCount];
            foreach (var r in records) counts[r.SectionIndex]++;
            return counts;
        }

        private List<PageRecord> Paginate(List<Section> sections, int pageCount, int[]? sectionCounts, out bool pageCountUsed)
        {
            var records = new List<PageRecord>();
            var scope = BaseScope();
            var ctx = new LayoutContext();
            int pageNumber = 0;
            for (int si = 0; si < sections.Count; si++)
            {
                var section = sections[si];
                var setup = section.PageSetup;
                double width = setup.ContentWidth;
                double height = setup.ContentHeight;
                if (width <= 0 || height <= 0)
                    throw new InvalidOperationException("余白が大きすぎて本文領域がありません。");
                var body = new ContainerNode(section.Blocks, scope, allowPageBreaks: true);
                object? cursor = null;
                int sectionPage = 0;
                do
                {
                    pageNumber++;
                    sectionPage++;
                    ctx.Page = new PageContext
                    {
                        PageNumber = pageNumber,
                        PageCount = pageCount,
                        SectionPageNumber = sectionPage,
                        SectionPageCount = sectionCounts != null ? sectionCounts[si] : 0,
                        PageSize = setup.Size,
                        ContentArea = new PdfRect(setup.Margins.Left, setup.Margins.Top, width, height),
                    };
                    var slice = body.Layout(ctx, width, height, cursor, true);
                    records.Add(new PageRecord { Section = section, SectionIndex = si, SectionPage = sectionPage, Body = slice.Draw });
                    cursor = slice.Next;
                    if (records.Count > 100000) throw new InvalidOperationException("ページ数が多すぎます（レイアウトが終了しません）。");
                }
                while (cursor != null);
            }
            pageCountUsed = ctx.PageCountUsed;
            return records;
        }
    }
}
