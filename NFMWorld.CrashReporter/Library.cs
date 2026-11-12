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
            var eventId = SentrySdk.CaptureException(ex);

            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "NFMWorld.CrashReporter.exe"
                        : "NFMWorld.CrashReporter",
                    ArgumentList =
                    {
                        dsn,
                        release,
                        eventId.ToString(),
                        ex.GetType().FullName ?? ex.GetType().Name
                    }
                }
            );

            SentrySdk.Flush(TimeSpan.FromSeconds(60));

            process?.WaitForExit();
        };
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var ex = args.Exception;
            var eventId = SentrySdk.CaptureException(ex);

            using var process = Process.Start(
                new ProcessStartInfo()
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "NFMWorld.CrashReporter.exe"
                        : "NFMWorld.CrashReporter",
                    ArgumentList =
                    {
                        dsn,
                        release,
                        eventId.ToString(),
                        ex.GetType().FullName ?? ex.GetType().Name
                    }
                }
            );

            SentrySdk.Flush(TimeSpan.FromSeconds(60));

            process?.WaitForExit();
        };
    }
}
