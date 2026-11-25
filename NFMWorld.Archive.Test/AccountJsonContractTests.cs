using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorldLibrary.Accounts;

namespace NFMWorld.Archive.Test;

/// <summary>
/// The game's account surface serializes through <see cref="AccountJsonContext"/> because a trimmed
/// build cannot make reflection metadata for these types: the metadata builder reads constructor
/// parameter names, trimming strips them, and <c>JsonContent.Create</c> then throws
/// <c>ConstructorContainsNullParameterNames</c> before the request leaves the client. These tests
/// post the generated bodies to the real endpoints, so a DTO that drifts from the archive's own
/// shape fails here rather than on the deployed browser build.
/// </summary>
[TestClass]
public class AccountJsonContractTests
{
    [TestMethod]
    public async Task Login_TheGeneratedBody_IsAcceptedAndTheReplyReadsBack()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", ArchiveAppFactory.Registration("alice"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest("alice", "password123"),
                AccountJsonContext.Default.LoginRequest
            )
        };
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.LoginResponse);
        Assert.IsNotNull(body);
        Assert.IsFalse(string.IsNullOrEmpty(body.Token));
        Assert.AreEqual("alice", body.Account.Username);
        Assert.AreEqual(NFMWorldLibrary.Multiplayer.UserRole.User, body.Account.Role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        using var me = await client.GetAsync("/auth/me");
        var account = await me.Content.ReadFromJsonAsync(AccountJsonContext.Default.AccountResponse);
        Assert.IsNotNull(account);
        Assert.AreEqual("alice", account.Username);
    }

    [TestMethod]
    public void AssetUpdate_LeavesUnsetFieldsOutOfTheBody()
    {
        var json = JsonSerializer.Serialize(
            new AssetUpdateRequest(null, "a description", false),
            AccountJsonContext.Default.AssetUpdateRequest
        );

        using var document = JsonDocument.Parse(json);
        CollectionAssert.AreEquivalent(
            new[] { "description", "isPublic" },
            document.RootElement.EnumerateObject().Select(p => p.Name).ToArray()
        );
        Assert.IsFalse(document.RootElement.GetProperty("isPublic").GetBoolean(), "false is a real value and must be sent.");
    }

    [TestMethod]
    public void StoredAccount_RoundTripsThroughTheGeneratedContext()
    {
        var stored = new StoredAccount("token", "alice", NFMWorldLibrary.Multiplayer.UserRole.Moderator, null);

        var json = JsonSerializer.Serialize(stored, AccountJsonContext.Default.StoredAccount);
        var back = JsonSerializer.Deserialize(json, AccountJsonContext.Default.StoredAccount);

        Assert.IsNotNull(back);
        Assert.AreEqual("token", back.Token);
        Assert.AreEqual("alice", back.Username);
        Assert.AreEqual(NFMWorldLibrary.Multiplayer.UserRole.Moderator, back.Role);
        Assert.IsNull(back.DeletionPendingUntil);
    }
}
