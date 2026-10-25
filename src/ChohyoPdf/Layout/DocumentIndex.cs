using System;
using System.Collections.Generic;
using System.Text;

namespace ChohyoPdf.Layout
{
    /// <summary>見出し（<see cref="ParagraphFormat.OutlineLevel"/> を持つ段落）。しおりと目次の元になる。</summary>
    internal sealed class HeadingInfo
    {
        public HeadingInfo(Paragraph paragraph, int index, int level, string title)
        {
            Paragraph = paragraph;
            Index = index;
            Level = level;
            Title = title;
            Key = "\u0001heading:" + index;
        }

        public Paragraph Paragraph { get; }

        /// <summary>文書内での順番（0 始まり）。</summary>
        public int Index { get; }

        public int Level { get; }

        /// <summary>しおりと目次に表示する文字列。</summary>
        public string Title { get; }

        /// <summary>見出しの位置を記録するときのアンカー名（利用者のアンカー名と重ならない）。</summary>
        public string Key { get; }
    }

    /// <summary>アンカーが描画されたページの情報。</summary>
    internal sealed class AnchorInfo
    {
        /// <summary>物理的なページの順番（0 始まり）。</summary>
        public int PageIndex;

        /// <summary>表示用のページ番号（数値）。</summary>
        public int DisplayNumber;

        /// <summary>そのページの番号の書式。</summary>
        public PageNumberFormat Format;

        /// <summary>そのページの番号を、セクションの書式で文字列にしたもの。</summary>
        public string Text = "";
    }

    /// <summary>アンカー名とページの対応。</summary>
    internal sealed class AnchorTable
    {
        private readonly Dictionary<string, AnchorInfo> _map = new Dictionary<string, AnchorInfo>();

        public int Count => _map.Count;

        public bool Contains(string name) => _map.ContainsKey(name);

        public bool TryGet(string name, out AnchorInfo info)
        {
            bool found = _map.TryGetValue(name, out var value);
            info = value!;
            return found;
        }

        public void Add(string name, AnchorInfo info) => _map[name] = info;

        /// <summary>同じアンカーが同じページ（表示も同じ）にあるか。ページ番号が確定したかの判定に使う。</summary>
        public bool SameAs(AnchorTable other)
        {
            if (_map.Count != other._map.Count) return false;
            foreach (var kv in _map)
            {
                if (!other._map.TryGetValue(kv.Key, out var o)) return false;
                if (o.PageIndex != kv.Value.PageIndex || o.Text != kv.Value.Text) return false;
            }
            return true;
        }
    }

    /// <summary>文書を走査した結果（レイアウトの前に決まるもの）。</summary>
    internal sealed class DocumentScan
    {
        public List<HeadingInfo> Headings { get; } = new List<HeadingInfo>();

        public Dictionary<Paragraph, HeadingInfo> ByParagraph { get; } = new Dictionary<Paragraph, HeadingInfo>();

        /// <summary>ページ参照または目次があり、ページ番号を確定する繰り返しが必要か。</summary>
        public bool UsesReferences { get; set; }

        /// <summary>脚注の参照記号ごとの番号（1 始まり。文書の並びの順で、レイアウトの前に決まる）。</summary>
        public Dictionary<FootnoteRun, int> FootnoteNumbers { get; } = new Dictionary<FootnoteRun, int>();

        /// <summary>これまでに振った脚注の番号（セクションの振り直しで 0 に戻る）。</summary>
        public int FootnoteCounter { get; set; }
    }

    /// <summary>1 回のレイアウトで使う索引。前回までに分かったアンカーのページを読み、参照されたアンカー名を集める。</summary>
    internal sealed class DocumentIndex
    {
        public DocumentIndex(DocumentScan scan, AnchorTable assumed)
        {
            Scan = scan;
            Assumed = assumed;
        }

        public DocumentScan Scan { get; }

        public List<HeadingInfo> Headings => Scan.Headings;

        public Dictionary<Paragraph, HeadingInfo> ByParagraph => Scan.ByParagraph;

        /// <summary>前回のレイアウトで分かったアンカーのページ（ページ参照と目次が表示に使う）。</summary>
        public AnchorTable Assumed { get; }

        /// <summary>文書内リンクやページ参照が指しているアンカー名。</summary>
        public HashSet<string> Referenced { get; } = new HashSet<string>();

        /// <summary>ページ参照または目次が <see cref="Assumed"/> を読んだか。</summary>
        public bool AnchorsRead { get; set; }
    }

    /// <summary>見出し 1 つの描画位置。</summary>
    internal sealed class HeadingHit
    {
        public HeadingHit(HeadingInfo heading, PdfPage page, double y)
        {
            Heading = heading;
            Page = page;
            Y = y;
        }

        public HeadingInfo Heading { get; }
        public PdfPage Page { get; }
        public double Y { get; }
    }

    /// <summary>
    /// 描画時にアンカーと見出しの位置を記録する。レイアウト中は呼ばれない（レイアウトは何度も試しに行われるため、
    /// 実際に描画された位置だけを記録する）。
    /// </summary>
    internal sealed class PassCollector
    {
        private readonly PdfDocument _pdf;
        private PdfPage? _page;
        private int _pageIndex;
        private int _displayNumber;
        private PageNumberFormat _format;
        private string _text = "";

        public PassCollector(PdfDocument pdf)
        {
            _pdf = pdf;
        }

        public AnchorTable Anchors { get; } = new AnchorTable();

        public List<HeadingHit> Hits { get; } = new List<HeadingHit>();

        public void BeginPage(int pageIndex, PdfPage page, int displayNumber, PageNumberFormat format, string text)
        {
            _pageIndex = pageIndex;
            _page = page;
            _displayNumber = displayNumber;
            _format = format;
            _text = text;
        }

        /// <summary>現在のページのアンカーを登録する（最初の登録を優先）。(x, y) は <paramref name="g"/> の座標系。</summary>
        public void Register(string name, PdfGraphics g, double x, double y)
        {
            Place(name, g, x, y);
        }

        public void RegisterHeading(HeadingInfo heading, PdfGraphics g, double x, double y)
        {
            var hit = Place(heading.Key, g, x, y);
            if (hit != null) Hits.Add(new HeadingHit(heading, _page!, hit.Value));
        }

        private double? Place(string name, PdfGraphics g, double x, double y)
        {
            if (_page == null || Anchors.Contains(name)) return null;
            g.ToPagePoint(x, y, out _, out double pageY);
            Anchors.Add(name, new AnchorInfo { PageIndex = _pageIndex, DisplayNumber = _displayNumber, Format = _format, Text = _text });
            _pdf.TryAddNamedDestination(name, _page, pageY);
            return pageY;
        }
    }

    /// <summary>文書の本文を走査して、見出しとアンカーを集める。</summary>
    internal static class DocumentScanner
    {
        public static DocumentScan Scan(Document doc, IEnumerable<Section> sections)
        {
            var scan = new DocumentScan();
            var anchors = new HashSet<string>();
            foreach (var section in sections)
            {
                if (section.FootnoteNumbering == FootnoteNumbering.Restart) scan.FootnoteCounter = 0;
                Walk(doc, section.Blocks, scan, anchors, true);
                foreach (var hf in new[] { section.Header, section.Footer, section.EvenHeader, section.EvenFooter, section.FirstPageHeader, section.FirstPageFooter })
                    Walk(doc, hf.Blocks, scan, anchors, false);
            }
            return scan;
        }

        /// <summary>段落の実効の見出しレベル（文書の既定、名前付きスタイル、段落自身の順に上書き）。未指定は null。</summary>
        public static int? GetOutlineLevel(Document doc, Paragraph p)
        {
            int? level = doc.DefaultParagraphFormat?.OutlineLevel;
            if (p.StyleName != null && doc.Styles.TryGetValue(p.StyleName, out var named) && named.Format?.OutlineLevel != null)
                level = named.Format.OutlineLevel;
            if (p.Format.OutlineLevel.HasValue) level = p.Format.OutlineLevel;
            return level;
        }

        private static void Walk(Document doc, IEnumerable<Block> blocks, DocumentScan scan, HashSet<string> anchors, bool body, bool inFootnote = false)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph p)
                {
                    Visit(doc, p, scan, anchors, body, inFootnote);
                }
                else if (block is Table table)
                {
                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row.Cells) Walk(doc, cell.Blocks, scan, anchors, body, inFootnote);
                    }
                }
                else if (block is ListBlock list)
                {
                    foreach (var item in list.Items) Walk(doc, item.Blocks, scan, anchors, body, inFootnote);
                }
                else if (block is TableOfContents toc)
                {
                    scan.UsesReferences = true;
                    if (body)
                    {
                        var title = toc.PrepareTitle();
                        if (title != null) Visit(doc, title, scan, anchors, true, false);
                    }
                }
            }
        }

        private static void Visit(Document doc, Paragraph p, DocumentScan scan, HashSet<string> anchors, bool body, bool inFootnote)
        {
            foreach (var inline in p.Inlines)
            {
                if (inline is FieldRun field && field.Field == FieldType.PageRef) scan.UsesReferences = true;
                else if (inline is FootnoteRun footnote)
                {
                    if (inFootnote)
                        throw new InvalidOperationException("脚注の本文の中に脚注は置けません（脚注を入れ子にはできません）。");
                    if (!body)
                        throw new InvalidOperationException("ヘッダー/フッターには脚注を置けません。脚注を使えるのは、セクションの本文、リストの項目、表のセルの段落です。");
                    if (!scan.FootnoteNumbers.ContainsKey(footnote))
                        scan.FootnoteNumbers.Add(footnote, ++scan.FootnoteCounter);
                    // 脚注の本文の中のページ参照なども走査する（見出しとアンカーは数えない）
                    Walk(doc, footnote.Footnote.Blocks, scan, anchors, false, true);
                }
            }
            if (!body) return;

            AddAnchor(anchors, p.Anchor);
            foreach (var inline in p.Inlines)
            {
                if (inline is AnchorRun a) AddAnchor(anchors, a.Name);
            }

            int? level = GetOutlineLevel(doc, p);
            if (!level.HasValue) return;
            string title = HeadingTitle(p);
            if (title.Length == 0) return;
            if (scan.ByParagraph.ContainsKey(p)) return;
            var heading = new HeadingInfo(p, scan.Headings.Count, level.Value, title);
            scan.Headings.Add(heading);
            scan.ByParagraph[p] = heading;
        }

        private static void AddAnchor(HashSet<string> anchors, string? name)
        {
            if (name == null) return;
            if (!anchors.Add(name))
                throw new InvalidOperationException("アンカー '" + name + "' が重複しています。アンカー名は文書内で一意にしてください。");
        }

        /// <summary>しおりと目次の文字列。<see cref="Paragraph.OutlineTitle"/> があればそれ、なければ段落の文字列（フィールドは含めない）。</summary>
        private static string HeadingTitle(Paragraph p)
        {
            string? raw = p.OutlineTitle;
            if (raw == null)
            {
                var sb = new StringBuilder();
                foreach (var inline in p.Inlines)
                {
                    if (inline is TextRun run) sb.Append(run.Text);
                    else if (inline is RubyRun ruby) sb.Append(ruby.BaseText);
                }
                raw = sb.ToString();
            }
            // 改行・タブは空白 1 つにまとめる
            var result = new StringBuilder(raw.Length);
            bool space = false;
            foreach (char c in raw)
            {
                if (c == '\n' || c == '\r' || c == '\t' || c == ' ' || c == '\u2028' || c == '\u2029')
                {
                    space = result.Length > 0;
                    continue;
                }
                if (space)
                {
                    result.Append(' ');
                    space = false;
                }
                result.Append(c);
            }
            return result.ToString();
        }
    }
}
