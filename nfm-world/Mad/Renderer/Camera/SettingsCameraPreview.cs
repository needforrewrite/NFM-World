using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld;

internal sealed class SettingsCameraPreview
{
    private readonly FollowCamera _follow = new();
    private readonly PerspectiveCamera _target = new();
    private Camera.CameraState _normal;
    private float _normalFov;
    private float _blend;

    public void RestoreNormal(PerspectiveCamera camera)
    {
        if (_blend <= 0) return;
        camera.Position = _normal.Position;
        camera.LookAt = _normal.LookAt;
        camera.Up = _normal.Up;
        camera.Fov = _normalFov;
    }

    public void Reset() => _blend = 0;

    public void Update(PerspectiveCamera camera, BackendCar car, bool active)
    {
        _normal = new Camera.CameraState(camera.Position, camera.LookAt, camera.Up);
        _normalFov = camera.Fov;
        if (_blend == 0)
            _target.Fov = camera.Fov;
        _blend = Math.Clamp(_blend + (active ? 1 : -1) / (Physics.TargetTps * 0.9f), 0, 1);
        if (_blend <= 0) return;

        _follow.Follow(_target, car, (float)car.CarPhysics.Cxz, 0, 0, car.Stats.Swits[2]);
        var t = _blend * _blend * (3 - 2 * _blend);
        var normalDirection = Vector3.Normalize(camera.LookAt - camera.Position);
        var followDirection = Vector3.Normalize(_target.LookAt - _target.Position);
        camera.Position = Vector3.Lerp(camera.Position, _target.Position, t);
        var rotation = Quaternion.Slerp(
            Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, normalDirection, camera.Up)),
            Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, followDirection, -Vector3.UnitY)), t);
        camera.LookAt = camera.Position + Vector3.Transform(-Vector3.UnitZ, rotation) * 100;
        camera.Up = Vector3.Transform(Vector3.UnitY, rotation);
        camera.Fov = _normalFov + (_target.Fov - _normalFov) * t;
    }
}
