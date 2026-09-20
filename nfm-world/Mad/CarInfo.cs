using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld;

public class CarMesh : Mesh
{
    public CarStats Stats;
    public LuaArray<Rad3dWheelDef> Wheels;
    public Rad3dRimsDef? Rims;

    public CarMesh(Rad3d rad) : base(rad)
    {
        Stats = CarStats.ValidateStats(rad.Stats, rad.FileName);

        Wheels = rad.Wheels;
        Rims = rad.Rims;
    }
}