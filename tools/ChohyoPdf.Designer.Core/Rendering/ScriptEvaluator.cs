using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using ChohyoPdf.Designer.CodeGen;
using ChohyoPdf.Designer.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>
    /// 差し込み項目の C# の式を、デザイナーの中で計算する。
    /// 出力するコードと同じデータのクラスと計算のメソッドを作ってコンパイルし、確認用の値で実行する。
    /// 式は利用者が書いた C# のコードなので、信頼できるデザインのときだけ呼ぶこと。
    /// </summary>
    public static class ScriptEvaluator
    {
        private static readonly object Sync = new object();
        private static string? _code;
        private static Compiled? _compiled;

        /// <summary>1 つの式の実行にかけてよい時間。超えたら中止したことにする（実行は止められないので、結果を待たない）。</summary>
        public static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

        private static readonly Lazy<List<MetadataReference>> References = new Lazy<List<MetadataReference>>(() =>
        {
            var refs = new List<MetadataReference>();
            string tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
            foreach (string path in tpa.Split(Path.PathSeparator))
            {
                string file = Path.GetFileName(path);
                // .NET の標準のライブラリだけを参照する
                if (!(file.StartsWith("System.", StringComparison.OrdinalIgnoreCase) || file == "System.dll" || file == "netstandard.dll" || file == "mscorlib.dll" || file == "Microsoft.CSharp.dll")) continue;
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(path));
                }
                catch (IOException)
                {
                }
            }
            return refs;
        });

        private sealed class Compiled
        {
            public AssemblyLoadContext? Context;
            public MethodInfo? CreateSample;
            public Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();
            public List<string> Errors = new List<string>();
        }

        /// <summary>
        /// 計算する差し込み項目の値を求める（名前 → decimal / DateTime / string）。計算できなかった項目は含めない。
        /// コンパイルや実行のエラーは errors に追加する。
        /// </summary>
        public static Dictionary<string, object?> Evaluate(ReportDefinition report, DateTime now, ICollection<string>? errors)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (!report.Fields.Any(f => f.IsComputed)) return result;
            var (code, methods) = CSharpGenerator.GenerateScriptHost(report);
            Compiled compiled;
            lock (Sync)
            {
                if (_code != code || _compiled == null)
                {
                    // 前のアセンブリは捨てる
                    _compiled?.Context?.Unload();
                    _compiled = Compile(code, methods);
                    _code = code;
                }
                compiled = _compiled;
            }
            foreach (string e in compiled.Errors) Add(errors, e);
            if (compiled.CreateSample == null) return result;

            foreach (var pair in compiled.Methods)
            {
                string name = pair.Key;
                var method = pair.Value;
                try
                {
                    // 式ごとにデータを作り直す（式がデータを書き換えても、ほかの式に影響しないように）
                    object data = compiled.CreateSample.Invoke(null, null)!;
                    var task = Task.Run(() => method.Invoke(null, new[] { data, (object)now }));
                    if (!task.Wait(Timeout))
                    {
                        Add(errors, "差し込み項目「" + name + "」の C# の式: 時間がかかりすぎるため、計算を待つのをやめました（くり返しが終わらない可能性があります）。");
                        continue;
                    }
                    result[name] = task.Result;
                }
                catch (AggregateException ex) when (ex.InnerException is TargetInvocationException tie && tie.InnerException != null)
                {
                    Add(errors, "差し込み項目「" + name + "」の C# の式でエラーが起きました: " + tie.InnerException.GetType().Name + ": " + tie.InnerException.Message);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    Add(errors, "差し込み項目「" + name + "」の C# の式でエラーが起きました: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                }
            }
            return result;
        }

        private static Compiled Compile(string code, IReadOnlyDictionary<string, string> methods)
        {
            var compiled = new Compiled();
            // 出力するコードは C# 7.3 でもコンパイルできるようにしているので、式も C# 7.3 で確かめる
            var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp7_3));
            var compilation = CSharpCompilation.Create("ChohyoPdfDesignerScript" + Guid.NewGuid().ToString("N"), new[] { tree }, References.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var ms = new MemoryStream();
            var emit = compilation.Emit(ms);
            if (!emit.Success)
            {
                foreach (var d in emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(10))
                {
                    var span = d.Location.GetMappedLineSpan();
                    // 式は #line で「項目の名前」の 1 行目からとして書いている
                    compiled.Errors.Add(span.HasMappedPath
                        ? "差し込み項目「" + span.Path + "」の C# の式（" + (span.StartLinePosition.Line + 1) + " 行目）: " + d.GetMessage()
                        : "C# の式をコンパイルできません: " + d.GetMessage());
                }
                return compiled;
            }
            ms.Position = 0;
            compiled.Context = new AssemblyLoadContext("ChohyoPdfDesignerScript", isCollectible: true);
            var assembly = compiled.Context.LoadFromStream(ms);
            var host = assembly.GetType("ScriptHost");
            compiled.CreateSample = assembly.GetTypes().Select(t => t.GetMethod("CreateSample", BindingFlags.Public | BindingFlags.Static)).FirstOrDefault(m => m != null);
            foreach (var pair in methods)
            {
                var method = host?.GetMethod(pair.Value.TrimStart('@'), BindingFlags.Public | BindingFlags.Static);
                if (method != null) compiled.Methods[pair.Key] = method;
            }
            return compiled;
        }

        private static void Add(ICollection<string>? errors, string message)
        {
            if (errors != null && !errors.Contains(message)) errors.Add(message);
        }
    }
}
