using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorld.Gameplay;
using NFMWorldLibrary;
using NFMWorldLibrary.Accounts;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Accounts;

/// <summary>
/// Talks to the archive's account endpoints.
///
/// Replaces the generated <c>NfmwClient</c>, which was bound to an OpenAPI document whose routes
/// no longer exist. The archive is a small, stable surface now, so the three calls it needs are
/// written directly.
/// </summary>
public sealed partial class AccountManager : IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    public AccountManager()
    {
        var url = ArchiveEndpoint.Url;
        _baseUri = new Uri(url.EndsWith('/') ? url : url + "/");
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // A token from a previous run is used immediately; the archive confirms it on first use
        // and a rejected token simply behaves as signed out.
        if (AccountStore.Load() is { } stored)
            ActiveAccount = new Account(stored.Token, stored.Username, stored.Role, stored.DeletionPendingUntil);
    }

    public Account? ActiveAccount { get; private set; }

    public bool LoggedIn => ActiveAccount is not null;

    public event Action<Account?>? ActiveAccountChanged;

    private void SetActive(Account? account)
    {
        ActiveAccount = account;
        ActiveAccountChanged?.Invoke(account);
    }

    /// <summary>Confirms the stored token still works and refreshes the username.</summary>
    public async Task<bool> RefreshAsync()
    {
        if (ActiveAccount is null)
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, "auth/me"));
            request.Headers.Authorization = new("Bearer", ActiveAccount.Token);
            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                SetActive(null);
                AccountStore.Clear();
                return false;
            }

            var account = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.AccountResponse);
            if (account is { Username.Length: > 0 })
                SetActive(new Account(ActiveAccount.Token, account.Username, account.Role, account.DeletionPendingUntil));
            return true;
        }
        catch (Exception ex)
        {
            // A refresh is fired and forgotten at the menu, so anything escaping here is an
            // unobserved fault: on the web build that reaches the crash reporter.
            Logging.Warning($"Could not reach the archive: {ex.Message}");
            return false;
        }
    }

    public Uri RegistrationUrl => new(_baseUri, "account/login?flow=register");

    public Uri AccountSecurityUrl => new(_baseUri, "account/security");

    public RequestResult OpenRegistration() => OpenBrowserPage(RegistrationUrl);

    public RequestResult OpenAccountSecurity() => OpenBrowserPage(AccountSecurityUrl);

    private RequestResult OpenBrowserPage(Uri url)
    {
        try
        {
#if WEB_AUDIO
            OpenBrowser(url.AbsoluteUri, "_blank", "noopener,noreferrer");
#else
            using var browser = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }
            );
#endif
            return new RequestResult("Opening in your browser.", true);
        }
        catch (Exception ex)
        {
            return new RequestResult($"Could not open the page: {ex.Message}", false);
        }
    }

#if WEB_AUDIO
    [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.open")]
    private static partial void OpenBrowser(string url, string target, string features);
#endif

    public async Task<RequestResult> LogInToLocalAccount(string username, string password)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                new Uri(_baseUri, "auth/login"),
                new LoginRequest(username, password),
                AccountJsonContext.Default.LoginRequest
            );
            if (!response.IsSuccessStatusCode)
                return new RequestResult(await ReadErrorAsync(response), false);

            var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.LoginResponse);
            if (body?.Token is not { Length: > 0 })
                return new RequestResult("The archive returned no session token.", false);

            AccountStore.Save(body.Token, body.Account.Username, body.Account.Role, body.Account.DeletionPendingUntil);
            SetActive(new Account(body.Token, body.Account.Username, body.Account.Role, body.Account.DeletionPendingUntil));
            return new RequestResult("Success", true);
        }
        catch (Exception ex)
        {
            // Never let a sign-in failure escape: the caller is fire-and-forget, so an exception
            // here becomes an unobserved task fault rather than a message on the form.
            Logging.Warning($"Sign-in failed: {ex}");
            return new RequestResult($"Could not sign in: {ex.Message}", false);
        }
    }

    public void LogOut()
    {
        AccountStore.Clear();
        SetActive(null);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, bool assetRequest = false)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.JsonElement);
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String && error.GetString() is { Length: > 0 } message)
                return message;
        }
        catch (JsonException)
        {
        }

        if (assetRequest)
        {
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Your Archive session has expired. Sign in again from the main menu.",
                System.Net.HttpStatusCode.Forbidden => "The Archive account does not have permission for this action (HTTP 403).",
                System.Net.HttpStatusCode.RequestEntityTooLarge => "The Archive's upload size limit rejected the package (HTTP 413).",
                System.Net.HttpStatusCode.TooManyRequests => "The Archive is receiving too many requests. Try again later (HTTP 429).",
                System.Net.HttpStatusCode.ServiceUnavailable => "The Archive is temporarily unavailable or read-only (HTTP 503).",
                _ => $"The Archive returned HTTP {(int)response.StatusCode} ({response.StatusCode})."
            };
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Wrong username or password.",
            System.Net.HttpStatusCode.Conflict => "That username is taken.",
            System.Net.HttpStatusCode.TooManyRequests => "Too many attempts. Try again later.",
            _ => "The archive rejected the request."
        };
    }

    public void Dispose() => _http.Dispose();
}
