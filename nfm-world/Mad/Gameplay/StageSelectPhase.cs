using System.Net.Http;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// Stage selection: the shared Sx selector over every playable stage (text, radpack and public
/// archive stages), with this phase owning the 3D preview and its music.
/// </summary>
public class StageSelectPhase : BaseStageRenderingPhase
{
    public event EventHandler<string>? StageSelected;

    private static readonly ArchiveContentClient ContentClient = GarageController.ContentClient;

    private readonly string[] _allStages;
    private readonly IReadOnlyDictionary<string, string> _stageNames;
    private readonly StageSelectState _state = new();
    private readonly StageSelectView.Actions _actions;
    private readonly AroundStageCamera _aroundStageCamera = new();
    private bool _confirmAfterLoad;
    private string _loadedStage = "";
    private int _floatTick;
    private CancellationTokenSource? _onlineCancellation;
    private int _onlineGeneration;

    public StageSelectPhase() : base()
    {
        _actions = new StageSelectView.Actions(
            CollectionSelected: SelectCollection,
            StageSelected: SelectStage,
            SearchChanged: OnSearchChanged,
            Confirm: Confirm,
            Back: GameSparker.ReturnToMainMenu,
            SourceSelected: SetOnlineSource,
            OnlineSearch: query => _ = SearchOnlineAsync(query)
        );
        _allStages = MenuPreferences.GetStages(VFS);
        _stageNames = MenuPreferences.GetStageNames(VFS);

        if (_allStages.Length == 0)
            throw new DirectoryNotFoundException(
                "No playable stages were found in data/stages. Check that the game assets are installed."
            );

        var collections = _allStages
            .Select(CollectionOf)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _state.Collections.Value = collections;
        _state.BrowserOpen.Value = true;

        // Start somewhere in the middle of the first collection, as the old screen did.
        var collection = collections[new System.Random().Next(0, collections.Length)];
        _state.Collection.Value = collection;
        RefreshStages(loadPreview: true);

        LoadStageMusic();
    }

    public override Node? BuildUi() => StageSelectView.Preview(_state, _actions);
    internal bool ShowingLoadingOverlay => _state.Busy.Value;

    protected override (string Path, double Tempo, double Frequency)? GetMusicTrack()
        => ("data/music/nfm1/stageselectremastered.opus", 1, 1);

    protected override void OnStageLoaded()
    {
        CurrentStage!.FadeFromOverride = 9999999;
        _aroundStageCamera.Reset();
        _aroundStageCamera.AroundStage(Camera, CurrentStage.Backend);
        _loadedStage = StageName!;
        _state.PreviewName.Value = _state.Stages.Value.FirstOrDefault(stage => stage.Id == _loadedStage)?.Label
                                   ?? CurrentStage.Backend.Name;
        ShowDescription(CurrentStage.Backend.StageLoader);
        if (_state.Selected.Value != StageName)
        {
            if (!_state.OnlineSource.Value)
            {
                _state.Busy.Value = false;
                _state.ConfirmEnabled.Value = _state.Selected.Value.Length > 0;
            }

            return;
        }

        _state.Busy.Value = false;
        _state.ConfirmEnabled.Value = true;
        _state.Status.Value = "";
        if (_confirmAfterLoad) StageSelected?.Invoke(this, StageName!);
        _confirmAfterLoad = false;
    }

    protected override void OnStageLoadFailed(Exception error)
    {
        _state.DescriptionLines.Value = [];
        _state.Busy.Value = false;
        _state.ConfirmEnabled.Value = false;
        _state.Status.Value = "Could not load the stage: " + error.Message;
        _confirmAfterLoad = false;
    }

    /// <summary>The loaded stage's briefing, or nothing when every line is blank.</summary>
    private void ShowDescription(StageLoader loader)
    {
        var lines = loader.Description;
        _state.DescriptionLines.Value = lines.Any(line => !string.IsNullOrWhiteSpace(line)) ? [.. lines] : [];
        _floatTick = 0;
        _state.DescriptionFloat.Value = 0;
    }

    /// <summary>The directory a stage id lives in; ids without one are shown as "stages".</summary>
    private static string CollectionOf(string stageId)
    {
        var slash = stageId.IndexOf('/');
        return slash < 0 ? "stages" : stageId[..slash];
    }

    private string StageLabel(string stageId)
    {
        if (_stageNames.TryGetValue(stageId, out var name))
            return name;

        var slash = stageId.LastIndexOf('/');
        return slash < 0 ? stageId : stageId[(slash + 1)..];
    }

    private void SetOnlineSource(bool online)
    {
        if (_state.OnlineSource.Value == online)
            return;

        _onlineCancellation?.Cancel();
        _onlineGeneration++;
        _confirmAfterLoad = false;

        _state.OnlineSource.Value = online;
        _state.BrowsingCollections.Value = false;
        _state.Search.Value = "";
        StageSelectView.ClearSearch();
        _state.Busy.Value = false;
        _state.Status.Value = "";
        _state.Restriction.Value = "";
        _state.Selected.Value = "";
        _state.ConfirmEnabled.Value = false;
        _state.Stages.Value = [];

        if (online)
        {
            _ = SearchOnlineAsync("");
            return;
        }

        if (_state.Collections.Value.Count > 0 && !_state.Collections.Value.Contains(_state.Collection.Value))
            _state.Collection.Value = _state.Collections.Value[0];
        RefreshStages(loadPreview: true);
    }

    private async Task SearchOnlineAsync(string query)
    {
        using var loading = LoadingWork.Begin("Searching archive...");
        _onlineCancellation?.Cancel();
        var cancellation = _onlineCancellation = new CancellationTokenSource();
        var generation = ++_onlineGeneration;
        _state.Busy.Value = true;
        _state.Status.Value = "";

        try
        {
            var page = await ContentClient.SearchAsync(query, "Stage", 40, null, cancellation.Token);
            await LoadingWork.NextFrame;
            if (generation != _onlineGeneration)
                return;

            var options = page.Items
                .Where(item => item.Eligible)
                .Select(item => new StageOption(
                        RadpackReader.ArchivePrefix + item.ContentHash.ToLowerInvariant(),
                        item.Name,
                        item.Author
                    )
                )
                .ToArray();

            _state.Stages.Value = options;
            _state.Status.Value = "";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await LoadingWork.NextFrame;
            if (generation != _onlineGeneration)
                return;

            _state.Stages.Value = [];
            _state.Status.Value = $"Could not search the archive: {ex.Message}";
        }
        finally
        {
            await LoadingWork.NextFrame;
            if (generation == _onlineGeneration)
                _state.Busy.Value = false;
        }
    }

    private void SelectCollection(string collection)
    {
        if (_state.OnlineSource.Value || _state.Collection.Value == collection)
            return;

        _state.Collection.Value = collection;
        RefreshStages(loadPreview: true);
    }

    private void OnSearchChanged(string query)
    {
        _state.Search.Value = query;
        RefreshStages(loadPreview: false);
    }

    private void SelectStage(string stageId)
    {
        var option = _state.Stages.Value.FirstOrDefault(stage => stage.Id == stageId);
        if (option is null)
            return;

        _state.Selected.Value = stageId;
        _state.ConfirmEnabled.Value = true;
        _state.Status.Value = "";
        if (stageId == _loadedStage && !IsStageLoading) return;
        _state.DescriptionLines.Value = [];

        if (RadpackReader.IsArchiveId(stageId))
        {
            // Archive stages have to be downloaded and verified before the preview can read them.
            PrepareStage(stageId, confirmAfter: false);
            return;
        }

        _confirmAfterLoad = false;
        _state.Busy.Value = true;
        _state.ConfirmEnabled.Value = false;
        LoadStage(stageId, loadMusic: false, reloadIfLoaded: true);
        // ApplyValues re-applies the stage's fadefrom() every frame, so override it after that.
        if (CurrentStage != null)
            CurrentStage.FadeFromOverride = 9999999;
    }

    /// <summary>Prepares a public stage, then previews it or starts the race when asked.</summary>
    private void PrepareStage(string stageId, bool confirmAfter)
    {
        _confirmAfterLoad = confirmAfter;
        _state.Busy.Value = true;
        _state.ConfirmEnabled.Value = false;
        _state.Status.Value = "";
        _state.Selected.Value = stageId;
        _state.DescriptionLines.Value = [];
        LoadStage(stageId, loadMusic: false, reloadIfLoaded: true);
    }

    private void Confirm()
    {
        if (!_state.ConfirmEnabled.Value || IsStageLoading)
            return;

        var selected = _state.Selected.Value;
        if (string.IsNullOrEmpty(selected))
        {
            _state.Status.Value = "Pick a stage first.";
            return;
        }

        if (selected != _loadedStage)
        {
            PrepareStage(selected, confirmAfter: true);
            return;
        }

        StageSelected?.Invoke(this, selected);
    }

    private void RefreshStages(bool loadPreview)
    {
        if (_state.OnlineSource.Value)
            return;

        var query = _state.Search.Value.Trim();
        var stages = _allStages
            .Where(id => string.Equals(CollectionOf(id), _state.Collection.Value, StringComparison.OrdinalIgnoreCase))
            .Where(id => query.Length == 0
                         || StageLabel(id).Contains(query, StringComparison.OrdinalIgnoreCase)
                         || id.Contains(query, StringComparison.OrdinalIgnoreCase)
            )
            .Select(id => new StageOption(id, StageLabel(id)))
            .ToArray();

        _state.Stages.Value = stages;
        if (!loadPreview) return;

        if (stages.Length == 0)
        {
            _state.Selected.Value = "";
            _state.ConfirmEnabled.Value = false;
            return;
        }

        var selected = stages.Any(stage => stage.Id == _state.Selected.Value)
            ? _state.Selected.Value
            : stages[0].Id;
        _state.Selected.Value = selected;
        _state.ConfirmEnabled.Value = true;

        if (selected != _loadedStage || IsStageLoading)
            SelectStage(selected);
    }

    public override void GameTick()
    {
        base.GameTick();

        if (CurrentStage != null)
            _aroundStageCamera.AroundStage(Camera, CurrentStage.Backend);

        if (_state.DescriptionLines.Value.Count == 0)
            return;

        // A slow bob; the briefing is meant to hover over the course, not sit on it.
        _floatTick++;
        _state.DescriptionFloat.Value = MathF.Sin(_floatTick * 0.03f) * 3f;
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard)
            return;

        StageSelectView.HandlePreviewKey(_state, _actions, key);
    }

    public override void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
            KeyPressed(key, imguiWantsKeyboard, keys);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _onlineGeneration++;
            _onlineCancellation?.Cancel();
            _onlineCancellation?.Dispose();
            _onlineCancellation = null;
        }
    }
}
