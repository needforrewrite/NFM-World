using System.Collections.Concurrent;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.Gameplay;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI.Components;

internal sealed class MenuStagePicker : IDisposable
{
    private readonly StageSelectState _state = new();
    private readonly StageSelectView.Actions _actions;
    private readonly StageOption[] _local;
    private readonly Action<string> _select;
    private readonly Action _close;
    private CancellationTokenSource? _request;
    private int _generation;
    private bool _disposed;
    private Node? _view;
    private readonly ConcurrentQueue<Action> _completions = new();

    public MenuStagePicker(string selected, Action<string> select, Action close)
    {
        _select = select;
        _close = close;
        var names = MenuPreferences.GetStageNames(TheVFS.VFS);
        _local = MenuPreferences.GetStages(TheVFS.VFS).Select(id => new StageOption(
            id,
            names.GetValueOrDefault(id) ?? id[(id.LastIndexOf('/') + 1)..],
            Collection(id)
        )
        ).ToArray();
        _state.Collections.Value = ["All", .. _local.Select(stage => stage.Subtitle!)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
        _state.Collection.Value = "All";
        _state.Selected.Value = selected;
        _state.ConfirmLabel.Value = "USE STAGE";
        _state.Description.Value = "Choose a menu background.";
        _state.ConfirmEnabled.Value = true;
        _actions = new(
            StageCollection,
            Pick,
            Filter,
            () => _ = ConfirmAsync(),
            Close,
            Source,
            query => _ = SearchAsync(query),
            SourceControlsHint: "PgUp  Local / PgDn  Archive"
        );
        RefreshLocal();
        if (RadpackReader.IsArchiveId(selected))
            _state.Stages.Value = [new(selected, "Selected public stage", "Archive"), .. _state.Stages.Value];
    }

    private static string Collection(string id) => id.Contains('/') ? id[..id.IndexOf('/')] : "stages";

    public Node Build()
    {
        _view = Sx.View(
            name: () => "Menu background stage picker",
            style: () => Sx.Styles(
                position: Position.Absolute,
                top: 0,
                bottom: 0,
                left: 0,
                right: 0,
                zIndex: 100,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 90)
            ),
            children: Sx.View(
                style: () => Sx.Styles(
                    width: 94.Percent(),
                    height: 90.Percent(),
                    backgroundColor: Theme.MenuPanel,
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: Theme.MenuBorder,
                    borderRadius: 8,
                    overflow: Overflow.Hidden
                ),
                children: StageSelectView.Build(_state, _actions, "Menu Background")
            )
        );
        return _view;
    }

    public void Tick()
    {
        while (_completions.TryDequeue(out var complete)) complete();
    }

    public bool HandleKey(Key key)
    {
        if (FocusManager.FocusedNode is not (TextInput or TextArea))
        {
            if (key == Key.PageUp)
            {
                Source(false);
                return true;
            }

            if (key == Key.PageDown)
            {
                Source(true);
                return true;
            }
        }

        return StageSelectView.HandleKey(_state, _actions, key);
    }

    private void Pick(string id)
    {
        if (_state.Busy.Value) return;
        _state.Selected.Value = id;
        _state.ConfirmEnabled.Value = true;
    }

    private void StageCollection(string collection)
    {
        if (_state.OnlineSource.Value) return;
        _state.Collection.Value = collection;
        RefreshLocal();
    }

    private void Filter(string query)
    {
        _state.Search.Value = query;
        if (!_state.OnlineSource.Value) RefreshLocal();
    }

    private void RefreshLocal()
    {
        var query = _state.Search.Value.Trim();
        IEnumerable<StageOption> stages = _local;
        if (_state.Collection.Value != "All") stages = stages.Where(stage => stage.Subtitle == _state.Collection.Value);
        else stages = stages.Prepend(new StageOption(MenuPreferences.LastPlayedStage, "Last played stage", "Automatic"));
        _state.Stages.Value = stages.Where(stage => query.Length == 0
                                                    || stage.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                                                    || stage.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
        ).ToArray();
    }

    private void Source(bool online)
    {
        if (_state.OnlineSource.Value == online) return;
        CancelRequest();
        _state.OnlineSource.Value = online;
        _state.BrowsingCollections.Value = false;
        _state.Search.Value = "";
        StageSelectView.ClearSearch();
        _state.Status.Value = "";
        _state.Stages.Value = [];
        _state.Busy.Value = false;
        if (online) _ = SearchAsync("");
        else RefreshLocal();
    }

    private bool IsCurrent(int generation) => !_disposed && generation == _generation
                                              && Contains(GameSparker.UiRenderer?.ActiveRoot, _view);

    private static bool Contains(Node? root, Node? target)
        => root is not null && target is not null
           && (ReferenceEquals(root, target) || root.VisualChildren.Any(child => Contains(child, target)));

    private async Task SearchAsync(string query)
    {
        using var loading = LoadingWork.Begin("Searching archive...");
        CancelRequest();
        var cancellation = _request = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var generation = _generation;
        _state.Busy.Value = true;
        _state.Status.Value = "Searching the archive...";
        try
        {
            var page = await GarageController.ContentClient.SearchAsync(query, "Stage", 40, null, cancellation.Token);
            _completions.Enqueue(() =>
                {
                    if (!IsCurrent(generation)) return;
                    _state.Stages.Value = page.Items.Where(item => item.Eligible)
                        .Select(item => new StageOption(
                            RadpackReader.ArchivePrefix + item.ContentHash.ToLowerInvariant(),
                        item.Name,
                            item.AuthorByline
                        )
                        ).ToArray();
                    _state.Status.Value = _state.Stages.Value.Count == 0 ? "No public stages matched." : "";
                }
            );
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _completions.Enqueue(() =>
                {
                    if (IsCurrent(generation)) _state.Status.Value = "Could not search the archive: " + ex.Message;
                }
            );
        }
        finally
        {
            _completions.Enqueue(() => { if (IsCurrent(generation)) _state.Busy.Value = false; });
        }
    }

    private async Task ConfirmAsync()
    {
        if (_state.Busy.Value || !_state.ConfirmEnabled.Value) return;
        using var loading = LoadingWork.Begin("Preparing background stage...");
        var selected = _state.Selected.Value;
        CancelRequest();
        var cancellation = _request = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var generation = _generation;
        _state.Busy.Value = true;
        _state.Status.Value = "Preparing background stage...";
        try
        {
            if (RadpackReader.IsArchiveId(selected))
            {
                var token = cancellation.Token;
                var result = await Task.Run(() => ContentCache.PrepareAsync(
                    GarageController.ContentClient,
                    selected,
                    AssetKind.Stage,
                    token
                )
                );
                if (!result.Ok)
                {
                    _completions.Enqueue(() =>
                        {
                            if (IsCurrent(generation))
                                _state.Status.Value = result.Error ?? "That stage could not be downloaded.";
                        }
                    );
                    return;
                }
            }

            _completions.Enqueue(() =>
                {
                    if (!IsCurrent(generation)) return;
                    _select(selected);
                    Close();
                }
            );
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _completions.Enqueue(() =>
                {
                    if (IsCurrent(generation)) _state.Status.Value = "Could not prepare the stage: " + ex.Message;
                }
            );
        }
        finally
        {
            _completions.Enqueue(() => { if (IsCurrent(generation)) _state.Busy.Value = false; });
        }
    }

    private void CancelRequest()
    {
        _generation++;
        _request?.Cancel();
        _request?.Dispose();
        _request = null;
    }

    private void Close()
    {
        Dispose();
        _close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelRequest();
    }
}
