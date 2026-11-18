using Microsoft.EntityFrameworkCore;
using NFMWorld.Archive.Auth;
using NFMWorld.Archive.Data;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Archive.Moderation;

/// <summary>
/// Command-line role administration, so a fresh deployment can create its first admin before any
/// web UI exists. Runs against the same database file and exits without starting the server.
/// </summary>
public static class RoleCommand
{
    /// <summary>Accepted: <c>--set-role &lt;username&gt; &lt;role&gt;</c> or <c>--list-roles</c>.</summary>
    public static bool TryParse(string[] args, out (string? Username, string? Role) parsed)
    {
        parsed = default;
        if (args.Length == 0)
            return false;

        if (args.Length == 1 && args[0] == "--list-roles")
        {
            parsed = (null, null);
            return true;
        }

        if (args.Length == 3 && args[0] == "--set-role")
        {
            parsed = (args[1], args[2]);
            return true;
        }

        return false;
    }

    public static async Task<int> RunAsync(
        (string? Username, string? Role) args,
        ArchiveOptions options,
        ILogger logger,
        CancellationToken ct
    )
    {
        var dbOptions = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(options.Database)
            .Options;
        await using var db = new ArchiveDbContext(dbOptions);

        await db.Database.MigrateAsync(ct);

        if (args.Username is null)
            return await ListRolesAsync(db, ct);

        if (!Enum.TryParse<UserRole>(args.Role, ignoreCase: true, out var role))
        {
            Console.WriteLine(
                "Unknown role. Accepted roles: " + string.Join(", ", Enum.GetNames<UserRole>()) + "."
            );
            return 2;
        }

        var normalized = UsernamePolicy.Normalize(args.Username);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized, ct);
        if (user is null)
        {
            Console.WriteLine("no such account");
            return 1;
        }

        user.Role = role;
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"{user.Username} -> {role}");
        return 0;
    }

    private static async Task<int> ListRolesAsync(ArchiveDbContext db, CancellationToken ct)
    {
        var rows = await db.Users
            .OrderBy(u => u.Id)
            .Select(u => new { u.Username, u.Role })
            .ToListAsync(ct);
        foreach (var row in rows)
            Console.WriteLine($"{row.Username} -> {row.Role}");
        return 0;
    }
}
