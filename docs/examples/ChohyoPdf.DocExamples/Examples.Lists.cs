using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void BulletLists()
        {
            // #example BulletLists
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            s.AddParagraph("持ち物").SetBold().SetFontSize(14).SetSpaceAfter(4);

            // 記号を指定しなければ、入れ子のレベルごとに ・ → ○ → ■ になる
            var list = s.AddBulletList();
            list.AddItem("筆記用具");
            list.AddItem("受験票と身分証明書。長い項目は折り返され、2 行目以降の行頭は本文の開始位置にそろいます。");
            list.AddItem("昼食");
            var drinks = list.AddSubList();                  // 最後の項目（昼食）の子リスト
            drinks.AddItem("お弁当");
            drinks.AddItem("飲み物");
            drinks.AddSubList().AddItem("水筒（容量は問いません）");
            list.AddItem("上履き");

            // 記号は任意の文字列で、色や大きさを本文と別に指定できる
            s.AddParagraph("注意事項").SetBold().SetFontSize(14).SetSpaceBefore(14).SetSpaceAfter(4);
            var notes = s.AddBulletList("●");
            notes.MarkerStyle = new TextStyle { Color = PdfColor.FromHex("#C0392B"), FontSize = 8 };
            notes.ItemSpacing = 3;                           // 項目の間隔
            notes.AddItem("集合時間の 10 分前までに会場へ入ってください。");
            notes.AddItem("電子機器の電源は切ってください。");
            notes.AddItem("体調が悪いときは、すぐに係員へ伝えてください。");

            // 項目に複数のブロックを入れる（記号は最初の段落の 1 行目にだけ付く）
            s.AddParagraph("連絡先").SetBold().SetFontSize(14).SetSpaceBefore(14).SetSpaceAfter(4);
            var contacts = s.AddBulletList("–");
            var item = contacts.AddItem();
            item.AddParagraph("事務局").SetBold();
            item.AddParagraph("平日 9:00〜17:00 に受け付けています。");
            contacts.AddItem("メール: info@example.com");

            doc.Save("bullet-lists.pdf");
            // #end
        }

        public static void NumberedLists()
        {
            // #example NumberedLists
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            s.AddParagraph("手順").SetBold().SetFontSize(14).SetSpaceAfter(4);

            // 番号の書式を指定しなければ、入れ子のレベルごとに 1. → (1) → ① になる
            var steps = s.AddNumberedList();
            steps.AddItem("書類を準備する");
            steps.AddItem("窓口で受付をする");
            var sub = steps.AddSubList();
            sub.AddItem("番号札を受け取る");
            sub.AddItem("呼ばれるまで待つ");
            sub.AddSubList().AddItem("待ち時間は 30 分ほどです");
            steps.AddItem("内容を確認して署名する");

            // 書式（PageNumberFormat）、前後の文字列、開始番号を指定する
            s.AddParagraph("契約条項").SetBold().SetFontSize(14).SetSpaceBefore(14).SetSpaceAfter(4);
            var clauses = s.AddNumberedList(PageNumberFormat.Kanji, suffix: "、");
            clauses.AddItem("甲は、乙に対し、本件業務を委託する。");
            clauses.AddItem("乙は、前条の業務を善良な管理者の注意をもって行う。");
            var detail = clauses.AddSubList(ListKind.Numbered);
            detail.NumberFormat = PageNumberFormat.Kanji;
            detail.Prefix = "（";
            detail.Suffix = "）";
            detail.AddItem("業務の範囲は、別紙に定めるとおりとする。");
            detail.AddItem("業務の期間は、契約の日から 1 年間とする。");

            var letters = s.AddNumberedList(PageNumberFormat.LowerLetter, startNumber: 1, prefix: "(", suffix: ")");
            letters.SpaceBefore = 8;
            letters.AddItem("a から始まる");
            letters.AddItem("次の項目は b");

            // 番号を右揃えにすると、9. → 10. のように桁が増えても右端がそろう
            s.AddParagraph("右揃えの番号").SetBold().SetFontSize(14).SetSpaceBefore(14).SetSpaceAfter(4);
            var right = s.AddNumberedList(startNumber: 8);
            right.MarkerAlignment = HorizontalAlignment.Right;
            right.MarkerStyle = new TextStyle { Bold = true };
            for (int i = 8; i <= 11; i++) right.AddItem("項目 " + i);

            doc.Save("numbered-lists.pdf");
            // #end
        }
    }
}
