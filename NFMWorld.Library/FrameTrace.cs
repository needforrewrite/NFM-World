using System.Runtime.CompilerServices;
using System.Text;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

// helpful little utility for showing information on screen without cluttering the console. messages are cleared
// at the start of a frame.
[PluginVisible]
public static partial class FrameTrace
{
    internal static readonly StringBuilder stringBuilder = new();

    public static bool IsEnabled =
#if DEBUG
            true
#else
            false
#endif
        ;

    [PluginVisible]
    public static void AddMessage(string message)
    {
        if (IsEnabled) stringBuilder.AppendLine(message);
    }

    public static void AddMessage(ref AppendInterpolatedStringHandler message)
    {
        if (IsEnabled) stringBuilder.AppendLine();
    }

    public static string GetMessageString()
    {
        return stringBuilder.ToString();
    }

    public static void ClearMessages()
    {
        stringBuilder.Clear();
    }
}

[InterpolatedStringHandler]
public ref struct AppendInterpolatedStringHandler(int literalLength, int formattedCount)
{
    private readonly bool _enabled = FrameTrace.IsEnabled;
    public StringBuilder.AppendInterpolatedStringHandler handler = new(literalLength, formattedCount, FrameTrace.stringBuilder);

    public void AppendLiteral(string value)
    {
        if (_enabled) handler.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value)
    {
        if (_enabled) handler.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        if (_enabled) handler.AppendFormatted(value, format);
    }

    public void AppendFormatted<T>(T value, int alignment)
    {
        if (_enabled) handler.AppendFormatted(value, alignment);
    }

    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (_enabled) handler.AppendFormatted(value, alignment, format);
    }

    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (_enabled) handler.AppendFormatted(value);
    }

    public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
    {
        if (_enabled) handler.AppendFormatted(value, alignment, format);
    }

    public void AppendFormatted(string? value)
    {
        if (_enabled) handler.AppendFormatted(value);
    }

    public void AppendFormatted(string? value, int alignment = 0, string? format = null)
    {
        if (_enabled) handler.AppendFormatted(value, alignment, format);
    }

    public void AppendFormatted(object? value, int alignment = 0, string? format = null)
    {
        if (_enabled) handler.AppendFormatted(value, alignment, format);
    }
}
