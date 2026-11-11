using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using NFMWorld.Gameplay;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

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
            ActiveAccount = new Account(stored.Token, stored.Username);
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

            var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
            if (account is { Username.Length: > 0 })
                SetActive(new Account(ActiveAccount.Token, account.Username));
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logging.Warning($"Could not reach the archive: {ex.Message}");
            return false;
        }
    }

    public Uri RegistrationUrl => new(_baseUri, "account/login#register");

    public RequestResult OpenRegistration()
    {
        try
        {
#if WEB_AUDIO
            OpenBrowser(RegistrationUrl.AbsoluteUri, "_blank", "noopener,noreferrer");
#else
            using var browser = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(
                    RegistrationUrl.AbsoluteUri
                ) { UseShellExecute = true }
            );
#endif
            return new RequestResult("Complete registration in your browser, then sign in here.", true);
        }
        catch (Exception ex)
        {
            return new RequestResult($"Could not open registration: {ex.Message}", false);
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
                new { Username = username, Password = password }
            );
            if (!response.IsSuccessStatusCode)
                return new RequestResult(await ReadErrorAsync(response), false);

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body?.Token is not { Length: > 0 })
                return new RequestResult("The archive returned no session token.", false);

            AccountStore.Save(body.Token, body.Account.Username);
            SetActive(new Account(body.Token, body.Account.Username));
            return new RequestResult("Success", true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RequestResult($"Could not reach the archive: {ex.Message}", false);
        }
    }

    public void LogOut()
    {
        AccountStore.Clear();
        SetActive(null);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.TryGetProperty("error", out var error) && error.GetString() is { Length: > 0 } message)
                return message;
        }
        catch (JsonException)
        {
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

    private sealed record AccountResponse(string Username, bool IsAdmin, double Elo, int Wins, int Losses, int Matches);

    private sealed record LoginResponse(string Token, AccountResponse Account);
}
