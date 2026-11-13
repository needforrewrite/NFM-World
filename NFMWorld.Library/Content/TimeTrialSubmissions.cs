using System.Security.Cryptography;
using TightPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Content;

/// <summary>One completed run waiting to be, or in the middle of being, uploaded.</summary>
[TightPackable(GenerateType.VersionTolerant)]
public sealed partial class PendingSubmission
{
    /// <summary>SHA-256 of the recording bytes; also the sidecar's key.</summary>
    [TightPackOrder(0)]
    public required string Digest { get; set; }

    [TightPackOrder(1)] public required string StageName { get; set; }
    [TightPackOrder(2)] public required string CarName { get; set; }
    [TightPackOrder(3)] public string? StageHash { get; set; }
    [TightPackOrder(4)] public string? CarHash { get; set; }

    /// <summary>The account that ran it. A later sign-in must not silently claim this run.</summary>
    [TightPackOrder(5)]
    public string? Account { get; set; }

    /// <summary><c>pending</c>, <c>uploaded</c>, <c>publish-required</c> or <c>rejected</c>.</summary>
    [TightPackOrder(6)]
    public required string Status { get; set; }

    [TightPackOrder(7)] public int FinishTicks { get; set; }
    [TightPackOrder(8)] public int ReplayVersion { get; set; }
    [TightPackOrder(9)] public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>
/// Durable queue of completed time trials, stored beside the recordings through the VFS.
/// </summary>
/// <remarks>
/// A run is written before any network work, with its immutable content hashes and the account
/// that ran it, so an offline finish, a login expiry or a restart loses nothing and a later
/// sign-in cannot attribute the run to somebody else. The sidecar is bound to the recording by
/// the recording's own digest.
/// </remarks>
public static class TimeTrialSubmissions
{
    public const string PendingDirectory = "data/tts/pending";
    public const int MaxPending = 20;

    private const string RecordingSuffix = ".timetrial";
    private const string SidecarSuffix = ".meta";

    public static PendingSubmission? Record(
        TimeTrialCompletion completion,
        string? stageHash,
        string? carHash,
        string? account,
        string status = "pending"
    )
    {
        if (completion.Recording.Length == 0)
            return null;

        var digest = Convert.ToHexStringLower(SHA256.HashData(completion.Recording));
        // Only work that is still outstanding counts against the bound. Uploaded runs stay on
        // disk for the browser, so counting them would fill the queue for good after 20 races.
        var existing = List();
        if (existing.All(s => s.Digest != digest)
            && existing.Count(s => s.Status == "pending") >= MaxPending)
        {
            // Bound the queue rather than growing without limit while offline.
            Logging.Warning("[TT] Submission queue is full; the run was saved locally only.");
            return null;
        }

        var recordingPath = $"{PendingDirectory}/{digest}{RecordingSuffix}";
        using (var stream = VFS.OpenWrite(recordingPath))
            stream.Write(completion.Recording);

        var submission = new PendingSubmission
        {
            Digest = digest,
            StageName = completion.StageName,
            CarName = completion.CarName,
            StageHash = stageHash,
            CarHash = carHash,
            Account = account,
            Status = status,
            FinishTicks = checked((int)completion.FinishTicks),
            ReplayVersion = completion.ReplayVersion,
            RecordedAt = DateTimeOffset.UtcNow
        };
        WriteSidecar(submission);
        return submission;
    }

    public static void UpdateStatus(PendingSubmission submission, string status)
    {
        submission.Status = status;
        WriteSidecar(submission);
    }

    /// <summary>Recreates sidecars whose write was interrupted, without ever deleting a recording.</summary>
    public static void Recover()
    {
        if (!VFS.DirectoryExists(PendingDirectory))
            return;

        foreach (var file in VFS.EnumerateWriteFiles(PendingDirectory, "*" + RecordingSuffix))
        {
            var digest = Path.GetFileNameWithoutExtension(file);
            if (VFS.FileExists(SidecarPath(digest)))
                continue;

            WriteSidecar(
                new PendingSubmission
                {
                    Digest = digest,
                    StageName = "",
                    CarName = "",
                    Status = "pending",
                    RecordedAt = DateTimeOffset.UtcNow
                }
            );
        }
    }

    public static IReadOnlyList<PendingSubmission> List()
    {
        if (!VFS.DirectoryExists(PendingDirectory))
            return [];

        var submissions = new List<PendingSubmission>();
        foreach (var file in VFS.EnumerateWriteFiles(PendingDirectory, "*" + SidecarSuffix))
        {
            try
            {
                var submission = TightPackSerializer.Deserialize<PendingSubmission>(VFS.ReadAllBytes(file));
                if (submission is not null)
                    submissions.Add(submission);
            }
            catch (Exception ex) when (ex is InvalidDataException or TightPackSerializationException)
            {
                // One malformed sidecar must not hide the rest of the browser.
            }
        }

        return submissions;
    }

    public static byte[]? ReadRecording(PendingSubmission submission)
    {
        var path = $"{PendingDirectory}/{submission.Digest}{RecordingSuffix}";
        if (!VFS.FileExists(path))
            return null;

        var bytes = VFS.ReadAllBytes(path);
        return Convert.ToHexStringLower(SHA256.HashData(bytes)) == submission.Digest ? bytes : null;
    }

    private static void WriteSidecar(PendingSubmission submission)
    {
        using var stream = VFS.OpenWrite(SidecarPath(submission.Digest));
        var bytes = TightPackSerializer.Serialize(submission);
        stream.Write(bytes);
    }

    private static string SidecarPath(string digest) => $"{PendingDirectory}/{digest}{SidecarSuffix}";
}
