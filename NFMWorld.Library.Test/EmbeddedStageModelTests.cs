using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.LegacyAssets;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

[TestClass]
public class EmbeddedStageModelTests
{
    private const string RadText =
        "disp(90)\ngrounded(130)\ntracks(1)\n<track>\nc(147,147,147)\nradx(700)\nradz(2800)\nrady(300)\ntx(0)\ntz(0)\nty(0)\n</track>";

    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
            // Sentry is a process-wide singleton and another test class may have initialized it.
        }
    }

    // amodel data is what phy-addons reads back through ZipUtils.inflateBytes: a zlib-wrapped
    // deflate stream, base64-encoded. Java's Deflater writes the zlib wrapper.
    private static string Encode(string rad) => Convert.ToBase64String(ZLib(Encoding.UTF8.GetBytes(rad)));

    [TestMethod]
    public void Amodel2_JoinsContinuationLines()
    {
        var base64 = Encode(RadText);

        var stage = Load(
            $"""
             amodel2(custom,2,)
             //- {base64[..(base64.Length / 2)]}
             //- {base64[(base64.Length / 2)..]}
             set(custom,0,0,0)
             """
        );

        Assert.AreEqual(1, stage.pieces.Count);
        Assert.AreEqual("custom", stage.pieces[0].Object.FileName);
    }

    [TestMethod]
    public void Amodel2_ReadsPhyAddonsPayload()
    {
        // Two `//- ` lines lifted verbatim from elo/survival_of_the_fittest.txt: phy-addons
        // wrote them with java.util.zip.Deflater, so this also proves the zlib wrapper is handled.
        var stage = Load(
            """
            amodel2(custom,2,)
            //- eNrtWtuOm0AMfecr8lQRKVaAEIaVov2XKETbqFESBbbd7NeXe8b2DIwh6lMfaLHDnOPjsWcGtNnptx8GSy875Tf/rbk5ny5HP156eXG9HA/Xc+YvvY/79fOSHTM/3JQP3a/7yukV9/3hV+6r8nZX3797Bz+M1aq9yif32ZevgqC++/ajtL19+Nt0s/SKhw/1zW7dju+BvsqfqoAu1+LP/nz2W6zIhFU7iy8/qf9/+IEFUIoHg4DfbYRE46aHCbcNTI9RlCz1g0YwMRZQMH0W0mDVXj0qElpiKWUMxW0wkNHe7lYP3ZbDmquqmibumx+sqnleQRTF2K7hbpVku6ces1vf3nuSeLtqL50E6BjsaoGtNqeRaQmTlETOPc4kw1o6YJvtnLIeIIzEWpoxMi0dj0CLTjOHZ5sgWGIaxMgy1uIxPnO+XAtZJEJUvrLgxTNATBzocNiDeTfAUJLpQfOqGYrbuf4JumP0zhU5FD9pJmoKi54oYEsCd8g18NVuWIN02Sar9riGCUs210A2G2oKl2uigW2Z3KERrNeLvNjfi8XnbZGfsmP+Almtp/zH4HlF/rpZecJyj07kwlT9gJlqjy6AmzIxZgoUOjMn5Ivkgq4EATPr50UU7erkylA/Po+AzC4xZQTVD4igcujxWmJfr71Z5zTeDUbXaw+FGrLBNaVLgNcwkMbgtrxTTDSkOZg9JXe8XYCUL7flHQOsokdZ5F0DwrYBed+Ac+MA6ZxBArbYW3YvbU+Rbyl2ErzNa/vJhO2ELfVsM8F7iXCdt8PrMVNLniPL6a2Hx5ZrQ4ycb83orlU6dPTXphJbInBt58D1r5W/cdtwixtYYQKrdpNHPLkGIlbxBs+EJgBWpoALn5niRjBQ4OKn5oR82b53PCmIKW4I2xcCG4O4KUDSFSBuC3DrCx24evk4nK/5sedQyaq9LKdn83Ec32tPIgFm8JHVeQTaNXDrAYkY+sOS4G1NNQrvIsB0Uh37hhqIZRgW
            //- t5eSWN9Kh1/IA2kt8TOKiKB+I0cv4vrn95IrarjSuJnbMEDm8NoaMiGD4GEcmLgQ2nN9imeBKwSmZkErHdmyDqnAtEEPweJUDx7mZHnGmcBpxZYwERgY5VTNgHVIL8nu/1L+d6U8iApEP5BkW9/fXdMNJCWMkGQcpHmxESiMp2bCK4RufcdVMliS9pGvi8Kck4yQDBNTmhACjtOrZkG7pVrLdL1JVn+XkC9+LA774viodsus/eMFC2dZAjVa9NyCkQ2tw2ZHKTszjTD0KescaYwQiTmeNkoAjAEIJrPlJOFbgjmibYQTQ+xmgEwH5QAKyh06zXMDj4KkSmQZxJPpfPr4Wfg1fqLa4e2Xxd6GzrGxOegr4zATIaI8lMbAIhQUhgmRFAYpkWDwhMk0WR0dY2NkRi6hOODqgIsxuqSknJNTcsYZhE9JUbckaZ6ACDJ50mlT2NExNkZm5JJOIVcHXIzZlU6dQiYRmCSjRyP0/gKCauUq
            set(custom,0,0,0)
            """
        );

        Assert.AreEqual(1, stage.pieces.Count);
        Assert.AreEqual("custom", stage.pieces[0].Object.FileName);
    }

    [TestMethod]
    public void Amodel_WithGarbagePayload_IsSkipped()
    {
        // A broken embedded model must not take the rest of the stage with it.
        var stage = Load("amodel(broken,not-base64!,)\nname(after)");

        Assert.AreEqual("after", stage.Name);
        Assert.AreEqual(0, stage.pieces.Count);
    }

    [TestMethod]
    public void Amodel_WithGarbageRad_IsSkipped()
    {
        var stage = Load($"amodel(broken,{Encode("track bogus(")},)\nname(after)");

        Assert.AreEqual("after", stage.Name);
    }

    [TestMethod]
    public void Amodel_WithSourceUrl_NeverFetches()
    {
        // The third argument is a URL phy-addons would download; a stage file must never make the
        // game contact a host, so the label stays unresolved and the piece falls back to the error mesh.
        BackendGameSparker.error_mesh = new Rad3d([], CarStats.Default, [], null, [], [], false, [], "error.rad");
        var stage = Load("amodel(remote,,http://example.com/evil.rad)\nset(remote,0,0,0)");

        Assert.AreEqual(1, stage.pieces.Count);
        Assert.AreEqual("error.rad", stage.pieces[0].Object.FileName);
    }

    private static StageLoader Load(string stageText, [CallerMemberName] string testName = "")
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-embedded-model", Guid.NewGuid().ToString("N"));
        var stages = Path.Combine(root, "data", "stages");
        Directory.CreateDirectory(stages);
        File.WriteAllText(Path.Combine(stages, testName + ".txt"), stageText);

        // VFS mounts accumulate across the whole run and the first mount wins, so each test needs
        // its own stage name (the caller's) to land on its own unmounted directory.
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(root);
        return new StageLoader(testName);
    }

    private static byte[] ZLib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var stream = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            stream.Write(data);
        return output.ToArray();
    }
}
