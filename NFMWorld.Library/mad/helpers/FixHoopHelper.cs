using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Mad.Helpers;

public class FixHoopHelper
{
    // TODO fix car, play car fixed sound
    public static bool HandleFixHoops(
        IStage currentStage,
        IInGameCar car)
    {
        for (var i = 0; i < currentStage.fixHoops.Count; i++)
        {
            var fixhoop = currentStage.fixHoops[i];
            if (fixhoop.Rotation.Xz.Degrees == 0)
            {
                if (fix64.Abs(car.Position.Z - fixhoop.Position.Z) < 200 &&
                    UMath.Py(
                        car.Position.X / 100,
                        fixhoop.Position.X / 100, 
                        car.Position.Y / 100, 
                        fixhoop.Position.Y / 100
                    ) < 30)
                {
                    return true;
                    // if (Im == XTGraphics.Im && !conto.Fix && !XTGraphics.Mutes)
                    // {
                    //     XTGraphics.Carfixed.Play();
                    // }
                    // conto.Fix = true;
                }
            }
            else if (fix64.Abs(car.Position.X - fixhoop.Position.X) < 200 &&
                     UMath.Py(
                         car.Position.Z / 100,
                         fixhoop.Position.Z  / 100,
                         car.Position.Y / 100, 
                         fixhoop.Position.Y / 100
                    ) < 30)
            {
                return true;
                // if (Im == XTGraphics.Im && !conto.Fix && !XTGraphics.Mutes)
                // {
                //     XTGraphics.Carfixed.Play();
                // }
                // conto.Fix = true;
            }
        }

        return false;
    }
}