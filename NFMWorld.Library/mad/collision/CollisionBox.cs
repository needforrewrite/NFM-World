using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Collision;

public readonly struct CollisionBox(
    f64Vector3 rad,
    f64Vector3 trackersPosition,
    fix64 contoXz,
    f64Vector3 contoPosition)
{
    public readonly struct Collision;

    public Collision? ResolveCollision(f64Vector3 position) {
        f64Vector3 localPosition = ((position + (contoPosition * fix64.MinusOne)).RotateXz(-contoXz)) + (trackersPosition * fix64.MinusOne);
        if (fix64.Abs(localPosition.X) > rad.X || fix64.Abs(localPosition.Y) > rad.Y || fix64.Abs(localPosition.Z) > rad.Z) { // Inside?
            return null;
        }

        return new Collision();
    }
}