using System.Net;
using System.Text;
using MemoryPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.HttpMessages;

BackendGameSparker.Load();

Logging.Info("NFMWorld Game Master starting...");

var endpoint = Environment.GetEnvironmentVariable("GM_HTTP_ENDPOINT") ?? "http://localhost:7003/";
var gamePort = ushort.Parse(Environment.GetEnvironmentVariable("GM_GAME_PORT") ?? "7002");

var keysConfig = Environment.GetEnvironmentVariable("GM_HMAC_KEYS") ?? "";
var knownKeys = HmacAuth.ParseKnownKeys(keysConfig);

if (knownKeys.Count == 0)
    Logging.Info("[GameMaster] WARNING: no HMAC keys configured.");

ENet.Library.Initialize();
var transport = new ENetMultiplayerServerTransport(gamePort);
var orchestrator = new RaceOrchestrator(transport);
orchestrator.Start();

// ── HTTP server (System.Net.HttpListener) ───────────────────────────

using var http = new HttpListener();
http.Prefixes.Add(endpoint);
http.Start();

Logging.Info($"[GameMaster] Game port {gamePort}, HTTP on {endpoint}");
Logging.Info("[GameMaster] Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) =>
{
    Logging.Info("[GameMaster] Shutting down...");
    cts.Cancel();
};

try
{
    while (!cts.IsCancellationRequested)
    {
        var ctx = await http.GetContextAsync().WaitAsync(cts.Token);
        _ = Task.Run(() => HandleRequest(ctx, orchestrator, knownKeys));
    }
}
catch (OperationCanceledException) { }

http.Stop();
orchestrator.Stop();
ENet.Library.Deinitialize();

return;

// ── Request handler ─────────────────────────────────────────────────

static void HandleRequest(
    HttpListenerContext ctx, RaceOrchestrator orchestrator,
    IReadOnlyDictionary<string, byte[]> knownKeys)
{
    var req = ctx.Request;
    var res = ctx.Response;

    Logging.Info($"[GameMaster] HTTP {req.HttpMethod} {req.Url!.LocalPath} from {req.RemoteEndPoint}");

    // Read body
    using var ms = new MemoryStream();
    req.InputStream.CopyTo(ms);
    var bodyArray = ms.ToArray();

    var authHeader = req.Headers["Authorization"];
    var error = HmacAuth.Verify(req.HttpMethod, req.Url!.LocalPath, bodyArray, authHeader, knownKeys);

    if (error is not null)
    {
        Logging.Info($"[GameMaster] Auth failed: {error}");
        res.StatusCode = 401;
        var errBytes = Encoding.UTF8.GetBytes(error);
        res.ContentLength64 = errBytes.Length;
        res.OutputStream.Write(errBytes);
        res.Close();
        return;
    }

    if (req.Url!.LocalPath == "/create-race")
    {
        try
        {
            var raceParams = MemoryPackSerializer.Deserialize<Lobby2RaceServer_CreateRace>(bodyArray);
            var responseBytes = MemoryPackSerializer.Serialize(orchestrator.CreateRace(raceParams));

            res.ContentType = "application/octet-stream";
            res.ContentLength64 = responseBytes.Length;
            res.OutputStream.Write(responseBytes);
            res.Close();
        }
        catch (Exception ex)
        {
            Logging.Info($"[GameMaster] CreateRace failed: {ex}");
            var errBytes = Encoding.UTF8.GetBytes(ex.Message);
            res.StatusCode = 500;
            res.ContentLength64 = errBytes.Length;
            res.OutputStream.Write(errBytes);
            res.Close();
        }
    }
    else
    {
        res.StatusCode = 404;
        res.Close();
    }
}