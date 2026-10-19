using System;
using System.IO;
using System.IO.Compression;

namespace ChohyoPdf.Internal
{
    /// <summary>
    /// zlib (RFC 1950) 形式の圧縮・展開。.NET Framework の DeflateStream は raw deflate しか扱わないため、
    /// ヘッダと Adler-32 を自前で付け外しする。
    /// </summary>
    internal static class Zlib
    {
        public static byte[] Compress(byte[] data) => Compress(data, 0, data.Length);

        public static byte[] Compress(byte[] data, int offset, int count)
        {
            using (var output = new MemoryStream(count / 2 + 64))
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9C);
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(data, offset, count);
                }
                uint adler = Adler32(data, offset, count);
                output.WriteByte((byte)(adler >> 24));
                output.WriteByte((byte)(adler >> 16));
                output.WriteByte((byte)(adler >> 8));
                output.WriteByte((byte)adler);
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            if (data.Length < 2) throw new InvalidDataException("zlib データが短すぎます。");
            int cmf = data[0];
            int flg = data[1];
            if ((cmf & 0x0F) != 8 || ((cmf << 8) | flg) % 31 != 0)
                throw new InvalidDataException("zlib ヘッダが不正です。");
            int start = 2 + ((flg & 0x20) != 0 ? 4 : 0);
            using (var input = new MemoryStream(data, start, data.Length - start))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(data.Length * 4))
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        public static uint Adler32(byte[] data, int offset, int count)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int end = offset + count;
            int i = offset;
            while (i < end)
            {
                int chunk = Math.Min(5552, end - i);
                for (int j = 0; j < chunk; j++)
                {
                    a += data[i++];
                    b += a;
                }
                a %= Mod;
                b %= Mod;
            }
            return (b << 16) | a;
        }
    }
}
