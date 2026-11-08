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

        // Clamp: after an alt-tab, a breakpoint or a multi-second shader compile, the
        // unclamped frame time would run thousands of ticks in one frame (a freeze).
        var maxFrameTime = targetDeltaTime * 4;
        if (frameTime > maxFrameTime)
        {
            frameTime = maxFrameTime;
            accumulator = 0;
        }

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
