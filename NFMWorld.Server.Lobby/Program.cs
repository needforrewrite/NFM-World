using System.Net;
using System.Text;
using Maxine.Extensions;
using MemoryPack;
using NFMWorldLibrary;
using NFMWorldLibrary.Multiplayer;
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

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) =>
{
    Console.WriteLine("[Lobby] Shutting down...");
    cts.Cancel();
};

Console.WriteLine($"[Lobby] Accepting connections on {httpEndpoint}");
Console.WriteLine("[Lobby] Press Ctrl+C to stop.");

_ = Task.Run(async () =>
{
    while (!cts.IsCancellationRequested)
    {
        try
        {
            var ctx = await http.GetContextAsync().WaitAsync(cts.Token);
            if (ctx.Request.IsWebSocketRequest)
                _ = transport.AcceptWebSocketRequest(ctx, cts.Token);
            else
                HandleHttpRequest(ctx);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}, cts.Token);

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

static void HandleHttpRequest(HttpListenerContext ctx)
{
    var req = ctx.Request;
    var res = ctx.Response;

    if (req.Url!.LocalPath == "/race-ended" && req.HttpMethod == "POST")
    {
        using var seq = req.InputStream.AsPooledReadOnlySequence();
        var results = MemoryPackSerializer.Deserialize<RaceServer2Lobby_RaceResults>(seq.Sequence);

        Console.WriteLine($"[Lobby] Race ended: MatchKey={results.MatchKey}, Players={results.Results.Standings?.Length ?? 0}");
        res.StatusCode = 200;
        res.Close();
    }
    else
    {
        res.StatusCode = 404;
        res.Close();
    }
}