using System;
using System.IO;
using System.Text.Json;
using ChohyoPdf.Designer.Model;

namespace ChohyoPdf.Designer.Services
{
    /// <summary>アプリケーションの設定（%APPDATA%\ChohyoPdfDesigner\settings.json に保存する）。</summary>
    public sealed class AppSettings : ObservableObject
    {
        private bool _showFontFaces = true;
        private bool _liveFontPreview = true;
        private bool _continuousPages = true;

        /// <summary>現在の設定。</summary>
        public static AppSettings Current { get; private set; } = new AppSettings();

        /// <summary>フォントの一覧で、フォント名をそのフォントで表示する。</summary>
        public bool ShowFontFaces { get => _showFontFaces; set => Set(ref _showFontFaces, value); }

        /// <summary>フォントの一覧でマウスを重ねたフォントを、デザイン画面に仮に適用して見せる。</summary>
        public bool LiveFontPreview { get => _liveFontPreview; set => Set(ref _liveFontPreview, value); }

        /// <summary>デザイン画面に、ページをつなげて表示する（Word の印刷レイアウトのように）。</summary>
        public bool ContinuousPages { get => _continuousPages; set => Set(ref _continuousPages, value); }

        private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChohyoPdfDesigner", "settings.json");

        /// <summary>保存した設定を読み込む。読めなければ既定の設定を使う。</summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath)) Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                Current = new AppSettings();
            }
        }

        /// <summary>設定を保存する。保存できなくても例外は投げない（設定は次回の起動まで有効）。</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>値を写す（設定の画面で、OK を押したときに反映する）。</summary>
        public void CopyFrom(AppSettings other)
        {
            ShowFontFaces = other.ShowFontFaces;
            LiveFontPreview = other.LiveFontPreview;
            ContinuousPages = other.ContinuousPages;
        }
    }
}
