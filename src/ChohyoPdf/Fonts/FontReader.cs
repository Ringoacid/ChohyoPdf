using System;
using System.IO;

namespace ChohyoPdf.Fonts
{
    /// <summary>ビッグエンディアンのバイト列読み取り。</summary>
    internal sealed class FontReader
    {
        private readonly byte[] _data;

        public FontReader(byte[] data)
        {
            _data = data;
        }

        public byte[] Data => _data;

        public int Length => _data.Length;

        private void Check(int offset, int size)
        {
            if (offset < 0 || offset + size > _data.Length)
                throw new InvalidDataException("フォントデータが破損しています（範囲外の読み取り: " + offset + "）。");
        }

        public byte U8(int offset)
        {
            Check(offset, 1);
            return _data[offset];
        }

        public ushort U16(int offset)
        {
            Check(offset, 2);
            return (ushort)((_data[offset] << 8) | _data[offset + 1]);
        }

        public short S16(int offset) => unchecked((short)U16(offset));

        public uint U32(int offset)
        {
            Check(offset, 4);
            return ((uint)_data[offset] << 24) | ((uint)_data[offset + 1] << 16) | ((uint)_data[offset + 2] << 8) | _data[offset + 3];
        }

        public string Tag(int offset)
        {
            Check(offset, 4);
            return new string(new[] { (char)_data[offset], (char)_data[offset + 1], (char)_data[offset + 2], (char)_data[offset + 3] });
        }

        public byte[] Slice(int offset, int length)
        {
            Check(offset, length);
            var result = new byte[length];
            Buffer.BlockCopy(_data, offset, result, 0, length);
            return result;
        }
    }

    /// <summary>ビッグエンディアンのバイト列書き込み。</summary>
    internal sealed class FontWriter
    {
        private readonly MemoryStream _stream = new MemoryStream();

        public int Position => (int)_stream.Position;

        public void U8(int value) => _stream.WriteByte((byte)value);

        public void U16(int value)
        {
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void U32(uint value)
        {
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
        }

        public void Tag(string tag)
        {
            foreach (char c in tag) _stream.WriteByte((byte)c);
        }

        public void Bytes(byte[] data) => _stream.Write(data, 0, data.Length);

        public void Pad4()
        {
            while (_stream.Position % 4 != 0) _stream.WriteByte(0);
        }

        public byte[] ToArray() => _stream.ToArray();
    }
}
