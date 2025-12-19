using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Mathematics;

public class CheckPoint : Mesh
{
    public enum CheckPointRotation 
    {
        None = 1,
        RightAngle = 2
    }

    public CheckPointRotation CheckPointRot { get; set; } = CheckPointRotation.None;

    public CheckPoint(Mesh mesh, Vector3 position, Euler rotation) : base(mesh, position, rotation)
    {
        CheckPointRot = rotation.Yaw.Degrees % 180 == 0 ? CheckPointRotation.None : CheckPointRotation.RightAngle;
    }
}