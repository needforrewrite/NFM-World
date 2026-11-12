namespace TightPack.Tests;

[TestClass]
public sealed class CircularReferenceTests
{
    [TestMethod]
    public void CircularReference_RoundTripsWithoutATail()
    {
        var value = new Node { Value = 1, Next = new Node { Value = 2 } };
        Node result = TightPackSerializer.Deserialize<Node>(TightPackSerializer.Serialize(in value));

        Assert.AreEqual(1, result.Value);
        Assert.AreEqual(2, result.Next!.Value);
        Assert.IsNull(result.Next.Next);
    }

    [TestMethod]
    public void CircularReference_PreservesACycle()
    {
        var first = new TreeNode { Name = "root" };
        var second = new TreeNode { Name = "child" };
        first.Left = second;
        second.Right = first;

        TreeNode result = TightPackSerializer.Deserialize<TreeNode>(TightPackSerializer.Serialize(in first));

        Assert.AreEqual("root", result.Name);
        Assert.AreEqual("child", result.Left!.Name);

        // The back edge must resolve to the same instance, not a copy.
        Assert.AreSame(result, result.Left.Right);
    }

    [TestMethod]
    public void CircularReference_SharesARepeatedInstance()
    {
        var shared = new TreeNode { Name = "shared" };
        var root = new TreeNode { Name = "root", Left = shared, Right = shared };

        TreeNode result = TightPackSerializer.Deserialize<TreeNode>(TightPackSerializer.Serialize(in root));

        Assert.AreSame(result.Left, result.Right);
        Assert.AreEqual("shared", result.Left!.Name);
    }

    [TestMethod]
    public void CircularReference_NullRoundTrips()
    {
        var value = new TreeNode { Name = "leaf" };
        TreeNode result = TightPackSerializer.Deserialize<TreeNode>(TightPackSerializer.Serialize(in value));

        Assert.IsNull(result.Left);
        Assert.IsNull(result.Right);
    }

    [TestMethod]
    public void CircularReference_ChainOfDistinctObjectsKeepsIdentity()
    {
        var value = new Node { Value = 0 };
        Node current = value;
        for (int i = 1; i < 10; i++)
        {
            current.Next = new Node { Value = i };
            current = current.Next;
        }

        Node result = TightPackSerializer.Deserialize<Node>(TightPackSerializer.Serialize(in value));

        int expected = 0;
        for (Node? node = result; node is not null; node = node.Next)
        {
            Assert.AreEqual(expected++, node.Value);
        }

        Assert.AreEqual(10, expected);
    }
}
