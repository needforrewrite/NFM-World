using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class CameraPresetTests
{
    [TestMethod]
    public void EmptyConfigUsesNfmwWithSmoothFov()
    {
        var camera = CameraPresetConfig.ReadCamera("// New configuration\n\n");
        Assert.AreEqual(80f, camera.Fov);
        Assert.AreEqual(-98, camera.FollowY);
        Assert.AreEqual(-250, camera.FollowZ);
        Assert.IsTrue(camera.SmoothFov);
    }

    [TestMethod]
    public void ExistingConfigWithoutCameraKeysKeepsClassicDefaults()
    {
        var camera = CameraPresetConfig.ReadCamera("video_renderer3 Auto\naudio_music 0.5\n");
        Assert.IsTrue(CameraPreset.Classic.Matches(camera));
    }

    [TestMethod]
    public void ExistingCustomizedCameraIsPreservedExactly()
    {
        var camera = CameraPresetConfig.ReadCamera(
            "video_antialias 4\ncamera_fov 93.25\ncamera_follow_y -86\ncamera_follow_z -260\ncamera_smooth_fov 1\n");
        Assert.AreEqual(93.25f, camera.Fov);
        Assert.AreEqual(-86, camera.FollowY);
        Assert.AreEqual(-260, camera.FollowZ);
        Assert.IsTrue(camera.SmoothFov);
    }

    [TestMethod]
    public void PartialLegacyCameraKeepsClassicForMissingFields()
    {
        var camera = CameraPresetConfig.ReadCamera("camera_follow_y -20\n");
        Assert.AreEqual(CameraPreset.Classic.Fov, camera.Fov);
        Assert.AreEqual(-20, camera.FollowY);
        Assert.AreEqual(0, camera.FollowZ);
        Assert.IsFalse(camera.SmoothFov);
    }

    [TestMethod]
    public void PresetMetadataNeverOverridesSavedCameraValues()
    {
        var camera = CameraPresetConfig.ReadCamera("camera_preset_name NFMW\ncamera_presets []\n"
            + "camera_fov 101.5\ncamera_follow_y -25\ncamera_follow_z -190\ncamera_smooth_fov 0\n");
        Assert.AreEqual(101.5f, camera.Fov);
        Assert.AreEqual(-25, camera.FollowY);
        Assert.AreEqual(-190, camera.FollowZ);
        Assert.IsFalse(camera.SmoothFov);
    }

    [TestMethod]
    public void CustomPresetsRoundTripWithNamesAndAllCameraValues()
    {
        CameraPreset[] presets = [new("My \"wide\" view", 104.5f, -120, -330, true), new("Miesto kamera", 78, -50, -200, false)];
        var json = CameraPresetConfig.WritePresets(presets);
        CollectionAssert.AreEqual(presets, CameraPresetConfig.ReadPresets(json).ToArray());
        Assert.IsFalse(json.Contains('\n'));
    }

    [TestMethod]
    public void PresetSnapshotCanRestoreEditsAndRemoval()
    {
        CameraPreset[] original = [new("Custom", 90, -30, -100, true)];
        var snapshot = CameraPresetConfig.WritePresets(original);
        var edited = CameraPresetConfig.ReadPresets(snapshot);
        edited[0] = edited[0] with { Name = "Renamed", Fov = 110 };
        edited.Clear();
        CollectionAssert.AreEqual(original, CameraPresetConfig.ReadPresets(snapshot).ToArray());
    }

    [TestMethod]
    public void BuiltInNamesAndLineBreaksCannotBeUsedForCustomPresets()
    {
        foreach (var name in new[] { CameraPreset.NFMW.Name, "wOrLd", "classic", " ", "Name\nAnother setting", new string('x', 41) })
            Assert.ThrowsExactly<ArgumentException>(() => CameraPreset.ValidateCustomName(name));
    }

    [TestMethod]
    public void InvalidAndDuplicatePresetsDoNotDiscardValidPresets()
    {
        var saved = CameraPresetConfig.WritePresets(
        [
            new(CameraPreset.NFMW.Name, 80, 0, 0, false),
            new("Valid", 80, -98, -250, true),
            new("valid", 90, 0, 0, false),
            new("Invalid FOV", 200, 0, 0, false)
        ]);
        var presets = CameraPresetConfig.ReadPresets(saved);
        Assert.AreEqual(1, presets.Count);
        Assert.AreEqual("Valid", presets[0].Name);
    }
}
