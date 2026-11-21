using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Helpers;

[PluginVisible]
public class FixHoopHelper
{
    [PluginVisible]
    public static bool HandleFixHoops(BackendStage currentStage, BackendCar car)
    {
        if (car.HasLeftRace || FindFixHoop(currentStage, car.Position) < 0) return false;
        car.Fix();
        return true;
    }

    [PluginVisible]
    public static int FindFixHoop(BackendStage stage, f64Vector3 position)
    {
        for (var i = 0; i < stage.FixHoops.Count; i++)
            if (IsInRange(stage.FixHoops[i], position, 200)) return i;

        return -1;
    }

    [PluginVisible]
    public static bool IsInRange(StageObject hoop, f64Vector3 position, int depth)
    {
        if (!hoop.IsFixHoopActive) return false;
        var hoopPosition = hoop.Position;
        return hoop.Rotation.Xz.Degrees == 0
            ? fix64.Abs(position.Z - hoopPosition.Z) < depth
              && UMath.Py(position.X / 100, hoopPosition.X / 100, position.Y / 100, hoopPosition.Y / 100) < 30
            : fix64.Abs(position.X - hoopPosition.X) < depth
              && UMath.Py(position.Z / 100, hoopPosition.Z / 100, position.Y / 100, hoopPosition.Y / 100) < 30;
    }
}
