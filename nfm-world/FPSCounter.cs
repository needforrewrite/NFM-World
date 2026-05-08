using Microsoft.Xna.Framework;
using NFMWorld.Util;

namespace NFMWorld;

public class FPSCounter
{
    private static double frames = 0;
    private static double updates = 0;
    private static double elapsed = 0;
    private static double last = 0;
    private static double now = 0;
    private static double msgFrequency = 0.05f;
    private static string msg;

    /// <summary>
    /// The msgFrequency here is the reporting time to update the message.
    /// </summary>
    public static void Update(GameTime gameTime)
    {
        now = gameTime.TotalGameTime.TotalSeconds;
        elapsed = now - last;
        if (elapsed > msgFrequency)
        {
            msg = $"Fps: {frames / elapsed:0.00}\nElapsed time: {elapsed:0.00}\nUpdates: {updates}\nFrames: {frames}";
            //Console.WriteLine(msg);
            elapsed = 0;
            frames = 0;
            updates = 0;
            last = now;
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