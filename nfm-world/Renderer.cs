namespace NFMWorld;

/// <summary>
/// The top-level rendering backend behind <c>--backend=</c>.
///
/// A runtime choice rather than a define, unlike the older <c>ANGLE</c> one: the comparison this
/// harness exists for is between sokol and desktop GL on the same machine, and a build-time
/// switch would mean the two numbers came from different binaries. Only the ANGLE backend stays
/// behind a define, because it is the one that needs a native ANGLE package the other paths do
/// not.
/// </summary>
public enum Renderer
{
    Auto,

    /// <summary>
    /// Our own Direct3D 11 backend, over TerraFX's raw COM bindings.
    ///
    /// Distinct from <c>--backend=sokol --sokol-backend=d3d11</c>, which reaches the same API
    /// through sokol_gfx's driver rather than through <see cref="WorldGame"/>'s. The two exist
    /// side by side so the comparison the other arms were built for can be extended to this one:
    /// same API, two implementations of the abstraction over it.
    ///
    /// No define gates it. Unlike ANGLE it needs no native package of its own - it P/Invokes
    /// d3d11.dll, dxgi.dll and d3dcompiler_47.dll, all of which ship with Windows - so the only
    /// cost of referencing it everywhere is a managed assembly. It throws at device creation on
    /// any other platform; see <see cref="ParseRenderer"/>'s note on that.
    /// </summary>
    D3d11,

    /// <summary>
    /// Desktop OpenGL 3.3 core, through our own backend rather than sokol's.
    ///
    /// Independent of sokol entirely: it links its own program from the bundles' <c>Glsl330</c>
    /// form and drives the host's context directly. This is the path that answers whether GL's
    /// cost on this scene was ANGLE's translation layer or GL's own.
    /// </summary>
    DesktopGl,

    /// <summary>
    /// The ANGLE/GLES backend, only present under the <c>ANGLE</c> define.
    ///
    /// Kept selectable so the baseline number can be reproduced without rebuilding, but it can
    /// only be named when the define is on - see <see cref="ParseRenderer"/>.
    /// </summary>
    Angle,
}
