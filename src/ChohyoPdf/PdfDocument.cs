using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ChohyoPdf.Fonts;
using ChohyoPdf.Internal;

namespace ChohyoPdf
{
    /// <summary>文書情報（PDF の Info 辞書）。</summary>
    public sealed class PdfDocumentInfo
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public string? Creator { get; set; }
        public string Producer { get; set; } = "ChohyoPdf";

        /// <summary>作成日時。null なら保存時の現在時刻。固定すると同じ入力から同じバイト列が得られる。</summary>
        public DateTimeOffset? CreationDate { get; set; }
    }

    /// <summary>
    /// 低レベルの PDF 文書。ページを追加し、<see cref="PdfGraphics"/> で座標を指定して描画する。
    /// 自動レイアウト（折り返し・改ページ・表）が必要なら <see cref="Document"/> を使う。
    /// </summary>
    public sealed class PdfDocument
    {
        internal sealed class FontResource
        {
            public string Name = "";
            public PdfFont Font = null!;
            public FontSubset Subset = null!;
            public int Id;
        }

        internal sealed class ImageResource
        {
            public string Name = "";
            public PdfImage Image = null!;
            public int Id;
            public int MaskId;
        }

        private readonly List<PdfPage> _pages = new List<PdfPage>();
        private readonly Dictionary<PdfFont, FontResource> _fonts = new Dictionary<PdfFont, FontResource>();
        private readonly List<FontResource> _fontList = new List<FontResource>();
        private readonly Dictionary<PdfImage, ImageResource> _images = new Dictionary<PdfImage, ImageResource>();
        private readonly List<ImageResource> _imageList = new List<ImageResource>();
        private readonly Dictionary<string, string> _alphaStates = new Dictionary<string, string>();

        public PdfDocumentInfo Info { get; } = new PdfDocumentInfo();

        /// <summary>文書の言語（例: "ja-JP"）。</summary>
        public string? Language { get; set; } = "ja-JP";

        /// <summary>コンテンツストリームを圧縮するか（既定 true）。</summary>
        public bool CompressContent { get; set; } = true;

        public IReadOnlyList<PdfPage> Pages => _pages;

        /// <summary>ページを追加する。</summary>
        public PdfPage AddPage(PageSize size)
        {
            var page = new PdfPage(this, size.Width, size.Height);
            _pages.Add(page);
            return page;
        }

        /// <summary>A4 縦のページを追加する。</summary>
        public PdfPage AddPage() => AddPage(PageSize.A4);

        internal FontResource GetFont(PdfFont font)
        {
            if (!_fonts.TryGetValue(font, out var res))
            {
                res = new FontResource { Name = "F" + (_fontList.Count + 1), Font = font, Subset = new FontSubset(font.Ttf) };
                _fonts[font] = res;
                _fontList.Add(res);
            }
            return res;
        }

        internal ImageResource GetImage(PdfImage image)
        {
            if (!_images.TryGetValue(image, out var res))
            {
                res = new ImageResource { Name = "Im" + (_imageList.Count + 1), Image = image };
                _images[image] = res;
                _imageList.Add(res);
            }
            return res;
        }

        /// <summary>不透明度用の ExtGState 名を返す。</summary>
        internal string GetAlphaState(double fillAlpha, double strokeAlpha, out string dictionary)
        {
            string key = PdfFormat.Num(fillAlpha) + "/" + PdfFormat.Num(strokeAlpha);
            dictionary = "<< /Type /ExtGState /ca " + PdfFormat.Num(fillAlpha) + " /CA " + PdfFormat.Num(strokeAlpha) + " >>";
            if (!_alphaStates.TryGetValue(key, out string? name))
            {
                name = "GS" + (_alphaStates.Count + 1);
                _alphaStates[key] = name;
            }
            return name;
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Save(fs);
            }
        }

        public byte[] ToArray()
        {
            using (var ms = new MemoryStream())
            {
                Save(ms);
                return ms.ToArray();
            }
        }

        public void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (_pages.Count == 0) AddPage();

            var output = new PdfOutput(stream);
            int catalogId = output.AllocateId();
            int pagesId = output.AllocateId();
            int infoId = output.AllocateId();

            var pageIds = new int[_pages.Count];
            var contentIds = new int[_pages.Count];
            var annotIds = new List<int>[_pages.Count];
            for (int i = 0; i < _pages.Count; i++)
            {
                pageIds[i] = output.AllocateId();
                contentIds[i] = output.AllocateId();
                annotIds[i] = new List<int>();
                foreach (var _ in _pages[i].Links) annotIds[i].Add(output.AllocateId());
            }

            var fontIds = new Dictionary<FontResource, int[]>();
            foreach (var f in _fontList)
            {
                f.Id = output.AllocateId();
                fontIds[f] = new[] { output.AllocateId(), output.AllocateId(), output.AllocateId(), output.AllocateId() };
            }
            foreach (var img in _imageList)
            {
                img.Id = output.AllocateId();
                img.MaskId = img.Image.AlphaData != null ? output.AllocateId() : 0;
            }

            output.WriteHeader();

            var catalog = new StringBuilder("<< /Type /Catalog /Pages " + pagesId + " 0 R");
            if (!string.IsNullOrEmpty(Language)) catalog.Append(" /Lang ").Append(PdfFormat.TextString(Language!));
            if (!string.IsNullOrEmpty(Info.Title)) catalog.Append(" /ViewerPreferences << /DisplayDocTitle true >>");
            catalog.Append(" >>");
            output.WriteObject(catalogId, catalog.ToString());

            var kids = new StringBuilder();
            foreach (int id in pageIds) kids.Append(id).Append(" 0 R ");
            output.WriteObject(pagesId, "<< /Type /Pages /Kids [" + kids.ToString().TrimEnd() + "] /Count " + _pages.Count + " >>");

            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                var dict = new StringBuilder();
                dict.Append("<< /Type /Page /Parent ").Append(pagesId).Append(" 0 R");
                dict.Append(" /MediaBox [0 0 ").Append(PdfFormat.Num(page.Width)).Append(' ').Append(PdfFormat.Num(page.Height)).Append(']');
                dict.Append(" /Resources ").Append(BuildResources(page));
                dict.Append(" /Contents ").Append(contentIds[i]).Append(" 0 R");
                if (annotIds[i].Count > 0)
                {
                    dict.Append(" /Annots [");
                    foreach (int id in annotIds[i]) dict.Append(id).Append(" 0 R ");
                    dict.Append(']');
                }
                dict.Append(" >>");
                output.WriteObject(pageIds[i], dict.ToString());

                byte[] content = Encoding.ASCII.GetBytes(page.BuildContent());
                if (CompressContent)
                    output.WriteStreamObject(contentIds[i], "/Filter /FlateDecode", Zlib.Compress(content));
                else
                    output.WriteStreamObject(contentIds[i], "", content);

                for (int a = 0; a < page.Links.Count; a++)
                {
                    var link = page.Links[a];
                    double x0 = link.Rect.X, x1 = link.Rect.Right;
                    double y0 = page.Height - link.Rect.Bottom, y1 = page.Height - link.Rect.Y;
                    output.WriteObject(annotIds[i][a],
                        "<< /Type /Annot /Subtype /Link /Rect [" + PdfFormat.Num(x0) + " " + PdfFormat.Num(y0) + " " + PdfFormat.Num(x1) + " " + PdfFormat.Num(y1) + "]"
                        + " /Border [0 0 0] /A << /S /URI /URI " + PdfFormat.TextString(link.Uri) + " >> >>");
                }
            }

            foreach (var f in _fontList) WriteFont(output, f, fontIds[f]);
            foreach (var img in _imageList) WriteImage(output, img);

            var created = Info.CreationDate ?? DateTimeOffset.Now;
            var info = new StringBuilder("<<");
            void Add(string key, string? value)
            {
                if (!string.IsNullOrEmpty(value)) info.Append(" /").Append(key).Append(' ').Append(PdfFormat.TextString(value!));
            }
            Add("Title", Info.Title);
            Add("Author", Info.Author);
            Add("Subject", Info.Subject);
            Add("Keywords", Info.Keywords);
            Add("Creator", Info.Creator);
            Add("Producer", Info.Producer);
            info.Append(" /CreationDate ").Append(PdfFormat.Date(created));
            info.Append(" /ModDate ").Append(PdfFormat.Date(created));
            info.Append(" >>");
            output.WriteObject(infoId, info.ToString());

            byte[] fileId;
            using (var sha = SHA256.Create())
            {
                var seed = Encoding.UTF8.GetBytes(PdfFormat.Date(created) + "|" + Info.Title + "|" + _pages.Count + "|" + output.ObjectCount);
                var hash = sha.ComputeHash(seed);
                fileId = new byte[16];
                Array.Copy(hash, fileId, 16);
            }
            output.WriteXrefAndTrailer(catalogId, infoId, fileId);
        }

        private string BuildResources(PdfPage page)
        {
            var sb = new StringBuilder("<< /ProcSet [/PDF /Text /ImageB /ImageC]");
            if (page.UsedFonts.Count > 0)
            {
                sb.Append(" /Font <<");
                foreach (var f in page.UsedFonts) sb.Append(" /").Append(f.Name).Append(' ').Append(f.Id).Append(" 0 R");
                sb.Append(" >>");
            }
            if (page.UsedImages.Count > 0)
            {
                sb.Append(" /XObject <<");
                foreach (var img in page.UsedImages) sb.Append(" /").Append(img.Name).Append(' ').Append(img.Id).Append(" 0 R");
                sb.Append(" >>");
            }
            if (page.UsedStates.Count > 0)
            {
                sb.Append(" /ExtGState <<");
                foreach (var kv in page.UsedStates) sb.Append(" /").Append(kv.Key).Append(' ').Append(kv.Value);
                sb.Append(" >>");
            }
            sb.Append(" >>");
            return sb.ToString();
        }

        private static void WriteFont(PdfOutput output, FontResource res, int[] ids)
        {
            int cidFontId = ids[0], descriptorId = ids[1], fileId = ids[2], toUnicodeId = ids[3];
            var ttf = res.Font.Ttf;
            var subset = res.Subset;
            // 幅配列はサブセット化前に計算する（Build で複合グリフの構成要素が追加されるため）
            int usedCount = subset.UsedCount;
            var widths = new StringBuilder();
            double scale = 1000.0 / ttf.UnitsPerEm;
            widths.Append("[ 0 [");
            for (int g = 0; g < usedCount; g++)
            {
                widths.Append(' ').Append(PdfFormat.Num(ttf.GetAdvance(subset.NewToOld[g]) * scale));
            }
            widths.Append(" ] ]");

            byte[] fontFile = subset.Build();
            string baseName = subset.ComputeTag() + "+" + SanitizeName(res.Font.PostScriptName);

            output.WriteObject(res.Id,
                "<< /Type /Font /Subtype /Type0 /BaseFont " + PdfFormat.Name(baseName) + " /Encoding /Identity-H"
                + " /DescendantFonts [" + cidFontId + " 0 R] /ToUnicode " + toUnicodeId + " 0 R >>");

            output.WriteObject(cidFontId,
                "<< /Type /Font /Subtype /CIDFontType2 /BaseFont " + PdfFormat.Name(baseName)
                + " /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>"
                + " /FontDescriptor " + descriptorId + " 0 R /DW 1000 /W " + widths + " /CIDToGIDMap /Identity >>");

            int flags = 4; // Symbolic
            if (ttf.IsFixedPitch) flags |= 1;
            int familyClass = ttf.FamilyClass >> 8;
            if (familyClass >= 1 && familyClass <= 7) flags |= 2; // Serif
            if (ttf.ItalicAngle != 0 || (ttf.MacStyle & 2) != 0) flags |= 64;
            double ascent = ttf.HheaAscender * scale;
            double descent = ttf.HheaDescender * scale;
            double capHeight = ttf.CapHeight > 0 ? ttf.CapHeight * scale : ascent * 0.8;
            double stemV = 50 + Math.Pow(ttf.WeightClass / 65.0, 2);
            output.WriteObject(descriptorId,
                "<< /Type /FontDescriptor /FontName " + PdfFormat.Name(baseName) + " /Flags " + flags
                + " /FontBBox [" + PdfFormat.Num(ttf.XMin * scale) + " " + PdfFormat.Num(ttf.YMin * scale) + " " + PdfFormat.Num(ttf.XMax * scale) + " " + PdfFormat.Num(ttf.YMax * scale) + "]"
                + " /ItalicAngle " + PdfFormat.Num(ttf.ItalicAngle) + " /Ascent " + PdfFormat.Num(ascent) + " /Descent " + PdfFormat.Num(descent)
                + " /CapHeight " + PdfFormat.Num(capHeight) + " /StemV " + PdfFormat.Num(stemV)
                + " /FontFile2 " + fileId + " 0 R >>");

            output.WriteStreamObject(fileId, "/Filter /FlateDecode /Length1 " + fontFile.Length, Zlib.Compress(fontFile));

            byte[] cmap = Encoding.ASCII.GetBytes(BuildToUnicode(subset));
            output.WriteStreamObject(toUnicodeId, "/Filter /FlateDecode", Zlib.Compress(cmap));
        }

        private static string SanitizeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (c > 0x20 && c < 0x7F && "()<>[]{}/%#".IndexOf(c) < 0) sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : "Font";
        }

        private static string BuildToUnicode(FontSubset subset)
        {
            var sb = new StringBuilder();
            sb.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n");
            sb.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
            sb.Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n");
            sb.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
            var entries = new List<KeyValuePair<ushort, string>>(subset.UnicodeMap);
            entries.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < entries.Count; i += 100)
            {
                int n = Math.Min(100, entries.Count - i);
                sb.Append(n).Append(" beginbfchar\n");
                for (int j = i; j < i + n; j++)
                {
                    sb.Append('<').Append(entries[j].Key.ToString("X4")).Append("> <");
                    foreach (char c in entries[j].Value) sb.Append(((int)c).ToString("X4"));
                    sb.Append(">\n");
                }
                sb.Append("endbfchar\n");
            }
            sb.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
            return sb.ToString();
        }

        private static void WriteImage(PdfOutput output, ImageResource res)
        {
            var img = res.Image;
            string colorSpace = img.Components == 1 ? "/DeviceGray" : img.Components == 4 ? "/DeviceCMYK" : "/DeviceRGB";
            var dict = new StringBuilder("/Type /XObject /Subtype /Image");
            dict.Append(" /Width ").Append(img.PixelWidth).Append(" /Height ").Append(img.PixelHeight);
            dict.Append(" /ColorSpace ").Append(colorSpace).Append(" /BitsPerComponent 8");
            dict.Append(img.Kind == PdfImage.ImageKind.Jpeg ? " /Filter /DCTDecode" : " /Filter /FlateDecode");
            if (img.InvertCmyk) dict.Append(" /Decode [1 0 1 0 1 0 1 0]");
            if (res.MaskId != 0) dict.Append(" /SMask ").Append(res.MaskId).Append(" 0 R");
            output.WriteStreamObject(res.Id, dict.ToString(), img.Data);
            if (res.MaskId != 0)
            {
                output.WriteStreamObject(res.MaskId,
                    "/Type /XObject /Subtype /Image /Width " + img.PixelWidth + " /Height " + img.PixelHeight
                    + " /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", img.AlphaData!);
            }
        }
    }
}
