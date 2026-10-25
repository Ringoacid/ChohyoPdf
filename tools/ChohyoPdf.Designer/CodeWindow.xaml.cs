using System;
using System.IO;
using System.Text;
using System.Windows;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using Microsoft.Win32;

namespace ChohyoPdf.Designer
{
    /// <summary>デザインから作った C# コードを表示して、コピー・保存する。</summary>
    public partial class CodeWindow : Window
    {
        private readonly ReportDefinition _report;
        private readonly string? _designPath;
        private bool _loaded;

        public CodeWindow(ReportDefinition report, string? designPath)
        {
            InitializeComponent();
            _report = report;
            _designPath = designPath;
            _loaded = true;
            Generate();
        }

        private CodeGenOptions Options => new CodeGenOptions
        {
            Namespace = NamespaceBox.Text,
            ClassName = ClassNameBox.Text,
            IncludeSampleData = SampleBox.IsChecked == true,
            DesignFileName = _designPath != null ? Path.GetFileName(_designPath) : null,
        };

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loaded) Generate();
        }

        private void Generate()
        {
            string code = CSharpGenerator.Generate(_report, Options);
            CodeBox.Text = code;
            UsageBox.Text = Usage(code);
        }

        /// <summary>生成したコードの呼び出し方の例。</summary>
        private string Usage(string code)
        {
            string? reportClass = FindClass(code, "public static class ");
            string? dataClass = FindClass(code, "public class ");
            if (reportClass == null || dataClass == null) return "";
            var sb = new StringBuilder();
            if (SampleBox.IsChecked == true)
                sb.AppendLine("var data = " + dataClass + ".CreateSample();   // または new " + dataClass + " { ... } で値を入れる");
            else
                sb.AppendLine("var data = new " + dataClass + " { /* 値を入れる */ };");
            sb.Append(reportClass + ".Save(data, \"" + (_report.Name.Length > 0 ? _report.Name : "文書") + ".pdf\");   // PdfDocument が欲しいときは " + reportClass + ".Create(data)");
            return sb.ToString();
        }

        private static string? FindClass(string code, string prefix)
        {
            int i = code.IndexOf(prefix, StringComparison.Ordinal);
            if (i < 0) return null;
            int start = i + prefix.Length;
            int end = code.IndexOfAny(new[] { '\r', '\n', ' ' }, start);
            return end < 0 ? null : code.Substring(start, end - start);
        }

        private void Copy_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(CodeBox.Text);

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string? name = FindClass(CodeBox.Text, "public static class ");
            var dialog = new SaveFileDialog { Filter = "C# (*.cs)|*.cs", FileName = (name ?? "Report").TrimStart('@') + ".cs" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllText(dialog.FileName, CodeBox.Text, new UTF8Encoding(true));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "保存できません。\n" + ex.Message, "C# コードを出力", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
