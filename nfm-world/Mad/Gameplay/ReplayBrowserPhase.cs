using NFMWorld.Reactor;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Gameplay;

public sealed class ReplayBrowserPhase : BasePhase
{
    private readonly ReplayBrowserController _browser = new(GameSparker.PopPhase);

    public override Node BuildUi() => _browser.BuildUi();

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        _browser.KeyPressed(key);
    }

    public override void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyRepeated(key, imguiWantsKeyboard, keys);
        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
            _browser.KeyPressed(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _browser.Dispose();
        base.Dispose(disposing);
    }
}
