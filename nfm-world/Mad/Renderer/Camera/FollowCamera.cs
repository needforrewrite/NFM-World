using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using NFMWorldMath;

namespace NFMWorld;

public class FollowCamera
{
    public static int FollowYOffset = 0;

    private float _bcxz;
    private Euler _angle;
    public static int FollowZOffset = 0;

    private static int _oldlookback = 0;

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
            if (lookback == 2)   //look right
            {
                if (_bcxz > -90) {
                    //_bcxz -= i28;
                    _bcxz = -90;
                }
                if (_bcxz < -90) {
                    _bcxz = -90;
                }
            }
            if (lookback == 3)   //look left
            {
                if (_bcxz < 90) {
                    //_bcxz += i28;
                    _bcxz = 90;
                }
                if (_bcxz > 90) {
                    _bcxz = 90;
                }
            }
            if (lookback == -1)  // look back
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