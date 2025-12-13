using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class FixHoop(Mesh baseMesh, Vector3 position, Euler rotation) : Mesh(baseMesh, position, rotation)
{
    public bool Rotated;
    
    public override void GameTick()
    {
        if (!Rotated || Rotation.Xz != AngleSingle.ZeroAngle)
        {
            var xy = Rotation.Xy.Degrees;
            xy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (xy > 360)
            {
                xy -= 360;
            }
            Rotation = Rotation with { Xy = AngleSingle.FromDegrees(xy) };
        }
        else
        {
            var zy = Rotation.Zy.Degrees;
            zy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (zy > 360)
            {
                zy -= 360;
            }
            Rotation = Rotation with { Zy = AngleSingle.FromDegrees(zy) };
        }
    }
}