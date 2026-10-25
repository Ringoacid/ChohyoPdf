using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChohyoPdf.Designer.Controls;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Services;
using ChohyoPdf.Designer.ViewModels;

namespace ChohyoPdf.Designer
{
    public partial class MainWindow : Window
    {
        private bool _syncingSelection;
        private readonly InPlaceEditor _inPlace;
        private readonly List<(Key Key, ModifierKeys Modifiers, ICommand Command)> _shortcuts = new List<(Key, ModifierKeys, ICommand)>();
        private MergeWindow? _mergeWindow;
        // 閉じた左のパネル・インスペクターを、同じ幅で開き直す
        private GridLength _navigationWidth = new GridLength(208), _propertyWidth = new GridLength(320);

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            ViewModel.CommitPendingEdits = CommitPendingEdits;
            DataContext = ViewModel;
            Surface.ViewModel = ViewModel;
            Surface.EditTextRequested += BeginInPlaceEdit;
            Surface.ContextMenuRequested += ShowSurfaceMenu;
            ViewModel.RevealRequested += RevealElement;
            _inPlace = new InPlaceEditor(EditorLayer, Surface, ViewModel);
            ViewModel.Selection.CollectionChanged += (s, e) => SyncListSelection();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncPageSelection();
            _wasContinuous = ViewModel.IsContinuous;
            StateChanged += (s, e) => UpdateMaximizedState();

            AddKey(Key.N, ModifierKeys.Control, ViewModel.NewCommand);
            AddKey(Key.O, ModifierKeys.Control, ViewModel.OpenCommand);
            AddKey(Key.S, ModifierKeys.Control, ViewModel.SaveCommand);
            AddKey(Key.S, ModifierKeys.Control | ModifierKeys.Shift, ViewModel.SaveAsCommand);
            AddKey(Key.Z, ModifierKeys.Control, ViewModel.UndoCommand);
            AddKey(Key.Y, ModifierKeys.Control, ViewModel.RedoCommand);
            AddKey(Key.E, ModifierKeys.Control, ViewModel.ExportPdfCommand);
            AddKey(Key.G, ModifierKeys.Control, new RelayCommand(ShowCode));
            AddKey(Key.F5, ModifierKeys.None, new RelayCommand(ShowPreview));
            AddKey(Key.OemPlus, ModifierKeys.Control, ViewModel.ZoomInCommand);
            AddKey(Key.Add, ModifierKeys.Control, ViewModel.ZoomInCommand);
            AddKey(Key.OemMinus, ModifierKeys.Control, ViewModel.ZoomOutCommand);
            AddKey(Key.Subtract, ModifierKeys.Control, ViewModel.ZoomOutCommand);
            AddKey(Key.D0, ModifierKeys.Control, ViewModel.ZoomResetCommand);
            AddKey(Key.PageUp, ModifierKeys.Control, ViewModel.PreviousPageCommand);
            AddKey(Key.PageDown, ModifierKeys.Control, ViewModel.NextPageCommand);
            // Alt+F で「ファイル」のメニューを開く（Word と同じ。差し込みのウィンドウには渡さない）
            InputBindings.Add(new KeyBinding(new RelayCommand(OpenFileMenu), Key.F, ModifierKeys.Alt));
            Loaded += (s, e) =>
            {
                UpdatePanes();
                Surface.Focus();
                UpdateVisiblePages();
            };
        }

        public MainViewModel ViewModel { get; }

        /// <summary>その場で文字を編集する入力欄（動作確認用）。</summary>
        internal InPlaceEditor InPlace => _inPlace;

        /// <summary>開いている差し込みのウィンドウ（動作確認用）。</summary>
        internal MergeWindow? Merge => _mergeWindow;

        private void AddKey(Key key, ModifierKeys modifiers, ICommand command)
        {
            InputBindings.Add(new KeyBinding(command, key, modifiers));
            _shortcuts.Add((key, modifiers, command));
        }

        /// <summary>ショートカットキー（保存・元に戻す・プレビューなど）を、ほかのウィンドウでも使えるようにする。</summary>
        private void ShareShortcuts(Window window)
        {
            foreach (var (key, modifiers, command) in _shortcuts) window.InputBindings.Add(new KeyBinding(command, key, modifiers));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!ViewModel.ConfirmDiscard()) e.Cancel = true;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPageIndex) || e.PropertyName == nameof(MainViewModel.Report) || e.PropertyName == nameof(MainViewModel.IsEditingCommon))
            {
                SyncPageSelection();
                ScrollToCurrentPage(_forceScroll);
                _forceScroll = false;
            }
            // ページを切り替えたときにも通知が来るので、表示のしかたが変わったときだけスクロールする
            if (e.PropertyName == nameof(MainViewModel.IsContinuous) && ViewModel.IsContinuous != _wasContinuous)
            {
                _wasContinuous = ViewModel.IsContinuous;
                ScrollToCurrentPage(true);
            }
            if (e.PropertyName is nameof(MainViewModel.ShowNavigationPane) or nameof(MainViewModel.ShowPropertyPane) or nameof(MainViewModel.NavigationTab))
                UpdatePanes();
        }

        // ---- タイトルバー ----

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        /// <summary>
        /// 最大化したときは、ウィンドウの枠の分だけ画面の外にはみ出すので、その分だけ内側に寄せる。
        /// 最大化ボタンのアイコンも、元に戻すボタンに変える。
        /// </summary>
        private void UpdateMaximizedState()
        {
            bool maximized = WindowState == WindowState.Maximized;
            RootBorder.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new System.Windows.Thickness(0);
            MaximizeButton.Content = maximized ? "" : "";
            MaximizeButton.ToolTip = maximized ? "元に戻す（縮小）" : "最大化";
        }

        // ---- メニュー ----

        private void FileMenu_Click(object sender, RoutedEventArgs e) => OpenFileMenu();

        private void OpenFileMenu()
        {
            if (FileButton.ContextMenu is ContextMenu menu) CommandBar.ShowBelow(FileButton, menu);
        }

        /// <summary>「PDF を出力」の横の ▾: そのほかの出力。</summary>
        private void ExportMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("PDF を出力(_E)…", ViewModel.ExportPdfCommand, "Ctrl+E", "\uEA90"));
            menu.Items.Add(Item("C# コードを出力(_C)…", ShowCode, "Ctrl+G", "\uE943"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("プレビュー(_P)", ShowPreview, "F5", "\uE8FF"));
            CommandBar.ShowBelow((FrameworkElement)sender, menu);
        }

        /// <summary>差し込みのウィンドウを開く（開いていれば前に出す）。</summary>
        private void OpenMerge_Click(object sender, RoutedEventArgs e) => OpenMergeWindow();

        internal void OpenMergeWindow()
        {
            if (_mergeWindow != null)
            {
                if (_mergeWindow.WindowState == WindowState.Minimized) _mergeWindow.WindowState = WindowState.Normal;
                _mergeWindow.Activate();
                return;
            }
            _mergeWindow = new MergeWindow(ViewModel) { Owner = this };
            ShareShortcuts(_mergeWindow);
            _mergeWindow.Closed += (s, args) => _mergeWindow = null;
            _mergeWindow.Show();
        }

        /// <summary>
        /// 「差し込み欄」: 差し込み欄 {名前} だけを書いたテキストか、一覧をくり返す表を、見えている所に置く。
        /// レールのボタンからは右に、書式バーのボタンからは下にメニューを開く。
        /// </summary>
        private void InsertFieldMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            AddFieldItems(menu, null, null);
            var tables = new MenuItem { Header = "一覧をくり返す表(_D)" };
            AddDetailTableItems(tables, null, null);
            if (tables.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(tables);
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("差し込みの設定(_S)…", OpenMergeWindow, null, "\uE9D5"));
            var button = (FrameworkElement)sender;
            CommandBar.ShowBelow(button, menu);
            if (button.Style == FindResource("RailButton")) menu.Placement = PlacementMode.Right;
        }

        /// <summary>
        /// 「ページ番号」: 全ページ共通の部品として、用紙の下の中央に「{ページ番号} / {総ページ数}」を置く。
        /// </summary>
        private void InsertPageNumber_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsEditingCommon = true;
            double width = 70;
            ViewModel.InsertText("{" + TextTemplate.PageNumber + "} / {" + TextTemplate.PageCount + "}",
                (ViewModel.CurrentPageWidth - width) / 2, ViewModel.CurrentPageHeight - 15, width, TextAlignment.Center);
            // 用紙の下に置くので、見えるようにスクロールする
            if (ViewModel.PrimarySelection is ReportElement added) RevealElement(added);
        }

        /// <summary>「日付」: PDF を作った日（{今日}）を、見えている所に置く。</summary>
        private void InsertDate_Click(object sender, RoutedEventArgs e)
        {
            var p = VisibleCenterMm();
            ViewModel.InsertFieldText("今日", p.X, p.Y);
        }

        /// <summary>
        /// 書式バーの用紙のボタン: 大きさと向き。「このページだけ用紙を変える」がオンならページの用紙、オフなら文書の用紙を変える。
        /// </summary>
        private void PaperMenu_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            var menu = new ContextMenu();
            foreach (string name in Choices.PaperNames)
            {
                string n = name;
                var item = new MenuItem { Header = n == PaperSizes.Custom ? "カスタム（幅と高さを指定）" : n, IsCheckable = true, IsChecked = vm.CurrentPaperName == n };
                item.Click += (s, args) =>
                {
                    vm.SetCurrentPaperName(n);
                    // カスタムの幅と高さは、インスペクターで入力する
                    if (n == PaperSizes.Custom) vm.ShowPageSettingsCommand.Execute(null);
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            foreach (var (label, landscape) in new[] { ("縦向き", false), ("横向き", true) })
            {
                var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = vm.CurrentPaperLandscape == landscape };
                item.Click += (s, args) => vm.SetCurrentPaperLandscape(landscape);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(Check("このページだけ用紙を変える", vm.CurrentPageHasOwnPaper, v => vm.CurrentPageHasOwnPaper = v));
            CommandBar.ShowBelow((FrameworkElement)sender, menu);
        }

        /// <summary>ページの一覧の「…」: 複製・削除・並べ替え・名前の変更。</summary>
        private void PageMoreMenu_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            var menu = new ContextMenu();
            menu.Items.Add(Item("このページを複製(_D)", vm.DuplicatePageCommand, null, "\uE7C4"));
            menu.Items.Add(Item("このページを削除", vm.DeletePageCommand, "Delete", "\uE74D"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("前へ移動", vm.MovePageUpCommand, null, "\uE74A"));
            menu.Items.Add(Item("後ろへ移動", vm.MovePageDownCommand, null, "\uE74B"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("ページの名前を変更(_R)…", () => RenamePage_Click(this, new RoutedEventArgs()), null, "\uE8AC"));
            CommandBar.ShowBelow((FrameworkElement)sender, menu);
        }

        /// <summary>ステータスバーの「表示」: グリッド・部品の枠・ページの並べ方・パネルの表示。</summary>
        private void ViewMenu_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            var menu = new ContextMenu();
            menu.Items.Add(Check("グリッドを表示", vm.ShowGrid, v => vm.ShowGrid = v));
            menu.Items.Add(Check("グリッドに合わせる", vm.SnapToGrid, v => vm.SnapToGrid = v));
            var sizes = new MenuItem { Header = "グリッドの間隔" };
            foreach (double size in Choices.GridSizes)
            {
                double g = size;
                var item = new MenuItem { Header = g + " mm", IsCheckable = true, IsChecked = Math.Abs(vm.GridSize - g) < 1e-9 };
                item.Click += (s, args) => vm.GridSize = g;
                sizes.Items.Add(item);
            }
            menu.Items.Add(sizes);
            menu.Items.Add(Check("部品の枠を表示", vm.ShowOutlines, v => vm.ShowOutlines = v));
            menu.Items.Add(Check("差し込み結果を表示", vm.ShowSampleValues, v => vm.ShowSampleValues = v));
            menu.Items.Add(new Separator());
            menu.Items.Add(Check("ページをつなげて表示", vm.ContinuousPages, v => vm.ContinuousPages = v));
            menu.Items.Add(new Separator());
            menu.Items.Add(Check("左のパネル（ページ・部品）", vm.ShowNavigationPane, v => vm.ShowNavigationPane = v));
            menu.Items.Add(Check("インスペクター（右の設定欄）", vm.ShowPropertyPane, v => vm.ShowPropertyPane = v));
            CommandBar.ShowBelow((FrameworkElement)sender, menu);
            menu.Placement = PlacementMode.Top;
        }

        private void ZoomMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            foreach (int percent in new[] { 50, 75, 100, 125, 150, 200, 300, 400 })
            {
                double zoom = percent / 100.0;
                var item = new MenuItem { Header = percent + "%", IsCheckable = true, IsChecked = Math.Abs(ViewModel.Zoom - zoom) < 0.005 };
                item.Click += (s, args) => ZoomAt(new Point(SurfaceScroll.ViewportWidth / 2, SurfaceScroll.ViewportHeight / 2), zoom);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("ページ全体を表示", () => FitZoom(false)));
            menu.Items.Add(Item("ページの幅に合わせる", () => FitZoom(true)));
            CommandBar.ShowBelow((FrameworkElement)sender, menu);
            menu.Placement = PlacementMode.Top;
        }

        /// <summary>編集しているページの全体（widthOnly なら幅）が、デザイン画面に収まる倍率にする。</summary>
        internal void FitZoom(bool widthOnly)
        {
            var page = Surface.MmToView(new Model.Rect(0, 0, ViewModel.CurrentPageWidth, ViewModel.CurrentPageHeight));
            if (page.Width <= 0 || page.Height <= 0) return;
            // 用紙のまわりの余白（DesignSurface の PageMargin）と、スクロールバーの分を除く
            const double margin = 32 * 2 + 14;
            double width = (SurfaceScroll.ViewportWidth - margin) / page.Width;
            double height = (SurfaceScroll.ViewportHeight - margin) / page.Height;
            ViewModel.Zoom = Math.Round(ViewModel.Zoom * (widthOnly ? width : Math.Min(width, height)), 2);
            ScrollToCurrentPage(true);
        }

        // ---- 左のパネルとインスペクター ----

        /// <summary>レールの「ページ」: ページの一覧を出す。出ているときに押すと、パネルを閉じる。</summary>
        private void PagesPanel_Click(object sender, RoutedEventArgs e) => TogglePanel(NavigationTab.Pages);

        /// <summary>レールの「部品」: 部品の一覧を出す。出ているときに押すと、パネルを閉じる。</summary>
        private void ElementsPanel_Click(object sender, RoutedEventArgs e) => TogglePanel(NavigationTab.Elements);

        private void TogglePanel(NavigationTab tab)
        {
            if (ViewModel.ShowNavigationPane && ViewModel.NavigationTab == tab) ViewModel.ShowNavigationPane = false;
            else
            {
                ViewModel.NavigationTab = tab;
                ViewModel.ShowNavigationPane = true;
            }
            UpdatePanes();
        }

        private void ClosePropertyPane_Click(object sender, RoutedEventArgs e) => ViewModel.ShowPropertyPane = false;

        /// <summary>
        /// 左右のパネルの表示に合わせて、列の幅を変える（閉じたら幅を 0 にし、開いたら元の幅に戻す）。
        /// レールの「ページ」「部品」のボタンも、開いているパネルに合わせる。
        /// </summary>
        private void UpdatePanes()
        {
            SetPane(NavigationColumn, NavigationPane, NavigationSplitter, ViewModel.ShowNavigationPane, ref _navigationWidth, 160);
            SetPane(PropertyColumn, PropertyPane, PropertySplitter, ViewModel.ShowPropertyPane, ref _propertyWidth, 250);
            PagesRailButton.IsChecked = ViewModel.ShowNavigationPane && ViewModel.NavigationTab == NavigationTab.Pages;
            ElementsRailButton.IsChecked = ViewModel.ShowNavigationPane && ViewModel.NavigationTab == NavigationTab.Elements;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateVisiblePages));
        }

        private static void SetPane(ColumnDefinition column, FrameworkElement pane, GridSplitter splitter, bool show, ref GridLength width, double minWidth)
        {
            bool shown = pane.Visibility == Visibility.Visible;
            if (show == shown) return;
            if (!show) width = column.Width;
            pane.Visibility = splitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            column.MinWidth = show ? minWidth : 0;
            column.Width = show ? width : new GridLength(0);
        }

        // ---- ページをつなげて表示 ----

        // ページの一覧で選んだときは、ページの先頭までスクロールする
        private bool _forceScroll;
        private bool _wasContinuous;

        private void SurfaceScroll_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateVisiblePages();

        /// <summary>見えている範囲をデザイン画面に知らせる（見えているページを描く）。</summary>
        private void UpdateVisiblePages()
        {
            if (!IsLoaded) return;
            var topLeft = SurfaceScroll.TranslatePoint(new Point(0, 0), Surface);
            Surface.SetViewport(new System.Windows.Rect(topLeft, new Size(SurfaceScroll.ViewportWidth, SurfaceScroll.ViewportHeight)));
        }

        /// <summary>
        /// ページをつなげて表示しているとき、編集しているページが見えるようにスクロールする。
        /// force でなければ、すでに見えているときは動かさない（クリックしたページに移ったときなど）。
        /// </summary>
        private void ScrollToCurrentPage(bool force)
        {
            // ページの配置が変わったあとで動かす
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!ViewModel.IsContinuous)
                {
                    UpdateVisiblePages();
                    return;
                }
                SurfaceScroll.UpdateLayout();
                if (Surface.PageRectOf(ViewModel.CurrentPageKey) is not System.Windows.Rect page) return;
                double top = Surface.TranslatePoint(page.TopLeft, SurfaceScroll).Y;
                double bottom = top + page.Height;
                bool visible = bottom > 40 && top < SurfaceScroll.ViewportHeight - 40;
                if (force || !visible) SurfaceScroll.ScrollToVerticalOffset(SurfaceScroll.VerticalOffset + top - 12);
                UpdateVisiblePages();
            }));
        }

        /// <summary>部品が見えていなければ、見えるようにスクロールする（ページの切り替えのスクロールのあとで動かす）。</summary>
        private void RevealElement(ReportElement element)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                SurfaceScroll.UpdateLayout();
                var view = Surface.MmToView(element.Bounds);
                var topLeft = Surface.TranslatePoint(view.TopLeft, SurfaceScroll);
                double margin = 24;
                if (topLeft.Y < 0 || topLeft.Y + view.Height > SurfaceScroll.ViewportHeight)
                    SurfaceScroll.ScrollToVerticalOffset(SurfaceScroll.VerticalOffset + topLeft.Y - Math.Max(margin, (SurfaceScroll.ViewportHeight - view.Height) / 3));
                if (topLeft.X < 0 || topLeft.X + view.Width > SurfaceScroll.ViewportWidth)
                    SurfaceScroll.ScrollToHorizontalOffset(SurfaceScroll.HorizontalOffset + topLeft.X - margin);
                UpdateVisiblePages();
            }));
        }

        // ---- ページの一覧 ----

        private bool _syncingPage;

        /// <summary>ページの一覧の選択を、編集しているページに合わせる。</summary>
        private void SyncPageSelection()
        {
            if (_syncingPage) return;
            _syncingPage = true;
            // 全ページ共通の部品を編集しているときは、どのページも選ばない（ページをクリックすると、そのページの編集に戻る）
            PageList.SelectedIndex = ViewModel.IsEditingCommon ? -1 : ViewModel.CurrentPageIndex;
            if (PageList.SelectedItem != null) PageList.ScrollIntoView(PageList.SelectedItem);
            _syncingPage = false;
        }

        private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingPage) return;
            // ページを削除したときなどに選択が外れたら、編集しているページを選び直す
            if (PageList.SelectedIndex < 0)
            {
                if (ViewModel.IsEditingCommon) return;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(SyncPageSelection));
                return;
            }
            _syncingPage = true;
            _forceScroll = true;
            ViewModel.CurrentPageIndex = PageList.SelectedIndex;
            _forceScroll = false;
            _syncingPage = false;
            ScrollToCurrentPage(true);
        }

        private void PageList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewModel.DeletePageCommand.CanExecute(null))
            {
                ViewModel.DeletePageCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>インスペクターにページの名前の欄を表示して、入力できるようにする。</summary>
        private void RenamePage_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsEditingCommon = false;
            ViewModel.Select(null);
            ViewModel.ShowPropertyPane = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                PageNameBox.Focus();
                PageNameBox.SelectAll();
            }));
        }

        /// <summary>
        /// 入力欄を離れたときに反映する欄（数値・フォント名・色・表のセル・その場の編集）で、入力途中の値をモデルに反映する。
        /// 保存・出力・終了などの前に呼ぶ（メニューやショートカットキーでは入力欄からフォーカスが移らないため）。
        /// 差し込みのウィンドウを開いていれば、そこで入力途中の値も反映する。
        /// </summary>
        private void CommitPendingEdits()
        {
            _inPlace.End(true);
            PendingEdits.Commit(this);
            _mergeWindow?.CommitPendingEdits();
        }

        // ---- 部品の一覧と選択の同期 ----

        private void ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingSelection) return;
            _syncingSelection = true;
            ViewModel.SelectMany(ElementList.SelectedItems.Cast<ReportElement>().ToList(), false);
            _syncingSelection = false;
        }

        private void SyncListSelection()
        {
            if (_syncingSelection) return;
            _syncingSelection = true;
            ElementList.SelectedItems.Clear();
            foreach (var e in ViewModel.Selection) ElementList.SelectedItems.Add(e);
            if (ViewModel.Selection.Count > 0) ElementList.ScrollIntoView(ViewModel.Selection[^1]);
            _syncingSelection = false;
        }

        private void ElementList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewModel.DeleteCommand.CanExecute(null))
            {
                ViewModel.DeleteCommand.Execute(null);
                e.Handled = true;
            }
        }

        // ---- 表示の移動と拡大・縮小 ----

        /// <summary>
        /// ホイールで上下、Shift ＋ ホイールで左右に動かす。Ctrl ＋ ホイールでは、マウスの位置を中心に拡大・縮小する。
        /// </summary>
        private void SurfaceScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => e.Handled = HandleWheel(e.GetPosition(SurfaceScroll), e.Delta, Keyboard.Modifiers);

        /// <summary>ホイールの操作を処理する。処理したら true（そのほかはスクロール領域の既定の動作に任せる）。</summary>
        internal bool HandleWheel(Point position, int delta, ModifierKeys modifiers)
        {
            if ((modifiers & ModifierKeys.Control) != 0)
            {
                double factor = delta > 0 ? 1.1 : 1 / 1.1;
                ZoomAt(position, Math.Round(ViewModel.Zoom * factor, 2));
                return true;
            }
            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                SurfaceScroll.ScrollToHorizontalOffset(SurfaceScroll.HorizontalOffset - delta);
                return true;
            }
            return false;
        }

        /// <summary>倍率を変える。point（スクロール領域の上の位置）にある用紙の点が、変えたあとも同じ位置に来るようにする。</summary>
        internal void ZoomAt(Point point, double zoom)
        {
            var mm = Surface.ViewToMm(SurfaceScroll.TranslatePoint(point, Surface));
            ViewModel.Zoom = zoom;
            SurfaceScroll.UpdateLayout();
            var after = Surface.TranslatePoint(Surface.MmToView(new Model.Rect(mm.X, mm.Y, 0, 0)).TopLeft, SurfaceScroll);
            SurfaceScroll.ScrollToHorizontalOffset(SurfaceScroll.HorizontalOffset + after.X - point.X);
            SurfaceScroll.ScrollToVerticalOffset(SurfaceScroll.VerticalOffset + after.Y - point.Y);
        }

        /// <summary>デザイン画面に見えている範囲の中心（mm）。</summary>
        private Point VisibleCenterMm()
        {
            var center = new Point(SurfaceScroll.ViewportWidth / 2, SurfaceScroll.ViewportHeight / 2);
            var mm = Surface.ViewToMm(SurfaceScroll.TranslatePoint(center, Surface));
            var vm = ViewModel;
            return new Point(Math.Round(Math.Max(0, Math.Min(vm.CurrentPageWidth - 40, mm.X)), 1), Math.Round(Math.Max(0, Math.Min(vm.CurrentPageHeight - 20, mm.Y)), 1));
        }

        // ---- 文字の編集 ----

        /// <summary>選択している部品（またはセル）の文字を、デザイン画面の上で編集する。</summary>
        private void BeginInPlaceEdit()
        {
            if (ViewModel.PrimarySelection is TextElement || ViewModel.PrimarySelection is TableElement) _inPlace.Begin();
        }

        /// <summary>差し込み欄の名前を選んだら、入力欄のカーソルの位置に {名前} を入れる。</summary>
        /// <summary>
        /// 書式バーの上付き・下付き・ルビのボタン（Tag は sup、sub、ruby）。文字を編集中なら、選んでいる文字に記法を付ける（付いていれば外す）。
        /// 編集中でなければ、選んでいるテキストやセルの文字全体を上付き・下付きにする（ルビは、その場の編集を始めてから付ける）。
        /// </summary>
        private void Markup_Click(object sender, RoutedEventArgs e)
        {
            string kind = (sender as FrameworkElement)?.Tag as string ?? "";
            string open = kind == "sub" ? TextMarkup.SubscriptOpen : TextMarkup.SuperscriptOpen;
            string close = kind == "sub" ? TextMarkup.SubscriptClose : TextMarkup.SuperscriptClose;
            // ボタンはフォーカスを取らないので、編集中の入力欄（用紙の上かプロパティの欄）にフォーカスが残っている
            var focused = Keyboard.FocusedElement as TextBox;
            bool editor = focused != null && (focused == _inPlace.TextBox || Equals(focused.Tag, "TextEditor") || Equals(focused.Tag, "CellTextEditor"));
            if (kind == "ruby")
            {
                if (editor) MarkupEditing.AddRuby(focused!);
                else
                {
                    // 編集を始めて、文字の最後に ｜《》 を入れる
                    BeginInPlaceEdit();
                    if (_inPlace.TextBox is TextBox box)
                    {
                        box.Select(box.Text.Length, 0);
                        MarkupEditing.AddRuby(box);
                    }
                }
                return;
            }
            if (editor)
            {
                MarkupEditing.Toggle(focused!, open, close);
                return;
            }
            if (ViewModel.CellEditor != null) ViewModel.CellEditor.Text = MarkupEditing.ToggleLines(ViewModel.CellEditor.Text, open, close);
            else if (ViewModel.PrimarySelection is TextElement text && text.Text.Length > 0) text.Text = MarkupEditing.ToggleLines(text.Text, open, close);
        }

        private void InsertField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo || combo.SelectedItem is not string name) return;
            if (combo.Tag is TextBox box)
            {
                string field = "{" + name + "}";
                int caret = box.SelectionStart;
                box.SelectedText = field;
                box.SelectionLength = 0;
                box.SelectionStart = caret + field.Length;
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                box.Focus();
            }
            // 選択を戻して、同じ名前をもう一度選べるようにする
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                combo.SelectedIndex = -1;
                combo.Text = "差し込み欄を挿入…";
            }));
        }

        // ---- 右クリックのメニュー ----

        /// <summary>最後に開いた右クリックのメニュー（動作確認用）。</summary>
        internal ContextMenu? LastMenu { get; private set; }

        private void ShowSurfaceMenu(Point mm, ReportElement? hit)
        {
            _inPlace.End(true);
            var menu = hit != null && ViewModel.Selection.Count > 0 ? BuildElementMenu() : BuildPageMenu(mm);
            menu.PlacementTarget = Surface;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
            LastMenu = menu;
        }

        private ContextMenu BuildElementMenu()
        {
            var vm = ViewModel;
            var menu = new ContextMenu();
            var primary = vm.PrimarySelection;
            if (primary is TextElement || primary is TableElement)
            {
                menu.Items.Add(Item(vm.CellEditor != null ? "セルの文字を編集(_E)" : "文字を編集(_E)", BeginInPlaceEdit, "F2", ""));
                menu.Items.Add(new Separator());
            }
            menu.Items.Add(Item("切り取り(_T)", vm.CutCommand, "Ctrl+X", ""));
            menu.Items.Add(Item("コピー(_C)", vm.CopyCommand, "Ctrl+C", ""));
            menu.Items.Add(Item("貼り付け(_P)", vm.PasteCommand, "Ctrl+V", ""));
            menu.Items.Add(Item("複製(_D)", vm.DuplicateCommand, "Ctrl+D"));
            menu.Items.Add(Item("削除", vm.DeleteCommand, "Delete", ""));

            if (primary is TableElement table)
            {
                menu.Items.Add(new Separator());
                var rows = new MenuItem { Header = "行と列(_L)" };
                rows.Items.Add(TableItem("上に行を挿入", "InsertRowAbove"));
                rows.Items.Add(TableItem("下に行を挿入", "InsertRowBelow"));
                rows.Items.Add(TableItem("左に列を挿入", "InsertColumnLeft"));
                rows.Items.Add(TableItem("右に列を挿入", "InsertColumnRight"));
                rows.Items.Add(new Separator());
                rows.Items.Add(TableItem("行を削除", "DeleteRow"));
                rows.Items.Add(TableItem("列を削除", "DeleteColumn"));
                menu.Items.Add(rows);
                if (vm.CellEditor != null)
                {
                    menu.Items.Add(TableItem("セルを結合(_M)", "Merge"));
                    menu.Items.Add(TableItem("結合を解除(_U)", "Unmerge"));
                }
                var detail = new MenuItem { Header = "くり返し行(_R)" };
                foreach (string name in vm.DetailSourceNames)
                {
                    string n = name;
                    var item = new MenuItem { Header = n.Length == 0 ? "くり返さない" : "一覧「" + n + "」をくり返す", IsCheckable = true, IsChecked = (table.DetailSource ?? "") == n };
                    item.Click += (s, e) => table.DetailSource = n;
                    detail.Items.Add(item);
                }
                menu.Items.Add(detail);
            }

            menu.Items.Add(new Separator());
            var order = new MenuItem { Header = "順序(_O)" };
            order.Items.Add(Item("最前面へ移動", vm.BringToFrontCommand));
            order.Items.Add(Item("前面へ移動", vm.BringForwardCommand));
            order.Items.Add(Item("背面へ移動", vm.SendBackwardCommand));
            order.Items.Add(Item("最背面へ移動", vm.SendToBackCommand));
            menu.Items.Add(order);
            if (vm.Selection.Count > 1)
            {
                var align = new MenuItem { Header = "揃える(_A)" };
                foreach (var (label, mode) in new[] { ("左揃え", "Left"), ("左右中央揃え", "Center"), ("右揃え", "Right"), ("上揃え", "Top"), ("上下中央揃え", "Middle"), ("下揃え", "Bottom") })
                    align.Items.Add(new MenuItem { Header = label, Command = vm.AlignCommand, CommandParameter = mode });
                menu.Items.Add(align);
            }
            // 全ページ共通の部品と、くり返し行のある表でページが増えるページの部品は、どのページに描くかを選べる
            if (vm.ShowVisibilityOption)
            {
                var pages = new MenuItem { Header = vm.VisibilityLabel + "(_G)" };
                foreach (var choice in vm.VisibilityChoices)
                {
                    var visibility = (PageVisibility)choice.Value!;
                    var item = new MenuItem { Header = choice.Label, IsCheckable = true, IsChecked = vm.Selection.All(x => x.Visibility == visibility) };
                    item.Click += (s, e) => vm.SetVisibility(visibility);
                    pages.Items.Add(item);
                }
                menu.Items.Add(pages);
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("詳しい設定(_R)…", () => vm.ShowPropertyPane = true, null, ""));
            return menu;
        }

        private ContextMenu BuildPageMenu(Point mm)
        {
            var vm = ViewModel;
            var menu = new ContextMenu();
            double x = Math.Round(mm.X, 1), y = Math.Round(mm.Y, 1);
            menu.Items.Add(Item("ここに貼り付け(_P)", () => vm.PasteAt(x, y), null, "", vm.PasteCommand.CanExecute(null)));
            var insert = new MenuItem { Header = "挿入(_I)" };
            AddInsertItems(insert, x, y);
            menu.Items.Add(insert);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("すべて選択(_A)", vm.SelectAllCommand, "Ctrl+A"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Check("差し込み結果を表示(_S)", vm.ShowSampleValues, v => vm.ShowSampleValues = v));
            menu.Items.Add(Check("グリッドを表示", vm.ShowGrid, v => vm.ShowGrid = v));
            menu.Items.Add(Check("グリッドに合わせる", vm.SnapToGrid, v => vm.SnapToGrid = v));
            menu.Items.Add(Check("部品の枠を表示", vm.ShowOutlines, v => vm.ShowOutlines = v));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("拡大", vm.ZoomInCommand, "Ctrl++"));
            menu.Items.Add(Item("縮小", vm.ZoomOutCommand, "Ctrl+-"));
            menu.Items.Add(Item("実寸（100%）", vm.ZoomResetCommand, "Ctrl+0"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("新しいページを追加(_N)", vm.AddPageCommand));
            menu.Items.Add(Item("ページの名前を変更(_R)…", () => RenamePage_Click(this, new RoutedEventArgs())));
            menu.Items.Add(Check("全ページ共通の部品を編集(_C)", vm.IsEditingCommon, v => vm.IsEditingCommon = v));
            menu.Items.Add(Item("ページと文書の設定(_U)…", () => vm.ShowPageSettingsCommand.Execute(null), null, ""));
            return menu;
        }

        /// <summary>右クリックの「挿入」のメニュー項目。右クリックした位置に、既定の大きさで置く。</summary>
        private void AddInsertItems(MenuItem parent, double x, double y)
        {
            var vm = ViewModel;
            foreach (var (label, tool, icon) in new[]
            {
                ("テキスト(_T)", Tool.Text, ""), ("表(_B)", Tool.Table, ""), ("直線(_L)", Tool.Line, null),
                ("矩形(_R)", Tool.Rectangle, null), ("楕円(_E)", Tool.Ellipse, null), ("画像(_I)…", Tool.Image, ""),
            })
            {
                var t = tool;
                parent.Items.Add(Item(label, () => vm.InsertAt(t, x, y), null, icon));
            }
            parent.Items.Add(new Separator());
            var fields = new MenuItem { Header = "差し込み欄(_F)" };
            AddFieldItems(fields, x, y);
            parent.Items.Add(fields);
            var tables = new MenuItem { Header = "一覧の表(_D)" };
            AddDetailTableItems(tables, x, y);
            tables.IsEnabled = tables.Items.Count > 0;
            parent.Items.Add(tables);
        }

        /// <summary>
        /// 差し込み欄 {名前} だけを書いたテキストを置くメニュー項目（差し込み項目と、組み込みの差し込み欄）。
        /// 位置を指定しなければ、デザイン画面に見えている所に置く。
        /// </summary>
        private void AddFieldItems(ItemsControl parent, double? x, double? y)
        {
            var vm = ViewModel;
            void add(string name, string? description)
            {
                parent.Items.Add(Item("{" + name + "}" + (description != null ? "　" + description : ""), () =>
                {
                    var p = x != null && y != null ? new Point(x.Value, y.Value) : VisibleCenterMm();
                    vm.InsertFieldText(name, p.X, p.Y);
                }));
            }
            foreach (var field in vm.Report.Fields) add(field.Name, null);
            // 値を渡さなくても使える差し込み欄（ページ番号・今日など）
            if (vm.Report.Fields.Count > 0) parent.Items.Add(new Separator());
            foreach (var builtIn in TextTemplate.BuiltIns) add(builtIn.Name, builtIn.Description);
        }

        /// <summary>一覧をくり返す表を置くメニュー項目。全ページ共通の部品の表では行をくり返せないので、そのときは何も足さない。</summary>
        private void AddDetailTableItems(ItemsControl parent, double? x, double? y)
        {
            var vm = ViewModel;
            if (vm.IsEditingCommon) return;
            foreach (var list in vm.Report.Lists)
            {
                var l = list;
                parent.Items.Add(Item("一覧「" + l.Name + "」をくり返す表", () => vm.InsertDetailTable(l, x, y)));
            }
        }

        private static MenuItem Item(string header, ICommand command, string? gesture = null, string? icon = null)
            => new MenuItem { Header = header, Command = command, InputGestureText = gesture ?? "", Icon = MenuIcon(icon) };

        private static MenuItem Item(string header, Action action, string? gesture = null, string? icon = null, bool enabled = true)
        {
            var item = new MenuItem { Header = header, InputGestureText = gesture ?? "", Icon = MenuIcon(icon), IsEnabled = enabled };
            item.Click += (s, e) => action();
            return item;
        }

        private MenuItem TableItem(string header, string operation)
            => new MenuItem { Header = header, Command = ViewModel.TableCommand, CommandParameter = operation, Icon = new TableOperationIcon { Operation = operation, Width = 16, Height = 16, LayoutTransform = new ScaleTransform(0.8, 0.8) } };

        private static MenuItem Check(string header, bool value, Action<bool> set)
        {
            var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = value };
            item.Click += (s, e) => set(item.IsChecked);
            return item;
        }

        private static object? MenuIcon(string? glyph)
            => glyph == null ? null : new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 };

        // ---- ほかの画面 ----

        private void Preview_Click(object sender, RoutedEventArgs e) => ShowPreview();

        private void ExportCode_Click(object sender, RoutedEventArgs e) => ShowCode();

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            var window = new OptionsWindow { Owner = this };
            if (window.ShowDialog() != true) return;
            // フォントの一覧の表示を作り直す
            CollectionViewSource.GetDefaultView(ViewModel.FontNames).Refresh();
        }

        private void ShowPreview()
        {
            CommitPendingEdits();
            var window = new PreviewWindow(ReportSerializer.Clone(ViewModel.Report), ViewModel.BaseDirectory, ViewModel.ScriptsTrusted) { Owner = this };
            window.ShowDialog();
        }

        private void ShowCode()
        {
            CommitPendingEdits();
            var window = new CodeWindow(ViewModel.Report, ViewModel.FilePath) { Owner = this };
            window.ShowDialog();
        }
    }
}
