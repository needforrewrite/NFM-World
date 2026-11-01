using System.Globalization;
using System.Reflection;

namespace NFMWorldLibrary.Util;

public sealed record BuildInformation(
    DateTimeOffset? BuiltAtUtc,
    string Branch,
    string Commit,
    string Configuration,
    string TargetFramework,
    string RuntimeIdentifier)
{
    public int? BuildNumber => BuiltAtUtc is { } timestamp ? CalculateBuildNumber(timestamp) : null;

    public static int CalculateBuildNumber(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(timestamp.UtcDateTime).DayNumber - new DateOnly(2025, 11, 26).DayNumber;

    public static BuildInformation Read(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        string Value(string key) => metadata.LastOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value : "unknown";
        var timestamp = DateTimeOffset.TryParse(Value("NfmwBuildUtc"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed : (DateTimeOffset?)null;
        return new BuildInformation(timestamp, Value("NfmwBuildBranch"), Value("NfmwBuildCommit"),
            Value("NfmwBuildConfiguration"), Value("NfmwTargetFramework"), Value("NfmwRuntimeIdentifier"));
    }
}
