using System.Linq;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    /// <summary>部品を動かしている間に、前に描いた画像を今の位置・大きさに合わせる計算。</summary>
    public class MovePreviewTests
    {
        [Fact]
        public void Text_DecorationStretchesButTextOnlyMoves()
        {
            var from = new TextElement { X = 10, Y = 20, Width = 40, Height = 10, Alignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Middle };
            var to = new TextElement { X = 10, Y = 20, Width = 60, Height = 30, Alignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Middle };
            var parts = MovePreviewGeometry.Parts(from, to);

            var decoration = parts.Single(p => p.Layer == PreviewLayer.Decoration).Map;
            Assert.Equal(1.5, decoration.ScaleX, 6);
            Assert.Equal(3, decoration.ScaleY, 6);
            Assert.Equal(70, decoration.MapX(50), 6);

            // 文字は伸び縮みさせず、右端・上下の中央をそろえる
            var content = parts.Single(p => p.Layer == PreviewLayer.Content);
            Assert.Equal(1, content.Map.ScaleX);
            Assert.Equal(1, content.Map.ScaleY);
            Assert.Equal(70, content.Map.MapX(50), 6);
            Assert.Equal(35, content.Map.MapY(25), 6);
            Assert.Equal(to.Bounds, content.Clip);
        }

        [Fact]
        public void Text_WithoutWrapIsNotClipped()
        {
            var from = new TextElement { X = 0, Y = 0, Width = 40, Height = 10, Wrap = false };
            var to = new TextElement { X = 5, Y = 0, Width = 20, Height = 10, Wrap = false };
            var content = MovePreviewGeometry.Parts(from, to).Single(p => p.Layer == PreviewLayer.Content);
            Assert.Null(content.Clip);
            Assert.Equal(5, content.Map.OffsetX, 6);
        }

        [Fact]
        public void Move_IsTranslationForEveryKind()
        {
            var table = TableElement.Create(2, 2);
            table.Rows[0].Cells[0].Text = "A";
            var moved = ReportSerializer.CloneElement(table);
            moved.X += 7;
            moved.Y += 3;
            foreach (var part in MovePreviewGeometry.Parts(table, moved))
            {
                Assert.Equal(1, part.Map.ScaleX, 6);
                Assert.Equal(1, part.Map.ScaleY, 6);
                Assert.Equal(7, part.Map.OffsetX, 6);
                Assert.Equal(3, part.Map.OffsetY, 6);
            }
        }

        [Fact]
        public void Table_TextFollowsEachCell()
        {
            var from = TableElement.Create(2, 1, 20, 10);
            from.Rows[0].Cells[1].Text = "右";
            from.Columns[1].Alignment = TextAlignment.Right;
            var to = ReportSerializer.CloneElement(from);
            to.Width = 80;

            var parts = MovePreviewGeometry.Parts(from, to);
            Assert.Equal(2, parts[0].Map.ScaleX, 6);
            // 文字のあるセルだけ。2 列目の右端（余白 1 mm の内側）を合わせる
            var cell = parts.Single(p => p.Layer == PreviewLayer.Content);
            Assert.Equal(1, cell.Map.ScaleX);
            Assert.Equal(79, cell.Map.MapX(39), 6);
            Assert.Equal(new Rect(40, 0, 40, 10), cell.Clip);
        }

        [Fact]
        public void UniformImage_KeepsAspectRatio()
        {
            var from = new ImageElement { X = 0, Y = 0, Width = 40, Height = 40, Fit = ImageFit.Uniform };
            var to = new ImageElement { X = 0, Y = 0, Width = 80, Height = 40, Fit = ImageFit.Uniform };
            // 縦横比 2:1 の画像は、40 × 20 から 80 × 40 になる
            var map = MovePreviewGeometry.Parts(from, to, 2).Single().Map;
            Assert.Equal(2, map.ScaleX, 6);
            Assert.Equal(2, map.ScaleY, 6);
            Assert.Equal(0, map.MapY(10), 6);

            // 引き伸ばす画像と、縦横比が分からない画像は、枠に合わせる
            from.Fit = to.Fit = ImageFit.Stretch;
            var stretch = MovePreviewGeometry.Parts(from, to, 2).Single().Map;
            Assert.Equal(2, stretch.ScaleX, 6);
            Assert.Equal(1, stretch.ScaleY, 6);
        }

        [Fact]
        public void Line_MapsEndpoints()
        {
            var from = new LineElement { X1 = 0, Y1 = 0, X2 = 10, Y2 = 0 };
            var to = new LineElement { X1 = 0, Y1 = 5, X2 = 30, Y2 = 5 };
            var map = MovePreviewGeometry.Parts(from, to).Single().Map;
            Assert.Equal(30, map.MapX(10), 6);
            Assert.Equal(5, map.MapY(0), 6);
        }

        [Fact]
        public void DrawnArea_CoversStrokeAndOverflowingText()
        {
            // 太さ 7.2 pt（2.54 mm）の線の図形は、線の太さ + 1 mm だけ広げる
            var shape = new RectangleElement { X = 50, Y = 50, Width = 20, Height = 10, StrokeWidth = 7.2 };
            var area = MovePreviewGeometry.DrawnArea(new[] { shape }, 210, 297)!.Value;
            Assert.Equal(50 - 3.54, area.X, 6);
            Assert.Equal(70 + 3.54, area.Right, 6);

            // 文字は、枠に収まらない 1 行分だけ上下に広げる。折り返さなければ用紙の左右の端まで広げる
            var text = new TextElement { X = 50, Y = 50, Width = 20, Height = 2, FontSize = 72, LineSpacing = 1, BorderWidth = 0, Wrap = false };
            area = MovePreviewGeometry.DrawnArea(new[] { text }, 210, 297)!.Value;
            Assert.Equal(0, area.X);
            Assert.Equal(210, area.Right);
            Assert.Equal(52 + 1 + 25.4, area.Bottom, 6);

            // 用紙の外は含めない。用紙の外にしかなければ null
            var edge = new RectangleElement { X = -10, Y = 290, Width = 30, Height = 20 };
            area = MovePreviewGeometry.DrawnArea(new[] { edge }, 210, 297)!.Value;
            Assert.Equal(0, area.X);
            Assert.Equal(297, area.Bottom);
            Assert.Null(MovePreviewGeometry.DrawnArea(new[] { new RectangleElement { X = 300, Y = 0, Width = 10, Height = 10 } }, 210, 297));
        }
    }
}
