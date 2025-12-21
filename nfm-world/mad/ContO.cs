using SoftFloat;
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
    public sfloat Xz 
    {
        get => (sfloat)_mesh.Rotation.Xz.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Xz = AngleSingle.FromDegrees((float)value) };
    }
    public sfloat Xy 
    {
        get => (sfloat)_mesh.Rotation.Xy.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Xy = AngleSingle.FromDegrees((float)value) };
    }
    public sfloat Zy 
    {
        get => (sfloat)_mesh.Rotation.Zy.Degrees;
        set => _mesh.Rotation = _mesh.Rotation with { Zy = AngleSingle.FromDegrees((float)value) };
    }

    public int Grat => _mesh.GroundAt;
    
    // wheel rotation
    public sfloat Wzy
    {
        get => (sfloat)_mesh.TurningWheelAngle.Zy.Degrees;
        set
        {
            _mesh.TurningWheelAngle = _mesh.TurningWheelAngle with { Zy = AngleSingle.FromDegrees((float)value) };
            _mesh.WheelAngle = _mesh.WheelAngle with { Zy = AngleSingle.FromDegrees((float)value) };
        }
    }

    public sfloat Wxz
    {
        get => (sfloat)_mesh.TurningWheelAngle.Xz.Degrees;
        set => _mesh.TurningWheelAngle = _mesh.TurningWheelAngle with { Xz = AngleSingle.FromDegrees((float)value) };
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

    public void DamageX(CarStats stat, int wheelnum, sfloat amount)
    {
        MeshDamage.DamageX(stat, _mesh, wheelnum, (float)amount);
    }
    public void DamageY(CarStats stat, int wheelnum, sfloat amount, bool mtouch, int nbsq, int squash)
    {
        MeshDamage.DamageY(stat, _mesh, wheelnum, (float)amount, mtouch, ref nbsq, ref squash);
    }
    public void DamageZ(CarStats stat, int wheelnum, sfloat amount)
    {
        MeshDamage.DamageZ(stat, _mesh, wheelnum, (float)amount);
    }

    public void Dust(int wheelidx, sfloat wheelx, sfloat wheely, sfloat wheelz, int scx, int scz, sfloat simag, int tilt, bool onRoof, int wheelGround)
    {
        _mesh.AddDust(wheelidx, (float)wheelx, (float)wheely, (float)wheelz, scx, scz, (float)simag, tilt, onRoof, wheelGround);
	}

    public void Spark(sfloat wheelx, sfloat wheely, sfloat wheelz, sfloat scx, sfloat scy, sfloat scz, int type, int wheelGround)
    {
        _mesh.Spark((float)wheelx, (float)wheely, (float)wheelz, (float)scx, (float)scy, (float)scz, type, wheelGround);
    }
}