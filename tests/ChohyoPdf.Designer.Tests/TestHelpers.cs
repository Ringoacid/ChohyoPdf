using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using ChohyoPdf.Designer.Model;
using ChohyoPdf.Designer.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ChohyoPdf.Designer.Tests
{
    internal static class TestFonts
    {
        private static (string Name, string? Bold)? _font;
        private static bool _searched;

        /// <summary>この環境で使えるフォント（見つからなければ null）。日本語フォントを優先する。</summary>
        public static (string Name, string? Bold)? Available
        {
            get
            {
                if (_searched) return _font;
                _searched = true;
                var candidates = new (string, string?)[]
                {
                    ("Yu Gothic Regular", "Yu Gothic Bold"), ("Meiryo", "Meiryo Bold"), ("MS Gothic", null),
                    ("Noto Sans JP", null), ("IPAexGothic", null), ("Arial", "Arial Bold"), ("Segoe UI", "Segoe UI Bold"), ("DejaVu Sans", null),
                };
                foreach (var (name, bold) in candidates)
                {
                    if (!FontProvider.TryCheck(name, out _)) continue;
                    _font = (name, bold != null && FontProvider.TryCheck(bold, out _) ? bold : null);
                    return _font;
                }
                return null;
            }
        }

        /// <summary>デザインのフォントを、この環境で使えるフォントに置き換える。使えるフォントがなければ false。</summary>
        public static bool Apply(ReportDefinition report)
        {
            var font = Available;
            if (font == null) return false;
            report.DefaultFontName = font.Value.Name;
            report.Fonts.Clear();
            if (font.Value.Bold != null) report.Fonts.Add(new FontDefinition { Name = font.Value.Name, BoldName = font.Value.Bold });
            foreach (var e in report.AllElements())
            {
                if (e is TextElement t) t.FontName = null;
                if (e is TableElement tb) tb.FontName = null;
            }
            return true;
        }
    }

    internal static class TestData
    {
        public static string Folder => Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    /// <summary>生成したコードをコンパイルして実行する。</summary>
    internal static class Compiler
    {
        private static readonly Lazy<List<MetadataReference>> References = new Lazy<List<MetadataReference>>(() =>
        {
            var refs = new List<MetadataReference>();
            string tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
            foreach (string path in tpa.Split(Path.PathSeparator))
            {
                if (path.Length > 0) refs.Add(MetadataReference.CreateFromFile(path));
            }
            refs.Add(MetadataReference.CreateFromFile(typeof(PdfDocument).Assembly.Location));
            return refs;
        });

        /// <summary>コンパイルする。エラーがあれば例外、警告があれば warnings に入れる。</summary>
        public static Assembly Compile(string code, LanguageVersion version, bool nullable, List<string> warnings)
        {
            var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(version));
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable);
            var compilation = CSharpCompilation.Create("Generated" + Guid.NewGuid().ToString("N"), new[] { tree }, References.Value, options);
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            foreach (var d in result.Diagnostics)
            {
                if (d.Severity == DiagnosticSeverity.Warning) warnings.Add(d.ToString());
            }
            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());
                throw new InvalidOperationException("生成したコードをコンパイルできません:\n" + string.Join("\n", errors) + "\n\n" + code);
            }
            ms.Position = 0;
            return AssemblyLoadContext.Default.LoadFromStream(ms);
        }
    }
}
