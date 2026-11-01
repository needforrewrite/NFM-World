using System.Reflection;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass]
public class BuildInformationTests
{
    [TestMethod]
    public void BuildNumberCountsCalendarDaysFromTheEpoch()
    {
        Assert.AreEqual(0, BuildInformation.CalculateBuildNumber(new DateTimeOffset(2025, 11, 26, 23, 59, 59, TimeSpan.Zero)));
        Assert.AreEqual(1, BuildInformation.CalculateBuildNumber(new DateTimeOffset(2025, 11, 27, 0, 0, 0, TimeSpan.Zero)));
        Assert.AreEqual(339, BuildInformation.CalculateBuildNumber(new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero)));
    }

    [TestMethod]
    public void BuildNumberUsesUtcRegardlessOfTheLocalDate()
    {
        var timestamp = new DateTimeOffset(2025, 11, 27, 1, 0, 0, TimeSpan.FromHours(2));
        Assert.AreEqual(0, BuildInformation.CalculateBuildNumber(timestamp));
    }

    [TestMethod]
    public void MissingMetadataDoesNotInventABuildDate()
    {
        var build = BuildInformation.Read(Assembly.GetExecutingAssembly());
        Assert.IsNull(build.BuiltAtUtc);
        Assert.IsNull(build.BuildNumber);
        Assert.AreEqual("unknown", build.Branch);
    }
}
