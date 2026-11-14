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

    /// <summary>
    /// Another test class can re-mount the process-wide write root (the config-persistence and
    /// menu-history tests do), so the isolated root is re-asserted per test rather than only once.
    /// </summary>
    [TestInitialize]
    public void UseIsolatedRoot() => TheVFS.VFS.MountWriteDestination(Root);

    private static TimeTrialCompletion Completion(byte[] recording)
        => new("nfmm/stage1", "nfmm/2000tornados", 1234, NFMWorldLibrary.Files.SavedTimeTrial.CURRENT_VERSION, recording);

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
    public void FindRecording_DistinguishesRunsWithTheSameStageAndCar()
    {
        var firstBytes = RandomBytes();
        var secondBytes = RandomBytes();
        var first = TimeTrialSubmissions.Record(Completion(firstBytes), null, null, "racer", "uploaded");
        var second = TimeTrialSubmissions.Record(Completion(secondBytes), null, null, "racer", "pending");
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);

        PendingSubmission[] submissions = [first, second];
        Assert.AreSame(second, TimeTrialSubmissions.FindRecording(submissions, secondBytes));
        Assert.AreSame(first, TimeTrialSubmissions.FindRecording(submissions, firstBytes.ToArray()));
        Assert.IsNull(TimeTrialSubmissions.FindRecording(submissions, RandomBytes()));
    }

    [TestMethod]
    public void DeleteRecording_RemovesExactLocalCopiesAndSidecar_ButKeepsOtherRuns()
    {
        var recording = RandomBytes();
        var different = RandomBytes();
        var submission = TimeTrialSubmissions.Record(Completion(recording), null, null, "racer");
        var other = TimeTrialSubmissions.Record(Completion(different), null, null, "racer");
        Assert.IsNotNull(submission);
        Assert.IsNotNull(other);
        var saved = $"data/tts/{Guid.NewGuid():N}.timetrial";
        using (var stream = TheVFS.VFS.OpenWrite(saved))
            stream.Write(recording);

        Assert.AreEqual(2, TimeTrialSubmissions.DeleteRecording(saved));
        Assert.IsFalse(TheVFS.VFS.FileExists(saved));
        Assert.IsFalse(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{submission.Digest}.timetrial"));
        Assert.IsFalse(TheVFS.VFS.FileExists($"{TimeTrialSubmissions.PendingDirectory}/{submission.Digest}.meta"));
        CollectionAssert.AreEqual(different, TimeTrialSubmissions.ReadRecording(other));
        Assert.IsTrue(TimeTrialSubmissions.List().Any(s => s.Digest == other.Digest));
    }

    [TestMethod]
    public void DeleteWriteFile_RefusesPathsOutsideTheWriteRoot()
    {
        Assert.Throws<ArgumentException>(() => TheVFS.VFS.DeleteWriteFile("../outside.timetrial"));
        Assert.Throws<ArgumentException>(() => TheVFS.VFS.DeleteWriteFile("data/tts/../../../outside.timetrial"));
        Assert.Throws<InvalidOperationException>(() => TimeTrialSubmissions.DeleteRecording("archive:42"));
    }

    [TestMethod]
    public void DeleteWriteFile_PreservesCopiesInReadMounts()
    {
        var fs = new NFMWorld.PhysicsFS.VirtualFS();
        var root = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        var mountedRoot = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        var file = "data/tts/run.timetrial";
        Directory.CreateDirectory(Path.Combine(mountedRoot, "data/tts"));
        File.WriteAllBytes(Path.Combine(mountedRoot, file), [1, 2, 3]);
        fs.MountWriteDestination(root);
        fs.MountDirectory(mountedRoot);
        using (var stream = fs.OpenWrite(file))
            stream.Write([4, 5, 6]);

        Assert.IsTrue(fs.DeleteWriteFile(file));
        Assert.IsFalse(fs.DeleteWriteFile(file));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, fs.ReadAllBytes(file));
    }

    [TestMethod]
    public void SaveTimestamp_ComesFromTheSavedFile()
    {
        var file = $"data/tts/{Guid.NewGuid():N}.timetrial";
        using (var stream = TheVFS.VFS.OpenWrite(file))
            stream.Write(RandomBytes());
        var savedAt = new DateTime(2026, 11, 12, 10, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(Root, file), savedAt);

        Assert.AreEqual(new DateTimeOffset(savedAt), TheVFS.VFS.GetLastWriteTimeUtc(file));
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
        var submission = TimeTrialSubmissions.Record(Completion(recording), null, null, "racer");
        Assert.IsNotNull(submission, "the submission queue must have room for a fresh run");
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
