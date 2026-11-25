using System.Diagnostics;
using System.Runtime.InteropServices;
using NFMWorld.Sentry;

namespace NFMWorld.CrashReporter;

public static class CrashReportLibrary
{
    public static void Hook(string dsn, string release)
    {
        // Native first: an access violation inside the renderer never reaches the managed
        // handler below, so this is the only thing that runs for one.
        CrashDump.Hook();

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            CrashDump.WriteSummary(ex, release);
            Report(dsn, release, ex);
        };
        TaskScheduler.UnobservedTaskException += (sender, args) => Report(dsn, release, args.Exception);
    }

    /// <summary>
    /// Captures to Sentry and starts the reporter where one can exist. A handler that throws
    /// takes the runtime down with it - an unobserved task exception is normally survivable, and
    /// the browser cannot start a process at all - so nothing here is allowed to escape.
    /// </summary>
    private static void Report(string dsn, string release, Exception ex)
    {
        try
        {
            var eventId = SentrySdk.CaptureException(ex);
            using var process = StartReporter(dsn, release, eventId, ex.GetType().FullName ?? ex.GetType().Name);

            SentrySdk.Flush(TimeSpan.FromSeconds(60));

            process?.WaitForExit();
        }
        catch (Exception reporterFailure)
        {
            // Reporting never gets to be the reason the process dies.
            Console.Error.WriteLine($"could not report the crash: {reporterFailure.Message}");
        }
    }

    /// <summary>
    /// The reporter is a sibling of the game's own binary, not a PATH entry: it is published
    /// beside it, and the working directory at crash time is wherever the player launched from.
    /// A browser has no processes to start, so it reports through Sentry alone.
    /// </summary>
    private static Process? StartReporter(string dsn, string release, SentryEventId eventId, string errorInfo)
    {
        if (OperatingSystem.IsBrowser())
            return null;

        return Process.Start(
            new ProcessStartInfo
            {
                FileName = Path.Combine(
                    AppContext.BaseDirectory,
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "NFMWorld.CrashReporter.exe"
                        : "NFMWorld.CrashReporter"
                ),
                ArgumentList = { dsn, release, eventId.ToString(), errorInfo }
            }
        );
    }
}
