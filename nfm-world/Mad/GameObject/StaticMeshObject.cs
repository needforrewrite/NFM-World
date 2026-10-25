using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

/// <summary>
/// A static mesh object backed by a <see cref="Rad3d"/> definition.
/// Use for editor previews, model viewer, and anywhere you need to render a RAD mesh
/// without a full physics-backed <see cref="IInGameCar"/>.
/// </summary>
public class StaticMeshObject : MeshedGameObject
{
    public Rad3d Rad { get; }

    public StaticMeshObject(Rad3d rad)
        : base(new CarMesh(rad))
    {
        Rad = rad;
    }

    public StaticMeshObject(Rad3d rad, f64Vector3 position, f64Euler rotation)
        : base(new CarMesh(rad), position, rotation)
    {
        Rad = rad;
    }
}
