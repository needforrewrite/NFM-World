using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.util;

namespace nfm_world.camera;

public class AroundStageCamera
{
    private float _hit = 45000f;
    private float _focus = 1f;
    private float _gofocus = 0.33f;
    private float _pitch = 67f;
    private float _yaw = 0f;
    private float _fallen = 0f;
    private float _startX = 0f;
    private float _startZ = 0f;
    private float _targetX = 0f;
    private float _targetY = 0f;
    private float _targetZ = 0f;

    private int _pointCount = 0;
    private int _point = 0;

    private void Reset()
    {
        _hit = 45000f;
        _focus = 1f;
        _pitch = 67f;
        _yaw = 0f;
        _fallen = 0f;
    }

    public void AroundStage(PerspectiveCamera camera, IStage stage)
    {
        if (_hit > 5000f)
        {
            camera.Fov = 400f;
            FallIntoPlace(stage);
        }
        else
        {
            FollowStage(camera, stage);
        }

        Vector3 location = new(_targetX, _targetY + 250, _targetZ);

        SinCosFloat sincos = new(_yaw);
        camera.Position = new Vector3(
            _targetX + (17000f * sincos.Cos),
            _targetY - _hit,
            _targetZ + (17000f * sincos.Sin)
        );
        camera.LookAt = location;
    }

    private void FollowStage(PerspectiveCamera camera, IStage stage)
    {
        camera.Fov = 400f * _focus;
        if(Math.Abs(_focus - _gofocus) > 0.005)
        {
            if(_focus > _gofocus)
            {
                _focus -= 0.005f * Physics.PHYSICS_MULTIPLIER;
            }
            else
            {
                _focus += 0.005f * Physics.PHYSICS_MULTIPLIER;
            }
        } else
        {
            _gofocus = 0.35f + (float)URandom.Double() * 1.3f;
        }

        _targetX -= (_targetX - (float)stage.nodes[_point].Position.X) / 10f * Physics.PHYSICS_MULTIPLIER;
        _targetY -= (_targetY - (float)stage.nodes[_point].Position.Y) / 10f * Physics.PHYSICS_MULTIPLIER;
        _targetZ -= (_targetZ - (float)stage.nodes[_point].Position.Z) / 10f * Physics.PHYSICS_MULTIPLIER;

        if (_pointCount >= 45 * Physics.PHYSICS_MULTIPLIER)
        {
            _point++;
            if (_point >= stage.nodes.Count)
            {
                _point = 0;
            }
            _pointCount = 0;
        }
        else
        {
            _pointCount += 1;
        }

        _yaw += 1f * Physics.PHYSICS_MULTIPLIER;
        if (_yaw >= 360)
        {
            _yaw -= 360;
        };
    }

    private void FallIntoPlace(IStage stage)
    {
        if (_hit == 45000f)
        {
            _startX = ((float)stage.nodes[0].Position.X - _targetX) / 116f;
            _startZ = ((float)stage.nodes[0].Position.Z - _targetZ) / 116f;
        }

        _hit -= _fallen;
        _fallen += 7 * Physics.PHYSICS_MULTIPLIER;

        _targetX += _startX * Physics.PHYSICS_MULTIPLIER;
        _targetZ += _startZ * Physics.PHYSICS_MULTIPLIER;

        if (_hit < 17600)
        {
            _pitch -= 2 * Physics.PHYSICS_MULTIPLIER;
        }

        if (_fallen > 500 * Physics.PHYSICS_MULTIPLIER)
        {
            _fallen = 500 * Physics.PHYSICS_MULTIPLIER;
        }

        if (_hit < 5000f)
        {
            _hit = 5000f;
            _fallen = 0f;
        }

        _yaw += 3 * Physics.PHYSICS_MULTIPLIER;
        if (_yaw >= 360)
        {
            _yaw -= 360;
        }
    }
}