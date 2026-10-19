using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ChohyoPdf;

namespace ChohyoPdf.DocExamples
{
    /// <summary>
    /// 説明書のサンプルをすべて実行し、PDF を出力する。
    /// 各サンプルのうち「// #example 名前」から「// #end」までが説明書に載る。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "output");
            Directory.CreateDirectory(outDir);
            foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "assets")))
                File.Copy(file, Path.Combine(outDir, Path.GetFileName(file)), true);
            Environment.CurrentDirectory = outDir;

            Examples.LoadFonts();

            var methods = typeof(Examples).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name != nameof(Examples.LoadFonts) && m.GetParameters().Length == 0)
                .OrderBy(m => m.Name);
            string? only = args.Length > 1 ? args[1] : null;
            foreach (var m in methods)
            {
                if (only != null && m.Name != only) continue;
                Console.WriteLine(m.Name);
                m.Invoke(null, null);
            }
            Console.WriteLine("出力先: " + outDir);
            return 0;
        }
    }

    internal static partial class Examples
    {
        // サンプルの中では、ローカル変数のように font / mincho と書けるようにしている
        private static PdfFont font = null!;
        private static PdfFont mincho = null!;

        public static void LoadFonts()
        {
            // #example Fonts
            // 游ゴシック（Windows 標準）。太字は専用のフォントを対応付ける
            var font = PdfFont.FromSystem("Yu Gothic Regular");
            font.BoldVariant = PdfFont.FromSystem("Yu Gothic Bold");

            // 游明朝
            var mincho = PdfFont.FromSystem("Yu Mincho Regular");
            // #end
            Examples.font = font;
            Examples.mincho = mincho;
        }
    }
}
