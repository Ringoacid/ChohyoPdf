using System;
using System.Collections.Generic;
using System.Globalization;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>
    /// 部品の設定から、ChohyoPdf のペン・文字スタイルなどを作る。
    /// 同じ規則で C# の式も作り、PDF 出力と生成コードの結果を一致させる（値を作るメソッドと
    /// 式を作るメソッドは隣に並べてあるので、片方を変えたらもう片方も合わせること）。
    /// </summary>
    internal static class Styles
    {
        // ---- 色 ----

        public static PdfColor Color(string hex) => PdfColor.FromHex(hex);

        public static string ColorCode(string hex) => "PdfColor.FromHex(" + Code.String(hex.Trim()) + ")";

        /// <summary>文字色が既定（黒）か。黒なら TextStyle.Color を設定しない。</summary>
        public static bool IsDefaultTextColor(string hex)
        {
            string h = hex.Trim().TrimStart('#').ToUpperInvariant();
            return h == "000" || h == "000000" || h == "000000FF";
        }

        // ---- ペン ----

        public static PdfPen? Pen(string? color, double width, DashStyle dash)
        {
            if (color == null || width <= 0) return null;
            var c = Color(color);
            return dash switch
            {
                DashStyle.Dash => PdfPen.Dashed(c, width),
                DashStyle.Dot => PdfPen.Dotted(c, width),
                _ => new PdfPen(c, width),
            };
        }

        public static string? PenCode(string? color, double width, DashStyle dash)
        {
            if (color == null || width <= 0) return null;
            string c = ColorCode(color);
            return dash switch
            {
                DashStyle.Dash => "PdfPen.Dashed(" + c + ", " + Code.Number(width) + ")",
                DashStyle.Dot => "PdfPen.Dotted(" + c + ", " + Code.Number(width) + ")",
                _ => "new PdfPen(" + c + ", " + Code.Number(width) + ")",
            };
        }

        // ---- テキスト ----

        /// <summary>テキストを描く矩形（ポイント）。内側の余白を引くと大きさがなくなる場合は null。</summary>
        public static PdfRect? TextBoxRect(TextElement e)
        {
            if (e.Padding <= 0) return new PdfRect(Unit.Mm(e.X), Unit.Mm(e.Y), Unit.Mm(e.Width), Unit.Mm(e.Height));
            double w = e.Width - e.Padding * 2, h = e.Height - e.Padding * 2;
            if (w <= 0 || h <= 0) return null;
            return new PdfRect(Unit.Mm(e.X + e.Padding), Unit.Mm(e.Y + e.Padding), Unit.Mm(w), Unit.Mm(h));
        }

        public static string? TextBoxRectCode(TextElement e)
        {
            if (e.Padding <= 0) return Code.MmRect(e.X, e.Y, e.Width, e.Height);
            double w = e.Width - e.Padding * 2, h = e.Height - e.Padding * 2;
            if (w <= 0 || h <= 0) return null;
            return Code.MmRect(e.X + e.Padding, e.Y + e.Padding, w, h);
        }

        public static TextStyle TextStyle(TextElement e, PdfFont font)
        {
            var style = new TextStyle(font, e.FontSize);
            if (!IsDefaultTextColor(e.Color)) style.Color = Color(e.Color);
            if (e.Bold) style.Bold = true;
            if (e.Italic) style.Italic = true;
            if (e.Underline) style.Underline = true;
            if (e.Strikeout) style.Strikeout = true;
            if (e.CharacterSpacing != 0) style.CharacterSpacing = e.CharacterSpacing;
            return style;
        }

        public static string TextStyleCode(TextElement e, string fontExpr)
        {
            var init = new List<string>();
            if (!IsDefaultTextColor(e.Color)) init.Add("Color = " + ColorCode(e.Color));
            if (e.Bold) init.Add("Bold = true");
            if (e.Italic) init.Add("Italic = true");
            if (e.Underline) init.Add("Underline = true");
            if (e.Strikeout) init.Add("Strikeout = true");
            if (e.CharacterSpacing != 0) init.Add("CharacterSpacing = " + Code.Number(e.CharacterSpacing));
            return "new TextStyle(" + fontExpr + ", " + Code.Number(e.FontSize) + ")" + Code.Initializer(init);
        }

        public static TextBoxOptions TextBoxOptions(TextElement e) => new TextBoxOptions
        {
            Alignment = e.Alignment,
            VerticalAlignment = e.VerticalAlignment,
            LineSpacing = e.LineSpacing,
            ShrinkToFit = e.ShrinkToFit,
            Wrap = e.Wrap,
        };

        public static string TextBoxOptionsCode(TextElement e)
        {
            var init = new List<string>();
            if (e.Alignment != TextAlignment.Left) init.Add("Alignment = TextAlignment." + e.Alignment);
            if (e.VerticalAlignment != VerticalAlignment.Top) init.Add("VerticalAlignment = VerticalAlignment." + e.VerticalAlignment);
            if (e.LineSpacing != 1.2) init.Add("LineSpacing = " + Code.Number(e.LineSpacing));
            if (e.ShrinkToFit) init.Add("ShrinkToFit = true");
            if (!e.Wrap) init.Add("Wrap = false");
            return "new TextBoxOptions" + (init.Count == 0 ? "()" : Code.Initializer(init));
        }

        // ---- 表のセル ----

        public static TextStyle CellStyle(TableElement t, TableCell cell, PdfFont font)
        {
            var style = new TextStyle(font, cell.FontSize ?? t.FontSize);
            string color = cell.Color ?? t.Color;
            if (!IsDefaultTextColor(color)) style.Color = Color(color);
            if (cell.Bold == true) style.Bold = true;
            return style;
        }

        public static string CellStyleCode(TableElement t, TableCell cell, string fontExpr)
        {
            var init = new List<string>();
            string color = cell.Color ?? t.Color;
            if (!IsDefaultTextColor(color)) init.Add("Color = " + ColorCode(color));
            if (cell.Bold == true) init.Add("Bold = true");
            return "new TextStyle(" + fontExpr + ", " + Code.Number(cell.FontSize ?? t.FontSize) + ")" + Code.Initializer(init);
        }

        public static TextBoxOptions CellOptions(TableElement t, TableCell cell, int column) => new TextBoxOptions
        {
            Alignment = cell.Alignment ?? t.Columns[column].Alignment,
            VerticalAlignment = cell.VerticalAlignment ?? t.VerticalAlignment,
            ShrinkToFit = t.ShrinkToFit,
            Wrap = t.Wrap,
        };

        public static string CellOptionsCode(TableElement t, TableCell cell, int column)
        {
            var init = new List<string>();
            var alignment = cell.Alignment ?? t.Columns[column].Alignment;
            var vertical = cell.VerticalAlignment ?? t.VerticalAlignment;
            if (alignment != TextAlignment.Left) init.Add("Alignment = TextAlignment." + alignment);
            if (vertical != VerticalAlignment.Top) init.Add("VerticalAlignment = VerticalAlignment." + vertical);
            if (t.ShrinkToFit) init.Add("ShrinkToFit = true");
            if (!t.Wrap) init.Add("Wrap = false");
            return "new TextBoxOptions" + (init.Count == 0 ? "()" : Code.Initializer(init));
        }
    }

    /// <summary>C# のコード片を作る。</summary>
    internal static class Code
    {
        /// <summary>double のリテラル。整数なら小数点を付けない（元の値と同じ double になる表記）。</summary>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("数値が不正です: " + value);
            if (value == Math.Floor(value) && Math.Abs(value) < 1e15) return ((long)value).ToString(CultureInfo.InvariantCulture);
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string Mm(double mm) => "Unit.Mm(" + Number(mm) + ")";

        public static string MmRect(double x, double y, double w, double h)
            => "new PdfRect(" + Mm(x) + ", " + Mm(y) + ", " + Mm(w) + ", " + Mm(h) + ")";

        public static string Initializer(List<string> items) => items.Count == 0 ? "" : " { " + string.Join(", ", items) + " }";

        /// <summary>文字列リテラル。</summary>
        public static string String(string value)
        {
            var sb = new System.Text.StringBuilder("\"");
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        // 制御文字と、改行として扱われる文字（U+0085, U+2028, U+2029）はエスケープする
                        if (char.IsControl(ch) || ch == (char)0x2028 || ch == (char)0x2029)
                            sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
