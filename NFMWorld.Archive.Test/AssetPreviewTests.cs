using Microsoft.Extensions.DependencyInjection;
using NFMWorld.Archive.Data;

namespace NFMWorld.Archive.Test;

/// <summary>
/// The website's entry point into the browser build. The link only exists when the archive is
/// configured with a preview host, and only for a kind that has a 3D view.
/// </summary>
[TestClass]
public class AssetPreviewTests
{
    private const string GameHost = "https://game.example";

    private static string Hash(char c) => new(c, 64);

    private static Asset NewAsset(string name, AssetType type, string hash) => new()
    {
        ReviewStatus = AssetReviewStatus.Legacy,
        Type = type,
        Name = name,
        ContentHash = hash,
        StoragePath = $"{hash[..2]}/{hash}",
        SizeBytes = 10,
        IsPublic = true,
        IsValidated = true
    };

    private static async Task<Dictionary<string, int>> SeedAsync(ArchiveAppFactory factory, params Asset[] assets)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArchiveDbContext>();
        db.Users.Add(new User { Username = "artist", UsernameNormalized = "artist" });
        await db.SaveChangesAsync();

        var owner = db.Users.Single();
        foreach (var asset in assets)
        {
            asset.OwnerId ??= owner.Id;
            db.Assets.Add(asset);
        }

        await db.SaveChangesAsync();
        return assets.ToDictionary(a => a.Name, a => a.Id);
    }

    private static async Task<string> PageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [TestMethod]
    public async Task AssetPage_LinksThePreviewForAStage()
    {
        using var factory = new ArchiveAppFactory { GamePreviewUrl = GameHost };
        using var client = factory.CreateClient();
        var ids = await SeedAsync(factory, NewAsset("course", AssetType.Stage, Hash('a')));

        var html = await PageAsync(client, $"/asset/{ids["course"]}");

        StringAssert.Contains(
            html,
            $"{GameHost}/index.html?hash={Hash('a')}&amp;preview=stage&amp;archive="
        );
        StringAssert.Contains(html, "Preview in 3D");
    }

    [TestMethod]
    public async Task AssetPage_LinksTheModelViewForAModelKind()
    {
        using var factory = new ArchiveAppFactory { GamePreviewUrl = GameHost };
        using var client = factory.CreateClient();
        var ids = await SeedAsync(factory, NewAsset("hot rod", AssetType.Car, Hash('b')));

        var html = await PageAsync(client, $"/asset/{ids["hot rod"]}");

        StringAssert.Contains(html, $"hash={Hash('b')}&amp;preview=model&amp;archive=");
    }

    [TestMethod]
    public async Task AssetPage_OmitsThePreviewWhenNoHostIsConfigured()
    {
        using var factory = new ArchiveAppFactory();
        using var client = factory.CreateClient();
        var ids = await SeedAsync(factory, NewAsset("course", AssetType.Stage, Hash('c')));

        var html = await PageAsync(client, $"/asset/{ids["course"]}");

        Assert.IsFalse(html.Contains("Preview in 3D"), html);
    }

    [TestMethod]
    public async Task AssetPage_OmitsThePreviewForAKindWithNo3DView()
    {
        using var factory = new ArchiveAppFactory { GamePreviewUrl = GameHost };
        using var client = factory.CreateClient();
        var ids = await SeedAsync(
            factory,
            NewAsset("road", AssetType.Texture, Hash('d')),
            NewAsset("run", AssetType.Replay, Hash('e'))
        );

        foreach (var name in new[] { "road", "run" })
        {
            var html = await PageAsync(client, $"/asset/{ids[name]}");
            Assert.IsFalse(html.Contains("Preview in 3D"), html);
        }
    }

    [TestMethod]
    public async Task Browse_LinksOnlyThePreviewableRows()
    {
        using var factory = new ArchiveAppFactory { GamePreviewUrl = GameHost };
        using var client = factory.CreateClient();
        await SeedAsync(
            factory,
            NewAsset("course", AssetType.Stage, Hash('f')),
            NewAsset("road", AssetType.Texture, Hash('9'))
        );

        var html = await PageAsync(client, "/browse");

        StringAssert.Contains(html, "title=\"Preview in 3D\"");
        StringAssert.Contains(html, $"{GameHost}/index.html?hash={Hash('f')}");
        Assert.AreEqual(
            1,
            html.Split($"{GameHost}/index.html").Length - 1,
            "only the stage row carries a preview link"
        );
    }

    [TestMethod]
    public void Url_NamesThisArchiveOnTheGameOrigin()
    {
        var options = new ArchiveOptions
        {
            GamePreviewUrl = GameHost + "/",
            PublicBaseUrl = "https://assets.example"
        };

        Assert.AreEqual(
            $"{GameHost}/index.html?hash={Hash('a')}&preview=stage&archive=https%3A%2F%2Fassets.example",
            AssetPreview.Url(options, AssetType.Stage, Hash('a'))
        );
        Assert.IsNull(AssetPreview.Url(options, AssetType.Texture, Hash('a')));
        Assert.IsNull(AssetPreview.Url(new ArchiveOptions(), AssetType.Stage, Hash('a')));
    }
}
