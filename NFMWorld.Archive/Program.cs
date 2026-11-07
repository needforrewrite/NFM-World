using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;
using NFMWorld.Archive.Validation;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Archive").Get<ArchiveOptions>() ?? new ArchiveOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new FileStorage(options.StorageRoot));
builder.Services.AddDbContext<ArchiveDbContext>(o => o.UseSqlite(options.Database));
builder.Services.AddScoped<SessionService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<NonceCache>();
builder.Services.AddSingleton<TrustedServerAuth>();
builder.Services.AddSingleton<ValidatorRunner>();
builder.Services.AddHttpClient();
// Pages carry a form that signs someone in, so the antiforgery token is validated, not just
// rendered. The JSON endpoints are not covered by it (they are token/bearer based).
builder.Services.AddRazorPages(options =>
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Form logout for the website, which has no JavaScript to call the JSON endpoint.
app.MapPost("/logout", async (SessionService sessions, HttpContext http, CancellationToken ct) =>
{
    if (http.Request.Cookies["nfmw_session"] is { Length: > 0 } cookie)
        await sessions.RevokeAsync(cookie, ct);

    http.Response.Cookies.Delete("nfmw_session");
    return Results.Redirect("/account/login");
});
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
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
    await next();
});

app.Run();

/// <summary>Entry point marker so integration tests can host the real app.</summary>
public partial class Program;
