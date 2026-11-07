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
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
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

        var page = await client.GetAsync("/account/login");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "websiteuser",
            ["RegisterPassword"] = "password123",
            ["__RequestVerificationToken"] = token
        });

        var register = await client.PostAsync("/account/login?handler=Register", form);
        Assert.AreEqual(HttpStatusCode.Redirect, register.StatusCode);

        var account = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, account.StatusCode);
        StringAssert.Contains(await account.Content.ReadAsStringAsync(), "websiteuser");
    }

    [TestMethod]
    public async Task LoginForm_RejectsAWrongPasswordWithoutSigningIn()
    {
        using var factory = new ArchiveAppFactory();
        using var client = Browser(factory);

        var page = await client.GetAsync("/account/login");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        await client.PostAsync("/account/login?handler=Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "someone", ["RegisterPassword"] = "password123", ["__RequestVerificationToken"] = token
        }));

        using var other = Browser(factory);
        var loginPage = await other.GetAsync("/account/login");
        var loginToken = ExtractAntiforgery(await loginPage.Content.ReadAsStringAsync());

        var login = await other.PostAsync("/account/login?handler=SignIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "someone", ["Password"] = "wrong", ["__RequestVerificationToken"] = loginToken
        }));

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

        var page = await client.GetAsync("/account/login");
        var token = ExtractAntiforgery(await page.Content.ReadAsStringAsync());

        await client.PostAsync("/account/login?handler=Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "redirector", ["RegisterPassword"] = "password123", ["__RequestVerificationToken"] = token
        }));

        using var other = Browser(factory);
        var loginPage = await other.GetAsync("/account/login");
        var loginToken = ExtractAntiforgery(await loginPage.Content.ReadAsStringAsync());

        var login = await other.PostAsync("/account/login?handler=SignIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = "redirector",
            ["Password"] = "password123",
            ["ReturnUrl"] = "https://evil.example/",
            ["__RequestVerificationToken"] = loginToken
        }));

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
