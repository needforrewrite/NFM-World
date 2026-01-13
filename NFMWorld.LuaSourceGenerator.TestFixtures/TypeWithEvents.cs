using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

[LuaVisible]
public class TypeWithEvents
{
    // Simple event with no arguments
    public event Action? SimpleEvent;

    // Event with standard EventHandler
    public event EventHandler? StandardEvent;

    // Event with custom EventArgs
    public event EventHandler<CustomEventArgs>? CustomEvent;

    // Static event
    public static event Action<string>? StaticEvent;

    // Event with multiple parameters (using custom delegate)
    public event MultiParamDelegate? MultiParamEvent;

    public delegate void MultiParamDelegate(int value, string message);

    // Helper methods to raise events for testing
    public void RaiseSimpleEvent()
    {
        SimpleEvent?.Invoke();
    }

    public void RaiseStandardEvent()
    {
        StandardEvent?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseCustomEvent(string message, int value)
    {
        CustomEvent?.Invoke(this, new CustomEventArgs { Message = message, Value = value });
    }

    public static void RaiseStaticEvent(string message)
    {
        StaticEvent?.Invoke(message);
    }

    public void RaiseMultiParamEvent(int value, string message)
    {
        MultiParamEvent?.Invoke(value, message);
    }
}

[LuaVisible]
public class CustomEventArgs : EventArgs
{
    public string Message { get; set; } = "";
    public int Value { get; set; }
}
