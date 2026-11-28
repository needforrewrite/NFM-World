using NFMWorldLibrary.Multiplayer;
using SignalsDotnet;

namespace NFMWorld.UI.ViewModels;

public sealed record RaceChatLine(long Id, string Sender, string Message, Color Color, bool IsGame = false)
{
    public long ReceivedAtMilliseconds { get; init; } = System.Environment.TickCount64;

    public float RecentOpacity(long nowMilliseconds)
        => Math.Clamp(1f - Math.Max(0, nowMilliseconds - ReceivedAtMilliseconds - 6000) / 2000f, 0f, 1f);
}

public sealed class RaceChatState
{
    public Signal<bool> Open { get; } = new(false);
    public Signal<string> Draft { get; } = new("");
    public Signal<IReadOnlyList<RaceChatLine>> Lines { get; } = new([]);
    private long _nextId;
    private long _lastReceivedId;

    public void Receive(string sender, string message, Color color, long messageId = 0, bool isGame = false)
    {
        if (messageId > 0 && messageId <= _lastReceivedId)

            return;
        var text = RaceChat.Normalize(message);

        if (text.Length == 0)

            return;
        if (messageId > 0)
            _lastReceivedId = messageId;
        Lines.Value = Lines.Value.TakeLast(RaceChat.HistoryLimit - 1)
            .Append(new RaceChatLine(++_nextId, sender, text, color, isGame)).ToArray();
    }

    public void Clear()
    {
        Open.Value = false;
        Draft.Value = "";
        Lines.Value = [];
        _lastReceivedId = 0;
    }
}
