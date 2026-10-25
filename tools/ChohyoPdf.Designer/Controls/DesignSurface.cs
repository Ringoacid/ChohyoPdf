using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.ViewModels;
using ModelRect = ChohyoPdf.Designer.Model.Rect;

namespace ChohyoPdf.Designer.Controls
{
    using DashStyle = System.Windows.Media.DashStyle;
    using ScrollViewer = System.Windows.Controls.ScrollViewer;

    /// <summary>
    /// 用紙を表示して、部品を選択・移動・変形・作成する画面。
    /// ページの見た目は ChohyoPdf で描いた画像をそのまま表示し、選択枠などだけをこのコントロールが描く。
    /// ページをつなげて表示するときは、すべてのページを縦に並べる。部品を編集できるのは、編集しているページ（クリックしたページ）だけ。
    /// </summary>
    public sealed class DesignSurface : FrameworkElement
    {
        private const double PageMargin = 32;
        private const double PageGap = 20;
        private const double HandleSize = 7;

        private static readonly Brush SelectionBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5)));
        private static readonly Pen SelectionPen = Freeze(new Pen(SelectionBrush, 1));
        private static readonly Pen OutlinePen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(110, 120, 120, 120)), 1) { DashStyle = new DashStyle(new[] { 3.0, 3.0 }, 0) });
        private static readonly Pen GridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(20, 0x3F, 0x3F, 0x46)), 1));
        private static readonly Pen MajorGridPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(40, 0x3F, 0x3F, 0x46)), 1));
        private static readonly Brush CellSelectionBrush = Freeze(new SolidColorBrush(Color.FromArgb(50, 0x4F, 0x46, 0xE5)));
        private static readonly Brush DetailBrush = Freeze(new SolidColorBrush(Color.FromArgb(28, 0, 160, 80)));
        private static readonly Brush DetailLabelBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 120, 60)));
        private static readonly Brush MarqueeBrush = Freeze(new SolidColorBrush(Color.FromArgb(30, 0x4F, 0x46, 0xE5)));
        private static readonly Pen PreviewPen = Freeze(new Pen(SelectionBrush, 1) { DashStyle = new DashStyle(new[] { 4.0, 2.0 }, 0) });
        private static readonly Pen CurrentPagePen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(140, 0x4F, 0x46, 0xE5)), 2));
        private static readonly Brush ShadowBrush = Freeze(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)));

        private enum DragMode
        {
            None,
            Moving,
            Resizing,
            Creating,
            Marquee,
            SelectingCells,
        }

        private MainViewModel? _vm;
        private DragMode _mode;
        private Point _startMm, _currentMm;
        private bool _dragStarted;
        private bool _releasingCapture;
        // 右ボタンを押している（ドラッグすれば表示位置を動かし、動かさずに離せばメニューを開く）。_panning はドラッグを始めた
        private bool _rightDown, _panning;
        // 右ボタンを押したときのマウスの位置（スクロールしても動かない、スクロールの領域の上の位置）と、そのときのスクロールの量
        private Point _panStart;
        private Vector _panOffset;
        private int _handle = -1;
        private CellRange _anchorCell;
        // Shift / Ctrl ＋クリックで選択済みの部品を押した（ドラッグしないで離したら、選択から外す）
        private ReportElement? _toggleOnRelease;
        private readonly List<(ReportElement Element, double[] Original)> _originals = new List<(ReportElement, double[])>();
        // 動かす前の部品の複製（重ねる画像ができるまで、選択枠をこの位置に描く）
        private readonly Dictionary<ReportElement, ReportElement> _snapshots = new Dictionary<ReportElement, ReportElement>();

        // ページの配置（キーはページの番号。全ページ共通の部品を編集しているときは、そのキーの 1 ページだけ）
        private readonly List<(int Key, System.Windows.Rect Rect)> _layout = new List<(int, System.Windows.Rect)>();
        // 編集しているページの左上（このコントロール上の位置）。mm との変換はこの位置を基準にする
        private Point _origin = new Point(PageMargin, PageMargin);
        private Size _extent;

        public DesignSurface()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = false;
            AllowDrop = true;
        }

        /// <summary>選択している部品のテキストを編集したい（ダブルクリック、Enter、F2）。</summary>
        public event Action? EditTextRequested;

        /// <summary>右クリックなどでメニューを開きたい（位置は mm、hit は右クリックした部品）。</summary>
        public event Action<Point, ReportElement?>? ContextMenuRequested;

        /// <summary>ドラッグ＆ドロップで差し込み欄を受け取るときのデータの形式（値は項目の名前）。</summary>
        public const string FieldDataFormat = "ChohyoPdfDesigner.Field";

        public MainViewModel? ViewModel
        {
            get => _vm;
            set
            {
                if (_vm != null)
                {
                    _vm.SurfaceInvalidated -= OnInvalidated;
                    _vm.PropertyChanged -= OnViewModelPropertyChanged;
                }
                _vm = value;
                if (_vm != null)
                {
                    _vm.SurfaceInvalidated += OnInvalidated;
                    _vm.PropertyChanged += OnViewModelPropertyChanged;
                }
                UpdatePixelsPerMm();
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        private void OnInvalidated()
        {
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Zoom) || e.PropertyName == nameof(MainViewModel.Report) || e.PropertyName == nameof(MainViewModel.CurrentPage)
                || e.PropertyName == nameof(MainViewModel.IsContinuous) || e.PropertyName == nameof(MainViewModel.CurrentPageWidth) || e.PropertyName == nameof(MainViewModel.CurrentPageHeight))
            {
                UpdatePixelsPerMm();
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            UpdatePixelsPerMm();
        }

        private void UpdatePixelsPerMm()
        {
            if (_vm == null) return;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            _vm.DpiScale = dpi;
            _vm.PixelsPerMm = Scale * dpi;
        }

        // ---- 座標 ----

        /// <summary>1 mm あたりの DIP。</summary>
        private double Scale => (_vm?.Zoom ?? 1) * 96 / 25.4;

        private ReportDefinition? Report => _vm?.Report;

        private Point ToView(double xMm, double yMm) => new Point(_origin.X + xMm * Scale, _origin.Y + yMm * Scale);

        private System.Windows.Rect ToView(ModelRect r)
            => new System.Windows.Rect(ToView(r.X, r.Y), new Size(Math.Max(0, r.Width * Scale), Math.Max(0, r.Height * Scale)));

        private Point ToMm(Point view) => new Point((view.X - _origin.X) / Scale, (view.Y - _origin.Y) / Scale);

        /// <summary>このコントロール上の位置を、編集しているページの mm にする。</summary>
        public Point ViewToMm(Point view)
        {
            EnsureLayout();
            return ToMm(view);
        }

        /// <summary>編集しているページの mm の矩形を、このコントロール上の矩形にする。</summary>
        public System.Windows.Rect MmToView(ModelRect r)
        {
            EnsureLayout();
            return ToView(r);
        }

        // ---- ページの配置 ----

        /// <summary>ページの配置を求め直す（ページの数・用紙・倍率・表示のしかたが変わっても合うように、使う前に毎回呼ぶ）。</summary>
        private void EnsureLayout()
        {
            _layout.Clear();
            _origin = new Point(PageMargin, PageMargin);
            _extent = new Size(0, 0);
            if (_vm == null) return;
            double s = Scale;
            int current = _vm.CurrentPageKey;
            if (!_vm.IsContinuous)
            {
                var (w, h) = _vm.PageSizeOf(current);
                _layout.Add((current, new System.Windows.Rect(PageMargin, PageMargin, w * s, h * s)));
            }
            else
            {
                // 幅の違うページは、中央に揃えて並べる
                int count = _vm.Report.Pages.Count;
                double maxWidth = 0;
                for (int k = 0; k < count; k++) maxWidth = Math.Max(maxWidth, _vm.PageSizeOf(k).Width);
                double y = PageMargin;
                for (int k = 0; k < count; k++)
                {
                    var (w, h) = _vm.PageSizeOf(k);
                    _layout.Add((k, new System.Windows.Rect(PageMargin + (maxWidth - w) * s / 2, y, w * s, h * s)));
                    y += h * s + PageGap;
                }
            }
            double right = 0, bottom = 0;
            foreach (var (key, rect) in _layout)
            {
                if (key == current) _origin = rect.TopLeft;
                right = Math.Max(right, rect.Right);
                bottom = Math.Max(bottom, rect.Bottom);
            }
            _extent = new Size(right + PageMargin, bottom + PageMargin);
        }

        /// <summary>編集しているページの矩形（このコントロール上）。</summary>
        private System.Windows.Rect CurrentPageRect()
        {
            int current = _vm?.CurrentPageKey ?? 0;
            foreach (var (key, rect) in _layout)
            {
                if (key == current) return rect;
            }
            return System.Windows.Rect.Empty;
        }

        /// <summary>ページの矩形（このコントロール上）。表示していないページなら null。</summary>
        public System.Windows.Rect? PageRectOf(int key)
        {
            EnsureLayout();
            foreach (var (k, rect) in _layout)
            {
                if (k == key) return rect;
            }
            return null;
        }

        /// <summary>
        /// ページをつなげて表示しているとき、view の位置にあるページを編集するページにする（Word で、クリックしたページに移るように）。
        /// </summary>
        private void ActivatePageAt(Point view)
        {
            if (_vm == null || !_vm.IsContinuous) return;
            foreach (var (key, rect) in _layout)
            {
                if (key == _vm.CurrentPageKey || !rect.Contains(view)) continue;
                _vm.CurrentPageIndex = key;
                EnsureLayout();
                return;
            }
        }

        /// <summary>スクロールして見えている範囲（このコントロール上）。見えているページを、ViewModel に知らせて描かせる。</summary>
        public void SetViewport(System.Windows.Rect viewport)
        {
            if (_vm == null) return;
            EnsureLayout();
            // 少し先のページも描いておく
            var area = viewport;
            area.Inflate(0, viewport.Height / 2);
            _vm.SetVisiblePages(_layout.Where(l => l.Rect.IntersectsWith(area)).Select(l => l.Key));
        }

        private double Snap(double mm, bool snap)
        {
            if (!snap || _vm == null || !_vm.SnapToGrid) return Math.Round(mm, 2);
            double g = _vm.GridSize;
            return Math.Round(Math.Round(mm / g) * g, 4);
        }

        private Point Snap(Point mm, bool snap) => new Point(Snap(mm.X, snap), Snap(mm.Y, snap));

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureLayout();
            return _extent;
        }

        // ---- 描画 ----

        protected override void OnRender(DrawingContext dc)
        {
            var report = Report;
            if (report == null || _vm == null) return;
            EnsureLayout();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            foreach (var (key, rect) in _layout)
            {
                dc.DrawRectangle(ShadowBrush, null, new System.Windows.Rect(rect.X + 3, rect.Y + 3, rect.Width, rect.Height));
                dc.DrawRectangle(Brushes.White, null, rect);
                if (_vm.TryGetMovePreview(key, out var background, out var foreground, out var parts))
                {
                    // 部品を動かしている間は、動かす部品の画像をずらしたり伸び縮みさせたりして重ねる（ページの外と、指定の範囲の外は描かない）。
                    // 用紙の上の位置 p（mm）を p × scale + offset に移す変換を、このコントロール上の位置に直して使う
                    dc.DrawImage(background, rect);
                    foreach (var (image, area, part) in parts)
                    {
                        var clip = rect;
                        if (part.Clip is ModelRect partClip) clip.Intersect(ToView(partClip));
                        if (clip.IsEmpty || clip.Width <= 0 || clip.Height <= 0) continue;
                        var map = part.Map;
                        dc.PushClip(new RectangleGeometry(clip));
                        dc.PushTransform(new MatrixTransform(map.ScaleX, 0, 0, map.ScaleY,
                            rect.X * (1 - map.ScaleX) + map.OffsetX * Scale, rect.Y * (1 - map.ScaleY) + map.OffsetY * Scale));
                        dc.DrawImage(image, area is ModelRect a ? ToView(a) : rect);
                        dc.Pop();
                        dc.Pop();
                    }
                    if (foreground != null) dc.DrawImage(foreground, rect);
                    continue;
                }
                var bitmap = _vm.GetPageBitmap(key);
                if (bitmap != null) dc.DrawImage(bitmap, rect);
            }
            var page = CurrentPageRect();
            // ページを並べているときは、編集しているページを枠で示す
            if (_layout.Count > 1) dc.DrawRectangle(null, CurrentPagePen, page);

            if (_vm.ShowGrid) DrawGrid(dc, page, _vm.CurrentPageWidth, _vm.CurrentPageHeight);

            foreach (var element in _vm.Elements)
            {
                if (Display(element) is TableElement table)
                {
                    DrawTableOverlay(dc, table);
                    if (_vm.ShowOutlines && table.BorderColor == null) DrawTableGrid(dc, table);
                }
                if (_vm.ShowOutlines && element is not LineElement && !_vm.Selection.Contains(element))
                    dc.DrawRectangle(null, OutlinePen, ToView(element.Bounds));
            }

            if (_vm.CellEditor != null && _vm.Selection.Contains(_vm.CellEditor.Table))
            {
                var rect = CellRangeRect((TableElement)Display(_vm.CellEditor.Table), _vm.CellEditor.Range);
                dc.DrawRectangle(CellSelectionBrush, new Pen(SelectionBrush, 2), ToView(rect));
            }

            foreach (var element in _vm.Selection) DrawSelection(dc, Display(element), _vm.Selection.Count == 1);

            if (_mode == DragMode.Creating && _dragStarted)
            {
                if (_vm.Tool == Tool.Line) dc.DrawLine(PreviewPen, ToView(_startMm.X, _startMm.Y), ToView(_currentMm.X, _currentMm.Y));
                else dc.DrawRectangle(null, PreviewPen, ToView(ModelRect.FromPoints(_startMm.X, _startMm.Y, _currentMm.X, _currentMm.Y)));
            }
            if (_mode == DragMode.Marquee && _dragStarted)
                dc.DrawRectangle(MarqueeBrush, SelectionPen, ToView(ModelRect.FromPoints(_startMm.X, _startMm.Y, _currentMm.X, _currentMm.Y)));
        }

        private void DrawGrid(DrawingContext dc, System.Windows.Rect page, double pageWidth, double pageHeight)
        {
            double g = _vm!.GridSize;
            // 線の間隔が狭すぎるときは間引く
            double step = g;
            while (step * Scale < 6) step *= 2;
            dc.PushClip(new RectangleGeometry(page));
            var minor = new StreamGeometry();
            var major = new StreamGeometry();
            using (var minorCtx = minor.Open())
            using (var majorCtx = major.Open())
            {
                for (double x = step; x < pageWidth; x += step)
                {
                    bool isMajor = Math.Abs(x / 10 - Math.Round(x / 10)) < 1e-6;
                    var ctx = isMajor ? majorCtx : minorCtx;
                    double vx = Math.Round(ToView(x, 0).X) + 0.5;
                    ctx.BeginFigure(new Point(vx, page.Top), false, false);
                    ctx.LineTo(new Point(vx, page.Bottom), true, false);
                }
                for (double y = step; y < pageHeight; y += step)
                {
                    bool isMajor = Math.Abs(y / 10 - Math.Round(y / 10)) < 1e-6;
                    var ctx = isMajor ? majorCtx : minorCtx;
                    double vy = Math.Round(ToView(0, y).Y) + 0.5;
                    ctx.BeginFigure(new Point(page.Left, vy), false, false);
                    ctx.LineTo(new Point(page.Right, vy), true, false);
                }
            }
            minor.Freeze();
            major.Freeze();
            if (step < 10) dc.DrawGeometry(null, GridPen, minor);
            dc.DrawGeometry(null, MajorGridPen, major);
            dc.Pop();
        }

        /// <summary>くり返し行の範囲に色を付けて、一覧の名前を表示する。</summary>
        private void DrawTableOverlay(DrawingContext dc, TableElement t)
        {
            if (!t.HasDetail) return;
            double top = t.Y + t.Rows.Sum(r => r.Height);
            var area = ToView(new ModelRect(t.X, top, t.Width, t.DetailRow.Height * t.DetailRowCount));
            dc.DrawRectangle(DetailBrush, null, area);
            var text = new FormattedText("「" + t.DetailSource + "」をくり返す × " + t.DetailRowCount + " 行", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Yu Gothic UI"), 11, DetailLabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(area.Right + 4, area.Top));
        }

        /// <summary>罫線のない表は、セルの区切りを点線で示す。</summary>
        private void DrawTableGrid(DrawingContext dc, TableElement t)
        {
            var owners = t.GetOwnerMap();
            for (int r = 0; r < t.EditableRowCount; r++)
            {
                for (int c = 0; c < t.Columns.Count; c++)
                {
                    if (owners[r, c] != (r, c)) continue;
                    dc.DrawRectangle(null, OutlinePen, ToView(CellRangeRect(t, new CellRange(r, c, r, c))));
                }
            }
        }

        private void DrawSelection(DrawingContext dc, ReportElement element, bool withHandles)
        {
            if (element is LineElement line)
            {
                dc.DrawLine(new Pen(SelectionBrush, 1) { DashStyle = DashStyles.Dash }, ToView(line.X1, line.Y1), ToView(line.X2, line.Y2));
                if (!withHandles) return;
                foreach (var p in GetHandles(element)) DrawHandle(dc, p);
                return;
            }
            var rect = ToView(element.Bounds);
            dc.DrawRectangle(null, SelectionPen, rect);
            if (!withHandles) return;
            foreach (var p in GetHandles(element)) DrawHandle(dc, p);
            if (element is TableElement) DrawMoveGrip(dc, rect);
        }

        private static void DrawHandle(DrawingContext dc, Point p)
        {
            dc.DrawRectangle(Brushes.White, SelectionPen, new System.Windows.Rect(p.X - HandleSize / 2, p.Y - HandleSize / 2, HandleSize, HandleSize));
        }

        /// <summary>表を動かすためのつまみ（表の中をクリックするとセルの選択になるため）。</summary>
        private static System.Windows.Rect MoveGripRect(System.Windows.Rect table) => new System.Windows.Rect(table.Left - 18, table.Top - 18, 16, 16);

        private void DrawMoveGrip(DrawingContext dc, System.Windows.Rect table)
        {
            var grip = MoveGripRect(table);
            dc.DrawRectangle(SelectionBrush, null, grip);
            var text = new FormattedText("✥", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Symbol"), 12, Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(grip.Left + (grip.Width - text.Width) / 2, grip.Top + (grip.Height - text.Height) / 2));
        }

        /// <summary>ハンドルの位置。矩形は左上から時計回りの 8 個、直線は両端の 2 個。</summary>
        private List<Point> GetHandles(ReportElement element)
        {
            if (element is LineElement line) return new List<Point> { ToView(line.X1, line.Y1), ToView(line.X2, line.Y2) };
            var r = ToView(element.Bounds);
            double cx = (r.Left + r.Right) / 2, cy = (r.Top + r.Bottom) / 2;
            return new List<Point>
            {
                r.TopLeft, new Point(cx, r.Top), r.TopRight, new Point(r.Right, cy),
                r.BottomRight, new Point(cx, r.Bottom), r.BottomLeft, new Point(r.Left, cy),
            };
        }

        // ---- 表のセル ----

        /// <summary>セルの範囲の矩形（mm）。くり返し行は 1 ページ分のくり返しの範囲全体。</summary>
        internal static ModelRect CellRangeRect(TableElement t, CellRange range)
        {
            double x = t.X, y = t.Y;
            for (int c = 0; c < range.Left; c++) x += t.Columns[c].Width;
            double w = 0;
            for (int c = range.Left; c <= range.Right && c < t.Columns.Count; c++) w += t.Columns[c].Width;
            for (int r = 0; r < range.Top && r < t.Rows.Count; r++) y += t.Rows[r].Height;
            double h = 0;
            for (int r = range.Top; r <= range.Bottom; r++)
            {
                if (r < t.Rows.Count) h += t.Rows[r].Height;
                else if (t.HasDetail) h += t.DetailRow.Height;
            }
            return new ModelRect(x, y, w, h);
        }

        /// <summary>mm の位置にあるセル（くり返しの範囲ならくり返し行）。</summary>
        private static (int Row, int Column) HitCell(TableElement t, Point mm)
        {
            int column = t.Columns.Count - 1;
            double x = t.X;
            for (int c = 0; c < t.Columns.Count; c++)
            {
                x += t.Columns[c].Width;
                if (mm.X < x)
                {
                    column = c;
                    break;
                }
            }
            int row = t.EditableRowCount - 1;
            double y = t.Y;
            for (int r = 0; r < t.Rows.Count; r++)
            {
                y += t.Rows[r].Height;
                if (mm.Y < y)
                {
                    row = r;
                    break;
                }
            }
            return (Math.Max(0, row), Math.Max(0, column));
        }

        // ---- 当たり判定 ----

        private ReportElement? HitElement(Point mm)
        {
            if (Report == null) return null;
            double tolerance = 4 / Scale;
            var elements = _vm!.Elements;
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var e = elements[i];
                if (e is LineElement line)
                {
                    if (DistanceToSegment(mm, line.X1, line.Y1, line.X2, line.Y2) <= tolerance) return e;
                    continue;
                }
                var b = e.Bounds;
                if (mm.X >= b.X - tolerance && mm.X <= b.Right + tolerance && mm.Y >= b.Y - tolerance && mm.Y <= b.Bottom + tolerance) return e;
            }
            return null;
        }

        private static double DistanceToSegment(Point p, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double len2 = dx * dx + dy * dy;
            double t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - x1) * dx + (p.Y - y1) * dy) / len2));
            double px = x1 + t * dx - p.X, py = y1 + t * dy - p.Y;
            return Math.Sqrt(px * px + py * py);
        }

        private int HitHandle(Point view)
        {
            if (_vm?.PrimarySelection == null) return -1;
            var handles = GetHandles(_vm.PrimarySelection);
            for (int i = 0; i < handles.Count; i++)
            {
                if (Math.Abs(view.X - handles[i].X) <= HandleSize && Math.Abs(view.Y - handles[i].Y) <= HandleSize) return i;
            }
            return -1;
        }

        private bool HitMoveGrip(Point view)
            => _vm?.PrimarySelection is TableElement t && MoveGripRect(ToView(t.Bounds)).Contains(view);

        /// <summary>選択している表の枠線の近く（ここをドラッグすると表を動かせる）。</summary>
        private bool HitTableEdge(TableElement t, Point view)
        {
            var r = ToView(t.Bounds);
            var outer = r;
            outer.Inflate(5, 5);
            var inner = r;
            inner.Inflate(-3, -3);
            return outer.Contains(view) && (inner.IsEmpty || !inner.Contains(view));
        }

        // ---- マウス ----

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_vm == null || _rightDown) return;
            Focus();
            PointerDown(e.GetPosition(this), Keyboard.Modifiers, e.ClickCount);
            if (_mode != DragMode.None) CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_rightDown)
            {
                if (Scroller is ScrollViewer scroller) RightPointerMove(e.GetPosition(scroller));
                return;
            }
            PointerMove(e.GetPosition(this), Keyboard.Modifiers, e.LeftButton == MouseButtonState.Pressed);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_rightDown) return;
            // 画像を作るときはファイルを選ぶ画面を開くので、先にキャプチャを解放する
            _releasingCapture = true;
            if (IsMouseCaptured) ReleaseMouseCapture();
            _releasingCapture = false;
            PointerUp(Keyboard.Modifiers);
        }

        /// <summary>ボタンを押した（view はこのコントロール上の位置）。</summary>
        internal void PointerDown(Point view, ModifierKeys modifiers, int clickCount)
        {
            if (_vm == null) return;
            EnsureLayout();
            ActivatePageAt(view);
            var mm = ToMm(view);
            bool add = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
            _startMm = _currentMm = mm;
            _dragStarted = false;
            _toggleOnRelease = null;
            _mode = DragMode.None;

            // Word のヘッダー・フッターのように、全ページ共通の部品をダブルクリックするとその編集に移り、
            // 編集中に部品のない所をダブルクリックするとページの編集に戻る
            if (clickCount == 2 && _vm.Tool == Tool.Select && HitElement(mm) == null)
            {
                if (_vm.IsEditingCommon)
                {
                    _vm.IsEditingCommon = false;
                    return;
                }
                if (_vm.HitCommonElement(mm.X, mm.Y) is ReportElement common)
                {
                    _vm.EditCommon(common);
                    return;
                }
            }

            if (clickCount == 2 && _vm.PrimarySelection != null && (_vm.PrimarySelection is TextElement || _vm.PrimarySelection is TableElement))
            {
                // 表は、ダブルクリックしたセルを編集する（1 回目のクリックで表を選んだだけのときは、セルを選んでいない）
                if (_vm.PrimarySelection is TableElement table && table.EditableRowCount > 0 && table.Columns.Count > 0 && HitElement(mm) == table)
                {
                    var (row, column) = HitCell(table, mm);
                    var (r, c) = table.GetOwner(row, column);
                    if (_vm.CellEditor?.Table != table || !_vm.CellEditor.Range.Contains(r, c)) _vm.SelectCells(table, new CellRange(r, c, r, c));
                }
                EditTextRequested?.Invoke();
                return;
            }

            if (_vm.Tool != Tool.Select)
            {
                _mode = DragMode.Creating;
                _startMm = _currentMm = Snap(mm, true);
            }
            else if ((_handle = HitHandle(view)) >= 0)
            {
                _mode = DragMode.Resizing;
                RememberOriginals(new[] { _vm.PrimarySelection! });
            }
            else if (HitMoveGrip(view) || (_vm.PrimarySelection is TableElement edgeTable && HitTableEdge(edgeTable, view)))
            {
                _mode = DragMode.Moving;
                RememberOriginals(_vm.Selection);
            }
            else
            {
                var hit = HitElement(mm);
                if (hit == null)
                {
                    if (!add) _vm.Select(null);
                    _mode = DragMode.Marquee;
                }
                else if (hit is TableElement table && _vm.Selection.Count == 1 && _vm.PrimarySelection == table && table.EditableRowCount > 0 && table.Columns.Count > 0)
                {
                    // 選択済みの表の中をクリックしたら、セルを選ぶ
                    var (row, column) = HitCell(table, mm);
                    bool extend = (modifiers & ModifierKeys.Shift) != 0 && _vm.CellEditor?.Table == table;
                    if (!extend) _anchorCell = new CellRange(row, column, row, column);
                    _vm.SelectCells(table, new CellRange(_anchorCell.Top, _anchorCell.Left, row, column));
                    _mode = DragMode.SelectingCells;
                }
                else
                {
                    if (!add)
                    {
                        if (!_vm.Selection.Contains(hit)) _vm.Select(hit);
                    }
                    else if (_vm.Selection.Contains(hit)) _toggleOnRelease = hit; // ドラッグ（移動・コピー）かもしれないので、離すときに決める
                    else _vm.Select(hit, true);
                    _mode = _vm.Selection.Contains(hit) ? DragMode.Moving : DragMode.None;
                    if (_mode == DragMode.Moving) RememberOriginals(_vm.Selection);
                }
            }
            if (_mode == DragMode.Moving || _mode == DragMode.Resizing)
            {
                _vm.BeginInteraction();
                // 動かすときに重ねる画像は、ボタンを押したときから用意する（部品を選んだときに先に描いてあれば、すぐに使える）
                if (_mode == DragMode.Moving) _vm.BeginMovePreview(_vm.Selection.ToList(), false);
                else _vm.BeginMovePreview(new[] { _originals[0].Element }, true);
            }
            InvalidateVisual();
        }

        /// <summary>ポインターを動かした。</summary>
        internal void PointerMove(Point view, ModifierKeys modifiers, bool pressed)
        {
            if (_vm == null) return;
            EnsureLayout();
            var mm = ToMm(view);
            _vm.CursorText = string.Format(CultureInfo.InvariantCulture, "X {0:0.0} mm, Y {1:0.0} mm", mm.X, mm.Y);

            if (_mode == DragMode.None || !pressed)
            {
                UpdateCursor(view, mm);
                return;
            }
            bool snap = (modifiers & ModifierKeys.Alt) == 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;
            if (!_dragStarted && (Math.Abs(mm.X - _startMm.X) * Scale > 3 || Math.Abs(mm.Y - _startMm.Y) * Scale > 3))
            {
                _dragStarted = true;
                // ドラッグを終えるまで、プロパティ欄の値は書き換えない（書き換えるたびに日本語入力の処理で止まることがある）
                if (_mode == DragMode.Moving || _mode == DragMode.Resizing) _vm.HoldPropertyPanel();
                // Ctrl を押しながら動かし始めたら、元の位置に部品を残して、コピーのほうを動かす
                if (_mode == DragMode.Moving && (modifiers & ModifierKeys.Control) != 0)
                {
                    // コピーを動かすので、重ねる画像も描き直す（元の部品は奥の画像に入る）
                    _vm.CopySelectionInPlace();
                    RememberOriginals(_vm.Selection);
                    _vm.BeginMovePreview(_vm.Selection.ToList(), false);
                }
                _toggleOnRelease = null;
            }
            if (!_dragStarted) return;

            switch (_mode)
            {
                case DragMode.Moving:
                    ApplyMove(mm, snap, shift);
                    break;
                case DragMode.Resizing:
                    ApplyResize(mm, snap, shift);
                    break;
                case DragMode.Creating:
                    _currentMm = Snap(mm, snap);
                    if (_vm.Tool == Tool.Line && shift) _currentMm = ConstrainLine(_startMm, _currentMm);
                    break;
                case DragMode.Marquee:
                    _currentMm = mm;
                    break;
                case DragMode.SelectingCells:
                    if (_vm.CellEditor?.Table is TableElement table)
                    {
                        var (row, column) = HitCell(table, mm);
                        _vm.SelectCells(table, new CellRange(_anchorCell.Top, _anchorCell.Left, row, column));
                    }
                    break;
            }
            // ドラッグしている間はプロパティ欄の値を書き換えないので、部品の位置と大きさはステータスバーに出す
            if ((_mode == DragMode.Moving || _mode == DragMode.Resizing) && _vm.PrimarySelection is ReportElement primary)
                _vm.CursorText = BoundsText(primary);
            InvalidateVisual();
        }

        private static string BoundsText(ReportElement element)
        {
            if (element is LineElement line)
                return string.Format(CultureInfo.InvariantCulture, "始点 {0:0.0}, {1:0.0} mm　終点 {2:0.0}, {3:0.0} mm", line.X1, line.Y1, line.X2, line.Y2);
            var b = element.Bounds;
            return string.Format(CultureInfo.InvariantCulture, "X {0:0.0} mm, Y {1:0.0} mm　幅 {2:0.0} mm, 高さ {3:0.0} mm", b.X, b.Y, b.Width, b.Height);
        }

        /// <summary>ボタンを離した。</summary>
        internal void PointerUp(ModifierKeys modifiers)
        {
            if (_vm == null) return;
            EnsureLayout();
            var mode = _mode;
            _mode = DragMode.None;
            switch (mode)
            {
                case DragMode.Creating:
                    {
                        var end = _dragStarted ? _currentMm : _startMm;
                        _vm.CreateElement(_vm.Tool, _startMm.X, _startMm.Y, end.X, end.Y);
                        break;
                    }
                case DragMode.Marquee:
                    if (_dragStarted && Report != null)
                    {
                        var area = ModelRect.FromPoints(_startMm.X, _startMm.Y, _currentMm.X, _currentMm.Y);
                        bool add = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
                        _vm.SelectMany(_vm.Elements.Where(x => area.IntersectsWith(x.Bounds)), add);
                    }
                    break;
                case DragMode.Moving:
                case DragMode.Resizing:
                    if (_toggleOnRelease != null) _vm.Select(_toggleOnRelease, true);
                    _vm.EndInteraction();
                    _vm.EndMovePreview();
                    _vm.ReleasePropertyPanel();
                    break;
            }
            _toggleOnRelease = null;
            _dragStarted = false;
            InvalidateVisual();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (_releasingCapture) return;
            // ボタンを離す前にキャプチャを失った（別のウィンドウが前面に出たなど）
            if (_mode == DragMode.Moving || _mode == DragMode.Resizing)
            {
                _vm?.EndInteraction();
                _vm?.EndMovePreview();
                _vm?.ReleasePropertyPanel();
            }
            _mode = DragMode.None;
            _dragStarted = false;
            if (_panning) ClearValue(CursorProperty);
            _rightDown = _panning = false;
            _toggleOnRelease = null;
            InvalidateVisual();
        }

        /// <summary>mm の位置を、このコントロール上の位置にする（動作確認用）。</summary>
        internal Point MmToView(double xMm, double yMm)
        {
            EnsureLayout();
            return ToView(xMm, yMm);
        }

        // ---- 右クリック ----

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (_vm == null || _mode != DragMode.None || Scroller is not ScrollViewer scroller) return;
            Focus();
            RightPointerDown(e.GetPosition(scroller));
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (_vm == null || !_rightDown) return;
            _releasingCapture = true;
            if (IsMouseCaptured) ReleaseMouseCapture();
            _releasingCapture = false;
            RightPointerUp(e.GetPosition(this));
            e.Handled = true;
        }

        /// <summary>デザイン画面をスクロールする領域（なければ null）。</summary>
        private ScrollViewer? Scroller
        {
            get
            {
                DependencyObject? d = this;
                while (d != null && d is not ScrollViewer) d = VisualTreeHelper.GetParent(d);
                return d as ScrollViewer;
            }
        }

        /// <summary>右ボタンを押した（position はスクロールの領域の上の位置）。ドラッグすれば表示位置を動かす。</summary>
        internal void RightPointerDown(Point position)
        {
            _rightDown = true;
            _panning = false;
            _panStart = position;
            var scroller = Scroller;
            _panOffset = scroller == null ? default : new Vector(scroller.HorizontalOffset, scroller.VerticalOffset);
        }

        /// <summary>
        /// 右ボタンを押したまま動かした。少し動かしたら、マウスに紙が付いてくるように表示位置を動かす
        /// （ホイールでの上下、Shift ＋ ホイールでの左右のスクロールを、まとめて行う）。
        /// </summary>
        internal void RightPointerMove(Point position)
        {
            if (!_rightDown || Scroller is not ScrollViewer scroller) return;
            var moved = position - _panStart;
            if (!_panning && Math.Abs(moved.X) <= 4 && Math.Abs(moved.Y) <= 4) return;
            if (!_panning)
            {
                _panning = true;
                Cursor = Cursors.SizeAll;
            }
            scroller.ScrollToHorizontalOffset(_panOffset.X - moved.X);
            scroller.ScrollToVerticalOffset(_panOffset.Y - moved.Y);
        }

        /// <summary>右ボタンを離した（view はこのコントロール上の位置）。表示位置を動かさなかったときは、メニューを開く。</summary>
        internal void RightPointerUp(Point view)
        {
            bool panned = _panning;
            _rightDown = _panning = false;
            if (panned)
            {
                ClearValue(CursorProperty);
                return;
            }
            OpenContextMenu(view);
        }

        /// <summary>右クリックと同じ処理（選び直してからメニューを開く）。</summary>
        internal void OpenContextMenu(Point view)
        {
            if (_vm == null) return;
            PrepareContextMenu(view);
            var mm = ToMm(view);
            ContextMenuRequested?.Invoke(mm, HitElement(mm));
        }

        /// <summary>
        /// 右クリックした位置のものを選ぶ（Office と同じく、選択の外を右クリックしたら選び直す）。
        /// 選択済みの表の中なら、そのセルを選ぶ（選んでいるセルの範囲の中なら、範囲はそのまま）。
        /// </summary>
        internal void PrepareContextMenu(Point view)
        {
            if (_vm == null) return;
            EnsureLayout();
            ActivatePageAt(view);
            var mm = ToMm(view);
            var hit = HitElement(mm);
            if (hit == null)
            {
                _vm.Select(null);
            }
            else if (hit is TableElement table && _vm.Selection.Count == 1 && _vm.PrimarySelection == table && table.EditableRowCount > 0 && table.Columns.Count > 0)
            {
                var (row, column) = HitCell(table, mm);
                if (_vm.CellEditor?.Table != table || !_vm.CellEditor.Range.Contains(row, column))
                {
                    _anchorCell = new CellRange(row, column, row, column);
                    _vm.SelectCells(table, _anchorCell);
                }
            }
            else if (!_vm.Selection.Contains(hit))
            {
                _vm.Select(hit);
            }
            InvalidateVisual();
        }

        // ---- ドラッグ＆ドロップ（データの項目を用紙に置く） ----

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            e.Effects = e.Data.GetDataPresent(FieldDataFormat) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetData(FieldDataFormat) is string name) DropField(name, e.GetPosition(this));
            e.Handled = true;
        }

        /// <summary>
        /// 差し込み欄 {name} を置く。テキストの上なら末尾に足し、表の上ならそのセルに足す。何もない所なら、新しいテキストを作る。
        /// </summary>
        internal void DropField(string name, Point view)
        {
            if (_vm == null) return;
            EnsureLayout();
            ActivatePageAt(view);
            var mm = ToMm(view);
            string field = "{" + name + "}";
            switch (HitElement(mm))
            {
                case TextElement text:
                    text.Text += field;
                    _vm.Select(text);
                    break;
                case TableElement table when table.EditableRowCount > 0 && table.Columns.Count > 0:
                    {
                        var (row, column) = HitCell(table, mm);
                        var (r, c) = table.GetOwner(row, column);
                        var cell = table.GetRow(r)!.Cells[c];
                        cell.Text += field;
                        _vm.Select(table);
                        _vm.SelectCells(table, new CellRange(r, c, r, c));
                        break;
                    }
                default:
                    _vm.InsertFieldText(name, Snap(mm.X, true), Snap(mm.Y, true));
                    break;
            }
            Focus();
        }

        private void UpdateCursor(Point view, Point mm)
        {
            if (_vm == null) return;
            // ほかのページの上では、クリックするとそのページに移るだけ
            if (_layout.Count > 1 && !CurrentPageRect().Contains(view) && _mode == DragMode.None)
            {
                Cursor = Cursors.Arrow;
                return;
            }
            if (_vm.Tool != Tool.Select)
            {
                Cursor = Cursors.Cross;
                return;
            }
            int handle = HitHandle(view);
            if (handle >= 0)
            {
                Cursor = _vm.PrimarySelection is LineElement ? Cursors.Cross
                    : handle == 0 || handle == 4 ? Cursors.SizeNWSE
                    : handle == 2 || handle == 6 ? Cursors.SizeNESW
                    : handle == 1 || handle == 5 ? Cursors.SizeNS
                    : Cursors.SizeWE;
                return;
            }
            if (HitMoveGrip(view) || (_vm.PrimarySelection is TableElement t && HitTableEdge(t, view)))
            {
                Cursor = Cursors.SizeAll;
                return;
            }
            var hit = HitElement(mm);
            Cursor = hit == null ? Cursors.Arrow : hit is TableElement && hit == _vm.PrimarySelection ? Cursors.IBeam : Cursors.SizeAll;
        }

        // ---- 移動と変形 ----

        /// <summary>
        /// 選択枠などを描くときの部品。動かし始めたが重ねる画像がまだできていない間は、動かす前の状態（ページの画像も動かす前のまま）。
        /// </summary>
        private ReportElement Display(ReportElement element)
            => _dragStarted && _vm?.IsMovePreviewPending == true && _snapshots.TryGetValue(element, out var snapshot) ? snapshot : element;

        private void RememberOriginals(IEnumerable<ReportElement> elements)
        {
            _originals.Clear();
            _snapshots.Clear();
            foreach (var e in elements)
            {
                _snapshots[e] = ReportSerializer.CloneElement(e);
                switch (e)
                {
                    case LineElement l:
                        _originals.Add((e, new[] { l.X1, l.Y1, l.X2, l.Y2 }));
                        break;
                    case TableElement t:
                        _originals.Add((e, new[] { t.X, t.Y, t.Width, t.Height }
                            .Concat(t.Columns.Select(c => c.Width)).Concat(t.Rows.Select(r => r.Height)).Concat(new[] { t.DetailRow.Height }).ToArray()));
                        break;
                    case BoxElement b:
                        _originals.Add((e, new[] { b.X, b.Y, b.Width, b.Height }));
                        break;
                }
            }
        }

        /// <summary>選んでいる部品を動かす。lockAxis が true なら、移動量の大きいほう（水平か垂直）にだけ動かす。</summary>
        private void ApplyMove(Point mm, bool snap, bool lockAxis)
        {
            if (_originals.Count == 0) return;
            double dx = mm.X - _startMm.X, dy = mm.Y - _startMm.Y;
            // 最初の部品の左上がグリッドに乗るように移動量を調整する
            var first = _originals[0];
            double ox = first.Element is LineElement ? Math.Min(first.Original[0], first.Original[2]) : first.Original[0];
            double oy = first.Element is LineElement ? Math.Min(first.Original[1], first.Original[3]) : first.Original[1];
            dx = Snap(ox + dx, snap) - ox;
            dy = Snap(oy + dy, snap) - oy;
            if (lockAxis)
            {
                if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0;
                else dx = 0;
            }
            foreach (var (element, o) in _originals)
            {
                switch (element)
                {
                    case LineElement l:
                        l.X1 = Round(o[0] + dx);
                        l.Y1 = Round(o[1] + dy);
                        l.X2 = Round(o[2] + dx);
                        l.Y2 = Round(o[3] + dy);
                        break;
                    case BoxElement b:
                        b.X = Round(o[0] + dx);
                        b.Y = Round(o[1] + dy);
                        break;
                }
            }
        }

        private void ApplyResize(Point mm, bool snap, bool shift)
        {
            if (_originals.Count == 0) return;
            var (element, o) = _originals[0];
            var p = Snap(mm, snap);
            if (element is LineElement line)
            {
                if (_handle == 0)
                {
                    if (shift) p = ConstrainLine(new Point(o[2], o[3]), p);
                    line.X1 = p.X;
                    line.Y1 = p.Y;
                }
                else
                {
                    if (shift) p = ConstrainLine(new Point(o[0], o[1]), p);
                    line.X2 = p.X;
                    line.Y2 = p.Y;
                }
                return;
            }
            double left = o[0], top = o[1], right = o[0] + o[2], bottom = o[1] + o[3];
            if (_handle == 0 || _handle == 6 || _handle == 7) left = Math.Min(p.X, right - 1);
            if (_handle == 2 || _handle == 3 || _handle == 4) right = Math.Max(p.X, left + 1);
            if (_handle == 0 || _handle == 1 || _handle == 2) top = Math.Min(p.Y, bottom - 1);
            if (_handle == 4 || _handle == 5 || _handle == 6) bottom = Math.Max(p.Y, top + 1);

            if (element is TableElement t)
            {
                // 表は、元の列の幅・行の高さを同じ比率で変える
                double fx = (right - left) / o[2], fy = (bottom - top) / o[3];
                int i = 4;
                foreach (var c in t.Columns) c.Width = Math.Round(o[i++] * fx, 2);
                foreach (var r in t.Rows) r.Height = Math.Round(o[i++] * fy, 2);
                t.DetailRow.Height = Math.Round(o[i] * fy, 2);
                t.X = Round(left);
                t.Y = Round(top);
                t.OnSizeChanged();
                return;
            }
            var box = (BoxElement)element;
            box.X = Round(left);
            box.Y = Round(top);
            box.Width = Round(right - left);
            box.Height = Round(bottom - top);
        }

        /// <summary>直線を水平・垂直・45 度に揃える。</summary>
        private static Point ConstrainLine(Point start, Point end)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y;
            if (Math.Abs(dx) > Math.Abs(dy) * 2) return new Point(end.X, start.Y);
            if (Math.Abs(dy) > Math.Abs(dx) * 2) return new Point(start.X, end.Y);
            double d = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return new Point(start.X + Math.Sign(dx) * d, start.Y + Math.Sign(dy) * d);
        }

        private static double Round(double v) => Math.Round(v, 4);

        // ---- キーボード ----

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_vm == null || e.Handled) return;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            double step = shift ? 0.1 : _vm.GridSize;
            switch (e.Key)
            {
                case Key.Left: _vm.MoveSelection(-step, 0); break;
                case Key.Right: _vm.MoveSelection(step, 0); break;
                case Key.Up: _vm.MoveSelection(0, -step); break;
                case Key.Down: _vm.MoveSelection(0, step); break;
                case Key.Delete: Execute(_vm.DeleteCommand); break;
                case Key.Escape:
                    if (_vm.Tool != Tool.Select) _vm.Tool = Tool.Select;
                    else if (_vm.CellEditor != null && _vm.PrimarySelection is TableElement table) _vm.SelectCells(table, null);
                    else _vm.Select(null);
                    break;
                case Key.C when ctrl: Execute(_vm.CopyCommand); break;
                case Key.X when ctrl: Execute(_vm.CutCommand); break;
                case Key.V when ctrl: Execute(_vm.PasteCommand); break;
                case Key.D when ctrl: Execute(_vm.DuplicateCommand); break;
                case Key.B when ctrl: _vm.ToggleBold(); break;
                case Key.I when ctrl: _vm.ToggleItalic(); break;
                case Key.A when ctrl: Execute(_vm.SelectAllCommand); break;
                case Key.Enter when _vm.PrimarySelection != null: EditTextRequested?.Invoke(); break;
                case Key.F2 when _vm.PrimarySelection != null: EditTextRequested?.Invoke(); break;
                case Key.System when e.SystemKey == Key.F10 && shift:
                case Key.Apps:
                    {
                        var b = _vm.PrimarySelection?.Bounds;
                        var mm = b != null ? new Point(b.Value.X + b.Value.Width / 2, b.Value.Y + b.Value.Height / 2) : new Point(10, 10);
                        ContextMenuRequested?.Invoke(mm, _vm.PrimarySelection);
                        break;
                    }
                default: return;
            }
            e.Handled = true;
        }

        private static void Execute(ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
