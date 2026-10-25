using System;
using System.Collections.Generic;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>デザイン画面で部品を動かしている間に重ねる画像の種類。</summary>
    public enum PreviewLayer
    {
        /// <summary>部品の枠線・背景（テキストと表の、文字以外）。大きさを変えると伸び縮みさせる。</summary>
        Decoration,

        /// <summary>部品の中身（テキストと表では文字だけ。ほかの部品はすべて）。</summary>
        Content,
    }

    /// <summary>用紙の上の位置（mm）p を、(p.X × ScaleX + OffsetX, p.Y × ScaleY + OffsetY) に移す変換。</summary>
    public readonly struct PreviewMap
    {
        public PreviewMap(double scaleX, double scaleY, double offsetX, double offsetY)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public double ScaleX { get; }
        public double ScaleY { get; }
        public double OffsetX { get; }
        public double OffsetY { get; }

        /// <summary>平行移動だけの変換。</summary>
        public static PreviewMap Translate(double dx, double dy) => new PreviewMap(1, 1, dx, dy);

        /// <summary>矩形 from を矩形 to に移す変換（幅や高さが 0 の向きは、伸び縮みさせずにずらす）。</summary>
        public static PreviewMap Between(Rect from, Rect to)
        {
            var (sx, tx) = Axis(from.X, from.Right, to.X, to.Right);
            var (sy, ty) = Axis(from.Y, from.Bottom, to.Y, to.Bottom);
            return new PreviewMap(sx, sy, tx, ty);
        }

        /// <summary>基準点 a1, a2 を b1, b2 に移す 1 方向の変換（a1 と a2 が同じなら、伸び縮みさせずにずらす）。</summary>
        internal static (double Scale, double Offset) Axis(double a1, double a2, double b1, double b2)
        {
            if (Math.Abs(a2 - a1) < 1e-6) return (1, b1 - a1);
            double s = (b2 - b1) / (a2 - a1);
            return (s, b1 - a1 * s);
        }

        public double MapX(double x) => x * ScaleX + OffsetX;

        public double MapY(double y) => y * ScaleY + OffsetY;
    }

    /// <summary>重ねる画像 1 枚の描き方。<see cref="Layer"/> の画像を <see cref="Map"/> で移して描き、<see cref="Clip"/>（mm。null なら切り抜かない）の外は描かない。</summary>
    public readonly struct PreviewPart
    {
        public PreviewPart(PreviewLayer layer, PreviewMap map, Rect? clip = null)
        {
            Layer = layer;
            Map = map;
            Clip = clip;
        }

        public PreviewLayer Layer { get; }
        public PreviewMap Map { get; }
        public Rect? Clip { get; }
    }

    /// <summary>
    /// 部品を動かしたり大きさを変えたりしている間に、前に描いた部品の画像を今の位置・大きさに合わせて表示するための計算。
    /// 描き直しが追いつかない間も、文字は伸び縮みさせずに揃えの位置に合わせてずらし、罫線や図形だけを伸び縮みさせる。
    /// </summary>
    public static class MovePreviewGeometry
    {
        /// <summary>
        /// 部品を <paramref name="rendered"/> の状態で描いた画像を、<paramref name="current"/> の状態に合わせて表示する描き方。
        /// <paramref name="imageAspect"/> は画像の部品の縦横比（幅 ÷ 高さ。分からなければ null）。
        /// <see cref="PreviewLayer.Decoration"/> は、テキストと表のときだけ使う（そのほかの部品は <see cref="PreviewLayer.Content"/> に全体を描く）。
        /// </summary>
        public static List<PreviewPart> Parts(ReportElement rendered, ReportElement current, double? imageAspect = null)
        {
            var parts = new List<PreviewPart>();
            switch (rendered)
            {
                case LineElement from when current is LineElement to:
                    {
                        var (sx, tx) = PreviewMap.Axis(from.X1, from.X2, to.X1, to.X2);
                        var (sy, ty) = PreviewMap.Axis(from.Y1, from.Y2, to.Y1, to.Y2);
                        parts.Add(new PreviewPart(PreviewLayer.Content, new PreviewMap(sx, sy, tx, ty)));
                        break;
                    }
                case TextElement from when current is TextElement to:
                    parts.Add(new PreviewPart(PreviewLayer.Decoration, PreviewMap.Between(from.Bounds, to.Bounds)));
                    parts.Add(new PreviewPart(PreviewLayer.Content, Anchored(TextArea(from), TextArea(to), from.Alignment, from.VerticalAlignment),
                        from.Wrap ? to.Bounds : (Rect?)null));
                    break;
                case TableElement from when current is TableElement to && SameShape(from, to):
                    parts.Add(new PreviewPart(PreviewLayer.Decoration, PreviewMap.Between(from.Bounds, to.Bounds)));
                    AddCells(parts, from, to);
                    break;
                case ImageElement from when current is ImageElement to && from.Fit == ImageFit.Uniform && imageAspect is double aspect && aspect > 0:
                    parts.Add(new PreviewPart(PreviewLayer.Content, PreviewMap.Between(Fit(from.Bounds, aspect), Fit(to.Bounds, aspect))));
                    break;
                default:
                    parts.Add(new PreviewPart(PreviewLayer.Content, PreviewMap.Between(rendered.Bounds, current.Bounds)));
                    break;
            }
            return parts;
        }

        /// <summary>文字を描く矩形（内側の余白を引いたもの）。</summary>
        private static Rect TextArea(TextElement e)
        {
            double p = Math.Max(0, Math.Min(e.Padding, Math.Min(e.Width, e.Height) / 2));
            return new Rect(e.X + p, e.Y + p, e.Width - p * 2, e.Height - p * 2);
        }

        /// <summary>from の揃えの基準点（左・中央・右、上・中央・下）を、to の同じ基準点に移す平行移動。</summary>
        private static PreviewMap Anchored(Rect from, Rect to, TextAlignment alignment, VerticalAlignment vertical)
        {
            double h = alignment == TextAlignment.Center ? 0.5 : alignment == TextAlignment.Right ? 1 : 0;
            double v = vertical == VerticalAlignment.Middle ? 0.5 : vertical == VerticalAlignment.Bottom ? 1 : 0;
            return PreviewMap.Translate(to.X + to.Width * h - (from.X + from.Width * h), to.Y + to.Height * v - (from.Y + from.Height * v));
        }

        private static bool SameShape(TableElement a, TableElement b)
            => a.Columns.Count == b.Columns.Count && a.Rows.Count == b.Rows.Count && a.HasDetail == b.HasDetail && a.DetailRowCount == b.DetailRowCount;

        /// <summary>表の文字は、セルごとに揃えの位置に合わせてずらし、今のセルの範囲で切り抜く。</summary>
        private static void AddCells(List<PreviewPart> parts, TableElement from, TableElement to)
        {
            var a = new TableLayout(from);
            var b = new TableLayout(to);
            var owners = from.GetOwnerMap();
            double padA = from.CellPadding, padB = to.CellPadding;
            for (int r = 0; r < from.Rows.Count; r++)
            {
                for (int c = 0; c < from.Columns.Count; c++)
                {
                    if (owners[r, c] != (r, c)) continue;
                    var cell = from.Rows[r].Cells[c];
                    if (cell.Text.Length == 0) continue;
                    AddCell(parts, a.GetFixedCell(r, c), b.GetFixedCell(r, c), padA, padB,
                        cell.Alignment ?? from.Columns[c].Alignment, cell.VerticalAlignment ?? from.VerticalAlignment);
                }
            }
            if (!from.HasDetail) return;
            foreach (int c in a.DetailColumns)
            {
                var cell = from.DetailRow.Cells[c];
                var spanA = a.GetDetailCell(c);
                var spanB = b.GetDetailCell(c);
                for (int row = 0; row < from.DetailRowCount; row++)
                {
                    var cellA = new Rect(spanA.X, a.DetailTop + row * from.DetailRow.Height, spanA.Width, from.DetailRow.Height);
                    var cellB = new Rect(spanB.X, b.DetailTop + row * to.DetailRow.Height, spanB.Width, to.DetailRow.Height);
                    AddCell(parts, cellA, cellB, padA, padB, cell.Alignment ?? from.Columns[c].Alignment, cell.VerticalAlignment ?? from.VerticalAlignment);
                }
            }
        }

        private static void AddCell(List<PreviewPart> parts, Rect from, Rect to, double padA, double padB, TextAlignment alignment, VerticalAlignment vertical)
            => parts.Add(new PreviewPart(PreviewLayer.Content, Anchored(Inset(from, padA), Inset(to, padB), alignment, vertical), to));

        private static Rect Inset(Rect r, double d) => new Rect(r.X + d, r.Y + d, Math.Max(0, r.Width - d * 2), Math.Max(0, r.Height - d * 2));

        /// <summary>縦横比 aspect の画像を、縦横比を保って矩形に収めた範囲（中央寄せ。DrawImageFit と同じ）。</summary>
        private static Rect Fit(Rect box, double aspect)
        {
            if (box.Width <= 0 || box.Height <= 0) return box;
            double w = Math.Min(box.Width, box.Height * aspect);
            double h = w / aspect;
            return new Rect(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }
    }
}
