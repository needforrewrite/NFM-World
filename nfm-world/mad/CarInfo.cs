using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Extensions;

public class CarInfo : ObjectInfo
{
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;

    public CarInfo(GraphicsDevice graphicsDevice, Rad3d rad, string fileName) : base(new Mesh(graphicsDevice, rad, fileName))
    {
        string? invalidStat = rad.Stats.Validate(fileName);
        if (invalidStat != null)
        {
            Stats = CarStats.Default;
            if(invalidStat == nameof(Stats.Name) || rad.Stats.Name.IsNullOrEmpty())
            {
                Stats = Stats with { Name = fileName };
            }
        }
        else
        {
            Stats = rad.Stats;
        }

        Wheels = rad.Wheels;
        Rims = rad.Rims;
    }
}