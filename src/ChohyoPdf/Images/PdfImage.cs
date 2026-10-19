using System;
using System.IO;
using ChohyoPdf.Images;

namespace ChohyoPdf
{
    /// <summary>
    /// PDF に埋め込む画像（JPEG / PNG）。同じインスタンスを何度描画しても、文書内には 1 回だけ埋め込まれる。
    /// </summary>
    public sealed class PdfImage
    {
        internal enum ImageKind
        {
            Jpeg,
            Flate,
        }

        internal ImageKind Kind { get; private set; }

        /// <summary>JPEG ならファイルそのもの、PNG なら zlib 圧縮した画素データ。</summary>
        internal byte[] Data { get; private set; } = Array.Empty<byte>();

        /// <summary>zlib 圧縮したアルファチャンネル（8 bit グレー）。なければ null。</summary>
        internal byte[]? AlphaData { get; private set; }

        internal int Components { get; private set; }

        internal bool InvertCmyk { get; private set; }

        /// <summary>画像の幅（ピクセル）。</summary>
        public int PixelWidth { get; private set; }

        /// <summary>画像の高さ（ピクセル）。</summary>
        public int PixelHeight { get; private set; }

        /// <summary>水平解像度（dpi）。画像に情報がなければ 96。</summary>
        public double DpiX { get; private set; } = 96;

        /// <summary>垂直解像度（dpi）。画像に情報がなければ 96。</summary>
        public double DpiY { get; private set; } = 96;

        /// <summary>解像度に基づく自然な幅（ポイント）。</summary>
        public double Width => PixelWidth * 72.0 / DpiX;

        /// <summary>解像度に基づく自然な高さ（ポイント）。</summary>
        public double Height => PixelHeight * 72.0 / DpiY;

        /// <summary>透明部分を持つか。</summary>
        public bool HasAlpha => AlphaData != null;

        private PdfImage()
        {
        }

        public static PdfImage FromFile(string path) => FromBytes(File.ReadAllBytes(path));

        public static PdfImage FromStream(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return FromBytes(ms.ToArray());
            }
        }

        /// <summary>JPEG または PNG のバイト列から画像を作る（形式は自動判別）。</summary>
        public static PdfImage FromBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8)
                return FromJpeg(data);
            if (data.Length > 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
                return FromPng(data);
            throw new NotSupportedException("対応していない画像形式です（JPEG / PNG のみ対応）。");
        }

        private static PdfImage FromJpeg(byte[] data)
        {
            var info = JpegInfo.Parse(data);
            return new PdfImage
            {
                Kind = ImageKind.Jpeg,
                Data = data,
                Components = info.Components,
                InvertCmyk = info.Components == 4 && info.Adobe,
                PixelWidth = info.Width,
                PixelHeight = info.Height,
                DpiX = info.DpiX > 0 ? info.DpiX : 96,
                DpiY = info.DpiY > 0 ? info.DpiY : 96,
            };
        }

        private static PdfImage FromPng(byte[] data)
        {
            var png = PngDecoder.Decode(data);
            return new PdfImage
            {
                Kind = ImageKind.Flate,
                Data = Internal.Zlib.Compress(png.Pixels),
                AlphaData = png.Alpha != null ? Internal.Zlib.Compress(png.Alpha) : null,
                Components = png.Components,
                PixelWidth = png.Width,
                PixelHeight = png.Height,
                DpiX = png.DpiX > 0 ? png.DpiX : 96,
                DpiY = png.DpiY > 0 ? png.DpiY : 96,
            };
        }

        /// <summary>
        /// 8 bit の生画素データから画像を作る。<paramref name="components"/> は 1（グレー）、3（RGB）、4（CMYK）。
        /// <paramref name="alpha"/> を渡すと透明度として使う（1 画素 1 バイト）。
        /// </summary>
        public static PdfImage FromRawPixels(int width, int height, int components, byte[] pixels, byte[]? alpha = null)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (components != 1 && components != 3 && components != 4) throw new ArgumentOutOfRangeException(nameof(components));
            if (pixels == null || pixels.Length < width * height * components) throw new ArgumentException("画素データが不足しています。", nameof(pixels));
            if (alpha != null && alpha.Length < width * height) throw new ArgumentException("アルファデータが不足しています。", nameof(alpha));
            return new PdfImage
            {
                Kind = ImageKind.Flate,
                Data = Internal.Zlib.Compress(pixels, 0, width * height * components),
                AlphaData = alpha != null ? Internal.Zlib.Compress(alpha, 0, width * height) : null,
                Components = components,
                PixelWidth = width,
                PixelHeight = height,
            };
        }
    }
}
