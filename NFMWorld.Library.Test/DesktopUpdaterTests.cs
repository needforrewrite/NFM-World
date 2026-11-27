using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using NFMWorld.Updates;

namespace NFMWorld.Library.Test;

[TestClass]
public class DesktopUpdaterTests
{
    private const string Old = "v1.9.0", New = "v1.10.0", Rid = "win-x64";

    [TestMethod]
    public async Task VerifiedArchiveChangesNothingUntilCommitAndPreservesUserFiles()
    {
        using var fixture = new Fixture();
        var archive = fixture.Archive();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pipe = new CommandPipe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var never = new TaskCompletionSource();
        var update = HelperProgram.AwaitCommit(
            pipe,
            s => { if (s == "READY") ready.SetResult(); },
            ct => PackageStaging.Extract(archive, fixture.Staged, New, Rid, ct),
            never.Task,
            timeout.Token
        );
        await ready.Task.WaitAsync(timeout.Token);
        Assert.AreEqual("previous plugin", fixture.Read("data/gamemodes/plugin.dll"));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, InstallLayout.Workspace)));
        pipe.Send("COMMIT");
        var staged = await update;
        Assert.IsNotNull(staged);
        fixture.Apply(staged);
        Assert.AreEqual("next plugin", fixture.Read("data/gamemodes/plugin.dll"));
        Assert.AreEqual("next binary", fixture.Read("NFMWorld.exe"));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "obsolete.txt")));
        Assert.AreEqual("locally edited", fixture.Read("edited.txt"));
        Assert.AreEqual("settings", fixture.Read("data/cfg/config.cfg"));
        Assert.AreEqual("custom mod", fixture.Read("mods/custom.txt"));
        Assert.AreEqual(New, InstallLayout.VerifyInstall(fixture.Root, New, Rid).Tag);
    }

    [TestMethod]
    public async Task RollbackRestoresReplacedAndDeletedFiles()
    {
        using var fixture = new Fixture();
        var next = await PackageStaging.Extract(fixture.Archive(), fixture.Staged, New, Rid, default);
        Assert.ThrowsExactly<IOException>(() => fixture.Apply(next, path =>
            {
                if (path == InstallLayout.Manifest) throw new IOException("Injected write failure");
            }
        )
        );
        Assert.AreEqual("previous plugin", fixture.Read("data/gamemodes/plugin.dll"));
        Assert.AreEqual("old binary", fixture.Read("NFMWorld.exe"));
        Assert.AreEqual("obsolete", fixture.Read("obsolete.txt"));
        Assert.AreEqual("settings", fixture.Read("data/cfg/config.cfg"));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "new/fresh.txt")));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, "new")));
        Assert.AreEqual(Old, InstallLayout.VerifyInstall(fixture.Root, Old, Rid).Tag);
    }

    [TestMethod]
    public async Task NonGameRootsFailBeforePreparingAnUpdate()
    {
        using var fixture = new Fixture();
        var unrelated = Directory.CreateDirectory(Path.Combine(fixture.Staged, "unrelated")).FullName;
        File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "keep");
        File.WriteAllText(Path.Combine(unrelated, "NFMWorld.exe"), "not the game");
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => Task.Run(() => InstallLayout.VerifyInstall(unrelated, Old, Rid))
        );
        CollectionAssert.AreEquivalent(
            new[] { "NFMWorld.exe", "keep.txt" },
            Directory.GetFiles(unrelated).Select(Path.GetFileName).ToArray()
        );
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TraversalIsRejectedInBothArchiveFormats(bool tar)
    {
        using var fixture = new Fixture();
        string archive;
        if (tar)
        {
            archive = Path.Combine(fixture.Root, "unsafe.tar.gz");
            using var gzip = new GZipStream(File.Create(archive), CompressionMode.Compress);
            using var writer = new TarWriter(gzip);
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "../escaped.txt") { DataStream = new MemoryStream([1]) });
        }
        else archive = fixture.Archive("../escaped.txt");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => PackageStaging.Extract(archive, fixture.Staged, New, tar ? "linux-x64" : Rid, default)
        );
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Staged, "../escaped.txt")));
        Assert.AreEqual("old binary", fixture.Read("NFMWorld.exe"));
    }

    [TestMethod]
    public async Task UnmanagedConflictDoesNotChangeInstallation()
    {
        using var fixture = new Fixture();
        fixture.Write("new/fresh.txt", "a user's file");
        var next = await PackageStaging.Extract(fixture.Archive(), fixture.Staged, New, Rid, default);
        Assert.ThrowsExactly<InvalidDataException>(() => fixture.Apply(next));
        Assert.AreEqual("a user's file", fixture.Read("new/fresh.txt"));
        Assert.AreEqual("old binary", fixture.Read("NFMWorld.exe"));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, InstallLayout.Workspace)));
    }

    [TestMethod]
    public async Task LinkedDirectoryDoesNotRedirectWrites()
    {
        using var fixture = new Fixture();
        var next = await PackageStaging.Extract(fixture.Archive(), fixture.Staged, New, Rid, default);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(fixture.Root, "new"), fixture.Staged);
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows account cannot create symbolic links.");
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows account cannot create symbolic links.");
        }

        Assert.ThrowsExactly<InvalidDataException>(() => fixture.Apply(next));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Staged, "fresh.txt")));
        Assert.AreEqual("old binary", fixture.Read("NFMWorld.exe"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("CANCEL")]
    public async Task CancelOrEofAfterReadyDoesNotAuthorizeWrites(string? command)
    {
        using var pipe = new CommandPipe();
        var never = new TaskCompletionSource();
        var prepared = await HelperProgram.AwaitCommit(
            pipe,
            status => { if (status == "READY") pipe.Send(command); },
            _ => Task.FromResult("staged"),
            never.Task,
            default
        );
        Assert.IsNull(prepared);
    }

    [TestMethod]
    [DataRow(New, false, false, Old, true)]
    [DataRow(New, true, false, Old, false)]
    [DataRow(New, false, true, Old, false)]
    [DataRow("release-123-aabbccdd", false, false, Old, false)]
    [DataRow(New, false, false, New, false)]
    [DataRow(New, false, false, "v2.0.0", false)]
    public async Task StableChannelComparesNumbersAndRefusesOtherReleases(
        string tag,
        bool draft,
        bool prerelease,
        string current,
        bool accepted
    )
    {
        using var client = ReleaseClient(tag, draft, prerelease);
        Assert.AreEqual(accepted, await ReleaseChannel.CheckAsync(client, current, Rid, default) is not null);
    }

    [TestMethod]
    public async Task WrongRidAndBadDigestCannotAuthorizeDownload()
    {
        using var client = ReleaseClient(New, false, false);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ReleaseChannel.CheckAsync(client, Old, "linux-x64", default));
        var candidate = await ReleaseChannel.CheckAsync(client, Old, Rid, default);
        Assert.IsNotNull(candidate);
        using var fixture = new Fixture();
        using var download = new HttpClient(new Handler(_ => new ByteArrayContent([1])));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => PackageStaging.Download(
            download,
            candidate,
            Path.Combine(fixture.Root, "test-asset"),
            _ => { },
            default
        )
        );
        Assert.AreEqual("old binary", fixture.Read("NFMWorld.exe"));
    }

    private static HttpClient ReleaseClient(string tag, bool draft, bool prerelease) => new(new Handler(_ => new StringContent(
        JsonSerializer.Serialize(
            new GitHubRelease(tag, draft, prerelease, [new(
                ReleaseChannel.AssetName(Rid),
                "uploaded",
                1,
            "sha256:" + new string('0', 64),
                $"https://github.com/needforrewrite/NFM-World/releases/download/{tag}/{ReleaseChannel.AssetName(Rid)}"
            )]
            ),
            UpdateJson.Default.GitHubRelease
        )
    )
    )
    );

    private sealed class Handler(Func<HttpRequestMessage, HttpContent> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = respond(request) });
    }

    private sealed class CommandPipe : TextReader
    {
        private readonly TaskCompletionSource<string?> _command = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Send(string? command) => _command.SetResult(command);
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => new(
            _command.Task.WaitAsync(cancellationToken)
        );
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("nfmw-test-");
        internal string Root { get; }
        internal string Staged => Path.Combine(_temp.FullName, "stage");
        private readonly InstallManifest _old;

        internal Fixture()
        {
            Root = Directory.CreateDirectory(Path.Combine(_temp.FullName, "install")).FullName;
            var previous = new Dictionary<string, string>
            {
                ["NFMWorld.exe"] = "old binary", ["NFMWorld.Updater.exe"] = "helper",
                [InstallLayout.Logo] = "logo", ["data/gamemodes/plugin.dll"] = "previous plugin", ["obsolete.txt"] = "obsolete",
                ["edited.txt"] = "stock"
            };
            foreach (var (path, text) in previous) Write(path, text);
            _old = Manifest(Old, previous);
            Write(InstallLayout.Manifest, JsonSerializer.Serialize(_old, UpdateJson.Default.InstallManifest));
            Write("edited.txt", "locally edited");
            Write("data/cfg/config.cfg", "settings");
            Write("mods/custom.txt", "custom mod");
        }

        internal string Archive(string? extra = null)
        {
            var next = new Dictionary<string, string>
            {
                ["NFMWorld.exe"] = "next binary", ["NFMWorld.Updater.exe"] = "helper",
                [InstallLayout.Logo] = "logo", ["data/gamemodes/plugin.dll"] = "next plugin", ["new/fresh.txt"] = "fresh"
            };
            next.Add(InstallLayout.Manifest, JsonSerializer.Serialize(Manifest(New, next), UpdateJson.Default.InstallManifest));
            if (extra is not null) next.Add(extra, "bad");
            var archive = Path.Combine(_temp.FullName, "asset.zip");
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
            foreach (var (path, text) in next)
            {
                using var output = zip.CreateEntry(path).Open();
                output.Write(Encoding.UTF8.GetBytes(text));
            }

            return archive;
        }

        private static InstallManifest Manifest(string tag, Dictionary<string, string> paths) => new(
            1,
            "NFMWorld",
            tag,
            Rid,
            paths.ToDictionary(
                p => p.Key,
                p => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(p.Value)))
            )
        );

        internal void Write(string path, string text)
        {
            path = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        internal string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
        internal void Apply(InstallManifest next, Action<string>? fault = null) => InstallTransaction.Apply(
            Root,
            Staged,
            _old,
            next,
            _ => { },
            fault
        );

        public void Dispose() => _temp.Delete(recursive: true);
    }
}
