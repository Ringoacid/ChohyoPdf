using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ChohyoPdf.Designer.Services;
using ChohyoPdf.Designer.ViewModels;

namespace ChohyoPdf.Designer.Controls
{
    /// <summary>
    /// フォントを選ぶコンボボックス。
    /// <list type="bullet">
    /// <item>フォントを指定していない（null）ときは、既定のフォントを選んだ状態で表示する。既定のフォントを選ぶと null に戻す。</item>
    /// <item>設定に応じて、一覧のフォント名をそのフォントで表示し、マウスを重ねたフォントをデザイン画面に仮に適用する。</item>
    /// </list>
    /// </summary>
    public sealed class FontComboBox : ComboBox
    {
        public static readonly DependencyProperty FontNameProperty = DependencyProperty.Register(
            nameof(FontName), typeof(string), typeof(FontComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((FontComboBox)d).UpdateText()));

        public static readonly DependencyProperty DefaultFontNameProperty = DependencyProperty.Register(
            nameof(DefaultFontName), typeof(string), typeof(FontComboBox),
            new PropertyMetadata(null, (d, e) => ((FontComboBox)d).UpdateText()));

        private readonly DispatcherTimer _previewTimer;
        private string? _hovered;
        private bool _updating;

        public FontComboBox()
        {
            IsEditable = true;
            IsTextSearchEnabled = true;
            ItemTemplate = (DataTemplate)Application.Current.FindResource("FontItemTemplate");
            var itemStyle = new Style(typeof(ComboBoxItem), Application.Current.TryFindResource(typeof(ComboBoxItem)) as Style);
            // ComboBoxItem はマウスが入ったことを処理済みにするので、処理済みのイベントも受け取る
            itemStyle.Setters.Add(new EventSetter(MouseEnterEvent, new MouseEventHandler(OnItemMouseEnter)) { HandledEventsToo = true });
            ItemContainerStyle = itemStyle;
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _previewTimer.Tick += (s, e) =>
            {
                _previewTimer.Stop();
                if (IsDropDownOpen && _hovered != null) ViewModel?.SetFontPreview(DataContext, _hovered);
            };
            Loaded += (s, e) => UpdateText();
        }

        /// <summary>選んだフォント。null なら既定のフォント。</summary>
        public string? FontName
        {
            get => (string?)GetValue(FontNameProperty);
            set => SetValue(FontNameProperty, value);
        }

        /// <summary>
        /// 既定のフォント（フォントを指定していないときに使うフォント）。
        /// 設定すると、フォントが null のときにこのフォントを表示し、このフォントを選ぶと null にする。
        /// </summary>
        public string? DefaultFontName
        {
            get => (string?)GetValue(DefaultFontNameProperty);
            set => SetValue(DefaultFontNameProperty, value);
        }

        private MainViewModel? ViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

        /// <summary>入力欄の文字を確定する。</summary>
        public void Commit()
        {
            if (_updating) return;
            string text = (Text ?? "").Trim();
            string? value = text.Length == 0 ? null : text;
            if (DefaultFontName != null && value != null && string.Equals(value, DefaultFontName, StringComparison.OrdinalIgnoreCase)) value = null;
            // 既定のフォントそのものを選ぶ欄（DefaultFontName がない）では、空にできない
            if (value == null && DefaultFontName == null)
            {
                UpdateText();
                return;
            }
            if (!string.Equals(value, FontName, StringComparison.Ordinal)) FontName = value;
            UpdateText();
        }

        private void UpdateText()
        {
            if (_updating) return;
            _updating = true;
            Text = FontName ?? DefaultFontName ?? "";
            _updating = false;
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (_updating || SelectedItem is not string name) return;
            // 一覧から選んだときだけ確定する（入力中の自動補完では確定しない）
            if (IsDropDownOpen || !IsKeyboardFocusWithin)
            {
                _updating = true;
                Text = name;
                _updating = false;
                Commit();
            }
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            base.OnDropDownOpened(e);
            _hovered = null;
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            _previewTimer.Stop();
            _hovered = null;
            ViewModel?.SetFontPreview(null, null);
            if (SelectedItem is string name && !string.Equals(name, Text, StringComparison.Ordinal)) UpdateText();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IsDropDownOpen)
            {
                Commit();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && !IsDropDownOpen)
            {
                UpdateText();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsKeyboardFocusWithinChanged(e);
            if (!(bool)e.NewValue && !IsDropDownOpen) Commit();
        }

        private void OnItemMouseEnter(object sender, MouseEventArgs e)
        {
            if (!AppSettings.Current.LiveFontPreview || sender is not ComboBoxItem item || item.DataContext is not string name) return;
            _hovered = name;
            _previewTimer.Stop();
            _previewTimer.Start();
        }
    }

    /// <summary>文字を、指定した名前のフォントで表示する（設定で「フォントをそのフォントで表示する」がオンのとき）。</summary>
    public static class FontFace
    {
        public static readonly DependencyProperty NameProperty = DependencyProperty.RegisterAttached(
            "Name", typeof(string), typeof(FontFace), new PropertyMetadata(null, OnNameChanged));

        public static string? GetName(DependencyObject d) => (string?)d.GetValue(NameProperty);

        public static void SetName(DependencyObject d, string? value) => d.SetValue(NameProperty, value);

        private static void OnNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock text) return;
            var face = AppSettings.Current.ShowFontFaces ? WpfFonts.Resolve(e.NewValue as string) : null;
            if (face == null)
            {
                text.ClearValue(TextBlock.FontFamilyProperty);
                text.ClearValue(TextBlock.FontWeightProperty);
                text.ClearValue(TextBlock.FontStyleProperty);
                return;
            }
            text.FontFamily = face.Family;
            text.FontWeight = face.Weight;
            text.FontStyle = face.Style;
        }
    }
}
