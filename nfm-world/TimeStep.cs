using Microsoft.Xna.Framework;
using NFMWorld.Mad;

namespace NFMWorld;

public class TimeStep
{
    public const float DeltaTime = (1000f / GameSparker.TargetTps) / 1000f; // in seconds
    private static double? currentTime;
    private static double accumulator = 0.0;

    // Returns the amount of times to tick game logic this frame.
    public static int Update(GameTime gameTime)
    {
        double newTime = gameTime.TotalGameTime.TotalSeconds;
        double frameTime = currentTime != null ? newTime - currentTime.Value : DeltaTime;
        currentTime = newTime;

        accumulator += frameTime;

        var updateCount = 0;
        while (accumulator >= DeltaTime)
        {
            updateCount++;
            accumulator -= DeltaTime;
            // t += dt;
        }

        return updateCount;
    }
}