using NFMWorld.UI.Cef;

namespace NFMWorld.Gameplay;

public class XamlTestPhase : BasePhase
{
    private readonly TestPhaseBridge _bridge = new();
    private int _counter;

    public XamlTestPhase()
    {
        CefBridge = _bridge;
        _bridge.IncrementRequested += () => _counter++;
        _bridge.BackRequested += () => GameSparker.ReturnToMainMenu();
    }

    public override void GameTick()
    {
        base.GameTick();
        // Counter is incremented by the JS button via IncrementRequested.
        // Push the current value each tick so the test page stays in sync.
        _bridge.PushCounter(_counter);
    }
}