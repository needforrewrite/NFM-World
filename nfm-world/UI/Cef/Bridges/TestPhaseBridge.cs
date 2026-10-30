namespace NFMWorld.UI.Cef;

/// <summary>
/// Bridge for XamlTestPhase — dev-only test page with a counter.
/// </summary>
public sealed class TestPhaseBridge() : PhaseBridge("test")
{
    public override bool EnableInput => true;

    protected override void OnMessage(string type, LuaValue args)
    {
        switch (type)
        {
            case "increment":
                IncrementRequested?.Invoke();
                break;
            case "back":
                BackRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Push the current counter value to JS.
    /// </summary>
    public void PushCounter(int value)
    {
        Push("counter", new CounterData { Value = value });
    }

    public event Action? IncrementRequested;
    public event Action? BackRequested;
}

[PluginVisible]
public sealed partial class CounterData
{
    [PluginVisible] public int Value { get; set; }
}