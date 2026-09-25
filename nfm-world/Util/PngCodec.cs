/*
https://github.com/winscripter/TheLightestPNG

MIT License

Copyright (c) [year] [fullname]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

using System.IO.Compression;
using System.IO.Hashing;
using System.Text;

namespace NFMWorld.Util;

public static class PngCodec
{
    public static void Encode(Stream output, ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        using var writer = new BinaryWriter(output);
        writer.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        using (var ms = new MemoryStream())
        using (var chunkWriter = new BinaryWriter(ms))
        {
            chunkWriter.Write(ToBigEndian(width));
            chunkWriter.Write(ToBigEndian(height));
            chunkWriter.Write((byte)8);
            chunkWriter.Write((byte)6);
            chunkWriter.Write((byte)0);
            chunkWriter.Write((byte)0);
            chunkWriter.Write((byte)0);
            WriteChunk(writer, "IHDR", ms);
        }

        var stride = width * 4;
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgbaPixels.Slice(y * stride, stride));
        }

        using var compressed = new MemoryStream();
        using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, true))
        {
            raw.Position = 0;
            raw.CopyTo(deflate);
        }

        WriteChunk(writer, "IDAT", compressed);
        WriteChunk(writer, "IEND", []);
    }

    private static void WriteChunk(BinaryWriter writer, string type, MemoryStream data)
    {
        writer.Write(ToBigEndian((int)data.Length));
        writer.Write(Encoding.ASCII.GetBytes(type));
        data.Position = 0;
        data.CopyTo(writer.BaseStream);
        writer.Write(ToBigEndian(Crc32(type, data.ToArray())));
    }

    private static void WriteChunk(BinaryWriter writer, string type, byte[] data)
    {
        writer.Write(ToBigEndian(data.Length));
        writer.Write(Encoding.ASCII.GetBytes(type));
        writer.Write(data);
        writer.Write(ToBigEndian(Crc32(type, data)));
    }

    private static byte[] ToBigEndian(int value) =>
        BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value));

    private static byte[] ToBigEndian(uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    private static uint Crc32(string type, byte[] data)
    {
        var crc32 = new Crc32();
        crc32.Append(Encoding.ASCII.GetBytes(type));
        crc32.Append(data);
        return BitConverter.ToUInt32(crc32.GetCurrentHash(), 0);
    }
}