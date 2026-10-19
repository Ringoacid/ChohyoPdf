using System;
using ChohyoPdf.Images;
using Xunit;

namespace ChohyoPdf.Tests
{
    public class ImageTests
    {
        [Theory]
        [InlineData("rgba8")]
        [InlineData("rgb8")]
        [InlineData("gray8")]
        [InlineData("graya8")]
        [InlineData("gray1")]
        [InlineData("palette2")]
        [InlineData("palette4")]
        [InlineData("palette8trns")]
        [InlineData("rgb8_interlaced")]
        [InlineData("rgba8_interlaced")]
        public void Png_DecodesSameAsReference(string name)
        {
            var png = PngDecoder.Decode(TestData.Bytes(name + ".png"));
            byte[] expected = TestData.Bytes(name + ".rgba");
            Assert.Equal(expected.Length, png.Width * png.Height * 4);
            for (int i = 0; i < png.Width * png.Height; i++)
            {
                byte r, g, b;
                if (png.Components == 1)
                {
                    r = g = b = png.Pixels[i];
                }
                else
                {
                    r = png.Pixels[i * 3];
                    g = png.Pixels[i * 3 + 1];
                    b = png.Pixels[i * 3 + 2];
                }
                byte a = png.Alpha != null ? png.Alpha[i] : (byte)255;
                Assert.True(expected[i * 4 + 3] == a, name + ": alpha mismatch at " + i);
                if (a == 0) continue; // 完全透明の画素の色は問わない
                Assert.True(expected[i * 4] == r && expected[i * 4 + 1] == g && expected[i * 4 + 2] == b,
                    name + ": color mismatch at " + i + " expected " + expected[i * 4] + "," + expected[i * 4 + 1] + "," + expected[i * 4 + 2] + " got " + r + "," + g + "," + b);
            }
        }

        [Fact]
        public void Png_16BitGray()
        {
            var png = PngDecoder.Decode(TestData.Bytes("gray16.png"));
            Assert.Equal(1, png.Components);
            Assert.Null(png.Alpha);
            Assert.Equal(TestData.Bytes("gray16.gray"), png.Pixels);
        }

        [Fact]
        public void Png_OpaqueImageHasNoAlpha()
        {
            var img = PdfImage.FromBytes(TestData.Bytes("rgb8.png"));
            Assert.False(img.HasAlpha);
            Assert.True(PdfImage.FromBytes(TestData.Bytes("rgba8.png")).HasAlpha);
        }

        [Theory]
        [InlineData("rgb.jpg", 3, false)]
        [InlineData("gray.jpg", 1, false)]
        [InlineData("cmyk.jpg", 4, true)]
        [InlineData("progressive.jpg", 3, false)]
        public void Jpeg_ReadsHeader(string file, int components, bool invert)
        {
            var img = PdfImage.FromBytes(TestData.Bytes(file));
            Assert.Equal(37, img.PixelWidth);
            Assert.Equal(23, img.PixelHeight);
            Assert.Equal(components, img.Components);
            Assert.Equal(invert, img.InvertCmyk);
        }

        [Fact]
        public void Jpeg_ReadsDpi()
        {
            var img = PdfImage.FromBytes(TestData.Bytes("rgb.jpg"));
            Assert.Equal(150, img.DpiX);
            Assert.Equal(37 * 72.0 / 150, img.Width, 6);
        }

        [Fact]
        public void UnknownFormatThrows()
        {
            Assert.Throws<NotSupportedException>(() => PdfImage.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        }

        [Fact]
        public void SameImageIsEmbeddedOnce()
        {
            var img = PdfImage.FromBytes(TestData.Bytes("rgba8.png"));
            var doc = new PdfDocument();
            for (int i = 0; i < 3; i++) doc.AddPage().Graphics.DrawImage(img, 10, 10, 50, 30);
            var bytes = doc.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            string s = PdfChecker.Latin1(bytes);
            Assert.Equal(1, CountOccurrences(s, "/Subtype /Image /Width 37 /Height 23 /ColorSpace /DeviceRGB"));
            Assert.Equal(1, CountOccurrences(s, "/ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode /Length"));
            Assert.Contains("/SMask", s);
        }

        private static int CountOccurrences(string s, string sub)
        {
            int count = 0, i = 0;
            while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += sub.Length;
            }
            return count;
        }
    }
}
