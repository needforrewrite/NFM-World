namespace NFMWorld.Mad;

public class MeshedGameObject(Mesh mesh) : GameObject
{
    public Mesh Mesh = mesh;

    public MeshedGameObject(Mesh mesh, Vector3 position, Euler rotation) : this(mesh)
    {
        Position = position;
        Rotation = rotation;
    }

    public bool CastsShadow { get; set; } = mesh.CastsShadow;

    public bool? GetsShadowed
    {
        get => field ?? (Parent is MeshedGameObject parent ? parent.GetsShadowed : null);
        set;
    }

    public float? AlphaOverride
    {
        get => field ?? (Parent is MeshedGameObject parent ? parent.AlphaOverride : null);
        set;
    }

    public bool? Glow
    {
        get => field ?? (Parent is MeshedGameObject parent ? parent.Glow : null);
        set;
    }

    public bool? Finish
    {
        get => field ?? (Parent is MeshedGameObject parent ? parent.Finish : null);
        set;
    }

    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true && !(CastsShadow || Position.Y < World.Ground)) yield break;
        
        foreach (var (element, renderOrder) in Mesh.GetRenderables(lighting, Finish ?? false))
        {
            var actualRenderOrder = AlphaOverride is {} alphaOverride and < 1.0f ? 1 : renderOrder;
            yield return new RenderData(element, MatrixWorld, GetsShadowed ?? true, AlphaOverride ?? 1.0f, Glow ?? false, Glow ?? false, actualRenderOrder);
        }

        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }
}