using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;

public class PlaceableObjectInfo : ObjectInfo
{
    public Rad3dBoxDef[] Boxes;
    public Vector2[] Atp;

    public PlaceableObjectInfo(GraphicsDevice graphicsDevice, Rad3d rad, string fileName) : base(new Mesh(graphicsDevice, rad, fileName))
    {
        Boxes = rad.Boxes;
        Atp = rad.Atp;
    }
}