using System;
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

    /// <summary>色（"#RRGGBB"）を入力する。見本のボタンからパレットを開いて選ぶこともできる。</summary>
    public sealed class ColorBox : UserControl
    {
        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color), typeof(string), typeof(ColorBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorBox)d).UpdateView()));

        public static readonly DependencyProperty AllowNoneProperty = DependencyProperty.Register(
            nameof(AllowNone), typeof(bool), typeof(ColorBox), new PropertyMetadata(true));

        private static readonly string[] Palette =
        {
            "#000000", "#404040", "#606060", "#808080", "#A0A0A0", "#C0C0C0", "#E0E0E0", "#FFFFFF",
            "#C00000", "#FF0000", "#FF8080", "#FFE0E0", "#E06000", "#FFA040", "#FFD0A0", "#FFF0E0",
            "#806000", "#E0B000", "#FFE060", "#FFFFE0", "#006000", "#00A040", "#80D0A0", "#E0F5E8",
            "#003080", "#0060C0", "#4090E0", "#DCE6F2", "#400080", "#8040C0", "#C0A0E0", "#F0E8FA",
            "#F5F8FC", "#F5F5F5", "#FFF8E8", "#EEF7EE",
        };

        private readonly Border _swatch = new Border { Width = 18, Height = 18, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
        private readonly TextBox _text = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
        private readonly Popup _popup = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };

        public ColorBox()
        {
            var button = new ToggleButton { Content = _swatch, Padding = new Thickness(3), Margin = new Thickness(0, 0, 4, 0), ToolTip = "色を選ぶ" };
            button.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Popup.IsOpen)) { Source = _popup, Mode = BindingMode.TwoWay });
            _popup.PlacementTarget = button;
            _popup.Child = BuildPalette(Choose, this, AllowNoneProperty);

            _text.LostFocus += (s, e) => Commit();
            _text.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) Commit();
            };

            // 最後の子（入力欄）が残りの幅を使う
            var panel = new DockPanel();
            DockPanel.SetDock(button, Dock.Left);
            panel.Children.Add(_popup);
            panel.Children.Add(button);
            panel.Children.Add(_text);
            Content = panel;
            UpdateView();
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

        /// <summary>色の一覧（パレット）を作る。choose には選んだ色（「なし」なら null）を渡す。</summary>
        /// <param name="allowNoneOwner">「なし」のボタンを出すかを、この要素の allowNoneProperty で決める。</param>
        internal static FrameworkElement BuildPalette(Action<string?> choose, DependencyObject allowNoneOwner, DependencyProperty allowNoneProperty)
        {
            var wrap = new WrapPanel { Width = 8 * 24 };
            foreach (string hex in Palette)
            {
                var b = new Button
                {
                    Width = 20, Height = 20, Margin = new Thickness(2), ToolTip = hex,
                    Background = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex)),
                    Style = Application.Current.TryFindResource("SwatchButton") as Style,
                };
                b.Click += (s, e) => choose(hex);
                wrap.Children.Add(b);
            }
            var none = new Button { Content = "なし", Margin = new Thickness(2, 4, 2, 2), Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Left };
            none.Click += (s, e) => choose(null);
            none.SetBinding(VisibilityProperty, new Binding { Path = new PropertyPath(allowNoneProperty), Source = allowNoneOwner, Converter = new BooleanToVisibilityConverter() });
            var stack = new StackPanel { Margin = new Thickness(6) };
            stack.Children.Add(wrap);
            stack.Children.Add(none);
            return new Border
            {
                Child = stack, Background = Brushes.White, BorderBrush = Application.Current.TryFindResource("PanelBorder") as Brush ?? Brushes.Gray,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(2),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.16 },
                Margin = new Thickness(0, 3, 10, 12),
            };
        }

        private void Choose(string? hex)
        {
            _popup.IsOpen = false;
            Color = hex;
        }

        /// <summary>入力途中の色を確定する。</summary>
        public void CommitText()
        {
            if (_text.Text != (Color ?? "")) Commit();
        }

        private void Commit()
        {
            string t = _text.Text.Trim();
            if (t.Length == 0)
            {
                if (AllowNone) Color = null;
                else UpdateView();
                return;
            }
            if (!t.StartsWith("#", StringComparison.Ordinal)) t = "#" + t;
            if (TryParse(t, out _)) Color = t.ToUpperInvariant();
            else UpdateView();
        }

        private void UpdateView()
        {
            _text.Text = Color ?? "";
            if (Color != null && TryParse(Color, out var c))
                _swatch.Background = new SolidColorBrush(c);
            else
                _swatch.Background = new LinearGradientBrush(Colors.White, Colors.White, 45)
                {
                    GradientStops = { new GradientStop(Colors.White, 0.45), new GradientStop(Colors.Red, 0.5), new GradientStop(Colors.White, 0.55) },
                };
        }

        /// <summary>"#RGB"、"#RRGGBB"、"#RRGGBBAA" を読む（ChohyoPdf の PdfColor.FromHex と同じ形式）。</summary>
        public static bool TryParse(string hex, out System.Windows.Media.Color color)
        {
            color = Colors.Transparent;
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            if (h.Length != 6 && h.Length != 8) return false;
            if (!uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
            if (h.Length == 6) color = System.Windows.Media.Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            else color = System.Windows.Media.Color.FromArgb((byte)v, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8));
            return true;
        }
    }
}
