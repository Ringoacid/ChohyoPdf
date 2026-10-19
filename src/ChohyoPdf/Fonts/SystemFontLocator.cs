using System;
using System.Collections.Generic;
using System.IO;

namespace ChohyoPdf.Fonts
{
    /// <summary>OS のフォントフォルダを走査し、名前からフォントファイルを探す。</summary>
    internal static class SystemFontLocator
    {
        private sealed class FaceEntry
        {
            public string Path = "";
            public int Index;
            public bool Variable;
            public FontNames Names = new FontNames();
        }

        private static readonly object Sync = new object();
        private static List<FaceEntry>? _faces;

        public static bool TryFind(string name, out string path, out int index)
        {
            path = "";
            index = 0;
            FaceEntry? best = null;
            int bestScore = 0;
            foreach (var face in GetFaces())
            {
                int score = Score(face.Names, name) * 2;
                // 可変フォントは既定のインスタンスでしか描画できないため、同名の静的フォントを優先する
                if (score > 0 && !face.Variable) score++;
                if (score > bestScore)
                {
                    best = face;
                    bestScore = score;
                }
            }
            if (best == null) return false;
            path = best.Path;
            index = best.Index;
            return true;
        }

        private static int Score(FontNames names, string name)
        {
            if (Eq(names.Full, name) || Eq(names.PostScript, name)) return 4;
            bool regular = Eq(names.Subfamily, "Regular") || Eq(names.Subfamily, "Normal") || Eq(names.Subfamily, "標準");
            if (Eq(names.Family, name)) return regular ? 3 : 1;
            foreach (string n in names.All)
            {
                if (Eq(n, name)) return regular ? 2 : 1;
            }
            return 0;
        }

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static List<FaceEntry> GetFaces()
        {
            lock (Sync)
            {
                if (_faces != null) return _faces;
                var faces = new List<FaceEntry>();
                foreach (string dir in GetFontDirectories())
                {
                    if (!Directory.Exists(dir)) continue;
                    IEnumerable<string> files;
                    try
                    {
                        files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    foreach (string file in files)
                    {
                        string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".ttf" && ext != ".ttc" && ext != ".otf") continue;
                        try
                        {
                            ReadFaces(file, faces);
                        }
                        catch (Exception)
                        {
                            // 壊れたフォントや読めないファイルは無視する
                        }
                    }
                }
                _faces = faces;
                return faces;
            }
        }

        private static IEnumerable<string> GetFontDirectories()
        {
            string windir = Environment.GetEnvironmentVariable("WINDIR") ?? "";
            if (windir.Length > 0) yield return System.IO.Path.Combine(windir, "Fonts");
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (local.Length > 0) yield return System.IO.Path.Combine(local, "Microsoft", "Windows", "Fonts");
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            if (home.Length > 0)
            {
                yield return System.IO.Path.Combine(home, ".fonts");
                yield return System.IO.Path.Combine(home, ".local", "share", "fonts");
                yield return System.IO.Path.Combine(home, "Library", "Fonts");
            }
        }

        /// <summary>ファイル全体を読まずに、オフセットテーブルと name テーブルだけを読む。</summary>
        private static void ReadFaces(string file, List<FaceEntry> faces)
        {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var header = new FontReader(ReadAt(fs, 0, 16));
                var offsets = new List<int>();
                if (header.Tag(0) == "ttcf")
                {
                    int count = (int)header.U32(8);
                    if (count <= 0 || count > 1000) return;
                    var list = new FontReader(ReadAt(fs, 12, count * 4));
                    for (int i = 0; i < count; i++) offsets.Add((int)list.U32(i * 4));
                }
                else
                {
                    offsets.Add(0);
                }

                for (int i = 0; i < offsets.Count; i++)
                {
                    var table = new FontReader(ReadAt(fs, offsets[i], 12));
                    string version = table.Tag(0);
                    if (version != "\0\u0001\0\0" && version != "true") continue; // CFF などは対象外
                    int numTables = table.U16(4);
                    var records = new FontReader(ReadAt(fs, offsets[i] + 12, numTables * 16));
                    int nameOffset = -1, nameLength = 0;
                    bool hasGlyf = false, variable = false;
                    for (int t = 0; t < numTables; t++)
                    {
                        string tag = records.Tag(t * 16);
                        if (tag == "name")
                        {
                            nameOffset = (int)records.U32(t * 16 + 8);
                            nameLength = (int)records.U32(t * 16 + 12);
                        }
                        else if (tag == "glyf")
                        {
                            hasGlyf = true;
                        }
                        else if (tag == "fvar")
                        {
                            variable = true;
                        }
                    }
                    if (nameOffset < 0 || !hasGlyf || nameLength <= 0 || nameLength > 4 * 1024 * 1024) continue;
                    var names = NameTable.Parse(new FontReader(ReadAt(fs, nameOffset, nameLength)), 0);
                    faces.Add(new FaceEntry { Path = file, Index = i, Variable = variable, Names = names });
                }
            }
        }

        private static byte[] ReadAt(FileStream fs, long offset, int length)
        {
            if (offset < 0 || offset + length > fs.Length) throw new InvalidDataException();
            fs.Position = offset;
            var buffer = new byte[length];
            int read = 0;
            while (read < length)
            {
                int n = fs.Read(buffer, read, length - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
            return buffer;
        }
    }
}
