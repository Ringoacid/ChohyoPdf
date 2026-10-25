using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChohyoPdf.Images;
using Xunit;

namespace ChohyoPdf.Tests
{
    /// <summary>JPEG の EXIF の向き（Orientation）に関するテスト。</summary>
    public class ImageOrientationTests
    {
        // rgb.jpg は 37 x 23 ピクセル、150 dpi
        private const int StoredWidth = 37;
        private const int StoredHeight = 23;

        private sealed class Entry
        {
            public int Tag;
            public int Type;
            public uint Count;
            public int Value;
        }

        private static void PutU16(List<byte> b, int v, bool little)
        {
            if (little)
            {
                b.Add((byte)(v & 0xFF));
                b.Add((byte)(v >> 8));
            }
            else
            {
                b.Add((byte)(v >> 8));
                b.Add((byte)(v & 0xFF));
            }
        }

        private static void PutU32(List<byte> b, uint v, bool little)
        {
            var bytes = new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
            if (little) Array.Reverse(bytes);
            b.AddRange(bytes);
        }

        /// <summary>APP1（Exif）セグメント（マーカーと長さを含む）。IFD0 に <paramref name="entries"/> を並べる。</summary>
        private static byte[] ExifSegment(bool little, params Entry[] entries)
        {
            var body = new List<byte>();
            body.AddRange(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 });
            body.AddRange(new[] { (byte)(little ? 'I' : 'M'), (byte)(little ? 'I' : 'M') });
            PutU16(body, 0x2A, little);
            PutU32(body, 8, little);
            PutU16(body, entries.Length, little);
            foreach (var e in entries)
            {
                PutU16(body, e.Tag, little);
                PutU16(body, e.Type, little);
                PutU32(body, e.Count, little);
                if (e.Type == 3)
                {
                    PutU16(body, e.Value, little);
                    PutU16(body, 0, little);
                }
                else
                {
                    PutU32(body, (uint)e.Value, little);
                }
            }
            PutU32(body, 0, little);        // 次の IFD なし
            return Wrap(body.ToArray());
        }

        private static byte[] Wrap(byte[] body)
        {
            var seg = new List<byte> { 0xFF, 0xE1 };
            int length = body.Length + 2;
            seg.Add((byte)(length >> 8));
            seg.Add((byte)(length & 0xFF));
            seg.AddRange(body);
            return seg.ToArray();
        }

        private static Entry Orientation(int value) => new Entry { Tag = 0x0112, Type = 3, Count = 1, Value = value };

        /// <summary>JPEG の SOI の直後に <paramref name="segment"/> を差し込む。</summary>
        private static byte[] Insert(byte[] jpeg, byte[] segment, int at = 2)
        {
            var result = new List<byte>(jpeg.Take(at));
            result.AddRange(segment);
            result.AddRange(jpeg.Skip(at));
            return result.ToArray();
        }

        private static byte[] WithOrientation(int value, bool little = true, int at = 2)
            => Insert(TestData.Bytes("rgb.jpg"), ExifSegment(little, Orientation(value)), at);

        /// <summary>垂直解像度を 300 dpi にした rgb.jpg（水平は 150 dpi）。向きで解像度が入れ替わるかを調べる。</summary>
        private static byte[] WithVerticalDpi300(byte[] jpeg)
        {
            var copy = (byte[])jpeg.Clone();
            int jfif = FindJfif(copy);
            copy[jfif + 10] = 0x01;
            copy[jfif + 11] = 0x2C;
            return copy;
        }

        private static int FindJfif(byte[] d)
        {
            for (int i = 0; i + 5 < d.Length; i++)
            {
                if (d[i] == 'J' && d[i + 1] == 'F' && d[i + 2] == 'I' && d[i + 3] == 'F' && d[i + 4] == 0) return i;
            }
            throw new InvalidOperationException("JFIF がありません");
        }

        // 元の画像の左上・右上の角が、向きを直した後の矩形のどこ（0〜1）に来るか（EXIF の規格の定義）
        private static readonly Dictionary<int, double[]> Corners = new Dictionary<int, double[]>
        {
            //   左上の (dx, dy)   右上の (dx, dy)
            { 1, new double[] { 0, 0, 1, 0 } },
            { 2, new double[] { 1, 0, 0, 0 } },
            { 3, new double[] { 1, 1, 0, 1 } },
            { 4, new double[] { 0, 1, 1, 1 } },
            { 5, new double[] { 0, 0, 0, 1 } },
            { 6, new double[] { 1, 0, 1, 1 } },
            { 7, new double[] { 1, 1, 1, 0 } },
            { 8, new double[] { 0, 1, 0, 0 } },
        };

        private static List<double[]> Matrices(byte[] pdf)
        {
            string content = string.Join("\n", PdfChecker.DecodedStreams(pdf));
            return Regex.Matches(content, @"(\S+) (\S+) (\S+) (\S+) (\S+) (\S+) cm\n/Im\d+ Do").Cast<Match>()
                .Select(m => Enumerable.Range(1, 6).Select(i => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray()).ToList();
        }

        private static double[] Matrix(PdfImage image, double x, double y, double w, double h)
        {
            var doc = new PdfDocument();
            doc.AddPage().Graphics.DrawImage(image, x, y, w, h);
            var list = Matrices(doc.ToArray());
            Assert.Single(list);
            return list[0];
        }

        // ---- 8 種類の向き ----

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, true)]
        [InlineData(5, true)]
        [InlineData(6, true)]
        [InlineData(7, true)]
        [InlineData(8, true)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(4, false)]
        [InlineData(5, false)]
        [InlineData(6, false)]
        [InlineData(7, false)]
        [InlineData(8, false)]
        public void AllOrientations_SizeAndMatrix(int orientation, bool little)
        {
            byte[] jpeg = WithVerticalDpi300(WithOrientation(orientation, little));
            var image = PdfImage.FromBytes(jpeg);
            bool swap = orientation >= 5;
            Assert.Equal(orientation, image.ExifOrientation);
            Assert.Equal(orientation != 1, image.OrientationApplied);

            // 縦横が入れ替わる向きでは、ピクセル数・解像度・自然な大きさも入れ替わる（水平 150 dpi、垂直 300 dpi）
            Assert.Equal(swap ? StoredHeight : StoredWidth, image.PixelWidth);
            Assert.Equal(swap ? StoredWidth : StoredHeight, image.PixelHeight);
            Assert.Equal(swap ? 300 : 150, image.DpiX);
            Assert.Equal(swap ? 150 : 300, image.DpiY);
            Assert.Equal(swap ? StoredHeight * 72.0 / 300 : StoredWidth * 72.0 / 150, image.Width, 6);
            Assert.Equal(swap ? StoredWidth * 72.0 / 150 : StoredHeight * 72.0 / 300, image.Height, 6);

            // 変換行列: 元の画像の左上・右上の角が、規格どおりの位置に来る
            const double x = 10, y = 20, w = 60, h = 40;
            var m = Matrix(image, x, y, w, h);
            double[] c = Corners[orientation];
            // 画像の XObject の座標は左下が (0, 0)、左上が (0, 1)、右上が (1, 1)
            double tlX = m[2] + m[4], tlY = m[3] + m[5];
            double trX = m[0] + m[2] + m[4], trY = m[1] + m[3] + m[5];
            Assert.Equal(x + w * c[0], tlX, 3);
            Assert.Equal(y + h * c[1], tlY, 3);
            Assert.Equal(x + w * c[2], trX, 3);
            Assert.Equal(y + h * c[3], trY, 3);
            // 左下の角は (e, f)。矩形の外には出ない
            double blX = m[4], blY = m[5];
            Assert.InRange(blX, x - 1e-6, x + w + 1e-6);
            Assert.InRange(blY, y - 1e-6, y + h + 1e-6);
            // 回転・反転だけで、傾きや歪みはない（行列式の絶対値が w * h、軸に平行）
            Assert.Equal(w * h, Math.Abs(m[0] * m[3] - m[1] * m[2]), 3);
            Assert.True((Math.Abs(m[1]) < 1e-9 && Math.Abs(m[2]) < 1e-9) || (Math.Abs(m[0]) < 1e-9 && Math.Abs(m[3]) < 1e-9));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        public void AllOrientations_MatrixHelperMatchesDefinition(int orientation)
        {
            var m = ImageOrientation.GetMatrix(orientation, 10, 20, 60, 40);
            double[] c = Corners[orientation];
            Assert.Equal(10 + 60 * c[0], m[2] + m[4], 6);
            Assert.Equal(20 + 40 * c[1], m[3] + m[5], 6);
            Assert.Equal(10 + 60 * c[2], m[0] + m[2] + m[4], 6);
            Assert.Equal(20 + 40 * c[3], m[1] + m[3] + m[5], 6);
        }

        [Theory]
        [InlineData(6)]
        [InlineData(8)]
        public void Orientation_KeepsPixelDataAndStoredSize(int orientation)
        {
            byte[] jpeg = WithOrientation(orientation);
            var image = PdfImage.FromBytes(jpeg);
            var doc = new PdfDocument();
            doc.AddPage().Graphics.DrawImage(image, 10, 10, 23, 37);
            byte[] pdf = doc.ToArray();
            PdfChecker.AssertValidStructure(pdf);
            string s = PdfChecker.Latin1(pdf);

            // 画素データ（DCTDecode）はそのまま埋め込み、XObject の大きさは保存されている画素の大きさのまま
            Assert.Contains("/Subtype /Image /Width " + StoredWidth + " /Height " + StoredHeight, s);
            Assert.Contains("/Filter /DCTDecode", s);
            Assert.Contains(PdfChecker.Latin1(jpeg), s);
        }

        [Fact]
        public void ExifPositionDoesNotMatter_BeforeOrAfterJfif()
        {
            // JFIF（APP0）の後ろに APP1 がある並び
            var image = PdfImage.FromBytes(WithOrientation(6, true, 20));
            Assert.Equal(6, image.ExifOrientation);
            Assert.Equal(StoredHeight, image.PixelWidth);
            Assert.Equal(150, image.DpiX);
        }

        [Fact]
        public void OrientationTagCanBeAnyEntryOfIfd()
        {
            var segment = ExifSegment(false,
                new Entry { Tag = 0x010F, Type = 2, Count = 4, Value = 0 },
                new Entry { Tag = 0x0110, Type = 2, Count = 4, Value = 0 },
                Orientation(8),
                new Entry { Tag = 0x011A, Type = 5, Count = 1, Value = 0 });
            var image = PdfImage.FromBytes(Insert(TestData.Bytes("rgb.jpg"), segment));
            Assert.Equal(8, image.ExifOrientation);
            Assert.True(image.OrientationApplied);
        }

        [Fact]
        public void OtherJpegKinds_ReadOrientation()
        {
            foreach (string file in new[] { "gray.jpg", "cmyk.jpg", "progressive.jpg" })
            {
                var image = PdfImage.FromBytes(Insert(TestData.Bytes(file), ExifSegment(true, Orientation(6))));
                Assert.Equal(6, image.ExifOrientation);
                Assert.Equal(StoredHeight, image.PixelWidth);
                Assert.Equal(StoredWidth, image.PixelHeight);
            }
        }

        // ---- 無効にする ----

        [Theory]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(8)]
        public void IgnoreOption_KeepsStoredOrientation(int orientation)
        {
            byte[] jpeg = WithOrientation(orientation);
            var image = PdfImage.FromBytes(jpeg, false);
            Assert.Equal(orientation, image.ExifOrientation);      // 記録されている向きは分かる
            Assert.False(image.OrientationApplied);
            Assert.Equal(StoredWidth, image.PixelWidth);
            Assert.Equal(StoredHeight, image.PixelHeight);
            Assert.Equal(StoredWidth * 72.0 / 150, image.Width, 6);

            // 描画は従来どおり（[w 0 0 -h x y+h]）
            var m = Matrix(image, 10, 20, 60, 40);
            Assert.Equal(new double[] { 60, 0, 0, -40, 10, 60 }, m);
        }

        [Fact]
        public void FromFileAndFromStream_ApplyByDefault_AndCanIgnore()
        {
            byte[] jpeg = WithOrientation(6);
            string path = Path.Combine(Path.GetTempPath(), "chohyo-orientation-" + Guid.NewGuid().ToString("N") + ".jpg");
            File.WriteAllBytes(path, jpeg);
            try
            {
                Assert.True(PdfImage.FromFile(path).OrientationApplied);
                Assert.True(PdfImage.FromFile(path, true).OrientationApplied);
                Assert.False(PdfImage.FromFile(path, false).OrientationApplied);
                using (var stream = new MemoryStream(jpeg)) Assert.True(PdfImage.FromStream(stream).OrientationApplied);
                using (var stream = new MemoryStream(jpeg)) Assert.True(PdfImage.FromStream(stream, true).OrientationApplied);
                using (var stream = new MemoryStream(jpeg)) Assert.False(PdfImage.FromStream(stream, false).OrientationApplied);
                Assert.True(PdfImage.FromBytes(jpeg).OrientationApplied);
                Assert.True(PdfImage.FromBytes(jpeg, true).OrientationApplied);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ImagesWithoutExif_AreUnchanged()
        {
            var image = PdfImage.FromBytes(TestData.Bytes("rgb.jpg"));
            Assert.Equal(1, image.ExifOrientation);
            Assert.False(image.OrientationApplied);
            Assert.Equal(StoredWidth, image.PixelWidth);
            var m = Matrix(image, 10, 20, 60, 40);
            Assert.Equal(new double[] { 60, 0, 0, -40, 10, 60 }, m);

            // 向きが 1 と明示されていても同じ
            var explicitOne = PdfImage.FromBytes(WithOrientation(1));
            Assert.False(explicitOne.OrientationApplied);
            Assert.Equal(new double[] { 60, 0, 0, -40, 10, 60 }, Matrix(explicitOne, 10, 20, 60, 40));
        }

        [Fact]
        public void Png_HasNoOrientation()
        {
            foreach (bool apply in new[] { true, false })
            {
                var png = PdfImage.FromBytes(TestData.Bytes("rgba8.png"), apply);
                Assert.Equal(1, png.ExifOrientation);
                Assert.False(png.OrientationApplied);
            }
            var raw = PdfImage.FromRawPixels(2, 1, 3, new byte[6]);
            Assert.False(raw.OrientationApplied);
            Assert.Equal(2, raw.PixelWidth);
        }

        // ---- 不正な EXIF ----

        [Fact]
        public void InvalidExif_IsIgnoredWithoutError()
        {
            byte[] jpeg = TestData.Bytes("rgb.jpg");
            var cases = new List<byte[]>
            {
                ExifSegment(true, Orientation(0)),
                ExifSegment(true, Orientation(9)),
                ExifSegment(false, Orientation(255)),
                ExifSegment(true, new Entry { Tag = 0x0112, Type = 4, Count = 1, Value = 6 }),       // LONG は Orientation ではない
                ExifSegment(true, new Entry { Tag = 0x0112, Type = 3, Count = 0, Value = 6 }),       // 個数 0
                ExifSegment(true),                                                                   // エントリなし
                ExifSegment(true, new Entry { Tag = 0x0100, Type = 3, Count = 1, Value = 6 }),       // Orientation 以外のタグ
            };

            // バイト順が II / MM でない
            var badOrder = ExifSegment(true, Orientation(6));
            badOrder[10] = (byte)'X';
            badOrder[11] = (byte)'X';
            cases.Add(badOrder);

            // TIFF の識別値（0x002A）が違う
            var badMagic = ExifSegment(true, Orientation(6));
            badMagic[12] = 0x2B;
            cases.Add(badMagic);

            // IFD0 のオフセットがセグメントの外
            var farIfd = ExifSegment(true, Orientation(6));
            farIfd[14] = 0xFF;
            farIfd[15] = 0xFF;
            cases.Add(farIfd);

            // エントリの数が実際より多く、セグメントの終わりを超える
            var tooMany = ExifSegment(true, new Entry { Tag = 0x0100, Type = 3, Count = 1, Value = 6 });
            tooMany[18] = 0xFF;
            cases.Add(tooMany);

            // 途中で終わっている（TIFF ヘッダーの途中、IFD の途中）
            cases.Add(Wrap(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, (byte)'I', (byte)'I', 0x2A }));
            cases.Add(Wrap(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, (byte)'I', (byte)'I', 0x2A, 0, 8, 0, 0, 0, 1, 0, 0x12 }));

            // Exif ではない APP1（XMP）と、短すぎる APP1
            cases.Add(Wrap(System.Text.Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0<x:xmpmeta/>")));
            cases.Add(Wrap(new byte[] { 1, 2 }));
            cases.Add(Wrap(new byte[0]));

            for (int index = 0; index < cases.Count; index++)
            {
                var segment = cases[index];
                var image = PdfImage.FromBytes(Insert(jpeg, segment));
                Assert.True(image.ExifOrientation == 1, "ケース " + index + ": " + image.ExifOrientation);
                Assert.Equal(1, image.ExifOrientation);
                Assert.False(image.OrientationApplied);
                Assert.Equal(StoredWidth, image.PixelWidth);
                Assert.Equal(StoredHeight, image.PixelHeight);
            }
        }

        [Fact]
        public void FirstExifSegmentWins_AndNonExifApp1IsSkipped()
        {
            var xmp = Wrap(System.Text.Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0<x:xmpmeta/>"));
            byte[] jpeg = Insert(Insert(TestData.Bytes("rgb.jpg"), ExifSegment(true, Orientation(3))), xmp);
            Assert.Equal(3, PdfImage.FromBytes(jpeg).ExifOrientation);

            byte[] xmpFirst = Insert(Insert(TestData.Bytes("rgb.jpg"), ExifSegment(false, Orientation(8))), xmp);
            Assert.Equal(8, PdfImage.FromBytes(xmpFirst).ExifOrientation);
        }

        // ---- レイアウトでの扱い ----

        [Fact]
        public void DocumentImage_UsesOrientedNaturalSize()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            var doc = new Document(font, 10);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var image = PdfImage.FromBytes(WithOrientation(6));
            doc.AddSection().AddImage(image);
            var m = Matrices(doc.ToArray());
            Assert.Single(m);
            // 回転後の自然な大きさ（幅 = 23 ピクセル、高さ = 37 ピクセル）で描かれる
            double width = Math.Abs(m[0][0]) + Math.Abs(m[0][2]);
            double height = Math.Abs(m[0][1]) + Math.Abs(m[0][3]);
            Assert.Equal(StoredHeight * 72.0 / 150, width, 3);
            Assert.Equal(StoredWidth * 72.0 / 150, height, 3);
        }

        [Fact]
        public void DocumentImage_WithWidth_KeepsOrientedAspectRatio()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            var doc = new Document(font, 10);
            doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            doc.AddSection().AddImage(PdfImage.FromBytes(WithOrientation(8)), width: 46);
            var m = Matrices(doc.ToArray())[0];
            double width = Math.Abs(m[0]) + Math.Abs(m[2]);
            double height = Math.Abs(m[1]) + Math.Abs(m[3]);
            Assert.Equal(46, width, 3);
            Assert.Equal(46 * StoredWidth / (double)StoredHeight, height, 3);     // 回転後は 23 : 37
        }

        [Fact]
        public void DrawImageFit_UsesOrientedAspectRatio()
        {
            var image = PdfImage.FromBytes(WithOrientation(6));       // 回転後は縦長（23 : 37）
            var doc = new PdfDocument();
            doc.AddPage().Graphics.DrawImageFit(image, 0, 0, 100, 100);
            var m = Matrices(doc.ToArray())[0];
            double width = Math.Abs(m[0]) + Math.Abs(m[2]);
            double height = Math.Abs(m[1]) + Math.Abs(m[3]);
            Assert.Equal(100, height, 3);
            Assert.Equal(100 * StoredHeight / (double)StoredWidth, width, 3);
        }

        [Fact]
        public void SameOrientedImage_IsEmbeddedOnce()
        {
            var image = PdfImage.FromBytes(WithOrientation(6));
            var doc = new PdfDocument();
            for (int i = 0; i < 3; i++) doc.AddPage().Graphics.DrawImage(image, 10, 10, 23, 37);
            byte[] pdf = doc.ToArray();
            PdfChecker.AssertValidStructure(pdf);
            Assert.Single(Regex.Matches(PdfChecker.Latin1(pdf), "/Subtype /Image /Width 37 /Height 23"));
            Assert.Equal(3, Matrices(pdf).Count);
        }
    }
}
