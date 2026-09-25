// LLM maintained.
//
// Which ANGLE backend and device to ask for, chosen at launch rather than at build time.
//
// This exists because the choice cannot be expressed any other way on this platform. SDL does not
// select an ANGLE backend - it only chooses which EGL/GLES *library* to load, and there is no
// SDL3 hint or environment variable for the backend itself (ANGLE_DEFAULT_PLATFORM is not honoured
// by SDL3, and `SDL_egl.c` contains no ANGLE backend logic at all). The attributes have to be
// handed to eglGetPlatformDisplay by the application, which is what the two flags here are for.
//
// The parameter names are ANGLE's own ("platform type" and "device type"), kept rather than
// flattened into one word because they are genuinely orthogonal: the platform type is which API
// ANGLE renders through, and the device type is which adapter within it. Vulkan + SwiftShader and
// D3D11 + WARP are both sensible pairs, and one flag cannot spell either without a second half
// being implied rather than asked for.
//
// Every token below is from ANGLE's own header, `include/EGL/eglext_angle.h`, and none of them are
// in the Khronos EGL registry the Maxine.Silk.EGL bindings are generated from - so they are numbers
// here for the same reason Egl.cs declares its copy of them by hand. See that file's header.
namespace NFMWorld.Graphics.OpenGL;

/// <summary>ANGLE's "platform type": which 3D API ANGLE renders through. <c>EGL_PLATFORM_ANGLE_TYPE_*_ANGLE</c>.</summary>
public enum AnglePlatformType
{
    /// <summary>Direct3D 11 (<c>EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE</c>). The default, and the backend every previous measurement of this path actually intended.</summary>
    D3d11,

    /// <summary>Vulkan (<c>EGL_PLATFORM_ANGLE_TYPE_VULKAN_ANGLE</c>).</summary>
    Vulkan,

    /// <summary>Desktop OpenGL (<c>EGL_PLATFORM_ANGLE_TYPE_OPENGL_ANGLE</c>) - a pass-through to the driver's own GL, so it is the one type that is not a translation layer.</summary>
    Gl,

    /// <summary>OpenGL ES (<c>EGL_PLATFORM_ANGLE_TYPE_OPENGLES_ANGLE</c>).</summary>
    Gles,

    /// <summary>
    /// ANGLE's null renderer (<c>EGL_PLATFORM_ANGLE_TYPE_NULL_ANGLE</c>). Renders nothing and exists
    /// to isolate the front end; useful only to prove the selection mechanism is live, which is
    /// exactly how this flag is verified.
    /// </summary>
    Null,

    /// <summary>Whatever ANGLE would pick on its own (<c>EGL_PLATFORM_ANGLE_TYPE_DEFAULT_ANGLE</c>).</summary>
    Default,
}

/// <summary>ANGLE's "device type": which adapter to use within the platform type. <c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_*_ANGLE</c>.</summary>
public enum AngleDeviceType
{
    /// <summary>A real GPU (<c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE</c>). The default.</summary>
    Hardware,

    /// <summary>Microsoft's software rasterizer, WARP (<c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_WARP_ANGLE</c>).</summary>
    Warp,

    /// <summary>The D3D11 debug/reference device (<c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_REFERENCE_ANGLE</c>). Very slow; for driver bugs.</summary>
    Reference,

    /// <summary>SwiftShader (<c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_SWIFTSHADER_ANGLE</c>) - Vulkan only.</summary>
    SwiftShader,

    /// <summary>ANGLE's null device (<c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_NULL_ANGLE</c>).</summary>
    Null,
}

/// <summary>
/// One resolved choice: a platform type and a device type, plus the tokens ANGLE matches on and the
/// launch-argument parsing that produces them.
///
/// Modelled on <c>SokolBackendSelection</c> (NFMWorld.Graphics.Sokol/SokolBackend.cs) deliberately,
/// down to the loud throw on an unknown name: the default here is a working configuration, so a
/// typo would otherwise produce a run that looks entirely normal and describes a different renderer
/// than the one asked for - which is the failure this whole mechanism exists to make impossible.
/// That is not hypothetical for this file: the bug it replaces was a wrong token that silently
/// selected ANGLE's null backend for the entire life of the ANGLE path.
/// </summary>
public readonly record struct AngleSelection(AnglePlatformType Platform, AngleDeviceType Device)
{
    /// <summary>The default: D3D11 on real hardware.</summary>
    public static AngleSelection Default => new(AnglePlatformType.D3d11, AngleDeviceType.Hardware);

    /// <summary>
    /// Whether this is <see cref="Default"/> - i.e. whether the attributes would ask for anything
    /// ANGLE does not already pick on its own.
    ///
    /// This matters because asking for D3D11 explicitly is not free: installing the platform
    /// attributes at all takes SDL down a different display-creation path (<c>eglGetPlatformDisplay(
    /// EGL_PLATFORM_ANGLE_ANGLE, …)</c> instead of its <c>eglGetDisplay</c> fallback), and on this
    /// backend the config that path lands on has been the source of real bugs - an earlier
    /// <c>SDL_GL_EGL_PLATFORM</c> attempt produced a context where every <c>gl*</c> call returned
    /// <c>GL_INVALID_OPERATION</c>, a failure recorded in full on <c>WorldGame.CreateAngleDevice</c>.
    /// So the caller uses this to leave a default run on exactly the display and config it has
    /// always used, and only takes the new path when the user actually asked for something else.
    /// </summary>
    public bool IsDefault => this == Default;

    /// <summary>The <c>--angle-backend=</c> argument, naming the platform type.</summary>
    public const string BackendArgumentPrefix = "--angle-backend=";

    /// <summary>The <c>--angle-device=</c> argument, naming the device type.</summary>
    public const string DeviceArgumentPrefix = "--angle-device=";

    /// <summary>
    /// <c>EGL_PLATFORM_ANGLE_ANGLE</c> - the platform value SDL is asked for, via
    /// <c>SDL_GL_SetAttribute(SDL_GL_EGL_PLATFORM, …)</c>.
    ///
    /// Exposed because the game has to set that attribute but <c>Egl</c> is internal to this
    /// assembly, and the alternative - a second hand-written <c>0x3202</c> outside it - is the exact
    /// shape of the bug this file was written to repair. A wrong token here is not a compile error;
    /// it is a process that starts normally and renders on a different backend than it reports.
    /// </summary>
    public const int PlatformAttribute = Egl.EglPlatformAngleAngle;

    /// <summary>
    /// The selection <paramref name="args"/> asks for, defaulting each half independently so that
    /// naming only one of them is meaningful.
    /// </summary>
    /// <exception cref="ArgumentException">On an unknown platform or device name.</exception>
    public static AngleSelection Parse(string[] args) =>
        new(ParseEnum(args, BackendArgumentPrefix, AnglePlatformType.D3d11),
            ParseEnum(args, DeviceArgumentPrefix, AngleDeviceType.Hardware));

    /// <summary>
    /// The <c>EGL_PLATFORM_ANGLE_TYPE_ANGLE</c> value for <see cref="Platform"/>.
    ///
    /// A switch rather than a field on the enum because enums cannot carry the token without an
    /// attribute lookup, and this way the mapping is one readable table next to the names.
    /// </summary>
    public int PlatformToken => Platform switch
    {
        AnglePlatformType.D3d11 => Egl.EglPlatformAngleTypeD3d11,
        AnglePlatformType.Vulkan => Egl.EglPlatformAngleTypeVulkan,
        AnglePlatformType.Gl => Egl.EglPlatformAngleTypeOpenGl,
        AnglePlatformType.Gles => Egl.EglPlatformAngleTypeOpenGles,
        AnglePlatformType.Null => Egl.EglPlatformAngleTypeNull,
        AnglePlatformType.Default => Egl.EglPlatformAngleTypeDefault,
        _ => throw new ArgumentOutOfRangeException(nameof(Platform), Platform,
            $"No EGL_PLATFORM_ANGLE_TYPE token is defined for this platform type, please update {nameof(AngleSelection)}"),
    };

    /// <summary>
    /// The <c>EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE</c> value for <see cref="Device"/>.
    ///
    /// A device type is only meaningful for the D3D platforms; ANGLE ignores it for the others, so
    /// no attempt is made here to refuse a nonsensical pairing - it is reported rather than policed.
    /// </summary>
    public int DeviceToken => Device switch
    {
        AngleDeviceType.Hardware => Egl.EglPlatformAngleDeviceTypeHardware,
        AngleDeviceType.Warp => Egl.EglPlatformAngleDeviceTypeD3dWarp,
        AngleDeviceType.Reference => Egl.EglPlatformAngleDeviceTypeD3dReference,
        AngleDeviceType.SwiftShader => Egl.EglPlatformAngleDeviceTypeSwiftShader,
        AngleDeviceType.Null => Egl.EglPlatformAngleDeviceTypeNull,
        _ => throw new ArgumentOutOfRangeException(nameof(Device), Device,
            $"No EGL_PLATFORM_ANGLE_DEVICE_TYPE token is defined for this device type, please update {nameof(AngleSelection)}"),
    };

    /// <summary>
    /// The pair in prose, for a startup log line.
    ///
    /// Worth logging: the only confirmation that these attributes were honoured is the GL_RENDERER
    /// string ANGLE reports afterwards, and that string is unreadable without the request it
    /// answers. It is also the only way to see the case where SDL's fallback path dropped the
    /// attribute list entirely.
    /// </summary>
    public string Describe() => $"{Platform.ToString().ToLowerInvariant()}/{Device.ToString().ToLowerInvariant()}";

    /// <summary>
    /// The <c>eglGetPlatformDisplay</c> attribute list for this selection: an <c>EGL_NONE</c>-terminated
    /// run of <c>{TYPE_ANGLE, platform, DEVICE_TYPE_ANGLE, device}</c>.
    ///
    /// Public because there are two consumers and they must agree exactly. <see cref="Egl.GetAngleDisplay"/>
    /// passes it to EGL directly; the game hands the same list to SDL through
    /// <c>SDL_EGL_SetAttributeCallbacks</c>, and SDL forwards the pointer straight to
    /// <c>eglGetPlatformDisplay</c> without interpreting it - so "the same request" means the same
    /// integer values in the same order, which is exactly the property that was broken when the two
    /// paths spelled the tokens independently and one of them was <c>0x33AE</c>.
    ///
    /// A <c>nint[]</c> because that is <c>EGLAttrib</c> - 64-bit on this platform, unlike EGL's
    /// 32-bit <c>EGLint</c>. Both consumers want that width and neither has to convert: the
    /// generated binding takes <c>nint*</c>, and SDL's callback contract is a native
    /// <c>EGLAttrib*</c> it frees with <c>SDL_free</c>. Getting this wrong in the other direction is
    /// silent - EGL reads each 64-bit entry as one shifted word - so the type is the assertion.
    /// </summary>
    public nint[] PlatformAttributes() =>
    [
        Egl.EglPlatformAngleTypeAngle, PlatformToken,
        Egl.EglPlatformAngleDeviceTypeAngle, DeviceToken,
        Egl.EglNone,
    ];

    /// <summary>
    /// Reads one <c>--flag=value</c> argument into an enum, or returns <paramref name="fallback"/>
    /// when the argument is absent.
    /// </summary>
    /// <remarks>
    /// Shared by both halves rather than repeated, because the two differ only in their prefix,
    /// their type and their default - and the error message is the part worth getting right.
    /// </remarks>
    private static TEnum ParseEnum<TEnum>(string[] args, string prefix, TEnum fallback)
        where TEnum : struct, Enum
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var value = arg[prefix.Length..];
            if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
                return parsed;

            throw new ArgumentException(
                $"Unknown {prefix}{value}. Valid values: " +
                $"{string.Join(", ", Enum.GetNames<TEnum>().Select(n => n.ToLowerInvariant()))}.");
        }

        return fallback;
    }
}
