using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Test;

/// <summary>
/// The owner's edit surface: metadata, visibility, replacement bytes and deletion, plus the
/// authorisation that separates an owner from a stranger from an administrator.
/// </summary>
[TestClass]
public class AssetCrudTests
{
    private static string Hash(char c) => new(c, 64);

    private static async Task<(HttpClient Client, string Token)> SignedInClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string username
    )
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration(username));
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    /// <summary>Seeds one row owned by <paramref name="owner"/> and returns its id.</summary>
    private static async Task<int> SeedAssetAsync(
        ArchiveAppFactory factory,
        string? owner,
        string name = "My stage",
        AssetType type = AssetType.Stage,
        bool isPublic = true,
        bool takedown = false,
        bool validated = true,
        string? hash = null
    )
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        int? ownerId = null;
        if (owner is not null)
        {
            var user = db.Users.FirstOrDefault(u => u.UsernameNormalized == owner);
            if (user is null)
            {
                user = new User { Username = owner, UsernameNormalized = owner };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            ownerId = user.Id;
        }

        var contentHash = hash ?? Hash('a');
        var asset = new Asset
        {
            OwnerId = ownerId,
            Type = type,
            Name = name,
            ContentHash = contentHash,
            StoragePath = $"{contentHash[..2]}/{contentHash}",
            SizeBytes = 10,
            IsPublic = isPublic,
            IsTakedown = takedown,
            IsValidated = validated
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    [TestMethod]
    public async Task Mine_RequiresSignIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/assets/mine")).StatusCode);
    }

    [TestMethod]
    public async Task Mine_ListsTheCallersRowsIncludingHiddenAndTakenDown()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        await SeedAssetAsync(factory, "artist", "listed");
        await SeedAssetAsync(factory, "artist", "hidden", isPublic: false, hash: Hash('b'));
        await SeedAssetAsync(factory, "artist", "removed", isPublic: false, takedown: true, hash: Hash('c'));
        await SeedAssetAsync(factory, "someoneelse", "theirs", hash: Hash('d'));

        var body = await client.GetFromJsonAsync<JsonElement>("/assets/mine");
        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        CollectionAssert.AreEquivalent(new[] { "listed", "hidden", "removed" }, names);
        var removed = body.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "removed");
        Assert.IsTrue(removed.GetProperty("isTakedown").GetBoolean());
        Assert.IsFalse(removed.GetProperty("isPublic").GetBoolean());
    }

    [TestMethod]
    public async Task Mine_PagesByTheSameCursorAsSearch()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        for (var i = 0; i < 5; i++)
            await SeedAssetAsync(factory, "artist", $"upload {i}", hash: i.ToString("x64"));

        var first = await client.GetFromJsonAsync<JsonElement>("/assets/mine?limit=2");
        var cursor = first.GetProperty("nextCursor").GetInt32();
        var second = await client.GetFromJsonAsync<JsonElement>($"/assets/mine?limit=2&after={cursor}");

        Assert.AreEqual(2, first.GetProperty("items").GetArrayLength());
        Assert.AreEqual(2, second.GetProperty("items").GetArrayLength());
        var ids = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray())
            .Select(item => item.GetProperty("id").GetInt32())
            .ToArray();
        Assert.AreEqual(4, ids.Distinct().Count());
    }

    [TestMethod]
    public async Task Update_RenamesHidesAndRelistsTheOwnersRow()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", "old name");

        var response = await client.PatchAsJsonAsync(
            $"/assets/{id}",
            new { Name = "  new name  ", Description = "a description", IsPublic = false }
        );
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var asset = body.GetProperty("asset");
        Assert.AreEqual("new name", asset.GetProperty("name").GetString());
        Assert.AreEqual("a description", asset.GetProperty("description").GetString());
        Assert.IsFalse(asset.GetProperty("isPublic").GetBoolean());

        // Hidden means gone from every public surface, but still the owner's.
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/meta/{Hash('a')}")).StatusCode);
        var mine = await client.GetFromJsonAsync<JsonElement>("/assets/mine");
        Assert.AreEqual("new name", mine.GetProperty("items")[0].GetProperty("name").GetString());

        var relist = await client.PatchAsJsonAsync($"/assets/{id}", new { IsPublic = true });
        Assert.AreEqual(HttpStatusCode.OK, relist.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/assets/meta/{Hash('a')}")).StatusCode);
    }

    [TestMethod]
    public async Task Update_ClearingTheDescriptionRemovesIt()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");
        await client.PatchAsJsonAsync($"/assets/{id}", new { Description = "first" });

        await client.PatchAsJsonAsync($"/assets/{id}", new { Description = "   " });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        Assert.IsNull(db.Assets.Single(a => a.Id == id).Description);
    }

    [TestMethod]
    public async Task Update_ByAStranger_IsRefusedAndChangesNothing()
    {
        using var factory = new ArchiveAppFactory();
        var (owner, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", "original");

        var (stranger, _) = await SignedInClient(factory, "intruder");
        var response = await stranger.PatchAsJsonAsync($"/assets/{id}", new { Name = "hijacked" });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        var mine = await owner.GetFromJsonAsync<JsonElement>("/assets/mine");
        Assert.AreEqual("original", mine.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task Update_TakedownIsAdministratorsOnly()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");

        var ownerAttempt = await client.PatchAsJsonAsync($"/assets/{id}", new { IsTakedown = true });
        Assert.AreEqual(HttpStatusCode.Forbidden, ownerAttempt.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            db.Users.Single(u => u.UsernameNormalized == "artist").Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        var adminAttempt = await client.PatchAsJsonAsync($"/assets/{id}", new { IsTakedown = true });
        Assert.AreEqual(HttpStatusCode.OK, adminAttempt.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/{Hash('a')}")).StatusCode);
    }

    [TestMethod]
    public async Task Update_RejectsAnEmptyOrOverlongName()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");

        Assert.AreEqual(
            HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/assets/{id}", new { Name = "   " })).StatusCode
        );
        Assert.AreEqual(
            HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync(
                $"/assets/{id}",
                new { Name = new string('x', NFMWorld.Archive.AssetUpload.MaxNameLength + 1) }
            )).StatusCode
        );
    }

    [TestMethod]
    public async Task Delete_RemovesTheRowItsFileAndTheReplaysThatPinnedIt()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", hash: Hash('a'));

        int replayId;
        string relative;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            var stored = await storage.PutAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
            relative = stored.RelativePath;
            var asset = db.Assets.Single(a => a.Id == id);
            asset.StoragePath = relative;
            asset.ContentHash = stored.Hash;

            var replay = new TimeTrial
            {
                User = db.Users.Single(u => u.UsernameNormalized == "artist"),
                StageHash = stored.Hash,
                CarHash = Hash('b'),
                StageName = "course",
                CarName = "car",
                StoragePath = "replays/one",
                TotalTicks = 1000
            };
            db.TimeTrials.Add(replay);
            await db.SaveChangesAsync();
            replayId = replay.Id;
        }

        var response = await client.DeleteAsync($"/assets/{id}");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            Assert.IsFalse(db.Assets.Any(a => a.Id == id));
            Assert.IsFalse(storage.Exists(relative), "the stored file outlived its row");
            Assert.IsFalse(db.TimeTrials.Single(t => t.Id == replayId).IsValid);
        }

        // The replay no longer ranks or downloads, and the upload no longer resolves.
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/tt/replay/{replayId}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/{Hash('a')}")).StatusCode);
    }

    [TestMethod]
    public async Task Delete_ByAStranger_IsRefused()
    {
        using var factory = new ArchiveAppFactory();
        await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");

        var (stranger, _) = await SignedInClient(factory, "intruder");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/assets/{id}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        Assert.IsTrue(scope.ServiceProvider.GetRequiredService<ArchiveDbContext>().Assets.Any(a => a.Id == id));
    }

    [TestMethod]
    public async Task Delete_OfASharedFile_LeavesTheOtherRowDownloadable()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var hash = Hash('c');
        var first = await SeedAssetAsync(factory, "artist", "first", hash: hash);
        await SeedAssetAsync(factory, "someoneelse", "second", hash: hash);

        string relative;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            var stored = await storage.PutAsync(new byte[] { 9, 9, 9 }, CancellationToken.None);
            relative = stored.RelativePath;
            foreach (var asset in db.Assets.Where(a => a.ContentHash == hash))
                asset.StoragePath = relative;
            await db.SaveChangesAsync();
        }

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/assets/{first}")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            Assert.IsTrue(storage.Exists(relative), "a shared file was deleted with one of its rows");
        }

        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/assets/{hash}")).StatusCode);
    }

    [TestMethod]
    public async Task Delete_OfAMissingRow_IsNotFound()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync("/assets/424242")).StatusCode);
    }

    [TestMethod]
    public async Task Writes_AreRefusedOnAReadOnlyArchive()
    {
        using var factory = new ArchiveAppFactory();
        // Registration itself is disabled on a read-only archive, so the session comes from the
        // writable host over the same database and the read-only host only serves the writes.
        var (registered, token) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");
        registered.Dispose();

        using var readOnly = factory.WithWebHostBuilder(builder => builder.UseSetting("Archive:ReadOnly", "true"));
        using var client = readOnly.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.AreEqual(
            HttpStatusCode.ServiceUnavailable,
            (await client.PatchAsJsonAsync($"/assets/{id}", new { Name = "renamed" })).StatusCode
        );
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await client.DeleteAsync($"/assets/{id}")).StatusCode);
    }

    [TestMethod]
    public async Task UploadLimit_RejectsNewFilesAndReplacementsWithoutRemovingExistingFiles()
    {
        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", "existing stage");
        var oversized = new byte[ArchiveSettings.BytesPerMiB + 1];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var existing = db.Assets.Single(asset => asset.Id == id);
            existing.SizeBytes = oversized.Length;
            var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
            var stored = await storage.PutAsync(oversized, CancellationToken.None);
            existing.StoragePath = stored.RelativePath;
            existing.ContentHash = stored.Hash;
            db.Settings.Single().UploadLimitMiB = 1;
            await db.SaveChangesAsync();
        }

        foreach (var route in new[] { "/assets/upload", "/assets/publish" })
        {
            using var form = PublicationForm(oversized, "new stage", "", true);
            form.Add(new StringContent("Stage"), "type");
            using var response = await client.PostAsync(route, form);
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual(ArchiveSettings.BytesPerMiB, error.GetProperty("maxUploadBytes").GetInt32());
        }

        foreach (var route in new[] { $"/assets/{id}/content", $"/assets/{id}/publication" })
        {
            using var form = PublicationForm(oversized, "must not rename", "", false);
            using var response = await client.PutAsync(route, form);
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }

        using var renamed = await client.PatchAsJsonAsync($"/assets/{id}", new { Name = "renamed stage" });
        Assert.AreEqual(HttpStatusCode.OK, renamed.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var saved = verifyScope.ServiceProvider.GetRequiredService<ArchiveDbContext>().Assets.Single();
        Assert.AreEqual(id, saved.Id);
        Assert.AreEqual("renamed stage", saved.Name);
        Assert.AreEqual(oversized.LongLength, saved.SizeBytes);
        Assert.IsTrue(saved.IsPublic);
        Assert.IsTrue(verifyScope.ServiceProvider.GetRequiredService<FileStorage>().Exists(saved.StoragePath));
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/assets/meta/{saved.ContentHash}")).StatusCode);
    }

    [TestMethod]
    public async Task EditorPublication_PublishesHiddenAndUpdatesTheSameListing()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        using var initial = PublicationForm(StagePackage("initial stage"), "First listing", "initial description", false);
        initial.Add(new StringContent("Stage"), "type");
        using var published = await client.PostAsync("/assets/publish", initial);
        Assert.AreEqual(HttpStatusCode.OK, published.StatusCode, await published.Content.ReadAsStringAsync());

        var mine = await client.GetFromJsonAsync<JsonElement>("/assets/mine?type=Stage");
        var original = mine.GetProperty("items").EnumerateArray().Single();
        var id = original.GetProperty("id").GetInt32();
        var originalHash = original.GetProperty("contentHash").GetString();
        Assert.IsFalse(original.GetProperty("isPublic").GetBoolean());
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/meta/{originalHash}")).StatusCode);

        var updatedBytes = StagePackage("updated stage");
        using var invalid = PublicationForm(updatedBytes, "", "Rejected description", true);
        using var rejected = await client.PutAsync($"/assets/{id}/publication", invalid);
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        mine = await client.GetFromJsonAsync<JsonElement>("/assets/mine?type=Stage");
        Assert.AreEqual(original.GetRawText(), mine.GetProperty("items").EnumerateArray().Single().GetRawText());

        using var revision = PublicationForm(updatedBytes, "New listing name", "Updated description", true);
        using var updated = await client.PutAsync($"/assets/{id}/publication", revision);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode, await updated.Content.ReadAsStringAsync());
        mine = await client.GetFromJsonAsync<JsonElement>("/assets/mine?type=Stage");
        var updatedAsset = mine.GetProperty("items").EnumerateArray().Single();
        Assert.AreEqual(id, updatedAsset.GetProperty("id").GetInt32());
        Assert.AreEqual("New listing name", updatedAsset.GetProperty("name").GetString());
        Assert.AreEqual("Updated description", updatedAsset.GetProperty("description").GetString());
        Assert.IsTrue(updatedAsset.GetProperty("isPublic").GetBoolean());
        var updatedHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(updatedBytes));
        Assert.AreEqual(updatedHash, updatedAsset.GetProperty("contentHash").GetString());

        using var metadata = PublicationForm(updatedBytes, "New listing name", "", false);
        using var hidden = await client.PutAsync($"/assets/{id}/publication", metadata);
        Assert.AreEqual(HttpStatusCode.OK, hidden.StatusCode, await hidden.Content.ReadAsStringAsync());
        var hiddenAsset = (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("asset");
        Assert.AreEqual(updatedHash, hiddenAsset.GetProperty("contentHash").GetString());
        Assert.IsFalse(hiddenAsset.GetProperty("isPublic").GetBoolean());
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/assets/meta/{updatedHash}")).StatusCode);
    }

    private static MultipartFormDataContent PublicationForm(byte[] bytes, string name, string description, bool isPublic)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "stage.radpack");
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent(description), "description");
        form.Add(new StringContent(isPublic.ToString()), "isPublic");
        return form;
    }

    [TestMethod]
    public async Task Replace_RequiresTheRealValidatorAndKeepsTheRowId()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", "course");

        var bytes = StagePackage("replaced course");
        var response = await ReplaceAsync(client, id, bytes, "course.radpack");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var asset = body.GetProperty("asset");
        Assert.AreEqual(id, asset.GetProperty("id").GetInt32());
        Assert.AreEqual(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
            asset.GetProperty("contentHash").GetString()
        );
    }

    [TestMethod]
    public async Task Replace_WithBytesAlreadyPublishedElsewhere_IsRefused()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var first = await SeedAssetAsync(factory, "artist", "first");
        var second = await SeedAssetAsync(factory, "artist", "second", hash: Hash('b'));

        var bytes = StagePackage("shared course");
        Assert.AreEqual(HttpStatusCode.OK, (await ReplaceAsync(client, first, bytes, "a.radpack")).StatusCode);

        var response = await ReplaceAsync(client, second, bytes, "b.radpack");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Replace_ByANonOwner_IsRefused()
    {
        using var factory = new ArchiveAppFactory();
        await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist");

        var (stranger, _) = await SignedInClient(factory, "intruder");
        var response = await ReplaceAsync(stranger, id, StagePackage("hijack"), "x.radpack");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A replacement is validated as what the bytes are, not by the name the uploader sent: a
    /// legacy row can be replaced by a radpack, which is what the game's editors always send.
    /// </summary>
    [TestMethod]
    public async Task Replace_AcceptsARadpackForALegacyRadRow()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        var legacy = Path.Combine(
            Path.GetDirectoryName(ArchiveAppFactory.ValidatorPath)!,
            "data",
            "models",
            "nfmm",
            "cars",
            "2000tornados.rad"
        );
        if (!File.Exists(legacy))
            Assert.Inconclusive("The validator's data tree is not beside its executable.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");

        // A legacy upload stores a row whose file name no longer exists to the replacement path.
        var upload = await UploadAsync(client, File.ReadAllBytes(legacy), "legacy.rad", "legacy car", "Car");
        Assert.AreEqual(HttpStatusCode.OK, upload.StatusCode, await upload.Content.ReadAsStringAsync());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("asset").GetProperty("id").GetInt32();

        var response = await ReplaceAsync(client, id, CarPackage(legacy), "replacement.radpack");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// A radpack that fails as a radpack must not be retried as a legacy text model: doing so read
    /// a wrong-kind package as something it is not.
    /// </summary>
    [TestMethod]
    public async Task Replace_WithAWrongKindRadpack_IsRefused()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        var legacy = Path.Combine(
            Path.GetDirectoryName(ArchiveAppFactory.ValidatorPath)!,
            "data",
            "models",
            "nfmm",
            "cars",
            "2000tornados.rad"
        );
        if (!File.Exists(legacy))
            Assert.Inconclusive("The validator's data tree is not beside its executable.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");

        var upload = await UploadAsync(client, File.ReadAllBytes(legacy), "legacy.rad", "legacy car", "Car");
        Assert.AreEqual(HttpStatusCode.OK, upload.StatusCode, await upload.Content.ReadAsStringAsync());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("asset").GetProperty("id").GetInt32();

        var response = await ReplaceAsync(client, id, StagePackage("a stage, not a car"), "replacement.radpack");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        StringAssert.Contains((await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetString(), "not a Car"
        );
    }

    /// <summary>
    /// The in-game publisher re-publishes a document by replacing the account's existing row of the
    /// same name, so two edits of one stage must leave one upload rather than a stale duplicate.
    /// </summary>
    [TestMethod]
    public async Task Replace_Twice_LeavesOneRowWithTheLatestBytes()
    {
        if (ArchiveAppFactory.ValidatorPath is null)
            Assert.Inconclusive("The validator executable was not built beside the tests.");

        using var factory = new ArchiveAppFactory();
        var (client, _) = await SignedInClient(factory, "artist");
        var id = await SeedAssetAsync(factory, "artist", "course");

        var first = StagePackage("first edit");
        Assert.AreEqual(HttpStatusCode.OK, (await ReplaceAsync(client, id, first, "a.radpack")).StatusCode);
        var second = StagePackage("second edit");
        var response = await ReplaceAsync(client, id, second, "b.radpack");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var rows = db.Assets.Where(a => a.OwnerId == db.Users.Single(u => u.UsernameNormalized == "artist").Id)
            .ToArray();
        Assert.HasCount(1, rows);
        Assert.AreEqual(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(second)),
            rows[0].ContentHash
        );
    }

    /// <summary>The same car as a radpack, so the replacement only changes the format.</summary>
    private static byte[] CarPackage(string legacyRadPath)
    {
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new NFMWorldLibrary.Radpack.RadpackRad3d
            {
                Metadata = new NFMWorldLibrary.Radpack.RadpackMetadata
                {
                    Name = "model replaced",
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = NFMWorldLibrary.Radpack.RadpackType.Car
                },
                Rad = NFMWorldLibrary.Rad.RadParser.ParseRad(File.ReadAllText(legacyRadPath), "2000tornados")
            },
            writer
        );
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] StagePackage(string name)
    {
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        RadpackSerializer.Serialize(
            new RadpackTrack
            {
                Metadata = new RadpackMetadata
                {
                    Name = name,
                    CreationDate = DateTimeOffset.UnixEpoch,
                    Type = RadpackType.Track
                },
                Stage = new NFMWorldLibrary.StageLoader()
            },
            writer
        );
        return writer.WrittenSpan.ToArray();
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        byte[] bytes,
        string fileName,
        string name,
        string type
    )
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent(type), "type");
        return await client.PostAsync("/assets/upload", form);
    }

    private static async Task<HttpResponseMessage> ReplaceAsync(
        HttpClient client,
        int id,
        byte[] bytes,
        string fileName
    )
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return await client.PutAsync($"/assets/{id}/content", form);
    }
}
