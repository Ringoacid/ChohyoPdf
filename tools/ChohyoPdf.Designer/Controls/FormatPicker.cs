using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Controls
{
    using Orientation = System.Windows.Controls.Orientation;
    using Thickness = System.Windows.Thickness;

    /// <summary>
    /// 数値・日付の書式を選ぶ。よく使う書式を、サンプルの値に書式を付けた見本から選べる。
    /// 一覧にない書式は「その他」を選んで、書式指定文字列を直接入力する。
    /// </summary>
    public sealed class FormatPicker : ContentControl
    {
        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            nameof(Kind), typeof(FieldKind), typeof(FormatPicker), new PropertyMetadata(FieldKind.Text, (d, e) => ((FormatPicker)d).Rebuild()));

        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
            nameof(Format), typeof(string), typeof(FormatPicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((FormatPicker)d).OnFormatChanged()));

        public static readonly DependencyProperty SampleProperty = DependencyProperty.Register(
            nameof(Sample), typeof(string), typeof(FormatPicker), new PropertyMetadata("", (d, e) => ((FormatPicker)d).Rebuild()));

        /// <summary>選択肢の 1 つ（見本と説明を表示する）。</summary>
        private sealed class Option
        {
            public FormatPreset? Preset { get; set; }
            public string Example { get; set; } = "";
            public string Description { get; set; } = "";
        }

        private readonly ComboBox _combo = new ComboBox();
        private readonly TextBox _custom = new TextBox { Margin = new Thickness(0, 4, 0, 0), ToolTip = "書式指定文字列（例: #,0.0、yyyy年M月d日）" };
        private readonly TextBlock _example = new TextBlock { Margin = new Thickness(0, 3, 0, 0), FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
        private readonly List<Option> _options = new List<Option>();
        private bool _updating, _customMode;

        public FormatPicker()
        {
            Focusable = false;
            _combo.ItemTemplate = CreateItemTemplate();
            _combo.SelectionChanged += (s, e) => OnComboChanged();
            _custom.TextChanged += (s, e) =>
            {
                if (_updating) return;
                _updating = true;
                Format = _custom.Text.Length == 0 ? null : _custom.Text;
                _updating = false;
                UpdateExample();
            };
            var panel = new StackPanel();
            panel.Children.Add(_combo);
            panel.Children.Add(_custom);
            panel.Children.Add(_example);
            Content = panel;
            Rebuild();
        }

        public FieldKind Kind
        {
            get => (FieldKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        /// <summary>書式指定文字列。null なら既定の書式。</summary>
        public string? Format
        {
            get => (string?)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        /// <summary>見本に使うサンプルの値。</summary>
        public string? Sample
        {
            get => (string?)GetValue(SampleProperty);
            set => SetValue(SampleProperty, value);
        }

        private static DataTemplate CreateItemTemplate()
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var example = new FrameworkElementFactory(typeof(TextBlock));
            example.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Option.Example)));
            example.SetValue(TextBlock.MinWidthProperty, 96.0);
            example.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            var description = new FrameworkElementFactory(typeof(TextBlock));
            description.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Option.Description)));
            description.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 0, 0));
            description.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x5F, 0x66, 0x70)));
            panel.AppendChild(example);
            panel.AppendChild(description);
            return new DataTemplate { VisualTree = panel };
        }

        private void Rebuild()
        {
            _updating = true;
            _options.Clear();
            foreach (var preset in FormatPresets.For(Kind))
                _options.Add(new Option { Preset = preset, Example = FormatPresets.Example(Kind, preset.Format, Sample) ?? "", Description = preset.Description });
            _options.Add(new Option { Example = "その他…", Description = "書式を入力する" });
            _combo.ItemsSource = _options.ToArray();
            _updating = false;
            Sync();
        }

        private void OnFormatChanged()
        {
            // 外から変わった（別の項目を選んだなど）ときは、選択肢から選び直す
            if (!_updating) _customMode = false;
            Sync();
        }

        /// <summary>書式に合わせて、選んでいる選択肢と入力欄を変える。</summary>
        private void Sync()
        {
            if (_updating) return;
            _updating = true;
            var preset = FormatPresets.Find(Kind, Format);
            int index = preset != null && !_customMode ? _options.FindIndex(o => o.Preset == preset) : _options.Count - 1;
            _combo.SelectedIndex = index;
            bool custom = index == _options.Count - 1;
            _custom.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            if (_custom.Text != (Format ?? "")) _custom.Text = Format ?? "";
            _updating = false;
            UpdateExample();
        }

        private void OnComboChanged()
        {
            if (_updating || _combo.SelectedIndex < 0) return;
            var option = _options[_combo.SelectedIndex];
            _customMode = option.Preset == null;
            if (option.Preset != null) Format = option.Preset.Format.Length == 0 ? null : option.Preset.Format;
            Sync();
            if (_customMode) _custom.Focus();
        }

        private void UpdateExample()
        {
            string? example = FormatPresets.Example(Kind, Format, Sample);
            _example.Text = example == null ? "書式が正しくありません。" : "表示例: " + example;
            _example.Foreground = example == null ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(0x5F, 0x66, 0x70));
        }
    }
}
