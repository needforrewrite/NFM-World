using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;

namespace NFMWorld.UI.Hud;

internal static class HudImages
{
    public static IImage? TryLoad(string path, Vector3? rgbMultiplier = null, byte maximumChannel = 255)
    {
        try
        {
            return rgbMultiplier is { } multiplier
                ? G.LoadSvgImage(path, multiplier, maximumChannel)
                : G.LoadImage(path);
        }
        catch (Exception ex)
        {
            Logging.Error($"Could not load HUD image '{path}': {ex.GetType().Name}: {ex.Message}. Skipping image.");
            return null;
        }
    }
}
