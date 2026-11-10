using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NFMWorld.CrashReporter;

/// <summary>
/// Writes a minidump when the process dies in a way the managed handler cannot see. An access
/// violation inside native code, or a runtime abort, ends the process before
/// <see cref="CrashReportLibrary"/>'s handlers run, so nothing was captured for those at all.
/// </summary>
public static class CrashDump
{
    private const int MiniDumpWithFullMemory = 0x00000002;

    private const int ExceptionContinuingSearch = 0;
    private const int ExceptionExecuteHandler = 1;

    private static bool _hooked;

    /// <summary>
    /// Held for the process lifetime: Windows keeps only the function pointer, so a collected
    /// delegate would leave the filter pointing at freed memory.
    /// </summary>
    private static TopLevelExceptionFilter? _filter;

    /// <summary>
    /// Where dumps land. The install directory is not writable for many users, and a dump of
    /// this process is tens of megabytes.
    /// </summary>
    public static string Folder { get; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "NFMWorld",
        "crashes"
    );

    /// <summary>
    /// Arms the native filter. Crashes of this process are fatal anyway, so this only decides
    /// whether a dump exists afterwards.
    /// </summary>
    public static void Hook()
    {
        if (_hooked || !OperatingSystem.IsWindows())
            return;

        // The game relaunches itself with this set, which makes the runtime's own createdump
        // write the dump - including for the fail-fasts this filter cannot see. Writing one here
        // as well would just duplicate it.
        if (System.Environment.GetEnvironmentVariable("DOTNET_DbgEnableMiniDump") == "1")
            return;

        _hooked = true;
        _filter = (_, _, _, _) =>
        {
            // The runtime's own filter and every registered handler get first refusal; this
            // only writes when nothing has handled it and the process is about to die.
            Write("crash");
            return ExceptionContinuingSearch;
        };
        SetUnhandledExceptionFilter(_filter);
    }

    /// <summary>Writes a minidump and a text summary beside it.</summary>
    public static void Write(string reason)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Folder);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

            using (var file = File.Create(Path.Combine(Folder, $"{stamp}-{reason}.dmp")))
            {
                MiniDumpWriteDump(
                    Process.GetCurrentProcess().Handle,
                    (uint)System.Environment.ProcessId,
                    file.SafeFileHandle.DangerousGetHandle(),
                    MiniDumpWithFullMemory,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero
                );
            }

            Console.Error.WriteLine($"wrote crash dump to {Folder}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"could not write a crash dump: {ex.Message}");
        }
    }

    /// <summary>A readable summary, for a dump that cannot be symbolicated on the user's machine.</summary>
    public static void WriteSummary(Exception ex, string? release = null)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Folder);
            File.WriteAllText(
                Path.Combine(Folder, $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}-crash.txt"),
                $"{release}\n{DateTimeOffset.UtcNow:O}\n\n{ex}"
            );
        }
        catch
        {
            // The process is going down either way.
        }
    }

    private delegate int TopLevelExceptionFilter(
        IntPtr exceptionPointers,
        IntPtr exceptionInfo,
        IntPtr context,
        IntPtr dispatcher
    );

    [DllImport("kernel32.dll")]
    private static extern IntPtr SetUnhandledExceptionFilter(TopLevelExceptionFilter filter);

    [DllImport("Dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr process,
        uint processId,
        IntPtr file,
        int dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam
    );
}
