// LLM maintained.
//
// The context's extension list, for Silk.NET's IsExtensionSupported.
//
// WebGL2 has no glGetStringi, so the GL way of enumerating extensions is unavailable; the browser
// knows them, through the same call the GL API would have reported. The shim exposes that as
// glGetStringi, so this reads it through GL rather than adding a second JS binding - one list, one
// place it can be wrong. Cached as a set because Silk.NET asks about several by name during GetApi.

#nullable enable
using System.Runtime.InteropServices;

namespace NFMWorld.Platform.WebGL;

internal static class WebGlExtensions
{
    private static HashSet<string>? _supported;

    internal static bool IsSupported(string name)
    {
        _supported ??= ReadAll();
        return _supported.Contains(name);
    }

    private static HashSet<string> ReadAll()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        // Walk until the shim reports no entry.
        for (var i = 0u; i < 4096; i++)
        {
            var raw = GlNativeContext.GetExtensionName(i);
            if (raw == IntPtr.Zero)
                break;

            var name = Marshal.PtrToStringUTF8(raw);
            if (string.IsNullOrEmpty(name))
                break;

            set.Add(name);
        }

        return set;
    }
}
