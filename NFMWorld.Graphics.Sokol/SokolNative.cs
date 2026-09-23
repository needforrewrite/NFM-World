using System.Text;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// Marshalling helpers shared by the sokol backend. sokol's C API takes <c>const char*</c>
/// strings and <c>sg_range</c> views over caller-owned memory, both of which have to stay
/// pinned for the duration of the call - these wrap that so the individual call sites don't
/// each re-derive it.
/// </summary>
internal static unsafe class SokolNative
{
    /// <summary>
    /// UTF-8 bytes of <paramref name="value"/> with a trailing NUL, ready to be pinned and
    /// passed as a <c>const char*</c>. Returns an array rather than a pointer because the
    /// caller must keep it alive across the call (see the <c>fixed</c> blocks at call sites).
    /// </summary>
    internal static byte[] Utf8Z(string value)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(value) + 1];
        Encoding.UTF8.GetBytes(value, bytes);
        // The final byte is left as 0 by the array initializer - that's the NUL.
        return bytes;
    }

    /// <summary>An <c>sg_range</c> over already-pinned memory.</summary>
    internal static sg_range Range(void* pointer, int size) => new()
    {
        ptr = pointer,
        size = (nuint)size,
    };

    /// <summary>
    /// sokol's <c>_Bool</c> fields marshal as a single byte. Note this is NOT the same as
    /// C#'s <c>bool</c>, which is also one byte but is not blittable in a struct field by
    /// default - the binding declares every flag as <c>byte</c> for that reason.
    /// </summary>
    internal static byte Bool(bool value) => value ? (byte)1 : (byte)0;
}
