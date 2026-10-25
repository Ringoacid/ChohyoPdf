using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void SuperscriptSubscript()
        {
            // #example SuperscriptSubscript
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // 上付き・下付きは、文字を 0.65 倍に縮小してベースラインを上下にずらす
            s.AddParagraph("アインシュタインの式 E = mc").AddSuperscript("2").AddText(" と、水の分子式 H").AddSubscript("2").AddText("O。");
            s.AddParagraph("注釈の番号は上付きで示す").AddSuperscript("※1").AddText("。面積は 5 m").AddSuperscript("2").AddText("、化学式は C").AddSubscript("6").AddText("H").AddSubscript("12").AddText("O").AddSubscript("6").AddText("。");

            // TextStyle.BaselineShift を使えば、色や下線と組み合わせたり、段落全体に指定したりできる
            s.AddParagraph().AddText("赤い上付き")
                .AddText("2", new TextStyle { BaselineShift = BaselineShift.Superscript, Color = PdfColor.Red })
                .AddText(" と下線付きの下付き")
                .AddSubscript("n", new TextStyle { Underline = true });

            // 座標指定の描画でも使える
            var pdf = doc.Render();
            var g = pdf.Pages[0].Graphics;
            var style = new TextStyle(font, 14);
            double x = Unit.Mm(20), y = Unit.Mm(55);
            x += g.DrawText("x", x, y, style);
            x += g.DrawText("2", x, y, new TextStyle(font, 14) { BaselineShift = BaselineShift.Superscript });
            x += g.DrawText(" + y", x, y, style);
            g.DrawText("2", x, y, new TextStyle(font, 14) { BaselineShift = BaselineShift.Superscript });

            pdf.Save("superscript-subscript.pdf");
            // #end
        }

        public static void TabStops()
        {
            // #example TabStops
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            double width = s.PageSetup.ContentWidth;          // 本文の幅

            s.AddParagraph("目次").SetFontSize(16).SetBold().SetSpaceAfter(6);
            string[] titles = { "はじめに", "インストール", "文字と段落", "表", "座標を指定して描く帳票" };
            int[] pages = { 1, 3, 5, 12, 20 };
            for (int i = 0; i < titles.Length; i++)
            {
                // 本文の右端に右揃えのタブを置き、点線（リーダー）でつなぐ
                s.AddParagraph((i + 1) + ". " + titles[i])
                    .AddTabStop(width, TabAlignment.Right, TabLeader.Dots)
                    .AddTab().AddText(pages[i].ToString())
                    .SetLineSpacing(1.8);
            }

            // 見積書のような、項目・数量・金額の並び（段落の書式として登録しておくと使い回せる）
            var fmt = new ParagraphFormat();
            fmt.AddTabStop(Unit.Mm(80), TabAlignment.Right);                       // 数量
            fmt.AddTabStop(Unit.Mm(110), TabAlignment.Decimal);                    // 小数点をそろえる
            fmt.AddTabStop(width, TabAlignment.Right, TabLeader.Line);            // 金額（実線でつなぐ）
            doc.Styles["見積"] = new ParagraphStyle(null, fmt);

            s.AddParagraph("見積").SetFontSize(16).SetBold().SetSpaceBefore(18).SetSpaceAfter(6);
            s.AddParagraph("品名\t数量\t単価\t金額", "見積").SetBold();
            s.AddParagraph("ノート PC\t2\t98,000.50\t196,001", "見積");
            s.AddParagraph("モニター\t10\t23,800.5\t238,005", "見積");
            s.AddParagraph("ケーブル\t100\t480\t48,000", "見積");

            // タブ位置を決めない段落の \t は、既定のタブ間隔ごとに進む
            s.AddParagraph("既定の間隔（36 ポイント）").SetFontSize(8).SetColor(PdfColor.Gray).SetSpaceBefore(18);
            s.AddParagraph("A\tB\tC\tD").SetDefaultTabStop(36);

            doc.Save("tab-stops.pdf");
            // #end
        }
    }
}
