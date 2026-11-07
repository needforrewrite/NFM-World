using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive;
using NFMWorld.Archive.Data;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Archive").Get<ArchiveOptions>() ?? new ArchiveOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new FileStorage(options.StorageRoot));
builder.Services.AddDbContext<ArchiveDbContext>(o => o.UseSqlite(options.Database));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
