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
        _onlinePartStatus = "Searching the archive...";
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

            _onlinePartStatus = _onlinePartResults.Count == 0
                ? "No public stage parts matched."
                : $"{_onlinePartResults.Count} public stage part(s). Click one to download and place it.";
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

    private void DownloadOnlinePart(RemoteAsset asset)
    {
        var reference = OnlinePartReference(asset);
        if (_availableParts.Contains(reference) || !_onlinePartDownloads.Add(reference))
            return;

        _onlinePartStatus = $"Downloading \"{asset.Name}\"...";
        Touch();
        _ = DownloadOnlinePartAsync(asset, reference);
    }

    private async Task DownloadOnlinePartAsync(RemoteAsset asset, string reference)
    {
        try
        {
            using var loading = LoadingWork.Begin($"Downloading {asset.Name}...");
            var result = await ContentCache.PrepareAsync(
                GarageController.ContentClient,
                reference,
                AssetKind.StagePiece,
                _editorLoading.Token
            );
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
            BeginPlacement(part);
            _onlinePartStatus = $"\"{asset.Name}\" is ready. Click the viewport to place it.";
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
            if (!_editorDisposed)
                Touch();
        }
    }
}
