using System.Windows;
using ChohyoPdf.Designer.Services;

namespace ChohyoPdf.Designer
{
    /// <summary>アプリケーションの設定の画面。OK を押したときだけ設定を変えて保存する。</summary>
    public partial class OptionsWindow : Window
    {
        private readonly AppSettings _draft = new AppSettings();

        public OptionsWindow()
        {
            InitializeComponent();
            _draft.CopyFrom(AppSettings.Current);
            DataContext = _draft;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Current.CopyFrom(_draft);
            AppSettings.Current.Save();
            DialogResult = true;
        }
    }
}
