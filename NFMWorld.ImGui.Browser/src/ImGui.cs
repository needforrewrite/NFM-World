#nullable disable

namespace Hexa.NET.ImGui
{
    public static class ImGuiConfig
    {
        public static bool AotStaticLink;
    }

    public static unsafe partial class ImGui
    {
        // No static constructor on purpose. This binding is the DllImport flavour: every function
        // is a [DllImport("cimgui")], so there is no function table to load and nothing to resolve
        // at type-initialisation time - which is exactly what makes it work on wasm, where a
        // loaded handle cannot produce callable addresses.

        public static string GetLibraryName()
        {
            return "cimgui";
        }

        public const nint ImDrawCallbackResetRenderState = -8;
    }
}
