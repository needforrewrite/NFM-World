namespace NFMWorldLibrary.Backend;

/// <summary>
/// Functions that the gamemode is allowed to call on the client.
/// </summary>
public interface IClientCallbacks
{
    void ResetCheckpointGlow();
    void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish);
    IClientCarCallbacks GetClientCarCallbacks(BackendCar car);
}