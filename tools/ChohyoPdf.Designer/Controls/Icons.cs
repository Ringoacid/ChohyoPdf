using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Controls
{
    using VerticalAlignment = ChohyoPdf.VerticalAlignment;

    /// <summary>アイコンで選ぶ選択肢。</summary>
    public sealed class IconChoice
    {
        public IconChoice(object? value, string toolTip, string geometry)
        {
            Value = value;
            ToolTip = toolTip;
            Geometry = geometry;
        }

        public object? Value { get; }
        public string ToolTip { get; }

        /// <summary>16 × 16 の範囲に描くアイコンの形（パスの書式）。</summary>
        public string Geometry { get; }
    }

    /// <summary>プロパティ欄やメニューで使うアイコン。</summary>
    public static class Icons
    {
        private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x3B, 0x45)));
        private static readonly Brush Light = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAE)));
        private static readonly Brush Highlight = Freeze(new SolidColorBrush(Color.FromRgb(0xBF, 0xDB, 0xF5)));
        private static readonly Brush DeleteHighlight = Freeze(new SolidColorBrush(Color.FromRgb(0xF7, 0xC9, 0xC5)));
        private static readonly Brush Add = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x8E, 0x3E)));
        private static readonly Brush Remove = Freeze(new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38)));

        // 横の揃え（Word の段落の揃えのアイコンのように、行の長さで示す）
        public static IReadOnlyList<IconChoice> Alignments { get; } = new[]
        {
            new IconChoice(TextAlignment.Left, "左揃え", "M1,2h14v1.5h-14z M1,5.5h9v1.5h-9z M1,9h14v1.5h-14z M1,12.5h9v1.5h-9z"),
            new IconChoice(TextAlignment.Center, "中央揃え", "M1,2h14v1.5h-14z M3.5,5.5h9v1.5h-9z M1,9h14v1.5h-14z M3.5,12.5h9v1.5h-9z"),
            new IconChoice(TextAlignment.Right, "右揃え", "M1,2h14v1.5h-14z M6,5.5h9v1.5h-9z M1,9h14v1.5h-14z M6,12.5h9v1.5h-9z"),
            new IconChoice(TextAlignment.Justify, "両端揃え（最後の行は左揃え）", "M1,2h14v1.5h-14z M1,5.5h14v1.5h-14z M1,9h14v1.5h-14z M1,12.5h8v1.5h-8z"),
            new IconChoice(TextAlignment.Distribute, "均等割り付け（文字の間を空けて幅いっぱいに並べる）",
                "M1,2h14v1.5h-14z M1,5.5h14v1.5h-14z M1,9h14v1.5h-14z M1,12.5h2.5v1.5h-2.5z M6.75,12.5h2.5v1.5h-2.5z M12.5,12.5h2.5v1.5h-2.5z"),
        };

        // 縦の揃え（Excel の上揃え・上下中央揃え・下揃えのように、枠の中の行の位置で示す）
        public static IReadOnlyList<IconChoice> VerticalAlignments { get; } = new[]
        {
            new IconChoice(VerticalAlignment.Top, "上揃え", "M1,1h14v1.5h-14z M4,4.5h8v1.5h-8z M4,7.5h8v1.5h-8z M1,14.2h14v0.8h-14z"),
            new IconChoice(VerticalAlignment.Middle, "上下中央揃え", "M1,1h14v0.8h-14z M4,5.5h8v1.5h-8z M4,9h8v1.5h-8z M1,14.2h14v0.8h-14z"),
            new IconChoice(VerticalAlignment.Bottom, "下揃え", "M1,1h14v0.8h-14z M4,7h8v1.5h-8z M4,10h8v1.5h-8z M1,13.5h14v1.5h-14z"),
        };

        /// <summary>選択肢のアイコン。</summary>
        public static FrameworkElement FromGeometry(string geometry)
            => new Path { Data = Geometry.Parse(geometry), Fill = Ink, Width = 16, Height = 16, SnapsToDevicePixels = true };

        /// <summary>
        /// 表の操作のアイコン（20 × 20）。表の形の中で、操作する行・列に色を付け、挿入は「＋」、削除は「×」を添える。
        /// </summary>
        public static FrameworkElement TableOperation(string operation)
        {
            var canvas = new Canvas { Width = 20, Height = 20, SnapsToDevicePixels = true };
            switch (operation)
            {
                case "InsertColumnLeft":
                    Grid(canvas, 7, 2, 12, 16, 2, 3, highlight: (0, -1, Highlight));
                    Plus(canvas, 0.5, 6.5);
                    break;
                case "InsertColumnRight":
                    Grid(canvas, 1, 2, 12, 16, 2, 3, highlight: (1, -1, Highlight));
                    Plus(canvas, 13.5, 6.5);
                    break;
                case "InsertRowAbove":
                    Grid(canvas, 2, 7, 16, 12, 3, 2, highlight: (-1, 0, Highlight));
                    Plus(canvas, 6.5, 0.5);
                    break;
                case "InsertRowBelow":
                    Grid(canvas, 2, 1, 16, 12, 3, 2, highlight: (-1, 1, Highlight));
                    Plus(canvas, 6.5, 13.5);
                    break;
                case "DeleteColumn":
                    Grid(canvas, 1, 1, 15, 15, 3, 3, highlight: (1, -1, DeleteHighlight));
                    Cross(canvas, 12.5, 12.5);
                    break;
                case "DeleteRow":
                    Grid(canvas, 1, 1, 15, 15, 3, 3, highlight: (-1, 1, DeleteHighlight));
                    Cross(canvas, 12.5, 12.5);
                    break;
                case "Merge":
                    // 2 × 2 のセルを 1 つにまとめる（中の区切りを消した大きなセル）
                    Box(canvas, 1, 3, 18, 14, Highlight, Ink);
                    Line(canvas, 4, 10, 8, 10, Ink, 1.2);
                    Line(canvas, 12, 10, 16, 10, Ink, 1.2);
                    Line(canvas, 6, 8, 8, 10, Ink, 1.2);
                    Line(canvas, 6, 12, 8, 10, Ink, 1.2);
                    Line(canvas, 14, 8, 12, 10, Ink, 1.2);
                    Line(canvas, 14, 12, 12, 10, Ink, 1.2);
                    break;
                case "Unmerge":
                    Grid(canvas, 1, 3, 18, 14, 2, 2, highlight: null);
                    Line(canvas, 8, 10, 4, 10, Ink, 1.2);
                    Line(canvas, 6, 8, 4, 10, Ink, 1.2);
                    Line(canvas, 6, 12, 4, 10, Ink, 1.2);
                    Line(canvas, 12, 10, 16, 10, Ink, 1.2);
                    Line(canvas, 14, 8, 16, 10, Ink, 1.2);
                    Line(canvas, 14, 12, 16, 10, Ink, 1.2);
                    break;
                case "DetailTable":
                    // 見出しの行と、くり返し行
                    Box(canvas, 1, 2, 18, 4, Highlight, Ink);
                    Box(canvas, 1, 6, 18, 12, null, Ink);
                    for (int i = 1; i < 4; i++) Line(canvas, 1, 6 + i * 3, 19, 6 + i * 3, Light, 1);
                    Line(canvas, 7, 2, 7, 18, Ink, 1);
                    break;
            }
            return canvas;
        }

        /// <summary>行・列の区切りのある表。highlight は色を付ける列（または行）の番号（-1 は使わない）。</summary>
        private static void Grid(Canvas canvas, double x, double y, double w, double h, int columns, int rows, (int Column, int Row, Brush Brush)? highlight)
        {
            if (highlight is var (column, row, brush))
            {
                if (column >= 0) Box(canvas, x + w / columns * column, y, w / columns, h, brush, null);
                if (row >= 0) Box(canvas, x, y + h / rows * row, w, h / rows, brush, null);
            }
            for (int c = 1; c < columns; c++) Line(canvas, x + w / columns * c, y, x + w / columns * c, y + h, Light, 1);
            for (int r = 1; r < rows; r++) Line(canvas, x, y + h / rows * r, x + w, y + h / rows * r, Light, 1);
            Box(canvas, x, y, w, h, null, Ink);
        }

        private static void Box(Canvas canvas, double x, double y, double w, double h, Brush? fill, Brush? stroke)
        {
            var rect = new Rectangle { Width = w, Height = h, Fill = fill, Stroke = stroke, StrokeThickness = 1 };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }

        private static void Line(Canvas canvas, double x1, double y1, double x2, double y2, Brush brush, double thickness)
            => canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thickness });

        private static void Plus(Canvas canvas, double x, double y)
        {
            Line(canvas, x + 3, y, x + 3, y + 6, Add, 2);
            Line(canvas, x, y + 3, x + 6, y + 3, Add, 2);
        }

        private static void Cross(Canvas canvas, double x, double y)
        {
            Line(canvas, x, y, x + 6, y + 6, Remove, 2);
            Line(canvas, x + 6, y, x, y + 6, Remove, 2);
        }

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }

    /// <summary>表の操作のアイコン（XAML から使う）。</summary>
    public sealed class TableOperationIcon : ContentControl
    {
        public static readonly DependencyProperty OperationProperty = DependencyProperty.Register(
            nameof(Operation), typeof(string), typeof(TableOperationIcon),
            new PropertyMetadata(null, (d, e) => ((TableOperationIcon)d).Content = e.NewValue is string op ? Icons.TableOperation(op) : null));

        public TableOperationIcon()
        {
            Focusable = false;
            IsTabStop = false;
        }

        public string? Operation
        {
            get => (string?)GetValue(OperationProperty);
            set => SetValue(OperationProperty, value);
        }
    }
}
