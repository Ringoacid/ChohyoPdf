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
        Gray,
        Rgb,
        Cmyk,
    }

    /// <summary>色。各成分は 0〜1。<see cref="Alpha"/> が 1 未満なら半透明で描画する。</summary>
    public readonly struct PdfColor : IEquatable<PdfColor>
    {
        private readonly double _c1, _c2, _c3, _c4;

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

        public static PdfColor Black => FromGray(0);
        public static PdfColor White => FromGray(1);
        public static PdfColor Gray => FromGray(0.5);
        public static PdfColor LightGray => FromGray(0.83);
        public static PdfColor DarkGray => FromGray(0.33);
        public static PdfColor Red => FromRgb(255, 0, 0);
        public static PdfColor Green => FromRgb(0, 128, 0);
        public static PdfColor Blue => FromRgb(0, 0, 255);
        public static PdfColor Navy => FromRgb(0, 0, 128);
        public static PdfColor Orange => FromRgb(255, 165, 0);
        public static PdfColor Yellow => FromRgb(255, 255, 0);
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

        public bool Equals(PdfColor other) => Space == other.Space && _c1 == other._c1 && _c2 == other._c2 && _c3 == other._c3 && _c4 == other._c4 && Alpha == other.Alpha;

        public override bool Equals(object? obj) => obj is PdfColor c && Equals(c);

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

        public static bool operator ==(PdfColor a, PdfColor b) => a.Equals(b);

        public static bool operator !=(PdfColor a, PdfColor b) => !a.Equals(b);

        public override string ToString() => Space + "(" + _c1 + ", " + _c2 + ", " + _c3 + ", " + _c4 + ", a=" + Alpha + ")";
    }

    /// <summary>線端の形。</summary>
    public enum LineCap
    {
        Butt = 0,
        Round = 1,
        Square = 2,
    }

    /// <summary>線の結合部の形。</summary>
    public enum LineJoin
    {
        Miter = 0,
        Round = 1,
        Bevel = 2,
    }

    /// <summary>線のスタイル。</summary>
    public sealed class PdfPen
    {
        public PdfPen(PdfColor color, double width = 1)
        {
            Color = color;
            Width = width;
        }

        public PdfColor Color { get; set; }

        /// <summary>線幅（ポイント）。</summary>
        public double Width { get; set; }

        /// <summary>破線パターン（ポイント単位の 線, 間隔, 線, 間隔 ...）。null または空なら実線。</summary>
        public double[]? DashPattern { get; set; }

        public double DashPhase { get; set; }

        public LineCap LineCap { get; set; } = LineCap.Butt;

        public LineJoin LineJoin { get; set; } = LineJoin.Miter;

        /// <summary>破線のペンを作る。</summary>
        public static PdfPen Dashed(PdfColor color, double width, params double[] pattern)
            => new PdfPen(color, width) { DashPattern = pattern.Length > 0 ? pattern : new[] { 3.0, 2.0 } };

        /// <summary>点線（丸い点）のペンを作る。</summary>
        public static PdfPen Dotted(PdfColor color, double width)
            => new PdfPen(color, width) { DashPattern = new[] { 0, width * 2 }, LineCap = LineCap.Round };

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
        public PageSize(double width, double height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "用紙サイズは正の値で指定してください。");
            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }

        /// <summary>横向き（幅 &gt; 高さ）にしたサイズ。</summary>
        public PageSize Landscape => Width >= Height ? this : new PageSize(Height, Width);

        /// <summary>縦向き（幅 &lt; 高さ）にしたサイズ。</summary>
        public PageSize Portrait => Width <= Height ? this : new PageSize(Height, Width);

        public static PageSize FromMm(double width, double height) => new PageSize(Unit.Mm(width), Unit.Mm(height));

        public static PageSize A3 => FromMm(297, 420);
        public static PageSize A4 => FromMm(210, 297);
        public static PageSize A5 => FromMm(148, 210);
        public static PageSize A6 => FromMm(105, 148);

        /// <summary>JIS B4（257 × 364 mm）。</summary>
        public static PageSize B4 => FromMm(257, 364);

        /// <summary>JIS B5（182 × 257 mm）。</summary>
        public static PageSize B5 => FromMm(182, 257);

        public static PageSize Letter => new PageSize(612, 792);
        public static PageSize Legal => new PageSize(612, 1008);

        /// <summary>官製はがき（100 × 148 mm）。</summary>
        public static PageSize Postcard => FromMm(100, 148);

        /// <summary>長形3号封筒（120 × 235 mm）。</summary>
        public static PageSize EnvelopeChou3 => FromMm(120, 235);

        public bool Equals(PageSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is PageSize p && Equals(p);

        public override int GetHashCode() => Width.GetHashCode() * 31 + Height.GetHashCode();

        public override string ToString() => Unit.ToMm(Width).ToString("0.#", CultureInfo.InvariantCulture) + " x " + Unit.ToMm(Height).ToString("0.#", CultureInfo.InvariantCulture) + " mm";
    }

    /// <summary>矩形（左上原点、ポイント）。</summary>
    public readonly struct PdfRect
    {
        public PdfRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Right => X + Width;
        public double Bottom => Y + Height;

        /// <summary>内側に余白を取った矩形。</summary>
        public PdfRect Deflate(double left, double top, double right, double bottom)
            => new PdfRect(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));

        public override string ToString() => "(" + X + ", " + Y + ", " + Width + ", " + Height + ")";
    }

    /// <summary>上下左右の余白・間隔（ポイント）。</summary>
    public sealed class Thickness
    {
        public Thickness()
        {
        }

        public Thickness(double all) : this(all, all, all, all)
        {
        }

        public Thickness(double horizontal, double vertical) : this(horizontal, vertical, horizontal, vertical)
        {
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }

        /// <summary>ミリメートル指定で作る。</summary>
        public static Thickness FromMm(double left, double top, double right, double bottom)
            => new Thickness(Unit.Mm(left), Unit.Mm(top), Unit.Mm(right), Unit.Mm(bottom));

        public static Thickness FromMm(double all) => new Thickness(Unit.Mm(all));

        public Thickness Clone() => new Thickness(Left, Top, Right, Bottom);
    }

    /// <summary>水平方向の揃え。</summary>
    public enum TextAlignment
    {
        Left,
        Center,
        Right,

        /// <summary>両端揃え（最終行は左揃え）。</summary>
        Justify,

        /// <summary>均等割り付け（最終行も含めて両端に揃える）。</summary>
        Distribute,
    }

    /// <summary>垂直方向の揃え。</summary>
    public enum VerticalAlignment
    {
        Top,
        Middle,
        Bottom,
    }

    /// <summary>ブロック（画像・図・表）の水平位置。</summary>
    public enum HorizontalAlignment
    {
        Left,
        Center,
        Right,
    }
}
