using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;

// This duplicates some code from PlaceableObjectInfo, no workaround
public class EditorObjectInfo(GraphicsDevice graphicsDevice, Rad3d rad, string fileName)
    : CarInfo(graphicsDevice, rad, fileName)
{
    public Rad3dBoxDef[] Boxes = rad.Boxes;
}