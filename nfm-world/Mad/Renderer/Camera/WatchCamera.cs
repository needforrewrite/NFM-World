using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld;

/// <summary>
/// Trackside camera: stays put and turns to watch the car go by, then jumps ahead of it once
/// it has drawn too far away.
/// </summary>
public class WatchCamera
{
    /// <summary>Distance at which the camera gives up and relocates ahead of the car.</summary>
    private const float RelocateDistance = 6000f;

    /// <summary>
    /// phy eased the pitch by a fifth of the remaining error per 21.4 tps tick; this is the same
    /// decay expressed per 63 tps tick.
    /// </summary>
    private static readonly float PitchEase = 1f - MathF.Pow(1f - 1f / 5f, Physics.PHYSICS_MULTIPLIER);

    private bool _relocate = true;
    private float _lookY;

    /// <summary>Makes the next frame pick a fresh vantage point.</summary>
    public void Reset() => _relocate = true;

    public void Watch(PerspectiveCamera camera, ITransform obj, float mxz)
    {
        camera.Fov = CameraSettings.Fov;

        var carX = (float)obj.Position.X;
        var carY = (float)obj.Position.Y;
        var carZ = (float)obj.Position.Z;

        var snap = _relocate;
        var position = camera.Position;
        if (_relocate)
        {
            // 5000 ahead along the car's heading, 400 to the side, up to 1400 above it.
            var sc = new SinCosFloat(mxz);
            position = new Vector3(
                carX + 400f * sc.Cos - 5000f * sc.Sin,
                carY - 300f - 1100f * System.Random.Shared.NextSingle(),
                carZ + 400f * sc.Sin + 5000f * sc.Cos
            );
            _lookY = carY;
            _relocate = false;
        }
        else
        {
            _lookY += (carY - _lookY) * PitchEase;
        }

        var lookAt = new Vector3(carX, _lookY, carZ);
        if (snap)
        {
            camera.PositionWithoutInterpolation = position;
            camera.LookAtWithoutInterpolation = lookAt;
        }
        else
        {
            camera.LookAt = lookAt;
        }

        if (Vector3.Distance(position, new Vector3(carX, carY, carZ)) > RelocateDistance)
        {
            _relocate = true;
        }
    }
}
