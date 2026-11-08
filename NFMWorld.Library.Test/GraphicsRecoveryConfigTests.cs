using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class GraphicsRecoveryConfigTests
{
    [TestMethod]
    public void SelectingRendererPreservesOtherSettingsAndRemovesDuplicateRendererKeys()
    {
        const string original =
            "// settings\nvideo_renderer3 OpenGL via ANGLE\naudio_music 0.5\nvideo_renderer3 OpenGL\nkey_Accelerate 87\n";
        var result = GraphicsRecoveryConfig.SelectRenderer(original, "Auto", reset: false);
        StringAssert.Contains(result, "audio_music 0.5");
        StringAssert.Contains(result, "key_Accelerate 87");
        StringAssert.Contains(result, "// settings");
        var rendererLines = result.Split('\n').Where(line => line.StartsWith("video_renderer3 ")).ToArray();
        CollectionAssert.AreEqual(new[] { "video_renderer3 Auto" }, rendererLines.Select(line => line.TrimEnd('\r')).ToArray());
    }

    [TestMethod]
    public void ResetLeavesOnlyAutomaticRendererOverride()
    {
        var result = GraphicsRecoveryConfig.SelectRenderer(
            "video_renderer3 OpenGL\nvideo_antialias 4\naudio_music 0.5",
            "Auto",
            reset: true
        );
        Assert.AreEqual("video_renderer3 Auto" + System.Environment.NewLine, result);
    }
}
