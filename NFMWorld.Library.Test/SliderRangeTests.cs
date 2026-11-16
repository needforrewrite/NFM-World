using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class SliderRangeTests
{
    [TestInitialize]
    public void Setup()
    {
        IBackend.Backend = new DummyBackend();
        FocusManager.ClearFocus();
    }

    [TestCleanup]
    public void Cleanup()
    {
        FocusManager.ClearFocus();
        UiScheduler.Instance.Flush();
    }

    [TestMethod]
    public void Pointer_ReusesSliderAfterZeroCapacityAndAfterCapacityShrinks()
    {
        var maxPlayers = new Signal<int>(1);
        var bots = new Signal<double>(0);
        using var mounted = Renderer.Mount(() => Controls.Slider(
            () => bots.Value,
            value => bots.Value = value,
            0,
            () => maxPlayers.Value - 1,
            1
        )
        );
        var slider = (Component)mounted.Root;
        slider.LayoutAndRender(new Vector2(400, 40));

        void ClickEnd()
            => slider.MousePressed!(new MouseEvent(
                slider.LayoutPaddingPosition + new Vector2(slider.LayoutPaddingSize.X, 0),
                MouseButton.Primary,
                MouseButtons.Primary,
                false,
                false,
                false,
                Vector2.Zero
            )
            );

        ClickEnd();
        Assert.AreEqual(0d, bots.Value);
        maxPlayers.Value = 4;
        UiScheduler.Instance.Flush();
        ClickEnd();
        Assert.AreEqual(3d, bots.Value);
        maxPlayers.Value = 16;
        UiScheduler.Instance.Flush();
        ClickEnd();
        Assert.AreEqual(15d, bots.Value);
        maxPlayers.Value = 15;
        UiScheduler.Instance.Flush();
        ClickEnd();
        Assert.AreEqual(14d, bots.Value);
    }

    [TestMethod]
    public void Keyboard_UsesCurrentCapacityWithoutRemounting()
    {
        var capacity = new Signal<int>(0);
        double bots = 0;
        using var mounted = Renderer.Mount(() => Controls.Slider(
            () => bots,
            value => bots = value,
            0,
            () => capacity.Value,
            1
        )
        );
        var root = (Component)mounted.Root;
        root.LayoutAndRender(new Vector2(400, 40));
        var navigation = new KeyboardNavigation();

        navigation.HandleKey(root, Key.End);
        Assert.AreEqual(0d, bots);
        capacity.Value = 12;
        navigation.HandleKey(root, Key.End);
        Assert.AreEqual(12d, bots);
        capacity.Value = 2;
        navigation.HandleKey(root, Key.End);
        Assert.AreEqual(2d, bots);
        capacity.Value = 12;
        navigation.HandleKey(root, Key.Right);
        Assert.AreEqual(3d, bots);
    }
}
