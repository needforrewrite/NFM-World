using SoftFloat;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

// temp conto for nfmm compatibility
public class ContO
{
    private readonly Mesh _mesh;
        
    public fix64 X 
    {
        get => (fix64) _mesh.Position.X;
        set => _mesh.Position = _mesh.Position with { X = (float)value };
    }
    public fix64 Y 
    {
        get => (fix64) _mesh.Position.Y;
        set => _mesh.Position = _mesh.Position with { Y = (float)value };
    }
    public fix64 Z 
    {
        get => (fix64) _mesh.Position.Z;
        set => _mesh.Position = _mesh.Position with { Z = (float)value };
    }
    public fix64 Xz 
    {
        get => _mesh.Rotation.Xz.DegreesSFloat;
        set => _mesh.Rotation = _mesh.Rotation with { Xz = AngleSingle.FromDegrees(value) };
    }
    public fix64 Xy 
    {
        get => _mesh.Rotation.Xy.DegreesSFloat;
        set => _mesh.Rotation = _mesh.Rotation with { Xy = AngleSingle.FromDegrees(value) };
    }
    public fix64 Zy 
    {
        get => _mesh.Rotation.Zy.DegreesSFloat;
        set => _mesh.Rotation = _mesh.Rotation with { Zy = AngleSingle.FromDegrees(value) };
    }

    public int Grat => _mesh.GroundAt;
    
    // wheel rotation
    public fix64 Wzy
    {
        get => _mesh.TurningWheelAngle.Zy.DegreesSFloat;
        set
        {
            _mesh.TurningWheelAngle = _mesh.TurningWheelAngle with { Zy = AngleSingle.FromDegrees(value) };
            _mesh.WheelAngle = _mesh.WheelAngle with { Zy = AngleSingle.FromDegrees(value) };
        }
    }

    public fix64 Wxz
    {
        get => _mesh.TurningWheelAngle.Xz.DegreesSFloat;
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
    public int MaxR => _mesh.MaxRadius;

    public ContO(Mesh mesh)
    {
        _mesh = mesh;

        Keyx = Array.ConvertAll(mesh.Wheels, static e => (int)e.Position.X);
        Keyz = Array.ConvertAll(mesh.Wheels, static e => (int)e.Position.Z);
    }

    public static implicit operator ContO(Mesh mesh) => new ContO(mesh);

    public void DamageX(CarStats stat, int wheelnum, fix64 amount)
    {
        MeshDamage.DamageX(stat, _mesh, wheelnum, (float)amount);
    }
    public void DamageY(CarStats stat, int wheelnum, fix64 amount, bool mtouch, int nbsq, int squash)
    {
        MeshDamage.DamageY(stat, _mesh, wheelnum, (float)amount, mtouch, ref nbsq, ref squash);
    }
    public void DamageZ(CarStats stat, int wheelnum, fix64 amount)
    {
        MeshDamage.DamageZ(stat, _mesh, wheelnum, (float)amount);
    }

    public void Dust(int wheelidx, fix64 wheelx, fix64 wheely, fix64 wheelz, int scx, int scz, fix64 simag, int tilt, bool onRoof, int wheelGround)
    {
        _mesh.AddDust(wheelidx, (float)wheelx, (float)wheely, (float)wheelz, scx, scz, (float)simag, tilt, onRoof, wheelGround);
	}

    public void Spark(fix64 wheelx, fix64 wheely, fix64 wheelz, fix64 scx, fix64 scy, fix64 scz, int type, int wheelGround)
    {
        _mesh.Spark((float)wheelx, (float)wheely, (float)wheelz, (float)scx, (float)scy, (float)scz, type, wheelGround);
    }
}