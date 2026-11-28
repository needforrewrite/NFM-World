using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;
using NFMWorld.Archive.Validation;
using NFMWorld.Archive.Moderation;
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
builder.Services.AddScoped<NFMWorld.Archive.Moderation.ModerationService>();
builder.Services.AddHostedService<NFMWorld.Archive.Moderation.DeletionSweeper>();
builder.Services.AddSingleton(new FileStorage(options.StorageRoot));
builder.Services.AddDbContext<ArchiveDbContext>(o => o.UseSqlite(options.Database));
builder.Services.AddScoped<SessionService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<NonceCache>();
builder.Services.AddSingleton<TrustedServerAuth>();
builder.Services.AddSingleton<ValidatorRunner>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<TurnstileVerifier>(client => client.Timeout = TimeSpan.FromSeconds(10));
// Edge purges are opt-in; the client only makes a request when a zone and token are configured.
builder.Services.AddHttpClient<NFMWorld.Archive.Cache.CloudflareCache>(
    client => client.Timeout = TimeSpan.FromSeconds(10)
);
// Pages carry a form that signs someone in, so the antiforgery token is validated, not just
// rendered. The JSON endpoints are not covered by it (they are token/bearer based).
builder.Services.AddRazorPages(options =>
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute())
);

// One policy, two kinds of caller: the standalone preview page, which fetches one asset by hash,
// and the browser build of the game, which is served from play.nfmw.net while the archive is
// nfmw.net, so its sign-in, asset search, uploads and time-trial submission are all cross-origin.
// Applied to route groups rather than globally, so the cookie-authenticated website pages stay
// unreadable from another origin.
builder.Services.AddCors(cors =>
    cors.AddPolicy(CorsPolicies.CrossOrigin, policy => ApplyCrossOrigin(policy, options.CorsOrigins))
);

static void ApplyCrossOrigin(CorsPolicyBuilder policy, string? configured)
{
    var origins = (configured ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length == 0 || origins is ["*"])
        policy.AllowAnyOrigin();
    else
        policy.WithOrigins(origins);

    policy.WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Content-Type", "Authorization", "Range", "If-Range")
        .WithExposedHeaders("Content-Length", "Content-Range", "Accept-Ranges", "Content-Disposition");
}

var app = builder.Build();

if (RoleCommand.TryParse(args, out var roleArgs))

    return await RoleCommand.RunAsync(roleArgs, options, app.Logger, CancellationToken.None);

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
    await AssetTags.BackfillAsync(db, CancellationToken.None);

    var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StockAssetCleanup");
    await StockAssetCleanup.RunAsync(db, storage, logger, CancellationToken.None);
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
app.MapModerationEndpoints();
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
        // The asset page frames the browser build's standalone preview, so its origin has to be
        // listed; everything else stays denied.
        var frameSources = "frame-src https://challenges.cloudflare.com";
        if (AssetPreview.FrameOrigin(options) is { } previewOrigin)
            frameSources += " " + previewOrigin;
        headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' https://challenges.cloudflare.com; "
            + frameSources
            + "; img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
        await next();
    }
);

app.UseStaticFiles();

// After the endpoint metadata is known and before it runs; the policy is applied per route, so
// this only affects the routes that ask for it.
app.UseCors();

app.Run();

return 0;

/// <summary>Entry point marker so integration tests can host the real app.</summary>
public partial class Program;
