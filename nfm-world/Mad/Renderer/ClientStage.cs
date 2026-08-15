using System;
using System.Collections.Specialized;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorld;

/// <summary>
/// Client-side representation of a stage. Composes a <see cref="BackendStage"/> for collision/AI data
/// and adds rendering (stage geometry, cars, scene management). Owns camera and light setup.
/// Fully self-contained — constructed once per phase; car visuals are synced lazily each tick.
/// </summary>
public class ClientStage : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<IInGameCar, CarVisual> _carVisuals = new();
    private readonly Dictionary<ClientSidePlayer, CarVisual> _playerVisuals = new();
    private ObservableUnlimitedArray<IInGameCar> _cars;
    private ObservableUnlimitedArray<ClientSidePlayer>? _players;
    private Scene _scene;
    private bool _disposed;

    public BackendStage Backend { get; }
    public ClientStageRenderer Renderer { get; }
    public Camera Camera { get; set; }
    public IReadOnlyList<Camera> LightCameras { get; set; }

    // ── Music metadata from stage loader ──
    public string MusicPath { get; }
    public string RemasteredMusicPath { get; }
    public double MusicFreqMul { get; }
    public double MusicTempoMul { get; }

    public ClientStage(
        GraphicsDevice graphicsDevice,
        string stageName,
        ObservableUnlimitedArray<IInGameCar> cars,
        Camera camera,
        IReadOnlyList<Camera> lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _cars = cars;
        Camera = camera;
        LightCameras = lightCameras;

        Backend = new BackendStage(stageName);
        Renderer = new ClientStageRenderer(graphicsDevice, Backend);
        Renderer.ApplyValues();

        // Scene starts with just the stage renderer — car visuals are added lazily in GameTick()
        _scene = new Scene(graphicsDevice, [Renderer], camera, lightCameras);

        // ── Music metadata ──
        MusicPath = Backend.stageLoader.musicPath;
        RemasteredMusicPath = Backend.stageLoader.remasteredMusicPath;
        MusicFreqMul = Backend.stageLoader.musicFreqMul;
        MusicTempoMul = Backend.stageLoader.musicTempoMul;

        if (string.IsNullOrEmpty(MusicPath))
            Logging.Error("No music is defined for this stage!");
        
        _cars.CollectionChanged += CarsOnCollectionChanged;
    }

    /// <summary>
    /// Replace the set of backend cars this stage tracks.
    /// </summary>
    public void SetCars(ObservableUnlimitedArray<IInGameCar> cars)
    {
        _cars.CollectionChanged -= CarsOnCollectionChanged;
        _cars = cars;
        _cars.CollectionChanged += CarsOnCollectionChanged;
    }

    /// <summary>
    /// Replaces the set of players this stage tracks (race path). The stage
    /// creates a <see cref="CarVisual"/> for each player's <see cref="ClientSidePlayer.Car"/>
    /// and keeps it in sync as cars are assigned or players join/leave.
    /// </summary>
    public void SetPlayers(ObservableUnlimitedArray<ClientSidePlayer> players)
    {
        if (_players is { } oldPlayers)
        {
            oldPlayers.CollectionChanged -= PlayersOnCollectionChanged;
            foreach (var player in oldPlayers)
                DetachPlayer(player);
        }

        _players = players;
        players.CollectionChanged += PlayersOnCollectionChanged;
        foreach (var player in players)
            AttachPlayer(player);
    }

    /// <summary>
    /// Gets or creates the <see cref="CarVisual"/> for a backend car.
    /// The visual is added to the scene immediately.
    /// </summary>
    public CarVisual GetCarVisual(IInGameCar car)
    {
        if (!_carVisuals.TryGetValue(car, out var visual))
        {
            visual = _carVisuals[car] = new CarVisual(_graphicsDevice, car);
            _scene.Objects.Add(visual);
        }
        return visual;
    }

    /// <summary>
    /// Gets the <see cref="CarVisual"/> for a backend car by index.
    /// In player mode the index is a player index.
    /// </summary>
    public CarVisual GetCarVisual(int index)
    {
        if (_players is { } players)
        {
            var car = players[index].Car
                ?? throw new InvalidOperationException($"Player {index} has no car.");
            return GetCarVisual(car);
        }

        return GetCarVisual(_cars.ElementAt(index));
    }

    /// <summary>
    /// The backend cars currently tracked by this stage.
    /// </summary>
    public IReadOnlyCollection<IInGameCar> Cars => _cars;

    // ── Scene lifecycle ──

    public void OnBeforeGameTick()
    {
        Camera.OnBeforeGameTick();
        foreach (var lightCamera in LightCameras)
            lightCamera.OnBeforeGameTick();
    }

    private void AttachPlayer(ClientSidePlayer player)
    {
        player.CarChanged += OnPlayerCarChanged;
        if (player.Car is { } car)
            CreatePlayerVisual(player, car);
    }

    private void DetachPlayer(ClientSidePlayer player)
    {
        player.CarChanged -= OnPlayerCarChanged;
        if (_playerVisuals.Remove(player, out var visual))
        {
            _scene.Objects.Remove(visual);
            _carVisuals.Remove(visual.Car);
            visual.Dispose();
        }
    }

    private void OnPlayerCarChanged(ClientSidePlayer player, IInGameCar? car)
    {
        if (_playerVisuals.Remove(player, out var oldVisual))
        {
            _scene.Objects.Remove(oldVisual);
            _carVisuals.Remove(oldVisual.Car);
            oldVisual.Dispose();
        }

        if (car is not null)
            CreatePlayerVisual(player, car);
    }

    private void CreatePlayerVisual(ClientSidePlayer player, IInGameCar car)
    {
        _playerVisuals[player] = GetCarVisual(car);
    }

    private void PlayersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (ClientSidePlayer player in e.NewItems!)
                    AttachPlayer(player);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (ClientSidePlayer player in e.OldItems!)
                    DetachPlayer(player);
                break;
            case NotifyCollectionChangedAction.Replace:
                foreach (ClientSidePlayer player in e.OldItems!)
                    DetachPlayer(player);
                foreach (ClientSidePlayer player in e.NewItems!)
                    AttachPlayer(player);
                break;
            case NotifyCollectionChangedAction.Move:
                // Visuals are tracked by player reference, not index.
                break;
            case NotifyCollectionChangedAction.Reset:
                if (_players is not { } players)
                    break;
                foreach (var player in players)
                    DetachPlayer(player);
                foreach (var player in players)
                    AttachPlayer(player);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Eagerly syncs car visuals against <see cref="_cars"/> (which is a live reference
    /// to the phase's <c>CarsInRace</c>).
    /// </summary>
    private void CarsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (IInGameCar car in e.NewItems!)
                {
                    if (!_carVisuals.ContainsKey(car))
                    {
                        var visual = _carVisuals[car] = new CarVisual(_graphicsDevice, car);
                        _scene.Objects.Add(visual);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (IInGameCar car in e.OldItems!)
                {
                    if (_carVisuals.Remove(car, out var visual))
                    {
                        _scene.Objects.Remove(visual);
                        visual.Dispose();
                    }
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                foreach (IInGameCar car in e.OldItems!)
                {
                    if (_carVisuals.Remove(car, out var visual))
                    {
                        _scene.Objects.Remove(visual);
                        visual.Dispose();
                    }
                }
                
                foreach (IInGameCar car in e.NewItems!)
                {
                    if (!_carVisuals.ContainsKey(car))
                    {
                        var visual = _carVisuals[car] = new CarVisual(_graphicsDevice, car);
                        _scene.Objects.Add(visual);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Move:
                // No need to do anything — the visuals are tracked by reference, not index, so moving items around
                // doesn't affect them.
                break;
            case NotifyCollectionChangedAction.Reset:
                // ── Remove visuals for cars that left the collection ──
                var removed = _carVisuals.Keys.Except(_cars).ToArray();
                foreach (var key in removed)
                {
                    if (_carVisuals.Remove(key, out var visual))
                    {
                        _scene.Objects.Remove(visual);
                        visual.Dispose();
                    }
                }
                
                // ── Create visuals for cars that joined the collection ──
                foreach (var car in _cars)
                {
                    if (!_carVisuals.ContainsKey(car))
                    {
                        var visual = _carVisuals[car] = new CarVisual(_graphicsDevice, car);
                        _scene.Objects.Add(visual);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Ticks all scene objects.
    /// </summary>
    public void GameTick()
    {
        foreach (var obj in _scene.Objects)
            obj.GameTick(Backend);
    }

    public void Render(float alpha, bool useShadowMapping = true, bool clearRenderBuffer = true)
    {
        Renderer.ApplyValues();
        _scene.ActiveCamera = Camera;
        _scene.Render(alpha, useShadowMapping, clearRenderBuffer);
    }

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cars.CollectionChanged -= CarsOnCollectionChanged;

        if (_players is { } players)
        {
            players.CollectionChanged -= PlayersOnCollectionChanged;
            foreach (var player in players)
                DetachPlayer(player);
        }

        foreach (var visual in _carVisuals.Values)
            visual.Dispose();
        _carVisuals.Clear();

        _scene.Dispose();
        Renderer.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}