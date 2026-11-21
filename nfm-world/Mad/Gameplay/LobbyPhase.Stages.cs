using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private readonly Dictionary<string, string> _sessionStages = new(StringComparer.OrdinalIgnoreCase);
    private bool _resolvingSessionStages;

    private string SessionStageName(string identity)
        => _sessionStages.TryGetValue(identity, out var name) ? name
            : RadpackReader.IsArchiveId(identity) ? "Loading stage name..." : identity;

    private void ResolveSessionStageNames()
    {
        var stages = _activeSessions.Select(session => session.StageName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in _sessionStages.Keys.Where(id => !stages.Contains(id)).ToArray())
            _sessionStages.Remove(identity);
        if (_carsActive && !_resolvingSessionStages && stages.Any(id => !_sessionStages.ContainsKey(id)))
            _ = ResolveSessionStageNamesAsync(_carCancellation.Token);
    }

    private async Task ResolveSessionStageNamesAsync(CancellationToken cancellation)
    {
        _resolvingSessionStages = true;
        try
        {
            using var loading = LoadingWork.Begin("Loading stage names...");
            while (!cancellation.IsCancellationRequested)
            {
                var identity = _activeSessions.Select(session => session.StageName)
                    .FirstOrDefault(id => !_sessionStages.ContainsKey(id));
                if (identity is null) break;
                var name = RadpackReader.IsArchiveId(identity) ? "Unavailable stage" : SessionStageName(identity);
                try
                {
                    if (RadpackReader.IsArchiveId(identity))
                    {
                        var metadata = await GarageController.ContentClient.MetadataAsync(
                            identity[RadpackReader.ArchivePrefix.Length..],
                            cancellation
                        );
                        if (metadata is { Name.Length: > 0 })
                            name = metadata.Name;
                    }
                    else if (MultiplayerContentPolicy.IsStockStage(identity))
                    {
                        name = await Task.Run(() => ReadLocalSessionStageName(identity), cancellation);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Could not read stage details: {ex.Message}");
                }

                await LoadingWork.NextFrame;
                if (cancellation.IsCancellationRequested) return;
                _sessionStages[identity] = name;
                Publish();
            }
        }
        finally
        {
            await LoadingWork.NextFrame;
            _resolvingSessionStages = false;
            if (_carsActive) ResolveSessionStageNames();
        }
    }

    private static string ReadLocalSessionStageName(string identity)
    {
        var textPath = $"data/stages/{identity}.txt";
        var packedPath = $"data/stages/{identity}.radpack";
        var hasText = VFS.FileExists(textPath);
        var hasPack = VFS.FileExists(packedPath);
        if (!hasText && !hasPack)
            return identity;

        var fallbackName = identity[(identity.LastIndexOf('/') + 1)..];

        if (hasPack)
        {
            try
            {
                if (RadpackReader.Read(packedPath, RadpackType.Track) is RadpackStage packed)
                {
                    return string.IsNullOrWhiteSpace(packed.Stage.Name) || packed.Stage.Name == "hogan rewish"
                        ? fallbackName
                        : packed.Stage.Name;
                }
            }
            catch (Exception ex)
            {
                Logging.Warning($"Could not read stage details: {ex.Message}");
            }
        }

        var textName = hasText ? MenuPreferences.GetStageName(VFS, textPath) : null;
        return textName ?? fallbackName;
    }
}
