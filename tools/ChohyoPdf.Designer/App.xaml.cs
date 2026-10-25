using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChohyoPdf.Designer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 暗黙のスタイルは派生クラス（MainWindow など）には効かないので、Window の既定のスタイルにする
            FrameworkElement.StyleProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata { DefaultValue = FindResource(typeof(Window)) });
            Services.AppSettings.Load();
            // 1 行の入力欄では、Enter キーで入力を確定する（数値の欄は入力欄を離れたときに反映するため）
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.KeyDownEvent, new KeyEventHandler((sender, args) =>
            {
                if (args.Key == Key.Enter && sender is TextBox box && !box.AcceptsReturn)
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }));
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show(MainWindow, "エラーが発生しました。\n" + args.Exception.Message, "ChohyoPdf Designer", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (e.Args.Length > 0) window.ViewModel.Open(e.Args[0]);
        }
    }
}
