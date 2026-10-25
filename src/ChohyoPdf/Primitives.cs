using System;
using System.Globalization;

namespace ChohyoPdf
{
    /// <summary>単位変換。ライブラリ内の長さはすべてポイント（1/72 インチ）。</summary>
    public static class Unit
    {
        /// <summary>ミリメートルをポイントに変換する。</summary>
        public static double Mm(double mm) => mm * 72.0 / 25.4;

        /// <summary>センチメートルをポイントに変換する。</summary>
        public static double Cm(double cm) => cm * 720.0 / 25.4;

        /// <summary>インチをポイントに変換する。</summary>
        public static double Inch(double inch) => inch * 72.0;

        /// <summary>ポイントをミリメートルに変換する。</summary>
        public static double ToMm(double points) => points * 25.4 / 72.0;
    }

    /// <summary>色空間。</summary>
    public enum PdfColorSpace
    {
        /// <summary>グレー。</summary>
        Gray,
        /// <summary>RGB。</summary>
        Rgb,
        /// <summary>CMYK。</summary>
        Cmyk,
    }

    /// <summary>色。各成分は 0〜1。<see cref="Alpha"/> が 1 未満なら半透明で描画する。</summary>
    public readonly struct PdfColor : IEquatable<PdfColor>
    {
        private readonly double _c1, _c2, _c3, _c4;

        /// <summary>色空間。</summary>
        public PdfColorSpace Space { get; }

        /// <summary>不透明度（0 = 透明、1 = 不透明）。</summary>
        public double Alpha { get; }

        private PdfColor(PdfColorSpace space, double c1, double c2, double c3, double c4, double alpha)
        {
            Space = space;
            _c1 = Clamp(c1);
            _c2 = Clamp(c2);
            _c3 = Clamp(c3);
            _c4 = Clamp(c4);
            Alpha = Clamp(alpha);
        }

        private static double Clamp(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>RGB（0〜255）で色を作る。</summary>
        public static PdfColor FromRgb(int r, int g, int b) => new PdfColor(PdfColorSpace.Rgb, r / 255.0, g / 255.0, b / 255.0, 0, 1);

        /// <summary>RGB（0〜1）で色を作る。</summary>
        public static PdfColor FromRgb(double r, double g, double b) => new PdfColor(PdfColorSpace.Rgb, r, g, b, 0, 1);

        /// <summary>グレー（0 = 黒、1 = 白）。</summary>
        public static PdfColor FromGray(double gray) => new PdfColor(PdfColorSpace.Gray, gray, 0, 0, 0, 1);

        /// <summary>CMYK（0〜1）。</summary>
        public static PdfColor FromCmyk(double c, double m, double y, double k) => new PdfColor(PdfColorSpace.Cmyk, c, m, y, k, 1);

        /// <summary>"#RRGGBB"、"#RGB"、"#RRGGBBAA" 形式から色を作る。</summary>
        public static PdfColor FromHex(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            if (h.Length != 6 && h.Length != 8) throw new FormatException("色の形式が不正です: " + hex);
            int Part(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var color = FromRgb(Part(0), Part(2), Part(4));
            return h.Length == 8 ? color.WithAlpha(Part(6) / 255.0) : color;
        }

        /// <summary>不透明度を変えた色を返す。</summary>
        public PdfColor WithAlpha(double alpha) => new PdfColor(Space, _c1, _c2, _c3, _c4, alpha);

        /// <summary>黒。</summary>
        public static PdfColor Black => FromGray(0);
        /// <summary>白。</summary>
        public static PdfColor White => FromGray(1);
        /// <summary>灰色（グレー 0.5）。</summary>
        public static PdfColor Gray => FromGray(0.5);
        /// <summary>明るい灰色（グレー 0.83）。</summary>
        public static PdfColor LightGray => FromGray(0.83);
        /// <summary>暗い灰色（グレー 0.33）。</summary>
        public static PdfColor DarkGray => FromGray(0.33);
        /// <summary>赤（#FF0000）。</summary>
        public static PdfColor Red => FromRgb(255, 0, 0);
        /// <summary>緑（#008000）。</summary>
        public static PdfColor Green => FromRgb(0, 128, 0);
        /// <summary>青（#0000FF）。</summary>
        public static PdfColor Blue => FromRgb(0, 0, 255);
        /// <summary>紺（#000080）。</summary>
        public static PdfColor Navy => FromRgb(0, 0, 128);
        /// <summary>オレンジ（#FFA500）。</summary>
        public static PdfColor Orange => FromRgb(255, 165, 0);
        /// <summary>黄（#FFFF00）。</summary>
        public static PdfColor Yellow => FromRgb(255, 255, 0);
        /// <summary>透明。</summary>
        public static PdfColor Transparent => FromGray(0).WithAlpha(0);

        internal string FillOperator()
        {
            switch (Space)
            {
                case PdfColorSpace.Gray: return Internal.PdfFormat.Num(_c1) + " g";
                case PdfColorSpace.Cmyk: return Internal.PdfFormat.Num(_c1) + " " + Internal.PdfFormat.Num(_c2) + " " + Internal.PdfFormat.Num(_c3) + " " + Internal.PdfFormat.Num(_c4) + " k";
                default: return Internal.PdfFormat.Num(_c1) + " " + Internal.PdfFormat.Num(_c2) + " " + Internal.PdfFormat.Num(_c3) + " rg";
            }
        }

        internal string StrokeOperator()
        {
            switch (Space)
            {
                case PdfColorSpace.Gray: return Internal.PdfFormat.Num(_c1) + " G";
                case PdfColorSpace.Cmyk: return Internal.PdfFormat.Num(_c1) + " " + Internal.PdfFormat.Num(_c2) + " " + Internal.PdfFormat.Num(_c3) + " " + Internal.PdfFormat.Num(_c4) + " K";
                default: return Internal.PdfFormat.Num(_c1) + " " + Internal.PdfFormat.Num(_c2) + " " + Internal.PdfFormat.Num(_c3) + " RG";
            }
        }

        /// <summary>色空間・各成分・不透明度がすべて等しいかを返す。</summary>
        public bool Equals(PdfColor other) => Space == other.Space && _c1 == other._c1 && _c2 == other._c2 && _c3 == other._c3 && _c4 == other._c4 && Alpha == other.Alpha;

        /// <summary>色空間・各成分・不透明度がすべて等しいかを返す。</summary>
        public override bool Equals(object? obj) => obj is PdfColor c && Equals(c);

        /// <summary>ハッシュコードを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Space;
                h = h * 31 + _c1.GetHashCode();
                h = h * 31 + _c2.GetHashCode();
                h = h * 31 + _c3.GetHashCode();
                h = h * 31 + _c4.GetHashCode();
                return h * 31 + Alpha.GetHashCode();
            }
        }

        /// <summary>2 つの色が等しいかを返す。</summary>
        public static bool operator ==(PdfColor a, PdfColor b) => a.Equals(b);

        /// <summary>2 つの色が異なるかを返す。</summary>
        public static bool operator !=(PdfColor a, PdfColor b) => !a.Equals(b);

        /// <summary>色空間と各成分を文字列で返す（デバッグ用）。</summary>
        public override string ToString() => Space + "(" + _c1 + ", " + _c2 + ", " + _c3 + ", " + _c4 + ", a=" + Alpha + ")";
    }

    /// <summary>線端の形。</summary>
    public enum LineCap
    {
        /// <summary>線の端で切る。</summary>
        Butt = 0,
        /// <summary>線の端を丸くする。</summary>
        Round = 1,
        /// <summary>線の端から線幅の半分だけ伸ばして、四角く切る。</summary>
        Square = 2,
    }

    /// <summary>線の結合部の形。</summary>
    public enum LineJoin
    {
        /// <summary>角をとがらせる。</summary>
        Miter = 0,
        /// <summary>角を丸くする。</summary>
        Round = 1,
        /// <summary>角を斜めに切る。</summary>
        Bevel = 2,
    }

    /// <summary>線のスタイル。</summary>
    public sealed class PdfPen
    {
        /// <summary>色と線幅（ポイント）を指定して作る。</summary>
        public PdfPen(PdfColor color, double width = 1)
        {
            Color = color;
            Width = width;
        }

        /// <summary>線の色。</summary>
        public PdfColor Color { get; set; }

        /// <summary>線幅（ポイント）。</summary>
        public double Width { get; set; }

        /// <summary>破線パターン（ポイント単位の 線, 間隔, 線, 間隔 ...）。null または空なら実線。</summary>
        public double[]? DashPattern { get; set; }

        /// <summary>破線パターンを始める位置（ポイント）。</summary>
        public double DashPhase { get; set; }

        /// <summary>線端の形（既定 <see cref="LineCap.Butt"/>）。</summary>
        public LineCap LineCap { get; set; } = LineCap.Butt;

        /// <summary>線の結合部の形（既定 <see cref="LineJoin.Miter"/>）。</summary>
        public LineJoin LineJoin { get; set; } = LineJoin.Miter;

        /// <summary>破線のペンを作る。</summary>
        public static PdfPen Dashed(PdfColor color, double width, params double[] pattern)
            => new PdfPen(color, width) { DashPattern = pattern.Length > 0 ? pattern : new[] { 3.0, 2.0 } };

        /// <summary>点線（丸い点）のペンを作る。</summary>
        public static PdfPen Dotted(PdfColor color, double width)
            => new PdfPen(color, width) { DashPattern = new[] { 0, width * 2 }, LineCap = LineCap.Round };

        /// <summary>複製を作る。</summary>
        public PdfPen Clone() => new PdfPen(Color, Width)
        {
            DashPattern = DashPattern == null ? null : (double[])DashPattern.Clone(),
            DashPhase = DashPhase,
            LineCap = LineCap,
            LineJoin = LineJoin,
        };
    }

    /// <summary>用紙サイズ（ポイント）。</summary>
    public readonly struct PageSize : IEquatable<PageSize>
    {
        /// <summary>幅と高さ（ポイント）を指定して作る。</summary>
        public PageSize(double width, double height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "用紙サイズは正の値で指定してください。");
            Width = width;
            Height = height;
        }

        /// <summary>幅（ポイント）。</summary>
        public double Width { get; }

        /// <summary>高さ（ポイント）。</summary>
        public double Height { get; }

        /// <summary>横向き（幅 &gt; 高さ）にしたサイズ。</summary>
        public PageSize Landscape => Width >= Height ? this : new PageSize(Height, Width);

        /// <summary>縦向き（幅 &lt; 高さ）にしたサイズ。</summary>
        public PageSize Portrait => Width <= Height ? this : new PageSize(Height, Width);

        /// <summary>幅と高さをミリメートルで指定して作る。</summary>
        public static PageSize FromMm(double width, double height) => new PageSize(Unit.Mm(width), Unit.Mm(height));

        /// <summary>A3（297 × 420 mm）。</summary>
        public static PageSize A3 => FromMm(297, 420);
        /// <summary>A4（210 × 297 mm）。</summary>
        public static PageSize A4 => FromMm(210, 297);
        /// <summary>A5（148 × 210 mm）。</summary>
        public static PageSize A5 => FromMm(148, 210);
        /// <summary>A6（105 × 148 mm）。</summary>
        public static PageSize A6 => FromMm(105, 148);

        /// <summary>JIS B4（257 × 364 mm）。</summary>
        public static PageSize B4 => FromMm(257, 364);

        /// <summary>JIS B5（182 × 257 mm）。</summary>
        public static PageSize B5 => FromMm(182, 257);

        /// <summary>レター（8.5 × 11 インチ）。</summary>
        public static PageSize Letter => new PageSize(612, 792);
        /// <summary>リーガル（8.5 × 14 インチ）。</summary>
        public static PageSize Legal => new PageSize(612, 1008);

        /// <summary>官製はがき（100 × 148 mm）。</summary>
        public static PageSize Postcard => FromMm(100, 148);

        /// <summary>長形3号封筒（120 × 235 mm）。</summary>
        public static PageSize EnvelopeChou3 => FromMm(120, 235);

        /// <summary>幅と高さが等しいかを返す。</summary>
        public bool Equals(PageSize other) => Width == other.Width && Height == other.Height;

        /// <summary>幅と高さが等しいかを返す。</summary>
        public override bool Equals(object? obj) => obj is PageSize p && Equals(p);

        /// <summary>ハッシュコードを返す。</summary>
        public override int GetHashCode() => Width.GetHashCode() * 31 + Height.GetHashCode();

        /// <summary>"210 x 297 mm" のように、ミリメートルで表した文字列を返す。</summary>
        public override string ToString() => Unit.ToMm(Width).ToString("0.#", CultureInfo.InvariantCulture) + " x " + Unit.ToMm(Height).ToString("0.#", CultureInfo.InvariantCulture) + " mm";
    }

    /// <summary>矩形（左上原点、ポイント）。</summary>
    public readonly struct PdfRect
    {
        /// <summary>左上の座標と、幅・高さ（ポイント）を指定して作る。</summary>
        public PdfRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>左端の x 座標。</summary>
        public double X { get; }
        /// <summary>上端の y 座標。</summary>
        public double Y { get; }
        /// <summary>幅。</summary>
        public double Width { get; }
        /// <summary>高さ。</summary>
        public double Height { get; }
        /// <summary>右端の x 座標。</summary>
        public double Right => X + Width;
        /// <summary>下端の y 座標。</summary>
        public double Bottom => Y + Height;

        /// <summary>内側に余白を取った矩形。</summary>
        public PdfRect Deflate(double left, double top, double right, double bottom)
            => new PdfRect(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));

        /// <summary>"(x, y, 幅, 高さ)" の形の文字列を返す。</summary>
        public override string ToString() => "(" + X + ", " + Y + ", " + Width + ", " + Height + ")";
    }

    /// <summary>上下左右の余白・間隔（ポイント）。</summary>
    public sealed class Thickness
    {
        /// <summary>すべて 0 で作る。</summary>
        public Thickness()
        {
        }

        /// <summary>上下左右を同じ値で作る。</summary>
        public Thickness(double all) : this(all, all, all, all)
        {
        }

        /// <summary>左右と上下の値を指定して作る。</summary>
        public Thickness(double horizontal, double vertical) : this(horizontal, vertical, horizontal, vertical)
        {
        }

        /// <summary>左・上・右・下の値を指定して作る。</summary>
        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>左（ポイント）。</summary>
        public double Left { get; set; }
        /// <summary>上（ポイント）。</summary>
        public double Top { get; set; }
        /// <summary>右（ポイント）。</summary>
        public double Right { get; set; }
        /// <summary>下（ポイント）。</summary>
        public double Bottom { get; set; }

        /// <summary>ミリメートル指定で作る。</summary>
        public static Thickness FromMm(double left, double top, double right, double bottom)
            => new Thickness(Unit.Mm(left), Unit.Mm(top), Unit.Mm(right), Unit.Mm(bottom));

        /// <summary>上下左右を同じ値（ミリメートル）で作る。</summary>
        public static Thickness FromMm(double all) => new Thickness(Unit.Mm(all));

        /// <summary>複製を作る。</summary>
        public Thickness Clone() => new Thickness(Left, Top, Right, Bottom);
    }

    /// <summary>水平方向の揃え。</summary>
    public enum TextAlignment
    {
        /// <summary>左揃え。</summary>
        Left,
        /// <summary>中央揃え。</summary>
        Center,
        /// <summary>右揃え。</summary>
        Right,

        /// <summary>両端揃え（最終行は左揃え）。</summary>
        Justify,

        /// <summary>均等割り付け（最終行も含めて両端に揃える）。</summary>
        Distribute,
    }

    /// <summary>垂直方向の揃え。</summary>
    public enum VerticalAlignment
    {
        /// <summary>上揃え。</summary>
        Top,
        /// <summary>中央揃え。</summary>
        Middle,
        /// <summary>下揃え。</summary>
        Bottom,
    }

    /// <summary>ブロック（画像・図・表）の水平位置。</summary>
    public enum HorizontalAlignment
    {
        /// <summary>左揃え。</summary>
        Left,
        /// <summary>中央揃え。</summary>
        Center,
        /// <summary>右揃え。</summary>
        Right,
    }
}
