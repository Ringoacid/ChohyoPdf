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
    }
}
