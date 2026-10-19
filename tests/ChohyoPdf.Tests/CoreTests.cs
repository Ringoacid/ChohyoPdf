using System;
using System.Globalization;
using System.Text;
using System.Threading;
using ChohyoPdf.Internal;
using Xunit;

namespace ChohyoPdf.Tests
{
    public class CoreTests
    {
        [Theory]
        [InlineData(0.0, "0")]
        [InlineData(1.0, "1")]
        [InlineData(-2.5, "-2.5")]
        [InlineData(0.00001, "0")]
        [InlineData(-0.00001, "0")]
        [InlineData(1234567.891234, "1234567.8912")]
        [InlineData(1e-5 * 50, "0.0005")]
        public void Num_IsInvariantAndNeverExponential(double value, string expected)
        {
            var saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); // 小数点がカンマの文化圏
                Assert.Equal(expected, PdfFormat.Num(value));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Fact]
        public void Num_RejectsNaN()
        {
            Assert.Throws<ArgumentException>(() => PdfFormat.Num(double.NaN));
        }

        [Fact]
        public void TextString_EncodesNonAsciiAsUtf16()
        {
            Assert.Equal("(a\\(b\\))", PdfFormat.TextString("a(b)"));
            Assert.Equal("<FEFF65E5672C>", PdfFormat.TextString("日本"));
        }

        [Fact]
        public void Name_EscapesSpecialCharacters()
        {
            Assert.Equal("/A#20B", PdfFormat.Name("A B"));
            Assert.Equal("/ABC+Font", PdfFormat.Name("ABC+Font"));
        }

        [Fact]
        public void Zlib_RoundTripAndAdler()
        {
            var data = Encoding.ASCII.GetBytes("Wikipedia");
            Assert.Equal(0x11E60398u, Zlib.Adler32(data, 0, data.Length));
            var big = new byte[100000];
            new Random(1).NextBytes(big);
            Assert.Equal(big, Zlib.Decompress(Zlib.Compress(big)));
        }

        [Fact]
        public void Color_FromHex()
        {
            var c = PdfColor.FromHex("#FF000080");
            Assert.Equal(PdfColorSpace.Rgb, c.Space);
            Assert.Equal(128 / 255.0, c.Alpha, 6);
            Assert.Equal("1 0 0 rg", c.FillOperator());
            Assert.Equal(PdfColor.FromRgb(255, 255, 255), PdfColor.FromHex("fff"));
        }

        [Fact]
        public void PageSize_Landscape()
        {
            var a4 = PageSize.A4;
            Assert.Equal(595.2756, a4.Width, 3);
            Assert.Equal(a4.Height, a4.Landscape.Width);
            Assert.Equal(a4, a4.Landscape.Portrait);
        }

        [Fact]
        public void EmptyDocument_ProducesValidPdf()
        {
            var pdf = new PdfDocument();
            var bytes = pdf.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            Assert.Equal(1, PdfChecker.CountPages(bytes));
        }

        [Fact]
        public void Graphics_ShapesProduceValidPdf()
        {
            var pdf = new PdfDocument { CompressContent = false };
            var g = pdf.AddPage(PageSize.A5).Graphics;
            g.DrawLine(new PdfPen(PdfColor.Red, 2), 0, 0, 100, 100);
            g.DrawRectangle(PdfPen.Dashed(PdfColor.Black, 1), PdfColor.Yellow.WithAlpha(0.5), 10, 10, 50, 50);
            g.DrawEllipse(null, PdfColor.Blue, 10, 10, 30, 20);
            g.DrawPie(null, PdfColor.Green, 50, 50, 20, 0, 90);
            g.SaveState();
            g.Rotate(30);
            g.SetClip(0, 0, 10, 10);
            g.RestoreState();
            g.AddLink(new PdfRect(0, 0, 10, 10), "https://example.com/");
            var bytes = pdf.ToArray();
            PdfChecker.AssertValidStructure(bytes);
            string s = PdfChecker.Latin1(bytes);
            Assert.Contains("/ca 0.5", s);
            Assert.Contains("/S /URI /URI (https://example.com/)", s);
            Assert.Contains("[3 2] 0 d", s);
        }

        [Fact]
        public void Graphics_UnbalancedRestoreThrows()
        {
            var g = new PdfDocument().AddPage().Graphics;
            Assert.Throws<InvalidOperationException>(() => g.RestoreState());
        }

        [Fact]
        public void Save_IsDeterministicWithFixedDate()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            byte[] Make()
            {
                var doc = new Document(font);
                doc.Info.CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                doc.Info.Title = "決定的";
                doc.AddSection().AddParagraph("同じ入力からは同じバイト列");
                return doc.ToArray();
            }
            Assert.Equal(Make(), Make());
        }
    }
}
