// LLM maintained.
//
// The browser's stand-in for the SDL/EGL context the desktop GL renderers get from the host.
//
// On desktop, WorldGame asks SDL for an ES 3.0 context, makes it current, and hands the
// present and swap-interval callbacks to the device. Here there is no SDL and no EGL: the
// context comes from emscripten's HTML5 API against a canvas, nothing can be swapped because
// the browser presents the canvas itself, and the swap interval is requestAnimationFrame's
// own rate. So this exposes the same three things the backend actually consumes -
// entry points, a present callback, a resize - over the WebGL2 shim.

#nullable enable
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace NFMWorld.Platform.WebGL;

/// <summary>An ES 3.0 (WebGL2) context on the page's canvas.</summary>
public static class WebGlContext
{
    private const string Selector = "#canvas";

    /// <summary>
    /// Creates the WebGL2 context, makes it current, and returns the entry-point source for
    /// <c>GL.GetApi</c>. Throws rather than returning a half-initialised context, since every
    /// caller needs all three pieces.
    /// </summary>
    public static INativeContext Create()
    {
        var result = ContextCreate(Selector);
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"Could not create a WebGL2 context on {Selector} (emscripten result {result}). " +
                "A browser that cannot give WebGL2 a context is one this build cannot run on.");
        }

        return GlNativeContext.Instance;
    }

    /// <summary>Resizes the canvas, and with it the drawable the context renders into.</summary>
    public static void Resize(int width, int height) => ContextResize(width, height);

    /// <summary>
    /// Ends the frame. The browser presents the canvas when the script yields, so unlike
    /// <c>SDL_GL_SwapWindow</c> there is nothing to hand over - this exists so the device's
    /// present callback has somewhere to point.
    /// </summary>
    public static void Present() => ContextSwap();

    /// <summary>
    /// The vertical sync state. WebGL has no swap interval: presentation is the browser's
    /// compositor, paced by requestAnimationFrame. Accepted and ignored rather than thrown on,
    /// because the setting is a user preference and a browser build is not the place to refuse it.
    /// </summary>
    public static void SetSwapInterval(int interval)
    {
    }

    [DllImport("webgl_shim", EntryPoint = "nfmw_context_create")]
    private static extern int ContextCreate([MarshalAs(UnmanagedType.LPUTF8Str)] string selector);

    [DllImport("webgl_shim", EntryPoint = "nfmw_context_resize")]
    private static extern int ContextResize(int width, int height);

    [DllImport("webgl_shim", EntryPoint = "nfmw_context_swap")]
    private static extern void ContextSwap();
}
