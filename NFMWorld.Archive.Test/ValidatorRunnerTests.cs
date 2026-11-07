using Microsoft.Extensions.Logging.Abstractions;
using NFMWorld.Archive;
using NFMWorld.Archive.Validation;

namespace NFMWorld.Archive.Test;

[TestClass]
public class ValidatorRunnerTests
{
    private static ValidatorRunner Runner(string? path, TimeSpan? timeout = null) => new(
        new ArchiveOptions { ValidatorPath = path, ValidationTimeout = timeout ?? TimeSpan.FromSeconds(5) },
        NullLogger<ValidatorRunner>.Instance);

    [TestMethod]
    public async Task Run_WithoutAConfiguredPath_Refuses()
    {
        var result = await Runner(null).RunAsync("radpack", ["x"], CancellationToken.None);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual("Validator is not configured on this server.", result.Error);
    }

    [TestMethod]
    public async Task Run_ReportsAValidatorThatExitsNonZero()
    {
        // A path that exists but is not the validator: it starts and exits with a failure.
        var exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/false";
        if (!File.Exists(exe) && !OperatingSystem.IsWindows())
            Assert.Inconclusive("No failure binary available.");

        var runner = new ValidatorRunner(
            new ArchiveOptions { ValidatorPath = exe, ValidationTimeout = TimeSpan.FromSeconds(5) },
            NullLogger<ValidatorRunner>.Instance);

        var result = await runner.RunAsync("radpack", ["C:\\" + "exit /b 1"], CancellationToken.None);
        Assert.IsFalse(result.Ok);
    }

    [TestMethod]
    public async Task Run_KillsAValidatorThatOverrunsItsWallClock()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses a Windows command for the hang.");

        // ping without a count blocks; the runner must kill it rather than wait.
        var runner = new ValidatorRunner(
            new ArchiveOptions
            {
                ValidatorPath = Path.Combine(Environment.SystemDirectory, "ping.exe"),
                ValidationTimeout = TimeSpan.FromMilliseconds(300)
            },
            NullLogger<ValidatorRunner>.Instance);

        var result = await runner.RunAsync("radpack", ["-n", "60", "127.0.0.1"], CancellationToken.None);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual("Validation timed out.", result.Error);
    }
}
