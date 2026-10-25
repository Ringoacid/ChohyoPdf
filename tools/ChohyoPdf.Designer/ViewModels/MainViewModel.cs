using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using ChohyoPdf.Designer.Services;
using Microsoft.Win32;
using ModelRect = ChohyoPdf.Designer.Model.Rect;

namespace ChohyoPdf.Designer.ViewModels
{
    /// <summary>配置する部品の種類（デザイン画面のツール）。</summary>
    public enum Tool
    {
        Select,
        Text,
        Line,
        Rectangle,
        Ellipse,
        Image,
        Table,
    }

    /// <summary>左のパネルで表示している一覧。</summary>
    public enum NavigationTab
    {
        Pages,
        Elements,
    }

    /// <summary>書式バー: 何も選んでいないとき（ページと用紙の操作を出す）。</summary>
    public sealed class PageToolbar
    {
        public static PageToolbar Instance { get; } = new PageToolbar();

        private PageToolbar()
        {
        }
    }

    /// <summary>書式バー: 全ページ共通の部品を編集していて、何も選んでいないとき。</summary>
    public sealed class CommonToolbar
    {
        public static CommonToolbar Instance { get; } = new CommonToolbar();

        private CommonToolbar()
        {
        }
    }

    /// <summary>書式バー: 2 つ以上の部品を選んでいるとき（揃えと重なりを出す）。</summary>
    public sealed class MultipleToolbar
    {
        public static MultipleToolbar Instance { get; } = new MultipleToolbar();

        private MultipleToolbar()
        {
        }
    }

    /// <summary>差し込みのウィンドウで表示している種類。</summary>
    public enum DataSection
    {
        Fields,
        Lists,
        BuiltIns,
    }

    /// <summary>デザイナーの画面全体の状態。</summary>
    public sealed class MainViewModel : ObservableObject
    {
        private const string ClipboardFormat = "ChohyoPdfDesigner.Elements";

        private ReportDefinition _report = SampleReports.CreateBlank();
        private ChangeTracker? _tracker;
        private readonly UndoManager _undo = new UndoManager();
        private readonly DispatcherTimer _snapshotTimer;
        private string? _filePath;
        private bool _isDirty;
        private Tool _tool;
        private double _zoom = 1;
        private double _gridSize = 1;
        private bool _snapToGrid = true;
        private bool _showGrid = true;
        private bool _showOutlines = true;
        private double _pixelsPerMm = 96 / 25.4;
        private CellEditor? _cellEditor;
        private ReportElement? _primary;
        private string _cursorText = "";
        private bool _renderRunning, _renderPending;
        private List<string> _renderWarnings = new List<string>();
        private ListDefinition? _selectedList;
        private FieldDefinition? _selectedField, _selectedColumn;
        private int _interactionDepth;
        private bool _showSampleValues;
        private (object? Target, string? FontName) _fontPreview;
        private DataTable? _sampleTable;
        private bool _writingSamples;
        private int _pageIndex;
        private bool _editingCommon;
        private bool _scriptsTrusted = true;
        private readonly DispatcherTimer _scriptTimer;
        private bool _scriptsRunning, _scriptsPending;
        private Dictionary<string, object?>? _scriptResults;
        private List<string> _scriptErrors = new List<string>();
        private readonly DispatcherTimer _thumbnailTimer;
        private bool _thumbnailsRunning, _thumbnailsPending;
        private DataSection _dataSection;
        private bool _isGuideOpen;
        private DataSection _guideSection;
        private IReadOnlyDictionary<object, int> _usageCounts = new Dictionary<object, int>();
        private readonly DispatcherTimer _usageTimer;

        // ページの画像（キーはページの番号。全ページ共通の部品を編集しているときの画像は CommonKey）
        private const int CommonKey = -1;
        private readonly Dictionary<int, PageImage> _pageImages = new Dictionary<int, PageImage>();
        // デザインを変えるたびに増やす。画像を描いたときの値と違えば、描き直す
        private int _version;
        private readonly DispatcherTimer _otherPagesTimer;
        private bool _otherPagesRunning, _otherPagesPending;
        private List<int> _visiblePages = new List<int>();

        private sealed class PageImage
        {
            public PageImage(BitmapSource bitmap, int version, int width)
            {
                Bitmap = bitmap;
                Version = version;
                Width = width;
            }

            public BitmapSource Bitmap { get; }
            public int Version { get; }
            public int Width { get; }
        }

        // 部品をドラッグで動かしたり大きさを変えたりしている間の表示。ページを描き直していると部品が遅れて付いてくるので、
        // 動かす部品・その奥・その手前の画像を描き、動かす部品の画像をずらして（大きさを変えるときは伸び縮みさせて）重ねる。
        // ボタンを押してすぐに動かしても間に合うよう、画像は部品を選んだときに先に描いておく
        private MovePreview? _movePreview;
        private PreparedLayers? _prepared;
        private readonly DispatcherTimer _prepareTimer;
        // 大きさを変えている間に、動かす部品の画像を描き直している
        private bool _movingLayerRunning, _movingLayerPending;

        /// <summary>動かす部品の画像。大きさを変えている間は描き直す。</summary>
        private sealed class MovingImages
        {
            public MovingImages(BitmapSource? decoration, BitmapSource content, ModelRect? area, ReportElement rendered, double? imageAspect)
            {
                Decoration = decoration;
                Content = content;
                Area = area;
                Rendered = rendered;
                ImageAspect = imageAspect;
            }

            /// <summary>枠線・背景（テキストか表を 1 つだけ動かすとき。それ以外は null で、<see cref="Content"/> に全体を描く）。</summary>
            public BitmapSource? Decoration { get; }
            public BitmapSource Content { get; }
            /// <summary>
            /// 画像に描いた、用紙の上の範囲（mm）。null なら用紙全体。画像が大きいと描くのにも表示するのにも時間がかかるので、
            /// 部品のまわりだけを描く（<see cref="MovePreviewGeometry.DrawnArea"/>）。
            /// </summary>
            public ModelRect? Area { get; }
            /// <summary>画像を描いたときの基準の部品（複製）。今の部品との違いから、画像の移し方を決める（<see cref="MovePreviewGeometry"/>）。</summary>
            public ReportElement Rendered { get; }
            /// <summary>画像の部品の縦横比（分からなければ null）。</summary>
            public double? ImageAspect { get; }
        }

        private sealed class LayerImages
        {
            public LayerImages(BitmapSource background, BitmapSource? foreground, MovingImages moving)
            {
                Background = background;
                Foreground = foreground;
                Moving = moving;
            }

            public BitmapSource Background { get; }
            /// <summary>動かす部品より手前の部品（なければ null）。</summary>
            public BitmapSource? Foreground { get; }
            public MovingImages Moving { get; }
        }

        /// <summary>先に描いた（描いている）画像。デザインの版・ページ・部品・画像の幅などが描いたときと同じなら使える（<see cref="LayerKey"/>）。</summary>
        private sealed class PreparedLayers
        {
            public PreparedLayers(string key, Task<LayerImages?> images)
            {
                Key = key;
                Images = images;
            }

            public string Key { get; }
            public Task<LayerImages?> Images { get; }
        }

        private sealed class MovePreview
        {
            public MovePreview(int key, IReadOnlyList<int> indexes, ReportElement reference, bool resizing)
            {
                Key = key;
                Indexes = indexes;
                Reference = reference;
                Resizing = resizing;
            }

            public int Key { get; }
            /// <summary>動かす部品の、ページの部品の中での番号。</summary>
            public IReadOnlyList<int> Indexes { get; }
            /// <summary>画像をずらす量と伸び縮みさせる量は、この部品の位置の変化から求める（グリッドに合わせた位置と同じになる）。</summary>
            public ReportElement Reference { get; }
            /// <summary>大きさを変えている。動かす部品の画像を、変えた大きさで描き直し続ける。</summary>
            public bool Resizing { get; }
            public BitmapSource? Background { get; set; }
            public MovingImages? Moving { get; set; }
            /// <summary>動かす部品より手前の部品（なければ null）。</summary>
            public BitmapSource? Foreground { get; set; }
            /// <summary>動かしている間に、ページの描き直しを見送った。</summary>
            public bool Deferred { get; set; }
            /// <summary>ボタンを離した。この版以降のページの画像ができるまで、重ねた画像を表示し続ける。</summary>
            public int? EndVersion { get; set; }
        }

        public MainViewModel()
        {
            _snapshotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _snapshotTimer.Tick += (s, e) =>
            {
                _snapshotTimer.Stop();
                if (_interactionDepth == 0) CommitSnapshot();
            };
            _thumbnailTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _thumbnailTimer.Tick += (s, e) =>
            {
                _thumbnailTimer.Stop();
                RenderThumbnails();
            };
            _scriptTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _scriptTimer.Tick += (s, e) =>
            {
                _scriptTimer.Stop();
                EvaluateScripts();
            };
            // 文字を入力するたびに数え直さないよう、変更が落ち着いてから数える
            _usageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _usageTimer.Tick += (s, e) =>
            {
                _usageTimer.Stop();
                RecountUsage();
            };
            _otherPagesTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _otherPagesTimer.Tick += (s, e) =>
            {
                _otherPagesTimer.Stop();
                RenderOtherPages();
            };
            _prepareTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _prepareTimer.Tick += (s, e) =>
            {
                _prepareTimer.Stop();
                PrepareLayers();
            };
            AppSettings.Current.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(AppSettings.ContinuousPages)) return;
                OnPropertyChanged(nameof(ContinuousPages));
                OnPropertyChanged(nameof(IsContinuous));
                Invalidate();
                RestartOtherPages();
            };
            Selection.CollectionChanged += (s, e) => OnSelectionChanged();
            CreateCommands();
            SetReport(SampleReports.CreateBlank(), null);
            LoadFontsInBackground();
        }

        // ---- 状態 ----

        public ReportDefinition Report => _report;

        public string? FilePath
        {
            get => _filePath;
            private set
            {
                if (!Set(ref _filePath, value)) return;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DocumentName));
            }
        }

        /// <summary>画像の相対パスの基準のフォルダ（未保存ならカレントフォルダ）。</summary>
        public string? BaseDirectory => FilePath != null ? Path.GetDirectoryName(FilePath) : null;

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (Set(ref _isDirty, value)) OnPropertyChanged(nameof(Title));
            }
        }

        public string Title => (IsDirty ? "* " : "") + (FilePath != null ? Path.GetFileName(FilePath) : "新しい文書") + " - ChohyoPdf Designer";

        /// <summary>タイトルバーに出す文書の名前（保存したファイルの名前。保存していなければ「新しい文書」）。</summary>
        public string DocumentName => FilePath != null ? Path.GetFileNameWithoutExtension(FilePath) : Report.Name.Length > 0 ? Report.Name : "新しい文書";

        public Tool Tool { get => _tool; set => Set(ref _tool, value); }

        // ---- ページ ----

        /// <summary>編集しているページの番号（0 から）。</summary>
        public int CurrentPageIndex
        {
            get => _pageIndex;
            set
            {
                int index = Math.Max(0, Math.Min(Report.Pages.Count - 1, value));
                if (index == _pageIndex && !_editingCommon) return;
                CommitPendingEdits?.Invoke();
                GoToPage(index);
            }
        }

        /// <summary>編集するページを切り替える（選択はやめる。全ページ共通の部品の編集も終える）。</summary>
        private void GoToPage(int index)
        {
            _editingCommon = false;
            _pageIndex = index;
            Selection.Clear();
            CellEditor = null;
            OnPageChanged();
            RequestRender();
        }

        /// <summary>全ページ共通の部品を編集している（Word でヘッダー・フッターを編集しているときのように）。</summary>
        public bool IsEditingCommon
        {
            get => _editingCommon;
            set
            {
                if (value == _editingCommon) return;
                CommitPendingEdits?.Invoke();
                _editingCommon = value;
                Selection.Clear();
                CellEditor = null;
                OnPageChanged();
                RequestRender();
            }
        }

        /// <summary>全ページ共通の部品の編集を始め、element を選ぶ（デザイン画面で共通の部品をダブルクリックしたとき）。</summary>
        public void EditCommon(ReportElement? element)
        {
            IsEditingCommon = true;
            if (element != null && Elements.Contains(element)) Select(element);
        }

        /// <summary>
        /// 編集しているページの (x, y) mm に描いてある、全ページ共通の部品（いちばん手前のもの）。
        /// ページの部品を編集しているときだけ探す。
        /// </summary>
        public ReportElement? HitCommonElement(double x, double y)
        {
            if (_editingCommon || CurrentPage.HideCommon) return null;
            for (int i = Report.Common.Elements.Count - 1; i >= 0; i--)
            {
                var e = Report.Common.Elements[i];
                // デザイン画面では、ページの番号で描くかどうかを決めている（ReportRenderer と同じ）
                if (!ReportRenderer.IsVisibleInDocument(e.Visibility, _pageIndex + 1, Report.Pages.Count)) continue;
                var b = e.Bounds;
                if (x >= b.X - 1 && x <= b.Right + 1 && y >= b.Y - 1 && y <= b.Bottom + 1) return e;
            }
            return null;
        }

        /// <summary>編集しているページ（全ページ共通の部品を編集しているときは、その入れ物）。</summary>
        public ReportPage CurrentPage => _editingCommon ? Report.Common : Report.Pages[_pageIndex];

        /// <summary>編集しているページの用紙の幅（mm）。全ページ共通の部品は文書の用紙に置く。</summary>
        public double CurrentPageWidth => _editingCommon ? Report.PageWidth : Report.PageWidthOf(CurrentPage);

        /// <summary>編集しているページの用紙の高さ（mm）。</summary>
        public double CurrentPageHeight => _editingCommon ? Report.PageHeight : Report.PageHeightOf(CurrentPage);

        /// <summary>編集しているページだけ、文書と違う用紙を使う。</summary>
        public bool CurrentPageHasOwnPaper
        {
            get => !_editingCommon && CurrentPage.Paper != null;
            set
            {
                if (_editingCommon || value == (CurrentPage.Paper != null)) return;
                CurrentPage.Paper = value ? Report.CopyPaper() : null;
                CommitSnapshot();
            }
        }

        /// <summary>ページの用紙の幅と高さ（mm）。key はページの番号か CommonKey。</summary>
        public (double Width, double Height) PageSizeOf(int key)
            => key == CommonKey || key < 0 || key >= Report.Pages.Count ? (Report.PageWidth, Report.PageHeight)
            : (Report.PageWidthOf(Report.Pages[key]), Report.PageHeightOf(Report.Pages[key]));

        /// <summary>デザイン画面に表示するページのキー（ページの番号。全ページ共通の部品を編集しているときは CommonKey）。</summary>
        public int CurrentPageKey => _editingCommon ? CommonKey : _pageIndex;

        /// <summary>左の部品の一覧の見出し。</summary>
        public string ElementListHeader => _editingCommon ? "全ページ共通の部品（下ほど手前）" : "このページの部品（下ほど手前）";

        /// <summary>部品の「描くページ」の欄の名前。</summary>
        public string VisibilityLabel => _editingCommon ? "描くページ" : "ページが増えたとき";

        /// <summary>部品の「描くページ」の選択肢。</summary>
        public IReadOnlyList<Choice> VisibilityChoices => _editingCommon ? Choices.CommonVisibilities : Choices.Visibilities;

        /// <summary>
        /// 部品の「描くページ」を選べる（全ページ共通の部品か、くり返し行のある表でページが増えるページの部品）。
        /// </summary>
        public bool ShowVisibilityOption => _editingCommon || CurrentPageRepeats;

        /// <summary>ページをつなげて表示する（Word の印刷レイアウトのように、縦に並べてスクロールする）。設定に保存する。</summary>
        public bool ContinuousPages
        {
            get => AppSettings.Current.ContinuousPages;
            set
            {
                if (AppSettings.Current.ContinuousPages == value) return;
                AppSettings.Current.ContinuousPages = value;
                AppSettings.Current.Save();
            }
        }

        /// <summary>デザイン画面にページをつなげて表示している（全ページ共通の部品を編集しているときは 1 ページだけ表示する）。</summary>
        public bool IsContinuous => ContinuousPages && !_editingCommon;

        /// <summary>編集しているページの部品。後ろのものほど手前に描く。</summary>
        public ObservableCollection<ReportElement> Elements => CurrentPage.Elements;

        /// <summary>ページの一覧（左のパネルの「ページ」。縮小画像を持つ）。</summary>
        public ObservableCollection<PageItem> PageItems { get; } = new ObservableCollection<PageItem>();

        public string PageText => _editingCommon ? "全ページ共通の部品" : "ページ " + (_pageIndex + 1) + " / " + Report.Pages.Count;

        /// <summary>ステータスバーに表示するページ（Word と同じく「2/3 ページ」と書く）。</summary>
        public string PageStatusText => _editingCommon ? "全ページ共通の部品" : (_pageIndex + 1) + "/" + Report.Pages.Count + " ページ";

        /// <summary>
        /// 編集しているページに、くり返し行のある表がある（件数が多いとページが増えるので、
        /// 部品の「ページが増えたとき」の設定を使える）。
        /// </summary>
        public bool CurrentPageRepeats => !_editingCommon && Elements.OfType<TableElement>().Any(t => t.HasDetail);

        private void OnPageChanged()
        {
            OnPropertyChanged(nameof(CurrentPageIndex));
            OnPropertyChanged(nameof(IsEditingCommon));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(Elements));
            OnPropertyChanged(nameof(PageText));
            OnPropertyChanged(nameof(PageStatusText));
            OnPropertyChanged(nameof(PropertyPaneTitle));
            OnPropertyChanged(nameof(ToolbarTarget));
            OnPropertyChanged(nameof(CurrentPageRepeats));
            OnPropertyChanged(nameof(VisibilityLabel));
            OnPropertyChanged(nameof(ElementListHeader));
            OnPropertyChanged(nameof(VisibilityChoices));
            OnPropertyChanged(nameof(ShowVisibilityOption));
            OnPropertyChanged(nameof(IsContinuous));
            OnPaperChanged();
            for (int i = 0; i < PageItems.Count; i++) PageItems[i].IsCurrent = i == _pageIndex;
            Invalidate();
        }

        /// <summary>ページの一覧を、ページの数と名前に合わせる（縮小画像は残す）。</summary>
        private void SyncPageItems()
        {
            while (PageItems.Count > Report.Pages.Count) PageItems.RemoveAt(PageItems.Count - 1);
            for (int i = 0; i < Report.Pages.Count; i++)
            {
                if (i == PageItems.Count) PageItems.Add(new PageItem());
                var item = PageItems[i];
                item.Number = i + 1;
                item.Name = Report.Pages[i].Name;
                item.IsCurrent = i == _pageIndex;
                var page = Report.Pages[i];
                item.ThumbnailHeight = Math.Round(PageItem.ThumbnailWidth * Report.PageHeightOf(page) / Math.Max(1, Report.PageWidthOf(page)));
            }
            OnPropertyChanged(nameof(PageText));
            OnPropertyChanged(nameof(PageStatusText));
        }

        /// <summary>書式バーの用紙のボタンで変える用紙の名前（このページだけ用紙を変えていればページの用紙、そうでなければ文書の用紙）。</summary>
        public string CurrentPaperName => !_editingCommon && CurrentPage.Paper is PaperSetting paper ? paper.PaperName : Report.PaperName;

        /// <summary>書式バーの用紙のボタンで変える用紙が横向き。</summary>
        public bool CurrentPaperLandscape => !_editingCommon && CurrentPage.Paper is PaperSetting paper ? paper.Landscape : Report.Landscape;

        /// <summary>書式バーの用紙のボタンの文字（例: A4 縦）。</summary>
        public string CurrentPaperLabel => CurrentPaperName + " " + (CurrentPaperLandscape ? "横" : "縦");

        /// <summary>用紙の大きさを変える（このページだけ用紙を変えていればページの用紙、そうでなければ文書の用紙）。</summary>
        public void SetCurrentPaperName(string name)
        {
            if (!_editingCommon && CurrentPage.Paper is PaperSetting paper) paper.PaperName = name;
            else Report.PaperName = name;
        }

        /// <summary>用紙の向きを変える（このページだけ用紙を変えていればページの用紙、そうでなければ文書の用紙）。</summary>
        public void SetCurrentPaperLandscape(bool landscape)
        {
            if (!_editingCommon && CurrentPage.Paper is PaperSetting paper) paper.Landscape = landscape;
            else Report.Landscape = landscape;
        }

        private void OnPaperChanged()
        {
            OnPropertyChanged(nameof(CurrentPaperName));
            OnPropertyChanged(nameof(CurrentPaperLandscape));
            OnPropertyChanged(nameof(CurrentPaperLabel));
            OnPropertyChanged(nameof(CurrentPageWidth));
            OnPropertyChanged(nameof(CurrentPageHeight));
            OnPropertyChanged(nameof(CurrentPageHasOwnPaper));
        }

        /// <summary>空のページを、編集しているページの後ろに足す。</summary>
        public void AddPage() => InsertPage(_pageIndex + 1, new ReportPage());

        /// <summary>編集しているページを複製して、後ろに足す。</summary>
        public void DuplicatePage()
        {
            var source = Report.Pages[_pageIndex];
            var copy = new ReportPage { Name = source.Name, HideCommon = source.HideCommon };
            if (source.Paper != null)
                copy.Paper = new PaperSetting { PaperName = source.Paper.PaperName, Landscape = source.Paper.Landscape, CustomWidth = source.Paper.CustomWidth, CustomHeight = source.Paper.CustomHeight };
            foreach (var e in ReportSerializer.ElementsFromJson(ReportSerializer.ElementsToJson(source.Elements))) copy.Elements.Add(e);
            InsertPage(_pageIndex + 1, copy);
        }

        private void InsertPage(int index, ReportPage page)
        {
            CommitPendingEdits?.Invoke();
            ShiftImages(index, 1);
            Report.Pages.Insert(index, page);
            GoToPage(index);
            CommitSnapshot();
        }

        /// <summary>編集しているページを削除する（最後の 1 ページは削除しない）。</summary>
        public void DeletePage()
        {
            if (Report.Pages.Count <= 1) return;
            CommitPendingEdits?.Invoke();
            int index = _pageIndex;
            Selection.Clear();
            CellEditor = null;
            // 削除している間も、残るページを指しておく（変更の通知で Elements を読むため）
            _pageIndex = index > 0 ? index - 1 : 0;
            _editingCommon = false;
            ShiftImages(index, -1);
            Report.Pages.RemoveAt(index);
            GoToPage(Math.Min(index, Report.Pages.Count - 1));
            CommitSnapshot();
        }

        /// <summary>編集しているページを前（delta &lt; 0）か後ろへ動かす。</summary>
        public void MovePage(int delta)
        {
            int target = _pageIndex + delta;
            if (target < 0 || target >= Report.Pages.Count) return;
            CommitPendingEdits?.Invoke();
            // 描いてあるページの画像も入れ替える（描き直すまで、前の画像を表示する）
            _pageImages.TryGetValue(_pageIndex, out var a);
            _pageImages.TryGetValue(target, out var b);
            _pageImages.Remove(_pageIndex);
            _pageImages.Remove(target);
            if (a != null) _pageImages[target] = a;
            if (b != null) _pageImages[_pageIndex] = b;
            Report.Pages.Move(_pageIndex, target);
            _pageIndex = target;
            OnPageChanged();
            // Move の通知で始めた描画は、動かす前の番号のページを描くので、描き直す
            RequestRender();
            CommitSnapshot();
        }

        /// <summary>ページを足した・削除したときに、描いてあるページの画像の番号をずらす。</summary>
        private void ShiftImages(int index, int delta)
        {
            var moved = _pageImages.Where(p => p.Key >= index).OrderBy(p => delta > 0 ? -p.Key : p.Key).ToList();
            foreach (var pair in moved)
            {
                _pageImages.Remove(pair.Key);
                if (delta < 0 && pair.Key == index) continue;
                _pageImages[pair.Key + delta] = pair.Value;
            }
        }

        // ---- ページの画像 ----

        /// <summary>デザイン画面に表示するページの画像（key はページの番号か CommonKey）。まだ描いていなければ null。</summary>
        public BitmapSource? GetPageBitmap(int key) => _pageImages.TryGetValue(key, out var image) ? image.Bitmap : null;

        /// <summary>ChohyoPdf で描いた、編集しているページの画像。</summary>
        public BitmapSource? PageBitmap => GetPageBitmap(CurrentPageKey);

        /// <summary>ページを画像にするときの幅（ピクセル）。拡大しすぎたときは画像の大きさを抑える（引き伸ばして表示する）。</summary>
        private int PixelWidth(int key) => (int)Math.Min(4000, Math.Ceiling(PageSizeOf(key).Width * PixelsPerMm));

        private void StoreImage(int key, BitmapSource? bitmap, int version, int width)
        {
            if (bitmap == null) return;
            _pageImages[key] = new PageImage(bitmap, version, width);
            if (_movePreview?.EndVersion is int end && _movePreview.Key == key && version >= end) _movePreview = null;
            // 見えていないページの画像は、たまりすぎたら捨てる
            if (_pageImages.Count > 24)
            {
                foreach (int k in _pageImages.Keys.Where(k => k != CurrentPageKey && !_visiblePages.Contains(k)).ToList()) _pageImages.Remove(k);
            }
            if (key == CurrentPageKey && version == _version) SchedulePrepare();
            Invalidate();
        }

        /// <summary>先に描いた画像を使えるかの判定に使う、描いたときの状態。</summary>
        private string LayerKey(IReadOnlyList<int> indexes)
            => string.Join("|", _version, CurrentPageKey, PixelWidth(CurrentPageKey), ShowSampleValues, _scriptsTrusted, string.Join(",", indexes));

        /// <summary>先に描いておく画像の幅の上限（ピクセル）。拡大して画像が大きいときは、メモリを使いすぎないよう先に描かない。</summary>
        private const int PrepareMaxWidth = 2400;

        /// <summary>選んでいる部品を動かすときに重ねる画像を、少し待ってから先に描いておく。</summary>
        private void SchedulePrepare()
        {
            _prepareTimer.Stop();
            if (Selection.Count > 0) _prepareTimer.Start();
            else if (_movePreview == null) _prepared = null;
        }

        private void PrepareLayers()
        {
            if (Selection.Count == 0 || _interactionDepth > 0 || _movePreview != null || PixelWidth(CurrentPageKey) > PrepareMaxWidth) return;
            // ページを描いている間は、それを先に済ませる
            if (_renderRunning)
            {
                SchedulePrepare();
                return;
            }
            var indexes = Selection.Select(e => Elements.IndexOf(e)).ToList();
            if (!indexes.Contains(-1)) GetLayers(indexes);
        }

        /// <summary>indexes の部品を動かすときに重ねる画像（先に描いてあれば、それを使う）。描けなければ null。</summary>
        private Task<LayerImages?> GetLayers(IReadOnlyList<int> indexes)
        {
            string key = LayerKey(indexes);
            if (_prepared?.Key == key && !(_prepared.Images.IsCompleted && _prepared.Images.Result == null)) return _prepared.Images;
            var task = new LayerJob(this).RenderAllAsync(indexes);
            _prepared = new PreparedLayers(key, task);
            return task;
        }

        /// <summary>
        /// 部品をドラッグで動かし始めた（resizing なら、elements の 1 つの部品の大きさを変え始めた）。ボタンを押したときに呼ぶ。
        /// <see cref="EndMovePreview"/> までページを描き直さず、部品の画像をずらしたり伸び縮みさせたりして表示する（<see cref="TryGetMovePreview"/>）。
        /// 画像ができるまでは、動かす前の表示のままにする（<see cref="IsMovePreviewPending"/>）。
        /// </summary>
        public async void BeginMovePreview(IReadOnlyList<ReportElement> elements, bool resizing)
        {
            _movePreview = null;
            var indexes = elements.Select(e => Elements.IndexOf(e)).ToList();
            if (indexes.Count == 0 || indexes.Contains(-1)) return;
            var preview = new MovePreview(CurrentPageKey, indexes, elements[0], resizing);
            _movePreview = preview;
            // 先に描いてあれば、待たずにすぐ使う
            var images = await GetLayers(indexes);
            if (_movePreview != preview) return;
            if (images == null)
            {
                // 描けなければ、これまでどおりページを描き直して表示する
                _movePreview = null;
                RequestRender();
                return;
            }
            preview.Background = images.Background;
            preview.Foreground = images.Foreground;
            preview.Moving = images.Moving;
            Invalidate();
            // 待っている間に大きさが変わっていたら、今の大きさで描き直す
            if (preview.Resizing && preview.Deferred && preview.EndVersion == null) RenderMovingLayer(preview);
        }

        /// <summary>部品を動かし始めたが、重ねる画像がまだできていない（デザイン画面は、選択枠も動かす前の位置に表示する）。</summary>
        public bool IsMovePreviewPending => _movePreview != null && _movePreview.EndVersion == null && _movePreview.Moving == null;

        /// <summary>大きさを変えている部品の画像を、今の大きさで描き直す（間引いて、最新の状態だけを描く）。</summary>
        private async void RenderMovingLayer(MovePreview preview)
        {
            if (_movingLayerRunning)
            {
                _movingLayerPending = true;
                return;
            }
            _movingLayerRunning = true;
            try
            {
                do
                {
                    _movingLayerPending = false;
                    var job = new LayerJob(this);
                    double? aspect = preview.Moving?.ImageAspect;
                    var images = await Task.Run(() =>
                    {
                        job.PrepareData();
                        return job.RenderMovingAsync(preview.Indexes, aspect);
                    });
                    if (_movePreview != preview || preview.EndVersion != null) return;
                    if (images == null) continue;
                    preview.Moving = images;
                    Invalidate();
                } while (_movingLayerPending);
            }
            catch (Exception)
            {
                // 描き直せなければ、前の画像のまま表示する（ボタンを離せばページを描き直す）
            }
            finally
            {
                _movingLayerRunning = false;
                _movingLayerPending = false;
            }
        }

        /// <summary>
        /// 部品を動かしている間の画像を描く。作ったときのデザインを複製しておき、別のスレッドで、選んだ部品だけを描く。
        /// </summary>
        private sealed class LayerJob
        {
            private readonly ReportDefinition _clone;
            private readonly RenderMode _mode;
            private readonly string? _baseDirectory;
            private readonly int _pageIndex;
            private readonly bool _common;
            private readonly DesignLayer _layer;
            private readonly int _width;
            private readonly (double Width, double Height) _pageSize;
            private readonly bool _scripts;
            private readonly Func<bool> _obsolete;
            private ReportData? _data;

            public LayerJob(MainViewModel vm)
            {
                // 先に描いている間にデザインが変わったら（動かし始めて使っているときを除く）、描いても使えないのでやめる。
                // ほかのスレッドから読むが、目安なので古い値でもかまわない
                int version = vm._version;
                _obsolete = () => vm._version != version && vm._movePreview == null;
                _clone = ReportSerializer.Clone(vm.Report);
                vm.ApplyFontPreview(_clone);
                _common = vm._editingCommon;
                _pageIndex = vm._pageIndex;
                Elements = ElementsOf(_clone);
                _mode = vm.ShowSampleValues ? RenderMode.Preview : RenderMode.Design;
                _baseDirectory = vm.BaseDirectory;
                _layer = _common ? DesignLayer.Common : DesignLayer.Page;
                _width = vm.PixelWidth(vm.CurrentPageKey);
                _pageSize = vm.PageSizeOf(vm.CurrentPageKey);
                _scripts = vm._scriptsTrusted;
            }

            /// <summary>複製したデザインの、編集しているページの部品。</summary>
            public IList<ReportElement> Elements { get; }

            private IList<ReportElement> ElementsOf(ReportDefinition report) => _common ? report.Common.Elements : report.Pages[_pageIndex].Elements;

            /// <summary>差し込み結果を表示しているときは、確認用の値を用意する（描く前に、別のスレッドで呼ぶ）。</summary>
            public void PrepareData()
            {
                if (_mode == RenderMode.Preview) _data = ReportData.FromSamples(_clone, null, null, _scripts);
            }

            /// <summary>動かす部品（indexes）・その奥・その手前の画像を、別のスレッドで描く。描けなければ null。</summary>
            public Task<LayerImages?> RenderAllAsync(IReadOnlyList<int> indexes) => Task.Run(async () =>
            {
                try
                {
                    // PDF を作っている間はページの描画を待たせるので、PDF を 1 つ作るごとに、まだ必要かを確かめる
                    if (_obsolete()) return null;
                    PrepareData();
                    var moving = new HashSet<ReportElement>(indexes.Select(i => Elements[i]));
                    // 動かす部品のうち最も奥のものより手前にある部品は、上に重ねる
                    var front = new HashSet<ReportElement>(Elements.Skip(indexes.Min() + 1).Where(e => !moving.Contains(e)));
                    // PDF は 1 つずつ作り（ReportRenderer）、画像にするのは並行して行う
                    var background = RenderAsync(_clone, e => !moving.Contains(e) && !front.Contains(e), false);
                    if (_obsolete()) return null;
                    var foreground = front.Count > 0 ? RenderAsync(_clone, front.Contains, true) : Task.FromResult<BitmapSource?>(null);
                    if (_obsolete()) return null;
                    var images = RenderMovingAsync(indexes, ImageAspect(Elements[indexes[0]]));
                    await Task.WhenAll(background, foreground, images);
                    if (background.Result == null || images.Result == null || (front.Count > 0 && foreground.Result == null)) return null;
                    return new LayerImages(background.Result, foreground.Result, images.Result);
                }
                catch (Exception)
                {
                    return null;
                }
            });

            /// <summary>
            /// 動かす部品の画像を描く（PrepareData の後に、別のスレッドで呼ぶ）。テキストか表を 1 つだけ動かすときは、
            /// 大きさを変えても文字が伸び縮みしないよう、枠線・背景と文字を別の画像に描く。描けなければ null。
            /// </summary>
            public async Task<MovingImages?> RenderMovingAsync(IReadOnlyList<int> indexes, double? imageAspect)
            {
                var reference = Elements[indexes[0]];
                var moving = new HashSet<ReportElement>(indexes.Select(i => Elements[i]));
                var (area, pixels) = PixelArea(MovePreviewGeometry.DrawnArea(moving, _pageSize.Width, _pageSize.Height));
                if (indexes.Count == 1 && (reference is TextElement || reference is TableElement))
                {
                    var (decorationReport, decoration) = Variant(indexes[0], RemoveText);
                    var (contentReport, content) = Variant(indexes[0], RemoveDecoration);
                    var d = RenderAsync(decorationReport, e => e == decoration, true, pixels);
                    var c = RenderAsync(contentReport, e => e == content, true, pixels);
                    await Task.WhenAll(d, c);
                    return d.Result == null || c.Result == null ? null : new MovingImages(d.Result, c.Result, area, reference, imageAspect);
                }
                var image = await RenderAsync(_clone, moving.Contains, true, pixels);
                return image == null ? null : new MovingImages(null, image, area, reference, imageAspect);
            }

            /// <summary>
            /// 用紙の上の範囲 area（mm）を、ページ全体の画像のピクセルの境目に合わせて広げたものと、そのピクセルの範囲。
            /// area が null（部品が用紙の外にある）なら、用紙全体（どちらも null）。
            /// </summary>
            private (ModelRect? Area, Int32Rect? Pixels) PixelArea(ModelRect? area)
            {
                if (area is not ModelRect a || _pageSize.Width <= 0) return (null, null);
                double perMm = _width / _pageSize.Width;
                int height = (int)Math.Ceiling(_pageSize.Height * perMm);
                int x0 = Math.Max(0, (int)Math.Floor(a.X * perMm)), y0 = Math.Max(0, (int)Math.Floor(a.Y * perMm));
                int x1 = Math.Min(_width, (int)Math.Ceiling(a.Right * perMm)), y1 = Math.Min(height, (int)Math.Ceiling(a.Bottom * perMm));
                if (x1 <= x0 || y1 <= y0) return (null, null);
                return (new ModelRect(x0 / perMm, y0 / perMm, (x1 - x0) / perMm, (y1 - y0) / perMm), new Int32Rect(x0, y0, x1 - x0, y1 - y0));
            }

            /// <summary>デザインをもう 1 つ複製して、index の部品を change で変えたもの。</summary>
            private (ReportDefinition Report, ReportElement Element) Variant(int index, Action<ReportElement> change)
            {
                var report = ReportSerializer.Clone(_clone);
                var element = ElementsOf(report)[index];
                change(element);
                return (report, element);
            }

            private static void RemoveText(ReportElement element)
            {
                if (element is TextElement text) text.Text = "";
                if (element is TableElement table)
                {
                    foreach (var row in table.Rows.Append(table.DetailRow))
                    {
                        foreach (var cell in row.Cells) cell.Text = "";
                    }
                }
            }

            private static void RemoveDecoration(ReportElement element)
            {
                if (element is TextElement text)
                {
                    text.BorderColor = null;
                    text.BackgroundColor = null;
                }
                if (element is TableElement table)
                {
                    table.BorderColor = null;
                    table.AlternateRowColor = null;
                    foreach (var row in table.Rows.Append(table.DetailRow))
                    {
                        foreach (var cell in row.Cells) cell.BackgroundColor = null;
                    }
                }
            }

            /// <summary>縦横比を保って収める画像の、縦横比（幅 ÷ 高さ）。そのほかの部品や、読めない画像は null。</summary>
            private double? ImageAspect(ReportElement element)
            {
                if (!(element is ImageElement image) || image.Fit != ImageFit.Uniform || image.Path.Length == 0) return null;
                try
                {
                    var pdfImage = PdfImage.FromFile(ReportRenderer.ResolvePath(image.Path, _baseDirectory));
                    return pdfImage.Height > 0 ? pdfImage.Width / pdfImage.Height : (double?)null;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is NotSupportedException || ex is FormatException || ex is ArgumentException)
                {
                    return null;
                }
            }

            /// <summary>
            /// include を満たす部品だけを描いた画像。overlay なら背景を透明にする（<see cref="ReportRenderer.RenderLayer"/>）。
            /// pixels を指定すると、ページ全体の画像のその範囲（ピクセル）だけを描く。
            /// </summary>
            private async Task<BitmapSource?> RenderAsync(ReportDefinition report, Func<ReportElement, bool> include, bool overlay, Int32Rect? pixels = null)
            {
                byte[] pdf = ReportRenderer.RenderLayer(report, _mode, _data, _baseDirectory, _pageIndex, _layer, include, overlay).ToArray();
                if (pixels is Int32Rect area) return await PdfRasterizer.RenderAreaAsync(pdf, _width, area, overlay);
                return (await PdfRasterizer.RenderAsync(pdf, _width, 1, overlay)).FirstOrDefault();
            }
        }

        /// <summary>ドラッグを終えた。ページを描き直し、描き終えるまでは重ねた画像を表示する。</summary>
        public void EndMovePreview()
        {
            var preview = _movePreview;
            if (preview == null || preview.EndVersion != null) return;
            // 描き直しを見送っていなければ（動かさずに離したとき）、これまでどおりの表示に戻す
            if (!preview.Deferred)
            {
                _movePreview = null;
                return;
            }
            // EndVersion を決めてから描き直す（決める前だと、描き直しも見送ってしまう）
            preview.EndVersion = int.MaxValue;
            RequestRender();
            preview.EndVersion = _version;
        }

        /// <summary>
        /// 部品を動かしている間の表示（key のページを描くとき）。background の上に parts の画像を順に重ね、その上に foreground（手前の部品。なければ null）を重ねる。
        /// parts の画像は用紙の上の範囲 Area（mm。null なら用紙全体）を描いたもので、それを Part.Map で移して描き、Part.Clip（mm）の外は描かない。
        /// </summary>
        public bool TryGetMovePreview(int key, out BitmapSource background, out BitmapSource? foreground, out List<(BitmapSource Image, ModelRect? Area, PreviewPart Part)> parts)
        {
            var preview = _movePreview;
            if (preview == null || preview.Key != key || preview.Background == null || preview.Moving == null)
            {
                background = null!;
                foreground = null;
                parts = null!;
                return false;
            }
            background = preview.Background;
            foreground = preview.Foreground;
            var moving = preview.Moving;
            parts = new List<(BitmapSource, ModelRect?, PreviewPart)>();
            if (preview.Indexes.Count > 1)
            {
                // いくつかの部品をまとめて動かすときは、ずらすだけ（大きさは変えられない）
                var from = moving.Rendered.Bounds;
                var to = preview.Reference.Bounds;
                parts.Add((moving.Content, moving.Area, new PreviewPart(PreviewLayer.Content, PreviewMap.Translate(to.X - from.X, to.Y - from.Y))));
                return true;
            }
            foreach (var part in MovePreviewGeometry.Parts(moving.Rendered, preview.Reference, moving.ImageAspect))
            {
                var image = part.Layer == PreviewLayer.Decoration ? moving.Decoration : moving.Content;
                if (image != null) parts.Add((image, moving.Area, part));
            }
            return true;
        }

        /// <summary>デザイン画面に見えているページ（ページをつなげて表示しているとき）。デザイン画面が設定する。</summary>
        public void SetVisiblePages(IEnumerable<int> pages)
        {
            var list = pages.ToList();
            if (list.SequenceEqual(_visiblePages)) return;
            _visiblePages = list;
            if (StalePages().Any()) RestartOtherPages();
        }

        private void RestartOtherPages()
        {
            _otherPagesTimer.Stop();
            if (IsContinuous) _otherPagesTimer.Start();
        }

        /// <summary>見えているページのうち、編集しているページ以外で、描き直しが必要なもの。</summary>
        private IEnumerable<int> StalePages()
        {
            if (!IsContinuous) yield break;
            foreach (int k in _visiblePages)
            {
                if (k < 0 || k >= Report.Pages.Count || k == CurrentPageKey) continue;
                if (!_pageImages.TryGetValue(k, out var image) || image.Version != _version || image.Width != PixelWidth(k)) yield return k;
            }
        }

        /// <summary>見えているほかのページを描き直す（編集しているページは RequestRender で描く）。</summary>
        private async void RenderOtherPages()
        {
            if (_otherPagesRunning)
            {
                _otherPagesPending = true;
                return;
            }
            _otherPagesRunning = true;
            try
            {
                do
                {
                    _otherPagesPending = false;
                    var targets = StalePages().Select(k => (Key: k, Width: PixelWidth(k))).ToList();
                    if (targets.Count == 0) break;
                    var clone = ReportSerializer.Clone(Report);
                    var mode = ShowSampleValues ? RenderMode.Preview : RenderMode.Design;
                    string? baseDir = BaseDirectory;
                    int version = _version;
                    bool scripts = _scriptsTrusted;
                    var images = await Task.Run(async () =>
                    {
                        var data = mode == RenderMode.Preview ? ReportData.FromSamples(clone, null, null, scripts) : null;
                        var result = new List<(int Key, int Width, BitmapSource? Bitmap)>();
                        foreach (var (key, width) in targets)
                        {
                            byte[] pdf = ReportRenderer.Render(clone, mode, data, baseDir, null, key, DesignLayer.Page).ToArray();
                            result.Add((key, width, (await PdfRasterizer.RenderAsync(pdf, width, 1)).FirstOrDefault()));
                        }
                        return result;
                    });
                    foreach (var (key, width, bitmap) in images)
                    {
                        if (key < Report.Pages.Count) StoreImage(key, bitmap, version, width);
                    }
                } while (_otherPagesPending);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException || ex is FormatException || ex is InvalidDataException || ex is System.Runtime.InteropServices.COMException)
            {
                // ほかのページを描けなくても、編集は続けられる（問題は編集しているページの描画で表示する）
            }
            finally
            {
                _otherPagesRunning = false;
            }
        }

        /// <summary>ページの縮小画像を描き直す（間引いて、最新の状態だけを描く）。</summary>
        private async void RenderThumbnails()
        {
            if (_thumbnailsRunning)
            {
                _thumbnailsPending = true;
                return;
            }
            _thumbnailsRunning = true;
            try
            {
                do
                {
                    _thumbnailsPending = false;
                    var clone = ReportSerializer.Clone(Report);
                    var mode = ShowSampleValues ? RenderMode.Preview : RenderMode.Design;
                    string? baseDir = BaseDirectory;
                    // 縮小画像は幅 120 DIP 程度で表示する
                    int width = Math.Max(60, (int)Math.Ceiling(PageItem.ThumbnailWidth * DpiScale));
                    bool scripts = _scriptsTrusted;
                    var images = await Task.Run(async () =>
                    {
                        var data = mode == RenderMode.Preview ? ReportData.FromSamples(clone, null, null, scripts) : null;
                        byte[] pdf = ReportRenderer.Render(clone, mode, data, baseDir).ToArray();
                        return await PdfRasterizer.RenderAsync(pdf, width);
                    });
                    for (int i = 0; i < images.Count && i < PageItems.Count; i++) PageItems[i].Thumbnail = images[i];
                } while (_thumbnailsPending);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException || ex is FormatException || ex is InvalidDataException || ex is System.Runtime.InteropServices.COMException)
            {
                // 縮小画像を描けなくても、編集は続けられる（問題はデザイン画面の描画で表示する）
            }
            finally
            {
                _thumbnailsRunning = false;
            }
        }

        /// <summary>画面の拡大率（DPI）。デザイン画面が設定する。</summary>
        public double DpiScale { get; set; } = 1;

        /// <summary>表示倍率（1 = 実寸）。</summary>
        public double Zoom
        {
            get => _zoom;
            set
            {
                if (Set(ref _zoom, Math.Max(0.25, Math.Min(4, value)))) OnPropertyChanged(nameof(ZoomText));
            }
        }

        public string ZoomText => Math.Round(Zoom * 100) + "%";

        /// <summary>グリッドの間隔（mm）。</summary>
        public double GridSize { get => _gridSize; set { if (Set(ref _gridSize, Math.Max(0.1, value))) Invalidate(); } }

        public bool SnapToGrid { get => _snapToGrid; set => Set(ref _snapToGrid, value); }

        public bool ShowGrid { get => _showGrid; set { if (Set(ref _showGrid, value)) Invalidate(); } }

        /// <summary>部品の枠を点線で表示する。</summary>
        public bool ShowOutlines { get => _showOutlines; set { if (Set(ref _showOutlines, value)) Invalidate(); } }

        /// <summary>画面の 1 mm あたりのピクセル数（倍率と DPI を含む）。デザイン画面が設定する。</summary>
        public double PixelsPerMm
        {
            get => _pixelsPerMm;
            set
            {
                if (Math.Abs(_pixelsPerMm - value) < 1e-6) return;
                _pixelsPerMm = value;
                RequestRender();
            }
        }

        public ObservableCollection<ReportElement> Selection { get; } = new ObservableCollection<ReportElement>();

        /// <summary>1 つだけ選んでいる部品（2 つ以上選んでいれば null）。</summary>
        public ReportElement? PrimarySelection
        {
            get => _primary;
            private set
            {
                if (Set(ref _primary, value)) OnPropertyChanged(nameof(PanelTarget));
            }
        }

        // 部品をドラッグしている間にプロパティ欄に表示する、動かす前の部品の複製
        private ReportElement? _heldPanelTarget;

        /// <summary>
        /// プロパティ欄に表示する部品。ふだんは <see cref="PrimarySelection"/> で、部品をドラッグしている間は動かす前の複製
        /// （<see cref="HoldPropertyPanel"/>）。
        /// </summary>
        public ReportElement? PanelTarget => _heldPanelTarget ?? PrimarySelection;

        /// <summary>
        /// ドラッグを終えるまで（<see cref="ReleasePropertyPanel"/>）、プロパティ欄の値を動かす前のままにする。
        /// 欄の入力欄の文字を書き換えるたびに日本語入力（IME）の処理が呼ばれ、ときどき数十ミリ秒止まって、ドラッグがカクつくため。
        /// </summary>
        public void HoldPropertyPanel()
        {
            if (_heldPanelTarget != null || PrimarySelection == null) return;
            _heldPanelTarget = ReportSerializer.CloneElement(PrimarySelection);
            OnPropertyChanged(nameof(PanelTarget));
        }

        /// <summary>プロパティ欄に、今の部品の値を表示する（<see cref="HoldPropertyPanel"/> をやめる）。</summary>
        public void ReleasePropertyPanel()
        {
            if (_heldPanelTarget == null) return;
            _heldPanelTarget = null;
            OnPropertyChanged(nameof(PanelTarget));
        }

        public string SelectionSummary => Selection.Count > 1 ? Selection.Count + " 個を選択" : "";

        public bool HasNoSelection => Selection.Count == 0;

        /// <summary>選択している表のセル。</summary>
        public CellEditor? CellEditor
        {
            get => _cellEditor;
            private set
            {
                if (!Set(ref _cellEditor, value)) return;
                    OnPropertyChanged(nameof(ToolbarTarget));
                Invalidate();
            }
        }

        // ---- 書式バーと左右のパネル ----

        /// <summary>
        /// 上の書式バーに出すもの。表のセル（CellEditor）、選んでいる部品、2 つ以上選んでいれば <see cref="MultipleToolbar"/>、
        /// 何も選んでいなければ <see cref="PageToolbar"/>（全ページ共通の部品を編集しているときは <see cref="CommonToolbar"/>）。
        /// </summary>
        public object ToolbarTarget => Selection.Count > 1 ? MultipleToolbar.Instance
            : (object?)CellEditor ?? (object?)PrimarySelection ?? (_editingCommon ? CommonToolbar.Instance : PageToolbar.Instance);

        /// <summary>右のインスペクター（設定欄）の見出し。</summary>
        public string PropertyPaneTitle => Selection.Count > 1 ? Selection.Count + " 個の部品"
            : PrimarySelection != null ? PrimarySelection.KindName
            : _editingCommon ? "全ページ共通の部品" : "ページと文書";

        private bool _showNavigationPane = true, _showPropertyPane = true;
        private NavigationTab _navigationTab;

        /// <summary>左のパネル（ページと部品の一覧）を表示する。</summary>
        public bool ShowNavigationPane { get => _showNavigationPane; set => Set(ref _showNavigationPane, value); }

        /// <summary>右のインスペクター（設定欄）を表示する。</summary>
        public bool ShowPropertyPane { get => _showPropertyPane; set => Set(ref _showPropertyPane, value); }

        /// <summary>左のパネルで表示している一覧。</summary>
        public NavigationTab NavigationTab { get => _navigationTab; set => Set(ref _navigationTab, value); }

        /// <summary>問題の一覧（デザインの検査と、描画時の警告）。</summary>
        public ObservableCollection<string> Warnings { get; } = new ObservableCollection<string>();

        public string WarningsText => Warnings.Count == 0 ? "問題はありません" : "問題 " + Warnings.Count + " 件";

        /// <summary>この PC で使えるフォント（候補のうち読み込めたもの）。</summary>
        public ObservableCollection<string> FontNames { get; } = new ObservableCollection<string>();

        /// <summary>差し込み欄に使える名前（テキストの差し込みメニュー用）。</summary>
        public IEnumerable<string> InsertableNames
        {
            get
            {
                foreach (var f in Report.Fields) yield return f.Name;
                if (PrimarySelection is TableElement t && CellEditor?.IsDetailRow == true && Report.FindList(t.DetailSource) is ListDefinition list)
                {
                    foreach (var c in list.Columns) yield return c.Name;
                }
                foreach (var b in TextTemplate.BuiltIns) yield return b.Name;
            }
        }

        /// <summary>組み込みの差し込み欄の一覧（差し込みのウィンドウに表示する。例は今の日時で作る）。</summary>
        public IReadOnlyList<BuiltInExample> BuiltInExamples
        {
            get
            {
                var now = DateTime.Now;
                return TextTemplate.BuiltIns.Select(b => new BuiltInExample("{" + b.Name + "}", TextTemplate.FormatBuiltIn(b, null, now, 1, 3), b.Description)).ToList();
            }
        }

        /// <summary>表でくり返せる一覧の名前（先頭の "" はくり返さない）。</summary>
        public IEnumerable<string> DetailSourceNames => new[] { "" }.Concat(Report.Lists.Select(l => l.Name));

        /// <summary>差し込みのウィンドウで選んでいる一覧。</summary>
        public ListDefinition? SelectedList
        {
            get => _selectedList;
            set
            {
                if (!Set(ref _selectedList, value)) return;
                SelectedColumn = value?.Columns.FirstOrDefault();
                RebuildSampleTable();
                OnDataDescriptionChanged();
            }
        }

        /// <summary>差し込みのウィンドウで選んでいる項目。</summary>
        public FieldDefinition? SelectedField
        {
            get => _selectedField;
            set
            {
                if (!Set(ref _selectedField, value)) return;
                OnPropertyChanged(nameof(SelectedFieldCode));
                OnPropertyChanged(nameof(SelectedFieldScriptResult));
            }
        }

        /// <summary>差し込みのウィンドウで選んでいる一覧の列。</summary>
        public FieldDefinition? SelectedColumn
        {
            get => _selectedColumn;
            set { if (Set(ref _selectedColumn, value)) OnPropertyChanged(nameof(SelectedColumnCode)); }
        }

        // ---- 差し込みのウィンドウの表示 ----

        /// <summary>項目か一覧が 1 つでもある（なければ差し込みのウィンドウに初回の案内を出す）。</summary>
        public bool HasDataDefinitions => Report.Fields.Count > 0 || Report.Lists.Count > 0;

        /// <summary>差し込み項目と一覧の数（タイトルバーの「差し込み」に出す）。</summary>
        public int DataDefinitionCount => Report.Fields.Count + Report.Lists.Count;

        /// <summary>差し込みのウィンドウで表示している種類（アプリを起動している間だけ覚えておく）。</summary>
        public DataSection DataSection { get => _dataSection; set => Set(ref _dataSection, value); }

        /// <summary>差し込みのウィンドウで、使い方を表示している。</summary>
        public bool IsGuideOpen { get => _isGuideOpen; set => Set(ref _isGuideOpen, value); }

        /// <summary>使い方を表示している章。</summary>
        public DataSection GuideSection
        {
            get => _guideSection;
            set => Set(ref _guideSection, value);
        }


        /// <summary>書式を付けた組み込みの差し込み欄の例（例は今の日時で作る）。</summary>
        public IReadOnlyList<BuiltInExample> FormatExamples
        {
            get
            {
                var now = DateTime.Now;
                BuiltInExample example(string name, string format)
                    => new BuiltInExample("{" + name + ":" + format + "}", TextTemplate.FormatBuiltIn(TextTemplate.FindBuiltIn(name)!, format, now, 1, 3), "");
                return new[]
                {
                    example("今日", "yyyy年M月d日"),
                    example("今日", "ggy年M月d日"),
                    example(TextTemplate.PageNumber, "000"),
                    example("今月", "00"),
                };
            }
        }

        // ---- 用紙での使用数 ----

        /// <summary>差し込み項目と一覧を、用紙で使っている数（キーは FieldDefinition か ListDefinition）。</summary>
        public IReadOnlyDictionary<object, int> UsageCounts
        {
            get => _usageCounts;
            private set => Set(ref _usageCounts, value);
        }

        /// <summary>項目・一覧の使用数を数え直す。</summary>
        public void RecountUsage()
        {
            _usageTimer.Stop();
            UsageCounts = DataUsage.CountAll(Report);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private int UsageOf(object? target) => target != null && UsageCounts.TryGetValue(target, out int n) ? n : 0;

        /// <summary>
        /// 項目（または一覧）を使っている部品を、用紙の上で選ぶ。今のページになければ、最初に見つかったページへ移る。
        /// ページで使っていなければ、全ページ共通の部品の編集に切り替える。
        /// </summary>
        public void FindInPaper(object? target)
        {
            var locations = target switch
            {
                FieldDefinition field => DataUsage.FindField(Report, field),
                ListDefinition list => DataUsage.FindList(Report, list),
                _ => null,
            };
            if (locations == null || locations.Count == 0) return;
            int key = locations.Any(x => x.PageIndex == CurrentPageKey) ? CurrentPageKey
                : locations.FirstOrDefault(x => !x.IsCommon)?.PageIndex ?? CommonKey;
            // ページを切り替えると選択が消えるので、切り替えてから選ぶ
            if (key == CommonKey) IsEditingCommon = true;
            else CurrentPageIndex = key;
            var found = locations.Where(x => x.PageIndex == key).Select(x => x.Element).ToList();
            SelectMany(found, false);
            RevealRequested?.Invoke(found[0]);
        }

        /// <summary>部品が見えるようにスクロールしてほしい（「用紙から探す」で選んだとき）。</summary>
        public event Action<ReportElement>? RevealRequested;

        // ---- C# の式 ----

        /// <summary>
        /// 差し込み項目の C# の式を実行してよい。新しく作ったデザインと、開くときに「実行する」を選んだファイルだけ true。
        /// ほかの人から受け取ったファイルの式が、勝手に実行されないようにする（Office のマクロと同じ考え方）。
        /// </summary>
        public bool ScriptsTrusted
        {
            get => _scriptsTrusted;
            private set
            {
                if (!Set(ref _scriptsTrusted, value)) return;
                OnPropertyChanged(nameof(ShowScriptsBlocked));
                RequestRender();
                Validate();
                RestartScriptResults();
            }
        }

        /// <summary>C# の式があるのに、実行していない（書式バーの下と差し込みのウィンドウに案内を出す）。</summary>
        public bool ShowScriptsBlocked => !_scriptsTrusted && Report.Fields.Any(f => f.IsComputed);

        /// <summary>選んでいる差し込み項目の C# の式の計算結果（またはエラー）。</summary>
        public string SelectedFieldScriptResult
        {
            get
            {
                var selected = SelectedField;
                if (selected == null || !selected.IsComputed) return "";
                if (!_scriptsTrusted) return "このファイルの C# の式は、まだ実行していません。";
                if (_scriptResults == null) return "計算しています…";
                var errors = _scriptErrors.Where(e => e.Contains("「" + selected.Name + "」") || e.StartsWith("C# の式をコンパイルできません", StringComparison.Ordinal)).ToList();
                if (errors.Count > 0) return string.Join("\n", errors);
                if (!_scriptResults.TryGetValue(selected.Name, out object? value)) return "";
                return "計算結果（今の日時で計算）: " + TextTemplate.FormatValue(value, selected.Kind, TextTemplate.EffectiveFormat(selected.Kind, null, selected.Format));
            }
        }

        private void RestartScriptResults()
        {
            _scriptTimer.Stop();
            if (Report.Fields.Any(f => f.IsComputed)) _scriptTimer.Start();
        }

        /// <summary>C# の式を、今の日時と確認用の値で計算し直す（差し込みのウィンドウに結果を表示する）。</summary>
        private async void EvaluateScripts()
        {
            if (_scriptsRunning)
            {
                _scriptsPending = true;
                return;
            }
            _scriptsRunning = true;
            try
            {
                do
                {
                    _scriptsPending = false;
                    if (!_scriptsTrusted) break;
                    var clone = ReportSerializer.Clone(Report);
                    var errors = new List<string>();
                    var results = await Task.Run(() => ScriptEvaluator.Evaluate(clone, DateTime.Now, errors));
                    _scriptResults = results;
                    _scriptErrors = errors;
                    OnPropertyChanged(nameof(SelectedFieldScriptResult));
                } while (_scriptsPending);
            }
            finally
            {
                _scriptsRunning = false;
            }
        }

        /// <summary>選んでいる項目に、C# のコードでどう値を入れるか。</summary>
        public string SelectedFieldCode
        {
            get
            {
                if (SelectedField == null) return "";
                if (SelectedField.IsComputed) return "（C# の式で計算するので、値は渡しません）";
                var shape = CSharpGenerator.DescribeData(Report);
                if (!shape.Fields.TryGetValue(SelectedField, out var member)) return "（同じ名前の項目が先にあるため、この項目は使われません）";
                return "data." + member.Property + " = " + member.ExampleValue + ";   // " + member.Type;
            }
        }

        /// <summary>選んでいる一覧の 1 件を、C# のコードでどう追加するか。</summary>
        public string SelectedListCode
        {
            get
            {
                if (SelectedList == null) return "";
                var shape = CSharpGenerator.DescribeData(Report);
                if (!shape.Lists.TryGetValue(SelectedList, out var list)) return "（同じ名前の一覧が先にあるため、この一覧は使われません）";
                var columns = SelectedList.Columns.Where(c => list.Columns.ContainsKey(c)).Select(c => list.Columns[c].Property + " = …");
                return "data." + list.Property + ".Add(new " + list.ItemClass + " { " + string.Join(", ", columns) + " });";
            }
        }

        /// <summary>選んでいる列が、1 件のクラスのどのプロパティになるか。</summary>
        public string SelectedColumnCode
        {
            get
            {
                if (SelectedList == null || SelectedColumn == null) return "";
                var shape = CSharpGenerator.DescribeData(Report);
                if (!shape.Lists.TryGetValue(SelectedList, out var list) || !list.Columns.TryGetValue(SelectedColumn, out var member)) return "";
                return list.ItemClass + "." + member.Property + "   // " + member.Type;
            }
        }

        /// <summary>データを作って PDF を保存する C# のコードの例。</summary>
        public string DataExample => CSharpGenerator.DataExample(Report);

        private void OnDataDescriptionChanged()
        {
            OnPropertyChanged(nameof(SelectedFieldCode));
            OnPropertyChanged(nameof(SelectedListCode));
            OnPropertyChanged(nameof(SelectedColumnCode));
            OnPropertyChanged(nameof(DataExample));
        }

        /// <summary><see cref="SampleTable"/> の列の ExtendedProperties で、一覧の列の定義（FieldDefinition）を入れるキー。</summary>
        public const string SampleColumnKey = "Column";

        /// <summary>選んでいる一覧のサンプルの行（表の形で編集する。列の名前は c0, c1, …）。</summary>
        public DataTable? SampleTable { get => _sampleTable; private set => Set(ref _sampleTable, value); }

        private void RebuildSampleTable()
        {
            var list = SelectedList;
            if (list == null)
            {
                SampleTable = null;
                return;
            }
            var table = new DataTable();
            for (int c = 0; c < list.Columns.Count; c++)
            {
                var column = new DataColumn("c" + c, typeof(string)) { Caption = list.Columns[c].Name, DefaultValue = "" };
                // 表の列見出しから、列の定義（名前・種類）を参照できるようにする
                column.ExtendedProperties[SampleColumnKey] = list.Columns[c];
                table.Columns.Add(column);
            }
            foreach (var sample in list.SampleRows)
            {
                var row = table.NewRow();
                for (int c = 0; c < list.Columns.Count; c++) row[c] = c < sample.Count ? sample[c] : "";
                table.Rows.Add(row);
            }
            table.AcceptChanges();
            table.RowChanged += (s, e) => WriteSampleRows(list, table);
            table.RowDeleted += (s, e) => WriteSampleRows(list, table);
            SampleTable = table;
        }

        /// <summary>表で編集したサンプルの行を一覧に書き戻す（すべての値が空の行は除く）。</summary>
        private void WriteSampleRows(ListDefinition list, DataTable table)
        {
            var rows = new List<List<string>>();
            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Deleted || row.RowState == DataRowState.Detached) continue;
                var values = row.ItemArray.Select(v => v as string ?? "").ToList();
                if (values.All(v => v.Length == 0)) continue;
                rows.Add(values);
            }
            if (rows.Count == list.SampleRows.Count && rows.Zip(list.SampleRows, (a, b) => a.SequenceEqual(b)).All(x => x)) return;
            _writingSamples = true;
            try
            {
                list.SampleRows = rows;
            }
            finally
            {
                _writingSamples = false;
            }
        }

        /// <summary>タブ区切りの文字列（Excel からコピーした範囲）を、選んでいる一覧のサンプルの行として追加する。</summary>
        public void PasteSampleRows(string text)
        {
            if (SelectedList == null) return;
            var rows = SelectedList.SampleRows.Select(r => r.ToList()).ToList();
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                rows.Add(line.Split('\t').ToList());
            }
            SelectedList.SampleRows = rows;
        }

        /// <summary>差し込み欄に、サンプルの値を入れて表示する（Word の「結果のプレビュー」のように）。</summary>
        public bool ShowSampleValues
        {
            get => _showSampleValues;
            set { if (Set(ref _showSampleValues, value)) RequestRender(); }
        }

        /// <summary>
        /// フォントの一覧でマウスを重ねたフォントを、デザイン画面に仮に適用する（モデルは変えない）。
        /// target は部品（テキスト・表）か、文書（既定のフォント）。null で元に戻す。
        /// </summary>
        public void SetFontPreview(object? target, string? fontName)
        {
            if (target == null || fontName == null)
            {
                target = null;
                fontName = null;
            }
            if (ReferenceEquals(target, _fontPreview.Target) && fontName == _fontPreview.FontName) return;
            _fontPreview = (target, fontName);
            RequestRender();
        }

        /// <summary>フォントの仮の適用中か（動作確認用）。</summary>
        internal bool IsFontPreviewActive => _fontPreview.FontName != null;

        private void ApplyFontPreview(ReportDefinition clone)
        {
            var (target, name) = _fontPreview;
            if (target == null || name == null) return;
            if (target == Report) clone.DefaultFontName = name;
            else if (target is ReportElement element && Elements.IndexOf(element) is int index && index >= 0)
            {
                var elements = _editingCommon ? clone.Common.Elements : clone.Pages[_pageIndex].Elements;
                if (elements[index] is TextElement t) t.FontName = name;
                else if (elements[index] is TableElement tb) tb.FontName = name;
            }
            else return;
            if (!clone.Fonts.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) && FontCatalog.SuggestBold(name) is string bold)
                clone.Fonts.Add(new FontDefinition { Name = name, BoldName = bold });
        }

        public string CursorText { get => _cursorText; set => Set(ref _cursorText, value); }

        /// <summary>入力途中の値をモデルに反映する（画面が設定する）。保存・出力などの前に呼ぶ。</summary>
        public Action? CommitPendingEdits { get; set; }

        /// <summary>デザイン画面の再描画が必要になった。</summary>
        public event Action? SurfaceInvalidated;

        public void Invalidate() => SurfaceInvalidated?.Invoke();

        // ---- デザインの読み込みと変更の監視 ----

        /// <param name="scriptsTrusted">差し込み項目の C# の式を実行してよい（開いたファイルでは、確かめてから決める）。</param>
        private void SetReport(ReportDefinition report, string? path, bool scriptsTrusted = true)
        {
            _tracker?.Dispose();
            _report = report;
            _scriptsTrusted = scriptsTrusted;
            _scriptResults = null;
            _scriptErrors = new List<string>();
            OnPropertyChanged(nameof(ScriptsTrusted));
            OnPropertyChanged(nameof(ShowScriptsBlocked));
            RestartScriptResults();
            _tracker = new ChangeTracker(report);
            _tracker.Changed += OnModelChanged;
            FilePath = path;
            Selection.Clear();
            CellEditor = null;
            _pageIndex = 0;
            _editingCommon = false;
            _pageImages.Clear();
            _movePreview = null;
            PageItems.Clear();
            SyncPageItems();
            OnPageChanged();
            SelectedList = report.Lists.FirstOrDefault();
            SelectedField = report.Fields.FirstOrDefault();
            _fontPreview = (null, null);
            _undo.Reset(ReportSerializer.ToJson(report));
            IsDirty = false;
            OnPropertyChanged(nameof(Report));
            OnPropertyChanged(nameof(DocumentName));
            OnPropertyChanged(nameof(DetailSourceNames));
            OnPropertyChanged(nameof(HasDataDefinitions));
            OnPropertyChanged(nameof(DataDefinitionCount));
            OnDataDescriptionChanged();
            RecountUsage();
            Validate();
            RequestRender();
        }

        private void OnModelChanged(object sender, string? property)
        {
            IsDirty = true;
            if (property == nameof(TextElement.FontName) || property == nameof(TableElement.FontName) || property == nameof(ReportDefinition.DefaultFontName))
                EnsureFontDefinitions();
            if (sender is ListDefinition || sender == Report.Lists) OnPropertyChanged(nameof(DetailSourceNames));
            if (sender == Report.Fields || sender == Report.Lists)
            {
                OnPropertyChanged(nameof(HasDataDefinitions));
                OnPropertyChanged(nameof(DataDefinitionCount));
            }
            _usageTimer.Stop();
            _usageTimer.Start();
            // 項目・一覧（確認用の値を含む）が変わったら、C# の式を計算し直す
            if (sender is FieldDefinition || sender is ListDefinition || sender == Report.Fields || sender == Report.Lists)
            {
                RestartScriptResults();
                OnPropertyChanged(nameof(ShowScriptsBlocked));
                if (sender == SelectedField) OnPropertyChanged(nameof(SelectedFieldScriptResult));
            }
            if (sender == Report.Pages || sender is ReportPage || sender == Report || sender is PaperSetting) SyncPageItems();
            if (sender == Report || sender is PaperSetting || (sender is ReportPage && property == nameof(ReportPage.Paper))) OnPaperChanged();
            if (sender == Report && property == nameof(ReportDefinition.Name)) OnPropertyChanged(nameof(DocumentName));
            if (sender == Elements || (sender is TableElement && property == nameof(TableElement.DetailSource)))
            {
                OnPropertyChanged(nameof(CurrentPageRepeats));
                OnPropertyChanged(nameof(ShowVisibilityOption));
            }
            // 種類を変えたら、前の種類の書式は使えないので消す（読み込みや元に戻すでは、ここを通らない）
            if (sender is FieldDefinition changed && property == nameof(FieldDefinition.Kind) && changed.Format != null) changed.Format = null;
            if (SelectedList != null && !Report.Lists.Contains(SelectedList)) SelectedList = Report.Lists.FirstOrDefault();
            else if (SelectedList != null && ((sender == SelectedList && property == nameof(ListDefinition.SampleRows) && !_writingSamples)
                || sender == SelectedList.Columns || (sender is FieldDefinition column && property == nameof(FieldDefinition.Name) && SelectedList.Columns.Contains(column))))
                RebuildSampleTable();
            if (SelectedField != null && !Report.Fields.Contains(SelectedField)) SelectedField = Report.Fields.FirstOrDefault();
            if (SelectedColumn != null && SelectedList?.Columns.Contains(SelectedColumn) != true) SelectedColumn = SelectedList?.Columns.FirstOrDefault();
            OnDataDescriptionChanged();
            CellEditor?.Refresh();
            _snapshotTimer.Stop();
            _snapshotTimer.Start();
            Validate();
            RequestRender();
            Invalidate();
        }

        /// <summary>操作（ドラッグなど）の途中は元に戻すの記録をしない。</summary>
        public void BeginInteraction() => _interactionDepth++;

        public void EndInteraction()
        {
            if (_interactionDepth > 0) _interactionDepth--;
            if (_interactionDepth == 0) CommitSnapshot();
        }

        private void CommitSnapshot()
        {
            _snapshotTimer.Stop();
            if (_undo.Commit(ReportSerializer.ToJson(Report))) System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RestoreSnapshot(string json)
        {
            var selected = Selection.Select(e => Elements.IndexOf(e)).ToList();
            int listIndex = SelectedList != null ? Report.Lists.IndexOf(SelectedList) : -1;
            int fieldIndex = SelectedField != null ? Report.Fields.IndexOf(SelectedField) : -1;
            string? path = FilePath;
            _tracker?.Dispose();
            _report = ReportSerializer.FromJson(json);
            _tracker = new ChangeTracker(_report);
            _tracker.Changed += OnModelChanged;
            Selection.Clear();
            CellEditor = null;
            _pageIndex = Math.Max(0, Math.Min(_report.Pages.Count - 1, _pageIndex));
            SyncPageItems();
            OnPageChanged();
            foreach (int i in selected)
            {
                if (i >= 0 && i < Elements.Count) Selection.Add(Elements[i]);
            }
            _selectedList = null;
            SelectedList = listIndex >= 0 && listIndex < _report.Lists.Count ? _report.Lists[listIndex] : _report.Lists.FirstOrDefault();
            SelectedField = fieldIndex >= 0 && fieldIndex < _report.Fields.Count ? _report.Fields[fieldIndex] : _report.Fields.FirstOrDefault();
            OnPropertyChanged(nameof(Report));
            OnPropertyChanged(nameof(DetailSourceNames));
            OnPropertyChanged(nameof(HasDataDefinitions));
            OnPropertyChanged(nameof(DataDefinitionCount));
            RecountUsage();
            FilePath = path;
            IsDirty = true;
            Validate();
            RequestRender();
        }

        /// <summary>使われているフォントに太字の対応がなければ、分かっている組み合わせを追加する。</summary>
        private void EnsureFontDefinitions()
        {
            var names = new List<string> { Report.DefaultFontName };
            foreach (var e in Report.AllElements())
            {
                if (e is TextElement t && t.FontName != null) names.Add(t.FontName);
                if (e is TableElement tb && tb.FontName != null) names.Add(tb.FontName);
            }
            foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Report.Fonts.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                string? bold = FontCatalog.SuggestBold(name);
                if (bold != null) Report.Fonts.Add(new FontDefinition { Name = name, BoldName = bold });
            }
        }

        private void LoadFontsInBackground()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            Task.Run(() =>
            {
                var names = FontCatalog.Candidates.Where(c => FontProvider.TryCheck(c.Name, out _)).Select(c => c.Name).ToList();
                dispatcher.BeginInvoke(() =>
                {
                    foreach (string name in names) FontNames.Add(name);
                });
            });
        }

        // ---- 検査と描画 ----

        private void Validate()
        {
            var messages = ReportValidator.Validate(Report);
            if (ShowScriptsBlocked) messages.Add("このファイルの C# の式は実行していません（計算する差し込み項目は空になります）。書式バーの下に出る「C# の式を実行する」で実行できます。");
            foreach (string w in _renderWarnings)
            {
                if (!messages.Contains(w)) messages.Add(w);
            }
            if (messages.SequenceEqual(Warnings)) return;
            Warnings.Clear();
            foreach (string m in messages) Warnings.Add(m);
            OnPropertyChanged(nameof(WarningsText));
        }

        /// <summary>デザイン画面に表示するページを描き直す（間引いて、最新の状態だけを描く）。</summary>
        public async void RequestRender()
        {
            _version++;
            // 部品を動かしている間は、ドラッグを終えるまでページを描き直さない（重ねる画像ができるまでは、動かす前の表示のまま）。
            // 大きさを変えているときは、その部品の画像だけを描き直す
            if (_movePreview != null && _movePreview.EndVersion == null)
            {
                _movePreview.Deferred = true;
                if (_movePreview.Resizing && _movePreview.Moving != null) RenderMovingLayer(_movePreview);
                return;
            }
            if (_renderRunning)
            {
                _renderPending = true;
                return;
            }
            _renderRunning = true;
            try
            {
                do
                {
                    _renderPending = false;
                    var clone = ReportSerializer.Clone(Report);
                    ApplyFontPreview(clone);
                    var mode = ShowSampleValues ? RenderMode.Preview : RenderMode.Design;
                    string? baseDir = BaseDirectory;
                    int pageIndex = _pageIndex;
                    int key = CurrentPageKey;
                    int version = _version;
                    var layer = _editingCommon ? DesignLayer.Common : DesignLayer.Page;
                    int width = PixelWidth(key);
                    var warnings = new List<string>();
                    bool scripts = _scriptsTrusted;
                    var bitmap = await Task.Run(async () =>
                    {
                        var data = mode == RenderMode.Preview ? ReportData.FromSamples(clone, warnings, null, scripts) : null;
                        byte[] pdf = ReportRenderer.Render(clone, mode, data, baseDir, warnings, pageIndex, layer).ToArray();
                        var pages = await PdfRasterizer.RenderAsync(pdf, width, 1);
                        return pages.FirstOrDefault();
                    });
                    StoreImage(key, bitmap, version, width);
                    _renderWarnings = warnings;
                    Validate();
                    _thumbnailTimer.Stop();
                    _thumbnailTimer.Start();
                    RestartOtherPages();
                } while (_renderPending);
            }
            catch (Exception ex)
            {
                _renderWarnings = new List<string> { "ページを描けません: " + ex.Message };
                Validate();
            }
            finally
            {
                _renderRunning = false;
            }
        }

        // ---- 選択 ----

        private void OnSelectionChanged()
        {
            PrimarySelection = Selection.Count == 1 ? Selection[0] : null;
            SchedulePrepare();
            if (CellEditor != null && PrimarySelection != CellEditor.Table) CellEditor = null;
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(HasNoSelection));
            OnPropertyChanged(nameof(ToolbarTarget));
            OnPropertyChanged(nameof(PropertyPaneTitle));
            OnPropertyChanged(nameof(InsertableNames));
            Invalidate();
        }

        public void Select(ReportElement? element, bool add = false)
        {
            if (!add) Selection.Clear();
            if (element == null) return;
            if (add && Selection.Contains(element)) Selection.Remove(element);
            else if (!Selection.Contains(element)) Selection.Add(element);
        }

        public void SelectMany(IEnumerable<ReportElement> elements, bool add)
        {
            if (!add) Selection.Clear();
            foreach (var e in elements)
            {
                if (!Selection.Contains(e)) Selection.Add(e);
            }
        }

        /// <summary>表のセルを選ぶ。range が null ならセルの選択をやめる。</summary>
        public void SelectCells(TableElement table, CellRange? range)
        {
            if (range == null)
            {
                CellEditor = null;
            }
            else
            {
                var expanded = TableEditing.ExpandToMerges(table, range.Value);
                if (CellEditor == null || CellEditor.Table != table || !CellEditor.Range.Equals(expanded))
                    CellEditor = new CellEditor(table, expanded);
            }
            OnPropertyChanged(nameof(InsertableNames));
        }

        // ---- 部品の作成と編集 ----

        /// <summary>ツールで描いた範囲に部品を作る。</summary>
        public void CreateElement(Tool tool, double x1, double y1, double x2, double y2)
        {
            var r = ModelRect.FromPoints(x1, y1, x2, y2);
            bool tiny = r.Width < 2 && r.Height < 2;
            ReportElement element;
            switch (tool)
            {
                case Tool.Text:
                    element = new TextElement { X = r.X, Y = r.Y, Width = tiny ? 40 : Math.Max(2, r.Width), Height = tiny ? 8 : Math.Max(2, r.Height), FontName = null };
                    break;
                case Tool.Line:
                    element = tiny
                        ? new LineElement { X1 = x1, Y1 = y1, X2 = x1 + 50, Y2 = y1 }
                        : new LineElement { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
                    break;
                case Tool.Rectangle:
                    element = new RectangleElement { X = r.X, Y = r.Y, Width = tiny ? 30 : r.Width, Height = tiny ? 20 : r.Height };
                    break;
                case Tool.Ellipse:
                    element = new EllipseElement { X = r.X, Y = r.Y, Width = tiny ? 20 : r.Width, Height = tiny ? 20 : r.Height };
                    break;
                case Tool.Image:
                    {
                        string? path = PickImage();
                        if (path == null) return;
                        double w = r.Width, h = r.Height;
                        if (tiny)
                        {
                            var img = PdfImage.FromFile(Path.IsPathRooted(path) ? path : Path.Combine(BaseDirectory ?? "", path));
                            w = 40;
                            h = 40 * img.Height / img.Width;
                        }
                        element = new ImageElement { X = r.X, Y = r.Y, Width = w, Height = h, Path = path };
                        break;
                    }
                case Tool.Table:
                    {
                        int columns = 3, rows = 3;
                        double cw = tiny || r.Width < 10 ? 30 : r.Width / columns;
                        double rh = tiny || r.Height < 10 ? 7 : r.Height / rows;
                        var table = TableElement.Create(columns, rows, Math.Round(cw, 1), Math.Round(rh, 1));
                        table.X = r.X;
                        table.Y = r.Y;
                        element = table;
                        break;
                    }
                default:
                    return;
            }
            AddElement(element);
            Tool = Tool.Select;
        }

        private void AddElement(ReportElement element)
        {
            if (element.Name.Length == 0) element.Name = UniqueName(element.KindName);
            Elements.Add(element);
            Select(element);
            CommitSnapshot();
        }

        /// <summary>(x, y) mm を左上にして、既定の大きさの部品を置く（右クリックの「挿入」）。</summary>
        public void InsertAt(Tool tool, double x, double y) => CreateElement(tool, x, y, x, y);

        /// <summary>差し込み欄 {name} だけを書いたテキストを置く。</summary>
        public void InsertFieldText(string name, double x, double y)
        {
            AddElement(new TextElement { X = Math.Round(x, 2), Y = Math.Round(y, 2), Width = 50, Height = 8, Text = "{" + name + "}" });
            Tool = Tool.Select;
        }

        /// <summary>文字を書いたテキストを置く（「ページ番号」などで使う）。</summary>
        public void InsertText(string text, double x, double y, double width, TextAlignment alignment)
        {
            AddElement(new TextElement { X = Math.Round(x, 2), Y = Math.Round(y, 2), Width = width, Height = 8, Text = text, Alignment = alignment });
            Tool = Tool.Select;
        }

        /// <summary>
        /// 一覧の列から、一覧をくり返す表を作って置く。見出しの行に列の名前、くり返し行に {列名} が入る。
        /// 位置を省略すると、左の余白 15 mm で、ほかの部品の下に置く。
        /// </summary>
        public void InsertDetailTable(ListDefinition list, double? x = null, double? y = null)
        {
            double left = x ?? 15;
            double top = y ?? Math.Min(CurrentPageHeight - 60, Elements.Count == 0 ? 20 : Elements.Max(e => e.Bounds.Bottom) + 5);
            top = Math.Max(0, top);
            double width = Math.Max(40, CurrentPageWidth - left - 15);
            // ページに収まる行数（見出しの 1 行を除く）。多くても 15 行
            int rows = (int)Math.Floor((CurrentPageHeight - top - 15) / 7) - 1;
            var table = TableEditing.CreateDetailTable(list, width, Math.Max(1, Math.Min(15, rows)));
            table.X = Math.Round(left, 2);
            table.Y = Math.Round(top, 2);
            AddElement(table);
            Tool = Tool.Select;
        }

        /// <summary>選択している部品を描くページを変える。</summary>
        public void SetVisibility(PageVisibility visibility)
        {
            foreach (var e in Selection) e.Visibility = visibility;
            CommitSnapshot();
        }

        private string UniqueName(string baseName)
        {
            for (int i = 1; ; i++)
            {
                string name = baseName + i;
                if (!Report.AllElements().Any(e => e.Name == name)) return name;
            }
        }

        /// <summary>画像ファイルを選ぶ。デザインを保存済みなら、そのフォルダからの相対パスにする。</summary>
        public string? PickImage()
        {
            var dialog = new OpenFileDialog { Filter = "画像 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|すべてのファイル (*.*)|*.*", Title = "画像を選択" };
            if (dialog.ShowDialog() != true) return null;
            return ToStoredPath(dialog.FileName);
        }

        private string ToStoredPath(string fullPath)
        {
            if (BaseDirectory == null) return fullPath;
            string rel = Path.GetRelativePath(BaseDirectory, fullPath);
            return Path.IsPathRooted(rel) ? fullPath : rel;
        }

        /// <summary>選択している部品を、元の位置から (dx, dy) だけ動かす。</summary>
        public void MoveSelection(double dx, double dy)
        {
            foreach (var e in Selection) e.MoveBy(dx, dy);
        }

        /// <summary>
        /// 選択している部品を、同じ位置にコピーして、コピーを選択し直す（Ctrl を押しながらドラッグして、元の位置に部品を残したまま動かすとき）。
        /// 元に戻すの記録は、操作（BeginInteraction ～ EndInteraction）の最後にまとめる。
        /// </summary>
        public void CopySelectionInPlace()
        {
            if (Selection.Count == 0) return;
            var copies = ReportSerializer.ElementsFromJson(ReportSerializer.ElementsToJson(Ordered(Selection)));
            Selection.Clear();
            foreach (var e in copies)
            {
                e.Name = UniqueName(e.KindName);
                Elements.Add(e);
                Selection.Add(e);
            }
        }

        /// <summary>太字を切り替える（Ctrl+B）。選んでいるセルがあればセル、なければ選んでいるテキスト。全部が太字なら解除し、そうでなければ太字にする。</summary>
        public void ToggleBold()
        {
            if (CellEditor != null)
            {
                CellEditor.Bold = CellEditor.Bold != true;
            }
            else
            {
                var texts = Selection.OfType<TextElement>().ToList();
                if (texts.Count == 0) return;
                bool on = !texts.All(t => t.Bold);
                foreach (var t in texts) t.Bold = on;
            }
            if (_interactionDepth == 0) CommitSnapshot();
        }

        /// <summary>斜体を切り替える（Ctrl+I）。選んでいるテキストが対象（表のセルには斜体がない）。</summary>
        public void ToggleItalic()
        {
            if (CellEditor != null) return;
            var texts = Selection.OfType<TextElement>().ToList();
            if (texts.Count == 0) return;
            bool on = !texts.All(t => t.Italic);
            foreach (var t in texts) t.Italic = on;
            if (_interactionDepth == 0) CommitSnapshot();
        }

        // ---- コマンド ----

        public RelayCommand NewCommand { get; private set; } = null!;
        public RelayCommand NewSampleCommand { get; private set; } = null!;
        public RelayCommand OpenCommand { get; private set; } = null!;
        public RelayCommand SaveCommand { get; private set; } = null!;
        public RelayCommand SaveAsCommand { get; private set; } = null!;
        public RelayCommand ExportPdfCommand { get; private set; } = null!;
        public RelayCommand UndoCommand { get; private set; } = null!;
        public RelayCommand RedoCommand { get; private set; } = null!;
        public RelayCommand CopyCommand { get; private set; } = null!;
        public RelayCommand CutCommand { get; private set; } = null!;
        public RelayCommand PasteCommand { get; private set; } = null!;
        public RelayCommand DuplicateCommand { get; private set; } = null!;
        public RelayCommand DeleteCommand { get; private set; } = null!;
        public RelayCommand SelectAllCommand { get; private set; } = null!;
        public RelayCommand BringToFrontCommand { get; private set; } = null!;
        public RelayCommand BringForwardCommand { get; private set; } = null!;
        public RelayCommand SendBackwardCommand { get; private set; } = null!;
        public RelayCommand SendToBackCommand { get; private set; } = null!;
        public RelayCommand AlignCommand { get; private set; } = null!;
        public RelayCommand SetToolCommand { get; private set; } = null!;
        public RelayCommand ZoomInCommand { get; private set; } = null!;
        public RelayCommand ZoomOutCommand { get; private set; } = null!;
        public RelayCommand ZoomResetCommand { get; private set; } = null!;
        public RelayCommand TableCommand { get; private set; } = null!;
        public RelayCommand BrowseImageCommand { get; private set; } = null!;
        public RelayCommand AddFieldCommand { get; private set; } = null!;
        public RelayCommand RemoveFieldCommand { get; private set; } = null!;
        public RelayCommand AddListCommand { get; private set; } = null!;
        public RelayCommand RemoveListCommand { get; private set; } = null!;
        public RelayCommand AddColumnCommand { get; private set; } = null!;
        public RelayCommand RemoveColumnCommand { get; private set; } = null!;
        public RelayCommand AddFontCommand { get; private set; } = null!;
        public RelayCommand InsertDetailTableCommand { get; private set; } = null!;
        public RelayCommand RemoveFontCommand { get; private set; } = null!;
        public RelayCommand AddPageCommand { get; private set; } = null!;
        public RelayCommand DuplicatePageCommand { get; private set; } = null!;
        public RelayCommand DeletePageCommand { get; private set; } = null!;
        public RelayCommand MovePageUpCommand { get; private set; } = null!;
        public RelayCommand MovePageDownCommand { get; private set; } = null!;
        public RelayCommand PreviousPageCommand { get; private set; } = null!;
        public RelayCommand NextPageCommand { get; private set; } = null!;
        public RelayCommand EditCommonCommand { get; private set; } = null!;
        public RelayCommand TrustScriptsCommand { get; private set; } = null!;
        public RelayCommand FindInPaperCommand { get; private set; } = null!;
        public RelayCommand CloseGuideCommand { get; private set; } = null!;
        public RelayCommand ShowPageSettingsCommand { get; private set; } = null!;

        private void CreateCommands()
        {
            bool hasSelection() => Selection.Count > 0;
            NewCommand = new RelayCommand(() => { if (ConfirmDiscard()) SetReport(SampleReports.CreateBlank(), null); });
            NewSampleCommand = new RelayCommand(p =>
            {
                if (!ConfirmDiscard()) return;
                SetReport((p as string) switch
                {
                    "Document" => SampleReports.CreateDocument(),
                    "Letter" => SampleReports.CreateLetter(),
                    _ => SampleReports.CreateInvoice(),
                }, null);
            });
            OpenCommand = new RelayCommand(() => Open(null));
            SaveCommand = new RelayCommand(() => Save(false));
            SaveAsCommand = new RelayCommand(() => Save(true));
            ExportPdfCommand = new RelayCommand(() => { CommitPendingEdits?.Invoke(); ExportPdf(); });
            UndoCommand = new RelayCommand(() => { CommitPendingEdits?.Invoke(); Undo(); }, () => _undo.CanUndo || IsDirty);
            RedoCommand = new RelayCommand(Redo, () => _undo.CanRedo);
            CopyCommand = new RelayCommand(Copy, hasSelection);
            CutCommand = new RelayCommand(() => { Copy(); Delete(); }, hasSelection);
            PasteCommand = new RelayCommand(Paste, () => Clipboard.ContainsData(ClipboardFormat));
            DuplicateCommand = new RelayCommand(() => Paste(ReportSerializer.ElementsToJson(Ordered(Selection))), hasSelection);
            DeleteCommand = new RelayCommand(Delete, hasSelection);
            SelectAllCommand = new RelayCommand(() => SelectMany(Elements, false));
            BringToFrontCommand = new RelayCommand(() => Reorder(int.MaxValue), hasSelection);
            BringForwardCommand = new RelayCommand(() => Reorder(1), hasSelection);
            SendBackwardCommand = new RelayCommand(() => Reorder(-1), hasSelection);
            SendToBackCommand = new RelayCommand(() => Reorder(int.MinValue), hasSelection);
            AlignCommand = new RelayCommand(p => Align(p as string ?? ""), _ => Selection.Count > 1);
            SetToolCommand = new RelayCommand(p => Tool = p is Tool t ? t : Enum.TryParse<Tool>(p as string, out var x) ? x : Tool.Select);
            ZoomInCommand = new RelayCommand(() => Zoom = Math.Round(Zoom * 1.25, 2));
            ZoomOutCommand = new RelayCommand(() => Zoom = Math.Round(Zoom / 1.25, 2));
            ZoomResetCommand = new RelayCommand(() => Zoom = 1);
            TableCommand = new RelayCommand(p => TableOperation(p as string ?? ""), _ => PrimarySelection is TableElement);
            BrowseImageCommand = new RelayCommand(() =>
            {
                if (PrimarySelection is ImageElement image && PickImage() is string path) image.Path = path;
            });
            AddFieldCommand = new RelayCommand(() =>
            {
                var field = new FieldDefinition { Name = UniqueFieldName("項目", Report.Fields) };
                Report.Fields.Add(field);
                SelectedField = field;
                DataSection = DataSection.Fields;
            });
            RemoveFieldCommand = new RelayCommand(p => { if ((p as FieldDefinition ?? SelectedField) is FieldDefinition f) Report.Fields.Remove(f); }, p => (p as FieldDefinition ?? SelectedField) != null);
            AddListCommand = new RelayCommand(() =>
            {
                var list = new ListDefinition { Name = UniqueListName() };
                list.Columns.Add(new FieldDefinition { Name = "名前" });
                Report.Lists.Add(list);
                SelectedList = list;
                DataSection = DataSection.Lists;
            });
            RemoveListCommand = new RelayCommand(() =>
            {
                if (SelectedList == null) return;
                Report.Lists.Remove(SelectedList);
                SelectedList = Report.Lists.FirstOrDefault();
            }, () => SelectedList != null);
            AddColumnCommand = new RelayCommand(() =>
            {
                if (SelectedList == null) return;
                var column = new FieldDefinition { Name = UniqueFieldName("列", SelectedList.Columns) };
                SelectedList.Columns.Add(column);
                SelectedColumn = column;
            }, () => SelectedList != null);
            RemoveColumnCommand = new RelayCommand(p =>
            {
                if (SelectedList == null || (p as FieldDefinition ?? SelectedColumn) is not FieldDefinition column) return;
                int index = SelectedList.Columns.IndexOf(column);
                SelectedList.Columns.RemoveAt(index);
                // サンプルの行からも、その列の値を取り除く
                var rows = SelectedList.SampleRows.Select(r => r.Where((_, i) => i != index).ToList()).ToList();
                SelectedList.SampleRows = rows;
            }, p => (p as FieldDefinition ?? SelectedColumn) != null);
            AddFontCommand = new RelayCommand(() => Report.Fonts.Add(new FontDefinition { Name = Report.DefaultFontName }));
            InsertDetailTableCommand = new RelayCommand(p =>
            {
                if ((p as ListDefinition ?? SelectedList) is ListDefinition list) InsertDetailTable(list);
            }, p => !_editingCommon && (p as ListDefinition ?? SelectedList) != null);
            RemoveFontCommand = new RelayCommand(p => { if (p is FontDefinition f) Report.Fonts.Remove(f); }, p => p is FontDefinition);
            AddPageCommand = new RelayCommand(AddPage);
            // 全ページ共通の部品を編集しているときは、どのページを操作するか分かりにくいので、ページの複製・削除・移動はしない
            DuplicatePageCommand = new RelayCommand(DuplicatePage, () => !_editingCommon);
            DeletePageCommand = new RelayCommand(DeletePage, () => !_editingCommon && Report.Pages.Count > 1);
            MovePageUpCommand = new RelayCommand(() => MovePage(-1), () => !_editingCommon && _pageIndex > 0);
            MovePageDownCommand = new RelayCommand(() => MovePage(1), () => !_editingCommon && _pageIndex < Report.Pages.Count - 1);
            EditCommonCommand = new RelayCommand(() => IsEditingCommon = !IsEditingCommon);
            TrustScriptsCommand = new RelayCommand(() =>
            {
                string message = "このファイルの C# の式を実行しますか？\n\nC# の式は、この PC の上でプログラムとして動きます。信頼できるファイルのときだけ実行してください。";
                if (MessageBox.Show(Application.Current.MainWindow, message, "C# の式の実行", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                    ScriptsTrusted = true;
            }, () => !_scriptsTrusted);
            FindInPaperCommand = new RelayCommand(FindInPaper, p => UsageOf(p) > 0);
            CloseGuideCommand = new RelayCommand(() => IsGuideOpen = false);
            // 部品の選択をやめると、インスペクターにページと文書の設定（用紙・フォントなど）が出る
            ShowPageSettingsCommand = new RelayCommand(() =>
            {
                Select(null);
                ShowPropertyPane = true;
            });
            PreviousPageCommand = new RelayCommand(() => CurrentPageIndex--, () => _pageIndex > 0);
            NextPageCommand = new RelayCommand(() => CurrentPageIndex++, () => _pageIndex < Report.Pages.Count - 1);
        }

        private static string UniqueFieldName(string baseName, IEnumerable<FieldDefinition> existing)
        {
            for (int i = 1; ; i++)
            {
                string name = baseName + i;
                if (!existing.Any(f => f.Name == name)) return name;
            }
        }

        private string UniqueListName()
        {
            for (int i = 1; ; i++)
            {
                string name = i == 1 ? "一覧" : "一覧" + i;
                if (Report.FindList(name) == null) return name;
            }
        }

        private List<ReportElement> Ordered(IEnumerable<ReportElement> elements)
            => elements.OrderBy(e => Elements.IndexOf(e)).ToList();

        // ---- ファイル ----

        /// <summary>未保存の変更があれば、保存するか尋ねる。続けてよければ true。</summary>
        public bool ConfirmDiscard()
        {
            CommitPendingEdits?.Invoke();
            if (!IsDirty) return true;
            var answer = MessageBox.Show(Application.Current.MainWindow, "変更を保存しますか？", "ChohyoPdf Designer", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.Yes) return Save(false);
            return true;
        }

        public void Open(string? path)
        {
            if (!ConfirmDiscard()) return;
            if (path == null)
            {
                var dialog = new OpenFileDialog { Filter = "ChohyoPdf デザイン (*" + ReportSerializer.FileExtension + ")|*" + ReportSerializer.FileExtension + "|すべてのファイル (*.*)|*.*" };
                if (dialog.ShowDialog() != true) return;
                path = dialog.FileName;
            }
            try
            {
                var loaded = ReportSerializer.Load(path);
                SetReport(loaded, path, ConfirmScripts(loaded, path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is System.Text.Json.JsonException)
            {
                MessageBox.Show(Application.Current.MainWindow, "ファイルを開けません。\n" + ex.Message, "ChohyoPdf Designer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 開いたファイルに C# の式があれば、実行してよいか尋ねる。式がなければ true。
        /// 式は利用者が書いた C# のコードで、PC の上で何でもできるため、信頼できるファイルのときだけ実行する。
        /// </summary>
        private static bool ConfirmScripts(ReportDefinition report, string path)
        {
            var scripts = report.Fields.Where(f => f.IsComputed).Select(f => f.Name).ToList();
            if (scripts.Count == 0) return true;
            string message = "「" + Path.GetFileName(path) + "」には、値を計算する C# の式があります（差し込み項目: " + string.Join("、", scripts.Take(5)) + (scripts.Count > 5 ? " ほか" : "") + "）。\n\n"
                + "C# の式は、この PC の上でプログラムとして動きます。自分で作ったファイルや、信頼できる人から受け取ったファイルのときだけ実行してください。\n\n"
                + "C# の式を実行しますか？（「いいえ」でも、デザインの表示と編集はできます。あとから書式バーの下に出る「C# の式を実行する」で実行できます）";
            return MessageBox.Show(Application.Current.MainWindow, message, "C# の式の実行", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        public bool Save(bool saveAs)
        {
            CommitPendingEdits?.Invoke();
            string? path = FilePath;
            if (saveAs || path == null)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "ChohyoPdf デザイン (*" + ReportSerializer.FileExtension + ")|*" + ReportSerializer.FileExtension,
                    FileName = (Report.Name.Length > 0 ? Report.Name : "文書") + ReportSerializer.FileExtension,
                };
                if (dialog.ShowDialog() != true) return false;
                path = dialog.FileName;
            }
            try
            {
                RebaseImagePaths(BaseDirectory, Path.GetDirectoryName(path));
                ReportSerializer.Save(Report, path);
                FilePath = path;
                IsDirty = false;
                CommitSnapshot();
                RequestRender();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(Application.Current.MainWindow, "保存できません。\n" + ex.Message, "ChohyoPdf Designer", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>保存先のフォルダが変わったら、画像の相対パスを新しいフォルダから見たものにする。</summary>
        private void RebaseImagePaths(string? oldDir, string? newDir)
        {
            if (newDir == null || string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var image in Report.AllElements().OfType<ImageElement>())
            {
                if (image.Path.Length == 0) continue;
                string full = Path.IsPathRooted(image.Path) ? image.Path : Path.GetFullPath(Path.Combine(oldDir ?? Directory.GetCurrentDirectory(), image.Path));
                string rel = Path.GetRelativePath(newDir, full);
                image.Path = Path.IsPathRooted(rel) ? full : rel;
            }
        }

        private void ExportPdf()
        {
            var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = (Report.Name.Length > 0 ? Report.Name : "文書") + ".pdf" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var warnings = new List<string>();
                ReportRenderer.RenderSample(Report, BaseDirectory, warnings, null, _scriptsTrusted).Save(dialog.FileName);
                string message = "PDF を保存しました。開きますか？";
                if (warnings.Count > 0) message = "PDF を保存しましたが、次の問題があります。\n\n" + string.Join("\n", warnings.Take(10)) + "\n\nPDF を開きますか？";
                if (MessageBox.Show(Application.Current.MainWindow, message, "PDF を出力", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidDataException || ex is FormatException || ex is ArgumentException || ex is InvalidOperationException)
            {
                MessageBox.Show(Application.Current.MainWindow, "PDF を作れません。\n" + ex.Message, "PDF を出力", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- 編集 ----

        private void Undo()
        {
            CommitSnapshot();
            string? state = _undo.Undo();
            if (state != null) RestoreSnapshot(state);
        }

        private void Redo()
        {
            string? state = _undo.Redo();
            if (state != null) RestoreSnapshot(state);
        }

        private void Copy()
        {
            string json = ReportSerializer.ElementsToJson(Ordered(Selection));
            var data = new DataObject();
            data.SetData(ClipboardFormat, json);
            Clipboard.SetDataObject(data, true);
        }

        private void Paste()
        {
            if (Clipboard.GetData(ClipboardFormat) is string json) Paste(json);
        }

        /// <summary>貼り付ける部品の左上が (x, y) mm に来るように貼り付ける（右クリックの「ここに貼り付け」）。</summary>
        public void PasteAt(double x, double y)
        {
            if (Clipboard.GetData(ClipboardFormat) is string json) Paste(json, x, y);
        }

        private void Paste(string json, double? x = null, double? y = null)
        {
            List<ReportElement> elements;
            try
            {
                elements = ReportSerializer.ElementsFromJson(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return;
            }
            Selection.Clear();
            double dx = 5, dy = 5;
            if (x != null && y != null && elements.Count > 0)
            {
                dx = x.Value - elements.Min(e => e.Bounds.X);
                dy = y.Value - elements.Min(e => e.Bounds.Y);
            }
            foreach (var e in elements)
            {
                e.MoveBy(dx, dy);
                if (Report.AllElements().Any(x => x.Name == e.Name)) e.Name = UniqueName(e.KindName);
                Elements.Add(e);
                Selection.Add(e);
            }
            CommitSnapshot();
        }

        private void Delete()
        {
            foreach (var e in Selection.ToList()) Elements.Remove(e);
            Selection.Clear();
            CommitSnapshot();
        }

        private void Reorder(int delta)
        {
            var items = Ordered(Selection);
            if (delta > 0) items.Reverse();
            foreach (var e in items)
            {
                int index = Elements.IndexOf(e);
                int target = delta == int.MaxValue ? Elements.Count - 1 : delta == int.MinValue ? 0 : index + delta;
                target = Math.Max(0, Math.Min(Elements.Count - 1, target));
                if (target != index) Elements.Move(index, target);
            }
            CommitSnapshot();
        }

        private void Align(string mode)
        {
            var bounds = Selection.Select(e => e.Bounds).ToList();
            double left = bounds.Min(b => b.X), top = bounds.Min(b => b.Y), right = bounds.Max(b => b.Right), bottom = bounds.Max(b => b.Bottom);
            foreach (var e in Selection)
            {
                var b = e.Bounds;
                switch (mode)
                {
                    case "Left": e.MoveBy(left - b.X, 0); break;
                    case "Right": e.MoveBy(right - b.Right, 0); break;
                    case "Top": e.MoveBy(0, top - b.Y); break;
                    case "Bottom": e.MoveBy(0, bottom - b.Bottom); break;
                    case "Center": e.MoveBy((left + right) / 2 - (b.X + b.Right) / 2, 0); break;
                    case "Middle": e.MoveBy(0, (top + bottom) / 2 - (b.Y + b.Bottom) / 2); break;
                }
            }
            CommitSnapshot();
        }

        private void TableOperation(string operation)
        {
            if (PrimarySelection is not TableElement t) return;
            var range = CellEditor?.Range ?? new CellRange(0, 0, 0, 0);
            switch (operation)
            {
                case "InsertColumnLeft": TableEditing.InsertColumn(t, range.Left, t.Columns[range.Left].Width); break;
                case "InsertColumnRight": TableEditing.InsertColumn(t, range.Right + 1, t.Columns[range.Right].Width); break;
                case "DeleteColumn":
                    for (int c = range.Right; c >= range.Left; c--) TableEditing.DeleteColumn(t, c);
                    break;
                case "InsertRowAbove":
                    TableEditing.InsertRow(t, range.Top >= t.Rows.Count ? t.Rows.Count : range.Top, t.GetRow(range.Top)?.Height ?? 7);
                    break;
                case "InsertRowBelow":
                    TableEditing.InsertRow(t, range.Top >= t.Rows.Count ? t.Rows.Count : range.Bottom + 1, t.GetRow(range.Bottom)?.Height ?? 7);
                    break;
                case "DeleteRow":
                    if (range.Top >= t.Rows.Count) return;
                    for (int r = Math.Min(range.Bottom, t.Rows.Count - 1); r >= range.Top; r--) TableEditing.DeleteRow(t, r);
                    break;
                case "Merge":
                    if (CellEditor == null) return;
                    TableEditing.Merge(t, range);
                    break;
                case "Unmerge":
                    if (CellEditor == null) return;
                    TableEditing.Unmerge(t, range);
                    break;
            }
            // 行・列の数が変わったので、選択を表の範囲に収める
            int maxRow = t.EditableRowCount - 1, maxCol = t.Columns.Count - 1;
            if (maxRow < 0) SelectCells(t, null);
            else SelectCells(t, new CellRange(Math.Min(range.Top, maxRow), Math.Min(range.Left, maxCol), Math.Min(range.Top, maxRow), Math.Min(range.Left, maxCol)));
            CommitSnapshot();
        }
    }

    /// <summary>左のパネルの「ページ」の 1 ページ。</summary>
    public sealed class PageItem : ObservableObject
    {
        /// <summary>縮小画像の幅（DIP）。</summary>
        public const double ThumbnailWidth = 112;

        private int _number;
        private string _name = "";
        private bool _isCurrent;
        private BitmapSource? _thumbnail;
        private double _thumbnailHeight = ThumbnailWidth * 297 / 210;

        public int Number { get => _number; set { if (Set(ref _number, value)) OnPropertyChanged(nameof(Label)); } }

        public string Name { get => _name; set { if (Set(ref _name, value)) OnPropertyChanged(nameof(Label)); } }

        public string Label => Name.Length > 0 ? Number + "  " + Name : Number.ToString();

        public bool IsCurrent { get => _isCurrent; set => Set(ref _isCurrent, value); }

        /// <summary>縮小画像の高さ（DIP）。用紙の縦横比に合わせる。</summary>
        public double ThumbnailHeight { get => _thumbnailHeight; set => Set(ref _thumbnailHeight, value); }

        public BitmapSource? Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }
    }
}
