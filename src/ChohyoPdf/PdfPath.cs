using System;
using System.Collections.Generic;
using System.Text;
using ChohyoPdf.Internal;

namespace ChohyoPdf
{
    /// <summary>直線・ベジェ曲線からなるパス（左上原点の座標）。</summary>
    public sealed class PdfPath
    {
        private const double Kappa = 0.5522847498;
        private readonly StringBuilder _ops = new StringBuilder();
        private double _x, _y;
        private bool _hasCurrent;

        /// <summary>現在点を移動して新しいサブパスを始める。</summary>
        public PdfPath MoveTo(double x, double y)
        {
            _ops.Append(PdfFormat.Num(x)).Append(' ').Append(PdfFormat.Num(y)).Append(" m\n");
            _x = x;
            _y = y;
            _hasCurrent = true;
            return this;
        }

        /// <summary>直線を引く。</summary>
        public PdfPath LineTo(double x, double y)
        {
            EnsureCurrent();
            _ops.Append(PdfFormat.Num(x)).Append(' ').Append(PdfFormat.Num(y)).Append(" l\n");
            _x = x;
            _y = y;
            return this;
        }

        /// <summary>3 次ベジェ曲線を引く。</summary>
        public PdfPath CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        {
            EnsureCurrent();
            _ops.Append(PdfFormat.Num(x1)).Append(' ').Append(PdfFormat.Num(y1)).Append(' ')
                .Append(PdfFormat.Num(x2)).Append(' ').Append(PdfFormat.Num(y2)).Append(' ')
                .Append(PdfFormat.Num(x3)).Append(' ').Append(PdfFormat.Num(y3)).Append(" c\n");
            _x = x3;
            _y = y3;
            return this;
        }

        /// <summary>2 次ベジェ曲線を引く。</summary>
        public PdfPath QuadraticTo(double cx, double cy, double x, double y)
        {
            EnsureCurrent();
            double x1 = _x + 2.0 / 3.0 * (cx - _x), y1 = _y + 2.0 / 3.0 * (cy - _y);
            double x2 = x + 2.0 / 3.0 * (cx - x), y2 = y + 2.0 / 3.0 * (cy - y);
            return CurveTo(x1, y1, x2, y2, x, y);
        }

        /// <summary>
        /// 中心 (cx, cy)、半径 rx, ry の楕円弧を追加する。角度は度単位で、0 度が右、時計回りが正。
        /// 現在点がなければ弧の始点から始める。あれば始点まで直線を引く。
        /// </summary>
        public PdfPath Arc(double cx, double cy, double rx, double ry, double startAngle, double sweepAngle)
        {
            double start = startAngle * Math.PI / 180;
            double sweep = sweepAngle * Math.PI / 180;
            int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2)));
            double delta = sweep / segments;
            double sx = cx + rx * Math.Cos(start), sy = cy + ry * Math.Sin(start);
            if (_hasCurrent) LineTo(sx, sy);
            else MoveTo(sx, sy);
            double k = 4.0 / 3.0 * Math.Tan(delta / 4);
            double a = start;
            for (int i = 0; i < segments; i++)
            {
                double b = a + delta;
                double cosA = Math.Cos(a), sinA = Math.Sin(a), cosB = Math.Cos(b), sinB = Math.Sin(b);
                CurveTo(
                    cx + rx * (cosA - k * sinA), cy + ry * (sinA + k * cosA),
                    cx + rx * (cosB + k * sinB), cy + ry * (sinB - k * cosB),
                    cx + rx * cosB, cy + ry * sinB);
                a = b;
            }
            return this;
        }

        /// <summary>現在のサブパスを閉じる。</summary>
        public PdfPath Close()
        {
            _ops.Append("h\n");
            return this;
        }

        /// <summary>矩形のサブパスを追加する。</summary>
        public PdfPath AddRectangle(double x, double y, double width, double height)
        {
            _ops.Append(PdfFormat.Num(x)).Append(' ').Append(PdfFormat.Num(y)).Append(' ')
                .Append(PdfFormat.Num(width)).Append(' ').Append(PdfFormat.Num(height)).Append(" re\n");
            _x = x;
            _y = y;
            _hasCurrent = true;
            return this;
        }

        /// <summary>角丸矩形のサブパスを追加する。</summary>
        public PdfPath AddRoundedRectangle(double x, double y, double width, double height, double radius)
        {
            double r = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            if (r <= 0) return AddRectangle(x, y, width, height);
            double k = r * Kappa;
            MoveTo(x + r, y);
            LineTo(x + width - r, y);
            CurveTo(x + width - r + k, y, x + width, y + r - k, x + width, y + r);
            LineTo(x + width, y + height - r);
            CurveTo(x + width, y + height - r + k, x + width - r + k, y + height, x + width - r, y + height);
            LineTo(x + r, y + height);
            CurveTo(x + r - k, y + height, x, y + height - r + k, x, y + height - r);
            LineTo(x, y + r);
            CurveTo(x, y + r - k, x + r - k, y, x + r, y);
            return Close();
        }

        /// <summary>矩形に内接する楕円のサブパスを追加する。</summary>
        public PdfPath AddEllipse(double x, double y, double width, double height)
        {
            double rx = width / 2, ry = height / 2;
            double cx = x + rx, cy = y + ry;
            double kx = rx * Kappa, ky = ry * Kappa;
            MoveTo(cx + rx, cy);
            CurveTo(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry);
            CurveTo(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy);
            CurveTo(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry);
            CurveTo(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy);
            return Close();
        }

        /// <summary>折れ線（閉じるなら多角形）のサブパスを追加する。</summary>
        public PdfPath AddPolygon(IList<double> xy, bool close = true)
        {
            if (xy == null || xy.Count < 4 || xy.Count % 2 != 0) throw new ArgumentException("座標は x, y の組で 2 点以上指定してください。", nameof(xy));
            MoveTo(xy[0], xy[1]);
            for (int i = 2; i < xy.Count; i += 2) LineTo(xy[i], xy[i + 1]);
            return close ? Close() : this;
        }

        internal bool IsEmpty => _ops.Length == 0;

        internal string Operators => _ops.ToString();

        private void EnsureCurrent()
        {
            if (!_hasCurrent) throw new InvalidOperationException("MoveTo で始点を指定してください。");
        }
    }
}
