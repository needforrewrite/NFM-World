using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private sealed record SessionStageInfo(string Name, IReadOnlyList<string> Briefing);

    private readonly Dictionary<string, SessionStageInfo> _sessionStages = new(StringComparer.OrdinalIgnoreCase);
    private bool _resolvingSessionStages;

    private string SessionStageName(string identity)
        => _sessionStages.TryGetValue(identity, out var info) ? info.Name
            : RadpackReader.IsArchiveId(identity) ? "Loading stage name..." : identity;

    private IReadOnlyList<string> SessionStageBriefing(string identity)
        => _sessionStages.TryGetValue(identity, out var info) ? info.Briefing : [];

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
                var info = new SessionStageInfo(
                    RadpackReader.IsArchiveId(identity) ? "Unavailable stage" : SessionStageName(identity),
                    []
                );
                try
                {
                    if (RadpackReader.IsArchiveId(identity))
                    {
                        var metadata = await GarageController.ContentClient.MetadataAsync(
                            identity[RadpackReader.ArchivePrefix.Length..],
                            cancellation
                        );
                        if (metadata is { Name.Length: > 0 })
                            info = info with { Name = metadata.Name };
                        if (metadata?.Description is { Length: > 0 } description)
                        {
                            info = info with
                            {
                                Briefing = [.. description
                                    .Replace("\r\n", "\n")
                                    .Split('\n')
                                    .Where(line => line.Length > 0)
                                ]
                            };
                        }
                    }
                    else if (MultiplayerContentPolicy.IsStockStage(identity))
                    {
                        info = await Task.Run(() => ReadLocalSessionStageInfo(identity), cancellation);
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
                _sessionStages[identity] = info;
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

    private static SessionStageInfo ReadLocalSessionStageInfo(string identity)
    {
        var textPath = $"data/stages/{identity}.txt";
        var packedPath = $"data/stages/{identity}.radpack";
        var hasText = VFS.FileExists(textPath);
        var hasPack = VFS.FileExists(packedPath);
        if (!hasText && !hasPack)
            return new SessionStageInfo(identity, []);

        if (hasPack)
        {
            try
            {
                if (RadpackReader.Read(packedPath, RadpackType.Track) is RadpackStage packed)
                {
                    var name = string.IsNullOrWhiteSpace(packed.Stage.Name) || packed.Stage.Name == "hogan rewish"
                        ? identity[(identity.LastIndexOf('/') + 1)..]
                        : packed.Stage.Name;
                    return new SessionStageInfo(
                        name,
                        [.. packed.Stage.Description.Where(line => !string.IsNullOrWhiteSpace(line))]
                    );
                }
            }
            catch (Exception ex)
            {
                Logging.Warning($"Could not read stage details: {ex.Message}");
            }
        }

        var textName = hasText ? MenuPreferences.GetStageName(VFS, textPath) : null;
        return new SessionStageInfo(
            textName ?? identity[(identity.LastIndexOf('/') + 1)..],
            hasText ? ReadTextStageBriefing(textPath) : []
        );
    }

    /// <summary>
    /// Reads phy's <c>desc()</c> lines without constructing a whole StageLoader. Both spellings
    /// are accepted: <c>desc(index,text)</c> and <c>desc(text)</c> with escaped newlines.
    /// </summary>
    private static IReadOnlyList<string> ReadTextStageBriefing(string path)
    {
        var lines = new List<string>();
        try
        {
            foreach (var raw in VFS.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("ds:")) line = line[3..];
                if (line.StartsWith('?')) line = line[1..];
                if (!line.StartsWith("desc")) continue;

                var end = line.LastIndexOf(')');
                if (end < 0) end = line.Length;
                if (end <= 5) continue;

                var body = line[5..end];
                if (body.Length >= 2 && body[0] is >= '0' and <= '9' && body[1] == ',')
                {
                    var comma = body.IndexOf(',');
                    if (!int.TryParse(body[..comma], out var index)) continue;
                    if (index is < 0 or >= 5) continue;
                    while (lines.Count <= index) lines.Add("");
                    lines[index] = body[(comma + 1)..];
                    continue;
                }

                var text = body.Replace("\\r", "\r").Replace("\\n", "\n");
                var split = text.Replace("\r\n", "\n").Split('\n');
                for (var i = 0; i < split.Length && i < 5; i++)
                {
                    while (lines.Count <= i) lines.Add("");
                    lines[i] = split[i];
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logging.Warning($"Could not read the briefing of stage '{path}': {ex.Message}");
        }

        return [.. lines.Where(line => line.Length > 0)];
    }
}
