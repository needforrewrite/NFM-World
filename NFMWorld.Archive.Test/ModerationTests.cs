using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Moderation;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Test;

/// <summary>
/// Staff roles, moderation actions and account erasure. Each test boots the real app against a
/// throwaway SQLite file, so the endpoint pipeline and the Razor pages run as deployed.
/// </summary>
[TestClass]
public class ModerationTests
{
    /// <summary>Persisted in the Users.Role and ModerationAction.Action columns; never renumber.</summary>
    [TestMethod]
    public void EnumValues_ArePinned()
    {
        Assert.AreEqual(2, (byte)UserRole.Admin);
        Assert.AreEqual(15, (byte)ModerationActionKind.DeleteErase);
    }

    private static async Task<(HttpClient Client, string Token)> SignedInClientAsync(
        ArchiveAppFactory factory,
        string username
    )
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration(username));
        Assert.AreEqual(HttpStatusCode.OK, register.StatusCode);
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, token);
    }

    private static int UserId(ArchiveAppFactory factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

        return db.Users.Single(u => u.UsernameNormalized == username).Id;
    }

    private static async Task PromoteAsync(ArchiveAppFactory factory, string username, UserRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var user = db.Users.Single(u => u.UsernameNormalized == username);
        user.Role = role;
        await db.SaveChangesAsync();
        Console.WriteLine($"PROMOTED {username} (id {user.Id}) to {role}");
    }

    private static async Task PromoteByCliAsync(ArchiveAppFactory factory, string username, UserRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        db.Users.Single(u => u.UsernameNormalized == username).Role = role;
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task Moderator_CannotBanAnAdmin()
    {
        using var factory = new ArchiveAppFactory();
        var (_, modToken) = await SignedInClientAsync(factory, "mod");
        await PromoteAsync(factory, "mod", UserRole.Moderator);
        var (_, adminToken) = await SignedInClientAsync(factory, "chief");
        await PromoteAsync(factory, "chief", UserRole.Admin);

        var before = BannedUntil(factory, "chief");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", modToken);
        var response = await client.PostAsJsonAsync("/moderation/actions", new
        {
            action = (int)ModerationActionKind.Ban,
            targetUserId = UserId(factory, "chief"),
            reason = "no reason can reach an admin"
        });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(before, BannedUntil(factory, "chief"));
    }

    [TestMethod]
    public async Task Ban_BlocksTheLobbyButNotSignIn()
    {
        using var factory = new ArchiveAppFactory();
        var (_, token) = await SignedInClientAsync(factory, "player");
        var (_, adminToken) = await SignedInClientAsync(factory, "admin2");
        await PromoteAsync(factory, "admin2", UserRole.Admin);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var ban = await client.PostAsJsonAsync("/moderation/actions", new
        {
            action = (int)ModerationActionKind.Ban,
            targetUserId = UserId(factory, "player"),
            reason = "griefing"
        });
        Assert.AreEqual(HttpStatusCode.OK, ban.StatusCode);

        // The banned account can still sign in on the website.
        var login = await client.PostAsJsonAsync("/auth/login", new { Username = "player", Password = "password123" });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        // ... but /auth/verify reports the ban for the lobby.
        var verify = factory.CreateClient();
        verify.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var verifyBody = await (await verify.GetAsync("/auth/verify")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(verifyBody.GetProperty("banned").GetBoolean());
    }

    [TestMethod]
    public async Task Erase_KeepsUploadsAnonymised()
    {
        using var factory = new ArchiveAppFactory();
        var (_, artistToken) = await SignedInClientAsync(factory, "artist");
        var (_, adminToken) = await SignedInClientAsync(factory, "admin3");
        await PromoteAsync(factory, "admin3", UserRole.Admin);
        _ = artistToken;

        // The artist owns one asset.
        var contentHash = new string('b', 64);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storageRoot = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration
                .IConfiguration>()["Archive:StorageRoot"]!;
            var path = Path.Combine(storageRoot, contentHash[..2], contentHash);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });

            db.Assets.Add(new Asset
            {
                ReviewStatus = AssetReviewStatus.Legacy,
                OwnerId = UserId(factory, "artist"),
                Type = AssetType.Stage,
                Name = "My stage",
                ContentHash = contentHash,
                StoragePath = $"{contentHash[..2]}/{contentHash}",
                SizeBytes = 3,
                IsPublic = true,
                IsValidated = true
            });
            await db.SaveChangesAsync();
        }

        var artistId = UserId(factory, "artist");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var erase = await client.PostAsJsonAsync("/moderation/actions", new
        {
            action = (int)ModerationActionKind.DeleteErase,
            targetUserId = artistId
        });
        Assert.AreEqual(HttpStatusCode.OK, erase.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var user = db.Users.Include(u => u.Sessions).Include(u => u.Ratings).Single(u => u.Id == artistId);

            Assert.AreEqual($"deleted-{artistId}", user.Username);
            Assert.IsNull(user.PasswordHash);
            Assert.IsNotNull(user.DeletedAt);
            Assert.AreEqual(UserRole.User, user.Role);

            // The row survives with its sessions gone, and the asset still points at it.
            Assert.AreEqual(0, user.Sessions.Count);
            var asset = db.Assets.Single(a => a.ContentHash == contentHash);
            Assert.AreEqual(artistId, asset.OwnerId);
            Assert.AreEqual(1, db.ModerationActions.Count(a => a.TargetUserId == artistId));

            // The audit row still carries the pre-erase name.
            var action = db.ModerationActions.Single(a => a.TargetUserId == artistId);
            Assert.AreEqual("artist", action.TargetName);
        }

        var download = await factory.CreateClient().GetAsync($"/assets/{contentHash}");
        Assert.AreEqual(HttpStatusCode.OK, download.StatusCode);

        var page = await factory.CreateClient().GetStringAsync($"/asset/{DbAssetId(factory, contentHash)}");
        StringAssert.Contains(page, "Deleted account");
        Assert.IsFalse(page.Contains($"deleted-{artistId}", StringComparison.Ordinal));
    }

    private static int DbAssetId(ArchiveAppFactory factory, string hash)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

        return db.Assets.Single(a => a.ContentHash == hash).Id;
    }

    private static DateTimeOffset? BannedUntil(ArchiveAppFactory factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();

        return db.Users.Single(u => u.UsernameNormalized == username).BannedUntil;
    }

    [TestMethod]
    public async Task Erase_IsIdempotent()
    {
        using var factory = new ArchiveAppFactory();
        await SignedInClientAsync(factory, "gone");
        var id = UserId(factory, "gone");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<NFMWorld.Archive.FileStorage>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("test");

        var target = await db.Users.SingleAsync(u => u.Id == id);
        Assert.AreEqual(1, await AccountErasure.EraseAsync(db, storage, target, logger, CancellationToken.None));
        Assert.AreEqual(0, await AccountErasure.EraseAsync(db, storage, target, logger, CancellationToken.None));
        Assert.AreEqual($"deleted-{id}", target.Username);
    }

    [TestMethod]
    public async Task Erase_LeavesTheAccountUnusable()
    {
        using var factory = new ArchiveAppFactory();
        var (_, token) = await SignedInClientAsync(factory, "departing");
        var id = UserId(factory, "departing");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<NFMWorld.Archive.FileStorage>();
            var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("test");
            var target = await db.Users.SingleAsync(u => u.Id == id);
            await AccountErasure.EraseAsync(db, storage, target, logger, CancellationToken.None);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new { Username = "departing", Password = "password123" });
        Assert.AreEqual(HttpStatusCode.Unauthorized, login.StatusCode);

        var me = await client.GetAsync("/auth/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, me.StatusCode);
        _ = token;
    }

    [TestMethod]
    public async Task DeletionSweeper_ErasesOnlyExpiredRequests()
    {
        using var factory = new ArchiveAppFactory();
        await SignedInClientAsync(factory, "expired");
        await SignedInClientAsync(factory, "recent");
        var expiredId = UserId(factory, "expired");
        var recentId = UserId(factory, "recent");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            db.Users.Single(u => u.Id == expiredId).DeletionRequestedAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(40);
            db.Users.Single(u => u.Id == recentId).DeletionRequestedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var count = await DeletionSweeper.SweepOnceAsync(factory.Services, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.AreEqual(1, count);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            Assert.IsNotNull(db.Users.Single(u => u.Id == expiredId).DeletedAt);
            Assert.IsNull(db.Users.Single(u => u.Id == recentId).DeletedAt);
        }

        Assert.AreEqual(0, await DeletionSweeper.SweepOnceAsync(factory.Services, DateTimeOffset.UtcNow, CancellationToken.None));
    }
}
