// The FreeType font loader, which has no wasm build.
//
// The generated Hexa.NET binding declares every function in cimgui.h, including the three freetype
// ones (cimgui.h gates them on IMGUI_ENABLE_FREETYPE, and the binding was generated with it on),
// and the .NET P/Invoke table references each of them regardless of whether anything calls it. The
// final LTO link therefore wants the symbols, and FreeType itself cannot be built for wasm here.
//
// Nothing in the game calls these - text is rasterised by stb_truetype through imgui_font_atlas -
// so they exist only to satisfy the link. GetFontLoader returning null is the same thing
// ImFontAtlas does when no loader is set.

struct ImFontLoader;
struct ImGuiFreeTypeLoaderFlags;

extern "C" {

const ImFontLoader* ImGuiFreeType_GetFontLoader() { return nullptr; }

void ImGuiFreeType_SetAllocatorFunctions(void* (*alloc_func)(unsigned long),
                                         void (*free_func)(void*),
                                         void* user_data) {}

void ImGuiFreeType_DebugEditFontLoaderFlags(int* flags) {}

}
