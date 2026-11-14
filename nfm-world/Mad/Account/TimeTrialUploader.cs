using NFMWorld.Gameplay;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Accounts;

/// <summary>
/// Turns a completed run into a durable submission and, when possible, uploads it.
/// </summary>
/// <remarks>
/// The recording is copied and written before any network work, so leaving the race, an expired
/// session or a restart never loses a legitimate finish. Content is identified by the canonical
/// hash of the exact stage and car the recording carries; unpublished custom content stays local
/// and is marked <c>publish-required</c> rather than uploaded under a guessed identity.
/// </remarks>
public static class TimeTrialUploader
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly SerialWorkQueue LocalWork = new();

    private sealed record Prepared(PendingSubmission? Submission, byte[] Recording, string? Error = null);

    private static readonly Lazy<ArchiveContentClient> Content =
        new(() => new ArchiveContentClient(ArchiveEndpoint.Url));

    private static readonly Lazy<IReadOnlyList<StockPackage>> Stock =
        new(() => StockCatalog.Build(AppContext.BaseDirectory, includeTextStages: true));

    private static bool _recovered;

    public static void Enqueue(TimeTrialCompletion completion)
    {
        var account = GameSparker.AccountManager.ActiveAccount;
        _ = ObserveAsync(SubmitAsync(LocalWork.Enqueue(() => Prepare(completion, account)), account));
    }

    public static void Enqueue(SavedTimeTrial recording, bool saveBest)
    {
        var account = GameSparker.AccountManager.ActiveAccount;
        var prepared = LocalWork.Enqueue(() =>
            {
                var bytes = recording.ToBytes();
                if (saveBest)
                    VFS.WriteAllBytesAtomic(SavedTimeTrial.GetPathName(recording.CarName, recording.StageName), bytes);
                var completion = new TimeTrialCompletion(
                    recording.StageName,
                    recording.CarName,
                recording.DemoData.Ticks.Count,
                    recording.Version ?? 0,
                    bytes
                );
                return Prepare(completion, account, recording);
            }
        );
        _ = ObserveAsync(SubmitAsync(prepared, account));
    }

    private static Prepared Prepare(TimeTrialCompletion completion, Account? account, SavedTimeTrial? replay = null)
    {
        if (!_recovered)
        {
            _recovered = true;
            TimeTrialSubmissions.Recover();
        }

        replay ??= SavedTimeTrial.Load(completion.Recording);
        if (replay is null)
        {
            Logging.Warning("[TT] Could not read the completed recording; it will not be submitted.");
            return new Prepared(null, [], "That recording could not be read.");
        }

        var stageHash = replay.StageData is { } stage
            ? StockCatalog.HashOf(StockCatalog.PackageStage(replay.StageName, stage))
            : null;
        var carHash = replay.CarData is { } car
            ? StockCatalog.HashOf(StockCatalog.PackageModel(AssetKind.Car, replay.CarName, car))
            : null;

        var submission = TimeTrialSubmissions.Record(
            completion,
            stageHash,
            carHash,
            account?.Username,
            account is null ? "pending" : "preparing"
        );
        if (submission is null)
            return new Prepared(null, [], "The submission queue is full. The recording is still saved locally.");

        return new Prepared(submission, completion.Recording);
    }

    /// <summary>Retries a durable submission from the replay browser.</summary>
    public static void Retry(PendingSubmission submission)
    {
        var account = GameSparker.AccountManager.ActiveAccount;
        var prepared = LocalWork.Enqueue(() => TimeTrialSubmissions.ReadRecording(submission) is { } recording
            ? new Prepared(submission, recording)
            : new Prepared(null, [], "The pending recording is missing or damaged."));
        _ = ObserveAsync(SubmitAsync(prepared, account));
    }

    public static void FlushPendingSaves()
    {
        try
        {
            LocalWork.WhenIdle().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logging.Warning($"[TT] Replay persistence failed: {ex.Message}");
        }
    }

    public static async Task DeleteAsync(string file)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await LocalWork.Enqueue(() => TimeTrialSubmissions.DeleteRecording(file)).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Manually submits a stored recording that has no durable submission, or retries the one it
    /// has. The replay browser calls this for its UPLOAD button.
    /// </summary>
    public static Task<RequestResult> UploadAsync(string file)
    {
        var account = GameSparker.AccountManager.ActiveAccount;
        var prepared = LocalWork.Enqueue(() =>
            {
                var recording = VFS.ReadAllBytes(file);
                var replay = SavedTimeTrial.Load(recording);
                if (replay is null)
                    return new Prepared(null, [], "That recording could not be read.");
                var existing = TimeTrialSubmissions.FindRecording(TimeTrialSubmissions.List(), recording);
                if (existing is not null)
                    return new Prepared(existing, recording);
                return Prepare(new TimeTrialCompletion(
                    replay.StageName,
                    replay.CarName,
                replay.DemoData.Ticks.Count,
                    replay.Version ?? 0,
                    recording
                ), account, replay
                );
            }
        );
        return SubmitAsync(prepared, account);
    }

    private static async Task<RequestResult> SubmitAsync(Task<Prepared> work, Account? account)
    {
        var prepared = await work.ConfigureAwait(false);
        return prepared.Submission is { } submission
            ? await UploadAsync(submission, prepared.Recording, account).ConfigureAwait(false)
            : new RequestResult(prepared.Error, false);
    }

    private static async Task ObserveAsync(Task<RequestResult> work)
    {
        try
        {
            var result = await work.ConfigureAwait(false);
            if (!result.Success)
                Logging.Warning($"[TT] {result.Message}");
        }
        catch (Exception ex)
        {
            Logging.Warning($"[TT] Could not save or submit the completed recording: {ex.Message}");
        }
    }

    private static async Task<RequestResult> UploadAsync(PendingSubmission submission, byte[] recording, Account? account)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (TimeTrialSubmissions.ReadRecording(submission) is null)
                return new RequestResult("This recording is no longer saved locally.", false);
            if (account is null)
            {
                return Finish(submission, "pending", "Sign in to submit this recording. It is still saved locally.", false);
            }

            // A run belongs to the account that finished it. Signing in as somebody else later
            // must not silently submit it in their name.
            if (submission.Account is { } owner
                && !string.Equals(owner, account.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Finish(
                    submission,
                    "pending",
                    $"This run belongs to {owner}. Sign in to that account to submit it.",
                    false
                );
            }

            if (submission.StageHash is not { } stageHash || submission.CarHash is not { } carHash)
            {
                return Finish(
                    submission,
                    "publish-required",
                    "This recording has no stage or car content identity. Its content must be published before uploading.",
                    false
                );
            }

            try
            {
                var stage = await Content.Value.MetadataAsync(stageHash, CancellationToken.None).ConfigureAwait(false);
                var car = await Content.Value.MetadataAsync(carHash, CancellationToken.None).ConfigureAwait(false);
                if (stage is null || car is null)
                {
                    // Stock content is expected to be seeded; a miss there is an archive problem.
                    // Custom content that is not published cannot be ranked yet.
                    var stock = Stock.Value;
                    var stageIsStock = stock.Any(p => p.Kind == AssetKind.Stage && p.Hash == stageHash);
                    var carIsStock = stock.Any(p => p.Kind == AssetKind.Car && p.Hash == carHash);
                    var missing = stage is null ? $"stage '{submission.StageName}'" : $"car '{submission.CarName}'";
                    return Finish(
                        submission,
                        stageIsStock && carIsStock ? "pending" : "publish-required",
                        $"The {missing} is not available at {ArchiveEndpoint.Url}. "
                                                                 + (stageIsStock
                                                                    && carIsStock ? "The archive needs matching stock content." : "Publish the matching content first."),
                        false
                    );
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                return Finish(submission, "pending", $"Archive metadata request failed: {ex.Message}", false);
            }

            var result = await GameSparker.AccountManager.UploadTimeTrialAsync(
                recording,
                stageHash,
                carHash,
                submission.StageName,
                submission.CarName,
                account
            ).ConfigureAwait(false);
            return Finish(
                submission,
                result.Success ? "uploaded" : "pending",
                result.Message ?? (result.Success ? "Submitted." : "The archive refused this recording."),
                result.Success
            );
        }
        finally
        {
            Gate.Release();
        }
    }

    private static RequestResult Finish(PendingSubmission submission, string status, string message, bool success)
    {
        TimeTrialSubmissions.UpdateStatus(submission, status);
        if (success)
            Logging.Info($"[TT] {submission.StageName} / {submission.CarName}: {message}");
        else
            Logging.Warning($"[TT] {submission.StageName} / {submission.CarName}: {message}");
        return new RequestResult(message, success);
    }
}
