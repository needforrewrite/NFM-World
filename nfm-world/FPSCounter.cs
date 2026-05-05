using nfm_world.util;

namespace nfm_world;

public class FPSCounter
{
    private static double frames = 0;
    private static double updates = 0;
    private static double elapsed = 0;
    private static double msgFrequency = 0.05f;
    private static string msg;
    private static System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// The msgFrequency here is the reporting time to update the message.
    /// </summary>
    public static void Update(TimeSpan delta)
    {
        elapsed = _stopwatch.Elapsed.TotalSeconds;
        if (elapsed > msgFrequency)
        {
            msg = $"Fps: {frames / elapsed:0.00}\nElapsed time: {elapsed:0.00}\nUpdates: {updates}\nFrames: {frames}";
            _stopwatch.Restart();
            frames = 0;
            updates = 0;
        }
        updates++;
    }

    public static void Render()
    {
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 16));
        G.SetColor(Color.Black);
        G.DrawStringStroke(msg, 10, 25);
        G.SetColor(Color.White);
        G.DrawString(msg, 10, 25);
        frames++;
    }
}