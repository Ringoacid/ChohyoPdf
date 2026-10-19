using System;
using System.IO;

namespace ChohyoPdf.Images
{
    /// <summary>JPEG のヘッダ情報（画素データはデコードせず、そのまま DCTDecode で埋め込む）。</summary>
    internal sealed class JpegInfo
    {
        public int Width;
        public int Height;
        public int Components;
        public bool Adobe;
        public double DpiX;
        public double DpiY;

        public static JpegInfo Parse(byte[] d)
        {
            var info = new JpegInfo();
            int p = 2;
            bool sofFound = false;
            while (p + 4 <= d.Length)
            {
                if (d[p] != 0xFF)
                {
                    p++;
                    continue;
                }
                byte marker = d[p + 1];
                if (marker == 0xFF)
                {
                    p++;
                    continue;
                }
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    p += 2;
                    continue;
                }
                if (marker == 0xD9 || marker == 0xDA) break; // EOI / SOS
                int length = (d[p + 2] << 8) | d[p + 3];
                int seg = p + 4;
                if (length < 2 || p + 2 + length > d.Length) break;

                bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof && length >= 8)
                {
                    info.Height = (d[seg + 1] << 8) | d[seg + 2];
                    info.Width = (d[seg + 3] << 8) | d[seg + 4];
                    info.Components = d[seg + 5];
                    sofFound = true;
                }
                else if (marker == 0xE0 && length >= 16 && d[seg] == 'J' && d[seg + 1] == 'F' && d[seg + 2] == 'I' && d[seg + 3] == 'F' && d[seg + 4] == 0)
                {
                    int units = d[seg + 7];
                    int xd = (d[seg + 8] << 8) | d[seg + 9];
                    int yd = (d[seg + 10] << 8) | d[seg + 11];
                    if (units == 1 && xd > 0 && yd > 0)
                    {
                        info.DpiX = xd;
                        info.DpiY = yd;
                    }
                    else if (units == 2 && xd > 0 && yd > 0)
                    {
                        info.DpiX = xd * 2.54;
                        info.DpiY = yd * 2.54;
                    }
                }
                else if (marker == 0xEE && length >= 12 && d[seg] == 'A' && d[seg + 1] == 'd' && d[seg + 2] == 'o' && d[seg + 3] == 'b' && d[seg + 4] == 'e')
                {
                    info.Adobe = true;
                }
                p += 2 + length;
            }
            if (!sofFound || info.Width <= 0 || info.Height <= 0)
                throw new InvalidDataException("JPEG のヘッダ（SOF）を読み取れません。");
            if (info.Components != 1 && info.Components != 3 && info.Components != 4)
                throw new NotSupportedException("JPEG の色成分数 " + info.Components + " には対応していません。");
            return info;
        }
    }
}
