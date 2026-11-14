using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;
using NFMWorld.Archive.Validation;
using NFMWorldLibrary.Multiplayer;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Archive").Get<ArchiveOptions>() ?? new ArchiveOptions();

// Resolve configured validator paths against the content root, so a relative Development path
// does not depend on the directory the process happened to start in.
if (!string.IsNullOrWhiteSpace(options.ValidatorPath))
    options.ValidatorPath = Path.GetFullPath(options.ValidatorPath, builder.Environment.ContentRootPath);
if (!string.IsNullOrWhiteSpace(options.ValidatorWorkingDirectory))
{
    options.ValidatorWorkingDirectory = Path.GetFullPath(
        options.ValidatorWorkingDirectory,
        builder.Environment.ContentRootPath
    );
}

// Development can use the validator built beside this project; a deployed archive must still set
// Archive:ValidatorPath explicitly, so an empty path keeps uploads refused there.
if (string.IsNullOrWhiteSpace(options.ValidatorPath)
    && builder.Environment.IsDevelopment()
    && ValidatorLocator.FindBesideRepository(builder.Environment.ContentRootPath) is { } localValidator)
{
    options.ValidatorPath = localValidator;
    options.ValidatorWorkingDirectory = Path.GetDirectoryName(localValidator);
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new FileStorage(options.StorageRoot));
builder.Services.AddDbContext<ArchiveDbContext>(o => o.UseSqlite(options.Database));
builder.Services.AddScoped<SessionService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<NonceCache>();
builder.Services.AddSingleton<TrustedServerAuth>();
builder.Services.AddSingleton<ValidatorRunner>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<TurnstileVerifier>(client => client.Timeout = TimeSpan.FromSeconds(10));
// Pages carry a form that signs someone in, so the antiforgery token is validated, not just
// rendered. The JSON endpoints are not covered by it (they are token/bearer based).
builder.Services.AddRazorPages(options =>
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute())
);

var app = builder.Build();

if (string.IsNullOrWhiteSpace(options.ValidatorPath) || !File.Exists(options.ValidatorPath))
{
    app.Logger.LogWarning(
        "No archive validator configured: uploads and time trials will be refused. "
        + "Set Archive__ValidatorPath to the built NFMWorld.Archive.Validator executable."
    );
}

// A misconfigured challenge refuses every visitor with the same message a failed challenge
// shows, so it has to be visible in the startup log rather than only at the login form.
if (TurnstileVerifier.FindConfigurationProblem(builder.Configuration, app.Environment) is { } turnstileProblem)
{
    app.Logger.LogWarning(
        "Turnstile is not configured ({Problem}): every web sign-in, registration and Discord "
        + "sign-in will be refused with \"{Error}\".",
        turnstileProblem,
        TurnstileVerifier.Error
    );
}

// TLS terminates at the proxy in the documented deployment, so without this every request
// looks like local plain HTTP: the login throttle would share one global IP bucket (locking
// everyone out on one attacker's attempts) and the session cookie would be issued without
// Secure. KnownNetworks/Proxies are cleared because the proxy is the only thing in front.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    db.Database.Migrate();

    if (!string.IsNullOrWhiteSpace(options.StockContentPath))
    {
        var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StockSeeder");
        await StockSeeder.SeedAsync(db, storage, options.StockContentPath, logger, CancellationToken.None);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Form logout for the website, which has no JavaScript to call the JSON endpoint.
app.MapPost(
    "/logout",
    async (SessionService sessions, HttpContext http, CancellationToken ct) =>
    {
        if (http.Request.Cookies["nfmw_session"] is { Length: > 0 } cookie)
            await sessions.RevokeAsync(cookie, ct);

        http.Response.Cookies.Delete("nfmw_session");
        return Results.Redirect("/account/login");
    }
);
app.MapAuthEndpoints();
app.MapDiscordEndpoints();
app.MapResultEndpoints();
app.MapTimeTrialEndpoints();
app.MapAssetEndpoints();
app.MapRazorPages();

// WAL is a per-database setting, so it is applied once at startup rather than per connection.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
}

app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        // Assets are meant to be downloaded, and a same-origin HTML/SVG payload would otherwise
        // run as script; nosniff keeps a browser from second-guessing the declared type.
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' https://challenges.cloudflare.com; frame-src https://challenges.cloudflare.com; img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
        await next();
    }
);

app.UseStaticFiles();

app.Run();

/// <summary>Entry point marker so integration tests can host the real app.</summary>
public partial class Program;
