using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    internal static partial class Examples
    {
        public static void ImageOrientation()
        {
            // #example ImageOrientation
            var doc = new Document(font, 10.5);
            var s = doc.AddSection();

            // スマートフォンの写真は、画素が横向きのまま保存され、「回転して表示する」という EXIF の向き（Orientation）が付いていることが多い。
            // photo-exif.jpg は、画素を縦長（400 × 640）に保存し、向き 6（時計回りに 90 度）を付けたもの
            var upright = PdfImage.FromFile("photo-exif.jpg");              // 既定: 向きを反映する
            var asStored = PdfImage.FromFile("photo-exif.jpg", false);      // false: 向きを無視して、画素の向きのまま

            s.AddParagraph("向きを反映する（既定）").SetBold().SetSpaceAfter(2);
            s.AddParagraph($"EXIF の向き {upright.ExifOrientation}、{upright.PixelWidth} × {upright.PixelHeight} px").SetFontSize(9);
            s.AddImage(upright, width: Unit.Mm(80));

            s.AddParagraph("向きを無視する").SetBold().SetSpaceBefore(12).SetSpaceAfter(2);
            s.AddParagraph($"EXIF の向き {asStored.ExifOrientation}（反映していない）、{asStored.PixelWidth} × {asStored.PixelHeight} px").SetFontSize(9);
            s.AddImage(asStored, height: Unit.Mm(50));

            doc.Save("image-orientation.pdf");
            // #end
        }
    }
}
