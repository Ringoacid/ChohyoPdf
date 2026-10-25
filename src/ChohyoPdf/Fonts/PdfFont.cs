using System;
using System.Collections.Generic;
using System.IO;
using ChohyoPdf.Fonts;

namespace ChohyoPdf
{
    /// <summary>
    /// PDF に埋め込む TrueType フォント（.ttf / .ttc）。使用した文字だけをサブセット化して埋め込む。
    /// 1 つのインスタンスを複数の文書で共有できる。
    /// </summary>
    public sealed class PdfFont
    {
        internal TrueTypeFont Ttf { get; }

        private PdfFont(TrueTypeFont ttf)
        {
            Ttf = ttf;
        }

        /// <summary>フォントファイルを読み込む。TTC の場合は <paramref name="faceIndex"/> でフォントを選ぶ。</summary>
        public static PdfFont FromFile(string path, int faceIndex = 0, bool ignoreEmbeddingRestrictions = false)
            => FromBytes(File.ReadAllBytes(path), faceIndex, ignoreEmbeddingRestrictions);

        /// <summary>フォントファイルを読み込み、名前（ファミリー名・フルネーム・PostScript 名のいずれか）で TTC 内のフォントを選ぶ。</summary>
        public static PdfFont FromFile(string path, string faceName, bool ignoreEmbeddingRestrictions = false)
            => FromBytes(File.ReadAllBytes(path), faceName, ignoreEmbeddingRestrictions);

        /// <summary>ストリームからフォントを読み込む。TTC の場合は <paramref name="faceIndex"/> でフォントを選ぶ。</summary>
        public static PdfFont FromStream(Stream stream, int faceIndex = 0, bool ignoreEmbeddingRestrictions = false)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return FromBytes(ms.ToArray(), faceIndex, ignoreEmbeddingRestrictions);
            }
        }

        /// <summary>フォントファイルの内容（バイト列）から読み込む。TTC の場合は <paramref name="faceIndex"/> でフォントを選ぶ。</summary>
        public static PdfFont FromBytes(byte[] data, int faceIndex = 0, bool ignoreEmbeddingRestrictions = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var ttf = TrueTypeFont.Load(data, faceIndex);
            CheckEmbedding(ttf, ignoreEmbeddingRestrictions);
            return new PdfFont(ttf);
        }

        /// <summary>フォントファイルの内容（バイト列）から読み込み、名前（ファミリー名・フルネーム・PostScript 名のいずれか）で TTC 内のフォントを選ぶ。</summary>
        public static PdfFont FromBytes(byte[] data, string faceName, bool ignoreEmbeddingRestrictions = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (faceName == null) throw new ArgumentNullException(nameof(faceName));
            int count = TrueTypeFont.GetFaceCount(data);
            var available = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var ttf = TrueTypeFont.Load(data, i);
                if (Matches(ttf, faceName))
                {
                    CheckEmbedding(ttf, ignoreEmbeddingRestrictions);
                    return new PdfFont(ttf);
                }
                available.Add(ttf.FullName);
            }
            throw new ArgumentException("フォント '" + faceName + "' が見つかりません。含まれるフォント: " + string.Join(", ", available.ToArray()), nameof(faceName));
        }

        /// <summary>フォントファイル（TTC を含む）に含まれるフォントのフルネーム一覧。</summary>
        public static IReadOnlyList<string> GetFaceNames(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int count = TrueTypeFont.GetFaceCount(data);
            var names = new List<string>();
            for (int i = 0; i < count; i++) names.Add(TrueTypeFont.Load(data, i).FullName);
            return names;
        }

        /// <summary>
        /// OS にインストールされたフォントを名前で探して読み込む（Windows のフォントフォルダ、
        /// macOS / Linux の一般的なフォントフォルダを検索）。日本語名（例: "游ゴシック"）でも指定できる。
        /// </summary>
        public static PdfFont FromSystem(string name, bool ignoreEmbeddingRestrictions = false)
        {
            if (!SystemFontLocator.TryFind(name, out string path, out int index))
                throw new FileNotFoundException("システムフォント '" + name + "' が見つかりません。");
            return FromFile(path, index, ignoreEmbeddingRestrictions);
        }

        /// <summary>
        /// OS にインストールされたフォントを名前で探す。候補を順に試し、最初に見つかったものを返す。
        /// </summary>
        public static bool TryFromSystem(out PdfFont? font, params string[] names)
        {
            foreach (string name in names)
            {
                if (SystemFontLocator.TryFind(name, out string path, out int index))
                {
                    font = FromFile(path, index);
                    return true;
                }
            }
            font = null;
            return false;
        }

        private static bool Matches(TrueTypeFont ttf, string name)
        {
            if (string.Equals(ttf.FullName, name, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ttf.PostScriptName, name, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ttf.FamilyName, name, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string n in ttf.AllNames)
            {
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void CheckEmbedding(TrueTypeFont ttf, bool ignore)
        {
            if (ignore) return;
            int fsType = ttf.FsType;
            if ((fsType & 0x000F) == 0x0002)
                throw new NotSupportedException("フォント '" + ttf.FullName + "' はライセンスにより埋め込みが禁止されています（OS/2 fsType = Restricted License）。");
            if ((fsType & 0x0200) != 0)
                throw new NotSupportedException("フォント '" + ttf.FullName + "' はビットマップのみ埋め込み可能なため使用できません。");
        }

        /// <summary>ファミリー名。</summary>
        public string FamilyName => Ttf.FamilyName;

        /// <summary>フルネーム。</summary>
        public string FullName => Ttf.FullName;

        /// <summary>PostScript 名。</summary>
        public string PostScriptName => Ttf.PostScriptName;

        /// <summary>
        /// 可変フォント（fvar テーブルを持つ）か。可変フォントは既定のインスタンス（フォントによっては Thin など）の形で描画される。
        /// </summary>
        public bool IsVariableFont => Ttf.HasTable("fvar");

        /// <summary>フォント自体が太字か（usWeightClass ≥ 600 など）。</summary>
        public bool IsBold => Ttf.WeightClass >= 600 || (Ttf.MacStyle & 1) != 0;

        /// <summary>フォント自体が斜体か。</summary>
        public bool IsItalic => (Ttf.MacStyle & 2) != 0 || Ttf.ItalicAngle != 0;

        /// <summary>
        /// 太字指定時に使うフォント。未設定なら線幅を加える疑似太字で描画する。
        /// </summary>
        public PdfFont? BoldVariant { get; set; }

        /// <summary>斜体指定時に使うフォント。未設定なら傾けて描画する疑似斜体になる。</summary>
        public PdfFont? ItalicVariant { get; set; }

        /// <summary>太字かつ斜体のときに使うフォント。</summary>
        public PdfFont? BoldItalicVariant { get; set; }

        /// <summary>1 em に対する上方向の高さ（ベースラインから）。</summary>
        public double Ascent
        {
            get
            {
                EnsureMetrics();
                return _ascent;
            }
        }

        /// <summary>1 em に対する下方向の深さ（正の値）。</summary>
        public double Descent
        {
            get
            {
                EnsureMetrics();
                return _descent;
            }
        }

        private bool _metricsReady;
        private double _ascent;
        private double _descent;

        private void EnsureMetrics()
        {
            if (_metricsReady) return;
            double upm = Ttf.UnitsPerEm;
            double asc, desc;
            if (Ttf.HasOs2 && Ttf.TypoAscender > 0 && Ttf.TypoAscender - Ttf.TypoDescender > 0)
            {
                asc = Ttf.TypoAscender;
                desc = -Ttf.TypoDescender;
            }
            else
            {
                asc = Ttf.HheaAscender;
                desc = -Ttf.HheaDescender;
            }
            if (asc <= 0) asc = upm * 0.88;
            if (desc < 0) desc = 0;
            _ascent = asc / upm;
            _descent = desc / upm;
            _metricsReady = true;
        }

        /// <summary>下線のベースラインからの位置（em 単位、下向きが正）。</summary>
        internal double UnderlineOffset => Ttf.UnderlinePosition != 0 ? -Ttf.UnderlinePosition / (double)Ttf.UnitsPerEm : 0.1;

        /// <summary>下線の太さ（em 単位）。</summary>
        internal double UnderlineThickness => Ttf.UnderlineThickness > 0 ? Ttf.UnderlineThickness / (double)Ttf.UnitsPerEm : 0.05;

        /// <summary>このフォントが指定したコードポイントのグリフを持つか。</summary>
        public bool HasGlyph(int codepoint) => Ttf.GetGlyphId(codepoint) != 0;

        internal ushort GetGlyphId(int codepoint) => Ttf.GetGlyphId(codepoint);

        /// <summary>グリフの送り幅（em 単位）。</summary>
        internal double GetAdvance(ushort gid) => Ttf.GetAdvance(gid) / (double)Ttf.UnitsPerEm;

        /// <summary>
        /// 1 行の文字列幅をポイント単位で測る（折り返しなし、フォールバックなし）。
        /// </summary>
        public double MeasureText(string text, double fontSize)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            double width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int cp = text[i];
                if (char.IsHighSurrogate(text, i) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                if (Text.TextUtil.IsIgnorable(cp)) continue;
                width += GetAdvance(GetGlyphId(cp));
            }
            return width * fontSize;
        }

        /// <summary>フォントのフルネーム（<see cref="FullName"/>）を返す。</summary>
        public override string ToString() => FullName;
    }
}
