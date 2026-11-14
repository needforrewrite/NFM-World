using System.Security.Cryptography;
using NFMWorld.Accounts;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;
using TightPack;

namespace NFMWorld.Gameplay;

/// <summary>
/// Lists local recordings and published replays, and opens one in the viewer.
/// </summary>
/// <remarks>
/// A malformed file is skipped with a count rather than failing the browser, and the list is
/// bounded so a directory full of recordings cannot stall the menu. The remote list is paged by
/// the archive's own cursor; nothing here keeps a full index.
/// </remarks>
public sealed class ReplayBrowserPhase : BasePhase
{
    private const int MaxListed = 200;
    private const int RemotePageSize = 40;
    private const string RemotePrefix = "archive:";

    private readonly ReplayBrowserState _state = new();
    private readonly Lazy<ArchiveReplayClient> _client =
        new(() => new ArchiveReplayClient(ArchiveEndpoint.Url));

    private CancellationTokenSource? _onlineCancellation;
    private int _onlineGeneration;
    private int? _nextCursor;
    private string _query = "";
    private IReadOnlyList<ReplayBrowserEntry> _localEntries = [];
    private bool _disposed;

    public ReplayBrowserPhase()
    {
        _ = RefreshLocalAsync(_onlineGeneration);
    }

    public override Node? BuildUi() => ReplayBrowserView.Build(
        _state,
        new ReplayBrowserView.Actions(
            Open: Open,
            Upload: Upload,
            UploadAll: UploadAll,
            SourceSelected: SelectSource,
            Search: Search,
            LoadMore: () => _ = BrowseAsync(_query, append: true),
            Back: GameSparker.PopPhase,
            Delete: id => _ = DeleteAsync(id)
        )
    );

    private void SelectSource(bool online)
    {
        if (_state.Online.Value == online)
            return;

        _onlineCancellation?.Cancel();
        _onlineGeneration++;
        _state.Online.Value = online;
        _state.SelectedId.Value = null;
        _state.DeleteCandidate.Value = null;
        _state.Status.Value = "";
        _state.Busy.Value = false;
        _state.HasMore.Value = false;
        _nextCursor = null;

        if (online)
        {
            _state.Entries.Value = [];
            _ = BrowseAsync(_query, append: false);
            return;
        }

        _ = RefreshLocalAsync(_onlineGeneration);
    }

    /// <summary>Local rows are filtered as you type; only the archive needs to be asked.</summary>
    private void Search(string query)
    {
        _query = query.Trim();
        if (_state.Online.Value)
        {
            _ = BrowseAsync(_query, append: false);
            return;
        }

        ApplyLocalFilter();
    }

    private async Task BrowseAsync(string? query, bool append)
    {
        var previous = _onlineCancellation;
        previous?.Cancel();
        previous?.Dispose();

        var cancellation = _onlineCancellation = new CancellationTokenSource();
        var generation = ++_onlineGeneration;
        _state.Busy.Value = true;
        _state.Status.Value = "Searching the archive...";

        try
        {
            var page = await _client.Value.BrowseAsync(query, RemotePageSize, append ? _nextCursor : null, cancellation.Token);
            if (generation != _onlineGeneration)
                return;

            var rows = page.Items.Select(ToRemoteEntry).ToArray();
            if (append && _state.Entries.Value is { Count: > 0 } existing)
                _state.Entries.Value = [.. existing, .. rows];
            else
                _state.Entries.Value = rows;

            _nextCursor = page.NextCursor;
            _state.HasMore.Value = page.NextCursor is not null;
            _state.Status.Value = _state.Entries.Value.Count == 0
                ? "No public replays matched."
                : "";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (generation != _onlineGeneration)
                return;

            if (!append)
                _state.Entries.Value = [];
            _state.Status.Value = $"Could not reach the archive: {ex.Message}";
        }
        finally
        {
            if (generation == _onlineGeneration)
                _state.Busy.Value = false;
        }
    }

    private static ReplayBrowserEntry ToRemoteEntry(RemoteReplay replay)
        => new(
            Id: $"{RemotePrefix}{replay.Id}",
            Stage: replay.StageName,
            Car: replay.CarName,
            Time: FormatTime(replay.TotalTicks),
            Detail: $"{replay.Username}  ·  replay #{replay.Id}",
            Compatible: replay.ReplayVersion == SavedTimeTrial.CURRENT_VERSION,
            Uploadable: false,
            SavedAt: replay.CreatedAt,
            Deletable: false
        );

    private async Task<bool> RefreshLocalAsync(int generation)
    {
        _state.Busy.Value = true;
        _state.Status.Value = "Reading recordings...";
        try
        {
            var result = await Task.Run(ReadLocal);
            if (_disposed || generation != _onlineGeneration)
                return false;
            _localEntries = result.Entries;
            ApplyLocalFilter();
            _state.HasMore.Value = false;
            _state.Status.Value = result.Malformed == 0 ? ""
                : $"{result.Malformed} recording(s) could not be read and were skipped.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or TightPackSerializationException)
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Status.Value = $"Could not read the recordings: {ex.Message}";
            return false;
        }
        finally
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Busy.Value = false;
        }
    }

    private static (IReadOnlyList<ReplayBrowserEntry> Entries, int Malformed) ReadLocal()
    {
        var pending = TimeTrialSubmissions.List();
        var entries = new List<ReplayBrowserEntry>();
        var malformed = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var writable = VFS.EnumerateWriteFiles("data/tts", "*.timetrial", SearchOption.AllDirectories)
            .ToHashSet(StringComparer.Ordinal);

        if (VFS.DirectoryExists("data/tts"))
        {
            foreach (var file in VFS.EnumerateFiles("data/tts", "*.timetrial", SearchOption.AllDirectories)
                .OrderBy(file => (writable.Contains(file) ? 0 : 2)
                                 + (file.StartsWith(TimeTrialSubmissions.PendingDirectory + "/", StringComparison
                                     .Ordinal
                                 ) ? 1 : 0)
                )
                .Take(MaxListed))
            {
                SavedTimeTrial? replay;
                byte[] recording;
                try
                {
                    recording = VFS.ReadAllBytes(file);
                    if (!seen.Add(Convert.ToHexStringLower(SHA256.HashData(recording))))
                        continue;
                    replay = SavedTimeTrial.Load(recording);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or TightPackSerializationException)
                {
                    Logging.Warning($"[Replay] Skipping unreadable recording {file}: {ex.Message}");
                    malformed++;
                    continue;
                }

                if (replay is null)
                {
                    malformed++;
                    continue;
                }

                var submission = TimeTrialSubmissions.FindRecording(pending, recording);
                var status = submission?.Status switch
                {
                    "uploaded" => "Uploaded",
                    "pending" or "preparing" => "Upload pending",
                    "publish-required" => "Publish required",
                    "rejected" => "Upload refused",
                    _ => "Local only"
                };

                entries.Add(
                    new ReplayBrowserEntry(
                        Id: file,
                        Stage: replay.StageName,
                        Car: replay.CarName,
                        Time: FormatTime(replay.DemoData.Ticks.Count),
                        Detail: status,
                        Compatible: IsCompatible(replay),
                        Uploadable: IsCompatible(replay) && submission is not { Status: "uploaded" },
                        SavedAt: VFS.GetLastWriteTimeUtc(file)
                        ?? (submission is { RecordedAt: var date } && date != default ? date : null),
                        Deletable: writable.Contains(file)
                    )
                );
            }
        }

        return (entries, malformed);
    }

    private void ApplyLocalFilter()
    {
        if (_query.Length == 0)
        {
            _state.Entries.Value = _localEntries;
            return;
        }

        _state.Entries.Value = _localEntries
            .Where(entry => entry.Stage.Contains(_query, StringComparison.OrdinalIgnoreCase)
                            || entry.Car.Contains(_query, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
    }

    private static string FormatTime(int ticks)
    {
        var seconds = ticks / 63.0;
        var minutes = (int)(seconds / 60);
        return $"{minutes}:{seconds - minutes * 60:00.000}";
    }

    /// <summary>
    /// A frame is a fixed-layout snapshot, so a recording from another version cannot be replayed
    /// as it stands. The list marks these too, but the click still has to refuse them.
    /// </summary>
    private static bool IsCompatible(SavedTimeTrial replay)
        => (replay.Version ?? 0) == SavedTimeTrial.CURRENT_VERSION;

    private void Open(string id)
    {
        if (_state.Busy.Value)
            return;

        if (id.StartsWith(RemotePrefix, StringComparison.Ordinal))
        {
            if (int.TryParse(id.AsSpan(RemotePrefix.Length), out var remoteId))
                _ = OpenRemoteAsync(remoteId);
            return;
        }

        var replay = SavedTimeTrial.Load(id);
        if (replay is null)
        {
            _state.Status.Value = "That recording could not be read.";
            return;
        }

        if (!IsCompatible(replay))
        {
            _state.Status.Value =
                $"That recording is version {replay.Version ?? 0}; this build plays {SavedTimeTrial.CURRENT_VERSION}.";
            return;
        }

        GameSparker.PushPhase(new ReplayViewerPhase(replay));
    }

    /// <summary>
    /// Downloads a published replay before pushing the viewer, so an unreachable archive is a
    /// message in this list rather than a viewer that has nothing to play.
    /// </summary>
    private async Task OpenRemoteAsync(int id)
    {
        _state.Busy.Value = true;
        _state.Status.Value = $"Downloading replay #{id}...";

        try
        {
            var bytes = await _client.Value.DownloadAsync(id, RadpackLimits.MaxTimeTrialBytes, CancellationToken.None);
            var replay = SavedTimeTrial.Load(bytes);
            if (replay is null)
            {
                _state.Status.Value = "That replay could not be read.";
                return;
            }

            if (!IsCompatible(replay))
            {
                _state.Status.Value =
                    $"Replay #{id} is version {replay.Version ?? 0}; this build plays {SavedTimeTrial.CURRENT_VERSION}.";
                return;
            }

            _state.Status.Value = "";
            GameSparker.PushPhase(new ReplayViewerPhase(replay));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException
            or TightPackSerializationException)
        {
            _state.Status.Value = $"Could not download replay #{id}: {ex.Message}";
        }
        finally
        {
            _state.Busy.Value = false;
        }
    }

    private void Upload(string id)
        => _ = UploadRecordingsAsync(_state.Entries.Value.Where(entry => entry.Id == id && entry.Uploadable).ToArray());

    private async Task DeleteAsync(string id)
    {
        if (_state.Online.Value || _state.Busy.Value || !_localEntries.Any(entry => entry.Id == id && entry.Deletable))
            return;

        var generation = ++_onlineGeneration;
        _state.Busy.Value = true;
        try
        {
            await TimeTrialUploader.DeleteAsync(id);
            if (_disposed || generation != _onlineGeneration)
                return;
            _state.SelectedId.Value = null;
            if (!await RefreshLocalAsync(generation))
                return;
            _state.Status.Value = "Replay deleted from this device.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Status.Value = $"Could not delete the replay: {ex.Message}";
        }
        finally
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Busy.Value = false;
        }
    }

    private void UploadAll()
        => _ = UploadRecordingsAsync(_state.Entries.Value.Where(entry => entry.Uploadable).ToArray());

    private async Task UploadRecordingsAsync(IReadOnlyList<ReplayBrowserEntry> uploadable)
    {
        if (_state.Online.Value || _state.Busy.Value)
            return;

        if (uploadable.Count == 0)
        {
            _state.Status.Value = "Every recording has already been submitted.";
            return;
        }

        var generation = ++_onlineGeneration;
        _state.Busy.Value = true;
        _state.Status.Value = $"Submitting {uploadable.Count} recording(s)...";
        try
        {
            var results = new List<RequestResult>();
            foreach (var entry in uploadable)
                results.Add(await TimeTrialUploader.UploadAsync(entry.Id));

            if (_disposed || generation != _onlineGeneration)
                return;

            if (!await RefreshLocalAsync(generation))
                return;
            var submitted = results.Count(result => result.Success);
            var failure = results.FirstOrDefault(result => !result.Success);
            _state.Status.Value = results.Count == 1
                ? results[0].Message ?? "The archive returned no upload status."
                : $"Submitted {submitted}/{results.Count} recordings. {failure?.Message}";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or TaskCanceledException
            or TightPackSerializationException)
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Status.Value = $"Could not submit the recording: {ex.Message}";
        }
        finally
        {
            if (!_disposed && generation == _onlineGeneration)
                _state.Busy.Value = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _disposed = true;

        // A connect still in flight would otherwise outlive the phase and write into a disposed
        // state object's signals.
        _onlineCancellation?.Cancel();
        _onlineCancellation?.Dispose();
        _onlineCancellation = null;

        if (_client.IsValueCreated)
            _client.Value.Dispose();
    }
}
