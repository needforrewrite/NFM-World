using Maxine.Extensions.Mathematics;
using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;
using BoundingSphere = Microsoft.Xna.Framework.BoundingSphere;

namespace NFMWorld;

public class MeshedGameObject(Mesh mesh) : GameObject
{
    public Mesh Mesh = mesh;

    public MeshedGameObject(Mesh mesh, f64Vector3 position, f64Euler rotation) : this(mesh)
    {
        PositionWithoutInterpolation = position;
        RotationWithoutInterpolation = rotation;
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

    public RenderBucket RenderBucket { get; set; } = RenderBucket.StagePieces;

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        var boundingSphere = new BoundingSphere(MatrixWorld.Translation, Mesh.MaxRadius);
        Mesh.SubmitRenderables(queue, lighting, Finish ?? false, boundingSphere, RenderBucket, MatrixWorld, GetsShadowed ?? true, AlphaOverride ?? 1.0f, Glow ?? false, Glow ?? false);

        base.SubmitDraws(queue, camera, lighting, pass);
    }
}