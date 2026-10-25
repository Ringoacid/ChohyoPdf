using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ChohyoPdf.Designer.Controls
{
    /// <summary>
    /// 差し込み項目・一覧を用紙で使っている数を表す丸（差し込みのウィンドウ）。0 のときは点線の丸にして目立たせる。
    /// 見た目は App.xaml のスタイルで決める。
    /// </summary>
    public sealed class UsageBadge : Control
    {
        public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
            nameof(Count), typeof(int), typeof(UsageBadge), new PropertyMetadata(0, (d, e) => ((UsageBadge)d).UpdateToolTip()));

        public static readonly DependencyProperty IsListProperty = DependencyProperty.Register(
            nameof(IsList), typeof(bool), typeof(UsageBadge), new PropertyMetadata(false, (d, e) => ((UsageBadge)d).UpdateToolTip()));

        public UsageBadge() => UpdateToolTip();

        /// <summary>使っている数（項目は差し込み欄の数、一覧は表の数）。</summary>
        public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }

        /// <summary>一覧の使用数（説明の文が変わる）。</summary>
        public bool IsList { get => (bool)GetValue(IsListProperty); set => SetValue(IsListProperty, value); }

        private void UpdateToolTip()
        {
            string text = Count == 0 ? "用紙でまだ使っていません"
                : IsList ? "用紙の表 " + Count + " つで使用中"
                : "用紙で " + Count + " か所に使用中";
            ToolTip = text;
            AutomationProperties.SetName(this, text);
        }
    }
}
