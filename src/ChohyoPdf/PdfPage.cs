using System;
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

            /// <summary>外部リンクの URL（文書内リンクのときは空）。</summary>
            public string Uri = "";

            /// <summary>文書内リンクの飛び先ページ（ページを直接指定したとき）。</summary>
            public PdfPage? DestinationPage;

            /// <summary>文書内リンクの飛び先の縦位置（ページ上端からの距離）。</summary>
            public double DestinationY;

            /// <summary>文書内リンクの飛び先の名前（保存時に <see cref="PdfDocument"/> の登録から解決する）。</summary>
            public string? DestinationName;

            public bool IsInternal => DestinationPage != null || DestinationName != null;
        }

        private PdfGraphics? _graphics;

        internal PdfPage(PdfDocument document, double width, double height)
        {
            Document = document;
            Width = width;
            Height = height;
        }

        /// <summary>このページがある文書。</summary>
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

        /// <summary>
        /// ページ上の矩形（ページ座標）に、文書内の別のページ（または同じページ）へ移動するリンクを設定する。
        /// </summary>
        /// <param name="rect">リンクの領域（このページの座標）。</param>
        /// <param name="destination">飛び先のページ（同じ <see cref="PdfDocument"/> のページ）。</param>
        /// <param name="y">飛び先の縦位置（飛び先のページの上端からの距離、ポイント）。この位置がウィンドウの上端に来る。</param>
        public void AddLink(PdfRect rect, PdfPage destination, double y = 0)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Document != Document) throw new ArgumentException("飛び先は、同じ PdfDocument のページを指定してください。", nameof(destination));
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y), "位置は有限の値で指定してください。");
            Links.Add(new LinkAnnotation { Rect = rect, DestinationPage = destination, DestinationY = y });
        }

        /// <summary>名前で登録された飛び先へのリンク（文書の自動レイアウトが使う。飛び先は保存時に解決する）。</summary>
        internal void AddNamedLink(PdfRect rect, string name)
        {
            Links.Add(new LinkAnnotation { Rect = rect, DestinationName = name });
        }

        /// <summary>このページの指定位置へ移動するしおりを追加する（<see cref="PdfDocument.AddBookmark"/> と同じ）。</summary>
        /// <param name="title">しおりに表示する文字列。</param>
        /// <param name="y">移動先の縦位置（ページ上端からの距離、ポイント）。</param>
        public PdfBookmark AddBookmark(string title, double y = 0) => Document.AddBookmark(title, this, y);

        internal string BuildContent()
        {
            // 左上原点・y 下向きの座標系にする
            string prefix = "1 0 0 -1 0 " + PdfFormat.Num(Height) + " cm\n";
            return _graphics == null ? prefix : prefix + _graphics.GetContent();
        }
    }
}
