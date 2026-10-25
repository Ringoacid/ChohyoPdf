using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void Ruby()
        {
            // #example Ruby
            var doc = new Document(font, 12);
            var s = doc.AddSection();
            s.AddParagraph("ルビ（振り仮名）").SetFontSize(18).SetBold().SetSpaceAfter(6);

            // 親文字とルビを組で追加する。ルビの文字サイズは親文字の半分で、親文字の中央にそろう
            s.AddParagraph("私は")
                .AddRuby("東京", "とうきょう")
                .AddText("の")
                .AddRuby("下町", "したまち")
                .AddText("で生まれました。");

            // 親文字の 1 文字ごとにルビを付ける（モノルビ）。組ごとに改行できる
            s.AddParagraph("モノルビ: ").AddMonoRuby("東京", new[] { "とう", "きょう" }).AddText("、")
                .AddMonoRuby("振り仮名", new[] { "ふ", "", "がな", "" });

            // 「｜親文字《ルビ》」の記法でまとめて書ける（「｜」を省くと、直前の漢字が親文字になる）
            s.AddParagraph().AddRubyMarkup("記法: 吾輩《わがはい》は猫《ねこ》である。｜Tokyo《とうきょう》、大阪府《おお|さか|ふ》。");

            // ルビが親文字より長いときは、親文字の文字の間を均等に空けてルビの幅に合わせる
            s.AddParagraph("長いルビ: ").AddRuby("漢字", "かんじのよみがな").AddText("と、短いルビ: ").AddRuby("東京都", "と");

            // ルビの色や文字サイズ、親文字の装飾
            var rubyStyle = new TextStyle { Color = PdfColor.FromHex("#C0392B") };
            s.AddParagraph("装飾: ")
                .AddRuby("重要", "じゅうよう", new TextStyle { Bold = true, Underline = true }, rubyStyle)
                .AddText("、")
                .AddRuby("注意", "ちゅうい", new TextStyle { Color = PdfColor.Blue }, new TextStyle { Color = PdfColor.Blue, FontSize = 4 });

            // 段落全体のルビの色は Paragraph.RubyStyle で決められる。両端揃えでも、折り返しでも、組は離れない
            var p = s.AddParagraph().SetAlignment(TextAlignment.Justify).SetRubyStyle(new TextStyle { Color = PdfColor.Gray });
            p.AddRubyMarkup("吾輩《わがはい》は猫《ねこ》である。名前《なまえ》はまだ無い。どこで生《うま》れたか頓《とん》と見当《けんとう》がつかぬ。"
                + "何《なん》でも薄暗《うすぐら》いじめじめした所《ところ》でニャーニャー泣《な》いていた事《こと》だけは記憶《きおく》している。");

            // 行送りを固定値（Exactly）にすると、ルビがあっても行の高さは変わらない
            s.AddParagraph("固定値の行送り: ").SetLineSpacing(22, LineSpacingRule.Exactly)
                .AddRuby("漢字", "かんじ").AddText("の行も、").AddRuby("次", "つぎ").AddText("の行も同じ間隔。");

            doc.Save("ruby.pdf");
            // #end
        }
    }
}
