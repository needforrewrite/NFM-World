using MoonWorks.Graphics;
using nfm_world.renderable.mesh;

namespace nfm_world;

public class ObjectInfo(Mesh mesh)
{
    public Mesh Mesh = mesh;
    public int GroundAt => Mesh.GroundAt;
    public int MaxRadius => Mesh.MaxRadius;
    public string FileName => Mesh.FileName;
    public GraphicsDevice GraphicsDevice => Mesh.GraphicsDevice;
}