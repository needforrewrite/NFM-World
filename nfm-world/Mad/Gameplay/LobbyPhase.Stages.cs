using NFMWorldLibrary;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private readonly Dictionary<string, string> _sessionStageNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _resolvingSessionStages;

    private string SessionStageName(string identity)
        => _sessionStageNames.TryGetValue(identity, out var name) ? name
            : LocalStages.FirstOrDefault(stage => stage.Id.Equals(identity, StringComparison.OrdinalIgnoreCase))?.Label
              ?? (RadpackReader.IsArchiveId(identity) ? "Loading stage name..." : identity);

    private void ResolveSessionStageNames()
    {
        var stages = _activeSessions.Select(session => session.StageName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in _sessionStageNames.Keys.Where(id => !stages.Contains(id)).ToArray())
            _sessionStageNames.Remove(identity);
        if (_carsActive && !_resolvingSessionStages && stages.Any(id => !_sessionStageNames.ContainsKey(id)))
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
                    .FirstOrDefault(id => !_sessionStageNames.ContainsKey(id));
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
                        if (metadata is { Name.Length: > 0 }) name = metadata.Name;
                    }
                    else
                    {
                        var packedName = await Task.Run(() =>
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var path = $"data/stages/{identity}.radpack";
                                return VFS.FileExists(path) && RadpackReader.Read(path, RadpackType.Track) is RadpackStage packed
                                ? packed.Stage.Name : null;
                            }, cancellation
                        );
                        if (!string.IsNullOrWhiteSpace(packedName) && packedName != "hogan rewish") name = packedName;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Could not read stage name: {ex.Message}");
                }

                await LoadingWork.NextFrame;
                if (cancellation.IsCancellationRequested) return;
                _sessionStageNames[identity] = name;
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
}
