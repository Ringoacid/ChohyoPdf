using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void Footnotes()
        {
            // #example Footnotes
            var doc = new Document(font, 10.5);
            doc.Styles["見出し"] = new ParagraphStyle(
                new TextStyle { FontSize = 13, Bold = true },
                new ParagraphFormat { SpaceBefore = 8, SpaceAfter = 4, KeepWithNext = true });

            // 脚注の書式は文書全体で指定する（既定は本文の 0.85 倍の文字サイズ）。区切り線も変えられる
            doc.FootnoteStyle = new TextStyle { Color = PdfColor.FromHex("#444444") };
            doc.FootnoteFormat = new ParagraphFormat { LineSpacing = 1.15 };
            doc.FootnoteSeparator = new PdfPen(PdfColor.Gray, 0.5);       // null なら線を引かない
            doc.FootnoteSeparatorLength = Unit.Mm(40);                    // 既定は本文の幅の 3 分の 1

            var s = doc.AddSection();
            s.AddParagraph("調査の概要", "見出し");

            // AddFootnote("文字列") で、その位置に脚注を付ける。本文に上付きの番号が入り、同じ番号の脚注がページの下に置かれる
            s.AddParagraph("この調査は、2026 年 4 月から 9 月にかけて実施した。")
                .AddFootnote("調査の対象は、国内に本社を置く従業員 50 人以上の企業。")
                .AddText("回答数は 1,248 社で、回収率は 41.6 %だった。")
                .AddFootnote("回収率は、発送数に対する有効回答の割合。");

            // 引数なしの AddFootnote() は脚注の本文を返す。段落やリストを複数入れられる
            var note = s.AddParagraph("業種の分類には、日本標準産業分類を用いた。").AddFootnote();
            note.AddParagraph("日本標準産業分類（総務省）の大分類による。ただし、次の業種はまとめて集計した。");
            var merged = note.AddBulletList();
            merged.AddItem("電気・ガス・熱供給・水道業");
            merged.AddItem("情報通信業");

            // 表のセルやリストの項目にも付けられる
            var table = s.AddTable();
            table.AddColumns(2, 1);
            table.AddHeaderRow("業種", "回答数");
            var row = table.AddRow();
            row.Cells[0].AddParagraph("製造業").AddFootnote("食料品・繊維・化学などを含む。");
            row.Cells[1].AddParagraph("412");
            table.AddRow("小売業", "236");

            s.AddParagraph("結果", "見出し");
            s.AddParagraph(Filler(14));

            doc.Save("footnotes.pdf");
            // #end
        }

        public static void FootnoteNumbers()
        {
            // #example FootnoteNumbers
            var doc = new Document(font, 10.5);

            // 番号の書式: 数字の書式（PageNumberFormat）か、記号の列。記号を使い切ると "**", "††" のように重ねる
            doc.FootnoteSymbols = new[] { "*", "†", "‡" };

            var first = doc.AddSection();
            first.AddParagraph("第 1 章").SetBold();
            first.AddParagraph("最初の注。").AddFootnote("記号の脚注（1 つ目）。");
            first.AddParagraph("次の注。").AddFootnote("記号の脚注（2 つ目）。");

            // 既定では前のセクションからの続き番号。Restart にすると、このセクションから 1 に戻る
            var second = doc.AddSection();
            second.FootnoteNumbering = FootnoteNumbering.Restart;
            second.AddParagraph("第 2 章").SetBold();
            second.AddParagraph("章が替わったので、番号が振り直される。").AddFootnote("この章の最初の脚注。");

            doc.Save("footnote-numbers.pdf");
            // #end
        }
    }
}
