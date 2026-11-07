using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NFMWorld.Archive.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> construct the context without booting the app,
/// which would run startup work against a real database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ArchiveDbContext>
{
    public ArchiveDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite("Data Source=archive.db")
            .Options;
        return new ArchiveDbContext(options);
    }
}
