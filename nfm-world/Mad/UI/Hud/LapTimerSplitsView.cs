using WorldXaml.UI.Yoga;

namespace NFMWorld.UI.Hud;

/// <summary>
/// Code-behind for LapTimerSplitsView.xaml.
/// </summary>
public partial class LapTimerSplitsView : View
{
    /// <summary>
    /// Creates a new LapTimerSplitsView and initializes it from XAML.
    /// </summary>
    public LapTimerSplitsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the lap display text.
    /// </summary>
    /// <param name="currentLap">Current lap number (0-based).</param>
    /// <param name="totalLaps">Total number of laps.</param>
    public void SetLapText(int currentLap, int totalLaps)
    {
        LapText?.Text = $"{currentLap + 1}/{totalLaps}";
    }
}
