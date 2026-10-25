// LLM maintained.
//
// The smoke test's context host: a hidden SDL window with a 3.3 core context, and the device
// attached to it.
//
// This exists because desktop GL has no ownerless surface, unlike the ANGLE backend's EGL pbuffer -
// a core-profile context on Windows has to be built on a window, even when nothing is ever drawn to
// the screen. So "headless" here means a window that is never shown rather than no window at all.
//
// The context setup deliberately mirrors WorldGame.CreateGlContext rather than being a simpler
// variant: same profile, same version, same stencil request, same order. If this test made its
// context differently from the game, it would be measuring a driver configuration the game never
// runs - which for a backend whose whole purpose is a fair comparison would defeat the exercise.
extern alias SDL3New;

using SDL3New::SDL3;
using NFMWorld.Platform.SDL3;

namespace NFMWorld.Graphics.DesktopGL.Smoke;

/// <summary>
/// A hidden SDL window with a desktop GL 3.3 core context, and the device on it.
///
/// Disposal is the reverse of construction and both halves are needed: the device must go first
/// because every GL object it created belongs to the context, and the context must be destroyed
/// before the window it was made from.
/// </summary>
internal sealed class GlHost : IDisposable
{
    /// <summary><c>SDL_GL_CONTEXT_PROFILE_CORE</c>, as WorldGame's own <c>CoreProfile</c>.</summary>
    private const int CoreProfile = 0x0001;

    private readonly SdlWindow _window;
    private readonly IntPtr _context;

    public GlGraphicsDevice Device { get; }

    public GlHost(int width, int height)
    {
        if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
            throw new InvalidOperationException($"SDL_Init failed: {SDL.SDL_GetError()}");

        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, CoreProfile);

        // The stencil buffer the game asks for, and for the same reason: every one of NanoVG's nine
        // pipelines is a stencil pass and the UI is drawn into the default framebuffer. A test
        // context without one would pass while the stencil checks below silently did nothing.
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLAttr.SDL_GL_STENCIL_SIZE, 8);

        if (!SDL.SDL_GL_LoadLibrary(null))
            throw new InvalidOperationException($"SDL_GL_LoadLibrary failed: {SDL.SDL_GetError()}");

        // Hidden, so a test run does not put a window on somebody's screen - but created, because
        // the context below needs one. Named from the enum rather than as literals: 0x08 for hidden
        // and 0x02 for OpenGL, where a hand-written 0x20 would be RESIZABLE and produce a window
        // that SDL_GL_CreateContext rejects with "isn't an OpenGL window".
        _window = SdlWindow.Create(
            "NFM World DesktopGL smoke", width, height,
            extraFlagsRaw: (ulong)(SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN |
                                   SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL),
            resizable: false, highPixelDensity: false);

        _context = SDL.SDL_GL_CreateContext(_window.Handle);
        if (_context == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.SDL_GetError()}");

        // Nothing presents: the window is never shown, and every check reads pixels back through
        // glReadPixels rather than from the screen. That is why the present callback is null here
        // and why this test needs no main loop.
        Device = GlGraphicsDevice.Create(SDL.SDL_GL_GetProcAddress, width, height, present: null);
    }

    public void Dispose()
    {
        Device.Dispose();
        SDL.SDL_GL_DestroyContext(_context);
        _window.Dispose();
    }
}
