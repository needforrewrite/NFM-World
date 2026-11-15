using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
public class RaceChatTests
{
    [TestMethod]
    public void Normalize_BoundsMessagesWithoutBreakingSurrogatePairs()
    {
        Assert.AreEqual("", RaceChat.Normalize(null));
        Assert.AreEqual("", RaceChat.Normalize(" \t\n"));
        Assert.AreEqual("hello world", RaceChat.Normalize(" hello\nworld "));
        Assert.AreEqual(RaceChat.MaxMessageLength, RaceChat.Normalize(new string('x', 2000)).Length);
        var text = RaceChat.Normalize(new string('x', RaceChat.MaxMessageLength - 1) + "😀");
        Assert.AreEqual(RaceChat.MaxMessageLength - 1, text.Length);
    }

    [TestMethod]
    public void PacketDispatch_RoundTripsBothChatDirections()
    {
        var request = new C2S_RaceChat { Message = "hello racers" };
        var decodedRequest = MultiplayerUtils.TryDeserializeC2SPacket(request.Opcode, TightPackSerializer.Serialize(request));
        Assert.IsInstanceOfType<C2S_RaceChat>(decodedRequest);
        Assert.AreEqual(request.Message, ((C2S_RaceChat)decodedRequest!).Message);
        var response = new S2C_RaceChat
        {
            SenderId = Guid.NewGuid(), Sender = "(SPEC) Racer", Message = request.Message,
            Color = new Color3(40, 120, 250), MessageId = 42
        };
        var decodedResponse = MultiplayerUtils.TryDeserializeS2CPacket(response.Opcode, TightPackSerializer.Serialize(response));
        Assert.IsInstanceOfType<S2C_RaceChat>(decodedResponse);
        Assert.AreEqual(response.SenderId, ((S2C_RaceChat)decodedResponse!).SenderId);
        Assert.AreEqual(response.Sender, ((S2C_RaceChat)decodedResponse!).Sender);
        Assert.AreEqual(response.Color, ((S2C_RaceChat)decodedResponse!).Color);
        Assert.AreEqual(response.MessageId, ((S2C_RaceChat)decodedResponse!).MessageId);
    }

    [TestMethod]
    public void ReplayedHistoryDoesNotDuplicateMessagesAfterReconnect()
    {
        var chat = new RaceChatState();
        chat.Receive("Racer", "first", default, 1);
        chat.Receive("(SPEC) Viewer", "second", default, 2);
        chat.Receive("Racer", "first", default, 1);
        chat.Receive("(SPEC) Viewer", "second", default, 2);
        chat.Receive("Racer", "third", default, 3);
        Assert.AreEqual(3, chat.Lines.Value.Count);
        Assert.AreEqual("(SPEC) Viewer", chat.Lines.Value[1].Sender);
        chat.Clear();
        chat.Receive("Racer", "new race", default, 1);
        Assert.AreEqual(1, chat.Lines.Value.Count);
    }

    [TestMethod]
    public void History_IsBoundedAndClearingDropsDraftAndMessages()
    {
        var chat = new RaceChatState();
        for (var i = 0; i < RaceChat.HistoryLimit + 5; i++)
            chat.Receive("Racer", $"message {i}", new Color(120, 180, 255));
        Assert.AreEqual(RaceChat.HistoryLimit, chat.Lines.Value.Count);
        Assert.AreEqual("message 5", chat.Lines.Value[0].Message);
        chat.Open.Value = true;
        chat.Draft.Value = "draft";
        chat.Clear();
        Assert.IsFalse(chat.Open.Value);
        Assert.AreEqual("", chat.Draft.Value);
        Assert.AreEqual(0, chat.Lines.Value.Count);
    }
}
