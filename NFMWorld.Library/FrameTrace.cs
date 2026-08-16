using System.Runtime.CompilerServices;
using System.Text;
using nfm_world_library.Lua;

namespace NFMWorldLibrary;

// helpful little utility for showing information on screen without cluttering the console. messages are cleared
// at the start of a frame.
[LuaVisible]
public static partial class FrameTrace
{
    internal static readonly StringBuilder stringBuilder = new();

    public static bool IsEnabled = true;

    [LuaName]
    public static void AddMessage(string message)
    {
        stringBuilder.AppendLine(message);
    }
    
    public static void AddMessage(ref AppendInterpolatedStringHandler message)
    {
        stringBuilder.AppendLine();
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
    public StringBuilder.AppendInterpolatedStringHandler handler = new(literalLength, formattedCount, FrameTrace.stringBuilder);

    public void AppendLiteral(string value) => handler.AppendLiteral(value);

    public void AppendFormatted<T>(T value) => handler.AppendFormatted(value);

    public void AppendFormatted<T>(T value, string? format) => handler.AppendFormatted(value, format);

    public void AppendFormatted<T>(T value, int alignment) => handler.AppendFormatted(value, alignment);

    public void AppendFormatted<T>(T value, int alignment, string? format) => handler.AppendFormatted(value, alignment, format);

    public void AppendFormatted(ReadOnlySpan<char> value) => handler.AppendFormatted(value);

    public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => handler.AppendFormatted(value, alignment, format);

    public void AppendFormatted(string? value) => handler.AppendFormatted(value);

    public void AppendFormatted(string? value, int alignment = 0, string? format = null) => handler.AppendFormatted(value, alignment, format);

    public void AppendFormatted(object? value, int alignment = 0, string? format = null) => handler.AppendFormatted(value, alignment, format);
}