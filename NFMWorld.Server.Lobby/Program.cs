using System.Net;
using Maxine.Extensions;
using TightPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Archive;
using NFMWorldLibrary.Multiplayer.HttpMessages;

Console.WriteLine("NFMWorld Lobby Server starting...");

BackendGameSparker.Load();

var httpEndpoint = Environment.GetEnvironmentVariable("LOBBY_HTTP_ENDPOINT") ?? "http://localhost:7001/";

// WebSocket server for client connections
var transport = new WebSocketMultiplayerServerTransport();
var orchestrator = new GameOrchestrator(transport);
orchestrator.Start();

// HTTP endpoint for Game Masters to report race results
using var http = new HttpListener();
http.Prefixes.Add(httpEndpoint);
http.Start();

// Shared with every Game Master for /race-ended; without it the endpoint is open to anyone.
var knownKeys = HmacAuth.ParseKnownKeys(Environment.GetEnvironmentVariable("GM_HMAC_KEYS") ?? "");
if (knownKeys.Count == 0)
    Logging.Warning("[Lobby] WARNING: no HMAC keys configured; /race-ended will reject every request.");
const int MaxBodyBytes = 1 << 20;

// Rejects a captured /race-ended replayed inside its timestamp window, which would otherwise
// report the same finished race a second time.
var seenNonces = new NonceCache();

// The archive only trusts this server to report results, so this key must not reach a game master.
ArchiveClient? archive = null;
if (Environment.GetEnvironmentVariable("ARCHIVE_URL") is { Length: > 0 } archiveUrl)
{
    archive = new ArchiveClient(
        archiveUrl,
        Environment.GetEnvironmentVariable("HMAC_KEY_ID"),
        Environment.GetEnvironmentVariable("ARCHIVE_SECRET_KEY")
    );
    if (!archive.CanReportResults)
        Logging.Warning("[Lobby] ARCHIVE_URL is set but no ARCHIVE_SECRET_KEY, so results will not be reported.");
}

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) =>
{
    Console.WriteLine("[Lobby] Shutting down...");
    cts.Cancel();
};

Console.WriteLine($"[Lobby] Accepting connections on {httpEndpoint}");
Console.WriteLine("[Lobby] Press Ctrl+C to stop.");

_ = Task.Run(
    async () =>
    {
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var ctx = await http.GetContextAsync().WaitAsync(cts.Token);
                if (ctx.Request.IsWebSocketRequest)
                    _ = transport.AcceptWebSocketRequest(ctx, cts.Token);
                else
                    _ = Task.Run(() => HandleHttpRequest(ctx, knownKeys, MaxBodyBytes, orchestrator, seenNonces));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A request that throws must not end the accept loop, or no client could
                // join again for the life of the process.
                Logging.Error($"[Lobby] Error accepting a request: {ex}");
            }
        }
    },
    cts.Token
);

try
{
    await Task.Delay(-1, cts.Token);
}
catch (OperationCanceledException)
{
}

http.Stop();
orchestrator.Stop();
return;

static async Task HandleHttpRequest(
    HttpListenerContext ctx,
    IReadOnlyDictionary<string, byte[]> knownKeys,
    int maxBodyBytes,
    GameOrchestrator orchestrator,
    NonceCache seenNonces
)
{
    var req = ctx.Request;
    var res = ctx.Response;

    try
    {
        if (req.Url!.LocalPath != "/race-ended" || req.HttpMethod != "POST")
        {
            res.StatusCode = 404;
            res.Close();
            return;
        }

        if (req.ContentLength64 > maxBodyBytes)
        {
            res.StatusCode = 413;
            res.Close();
            return;
        }

        using var seq = req.InputStream.AsPooledReadOnlySequence();
        if (seq.Sequence.Length > maxBodyBytes)
        {
            res.StatusCode = 413;
            res.Close();
            return;
        }

        // Copied once so the signature can be checked over a contiguous span; the same
        // copy is then deserialized.
        var body = System.Buffers.BuffersExtensions.ToArray(seq.Sequence);

        // The body is what the signature covers, so it has to be read before verifying.
        var authError = HmacAuth.Verify(
            req.HttpMethod,
            req.Url!.LocalPath,
            body,
            req.Headers["Authorization"],
            knownKeys,
            out var keyId,
            out var nonce
        );
        if (authError is not null || !seenNonces.TryUse(keyId ?? "", nonce))
        {
            Logging.Warning($"[Lobby] /race-ended rejected: {authError ?? "nonce is missing or was already used"}");
            res.StatusCode = 401;
            res.Close();
            return;
        }

        // A game master is untrusted, so its report is only a claim: the signature proves which
        // lobby-issued race it refers to, and the lobby decides whether the match is still open.
        var results = TightPackSerializer.Deserialize<RaceServer2Lobby_RaceResults>(body);
        Logging.Info($"[Lobby] Race ended: MatchKey={results.MatchKey}, Players={results.Results.Standings?.Length ?? 0}");

        if (Guid.TryParse(results.MatchKey, out var matchKey))
            await orchestrator.ReportMatchResultsAsync(matchKey, results.Results, CancellationToken.None);

        res.StatusCode = 200;
        res.Close();
    }
    catch (Exception ex)
    {
        Logging.Error($"[Lobby] Error handling /race-ended: {ex}");
        try
        {
            res.StatusCode = 500;
            res.Close();
        }
        catch
        {
            // The response may already be closed.
        }
    }
}
