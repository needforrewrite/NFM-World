using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;

public class ObjectInfo(Mesh mesh)
{
    public Mesh Mesh = mesh;
    public int GroundAt => Mesh.GroundAt;
    public int MaxRadius => Mesh.MaxRadius;
    public string FileName => Mesh.FileName;
    public GraphicsDevice GraphicsDevice => Mesh.GraphicsDevice;
}