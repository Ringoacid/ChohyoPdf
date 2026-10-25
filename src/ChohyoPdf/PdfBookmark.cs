using System;
using System.Collections.Generic;

namespace ChohyoPdf
{
    /// <summary>
    /// しおり（PDF のアウトライン）の項目。クリックすると指定したページの位置へ移動する。
    /// <see cref="PdfDocument.AddBookmark"/> / <see cref="PdfPage.AddBookmark"/> で追加し、
    /// <see cref="AddChild"/> で入れ子にする。
    /// </summary>
    public sealed class PdfBookmark
    {
        private readonly PdfDocument _document;
        private readonly List<PdfBookmark> _children = new List<PdfBookmark>();
        private string _title;
        private PdfPage _page;

        internal PdfBookmark(PdfDocument document, string title, PdfPage page, double y)
        {
            _document = document;
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _page = CheckPage(page);
            Y = CheckY(y);
        }

        /// <summary>しおりに表示する文字列。</summary>
        public string Title
        {
            get => _title;
            set => _title = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>移動先のページ（この項目と同じ <see cref="PdfDocument"/> のページ）。</summary>
        public PdfPage Page
        {
            get => _page;
            set => _page = CheckPage(value);
        }

        private double _y;

        /// <summary>
        /// 移動先の縦位置（ページ上端からの距離、ポイント。座標系は <see cref="PdfGraphics"/> と同じで y は下向き）。
        /// この位置がウィンドウの上端に来るようにスクロールする。
        /// </summary>
        public double Y
        {
            get => _y;
            set => _y = CheckY(value);
        }

        /// <summary>子の項目を開いた状態で表示するか（既定 true）。</summary>
        public bool IsOpen { get; set; } = true;

        /// <summary>子の項目。</summary>
        public IReadOnlyList<PdfBookmark> Children => _children;

        /// <summary>子の項目を追加する。</summary>
        public PdfBookmark AddChild(string title, PdfPage page, double y = 0)
        {
            var child = new PdfBookmark(_document, title, page, y);
            _children.Add(child);
            return child;
        }

        private PdfPage CheckPage(PdfPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (page.Document != _document) throw new ArgumentException("しおりの移動先は、同じ PdfDocument のページを指定してください。", nameof(page));
            return page;
        }

        private static double CheckY(double y)
        {
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y), "位置は有限の値で指定してください。");
            return y;
        }
    }
}
