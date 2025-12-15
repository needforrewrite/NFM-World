using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;

public class Car : Mesh
{
    public CarStats Stats;

    public Car(GraphicsDevice device, Rad3d rad) : base(device, rad)
    {
        Stats = rad.Stats;
    }
}