// LLM maintained.
//
// The EGL context: a thin wrapper over the Maxine.Silk.EGL bindings, which supply both the EGL
// entry points and (through Maxine.Silk.OpenGLES.ANGLE.Native) the ANGLE libraries underneath them.
//
// This file used to be a hand-written EGL binding, because Silk.NET 2.x ships none. That is still
// true of Silk.NET itself, but no longer true here: Maxine.Silk.EGL is a Silk.NET-style EGL package
// built over the same Khronos registry with Silk.NET.BuildTools + SilkTouch, so EGL.GetApi() returns
// an object with every entry point this backend needs already declared and typed.
//
// Two things that were hand-rolled before are now handled by the bindings and are worth stating,
// because the old code's whole shape was a workaround for their absence:
//
// 1. Finding an ANGLE. Maxine.Silk.OpenGLES.ANGLE.Native is referenced as a package, so its targets
//    copy libEGL/libGLESv2 for the current RID next to the executable, and Silk.NET's loader finds
//    them by bare name. Nothing searches directories, nothing sets an environment variable, and
//    nothing reads a PE header: the previous implementation did all three to work around
//    Silk.NET.OpenGLES.ANGLE.Native's win-x64 binaries being 32-bit, which Maxine's are not.
//
// 2. Resolving GL entry points. ANGLE's libEGL exports only egl* (115 of them) and its libGLESv2
//    exports only gl* (826); eglGetProcAddress lives on the EGL side. A GL loader therefore has to
//    consult both, which is what MultiNativeContext is for. The previous GlEntryPoints class did
//    the same two-step lookup by hand.
//
// The remaining hand-rolled part is the platform-display attributes, and that is a limitation of
// the registry rather than of the bindings: EGL_PLATFORM_ANGLE_* is defined in ANGLE's own
// eglext_angle.h and appears nowhere in the Khronos EGL registry the bindings generate from, so no
// amount of regeneration can produce these constants. They are declared below, with their values.
using Maxine.EGL;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

internal static class Egl
{
    /// <summary>
    /// ANGLE's platform-selection tokens, from <c>eglext_angle.h</c>.
    ///
    /// These are absent from the Khronos EGL registry the bindings are generated from - that
    /// registry carries only <c>EGL_ANGLE_query_surface_pointer</c>, <c>_sync_control_rate</c> and
    /// <c>_window_fixed_size</c>, plus the D3D device/shared-handle tokens. ANGLE's platform
    /// selection extension is documented in ANGLE's own repository instead, so its tokens have to be
    /// spelled out here. The values are fixed by the extension and are what ANGLE matches on.
    ///
    /// Internal rather than private because <see cref="AngleSelection"/> maps its enum members onto
    /// these, so that there is exactly one copy of each number in the project. The names are
    /// ANGLE's, spelled out in full, because a shorter scheme is how the bug these replaced stayed
    /// invisible: the previous code passed <c>0x33AE</c> as the platform type, and <c>0x33AE</c> is
    /// <see cref="EglPlatformAngleTypeNull"/> - ANGLE's null renderer.
    /// </summary>
    internal const int EglPlatformAngleAngle = 0x3202;
    internal const int EglPlatformAngleTypeAngle = 0x3203;
    internal const int EglPlatformAngleTypeDefault = 0x3206;
    internal const int EglPlatformAngleTypeD3d11 = 0x3208;
    internal const int EglPlatformAngleTypeOpenGl = 0x320D;
    internal const int EglPlatformAngleTypeOpenGles = 0x320E;
    internal const int EglPlatformAngleTypeVulkan = 0x3450;
    internal const int EglPlatformAngleTypeMetal = 0x3489;
    internal const int EglPlatformAngleTypeNull = 0x33AE;
    internal const int EglPlatformAngleDeviceTypeAngle = 0x3209;
    internal const int EglPlatformAngleDeviceTypeHardware = 0x320A;
    internal const int EglPlatformAngleDeviceTypeD3dWarp = 0x320B;
    internal const int EglPlatformAngleDeviceTypeD3dReference = 0x320C;
    internal const int EglPlatformAngleDeviceTypeNull = 0x345E;
    internal const int EglPlatformAngleDeviceTypeSwiftShader = 0x3487;

    /// <summary>
    /// <c>EGL_NONE</c>, the terminator for every EGL attribute list.
    ///
    /// Spelled out here rather than reached for as <c>(int)EGLEnum.None</c> because
    /// <see cref="AngleSelection.PlatformAttributes"/> is public and <c>EGLEnum</c> is a Maxine
    /// binding type - widening the surface just for a terminator would be worse than one more
    /// constant in the block that already exists to hold numbers the bindings cannot supply.
    /// </summary>
    internal const int EglNone = 0x3038;

    /// <summary>
    /// A live EGL display, context and draw surface. Disposing unbinds the context and destroys all
    /// three, so a headless device can be torn down without leaking the D3D11 device ANGLE created
    /// underneath.
    /// </summary>
    internal sealed class Context : IDisposable
    {
        private readonly EGL _egl;

        /// <summary>
        /// The GL entry-point source handed to <c>GL.GetApi</c>. libEGL serves the egl* calls and
        /// libGLESv2 the gl* ones, so both are consulted - see the note in the file header.
        /// </summary>
        private readonly INativeContext _glContext;

        internal nint Display { get; private init; }
        internal nint Handle { get; private init; }

        /// <summary>
        /// The config the context and surface were created from. Kept because a resize has to build
        /// a new surface, and EGL requires the same config for it.
        /// </summary>
        internal nint Config { get; private init; }

        /// <summary>The draw/read surface. Mutable because <see cref="ResizeSurface"/> replaces it.</summary>
        internal nint Surface { get; private set; }

        /// <summary>The vendor string, e.g. "Google Inc." for ANGLE - reported so a silent fall back to a different EGL implementation is visible.</summary>
        internal string Vendor { get; private init; } = "";

        internal string Version { get; private init; } = "";

        /// <summary>The GL entry-point source for <c>GL.GetApi</c>.</summary>
        internal INativeContext GlContext => _glContext;

        /// <summary>
        /// Where the ANGLE that actually loaded lives, for the smoke test to report.
        ///
        /// Read back off the loaded modules rather than remembered from a directory scan, so what is
        /// reported is what ran. This matters because the package that used to be referenced placed
        /// a 32-bit decoy in its win-x64 folder; this one does not, and the path is how a reader can
        /// confirm which build is in play without taking it on trust.
        /// </summary>
        internal string LoadedFrom
        {
            get
            {
                var modules = System.Diagnostics.Process.GetCurrentProcess().Modules;
                foreach (System.Diagnostics.ProcessModule module in modules)
                {
                    var name = module.FileName;
                    if (name.EndsWith("libEGL.dll", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("libEGL.so", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith("libEGL.dylib", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }

                return "<unknown>";
            }
        }

        private Context(EGL egl, INativeContext glContext)
        {
            _egl = egl;
            _glContext = glContext;
        }

        /// <summary>
        /// Brings up a headless ES 3.0 context on ANGLE's <paramref name="selection"/> backend, with
        /// a pbuffer surface sized to <paramref name="width"/>x<paramref name="height"/>.
        ///
        /// The platform display is requested explicitly - see <see cref="GetAngleDisplay"/>, which
        /// is what makes the backend deterministic.
        ///
        /// The call goes through <c>eglGetPlatformDisplay</c> (EGL 1.5 core, which the bindings
        /// declare) rather than <c>eglGetPlatformDisplayEXT</c>: the core entry point takes the same
        /// platform enum and attributes, and ANGLE exports both.
        /// </summary>
        /// <param name="selection">Which ANGLE backend and device to ask for. Defaults to D3D11 on hardware, which is the backend this path measured before the choice was selectable.</param>
        internal static Context CreateHeadless(int width, int height, AngleSelection selection = default)
        {
            // A default-constructed struct has both members at their zero values, which is D3d11 and
            // Hardware - so the documented default holds without a second overload.
            if (selection == default)
                selection = AngleSelection.Default;

            var egl = EGL.GetApi();

            // libGLESv2 supplies the gl* symbols that libEGL does not export. Resolved by bare name
            // for the same reason EGL.GetApi() is: the native package puts both next to the
            // executable, so Silk.NET's loader finds them on its own.
            var gles = new DefaultNativeContext("libGLESv2.dll");
            var glContext = new MultiNativeContext(egl.Context, gles);

            try
            {
                var display = GetAngleDisplay(egl, selection);
                if (display == 0)
                    throw new InvalidOperationException(
                        $"eglGetPlatformDisplay returned no display for {selection.Describe()} " +
                        $"(EGL error {ErrorText(egl)}).");

                if (!egl.Initialize(display, out var major, out var minor))
                    throw new InvalidOperationException(
                        $"eglInitialize failed for {selection.Describe()} (EGL error {ErrorText(egl)}). " +
                        "This is what an unavailable backend looks like: ANGLE's Vulkan platform needs " +
                        "its own ICD, and its OpenGL/GLES platform needs a display to attach to, so a " +
                        "selection that does not exist on this machine fails here rather than falling " +
                        "back to one that does.");

                // The ES3 config. A pbuffer is requested because this context is headless: there is
                // no window for a window surface to attach to.
                ReadOnlySpan<int> configAttribs =
                [
                    (int)EGLEnum.SurfaceType, (int)EGLEnum.PbufferBit,
                    (int)EGLEnum.RenderableType, (int)EGLEnum.OpenglES3Bit,
                    (int)EGLEnum.RedSize, 8,
                    (int)EGLEnum.GreenSize, 8,
                    (int)EGLEnum.BlueSize, 8,
                    (int)EGLEnum.AlphaSize, 8,
                    (int)EGLEnum.DepthSize, 24,
                    (int)EGLEnum.None
                ];

                Span<nint> configs = stackalloc nint[1];
                Span<int> configCount = stackalloc int[1];
                if (!egl.ChooseConfig(display, configAttribs, configs, 1, configCount) || configCount[0] < 1)
                    throw new InvalidOperationException($"eglChooseConfig found no ES3 config (EGL error {ErrorText(egl)}).");
                var config = configs[0];

                var context = egl.CreateContext(display, config, 0, [(int)EGLEnum.ContextClientVersion, 3, (int)EGLEnum.None]);
                if (context == 0)
                    throw new InvalidOperationException($"eglCreateContext failed (EGL error {ErrorText(egl)}).");

                var surface = egl.CreatePbufferSurface(display, config, [(int)EGLEnum.Width, width, (int)EGLEnum.Height, height, (int)EGLEnum.None]);
                if (surface == 0)
                    throw new InvalidOperationException($"eglCreatePbufferSurface failed (EGL error {ErrorText(egl)}).");

                var result = new Context(egl, glContext)
                {
                    Display = display,
                    Handle = context,
                    Config = config,
                    Surface = surface,
                    Vendor = egl.QueryStringS(display, (int)EGLEnum.Vendor),
                    Version = $"{major}.{minor}",
                };

                result.MakeCurrent();
                return result;
            }
            catch
            {
                // The same single-owner rule as Dispose: releasing the multi-context releases the
                // contexts it was built over. Nothing else is disposed here, or the handle that was
                // already freed would be freed again.
                glContext.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Asks for ANGLE on <paramref name="selection"/>'s platform and device, through EGL 1.5's
        /// core <c>eglGetPlatformDisplay</c>.
        ///
        /// The attribute list is the platform-selection extension's, which is why its tokens are
        /// declared by hand - see the file header. The values come from
        /// <see cref="AngleSelection.PlatformAttributes"/> rather than being built here, so that the
        /// game's <c>--angle-backend=</c> flag and this headless path describe the same request.
        ///
        /// <c>native_display</c> is null because the ANGLE platform has no underlying native display
        /// to name: it creates the D3D device itself. The other overloads would pass a pointer to a
        /// local instead, which is not the same thing, so the null is spelled out.
        /// </summary>
        /// <remarks>
        /// Requesting the platform explicitly is what makes the backend deterministic. Without it a
        /// machine whose default is D3D9 or a software rasterizer would still come up, and every
        /// measurement or pixel this path produces would silently describe a different renderer.
        /// Note the parameter list does not default to D3D11 by accident: what this method used to
        /// pass as the platform type was <c>0x33AE</c>, ANGLE's null backend, which is the concrete
        /// form of exactly that failure.
        /// <para>
        /// The entry point is named rather than reached through Silk's extension mechanism, which is
        /// a second, independent reason this path did not work: <c>EGL.TryGetExtension</c> and
        /// <c>EGL.IsExtensionPresent</c> are hand-written stubs in Maxine.Silk.EGL that throw
        /// <c>NotImplementedException</c> outright (src/Maxine.EGL/EGL.cs:23-32), so the previous
        /// <c>TryGetExtension(out ExtPlatformBase)</c> threw before any EGL call was made. The
        /// generated core binding is declared and works, so it is what is used.
        /// </para>
        /// </remarks>
        internal static unsafe nint GetAngleDisplay(EGL egl, AngleSelection selection)
        {
            var attribs = selection.PlatformAttributes();

            // The core EGL 1.5 entry point, not eglGetPlatformDisplayEXT. That is a working-around
            // rather than a preference: Silk's extension lookup - EGL.TryGetExtension and
            // EGL.IsExtensionPresent - is a hand-written stub in Maxine.Silk.EGL that throws
            // NotImplementedException outright (src/Maxine.EGL/EGL.cs:23-32), so the EXT route cannot
            // be taken at all, and the extension type it would return has no other public way to be
            // built. Reaching the same entry point by name needs neither: ANGLE exports
            // eglGetPlatformDisplay and the bindings declare it, with an identical platform enum and
            // attribute list.
            fixed (nint* pinned = attribs)
            {
                return egl.GetPlatformDisplay((EGLEnum)EglPlatformAngleAngle, null, pinned);
            }
        }

        /// <summary>
        /// Replaces the pbuffer with one of a new size.
        ///
        /// The order matters: the current binding has to be released before the old surface is
        /// destroyed (EGL leaves the context unbound to a destroyed surface, and behaviour after that
        /// is implementation-defined), and the new surface has to be made current before any GL call,
        /// or every subsequent draw targets nothing.
        ///
        /// The config is reused rather than re-chosen, because EGL requires a surface to be created
        /// from the same config as the context that renders into it.
        /// </summary>
        internal void ResizeSurface(int width, int height)
        {
            ReleaseCurrent();

            if (Surface != 0)
                _egl.DestroySurface(Display, Surface);

            var surface = _egl.CreatePbufferSurface(Display, Config,
            [(int)EGLEnum.Width, width, (int)EGLEnum.Height, height, (int)EGLEnum.None]);

            if (surface == 0)
                throw new InvalidOperationException(
                    $"eglCreatePbufferSurface failed for {width}x{height} (EGL error {ErrorText(_egl)}).");

            Surface = surface;
            MakeCurrent();
        }

        internal void MakeCurrent() => _egl.MakeCurrent(Display, Surface, Surface, Handle);

        internal void ReleaseCurrent() => _egl.MakeCurrent(Display, 0, 0, 0);

        internal void SwapBuffers() => _egl.SwapBuffers(Display, Surface);

        public void Dispose()
        {
            // EGL teardown first, while the loader is still standing: after the native contexts are
            // released there is no way to call eglTerminate at all.
            ReleaseCurrent();
            if (Surface != 0) _egl.DestroySurface(Display, Surface);
            if (Handle != 0) _egl.DestroyContext(Display, Handle);
            if (Display != 0) _egl.Terminate(Display);

            // Then the native contexts, and only through _glContext. MultiNativeContext takes
            // ownership of the contexts it is constructed over, so disposing it frees the EGL
            // container's loader too - calling _egl.Dispose() as well would free the same handle
            // twice and throw from NativeLibrary.Free. Verified rather than assumed: disposing the
            // two in either order, or either one alone, is what established this.
            //
            // _egl itself is not disposed but is still the live API object until here, which is why
            // the egl* calls above go through it.
            _glContext.Dispose();
        }
    }

    /// <summary>EGL's own error code, formatted for a thrown message. 0x3000 is EGL_SUCCESS.</summary>
    private static string ErrorText(EGL egl) => $"0x{egl.GetError():X4}";
}
