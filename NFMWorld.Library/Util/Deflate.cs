using System.Buffers;
using System.IO.Compression;

namespace NFMWorldLibrary.Util;

/// <summary>
/// Compresses a serialized payload with raw DEFLATE.
///
/// This replaces MemoryPack's <c>BrotliCompressor</c> for anything the browser has to read. Brotli
/// lives in <c>System.IO.Compression.Native</c>, and that library has no wasm build, so every Brotli
/// call on the browser throws <c>PlatformNotSupportedException</c> - which surfaced as time trials
/// that could be driven but never saved and never loaded. Deflate is pure managed code
/// (<c>System.IO.Compression.DeflateStream</c>), so it is the one compressor that works on every
/// target the game ships to.
///
/// The format therefore changed everywhere at once rather than only on the browser: a file written
/// by one platform has to be readable by another, and a per-platform choice would produce archives
/// that silently only half-work.
///
/// Raw deflate, without a zlib or gzip wrapper, because the caller's own header carries the framing
/// and a wrapper would add bytes nothing reads.
/// </summary>
internal static class Deflate
{
    /// <summary>Compresses <paramref name="source"/> and writes the raw deflate stream to <paramref name="writer"/>.</summary>
    internal static void Compress(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        using var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            stream.Write(source);
        writer.Write(output.GetBuffer().AsSpan(0, (int)output.Length));
    }

    /// <summary>Compresses and returns the bytes, for callers that are not already holding a writer.</summary>
    internal static byte[] Compress(ReadOnlySpan<byte> source)
    {
        using var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            stream.Write(source);
        return output.ToArray();
    }

    /// <summary>Decompresses a raw deflate stream.</summary>
    internal static byte[] Decompress(ReadOnlySpan<byte> source)
    {
        using var input = new MemoryStream(source.ToArray(), writable: false);
        using var stream = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Decompresses a raw deflate stream that arrives as a read-only sequence.</summary>
    internal static byte[] Decompress(in ReadOnlySequence<byte> source) => Decompress(source.ToArray());
}
