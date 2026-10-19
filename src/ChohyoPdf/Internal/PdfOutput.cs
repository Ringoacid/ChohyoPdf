using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChohyoPdf.Internal
{
    /// <summary>
    /// 間接オブジェクトを書き出し、相互参照表のためにバイトオフセットを記録する低レベルライター。
    /// </summary>
    internal sealed class PdfOutput
    {
        private readonly Stream _stream;
        private readonly Dictionary<int, long> _offsets = new Dictionary<int, long>();
        private long _position;
        private int _nextId = 1;

        public PdfOutput(Stream stream)
        {
            _stream = stream;
        }

        public int AllocateId() => _nextId++;

        public int ObjectCount => _nextId - 1;

        public void Write(string ascii)
        {
            var bytes = Encoding.ASCII.GetBytes(ascii);
            WriteBytes(bytes);
        }

        public void WriteBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
            _position += bytes.Length;
        }

        public void WriteHeader()
        {
            Write("%PDF-1.7\n");
            // バイナリファイルであることを示すコメント行
            WriteBytes(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });
        }

        /// <summary>辞書オブジェクトを書き出す。<paramref name="body"/> は "&lt;&lt; ... &gt;&gt;" などのオブジェクト本体。</summary>
        public void WriteObject(int id, string body)
        {
            BeginObject(id);
            Write(body);
            Write("\nendobj\n");
        }

        /// <summary>ストリームオブジェクトを書き出す。/Length は自動で付与する。</summary>
        public void WriteStreamObject(int id, string dictionaryEntries, byte[] data)
        {
            BeginObject(id);
            Write("<< " + dictionaryEntries + " /Length " + PdfFormat.Int(data.Length) + " >>\nstream\n");
            WriteBytes(data);
            Write("\nendstream\nendobj\n");
        }

        private void BeginObject(int id)
        {
            if (_offsets.ContainsKey(id)) throw new InvalidOperationException("オブジェクト " + id + " は既に書き出されています。");
            _offsets[id] = _position;
            Write(PdfFormat.Int(id) + " 0 obj\n");
        }

        public void WriteXrefAndTrailer(int rootId, int infoId, byte[] fileId)
        {
            long xrefOffset = _position;
            int size = _nextId;
            var sb = new StringBuilder();
            sb.Append("xref\n0 ").Append(size).Append('\n');
            sb.Append("0000000000 65535 f\r\n");
            for (int id = 1; id < size; id++)
            {
                if (!_offsets.TryGetValue(id, out long offset))
                    throw new InvalidOperationException("オブジェクト " + id + " が書き出されていません。");
                sb.Append(offset.ToString("0000000000", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
            }
            string idHex = PdfFormat.Hex(fileId);
            sb.Append("trailer\n<< /Size ").Append(size)
              .Append(" /Root ").Append(rootId).Append(" 0 R")
              .Append(" /Info ").Append(infoId).Append(" 0 R")
              .Append(" /ID [").Append(idHex).Append(' ').Append(idHex).Append("] >>\n");
            sb.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
            Write(sb.ToString());
        }
    }
}
