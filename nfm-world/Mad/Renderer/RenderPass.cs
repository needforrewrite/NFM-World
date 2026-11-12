namespace NFMWorld;

/// <summary>
/// Distinguishes between shadow-map and main rendering passes.
/// Replaces the <c>Lighting.IsCreateShadowMap</c> boolean pattern.
/// </summary>
public enum RenderPassKind : byte
{
    /// <summary>Rendering depth into a cascade shadow map.</summary>
    Shadow,

    /// <summary>Rendering colour to the backbuffer.</summary>
    Main
}

/// <summary>
/// Describes the current rendering pass — shadow map creation or main colour pass.
/// Carries cascade information for shadow passes.
/// </summary>
public readonly struct RenderPass(RenderPassKind kind, int cascadeIndex, int totalCascades)
{
    public readonly RenderPassKind Kind = kind;
    public readonly int CascadeIndex = cascadeIndex;
    public readonly int TotalCascades = totalCascades;

    public bool IsShadow => Kind == RenderPassKind.Shadow;
    public bool IsMain => Kind == RenderPassKind.Main;

    public static RenderPass Shadow(int cascadeIndex, int totalCascades)
        => new(RenderPassKind.Shadow, cascadeIndex, totalCascades);

    public static RenderPass Main(int totalCascades = 0)
        => new(RenderPassKind.Main, 0, totalCascades);

    /// <summary>
    /// Construct a <see cref="RenderPass"/> from legacy <see cref="Lighting"/> data.
    /// Used during migration; prefer the explicit <see cref="Shadow"/> / <see cref="Main"/> factories.
    /// </summary>
    public static RenderPass FromLighting(Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true)
            return Shadow(lighting.NumCascade, lighting.TotalCascades);
        return Main(lighting?.TotalCascades ?? 0);
    }
}
