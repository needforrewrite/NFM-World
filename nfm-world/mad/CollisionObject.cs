using NFMWorld.Mad;

public class CollisionObject : MeshedGameObject
{
    public PlaceableObjectInfo PlaceableObjectInfo;

    public Rad3dBoxDef[] Boxes => PlaceableObjectInfo.Boxes;
    private readonly CollisionDebugMesh? _collisionDebugMesh;
    
    public CollisionObject(PlaceableObjectInfo placeableObjectInfo) : base(placeableObjectInfo.Mesh)
    {
        PlaceableObjectInfo = placeableObjectInfo;
        if (PlaceableObjectInfo.Boxes.Length > 0)
        {
            _collisionDebugMesh = new CollisionDebugMesh(PlaceableObjectInfo.Boxes)
            {
                Parent = this
            };
        }
    }
    public CollisionObject(PlaceableObjectInfo placeableObjectInfo, Vector3 position, Euler rotation) : this(placeableObjectInfo)
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