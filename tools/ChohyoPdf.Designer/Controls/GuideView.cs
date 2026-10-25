using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChohyoPdf.Designer.Controls
{
    // 名前空間 ChohyoPdf の同名の型より WPF の型を優先する
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using TextAlignment = System.Windows.TextAlignment;
    using Thickness = System.Windows.Thickness;
    using VerticalAlignment = System.Windows.VerticalAlignment;

    /// <summary>使い方の 1 つの手順（実際の画面の画像と説明）。</summary>
    public sealed class GuideStep
    {
        public GuideStep(string image, string title, string text)
        {
            Image = image;
            Title = title;
            Text = text;
        }

        /// <summary>画像のファイル名（Images/Guide の中）。</summary>
        public string Image { get; }

        public string Title { get; }

        public string Text { get; }
    }

    /// <summary>
    /// 使い方を、実際の画面の画像で手順ごとに見せる（スライドのように自動で送る。止めたり、手で送ったりもできる）。
    /// </summary>
    public sealed class GuideView : UserControl
    {
        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(
            nameof(Steps), typeof(IReadOnlyList<GuideStep>), typeof(GuideView), new PropertyMetadata(null, (d, e) => ((GuideView)d).OnStepsChanged()));

        /// <summary>手順を送る間隔。</summary>
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(6);

        private readonly Image _image = new Image { Stretch = Stretch.Uniform };
        private readonly TextBlock _number = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
        private readonly TextBlock _title = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _text = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 19, Margin = new Thickness(0, 4, 0, 0) };
        private readonly StackPanel _dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly Button _play;
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = Interval };
        private int _index;
        private bool _playing = true;

        public GuideView()
        {
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
            _number.SetResourceReference(TextBlock.ForegroundProperty, "StepText");
            _text.SetResourceReference(TextBlock.ForegroundProperty, "SubtleText");

            // 画像（角の丸い枠の中）
            var imageFrame = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Height = 300,
                Child = _image,
                Cursor = Cursors.Hand,
                ToolTip = "クリックすると次の手順へ進みます",
            };
            imageFrame.SetResourceReference(Border.BackgroundProperty, "ChromeBackground");
            imageFrame.SetResourceReference(Border.BorderBrushProperty, "PanelBorder");
            imageFrame.MouseLeftButtonUp += (s, e) => Go(_index + 1, true);

            // 手順の番号・見出し・説明
            var badge = new Grid { Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };
            var circle = new Ellipse();
            circle.SetResourceReference(Shape.FillProperty, "StepBackground");
            badge.Children.Add(circle);
            badge.Children.Add(_number);
            var heading = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(badge, Dock.Left);
            heading.Children.Add(badge);
            _title.Margin = new Thickness(8, 0, 0, 0);
            heading.Children.Add(_title);

            // 送るボタンと、手順の点
            var previous = NavButton("", "前の手順");
            previous.Click += (s, e) => Go(_index - 1, true);
            var next = NavButton("", "次の手順");
            next.Click += (s, e) => Go(_index + 1, true);
            _play = NavButton("", "自動で送るのを止める");
            _play.Click += (s, e) => SetPlaying(!_playing);
            var controls = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(_play);
            buttons.Children.Add(previous);
            buttons.Children.Add(next);
            DockPanel.SetDock(buttons, Dock.Right);
            DockPanel.SetDock(_dots, Dock.Left);
            controls.Children.Add(buttons);
            controls.Children.Add(_dots);

            var root = new StackPanel();
            root.Children.Add(imageFrame);
            root.Children.Add(heading);
            root.Children.Add(new Border { Margin = new Thickness(30, 0, 0, 0), Child = _text, MinHeight = 40 });
            root.Children.Add(controls);
            Content = root;

            _timer.Tick += (s, e) => Go(_index + 1, false);
            // 見えている間だけ、自動で送る。マウスを重ねている間は止める
            IsVisibleChanged += (s, e) => UpdateTimer();
            MouseEnter += (s, e) => UpdateTimer();
            MouseLeave += (s, e) => UpdateTimer();
        }

        public IReadOnlyList<GuideStep>? Steps
        {
            get => (IReadOnlyList<GuideStep>?)GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        /// <summary>表示している手順（0 から。動作確認用）。</summary>
        internal int Index => _index;

        /// <summary>最初の手順から見せ直す。</summary>
        public void Restart()
        {
            _index = 0;
            SetPlaying(true);
            Show();
        }

        private Button NavButton(string glyph, string toolTip)
        {
            var button = new Button { Content = glyph, ToolTip = toolTip, Width = 30, Height = 28, Margin = new Thickness(4, 0, 0, 0) };
            button.SetResourceReference(StyleProperty, "IconButton");
            button.FontSize = 12;
            System.Windows.Automation.AutomationProperties.SetName(button, toolTip);
            return button;
        }

        private void OnStepsChanged()
        {
            _index = 0;
            _dots.Children.Clear();
            int count = Steps?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                int target = i;
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
                dot.MouseLeftButtonUp += (s, e) => Go(target, true);
                _dots.Children.Add(dot);
            }
            Show();
            UpdateTimer();
        }

        /// <summary>手順を移る。user なら、利用者の操作なので自動で送るのを止める。</summary>
        private void Go(int index, bool user)
        {
            int count = Steps?.Count ?? 0;
            if (count == 0) return;
            _index = (index % count + count) % count;
            if (user) SetPlaying(false);
            Show();
            // 手で送ったときも、次の自動の送りまでの時間をやり直す
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _timer.Start();
            }
        }

        private void SetPlaying(bool playing)
        {
            _playing = playing;
            _play.Content = playing ? "" : "";
            _play.ToolTip = playing ? "自動で送るのを止める" : "自動で送る";
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            bool run = _playing && IsVisible && !IsMouseOver && (Steps?.Count ?? 0) > 1;
            if (run && !_timer.IsEnabled) _timer.Start();
            else if (!run) _timer.Stop();
        }

        private void Show()
        {
            var steps = Steps;
            if (steps == null || steps.Count == 0) return;
            var step = steps[_index];
            _image.Source = LoadImage(step.Image);
            _number.Text = (_index + 1).ToString();
            _title.Text = step.Title;
            _text.Text = step.Text;
            for (int i = 0; i < _dots.Children.Count; i++)
            {
                var dot = (Border)_dots.Children[i];
                if (i == _index)
                {
                    dot.Width = 20;
                    dot.SetResourceReference(Border.BackgroundProperty, "Accent");
                }
                else
                {
                    dot.Width = 8;
                    dot.SetResourceReference(Border.BackgroundProperty, "ControlBorder");
                }
            }
        }

        private static readonly Dictionary<string, BitmapImage> Cache = new Dictionary<string, BitmapImage>();

        private static BitmapImage? LoadImage(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            try
            {
                var image = new BitmapImage(new Uri("pack://application:,,,/Images/Guide/" + name, UriKind.Absolute));
                image.Freeze();
                Cache[name] = image;
                return image;
            }
            catch (Exception)
            {
                // 画像がなくても、説明の文は表示する
                return null;
            }
        }
    }

    /// <summary>差し込みの使い方の手順（画像は、実際のデザイナーの画面を撮ったもの）。</summary>
    public static class Guides
    {
        public static IReadOnlyList<GuideStep> Fields { get; } = new[]
        {
            new GuideStep("field-1.png", "差し込み項目を作る", "「＋ 追加」で項目を作り、名前（例: 氏名）と、確認用の値（例: 山田 太郎）を入れます。"),
            new GuideStep("field-2.png", "用紙に {氏名} と書く", "テキストに {氏名} と書いた所に、値が入ります。項目を用紙へドラッグして置くこともできます。"),
            new GuideStep("field-3.png", "「結果を表示」で確かめる", "タイトルバーの「結果を表示」を押すと、確認用の値を入れた見た目になります。プレビューや PDF の出力にも、この値を使います。"),
            new GuideStep("field-4.png", "PDF ごとに値を変える", "出力した C# コードでは、data.氏名 のように値を渡します。同じデザインのまま、宛名だけを変えた PDF を何枚も作れます。"),
        };

        public static IReadOnlyList<GuideStep> Lists { get; } = new[]
        {
            new GuideStep("list-1.png", "一覧を作り、列と確認用の行を入れる", "「＋ 追加」で一覧（例: 参加者）を作り、1 行に並べる列（氏名・所属など）を決めます。下の表に確認用の行を入れます（Excel から貼り付けもできます）。"),
            new GuideStep("list-2.png", "一覧の表を用紙に置く", "「この一覧の表を用紙に置く」を押すと、緑色のくり返し行のある表ができます。くり返し行のセルには {氏名} のように列の名前を書きます。"),
            new GuideStep("list-3.png", "件数だけ行がくり返される", "「結果を表示」を押すと、確認用の行が 1 件ずつ並びます。1 ページに入りきらなければ、ページを増やして続きを描きます。"),
        };

        public static IReadOnlyList<GuideStep> BuiltIns { get; } = new[]
        {
            new GuideStep("builtin-1.png", "値を渡さなくても入る欄", "{ページ番号} {総ページ数} {今日} などは、値を渡さなくても使えます。ページ番号は「全ページ共通の部品」に置くと、すべてのページに入ります。"),
            new GuideStep("builtin-2.png", "書式を付ける", "「:」のあとに書式を書けます。例: {今日:yyyy年M月d日}、{ページ番号:000}。「結果を表示」で、今の日時で確かめられます。"),
        };
    }
}
