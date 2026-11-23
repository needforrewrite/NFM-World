using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Test;

/// <summary>
/// The browser frontend. These go through the real Razor pipeline with a cookie container, so
/// the form posts and the redirect that follows are exercised as a browser would drive them.
/// </summary>
[TestClass]
public class WebPageTests
{
    private static HttpClient Browser(ArchiveAppFactory factory)
    {
        var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false
            }
        );
        return client;
    }

    [TestMethod]
    public async Task Root_RedirectsASignedOutVisitorToSignIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var response = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.Contains(response.Headers.Location!.ToString(), "/account/login");
    }

    [TestMethod]
    public async Task RegisterForm_SignsInAndReturnsToTheAccountPage()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var page = await client.GetAsync("/account/login?flow=register");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Username"] = "websiteuser",
                ["RegisterPassword"] = "password123",
                ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"), ["__RequestVerificationToken"] = token
            }
        );

        var register = await client.PostAsync("/account/login?handler=Register", form);
        Assert.AreEqual(HttpStatusCode.Redirect, register.StatusCode);

        var account = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, account.StatusCode);
        var html = await account.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "websiteuser");
        StringAssert.Contains(html, "Sign out");
    }

    [TestMethod]
    public async Task LoginForm_RejectsAWrongPasswordWithoutSigningIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var page = await client.GetAsync("/account/login?flow=register");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        await client.PostAsync(
            "/account/login?handler=Register",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "someone", ["RegisterPassword"] = "password123",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"), ["__RequestVerificationToken"] = token
                }
            )
        );

        using var other = Browser(factory);
        var loginPage = await other.GetAsync("/account/login");
        var loginToken = ExtractAntiforgery(await loginPage.Content.ReadAsStringAsync());

        var login = await other.PostAsync(
            "/account/login?handler=SignIn",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "someone", ["Password"] = "wrong",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("signin"), ["__RequestVerificationToken"] = loginToken
                }
            )
        );

        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        StringAssert.Contains(await login.Content.ReadAsStringAsync(), "Wrong username or password");

        var account = await other.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.Redirect, account.StatusCode);
    }

    [TestMethod]
    public async Task LoginForm_DoesNotRedirectOffSite()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var page = await client.GetAsync("/account/login?flow=register");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        await client.PostAsync(
            "/account/login?handler=Register",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "redirector", ["RegisterPassword"] = "password123",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"), ["__RequestVerificationToken"] = token
                }
            )
        );

        using var other = Browser(factory);
        var loginPage = await other.GetAsync("/account/login");
        var loginToken = ExtractAntiforgery(await loginPage.Content.ReadAsStringAsync());

        var login = await other.PostAsync(
            "/account/login?handler=SignIn",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "redirector",
                    ["Password"] = "password123",
                    ["ReturnUrl"] = "https://evil.example/",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("signin"), ["__RequestVerificationToken"] = loginToken
                }
            )
        );

        Assert.AreEqual(HttpStatusCode.Redirect, login.StatusCode);
        Assert.AreEqual("/", login.Headers.Location!.ToString());
    }

    [TestMethod]
    public async Task LoginPage_IsReachableWhenSignedOut()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var response = await client.GetAsync("/account/login");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task LoginPage_ServesVersionedBladesAndSiteStylesLocally()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        var html = await client.GetStringAsync("/account/login");
        StringAssert.Contains(html, "data-theme=\"dark\"");
        StringAssert.Contains(html, "class=\"account-form\"");
        var styles = Regex.Matches(html, "<link[^>]*href=\"([^\"]+)\"")
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToArray();
        Assert.HasCount(2, styles);
        StringAssert.StartsWith(styles[0], "/lib/blades/blades.min.css?v=");
        StringAssert.StartsWith(styles[1], "/css/archive.css?v=");

        foreach (var url in styles)
        {
            using var response = await client.GetAsync(url);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, url);
            Assert.AreEqual("text/css", response.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            var css = await response.Content.ReadAsStringAsync();
            StringAssert.Contains(css, "--pico-");
        }
    }

    /// <summary>
    /// The browse page used to order assets by CreatedAt, which SQLite refuses to sort
    /// (DateTimeOffset is stored as TEXT), so the page failed with a 500 as soon as it loaded.
    /// </summary>
    [TestMethod]
    public async Task BrowsePage_LoadsWithAssetsTheDatabaseCanOrder()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var page = await client.GetAsync("/account/login?flow=register");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());
        await client.PostAsync(
            "/account/login?handler=Register",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "browser", ["RegisterPassword"] = "password123",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"), ["__RequestVerificationToken"] = token
                }
            )
        );

        var browse = await client.GetAsync("/browse");
        Assert.AreEqual(
            HttpStatusCode.OK,
            browse.StatusCode,
            await browse.Content.ReadAsStringAsync()
        );
    }

    /// <summary>
    /// After browsing, an upload opens its own page rather than downloading at once, so the
    /// description, creator and metadata are readable before anything is fetched.
    /// </summary>
    [TestMethod]
    public async Task AssetPage_ShowsMetadataAndLinksToTheDownload()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var hash = new string('a', 64);
        int id, otherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var owner = new User { Username = "artist", UsernameNormalized = "artist" };
            var asset = new Asset
            {
                Owner = owner,
                Type = AssetType.Stage,
                Name = "Prolific Dryland",
                Description = "A long downhill\nwith a shortcut.",
                ContentHash = hash,
                StoragePath = "aa/" + hash,
                SizeBytes = 54 * 1024,
                IsPublic = true,
                IsValidated = true,
                DownloadCount = 3
            };
            var other = new Asset
            {
                Owner = owner,
                Type = AssetType.Car,
                Name = "Other upload",
                ContentHash = new string('b', 64),
                StoragePath = "bb/" + new string('b', 64),
                SizeBytes = 1024,
                IsPublic = true,
                IsValidated = true
            };
            db.Assets.AddRange(asset, other);
            await db.SaveChangesAsync();
            id = asset.Id;
            otherId = other.Id;
        }

        var response = await client.GetAsync($"/asset/{id}");
        var html = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, html);

        StringAssert.Contains(html, "Prolific Dryland");
        StringAssert.Contains(html, "A long downhill");
        StringAssert.Contains(html, "artist");
        StringAssert.Contains(html, "54 KB");
        StringAssert.Contains(html, "<dd>3</dd>");
        StringAssert.Contains(html, $"/assets/{hash}");
        StringAssert.Contains(html, $"/asset/{otherId}");

        var browse = await client.GetStringAsync("/browse");
        StringAssert.Contains(browse, $"/asset/{id}");
    }

    /// <summary>A page is only rendered for a row the download endpoint would also serve.</summary>
    [TestMethod]
    public async Task AssetPage_IsNotFoundForAHiddenOrMissingRow()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        int privateId, takedownId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            var owner = new User { Username = "artist", UsernameNormalized = "artist" };
            var hidden = new Asset
            {
                Owner = owner, Type = AssetType.Stage, Name = "Private", ContentHash = new string('c', 64),
                StoragePath = "cc/" + new string('c', 64), IsPublic = false
            };
            var takedown = new Asset
            {
                Owner = owner, Type = AssetType.Stage, Name = "Removed", ContentHash = new string('d', 64),
                StoragePath = "dd/" + new string('d', 64), IsPublic = true, IsTakedown = true
            };
            db.Assets.AddRange(hidden, takedown);
            await db.SaveChangesAsync();
            privateId = hidden.Id;
            takedownId = takedown.Id;
        }

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/asset/{privateId}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/asset/{takedownId}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/asset/999999")).StatusCode);
    }

    /// <summary>
    /// The publish form has to refuse the same uploads the JSON API refuses. It used to check only
    /// that a <c>.rad</c> claimed a model kind, so a <c>.txt</c> or a <c>.radpack</c> that was not a
    /// radpack got stored with no validation at all.
    /// </summary>
    [TestMethod]
    public async Task UploadPage_RefusesAnExtensionTheApiRefuses()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var token = ExtractAntiforgery(await (await client.GetAsync("/account/login?flow=register"))
            .Content.ReadAsStringAsync()
        );
        await client.PostAsync(
            "/account/login?handler=Register",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "uploader", ["RegisterPassword"] = "password123",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"),
                    ["__RequestVerificationToken"] = token
                }
            )
        );

        var page = await client.GetStringAsync("/upload");
        var uploadToken = ExtractAntiforgery(page);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not a model"u8.ToArray()), "File", "notes.txt");
        form.Add(new StringContent("notes"), "Name");
        form.Add(new StringContent("Car"), "Type");
        form.Add(new StringContent(uploadToken), "__RequestVerificationToken");

        var response = await client.PostAsync("/upload", form);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        // The page answers 200 with the error rendered, so the refusal is the message.
        var html = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(html, "Only .radpack and .rad files are accepted");
    }

    /// <summary>
    /// The edit page is the website's half of the same CRUD the game drives through the JSON API:
    /// only the owner (or an administrator) may reach it, and a rename is applied in place.
    /// </summary>
    [TestMethod]
    public async Task EditPage_RenamesTheOwnersUpload()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        await SignUpAsync(client, "artist");
        var id = await SeedOwnedAsync(factory, "artist", "old name");

        var page = await client.GetStringAsync($"/asset/{id}/edit");
        StringAssert.Contains(page, "old name");

        var token = ExtractAntiforgery(page);
        var response = await client.PostAsync(
            $"/asset/{id}/edit?handler=Update",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Name"] = "new name",
                    ["Description"] = "edited on the website",
                    ["IsPublic"] = "true",
                    ["__RequestVerificationToken"] = token
                }
            )
        );
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);

        var detail = await client.GetStringAsync($"/asset/{id}");
        StringAssert.Contains(detail, "new name");
        StringAssert.Contains(detail, "edited on the website");
    }

    [TestMethod]
    public async Task EditPage_IsNotFoundForAStranger()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        await SignUpAsync(client, "artist");
        var id = await SeedOwnedAsync(factory, "artist", "mine");

        using var stranger = Browser(factory);
        await SignUpAsync(stranger, "intruder");

        Assert.AreEqual(HttpStatusCode.NotFound, (await stranger.GetAsync($"/asset/{id}/edit")).StatusCode);
    }

    /// <summary>Hiding an upload removes it from every public surface but keeps it editable.</summary>
    [TestMethod]
    public async Task EditPage_CanHideAnUploadAndStillReachIt()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        await SignUpAsync(client, "artist");
        var id = await SeedOwnedAsync(factory, "artist", "visible");

        var token = ExtractAntiforgery(await client.GetStringAsync($"/asset/{id}/edit"));
        await client.PostAsync(
            $"/asset/{id}/edit?handler=Update",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Name"] = "visible",
                    ["IsPublic"] = "false",
                    ["__RequestVerificationToken"] = token
                }
            )
        );

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/asset/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/asset/{id}/edit")).StatusCode);

        var account = await client.GetStringAsync("/");
        StringAssert.Contains(account, "Hidden");
    }

    [TestMethod]
    public async Task EditPage_DeletesTheUpload()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        await SignUpAsync(client, "artist");
        var id = await SeedOwnedAsync(factory, "artist", "doomed");

        var token = ExtractAntiforgery(await client.GetStringAsync($"/asset/{id}/edit"));
        var response = await client.PostAsync(
            $"/asset/{id}/edit?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })
        );
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        Assert.IsFalse(db.Assets.Any(a => a.Id == id));
    }

    [TestMethod]
    public async Task EditPage_RefusesAReplacementFileTheApiRefuses()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);
        await SignUpAsync(client, "artist");
        var id = await SeedOwnedAsync(factory, "artist", "course");

        var token = ExtractAntiforgery(await client.GetStringAsync($"/asset/{id}/edit"));
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not a model"u8.ToArray()), "Replacement", "notes.txt");
        form.Add(new StringContent(token), "__RequestVerificationToken");

        var response = await client.PostAsync($"/asset/{id}/edit?handler=Replace", form);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Only .radpack and .rad files are accepted");
    }

    /// <summary>Registers a browser session and returns the signed-in client.</summary>
    private static async Task SignUpAsync(HttpClient client, string username)
    {
        var token = ExtractAntiforgery(
            await (await client.GetAsync("/account/login?flow=register")).Content.ReadAsStringAsync()
        );
        await client.PostAsync(
            "/account/login?handler=Register",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = username,
                    ["RegisterPassword"] = "password123",
                    ["cf-turnstile-response"] = ArchiveAppFactory.Challenge("register"),
                    ["__RequestVerificationToken"] = token
                }
            )
        );
    }

    /// <summary>Seeds a row owned by <paramref name="owner"/>, creating the account when absent.</summary>
    private static async Task<int> SeedOwnedAsync(ArchiveAppFactory factory, string owner, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        var user = db.Users.FirstOrDefault(u => u.UsernameNormalized == owner);
        if (user is null)
        {
            user = new User { Username = owner, UsernameNormalized = owner };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var hash = new string((char)('a' + db.Assets.Count() % 26), 64);
        var asset = new Asset
        {
            OwnerId = user.Id,
            Type = AssetType.Stage,
            Name = name,
            ContentHash = hash,
            StoragePath = $"{hash[..2]}/{hash}",
            SizeBytes = 1024,
            IsPublic = true,
            IsValidated = true
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    [TestMethod]
    public async Task Administration_OnlyAdminsCanSetTheLimitUsedByWebUploads()
    {
        using var factory = new ArchiveAppFactory();
        using var admin = Browser(factory);
        using var moderator = Browser(factory);
        using var anonymous = Browser(factory);
        await SignUpAsync(admin, "adminuser");
        await SignUpAsync(moderator, "moduser");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
            db.Users.Single(user => user.UsernameNormalized == "adminuser").Role = NFMWorldLibrary.Multiplayer.UserRole.Admin;
            db.Users.Single(user => user.UsernameNormalized == "moduser").Role = NFMWorldLibrary.Multiplayer.UserRole.Moderator;
            await db.SaveChangesAsync();
        }

        Assert.AreEqual(HttpStatusCode.NotFound, (await moderator.GetAsync("/administration")).StatusCode);
        var modToken = ExtractAntiforgery(await moderator.GetStringAsync("/upload"));
        using var forbidden = await moderator.PostAsync("/administration", LimitForm(modToken, "1"));
        Assert.AreEqual(HttpStatusCode.NotFound, forbidden.StatusCode);

        using var initialResponse = await anonymous.GetAsync("/assets/limits");
        Assert.AreEqual(HttpStatusCode.OK, initialResponse.StatusCode);
        Assert.IsTrue(initialResponse.Headers.CacheControl?.NoStore == true);
        var initial = await initialResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(
            ArchiveSettings.DefaultUploadMiB * ArchiveSettings.BytesPerMiB,
            initial.GetProperty("maxUploadBytes").GetInt32()
        );
        var page = await admin.GetStringAsync("/administration");
        var token = ExtractAntiforgery(page);
        StringAssert.Contains(page, "Maximum upload size (MiB)");
        using var invalid = await admin.PostAsync("/administration", LimitForm(token, "65"));
        StringAssert.Contains(await invalid.Content.ReadAsStringAsync(), "Enter a whole number between 1 and 64 MiB.");

        using var saved = await admin.PostAsync("/administration", LimitForm(token, "1"));
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        var limits = await admin.GetFromJsonAsync<JsonElement>("/assets/limits");
        Assert.AreEqual(ArchiveSettings.BytesPerMiB, limits.GetProperty("maxUploadBytes").GetInt32());

        var uploadPage = await moderator.GetStringAsync("/upload");
        StringAssert.Contains(uploadPage, "Maximum upload size: 1 MiB.");
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(new byte[ArchiveSettings.BytesPerMiB + 1]), "File", "large.radpack");
        upload.Add(new StringContent("Large asset"), "Name");
        upload.Add(new StringContent("Stage"), "Type");
        upload.Add(new StringContent(ExtractAntiforgery(uploadPage)), "__RequestVerificationToken");
        using var uploadResponse = await moderator.PostAsync("/upload", upload);
        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, uploadResponse.StatusCode);

        var assetId = await SeedOwnedAsync(factory, "moduser", "old stage");
        var editPage = await moderator.GetStringAsync($"/asset/{assetId}/edit");
        StringAssert.Contains(editPage, "Maximum replacement size: 1 MiB.");
        using var replacement = new MultipartFormDataContent();
        replacement.Add(new ByteArrayContent(new byte[ArchiveSettings.BytesPerMiB + 1]), "Replacement", "large.radpack");
        replacement.Add(new StringContent(ExtractAntiforgery(editPage)), "__RequestVerificationToken");
        using var replacementResponse = await moderator.PostAsync($"/asset/{assetId}/edit?handler=Replace", replacement);
        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, replacementResponse.StatusCode);
    }

    private static FormUrlEncodedContent LimitForm(string token, string limit) => new(
        new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["UploadLimitMiB"] = limit }
    );

    private static string ExtractAntiforgery(string html)
    {
        // Attribute order is Razor's business; match the input element and pull value out of it.
        var tag = Regex.Match(html, "<input[^>]*__RequestVerificationToken[^>]*>");
        Assert.IsTrue(tag.Success, "the sign-in page has no antiforgery token");

        var value = Regex.Match(tag.Value, "value=\"([^\"]+)\"");
        Assert.IsTrue(value.Success, "the antiforgery input has no value");
        return value.Groups[1].Value;
    }
}
