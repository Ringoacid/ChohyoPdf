using System;
using System.Collections.Generic;
using System.Text;
using ChohyoPdf.Internal;
using ChohyoPdf.Text;

namespace ChohyoPdf
{
    /// <summary>テキストボックス描画のオプション。</summary>
    public sealed class TextBoxOptions
    {
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        public LineSpacingRule LineSpacingRule { get; set; } = LineSpacingRule.Multiple;

        /// <summary>行送り（<see cref="LineSpacingRule"/> が Multiple なら文字サイズの倍数、それ以外はポイント）。</summary>
        public double LineSpacing { get; set; } = 1.2;

        /// <summary>収まらないとき文字サイズを縮小して全体を表示する（Excel の「縮小して全体を表示」に相当）。</summary>
        public bool ShrinkToFit { get; set; }

        /// <summary>縮小時の最小文字サイズ。</summary>
        public double MinFontSize { get; set; } = 4;

        /// <summary>折り返すか。false なら改行文字でのみ改行する。</summary>
        public bool Wrap { get; set; } = true;

        /// <summary>はみ出した行も描画するか（false なら矩形に収まる行だけを描画する）。</summary>
        public bool DrawOverflow { get; set; }
    }

    /// <summary>テキストボックス描画の結果。</summary>
    public sealed class TextBoxResult
    {
        /// <summary>描画したテキストの高さ。</summary>
        public double Height { get; internal set; }

        /// <summary>描画した行数。</summary>
        public int LinesDrawn { get; internal set; }

        /// <summary>全体の行数。</summary>
        public int TotalLines { get; internal set; }

        /// <summary>収まりきらなかったか。</summary>
        public bool Overflowed { get; internal set; }

        /// <summary>実際に使った文字サイズ（縮小した場合は縮小後の値）。</summary>
        public double FontSize { get; internal set; }
    }

    /// <summary>
    /// ページへの描画 API。座標は左上原点、y は下向き、単位はポイント。
    /// </summary>
    public sealed class PdfGraphics
    {
        private struct Matrix
        {
            public double A, B, C, D, E, F;

            public static Matrix Identity => new Matrix { A = 1, D = 1 };

            public Matrix Multiply(Matrix m)
            {
                // PDF の cm と同じく、新しい変換 m を現在の行列の前に掛ける: CTM' = m × CTM
                return new Matrix
                {
                    A = m.A * A + m.B * C,
                    B = m.A * B + m.B * D,
                    C = m.C * A + m.D * C,
                    D = m.C * B + m.D * D,
                    E = m.E * A + m.F * C + E,
                    F = m.E * B + m.F * D + F,
                };
            }

            public void Transform(double x, double y, out double tx, out double ty)
            {
                tx = A * x + C * y + E;
                ty = B * x + D * y + F;
            }
        }

        private readonly PdfPage _page;
        private readonly StringBuilder _content = new StringBuilder();
        private readonly Stack<Matrix> _stack = new Stack<Matrix>();
        private Matrix _ctm = Matrix.Identity;

        internal PdfGraphics(PdfPage page)
        {
            _page = page;
        }

        public PdfPage Page => _page;

        public double PageWidth => _page.Width;

        public double PageHeight => _page.Height;

        internal string GetContent()
        {
            var sb = new StringBuilder(_content.Length + 16);
            sb.Append(_content);
            // 対応しない SaveState が残っていても閉じる
            for (int i = 0; i < _stack.Count; i++) sb.Append("Q\n");
            return sb.ToString();
        }

        private void Op(string s) => _content.Append(s).Append('\n');

        private static string N(double v) => PdfFormat.Num(v);

        // ---- 状態・座標変換 ----

        /// <summary>グラフィックス状態（座標変換・クリップなど）を保存する。</summary>
        public void SaveState()
        {
            _stack.Push(_ctm);
            Op("q");
        }

        /// <summary>直前の <see cref="SaveState"/> の状態に戻す。</summary>
        public void RestoreState()
        {
            if (_stack.Count == 0) throw new InvalidOperationException("SaveState と RestoreState の対応が取れていません。");
            _ctm = _stack.Pop();
            Op("Q");
        }

        /// <summary>原点を移動する。</summary>
        public void Translate(double dx, double dy) => Transform(1, 0, 0, 1, dx, dy);

        /// <summary>拡大縮小する。</summary>
        public void Scale(double sx, double sy) => Transform(sx, 0, 0, sy, 0, 0);

        /// <summary>原点を中心に回転する（度単位、時計回りが正）。</summary>
        public void Rotate(double degrees)
        {
            double r = degrees * Math.PI / 180;
            double cos = Math.Cos(r), sin = Math.Sin(r);
            Transform(cos, sin, -sin, cos, 0, 0);
        }

        /// <summary>指定点を中心に回転する（度単位、時計回りが正）。</summary>
        public void RotateAt(double degrees, double cx, double cy)
        {
            Translate(cx, cy);
            Rotate(degrees);
            Translate(-cx, -cy);
        }

        /// <summary>任意の座標変換行列を掛ける。</summary>
        public void Transform(double a, double b, double c, double d, double e, double f)
        {
            _ctm = _ctm.Multiply(new Matrix { A = a, B = b, C = c, D = d, E = e, F = f });
            Op(N(a) + " " + N(b) + " " + N(c) + " " + N(d) + " " + N(e) + " " + N(f) + " cm");
        }

        /// <summary>以降の描画を矩形内に制限する（<see cref="RestoreState"/> で解除）。</summary>
        public void SetClip(double x, double y, double width, double height)
        {
            Op(N(x) + " " + N(y) + " " + N(width) + " " + N(height) + " re W n");
        }

        /// <summary>以降の描画をパスの内側に制限する（<see cref="RestoreState"/> で解除）。</summary>
        public void SetClip(PdfPath path)
        {
            _content.Append(path.Operators);
            Op("W n");
        }

        // ---- 図形 ----

        private void BeginPaint(PdfPen? pen, PdfColor? fill)
        {
            Op("q");
            double fa = fill?.Alpha ?? 1, sa = pen?.Color.Alpha ?? 1;
            if (fa < 1 || sa < 1) SetAlpha(fa, sa);
            if (fill.HasValue) Op(fill.Value.FillOperator());
            if (pen != null)
            {
                Op(pen.Color.StrokeOperator());
                Op(N(pen.Width) + " w");
                if (pen.LineCap != LineCap.Butt) Op((int)pen.LineCap + " J");
                if (pen.LineJoin != LineJoin.Miter) Op((int)pen.LineJoin + " j");
                if (pen.DashPattern != null && pen.DashPattern.Length > 0)
                {
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < pen.DashPattern.Length; i++)
                    {
                        if (i > 0) sb.Append(' ');
                        sb.Append(N(pen.DashPattern[i]));
                    }
                    sb.Append("] ").Append(N(pen.DashPhase)).Append(" d");
                    Op(sb.ToString());
                }
            }
        }

        private void EndPaint(PdfPen? pen, PdfColor? fill)
        {
            bool doFill = fill.HasValue && fill.Value.Alpha > 0;
            bool doStroke = pen != null && pen.Width >= 0 && pen.Color.Alpha > 0;
            Op(doFill && doStroke ? "B" : doFill ? "f" : doStroke ? "S" : "n");
            Op("Q");
        }

        private void SetAlpha(double fillAlpha, double strokeAlpha)
        {
            string name = _page.Document.GetAlphaState(fillAlpha, strokeAlpha, out string dict);
            _page.UsedStates[name] = dict;
            Op("/" + name + " gs");
        }

        /// <summary>直線を引く。</summary>
        public void DrawLine(PdfPen pen, double x1, double y1, double x2, double y2)
        {
            if (pen == null) throw new ArgumentNullException(nameof(pen));
            BeginPaint(pen, null);
            Op(N(x1) + " " + N(y1) + " m " + N(x2) + " " + N(y2) + " l");
            EndPaint(pen, null);
        }

        /// <summary>折れ線を引く。座標は x0, y0, x1, y1, ... の順。</summary>
        public void DrawPolyline(PdfPen pen, params double[] xy)
        {
            DrawPath(new PdfPath().AddPolygon(xy, close: false), pen, null);
        }

        /// <summary>多角形を描く。</summary>
        public void DrawPolygon(PdfPen? pen, PdfColor? fill, params double[] xy)
        {
            DrawPath(new PdfPath().AddPolygon(xy, close: true), pen, fill);
        }

        /// <summary>矩形を描く。<paramref name="pen"/> と <paramref name="fill"/> はどちらか省略可能。</summary>
        public void DrawRectangle(PdfPen? pen, PdfColor? fill, double x, double y, double width, double height)
        {
            BeginPaint(pen, fill);
            Op(N(x) + " " + N(y) + " " + N(width) + " " + N(height) + " re");
            EndPaint(pen, fill);
        }

        /// <summary>矩形の枠線を描く。</summary>
        public void DrawRectangle(PdfPen pen, double x, double y, double width, double height) => DrawRectangle(pen, null, x, y, width, height);

        /// <summary>矩形を塗りつぶす。</summary>
        public void FillRectangle(PdfColor fill, double x, double y, double width, double height) => DrawRectangle(null, fill, x, y, width, height);

        /// <summary>角丸矩形を描く。</summary>
        public void DrawRoundedRectangle(PdfPen? pen, PdfColor? fill, double x, double y, double width, double height, double radius)
            => DrawPath(new PdfPath().AddRoundedRectangle(x, y, width, height, radius), pen, fill);

        /// <summary>矩形に内接する楕円を描く。</summary>
        public void DrawEllipse(PdfPen? pen, PdfColor? fill, double x, double y, double width, double height)
            => DrawPath(new PdfPath().AddEllipse(x, y, width, height), pen, fill);

        /// <summary>中心と半径で円を描く。</summary>
        public void DrawCircle(PdfPen? pen, PdfColor? fill, double cx, double cy, double radius)
            => DrawEllipse(pen, fill, cx - radius, cy - radius, radius * 2, radius * 2);

        /// <summary>扇形を描く（角度は度単位、0 度が右、時計回りが正）。円グラフなどに使う。</summary>
        public void DrawPie(PdfPen? pen, PdfColor? fill, double cx, double cy, double radius, double startAngle, double sweepAngle)
        {
            var path = new PdfPath().MoveTo(cx, cy).Arc(cx, cy, radius, radius, startAngle, sweepAngle).Close();
            DrawPath(path, pen, fill);
        }

        /// <summary>パスを描く。</summary>
        public void DrawPath(PdfPath path, PdfPen? pen, PdfColor? fill)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.IsEmpty) return;
            BeginPaint(pen, fill);
            _content.Append(path.Operators);
            EndPaint(pen, fill);
        }

        // ---- 画像 ----

        /// <summary>画像を指定の矩形に描画する。</summary>
        public void DrawImage(PdfImage image, double x, double y, double width, double height)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            var res = _page.Document.GetImage(image);
            if (!_page.UsedImages.Contains(res)) _page.UsedImages.Add(res);
            Op("q");
            Op(N(width) + " 0 0 " + N(-height) + " " + N(x) + " " + N(y + height) + " cm");
            Op("/" + res.Name + " Do");
            Op("Q");
        }

        /// <summary>画像を解像度に基づく自然なサイズで描画する。</summary>
        public void DrawImage(PdfImage image, double x, double y) => DrawImage(image, x, y, image.Width, image.Height);

        /// <summary>画像を縦横比を保って矩形内に収まるよう描画する（中央寄せ）。</summary>
        public void DrawImageFit(PdfImage image, double x, double y, double width, double height)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            double scale = Math.Min(width / image.Width, height / image.Height);
            double w = image.Width * scale, h = image.Height * scale;
            DrawImage(image, x + (width - w) / 2, y + (height - h) / 2, w, h);
        }

        // ---- テキスト ----

        /// <summary>
        /// テキストを描画する（折り返しなし。改行文字では改行する）。
        /// <paramref name="y"/> は 1 行目の上端。<paramref name="alignment"/> が Center なら x は中央、Right なら右端の座標。
        /// </summary>
        /// <returns>描画したテキストの幅。</returns>
        public double DrawText(string text, double x, double y, TextStyle style, TextAlignment alignment = TextAlignment.Left)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var block = TextBlock.Build(text, ResolvedStyle.From(style));
            block.Layout(double.MaxValue / 4, 0, LineSpacingRule.Multiple, 1.2);
            double width = block.NaturalWidth();
            double left = alignment == TextAlignment.Center ? x - width / 2 : alignment == TextAlignment.Right ? x - width : x;
            var lineAlign = alignment == TextAlignment.Justify || alignment == TextAlignment.Distribute ? TextAlignment.Left : alignment;
            block.Draw(this, left, y, width, lineAlign, 0, block.Lines.Count);
            return width;
        }

        /// <summary>
        /// 矩形内にテキストを折り返して描画する。禁則処理を行い、必要なら縮小して収める。
        /// </summary>
        public TextBoxResult DrawTextBox(string text, PdfRect rect, TextStyle style, TextBoxOptions? options = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            options = options ?? new TextBoxOptions();
            var baseStyle = ResolvedStyle.From(style);
            double size = baseStyle.Size;
            TextBlock block = BuildBox(text, baseStyle, rect.Width, options);
            if (options.ShrinkToFit)
            {
                while (size > options.MinFontSize && !Fits(block, rect, options))
                {
                    size = Math.Max(options.MinFontSize, size - Math.Max(0.25, size * 0.05));
                    block = BuildBox(text, baseStyle.WithSize(size), rect.Width, options);
                }
            }

            int count = block.Lines.Count;
            double height = 0;
            int fit = 0;
            for (; fit < count; fit++)
            {
                double h = block.Lines[fit].Height;
                if (!options.DrawOverflow && height + h > rect.Height + 1e-6) break;
                height += h;
            }
            double top = rect.Y;
            if (options.VerticalAlignment == VerticalAlignment.Middle) top += (rect.Height - height) / 2;
            else if (options.VerticalAlignment == VerticalAlignment.Bottom) top += rect.Height - height;

            block.Draw(this, rect.X, top, rect.Width, options.Alignment, 0, fit);
            return new TextBoxResult
            {
                Height = height,
                LinesDrawn = fit,
                TotalLines = count,
                Overflowed = fit < count || block.MaxLineWidth > rect.Width + 1e-6,
                FontSize = size,
            };
        }

        private static TextBlock BuildBox(string text, ResolvedStyle style, double width, TextBoxOptions options)
        {
            var block = TextBlock.Build(text, style);
            block.Layout(options.Wrap ? width : double.MaxValue / 4, 0, options.LineSpacingRule, options.LineSpacing);
            return block;
        }

        private static bool Fits(TextBlock block, PdfRect rect, TextBoxOptions options)
        {
            if (block.TotalHeight > rect.Height + 1e-6) return false;
            return block.MaxLineWidth <= rect.Width + 1e-6;
        }

        /// <summary>折り返しなしでテキストの幅を測る。</summary>
        public static double MeasureText(string text, TextStyle style)
        {
            var block = TextBlock.Build(text, ResolvedStyle.From(style));
            return block.NaturalWidth();
        }

        /// <summary>指定幅で折り返したときのテキストの高さを測る。</summary>
        public static double MeasureTextHeight(string text, TextStyle style, double width, double lineSpacing = 1.2, LineSpacingRule rule = LineSpacingRule.Multiple)
        {
            var block = TextBlock.Build(text, ResolvedStyle.From(style));
            block.Layout(width, 0, rule, lineSpacing);
            return block.TotalHeight;
        }

        /// <summary>グリフ列を描画する（内部用）。<paramref name="baseline"/> はベースラインの y 座標。</summary>
        internal void ShowGlyphs(PdfFont font, double size, PdfColor color, bool simBold, bool simItalic, double charSpacing,
            double x, double baseline, ushort[] gids, int[] codepoints)
        {
            var res = _page.Document.GetFont(font);
            if (!_page.UsedFonts.Contains(res)) _page.UsedFonts.Add(res);
            var hex = new StringBuilder(gids.Length * 4 + 2);
            hex.Append('<');
            for (int i = 0; i < gids.Length; i++)
            {
                ushort cid = res.Subset.Map(gids[i], codepoints[i]);
                hex.Append(cid.ToString("X4"));
            }
            hex.Append('>');

            Op("q");
            if (color.Alpha < 1) SetAlpha(color.Alpha, color.Alpha);
            Op("BT");
            Op("/" + res.Name + " " + N(size) + " Tf");
            Op(color.FillOperator());
            if (simBold)
            {
                Op(color.StrokeOperator());
                Op(N(size * 0.03) + " w 2 Tr");
            }
            if (charSpacing != 0) Op(N(charSpacing) + " Tc");
            Op("1 0 " + (simItalic ? "0.2126" : "0") + " -1 " + N(x) + " " + N(baseline) + " Tm");
            Op(hex + " Tj");
            Op("ET");
            Op("Q");
        }

        /// <summary>現在の座標系での矩形にリンクを設定する。</summary>
        public void AddLink(PdfRect rect, string uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in new[] { new[] { rect.X, rect.Y }, new[] { rect.Right, rect.Y }, new[] { rect.X, rect.Bottom }, new[] { rect.Right, rect.Bottom } })
            {
                _ctm.Transform(p[0], p[1], out double tx, out double ty);
                minX = Math.Min(minX, tx);
                minY = Math.Min(minY, ty);
                maxX = Math.Max(maxX, tx);
                maxY = Math.Max(maxY, ty);
            }
            _page.AddLink(new PdfRect(minX, minY, maxX - minX, maxY - minY), uri);
        }
    }
}
