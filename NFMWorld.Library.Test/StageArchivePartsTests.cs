using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

[TestClass]
public class StageArchivePartsTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void ReferencedArchiveModels_FindsPlacementsAndDeduplicates()
    {
        var references = StageLoader.ReferencedArchiveModels(
            [
                "set(archive/" + Hash + ",0,0,0)",
                "fset(archive/" + Hash + ",10,20,30,0)",
                "fix(archive/" + Hash.ToUpperInvariant() + ",0,0,0,0)",
                "chk(nfmm/checkpoint1,0,0,0)"
            ]
        );

        Assert.AreEqual(1, references.Count);
        Assert.AreEqual("archive/" + Hash, references[0]);
    }

    [TestMethod]
    public void ReferencedArchiveModels_IgnoresIncompleteAndInvalidReferences()
    {
        var references = StageLoader.ReferencedArchiveModels(
            [
                "set(archive/" + Hash[..40] + ",0,0,0)",
                "set(archive/not-a-hash,0,0,0)",
                "set(user/my part,0,0,0)"
            ]
        );

        Assert.AreEqual(0, references.Count);
    }

    [TestMethod]
    public void ReferencedArchiveModels_IgnoresNonPlacementLines()
    {
        // A briefing or a comment must not make the client download anything.
        var references = StageLoader.ReferencedArchiveModels(
            [
                "desc(archive/" + Hash + ")",
                "# archive/" + Hash,
                "// set(archive/" + Hash + ",0,0,0)"
            ]
        );

        Assert.AreEqual(0, references.Count);
    }

    [TestMethod]
    public void ReferencedArchiveModels_FindsEveryDistinctPart()
    {
        var second = new string('a', 64);
        var references = StageLoader.ReferencedArchiveModels(
            [
                "set(archive/" + Hash + ",0,0,0)",
                "fset(archive/" + second + ",0,0,0,0)"
            ]
        );

        CollectionAssert.AreEqual(new[] { "archive/" + Hash, "archive/" + second }, references.ToArray());
    }
}
