using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ChohyoPdf.Designer.Controls
{
    // 名前空間 ChohyoPdf の同名の型より WPF の型を優先する
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using Thickness = System.Windows.Thickness;
    using VerticalAlignment = System.Windows.VerticalAlignment;

    /// <summary>
    /// 書式バー・ツールのレール・タイトルバーのボタンに付ける値（アイコンと、押したときにメニューを開くか）。
    /// ボタンの見た目は MainWindow のスタイルで決める。
    /// </summary>
    public static class CommandBar
    {
        /// <summary>アイコンの文字（Segoe Fluent Icons）。</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
            "Icon", typeof(string), typeof(CommandBar), new PropertyMetadata(null));

        /// <summary>アイコンのフォント（Segoe Fluent Icons 以外の記号を使うとき）。</summary>
        public static readonly DependencyProperty IconFontProperty = DependencyProperty.RegisterAttached(
            "IconFont", typeof(FontFamily), typeof(CommandBar), new PropertyMetadata(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets")));

        /// <summary>アイコンの色。</summary>
        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.RegisterAttached(
            "IconBrush", typeof(Brush), typeof(CommandBar), new PropertyMetadata(null));

        /// <summary>文字の代わりに表示するアイコン（表の操作のアイコンなど）。</summary>
        public static readonly DependencyProperty IconElementProperty = DependencyProperty.RegisterAttached(
            "IconElement", typeof(object), typeof(CommandBar), new PropertyMetadata(null));

        /// <summary>押すと ContextMenu をボタンの下に開く（名前の横に ▾ を付ける）。</summary>
        public static readonly DependencyProperty IsDropDownProperty = DependencyProperty.RegisterAttached(
            "IsDropDown", typeof(bool), typeof(CommandBar), new PropertyMetadata(false, OnIsDropDownChanged));

        public static string? GetIcon(DependencyObject d) => (string?)d.GetValue(IconProperty);
        public static void SetIcon(DependencyObject d, string? value) => d.SetValue(IconProperty, value);
        public static FontFamily GetIconFont(DependencyObject d) => (FontFamily)d.GetValue(IconFontProperty);
        public static void SetIconFont(DependencyObject d, FontFamily value) => d.SetValue(IconFontProperty, value);
        public static Brush? GetIconBrush(DependencyObject d) => (Brush?)d.GetValue(IconBrushProperty);
        public static void SetIconBrush(DependencyObject d, Brush? value) => d.SetValue(IconBrushProperty, value);
        public static object? GetIconElement(DependencyObject d) => d.GetValue(IconElementProperty);
        public static void SetIconElement(DependencyObject d, object? value) => d.SetValue(IconElementProperty, value);
        public static bool GetIsDropDown(DependencyObject d) => (bool)d.GetValue(IsDropDownProperty);
        public static void SetIsDropDown(DependencyObject d, bool value) => d.SetValue(IsDropDownProperty, value);

        private static void OnIsDropDownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ButtonBase button) return;
            button.Click -= OpenMenu;
            if ((bool)e.NewValue) button.Click += OpenMenu;
            // 右クリックでは開かない（右クリックで開くと、メニューのコマンドを探す DataContext が変わる）
            ContextMenuService.SetIsEnabled(button, !(bool)e.NewValue);
        }

        private static void OpenMenu(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement button && button.ContextMenu is ContextMenu menu) ShowBelow(button, menu);
        }

        /// <summary>メニューをボタンの下に開く。</summary>
        public static void ShowBelow(FrameworkElement button, ContextMenu menu)
        {
            // メニューのコマンドは、画面全体の状態（MainViewModel）にある
            menu.DataContext = Window.GetWindow(button)?.DataContext ?? button.DataContext;
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    /// <summary>
    /// 押すと、下に小さな設定のパネル（Flyout）を開くボタン。パネルの中身の DataContext は、ボタンと同じ。
    /// </summary>
    public sealed class FlyoutButton : ToggleButton
    {
        public static readonly DependencyProperty FlyoutProperty = DependencyProperty.Register(
            nameof(Flyout), typeof(object), typeof(FlyoutButton), new PropertyMetadata(null, (d, e) => ((FlyoutButton)d)._host.Content = e.NewValue));

        /// <summary>
        /// パネルの中身のテンプレート。パネルはウィンドウの外（ポップアップ）に出るので、型で選ぶテンプレートは見つからない。
        /// 部品を表示するときは、Flyout に部品を、FlyoutTemplate にテンプレートを渡す。
        /// </summary>
        public static readonly DependencyProperty FlyoutTemplateProperty = DependencyProperty.Register(
            nameof(FlyoutTemplate), typeof(DataTemplate), typeof(FlyoutButton), new PropertyMetadata(null, (d, e) => ((FlyoutButton)d)._host.ContentTemplate = (DataTemplate?)e.NewValue));

        private readonly ContentPresenter _host = new ContentPresenter();
        private readonly Popup _popup = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade };

        public FlyoutButton()
        {
            Focusable = false;
            _popup.PlacementTarget = this;
            _popup.Child = new Border
            {
                Child = _host,
                Background = Brushes.White,
                BorderBrush = Application.Current.TryFindResource("PanelBorder") as Brush ?? Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 4, 12, 14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = 0.16 },
            };
            _popup.SetBinding(DataContextProperty, new Binding(nameof(DataContext)) { Source = this });
            SetBinding(IsCheckedProperty, new Binding(nameof(Popup.IsOpen)) { Source = _popup, Mode = BindingMode.TwoWay });
            // 開いているときにボタンを押すと、外側のクリックで閉じたあと、すぐにまた開いてしまうので、開いている間は押せなくする
            _popup.Opened += (s, e) => IsHitTestVisible = false;
            _popup.Closed += (s, e) => IsHitTestVisible = true;
        }

        /// <summary>パネルの中身。</summary>
        public object? Flyout
        {
            get => GetValue(FlyoutProperty);
            set => SetValue(FlyoutProperty, value);
        }

        public DataTemplate? FlyoutTemplate
        {
            get => (DataTemplate?)GetValue(FlyoutTemplateProperty);
            set => SetValue(FlyoutTemplateProperty, value);
        }
    }

    /// <summary>
    /// 色を選ぶボタン（アイコンの下に、選んでいる色の帯を出す）。
    /// 押すと色の一覧を開く。
    /// </summary>
    public sealed class ColorButton : ToggleButton
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color), typeof(string), typeof(ColorButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorButton)d).UpdateBar()));

        public static readonly DependencyProperty AllowNoneProperty = DependencyProperty.Register(
            nameof(AllowNone), typeof(bool), typeof(ColorButton), new PropertyMetadata(true));

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(ColorButton), new PropertyMetadata("", (d, e) => ((ColorButton)d)._glyph.Text = (string)e.NewValue));

        private readonly TextBlock _glyph = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly Border _bar = new Border { Height = 4, Width = 16, Margin = new Thickness(0, 1, 0, 0), BorderBrush = Brushes.Gray };
        private readonly Popup _popup = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };

        public ColorButton()
        {
            Focusable = false;
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(_glyph);
            stack.Children.Add(_bar);
            var arrow = new TextBlock
            {
                Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 7,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0),
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(stack);
            panel.Children.Add(arrow);
            panel.Children.Add(_popup);
            Content = panel;
            _popup.PlacementTarget = this;
            _popup.Child = ColorBox.BuildPalette(Choose, this, AllowNoneProperty);
            SetBinding(IsCheckedProperty, new Binding(nameof(Popup.IsOpen)) { Source = _popup, Mode = BindingMode.TwoWay });
            UpdateBar();
        }

        public string? Color
        {
            get => (string?)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>「なし」（null）を選べる。</summary>
        public bool AllowNone
        {
            get => (bool)GetValue(AllowNoneProperty);
            set => SetValue(AllowNoneProperty, value);
        }

        /// <summary>帯の上に出すアイコンの文字（Segoe Fluent Icons）。</summary>
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        private void Choose(string? hex)
        {
            _popup.IsOpen = false;
            Color = hex;
        }

        private void UpdateBar()
        {
            if (Color != null && ColorBox.TryParse(Color, out var c))
            {
                _bar.Background = new SolidColorBrush(c);
                _bar.BorderThickness = new Thickness(0);
            }
            else
            {
                // 色がないときは、白い帯に枠を付ける
                _bar.Background = Brushes.White;
                _bar.BorderThickness = new Thickness(0.5);
            }
        }
    }

    /// <summary>
    /// 文字の大きさ（ポイント）を選ぶか入力する欄。Enter・一覧から選んだとき・欄を離れたときに確定する。
    /// AllowEmpty なら空にでき、空は null（表のセルで、表の設定に従う）。
    /// </summary>
    public sealed class FontSizeBox : ComboBox
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double?), typeof(FontSizeBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((FontSizeBox)d).UpdateText()));

        public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
            nameof(AllowEmpty), typeof(bool), typeof(FontSizeBox), new PropertyMetadata(false));

        private static readonly IReadOnlyList<double> Sizes = new[] { 6, 7, 8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 28, 36, 48, 72 };
        private bool _updating;

        public FontSizeBox()
        {
            IsEditable = true;
            IsTextSearchEnabled = false;
            ItemsSource = Sizes;
            Loaded += (s, e) => UpdateText();
        }

        public double? Value
        {
            get => (double?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public bool AllowEmpty
        {
            get => (bool)GetValue(AllowEmptyProperty);
            set => SetValue(AllowEmptyProperty, value);
        }

        /// <summary>入力欄の文字を確定する。</summary>
        public void Commit()
        {
            if (_updating) return;
            string text = (Text ?? "").Trim();
            if (text.Length == 0)
            {
                if (AllowEmpty) Value = null;
                UpdateText();
                return;
            }
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double size) && size > 0 && size <= 1000)
            {
                if (Value != size) Value = size;
            }
            UpdateText();
        }

        private void UpdateText()
        {
            _updating = true;
            Text = Value?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            _updating = false;
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (_updating || SelectedItem is not double size) return;
            if (!IsDropDownOpen && IsKeyboardFocusWithin) return;
            _updating = true;
            Text = size.ToString("0.##", CultureInfo.CurrentCulture);
            _updating = false;
            Commit();
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
    }
}
