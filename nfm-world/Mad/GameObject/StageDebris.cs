using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Rendering;

namespace NFMWorld;

internal sealed class StageDebris : GameObject, IDisposable
{
    private sealed class Shard(Mesh mesh, f64Vector3 velocity, f64Vector3 spin) : MeshedGameObject(mesh)
    {
        public f64Vector3 Velocity = velocity;
        public readonly f64Vector3 Spin = spin;
    }

    private readonly List<Shard> _shards = [];
    private int _age;
    public bool Expired => _age >= Physics.TargetTps * 4;

    public StageDebris(StageObject obj, f64Vector3 impactVelocity)
    {
        var random = new System.Random(obj.DestructionId);
        var world = Matrix.CreateFromEuler((Euler)obj.Rotation) * Matrix.CreateTranslation((Vector3)obj.Position);
        var speed = Math.Clamp((float)impactVelocity.LengthNoOverflow(), 20, 500);
        float Scatter() => (random.NextSingle() * 2 - 1) * speed * 0.25f;

        try
        {
            var polys = StageObjectDamage.Deform(obj);
            var step = Math.Max(1, (polys.Length + 95) / 96);

            for (var i = 0; i < polys.Length; i += step)
            {
                var poly = polys[i];

                if (poly.Points.Length < 3)

                    continue;
                var center = Vector3.Transform(poly.Centroid, world);
                var points = poly.Points.Select(p => Vector3.Transform(p, world) - center).ToArray();
                var mesh = new Mesh(new Rad3d([poly.WithPoints(points, poly.Triangles)], false, "stage-shard"));
                var velocity = impactVelocity * (fix64)0.65f
                               + new f64Vector3((fix64)Scatter(), (fix64)(-speed * 0.2f - Math.Abs(Scatter())), (fix64)Scatter());
                var shard = new Shard(
                    mesh,
                    velocity,
                    new f64Vector3((fix64)Scatter(), (fix64)Scatter(), (fix64)Scatter())
                )
                {
                    Parent = this,
                    PositionWithoutInterpolation = (f64Vector3)center,
                    RotationWithoutInterpolation = f64Euler.Identity,
                    CastsShadow = false
                };
                _shards.Add(shard);
            }

            Children = _shards;
        }
        catch
        {
            Dispose();

            throw;
        }
    }

    public override void GameTick(BackendStage? stage = null)
    {
        base.GameTick(stage);
        _age++;
        var dt = Physics.PHYSICS_MULTIPLIER_F64;

        foreach (var shard in _shards)
        {
            shard.Velocity += new f64Vector3(0, (fix64)4 * dt, 0);
            shard.Position += shard.Velocity * dt;

            if (shard.Position.Y > 250 && shard.Velocity.Y > 0)
            {
                shard.Position = shard.Position with { Y = (fix64)250 };
                shard.Velocity = new f64Vector3(
                    shard.Velocity.X * (fix64)0.7f,
                    -shard.Velocity.Y * (fix64)0.3f,
                    shard.Velocity.Z * (fix64)0.7f
                );
            }

            shard.Rotation = new f64Euler(
                f64AngleSingle.FromDegrees(shard.Rotation.Xz.Degrees + shard.Spin.X * dt),
                f64AngleSingle.FromDegrees(shard.Rotation.Zy.Degrees + shard.Spin.Y * dt),
                f64AngleSingle.FromDegrees(shard.Rotation.Xy.Degrees + shard.Spin.Z * dt)
            );
            shard.AlphaOverride = Math.Clamp((Physics.TargetTps * 4 - _age) / Physics.TargetTps, 0, 1);
        }
    }

    public void Dispose()
    {
        foreach (var shard in _shards)
            shard.Mesh.Dispose();
        _shards.Clear();
        Children = [];
    }
}
