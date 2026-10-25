using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ChohyoPdf.Tests
{
    internal static class TestFonts
    {
        private static PdfFont? _gothic;
        private static bool _searched;

        private static readonly object _lock = new object();

        /// <summary>日本語フォント（見つからない環境では null）。</summary>
        public static PdfFont? Japanese
        {
            get
            {
                // テストは並列に実行されるので、検索が終わるまで他のスレッドを待たせる
                lock (_lock)
                {
                    if (_searched) return _gothic;
                    string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
                    foreach (string name in new[] { "YuGothR.ttc", "msgothic.ttc", "meiryo.ttc" })
                    {
                        string path = Path.Combine(windir, "Fonts", name);
                        if (File.Exists(path))
                        {
                            _gothic = PdfFont.FromFile(path, 0);
                            _searched = true;
                            return _gothic;
                        }
                    }
                    PdfFont.TryFromSystem(out _gothic, "Noto Sans JP", "IPAexGothic", "Noto Sans CJK JP");
                    _searched = true;
                    return _gothic;
                }
            }
        }

        public static string? WindowsFont(string file)
        {
            string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
            string path = Path.Combine(windir, "Fonts", file);
            return File.Exists(path) ? path : null;
        }
    }

    internal static class TestData
    {
        public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", name);

        public static byte[] Bytes(string name) => File.ReadAllBytes(Path(name));
    }

    /// <summary>生成した PDF のファイル構造（相互参照表・オブジェクト位置）を検査する。</summary>
    internal static class PdfChecker
    {
        public static string Latin1(byte[] data)
        {
            var sb = new StringBuilder(data.Length);
            foreach (byte b in data) sb.Append((char)b);
            return sb.ToString();
        }

        public static void AssertValidStructure(byte[] pdf)
        {
            string s = Latin1(pdf);
            Assert.StartsWith("%PDF-1.7", s);
            Assert.EndsWith("%%EOF\n", s);
            var m = Regex.Match(s, @"startxref\n(\d+)\n%%EOF\n$");
            Assert.True(m.Success, "startxref がありません");
            int xref = int.Parse(m.Groups[1].Value);
            Assert.Equal("xref", s.Substring(xref, 4));
            var header = Regex.Match(s.Substring(xref), @"^xref\n0 (\d+)\n");
            Assert.True(header.Success);
            int count = int.Parse(header.Groups[1].Value);
            int pos = xref + header.Length;
            for (int i = 0; i < count; i++)
            {
                string entry = s.Substring(pos + i * 20, 20);
                Assert.Matches(@"^\d{10} \d{5} [fn]\r\n$", entry);
                if (i == 0) continue;
                int offset = int.Parse(entry.Substring(0, 10));
                string expected = i + " 0 obj";
                Assert.Equal(expected, s.Substring(offset, expected.Length));
            }
            var trailer = Regex.Match(s, @"trailer\n<< /Size (\d+) /Root (\d+) 0 R");
            Assert.True(trailer.Success);
            Assert.Equal(count, int.Parse(trailer.Groups[1].Value));

            // ストリームの /Length が実際の長さと一致すること
            foreach (Match sm in Regex.Matches(s, @"/Length (\d+) >>\nstream\n"))
            {
                int len = int.Parse(sm.Groups[1].Value);
                int start = sm.Index + sm.Length;
                Assert.Equal("\nendstream", s.Substring(start + len, 10));
            }
        }

        public static int CountPages(byte[] pdf)
        {
            var m = Regex.Match(Latin1(pdf), @"/Type /Pages /Kids \[[^\]]*\] /Count (\d+)");
            Assert.True(m.Success);
            return int.Parse(m.Groups[1].Value);
        }

        /// <summary>全ストリームを展開した内容（Flate のもの）を返す。</summary>
        public static List<string> DecodedStreams(byte[] pdf)
        {
            string s = Latin1(pdf);
            var result = new List<string>();
            foreach (Match sm in Regex.Matches(s, @"<< ([^\n]*?)/Length (\d+) >>\nstream\n"))
            {
                int len = int.Parse(sm.Groups[2].Value);
                int start = sm.Index + sm.Length;
                var data = new byte[len];
                Array.Copy(pdf, start, data, 0, len);
                if (sm.Groups[1].Value.Contains("/FlateDecode") && !sm.Groups[1].Value.Contains("/Subtype /Image"))
                    result.Add(Latin1(Internal.Zlib.Decompress(data)));
            }
            return result;
        }
    }
}
