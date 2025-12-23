using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class FollowCamera
{
    public int FollowYOffset = 0;

    private float _bcxz;
    private Euler _angle;
    public int FollowZOffset = 0;

    public void Follow(PerspectiveCamera camera, Mesh mesh, float cxz, int lookback)
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
        camera.Position = camera.Position with
        {
            X = mesh.Position.X + (followDistance * UMath.Sin(cxz)),
            Z = mesh.Position.Z - (followDistance * UMath.Cos(cxz)),
            Y = mesh.Position.Y - 250 - FollowYOffset,
        };
        
        // Calculate the look direction by rotating the forward vector
        var lookDirection = (_angle * Vector3.UnitZ) * 100;
        // LookAt should be a target point, not a direction - add direction to position
        var lookAtPoint = camera.Position + lookDirection;
        camera.LookAt = lookAtPoint;
    }
}