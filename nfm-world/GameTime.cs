namespace NFMWorld;

/// <summary>
/// One frame's timestamps - the game's own replacement for FNA's
/// <c>Microsoft.Xna.Framework.GameTime</c>, which the exe no longer references (Milestone 7).
/// </summary>
/// <remarks>
/// Only the members this codebase actually reads are modelled. FNA's own <c>IsRunningSlowly</c> is
/// deliberately absent: the manual loop in <see cref="WorldGame.Main"/> carries its overshoot in
/// <c>accumulatedElapsedTime</c> instead of reporting it through the clock (see the fixed-timestep
/// branch there), and nothing else ever read it.
/// </remarks>
/// <param name="TotalGameTime">Time since the loop started (<c>Stopwatch.Elapsed</c>).</param>
/// <param name="ElapsedGameTime">
/// The time budgeted to this frame: <see cref="WorldGame.TargetElapsedTime"/> on a fixed step, or the
/// accumulated drift on a variable step.
/// </param>
public readonly struct GameTime(TimeSpan totalGameTime, TimeSpan elapsedGameTime)
{
    public TimeSpan TotalGameTime { get; } = totalGameTime;

    public TimeSpan ElapsedGameTime { get; } = elapsedGameTime;
}
