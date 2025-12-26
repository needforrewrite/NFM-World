namespace NFMWorld.Mad;

public readonly record struct RenderData(
    IInstancedRenderElement RenderElement,
    Matrix World,
    bool GetsShadowed = true,
    float AlphaOverride = 1.0f,
    bool IsFullbright = false,
    bool Glow = false,
    int RenderOrder = 0
)
{
    public InstanceData ToInstanceData() => new(World, GetsShadowed, AlphaOverride, IsFullbright, Glow);
}