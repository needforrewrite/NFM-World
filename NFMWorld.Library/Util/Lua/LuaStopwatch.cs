using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaVisible, LuaName("Stopwatch")]
public partial class LuaStopwatch
{
    private readonly MicroStopwatch _stopwatch = new();

    [LuaName]
    public LuaStopwatch()
    {
    }

    [LuaName]
    public static LuaStopwatch StartNew()
    {
        var sw = new LuaStopwatch();
        sw.Start();
        return sw;
    }

    [LuaName]
    public void Stop() => _stopwatch.Stop();

    [LuaName]
    public void Start() => _stopwatch.Start();

    [LuaName]
    public void Restart() => _stopwatch.Restart();

    [LuaName]
    public void Reset() => _stopwatch.Reset();

    [LuaName]
    public bool IsRunning => _stopwatch.IsRunning;
    
    [LuaName]
    public double Elapsed => _stopwatch.Elapsed.TotalSeconds;
    
    [LuaName]
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
    
    [LuaName]
    public long ElapsedMicroseconds => _stopwatch.ElapsedMicroseconds;
}