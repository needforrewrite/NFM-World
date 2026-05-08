using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Mad.Rad;

namespace NFMWorld;

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