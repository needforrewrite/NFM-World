using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorld.Archive.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Archive").Get<ArchiveOptions>() ?? new ArchiveOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new FileStorage(options.StorageRoot));
builder.Services.AddDbContext<ArchiveDbContext>(o => o.UseSqlite(options.Database));
builder.Services.AddScoped<SessionService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<NonceCache>();
builder.Services.AddSingleton<TrustedServerAuth>();
builder.Services.AddHttpClient();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapAuthEndpoints();
app.MapDiscordEndpoints();
app.MapResultEndpoints();

app.Run();

/// <summary>Entry point marker so integration tests can host the real app.</summary>
public partial class Program;
