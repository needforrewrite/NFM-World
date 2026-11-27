using System.Net.Http;
using Maxine.Extensions;
using NFMWorld.Gameplay;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    /// <summary>The reference a public archive part is downloaded and saved under.</summary>
    private static string OnlinePartReference(RemoteAsset asset)
        => RadpackReader.ArchivePrefix + asset.ContentHash.ToLowerInvariant();

    private void SearchOnlineParts(string query)
    {
        _onlinePartSearch?.Cancel();
        _onlinePartSearch?.Dispose();
        var search = _onlinePartSearch = new CancellationTokenSource();
        var generation = ++_onlinePartSearchGeneration;
        _onlinePartSearching = true;
        _onlinePartStatus = "";
        Touch();
        _ = SearchOnlinePartsAsync(query, generation, search.Token);
    }

    /// <summary>Opens the online list on the first visit; a later visit keeps the last results.</summary>
    private void SearchOnlinePartsIfNeeded()
    {
        if (!_onlinePartSearching && _onlinePartResults.Count == 0)
            SearchOnlineParts("");
    }

    private async Task SearchOnlinePartsAsync(string query, int generation, CancellationToken token)
    {
        try
        {
            var page = await GarageController.ContentClient.SearchAsync(
                query,
                AssetKinds.Name(AssetKinds.StagePiece),
                40,
                null,
                token
            );
            await LoadingWork.NextFrame;
            if (generation != _onlinePartSearchGeneration || _editorDisposed)
                return;

            _onlinePartResults = page.Items.Where(item => item.Eligible).ToList();
            foreach (var item in _onlinePartResults)
                _onlinePartNames[OnlinePartReference(item)] = item.Name;
        }

        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await LoadingWork.NextFrame;
            if (generation != _onlinePartSearchGeneration || _editorDisposed)
                return;

            _onlinePartResults = [];
            _onlinePartStatus = $"Could not search the archive: {ex.Message}";
        }
        finally
        {
            if (generation == _onlinePartSearchGeneration)
            {
                await LoadingWork.NextFrame;
                if (generation == _onlinePartSearchGeneration && !_editorDisposed)
                {
                    _onlinePartSearching = false;
                    Touch();
                }
            }
        }
    }

    private void DownloadOnlinePart(RemoteAsset asset, bool placeWhenReady = true)
    {
        var reference = OnlinePartReference(asset);
        if (_availableParts.GetValueOrDefault(reference) is { } available)
        {
            if (placeWhenReady)
                BeginPlacement(available);
            return;
        }

        if (placeWhenReady)
            _onlinePartPlacementRequests.Add(reference);
        if (!_onlinePartDownloads.Add(reference))
            return;

        _onlinePartStatus = "";
        Touch();
        _ = DownloadOnlinePartAsync(asset, reference);
    }

    private async Task DownloadOnlinePartAsync(RemoteAsset asset, string reference)
    {
        LoadingWork.Operation? loading = null;
        try
        {
            using var statusDelayCancellation = CancellationTokenSource.CreateLinkedTokenSource(_editorLoading.Token);
            var prepare = ContentCache.PrepareAsync(
                GarageController.ContentClient,
                reference,
                AssetKind.StagePiece,
                _editorLoading.Token
            );
            var statusDelay = Task.Delay(250, statusDelayCancellation.Token);
            if (await Task.WhenAny(prepare, statusDelay) == statusDelay && !prepare.IsCompleted)
            {
                await LoadingWork.NextFrame;
                if (!_editorDisposed && !prepare.IsCompleted && _onlinePartDownloads.Contains(reference))
                {
                    loading = LoadingWork.Begin($"Downloading {asset.Name}...");
                    _onlinePartSlowDownloads[reference] = asset.Name;
                    Touch();
                }
            }

            statusDelayCancellation.Cancel();
            var result = await prepare;
            await LoadingWork.NextFrame;
            if (_editorDisposed)
                return;

            if (!result.Ok)
            {
                _onlinePartStatus = $"Could not download \"{asset.Name}\": {result.Error}";
                return;
            }

            if (RadpackReader.TryReadModel(reference, RadpackType.TrackPiece, out var error) is not { } part)
            {
                _onlinePartStatus = $"The downloaded part could not be read: {error}";
                return;
            }

            part.FileName = reference;
            _availableParts.TryAdd(part);
            _onlinePartNames[reference] = asset.Name;
            var placeWhenReady = _onlinePartPlacementRequests.Remove(reference);
            if (placeWhenReady)
                BeginPlacement(part);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException)
        {
            if (!_editorDisposed)
                _onlinePartStatus = $"Could not download \"{asset.Name}\": {ex.Message}";
        }
        finally
        {
            _onlinePartDownloads.Remove(reference);
            _onlinePartPlacementRequests.Remove(reference);
            _onlinePartSlowDownloads.Remove(reference);
            loading?.Dispose();
            if (!_editorDisposed)
                Touch();
        }
    }

    private string CurrentOnlinePartStatus()
    {
        if (_onlinePartSlowDownloads.Count > 1)
            return $"Downloading {_onlinePartSlowDownloads.Count} stage parts...";

        foreach (var name in _onlinePartSlowDownloads.Values)
            return $"Downloading \"{name}\"...";

        return _onlinePartStatus;
    }
}
