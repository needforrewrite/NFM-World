﻿using System.Runtime.InteropServices;
using MemoryPack;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Required gameplay info to construct a match on the client or server.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[MemoryPackable]
public partial struct MatchGameplayInfo()
{
    [MemoryPackOrder(0)] public required string StageName { get; set; }
        
    /// <summary>
    /// Key: player car index
    /// Value: client ID
    /// </summary>
    [MemoryPackOrder(1)] public required IDictionary<byte, ServerSidePlayerInfo> Players { get; set; }

    [MemoryPackOrder(2)] public required string Gamemode { get; set; } = DefaultGamemodes.PvP;
    
    /// <summary>
    /// Gamemode config. Strings, not objects: MemoryPack has no formatter for <c>object</c>,
    /// and resolving one fails even for an empty dictionary. Every declared gamemode config
    /// property is a string; the plugin boundary converts to <c>object</c>.
    /// </summary>
    [MemoryPackOrder(3)] public Dictionary<string, string> Parameters { get; set; } = [];
}

public static class DefaultGamemodes
{
    public const string PvP = "nfmm/pvp";
}