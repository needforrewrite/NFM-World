using nfm_world_library.Lua;
using NFMWorldLibrary.Backend;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Lua-facing view over the stage being raced. Exposes the small surface
/// gamemode scripts need without leaking the full backend stage graph.
/// </summary>
[LuaVisible]
public sealed partial class LuaStage
{
    private readonly BackendStage _backend;

    [LuaHidden]
    public LuaStage(BackendStage backend) => _backend = backend;

    [LuaName("name")]
    public string Name => _backend.Path;

    [LuaName("nlaps")]
    public int Nlaps => _backend.nlaps;

    [LuaName("checkpointCount")]
    public int CheckpointCount => _backend.checkpoints.Count;

    [LuaName("checkpointPosition")]
    public f64Vector3 CheckpointPosition(int index) => _backend.checkpoints[index].Position;
}
