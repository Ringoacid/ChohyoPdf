using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>タブのリーダー（<see cref="TextBlock.Layout"/> がタブ位置に従って決める）。</summary>
        public TabLeader Leader;

        /// <summary>タブの先頭からタブ位置までの距離（リーダーの位置合わせに使う）。</summary>
        public double Reach;

        /// <summary>アンカーの印（幅のない要素。描画時にその位置を通知する）。アンカーでなければ null。</summary>
        public string? Marker;

        /// <summary>ルビを付ける親文字なら、その組（親文字の 1 文字ごとに同じ参照を持つ）。付けない文字は null。</summary>
        public RubyGroup? Ruby;

        /// <summary>ルビの組の 2 文字目以降（組の途中では改行しない）。</summary>
        public bool RubyCont;

        /// <summary>ルビが親文字より長いときに、親文字の幅へ加えた余白（字形は余白の半分だけ右にずらして描く）。<see cref="Width"/> に含まれる。</summary>
        public double Pad;
    }

    /// <summary>
    /// ルビと親文字の組（内部用・不変）。ルビの寸法は <see cref="TextBlock.Build(IList{KeyValuePair{string, ResolvedStyle}}, ResolvedStyle)"/> が
    /// 決める。レイアウトを何度やり直しても値は変わらない。
    /// </summary>
    internal sealed class RubyGroup
    {
        /// <summary>ルビの文字（幅・字形が確定したもの）。</summary>
        public TextItem[] Items = new TextItem[0];

        /// <summary>ルビの幅（最後の文字の字間を除く）。</summary>
        public double Width;

        /// <summary>ルビのベースラインから上の高さと下の深さ（ルビの文字サイズで計算した最大値）。</summary>
        public double Ascent;

        public double Descent;

        /// <summary>親文字のベースラインから上端までの高さ（上付きなどのずれを含む）。ルビは、この上にルビの下端がくる位置に描く。</summary>
        public double ParentAscent;

        /// <summary>親文字の最初の項目の位置と、親文字の項目数。</summary>
        public int First;

        public int Count;

        /// <summary>ベースラインより上に必要な高さ（親文字の上端 + ルビの高さ）。</summary>
        public double Reach => ParentAscent + Ascent + Descent;
    }

    /// <summary>タブ位置の設定（段落書式から作る内部用）。</summary>
    internal sealed class TabLayout
    {
        /// <summary>タブ位置だけを指定したときの既定のタブ間隔（ポイント）。</summary>
        public const double FallbackInterval = 36;

        public TabStop[] Stops { get; }

        public double Interval { get; }

        private TabLayout(TabStop[] stops, double interval)
        {
            Stops = stops;
            Interval = interval;
        }

        /// <summary>タブ位置も既定のタブ間隔も未指定なら null（従来どおり、タブは空白 4 文字分の幅）。</summary>
        public static TabLayout? Create(IList<TabStop>? stops, double? defaultTabStop)
        {
            if (stops == null && !defaultTabStop.HasValue) return null;
            double interval = defaultTabStop ?? FallbackInterval;
            if (!(interval > 0) || double.IsInfinity(interval))
                throw new InvalidOperationException("既定のタブ間隔は正の値で指定してください。");
            var sorted = stops == null
                ? new TabStop[0]
                : stops.Where(s => s != null).OrderBy(s => s.Position).ToArray();
            return new TabLayout(sorted, interval);
        }
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

        /// <summary>タブ位置に従って幅を決めたタブを含む（両端揃え・均等割り付けの対象にしない）。</summary>
        public bool HasTab;
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
        private TabLayout? _tabs;

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
            if (style.Marker != null)
            {
                // アンカーの印: 幅も字形もない要素として置く
                items.Add(new TextItem { Codepoint = 0, Style = style, Class = BreakClass.Alphabetic, Font = style.Font, Marker = style.Marker });
                return;
            }
            if (string.IsNullOrEmpty(text)) return;
            if (style.RubyText != null && style.RubyStyle != null)
            {
                AppendRuby(items, text, style);
                return;
            }
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
                item.Width = chosen.GetAdvance(gid) * style.DrawSize + style.CharacterSpacing;
                items.Add(item);
            }
        }

        /// <summary>
        /// ルビ付きの親文字を追加する。親文字は普通の項目として並べ、ルビの寸法はここで確定する。
        /// ルビが親文字より長いときは、親文字の各文字の幅を同じ量だけ広げる（均等割り。ルビは前後の文字にはみ出させない）。
        /// 改行やタブを含む、ルビが空など、組として扱えないときは、ルビなしの普通の文字列にする。
        /// </summary>
        private static void AppendRuby(List<TextItem> items, string text, ResolvedStyle style)
        {
            var plain = style.WithRuby(null, null);
            var parent = new List<TextItem>();
            AppendText(parent, text, plain);

            var ruby = new List<TextItem>();
            AppendText(ruby, style.RubyText!, style.RubyStyle!);
            ruby.RemoveAll(r => r.Class == BreakClass.HardBreak || r.Codepoint == '	' || !r.HasGlyph);
            bool usable = ruby.Count > 0 && parent.Count > 0;
            foreach (var p in parent)
            {
                if (p.Class == BreakClass.HardBreak || p.Codepoint == '	') usable = false;
            }
            if (!usable)
            {
                items.AddRange(parent);
                return;
            }

            var group = new RubyGroup { Items = ruby.ToArray(), First = items.Count, Count = parent.Count };
            foreach (var r in ruby)
            {
                group.Width += r.Width;
                group.Ascent = Math.Max(group.Ascent, r.Font.Ascent * r.Style.Size);
                group.Descent = Math.Max(group.Descent, r.Font.Descent * r.Style.Size);
            }
            group.Width -= ruby[ruby.Count - 1].Style.CharacterSpacing;

            double parentWidth = 0;
            int visible = 0;
            foreach (var p in parent)
            {
                parentWidth += p.Width;
                if (p.Width > 0) visible++;
                if (p.HasGlyph) group.ParentAscent = Math.Max(group.ParentAscent, p.Font.Ascent * p.Style.DrawSize - p.Style.BaselineOffset);
            }
            if (group.ParentAscent <= 0) group.ParentAscent = plain.Font.Ascent * plain.Size;

            double pad = visible > 0 && group.Width > parentWidth + 1e-9 ? (group.Width - parentWidth) / visible : 0;
            for (int j = 0; j < parent.Count; j++)
            {
                var item = parent[j];
                item.Ruby = group;
                item.RubyCont = j > 0;
                if (pad > 0 && item.Width > 0)
                {
                    item.Width += pad;
                    item.Pad = pad;
                }
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

        /// <summary>指定幅で改行位置を決め、各行の寸法を計算する。<c>tabs</c> が null なら、タブ文字は空白 4 文字分の幅になる。</summary>
        public void Layout(double width, double firstLineIndent, LineSpacingRule rule, double spacing, TabLayout? tabs = null)
        {
            if (width == _layoutWidth && firstLineIndent == _layoutIndent && rule == _rule && spacing == _spacing && ReferenceEquals(tabs, _tabs)) return;
            _layoutWidth = width;
            _layoutIndent = firstLineIndent;
            _rule = rule;
            _spacing = spacing;
            _tabs = tabs;
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
                    if (i > start && !item.RubyCont && TextUtil.CanBreakBetween(_items[i - 1].Class, item.Class)) lastBreak = i;
                    if (tabs != null && item.Codepoint == '\t')
                    {
                        // タブの幅は、行内の現在位置とタブ位置で決まる
                        item = ResolveTab(i, x, indent, width, tabs);
                        _items[i] = item;
                    }
                    if (item.Class == BreakClass.Space)
                    {
                        x += item.Width;
                        i++;
                        continue;
                    }
                    if (i > start && x + item.Width > max + Epsilon && item.Width > 0)
                    {
                        end = lastBreak > start ? lastBreak : i;
                        // ルビと親文字の組は途中で改行しない（行に収まらなくても 1 行に置く）
                        if (end == i && item.RubyCont) end = Math.Min(n, item.Ruby!.First + item.Ruby.Count);
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
                if (tabs != null)
                {
                    for (int k = start; k < contentEnd; k++)
                    {
                        if (_items[k].Codepoint == '\t')
                        {
                            line.HasTab = true;
                            break;
                        }
                    }
                }
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

        /// <summary>
        /// タブの幅・リーダー・タブ位置までの距離を決める。位置は本文領域の左端（左インデントの位置）が 0。
        /// 現在位置より右にある最初のタブ位置まで進み、タブ位置がなければ既定のタブ間隔の整数倍の位置まで進む。
        /// 1 行目がぶら下げインデントで左に出ているときは、左インデントの位置にもタブ位置があるものとして扱う。
        /// 右揃えのタブ位置が行の右端（<paramref name="layoutWidth"/>）を超えるときは、右端に寄せる。
        /// </summary>
        private TextItem ResolveTab(int index, double x, double lineStart, double layoutWidth, TabLayout tabs)
        {
            var item = _items[index];
            const double Epsilon = 1e-6;
            double current = lineStart + x;
            double target = 0;
            var alignment = TabAlignment.Left;
            var leader = TabLeader.None;
            bool found = false;
            if (lineStart < 0 && current < -Epsilon)
            {
                found = true;
            }
            else
            {
                foreach (var stop in tabs.Stops)
                {
                    double pos = stop.Position;
                    if (stop.Alignment == TabAlignment.Right && pos > layoutWidth) pos = layoutWidth;
                    if (pos > current + Epsilon)
                    {
                        target = pos;
                        alignment = stop.Alignment;
                        leader = stop.Leader;
                        found = true;
                        break;
                    }
                }
            }
            if (!found) target = (Math.Floor((current + Epsilon) / tabs.Interval) + 1) * tabs.Interval;

            double reach = target - current;
            double width = reach;
            if (alignment != TabAlignment.Left)
            {
                double content = MeasureAfterTab(index + 1, alignment == TabAlignment.Decimal);
                width = alignment == TabAlignment.Center ? reach - content / 2 : reach - content;
                if (width < 0) width = 0;
            }
            item.Width = width;
            item.Leader = leader;
            item.Reach = reach;
            return item;
        }

        /// <summary>タブの後ろの文字列（次のタブか改行まで。末尾の空白を除く）の幅。<paramref name="untilDecimal"/> なら小数点の手前まで。</summary>
        private double MeasureAfterTab(int from, bool untilDecimal)
        {
            double w = 0, visible = 0;
            for (int k = from; k < _items.Count; k++)
            {
                var it = _items[k];
                if (it.Class == BreakClass.HardBreak || it.Codepoint == '\t') break;
                if (untilDecimal && (it.Codepoint == '.' || it.Codepoint == 0xFF0E)) break;
                w += it.Width;
                if (it.Class != BreakClass.Space) visible = w;
            }
            return visible;
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

            // ルビがある行は、行の上端からベースラインまでの距離がルビの分だけ足りなければ、足りない分だけ行を高くする
            // （行間の余白に収まるときは広げない。固定値の行送りでは行の高さを変えず、ルビは行の上にはみ出して描く）
            if (_rule != LineSpacingRule.Exactly)
            {
                double reach = 0;
                for (int k = line.Start; k < line.End; k++)
                {
                    var ruby = _items[k].Ruby;
                    if (ruby != null && ruby.First == k) reach = Math.Max(reach, ruby.Reach);
                }
                if (reach > line.Baseline + 1e-9)
                {
                    double extra = reach - line.Baseline;
                    line.Height += extra;
                    line.Baseline += extra;
                }
            }
        }

        /// <summary>
        /// 脚注の参照記号（<see cref="ResolvedStyle.FootnoteId"/> を持つ文字の連なり）の、行の番号（0 始まり）と脚注の通し番号の組を、文字列の並びの順に返す。
        /// 参照記号が行をまたぐことはないので、記号の最初の文字がある行を採る。
        /// </summary>
        public List<KeyValuePair<int, int>> FootnoteRefs()
        {
            var result = new List<KeyValuePair<int, int>>();
            for (int li = 0; li < Lines.Count; li++)
            {
                var line = Lines[li];
                for (int k = line.Start; k < line.End && k < _items.Count; k++)
                {
                    int id = _items[k].Style.FootnoteId;
                    if (id == 0) continue;
                    if (k > 0 && _items[k - 1].Style.FootnoteId == id) continue;
                    result.Add(new KeyValuePair<int, int>(li, id));
                }
            }
            return result;
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

        /// <summary>
        /// 段落の幅の計測（表の列幅を内容に合わせるときに使う）。<paramref name="min"/> は折り返せない最長の単位の幅
        /// （改行位置は <see cref="Layout"/> と同じ規則で、禁則処理・ルビの組・欧文の単語を含む。行末の空白は含めない）、
        /// <paramref name="max"/> は折り返さないときの最も長い行（強制改行で区切る）の幅。1 行目には <paramref name="firstLineIndent"/> を加える。
        /// <see cref="Layout"/> の状態（<see cref="Lines"/> やタブの幅）は変えないので、何度呼んでも、レイアウトの前後でも同じ結果になる。
        /// タブ位置（<see cref="TabLayout"/>）は反映せず、タブは既定の幅（空白 4 文字分）として数える。
        /// </summary>
        public void MeasureWidths(double firstLineIndent, out double min, out double max)
        {
            min = 0;
            max = 0;
            int n = _items.Count;
            int lineStart = 0;
            bool firstLine = true;
            double line = 0, lineTrail = 0;      // 行の幅と、末尾の空白の幅
            double seg = 0, segTrail = 0;        // 折り返せない単位の幅と、末尾の空白の幅
            bool firstSeg = true;
            for (int i = 0; i <= n; i++)
            {
                bool end = i == n;
                bool hard = !end && _items[i].Class == BreakClass.HardBreak;
                bool canBreak = !end && !hard && i > lineStart && !_items[i].RubyCont && TextUtil.CanBreakBetween(_items[i - 1].Class, _items[i].Class);
                if (end || hard || canBreak)
                {
                    // 折り返せない単位が終わった
                    double indent = firstSeg && firstLine ? firstLineIndent : 0;
                    min = Math.Max(min, Math.Max(0, seg - segTrail + indent));
                    seg = 0;
                    segTrail = 0;
                    firstSeg = false;
                }
                if (end || hard)
                {
                    max = Math.Max(max, Math.Max(0, line - lineTrail + (firstLine ? firstLineIndent : 0)));
                    line = 0;
                    lineTrail = 0;
                    firstSeg = true;
                    firstLine = false;
                    lineStart = i + 1;
                    continue;
                }
                var item = _items[i];
                double w = item.Codepoint == '	' ? DefaultTabWidth(item) : item.Width;
                seg += w;
                line += w;
                if (item.Class == BreakClass.Space)
                {
                    segTrail += w;
                    lineTrail += w;
                }
                else
                {
                    segTrail = 0;
                    lineTrail = 0;
                }
            }
            if (min > max) min = max;
        }

        /// <summary>タブ位置を使わないときのタブの幅（空白 4 文字分）。<see cref="Layout"/> が書き換えた幅には依存しない。</summary>
        private static double DefaultTabWidth(TextItem tab)
        {
            double space = tab.Font.GetAdvance(tab.Font.GetGlyphId(' '));
            if (space <= 0) space = 0.25;
            return space * 4 * tab.Style.Size + tab.Style.CharacterSpacing;
        }

        /// <summary>
        /// 行 [from, to) を描画する。<paramref name="top"/> は最初の行の上端。
        /// <paramref name="onMarker"/> を指定すると、描画範囲にあるアンカーの印ごとに（名前、x、行の上端の y）を通知する。
        /// </summary>
        public void Draw(PdfGraphics g, double x, double top, double width, TextAlignment alignment, int from, int to, Action<string, double, double>? onMarker = null)
        {
            double y = top;
            for (int li = from; li < to && li < Lines.Count; li++)
            {
                var line = Lines[li];
                DrawLine(g, line, x + line.Indent, y + line.Baseline, width - line.Indent, alignment, onMarker, y);
                y += line.Height;
            }
        }

        private void DrawLine(PdfGraphics g, TextLine line, double x, double baseline, double width, TextAlignment alignment, Action<string, double, double>? onMarker = null, double lineTop = 0)
        {
            int start = line.Start, end = line.ContentEnd;
            if (end <= start) return;
            double slack = width - line.Width;
            double offset = 0;
            double gapExtra = 0;
            bool spaceMode = false;

            // タブ位置に従うタブを含む行は、タブ位置がずれるので両端揃え・均等割り付けにしない
            bool distribute = !line.HasTab && (alignment == TextAlignment.Distribute || (alignment == TextAlignment.Justify && !line.IsLast));
            if (distribute && slack > 0)
            {
                int spaces = 0, visible = 0;
                bool ideographic = false;
                for (int k = start; k < end; k++)
                {
                    var it = _items[k];
                    if (it.Class == BreakClass.Space) spaces++;
                    if (it.Width > 0 && !FollowedByRubyCont(k, end)) visible++;
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
                    else extra = it.Width > 0 && !FollowedByRubyCont(k, end) ? gapExtra : 0;
                }
                extras[k - start] = extra;
                cx += it.Width + extra;
            }

            // アンカーの位置を通知する
            if (onMarker != null)
            {
                for (int k = start; k < end; k++)
                {
                    if (_items[k].Marker != null) onMarker(_items[k].Marker!, xs[k - start], lineTop);
                }
            }

            // 背景色
            ForEachSpan(start, end, it => it.Style.Background.HasValue, (s, e) =>
            {
                var st = _items[s].Style;
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width + extras[e - 1 - start];
                g.FillRectangle(st.Background!.Value, x0, baseline - line.Ascent, x1 - x0, line.Ascent + line.Descent);
            });

            // ルビの位置を決めるため、この行にあるルビの組を集める
            List<RubyGroup>? rubies = null;
            for (int k = start; k < end; k++)
            {
                var group = _items[k].Ruby;
                if (group != null && group.First == k) (rubies ?? (rubies = new List<RubyGroup>())).Add(group);
            }
            int nextRuby = 0;

            // 文字
            int runStart = start;
            while (runStart < end)
            {
                // 親文字を描き終えた組から順にルビを描く（テキスト抽出の順序が「親文字 → ルビ」になる）
                while (rubies != null && nextRuby < rubies.Count && rubies[nextRuby].First + rubies[nextRuby].Count <= runStart)
                    DrawRuby(g, rubies[nextRuby++], xs, start, end, baseline);
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
                    if (!it.HasGlyph || it.Font != first.Font || !SameVisual(it, first) || extras[runEnd - start] != extras[runStart - start]
                        || it.Ruby != first.Ruby || it.Pad != 0 || first.Pad != 0) break;
                    runEnd++;
                }
                var gids = new ushort[runEnd - runStart];
                var cps = new int[runEnd - runStart];
                for (int k = runStart; k < runEnd; k++)
                {
                    gids[k - runStart] = _items[k].Gid;
                    cps[k - runStart] = _items[k].Codepoint;
                }
                g.ShowGlyphs(first.Font, first.Style.DrawSize, first.Style.Color, first.SimBold, first.SimItalic,
                    first.Style.CharacterSpacing + extras[runStart - start], xs[runStart - start] + first.Pad / 2, baseline + first.Style.BaselineOffset, gids, cps);
                runStart = runEnd;
            }
            while (rubies != null && nextRuby < rubies.Count) DrawRuby(g, rubies[nextRuby++], xs, start, end, baseline);

            // タブのリーダー
            if (line.HasTab)
            {
                for (int k = start; k < end; k++)
                {
                    var it = _items[k];
                    if (it.Codepoint == '\t' && it.Leader != TabLeader.None && it.Width > 0)
                        DrawLeader(g, it, xs[k - start], baseline);
                }
            }

            // 下線・取り消し線
            // 上付き・下付きの文字は、縮小した大きさとずらしたベースラインに合わせて線を引く
            ForEachSpan(start, end, it => it.Style.Underline, (s, e) =>
            {
                var it = _items[s];
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                double thickness = it.Font.UnderlineThickness * it.Style.DrawSize;
                double y = baseline + it.Style.BaselineOffset + it.Font.UnderlineOffset * it.Style.DrawSize;
                g.FillRectangle(it.Style.Color, x0, y - thickness / 2, x1 - x0, thickness);
            }, splitOnShift: true);
            ForEachSpan(start, end, it => it.Style.Strikeout, (s, e) =>
            {
                var it = _items[s];
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                double thickness = it.Font.UnderlineThickness * it.Style.DrawSize;
                double y = baseline + it.Style.BaselineOffset - it.Style.DrawSize * 0.3;
                g.FillRectangle(it.Style.Color, x0, y - thickness / 2, x1 - x0, thickness);
            }, splitOnShift: true);

            // リンク
            ForEachSpan(start, end, it => it.Style.Link != null || it.Style.LinkAnchor != null, (s, e) =>
            {
                double x0 = xs[s - start];
                double x1 = xs[e - 1 - start] + _items[e - 1].Width;
                var linkRect = new PdfRect(x0, baseline - line.Ascent, x1 - x0, line.Ascent + line.Descent);
                var linkStyle = _items[s].Style;
                if (linkStyle.Link != null) g.AddLink(linkRect, linkStyle.Link);
                else g.AddNamedLink(linkRect, linkStyle.LinkAnchor!);
            }, sameStyleOnly: true);
        }

        /// <summary>項目 <paramref name="k"/> の次が、同じルビの組の続き（両端揃えで文字の間を空けない）か。</summary>
        private bool FollowedByRubyCont(int k, int end) => k + 1 < end && _items[k + 1].RubyCont;

        /// <summary>
        /// ルビを描く。親文字の範囲（余白を含む）の中央にルビの中央をそろえ、ルビの下端を親文字の上端に付ける。
        /// ルビは「内容が空のテキスト（ActualText）」の印を付け、テキスト抽出では親文字だけが本文になるようにする。
        /// </summary>
        private void DrawRuby(PdfGraphics g, RubyGroup group, double[] xs, int start, int end, double baseline)
        {
            int last = Math.Min(group.First + group.Count, end) - 1;
            double x0 = xs[group.First - start];
            double x1 = xs[last - start] + _items[last].Width;
            double x = (x0 + x1) / 2 - group.Width / 2;
            double y = baseline - group.ParentAscent - group.Descent;
            var items = group.Items;
            g.BeginIgnoredText();
            int runStart = 0;
            double runX = x;
            while (runStart < items.Length)
            {
                var first = items[runStart];
                int runEnd = runStart + 1;
                double nextX = runX + first.Width;
                while (runEnd < items.Length && items[runEnd].Font == first.Font && SameVisual(items[runEnd], first))
                {
                    nextX += items[runEnd].Width;
                    runEnd++;
                }
                var gids = new ushort[runEnd - runStart];
                var cps = new int[runEnd - runStart];
                for (int k = runStart; k < runEnd; k++)
                {
                    gids[k - runStart] = items[k].Gid;
                    cps[k - runStart] = items[k].Codepoint;
                }
                g.ShowGlyphs(first.Font, first.Style.DrawSize, first.Style.Color, first.SimBold, first.SimItalic,
                    first.Style.CharacterSpacing, runX, y + first.Style.BaselineOffset, gids, cps);
                runStart = runEnd;
                runX = nextX;
            }
            g.EndMarkedContent();
        }

        /// <summary>
        /// タブの空き部分にリーダーを描く。点線は丸い点、破線は短い線を、タブ位置を基準にした一定の間隔で並べる
        /// （行が違っても、同じタブ位置なら点の位置が縦にそろう）。
        /// </summary>
        private static void DrawLeader(PdfGraphics g, TextItem tab, double x0, double baseline)
        {
            var st = tab.Style;
            double size = st.Size;
            double end = x0 + tab.Width;
            double anchor = x0 + tab.Reach;
            double margin = size * 0.2;
            double lo = x0 + margin, hi = end - margin;
            if (hi <= lo) return;
            switch (tab.Leader)
            {
                case TabLeader.Dots:
                {
                    double d = size * 0.1, pitch = size * 0.3;
                    double first = anchor - Math.Floor((anchor - lo) / pitch) * pitch;
                    if (first > hi) return;
                    double last = first + Math.Floor((hi - first) / pitch + 1e-9) * pitch;
                    var pen = new PdfPen(st.Color, d) { DashPattern = new[] { 0, pitch }, LineCap = LineCap.Round };
                    double y = baseline - d / 2;
                    g.DrawLine(pen, first, y, last + pitch / 2, y);
                    break;
                }
                case TabLeader.Dashes:
                {
                    double dash = size * 0.35, gap = size * 0.25, pitch = dash + gap;
                    double first = anchor - Math.Floor((anchor - lo) / pitch) * pitch;
                    if (first + dash > hi) return;
                    double last = first + Math.Floor((hi - dash - first) / pitch + 1e-9) * pitch;
                    var pen = new PdfPen(st.Color, Math.Max(0.3, size * 0.05)) { DashPattern = new[] { dash, gap } };
                    double y = baseline - size * 0.25;
                    g.DrawLine(pen, first, y, last + dash, y);
                    break;
                }
                case TabLeader.Line:
                {
                    double thickness = tab.Font.UnderlineThickness * size;
                    double y = baseline + tab.Font.UnderlineOffset * size * 0.5;
                    g.FillRectangle(st.Color, lo, y - thickness / 2, hi - lo, thickness);
                    break;
                }
            }
        }

        private static bool SameVisual(TextItem a, TextItem b)
        {
            return a.Style.DrawSize == b.Style.DrawSize && a.Style.BaselineOffset == b.Style.BaselineOffset && a.Style.Color == b.Style.Color && a.SimBold == b.SimBold && a.SimItalic == b.SimItalic
                && a.Style.CharacterSpacing == b.Style.CharacterSpacing;
        }

        private void ForEachSpan(int start, int end, Func<TextItem, bool> predicate, Action<int, int> action, bool sameStyleOnly = false, bool splitOnShift = false)
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
                while (k < end && predicate(_items[k]) && (!splitOnShift || _items[k].Style.Shift == style.Shift) && (!sameStyleOnly || ReferenceEquals(_items[k].Style, style) || (_items[k].Style.Link == style.Link && _items[k].Style.LinkAnchor == style.LinkAnchor))) k++;
                action(s, k);
            }
        }
    }
}
