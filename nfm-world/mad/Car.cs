using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Mathematics;

public class Car : Mesh
{
    public CarStats Stats;

    public Car(GraphicsDevice device, Rad3d rad) : base(device, rad)
    {
        string? invalidStat = rad.Stats.Validate();
        if (invalidStat != null)
        {
            Stats = CarStats.Default;
        }
        else
        {

            Stats = rad.Stats;
        }
    }

    public Car(Car baseCar, Vector3 position, Euler rotation) : base(baseCar, position, rotation)
    {
        string? invalidStat = baseCar.Stats.Validate();
        if (invalidStat != null)
        {
            Stats = CarStats.Default;
        }
        else
        {

            Stats = baseCar.Stats;
        }
    }
}