using System.Buffers;
using System.Security.Cryptography;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Library.Test;

[TestClass]
public class TimeTrialSubmissionsTests
{
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "nfmw-tt-submissions", Guid.NewGuid().ToString("N"));

    static TimeTrialSubmissionsTests()
    {
        Directory.CreateDirectory(Root);
        TheVFS.VFS.MountWriteDestination(Root);
    }

    private static TimeTrialCompletion Completion(byte[] recording)
        => new("nfmm/stage1", "nfmm/2000tornados", 1234, SavedTimeTrialVersion, recording);

    private const int SavedTimeTrialVersion = 5;

    [TestMethod]
    public void Record_WritesTheRecordingAndItsSidecar()
    {
        var recording = RandomBytes();
        var submission = TimeTrialSubmissions.Record(
            Completion(recording),
            "stage-hash",
            "car-hash",
            "racer",
            "pending"
        );

        Assert.IsNotNull(submission);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(recording)), submission.Digest);
        Assert.IsTrue(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{submission.Digest}.timetrial"));
        Assert.IsTrue(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{submission.Digest}.meta"));

        var listed = TimeTrialSubmissions.List().Single(s => s.Digest == submission.Digest);
        Assert.AreEqual("racer", listed.Account);
        Assert.AreEqual("pending", listed.Status);
        CollectionAssert.AreEqual(recording, TimeTrialSubmissions.ReadRecording(listed));
    }

    [TestMethod]
    public void Recorder_KeepsOneCopyOfIdenticalBytes()
    {
        var recording = RandomBytes();
        var first = TimeTrialSubmissions.Record(Completion(recording), null, null, "racer");
        var second = TimeTrialSubmissions.Record(Completion(recording), null, null, "racer");

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(first.Digest, second.Digest);
        Assert.AreEqual(1, TimeTrialSubmissions.List().Count(s => s.Digest == first.Digest));
    }

    [TestMethod]
    public void Recover_CreatesAMissingSidecarAndKeepsTheRecording()
    {
        var recording = RandomBytes();
        var digest = Convert.ToHexStringLower(SHA256.HashData(recording));
        using (var stream = TheVFS.VFS.OpenWrite($"{TimeTrialSubmissions.PendingDirectory}/{digest}.timetrial"))
            stream.Write(recording);

        TimeTrialSubmissions.Recover();

        Assert.IsTrue(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{digest}.meta"));
        Assert.IsTrue(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{digest}.timetrial"));
        var recovered = TimeTrialSubmissions.List().Single(s => s.Digest == digest);
        Assert.AreEqual("pending", recovered.Status);
        CollectionAssert.AreEqual(recording, TimeTrialSubmissions.ReadRecording(recovered));
    }

    [TestMethod]
    public void ReadRecording_RefusesBytesThatNoLongerMatchTheDigest()
    {
        var recording = RandomBytes();
        var submission = TimeTrialSubmissions.Record(Completion(recording), null, null, "racer")!;
        using (var stream = TheVFS.VFS.OpenWrite($"{TimeTrialSubmissions.PendingDirectory}/{submission.Digest}.timetrial"))
            stream.Write(RandomBytes());

        Assert.IsNull(TimeTrialSubmissions.ReadRecording(submission));
    }

    private static byte[] RandomBytes()
    {
        var bytes = new byte[64];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}
