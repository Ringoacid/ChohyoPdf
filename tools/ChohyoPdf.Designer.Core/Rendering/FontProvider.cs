using System;
using System.Collections.Generic;
using System.IO;

namespace ChohyoPdf.Designer.Rendering
{
    /// <summary>OS のフォントを読み込んでキャッシュする。</summary>
    public static class FontProvider
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, PdfFont> Cache = new Dictionary<string, PdfFont>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> Errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// フォントを読み込む。太字のフォントを指定すると <see cref="PdfFont.BoldVariant"/> に設定する。
        /// 読み込めなければ例外を投げる。
        /// </summary>
        public static PdfFont Get(string name, string? boldName)
        {
            string key = name + "\n" + (boldName ?? "");
            lock (Sync)
            {
                if (Cache.TryGetValue(key, out var cached)) return cached;
                var font = Load(name);
                if (!string.IsNullOrWhiteSpace(boldName)) font.BoldVariant = Load(boldName!);
                Cache[key] = font;
                return font;
            }
        }

        /// <summary>フォントを読み込めるか。読み込めなければ理由を返す（結果だけを覚え、フォントは保持しない）。</summary>
        public static bool TryCheck(string name, out string? error)
        {
            lock (Sync)
            {
                error = null;
                if (Available.Contains(name) || Cache.ContainsKey(name + "\n")) return true;
                if (Errors.TryGetValue(name, out error)) return false;
            }
            try
            {
                Load(name);
                lock (Sync) Available.Add(name);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is InvalidDataException || ex is ArgumentException)
            {
                error = ex.Message;
                lock (Sync) Errors[name] = error;
                return false;
            }
        }

        private static PdfFont Load(string name)
        {
            try
            {
                return PdfFont.FromSystem(name);
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException("フォント「" + name + "」が見つかりません。");
            }
        }
    }

    /// <summary>フォントの選択肢。</summary>
    public static class FontCatalog
    {
        /// <summary>よく使うフォントと、その太字のフォント。</summary>
        public static IReadOnlyList<(string Name, string? Bold)> Candidates { get; } = new (string, string?)[]
        {
            ("Yu Gothic Regular", "Yu Gothic Bold"),
            ("Yu Gothic Medium", "Yu Gothic Bold"),
            ("Yu Gothic Light", "Yu Gothic Bold"),
            ("Yu Mincho Regular", "Yu Mincho Demibold"),
            ("Yu Mincho Light", "Yu Mincho Demibold"),
            ("Meiryo", "Meiryo Bold"),
            ("Meiryo UI", "Meiryo UI Bold"),
            ("MS Gothic", null),
            ("MS PGothic", null),
            ("MS UI Gothic", null),
            ("MS Mincho", null),
            ("MS PMincho", null),
            ("BIZ UDGothic", "BIZ UDGothic Bold"),
            ("BIZ UDPGothic", "BIZ UDPGothic Bold"),
            ("BIZ UDMincho Medium", null),
            ("BIZ UDPMincho Medium", null),
            ("UD Digi Kyokasho N-R", "UD Digi Kyokasho N-B"),
            ("Noto Sans JP", "Noto Sans JP Bold"),
            ("Noto Serif JP", "Noto Serif JP Bold"),
            ("IPAexGothic", null),
            ("IPAexMincho", null),
            ("Arial", "Arial Bold"),
            ("Times New Roman", "Times New Roman Bold"),
            ("Segoe UI", "Segoe UI Bold"),
            ("Consolas", "Consolas Bold"),
        };

        /// <summary>候補の中で太字の対応が分かっていれば、その名前を返す。</summary>
        public static string? SuggestBold(string name)
        {
            foreach (var c in Candidates)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c.Bold;
            }
            return null;
        }
    }
}
