using Microsoft.Xna.Framework;
using NFMWorld.Mad;

namespace NFMWorld;

public class TimeStep(float targetDeltaTime)
{
    private double? currentTime;
    private double accumulator = 0.0;

    // Returns the amount of times to tick game logic this frame.
    public int Update(GameTime gameTime)
    {
        double newTime = gameTime.TotalGameTime.TotalSeconds;
        double frameTime = currentTime != null ? newTime - currentTime.Value : targetDeltaTime;
        currentTime = newTime;

        accumulator += frameTime;

        var updateCount = 0;
        while (accumulator >= targetDeltaTime)
        {
            updateCount++;
            accumulator -= targetDeltaTime;
            // t += dt;
        }

        return updateCount;
    }
}