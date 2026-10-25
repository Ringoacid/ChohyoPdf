using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void Bookmarks()
        {
            // #example Bookmarks
            var doc = new Document(font, 10.5);
            doc.ShowBookmarkPane = true;   // PDF を開いたときにしおりの欄を表示する

            // 見出しレベルは名前付きスタイルで指定できる（段落の SetOutline(レベル) でもよい）
            doc.Styles["見出し1"] = new ParagraphStyle(
                new TextStyle { FontSize = 16, Bold = true },
                new ParagraphFormat { OutlineLevel = 1, SpaceBefore = 12, SpaceAfter = 6, KeepWithNext = true });
            doc.Styles["見出し2"] = new ParagraphStyle(
                new TextStyle { FontSize = 12.5, Bold = true },
                new ParagraphFormat { OutlineLevel = 2, SpaceBefore = 8, SpaceAfter = 4, KeepWithNext = true });

            var s = doc.AddSection();
            s.AddParagraph("第 1 章　製品の概要", "見出し1");
            s.AddParagraph("1.1 特長", "見出し2");
            s.AddParagraph(Filler(6));
            s.AddParagraph("1.2 仕様", "見出し2");
            s.AddParagraph(Filler(6));
            s.AddParagraph("第 2 章　使い方", "見出し1").SetPageBreakBefore();
            s.AddParagraph("2.1 準備", "見出し2");
            s.AddParagraph(Filler(6));

            // しおりの文字列は、段落の文字列と別にもできる
            s.AddParagraph("付録 A", "見出し1").SetPageBreakBefore().OutlineTitle = "付録 A　用語集";
            s.AddParagraph(Filler(3));

            doc.Save("bookmarks.pdf");
            // #end
        }

        public static void BookmarksLowLevel()
        {
            // #example BookmarksLowLevel
            var pdf = new PdfDocument();
            var cover = pdf.AddPage();
            var body = pdf.AddPage();
            var appendix = pdf.AddPage();
            cover.Graphics.DrawText("表紙", 72, 100, new TextStyle(font, 20));
            body.Graphics.DrawText("本文", 72, 100, new TextStyle(font, 20));
            appendix.Graphics.DrawText("付録", 72, 100, new TextStyle(font, 20));

            // ページと、ページ上端からの位置（ポイント）を指定する
            pdf.AddBookmark("表紙", cover);
            var chapter = pdf.AddBookmark("本文", body, 100);
            chapter.AddChild("1.1 はじめに", body, 100);
            chapter.AddChild("1.2 おわりに", body, 400);
            appendix.AddBookmark("付録");      // PdfPage.AddBookmark もある
            pdf.ShowBookmarkPane = true;

            // 文書内リンク: 矩形をクリックすると、別のページの指定位置へ移動する
            var link = new PdfRect(72, 140, 120, 20);
            cover.Graphics.DrawText("本文へ", link.X, link.Y + 14, new TextStyle(font, 12) { Color = PdfColor.FromRgb(0, 0, 238), Underline = true });
            cover.AddLink(link, body, 100);

            pdf.Save("bookmarks-lowlevel.pdf");
            // #end
        }

        public static void InternalLinks()
        {
            // #example InternalLinks
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();
            s.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber();

            // 段落にアンカー（名前）を付ける。リンクとページ参照は、名前で指す
            s.AddParagraph("ご利用ガイド").SetFontSize(16).SetBold().SetAnchor("top");
            s.AddParagraph().AddText("詳しくは ").AddInternalLink("第 3 章（設定）", "settings")
                .AddText("（")
                .AddPageRef("settings")        // アンカーがあるページの番号（後ろのページでもよい）
                .AddText(" ページ）を参照してください。");
            s.AddParagraph(Filler(10));

            s.AddPageBreak();
            s.AddParagraph("第 2 章　基本操作").SetFontSize(14).SetBold();
            s.AddParagraph(Filler(10));

            s.AddPageBreak();
            s.AddParagraph("第 3 章　設定").SetFontSize(14).SetBold().SetAnchor("settings");
            // 段落の途中の位置にもアンカーを置ける
            s.AddParagraph("この段落の途中に").AddAnchor("note").AddText("アンカーがあります。");
            s.AddParagraph().AddInternalLink("先頭へ戻る", "top").AddText("　／　")
                .AddInternalLink("段落の途中のアンカーへ", "note");

            doc.Save("internal-links.pdf");
            // #end
        }

        public static void TableOfContents()
        {
            // #example TableOfContents
            var doc = new Document(font, 10.5);
            doc.ShowBookmarkPane = true;
            doc.Styles["見出し1"] = new ParagraphStyle(
                new TextStyle { FontSize = 15, Bold = true },
                new ParagraphFormat { OutlineLevel = 1, SpaceBefore = 10, SpaceAfter = 6, KeepWithNext = true, PageBreakBefore = true });
            doc.Styles["見出し2"] = new ParagraphStyle(
                new TextStyle { FontSize = 12, Bold = true },
                new ParagraphFormat { OutlineLevel = 2, SpaceBefore = 8, SpaceAfter = 4, KeepWithNext = true });
            doc.Styles["目次見出し"] = new ParagraphStyle(
                new TextStyle { FontSize = 18, Bold = true }, new ParagraphFormat { SpaceAfter = 12, Alignment = TextAlignment.Center });
            doc.Styles["目次1"] = new ParagraphStyle(new TextStyle { Bold = true }, new ParagraphFormat { SpaceBefore = 4 });

            // 前付け: ローマ数字。目次と序文
            var front = doc.AddSection();
            front.PageNumberFormat = PageNumberFormat.LowerRoman;
            front.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber();
            var toc = front.AddTableOfContents("目次", maxLevel: 2);   // レベル 2 までを載せる
            toc.TitleStyleName = "目次見出し";                          // 目次の見出しのスタイル
            toc.LevelStyleNames[1] = "目次1";                           // レベルごとのスタイル
            toc.IndentPerLevel = 18;                                    // レベルが 1 つ深いごとのインデント
            front.AddParagraph("序文", "見出し1");
            front.AddParagraph(Filler(8));

            // 本文: 1 から数え直す。目次の番号は、見出しのページに表示される番号になる
            var body = doc.AddSection();
            body.PageNumberStart = 1;
            body.PageNumberFormat = PageNumberFormat.Arabic;
            body.Footer.AddParagraph().SetAlignment(TextAlignment.Center).AddPageNumber();
            for (int chapter = 1; chapter <= 8; chapter++)
            {
                body.AddParagraph("第 " + chapter + " 章", "見出し1");
                for (int section = 1; section <= 2; section++)
                {
                    body.AddParagraph(chapter + "." + section + " 節の見出し", "見出し2");
                    body.AddParagraph(Filler(14));
                }
            }

            doc.Save("table-of-contents.pdf");
            // #end
        }
    }
}
