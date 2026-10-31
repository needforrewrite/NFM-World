using NFMWorldLibrary.Backend;

namespace NFMWorld;

public sealed class MenuCameraDirector
{
    private readonly Random _random = new();
    private BackendStage? _stage;
    private MenuStageSurvey _survey = null!;
    private Vector3[] _positions = [];
    private Vector3[] _targets = [];
    private float _age;
    private float _duration;
    private float _startFov;
    private float _endFov;
    private int _lastKind = -1;
    private float _pace;
    public int ShotId { get; private set; }
    public MenuRoadSegment? CurrentRoad { get; private set; }
    public MenuRoadSegment? CarActionRoad { get; private set; }
    public int ViewDistance => (int)Math.Clamp(_survey.Radius * 2, 15000, 45000);

    public float Fade => Math.Max(Math.Clamp(1 - _age / 0.7f, 0, 1),
        Math.Clamp((_age - _duration + 0.7f) / 0.7f, 0, 1));

    public void Reset(PerspectiveCamera camera, BackendStage stage)
    {
        if (!ReferenceEquals(_stage, stage))
        {
            _stage = stage;
            _survey = new MenuStageSurvey(stage);
        }
        _lastKind = -1;
        NextShot(camera, stage);
    }

    private void NextShot(PerspectiveCamera camera, BackendStage stage)
    {
        CurrentRoad = null;
        CarActionRoad = null;
        _positions = new Vector3[65];
        _targets = new Vector3[65];
        var kind = _lastKind < 0 ? 5 : _lastKind == 8 ? _random.Next(8)
            : (_lastKind + _random.Next(1, 8)) % 8;
        if (_lastKind >= 0 && _lastKind != 8 && _random.NextSingle() < 0.12f && TryCarActionShot(stage))
            kind = 8;
        else if (kind is 5 or 6)
            MakeFlyover(kind == 6);
        else if (kind == 7)
            MakeBoundaryGlide();
        else if (kind == 4 || !TryRoadShot(kind))
            MakeOverview();
        var length = 0f;
        for (var i = 1; i < _positions.Length; i++)
            length += Vector3.Distance(_positions[i - 1], _positions[i]);
        _duration = kind == 8 ? Math.Clamp(length / (900 + _random.NextSingle() * 600), 7, 12)
            : Math.Clamp(length / (350 + _random.NextSingle() * 1450), 6, 17);
        _pace = kind == 8 ? 1 : 0.85f + _random.NextSingle() * 0.35f;
        _age = 0;
        _lastKind = kind;
        ShotId++;
        SetFlight(camera, true);
    }

    private bool TryCarActionShot(BackendStage stage)
    {
        if (stage.Checkpoints.Count == 0) return false;
        var finalCheckpoint = stage.Nodes.IndexOf(stage.Checkpoints[stage.Checkpoints.Count - 1]);
        var roads = _survey.Roads.Concat(_survey.Cruises)
            .Where(road => road.Length is >= 4000 and <= 20000
                && road.StartNode < finalCheckpoint && Math.Abs(road.Direction.Y) < 0.3f).ToArray();
        if (roads.Length == 0) return false;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var road = roads[_random.Next(roads.Length)];
            var side = Vector3.Normalize(new Vector3(road.Direction.Z, 0, -road.Direction.X));
            side *= _random.Next(2) == 0 ? 1 : -1;
            var height = 450 + _random.NextSingle() * 300 + attempt / 8 * 220;
            for (var i = 0; i < _positions.Length; i++)
            {
                var t = i / (float)(_positions.Length - 1);
                _positions[i] = road.Start + road.Direction * (-1600 + t * road.Length * 0.85f)
                    + side * (650 + 150 * MathF.Sin(t * MathF.PI)) + new Vector3(0, -height, 0);
            }
            if (!IsPathClear()) continue;
            SetGaze(road.Direction, 10, 14, (_random.NextSingle() - 0.5f) * 4);
            CarActionRoad = road;
            _startFov = 70;
            _endFov = 66;
            return true;
        }
        return false;
    }

    private bool TryRoadShot(int kind)
    {
        var roads = _survey.Cruises.Count == 0 || _random.Next(3) == 0 && _survey.Roads.Count > 0
            ? _survey.Roads : _survey.Cruises;
        if (roads.Count == 0) return false;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var road = roads[_random.Next(roads.Count)];
            var side = Vector3.Normalize(new Vector3(road.Direction.Z, 0, -road.Direction.X));
            side *= _random.Next(2) == 0 ? 1 : -1;
            var altitude = 700 + _random.NextSingle() * 1300 + attempt / 5 * 350;
            var offset = 650 + _random.NextSingle() * 1300;
            for (var i = 0; i < _positions.Length; i++)
            {
                var t = i / (float)(_positions.Length - 1);
                var along = kind == 1 ? 0.9f - t * 0.8f : kind == 2 ? 0.35f + t * 0.3f : 0.1f + t * 0.8f;
                var travel = Vector3.Lerp(road.Start, road.End, along);
                var lateral = kind == 2 ? offset * (1 - 2 * t)
                    : kind == 3 ? offset * (1 - 1.5f * t) : offset;
                _positions[i] = travel + side * lateral + new Vector3(0, -altitude - t * 180, 0);
            }
            if (_positions.Any(position => !_survey.InCourse(position)) || !IsPathClear()) continue;
            SetGaze(kind == 1 ? -road.Direction : road.Direction,
                4 + _random.NextSingle() * 9, 4 + _random.NextSingle() * 9,
                (_random.NextSingle() - 0.5f) * 18);
            CurrentRoad = road;
            _startFov = 58 + _random.NextSingle() * 12;
            _endFov = Math.Clamp(_startFov + (_random.NextSingle() - 0.65f) * 20, 50, 72);
            return true;
        }
        return false;
    }

    private void MakeOverview()
    {
        var roads = _survey.Cruises;
        var anchor = _survey.Center;
        if (roads.Count > 0)
        {
            var closest = roads[_random.Next(roads.Count)];
            anchor = Vector3.Lerp(closest.Start, closest.End, 0.5f);
        }
        var angle = _random.NextSingle() * MathF.Tau;
        var direction = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        var cross = new Vector3(-direction.Z, 0, direction.X);
        var distance = 3500 + _random.NextSingle() * 4500;
        var start = anchor + direction * distance + cross * distance * 0.25f;
        var end = anchor + direction * distance * 0.55f - cross * distance * 0.4f;
        var top = _survey.LocalCeiling(start, end, 1000);
        start.Y = Math.Min(top - 1000, anchor.Y - 1800 - _random.NextSingle() * 1700);
        end.Y = start.Y - 400;
        var bend = (start + end) * 0.5f + cross * distance * (_random.NextSingle() - 0.5f) * 0.3f;
        for (var i = 0; i < _positions.Length; i++)
        {
            var t = i / (float)(_positions.Length - 1);
            _positions[i] = Vector3.Lerp(Vector3.Lerp(start, bend, t), Vector3.Lerp(bend, end, t), t);
        }
        ClearPathHeight();
        SetGaze(-direction, 5 + _random.NextSingle() * 9, 5 + _random.NextSingle() * 9,
            (_random.NextSingle() - 0.5f) * 20);
        _startFov = 62;
        _endFov = 58;
    }

    private void MakeFlyover(bool skySweep)
    {
        var angle = _random.NextSingle() * MathF.Tau;
        var direction = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        var side = new Vector3(-direction.Z, 0, direction.X);
        var length = Math.Clamp(_survey.Radius * (0.6f + _random.NextSingle() * 0.5f), 8000, 24000);
        var offset = side * (_random.NextSingle() - 0.5f) * length * 0.35f;
        var start = _survey.Center - direction * length * 0.5f + offset;
        var end = _survey.Center + direction * length * 0.5f + offset;
        var altitude = 2300 + _random.NextSingle() * 2200;
        start.Y = _survey.Center.Y - altitude;
        end.Y = start.Y + (skySweep ? -500 : 350);
        var bend = (start + end) * 0.5f + side * length * (_random.NextSingle() - 0.5f) * 0.25f;
        for (var i = 0; i < _positions.Length; i++)
        {
            var t = i / (float)(_positions.Length - 1);
            _positions[i] = Vector3.Lerp(Vector3.Lerp(start, bend, t), Vector3.Lerp(bend, end, t), t);
        }
        ClearPathHeight();
        SetGaze(direction, skySweep ? -4 : 8, skySweep ? 7 : 14,
            (skySweep ? 36 : 18) * (_random.Next(2) == 0 ? -1 : 1));
        _startFov = skySweep ? 72 : 68;
        _endFov = skySweep ? 68 : 62;
    }

    private void MakeBoundaryGlide()
    {
        var edge = _random.Next(4);
        var minimum = _survey.BoundaryMin;
        var maximum = _survey.BoundaryMax;
        var center = (minimum + maximum) * 0.5f;
        var margin = 1200 + _random.NextSingle() * 2000;
        Vector3 start;
        Vector3 end;
        Vector3 inward;
        if (edge < 2)
        {
            var x = edge == 0 ? minimum.X - margin : maximum.X + margin;
            var halfLength = Math.Clamp((maximum.Z - minimum.Z) * 0.35f, 4000, 11000);
            start = new Vector3(x, 0, center.Z - halfLength);
            end = new Vector3(x, 0, center.Z + halfLength);
            inward = edge == 0 ? Vector3.UnitX : -Vector3.UnitX;
        }
        else
        {
            var z = edge == 2 ? minimum.Z - margin : maximum.Z + margin;
            var halfLength = Math.Clamp((maximum.X - minimum.X) * 0.35f, 4000, 11000);
            start = new Vector3(center.X - halfLength, 0, z);
            end = new Vector3(center.X + halfLength, 0, z);
            inward = edge == 2 ? Vector3.UnitZ : -Vector3.UnitZ;
        }
        if (_random.Next(2) == 0)
            (start, end) = (end, start);
        start.Y = _survey.Center.Y - 2200 - _random.NextSingle() * 2000;
        end.Y = start.Y - 200;
        for (var i = 0; i < _positions.Length; i++)
            _positions[i] = Vector3.Lerp(start, end, i / (float)(_positions.Length - 1));
        ClearPathHeight();
        SetGaze(inward, 4, 10, (_random.NextSingle() - 0.5f) * 12);
        _startFov = 70;
        _endFov = 64;
    }

    private void ClearPathHeight()
    {
        if (!IsPathClear())
        {
            var localCeiling = _survey.LocalCeiling(_positions.Aggregate(Vector3.Min),
                _positions.Aggregate(Vector3.Max), 250);
            for (var i = 0; i < _positions.Length; i++)
                _positions[i].Y = localCeiling - 800;
        }
    }

    private void SetGaze(Vector3 forward, float startPitch, float endPitch, float yawSweep)
    {
        forward.Y = 0;
        forward = Vector3.Normalize(forward);
        for (var i = 0; i < _positions.Length; i++)
        {
            var t = i / (float)(_positions.Length - 1);
            var yaw = (t - 0.5f) * yawSweep * MathF.PI / 180;
            var pitch = (startPitch + (endPitch - startPitch) * t) * MathF.PI / 180;
            var direction = new Vector3(forward.X * MathF.Cos(yaw) - forward.Z * MathF.Sin(yaw),
                MathF.Tan(pitch), forward.X * MathF.Sin(yaw) + forward.Z * MathF.Cos(yaw));
            _targets[i] = _positions[i] + direction * 12000;
        }
    }

    private bool IsPathClear()
    {
        for (var i = 0; i < _positions.Length; i++)
        {
            if (!_survey.IsClear(_positions[i], _positions[Math.Max(0, i - 1)], 250)) return false;
        }
        return true;
    }

    public void Tick(PerspectiveCamera camera, BackendStage stage, float seconds)
    {
        _age += seconds;
        if (_age >= _duration)
        {
            NextShot(camera, stage);
        }
        else
            SetFlight(camera, false);
    }

    private void SetFlight(PerspectiveCamera camera, bool cut)
    {
        var t = MathF.Pow(Math.Clamp(_age / _duration, 0, 1), _pace);
        var sample = t * (_positions.Length - 1);
        var index = Math.Min((int)sample, _positions.Length - 2);
        var position = Vector3.Lerp(_positions[index], _positions[index + 1], sample - index);
        var target = Vector3.Lerp(_targets[index], _targets[index + 1], sample - index);
        camera.Fov = _startFov + (_endFov - _startFov) * t;
        if (cut)
        {
            camera.UpWithoutInterpolation = -Vector3.UnitY;
            camera.PositionWithoutInterpolation = position;
            camera.LookAtWithoutInterpolation = target;
        }
        else
        {
            camera.Position = position;
            camera.LookAt = target;
        }
    }

    public void ShowCar(PerspectiveCamera camera, BackendCar car, float seconds, bool cut = false, float garageBlend = 0)
    {
        var target = ToVector(car.Position) + new Vector3(0, -120, 0);
        var angle = 0.65f + 1.7f * garageBlend + MathF.Sin(seconds * 0.06f) * 0.12f * (1 - garageBlend);
        var radius = 1600 - 400 * garageBlend;
        var height = 500 - 170 * garageBlend;
        var position = target + new Vector3(MathF.Cos(angle) * radius, -height, MathF.Sin(angle) * radius);
        // Keep the car on the right, clear of the menu.
        var framing = 410 - 130 * garageBlend;
        target += new Vector3(MathF.Sin(angle) * framing, 0, -MathF.Cos(angle) * framing);
        camera.Fov = 53;
        if (cut)
        {
            camera.UpWithoutInterpolation = -Vector3.UnitY;
            camera.PositionWithoutInterpolation = position;
            camera.LookAtWithoutInterpolation = target;
        }
        else
        {
            camera.Position = position;
            camera.LookAt = target;
        }
    }

    private static Vector3 ToVector(f64Vector3 value)
        => new((float)value.X, (float)value.Y, (float)value.Z);
}
