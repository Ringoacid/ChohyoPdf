using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ChohyoPdf.Designer.Controls;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Services;
using ChohyoPdf.Designer.ViewModels;

namespace ChohyoPdf.Designer
{
    /// <summary>
    /// 差し込みの設定（差し込み項目・一覧・組み込みの欄）のウィンドウ。3 つの章は同じ作り（見出し・使い方・追加、左に一覧、右に設定）にする。
    /// 開いたまま用紙を編集できるよう、モードレスで開く（項目を用紙へドラッグしたり、「用紙から探す」で部品を選んだりする）。
    /// </summary>
    public partial class MergeWindow : Window
    {
        private Point _dragStart;
        private FieldDefinition? _dragField;

        public MergeWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Closed += (s, e) =>
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                viewModel.IsGuideOpen = false;
            };
            RebuildSampleGrid();
        }

        public MainViewModel ViewModel { get; }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SampleTable)) RebuildSampleGrid();
            if (e.PropertyName == nameof(MainViewModel.IsGuideOpen)) OnGuideOpenChanged();
        }

        /// <summary>入力途中の値（確認用の行の表のセルや、入力欄）をモデルに反映する。保存・出力などの前に呼ぶ。</summary>
        internal void CommitPendingEdits()
        {
            SampleGrid.CommitEdit(DataGridEditingUnit.Row, true);
            PendingEdits.Commit(this);
        }

        // ---- 列と確認用の行の表 ----

        /// <summary>
        /// 列と確認用の行の表の列を、選んでいる一覧の列に合わせて作り直す。
        /// 列見出しには列の定義（FieldDefinition）を入れ、名前のチップと種類を表示する（名前や種類を変えると、見出しもすぐ変わる）。
        /// </summary>
        private void RebuildSampleGrid()
        {
            if (SampleGrid.ItemsSource is DataView oldView) oldView.ListChanged -= SampleView_ListChanged;
            SampleGrid.Columns.Clear();
            var table = ViewModel.SampleTable;
            if (table == null)
            {
                SampleGrid.ItemsSource = null;
                return;
            }
            var headerTemplate = (DataTemplate)FindResource("SampleColumnHeader");
            foreach (DataColumn column in table.Columns)
            {
                SampleGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = column.ExtendedProperties[MainViewModel.SampleColumnKey] ?? column.Caption,
                    HeaderTemplate = headerTemplate,
                    Binding = new Binding(column.ColumnName) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    MinWidth = 70,
                });
            }
            SampleGrid.ItemsSource = table.DefaultView;
            table.DefaultView.ListChanged += SampleView_ListChanged;
            // 列を足したときは、右に増えた列が見えるようにする
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var selected = SampleGrid.Columns.FirstOrDefault(c => ReferenceEquals(c.Header, ViewModel.SelectedColumn));
                if (selected != null && SampleGrid.IsVisible) SampleGrid.ScrollIntoView(null, selected);
            }));
        }

        /// <summary>列見出しをクリックしたら、その列を選んで、表の下に列の設定を出す。</summary>
        private void SampleGridHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DataGridColumnHeader header && header.Column?.Header is FieldDefinition column) ViewModel.SelectedColumn = column;
        }

        // 行見出しには 1, 2, 3… の行番号を、新しい行を入力する最後の行には「＋」を出す
        private void SampleGrid_LoadingRow(object sender, DataGridRowEventArgs e) => e.Row.Header = RowLabel(e.Row);

        private static string RowLabel(DataGridRow row) => row.IsNewItem ? "＋" : (row.GetIndex() + 1).ToString();

        /// <summary>行を足したり消したりしたら、表示している行の番号を振り直す。</summary>
        private void SampleView_ListChanged(object? sender, ListChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                for (int i = 0; i < SampleGrid.Items.Count; i++)
                {
                    if (SampleGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row) row.Header = RowLabel(row);
                }
            }));
        }

        private void PasteSampleRows_Click(object sender, RoutedEventArgs e)
        {
            SampleGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (Clipboard.ContainsText()) ViewModel.PasteSampleRows(Clipboard.GetText());
        }

        private void DeleteSampleRows_Click(object sender, RoutedEventArgs e)
        {
            SampleGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var rows = SampleGrid.SelectedCells.Select(c => c.Item).Concat(SampleGrid.SelectedItems.Cast<object>()).OfType<DataRowView>().Distinct().ToList();
            foreach (var row in rows) row.Row.Delete();
        }

        // ---- 用紙へのドラッグ ----

        // 項目の一覧の行と、列見出しのチップを用紙へドラッグして、差し込み欄を置く
        private void FieldDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(this);
            _dragField = (e.OriginalSource as FrameworkElement)?.DataContext as FieldDefinition;
        }

        // 列見出しのチップ以外を押したときは、ドラッグを始めない（チップの PreviewMouseLeftButtonDown より先に来る）
        private void SampleGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragField = null;

        private void FieldDrag_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragField == null || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var field = _dragField;
            _dragField = null;
            // 列見出しはボタンなので、押したときにマウスを捕まえている。放してからドラッグする
            Mouse.Capture(null);
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DesignSurface.FieldDataFormat, field.Name), DragDropEffects.Copy);
        }

        /// <summary>組み込みの差し込み欄（{ページ番号} など）をクリップボードにコピーする。</summary>
        private void CopyBuiltIn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string text) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // ほかのアプリがクリップボードを使っているときは、コピーしない
            }
        }

        // ---- 使い方 ----

        /// <summary>章の見出しの「使い方」: その章の使い方を、実際の画面の画像で見せる。</summary>
        private void HowTo_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string tag && Enum.TryParse<DataSection>(tag, out var section)) ViewModel.GuideSection = section;
            ViewModel.IsGuideOpen = true;
        }

        // 暗くした部分（ダイアログの外）をクリックしたら閉じる
        private void GuideBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == GuideBackdrop) ViewModel.IsGuideOpen = false;
        }

        // Esc で閉じる（ガイドを開いている間は、ダイアログにフォーカスを置いている）
        private void GuideDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            ViewModel.IsGuideOpen = false;
            e.Handled = true;
        }

        private void OnGuideOpenChanged()
        {
            if (!ViewModel.IsGuideOpen) return;
            (GuideSteps.Steps, GuideTitle.Text) = ViewModel.GuideSection switch
            {
                DataSection.Lists => (Guides.Lists, "一覧の使い方"),
                DataSection.BuiltIns => (Guides.BuiltIns, "組み込みの欄の使い方"),
                _ => (Guides.Fields, "差し込み項目の使い方"),
            };
            GuideSteps.Restart();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => GuideDialog.Focus()));
        }
    }
}
