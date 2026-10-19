using System;
using System.Collections.Generic;
using System.IO;
using ChohyoPdf.Fonts;
using ChohyoPdf.Text;
using Xunit;

namespace ChohyoPdf.Tests
{
    public class FontAndTextTests
    {
        /// <summary>全角 1em・半角 0.5em の等幅フォント（MS ゴシック）。</summary>
        private static PdfFont? Mono()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            return path == null ? null : PdfFont.FromFile(path, "MS Gothic");
        }

        private static List<string> Break(PdfFont font, string text, double widthEm, double size = 10)
        {
            var block = TextBlock.Build(text, ResolvedStyle.From(new TextStyle(font, size)));
            block.Layout(widthEm * size, 0, LineSpacingRule.Multiple, 1.2);
            var lines = new List<string>();
            for (int i = 0; i < block.Lines.Count; i++) lines.Add(block.LineText(i));
            return lines;
        }

        [Fact]
        public void Font_LoadsFromCollectionByName()
        {
            string? path = TestFonts.WindowsFont("msgothic.ttc");
            if (path == null) return;
            var names = PdfFont.GetFaceNames(path);
            Assert.Contains("MS PGothic", names);
            var font = PdfFont.FromFile(path, "MS PGothic");
            Assert.Equal("MS PGothic", font.FullName);
            Assert.True(font.HasGlyph('あ'));
            Assert.False(font.HasGlyph(0x10FFFD));
            Assert.Throws<ArgumentException>(() => PdfFont.FromFile(path, "存在しないフォント"));
        }

        [Fact]
        public void Font_MeasuresFullAndHalfWidth()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(10, font.MeasureText("あ", 10), 3);
            Assert.Equal(5, font.MeasureText("A", 10), 3);
            Assert.Equal(40, font.MeasureText("日本語ab", 10), 3);
        }

        [Fact]
        public void Font_RejectsCffOpenType()
        {
            var data = new byte[64];
            data[0] = (byte)'O';
            data[1] = (byte)'T';
            data[2] = (byte)'T';
            data[3] = (byte)'O';
            Assert.Throws<NotSupportedException>(() => PdfFont.FromBytes(data));
        }

        [Fact]
        public void Font_SystemLookupByJapaneseName()
        {
            if (TestFonts.WindowsFont("msgothic.ttc") == null) return;
            var font = PdfFont.FromSystem("ＭＳ ゴシック");
            Assert.Equal("MS Gothic", font.FullName);
        }

        [Fact]
        public void Subset_IsAValidTrueTypeFontIncludingCompositeGlyphs()
        {
            string? path = TestFonts.WindowsFont("arial.ttf");
            if (path == null) return;
            var font = PdfFont.FromFile(path);
            var subset = new FontSubset(font.Ttf);
            foreach (char c in "Héllo Ä ñ")
            {
                subset.Map(font.GetGlyphId(c), c);
            }
            int used = subset.UsedCount;
            byte[] data = subset.Build();
            var parsed = TrueTypeFont.Load(data, 0);
            Assert.True(parsed.NumGlyphs >= used);
            Assert.Equal(subset.NewToOld.Count, parsed.NumGlyphs);
            for (ushort g = 0; g < parsed.NumGlyphs; g++)
            {
                parsed.GetGlyphLocation(g, out int offset, out int length);
                Assert.True(length >= 0);
                Assert.Equal(font.Ttf.GetAdvance(subset.NewToOld[g]), parsed.GetAdvance(g));
                if (length >= 10)
                {
                    var r = parsed.Reader;
                    if (r.S16(offset) < 0)
                    {
                        // 複合グリフの構成要素はサブセット内のグリフ ID を指していること
                        ushort component = r.U16(offset + 12);
                        Assert.True(component < parsed.NumGlyphs);
                        Assert.NotEqual(0, component);
                    }
                }
            }
        }

        [Fact]
        public void Kinsoku_ClosingPunctuationNeverStartsALine()
        {
            var font = Mono();
            if (font == null) return;
            var lines = Break(font, "あいうえお。かきくけこ", 5);
            Assert.Equal(new[] { "あいうえ", "お。かきく", "けこ" }, lines);
        }

        [Fact]
        public void Kinsoku_OpeningBracketNeverEndsALine()
        {
            var font = Mono();
            if (font == null) return;
            var lines = Break(font, "あいう「えお」", 4);
            Assert.Equal(new[] { "あいう", "「えお」" }, lines);
        }

        [Fact]
        public void Kinsoku_SmallKanaAndLongVowel()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(new[] { "あい", "うっか" }, Break(font, "あいうっか", 3));
            Assert.Equal(new[] { "コン", "ピュー", "タ" }, Break(font, "コンピュータ", 3));
        }

        [Fact]
        public void Latin_WrapsAtWordBoundaries()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(new[] { "hello", "world", "foo" }, Break(font, "hello world foo", 4));
            Assert.Equal(new[] { "hello", "world." }, Break(font, "hello world.", 3.5));
        }

        [Fact]
        public void Latin_LongWordIsForceBroken()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(new[] { "abcdef", "ghij" }, Break(font, "abcdefghij", 3));
        }

        [Fact]
        public void MixedJapaneseAndLatin()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(new[] { "日本語English", "テキスト" }, Break(font, "日本語Englishテキスト", 7));
        }

        [Fact]
        public void HardBreaks()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(new[] { "a", "b" }, Break(font, "a\nb", 10));
            Assert.Equal(new[] { "a", "b" }, Break(font, "a\r\nb", 10));
            Assert.Equal(new[] { "a", "" }, Break(font, "a\n", 10));
            Assert.Equal(new[] { "", "", "b" }, Break(font, "\n\nb", 10));
        }

        [Fact]
        public void ZeroWidthDoesNotHang()
        {
            var font = Mono();
            if (font == null) return;
            var lines = Break(font, "あいう abc", 0);
            Assert.Equal(new[] { "あ", "い", "う", "a", "b", "c" }, lines);
        }

        [Fact]
        public void SurrogatePairsStayTogether()
        {
            var font = TestFonts.Japanese;
            if (font == null) return;
            string text = "𠮷𠮷𠮷𩸽𩸽";
            var lines = Break(font, text, 2);
            Assert.True(lines.Count >= 2);
            Assert.Equal(text, string.Concat(lines));
        }

        [Fact]
        public void VariationSelectorsAreIgnoredInWidth()
        {
            var font = Mono();
            if (font == null) return;
            Assert.Equal(font.MeasureText("葛", 10), font.MeasureText("葛\U000E0100", 10), 6);
        }

        [Fact]
        public void TextBox_ShrinkToFit()
        {
            var font = Mono();
            if (font == null) return;
            var g = new PdfDocument().AddPage().Graphics;
            var result = g.DrawTextBox("あいうえおかきくけこさしすせそ", new PdfRect(0, 0, 50, 12), new TextStyle(font, 10),
                new TextBoxOptions { ShrinkToFit = true, LineSpacing = 1.0 });
            Assert.False(result.Overflowed);
            Assert.True(result.FontSize < 10);
            Assert.Equal(result.TotalLines, result.LinesDrawn);
        }

        [Fact]
        public void TextBox_ReportsOverflow()
        {
            var font = Mono();
            if (font == null) return;
            var g = new PdfDocument().AddPage().Graphics;
            var result = g.DrawTextBox("あいうえおかきくけこさしすせそ", new PdfRect(0, 0, 50, 12), new TextStyle(font, 10),
                new TextBoxOptions { LineSpacing = 1.0 });
            Assert.True(result.Overflowed);
            Assert.Equal(1, result.LinesDrawn);
            Assert.Equal(3, result.TotalLines);
        }

        [Fact]
        public void Fallback_UsesSecondFontForMissingGlyphs()
        {
            string? arial = TestFonts.WindowsFont("arial.ttf");
            var jp = TestFonts.Japanese;
            if (arial == null || jp == null) return;
            var latin = PdfFont.FromFile(arial);
            Assert.False(latin.HasGlyph('あ'));
            var doc = new PdfDocument { CompressContent = false };
            doc.AddPage().Graphics.DrawText("Aあ", 0, 0, new TextStyle(latin, 10) { FallbackFonts = new[] { jp } });
            string s = PdfChecker.Latin1(doc.ToArray());
            Assert.Contains("/F1 10 Tf", s);
            Assert.Contains("/F2 10 Tf", s);
        }

        [Fact]
        public void ToUnicode_ContainsSurrogatePair()
        {
            var font = TestFonts.Japanese;
            if (font == null || !font.HasGlyph(0x20BB7)) return;
            var doc = new PdfDocument();
            doc.AddPage().Graphics.DrawText("𠮷", 0, 0, new TextStyle(font, 10));
            var streams = PdfChecker.DecodedStreams(doc.ToArray());
            Assert.Contains(streams, s => s.Contains("beginbfchar") && s.Contains("<D842DFB7>"));
        }
    }
}
