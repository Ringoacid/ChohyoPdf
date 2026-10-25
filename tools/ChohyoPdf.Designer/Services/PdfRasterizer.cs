using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace ChohyoPdf.Designer.Services
{
    /// <summary>PDF のページを画像にする（Windows 標準の PDF 描画機能を使う）。</summary>
    public static class PdfRasterizer
    {
        /// <summary>
        /// ページを幅 <paramref name="pixelWidth"/> の画像にする。<paramref name="pages"/> が null ならすべてのページ。
        /// transparent なら、何も描いていないところを透明にする（白にしない）。
        /// </summary>
        public static async Task<List<BitmapSource>> RenderAsync(byte[] pdf, int pixelWidth, int? pages = null, bool transparent = false)
            => (await RenderAsync(pdf, _ => pixelWidth, pages, transparent)).ConvertAll(p => p.Image);

        /// <summary>
        /// すべてのページを、実寸（96 DPI の DIP）の dpiScale 倍のピクセルで画像にする。用紙の大きさがページごとに違ってもよい。
        /// 戻り値の WidthDip はページの幅（DIP）。
        /// </summary>
        public static Task<List<(BitmapSource Image, double WidthDip)>> RenderActualSizeAsync(byte[] pdf, double dpiScale)
            => RenderAsync(pdf, widthDip => (int)Math.Ceiling(widthDip * dpiScale), null);

        /// <summary>
        /// 最初のページを幅 <paramref name="pixelWidth"/> の画像にしたときの、<paramref name="area"/>（ピクセル）の範囲だけを画像にする。
        /// transparent なら、何も描いていないところを透明にする。ページがなければ null。
        /// </summary>
        public static async Task<BitmapSource?> RenderAreaAsync(byte[] pdf, int pixelWidth, Int32Rect area, bool transparent)
        {
            using var input = await LoadAsync(pdf);
            var document = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(input);
            if (document.PageCount == 0) return null;
            using var page = document.GetPage(0);
            // SourceRect は 96 DPI の DIP
            double dip = page.Size.Width / Math.Max(1, pixelWidth);
            var options = new PdfPageRenderOptions
            {
                SourceRect = new Windows.Foundation.Rect(area.X * dip, area.Y * dip, area.Width * dip, area.Height * dip),
                DestinationWidth = (uint)Math.Max(1, area.Width),
                DestinationHeight = (uint)Math.Max(1, area.Height),
            };
            if (transparent) options.BackgroundColor = Windows.UI.Color.FromArgb(0, 255, 255, 255);
            return await ToBitmapAsync(page, options);
        }

        private static async Task<List<(BitmapSource Image, double WidthDip)>> RenderAsync(byte[] pdf, Func<double, int> pixelWidth, int? pages, bool transparent = false)
        {
            using var input = await LoadAsync(pdf);
            // ChohyoPdf.PdfDocument と名前が重なるので、完全な名前で書く
            var document = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(input);
            int count = (int)Math.Min(document.PageCount, (uint)(pages ?? int.MaxValue));
            var result = new List<(BitmapSource, double)>();
            for (uint i = 0; i < count; i++)
            {
                using var page = document.GetPage(i);
                // Size は 96 DPI の DIP
                double widthDip = page.Size.Width;
                var options = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(1, pixelWidth(widthDip)) };
                if (transparent) options.BackgroundColor = Windows.UI.Color.FromArgb(0, 255, 255, 255);
                result.Add((await ToBitmapAsync(page, options), widthDip));
            }
            return result;
        }

        private static async Task<InMemoryRandomAccessStream> LoadAsync(byte[] pdf)
        {
            var input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(input))
            {
                writer.WriteBytes(pdf);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            input.Seek(0);
            return input;
        }

        private static async Task<BitmapSource> ToBitmapAsync(Windows.Data.Pdf.PdfPage page, PdfPageRenderOptions options)
        {
            using var output = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(output, options);
            var bytes = new byte[output.Size];
            output.Seek(0);
            using (var reader = new DataReader(output))
            {
                await reader.LoadAsync((uint)output.Size);
                reader.ReadBytes(bytes);
            }
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
