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

        /// <summary>画像の幅（ピクセル）。EXIF の向きを適用した画像（<see cref="OrientationApplied"/>）では、向きを直した後の幅。</summary>
        public int PixelWidth { get; private set; }

        /// <summary>画像の高さ（ピクセル）。EXIF の向きを適用した画像では、向きを直した後の高さ。</summary>
        public int PixelHeight { get; private set; }

        /// <summary>埋め込む画素データそのものの幅（ピクセル。PDF の画像 XObject の /Width）。</summary>
        internal int StoredWidth { get; private set; }

        /// <summary>埋め込む画素データそのものの高さ（ピクセル。PDF の画像 XObject の /Height）。</summary>
        internal int StoredHeight { get; private set; }

        /// <summary>描画時に適用する向き（1〜8。1 はそのまま）。</summary>
        internal int Orientation { get; private set; } = 1;

        /// <summary>
        /// JPEG の EXIF に記録されている向き（Orientation。1〜8）。EXIF がない、値が不正、JPEG 以外のときは 1。
        /// 適用するかどうかは <see cref="OrientationApplied"/> で分かる。
        /// </summary>
        public int ExifOrientation { get; private set; } = 1;

        /// <summary>
        /// EXIF の向きを描画に反映しているか。true なら、<see cref="Width"/> / <see cref="Height"/> / <see cref="PixelWidth"/> / <see cref="PixelHeight"/> /
        /// <see cref="DpiX"/> / <see cref="DpiY"/> は向きを直した後の画像のもの。画素データは再エンコードせず、描画時の変換行列で向きを直す。
        /// </summary>
        public bool OrientationApplied => Orientation != 1;

        /// <summary>水平解像度（dpi）。画像に情報がなければ 96。向きを適用した画像では、向きを直した後の水平方向のもの。</summary>
        public double DpiX { get; private set; } = 96;

        /// <summary>垂直解像度（dpi）。画像に情報がなければ 96。向きを適用した画像では、向きを直した後の垂直方向のもの。</summary>
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

        /// <summary>JPEG または PNG のファイルから画像を作る。JPEG の EXIF の向きは反映する（<see cref="FromBytes(byte[], bool)"/> を参照）。</summary>
        public static PdfImage FromFile(string path) => FromBytes(File.ReadAllBytes(path), true);

        /// <summary>ファイルから画像を作る。<paramref name="applyExifOrientation"/> が false なら、JPEG の EXIF の向きを無視する。</summary>
        public static PdfImage FromFile(string path, bool applyExifOrientation) => FromBytes(File.ReadAllBytes(path), applyExifOrientation);

        /// <summary>ストリームから画像を作る。JPEG の EXIF の向きは反映する（<see cref="FromBytes(byte[], bool)"/> を参照）。</summary>
        public static PdfImage FromStream(Stream stream) => FromStream(stream, true);

        /// <summary>ストリームから画像を作る。<paramref name="applyExifOrientation"/> が false なら、JPEG の EXIF の向きを無視する。</summary>
        public static PdfImage FromStream(Stream stream, bool applyExifOrientation)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return FromBytes(ms.ToArray(), applyExifOrientation);
            }
        }

        /// <summary>
        /// JPEG または PNG のバイト列から画像を作る（形式は自動判別）。
        /// JPEG に EXIF の向き（Orientation）があれば、描画時に回転・反転して正しい向きで表示する（<see cref="FromBytes(byte[], bool)"/> で無効にできる）。
        /// </summary>
        public static PdfImage FromBytes(byte[] data) => FromBytes(data, true);

        /// <summary>
        /// JPEG または PNG のバイト列から画像を作る（形式は自動判別）。
        /// <paramref name="applyExifOrientation"/> が true なら、JPEG の EXIF の向き（Orientation 1〜8。TIFF ヘッダーのバイト順は II / MM とも）に従い、
        /// 描画時の変換行列で回転・反転して正しい向きで表示する（画素データは再エンコードせず、そのまま埋め込む）。
        /// false なら EXIF の向きを無視し、保存されている画素の向きのままにする。PNG には向きの情報がないので、指定は関係しない。
        /// </summary>
        public static PdfImage FromBytes(byte[] data, bool applyExifOrientation)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8)
                return FromJpeg(data, applyExifOrientation);
            if (data.Length > 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
                return FromPng(data);
            throw new NotSupportedException("対応していない画像形式です（JPEG / PNG のみ対応）。");
        }

        private static PdfImage FromJpeg(byte[] data, bool applyExifOrientation)
        {
            var info = JpegInfo.Parse(data);
            int orientation = applyExifOrientation && ImageOrientation.IsValid(info.Orientation) ? info.Orientation : 1;
            bool swap = ImageOrientation.SwapsAxes(orientation);
            double dpiX = info.DpiX > 0 ? info.DpiX : 96;
            double dpiY = info.DpiY > 0 ? info.DpiY : 96;
            return new PdfImage
            {
                Kind = ImageKind.Jpeg,
                Data = data,
                Components = info.Components,
                InvertCmyk = info.Components == 4 && info.Adobe,
                StoredWidth = info.Width,
                StoredHeight = info.Height,
                Orientation = orientation,
                ExifOrientation = ImageOrientation.IsValid(info.Orientation) ? info.Orientation : 1,
                // 縦横が入れ替わる向きでは、幅と解像度も入れ替える
                PixelWidth = swap ? info.Height : info.Width,
                PixelHeight = swap ? info.Width : info.Height,
                DpiX = swap ? dpiY : dpiX,
                DpiY = swap ? dpiX : dpiY,
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
                StoredWidth = png.Width,
                StoredHeight = png.Height,
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
                StoredWidth = width,
                StoredHeight = height,
                PixelWidth = width,
                PixelHeight = height,
            };
        }
    }
}
