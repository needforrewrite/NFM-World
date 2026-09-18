using System.Runtime.CompilerServices;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace NFMWorld;

/// <summary>
/// nfm-world's <c>Color</c> alias points at <see cref="Maxine.Extensions.Mathematics.Color"/> (matching
/// NFMWorldLibrary's engine-agnostic interfaces), but code that talks directly to FNA APIs
/// (SpriteBatch, Effect parameters, VertexPositionColor, NvgSharp's FNA renderer) still needs
/// <c>Microsoft.Xna.Framework.Color</c>. Both are 4-byte R,G,B,A structs with identical memory
/// layout on little-endian platforms (XNA's Color packs R|G&lt;&lt;8|B&lt;&lt;16|A&lt;&lt;24 into a uint, which is
/// exactly R,G,B,A byte order in memory on little-endian CPUs - the only realistic target here),
/// so the conversion is a free bitcast rather than a field-by-field copy.
/// </summary>
public static class ColorInterop
{
    public static XnaColor ToXna(this Color c) => Unsafe.BitCast<Color, XnaColor>(c);
    public static Color ToMaxine(this XnaColor c) => Unsafe.BitCast<XnaColor, Color>(c);
}
