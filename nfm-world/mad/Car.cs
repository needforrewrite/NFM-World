using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

public class Car : Mesh
{
    public CarStats Stats;

    public Car(GraphicsDevice device, Rad3d rad, string fileName) : base(device, rad, fileName)
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
    }

    public Car(Car baseCar, Vector3 position, Euler rotation) : base(baseCar, position, rotation)
    {
        string? invalidStat = baseCar.Stats.Validate(baseCar.Stats.Name);
        if (invalidStat != null)
        {
            // Name should always be defined by now so dont bother checking for that here
            Stats = CarStats.Default;
        }
        else
        {

            Stats = baseCar.Stats;
        }
    }
}