using System.Diagnostics.CodeAnalysis;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Collision;

public readonly struct BoxWall(
    f64Vector3 rad,
    fix64 trackersXz,
    f64Vector3 trackersPosition,
    fix64 contoXz,
    f64Vector3 contoPosition)
{
    private static f64Vector3 zDir { get; } = new f64Vector3(0, 0, 1); // We push toward +Z
    private readonly f64Vector3 pushDir = zDir.RotateXz(trackersXz).RotateXz(contoXz);

    public readonly struct Collision(f64Vector3 positionDelta, f64Vector3 impactComponent)
    {
        public readonly f64Vector3 positionDelta = positionDelta; // how much we have to translate the wheels to push out from the box
        public readonly f64Vector3 impactComponent = impactComponent; // the component of velocity pointing directly into the box
    }

    public Collision? ResolveCollision(in f64Vector3 position, in f64Vector3 velocity) {
        f64Vector3 localPosition = ((position + (contoPosition * -1)).RotateXz(-contoXz)
                                    + (trackersPosition * -1)).RotateXz(-trackersXz);
        if (fix64.Abs(localPosition.X) > rad.X || fix64.Abs(localPosition.Y) > rad.Y || fix64.Abs(localPosition.Z) > rad.Z) { // Inside?
            return null;
        }
        
        if (f64Vector3.Dot(velocity, pushDir) >= 0) { // Moving into the wall?
            return null;
        }

        fix64 penetration = rad.Z - localPosition.Z;
        if (penetration < 0)
        {
            ThrowInvalidOperationException(penetration);
        }
        f64Vector3 positionDelta = pushDir * penetration;
        f64Vector3 impactComponent = pushDir * (f64Vector3.Dot(pushDir, velocity));
        return new Collision(positionDelta, impactComponent);

        [DoesNotReturn]
        static void ThrowInvalidOperationException(fix64 penetration)
        {
            throw new InvalidOperationException("Expected non-negative penetration, got: " + penetration);
        }
    }
}