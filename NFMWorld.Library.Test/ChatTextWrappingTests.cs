using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.Library.Test;

[TestClass]
[DoNotParallelize]
public class ChatTextWrappingTests
{
    [TestMethod]
    public void LongUnbrokenChatMessage_FitsEachLineWithinTheChatWidth()
    {
        IBackend.Backend = new StageSelectViewTests.MetricsBackend();
        using var mounted = Renderer.Mount(() => Sx.Text(
            style: () => Sx.TextStyles(
                fontSize: 13,
                breakType: BreakType.Character,
                overflowBehavior: OverflowBehavior.ContinueVertically
            ),
            layout: () => Sx.Styles(width: 140),
            text: () => "Racer: " + new string('f', 120)
        )
        );
        var text = (Text)mounted.Root;
        text.LayoutAndRender(new Vector2(140, 600));
        var measured = text.LaidOutComplexText!.Value;
        Assert.IsGreaterThan(2, measured.Elements.Count);
        Assert.IsTrue(measured.Elements.All(line => line.Size.X <= 140.5f));
        Assert.AreEqual(120, string.Concat(measured.Elements.Select(line => line.Text)).Count(ch => ch == 'f'));
    }
}
