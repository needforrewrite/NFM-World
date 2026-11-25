namespace NFMWorld.Archive;

/// <summary>
/// Name of the one CORS policy the archive serves cross-origin traffic under, registered in
/// <c>Program.cs</c>. It is applied per route group, never globally, so the cookie-authenticated
/// website pages stay unreadable from another origin.
/// </summary>
/// <remarks>
/// The game client's session is a bearer header, not a cookie, so admitting any origin cannot
/// expose another visitor's data: a third-party page has no token to send. Credentials are off in
/// both directions, and <c>AllowAnyOrigin</c> forbids turning them on.
/// </remarks>
public static class CorsPolicies
{
    public const string CrossOrigin = "nfmw-cross-origin";
}
