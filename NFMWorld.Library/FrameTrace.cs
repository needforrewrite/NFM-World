using System.Runtime.InteropServices;
using nfm_world_library.Lua;

namespace nfm_world_library;

// helpful little utility for showing information on screen without cluttering the console. messages are cleared
// at the start of a frame.
[LuaVisible]
public class FrameTrace
{
    private static readonly List<string> _messages = [];

    public static bool IsEnabled = true;
    
    public static void AddMessage(string message)
    {
        _messages.Add(message);
    }

    [LuaHidden]
    public static ReadOnlySpan<string> GetMessages()
    {
        return CollectionsMarshal.AsSpan(_messages);
    }

    [LuaHidden]
    public static void ClearMessages()
    {
        _messages.Clear();
    }
}