using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Backend.AI;

/// <summary>
/// Base AI class for gamemode-specific AI implementations.
/// </summary>
[PluginVisible]
public abstract partial class BaseAi
{
    [PluginVisible]
    public abstract void RunAi();
}

// End of ReLitAi class