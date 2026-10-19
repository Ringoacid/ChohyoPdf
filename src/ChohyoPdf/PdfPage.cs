using System.Collections.Generic;
using ChohyoPdf.Internal;

namespace ChohyoPdf
{
    /// <summary>PDF のページ。</summary>
    public sealed class PdfPage
    {
        internal sealed class LinkAnnotation
        {
            public PdfRect Rect;
            public string Uri = "";
        }

        private PdfGraphics? _graphics;

        internal PdfPage(PdfDocument document, double width, double height)
        {
            Document = document;
            Width = width;
            Height = height;
        }

        public PdfDocument Document { get; }

        /// <summary>ページの幅（ポイント）。</summary>
        public double Width { get; }

        /// <summary>ページの高さ（ポイント）。</summary>
        public double Height { get; }

        /// <summary>このページに描画するためのグラフィックス（座標は左上原点、ポイント単位、y は下向き）。</summary>
        public PdfGraphics Graphics => _graphics ?? (_graphics = new PdfGraphics(this));

        internal List<PdfDocument.FontResource> UsedFonts { get; } = new List<PdfDocument.FontResource>();
        internal List<PdfDocument.ImageResource> UsedImages { get; } = new List<PdfDocument.ImageResource>();
        internal Dictionary<string, string> UsedStates { get; } = new Dictionary<string, string>();
        internal List<LinkAnnotation> Links { get; } = new List<LinkAnnotation>();

        /// <summary>ページ上の矩形（ページ座標）に URL へのリンクを設定する。</summary>
        public void AddLink(PdfRect rect, string uri)
        {
            Links.Add(new LinkAnnotation { Rect = rect, Uri = uri });
        }

        internal string BuildContent()
        {
            // 左上原点・y 下向きの座標系にする
            string prefix = "1 0 0 -1 0 " + PdfFormat.Num(Height) + " cm\n";
            return _graphics == null ? prefix : prefix + _graphics.GetContent();
        }
    }
}
