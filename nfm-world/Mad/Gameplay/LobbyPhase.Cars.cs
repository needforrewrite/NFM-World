using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;
using SignalsDotnet;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private sealed record CarDetails(string Name, string? Class, CarColorSlot[] Colors);

    private readonly Dictionary<string, CarDetails> _carDetails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Signal<bool> _showCarPicker = new(false);
    private CancellationTokenSource _carCancellation = new();
    private CancellationTokenSource? _copyCancellation;
    private bool _resolvingCars;
    private bool _carsActive;
    private long _carAction;
    private GarageController? _carPicker;

    private Node BuildLobbyUi() => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, right: 0, bottom: 0),
        children: [UI.Routes.LobbyView.Build(Actions, BuildStagePreview()),
            Sx.Show(() => _showCarPicker.Value, () => Sx.View(
                style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                    zIndex: 3000,
                    backgroundColor: new Color(3, 8, 20, 255)
                ),
                children: [_carPicker!.BuildUi()]
            )
            )]
    );

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (!imguiWantsKeyboard && key == Key.Escape && _stagePreview.Value is not null)
            CloseStagePreview();
        else
            _carPicker?.KeyPressed(key, imguiWantsKeyboard, keys);
    }

    public override void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
        => _carPicker?.KeyRepeated(key, imguiWantsKeyboard, keys);

    public override void MouseReleased(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        if (button == MouseButton.Primary) _carPicker?.FinishPaintInteraction();
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);
        _carPicker?.RenderThumbnails(cb);
        _stagePreview.Value?.Render(cb, alpha);
    }

    public override void Exit()
    {
        _carsActive = false;
        _carCancellation.Cancel();
        CloseCarPicker();
        CloseStagePreview();
        base.Exit();
    }

    private async void OpenCarPicker()
    {
        if (!_carsActive || _carPicker is not null) return;
        CancelCarAction();
        var action = _carAction;
        var cancellation = _carCancellation.Token;
        var identity = MenuHistory.LastPlayed.Car;
        var originalColors = CarPaint.SavedColors(identity);
        var originalWheels = CarWheels.Get(identity);
        Rad3d? rad;
        using var loading = LoadingWork.Begin("Loading car picker...");
        try
        {
            rad = await ReadLobbyCarAsync(identity, cancellation);
            await LoadingWork.NextFrame;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await LoadingWork.NextFrame;
            if (!cancellation.IsCancellationRequested && action == _carAction)
                SetStatus($"Could not load the selected car: {ex.Message}");
            rad = null;
        }

        if (cancellation.IsCancellationRequested || action != _carAction) return;
        var current = rad is not null && BackendCar.IsDrivable(rad)
            ? new BackendCar(CarPaint.CreateInstance(rad), 0, 0, 0, true) : null;
        _carPicker = new GarageController(
            new ObservableUnlimitedArray<BackendCar>(),
            current,
            quickSelect: true,
            confirmLabel: "Select car",
            canSelect: MultiplayerContentPolicy.IsAcceptedCar
        );
        _carPicker.CarSelected += (_, _) =>
        {
            CloseCarPicker();
            IdentityChanged();
        };
        _carPicker.CarSelectionCancelled += (_, _) =>
        {
            CloseCarPicker();
            if (_carsActive && (!originalColors.SequenceEqual(CarPaint.SavedColors(identity))
                                || !Equals(originalWheels, CarWheels.Get(identity))))
                IdentityChanged();
        };
        _carPicker.Enter();
        _showCarPicker.Value = true;
    }

    private void CloseCarPicker()
    {
        CancelCarAction();
        _showCarPicker.Value = false;
        _carPicker?.End();
        _carPicker = null;
    }

    private void CancelCarAction()
    {
        _carAction++;
        _copyCancellation?.Cancel();
        _copyCancellation?.Dispose();
        _copyCancellation = null;
    }

    private void IdentityChanged()
    {
        if (CanAct) SendUpdatePlayerIdentity();
        var index = _players.FindIndex(p => p.Id == _player.Id);
        if (index < 0) return;
        var player = _players[index];
        player.CarName = MenuHistory.LastPlayed.Car;
        player.CarColors = CarPaint.SavedColors(player.CarName);
        player.Wheels = CarWheels.Get(player.CarName);
        _players[index] = player;
        Publish();
    }

    private string CarLabel(string identity)
        => _carDetails.TryGetValue(identity, out var details)
            ? string.IsNullOrWhiteSpace(details.Class) ? details.Name : $"{details.Name} · Class {details.Class}"
            : "Loading car...";

    private IReadOnlyList<Color> PlayerColors(ServerSidePlayerInfo player)
        => PlayerColors(player.CarName, player.CarColors);

    private IReadOnlyList<Color> PlayerColors(string carName, IReadOnlyList<Color3?>? overrides)
        => _carDetails.TryGetValue(carName, out var details)
            ? EffectiveColors(details, overrides).Select(slot => ToColor(slot.Color)).ToArray()
            : [];

    private static CarColorSlot[] EffectiveColors(CarDetails details, IReadOnlyList<Color3?>? overrides)
        => details.Colors.Select(slot => new CarColorSlot(
            slot.Slot,
            overrides is not null && slot.Slot < overrides.Count && overrides[slot.Slot] is { } color
                ? CarPaint.ClampColor(color) : slot.Color
        )
        ).ToArray();

    private IEnumerable<string> LobbyCarIdentities()
        => _players.Select(player => player.CarName)
            .Concat(_activeSessions.SelectMany(session => session.Bots.Values.Select(bot => bot.CarName)))
            .Concat(_activeSessions.SelectMany(session => session.FinalStandings.Select(standing => standing.CarName)))
            .Where(identity => !string.IsNullOrEmpty(identity));

    private void ResolvePlayerCars()
    {
        var identities = LobbyCarIdentities().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in _carDetails.Keys.Where(id => !identities.Contains(id)).ToArray())
            _carDetails.Remove(identity);
        if (!_carsActive || _resolvingCars || identities.All(_carDetails.ContainsKey)) return;
        _ = ResolvePlayerCarsAsync(_carCancellation.Token);
    }

    private async Task ResolvePlayerCarsAsync(CancellationToken cancellation)
    {
        _resolvingCars = true;
        try
        {
            using var loading = LoadingWork.Begin("Loading player cars...");
            while (!cancellation.IsCancellationRequested)
            {
                var identity = LobbyCarIdentities().FirstOrDefault(id => !_carDetails.ContainsKey(id));
                if (identity is null) break;
                CarDetails details;
                try
                {
                    var rad = await ReadLobbyCarAsync(identity, cancellation);
                    details = rad is null
                        ? new CarDetails("Unavailable car", null, [])
                        : new CarDetails(rad.Stats.Name, rad.Stats.Class, CarPaint.Slots(rad).ToArray());
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logging.Warning($"Could not read lobby car '{identity}': {ex.Message}");
                    details = new CarDetails("Unavailable car", null, []);
                }

                await LoadingWork.NextFrame;
                if (cancellation.IsCancellationRequested) return;
                _carDetails[identity] = details;
                Publish();
            }
        }
        finally
        {
            await LoadingWork.NextFrame;
            _resolvingCars = false;
            if (_carsActive) ResolvePlayerCars();
        }
    }

    private static Task<Rad3d?> ReadLobbyCarAsync(string identity, CancellationToken cancellation)
    {
        var loaded = BackendGameSparker.FindLoadedCar(identity);
        return Task.Run(async () =>
            {
                cancellation.ThrowIfCancellationRequested();
                if (loaded is not null) return loaded;
                if (!MultiplayerContentPolicy.IsAcceptedCar(identity)) return null;
                if (RadpackReader.IsArchiveId(identity))
                {
                    var prepared = await ContentCache.PrepareAsync(
                        GarageController.ContentClient,
                    identity,
                        AssetKind.Car,
                        cancellation
                    );
                    if (!prepared.Ok) throw new InvalidDataException(prepared.Error);
                }

                cancellation.ThrowIfCancellationRequested();
                var rad = RadpackReader.TryReadModel(identity, RadpackType.Car, out var error);
                if (error is not null) throw new InvalidDataException(error);
                return rad;
            }, cancellation
        );
    }

    private async void CopyPlayerCar(Guid playerId, bool colorsOnly)
    {
        if (!_carsActive || _carPicker is not null || playerId == _player.Id) return;
        var source = _players.FirstOrDefault(p => p.Id == playerId);
        if (source.Id != playerId) return;
        CancelCarAction();
        var action = _carAction;
        var destination = MenuHistory.LastPlayed.Car;
        var copyCancellation = CancellationTokenSource.CreateLinkedTokenSource(_carCancellation.Token);
        _copyCancellation = copyCancellation;
        var cancellation = copyCancellation.Token;
        var sourceColors = source.CarColors?.ToArray();
        using var loading = LoadingWork.Begin(colorsOnly ? "Copying car colors..." : "Loading car...");
        try
        {
            var sourceRad = await ReadLobbyCarAsync(source.CarName, cancellation);
            await LoadingWork.NextFrame;
            if (cancellation.IsCancellationRequested || action != _carAction) return;
            var destinationRad = colorsOnly ? await ReadLobbyCarAsync(destination, cancellation) : sourceRad;
            await LoadingWork.NextFrame;
            if (cancellation.IsCancellationRequested || action != _carAction) return;
            if (sourceRad is null || destinationRad is null || !BackendCar.IsDrivable(destinationRad))
            {
                SetStatus("That car could not be loaded.");
                return;
            }

            _carDetails[source.CarName] = new CarDetails(
                sourceRad.Stats.Name,
                sourceRad.Stats.Class,
                CarPaint.Slots(sourceRad).ToArray()
            );
            if (colorsOnly)
            {
                var details = new CarDetails(
                    sourceRad.Stats.Name,
                    sourceRad.Stats.Class,
                    CarPaint.Slots(sourceRad).ToArray()
                );
                var colors = EffectiveColors(details, sourceColors);
                var copied = CarColorCopy.Match(CarPaint.Slots(destinationRad), colors);
                foreach (var slot in copied) CarPaint.Set(destination, slot.Slot, slot.Color);
                CarPaint.Flush();
                if (copied.Count == 0)
                {
                    SetStatus("These cars have no matching color channels.");
                    return;
                }

                CarThumbnails.Invalidate(destination);
            }
            else
            {
                if (!MultiplayerContentPolicy.IsAcceptedCar(source.CarName)) return;
                MenuHistory.Remember(car: source.CarName);
            }

            IdentityChanged();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await LoadingWork.NextFrame;
            if (!cancellation.IsCancellationRequested && action == _carAction)
                SetStatus($"Could not copy the car{(colorsOnly ? " colors" : "")}: {ex.Message}");
        }
        finally
        {
            await LoadingWork.NextFrame;
            if (ReferenceEquals(_copyCancellation, copyCancellation))
            {
                _copyCancellation = null;
                copyCancellation.Dispose();
            }
        }
    }
}
