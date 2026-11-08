using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using NFMWorldMath;

namespace NFMWorld;

public class FollowCamera
{
    public static int FollowYOffset
    {
        get;
        set => field = Math.Clamp(value, CameraPreset.MinFollowY, CameraPreset.MaxFollowY);
    }

    private float _bcxz;
    private Euler _angle;

    public static int FollowZOffset
    {
        get;
        set => field = Math.Clamp(value, CameraPreset.MinFollowZ, CameraPreset.MaxFollowZ);
    }

    private static int _oldlookback = 0;

    /// <summary>
    /// Fraction of the remaining aim error turned each tick by the car cam. At 63 ticks/s this
    /// settles a checkpoint-sized retarget in roughly 0.4 s: long enough to read as a turn rather
    /// than a cut, short enough not to lag behind the arrow.
    /// </summary>
    private const float AimSmoothing = 0.16f;

    private float _aimAngle;
    private bool _aimSet;

    /// <summary>Drops the eased aim, so the next car-cam frame starts on its target.</summary>
    public void ResetAim() => _aimSet = false;

    /// <summary>
    /// Sits behind the car like <see cref="Follow"/>, but aims at <paramref name="target"/> instead
    /// of straight down the car's nose: the camera looks where the guidance arrow points.
    /// </summary>
    public void FollowTarget(
        PerspectiveCamera camera,
        ITransform obj,
        float cxz,
        float speed,
        float topSpeed,
        Vector3 target,
        bool interpolate
    )
    {
        var followDistance = 800 + FollowZOffset;
        if (CameraSettings.SmoothFov)
        {
            var targetFov = float.Lerp(CameraSettings.Fov, CameraSettings.Fov * 1.2f, Math.Abs(speed) / topSpeed);
            camera.Fov = float.Lerp(camera.Fov, targetFov, 0.075f);
        }
        else
        {
            camera.Fov = CameraSettings.Fov;
        }

        var carX = (float)obj.Position.X;
        var carY = (float)obj.Position.Y;
        var carZ = (float)obj.Position.Z;

        // Direction from the car towards the target, which is what this camera turns to face.
        // Falls back to the car's own forward, (-sin, cos) - the follow camera sits at the
        // opposite offset, so that is the direction it looks along.
        var dx = target.X - carX;
        var dz = target.Z - carZ;
        var length = MathF.Sqrt(dx * dx + dz * dz);
        float wantX, wantZ;
        if (length > 0.01f)
        {
            wantX = dx / length;
            wantZ = dz / length;
        }
        else
        {
            wantX = -(float)UMath.Sin(cxz);
            wantZ = (float)UMath.Cos(cxz);
        }

        // Ease the aim angle rather than snapping to it: passing a checkpoint retargets the arrow
        // instantly, which would whip the whole view round in one tick. Turning by the shortest
        // arc also means a retarget close to 180 degrees sweeps through the near side instead of
        // flipping the camera. The first call after a mode switch starts on target.
        var wanted = MathF.Atan2(wantZ, wantX);
        if (!_aimSet)
        {
            _aimAngle = wanted;
            _aimSet = true;
        }
        else
        {
            var delta = wanted - _aimAngle;
            delta -= MathF.Tau * MathF.Round(delta / MathF.Tau);
            _aimAngle += delta * AimSmoothing;
            // Keep the accumulator bounded so it stays comparable with Atan2's output forever.
            _aimAngle -= MathF.Tau * MathF.Floor(_aimAngle / MathF.Tau);
        }

        var dirX = MathF.Cos(_aimAngle);
        var dirZ = MathF.Sin(_aimAngle);

        // Sit behind the car on that line and aim a short way AHEAD of the car, not at the
        // target. Aiming at the target loses the car off the bottom of the frame once the target
        // is more than a little way off: the camera is only ~450 up, so a target a few thousand
        // units out leaves the car tens of degrees below the view axis. Aiming just ahead keeps
        // the car on the axis, low in the frame, with the target direction straight up the screen.
        var position = new Vector3(
            carX - dirX * followDistance,
            carY - 250 - FollowYOffset - 200,
            carZ - dirZ * followDistance
        );
        var lookAt = new Vector3(carX + dirX * 300f, carY, carZ + dirZ * 300f);

        if (interpolate)
        {
            camera.Position = position;
            camera.LookAt = lookAt;
        }
        else
        {
            camera.PositionWithoutInterpolation = position;
            camera.LookAtWithoutInterpolation = lookAt;
        }
    }

    public void Follow(PerspectiveCamera camera, ITransform obj, float cxz, int lookback, float speed, float topSpeed)
    {
        // x: yaw = xz
        // y: pitch = zy
        // z: roll = xy
        _angle.Pitch = AngleSingle.FromDegrees(-10);
        var i28 = 2 + Math.Abs(_bcxz) / 4;
        if (i28 > 20)
        {
            i28 = 20;
        }

        var interpolateAngle = true;
        if (lookback != _oldlookback)
        {
            _oldlookback = lookback;
            interpolateAngle = false;
        }

        if (lookback != 0)
        {
            if (lookback == 2) //look right
            {
                if (_bcxz > -90)
                {
                    //_bcxz -= i28;
                    _bcxz = -90;
                }

                if (_bcxz < -90)
                {
                    _bcxz = -90;
                }
            }

            if (lookback == 3) //look left
            {
                if (_bcxz < 90)
                {
                    //_bcxz += i28;
                    _bcxz = 90;
                }

                if (_bcxz > 90)
                {
                    _bcxz = 90;
                }
            }

            if (lookback == -1) // look back
            {
                if (_bcxz > -180)
                {
                    //_bcxz -= i28;
                    _bcxz = -180;
                }

                if (_bcxz < -180)
                {
                    _bcxz = -180;
                }
            }
        }
        else if (Math.Abs(_bcxz) > i28)
        {
            if (_bcxz > 0)
            {
                //_bcxz -= i28;
                _bcxz = 0;
            }
            else
            {
                //_bcxz += i28;
                _bcxz = 0;
            }
        }
        else
        {
            _bcxz = 0;
        }

        cxz += _bcxz;
        _angle.Yaw = AngleSingle.FromDegrees(-cxz);

        var followDistance = 800 + FollowZOffset;

        if (CameraSettings.SmoothFov)
        {
            var targetFov = float.Lerp(CameraSettings.Fov, CameraSettings.Fov * 1.2f, Math.Abs(speed) / topSpeed);
            camera.Fov = float.Lerp(camera.Fov, targetFov, 0.075f);
        }
        else
        {
            camera.Fov = CameraSettings.Fov;
        }

        if (interpolateAngle)
        {
            camera.Position = camera.Position with
            {
                X = (float)(obj.Position.X + (followDistance * UMath.Sin(cxz))),
                Z = (float)(obj.Position.Z - (followDistance * UMath.Cos(cxz))),
                Y = (float)(obj.Position.Y - 250 - FollowYOffset),
            };

            // Calculate the look direction by rotating the forward vector
            var lookDirection = (_angle * Vector3.UnitZ) * 100;
            // LookAt should be a target point, not a direction - add direction to position
            var lookAtPoint = camera.Position + lookDirection;

            camera.LookAt = lookAtPoint;
        }
        else
        {
            camera.PositionWithoutInterpolation = camera.Position with
            {
                X = (float)(obj.Position.X + (followDistance * UMath.Sin(cxz))),
                Z = (float)(obj.Position.Z - (followDistance * UMath.Cos(cxz))),
                Y = (float)(obj.Position.Y - 250 - FollowYOffset),
            };

            // Calculate the look direction by rotating the forward vector
            var lookDirection = (_angle * Vector3.UnitZ) * 100;
            // LookAt should be a target point, not a direction - add direction to position
            var lookAtPoint = camera.Position + lookDirection;

            camera.LookAtWithoutInterpolation = lookAtPoint;
        }
    }
}
