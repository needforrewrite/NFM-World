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
    private static readonly Lazy<ArchiveContentClient> Content =
        new(() => new ArchiveContentClient(ArchiveEndpoint.Url));

    private static readonly Lazy<IReadOnlyList<StockPackage>> Stock =
        new(() => StockCatalog.Build(AppContext.BaseDirectory, includeTextStages: true));

    private static bool _recovered;

    public static void Enqueue(TimeTrialCompletion completion)
        => _ = EnqueueAsync(completion);

    private static async Task<RequestResult> EnqueueAsync(TimeTrialCompletion completion)
    {
        if (!_recovered)
        {
            _recovered = true;
            TimeTrialSubmissions.Recover();
        }

        var replay = SavedTimeTrial.Load(completion.Recording);
        if (replay is null)
        {
            Logging.Warning("[TT] Could not read the completed recording; it will not be submitted.");
            return new RequestResult("That recording could not be read.", false);
        }

        var stageHash = replay.StageData is { } stage
            ? StockCatalog.HashOf(StockCatalog.PackageStage(replay.StageName, stage))
            : null;
        var carHash = replay.CarData is { } car
            ? StockCatalog.HashOf(StockCatalog.PackageModel(AssetKind.Car, replay.CarName, car))
            : null;

        var account = GameSparker.AccountManager.ActiveAccount;
        var submission = TimeTrialSubmissions.Record(
            completion,
            stageHash,
            carHash,
            account?.Username,
            account is null ? "pending" : "preparing"
        );
        if (submission is null)
            return new RequestResult("The submission queue is full. The recording is still saved locally.", false);

        return await UploadAsync(submission, completion.Recording);
    }

    /// <summary>Retries a durable submission from the replay browser.</summary>
    public static void Retry(PendingSubmission submission)
    {
        if (TimeTrialSubmissions.ReadRecording(submission) is not { } recording)
        {
            Logging.Warning("[TT] The pending recording is missing or damaged.");
            return;
        }

        _ = UploadAsync(submission, recording);
    }

    public static async Task DeleteAsync(string file)
    {
        await Gate.WaitAsync();
        try
        {
            TimeTrialSubmissions.DeleteRecording(file);
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
    public static async Task<RequestResult> UploadAsync(string file)
    {
        var replay = SavedTimeTrial.Load(file);
        if (replay is null)
            return new RequestResult("That recording could not be read.", false);

        var recording = VFS.ReadAllBytes(file);
        var existing = TimeTrialSubmissions.FindRecording(TimeTrialSubmissions.List(), recording);
        if (existing is { } submission)
        {
            return await UploadAsync(submission, recording);
        }

        return await EnqueueAsync(
            new TimeTrialCompletion(
                replay.StageName,
                replay.CarName,
                replay.DemoData.Ticks.Count,
                replay.Version ?? 0,
                recording
            )
        );
    }

    private static async Task<RequestResult> UploadAsync(PendingSubmission submission, byte[] recording)
    {
        await Gate.WaitAsync();
        try
        {
            var account = GameSparker.AccountManager.ActiveAccount;
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
                var stage = await Content.Value.MetadataAsync(stageHash, CancellationToken.None);
                var car = await Content.Value.MetadataAsync(carHash, CancellationToken.None);
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
                submission.CarName
            );
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
