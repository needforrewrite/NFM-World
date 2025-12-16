using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;
using NFMWorld.Util;
using Silk.NET.OpenGL;
using SkiaSharp;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;
using File = NFMWorld.Util.File;

namespace NFMWorld;

/*
The MIT License (MIT)

Copyright (c) 2013-2024 FlatRedBall, LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */
file class GlConstants
{
    public const int SDL_GL_SHARE_WITH_CURRENT_CONTEXT = 22;
    public const int GL_ALL_ATTRIB_BITS = 0xfffff;
    public const int GL_SAMPLES = 0x80a9;
    public const int GL_TEXTURE_BINDING_2D = 0x8069;

    internal enum RenderbufferTarget
    {
        Renderbuffer = 0x8D41,
        RenderbufferExt = 0x8D41,
    }

    internal enum FramebufferTarget
    {
        Framebuffer = 0x8D40,
        FramebufferExt = 0x8D40,
        ReadFramebuffer = 0x8CA8,
    }

    internal enum RenderbufferStorage
    {
        Rgba8 = 0x8058,
        DepthComponent16 = 0x81a5,
        DepthComponent24 = 0x81a6,
        Depth24Stencil8 = 0x88F0,
        // GLES Values
        DepthComponent24Oes = 0x81A6,
        Depth24Stencil8Oes = 0x88F0,
        StencilIndex8 = 0x8D48,
    }

    internal enum FramebufferAttachment
    {
        ColorAttachment0 = 0x8CE0,
        ColorAttachment0Ext = 0x8CE0,
        DepthAttachment = 0x8D00,
        StencilAttachment = 0x8D20,
        ColorAttachmentExt = 0x1800,
        DepthAttachementExt = 0x1801,
        StencilAttachmentExt = 0x1802,
    }

    internal enum TextureTarget
    {
        Texture2D = 0x0DE1,
        Texture3D = 0x806F,
        TextureCubeMap = 0x8513,
        TextureCubeMapPositiveX = 0x8515,
        TextureCubeMapPositiveY = 0x8517,
        TextureCubeMapPositiveZ = 0x8519,
        TextureCubeMapNegativeX = 0x8516,
        TextureCubeMapNegativeY = 0x8518,
        TextureCubeMapNegativeZ = 0x851A,
    }

    internal enum FramebufferErrorCode
    {
        FramebufferUndefined = 0x8219,
        FramebufferComplete = 0x8CD5,
        FramebufferCompleteExt = 0x8CD5,
        FramebufferIncompleteAttachment = 0x8CD6,
        FramebufferIncompleteAttachmentExt = 0x8CD6,
        FramebufferIncompleteMissingAttachment = 0x8CD7,
        FramebufferIncompleteMissingAttachmentExt = 0x8CD7,
        FramebufferIncompleteDimensionsExt = 0x8CD9,
        FramebufferIncompleteFormatsExt = 0x8CDA,
        FramebufferIncompleteDrawBuffer = 0x8CDB,
        FramebufferIncompleteDrawBufferExt = 0x8CDB,
        FramebufferIncompleteReadBuffer = 0x8CDC,
        FramebufferIncompleteReadBufferExt = 0x8CDC,
        FramebufferUnsupported = 0x8CDD,
        FramebufferUnsupportedExt = 0x8CDD,
        FramebufferIncompleteMultisample = 0x8D56,
        FramebufferIncompleteLayerTargets = 0x8DA8,
        FramebufferIncompleteLayerCount = 0x8DA9,
    }

    internal enum ErrorCode
    {
        NoError = 0,
    }
}

file static class GlWrapper
{
    private const CallingConvention callingConvention = CallingConvention.Winapi;

    // Native function attribute ported from MonoGame source
    [AttributeUsage(AttributeTargets.Delegate)]
    internal sealed class NativeFunctionWrapper : Attribute { }

    static FieldInfo _winHandleField;
    static PropertyInfo _contextProperty;

    static object _sdl_GL_GetCurrentContextValue;
    static MethodInfo _sdl_GL_GetCurrentContextMethod;

    static object _sdl_GL_CreateContextValue;
    static MethodInfo _sdl_GL_CreateContextMethod;

    static object _sdl_GL_SetAttributeValue;
    static MethodInfo _sdl_GL_SetAttributeMethod;

    static object _makeCurrentValue;
    static MethodInfo _makeCurrentMethod;

    static MethodInfo _loadFunctionMethod;
    
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "MonoGame.OpenGL.GL", "MonoGame.Framework")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Sdl", "MonoGame.Framework")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Sdl+GL", "MonoGame.Framework")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "MonoGame.OpenGL.GraphicsContext", "MonoGame.Framework")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Microsoft.Xna.Framework.Graphics.GraphicsDevice", "MonoGame.Framework")]
    static GlWrapper()
    {
        var monoGameAssembly = typeof(Texture2D).Assembly;
        var sdlGlType = monoGameAssembly.GetType("Sdl").GetNestedType("GL");
        var mgGlType = monoGameAssembly.GetType("MonoGame.OpenGL.GL");

        _winHandleField = monoGameAssembly.GetType("MonoGame.OpenGL.GraphicsContext").GetField("_winHandle", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new Exception("Could not find _winHandle field in GraphicsContext.");
        _contextProperty = monoGameAssembly.GetType("Microsoft.Xna.Framework.Graphics.GraphicsDevice").GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Could not find Context property in GraphicsDevice.");

        var sdl_GL_GetCurrentContextField = sdlGlType.GetField("SDL_GL_GetCurrentContext", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("Could not find SDL_GL_GetCurrentContext field in Sdl.GL.");
        _sdl_GL_GetCurrentContextValue = sdl_GL_GetCurrentContextField.GetValue(null)
            ?? throw new Exception("SDL_GL_GetCurrentContext field value is null.");
        _sdl_GL_GetCurrentContextMethod = _sdl_GL_GetCurrentContextValue.GetType().GetMethod("Invoke")
            ?? throw new Exception("Could not find Invoke method in SDL_GL_GetCurrentContext delegate.");

        var sdl_GL_CreateContextField = sdlGlType.GetField("SDL_GL_CreateContext", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("Could not find SDL_GL_CreateContext field in Sdl.GL.");
        _sdl_GL_CreateContextValue = sdl_GL_CreateContextField.GetValue(null)
            ?? throw new Exception("SDL_GL_CreateContext field value is null.");
        _sdl_GL_CreateContextMethod = _sdl_GL_CreateContextValue.GetType().GetMethod("Invoke")
            ?? throw new Exception("Could not find Invoke method in SDL_GL_CreateContext delegate.");

        var sdl_GL_SetAttributeField = sdlGlType.GetField("SDL_GL_SetAttribute", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("Could not find SDL_GL_SetAttribute field in Sdl.GL.");
        _sdl_GL_SetAttributeValue = sdl_GL_SetAttributeField.GetValue(null)
            ?? throw new Exception("SDL_GL_SetAttribute field value is null.");
        _sdl_GL_SetAttributeMethod = _sdl_GL_SetAttributeValue.GetType().GetMethod("Invoke")
            ?? throw new Exception("Could not find Invoke method in SDL_GL_SetAttribute delegate.");

        var makeCurrentField = sdlGlType.GetField("MakeCurrent", BindingFlags.Public | BindingFlags.Static)
            ?? throw new Exception("Could not find MakeCurrent field in Sdl.GL.");
        _makeCurrentValue = makeCurrentField.GetValue(null)
            ?? throw new Exception("MakeCurrent field value is null.");
        _makeCurrentMethod = _makeCurrentValue.GetType().GetMethod("Invoke")
            ?? throw new Exception("Could not find Invoke method in MakeCurrent delegate.");

        _loadFunctionMethod = mgGlType.GetMethod("LoadFunction", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("Could not find LoadFunction method in MonoGame.OpenGL.GL.");
    }

    internal static IntPtr GetMgWindowId()
    {
        var context = _contextProperty.GetValue(SkiaGlManager.GraphicsDevice);
        return (IntPtr)_winHandleField.GetValue(context)!;
    }

    internal static IntPtr SDL_GL_GetCurrentContext()
    {
        return (IntPtr)_sdl_GL_GetCurrentContextMethod.Invoke(_sdl_GL_GetCurrentContextValue, null)!;
    }

    internal static IntPtr SDL_GL_CreateContext(IntPtr window)
    {
        return (IntPtr)_sdl_GL_CreateContextMethod.Invoke(_sdl_GL_CreateContextValue, new object[] { window })!;
    }

    internal static int SDL_GL_SetAttribute(int attribute, int value)
    {
        return (int)_sdl_GL_SetAttributeMethod.Invoke(_sdl_GL_SetAttributeValue, new object[] { attribute, value })!;
    }

    // This allocates a little, we can make it a little quieter by reusing this object array:
    static object[] makeCurrentArray = new object[2];
    internal static int MakeCurrent(IntPtr window, IntPtr context)
    {
        makeCurrentArray[0] = window;
        makeCurrentArray[1] = context;
        return (int)_makeCurrentMethod.Invoke(_makeCurrentValue, makeCurrentArray);
    }

    internal static T LoadFunction<T>(string nativeMethodName)
    {
        var method = _loadFunctionMethod.MakeGenericMethod(typeof(T));
        return (T)method.Invoke(null, new object[] { nativeMethodName, false });
    }

    /// <summary>
    /// OpenGL functions wrapper for the MonoGame context.
    /// </summary>
    internal static class MgGlFunctions
    {
        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal unsafe delegate void GetIntegerDelegate(int param, [Out] int* data);
        internal static GetIntegerDelegate GetIntegerv;

        internal static void LoadFunctions()
        {
            GetIntegerv = LoadFunction<GetIntegerDelegate>("glGetIntegerv");
        }

        internal unsafe static void GetInteger(int name, out int value)
        {
            fixed (int* ptr = &value)
            {
                GetIntegerv(name, ptr);
            }
        }
    }

    /// <summary>
    /// OpenGL functions wrapper for the Skia context.
    /// </summary>
    internal static class SkGlFunctions
    {
        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void GenRenderbuffersDelegate(int count, [Out] out int buffer);
        internal static GenRenderbuffersDelegate GenRenderbuffers;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void BindRenderbufferDelegate(RenderbufferTarget target, int buffer);
        internal static BindRenderbufferDelegate BindRenderbuffer;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void DeleteRenderbuffersDelegate(int count, [In][Out] ref int buffer);
        internal static DeleteRenderbuffersDelegate DeleteRenderbuffers;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void GenFramebuffersDelegate(int count, out int buffer);
        internal static GenFramebuffersDelegate GenFramebuffers;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void BindFramebufferDelegate(FramebufferTarget target, int buffer);
        internal static BindFramebufferDelegate BindFramebuffer;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void DeleteFramebuffersDelegate(int count, ref int buffer);
        internal static DeleteFramebuffersDelegate DeleteFramebuffers;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        public delegate void InvalidateFramebufferDelegate(FramebufferTarget target, int numAttachments, FramebufferAttachment[] attachments);
        public static InvalidateFramebufferDelegate InvalidateFramebuffer;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void FramebufferTexture2DDelegate(FramebufferTarget target, FramebufferAttachment attachement,
            TextureTarget textureTarget, int texture, int level);
        internal static FramebufferTexture2DDelegate FramebufferTexture2D;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate void FramebufferRenderbufferDelegate(FramebufferTarget target, FramebufferAttachment attachement,
            RenderbufferTarget renderBufferTarget, int buffer);
        internal static FramebufferRenderbufferDelegate FramebufferRenderbuffer;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        public delegate void RenderbufferStorageDelegate(RenderbufferTarget target, GlConstants.RenderbufferStorage storage, int width, int hegiht);
        public static RenderbufferStorageDelegate RenderbufferStorage;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal delegate GlConstants.FramebufferErrorCode CheckFramebufferStatusDelegate(FramebufferTarget target);
        internal static CheckFramebufferStatusDelegate CheckFramebufferStatus;

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [UnmanagedFunctionPointer(callingConvention)]
        [NativeFunctionWrapper]
        internal unsafe delegate void GetIntegerDelegate(int param, [Out] int* data);
        internal static GetIntegerDelegate GetIntegerv;

        internal static readonly FramebufferAttachment[] FramebufferAttachements = {
            FramebufferAttachment.ColorAttachment0,
            FramebufferAttachment.DepthAttachment,
            FramebufferAttachment.StencilAttachment,
        };

        internal static void LoadFunctions()
        {
            GenRenderbuffers = LoadFunction<GenRenderbuffersDelegate>("glGenRenderbuffers");
            BindRenderbuffer = LoadFunction<BindRenderbufferDelegate>("glBindRenderbuffer");
            DeleteRenderbuffers = LoadFunction<DeleteRenderbuffersDelegate>("glDeleteRenderbuffers");
            GenFramebuffers = LoadFunction<GenFramebuffersDelegate>("glGenFramebuffers");
            BindFramebuffer = LoadFunction<BindFramebufferDelegate>("glBindFramebuffer");
            DeleteFramebuffers = LoadFunction<DeleteFramebuffersDelegate>("glDeleteFramebuffers");
            InvalidateFramebuffer = LoadFunction<InvalidateFramebufferDelegate>("glInvalidateFramebuffer");
            FramebufferTexture2D = LoadFunction<FramebufferTexture2DDelegate>("glFramebufferTexture2D");
            FramebufferRenderbuffer = LoadFunction<FramebufferRenderbufferDelegate>("glFramebufferRenderbuffer");
            RenderbufferStorage = LoadFunction<RenderbufferStorageDelegate>("glRenderbufferStorage");
            CheckFramebufferStatus = LoadFunction<CheckFramebufferStatusDelegate>("glCheckFramebufferStatus");

            GetIntegerv = LoadFunction<GetIntegerDelegate>("glGetIntegerv");
        }

        internal unsafe static void GetInteger(int name, out int value)
        {
            fixed (int* ptr = &value)
            {
                GetIntegerv(name, ptr);
            }
        }
    }
}

/// <summary>
/// Manages contexts and loads all the needed functions for Skia drawing. You should 
/// call Initialize() once when the game is created passing a valid GraphicsDevice.
/// </summary>
file static class SkiaGlManager
{
    internal static GraphicsDevice GraphicsDevice { get; private set; }

    static bool _initialized;
    static IntPtr _windowId;
    static IntPtr _mgContextId;
    static IntPtr _skContextId;

    internal static GRContext SkiaGrContext { get; private set; }

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        if (_initialized)
            throw new InvalidOperationException("SkiaGlManager is already initialized.");

        GraphicsDevice = graphicsDevice;

        // Get the SDL window ID. We need it to create new contexts.
        _windowId = GlWrapper.GetMgWindowId();

        // The MonoGame context is already created by the MG library. Here we get the ID.
        _mgContextId = GlWrapper.SDL_GL_GetCurrentContext();

        // Load the MonoGame context functions that we will use
        GlWrapper.MgGlFunctions.LoadFunctions();

        // This will tell OpenGL that the next context created will share objects with the main context
        var setAttributeResult = GlWrapper.SDL_GL_SetAttribute(GlConstants.SDL_GL_SHARE_WITH_CURRENT_CONTEXT, 1);

        if (setAttributeResult < 0)
            throw new Exception("SDL_GL_SetAttribute failed.");

        // Create the alternate context for Skia
        _skContextId = GlWrapper.SDL_GL_CreateContext(_windowId);

        if (_skContextId == IntPtr.Zero)
            throw new Exception("SDL_GL_CreateContext failed.");

        // Set the Skia context as current
        SetSkiaContextAsCurrent();

        // Load the Skia context functions that we will use
        GlWrapper.SkGlFunctions.LoadFunctions();

        // Create the Skia context object that will be using the alternate OpenGL context
        SkiaGrContext = GRContext.CreateGl();

        // Now that everything has been set up make the default context current again so MonoGame runs normally
        SetMonoGameContextAsCurrent();

        _initialized = true;
    }

    private static void SetContextAsCurrent(IntPtr contextId)
    {
        var makeCurrentResult = GlWrapper.MakeCurrent(_windowId, contextId);

        if (makeCurrentResult < 0)
            throw new Exception("SDL_GL_MakeCurrent failed.");
    }

    internal static void SetMonoGameContextAsCurrent()
    {
        SetContextAsCurrent(_mgContextId);
    }

    internal static void SetSkiaContextAsCurrent()
    {
        SetContextAsCurrent(_skContextId);
    }
}

public class MonoGameSkia
{
    private GRBackendRenderTarget _renderTarget;
    private SKSurface _surface;
    private SKCanvas _canvas;
    private SkiaSharpBackend _backend;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr d_sdl_gl_getprocaddress(string proc);

    public MonoGameSkia(GraphicsDevice graphicsDevice)
    {
        SkiaGlManager.Initialize(graphicsDevice);
        
        _renderTarget = new GRBackendRenderTarget(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, 0, 8, new GRGlFramebufferInfo(0, (uint)0x8058));
        _surface = SKSurface.Create(SkiaGlManager.SkiaGrContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;

        _backend = (SkiaSharpBackend)(IBackend.Backend = new SkiaSharpBackend(_canvas));
    }
    
    public void RemakeRenderTarget(int width, int height) 
    {
        _canvas.Dispose();
        _surface.Dispose();
        _renderTarget.Dispose();
        
        _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, (uint)0x8058));
        _surface = SKSurface.Create(SkiaGlManager.SkiaGrContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;

        _backend = (SkiaSharpBackend)(IBackend.Backend = new SkiaSharpBackend(_canvas));
    }

    public void Render()
    {
        SkiaGlManager.SetSkiaContextAsCurrent();
        SkiaGlManager.SkiaGrContext.ResetContext();
        _canvas.Flush();
        SkiaGlManager.SetMonoGameContextAsCurrent();
    }
}

internal class SkiaSharpBackend(SKCanvas canvas) : IBackend
{
    public IRadicalMusic LoadMusic(File file)
    {
        return new RadicalMusic(file);
    }

    public IImage LoadImage(File file)
    {
        return new MadSharpSKImage(System.IO.File.ReadAllBytes(file.Path));
    }

    public IImage LoadImage(ReadOnlySpan<byte> file)
    {
        return new MadSharpSKImage(file);
    }

    public void StopAllSounds()
    {
        SoundClip.StopAll();
    }

    public ISoundClip GetSound(string filePath)
    {
        return new SoundClip(filePath);
    }

    public IGraphics Graphics { get; } = new SkiaSharpGraphics(canvas);

    public Vector2 Ratio
    {
        set => ((SkiaSharpGraphics)Graphics).Ratio = value;
    }

    public void SetAllVolumes(float vol)
    {
        SoundClip.SetAllVolumes(vol);
    }
}

internal class SkiaSharpGraphics(SKCanvas canvas) : IGraphics
{
    private float sx = 1;
    private float sy = 1;

    public Vector2 Ratio
    {
        set
        {
            //canvas.Scale(1/sx, 1/sy);
            //canvas.Scale(value.X, value.Y);
            sx = value.X;
            sy = value.Y;
            //_paint.StrokeWidth = (sx + sy) / 3;
        }
    }

    private readonly SKPaint _paint = new()
    {
        IsAntialias = false,
    };
    private SKFont _font = new();
    private Dictionary<(string FamilyName, byte Flags), SKTypeface> _typefaceCache = new();

    public void SetColor(Color c)
    {
        _paint.Shader = null;
        _paint.Color = new SKColor(c.R, c.G, c.B, c.A);
    }

        /// <summary>
    /// Sets a gradient. May need to be cleared once done with it (ClearShader)
    /// </summary>
    /// <param name="x">Absolute X where the gradient starts</param>
    /// <param name="y">Absolute Y where the gradient starts</param>
    /// <param name="width">Gradient width</param>
    /// <param name="height">Gradient height</param>
    /// <param name="colors">Array of colours the gradient should traverse through</param>
    /// <param name="colorPos">Same length as colors, values between 0 to 1 which is the ratio of that colour, or null for auto.</param>
    public void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos)
    {
        var skcolors = new SKColor[colors.Length];
        int i = 0;

        foreach(Color c in colors) {
            skcolors[i++] = new SKColor(c.R, c.G, c.B, c.A);
        }

        var shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y),
            new SKPoint(x + width, y + height),
            skcolors,
            colorPos,
            SKShaderTileMode.Clamp);

        _paint.Shader = shader;
    }

    public void FillPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 2)
        {
            return;
        }
        _paint.Style = SKPaintStyle.Fill;
        
        using var path = new SKPath();
        
        // calculate centroid
        float cx, cy;
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < nPoints; i++) {
                x += xPoints[i];
                y += yPoints[i];
            }
            cx = (float) x / nPoints;
            cy = (float) y / nPoints;
        }
        float sx = xPoints[0] < cx ? xPoints[0] - 0.5f : xPoints[0] + 0.5f;
        float sy = yPoints[0] < cy ? yPoints[0] - 0.5f : yPoints[0] + 0.5f;
        
        path.MoveTo(sx, sy);
        
        for (int i = 1; i < nPoints; i++) {
            int x = xPoints[i];
            int y = yPoints[i];
            if (xPoints[i - 1] == x && yPoints[i - 1] == y) continue;
            path.LineTo(
                x < cx ? x - 0.5f : x + 0.5f,
                y < cy ? y - 0.5f : y + 0.5f
            );
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void DrawPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 1 || (nPoints == 2 && xPoints[0] == xPoints[1] && yPoints[0] == yPoints[1]))
        {
            return;
        }
        
        _paint.Style = SKPaintStyle.Stroke;
        
        using var path = new SKPath();
        path.MoveTo(xPoints[0], yPoints[0]);
        
        for (int i = 1; i < nPoints; i++) {
            if (xPoints[i] == xPoints[i - 1] && yPoints[i] == yPoints[i - 1]) continue;
            path.LineTo(xPoints[i], yPoints[i]);
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void FillRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(x1, y1, x2, y2, _paint);
    }

    public void SetAlpha(float f)
    {
        _paint.Color = _paint.Color.WithAlpha((byte)(f * 255));
    }

    public void DrawImage(IImage image, int x, int y)
    {
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, x, y);
    }

    public void SetFont(Font font)
    {
        if (_typefaceCache.TryGetValue((font.FontName, (byte)font.Flags), out var typeface))
        {
            _font.Typeface = typeface;
        }
        else

        {
            _font.Typeface = _typefaceCache[(font.FontName, (byte)font.Flags)] = SKTypeface.FromFamilyName(
                font.FontName,
                font.Flags == Font.BOLD ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                font.Flags == Font.ITALIC ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
            );
        }
        
        _font.Size = font.Size;
    }

    public IFontMetrics GetFontMetrics()
    {
        return new SkiaFontMetrics(_font);
    }

    public void DrawString(string text, int x, int y)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawText(text, x, y, SKTextAlign.Left, _font, _paint);
    }

    public void FillOval(int p0, int p1, int p2, int p3)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawOval(p0, p1, p2, p3, _paint);
    }

    public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawImage(IImage image, int x, int y, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, new SKRect(x, y, width + x, height + y));
    }

    public void SetAntialiasing(bool useAntialias)
    {
        _paint.IsAntialias = useAntialias;
    }
}

internal class SkiaFontMetrics(SKFont font) : IFontMetrics
{
    public int StringWidth(string astring)
    {
        return (int)font.MeasureText(astring);
    }

    public int Height(string astring)
    {
        return (int)font.Metrics.Ascent;
    }
}

internal class MadSharpSKImage : IImage
{
    internal readonly SKImage SkImage;

    public MadSharpSKImage(ReadOnlySpan<byte> file)
    {
        SkImage = SKImage.FromEncodedData(file);
    }

    public int Height => SkImage.Width;
    public int Width => SkImage.Height;
}