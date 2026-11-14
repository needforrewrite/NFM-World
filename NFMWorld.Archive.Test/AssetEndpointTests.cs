using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive;
using NFMWorld.Archive.Data;
using NFMWorldLibrary;
using NFMWorldLibrary.Radpack;

namespace NFMWorld.Archive.Test;

/// <summary>
/// Asset browsing: the display metadata, the keyset paging, the single visibility rule, and the
/// shared kind mapping that makes an uploaded stage a <c>Stage</c> rather than a <c>Track</c>.
/// </summary>
[TestClass]
public class AssetEndpointTests
{
    private static string Hash(char c) => new(c, 64);

    private static async Task SeedAsync(ArchiveAppFactory factory, params Asset[] assets)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        db.Users.Add(new User { Username = "artist", UsernameNormalized = "artist" });
        await db.SaveChangesAsync();

        var owner = db.Users.Single();
        foreach (var asset in assets)
        {
            asset.OwnerId ??= owner.Id;
            db.Assets.Add(asset);
        }

        await db.SaveChangesAsync();
    }

    private static Asset NewAsset(string name, AssetType type, string hash, bool isPublic = true, bool takedown = false)
        => new()
        {
            Type = type,
            Name = name,
            ContentHash = hash,
            StoragePath = $"{hash[..2]}/{hash}",
            SizeBytes = 10,
            IsPublic = isPublic,
            IsTakedown = takedown,
            IsValidated = true
        };

    [TestMethod]
    public async Task StockCleanup_RemovesSeededRowsAndFiles_AndPreservesUserUploads()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        string stockHash;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            var stored = await storage.PutAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
            stockHash = stored.Hash;
            db.Assets.Add(new Asset
            {
                Name = "nfm2/1_introductory", Type = AssetType.Stage, OwnerId = null,
                Description = StockAssetCleanup.LegacyDescription, ContentHash = stockHash,
                StoragePath = stored.RelativePath, SizeBytes = 3, IsPublic = true, IsValidated = true
            });
            var uploaded = NewAsset("Custom stage", AssetType.Stage, Hash('b'));
            uploaded.Owner = new User { Username = "artist", UsernameNormalized = "artist" };
            uploaded.Description = StockAssetCleanup.LegacyDescription;
            db.Assets.Add(uploaded);
            await db.SaveChangesAsync();
            Assert.AreEqual(1, await StockAssetCleanup.RunAsync(
                db,
                storage,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                CancellationToken.None
            )
            );
            Assert.IsFalse(storage.Exists(stored.RelativePath));
            Assert.AreEqual(1, db.Assets.Count());
        }

        var search = await client.GetFromJsonAsync<JsonElement>("/assets/search?type=Stage");
        var items = search.GetProperty("items");
        Assert.AreEqual(1, items.GetArrayLength());
        Assert.AreEqual("Custom stage", items[0].GetProperty("name").GetString());
        var html = await client.GetStringAsync("/browse");
        StringAssert.DoesNotContain(html, "nfm2/1_introductory");
        StringAssert.Contains(html, "Custom stage");

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/assets/meta/" + stockHash)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/assets/" + stockHash)).StatusCode);
    }

    [TestMethod]
    public async Task StockCleanup_PreservesSharedFilesAndReplayRecords()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
        var shared = await storage.PutAsync(new byte[] { 4, 5, 6 }, CancellationToken.None);
        var replay = await storage.PutAsync(new byte[] { 7, 8, 9 }, CancellationToken.None);
        var owner = new User { Username = "artist", UsernameNormalized = "artist" };
        var upload = NewAsset("User upload", AssetType.Stage, shared.Hash);
        upload.Owner = owner;
        upload.StoragePath = shared.RelativePath;
        db.Assets.Add(upload);
        foreach (var stored in new[] { shared, replay })
        {
            var stock = NewAsset("nfm2/test", AssetType.Stage, stored.Hash);
            stock.StoragePath = stored.RelativePath;
            stock.Description = StockAssetCleanup.LegacyDescription;
            db.Assets.Add(stock);
        }

        db.TimeTrials.Add(new TimeTrial
        {
            User = owner, StageHash = shared.Hash, CarHash = Hash('c'),
            StageName = "nfm2/test", CarName = "nfmm/test", StoragePath = replay.RelativePath
        });
        await db.SaveChangesAsync();

        Assert.AreEqual(2, await StockAssetCleanup.RunAsync(
            db,
            storage,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            CancellationToken.None
        )
        );
        Assert.IsTrue(storage.Exists(shared.RelativePath));
        Assert.IsTrue(storage.Exists(replay.RelativePath));
        Assert.AreEqual(1, db.Assets.Count());
        Assert.AreEqual(1, db.TimeTrials.Count());
    }

    [TestMethod]
    public async Task OlderUploads_AreLinkedOnTheAccountPage_AndReachableThroughBrowsePaging()
    {
        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);
        int cursor;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var owner = db.Users.Single();
            var first = NewAsset("My first published stage", AssetType.Stage, Hash('a'));
            var second = NewAsset("My second published stage", AssetType.Stage, Hash('b'));
            first.OwnerId = second.OwnerId = owner.Id;
            db.Assets.AddRange(first, second);
            await db.SaveChangesAsync();
            var other = new User { Username = "other", UsernameNormalized = "other" };
            for (var i = 0; i < 101; i++)
            {
                var asset = NewAsset($"Newer upload {i}", AssetType.Stage, i.ToString("x64"));
                asset.Owner = other;
                db.Assets.Add(asset);
            }

            await db.SaveChangesAsync();
            cursor = db.Assets.OrderByDescending(asset => asset.Id).Skip(99).Select(asset => asset.Id).First();
        }

        var account = await client.GetStringAsync("/");
        StringAssert.Contains(account, "My first published stage");
        StringAssert.Contains(account, "My second published stage");
        var newest = await client.GetStringAsync("/browse");
        StringAssert.Contains(newest, "Older uploads");
        var older = await client.GetStringAsync($"/browse?after={cursor}");
        StringAssert.Contains(older, "My first published stage");
        StringAssert.Contains(older, "My second published stage");
    }

    [TestMethod]
    public async Task Search_PagesWithoutDuplicatesAndReportsAStableCursor()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await SeedAsync(
            factory,
            NewAsset("one", AssetType.Car, Hash('1')),
            NewAsset("two", AssetType.Car, Hash('2')),
            NewAsset("three", AssetType.Car, Hash('3')),
            NewAsset("four", AssetType.Stage, Hash('4')),
            NewAsset("five", AssetType.Stage, Hash('5'))
        );

        var seen = new List<int>();
        int? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var url = "/assets/search?limit=2" + (cursor is null ? "" : $"&after={cursor}");
            var body = await client.GetFromJsonAsync<JsonElement>(url);
            foreach (var item in body.GetProperty("items").EnumerateArray())
                seen.Add(item.GetProperty("id").GetInt32());

            if (body.GetProperty("nextCursor").ValueKind == JsonValueKind.Null)
                break;
            cursor = body.GetProperty("nextCursor").GetInt32();
        }

        Assert.AreEqual(5, seen.Count);
        Assert.AreEqual(5, seen.Distinct().Count());
        CollectionAssert.AreEqual(seen.OrderByDescending(i => i).ToArray(), seen);
    }

    [TestMethod]
    public async Task Search_ExcludesPrivateAndTakenDownAssets()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await SeedAsync(
            factory,
            NewAsset("visible", AssetType.Car, Hash('a')),
            NewAsset("private", AssetType.Car, Hash('b'), isPublic: false),
            NewAsset("taken down", AssetType.Car, Hash('c'), takedown: true)
        );

        var body = await client.GetFromJsonAsync<JsonElement>("/assets/search");
        var names = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString())
            .ToArray();

        CollectionAssert.AreEqual(new[] { "visible" }, names);
    }

    [TestMethod]
    public async Task Search_RendersTheSharedKindNameAndEligibility()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, NewAsset("course", AssetType.Stage, Hash('d')));

        var body = await client.GetFromJsonAsync<JsonElement>("/assets/search");
        var item = body.GetProperty("items")[0];

        Assert.AreEqual("Stage", item.GetProperty("type").GetString());
        Assert.AreEqual("artist", item.GetProperty("author").GetString());
        Assert.IsTrue(item.GetProperty("eligible").GetBoolean());
    }

    [TestMethod]
    public async Task Search_RejectsAnUnknownType()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/assets/search?type=Nonsense");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Metadata_ByHash_ReportsKindAndEligibility()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, NewAsset("piece", AssetType.StagePiece, Hash('e')));

        var body = await client.GetFromJsonAsync<JsonElement>($"/assets/meta/{Hash('e')}");

        Assert.AreEqual("StagePiece", body.GetProperty("type").GetString());
        Assert.AreEqual(Hash('e'), body.GetProperty("contentHash").GetString());
        Assert.IsTrue(body.GetProperty("eligible").GetBoolean());
    }

    [TestMethod]
    public async Task Metadata_ForAnUnvalidatedLegacyRow_IsNotEligible()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var asset = NewAsset("legacy", AssetType.Car, Hash('f'));
        asset.IsValidated = false;
        await SeedAsync(factory, asset);

        var body = await client.GetFromJsonAsync<JsonElement>($"/assets/meta/{Hash('f')}");

        Assert.IsFalse(body.GetProperty("eligible").GetBoolean());
    }

    [TestMethod]
    public async Task Download_IsPubliclyFetchableAndCacheable()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        string relative;
        using (var scope = factory.Services.CreateScope())
            relative = (await scope.ServiceProvider.GetRequiredService<FileStorage>().PutAsync(new byte[] { 1, 2, 3 }, default))
                .RelativePath;

        var asset = NewAsset("public", AssetType.Car, Hash('0'));
        asset.StoragePath = relative;
        await SeedAsync(factory, asset);

        var response = await client.GetAsync($"/assets/{Hash('0')}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync());
        StringAssert.Contains(response.Headers.CacheControl!.ToString(), "immutable");
    }

    [TestMethod]
    public async Task Download_OfATakenDownAsset_IsNotFound()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, NewAsset("gone", AssetType.Car, Hash('9'), takedown: true));

        var response = await client.GetAsync($"/assets/{Hash('9')}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Upload_RequiresSignIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "car.radpack");
        form.Add(new StringContent("car"), "name");
        form.Add(new StringContent("Car"), "type");

        var response = await client.PostAsync("/assets/upload", form);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpClient> SignedInClient(ArchiveAppFactory factory, string username = "artist")
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration(username));
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        byte[] bytes,
        string fileName,
        string name,
        string type
    )
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent("published from a test"), "description");
        form.Add(new StringContent(type), "type");
        return await client.PostAsync("/assets/upload", form);
    }

    [TestMethod]
    public async Task Upload_WithTheRealValidator_StoresATypedStageAndReturnsItsHash()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackTrack
            {
                Metadata = new RadpackMetadata
                {
                    Name = "test course",
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Track
                },
                Stage = new StageLoader()
            },
            writer
        );
        var bytes = writer.WrittenSpan.ToArray();
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var response = await UploadAsync(client, bytes, "course.radpack", "test course", "Stage");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var asset = body.GetProperty("asset");
        Assert.AreEqual(expectedHash, asset.GetProperty("contentHash").GetString());
        Assert.AreEqual("Stage", asset.GetProperty("type").GetString());
        Assert.IsTrue(asset.GetProperty("eligible").GetBoolean());

        var meta = await client.GetFromJsonAsync<JsonElement>($"/assets/meta/{expectedHash}");
        Assert.AreEqual("Stage", meta.GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task Upload_WithTheRealValidator_AcceptsALegacyRadModel()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        var source = Path.Combine(
            Path.GetDirectoryName(ArchiveAppFactory.ValidatorPath)!,
            "data",
            "models",
            "nfmm",
            "cars",
            "2000tornados.rad"
        );
        if (!File.Exists(source))
            Assert.Inconclusive("The validator's data tree is not beside its executable.");

        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        var response = await UploadAsync(client, File.ReadAllBytes(source), "legacy.rad", "legacy car", "Car");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("Car", body.GetProperty("asset").GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task Upload_RejectsAnExecutableKind()
    {
        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        var response = await UploadAsync(client, [1, 2, 3], "mode.radpack", "mode", "Gamemode");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Upload_RejectsAnExtensionThatIsNotAModel()
    {
        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        // A plain text model has no kind of its own, so it cannot claim to be a stage; the website's
        // publish form has to refuse it for the same reason.
        var response = await UploadAsync(client, [1, 2, 3], "notes.txt", "notes", "Car");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.IsFalse(AssetKinds.IsUploadExtensionAllowed(AssetKinds.Stage, ".rad"));
        Assert.IsFalse(AssetKinds.IsUploadExtensionAllowed(AssetKinds.Texture, ".rad"));
        Assert.IsTrue(AssetKinds.IsUploadExtensionAllowed(AssetKinds.Car, ".rad"));
        Assert.IsTrue(AssetKinds.IsUploadExtensionAllowed(AssetKinds.Stage, ".radpack"));
        Assert.IsFalse(AssetKinds.IsUploadExtensionAllowed(AssetKinds.Gamemode, ".radpack"));
    }

    [TestMethod]
    public async Task LegacyStockConfiguration_DoesNotCreateArchivedAssets()
    {
        using var factory = new ArchiveAppFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Archive:StockContentPath", AppContext.BaseDirectory));
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/assets/search?limit=100&type=Car");
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.IsEmpty(items);
        using var scope = factory.Services.CreateScope();
        Assert.AreEqual(0, scope.ServiceProvider.GetRequiredService<ArchiveDbContext>().Assets.Count());
    }

    [TestMethod]
    public async Task AssetKinds_MapStageToTrackAndBack()
    {
        Assert.AreEqual(RadpackType.Track, AssetKinds.ToRadpackType(AssetKinds.Stage));
        Assert.AreEqual(AssetKinds.Stage, AssetKinds.FromRadpackType(RadpackType.Track));
        Assert.AreEqual(RadpackType.TrackPiece, AssetKinds.ToRadpackType(AssetKinds.StagePiece));
        Assert.IsNull(AssetKinds.ToRadpackType(AssetKinds.Gamemode));
        Assert.IsFalse(AssetKinds.IsPublishable(AssetKinds.Replay));
    }
}
