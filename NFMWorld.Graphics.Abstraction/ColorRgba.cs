namespace NFMWorld.Graphics;

/// <summary>
/// Self-contained linear/sRGB-agnostic color value used at the graphics-abstraction boundary,
/// so this project has no dependency on FNA.Math's <c>Microsoft.Xna.Framework.Color</c>.
/// </summary>
public readonly record struct ColorRgba(float R, float G, float B, float A = 1f);
