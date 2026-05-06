using MoonWorks.Graphics;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;

namespace nfm_world.renderable.mesh;

public class CarMesh : Mesh
{
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;

    public CarMesh(GraphicsDevice graphicsDevice, Rad3d rad) : base(graphicsDevice, rad)
    {
        Stats = CarStats.ValidateStats(rad.Stats, rad.FileName);

        Wheels = rad.Wheels;
        Rims = rad.Rims;
    }
}