using Lua;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
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
    private static long lastCallCount = 0;
    private static double lastLuaMs = 0;
    private static long lastTableCount = 0;
    private static long lastStringInsertCount = 0;
    private static double lastStringInsertMs = 0;
    private static long lastStringResizeCount = 0;
    private static double lastStringResizeMs = 0;

    /// <summary>
    /// The msgFrequency here is the reporting time to update the message.
    /// </summary>
    public static void Update(GameTime gameTime, long tickUs, long frameMs, double asyncUs)
    {
        now = gameTime.TotalGameTime.TotalSeconds;
        elapsed = now - last;
        if (elapsed > msgFrequency)
        {
            var luaCalls = LuaCallDiagnostics.CallCount;
            var luaMs = LuaCallDiagnostics.ElapsedMilliseconds;
            if (luaCalls != 0)
            {
                lastCallCount = luaCalls;
                lastLuaMs = luaMs;
                Console.WriteLine($"Lua calls: {lastCallCount} ({lastLuaMs:0.00}ms)");
            }
            LuaCallDiagnostics.Reset();

            var tableCount = LuaTableDiagnostics.TableCount;
            var stringInsertCount = LuaTableDiagnostics.StringInsertCount;
            var stringInsertMs = LuaTableDiagnostics.StringInsertMilliseconds;
            var stringResizeCount = LuaTableDiagnostics.StringResizeCount;
            var stringResizeMs = LuaTableDiagnostics.StringResizeMilliseconds;
            if (tableCount != 0 || stringInsertCount != 0)
            {
                lastTableCount = tableCount;
                lastStringInsertCount = stringInsertCount;
                lastStringInsertMs = stringInsertMs;
                lastStringResizeCount = stringResizeCount;
                lastStringResizeMs = stringResizeMs;
                Console.WriteLine($"Tables created: {lastTableCount}, string inserts: {lastStringInsertCount} ({lastStringInsertMs:0.00}ms), string resizes: {lastStringResizeCount} ({lastStringResizeMs:0.00}ms)");
            }
            LuaTableDiagnostics.Reset();

            msg = $"Fps: {frames / elapsed:0.00}\nElapsed time: {elapsed:0.00}\nTick: {tickUs}us\nGTC: {asyncUs:0.00}us\nFrame (CPU): {frameMs}ms\nUpdates: {updates}\nFrames: {frames}\nLua calls: {lastCallCount} ({lastLuaMs:0.00}ms)\nTables: {lastTableCount}, str-ins: {lastStringInsertCount} ({lastStringInsertMs:0.00}ms), resize: {lastStringResizeCount} ({lastStringResizeMs:0.00}ms)";
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
        G.SetFont(new Font(FontFamily.NotoSans, FontStyle.Plain, 16));
        G.SetColor(Color.Black);
        G.DrawStringStroke(msg, 10, 25);
        G.SetColor(Color.White);
        G.DrawString(msg, 10, 25);
        frames++;
    }
}