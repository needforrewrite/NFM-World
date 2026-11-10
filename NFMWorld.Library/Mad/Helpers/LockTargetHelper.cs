using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Helpers;

/// <summary>
/// Resolves which opponent a lock key picks. Shared by the HUD's arrow and the spectate camera
/// so a locked car is the same car in both, and a wasted car is never one of them.
/// </summary>
public static class LockTargetHelper
{
    /// <summary>
    /// The live opponent at <paramref name="lockIndex"/> (a player index), or null when that
    /// index is unset, absent, or holds a wasted car.
    /// </summary>
    public static BackendCar? LockedCar(ClientSidePlayer[] opponents, int lockIndex)
    {
        if (lockIndex < 0) return null;
        foreach (var player in opponents)
        {
            if (player.Index != lockIndex) continue;
            return player.Car is { Wasted: false } car ? car : null;
        }

        return null;
    }
}
