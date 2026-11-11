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
            return;
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
            return;

        _ = UploadAsync(submission, completion.Recording);
    }

    private static async Task UploadAsync(PendingSubmission submission, byte[] recording)
    {
        await Gate.WaitAsync();
        try
        {
            var account = GameSparker.AccountManager.ActiveAccount;
            if (account is null)
            {
                TimeTrialSubmissions.UpdateStatus(submission, "pending");
                return;
            }

            // A run belongs to the account that finished it. Signing in as somebody else later
            // must not silently submit it in their name.
            if (submission.Account is { } owner
                && !string.Equals(owner, account.Username, StringComparison.OrdinalIgnoreCase))
            {
                TimeTrialSubmissions.UpdateStatus(submission, "pending");
                return;
            }

            if (submission.StageHash is not { } stageHash || submission.CarHash is not { } carHash)
            {
                TimeTrialSubmissions.UpdateStatus(submission, "publish-required");
                return;
            }

            var stock = Stock.Value;
            var stageIsStock = stock.Any(p => p.Kind == AssetKind.Stage && p.Hash == stageHash);
            var carIsStock = stock.Any(p => p.Kind == AssetKind.Car && p.Hash == carHash);

            try
            {
                var stage = await Content.Value.MetadataAsync(stageHash, CancellationToken.None);
                var car = await Content.Value.MetadataAsync(carHash, CancellationToken.None);
                if (stage is null || car is null)
                {
                    // Stock content is expected to be seeded; a miss there is an archive problem.
                    // Custom content that is not published cannot be ranked yet.
                    TimeTrialSubmissions.UpdateStatus(
                        submission,
                        stageIsStock && carIsStock ? "pending" : "publish-required"
                    );
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                TimeTrialSubmissions.UpdateStatus(submission, "pending");
                return;
            }

            var result = await GameSparker.AccountManager.UploadTimeTrialAsync(
                recording,
                stageHash,
                carHash,
                submission.StageName,
                submission.CarName
            );
            TimeTrialSubmissions.UpdateStatus(
                submission,
                result.Success ? "uploaded" : "pending"
            );
        }
        finally
        {
            Gate.Release();
        }
    }
}
