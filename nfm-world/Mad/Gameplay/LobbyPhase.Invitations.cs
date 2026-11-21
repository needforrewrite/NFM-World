using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorld.UI.Components;

namespace NFMWorld.Gameplay;

public partial class LobbyPhase
{
    private readonly List<IPacketServerToClient> _deferredLobbyPackets = [];

    internal void ReadBackgroundNotifications()
    {
        foreach (var packet in Transport.GetNewPackets())
            if (!HandleLobbyNotification(packet))
                _deferredLobbyPackets.Add(packet);
    }

    private IPacketServerToClient[] ReadLobbyPackets()
    {
        var packets = Transport.GetNewPackets();
        if (_deferredLobbyPackets.Count == 0) return packets;

        _deferredLobbyPackets.AddRange(packets);
        var pending = _deferredLobbyPackets.ToArray();
        _deferredLobbyPackets.Clear();
        return pending;
    }

    private bool HandleLobbyNotification(IPacketServerToClient packet)
    {
        switch (packet)
        {
            case S2C_GameInvite invite:
                GameSparker.UiRenderer?.Notifications.Show(
                    this,
                    "Game invite",
                    $"{invite.CreatorName} invited you to join their game.",
                    key: $"invite-{invite.SessionId}",
                    expiresAtUnixMs: invite.ExpiresAtUnixMs,
                    action: new NotificationCenter.NotificationAction(
                        "JOIN GAME",
                        () => JoinGame(invite.SessionId, invite.InviteId),
                        () => _carsActive && Lobby.Connected.Value,
                        "Return to the lobby to join."
                    )
                );
                return true;
            case S2C_GameLobbyNotice notice:
                GameSparker.UiRenderer?.Notifications.Show(this, "Game lobby", notice.Message);
                return true;
            default:
                return false;
        }
    }

    private bool CanManageGame(uint sessionId) => CanAct && Lobby.ViewedSessionId.Value == sessionId
                                                  && Lobby.Sessions.Value.Any(
                                                      session => session.Id == sessionId && session.CanManage
                                                  );

    private void OnKickGamePlayer(uint sessionId, Guid playerId)
    {
        if (CanManageGame(sessionId))
            Transport.SendPacketToServer(new C2S_KickGamePlayer { SessionId = sessionId, PlayerId = playerId });
    }

    private void OnInviteGamePlayer(uint sessionId, Guid playerId)
    {
        if (CanAct && Lobby.CurrentSession.Value is { CanManage: true, EmptySlots: > 0 } session
            && session.Id == sessionId)
            Transport.SendPacketToServer(new C2S_InviteGamePlayer { SessionId = sessionId, PlayerId = playerId });
    }
}
