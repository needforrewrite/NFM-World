using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Mad;

namespace NFMWorld;

public class StageObjectGameObject : MeshedGameObject
{
    private readonly StageObject _obj;

    public StageObjectGameObject(Mesh mesh, StageObject obj) : base(mesh, obj.Position, obj.Rotation)
    {
        _obj = obj;
    }

    public override void GameTick(IStage? stage = null)
    {
        base.GameTick(stage);
        Position = _obj.Position;
        Rotation = _obj.Rotation;
    }
}