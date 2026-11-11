namespace NFMWorld.Archive.Validation;

/// <summary>
/// Finds a validator built beside this repository, so `dotnet run` works without exporting an
/// absolute path. Deployments must still configure <c>Archive:ValidatorPath</c>; discovery is
/// only consulted in Development.
/// </summary>
public static class ValidatorLocator
{
    public static string? FindBesideRepository(string contentRoot)
    {
        var executable = OperatingSystem.IsWindows()
            ? "NFMWorld.Archive.Validator.exe"
            : "NFMWorld.Archive.Validator";

        // Prefer the configuration this build used, then the other one.
        string[] configurations =
#if DEBUG
            ["Debug", "Release"];
#else
        ["Release", "Debug"];
#endif

        for (var directory = new DirectoryInfo(contentRoot); directory is not null; directory = directory.Parent)
        {
            foreach (var configuration in configurations)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "NFMWorld.Archive.Validator",
                    "bin",
                    configuration,
                    "net11.0",
                    executable
                );
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
