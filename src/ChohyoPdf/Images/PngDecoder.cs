using System;
using System.IO;

namespace ChohyoPdf.Images
{
    /// <summary>
    /// PNG デコーダー。全カラータイプ・全ビット深度・インターレース（Adam7）に対応し、
    /// 8 bit のグレーまたは RGB 画素と、必要ならアルファを返す。
    /// </summary>
    internal sealed class PngDecoder
    {
        public int Width;
        public int Height;
        public int Components;
        public byte[] Pixels = Array.Empty<byte>();
        public byte[]? Alpha;
        public double DpiX;
        public double DpiY;

        private static readonly int[] PassXStart = { 0, 4, 0, 2, 0, 1, 0 };
        private static readonly int[] PassYStart = { 0, 0, 4, 0, 2, 0, 1 };
        private static readonly int[] PassXStep = { 8, 8, 4, 4, 2, 2, 1 };
        private static readonly int[] PassYStep = { 8, 8, 8, 4, 4, 2, 2 };

        public static PngDecoder Decode(byte[] d)
        {
            int p = 8;
            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[]? palette = null;
            byte[]? trns = null;
            double dpiX = 0, dpiY = 0;
            var idat = new MemoryStream();
            while (p + 8 <= d.Length)
            {
                int length = (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
                string type = new string(new[] { (char)d[p + 4], (char)d[p + 5], (char)d[p + 6], (char)d[p + 7] });
                int data = p + 8;
                if (length < 0 || data + length > d.Length) throw new InvalidDataException("PNG チャンクが壊れています。");
                switch (type)
                {
                    case "IHDR":
                        width = (d[data] << 24) | (d[data + 1] << 16) | (d[data + 2] << 8) | d[data + 3];
                        height = (d[data + 4] << 24) | (d[data + 5] << 16) | (d[data + 6] << 8) | d[data + 7];
                        bitDepth = d[data + 8];
                        colorType = d[data + 9];
                        interlace = d[data + 12];
                        break;
                    case "PLTE":
                        palette = new byte[length];
                        Buffer.BlockCopy(d, data, palette, 0, length);
                        break;
                    case "tRNS":
                        trns = new byte[length];
                        Buffer.BlockCopy(d, data, trns, 0, length);
                        break;
                    case "pHYs":
                        if (length >= 9 && d[data + 8] == 1)
                        {
                            long ppx = ((long)d[data] << 24) | ((long)d[data + 1] << 16) | ((long)d[data + 2] << 8) | d[data + 3];
                            long ppy = ((long)d[data + 4] << 24) | ((long)d[data + 5] << 16) | ((long)d[data + 6] << 8) | d[data + 7];
                            dpiX = ppx * 0.0254;
                            dpiY = ppy * 0.0254;
                        }
                        break;
                    case "IDAT":
                        idat.Write(d, data, length);
                        break;
                }
                if (type == "IEND") break;
                p = data + length + 4; // CRC を読み飛ばす
            }

            if (width <= 0 || height <= 0) throw new InvalidDataException("PNG の IHDR が不正です。");
            int channels;
            switch (colorType)
            {
                case 0: channels = 1; break;
                case 2: channels = 3; break;
                case 3: channels = 1; break;
                case 4: channels = 2; break;
                case 6: channels = 4; break;
                default: throw new NotSupportedException("PNG のカラータイプ " + colorType + " には対応していません。");
            }
            if (colorType == 3 && palette == null) throw new InvalidDataException("PNG のパレットがありません。");

            byte[] raw = Internal.Zlib.Decompress(idat.ToArray());
            var dec = new PngDecoder
            {
                Width = width,
                Height = height,
                DpiX = dpiX,
                DpiY = dpiY,
                Components = colorType == 0 || colorType == 4 ? 1 : 3,
            };
            dec.Pixels = new byte[width * height * dec.Components];
            var alpha = new byte[width * height];
            bool hasAlpha = false;

            int bitsPerPixel = channels * bitDepth;
            int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
            int offset = 0;
            int passes = interlace == 1 ? 7 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                int xs = interlace == 1 ? PassXStart[pass] : 0;
                int ys = interlace == 1 ? PassYStart[pass] : 0;
                int xStep = interlace == 1 ? PassXStep[pass] : 1;
                int yStep = interlace == 1 ? PassYStep[pass] : 1;
                int pw = (width - xs + xStep - 1) / xStep;
                int ph = (height - ys + yStep - 1) / yStep;
                if (pw <= 0 || ph <= 0) continue;
                int stride = (pw * bitsPerPixel + 7) / 8;
                var prev = new byte[stride];
                var row = new byte[stride];
                for (int y = 0; y < ph; y++)
                {
                    if (offset + 1 + stride > raw.Length) throw new InvalidDataException("PNG の画素データが不足しています。");
                    int filter = raw[offset];
                    Buffer.BlockCopy(raw, offset + 1, row, 0, stride);
                    offset += 1 + stride;
                    Unfilter(filter, row, prev, bytesPerPixel);
                    int outY = ys + y * yStep;
                    for (int x = 0; x < pw; x++)
                    {
                        int outX = xs + x * xStep;
                        int idx = outY * width + outX;
                        byte a = 255;
                        switch (colorType)
                        {
                            case 0:
                            {
                                int v = Sample(row, x, 0, 1, bitDepth);
                                dec.Pixels[idx] = Scale(v, bitDepth);
                                if (trns != null && trns.Length >= 2 && v == ((trns[0] << 8) | trns[1])) a = 0;
                                break;
                            }
                            case 2:
                            {
                                int r = Sample(row, x, 0, 3, bitDepth), g = Sample(row, x, 1, 3, bitDepth), b = Sample(row, x, 2, 3, bitDepth);
                                dec.Pixels[idx * 3] = Scale(r, bitDepth);
                                dec.Pixels[idx * 3 + 1] = Scale(g, bitDepth);
                                dec.Pixels[idx * 3 + 2] = Scale(b, bitDepth);
                                if (trns != null && trns.Length >= 6 && r == ((trns[0] << 8) | trns[1]) && g == ((trns[2] << 8) | trns[3]) && b == ((trns[4] << 8) | trns[5])) a = 0;
                                break;
                            }
                            case 3:
                            {
                                int i = Sample(row, x, 0, 1, bitDepth);
                                if (i * 3 + 2 < palette!.Length)
                                {
                                    dec.Pixels[idx * 3] = palette[i * 3];
                                    dec.Pixels[idx * 3 + 1] = palette[i * 3 + 1];
                                    dec.Pixels[idx * 3 + 2] = palette[i * 3 + 2];
                                }
                                if (trns != null && i < trns.Length) a = trns[i];
                                break;
                            }
                            case 4:
                                dec.Pixels[idx] = Scale(Sample(row, x, 0, 2, bitDepth), bitDepth);
                                a = Scale(Sample(row, x, 1, 2, bitDepth), bitDepth);
                                break;
                            case 6:
                                dec.Pixels[idx * 3] = Scale(Sample(row, x, 0, 4, bitDepth), bitDepth);
                                dec.Pixels[idx * 3 + 1] = Scale(Sample(row, x, 1, 4, bitDepth), bitDepth);
                                dec.Pixels[idx * 3 + 2] = Scale(Sample(row, x, 2, 4, bitDepth), bitDepth);
                                a = Scale(Sample(row, x, 3, 4, bitDepth), bitDepth);
                                break;
                        }
                        alpha[idx] = a;
                        if (a != 255) hasAlpha = true;
                    }
                    var tmp = prev;
                    prev = row;
                    row = tmp;
                }
            }
            dec.Alpha = hasAlpha ? alpha : null;
            return dec;
        }

        private static int Sample(byte[] row, int x, int channel, int channels, int bitDepth)
        {
            switch (bitDepth)
            {
                case 8: return row[x * channels + channel];
                case 16:
                {
                    int i = (x * channels + channel) * 2;
                    return (row[i] << 8) | row[i + 1];
                }
                default:
                {
                    // 1, 2, 4 bit はグレーとパレットのみ（channels == 1）
                    int bitIndex = x * bitDepth;
                    int b = row[bitIndex >> 3];
                    int shift = 8 - bitDepth - (bitIndex & 7);
                    return (b >> shift) & ((1 << bitDepth) - 1);
                }
            }
        }

        private static byte Scale(int value, int bitDepth)
        {
            switch (bitDepth)
            {
                case 8: return (byte)value;
                case 16: return (byte)(value >> 8);
                default: return (byte)(value * 255 / ((1 << bitDepth) - 1));
            }
        }

        private static void Unfilter(int filter, byte[] row, byte[] prev, int bpp)
        {
            int n = row.Length;
            switch (filter)
            {
                case 0:
                    break;
                case 1:
                    for (int i = bpp; i < n; i++) row[i] = (byte)(row[i] + row[i - bpp]);
                    break;
                case 2:
                    for (int i = 0; i < n; i++) row[i] = (byte)(row[i] + prev[i]);
                    break;
                case 3:
                    for (int i = 0; i < n; i++)
                    {
                        int left = i >= bpp ? row[i - bpp] : 0;
                        row[i] = (byte)(row[i] + ((left + prev[i]) >> 1));
                    }
                    break;
                case 4:
                    for (int i = 0; i < n; i++)
                    {
                        int a = i >= bpp ? row[i - bpp] : 0;
                        int b = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int pp = a + b - c;
                        int pa = Math.Abs(pp - a), pb = Math.Abs(pp - b), pc = Math.Abs(pp - c);
                        int pred = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                        row[i] = (byte)(row[i] + pred);
                    }
                    break;
                default:
                    throw new InvalidDataException("PNG のフィルタ種別 " + filter + " が不正です。");
            }
        }
    }
}
