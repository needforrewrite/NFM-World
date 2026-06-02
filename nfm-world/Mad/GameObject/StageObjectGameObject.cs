using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld;

public class StageObjectGameObject : MeshedGameObject
{
    private readonly StageObject _obj;

    public StageObjectGameObject(Mesh mesh, StageObject obj) : base(mesh, obj.Position, obj.EulerAngles)
    {
        _obj = obj;
        Children = [new CollisionDebugMesh(obj.Boxes)
        {
            Parent = this
        }];
    }

    public override void GameTick(IStage? stage = null)
    {
        base.GameTick(stage);
        Position = _obj.Position;
        EulerAngles = _obj.EulerAngles;
    }
}