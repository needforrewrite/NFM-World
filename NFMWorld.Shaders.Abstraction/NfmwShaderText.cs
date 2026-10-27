using System.IO.Compression;

namespace NFMWorld.Shaders;

/// <summary>
/// Inflates the compressed shader sources the generated bundles carry.
///
/// The compiler stores each source deflated rather than as a string literal: a file of shader
/// *variants* is many copies of one program, and string literals are counted against a
/// per-assembly limit that sixteen apos-shapes variants exceed. The generated code hands over the
/// bytes and caches the result itself, so nothing here holds state.
/// </summary>
public static class NfmwShaderText
{
    /// <summary>Decompresses one deflated shader source back to its text.</summary>
    public static string Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(deflate);
        return reader.ReadToEnd();
    }

    /// <summary>Forces the generated lazy and returns its value.</summary>
    public static string Get(Lazy<string> source) => source.Value;
}
