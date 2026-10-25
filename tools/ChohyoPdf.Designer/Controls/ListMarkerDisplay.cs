using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ChohyoPdf.Designer.Rendering;

namespace ChohyoPdf.Designer.Controls
{
    using Rect = System.Windows.Rect;
    using TextElement = ChohyoPdf.Designer.Model.TextElement;
    using Thickness = System.Windows.Thickness;

    /// <summary>
    /// 箇条書きのテキストを編集している入力欄で、各項目の行の左に記号や番号を表示する（文字には含めず、表示するだけ）。
    /// 入力欄に <c>ListMarkerDisplay.Element</c> でテキストの部品を指定する。箇条書きにしていなければ何も表示しない。
    /// </summary>
    public static class ListMarkerDisplay
    {
        public static readonly DependencyProperty ElementProperty = DependencyProperty.RegisterAttached(
            "Element", typeof(TextElement), typeof(ListMarkerDisplay), new PropertyMetadata(null, OnElementChanged));

        public static TextElement? GetElement(DependencyObject d) => (TextElement?)d.GetValue(ElementProperty);

        public static void SetElement(DependencyObject d, TextElement? value) => d.SetValue(ElementProperty, value);

        // 入力欄に付けた表示
        private static readonly DependencyProperty AdornerProperty = DependencyProperty.RegisterAttached(
            "Adorner", typeof(ListMarkerAdorner), typeof(ListMarkerDisplay), new PropertyMetadata(null));

        private static void OnElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox box) return;
            box.Loaded -= OnLoaded;
            box.Unloaded -= OnUnloaded;
            Detach(box);
            if (e.NewValue == null) return;
            box.Loaded += OnLoaded;
            box.Unloaded += OnUnloaded;
            if (box.IsLoaded) Attach(box);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Attach((TextBox)sender);

        private static void OnUnloaded(object sender, RoutedEventArgs e) => Detach((TextBox)sender);

        private static void Attach(TextBox box)
        {
            Detach(box);
            if (GetElement(box) is not TextElement element || AdornerLayer.GetAdornerLayer(box) is not AdornerLayer layer) return;
            var adorner = new ListMarkerAdorner(box, element);
            layer.Add(adorner);
            box.SetValue(AdornerProperty, adorner);
        }

        private static void Detach(TextBox box)
        {
            if (box.GetValue(AdornerProperty) is not ListMarkerAdorner adorner) return;
            box.ClearValue(AdornerProperty);
            adorner.Close();
            AdornerLayer.GetAdornerLayer(box)?.Remove(adorner);
        }
    }

    /// <summary>入力欄の各項目の行の左に、記号や番号を描く。記号を置く分だけ、入力欄の左の余白を広げる。</summary>
    internal sealed class ListMarkerAdorner : Adorner
    {
        private static readonly Brush MarkerBrush = CreateBrush();
        private readonly TextBox _box;
        private readonly TextElement _element;
        // 記号の分を足す前の、入力欄の余白
        private readonly Thickness _padding;
        // 文字の配置が決まる前に描こうとして、描き直しを予約した回数（変更のたびに 0 に戻す）
        private int _retries;

        public ListMarkerAdorner(TextBox box, TextElement element) : base(box)
        {
            _box = box;
            _element = element;
            _padding = box.Padding;
            IsHitTestVisible = false;
            box.TextChanged += OnTextChanged;
            box.SizeChanged += OnBoxChanged;
            box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            element.PropertyChanged += OnElementPropertyChanged;
            UpdatePadding();
        }

        private static Brush CreateBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x71, 0x71, 0x7A));
            brush.Freeze();
            return brush;
        }

        /// <summary>表示をやめる（入力欄の余白を元に戻す）。</summary>
        public void Close()
        {
            _box.TextChanged -= OnTextChanged;
            _box.SizeChanged -= OnBoxChanged;
            _box.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
            _element.PropertyChanged -= OnElementPropertyChanged;
            _box.Padding = _padding;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePadding();
            Refresh();
        }

        private void OnBoxChanged(object sender, SizeChangedEventArgs e) => Refresh();

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Refresh();

        private void Refresh()
        {
            _retries = 0;
            InvalidateVisual();
        }

        private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextElement.ListStyle) || e.PropertyName == nameof(TextElement.Bullet) || e.PropertyName == nameof(TextElement.NumberStyle))
            {
                UpdatePadding();
                Refresh();
            }
        }

        private System.Collections.Generic.List<ListMarker> Markers()
            => ListMarkers.For(_box.Text, _element.ListStyle, _element.Bullet, _element.NumberStyle);

        /// <summary>いちばん外側の項目の記号を置く分だけ、左の余白を広げる（PDF と同じく、記号の幅 + 文字の大きさの半分）。</summary>
        private void UpdatePadding()
        {
            double gutter = 0;
            foreach (var marker in Markers())
            {
                if (marker.Depth == 0) gutter = Math.Max(gutter, Format(marker.Text).WidthIncludingTrailingWhitespace);
            }
            if (gutter > 0) gutter += _box.FontSize * 0.5;
            var padding = new Thickness(_padding.Left + gutter, _padding.Top, _padding.Right, _padding.Bottom);
            if (_box.Padding != padding) _box.Padding = padding;
        }

        private FormattedText Format(string text)
            => new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(_box.FontFamily, _box.FontStyle, FontWeights.Normal, _box.FontStretch), _box.FontSize, MarkerBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected override void OnRender(DrawingContext dc)
        {
            var markers = Markers();
            if (markers.Count == 0) return;
            dc.PushClip(new RectangleGeometry(new Rect(_box.RenderSize)));
            string text = _box.Text;
            double gap = _box.FontSize * 0.5;
            bool missing = false;
            int start = 0;
            for (int line = 0; line < markers.Count && start <= text.Length; line++)
            {
                // 行頭のタブ（入れ子の深さ）の後ろの、最初の文字の左に記号を置く
                int first = start;
                while (first < text.Length && text[first] == '\t') first++;
                var rect = _box.GetRectFromCharacterIndex(first);
                if (rect.IsEmpty) missing = true;
                else
                {
                    var formatted = Format(markers[line].Text);
                    dc.DrawText(formatted, new Point(rect.Left - gap - formatted.WidthIncludingTrailingWhitespace, rect.Top + (rect.Height - formatted.Height) / 2));
                }
                int next = text.IndexOf('\n', start);
                if (next < 0) break;
                start = next + 1;
            }
            dc.Pop();
            // 入力欄の文字の配置がまだ決まっていなければ、決まってから描き直す（見えていない行は位置が分からないので、回数を限る）
            if (missing && _retries < 2)
            {
                _retries++;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(InvalidateVisual));
            }
        }
    }
}
