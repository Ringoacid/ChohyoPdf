using System.Linq;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Xunit;

namespace ChohyoPdf.Designer.Tests
{
    /// <summary>上付き・下付き・ルビの記法と、箇条書きの項目の分け方。</summary>
    public class TextMarkupTests
    {
        private static string Describe(string text)
            => string.Join("|", TextMarkup.Parse(text).Select(r => r.Kind + ":" + r.Text + (r.Kind == MarkupKind.Ruby ? "/" + r.Ruby : "")));

        [Fact]
        public void Parse_SuperscriptSubscriptAndRuby()
        {
            Assert.Equal("Text:m|Superscript:2", Describe("m<sup>2</sup>"));
            Assert.Equal("Text:H|Subscript:2|Text:O", Describe("H<SUB>2</SUB>O"));
            Assert.Equal("Ruby:東京/とうきょう|Text:と|Ruby:{氏名}/{フリガナ}", Describe("｜東京《とうきょう》と|{氏名}《{フリガナ}》"));
            Assert.Equal("Text:a|Superscript:", Describe("a<sup></sup>"));
            Assert.True(TextMarkup.HasMarkup("x<sup>2</sup>"));
        }

        [Fact]
        public void Parse_KeepsIncompleteMarkupAsText()
        {
            // 閉じていない記法や、｜ のないルビは、そのままの文字（以前のデザインの見た目を変えない）
            foreach (string text in new[] { "<sup>2", "東京《とうきょう》", "｜東京《とうきょう", "｜《ルビ》", "｜改\n行《ルビ》", "a < b > c", "</sup>" })
            {
                Assert.False(TextMarkup.HasMarkup(text), text);
                Assert.Equal(text, string.Concat(TextMarkup.Parse(text).Select(r => r.Text)));
            }
        }

        [Fact]
        public void ListItems_NestByLeadingTabs()
        {
            var items = RichText.Items("一\n\t二\n\t\t\t三\n\t四\n五\n\t\t六");
            Assert.Equal(new[] { 0, 1, 2, 1, 0, 1 }, items.Select(i => i.Depth));
            Assert.Equal(new[] { false, true, true, false, false, true }, items.Select(i => i.NewSubList));
            Assert.Equal("三", items[2].Runs.Single().Text);
            // 最初の項目は字下げできない
            Assert.Equal(0, RichText.Items("\t先頭").Single().Depth);
        }

        [Fact]
        public void IsRich_OnlyWhenNeeded()
        {
            Assert.False(new TextElement { Text = "普通の文字 {名前}" }.IsRich);
            Assert.True(new TextElement { Text = "普通の文字", ListStyle = TextListStyle.Bullet }.IsRich);
            Assert.True(new TextElement { Text = "m<sup>2</sup>" }.IsRich);
        }

        [Fact]
        public void ListSettings_AreSavedOnlyWhenUsed()
        {
            var report = new ReportDefinition();
            report.Pages[0].Elements.Add(new TextElement { Text = "普通" });
            report.Pages[0].Elements.Add(new TextElement { Text = "項目", ListStyle = TextListStyle.Numbered, NumberStyle = ListNumberStyle.Kanji, Bullet = "●" });
            string json = ReportSerializer.ToJson(report);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"listStyle\""));
            var loaded = (TextElement)ReportSerializer.FromJson(json).Pages[0].Elements[1];
            Assert.Equal(TextListStyle.Numbered, loaded.ListStyle);
            Assert.Equal(ListNumberStyle.Kanji, loaded.NumberStyle);
            Assert.Equal("●", loaded.Bullet);
        }

        private static string Markers(TextElement e) => string.Join("|", ListMarkers.For(e).Select(m => m.Depth + ":" + m.Text));

        [Fact]
        public void ListMarkers_FollowListBlockRules()
        {
            // 入れ子の項目は深さごとの既定の記号。空の行も項目。深すぎるタブは 1 つ深いものとして扱う
            var bullets = new TextElement { ListStyle = TextListStyle.Bullet, Bullet = "◆", Text = "a\n\tb\n\t\t\t\tc\n\nd" };
            Assert.Equal("0:◆|1:○|2:■|0:◆|0:◆", Markers(bullets));

            // 番号は子リストごとに 1 から数え、浅い項目に戻ったらその深さの続きの番号
            var numbers = new TextElement { ListStyle = TextListStyle.Numbered, Text = "a\n\tb\n\tc\n\t\td\ne\n\tf" };
            Assert.Equal("0:1.|1:(1)|1:(2)|2:①|0:2.|1:(1)", Markers(numbers));

            // 番号の形を指定するのは、いちばん外側だけ
            numbers.NumberStyle = ListNumberStyle.KanjiParenthesized;
            Assert.Equal("0:（一）|1:(1)|1:(2)|2:①|0:（二）|1:(1)", Markers(numbers));
            numbers.NumberStyle = ListNumberStyle.Circled;
            Assert.StartsWith("0:①|", Markers(numbers));
            numbers.NumberStyle = ListNumberStyle.UpperRoman;
            Assert.StartsWith("0:I.|", Markers(numbers));

            Assert.Empty(ListMarkers.For(new TextElement { Text = "a\nb" }));
        }
    }
}
