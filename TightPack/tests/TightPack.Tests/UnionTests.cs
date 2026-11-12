namespace TightPack.Tests;

[TestClass]
public sealed class UnionTests
{
    [TestMethod]
    public void Union_DispatchesOnTheRuntimeType()
    {
        var value = new ShapeHolder
        {
            Shape = new Circle { Radius = 2.5 },
            Shapes = [new Circle { Radius = 1 }, new Square { Side = 3 }],
        };

        ShapeHolder result = TightPackSerializer.Deserialize<ShapeHolder>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.IsInstanceOfType<Circle>(result.Shape);
        Assert.AreEqual(2.5, ((Circle)result.Shape!).Radius);

        var shapes = result.Shapes!;
        Assert.HasCount(2, shapes);
        Assert.IsInstanceOfType<Circle>(shapes[0]);
        Assert.IsInstanceOfType<Square>(shapes[1]);
        Assert.AreEqual(3, ((Square)shapes[1]).Side);
    }

    [TestMethod]
    public void Union_NullStaysNull()
    {
        var value = new ShapeHolder();
        ShapeHolder result = TightPackSerializer.Deserialize<ShapeHolder>(
            TightPackSerializer.Serialize(in value)
        );

        Assert.IsNull(result.Shape);
        Assert.IsNull(result.Shapes);
    }

    [TestMethod]
    public void Union_TopLevelRoundTrips()
    {
        IShape value = new Square { Side = 4 };
        IShape result = TightPackSerializer.Deserialize<IShape>(TightPackSerializer.Serialize(in value));

        Assert.IsInstanceOfType<Square>(result);
        Assert.AreEqual(4, ((Square)result).Side);
    }

    [TestMethod]
    public void Union_UnknownTagThrows()
    {
        // tag 99 is not declared
        byte[] bytes = [99, 0, 0, 0, 0];

        Assert.ThrowsExactly<TightPackSerializationException>(
            () => TightPackSerializer.Deserialize<IShape>(bytes)
        );
    }
}
