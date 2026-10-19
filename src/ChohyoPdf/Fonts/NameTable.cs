using System.Collections.Generic;
using System.Text;

namespace ChohyoPdf.Fonts
{
    /// <summary>name テーブルから得たフォント名。</summary>
    internal sealed class FontNames
    {
        public string Family = "";
        public string Subfamily = "";
        public string Full = "";
        public string PostScript = "";
        public readonly List<string> All = new List<string>();
    }

    internal static class NameTable
    {
        /// <summary><paramref name="offset"/> から始まる name テーブルを読む。</summary>
        public static FontNames Parse(FontReader r, int offset)
        {
            var names = new FontNames();
            int count = r.U16(offset + 2);
            int stringOffset = offset + r.U16(offset + 4);
            // 優先度: Windows 英語 > Windows / Unicode その他の言語 > Mac Roman
            var best = new Dictionary<int, KeyValuePair<int, string>>();
            for (int i = 0; i < count; i++)
            {
                int rr = offset + 6 + i * 12;
                if (rr + 12 > r.Length) break;
                int platform = r.U16(rr);
                int encoding = r.U16(rr + 2);
                int language = r.U16(rr + 4);
                int nameId = r.U16(rr + 6);
                int length = r.U16(rr + 8);
                int strOffset = r.U16(rr + 10);
                if (nameId != 1 && nameId != 2 && nameId != 4 && nameId != 6 && nameId != 16 && nameId != 17) continue;
                if (stringOffset + strOffset + length > r.Length) continue;
                byte[] raw = r.Slice(stringOffset + strOffset, length);
                string value;
                int priority;
                if (platform == 3 || platform == 0)
                {
                    value = Encoding.BigEndianUnicode.GetString(raw);
                    priority = platform == 3 && language == 0x409 ? 3 : 2;
                }
                else if (platform == 1 && encoding == 0)
                {
                    var sb = new StringBuilder(raw.Length);
                    foreach (byte b in raw) sb.Append(b < 0x80 ? (char)b : '?');
                    value = sb.ToString();
                    priority = 1;
                }
                else
                {
                    continue;
                }
                value = value.Trim('\0', ' ');
                if (value.Length == 0) continue;
                if ((nameId == 1 || nameId == 4 || nameId == 6 || nameId == 16) && !names.All.Contains(value)) names.All.Add(value);
                if (!best.TryGetValue(nameId, out var current) || current.Key < priority)
                    best[nameId] = new KeyValuePair<int, string>(priority, value);
            }
            string Get(int id) => best.TryGetValue(id, out var v) ? v.Value : "";
            names.Family = Get(16).Length > 0 ? Get(16) : Get(1);
            names.Subfamily = Get(17).Length > 0 ? Get(17) : Get(2);
            names.Full = Get(4);
            names.PostScript = Get(6);
            if (names.Full.Length == 0) names.Full = (names.Family + " " + names.Subfamily).Trim();
            if (names.PostScript.Length == 0) names.PostScript = names.Full.Replace(" ", "");
            return names;
        }
    }
}
