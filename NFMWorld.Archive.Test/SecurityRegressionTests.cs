using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Test;

[TestClass]
public class SecurityRegressionTests
{
    // The prune path: once entries have expired, they are dropped and the cache keeps working.
    // (Removing during enumeration does not throw on .NET 11 — measured — so this asserts the
    // outcome rather than an exception.)
    [TestMethod]
    public void NonceCache_PrunesExpiredEntries()
    {
        var cache = new NonceCache(TimeSpan.FromMilliseconds(50));
        for (var i = 0; i < 10_050; i++)
            cache.TryUse("k", $"n{i}");

        Thread.Sleep(120);

        // Triggers the prune; a pruned nonce is usable again.
        Assert.IsTrue(cache.TryUse("k", "n0"));
    }

    [TestMethod]
    public void NonceCache_RejectsAReusedNonce()
    {
        var cache = new NonceCache();
        Assert.IsTrue(cache.TryUse("k", "one"));
        Assert.IsFalse(cache.TryUse("k", "one"));
    }

    // #6: registration was open to an unbounded PBKDF2 flood.
    [TestMethod]
    public async Task Register_IsThrottledPerIp()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 12; i++)
            last = await client.PostAsJsonAsync("/auth/register", new { Username = $"user{i}", Password = "password123" });

        Assert.AreEqual(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    // #2: a rejected upload of bytes another row already points at must not delete that file.
    [TestMethod]
    public async Task RejectedUpload_DoesNotDeleteBytesAnotherRowUses()
    {
        using var factory = new TrustedArchiveAppFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register",
            new { Username = "alice", Password = "password123" });
        var token = (await register.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var shared = "shared-bytes"u8.ToArray();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<NFMWorld.Archive.FileStorage>();

        var (hash, relative, _) = await storage.PutAsync((ReadOnlyMemory<byte>)shared, CancellationToken.None);
        db.Assets.Add(new Asset
        {
            OwnerId = db.Users.Single(u => u.UsernameNormalized == "alice").Id,
            Type = AssetType.Car,
            Name = "existing",
            ContentHash = hash,
            StoragePath = relative,
            SizeBytes = shared.Length,
            IsPublic = true
        });
        await db.SaveChangesAsync();

        // A second upload of the same bytes, which fails validation (no validator configured) —
        // this must leave the existing row's file in place.
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(shared), "file", "again.radpack");
        form.Add(new StringContent("second"), "name");
        form.Add(new StringContent("Car"), "type");
        await client.PostAsync("/assets/upload", form);

        Assert.IsTrue(storage.Exists(relative), "the existing asset's file was deleted by a rejected upload");
    }
}
