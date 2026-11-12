using NFMWorldLibrary.Backend;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using BoundingSphere = Maxine.Extensions.Mathematics.BoundingSphere;

namespace NFMWorld;

public class StageObjectGameObject : MeshedGameObject, IDisposable
{
    private readonly StageObject _obj;
    private readonly CollisionDebugMesh? _collisionDebug;
    private readonly Mesh? _checkpointBounds;

    private static readonly Rad3d CheckpointBoundsModel = new(
        [
            new Rad3dPoly(
                new Color3(31, 107, 255),
                null,
                PolyType.Flat,
                null,
                0,
                [new(-700, -800, 0), new(700, -800, 0), new(700, 100, 0), new(-700, 100, 0)],
                [0, 1, 2, 0, 2, 3]
            )
        ],
        false,
        "checkpoint-trigger-bounds"
    );

    public StageObjectGameObject(Mesh mesh, StageObject obj) : base(mesh, obj.Position, obj.Rotation)
    {
        _obj = obj;
        _collisionDebug = new CollisionDebugMesh(obj.Boxes)
        {
            Parent = this
        };
        Children = [_collisionDebug];

        var modelName = obj.FileName.AsSpan(obj.FileName.LastIndexOf('/') + 1);
        if (obj.Kind == AiNodeKind.CheckPoint && !modelName.Contains("checkpoint", StringComparison.OrdinalIgnoreCase))
        {
            _checkpointBounds = GameSparker.GetStagePartMesh(CheckpointBoundsModel);
            _checkpointBounds.OverlayMode = PolyOverlayMode.DepthRead;
        }
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        base.SubmitDraws(queue, camera, lighting, pass);

        if (!pass.IsShadow && Glow == true && _checkpointBounds is { } bounds)
        {
            bounds.SubmitRenderables(
                queue,
                lighting,
                false,
                new BoundingSphere(MatrixWorld.Translation, bounds.MaxRadius),
                RenderBucket.CheckpointBounds,
                MatrixWorld,
                Layer ?? 0,
                getsShadowed: false,
                alphaOverride: 0.25f,
                isFullbright: true
            );
        }
    }

    public override void GameTick(BackendStage? stage = null)
    {
        base.GameTick(stage);
        Position = _obj.Position;
        Rotation = _obj.Rotation;
    }

    public void Dispose()
    {
        _collisionDebug?.Dispose();
        GC.SuppressFinalize(this);
    }
}
