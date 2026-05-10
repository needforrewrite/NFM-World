using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

// This duplicates some code from CollisionObject, no workaround
namespace NFMWorld;

public class EditorObject : ClientCar
{
    public Rad3dBoxDef[] Boxes { get; }

    private readonly CollisionDebugMesh? _collisionDebugMesh;

    public EditorObject(GraphicsDevice graphicsDevice, Rad3d rad) : base(graphicsDevice, new ClientOnlyBackendCar(rad))
    {
        Boxes = rad.Boxes;
        if (rad.Boxes.Length > 0)
        {
            _collisionDebugMesh = new CollisionDebugMesh(rad.Boxes)
            {
                Parent = this
            };
        }
    }

    public EditorObject(GraphicsDevice graphicsDevice, Rad3d rad, f64Vector3 position, f64Euler rotation) : this(graphicsDevice, rad)
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