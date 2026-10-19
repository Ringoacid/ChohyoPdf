using System;
using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void FontLookup()
        {
            // #example FontLookup
            // TTC ファイルの中のフォント名を調べ、名前で選んで読み込む
            foreach (string name in PdfFont.GetFaceNames(@"C:\Windows\Fonts\msgothic.ttc"))
                Console.WriteLine(name);                     // MS Gothic、MS UI Gothic、MS PGothic
            var pgothic = PdfFont.FromFile(@"C:\Windows\Fonts\msgothic.ttc", "MS PGothic");

            // 候補を順に探す（サーバーなど、環境によってフォントが違うとき）
            if (!PdfFont.TryFromSystem(out var gothic, "Yu Gothic Regular", "Meiryo", "Noto Sans JP", "IPAexGothic"))
                throw new InvalidOperationException("日本語フォントがありません");

            // 文字があるかを調べる。サロゲートペアの文字はコードポイントにしてから渡す
            Console.WriteLine(gothic!.HasGlyph('あ'));                    // True
            Console.WriteLine(gothic.HasGlyph(char.ConvertToUtf32("𠮷", 0))); // True
            Console.WriteLine(gothic.HasGlyph(0x265A));                   // False（♚ は游ゴシックにない）
            // #end
            _ = pgothic;
        }
    }
}
