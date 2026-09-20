using DrawingColor = System.Drawing.Color;

namespace NFMWorld;

/// <summary>
/// Conversions between nfm-world's <c>Color</c> alias (<see cref="Maxine.Extensions.Mathematics.Color"/>,
/// matching NFMWorldLibrary's engine-agnostic interfaces) and the color types other layers still
/// want. The XNA <c>Microsoft.Xna.Framework.Color</c> bitcast helpers this class started with
/// (<c>ToXna</c>/<c>ToMaxine</c>) are gone: the per-draw <c>GraphicsDevice</c> calls that needed
/// them were the last in-tree consumers, and everything now renders through the graphics
/// abstraction, which takes <see cref="NFMWorld.Graphics.ColorRgba"/> or the aliased <c>Color</c>.
/// </summary>
public static class ColorInterop
{
    /// <summary>
    /// NvgSharp's PLATFORM_AGNOSTIC build (<see cref="AbstractionNvgRenderer"/>'s consumer) uses
    /// <see cref="System.Drawing.Color"/> - a real field-by-field conversion rather than a bitcast:
    /// System.Drawing.Color is a much larger struct (name/known-color state) with no matching memory
    /// layout.
    /// </summary>
    public static DrawingColor ToDrawing(this Color c) => DrawingColor.FromArgb(c.A, c.R, c.G, c.B);

    /// <summary>System.Drawing.Color is immutable with no public setters, so `with {}` doesn't apply to it.</summary>
    public static DrawingColor WithAlpha(this DrawingColor c, byte alpha) => DrawingColor.FromArgb(alpha, c.R, c.G, c.B);
}
