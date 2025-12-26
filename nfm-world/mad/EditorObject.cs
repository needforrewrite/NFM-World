using NFMWorld.Mad;

// This duplicates some code from CollisionObject, no workaround
public class EditorObject : Car
{
    public EditorObjectInfo EditorObjectInfo;
    public Rad3dBoxDef[] Boxes => EditorObjectInfo.Boxes;

    private readonly CollisionDebugMesh? _collisionDebugMesh;

    public EditorObject(EditorObjectInfo carInfo) : base(carInfo)
    {
        EditorObjectInfo = carInfo;
        if (EditorObjectInfo.Boxes.Length > 0)
        {
            _collisionDebugMesh = new CollisionDebugMesh(EditorObjectInfo.Boxes)
            {
                Parent = this
            };
        }
    }

    public EditorObject(EditorObjectInfo carInfo, Vector3 position, Euler rotation) : this(carInfo)
    {
        Position = position;
        Rotation = rotation;
    }

    public override IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        foreach (var renderData in base.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        base.Render(camera, lighting);
        _collisionDebugMesh?.Render(camera, lighting);
    }
}