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
using NFMWorldLibrary.Rad;
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
            ReviewStatus = AssetReviewStatus.Legacy,
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
    public async Task Tags_BackfillAndSearchIncludeHashtagsWithoutSearchingOtherDescriptionText()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var asset = NewAsset("Coast", AssetType.Stage, Hash('a'));
        asset.Description = "Scenic #Rally #night_2 #rally #ПРИВЕТ, ordinary words";
        var hidden = NewAsset("Hidden", AssetType.Stage, Hash('b'), isPublic: false);
        hidden.Description = "#Rally";
        await SeedAsync(factory, asset, hidden);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(
                db.Database,
                "UPDATE Assets SET Tags = NULL"
            );
            await AssetTags.BackfillAsync(db, CancellationToken.None);
        }

        foreach (var query in new[] { "rally", "%23Rally", "night_2", Uri.EscapeDataString("#привет") })
        {
            var result = await client.GetFromJsonAsync<JsonElement>("/assets/search?query=" + query);
            var items = result.GetProperty("items");
            Assert.AreEqual(1, items.GetArrayLength());
            Assert.AreEqual("Coast", items[0].GetProperty("name").GetString());
            CollectionAssert.AreEqual(
                new[] { "#Rally", "#night_2", "#ПРИВЕТ" },
                items[0].GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToArray()
            );
        }

        var meta = await client.GetFromJsonAsync<JsonElement>("/assets/meta/" + Hash('a'));
        Assert.AreEqual(3, meta.GetProperty("tags").GetArrayLength());
        var noMatch = await client.GetFromJsonAsync<JsonElement>("/assets/search?query=ordinary");
        Assert.AreEqual(0, noMatch.GetProperty("items").GetArrayLength());
        var wildcard = await client.GetFromJsonAsync<JsonElement>("/assets/search?query=%25");
        Assert.AreEqual(0, wildcard.GetProperty("items").GetArrayLength());
        StringAssert.Contains(await client.GetStringAsync("/browse?query=%23Rally"), "Coast");
        StringAssert.Contains(await client.GetStringAsync("/browse?query=" + Uri.EscapeDataString("привет")), "Coast");
    }

    [TestMethod]
    public async Task AssetDetail_HashtagsLinkToBrowse_AndDescriptionIsEncoded()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var asset = NewAsset("Coast", AssetType.Stage, Hash('a'));
        asset.Description = "<script> #Rally, #車 #Rally #!";
        await SeedAsync(factory, asset);

        var page = await client.GetStringAsync($"/asset/{asset.Id}");
        StringAssert.Contains(page, "&lt;script&gt;");
        StringAssert.Contains(page, "query=%23Rally");
        StringAssert.Contains(page, "query=%23%E8%BB%8A");
        Assert.AreEqual(2, page.Split("query=%23Rally").Length - 1);
        StringAssert.Contains(await client.GetStringAsync("/browse?query=%23Rally"), "Coast");
    }

    [TestMethod]
    public void Tags_ExtractUnicodeHashtagsAndDeduplicateIgnoringCase()
    {
        Assert.AreEqual("#First #second_2 #3 #é #車 #á", AssetTags.Extract("#First #first #second_2! #3 # #é #車 #á"));
        Assert.AreEqual("", AssetTags.Extract(null));
    }

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
                ReviewStatus = AssetReviewStatus.Legacy,
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
        Assert.IsFalse(html.Contains("nfm2/1_introductory", StringComparison.Ordinal));
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
        StringAssert.Contains(response.Headers.GetValues("CDN-Cache-Control").Single(), "max-age=31536000");
    }

    [TestMethod]
    public async Task Cors_Download_AllowsACrossOriginPage()
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

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/assets/{Hash('0')}");
        request.Headers.Add("Origin", "https://example.test");
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [TestMethod]
    public async Task Cors_Preflight_AllowsGetWithRange()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, $"/assets/{Hash('0')}");
        request.Headers.Add("Origin", "https://example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "range");
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        StringAssert.Contains(response.Headers.GetValues("Access-Control-Allow-Methods").Single(), "GET");
        StringAssert.Contains(response.Headers.GetValues("Access-Control-Allow-Methods").Single(), "HEAD");
        StringAssert.Contains(response.Headers.GetValues("Access-Control-Allow-Headers").Single(), "Range");
    }

    [TestMethod]
    public async Task Cors_Preflight_AllowsSignInFromTheBrowserBuild()
    {
        // The browser build is served from its own origin, so its sign-in is a cross-origin POST
        // with a JSON body: an unauthorised preflight makes the archive unreachable from the web.
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
        request.Headers.Add("Origin", "https://play.nfmw.net");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        StringAssert.Contains(response.Headers.GetValues("Access-Control-Allow-Methods").Single(), "POST");
        StringAssert.Contains(response.Headers.GetValues("Access-Control-Allow-Headers").Single(), "Content-Type");
    }

    [TestMethod]
    public async Task Cors_AuthenticatedRequest_IsReadableCrossOrigin()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration("alice"));
        var login = await client.PostAsJsonAsync("/auth/login", new { Username = "alice", Password = "password123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Origin", "https://play.nfmw.net");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
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
        Assert.IsFalse(asset.GetProperty("eligible").GetBoolean());
        Assert.AreEqual("Pending", asset.GetProperty("reviewStatus").GetString());

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/meta/{expectedHash}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/{expectedHash}")).StatusCode);
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
    public async Task Upload_WithTheRealValidator_RefusesACarThePhysicsCannotDrive()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        // Three wheels: the physics builds a four-wheel contact set and throws in the
        // constructor, so this is not a car however well-formed the radpack is.
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackRad3d
            {
                Metadata = new RadpackMetadata
                {
                    Name = "trike",
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Car
                },
                Rad = RadParser.ParseRad(
                    """
                    swits(80,200,300)
                    acelf(11.0,7.5,4.0)
                    handb(7)
                    airs(1.3)
                    airc(100)
                    turn(7)
                    grip(25.0)
                    bounce(1.1)
                    simag(0.9)
                    moment(1.5)
                    comprad(0.5)
                    push(2)
                    revpush(2)
                    lift(30)
                    revlift(0)
                    powerloss(3000000)
                    flipy(-63)
                    msquash(3)
                    clrad(5500)
                    dammult(0.38)
                    maxmag(5800)
                    dishandle(1.0)
                    outdam(0.85)
                    name(Trike)
                    <p>
                    c(120,120,120)
                    p(0,0,0)
                    p(40,0,0)
                    p(0,0,40)
                    </p>
                    w(-42,2,72,11,17,12)
                    w(42,2,72,11,-17,12)
                    w(-42,2,-84,0,17,12)
                    """,
                    "trike"
                )
            },
            writer
        );

        var response = await UploadAsync(client, writer.WrittenSpan.ToArray(), "trike.radpack", "trike", "Car");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        StringAssert.Contains(body.GetProperty("error").GetString(), "four wheels");
    }

    [TestMethod]
    public async Task Upload_WithTheRealValidator_AcceptsARealCar()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        var source = Path.Combine(
            Path.GetDirectoryName(ArchiveAppFactory.ValidatorPath)!,
            "data",
            "models",
            "nfmm",
            "cars",
            "radicalone.rad"
        );
        if (!File.Exists(source))
            Assert.Inconclusive("The validator's data tree is not beside its executable.");

        using var factory = new ArchiveAppFactory();
        using var client = await SignedInClient(factory);

        var rad = RadParser.ParseRad(File.ReadAllText(source), "radicalone");
        var writer = new ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackRad3d
            {
                Metadata = new RadpackMetadata
                {
                    Name = "radical one",
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Car
                },
                Rad = rad
            },
            writer
        );

        var response = await UploadAsync(client, writer.WrittenSpan.ToArray(), "radicalone.radpack", "radical one", "Car");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Car", (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("asset").GetProperty("type").GetString()
        );
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
