using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Round-trips the packets that carry a gamemode's config.
///
/// These were <c>Dictionary&lt;string, object&gt;</c>, which the wire format cannot serialize at all -
/// it has no formatter for <c>object</c>, and resolving the value formatter fails even for an
/// empty dictionary. Creating a session crashed on the client; starting one would have crashed
/// the same way, because <see cref="MatchGameplayInfo"/> is on that wire too.
/// </summary>
[TestClass]
public class LobbyPacketTests
{
    private static T RoundTrip<T>(T packet, sbyte opcode) where T : IReadableWritable<T>, TightPack.ITightPackable<T>
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        packet.Write(writer);

        // C2S and S2C live in separate opcode spaces, so try the one the packet belongs to.
        object? deserialized = opcode > 0
            ? MultiplayerUtils.TryDeserializeC2SPacket(opcode, writer.WrittenMemory)
            : MultiplayerUtils.TryDeserializeS2CPacket(opcode, writer.WrittenMemory);
        Assert.IsNotNull(deserialized, $"opcode {opcode} did not deserialize");
        return (T)deserialized;
    }

    [TestMethod]
    public void SetGameRule_CarriesTheSessionAndSelectedValue()
    {
        var received = RoundTrip(new C2S_SetGameRule { SessionId = 10, Name = "walls", Value = "3" }, 17);
        Assert.AreEqual(10u, received.SessionId);
        Assert.AreEqual("walls", received.Name);
        Assert.AreEqual("3", received.Value);
    }

    [TestMethod]
    public void CreateSession_CarriesGamemodeParameters()
    {
        var sent = new C2S_CreateSession
        {
            StageName = "nfmm/road",
            MaxPlayers = 6,
            GameMode = DefaultGamemodes.PvP,
            GameModeParameters = new Dictionary<string, string> { ["constraint"] = "both" }
        };

        var received = RoundTrip(sent, 1);

        Assert.AreEqual("nfmm/road", received.StageName);
        Assert.AreEqual(6, received.MaxPlayers);
        Assert.AreEqual(DefaultGamemodes.PvP, received.GameMode);
        Assert.AreEqual("both", received.GameModeParameters["constraint"]);
    }

    [TestMethod]
    public void CreateSession_SerializesWithNoParameters()
    {
        // The old field crashed on this case too: the value formatter is resolved before the
        // dictionary is even read, so "there are no entries" did not save it.
        var sent = new C2S_CreateSession
        {
            StageName = "nfmm/road",
            MaxPlayers = 4,
            GameMode = DefaultGamemodes.PvP,
            GameModeParameters = []
        };

        Assert.AreEqual(0, RoundTrip(sent, 1).GameModeParameters.Count);
    }

    [TestMethod]
    public void RaceStarted_CarriesMatchInfo()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfmm/road",
            Gamemode = DefaultGamemodes.PvP,
            Parameters = new Dictionary<string, string> { ["constraint"] = "racing" },
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [0] = new()
                {
                    Id = Guid.NewGuid(),
                    PlayerName = "alice",
                    CarName = "nfmm/radicalone",
                    Color = new Color3(1, 2, 3)
                }
            }
        };

        var sent = new S2C_RaceStarted
        {
            MatchGameplayInfo = info,
            State = SessionState.WaitingToLoad,
            JoinInfo = new S2C_RaceStarted.GameJoinInfo
            {
                RaceServerIpAddress = new IpAndPort(
                    new CompactIpAddress(System.Net.IPAddress.Parse("203.0.113.7").GetAddressBytes()),
                    7777
                ),
                JoinToken = Guid.NewGuid()
            }
        };

        var received = RoundTrip(sent, -6);

        Assert.AreEqual("nfmm/road", received.MatchGameplayInfo.StageName);
        Assert.AreEqual("racing", received.MatchGameplayInfo.Parameters["constraint"]);
        Assert.AreEqual("alice", received.MatchGameplayInfo.Players[0].PlayerName);

        // The address was silently dropped while the format wrote zero members for it, so a join
        // was sent to 0.0.0.0. It has to survive as the actual endpoint.
        Assert.AreEqual("203.0.113.7", ((System.Net.IPAddress)received.JoinInfo.RaceServerIpAddress.Address).ToString());
        Assert.AreEqual(7777, received.JoinInfo.RaceServerIpAddress.Port);
    }

    [TestMethod]
    public void CompactIpAddress_RoundTripsBothFamilies()
    {
        foreach (var text in new[] { "203.0.113.7", "2001:db8::1", "::1" })
        {
            var sent = new IpAndPort(
                new CompactIpAddress(System.Net.IPAddress.Parse(text).GetAddressBytes()),
                4242
            );

            var received = TightPack.TightPackSerializer.Deserialize<IpAndPort>(
                TightPack.TightPackSerializer.Serialize(in sent)
            );

            Assert.AreEqual(text, ((System.Net.IPAddress)received.Address).ToString());
            Assert.AreEqual(4242, received.Port);
        }
    }

    [TestMethod]
    public void PrepareContent_RoundTripsTheAssetList()
    {
        var content = new[]
        {
            new PreparedContent { Id = "archive/" + new string('a', 64), Kind = 1 },
            new PreparedContent { Id = "archive/" + new string('b', 64), Kind = 0 }
        };

        var sent = new S2C_PrepareContent
        {
            SessionId = 42,
            PreparationId = Guid.NewGuid(),
            Content = content
        };

        var received = RoundTrip(sent, -11);

        Assert.AreEqual(42u, received.SessionId);
        Assert.AreEqual(sent.PreparationId, received.PreparationId);
        Assert.AreEqual(2, received.Content.Length);
        Assert.AreEqual(content[0].Id, received.Content[0].Id);
        Assert.AreEqual((byte)1, received.Content[0].Kind);
    }

    [TestMethod]
    public void ContentReady_RoundTripsTheReason()
    {
        var sent = new C2S_ContentReady
        {
            SessionId = 7,
            PreparationId = Guid.NewGuid(),
            Ready = false,
            Reason = "Could not download the asset"
        };

        var received = RoundTrip(sent, 13);

        Assert.AreEqual(7u, received.SessionId);
        Assert.AreEqual(sent.PreparationId, received.PreparationId);
        Assert.IsFalse(received.Ready);
        Assert.AreEqual("Could not download the asset", received.Reason);
    }

    /// <summary>
    /// The client gamemode indexes players by array position while relayed car state is
    /// addressed by id, so the roster has to be laid out in slot order. Enumerated as-is, a
    /// dictionary built by the lobby put each player in another player's slot and the cars
    /// never moved - and what order a dictionary enumerates in is not something to rely on.
    /// </summary>
    [TestMethod]
    public void PlayerIdentityAndRosterRoundTripIndividualCarColors()
    {
        Color3?[] colors = [new(200, 30, 40), null, new(40, 200, 60), null];
        var identity = new C2S_PlayerIdentity
        {
            PlayerName = "painted player", SelectedVehicle = "nfmm/radicalone", Color = default,
            CarColors = colors,
            Wheels = new WheelAppearance
            {
                OriginalWheels = true, RimColor = new Color3(80, 120, 160), Colors = colors
            }
        };
        var receivedIdentity = RoundTrip(identity, 6);
        CollectionAssert.AreEqual(colors, receivedIdentity.CarColors);
        Assert.IsTrue(receivedIdentity.Wheels!.OriginalWheels);
        Assert.AreEqual(identity.Wheels.RimColor, receivedIdentity.Wheels.RimColor);
        CollectionAssert.AreEqual(colors, receivedIdentity.Wheels.Colors);

        var player = new ServerSidePlayerInfo
        {
            Id = Guid.NewGuid(), PlayerName = identity.PlayerName, CarName = identity.SelectedVehicle,
            Color = default, CarColors = receivedIdentity.CarColors, Wheels = receivedIdentity.Wheels
        };
        var receivedPlayer = TightPack.TightPackSerializer.Deserialize<ServerSidePlayerInfo>(
            TightPack.TightPackSerializer.Serialize(in player)
        );
        CollectionAssert.AreEqual(colors, receivedPlayer.CarColors);
        Assert.IsTrue(receivedPlayer.Wheels!.OriginalWheels);
        Assert.AreEqual(identity.Wheels.RimColor, receivedPlayer.Wheels.RimColor);
        CollectionAssert.AreEqual(colors, receivedPlayer.Wheels.Colors);
    }

    [TestMethod]
    public void LobbyChat_CarriesTheSessionAndSenderColour()
    {
        var sent = new S2C_LobbyChatMessage
        {
            SenderId = Guid.NewGuid(),
            Sender = "alice",
            Message = "hello game",
            SessionId = 17,
            Color = new Color3(10, 20, 30)
        };

        var received = RoundTrip(sent, -1);

        Assert.AreEqual(17u, received.SessionId);
        Assert.AreEqual(sent.SenderId, received.SenderId);
        Assert.AreEqual("hello game", received.Message);
        Assert.AreEqual(new Color3(10, 20, 30), received.Color);

        var request = RoundTrip(new C2S_LobbyChatMessage { Message = "hi", SessionId = 17 }, 4);
        Assert.AreEqual(17u, request.SessionId);
        Assert.AreEqual("hi", request.Message);
    }

    [TestMethod]
    public void LobbyState_CarriesSessionOptionsAndBotSlots()
    {
        var bot = new ServerSidePlayerInfo
        {
            Id = Guid.NewGuid(),
            PlayerName = "Bot 1",
            CarName = "nfmm/radicalone",
            Color = new Color3(200, 60, 60),
            IsBot = true
        };
        var sent = new S2C_LobbyState
        {
            ClientId = Guid.NewGuid(),
            Players = [],
            ActiveSessions =
            [
                new S2C_LobbyState.GameSession
                {
                    Id = 9,
                    CreatorId = Guid.NewGuid(),
                    CreatorName = "alice",
                    StageName = "nfmm/road",
                    MaxPlayers = 4,
                    Players = new Dictionary<byte, Guid> { [0] = Guid.NewGuid() },
                    State = SessionState.NotStarted,
                    Gamemode = DefaultGamemodes.PvP,
                    GamemodeParameters = new Dictionary<string, string> { ["walls"] = "3" },
                    Bots = new Dictionary<byte, ServerSidePlayerInfo> { [1] = bot },
                    FinalStandings =
                    [
                        new SessionStanding
                        {
                            PlayerId = bot.Id,
                            PlayerName = "Bot 1",
                            CarName = "nfmm/radicalone",
                            Color = new Color3(200, 60, 60),
                            IsBot = true,
                            Position = 0,
                            Dnf = true,
                            Slot = 1,
                            CarColors = [new Color3(90, 70, 30), null],
                            Lap = 2,
                            TotalLaps = 3,
                            Wasted = true,
                            PowerPercent = 25,
                            DamagePercent = 100
                        }
                    ],
                    ClosesAtUnixMs = 1_700_000_000_000
                }
            ]
        };

        var received = RoundTrip(sent, -2);
        var session = received.ActiveSessions[0];

        Assert.AreEqual(DefaultGamemodes.PvP, session.Gamemode);
        Assert.AreEqual("3", session.GamemodeParameters["walls"]);
        Assert.AreEqual(1, session.Players.Count);
        Assert.AreEqual("Bot 1", session.Bots[1].PlayerName);
        // Occupied slots include bots: a 1/4 row with a bot is 2/4.
        Assert.AreEqual(2, session.PlayerCount);
        Assert.AreEqual("Bot 1", session.FinalStandings[0].PlayerName);
        var final = session.FinalStandings[0];
        Assert.AreEqual(0, final.Position);
        Assert.AreEqual((byte)1, final.Slot);
        Assert.AreEqual<Color3?>(new Color3(90, 70, 30), final.CarColors?[0]);
        Assert.AreEqual((byte)2, final.Lap);
        Assert.AreEqual((ushort)3, final.TotalLaps);
        Assert.IsTrue(final.Wasted);
        Assert.AreEqual<byte?>(25, final.PowerPercent);
        Assert.AreEqual<byte?>(100, final.DamagePercent);
        Assert.AreEqual(1_700_000_000_000, session.ClosesAtUnixMs);
    }

    [TestMethod]
    public void SessionLiveState_CarriesOrderAndCarReadings()
    {
        var playerId = Guid.NewGuid();
        var sent = new S2C_SessionLiveState
        {
            SessionId = 12,
            Standings =
            [
                new RaceLiveStanding
                {
                    PlayerId = playerId,
                    PlayerName = "alice",
                    CarName = "nfmm/radicalone",
                    Color = new Color3(10, 20, 30),
                    IsBot = false,
                    Position = 0,
                    Lap = 2,
                    Checkpoint = 5,
                    Wasted = false,
                    Forfeited = false,
                    TotalLaps = 3,
                    PowerPercent = 72,
                    DamagePercent = 45
                }
            ]
        };

        var received = RoundTrip(sent, -20);

        Assert.AreEqual(12u, received.SessionId);
        Assert.AreEqual(1, received.Standings.Count);
        Assert.AreEqual("alice", received.Standings[0].PlayerName);
        Assert.AreEqual(2, received.Standings[0].Lap);
        Assert.AreEqual((ushort)5, received.Standings[0].Checkpoint);
        Assert.AreEqual((ushort)3, received.Standings[0].TotalLaps);
        Assert.AreEqual<byte?>(72, received.Standings[0].PowerPercent);
        Assert.AreEqual<byte?>(45, received.Standings[0].DamagePercent);

        var report = new RaceServer2Lobby_RaceResults
        {
            MatchKey = Guid.NewGuid().ToString(),
            Results = new RaceResults { GamemodeId = DefaultGamemodes.PvP, RaceDuration = TimeSpan.Zero, Standings = [] },
            FinalState = new RaceLiveState { Standings = sent.Standings.ToArray() }
        };
        var receivedReport = TightPack.TightPackSerializer.Deserialize<RaceServer2Lobby_RaceResults>(
            TightPack.TightPackSerializer.Serialize(report)
        );
        Assert.AreEqual<byte?>(45, receivedReport.FinalState?.Standings[0].DamagePercent);
    }

    [TestMethod]
    public void InSlotOrder_LaysPlayersOutBySlotNotEnumerationOrder()
    {
        var info = new MatchGameplayInfo
        {
            StageName = "nfmm/road",
            Gamemode = "nfmm/pvp",
            Players = new Dictionary<byte, ServerSidePlayerInfo>
            {
                [2] = Player("carol"),
                [0] = Player("alice"),
                [1] = Player("bob")
            }
        };

        Assert.AreEqual(
            "alice,bob,carol",
            string.Join(",", info.InSlotOrder().Select(p => p.PlayerName))
        );

        static ServerSidePlayerInfo Player(string name) => new()
        {
            Id = Guid.NewGuid(), PlayerName = name, CarName = "nfmm/radicalone", Color = default
        };
    }
}
