using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using ChohyoPdf.Designer.Services;
using Microsoft.Win32;

namespace ChohyoPdf.Designer
{
    /// <summary>確認用の値を差し込んだ PDF を、すべてのページ表示する。</summary>
    public partial class PreviewWindow : Window
    {
        private readonly ReportDefinition _report;
        private readonly string? _baseDirectory;
        private readonly bool _runScripts;
        private byte[]? _pdf;
        private List<PageItem> _items = new List<PageItem>();
        private double _zoom = 1;
        private int _rasterVersion;
        private readonly DispatcherTimer _rasterTimer;

        private const double MinZoom = 0.25;
        private const double MaxZoom = 4;
        // 拡大したときに描き直す画像の倍率の上限（これより大きくすると、ページが多いときにメモリを使いすぎる）
        private const double MaxRasterScale = 2;

        public static readonly RoutedCommand ZoomInCommand = new RoutedCommand();
        public static readonly RoutedCommand ZoomOutCommand = new RoutedCommand();
        public static readonly RoutedCommand ZoomResetCommand = new RoutedCommand();

        /// <param name="runScripts">差し込み項目の C# の式を実行する（信頼できるデザインのときだけ）。</param>
        public PreviewWindow(ReportDefinition report, string? baseDirectory, bool runScripts)
        {
            InitializeComponent();
            _report = report;
            _baseDirectory = baseDirectory;
            _runScripts = runScripts;
            // 倍率を変えている間は今の画像を拡大・縮小して見せ、止まってから拡大後の大きさで描き直す
            _rasterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _rasterTimer.Tick += async (s, e) =>
            {
                _rasterTimer.Stop();
                await RasterizeAsync();
            };
            Loaded += async (s, e) => await RenderAsync();
        }

        /// <summary>プレビューのページ。DisplayWidth は、倍率を掛けた表示の幅（DIP）。</summary>
        private sealed class PageItem : INotifyPropertyChanged
        {
            private BitmapSource _image = null!;
            private double _displayWidth;

            public BitmapSource Image
            {
                get => _image;
                set { _image = value; OnChanged(nameof(Image)); }
            }

            /// <summary>倍率が 100% のときの、ページの幅（DIP）。</summary>
            public double Width { get; set; }

            public double DisplayWidth
            {
                get => _displayWidth;
                set { _displayWidth = value; OnChanged(nameof(DisplayWidth)); }
            }

            public string Label { get; set; } = "";

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task RenderAsync()
        {
            var warnings = new List<string>();
            try
            {
                double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                var (pdf, images) = await Task.Run(async () =>
                {
                    byte[] bytes = ReportRenderer.RenderSample(_report, _baseDirectory, warnings, null, _runScripts).ToArray();
                    // ページごとに用紙の大きさが違ってもよいように、実寸で描く
                    var pages = await PdfRasterizer.RenderActualSizeAsync(bytes, dpi);
                    return (bytes, pages);
                });
                _pdf = pdf;
                _items = images.Select((p, i) => new PageItem { Image = p.Image, Width = p.WidthDip, DisplayWidth = p.WidthDip * _zoom, Label = (i + 1) + " / " + images.Count }).ToList();
                Pages.ItemsSource = _items;
                if (_zoom > 1) _rasterTimer.Start();
                Status.Text = images.Count + " ページ（" + (pdf.Length / 1024.0).ToString("#,0.0") + " KB）";
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is InvalidDataException || ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Status.Text = "PDF を作れません: " + ex.Message;
            }
            if (warnings.Count > 0)
            {
                WarningList.ItemsSource = warnings;
                WarningPanel.Visibility = Visibility.Visible;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_pdf == null) return;
            var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = (_report.Name.Length > 0 ? _report.Name : "文書") + ".pdf" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllBytes(dialog.FileName, _pdf);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "保存できません。\n" + ex.Message, "プレビュー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ---- 拡大・縮小 ----

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomAtCenter(_zoom * 1.25);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomAtCenter(_zoom / 1.25);

        private void ZoomReset_Click(object sender, RoutedEventArgs e) => ZoomAtCenter(1);

        /// <summary>ページの幅が、画面に収まる倍率にする。</summary>
        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;
            // ページのまわりの枠と、左右の余白の分を除く
            double width = _items.Max(p => p.Width) + 2;
            ZoomAtCenter((PageScroll.ViewportWidth - 40) / width);
        }

        /// <summary>Ctrl ＋ ホイールで、マウスの位置を中心に拡大・縮小する（ふつうのホイールは、上下にスクロールする）。</summary>
        private void PageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            SetZoom(_zoom * factor, e.GetPosition(PageScroll));
            e.Handled = true;
        }

        private void ZoomAtCenter(double zoom)
            => SetZoom(zoom, new Point(PageScroll.ViewportWidth / 2, PageScroll.ViewportHeight / 2));

        /// <summary>倍率を変える。anchor（スクロール領域の上の位置）にある場所が、変えたあとも同じ位置に来るようにする。</summary>
        private void SetZoom(double zoom, Point anchor)
        {
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, Math.Round(zoom, 2)));
            if (Math.Abs(zoom - _zoom) < 1e-9) return;
            // 内容全体のうち、anchor がある場所の割合を覚えておく
            double fx = PageScroll.ExtentWidth > 0 ? (PageScroll.HorizontalOffset + anchor.X) / PageScroll.ExtentWidth : 0;
            double fy = PageScroll.ExtentHeight > 0 ? (PageScroll.VerticalOffset + anchor.Y) / PageScroll.ExtentHeight : 0;
            _zoom = zoom;
            foreach (var item in _items) item.DisplayWidth = item.Width * zoom;
            ZoomLabel.Content = Math.Round(zoom * 100) + "%";
            PageScroll.UpdateLayout();
            PageScroll.ScrollToHorizontalOffset(fx * PageScroll.ExtentWidth - anchor.X);
            PageScroll.ScrollToVerticalOffset(fy * PageScroll.ExtentHeight - anchor.Y);
            _rasterVersion++;
            _rasterTimer.Stop();
            _rasterTimer.Start();
        }

        /// <summary>今の倍率の大きさで、ページの画像を描き直す（拡大したときに、ぼやけないように）。</summary>
        private async Task RasterizeAsync()
        {
            if (_pdf == null || _items.Count == 0) return;
            double scale = Math.Max(1, Math.Min(MaxRasterScale, _zoom));
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX * scale;
            int version = ++_rasterVersion;
            var items = _items;
            byte[] pdf = _pdf;
            try
            {
                var images = await PdfRasterizer.RenderActualSizeAsync(pdf, dpi);
                // 描いている間に倍率が変わったり、プレビューが作り直されたときは使わない
                if (version != _rasterVersion || items != _items) return;
                for (int i = 0; i < items.Count && i < images.Count; i++) items[i].Image = images[i].Image;
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is InvalidDataException || ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
            {
                // 描き直せなくても、今の画像のまま見られる
            }
        }
    }
}
