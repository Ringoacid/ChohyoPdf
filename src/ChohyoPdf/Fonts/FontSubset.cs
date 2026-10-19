using System;
using System.Collections.Generic;

namespace ChohyoPdf.Fonts
{
    /// <summary>
    /// 1 つの PDF 文書内で使われたグリフを集め、サブセットの TrueType フォントを生成する。
    /// コンテンツストリームには初回使用順に振った新しいグリフ ID（= CID）を書き込む。
    /// </summary>
    internal sealed class FontSubset
    {
        private readonly TrueTypeFont _font;
        private readonly Dictionary<ushort, ushort> _oldToNew = new Dictionary<ushort, ushort>();
        private readonly List<ushort> _newToOld = new List<ushort>();
        private readonly Dictionary<ushort, string> _unicode = new Dictionary<ushort, string>();

        public FontSubset(TrueTypeFont font)
        {
            _font = font;
            _oldToNew[0] = 0;
            _newToOld.Add(0);
        }

        /// <summary>コンテンツに書き込んだグリフの数（.notdef を含む）。</summary>
        public int UsedCount => _newToOld.Count;

        public ushort Map(ushort oldGid, int codepoint)
        {
            if (!_oldToNew.TryGetValue(oldGid, out ushort newGid))
            {
                if (_newToOld.Count >= 0xFFFF) throw new InvalidOperationException("1 つのフォントで使用できるグリフ数の上限を超えました。");
                newGid = (ushort)_newToOld.Count;
                _newToOld.Add(oldGid);
                _oldToNew[oldGid] = newGid;
            }
            if (codepoint > 0 && newGid != 0 && !_unicode.ContainsKey(newGid))
                _unicode[newGid] = char.ConvertFromUtf32(codepoint);
            return newGid;
        }

        public IReadOnlyList<ushort> NewToOld => _newToOld;

        public IReadOnlyDictionary<ushort, string> UnicodeMap => _unicode;

        /// <summary>サブセットタグ（6 文字の英大文字）。使用グリフから決定的に計算する。</summary>
        public string ComputeTag()
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (ushort g in _newToOld)
                {
                    h = (h ^ g) * 16777619;
                    h = (h ^ (uint)(g >> 8)) * 16777619;
                }
                var chars = new char[6];
                for (int i = 0; i < 6; i++)
                {
                    chars[i] = (char)('A' + h % 26);
                    h /= 26;
                    if (h == 0) h = 0x9E3779B9u + (uint)i;
                }
                return new string(chars);
            }
        }

        /// <summary>サブセットフォント（単体 TrueType）のバイト列を生成する。</summary>
        public byte[] Build()
        {
            var r = _font.Reader;
            // 複合グリフの構成要素を追加（追加されたものも走査するため添字ループ）
            var glyphs = new List<byte[]>();
            var componentOffsets = new List<List<int>>();
            for (int i = 0; i < _newToOld.Count; i++)
            {
                ushort oldGid = _newToOld[i];
                _font.GetGlyphLocation(oldGid, out int offset, out int length);
                byte[] data = length > 0 ? r.Slice(offset, length) : Array.Empty<byte>();
                var comps = new List<int>();
                if (data.Length >= 10 && (short)((data[0] << 8) | data[1]) < 0)
                {
                    int p = 10;
                    while (p + 4 <= data.Length)
                    {
                        int flags = (data[p] << 8) | data[p + 1];
                        ushort component = (ushort)((data[p + 2] << 8) | data[p + 3]);
                        comps.Add(p + 2);
                        if (!_oldToNew.ContainsKey(component))
                        {
                            if (component >= _font.NumGlyphs) component = 0;
                            else
                            {
                                _oldToNew[component] = (ushort)_newToOld.Count;
                                _newToOld.Add(component);
                            }
                        }
                        p += 4;
                        p += (flags & 0x0001) != 0 ? 4 : 2;
                        if ((flags & 0x0008) != 0) p += 2;
                        else if ((flags & 0x0040) != 0) p += 4;
                        else if ((flags & 0x0080) != 0) p += 8;
                        if ((flags & 0x0020) == 0) break;
                    }
                }
                glyphs.Add(data);
                componentOffsets.Add(comps);
            }

            int count = _newToOld.Count;

            // glyf と loca（long 形式）
            var glyf = new FontWriter();
            var loca = new FontWriter();
            for (int i = 0; i < count; i++)
            {
                loca.U32((uint)glyf.Position);
                byte[] data = glyphs[i];
                if (componentOffsets[i].Count > 0)
                {
                    data = (byte[])data.Clone();
                    foreach (int pos in componentOffsets[i])
                    {
                        ushort oldComponent = (ushort)((data[pos] << 8) | data[pos + 1]);
                        ushort newComponent = _oldToNew.TryGetValue(oldComponent, out var mapped) ? mapped : (ushort)0;
                        data[pos] = (byte)(newComponent >> 8);
                        data[pos + 1] = (byte)newComponent;
                    }
                }
                glyf.Bytes(data);
                glyf.Pad4();
            }
            loca.U32((uint)glyf.Position);

            // hmtx
            var hmtx = new FontWriter();
            for (int i = 0; i < count; i++)
            {
                hmtx.U16(_font.GetAdvance(_newToOld[i]));
                hmtx.U16(_font.GetLeftSideBearing(_newToOld[i]));
            }

            byte[] head = _font.GetTableData("head");
            head[8] = head[9] = head[10] = head[11] = 0; // checkSumAdjustment
            head[50] = 0;
            head[51] = 1; // indexToLocFormat = long

            byte[] hhea = _font.GetTableData("hhea");
            hhea[34] = (byte)(count >> 8);
            hhea[35] = (byte)count;

            byte[] maxp = _font.GetTableData("maxp");
            maxp[4] = (byte)(count >> 8);
            maxp[5] = (byte)count;

            // post はグリフ名を持たない version 3.0 にする
            var post = new FontWriter();
            post.U32(0x00030000);
            if (_font.TryGetTable("post", out var postRec) && postRec.Length >= 32)
            {
                byte[] orig = r.Slice(postRec.Offset + 4, 28);
                post.Bytes(orig);
            }
            else
            {
                for (int i = 0; i < 28; i++) post.U8(0);
            }

            var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["cmap"] = BuildCmap(),
                ["glyf"] = glyf.ToArray(),
                ["head"] = head,
                ["hhea"] = hhea,
                ["hmtx"] = hmtx.ToArray(),
                ["loca"] = loca.ToArray(),
                ["maxp"] = maxp,
                ["post"] = post.ToArray(),
            };
            foreach (string tag in new[] { "cvt ", "fpgm", "prep", "OS/2" })
            {
                if (_font.HasTable(tag)) tables[tag] = _font.GetTableData(tag);
            }

            return Assemble(tables);
        }

        /// <summary>
        /// Unicode（BMP）から新しいグリフ ID への cmap（format 4）。PDF の表示には使われないが、
        /// cmap を必須とするツールやビューアーのために入れておく。
        /// </summary>
        private byte[] BuildCmap()
        {
            var map = new SortedDictionary<int, ushort>();
            foreach (var kv in _unicode)
            {
                int cp = char.ConvertToUtf32(kv.Value, 0);
                if (cp < 0xFFFF && !map.ContainsKey(cp)) map[cp] = kv.Key;
            }
            // 連続するコードポイントとグリフ ID をまとめてセグメントにする
            var starts = new List<int>();
            var ends = new List<int>();
            var deltas = new List<int>();
            foreach (var kv in map)
            {
                int n = starts.Count;
                if (n > 0 && ends[n - 1] == kv.Key - 1 && (kv.Value - kv.Key) == deltas[n - 1])
                {
                    ends[n - 1] = kv.Key;
                    continue;
                }
                starts.Add(kv.Key);
                ends.Add(kv.Key);
                deltas.Add(kv.Value - kv.Key);
            }
            starts.Add(0xFFFF);
            ends.Add(0xFFFF);
            deltas.Add(1);

            int segCount = starts.Count;
            int entrySelector = 0;
            while ((1 << (entrySelector + 1)) <= segCount) entrySelector++;
            int searchRange = 2 * (1 << entrySelector);

            var sub = new FontWriter();
            sub.U16(4);
            sub.U16(16 + segCount * 8);
            sub.U16(0);
            sub.U16(segCount * 2);
            sub.U16(searchRange);
            sub.U16(entrySelector);
            sub.U16(segCount * 2 - searchRange);
            foreach (int e in ends) sub.U16(e);
            sub.U16(0);
            foreach (int st in starts) sub.U16(st);
            foreach (int d in deltas) sub.U16(d & 0xFFFF);
            foreach (int _ in starts) sub.U16(0);

            var w = new FontWriter();
            w.U16(0);
            w.U16(1);
            w.U16(3);
            w.U16(1);
            w.U32(12);
            w.Bytes(sub.ToArray());
            return w.ToArray();
        }

        private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
        {
            int numTables = tables.Count;
            int entrySelector = 0;
            while ((1 << (entrySelector + 1)) <= numTables) entrySelector++;
            int searchRange = (1 << entrySelector) * 16;

            var w = new FontWriter();
            w.U32(0x00010000);
            w.U16(numTables);
            w.U16(searchRange);
            w.U16(entrySelector);
            w.U16(numTables * 16 - searchRange);

            int offset = 12 + numTables * 16;
            var offsets = new Dictionary<string, int>();
            foreach (var kv in tables)
            {
                offsets[kv.Key] = offset;
                offset += (kv.Value.Length + 3) & ~3;
            }
            foreach (var kv in tables)
            {
                w.Tag(kv.Key);
                w.U32(Checksum(kv.Value));
                w.U32((uint)offsets[kv.Key]);
                w.U32((uint)kv.Value.Length);
            }
            foreach (var kv in tables)
            {
                w.Bytes(kv.Value);
                w.Pad4();
            }
            byte[] result = w.ToArray();

            uint adjustment = unchecked(0xB1B0AFBA - Checksum(result));
            int headOffset = offsets["head"];
            result[headOffset + 8] = (byte)(adjustment >> 24);
            result[headOffset + 9] = (byte)(adjustment >> 16);
            result[headOffset + 10] = (byte)(adjustment >> 8);
            result[headOffset + 11] = (byte)adjustment;
            return result;
        }

        private static uint Checksum(byte[] data)
        {
            uint sum = 0;
            unchecked
            {
                for (int i = 0; i < data.Length; i += 4)
                {
                    uint v = 0;
                    for (int j = 0; j < 4; j++)
                    {
                        v <<= 8;
                        if (i + j < data.Length) v |= data[i + j];
                    }
                    sum += v;
                }
            }
            return sum;
        }
    }
}
