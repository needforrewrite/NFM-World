using nfm_world_library.Lua;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.AI;

/// <summary>
/// Base AI class for gamemode-specific AI implementations.
/// </summary>
[LuaVisible]
public abstract partial class BaseAi
{
    [LuaName]
    public abstract void RunAi();
}

// End of ReLitAi class