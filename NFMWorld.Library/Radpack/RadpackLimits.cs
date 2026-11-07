namespace NFMWorldLibrary.Radpack;

/// <summary>
/// Size ceilings shared by the client (which uploads) and any server that accepts uploads.
/// Kept in the library so the two cannot drift: a limit that only exists on the server turns
/// an avoidable upload failure into a network round trip that ends in a rejection.
/// </summary>
public static class RadpackLimits
{
    /// <summary>Largest compressed radpack a client will hand to a server.</summary>
    public const int MaxCompressedBytes = 64 * 1024 * 1024;

    /// <summary>Largest a radpack may decompress to.</summary>
    public const int MaxDecompressedBytes = 256 * 1024 * 1024;

    /// <summary>Largest time-trial replay that may be uploaded.</summary>
    public const int MaxTimeTrialBytes = 10 * 1024 * 1024;
}
