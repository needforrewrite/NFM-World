using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Buffers;

namespace nfm_world.mad.account.oauth2.discord;

/// <summary>
/// Manager to help process Discord Oauth2 account management.
/// Behavior:
///  - Create a temporary local HTTP server to handle login callback
///  - Open the default web browser asking the user to log in and authorize the given client id
///  - Await user login on HTTP server callback
///  - Extract the authorization `code` and (optionally) exchange it for an access token
/// </summary>
public class DiscordOauth2Manager
{
    /// <summary>
    /// Result returned by the OAuth flow.
    /// </summary>
    public class DiscordOauthResult
    {
        public bool Success { get; set; }
        public string? Code { get; set; }
        public string? Error { get; set; }
        public string? RedirectUri { get; set; }
    }

    /// <summary>
    /// Start an OAuth2 authorization flow.
    /// - `clientId`: Discord application client id
    /// - `scopes`: scopes requested (e.g. new[] { "identify", "email" })
    /// Returns a <see cref="DiscordOauthResult"/> containing the authorization `Code` or an error message.
    /// </summary>
    public async Task<DiscordOauthResult> AuthorizeAsync(string clientId, string[] scopes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("clientId is required", nameof(clientId));

        // Start a loopback listener on an ephemeral port
        var listener = new TcpListener(IPAddress.Loopback, 8122);
        listener.Start();
        try
        {
            var localEp = (IPEndPoint)listener.LocalEndpoint!;
            var port = localEp.Port;
            var redirectUri = $"http://127.0.0.1:{port}/callback";

            var scopeStr = Uri.EscapeDataString(string.Join(' ', scopes));
            var authUrl = $"https://discord.com/api/oauth2/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&scope={scopeStr}&redirect_uri={Uri.EscapeDataString(redirectUri)}&prompt=consent";

            // Open the browser to the authorization URL
            OpenBrowser(authUrl);

            // Wait for a single incoming HTTP request (the OAuth2 redirect)
            using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var acceptTask = listener.AcceptTcpClientAsync();
            var completed = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, acceptCts.Token));
            if (acceptTask.IsCompletedSuccessfully)
            {
                using var client = acceptTask.Result;
                var query = await ReadRequestAndRespondAsync(client);

                // parse query string for code or error
                var qs = ParseQuery(query);
                if (qs.TryGetValue("error", out var err))
                {
                    return new DiscordOauthResult { Success = false, Error = err, RedirectUri = redirectUri };
                }

                qs.TryGetValue("code", out var code);
                if (string.IsNullOrEmpty(code))
                {
                    return new DiscordOauthResult { Success = false, Error = "No code in callback", RedirectUri = redirectUri };
                }

                // Return the authorization code to the caller; the service layer will exchange it for tokens
                return new DiscordOauthResult { Success = true, Code = code, RedirectUri = redirectUri };
            }
            else
            {
                return new DiscordOauthResult { Success = false, Error = "Listener accept timed out or was cancelled", RedirectUri = redirectUri };
            }
        }
        finally
        {
            try { listener.Stop(); listener.Dispose(); } catch { }
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            // Cross-platform approach
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Fallbacks for platforms where UseShellExecute=false by default
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
    }

    private static async Task<string> ReadRequestAndRespondAsync(TcpClient client)
    {
        using var ns = client.GetStream();
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        var sb = new StringBuilder();
        int read;
        // Read until header terminator \r\n\r\n
        while ((read = await ns.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (sb.ToString().Contains("\r\n\r\n"))
                break;
            if (sb.Length > 64 * 1024) // defensive
                break;
        }

        var req = sb.ToString();
        // first line: GET /callback?code=... HTTP/1.1
        var firstLine = req.Split([ '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var parts = firstLine.Split(' ');
        var pathAndQuery = parts.Length >= 2 ? parts[1] : "/";

        // Respond with a simple page telling the user they can close the browser
        var responseBody = "<html><body><h2>Authentication complete — you can close this window.</h2></body></html>";
        var response = "HTTP/1.1 200 OK\r\n" +
                       "Content-Type: text/html; charset=utf-8\r\n" +
                       $"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}\r\n" +
                       "Connection: close\r\n\r\n" +
                       responseBody;

        var respBytes = Encoding.UTF8.GetBytes(response);
        await ns.WriteAsync(respBytes.AsMemory(0, respBytes.Length));
        await ns.FlushAsync();

        // extract query part
        var qIdx = pathAndQuery.IndexOf('?');
        var query = qIdx >= 0 ? pathAndQuery[(qIdx + 1)..] : string.Empty;
        return query;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query))
            return dict;
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var idx = p.IndexOf('=');
            if (idx >= 0)
            {
                var k = Uri.UnescapeDataString(p[..idx]);
                var v = Uri.UnescapeDataString(p[(idx + 1)..]);
                dict[k] = v;
            }
            else
            {
                dict[Uri.UnescapeDataString(p)] = string.Empty;
            }
        }
        return dict;
    }
}