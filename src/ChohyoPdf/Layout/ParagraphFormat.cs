using System;
using System.Collections.Generic;

namespace ChohyoPdf
{
    /// <summary>タブ位置での文字の揃え。</summary>
    public enum TabAlignment
    {
        /// <summary>タブ位置から右へ文字を並べる（文字の左端がタブ位置）。</summary>
        Left,

        /// <summary>タブ位置を中心に文字を並べる。</summary>
        Center,

        /// <summary>タブ位置の左へ文字を並べる（文字の右端がタブ位置）。目次のページ番号などに使う。</summary>
        Right,

        /// <summary>小数点（「.」または「．」）をタブ位置に合わせる。小数点がなければ <see cref="Right"/> と同じ。</summary>
        Decimal,
    }

    /// <summary>タブで空いた部分を埋めるリーダー（導線）。</summary>
    public enum TabLeader
    {
        /// <summary>何も描かない。</summary>
        None,

        /// <summary>点線（「…………」）。</summary>
        Dots,

        /// <summary>破線（「- - - -」）。</summary>
        Dashes,

        /// <summary>実線（下線のような 1 本の線）。</summary>
        Line,
    }

    /// <summary>
    /// タブ位置。段落の文字列中のタブ文字（\t）は、現在位置より右にある最初のタブ位置まで進む。
    /// 位置は、段落の本文領域の左端（<see cref="ParagraphFormat.LeftIndent"/> の位置）からの距離（ポイント）。
    /// </summary>
    public sealed class TabStop
    {
        /// <summary>位置（ポイント）・揃え・リーダーを指定してタブ位置を作る。</summary>
        public TabStop(double position, TabAlignment alignment = TabAlignment.Left, TabLeader leader = TabLeader.None)
        {
            if (double.IsNaN(position) || double.IsInfinity(position)) throw new ArgumentOutOfRangeException(nameof(position), "タブ位置は有限の値で指定してください。");
            Position = position;
            Alignment = alignment;
            Leader = leader;
        }

        /// <summary>段落の本文領域の左端からの位置（ポイント）。</summary>
        public double Position { get; }

        /// <summary>タブ位置での文字の揃え。</summary>
        public TabAlignment Alignment { get; }

        /// <summary>タブで空いた部分を埋めるリーダー。</summary>
        public TabLeader Leader { get; }
    }

    /// <summary>
    /// 段落の書式。未設定（null）のプロパティは親（文書の既定値、名前付きスタイル、セルなど）から引き継ぐ。
    /// </summary>
    public sealed class ParagraphFormat
    {
        /// <summary>水平方向の揃え。</summary>
        public TextAlignment? Alignment { get; set; }

        /// <summary>行送りの指定方法。</summary>
        public LineSpacingRule? LineSpacingRule { get; set; }

        /// <summary>行送り（Multiple なら文字サイズの倍数、それ以外はポイント）。</summary>
        public double? LineSpacing { get; set; }

        /// <summary>1 行目の字下げ（ポイント）。負の値ならぶら下げインデント。</summary>
        public double? FirstLineIndent { get; set; }

        /// <summary>左インデント（ポイント）。</summary>
        public double? LeftIndent { get; set; }

        /// <summary>右インデント（ポイント）。</summary>
        public double? RightIndent { get; set; }

        /// <summary>段落前の間隔（ポイント）。ページやセルの先頭では無視される。</summary>
        public double? SpaceBefore { get; set; }

        /// <summary>段落後の間隔（ポイント）。</summary>
        public double? SpaceAfter { get; set; }

        /// <summary>段落をページで分割しない。</summary>
        public bool? KeepTogether { get; set; }

        /// <summary>次のブロックと同じページに置く（見出しなど）。</summary>
        public bool? KeepWithNext { get; set; }

        /// <summary>段落の前で改ページする。</summary>
        public bool? PageBreakBefore { get; set; }

        /// <summary>
        /// 改ページ位置の調整（段落の 1 行目だけがページ末尾に残ったり、最終行だけが次ページに送られたりしないようにする）。
        /// </summary>
        public bool? WidowControl { get; set; }

        /// <summary>段落の背景色。</summary>
        public PdfColor? Background { get; set; }

        /// <summary>段落の枠線。</summary>
        public PdfPen? Border { get; set; }

        /// <summary>段落の内側の余白（背景・枠線とテキストの間）。</summary>
        public Thickness? Padding { get; set; }

        /// <summary>
        /// タブ位置。段落の文字列中のタブ文字（\t）は、現在位置より右にある最初のタブ位置まで進む。
        /// 指定すると親（文書の既定値、名前付きスタイルなど）のタブ位置を置き換える。
        /// <see cref="TabStops"/> と <see cref="DefaultTabStop"/> のどちらも未指定の段落では、タブ文字は空白 4 文字分の幅になる（従来の動作）。
        /// </summary>
        public IList<TabStop>? TabStops { get; set; }

        private int? _outlineLevel;

        /// <summary>
        /// 見出しレベル（1〜9。1 が最上位）。指定した段落は見出しになり、PDF のしおり（<see cref="Document.CreateBookmarks"/>）と
        /// 目次（<see cref="TableOfContents"/>）に入る。入れ子はレベルの大小で決まる。
        /// 名前付きスタイルで指定できる。null（既定）なら見出しではない。
        /// </summary>
        public int? OutlineLevel
        {
            get => _outlineLevel;
            set
            {
                if (value.HasValue && (value.Value < 1 || value.Value > 9))
                    throw new ArgumentOutOfRangeException(nameof(value), "見出しレベルは 1〜9 で指定してください。");
                _outlineLevel = value;
            }
        }

        /// <summary>
        /// 既定のタブ間隔（ポイント）。最後のタブ位置より右では、この間隔の整数倍の位置にタブ位置があるものとして扱う
        /// （<see cref="TabStops"/> だけを指定したときは 36 ポイント）。
        /// </summary>
        public double? DefaultTabStop { get; set; }

        /// <summary>タブ位置を追加する（<see cref="TabStops"/> は位置の昇順に並べ替える）。</summary>
        /// <param name="position">段落の本文領域の左端からの位置（ポイント）。</param>
        /// <param name="alignment">タブ位置での文字の揃え。</param>
        /// <param name="leader">タブで空いた部分を埋めるリーダー。</param>
        public ParagraphFormat AddTabStop(double position, TabAlignment alignment = TabAlignment.Left, TabLeader leader = TabLeader.None)
        {
            var list = TabStops == null ? new List<TabStop>() : new List<TabStop>(TabStops);
            list.Add(new TabStop(position, alignment, leader));
            list.Sort((a, b) => a.Position.CompareTo(b.Position));
            TabStops = list;
            return this;
        }

        /// <summary>複製を作る。</summary>
        public ParagraphFormat Clone() => new ParagraphFormat().Merge(this);

        /// <summary><paramref name="other"/> の設定済みプロパティで上書きした新しい書式を返す。</summary>
        public ParagraphFormat Merge(ParagraphFormat? other)
        {
            var r = new ParagraphFormat
            {
                Alignment = Alignment,
                LineSpacingRule = LineSpacingRule,
                LineSpacing = LineSpacing,
                FirstLineIndent = FirstLineIndent,
                LeftIndent = LeftIndent,
                RightIndent = RightIndent,
                SpaceBefore = SpaceBefore,
                SpaceAfter = SpaceAfter,
                KeepTogether = KeepTogether,
                KeepWithNext = KeepWithNext,
                PageBreakBefore = PageBreakBefore,
                WidowControl = WidowControl,
                Background = Background,
                Border = Border,
                Padding = Padding,
                TabStops = TabStops == null ? null : new List<TabStop>(TabStops),
                DefaultTabStop = DefaultTabStop,
                OutlineLevel = OutlineLevel,
            };
            if (other == null) return r;
            if (other.Alignment.HasValue) r.Alignment = other.Alignment;
            if (other.LineSpacingRule.HasValue) r.LineSpacingRule = other.LineSpacingRule;
            if (other.LineSpacing.HasValue) r.LineSpacing = other.LineSpacing;
            if (other.FirstLineIndent.HasValue) r.FirstLineIndent = other.FirstLineIndent;
            if (other.LeftIndent.HasValue) r.LeftIndent = other.LeftIndent;
            if (other.RightIndent.HasValue) r.RightIndent = other.RightIndent;
            if (other.SpaceBefore.HasValue) r.SpaceBefore = other.SpaceBefore;
            if (other.SpaceAfter.HasValue) r.SpaceAfter = other.SpaceAfter;
            if (other.KeepTogether.HasValue) r.KeepTogether = other.KeepTogether;
            if (other.KeepWithNext.HasValue) r.KeepWithNext = other.KeepWithNext;
            if (other.PageBreakBefore.HasValue) r.PageBreakBefore = other.PageBreakBefore;
            if (other.WidowControl.HasValue) r.WidowControl = other.WidowControl;
            if (other.Background.HasValue) r.Background = other.Background;
            if (other.Border != null) r.Border = other.Border;
            if (other.Padding != null) r.Padding = other.Padding;
            if (other.TabStops != null) r.TabStops = new List<TabStop>(other.TabStops);
            if (other.DefaultTabStop.HasValue) r.DefaultTabStop = other.DefaultTabStop;
            if (other.OutlineLevel.HasValue) r.OutlineLevel = other.OutlineLevel;
            return r;
        }
    }

    /// <summary>名前付き段落スタイル（文字スタイルと段落書式の組）。</summary>
    public sealed class ParagraphStyle
    {
        /// <summary>空のスタイルを作る。</summary>
        public ParagraphStyle()
        {
        }

        /// <summary>文字スタイルと段落書式を指定して作る。</summary>
        public ParagraphStyle(TextStyle? text, ParagraphFormat? format)
        {
            Text = text;
            Format = format;
        }

        /// <summary>文字スタイル。</summary>
        public TextStyle? Text { get; set; }

        /// <summary>段落書式。</summary>
        public ParagraphFormat? Format { get; set; }
    }
}
