using System.Threading.Tasks;

namespace NFMWorld.PhysicsFS
{
    public interface IVirtualFileSystem
    {
        Stream? OpenRead(string path);
        Stream? OpenWrite(string path);
        bool FileExists(string path);
        bool DirectoryExists(string path);
        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

        /// <summary>Immediate subdirectories of <paramref name="path"/>, relative to the mount root.
        /// A default of nothing keeps the read-only mounts that cannot list directories out of the
        /// way - a mount that only answers for known paths is still a valid one.</summary>
        IEnumerable<string> EnumerateDirectories(string path) => [];

        /// <summary>
        /// Asynchronous read, for a mount whose bytes are not present yet.
        ///
        /// The browser has no synchronous HTTP, so a music file cannot be fetched inside
        /// <see cref="OpenRead"/>; the mount that serves them over the network implements this
        /// instead and refuses the synchronous call. Every other mount keeps the default, which is
        /// the synchronous read - the work is already done by the time it returns.
        /// </summary>
        Task<Stream?> OpenReadAsync(string path) => Task.FromResult(OpenRead(path));
    }
}
