using System;
using System.Collections.Generic;
using System.IO;

namespace ChohyoPdf.Fonts
{
    internal struct TableRecord
    {
        public int Offset;
        public int Length;
    }

    /// <summary>
    /// TrueType（glyf アウトライン）フォントのパーサー。TTC（フォントコレクション）にも対応する。
    /// CFF アウトラインの OpenType フォントには対応しない。
    /// </summary>
    internal sealed class TrueTypeFont
    {
        private readonly FontReader _r;
        private readonly Dictionary<string, TableRecord> _tables = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
        private readonly Dictionary<int, ushort> _cmap = new Dictionary<int, ushort>();
        private ushort[] _advances = Array.Empty<ushort>();
        private short[] _lsbs = Array.Empty<short>();
        private bool _longLoca;

        public string FamilyName { get; private set; } = "";
        public string SubfamilyName { get; private set; } = "";
        public string FullName { get; private set; } = "";
        public string PostScriptName { get; private set; } = "";
        public List<string> AllNames { get; } = new List<string>();

        public int UnitsPerEm { get; private set; }
        public int NumGlyphs { get; private set; }
        public short XMin { get; private set; }
        public short YMin { get; private set; }
        public short XMax { get; private set; }
        public short YMax { get; private set; }
        public int MacStyle { get; private set; }

        public short HheaAscender { get; private set; }
        public short HheaDescender { get; private set; }
        public short HheaLineGap { get; private set; }

        public bool HasOs2 { get; private set; }
        public int WeightClass { get; private set; } = 400;
        public int FsType { get; private set; }
        public int FsSelection { get; private set; }
        public short TypoAscender { get; private set; }
        public short TypoDescender { get; private set; }
        public short TypoLineGap { get; private set; }
        public int WinAscent { get; private set; }
        public int WinDescent { get; private set; }
        public short CapHeight { get; private set; }
        public int FamilyClass { get; private set; }

        public double ItalicAngle { get; private set; }
        public short UnderlinePosition { get; private set; }
        public short UnderlineThickness { get; private set; }
        public bool IsFixedPitch { get; private set; }

        private TrueTypeFont(byte[] data)
        {
            _r = new FontReader(data);
        }

        public FontReader Reader => _r;

        /// <summary>TTC 内のフォント数を返す（単体フォントなら 1）。</summary>
        public static int GetFaceCount(byte[] data)
        {
            var r = new FontReader(data);
            if (r.Length < 12) throw new InvalidDataException("フォントファイルが短すぎます。");
            return r.Tag(0) == "ttcf" ? (int)r.U32(8) : 1;
        }

        public static TrueTypeFont Load(byte[] data, int faceIndex)
        {
            var font = new TrueTypeFont(data);
            font.Parse(faceIndex);
            return font;
        }

        private void Parse(int faceIndex)
        {
            int offsetTable = 0;
            string tag = _r.Tag(0);
            if (tag == "ttcf")
            {
                int count = (int)_r.U32(8);
                if (faceIndex < 0 || faceIndex >= count)
                    throw new ArgumentOutOfRangeException(nameof(faceIndex), "フォントコレクションに索引 " + faceIndex + " のフォントはありません（" + count + " 個）。");
                offsetTable = (int)_r.U32(12 + faceIndex * 4);
                tag = _r.Tag(offsetTable);
            }
            else if (faceIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(faceIndex), "単体フォントの索引は 0 のみです。");
            }

            if (tag == "OTTO")
                throw new NotSupportedException("CFF アウトラインの OpenType フォント（.otf）には対応していません。TrueType アウトラインのフォント（.ttf / .ttc）を使用してください。");
            if (tag != "\0\u0001\0\0" && tag != "true")
                throw new InvalidDataException("TrueType フォントではありません。");

            int numTables = _r.U16(offsetTable + 4);
            for (int i = 0; i < numTables; i++)
            {
                int rec = offsetTable + 12 + i * 16;
                string t = _r.Tag(rec);
                _tables[t] = new TableRecord { Offset = (int)_r.U32(rec + 8), Length = (int)_r.U32(rec + 12) };
            }

            if (!_tables.ContainsKey("glyf") || !_tables.ContainsKey("loca"))
                throw new NotSupportedException("glyf / loca テーブルを持たないフォントには対応していません。");

            ParseHead();
            ParseMaxp();
            ParseHhea();
            ParseHmtx();
            ParseOs2();
            ParsePost();
            ParseName();
            ParseCmap();
        }

        public bool HasTable(string tag) => _tables.ContainsKey(tag);

        public bool TryGetTable(string tag, out TableRecord record) => _tables.TryGetValue(tag, out record);

        public byte[] GetTableData(string tag)
        {
            var rec = Require(tag);
            return _r.Slice(rec.Offset, rec.Length);
        }

        private TableRecord Require(string tag)
        {
            if (!_tables.TryGetValue(tag, out var rec))
                throw new InvalidDataException("必須テーブル '" + tag + "' がありません。");
            return rec;
        }

        private void ParseHead()
        {
            int o = Require("head").Offset;
            UnitsPerEm = _r.U16(o + 18);
            if (UnitsPerEm == 0) UnitsPerEm = 1000;
            XMin = _r.S16(o + 36);
            YMin = _r.S16(o + 38);
            XMax = _r.S16(o + 40);
            YMax = _r.S16(o + 42);
            MacStyle = _r.U16(o + 44);
            _longLoca = _r.S16(o + 50) != 0;
        }

        private void ParseMaxp()
        {
            NumGlyphs = _r.U16(Require("maxp").Offset + 4);
        }

        private void ParseHhea()
        {
            int o = Require("hhea").Offset;
            HheaAscender = _r.S16(o + 4);
            HheaDescender = _r.S16(o + 6);
            HheaLineGap = _r.S16(o + 8);
        }

        private void ParseHmtx()
        {
            int numberOfHMetrics = _r.U16(Require("hhea").Offset + 34);
            int o = Require("hmtx").Offset;
            if (numberOfHMetrics == 0 || numberOfHMetrics > NumGlyphs) numberOfHMetrics = Math.Max(1, Math.Min(NumGlyphs, numberOfHMetrics));
            _advances = new ushort[NumGlyphs];
            _lsbs = new short[NumGlyphs];
            ushort last = 0;
            for (int i = 0; i < NumGlyphs; i++)
            {
                if (i < numberOfHMetrics)
                {
                    last = _r.U16(o + i * 4);
                    _advances[i] = last;
                    _lsbs[i] = _r.S16(o + i * 4 + 2);
                }
                else
                {
                    _advances[i] = last;
                    int lsbOffset = o + numberOfHMetrics * 4 + (i - numberOfHMetrics) * 2;
                    _lsbs[i] = lsbOffset + 2 <= _r.Length ? _r.S16(lsbOffset) : (short)0;
                }
            }
        }

        private void ParseOs2()
        {
            if (!_tables.TryGetValue("OS/2", out var rec) || rec.Length < 78) return;
            int o = rec.Offset;
            HasOs2 = true;
            int version = _r.U16(o);
            WeightClass = _r.U16(o + 4);
            FsType = _r.U16(o + 8);
            FamilyClass = _r.S16(o + 30);
            FsSelection = _r.U16(o + 62);
            TypoAscender = _r.S16(o + 68);
            TypoDescender = _r.S16(o + 70);
            TypoLineGap = _r.S16(o + 72);
            WinAscent = _r.U16(o + 74);
            WinDescent = _r.U16(o + 76);
            if (version >= 2 && rec.Length >= 90) CapHeight = _r.S16(o + 88);
        }

        private void ParsePost()
        {
            if (!_tables.TryGetValue("post", out var rec) || rec.Length < 16) return;
            int o = rec.Offset;
            ItalicAngle = _r.S16(o + 4) + _r.U16(o + 6) / 65536.0;
            UnderlinePosition = _r.S16(o + 8);
            UnderlineThickness = _r.S16(o + 10);
            IsFixedPitch = _r.U32(o + 12) != 0;
        }

        private void ParseName()
        {
            if (!_tables.TryGetValue("name", out var rec)) return;
            var names = NameTable.Parse(_r, rec.Offset);
            FamilyName = names.Family;
            SubfamilyName = names.Subfamily;
            FullName = names.Full;
            PostScriptName = names.PostScript;
            AllNames.AddRange(names.All);
        }

        private void ParseCmap()
        {
            int o = Require("cmap").Offset;
            int count = _r.U16(o + 2);
            int fmt4 = -1, fmt12 = -1, symbol = -1;
            int fmt4Priority = -1;
            for (int i = 0; i < count; i++)
            {
                int rr = o + 4 + i * 8;
                int platform = _r.U16(rr);
                int encoding = _r.U16(rr + 2);
                int sub = o + (int)_r.U32(rr + 4);
                int format = _r.U16(sub);
                if (format == 12 && ((platform == 3 && encoding == 10) || platform == 0))
                {
                    if (fmt12 < 0 || platform == 3) fmt12 = sub;
                }
                else if (format == 4)
                {
                    int priority = platform == 3 && encoding == 1 ? 3 : platform == 0 ? 2 : platform == 3 && encoding == 0 ? 0 : -1;
                    if (platform == 3 && encoding == 0) symbol = sub;
                    else if (priority > fmt4Priority)
                    {
                        fmt4 = sub;
                        fmt4Priority = priority;
                    }
                }
            }

            if (fmt4 >= 0) ReadFormat4(fmt4);
            if (fmt12 >= 0) ReadFormat12(fmt12);
            if (fmt4 < 0 && fmt12 < 0 && symbol >= 0)
            {
                // シンボルフォント: U+F020..F0FF を U+0020..00FF にも割り当てる
                ReadFormat4(symbol);
                var extra = new List<KeyValuePair<int, ushort>>();
                foreach (var kv in _cmap)
                {
                    if (kv.Key >= 0xF000 && kv.Key <= 0xF0FF) extra.Add(new KeyValuePair<int, ushort>(kv.Key - 0xF000, kv.Value));
                }
                foreach (var kv in extra)
                {
                    if (!_cmap.ContainsKey(kv.Key)) _cmap[kv.Key] = kv.Value;
                }
            }
        }

        private void ReadFormat4(int sub)
        {
            int segCount = _r.U16(sub + 6) / 2;
            int endCodes = sub + 14;
            int startCodes = endCodes + segCount * 2 + 2;
            int idDeltas = startCodes + segCount * 2;
            int idRangeOffsets = idDeltas + segCount * 2;
            for (int s = 0; s < segCount; s++)
            {
                int end = _r.U16(endCodes + s * 2);
                int start = _r.U16(startCodes + s * 2);
                int delta = _r.S16(idDeltas + s * 2);
                int rangeOffsetPos = idRangeOffsets + s * 2;
                int rangeOffset = _r.U16(rangeOffsetPos);
                if (start > end) continue;
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int gid;
                    if (rangeOffset == 0)
                    {
                        gid = (c + delta) & 0xFFFF;
                    }
                    else
                    {
                        int glyphPos = rangeOffsetPos + rangeOffset + (c - start) * 2;
                        if (glyphPos + 2 > _r.Length) continue;
                        gid = _r.U16(glyphPos);
                        if (gid != 0) gid = (gid + delta) & 0xFFFF;
                    }
                    if (gid != 0 && gid < NumGlyphs) _cmap[c] = (ushort)gid;
                }
            }
        }

        private void ReadFormat12(int sub)
        {
            int groups = (int)_r.U32(sub + 12);
            for (int g = 0; g < groups; g++)
            {
                int rr = sub + 16 + g * 12;
                uint start = _r.U32(rr);
                uint end = _r.U32(rr + 4);
                uint glyph = _r.U32(rr + 8);
                if (end > 0x10FFFF || start > end) continue;
                for (uint c = start; c <= end; c++)
                {
                    uint gid = glyph + (c - start);
                    if (gid != 0 && gid < NumGlyphs) _cmap[(int)c] = (ushort)gid;
                }
            }
        }

        /// <summary>コードポイントからグリフ ID を得る。存在しなければ 0。</summary>
        public ushort GetGlyphId(int codepoint) => _cmap.TryGetValue(codepoint, out var gid) ? gid : (ushort)0;

        public int GetAdvance(ushort gid) => gid < _advances.Length ? _advances[gid] : (_advances.Length > 0 ? _advances[_advances.Length - 1] : 0);

        public short GetLeftSideBearing(ushort gid) => gid < _lsbs.Length ? _lsbs[gid] : (short)0;

        /// <summary>glyf テーブル内のグリフデータの位置（絶対オフセット）と長さ。</summary>
        public void GetGlyphLocation(ushort gid, out int offset, out int length)
        {
            var loca = Require("loca");
            var glyf = Require("glyf");
            int start, end;
            if (_longLoca)
            {
                start = (int)_r.U32(loca.Offset + gid * 4);
                end = (int)_r.U32(loca.Offset + gid * 4 + 4);
            }
            else
            {
                start = _r.U16(loca.Offset + gid * 2) * 2;
                end = _r.U16(loca.Offset + gid * 2 + 2) * 2;
            }
            if (end < start || end > glyf.Length)
            {
                offset = glyf.Offset;
                length = 0;
                return;
            }
            offset = glyf.Offset + start;
            length = end - start;
        }
    }
}
