using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Auth;

namespace NFMWorld.Archive.Test;

/// <summary>
/// Boots the real app against a throwaway SQLite file, so migrations, cookies and the
/// endpoint pipeline are exercised as deployed rather than mocked out.
/// </summary>
public class ArchiveAppFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nfmw-archive-test", Guid.NewGuid().ToString("N"));

    public FakeSiteverifyHandler Siteverify { get; } = new();

    public static string Challenge(string action) => action + ":" + Guid.NewGuid().ToString("N");

    public static Dictionary<string, string> Registration(string username, string password = "password123") => new()
    {
        ["Username"] = username,
        ["Password"] = password,
        ["cf-turnstile-response"] = Challenge("register")
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_root);

        // Without this the host searches upward for a content root and finds the test project,
        // so appsettings.json never loads and every test shares one real archive.db.
        builder.UseContentRoot(AppContext.BaseDirectory);

        // UseSetting (not just ConfigureAppConfiguration) because the app binds ArchiveOptions at
        // build time, before a later configuration source would be layered on.
        builder.UseSetting("Archive:Database", $"Data Source={Path.Combine(_root, "test.db")}");
        builder.UseSetting("Archive:StorageRoot", Path.Combine(_root, "storage"));
        builder.UseSetting("TURNSTILE_SECRET", "test-secret");
        builder.UseSetting("TURNSTILE_HOSTNAMES", "archive.example");
        builder.ConfigureServices(services =>
            services.AddHttpClient<TurnstileVerifier>().ConfigurePrimaryHttpMessageHandler(() => Siteverify)
        );

        builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Archive:Database"] = $"Data Source={Path.Combine(_root, "test.db")}",
                        ["Archive:StorageRoot"] = Path.Combine(_root, "storage"),
                        ["Archive:ReadOnly"] = "false"
                    }
                );
            }
        );
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
