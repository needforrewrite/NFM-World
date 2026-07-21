using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorld.Util;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

public class InRacePhase(
    GraphicsDevice graphicsDevice,
    string stageName,
    BaseGamemodeFactory gamemode,
    IReadOnlyList<PlayerParameters> players)
    : BaseRacePhase(graphicsDevice, stageName, gamemode, players);