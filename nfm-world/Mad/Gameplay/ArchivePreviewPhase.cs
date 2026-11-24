using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// One archive radpack, shown on its own: a track under the stage-select flyover, or a model
/// spinning under an orbit camera. The browser's preview page boots this instead of the game.
/// </summary>
/// <remarks>
/// The radpack is fetched once here and loaded from those bytes, rather than through
/// <see cref="ContentCache"/>: the cache's prepare path is bound to the baked archive endpoint,
/// and a preview page may name another host. A stage therefore goes in with an explicit
/// <see cref="StageLoader"/> instead of being re-read from the content mount.
/// </remarks>
public sealed class ArchivePreviewPhase : BaseStageRenderingPhase
{
    private const float SpinDegreesPerSecond = 22f;
    private const float OrbitSensitivity = 0.3f;
    private const float ZoomSpeed = 0.25f;
    private const float MinDistance = 100f;
    private const float MaxDistance = 10000f;

    private readonly ArchivePreviewRequest _request;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly AroundStageCamera _stageCamera = new();

    /// <summary>Only for an explicit archive host; the baked endpoint's client is shared.</summary>
    private readonly ArchiveContentClient? _ownClient;

    private Scene? _scene;
    private EditorObject? _model;
    private float _distance = 800f;
    private float _yaw;
    private float _pitch = 10f;
    private float _spin;
    private bool _orbiting;
    private int _dragX;
    private int _dragY;
    private float _dragYaw;
    private float _dragPitch;
    private bool _disposed;

    public ArchivePreviewPhase(ArchivePreviewRequest request) : base()
    {
        _request = request;
        _ownClient = request.ArchiveUrl is null ? null : new ArchiveContentClient(request.ArchiveUrl);

        // A preview shows one asset, not the frame trace or the shadow-cascade readback.
        DebugDisplay = false;

        _ = LoadAsync();
    }

    private ArchiveContentClient Client => _ownClient ?? GarageController.ContentClient;

    private async Task LoadAsync()
    {
        using var operation = LoadingWork.Begin("Loading the asset…");
        try
        {
            if (!ContentHashes.TryResolve(_request.Hash, out var hash))
            {
                await FailAsync("Could not preview the asset", "That is not a content hash.");
                return;
            }

            operation.Report("Downloading the asset…");
            var bytes = await Client.DownloadAsync(hash, RadpackLimits.MaxCompressedBytes, _cancellation.Token);
            await LoadingWork.NextFrame;
            if (_cancellation.IsCancellationRequested) return;

            var kind = HeaderKind(bytes);
            var mode = ResolveMode(kind);
            var reference = RadpackReader.ArchivePrefix + hash;

            if (mode == ArchivePreviewMode.Stage)
            {
                operation.Report("Building the stage…");
                var stage = RadpackReader.Read(
                    bytes,
                    reference,
                    RadpackType.Track,
                    RadpackLimits.MaxCompressedBytes,
                    RadpackLimits.MaxDecompressedBytes
                );
                if (stage is not RadpackStage track)
                    throw new InvalidDataException("The radpack carries no stage.");

                await LoadingWork.NextFrame;
                if (_cancellation.IsCancellationRequested) return;
                BeginStageLoad(reference, loadMusic: false, stageData: track.Stage);
                return;
            }

            operation.Report("Building the model…");
            var asset = RadpackReader.Read(
                bytes,
                reference,
                kind,
                RadpackLimits.MaxCompressedBytes,
                RadpackLimits.MaxDecompressedBytes
            );
            if (asset is not RadpackRad3d model)
                throw new InvalidDataException("The radpack carries no model.");

            await LoadingWork.NextFrame;
            if (_cancellation.IsCancellationRequested) return;
            BuildModel(model.Rad);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Our own token: the phase is going away, so there is nobody to tell.
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException
                                       or IOException or UnauthorizedAccessException)
        {
            await FailAsync("Could not preview the asset", DescribeFailure(ex));
        }
        catch (Exception ex)
        {
            await FailAsync("Could not preview the asset", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// A message a player can act on. The browser build runs without ICU, so a framework
    /// exception's own <see cref="Exception.Message"/> is its resource key
    /// (<c>net_http_message_not_success_statuscode_reason</c>) rather than a sentence.
    /// </summary>
    private static string DescribeFailure(Exception ex)
        => ex switch
        {
            HttpRequestException { StatusCode: { } status } => $"The archive answered {(int)status} {status}.",
            HttpRequestException => "Could not reach the archive.",
            TaskCanceledException => "The archive did not answer in time.",
            _ => ex.Message
        };

    private async Task FailAsync(string title, string message)
    {
        Logging.Error($"[Preview] {title}: {message}");
        await LoadingWork.NextFrame;
        if (_disposed || _cancellation.IsCancellationRequested) return;
        GameSparker.MessageWindow.ShowMessage(title, message);
    }

    /// <summary>The radpack header's declared kind; the whole format starts with it.</summary>
    private static RadpackType HeaderKind(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 'R' || bytes[1] != 'A' || bytes[2] != 'D'
            || bytes[3] > (byte)RadpackType.Max)
            throw new InvalidDataException("The download is not a radpack.");

        return (RadpackType)bytes[3];
    }

    /// <summary>Which of the two views the header kind allows and the request asked for.</summary>
    private ArchivePreviewMode ResolveMode(RadpackType kind)
    {
        var isModel = kind is RadpackType.Car or RadpackType.TrackPiece or RadpackType.Wheel;
        if (isModel && _request.Mode != ArchivePreviewMode.Stage)
            return ArchivePreviewMode.Model;

        if (kind == RadpackType.Track && _request.Mode != ArchivePreviewMode.Model)
            return ArchivePreviewMode.Stage;

        if (_request.Mode == ArchivePreviewMode.Auto)
            throw new InvalidDataException($"A {kind} radpack has nothing to show.");

        throw new InvalidDataException(
            $"This upload is a {kind}, not a {(_request.Mode == ArchivePreviewMode.Stage ? "stage" : "model")}."
        );
    }

    private void BuildModel(Rad3d rad)
    {
        // What the editors do before they draw a model: the polygons read World's environment
        // values, which no stage has set here.
        World.ResetValues();

        _model = new EditorObject(rad, showCollision: false);
        _scene = new Scene(GameSparker.GraphicsDevice, [_model], Camera, []);
        _distance = Math.Clamp(rad.MaxRadius * 2.5f, MinDistance, MaxDistance);
        UpdateModelCamera();
    }

    protected override void OnStageLoaded()
    {
        CurrentStage!.FadeFromOverride = 9999999;
        _stageCamera.Reset();
        _stageCamera.AroundStage(Camera, CurrentStage.Backend);
    }

    public override void GameTick()
    {
        if (_model is { } model)
        {
            // Snapshot the previous transform, then advance: the render interpolates between them.
            model.GameTick();
            _spin = (_spin + SpinDegreesPerSecond / Physics.TargetTps) % 360f;
            model.Rotation = new f64Euler(
                f64AngleSingle.FromDegrees((fix64)_spin),
                f64AngleSingle.ZeroAngle,
                f64AngleSingle.ZeroAngle
            );
            UpdateModelCamera();
            return;
        }

        base.GameTick();
        if (CurrentStage != null)
            _stageCamera.AroundStage(Camera, CurrentStage.Backend);
    }

    public override void Render(ICommandBuffer cb, float alpha)
    {
        if (_scene is null)
        {
            base.Render(cb, alpha);
            return;
        }

        _scene.Render(cb, alpha, useShadowMapping: false);
    }

    private void UpdateModelCamera()
    {
        var yawRad = MathUtil.DegreesToRadians(_yaw);
        var pitchRad = MathUtil.DegreesToRadians(_pitch);
        var x = _distance * (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
        var y = -_distance * (float)Math.Sin(pitchRad);
        var z = _distance * (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));

        // -z, so yaw 0 looks at the model's nose side the way the model editor does.
        Camera.PositionWithoutInterpolation = new Vector3(x, y, -z);
        Camera.LookAtWithoutInterpolation = Vector3.Zero;
    }

    public override void MousePressed(
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
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        if (_model is null || imguiWantsMouse || button != MouseButton.Secondary) return;

        _orbiting = true;
        _dragX = x;
        _dragY = y;
        _dragYaw = _yaw;
        _dragPitch = _pitch;
    }

    public override void MouseMoved(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        if (!_orbiting || _model is null) return;

        _yaw = _dragYaw + (x - _dragX) * OrbitSensitivity;
        _pitch = Math.Clamp(_dragPitch - (y - _dragY) * OrbitSensitivity, -89f, 89f);
        UpdateModelCamera();
    }

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
        if (button == MouseButton.Secondary)
            _orbiting = false;
    }

    public override void MouseScrolled(
        int x,
        int y,
        int delta,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        if (_model is null || imguiWantsMouse || delta == 0) return;

        _distance = Math.Clamp(_distance - delta * ZoomSpeed, MinDistance, MaxDistance);
        UpdateModelCamera();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _disposed) return;
        _disposed = true;

        _cancellation.Cancel();
        _cancellation.Dispose();
        _ownClient?.Dispose();
        _scene?.Dispose();
        _scene = null;
        _model?.Dispose();
        _model = null;
    }
}
