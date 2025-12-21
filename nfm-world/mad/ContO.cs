using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

// temp conto for nfmm compatibility
public class ContO
{
    private readonly Mesh _mesh;
        
    public int X 
    {
        get => (int) _mesh.Position.X;
        set => _mesh.Position = _mesh.Position with { X = value };
    }
    public int Y 
    {
        get => (int) _mesh.Position.Y;
        set => _mesh.Position = _mesh.Position with { Y = value };
    }
    public int Z 
    {
        get => (int) _mesh.Position.Z;
        set => _mesh.Position = _mesh.Position with { Z = value };
    }
    public float Xz 
    {
        get => _mesh.Rotation.Xz.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Xz = AngleSingle.FromDegrees(value) };
    }
    public float Xy 
    {
        get => _mesh.Rotation.Xy.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Xy = AngleSingle.FromDegrees(value) };
    }
    public float Zy 
    {
        get => _mesh.Rotation.Zy.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Zy = AngleSingle.FromDegrees(value) };
    }

    public int Grat => _mesh.GroundAt;
    
    // wheel rotation
    public float Wzy
    {
        get => _mesh.TurningWheelAngle.Zy.Degrees;
        set
        {
            _mesh.TurningWheelAngle = _mesh.TurningWheelAngle with { Zy = AngleSingle.FromDegrees(value) };
            _mesh.WheelAngle = _mesh.WheelAngle with { Zy = AngleSingle.FromDegrees(value) };
        }
    }

    public float Wxz
    {
        get => _mesh.TurningWheelAngle.Xz.Degrees;
        set => _mesh.TurningWheelAngle = _mesh.TurningWheelAngle with { Xz = AngleSingle.FromDegrees(value) };
    }
    
    // wheel position
    public int[] Keyx { get; }
    public int[] Keyz { get; }
    
    public bool Wasted
    {
        get => _mesh.Wasted;
        set => _mesh.Wasted = value;
    }
    
    public int Fcnt { get; set; } // TODO car fixed ticks

    public ContO(Mesh mesh)
    {
        _mesh = mesh;

        Keyx = mesh.Wheels.Select(e => (int)e.Position.X).ToArray();
        Keyz = mesh.Wheels.Select(e => (int)e.Position.Z).ToArray();
    }

    public static implicit operator ContO(Mesh mesh) => new ContO(mesh);

    public void DamageX(CarStats stat, int wheelnum, float amount)
    {
        MeshDamage.DamageX(stat, _mesh, wheelnum, amount);
    }
    public void DamageY(CarStats stat, int wheelnum, float amount, bool mtouch, int nbsq, int squash)
    {
        MeshDamage.DamageY(stat, _mesh, wheelnum, amount, mtouch, ref nbsq, ref squash);
    }
    public void DamageZ(CarStats stat, int wheelnum, float amount)
    {
        MeshDamage.DamageZ(stat, _mesh, wheelnum, amount);
    }

    public void Dust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
    {
        _mesh.AddDust(wheelidx, wheelx, wheely, wheelz, scx, scz, simag, tilt, onRoof, wheelGround);
	}

    public void Spark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        _mesh.Spark(wheelx, wheely, wheelz, scx, scy, scz, type, wheelGround);
    }
}