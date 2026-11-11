using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// Stage selection: the shared Sx selector over every playable stage (text and radpack), with
/// this phase owning the 3D preview and its music.
/// </summary>
public class StageSelectPhase : BaseStageRenderingPhase
{
    public event EventHandler<string>? StageSelected;

    private readonly string[] _allStages;
    private readonly StageSelectState _state = new();
    private readonly AroundStageCamera _aroundStageCamera = new();
    private IRadicalMusic _stageSelectMusic;
    private string _loadedStage = "";

    public StageSelectPhase() : base()
    {
        _allStages = MenuPreferences.GetStages(VFS);

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

        // Start somewhere in the middle of the first collection, as the old screen did.
        var collection = collections[new System.Random().Next(0, collections.Length)];
        _state.Collection.Value = collection;
        RefreshStages(loadPreview: true);

        _stageSelectMusic = IBackend.Backend.LoadMusic("data/music/nfm1/stageselectremastered.mp3");
    }

    public override Node? BuildUi() => StageSelectView.Build(
        _state,
        new StageSelectView.Actions(
            CollectionSelected: SelectCollection,
            StageSelected: SelectStage,
            SearchChanged: OnSearchChanged,
            Confirm: Confirm,
            Back: GameSparker.ReturnToMainMenu
        )
    );

    public override void Enter()
    {
        base.Enter();
        GameSparker.CurrentMusic = _stageSelectMusic;
    }

    /// <summary>The directory a stage id lives in; ids without one are shown as "stages".</summary>
    private static string CollectionOf(string stageId)
    {
        var slash = stageId.IndexOf('/');
        return slash < 0 ? "stages" : stageId[..slash];
    }

    private string StageLabel(string stageId)
    {
        var slash = stageId.LastIndexOf('/');
        return slash < 0 ? stageId : stageId[(slash + 1)..];
    }

    private void SelectCollection(string collection)
    {
        if (_state.Collection.Value == collection)
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
        if (!_state.Stages.Value.Contains(stageId))
            return;

        _state.Selected.Value = stageId;
        _state.ConfirmEnabled.Value = true;
        LoadStage(stageId, loadMusic: false, reloadIfLoaded: true);
        _loadedStage = stageId;
        // ApplyValues re-applies the stage's fadefrom() every frame, so override it after that.
        if (CurrentStage != null)
            CurrentStage.FadeFromOverride = 9999999;
    }

    private void Confirm()
    {
        if (!_state.ConfirmEnabled.Value || string.IsNullOrEmpty(_state.Selected.Value))
            return;

        StageSelected?.Invoke(this, _state.Selected.Value);
    }

    private void RefreshStages(bool loadPreview)
    {
        var query = _state.Search.Value.Trim();
        var stages = _allStages
            .Where(id => string.Equals(CollectionOf(id), _state.Collection.Value, StringComparison.OrdinalIgnoreCase))
            .Where(id => query.Length == 0
                         || StageLabel(id).Contains(query, StringComparison.OrdinalIgnoreCase)
                         || id.Contains(query, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();

        _state.Stages.Value = stages;

        if (stages.Length == 0)
        {
            _state.Selected.Value = "";
            _state.ConfirmEnabled.Value = false;
            return;
        }

        var selected = stages.Contains(_state.Selected.Value) ? _state.Selected.Value : stages[0];
        _state.Selected.Value = selected;
        _state.ConfirmEnabled.Value = true;

        if (loadPreview && selected != _loadedStage)
            SelectStage(selected);
    }

    public override void GameTick()
    {
        base.GameTick();

        if (CurrentStage != null)
            _aroundStageCamera.AroundStage(Camera, CurrentStage.Backend);
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        if (CurrentStage == null)
            return;

        G.SetFont(new Font(FontFamily.NotoSans, FontStyle.Bold, 48));
        G.SetColor(new Color(255, 255, 255));
        G.DrawStringOutlinedAligned(
            CurrentStage.Backend.Name,
            0,
            60,
            GameSparker.Game.Window.Width,
            GameSparker.Game.Window.Height,
            new Color(0, 0, 0),
            1f,
            TextHorizontalAlignment.Center
        );
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        if (imguiWantsKeyboard)
            return;

        var stages = _state.Stages.Value;
        if (stages.Count == 0)
            return;

        var index = 0;
        for (var i = 0; i < stages.Count; i++)
        {
            if (stages[i] == _state.Selected.Value)
            {
                index = i;
                break;
            }
        }

        switch (key)
        {
            case Key.Left:
                SelectStage(stages[(index - 1 + stages.Count) % stages.Count]);
                break;
            case Key.Right:
                SelectStage(stages[(index + 1) % stages.Count]);
                break;
            case Key.Enter:
                Confirm();
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _stageSelectMusic.Dispose();
            _stageSelectMusic = null!;
        }
    }
}
