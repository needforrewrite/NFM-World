using System;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld;

public class StageObjectGameObject : MeshedGameObject, IDisposable
{
    private readonly StageObject _obj;
    private readonly CollisionDebugMesh? _collisionDebug;

    public StageObjectGameObject(Mesh mesh, StageObject obj) : base(mesh, obj.Position, obj.Rotation)
    {
        _obj = obj;
        _collisionDebug = new CollisionDebugMesh(obj.Boxes)
        {
            Parent = this
        };
        Children = [_collisionDebug];
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