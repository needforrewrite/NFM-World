using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class HsbColorStateTests
{
    [TestMethod]
    public void SaturationZero_PreservesHueForTheNextEdit()
    {
        var state = new HsbColorState(new Color3(255, 0, 0));
        state.SetChannel(0, 1.0 / 3);
        state.SetChannel(1, 0);
        Assert.AreEqual(new Color3(255, 255, 255), state.Color);
        state.SetChannel(1, 1);
        Assert.IsTrue(state.Color.G > 250 && state.Color.R < 5 && state.Color.B < 5);
    }

    [TestMethod]
    public void GaragePaint_ClampsEveryRgbChannel()
    {
        var state = new HsbColorState(new Color3(255, 0, 0), CarPaint.MinimumChannel);
        state.SetChannel(2, 0);
        Assert.AreEqual(new Color3(15, 15, 15), state.Color);
        state.SetChannel(2, 1);
        Assert.AreEqual(new Color3(255, 15, 15), state.Color);
    }

    [TestMethod]
    public void EditorColors_CanStillUseBlack()
    {
        var state = new HsbColorState(new Color3(100, 200, 100));
        state.SetChannel(2, 0);
        Assert.AreEqual(new Color3(0, 0, 0), state.Color);
    }

    [TestMethod]
    public void ExternalColorChange_UpdatesTheControls()
    {
        var state = new HsbColorState(new Color3(255, 0, 0));
        state.Set(new Color3(0, 0, 255));
        Assert.AreEqual(2f / 3, state.Hue.Value, 0.001f);
        Assert.AreEqual(1f, state.Saturation.Value);
        Assert.AreEqual(1f, state.Brightness.Value);
    }
}
