using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Test;

public sealed class DiscordArchiveAppFactory : ArchiveAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Archive:DiscordClientId", "test-client");
        builder.UseSetting("Archive:DiscordClientSecret", "test-discord-secret");
        builder.UseSetting("Archive:DiscordRedirectUri", "https://archive.example/auth/discord/callback");
        builder.ConfigureServices(services => services.AddHttpClient("discord")
            .ConfigurePrimaryHttpMessageHandler(() => new DiscordHandler())
        );
    }

    private sealed class DiscordHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = request.Method == HttpMethod.Post
                        ? JsonContent.Create(new { access_token = "test-token", token_type = "Bearer" })
                        : JsonContent.Create(new { id = "12345", username = "discorduser" })
                }
            );
    }
}

[TestClass]
public class DiscordTests
{
    [TestMethod]
    public async Task Discord_OnlyVerifiedBrowserCanProvisionAnAccountAndStateIsSingleUse()
    {
        using var factory = new DiscordArchiveAppFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var oldStart = await client.GetAsync("/auth/discord/start");
        Assert.AreEqual("/account/login?flow=discord", oldStart.Headers.Location!.ToString());

        var html = await client.GetStringAsync(oldStart.Headers.Location);
        var antiforgery = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = antiforgery };
        var missing = await client.PostAsync("/auth/discord/start", new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            Assert.AreEqual(0, db.OauthPending.Count());
            Assert.AreEqual(0, db.Users.Count());
        }

        form["cf-turnstile-response"] = ArchiveAppFactory.Challenge("discord");
        var start = await client.PostAsync("/auth/discord/start", new FormUrlEncodedContent(form));
        Assert.AreEqual(HttpStatusCode.Redirect, start.StatusCode);
        var state = Regex.Match(start.Headers.Location!.Query, "[?&]state=([^&]+)").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(state));

        using var other = factory.CreateClient(new() { AllowAutoRedirect = false });
        var callback = "/auth/discord/callback?code=test-code&state=" + state;
        Assert.AreEqual(HttpStatusCode.BadRequest, (await other.GetAsync(callback)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Redirect, (await client.GetAsync(callback)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.GetAsync(callback)).StatusCode);
        using var resultScope = factory.Services.CreateScope();
        var resultDb = resultScope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        Assert.AreEqual("discorduser", resultDb.Users.Single().Username);
        Assert.AreEqual(0, resultDb.OauthPending.Count());
    }

    [TestMethod]
    public async Task Discord_RejectsChallengeWithoutAntiforgery()
    {
        using var factory = new DiscordArchiveAppFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            "/auth/discord/start",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("discord")
                }
            )
        );
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, factory.Siteverify.Calls);
    }
}
