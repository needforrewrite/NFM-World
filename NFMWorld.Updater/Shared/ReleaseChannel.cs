using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFMWorld.Updates;

internal static class ReleaseChannel
{
    internal const string Latest = "https://api.github.com/repos/needforrewrite/NFM-World/releases/latest";
    private const string Downloads = "https://github.com/needforrewrite/NFM-World/releases/download/";
    internal const long MaxArchive = 2L << 30;

    internal static Version? ParseTag(string? tag)
    {
        if (tag is null || tag.Length is < 6 or > 18 || tag[0] != 'v') return null;
        var parts = tag[1..].Split('.');
        if (parts.Length != 3 || parts.Any(p => p.Length is < 1 or > 5 ||
            (p.Length > 1 && p[0] == '0') || p.Any(c => !char.IsAsciiDigit(c)) || int.Parse(p) > 65534)) return null;
        return new Version(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    internal static string? AssetName(string rid) => rid switch
    {
        "win-x64" => "nfm-world-win-x64.zip",
        "linux-x64" or "osx-x64" or "osx-arm64" => $"nfm-world-{rid}.tar.gz",
        _ => null
    };

    internal static string? ProcessRid => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
        Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
        Architecture.X64 when OperatingSystem.IsMacOS() => "osx-x64",
        Architecture.Arm64 when OperatingSystem.IsMacOS() => "osx-arm64",
        _ => null
    };

    internal static HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders = { UserAgent = { new("NFMWorld-Updater", "1") } }
    };

    internal static async Task<ReleaseCandidate?> CheckAsync(HttpClient client, string current, string rid, CancellationToken ct)
    {
        var installed = ParseTag(current) ?? throw new InvalidDataException("Invalid installed release tag.");
        var name = AssetName(rid) ?? throw new InvalidDataException("Unsupported desktop platform.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var result = await client.GetAsync(Latest, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (result.StatusCode == HttpStatusCode.NotFound) return null;
        result.EnsureSuccessStatusCode();
        const int limit = 1 << 20;
        if (result.Content.Headers.ContentLength > limit) throw new InvalidDataException("Release response is too large.");
        await using var body = await result.Content.ReadAsStreamAsync(deadline.Token);
        var bytes = new byte[limit + 1];
        var length = await body.ReadAtLeastAsync(bytes, bytes.Length, false, deadline.Token);
        if (length > limit) throw new InvalidDataException("Release response is too large.");
        var release = JsonSerializer.Deserialize(bytes.AsSpan(0, length), UpdateJson.Default.GitHubRelease)
                      ?? throw new InvalidDataException("Missing release metadata.");
        if (release.Draft is null || release.Prerelease is null || release.Tag is null)
            throw new InvalidDataException("Incomplete release metadata.");
        if (release.Draft.Value || release.Prerelease.Value || ParseTag(release.Tag) is not { } latest || latest <= installed)
            return null;
        var matches = release.Assets?.Where(a => a?.Name == name).ToArray();
        var url = Downloads + release.Tag + "/" + name;
        if (matches is not [ { State: "uploaded", Size: > 0 and <= MaxArchive, Digest: { } digest } asset] ||
            asset.Url != url || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !IsHash(digest.AsSpan(7)))
            throw new InvalidDataException("This release has no verified download for your platform.");
        return new ReleaseCandidate(release.Tag, new Uri(url), asset.Size, digest[7..].ToLowerInvariant());
    }

    internal static bool IsHash(ReadOnlySpan<char> hash) => hash.Length == 64 && hash.IndexOfAnyExcept("0123456789abcdefABCDEF")
                                                            < 0;
}

internal sealed record ReleaseCandidate(string Tag, Uri Url, long Size, string Hash);

internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? Tag,
    bool? Draft,
    bool? Prerelease,
    GitHubAsset?[]? Assets);

internal sealed record GitHubAsset(
    string? Name,
    string? State,
    long Size,
    string? Digest,
    [property: JsonPropertyName("browser_download_url")] string? Url);

internal sealed record InstallManifest(int Format, string Product, string Tag, string Rid, Dictionary<string, string> Files);

internal sealed record UpdateJournal(string OldTag, string NewTag, JournalEntry[] Entries);

internal sealed record JournalEntry(string Path, bool HadOriginal);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(InstallManifest))]
[JsonSerializable(typeof(UpdateJournal))]
internal sealed partial class UpdateJson : JsonSerializerContext;
