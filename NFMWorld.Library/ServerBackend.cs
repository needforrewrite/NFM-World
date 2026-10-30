using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary;

public class ServerBackend : IBackend
{
    public float Scale { get; set; } = 1f;
    public Vector2 Viewport { get; } = Vector2.Zero;

    public IRadicalMusic LoadMusic(string file, double tempomul) => ClientServer.AccidentallyCalledClientMethodOnServer<IRadicalMusic>();

    public IImage LoadImage(string file) => ClientServer.AccidentallyCalledClientMethodOnServer<IImage>();

    public IImage LoadCachedImage(string file) => ClientServer.AccidentallyCalledClientMethodOnServer<IImage>();

    public IImage LoadImage(ReadOnlySpan<byte> file) => ClientServer.AccidentallyCalledClientMethodOnServer<IImage>();

    public void StopAllSounds() => ClientServer.AccidentallyCalledClientMethodOnServer();

    public ISoundClip GetSound(string filePath) => ClientServer.AccidentallyCalledClientMethodOnServer<ISoundClip>();

    public IGraphics Graphics => ClientServer.AccidentallyCalledClientMethodOnServer<IGraphics>();

    public IGraphics3D Graphics3D => ClientServer.AccidentallyCalledClientMethodOnServer<IGraphics3D>();
    
    public void SetAllVolumes(float vol) => ClientServer.AccidentallyCalledClientMethodOnServer();
    
    public Key GetKeyFromScancode(Key key) => ClientServer.AccidentallyCalledClientMethodOnServer<Key>();
}