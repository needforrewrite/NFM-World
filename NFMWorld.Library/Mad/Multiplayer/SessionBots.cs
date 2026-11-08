using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>Placing a session's bots in the slots humans did not take.</summary>
public static class SessionBots
{
    /// <summary>
    /// Places <paramref name="count"/> bots in the lowest slots that are not in
    /// <paramref name="occupied"/>, owned by <paramref name="ownerId"/> - the client that
    /// simulates them.
    /// </summary>
    public static Dictionary<byte, ServerSidePlayerInfo> Place(
        IEnumerable<byte> occupied, int count, Guid ownerId)
    {
        var taken = occupied.ToHashSet();
        var bots = new Dictionary<byte, ServerSidePlayerInfo>();

        for (byte slot = 0; slot < MatchGameplayInfo.MaxPlayers && bots.Count < count; slot++)
        {
            if (taken.Contains(slot))
                continue;

            bots[slot] = new ServerSidePlayerInfo
            {
                // The match addresses a bot's relayed state by its id, so it needs one of its
                // own; a fresh one per race keeps it out of the human id space entirely.
                Id = Guid.NewGuid(),
                PlayerName = $"Bot {slot}",
                CarName = "nfmm/radicalone",
                Color = new Color3(200, 60, 60),
                IsBot = true,
                OwnerId = ownerId
            };
        }

        return bots;
    }
}
