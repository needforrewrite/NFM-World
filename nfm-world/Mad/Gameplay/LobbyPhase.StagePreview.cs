using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using SignalsDotnet;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private readonly Signal<LobbyStagePreview?> _stagePreview = new(null);

    private Node BuildStagePreview()
        => Sx.Show(() => _stagePreview.Value is not null, () =>
            {
                var preview = _stagePreview.Value!;
                return LobbyView.StagePreviewWindow(
                () => Lobby.Sessions.Value.FirstOrDefault(session => session.Id == preview.SessionId)?.StageName
                      ?? "Stage preview",
                preview.State,
                () => preview.Image,
                CloseStagePreview
            );
            }
        );

    private void OpenStagePreview(uint sessionId)
    {
        if (!_carsActive || _stagePreview.Value is not null || Lobby.Page.Value != UiState.LobbyPage.Match
            || Lobby.ViewedSessionId.Value != sessionId)
            return;

        var stageId = _activeSessions.FirstOrDefault(session => session.Id == sessionId).StageName;
        if (!MultiplayerContentPolicy.IsAcceptedStage(stageId)) return;

        FocusManager.ClearFocus();
        var preview = new LobbyStagePreview(sessionId);
        _stagePreview.Value = preview;
        _ = preview.LoadAsync(stageId);
    }

    private void CloseStagePreview()
    {
        var preview = _stagePreview.Value;
        _stagePreview.Value = null;
        preview?.Dispose();
    }

    private sealed class LobbyStagePreview(uint sessionId) : IDisposable
    {
        private const int Width = 960;
        private const int Height = 540;

        private readonly CancellationTokenSource _cancellation = new(TimeSpan.FromMinutes(2));
        private readonly PerspectiveCamera _camera = new() { Width = Width, Height = Height };
        private readonly AroundStageCamera _flyover = new();
        private ClientStage? _stage;
        private IRenderTarget? _target;
        private int _floatTick;
        private bool _disposed;

        public uint SessionId { get; } = sessionId;
        public StageSelectState State { get; } = new();
        public IImage? Image { get; private set; }

        public async Task LoadAsync(string stageId)
        {
            State.Busy.Value = true;
            State.Status.Value = "Loading stage...";
            var token = _cancellation.Token;
            ClientStage? stage = null;
            try
            {
                stage = await ClientStage.CreateAsync(stageId, [], _camera, [], token);
                await LoadingWork.NextFrame;
                token.ThrowIfCancellationRequested();

                var device = GameSparker.GraphicsDevice;
                _target = device.CreateRenderTarget(new RenderTargetDesc(Width, Height, TextureFormat.Rgba8));
                Image = G.WrapTexture(_target.ColorTexture);
                stage.Renderer.Activate();
                stage.FadeFromOverride = 9999999;
                _flyover.AroundStage(_camera, stage.Backend);
                _camera.OnBeforeGameTick();
                var lines = stage.Backend.StageLoader.Description;
                State.DescriptionLines.Value = lines.Any(line => !string.IsNullOrWhiteSpace(line)) ? [.. lines] : [];
                stage.Adopt();
                _stage = stage;
                stage = null;
                State.Status.Value = "";
            }
            catch (Exception error)
            {
                await LoadingWork.NextFrame;
                if (!_disposed)
                {
                    Image = null;
                    _target?.Dispose();
                    _target = null;
                    State.Status.Value = error is OperationCanceledException
                        ? "The stage preview timed out."
                        : $"Could not load the stage preview. {error.Message}";
                }
            }
            finally
            {
                stage?.Dispose();
                if (!_disposed) State.Busy.Value = false;
            }
        }

        public void Tick()
        {
            if (_stage is not { } stage) return;
            stage.OnBeforeGameTick();
            stage.GameTick();
            _flyover.AroundStage(_camera, stage.Backend);
            if (State.DescriptionLines.Value.Count > 0)
                State.DescriptionFloat.Value = MathF.Sin(++_floatTick * 0.03f) * 3f;
        }

        public void Render(ICommandBuffer cb, float alpha)
        {
            if (_stage is not { } stage || _target is not { } target) return;
            var swapchain = GameSparker.GraphicsDevice.Swapchain;
            try
            {
                cb.SetRenderTarget(target);
                cb.SetViewport(new Viewport(0, 0, Width, Height));
                cb.SetScissorRect(new ScissorRect(0, 0, Width, Height));
                stage.Render(cb, alpha, useShadowMapping: false);
            }
            finally
            {
                cb.SetRenderTarget(null);
                cb.SetViewport(new Viewport(0, 0, swapchain.Width, swapchain.Height));
                GameSparker.Game.SetFullScreenScissor(cb);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
            _stage?.Dispose();
            Image = null;
            _target?.Dispose();
        }
    }
}
