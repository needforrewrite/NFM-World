using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorld.Archive.Auth;

namespace NFMWorld.Archive.Test;

[TestClass]
public class PasswordHasherTests
{
    [TestMethod]
    public void Verify_AcceptsTheOriginalPassword()
    {
        var hash = PasswordHasher.Hash("correct horse");

        Assert.IsTrue(PasswordHasher.Verify("correct horse", hash));
    }

    [TestMethod]
    public void Verify_RejectsADifferentPassword()
    {
        var hash = PasswordHasher.Hash("correct horse");

        Assert.IsFalse(PasswordHasher.Verify("correct horst", hash));
    }

    [TestMethod]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently()
    {
        Assert.AreNotEqual(PasswordHasher.Hash("password1"), PasswordHasher.Hash("password1"));
    }

    [TestMethod]
    public void Verify_RejectsTheOldArchiveSha512Format()
    {
        // The old archive stored SHA-512(salt + password); it must not be confused with ours.
        const string legacy = "pbkdf2-sha512$210000$AAAA$AAAA";
        Assert.IsFalse(PasswordHasher.Verify("anything", legacy));
    }
}

[TestClass]
public class UsernamePolicyTests
{
    [TestMethod]
    [DataRow("alice")]
    [DataRow("Alice_99")]
    public void TryValidate_AcceptsOrdinaryNames(string name)
        => Assert.IsTrue(UsernamePolicy.TryValidate(name, out _));

    [TestMethod]
    [DataRow("")]
    [DataRow("ab")]
    public void TryValidate_RejectsShortNames(string name)
        => Assert.IsFalse(UsernamePolicy.TryValidate(name, out _));

    [TestMethod]
    public void TryValidate_RejectsNamesThatAreOnlySpaces()
        => Assert.IsFalse(UsernamePolicy.TryValidate("   ", out _));

    [TestMethod]
    public void TryValidate_RejectsEmbeddedSpaces()
        => Assert.IsFalse(UsernamePolicy.TryValidate("not alice", out _));

    [TestMethod]
    public void Normalize_FoldsCaseAndFullWidth()
    {
        Assert.AreEqual(UsernamePolicy.Normalize("Alice"), UsernamePolicy.Normalize("alice"));
        Assert.AreEqual(UsernamePolicy.Normalize("Alice"), UsernamePolicy.Normalize("Ａlice"));
    }
}

[TestClass]
public class AuthEndpointTests
{
    [TestMethod]
    public async Task Register_ThenMe_ReturnsTheAccount()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });
        Assert.AreEqual(HttpStatusCode.OK, register.StatusCode, await register.Content.ReadAsStringAsync());

        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();
        Assert.IsFalse(string.IsNullOrEmpty(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/auth/me");
        Assert.AreEqual(HttpStatusCode.OK, me.StatusCode);

        var account = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("alice", account.GetProperty("username").GetString());
    }

    [TestMethod]
    public async Task Register_RejectsADuplicateNameIgnoringCase()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });
        var second = await client.PostAsJsonAsync("/auth/register", new { Username = "ALICE", Password = "password456" });

        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

    [TestMethod]
    public async Task Login_RejectsAWrongPassword()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });
        var login = await client.PostAsJsonAsync("/auth/login", new { Username = "alice", Password = "wrong" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [TestMethod]
    public async Task Login_BlocksAfterRepeatedFailures()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });

        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync("/auth/login", new { Username = "alice", Password = "wrong" });

        var blocked = await client.PostAsJsonAsync("/auth/login", new { Username = "alice", Password = "wrong" });
        Assert.AreEqual(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [TestMethod]
    public async Task Me_RejectsAMadeUpToken()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [TestMethod]
    public async Task Logout_InvalidatesTheToken()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.PostAsync("/auth/logout", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [TestMethod]
    public async Task LogoutAll_InvalidatesEveryOtherSession()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register", new { Username = "alice", Password = "password123" });
        var login = await client.PostAsJsonAsync("/auth/login", new { Username = "alice", Password = "password123" });
        var secondToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        Assert.AreEqual(HttpStatusCode.OK, (await other.GetAsync("/auth/me")).StatusCode);

        Assert.AreEqual(HttpStatusCode.NoContent, (await other.PostAsync("/auth/logout-all", null)).StatusCode);

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await other.GetAsync("/auth/me")).StatusCode);
    }
}
