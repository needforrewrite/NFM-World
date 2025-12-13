using NFMWorld.Util;

namespace NFMWorld.Mad;

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

    public static void RenderMessages()
    {
        if (!IsEnabled) return;
        
        var y = 0f;
        const float x = 250;
        const float increment = 20;
        
        G.SetColor(new Color(0, 0, 0));
        foreach (var message in _messages)
        {
            y += increment;
            G.DrawString(message, (int)x, (int)y);
        }
    }
    
    public static void ClearMessages()
    {
        _messages.Clear();
    }
}