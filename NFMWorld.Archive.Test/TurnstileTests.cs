using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using NFMWorld.Archive.Auth;

namespace NFMWorld.Archive.Test;

public sealed class FakeSiteverifyHandler : HttpMessageHandler
{
    private readonly HashSet<string> _used = [];
    public string? Hostname { get; set; } = "archive.example";
    public string? Action { get; set; }
    public bool Success { get; set; } = true;
    public string? RawResponse { get; set; }
    public string[]? ErrorCodes { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public Exception? Exception { get; set; }
    public int Calls { get; private set; }

    /// <summary>The parameters of the last Siteverify call, so tests can assert what we sent.</summary>
    public IReadOnlyDictionary<string, string> LastFields { get; private set; } =
        new Dictionary<string, string>();

    public string ExpectedSecret { get; set; } = "test-secret";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Assert.AreEqual("https://challenges.cloudflare.com/turnstile/v0/siteverify", request.RequestUri!.ToString());
        Assert.AreEqual(HttpMethod.Post, request.Method);
        if (Exception is not null)
            throw Exception;
        var fields = (await request.Content!.ReadAsStringAsync(ct)).Split('&')
            .Select(field => field.Split('=', 2))
            .ToDictionary(field => field[0], field => Uri.UnescapeDataString(field[1]));
        LastFields = fields;
        Assert.AreEqual(ExpectedSecret, fields["secret"]);
        var token = fields["response"];
        return new HttpResponseMessage(Status)
        {
            Content = RawResponse is null
                ? JsonContent.Create(
                    new Dictionary<string, object?>
                    {
                        ["success"] = Success && _used.Add(token),
                        ["hostname"] = Hostname,
                        ["action"] = Action ?? token.Split(':')[0],
                        ["error-codes"] = ErrorCodes
                    }
                )
                : new StringContent(RawResponse)
        };
    }
}

[TestClass]
public class TurnstileTests
{
    [TestMethod]
    [DataRow("", "signin", "/account/login?handler=SignIn")]
    [DataRow("?flow=signin", "signin", "/account/login?handler=SignIn")]
    [DataRow("?flow=register", "register", "/account/login?handler=Register")]
    [DataRow("?flow=discord", "discord", "/auth/discord/start")]
    [DataRow("?flow=unexpected", "signin", "/account/login?handler=SignIn")]
    public async Task LoginPage_RendersOneWidgetForTheSelectedFlow(string query, string action, string formAction)
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/account/login" + query);

        Assert.HasCount(1, Regex.Matches(html, "class=\"cf-turnstile\""));
        StringAssert.Contains(html, $"data-action=\"{action}\"");
        var forms = Regex.Matches(html, "<form[^>]*action=\"([^\"]+)\"");
        Assert.HasCount(1, forms);
        Assert.AreEqual(formAction.ToLowerInvariant(), WebUtility.HtmlDecode(forms[0].Groups[1].Value).ToLowerInvariant());
        StringAssert.Contains(html, "name=\"__RequestVerificationToken\"");
        Assert.AreEqual(action != "discord", html.Contains("name=\"Username\""));
        Assert.AreEqual(action == "register", html.Contains("name=\"RegisterPassword\""));
        Assert.AreEqual(action == "signin", html.Contains("name=\"Password\""));
    }

    [TestMethod]
    [DataRow("Development", "1x00000000000000000000AA", true)]
    [DataRow("Production", "0x4AAAAAAF8y76GGmumUX1_K", false)]
    [DataRow("Staging", "0x4AAAAAAF8y76GGmumUX1_K", false)]
    public async Task TestCredentials_AreUsedOnlyInDevelopment(string environment, string siteKey, bool accepted)
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.ExpectedSecret = accepted ? "1x0000000000000000000000000000000AA" : "test-secret";
        factory.Siteverify.RawResponse = """{"success":true,"hostname":"example.com"}""";
        using var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("TURNSTILE_HOSTNAMES", "example.com");
            }
        );
        using var client = configured.CreateClient();
        var html = await client.GetStringAsync("/account/login");
        StringAssert.Contains(html, $"data-sitekey=\"{siteKey}\"");
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        Assert.AreEqual(accepted, (await verifier.VerifyAsync("test-token", "signin", default)).Accepted);
        Assert.AreEqual(1, factory.Siteverify.Calls);
    }

    [TestMethod]
    public async Task Development_DoesNotRequireProductionSettingsButStillValidatesTokens()
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.ExpectedSecret = "1x0000000000000000000000000000000AA";
        factory.Siteverify.RawResponse = """{"success":true,"hostname":"example.com"}""";
        using var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("TURNSTILE_SECRET", "");
                builder.UseSetting("TURNSTILE_HOSTNAMES", "");
            }
        );
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        foreach (var token in new[] { null, "", new string('a', 2049) })
            Assert.IsFalse((await verifier.VerifyAsync(token, "signin", default)).Accepted);
        Assert.AreEqual(0, factory.Siteverify.Calls);
        Assert.IsTrue((await verifier.VerifyAsync("test-token", "signin", default)).Accepted);

        factory.Siteverify.RawResponse = """{"success":false}""";
        Assert.IsFalse((await verifier.VerifyAsync("test-token", "signin", default)).Accepted);
        factory.Siteverify.Exception = new HttpRequestException();
        Assert.IsFalse((await verifier.VerifyAsync("test-token", "signin", default)).Accepted);
    }

    [TestMethod]
    public async Task Verify_AcceptsOnceAndRejectsReplay()
    {
        using var factory = new ArchiveAppFactory();
        using var scope = factory.Services.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<TurnstileVerifier>();
        var token = ArchiveAppFactory.Challenge("register");
        Assert.IsTrue((await verifier.VerifyAsync(token, "register", default)).Accepted);
        Assert.IsFalse((await verifier.VerifyAsync(token, "register", default)).Accepted);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("untrusted.example")]
    [DataRow("archive.example.attacker.test")]
    public async Task Verify_RejectsMissingOrWrongHostname(string? hostname)
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.Hostname = hostname;
        var verifier = factory.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    /// <summary>
    /// Cloudflare authorizes a listed hostname and all of its subdomains, so the widget issues a
    /// token for `dev.nfmw.net` when only `nfmw.net` is listed. An exact match on our side then
    /// refused a perfectly good token: the widget passed and the page still said verification
    /// failed. The server check has to follow the same rule.
    /// </summary>
    [TestMethod]
    [DataRow("archive.example")]
    [DataRow("dev.archive.example")]
    [DataRow("a.b.archive.example")]
    [DataRow("ARCHIVE.EXAMPLE")]
    [DataRow("dev.archive.example.")]
    public async Task Verify_AcceptsAListedHostnameAndItsSubdomains(string hostname)
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.Hostname = hostname;
        var verifier = factory.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsTrue((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    /// <summary>
    /// The exact production shape: the widget lists `nfmw.net`, prod's env listed only `nfmw.net`,
    /// and the dev archive is served from `dev.nfmw.net`. Cloudflare authorizes the subdomain and
    /// issues the token; this check used to refuse it.
    /// </summary>
    [TestMethod]
    public async Task Verify_AcceptsTheDevSubdomainWhenTheRootIsListed()
    {
        using var factory = new ArchiveAppFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("TURNSTILE_HOSTNAMES", "nfmw.net"));
        factory.Siteverify.Hostname = "dev.nfmw.net";
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsTrue((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    [TestMethod]
    [DataRow("signin")]
    [DataRow("")]
    public async Task Verify_RejectsWrongOrMissingAction(string action)
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.Action = action;
        var verifier = factory.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    [TestMethod]
    public async Task Verify_FailsClosedOnErrors()
    {
        using var factory = new ArchiveAppFactory();
        var verifier = factory.Services.GetRequiredService<TurnstileVerifier>();
        foreach (var error in new Exception[] { new HttpRequestException(), new TaskCanceledException() })
        {
            factory.Siteverify.Exception = error;
            var outage = await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default);
            Assert.IsFalse(outage.Accepted);
            // An outage must not read as a challenge the visitor can retry.
            Assert.AreEqual(TurnstileVerifier.UnavailableError, outage.Message);
        }

        factory.Siteverify.Exception = null;
        factory.Siteverify.RawResponse = "not json";
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
        factory.Siteverify.RawResponse = null;
        factory.Siteverify.Status = HttpStatusCode.InternalServerError;
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
        factory.Siteverify.Status = HttpStatusCode.OK;
        factory.Siteverify.Success = false;
        var refused = await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default);
        Assert.IsFalse(refused.Accepted);
        Assert.AreEqual(TurnstileVerifier.Error, refused.Message);
    }

    /// <summary>
    /// A refusal must name the hostname Cloudflare reports, because a mismatch against
    /// <c>TURNSTILE_HOSTNAMES</c> is the one cause the operator can act on and it is invisible
    /// from the visitor's side.
    /// </summary>
    [TestMethod]
    public async Task Verify_NamesTheHostnameItRefused()
    {
        using var factory = new ArchiveAppFactory();
        var logger = new RecordingLoggerProvider();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logger))
        );
        factory.Siteverify.Hostname = "www.nfmw.net";
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("signin"), "signin", default)).Accepted);
        Assert.IsTrue(
            logger.Messages.Any(message => message.Contains("www.nfmw.net") && message.Contains("TURNSTILE_HOSTNAMES")),
            "the refusal must name the hostname and the allowlist: " + string.Join(" | ", logger.Messages)
        );
    }

    /// <summary>
    /// The siteverify refusal code and the address we reported are the only things that say why a
    /// solved token was rejected, and both used to be discarded.
    /// </summary>
    [TestMethod]
    public async Task Verify_SurfacesTheSiteverifyErrorCodeAndReportedAddress()
    {
        using var factory = new ArchiveAppFactory();
        var logger = new RecordingLoggerProvider();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logger))
        );
        factory.Siteverify.RawResponse = """{"success":false,"error-codes":["invalid-input-response"]}""";
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse(
            (await verifier.VerifyAsync(ArchiveAppFactory.Challenge("signin"), "signin", default, "203.0.113.7")).Accepted
        );
        Assert.IsTrue(
            logger.Messages.Any(
                message => message.Contains("invalid-input-response") && message.Contains("203.0.113.7")
            ),
            "the refusal reason and the reported address must reach the log: " + string.Join(" | ", logger.Messages)
        );
    }

    /// <summary>
    /// A bad or missing secret makes Siteverify answer 400 with an error-codes body. That is our
    /// fault, so it must read as an outage and name the code rather than blaming the visitor.
    /// </summary>
    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest, "invalid-input-secret")]
    [DataRow(HttpStatusCode.OK, "invalid-input-secret")]
    public async Task Verify_TreatsOurOwnSiteverifyErrorAsAnOutage(HttpStatusCode status, string code)
    {
        using var factory = new ArchiveAppFactory();
        var logger = new RecordingLoggerProvider();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logger))
        );
        factory.Siteverify.Status = status;
        factory.Siteverify.RawResponse = $$"""{"success":false,"error-codes":["{{code}}"]}""";
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        var outcome = await verifier.VerifyAsync(ArchiveAppFactory.Challenge("signin"), "signin", default);
        Assert.IsFalse(outcome.Accepted);
        Assert.AreEqual(TurnstileVerifier.UnavailableError, outcome.Message);
        Assert.IsTrue(
            logger.Messages.Any(message => message.Contains(code)),
            "the code must reach the log: " + string.Join(" | ", logger.Messages)
        );
    }

    [TestMethod]
    [DataRow("TURNSTILE_SECRET", "")]
    [DataRow("TURNSTILE_HOSTNAMES", "")]
    [DataRow("TURNSTILE_HOSTNAMES", "localhost")]
    [DataRow("TURNSTILE_HOSTNAMES", "localhost.")]
    [DataRow("TURNSTILE_HOSTNAMES", "127.0.0.1")]
    [DataRow("TURNSTILE_HOSTNAMES", "::1")]
    public async Task Verify_RequiresConfigurationAndRejectsProductionLocalhost(string key, string value)
    {
        using var factory = new ArchiveAppFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(key, value);
            }
        );
        if (key == "TURNSTILE_HOSTNAMES" && value.Length > 0)
            factory.Siteverify.Hostname = value;
        var verifier = configured.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    [TestMethod]
    public async Task Verify_RejectsAbsentAction()
    {
        using var factory = new ArchiveAppFactory();
        factory.Siteverify.RawResponse = """{"success":true,"hostname":"archive.example"}""";
        var verifier = factory.Services.GetRequiredService<TurnstileVerifier>();
        Assert.IsFalse((await verifier.VerifyAsync(ArchiveAppFactory.Challenge("register"), "register", default)).Accepted);
    }

    [TestMethod]
    public async Task RegisterApi_RejectsMissingOversizedWrongActionAndReplayedTokens()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        Assert.AreEqual(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(
                "/auth/register",
                new { Username = "someone", Password = "password123" }
            )).StatusCode
        );
        foreach (var token in new[] { "", new string('a', 2049), ArchiveAppFactory.Challenge("signin") })
        {
            var form = ArchiveAppFactory.Registration("someone");
            form["cf-turnstile-response"] = token;
            Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/register", form)).StatusCode);
        }

        var registration = ArchiveAppFactory.Registration("someone");
        Assert.AreEqual(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/register", registration)).StatusCode);
        registration["Username"] = "another";
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/register", registration)).StatusCode);
    }

    [TestMethod]
    [DataRow("SignIn")]
    [DataRow("Register")]
    public async Task WebForms_RejectMissingChallenge(string handler)
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync("/account/login");
        var antiforgery = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var response = await client.PostAsync(
            "/account/login?handler=" + handler,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["Username"] = "someone", ["Password"] = "password123", ["RegisterPassword"] = "password123",
                    ["__RequestVerificationToken"] = antiforgery
                }
            )
        );
        var errorPage = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(errorPage, TurnstileVerifier.Error);
        Assert.HasCount(1, Regex.Matches(errorPage, "class=\"cf-turnstile\""));
        StringAssert.Contains(errorPage, $"data-action=\"{handler.ToLowerInvariant()}\"");
        Assert.IsFalse(
            response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("nfmw_session="))
        );
        Assert.AreEqual(0, factory.Siteverify.Calls);
    }

    /// <summary>
    /// An archive deployed without the challenge settings refuses every sign-in with the same
    /// message a failed challenge shows. The startup log has to name the missing variable, or the
    /// operator sees "human verification failed" in the logs with nothing pointing at the cause.
    /// </summary>
    [TestMethod]
    [DataRow("TURNSTILE_SECRET", "TURNSTILE_SECRET is not set")]
    [DataRow("TURNSTILE_HOSTNAMES", "TURNSTILE_HOSTNAMES is not set")]
    public async Task Startup_WarnsWhenTheChallengeIsMisconfigured(string key, string expected)
    {
        using var factory = new ArchiveAppFactory();
        var logger = new RecordingLoggerProvider();
        using var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(key, "");
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logger));
            }
        );
        using var client = configured.CreateClient();
        await client.GetStringAsync("/health");

        Assert.IsTrue(
            logger.Messages.Any(message => message.Contains(expected)),
            $"startup must name {expected}: " + string.Join(" | ", logger.Messages)
        );
    }

    [TestMethod]
    public async Task Development_IsNotReportedAsMisconfigured()
    {
        using var factory = new ArchiveAppFactory();
        var logger = new RecordingLoggerProvider();
        using var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("TURNSTILE_SECRET", "");
                builder.UseSetting("TURNSTILE_HOSTNAMES", "");
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logger));
            }
        );
        using var client = configured.CreateClient();
        await client.GetStringAsync("/health");

        Assert.IsFalse(logger.Messages.Any(message => message.Contains("TURNSTILE_")));
    }
}

/// <summary>Collects formatted log messages so a test can assert a reason reached the log.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public List<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
            => messages.Add(formatter(state, exception));
    }
}
