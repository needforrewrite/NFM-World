using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld;

/// <summary>Navigation keys held for <see cref="FreelookCamera"/>.</summary>
public readonly record struct FreelookInput(bool Forward, bool Back, bool Left, bool Right, bool Rise, bool Sink);

/// <summary>
/// Free-flying camera. Picks up from wherever the previous view left the camera, then flies
/// under the player's control at phy's rates (per 21.4 tps tick), scaled down to the 63 tps loop.
/// </summary>
public class FreelookCamera
{
    private const float MoveSpeed = 500f;
    private const float TurnSpeed = 5f;
    private const float ClimbSpeed = 100f;
    private const float MinY = -75000f;
    private const float MaxY = -300f;

    private bool _active;
    private float _yaw;
    private Vector3 _position;

    /// <summary>Makes the next frame start from the camera's current pose.</summary>
    public void Reset() => _active = false;

    public void Fly(PerspectiveCamera camera, in FreelookInput input)
    {
        camera.Fov = CameraSettings.Fov;

        if (!_active)
        {
            _active = true;
            _position = camera.Position;
            var forward = camera.LookAt - camera.Position;
            _yaw = MathF.Atan2(forward.X, forward.Z) * (180f / MathF.PI);
        }

        var move = MoveSpeed * Physics.PHYSICS_MULTIPLIER;
        var turn = TurnSpeed * Physics.PHYSICS_MULTIPLIER;
        var climb = ClimbSpeed * Physics.PHYSICS_MULTIPLIER;

        var sc = new SinCosFloat(_yaw);
        if (input.Forward)
        {
            _position.X += move * sc.Sin;
            _position.Z += move * sc.Cos;
        }

        if (input.Back)
        {
            _position.X -= move * sc.Sin;
            _position.Z -= move * sc.Cos;
        }

        // Reversing flips the steering, like a car.
        var steer = input.Back ? -1f : 1f;
        if (input.Left) _yaw -= turn * steer;
        if (input.Right) _yaw += turn * steer;

        if (input.Sink) _position.Y += climb;
        if (input.Rise) _position.Y -= climb;
        _position.Y = Math.Clamp(_position.Y, MinY, MaxY);

        // A shallow downward tilt (phy's 6 degrees), 1000 units out.
        var look = new SinCosFloat(_yaw);
        camera.Position = _position;
        camera.LookAt = _position + new Vector3(look.Sin * 1000f, 1000f * MathF.Tan(6f * MathF.PI / 180f), look.Cos * 1000f);
    }
}
