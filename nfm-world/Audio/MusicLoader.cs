using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;

namespace NFMWorld.Audio;

internal static class MusicLoader
{
    internal static async Task<IRadicalMusic> LoadAsync(string path, double tempo, byte[]? embedded = null)
    {
        using var loading = LoadingWork.Begin("Loading music...");
        if (!OperatingSystem.IsBrowser())
            return await Task.Run<IRadicalMusic>(() => FaudioMusic.Load(path, tempo, embedded));

        await LoadingWork.NextFrame;
        using var stream = embedded is not null ? new MemoryStream(embedded, writable: false) : TheVFS.VFS.OpenRead(path);
        return await WebAudioMusic.LoadAsync(stream, Path.GetExtension(path), tempo);
    }
}
