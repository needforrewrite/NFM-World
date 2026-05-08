namespace nfm_world;

public class TimeStep(float targetDeltaTime)
{
    private double accumulator = 0.0;

    // Returns the amount of times to tick game logic this frame.
    public int Update(TimeSpan delta)
    {
        accumulator += delta.TotalSeconds;

        var updateCount = 0;
        while (accumulator >= targetDeltaTime)
        {
            updateCount++;
            accumulator -= targetDeltaTime;
        }

        return updateCount;
    }
}