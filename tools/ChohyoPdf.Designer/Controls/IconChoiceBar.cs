using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ChohyoPdf.Designer.Controls
{
    using Orientation = System.Windows.Controls.Orientation;

    /// <summary>
    /// アイコンのボタンを並べて、1 つを選ぶ（Office の揃えのボタンのように）。
    /// <see cref="AllowNone"/> が true なら、選んでいるボタンをもう一度押すと選択をやめる（null にする）。
    /// </summary>
    public sealed class IconChoiceBar : ContentControl
    {
        public static readonly DependencyProperty ChoicesProperty = DependencyProperty.Register(
            nameof(Choices), typeof(IReadOnlyList<IconChoice>), typeof(IconChoiceBar),
            new PropertyMetadata(null, (d, e) => ((IconChoiceBar)d).Build()));

        public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
            nameof(SelectedValue), typeof(object), typeof(IconChoiceBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((IconChoiceBar)d).UpdateChecks()));

        public static readonly DependencyProperty AllowNoneProperty = DependencyProperty.Register(
            nameof(AllowNone), typeof(bool), typeof(IconChoiceBar), new PropertyMetadata(false));

        private readonly StackPanel _panel = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly List<(ToggleButton Button, IconChoice Choice)> _buttons = new List<(ToggleButton, IconChoice)>();
        private bool _updating;

        public IconChoiceBar()
        {
            Content = _panel;
            Focusable = false;
        }

        public IReadOnlyList<IconChoice>? Choices
        {
            get => (IReadOnlyList<IconChoice>?)GetValue(ChoicesProperty);
            set => SetValue(ChoicesProperty, value);
        }

        public object? SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        public bool AllowNone
        {
            get => (bool)GetValue(AllowNoneProperty);
            set => SetValue(AllowNoneProperty, value);
        }

        private void Build()
        {
            _panel.Children.Clear();
            _buttons.Clear();
            if (Choices == null) return;
            foreach (var choice in Choices)
            {
                var button = new ToggleButton
                {
                    Content = Icons.FromGeometry(choice.Geometry),
                    ToolTip = choice.ToolTip,
                    Style = (Style)FindResource("IconToggle"),
                };
                var c = choice;
                button.Click += (s, e) => OnClick(c);
                _panel.Children.Add(button);
                _buttons.Add((button, choice));
            }
            UpdateChecks();
        }

        private void OnClick(IconChoice choice)
        {
            if (_updating) return;
            SelectedValue = AllowNone && Equals(SelectedValue, choice.Value) ? null : choice.Value;
            UpdateChecks();
        }

        private void UpdateChecks()
        {
            _updating = true;
            foreach (var (button, choice) in _buttons) button.IsChecked = Equals(SelectedValue, choice.Value);
            _updating = false;
        }
    }
}
