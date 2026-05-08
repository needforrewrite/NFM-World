using System.Runtime.InteropServices;

namespace NFMWorldLibrary;

// helpful little utility for showing information on screen without cluttering the console. messages are cleared
// at the start of a frame.
public static class FrameTrace
{
    private static readonly List<string> _messages = [];

    public static bool IsEnabled = true;
    
    public static void AddMessage(string message)
    {
        _messages.Add(message);
    }

    public static ReadOnlySpan<string> GetMessages()
    {
        return CollectionsMarshal.AsSpan(_messages);
    }

    public static void ClearMessages()
    {
        _messages.Clear();
    }
}