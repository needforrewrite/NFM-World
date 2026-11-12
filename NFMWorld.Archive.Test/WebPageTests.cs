using System.Net;
using System.Text.RegularExpressions;

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
