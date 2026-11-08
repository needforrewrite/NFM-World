using System.Numerics;
using NFMWorld.Reactor;

namespace NFMWorld.Library.Test;

[TestClass]
public class MouseCursorStateTests
{
    private static readonly Vector2 Viewport = new(1920, 1080);

    [TestMethod]
    public void GameplayHidesAndCentersThenRestoresTheMenuPosition()
    {
        var state = new MouseCursorState();
        var menuPosition = new Vector2(125, 240);
        var menu = state.Update(true, true, menuPosition, Viewport);
        Assert.IsTrue(menu.Visible);
        Assert.IsFalse(state.Locked);
        Assert.IsNull(menu.WarpTo);

        var gameplay = state.Update(false, true, menuPosition, Viewport);
        Assert.IsFalse(gameplay.Visible);
        Assert.IsTrue(state.Locked);
        Assert.AreEqual(Viewport / 2, gameplay.WarpTo);
        Assert.IsNull(state.Update(false, true, Viewport / 2, Viewport).WarpTo);
        state.Update(false, true, new Vector2(1000, 600), Viewport);

        var paused = state.Update(true, true, Viewport / 2, Viewport);
        Assert.IsTrue(paused.Visible);
        Assert.IsFalse(state.Locked);
        Assert.AreEqual(menuPosition, paused.WarpTo);
    }

    [TestMethod]
    public void ResumingSavesTheLatestMenuPosition()
    {
        var state = new MouseCursorState();
        state.Update(false, true, new Vector2(10, 20), Viewport);
        state.Update(true, true, Viewport / 2, Viewport);
        var moved = new Vector2(600, 350);
        state.Update(false, true, moved, Viewport);

        Assert.AreEqual(moved, state.Update(true, true, Viewport / 2, Viewport).WarpTo);
    }

    [TestMethod]
    public void LosingFocusNeverWarpsOrOverwritesTheSavedPosition()
    {
        var state = new MouseCursorState();
        var saved = new Vector2(50, 60);
        state.Update(false, true, saved, Viewport);
        foreach (var menuVisible in new[] { false, true })
        {
            var inactive = state.Update(menuVisible, false, new Vector2(-500, 3000), Viewport);
            Assert.IsTrue(inactive.Visible);
            Assert.IsFalse(state.Locked);
            Assert.IsNull(inactive.WarpTo);
        }

        var active = state.Update(false, true, new Vector2(1200, 700), Viewport);
        Assert.IsFalse(active.Visible);
        Assert.IsTrue(state.Locked);
        Assert.AreEqual(saved, state.Update(true, true, Viewport / 2, Viewport).WarpTo);
    }

    [TestMethod]
    public void AResizedWindowClampsTheRestoredPositionInsideItsBounds()
    {
        var state = new MouseCursorState();
        state.Update(false, true, new Vector2(1800, 1000), Viewport);

        Assert.AreEqual(new Vector2(799, 599), state.Update(true, true, new Vector2(400, 300), new Vector2(800, 600)).WarpTo);
    }

    [TestMethod]
    public void OddViewportDimensionsUseAStablePixelCenter()
    {
        var state = new MouseCursorState();
        var viewport = new Vector2(801, 601);
        var centered = state.Update(false, true, Vector2.Zero, viewport);

        Assert.AreEqual(new Vector2(400, 300), centered.WarpTo);
        Assert.IsNull(state.Update(false, true, centered.WarpTo!.Value, viewport).WarpTo);
    }
}
