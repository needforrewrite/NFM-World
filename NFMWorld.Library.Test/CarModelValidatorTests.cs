using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// The publish gate for a car. The garage only asks for four wheels and geometry; an upload is
/// driven by strangers, so the values have to hold up as well.
/// </summary>
[TestClass, DoNotParallelize]
public class CarModelValidatorTests
{
    /// <summary>
    /// Every shipped car must pass, or publishing would refuse content the game itself drives.
    /// </summary>
    [TestInitialize]
    public void MountData() => TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

    [TestMethod]
    public void EveryShippedCar_IsPublishable()
    {
        var checkedCars = 0;
        foreach (var directory in new[] { "nfmm", "world", "elo", "football" })
        foreach (var path in TheVFS.VFS.EnumerateFiles($"data/models/{directory}/cars", "*.rad"))
        {
            var name = path[(path.LastIndexOf('/') + 1)..^".rad".Length];
            var rad = RadParser.ParseRad(TheVFS.VFS.ReadAllText(path), name);
            checkedCars++;
            Assert.IsNull(CarModelValidator.Validate(rad), $"{path} was refused: {CarModelValidator.Validate(rad)}");
        }

        Assert.IsTrue(checkedCars > 50, $"Only {checkedCars} cars were checked.");
    }

    [TestMethod]
    public void ShippedCar_IsPublishable()
    {
        var rad = RadParser.ParseRad(
            TheVFS.VFS.ReadAllText("data/models/nfmm/cars/radicalone.rad"),
            "nfmm/radicalone"
        );
        Assert.IsNull(CarModelValidator.Validate(rad));
    }

    [TestMethod]
    public void ModelWithoutFourWheels_IsRefused()
    {
        var rad = Car(wheels: "w(-42,2,72,11,17,12)\nw(42,2,72,11,-17,12)\nw(-42,2,-84,0,17,12)");
        Assert.AreEqual("A car needs at least four wheels.", CarModelValidator.Validate(rad));
        Assert.IsFalse(BackendCar.IsDrivable(rad));
    }

    [TestMethod]
    public void ModelWithoutPolygons_IsRefused()
    {
        var rad = Car();
        rad.Polys = [];
        Assert.AreEqual("The model has no polygons.", CarModelValidator.Validate(rad));
        Assert.IsFalse(BackendCar.IsDrivable(rad));
    }

    [TestMethod]
    public void MissingStats_AreRefusedRatherThanSilentlyDefaulted()
    {
        // Without grip the physics divides by a fallback set the author never saw.
        var rad = Car(("grip", null));
        StringAssert.Contains(CarModelValidator.Validate(rad), "missing its Grip stat");
    }

    [TestMethod]
    [DataRow("grip(0)")]
    [DataRow("grip(-5)")]
    [DataRow("simag(0)")]
    [DataRow("simag(-1)")]
    [DataRow("powerloss(0)")]
    [DataRow("swits(80,200,0)")]
    [DataRow("maxmag(0)")]
    [DataRow("turn(-1)")]
    [DataRow("bounce(-1)")]
    public void ValuesThePhysicsCannotUse_AreRefused(string stat)
    {
        var name = stat[..stat.IndexOf('(')];
        Assert.IsNotNull(CarModelValidator.Validate(Car((name, stat))), $"{stat} was accepted.");
    }

    [TestMethod]
    [DataRow("grip(2000000000)")]
    [DataRow("moment(2000000000)")]
    public void ValuesThatWrapTheFixedPoint_AreRefused(string stat)
    {
        var name = stat[..stat.IndexOf('(')];
        Assert.IsNotNull(CarModelValidator.Validate(Car((name, stat))), $"{stat} was accepted.");
    }

    [TestMethod]
    public void TriangleIndexOutsideThePolygon_IsRefused()
    {
        var rad = Car();
        rad.Polys[0] = rad.Polys[0] with { Triangles = [0, 1, 99] };
        StringAssert.Contains(CarModelValidator.Validate(rad), "triangle pointing outside itself");
    }

    [TestMethod]
    public void IncompleteTriangle_IsRefused()
    {
        var rad = Car();
        rad.Polys[0] = rad.Polys[0] with { Triangles = [0, 1] };
        StringAssert.Contains(CarModelValidator.Validate(rad), "incomplete triangle");
    }

    [TestMethod]
    public void NaNVertex_IsRefused()
    {
        var rad = Car();
        var poly = rad.Polys[0];
        var points = poly.Points.ToArray();
        points[1] = points[1] with { X = float.NaN };
        rad.Polys[0] = poly with { Points = points };
        StringAssert.Contains(CarModelValidator.Validate(rad), "not a number");
    }

    /// <summary>
    /// The garage's own gate and the publish gate must agree on structure, or a car could be
    /// offered in the picker and then refused on upload.
    /// </summary>
    [TestMethod]
    public void StructuralGate_MatchesTheGarageGate()
    {
        var healthy = Car();
        Assert.IsTrue(BackendCar.IsDrivable(healthy));
        Assert.IsNull(CarModelValidator.Validate(healthy));

        var wheelsOnly = Car();
        wheelsOnly.Polys = [];
        Assert.IsFalse(BackendCar.IsDrivable(wheelsOnly));
        Assert.IsNotNull(CarModelValidator.Validate(wheelsOnly));
    }

    private static readonly string[] StatLines =
    [
        "swits(80,200,300)", "acelf(11.0,7.5,4.0)", "handb(7)", "airs(1.3)", "airc(100)", "turn(7)", "grip(25.0)",
        "bounce(1.1)", "simag(0.9)", "moment(1.5)", "comprad(0.5)", "push(2)", "revpush(2)", "lift(30)", "revlift(0)",
        "powerloss(3000000)", "flipy(-63)", "msquash(3)", "clrad(5500)", "dammult(0.38)", "maxmag(5800)",
        "dishandle(1.0)", "outdam(0.85)", "name(Test Car)"
    ];

    private static readonly string WheelLines =
        "w(-42,2,72,11,17,12)\nw(42,2,72,11,-17,12)\nw(-42,2,-84,0,17,12)\nw(42,2,-84,0,-17,12)";

    /// <summary>
    /// A minimal but complete car, with the requested stat lines replaced or (for a null value)
    /// dropped.
    /// </summary>
    private static Rad3d Car(params (string Name, string? Line)[] overrides)
        => Car(WheelLines, overrides);

    private static Rad3d Car(string wheels, params (string Name, string? Line)[] overrides)
    {
        var lines = new List<string>();
        foreach (var statLine in StatLines)
        {
            var name = statLine[..statLine.IndexOf('(')];
            if (overrides.FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Name: not null } o)
            {
                if (o.Line is not null)
                    lines.Add(o.Line);
                continue;
            }

            lines.Add(statLine);
        }

        var text = string.Join('\n', lines)
                   + "\n<p>\nc(120,120,120)\np(0,0,0)\np(40,0,0)\np(0,0,40)\n</p>\n"
                   + wheels;

        return RadParser.ParseRad(text, "test");
    }
}
