using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Mathematics;

public class Car : Mesh
{
    public CarStats Stats;

    public Car(GraphicsDevice device, Rad3d rad) : base(device, rad)
    {
        Stats = rad.Stats;
    }

    public Car(Car baseCar, Vector3 position, Euler rotation) : base(baseCar, position, rotation)
    {
        Stats = baseCar.Stats;
    }
}