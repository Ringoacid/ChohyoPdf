using System;
using System.Collections.Generic;

namespace ChohyoPdf
{
    /// <summary>行送りの指定方法。</summary>
    public enum LineSpacingRule
    {
        /// <summary>文字サイズの倍数（例: 1.5 なら文字サイズ × 1.5 の行送り）。</summary>
        Multiple,

        /// <summary>固定値（ポイント）。</summary>
        Exactly,

        /// <summary>最小値（ポイント）。文字が大きければ広がる。</summary>
        AtLeast,
    }
}

namespace ChohyoPdf.Text
{
    internal struct TextItem
    {
        public int Codepoint;
        public ushort Gid;
        public PdfFont Font;
        public ResolvedStyle Style;
        public double Width;
        public bool HasGlyph;
        public bool SimBold;
        public bool SimItalic;
        public BreakClass Class;
    }

    internal sealed class TextLine
    {
        public int Start;
        public int End;
        public int ContentEnd;
        public double Width;
        public double Ascent;
        public double Descent;
        public double Height;
        public double Baseline;
        public bool IsLast;
        public double Indent;
    }

    /// <summary>テキストの文字列を測定・改行し、行単位で描画する。段落とテキストボックスで共通に使う。</summary>
    internal sealed class TextBlock
    {
        private readonly List<TextItem> _items;
        private readonly ResolvedStyle _baseStyle;
        private double _layoutWidth = double.NaN;
        private double _layoutIndent;
        private LineSpacingRule _rule;
        private double _spacing;

        public List<TextLine> Lines { get; } = new List<TextLine>();

        private TextBlock(List<TextItem> items, ResolvedStyle baseStyle)
        {
            _items = items;
            _baseStyle = baseStyle;
        }

        public int ItemCount => _items.Count;

        public static TextBlock Build(IList<KeyValuePair<string, ResolvedStyle>> segments, ResolvedStyle baseStyle)
        {
            var items = new List<TextItem>();
            foreach (var seg in segments)
            {
                AppendText(items, seg.Key, seg.Value);
            }
            return new TextBlock(items, baseStyle);
        }

        public static TextBlock Build(string text, ResolvedStyle style)
            => Build(new[] { new KeyValuePair<string, ResolvedStyle>(text, style) }, style);

        private static void AppendText(List<TextItem> items, string text, ResolvedStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < text.Length; i++)
            {
                int cp = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(text[i]))
                {
                    cp = 0xFFFD;
                }

                if (cp == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                    cp = '\n';
                }
                if (cp == 0x2028 || cp == 0x2029) cp = '\n';

                var item = new TextItem { Codepoint = cp, Style = style, Class = TextUtil.Classify(cp) };
                if (cp == '\n')
                {
                    item.Font = style.Font;
                    items.Add(item);
                    continue;
                }
                if (TextUtil.IsIgnorable(cp))
                {
                    item.Font = style.Font;
                    items.Add(item);
                    continue;
                }

                if (cp == '\t')
                {
                    var face = SelectFace(style.Font, style, out _, out _);
                    double space = face.GetAdvance(face.GetGlyphId(' '));
                    if (space <= 0) space = 0.25;
                    item.Font = face;
                    item.Width = space * 4 * style.Size + style.CharacterSpacing;
                    items.Add(item);
                    continue;
                }

                int glyphCp = cp == 0x00A0 ? ' ' : cp;
                PdfFont? chosen = null;
                bool simBold = false, simItalic = false;
                ushort gid = 0;
                var primary = SelectFace(style.Font, style, out bool pb, out bool pi);
                gid = primary.GetGlyphId(glyphCp);
                if (gid != 0)
                {
                    chosen = primary;
                    simBold = pb;
                    simItalic = pi;
                }
                else
                {
                    foreach (var fb in style.Fallbacks)
                    {
                        var face = SelectFace(fb, style, out bool fbBold, out bool fbItalic);
                        ushort g = face.GetGlyphId(glyphCp);
                        if (g != 0)
                        {
                            chosen = face;
                            gid = g;
                            simBold = fbBold;
                            simItalic = fbItalic;
                            break;
                        }
                    }
                }
                if (chosen == null)
                {
                    chosen = primary;
                    simBold = pb;
                    simItalic = pi;
                    gid = 0;
                }

                item.Font = chosen;
                item.Gid = gid;
                item.HasGlyph = true;
                item.SimBold = simBold;
                item.SimItalic = simItalic;
                item.Width = chosen.GetAdvance(gid) * style.Size + style.CharacterSpacing;
                items.Add(item);
            }
        }

        /// <summary>太字・斜体の指定に応じて実際に使うフォントを選ぶ。</summary>
        private static PdfFont SelectFace(PdfFont font, ResolvedStyle style, out bool simBold, out bool simItalic)
        {
            simBold = false;
            simItalic = false;
            if (style.Bold && style.Italic)
            {
                if (font.BoldItalicVariant != null) return font.BoldItalicVariant;
                if (font.BoldVariant != null)
                {
                    simItalic = !font.BoldVariant.IsItalic;
                    return font.BoldVariant;
                }
                if (font.ItalicVariant != null)
                {
                    simBold = !font.ItalicVariant.IsBold;
                    return font.ItalicVariant;
                }
                simBold = !font.IsBold;
                simItalic = !font.IsItalic;
                return font;
            }
            if (style.Bold)
            {
                if (font.BoldVariant != null) return font.BoldVariant;
                simBold = !font.IsBold;
                return font;
            }
            if (style.Italic)
            {
                if (font.ItalicVariant != null) return font.ItalicVariant;
                simItalic = !font.IsItalic;
                return font;
            }
            return font;
        }

        /// <summary>指定幅で改行位置を決め、各行の寸法を計算する。</summary>
        public void Layout(double width, double firstLineIndent, LineSpacingRule rule, double spacing)
        {
            if (width == _layoutWidth && firstLineIndent == _layoutIndent && rule == _rule && spacing == _spacing) return;
            _layoutWidth = width;
            _layoutIndent = firstLineIndent;
            _rule = rule;
            _spacing = spacing;
            Lines.Clear();

            int n = _items.Count;
            int start = 0;
            bool first = true;
            const double Epsilon = 1e-6;
            if (n == 0)
            {
                var empty = new TextLine { Start = 0, End = 0, ContentEnd = 0, IsLast = true, Indent = firstLineIndent };
                Measure(empty);
                Lines.Add(empty);
                return;
            }

            while (start < n)
            {
                double indent = first ? firstLineIndent : 0;
                double max = width - indent;
                double x = 0;
                int lastBreak = -1;
                int end = n;
                int next = n;
                bool hard = false;
                int i = start;
                while (i < n)
                {
                    var item = _items[i];
                    if (item.Class == BreakClass.HardBreak)
                    {
                        end = i;
                        next = i + 1;
                        hard = true;
                        break;
                    }
                    if (i > start && TextUtil.CanBreakBetween(_items[i - 1].Class, item.Class)) lastBreak = i;
                    if (item.Class == BreakClass.Space)
                    {
                        x += item.Width;
                        i++;
                        continue;
                    }
                    if (i > start && x + item.Width > max + Epsilon && item.Width > 0)
                    {
                        end = lastBreak > start ? lastBreak : i;
                        next = end;
                        break;
                    }
                    x += item.Width;
                    i++;
                }
                if (i >= n && !hard)
                {
                    end = n;
                    next = n;
                }

                var line = new TextLine { Start = start, End = end, Indent = indent };
                int contentEnd = end;
                while (contentEnd > start && _items[contentEnd - 1].Class == BreakClass.Space) contentEnd--;
                line.ContentEnd = contentEnd;
                double w = 0;
                for (int k = start; k < contentEnd; k++) w += _items[k].Width;
                line.Width = w;
                line.IsLast = hard || next >= n;
                Measure(line);
                Lines.Add(line);

                if (!hard)
                {
                    while (next < n && _items[next].Class == BreakClass.Space) next++;
                }
                start = next;
                first = false;
                if (hard && start >= n)
                {
                    // 末尾の改行の後にも空行を 1 行置く
                    var trailing = new TextLine { Start = n, End = n, ContentEnd = n, IsLast = true };
                    Measure(trailing, _items[n - 1].Style);
                    Lines.Add(trailing);
                }
            }
        }

        private void Measure(TextLine line, ResolvedStyle? emptyStyle = null)
        {
            double asc = 0, desc = 0, maxSize = 0;
            bool any = false;
            for (int k = line.Start; k < line.End; k++)
            {
                var it = _items[k];
                if (!it.HasGlyph && it.Class != BreakClass.Space) continue;
                any = true;
                asc = Math.Max(asc, it.Font.Ascent * it.Style.Size);
                desc = Math.Max(desc, it.Font.Descent * it.Style.Size);
                maxSize = Math.Max(maxSize, it.Style.Size);
            }
            if (!any)
            {
                var style = emptyStyle ?? (line.End < _items.Count ? _items[line.End].Style : _baseStyle);
                if (line.Start < _items.Count) style = _items[line.Start].Style;
                asc = style.Font.Ascent * style.Size;
                desc = style.Font.Descent * style.Size;
                maxSize = style.Size;
            }
            double natural = asc + desc;
            double height;
            switch (_rule)
            {
                case LineSpacingRule.Exactly:
                    height = _spacing;
                    break;
                case LineSpacingRule.AtLeast:
                    height = Math.Max(_spacing, natural);
                    break;
                default:
                    height = maxSize * _spacing;
                    break;
            }
            line.Ascent = asc;
            line.Descent = desc;
            line.Height = height;
            line.Baseline = (height - natural) / 2 + asc;
        }

        /// <summary>行の文字列（末尾の空白を除く。テスト・デバッグ用）。</summary>
        public string LineText(int index)
        {
            var line = Lines[index];
            var sb = new System.Text.StringBuilder();
            for (int k = line.Start; k < line.ContentEnd; k++)
            {
                int cp = _items[k].Codepoint;
                if (cp > 0) sb.Append(char.ConvertFromUtf32(cp));
            }
            return sb.ToString();
        }

        public double TotalHeight
        {
            get
            {
                double h = 0;
                foreach (var l in Lines) h += l.Height;
                return h;
            }
        }

        /// <summary>最も長い行の幅（インデント込み）。</summary>
        public double MaxLineWidth
        {
            get
            {
                double w = 0;
                foreach (var l in Lines) w = Math.Max(w, l.Width + l.Indent);
                return w;
            }
        }

        /// <summary>折り返しなしの自然な幅（改行で区切られた各行の最大幅）。</summary>
        public double NaturalWidth()
        {
            double max = 0, current = 0, trailingSpace = 0;
            foreach (var it in _items)
            {
                if (it.Class == BreakClass.HardBreak)
                {
                    max = Math.Max(max, current - trailingSpace);
                    current = 0;
                    trailingSpace = 0;
                    continue;
                }
                current += it.Width;
                trailingSpace = it.Class == BreakClass.Space ? trailingSpace + it.Width : 0;
            }
            return Math.Max(max, current - trailingSpace);
        }

        /// <summary>行 [from, to) を描画する。<paramref name="top"/> は最初の行の上端。</summary>
        public void Draw(PdfGraphics g, double x, double top, double width, TextAlignment alignment, int from, int to)
        {
            double y = top;
            for (int li = from; li < to && li < Lines.Count; li++)
            {
                var line = Lines[li];
                DrawLine(g, line, x + line.Indent, y + line.Baseline, width - line.Indent, alignment);
                y += line.Height;
            }
        }

        private void DrawLine(PdfGraphics g, TextLine line, double x, double baseline, double width, TextAlignment alignment)
        {
            int start = line.Start, end = line.ContentEnd;
            if (end <= start) return;
            double slack = width - line.Width;
            double offset = 0;
            double gapExtra = 0;
            bool spaceMode = false;

            bool distribute = alignment == TextAlignment.Distribute || (alignment == TextAlignment.Justify && !line.IsLast);
            if (distribute && slack > 0)
            {
                int spaces = 0, visible = 0;
                bool ideographic = false;
                for (int k = start; k < end; k++)
                {
                    var it = _items[k];
                    if (it.Class == BreakClass.Space) spaces++;
                    if (it.Width > 0) visible++;
                    if (it.Class == BreakClass.Ideographic) ideographic = true;
                }
                if (spaces > 0 && !ideographic && alignment == TextAlignment.Justify)
                {
                    spaceMode = true;
                    gapExtra = slack / spaces;
                }
                else if (visible > 1)
                {
                    gapExtra = slack / (visible - 1);
                }
                else
                {
                    offset = slack / 2;
                }
            }
            else if (alignment == TextAlignment.Center)
            {
                offset = slack / 2;
            }
            else if (alignment == TextAlignment.Right)
            {
                offset = slack;
            }

            // 各文字の位置を求める
            int count = end - start;
            var xs = new double[count];
            var extras = new double[count];
            double cx = x + offset;
            int lastVisible = -1;
            for (int k = end - 1; k >= start; k--)
            {
                if (_items[k].Width > 0)
                {
                    lastVisible = k;
                    break;
                }
            }
            for (int k = start; k < end; k++)
            {
                var it = _items[k];
                xs[k - start] = cx;
                double extra = 0;
                if (gapExtra > 0 && k < lastVisible)
                {
                    if (spaceMode) extra = it.Class == BreakClass.Space ? gapExtra : 0;
                    else extra = it.Width > 0 ? gapExtra : 0;
                }
                extras[k - start] = extra;
                cx += it.Width + extra;
            }

            // 背景色
            ForEachSpan(start, end, it => it.Style.Background.HasValue, (s, e) =>
            {
                var st = _items[s].Style;
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width + extras[e - 1 - start];
                g.FillRectangle(st.Background!.Value, x0, baseline - line.Ascent, x1 - x0, line.Ascent + line.Descent);
            });

            // 文字
            int runStart = start;
            while (runStart < end)
            {
                var first = _items[runStart];
                if (!first.HasGlyph)
                {
                    runStart++;
                    continue;
                }
                int runEnd = runStart + 1;
                while (runEnd < end)
                {
                    var it = _items[runEnd];
                    if (!it.HasGlyph || it.Font != first.Font || !SameVisual(it, first) || extras[runEnd - start] != extras[runStart - start]) break;
                    runEnd++;
                }
                var gids = new ushort[runEnd - runStart];
                var cps = new int[runEnd - runStart];
                for (int k = runStart; k < runEnd; k++)
                {
                    gids[k - runStart] = _items[k].Gid;
                    cps[k - runStart] = _items[k].Codepoint;
                }
                g.ShowGlyphs(first.Font, first.Style.Size, first.Style.Color, first.SimBold, first.SimItalic,
                    first.Style.CharacterSpacing + extras[runStart - start], xs[runStart - start], baseline, gids, cps);
                runStart = runEnd;
            }

            // 下線・取り消し線
            ForEachSpan(start, end, it => it.Style.Underline, (s, e) =>
            {
                var it = _items[s];
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                double thickness = it.Font.UnderlineThickness * it.Style.Size;
                double y = baseline + it.Font.UnderlineOffset * it.Style.Size;
                g.FillRectangle(it.Style.Color, x0, y - thickness / 2, x1 - x0, thickness);
            });
            ForEachSpan(start, end, it => it.Style.Strikeout, (s, e) =>
            {
                var it = _items[s];
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                double thickness = it.Font.UnderlineThickness * it.Style.Size;
                double y = baseline - it.Style.Size * 0.3;
                g.FillRectangle(it.Style.Color, x0, y - thickness / 2, x1 - x0, thickness);
            });

            // リンク
            ForEachSpan(start, end, it => it.Style.Link != null, (s, e) =>
            {
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                g.AddLink(new PdfRect(x0, baseline - line.Ascent, x1 - x0, line.Ascent + line.Descent), _items[s].Style.Link!);
            }, sameStyleOnly: true);
        }

        private static bool SameVisual(TextItem a, TextItem b)
        {
            return a.Style.Size == b.Style.Size && a.Style.Color == b.Style.Color && a.SimBold == b.SimBold && a.SimItalic == b.SimItalic
                && a.Style.CharacterSpacing == b.Style.CharacterSpacing;
        }

        private void ForEachSpan(int start, int end, Func<TextItem, bool> predicate, Action<int, int> action, bool sameStyleOnly = false)
        {
            int k = start;
            while (k < end)
            {
                if (!predicate(_items[k]))
                {
                    k++;
                    continue;
                }
                int s = k;
                var style = _items[k].Style;
                k++;
                while (k < end && predicate(_items[k]) && (!sameStyleOnly || ReferenceEquals(_items[k].Style, style) || _items[k].Style.Link == style.Link)) k++;
                action(s, k);
            }
        }
    }
}
