using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TightPack;

/// <summary>
/// Size helpers for generated code. This is the only place the library asks a question whose answer
/// is a compile-time constant; <see cref="Unsafe.SizeOf{T}"/> performs no memory access, so it cannot
/// read out of bounds. Nothing here reinterprets memory.
/// </summary>
public static class TightPackBlit
{
    /// <summary>The runtime size of <typeparamref name="T"/>, cached per closed type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T>()
        where T : unmanaged => SizeCache<T>.Value;

    /// <summary>
    /// Fails when the generator's padding-free proof for <typeparamref name="T"/> does not match the
    /// runtime. Generated blitting code calls this from its static constructor, so a layout the
    /// analyzer got wrong surfaces as an exception instead of as uninitialised bytes on the wire.
    /// </summary>
    public static bool VerifySize<T>(int expected)
        where T : unmanaged
    {
        int actual = SizeOf<T>();
        if (actual != expected)
        {
            TightPackSerializationException.ThrowInvalidLayout(typeof(T), expected, actual);
        }

        return true;
    }

    private static class SizeCache<T>
        where T : unmanaged
    {
        public static readonly int Value = Unsafe.SizeOf<T>();
    }
}

/// <summary>
/// Reinterprets a span of <typeparamref name="T"/> as bytes. <see cref="System.Runtime.InteropServices.MemoryMarshal.AsBytes{T}(Span{T})"/>
/// validates that <typeparamref name="T"/> has no references and that the spans are large enough for
/// the conversion, so no bounds arithmetic happens here.
/// </summary>
public static class TightPackSpan
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsBytes<T>(Span<T> values)
        where T : struct =>
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsBytes<T>(ReadOnlySpan<T> values)
        where T : struct =>
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(values);

    /// <summary>
    /// Interprets <paramref name="bytes"/> as <typeparamref name="T"/> elements. The span length must
    /// be an exact multiple of the element size; <see cref="System.Runtime.InteropServices.MemoryMarshal.Cast{TFrom,TTo}(Span{TFrom})"/>
    /// throws otherwise, so a truncated payload can never produce a partially readable element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsValues<T>(Span<byte> bytes)
        where T : struct =>
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsValues<T>(ReadOnlySpan<byte> bytes)
        where T : struct =>
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(bytes);
}
