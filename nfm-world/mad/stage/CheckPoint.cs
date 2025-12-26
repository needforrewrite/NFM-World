using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using Stride.Core.Mathematics;

public class CheckPoint(PlaceableObjectInfo placeableObjectInfo, Vector3 position, Euler rotation) : CollisionObject(placeableObjectInfo, position, rotation)
{
    public enum CheckPointRotation 
    {
        None = 1,
        RightAngle = 2
    }

    public CheckPointRotation CheckPointRot
    {
        get
        {
            if (Rotation.Yaw == AngleSingle.ZeroAngle || Rotation.Yaw == AngleSingle.StraightAngle)
                return CheckPointRotation.None;
            else
                return CheckPointRotation.RightAngle;
        } 
    }
}