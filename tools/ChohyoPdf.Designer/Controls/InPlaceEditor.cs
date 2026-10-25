using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Services;
using ChohyoPdf.Designer.ViewModels;

namespace ChohyoPdf.Designer.Controls
{
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using ModelRect = ChohyoPdf.Designer.Model.Rect;
    using Thickness = System.Windows.Thickness;
    using WpfTextAlignment = System.Windows.TextAlignment;
    using WpfVerticalAlignment = System.Windows.VerticalAlignment;

    /// <summary>
    /// デザイン画面の上で、テキストや表のセルの文字を直接編集する入力欄。
    /// 入力欄はデザイン画面の上に重ねた Canvas に置く（デザイン画面のキー操作と混ざらないように、デザイン画面の外に置く）。
    /// 編集の開始から終了までを、元に戻すの 1 回分として記録する。
    /// </summary>
    public sealed class InPlaceEditor
    {
        private readonly Canvas _layer;
        private readonly DesignSurface _surface;
        private readonly MainViewModel _vm;
        private TextBox? _box;
        private Func<string>? _get;
        private Action<string>? _set;
        private Func<ModelRect?>? _bounds;
        private Func<(bool Bold, bool Italic)>? _style;
        private WpfTypeface? _face;
        private TableElement? _table;
        private int _row, _column;
        private string _original = "";
        private bool _ending;

        public InPlaceEditor(Canvas layer, DesignSurface surface, MainViewModel vm)
        {
            _layer = layer;
            _surface = surface;
            _vm = vm;
            _vm.SurfaceInvalidated += UpdatePosition;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        public bool IsEditing => _box != null;

        /// <summary>編集中の入力欄（動作確認用）。</summary>
        internal TextBox? TextBox => _box;

        /// <summary>選択しているテキスト、または表のセルの編集を始める。</summary>
        public void Begin()
        {
            End(true);
            if (_vm.PrimarySelection is TableElement table && table.EditableRowCount > 0 && table.Columns.Count > 0)
            {
                if (_vm.CellEditor?.Table != table) _vm.SelectCells(table, new CellRange(0, 0, 0, 0));
                BeginCell(table, _vm.CellEditor!.Range.Top, _vm.CellEditor.Range.Left);
            }
            else if (_vm.PrimarySelection is TextElement text)
            {
                BeginText(text);
            }
        }

        private void BeginText(TextElement t)
        {
            _table = null;
            _get = () => t.Text;
            _set = value => t.Text = value;
            _bounds = () => t.Bounds;
            _style = () => (t.Bold, t.Italic);
            var box = CreateBox(t.FontName, t.FontSize, t.Bold, t.Italic, t.Alignment, t.VerticalAlignment, t.Padding, t.Wrap);
            if (t.Underline) box.TextDecorations = TextDecorations.Underline;
            Show(box);
        }

        private void BeginCell(TableElement t, int row, int column)
        {
            var (r, c) = t.GetOwner(row, column);
            var cell = t.GetRow(r)!.Cells[c];
            _table = t;
            _row = r;
            _column = c;
            _get = () => cell.Text;
            _set = value => cell.Text = value;
            _bounds = () => CellBounds(t, r, c);
            _style = () => (cell.Bold ?? false, false);
            var box = CreateBox(t.FontName, cell.FontSize ?? t.FontSize, cell.Bold ?? false, false,
                cell.Alignment ?? t.Columns[c].Alignment, cell.VerticalAlignment ?? t.VerticalAlignment, t.CellPadding, t.Wrap);
            Show(box);
        }

        /// <summary>結合を含めたセルの矩形（mm）。表から行や列が消えていれば null。</summary>
        private static ModelRect? CellBounds(TableElement t, int row, int column)
        {
            if (row >= t.EditableRowCount || column >= t.Columns.Count) return null;
            var owners = t.GetOwnerMap();
            int bottom = row, right = column;
            for (int r = row; r < t.EditableRowCount; r++)
            {
                for (int c = column; c < t.Columns.Count; c++)
                {
                    if (owners[r, c] != (row, column)) continue;
                    bottom = Math.Max(bottom, r);
                    right = Math.Max(right, c);
                }
            }
            return DesignSurface.CellRangeRect(t, new CellRange(row, column, bottom, right));
        }

        private TextBox CreateBox(string? fontName, double fontSize, bool bold, bool italic, ChohyoPdf.TextAlignment alignment,
            ChohyoPdf.VerticalAlignment verticalAlignment, double paddingMm, bool wrap)
        {
            double scale = _vm.Zoom * 96 / 25.4;
            var face = _face = WpfFonts.Resolve(fontName ?? _vm.Report.DefaultFontName);
            var box = new TextBox
            {
                AcceptsReturn = true,
                // テキストでは、Tab で箇条書きの項目を字下げする（入れ子にする）。セルでは右のセルへ移る
                AcceptsTab = _table == null,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                FontSize = Math.Max(1, fontSize * _vm.Zoom * 96 / 72),
                FontWeight = bold ? FontWeights.Bold : face?.Weight ?? FontWeights.Normal,
                FontStyle = italic ? FontStyles.Italic : face?.Style ?? FontStyles.Normal,
                TextAlignment = alignment == ChohyoPdf.TextAlignment.Center ? WpfTextAlignment.Center
                    : alignment == ChohyoPdf.TextAlignment.Right ? WpfTextAlignment.Right
                    : alignment == ChohyoPdf.TextAlignment.Left ? WpfTextAlignment.Left : WpfTextAlignment.Justify,
                VerticalContentAlignment = verticalAlignment == ChohyoPdf.VerticalAlignment.Middle ? WpfVerticalAlignment.Center
                    : verticalAlignment == ChohyoPdf.VerticalAlignment.Bottom ? WpfVerticalAlignment.Bottom : WpfVerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(Math.Max(0, paddingMm * scale - 2)),
                Style = (Style)Application.Current.FindResource("PlainTextBox"),
                Background = Brushes.White,
                BorderBrush = (Brush)Application.Current.FindResource("Accent"),
                BorderThickness = new Thickness(2),
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                ToolTip = _table != null
                    ? "Enter で確定、Alt+Enter で改行、Tab で右のセル、Esc で取り消し"
                    : "Ctrl+Enter か、ほかの場所をクリックで確定。Esc で取り消し。箇条書きでは、行頭の Tab で入れ子にします",
            };
            if (face != null) box.FontFamily = face.Family;
            return box;
        }

        private void Show(TextBox box)
        {
            _original = _get!();
            box.Text = _original;
            box.TextChanged += (s, e) =>
            {
                if (!_ending && _set != null && _get!() != box.Text) _set(box.Text);
            };
            box.PreviewKeyDown += OnPreviewKeyDown;
            box.LostKeyboardFocus += OnLostKeyboardFocus;
            box.ContextMenu = new ContextMenu();
            box.ContextMenuOpening += (s, e) => BuildContextMenu(box);
            _box = box;
            _vm.BeginInteraction();
            _layer.Children.Add(box);
            UpdatePosition();
            box.Focus();
            box.SelectAll();
        }

        private void BuildContextMenu(TextBox box)
        {
            var menu = box.ContextMenu!;
            menu.Items.Clear();
            menu.Items.Add(new MenuItem { Header = "切り取り(_T)", Command = ApplicationCommands.Cut, CommandTarget = box });
            menu.Items.Add(new MenuItem { Header = "コピー(_C)", Command = ApplicationCommands.Copy, CommandTarget = box });
            menu.Items.Add(new MenuItem { Header = "貼り付け(_P)", Command = ApplicationCommands.Paste, CommandTarget = box });
            menu.Items.Add(new Separator());
            var insert = new MenuItem { Header = "差し込み欄を挿入(_I)" };
            foreach (string name in _vm.InsertableNames)
            {
                var item = new MenuItem { Header = "{" + name + "}" };
                string n = name;
                item.Click += (s, e) =>
                {
                    box.SelectedText = "{" + n + "}";
                    box.SelectionStart += box.SelectionLength;
                    box.SelectionLength = 0;
                    box.Focus();
                };
                insert.Items.Add(item);
            }
            menu.Items.Add(insert);
            menu.Items.Add(new Separator());
            menu.Items.Add(MarkupItem("上付きにする(_U)", () => MarkupEditing.Toggle(box, TextMarkup.SuperscriptOpen, TextMarkup.SuperscriptClose)));
            menu.Items.Add(MarkupItem("下付きにする(_D)", () => MarkupEditing.Toggle(box, TextMarkup.SubscriptOpen, TextMarkup.SubscriptClose)));
            menu.Items.Add(MarkupItem("ルビを付ける(_R)", () => MarkupEditing.AddRuby(box)));
        }

        private static MenuItem MarkupItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            return item;
        }


        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_box == null) return;
            var modifiers = Keyboard.Modifiers;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            switch (key)
            {
                case Key.Escape:
                    End(false);
                    _surface.Focus();
                    e.Handled = true;
                    break;
                case Key.B when modifiers == ModifierKeys.Control:
                    _vm.ToggleBold();
                    ApplyStyle();
                    e.Handled = true;
                    break;
                case Key.I when modifiers == ModifierKeys.Control:
                    _vm.ToggleItalic();
                    ApplyStyle();
                    e.Handled = true;
                    break;
                case Key.Enter when _table != null && (modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) != 0:
                    // セルでは Alt+Enter（Excel と同じ）か Shift+Enter で改行する
                    _box.SelectedText = Environment.NewLine;
                    _box.SelectionStart += _box.SelectionLength;
                    _box.SelectionLength = 0;
                    e.Handled = true;
                    break;
                case Key.Enter when _table != null || (modifiers & ModifierKeys.Control) != 0:
                    {
                        var table = _table;
                        int row = _row, column = _column;
                        End(true);
                        if (table != null && modifiers == ModifierKeys.None) MoveCell(table, row, column, 1, 0);
                        _surface.Focus();
                        e.Handled = true;
                        break;
                    }
                case Key.Tab when _table != null:
                    {
                        var table = _table;
                        int row = _row, column = _column;
                        End(true);
                        MoveCell(table, row, column, 0, (modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                        _surface.Focus();
                        e.Handled = true;
                        break;
                    }
            }
        }

        /// <summary>太字・斜体を切り替えたあとの見た目を、入力欄に反映する。</summary>
        private void ApplyStyle()
        {
            if (_box == null || _style == null) return;
            var (bold, italic) = _style();
            _box.FontWeight = bold ? FontWeights.Bold : _face?.Weight ?? FontWeights.Normal;
            _box.FontStyle = italic ? FontStyles.Italic : _face?.Style ?? FontStyles.Normal;
        }

        /// <summary>選んでいるセルを動かす（Enter で下、Tab で右）。端では折り返す。</summary>
        private void MoveCell(TableElement table, int row, int column, int dRow, int dColumn)
        {
            if (dColumn != 0)
            {
                // 結合したセルは飛ばす
                var owners = table.GetOwnerMap();
                int r = row, c = column;
                do
                {
                    c += dColumn;
                    if (c >= table.Columns.Count) { c = 0; r++; }
                    if (c < 0) { c = table.Columns.Count - 1; r--; }
                    if (r < 0 || r >= table.EditableRowCount) return;
                } while (owners[r, c] != (r, c));
                _vm.SelectCells(table, new CellRange(r, c, r, c));
                return;
            }
            var map = table.GetOwnerMap();
            int next = row;
            do next += dRow; while (next < table.EditableRowCount && map[next, column].Row == row);
            if (next < table.EditableRowCount)
            {
                var (r, c) = map[next, column];
                _vm.SelectCells(table, new CellRange(r, c, r, c));
            }
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 入力欄の右クリックメニューを開いたときは続ける
            if (_box?.ContextMenu?.IsOpen == true) return;
            if (e.NewFocus is DependencyObject d && _box != null && IsInside(d, _box)) return;
            End(true);
        }

        private static bool IsInside(DependencyObject d, DependencyObject ancestor)
        {
            for (var x = d; x != null; x = x is Visual ? VisualTreeHelper.GetParent(x) : LogicalTreeHelper.GetParent(x))
            {
                if (x == ancestor) return true;
            }
            return false;
        }

        /// <summary>編集を終える。commit が false なら、元の文字に戻す。</summary>
        public void End(bool commit)
        {
            if (_box == null || _ending) return;
            _ending = true;
            try
            {
                if (!commit && _set != null && _get!() != _original) _set(_original);
                else if (commit && _set != null && _get!() != _box.Text) _set(_box.Text);
                var box = _box;
                _box = null;
                _layer.Children.Remove(box);
                _get = null;
                _set = null;
                _bounds = null;
                _style = null;
                _table = null;
                _vm.EndInteraction();
            }
            finally
            {
                _ending = false;
            }
        }

        private void UpdatePosition()
        {
            if (_box == null || _bounds == null) return;
            var mm = _bounds();
            if (mm == null)
            {
                End(true);
                return;
            }
            var rect = _surface.MmToView(mm.Value);
            var origin = _surface.TranslatePoint(rect.TopLeft, _layer);
            Canvas.SetLeft(_box, origin.X);
            Canvas.SetTop(_box, origin.Y);
            _box.Width = Math.Max(24, rect.Width);
            _box.Height = Math.Max(_box.FontSize * 1.6, rect.Height);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // デザインを開き直した・元に戻した・ページを切り替えたときは、編集中の部品が見えなくなるので終える
            // 倍率を変えたときも、文字の大きさが合わなくなるので終える
            if (e.PropertyName == nameof(MainViewModel.Report) || e.PropertyName == nameof(MainViewModel.CurrentPage) || e.PropertyName == nameof(MainViewModel.Zoom)) End(true);
        }
    }
}
