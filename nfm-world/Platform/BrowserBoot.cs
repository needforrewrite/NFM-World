// LLM maintained.
//
// What replaces the filesystem in the browser before the game mounts anything.
//
// BackendGameSparker.Load mounts a directory for the app's own data, and Directory.GetCurrentDirectory
// and friends return MEMFS paths that are empty here - there is no install directory in a page.
// So on browser the assets come from a zip fetched from the site instead, mounted through the
// same IVirtualFileSystem seam the game already reads through, and the write destination moves
// into MEMFS so config and saved time trials still work (in memory; a refresh clears them).
//
// The caller decides which files are in the archive - see the publish target in
// NFMWorld.Browser.csproj. Music is deliberately separate: it is ~90% of the data tree and is
// fetched per track rather than up front.

#nullable enable
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;

namespace NFMWorld.Platform;

/// <summary>Browser-only data mounting, called from the browser head before the game boots.</summary>
public static class BrowserBoot
{
    /// <summary>
    /// Where writable state goes. MEMFS, so it behaves like a disk for the duration of the page
    /// and disappears with it; the game's config and time-trial code paths need no changes.
    /// </summary>
    private const string WriteRoot = "/tmp/nfmw";

    /// <summary>
    /// Mounts the boot archive's entries into the game's VFS and points writes at MEMFS.
    /// </summary>
    /// <param name="zipBytes">
    /// The whole boot archive. Fetched by the caller, which is also what lets a failing download
    /// surface as a page-level error rather than a partially mounted filesystem.
    /// </param>
    public static void Mount(byte[] zipBytes)
    {
        // MountZip(Memory) rather than a path: the archive only ever exists as bytes in memory.
        TheVFS.VFS.MountZip(zipBytes);

        TheVFS.VFS.MountWriteDestination(WriteRoot);
    }

    /// <summary>
    /// Installs the music mount: entries served from the music archive over the network, on demand.
    /// The read is synchronous because the browser's range fetch is (see http.js) - the game is
    /// paused while a track loads, so blocking here costs a frame rather than a hang.
    /// </summary>
    /// <param name="readEntry">Reads one entry of the music archive, or returns null when absent.</param>
    public static void MountMusic(Func<string, byte[]?> readEntry)
    {
        TheVFS.VFS.Mount(new FetchFileSystem(readEntry));
    }

    /// <summary>
    /// Whether the state written by <see cref="Mount"/> can be expected to survive a reload.
    /// False here: MEMFS is per-page. Exposed so a caller can say so rather than promise more.
    /// </summary>
    public static bool IsWritePersistent => false;
}
