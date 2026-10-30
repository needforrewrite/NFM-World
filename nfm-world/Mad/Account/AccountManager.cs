using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using NFMWorld.Api;

namespace NFMWorld.Accounts;

public class AccountManager
{
    public static NfmwClient Client { get; } = new("https://nfmwapi.jacher.io", new HttpClient());
    
    public Account? ActiveAccount
    {
        get;
        set
        {
            field = value;
            ActiveAccountChanged?.Invoke(value);
        }
    }

    public IObservable<Account?> ActiveAccountObservable;

    public event Action<Account?>? ActiveAccountChanged;

    private bool _signingIn;

    public AccountManager()
    {
        ActiveAccountObservable = Observable.FromEventPattern<Action<Account?>, Account?>(
                h => ActiveAccountChanged += h,
                h => ActiveAccountChanged -= h)
            .Select(e => e.EventArgs);
    }

    public bool LoggedIn => ActiveAccount is not null;

    // TODO: Logout properly by querying API to invalidate token?
    public void LogOut()
    {
        ActiveAccount = null;
    }

    /// <summary>
    /// Create an account. On success, this method has no side effects.
    /// Once an account is successfully created, it can be logged into with AccountManager.LogIn
    /// For now, this endpoint requires use of the "master token" set in Authorization. This allows admins
    /// to create accounts.
    /// Oauth2 accounts can be made by anyone but may stil need approval after creation.
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns>The result of account creation. Throws an exception where there is a server error or input validation error.</returns>
    public async Task<RequestResult> CreateLocalAccount(string username, string password)
    {
        try
        {
            var res = await Client.CreateLocalAccountAsync(new CreateLocalAccountRequest()
            {
                Username = username,
                Password = password
            });

            return new RequestResult("Success", true);
        }
        catch (ApiException<ErrorResponse> ex)
        {
            return new RequestResult(ex.Result.Error, false);
        }
    }

    /// <summary>
    /// Log in to a local account. On success, Account property is set to the logged in account.
    /// Account must be null or an exception will be thrown. Call LogOut first to remove the active session
    /// if already logged in.
    /// For Oauth2 accounts use those respective methods.
    /// 
    /// Session token retention policy is that a session token remains valid for a minimum of 24 hours after creation,
    /// and this duration resets every time the session token is used. However, session tokens are force invalidated after
    /// a period of 28 days. They are also invalidated if the user manually revokes any active session tokens.
    /// 
    /// Each user can only have one active session token at a time. If the user logs in from a new source when still logged in,
    /// the prior session token is revoked. This prevents multiple users playing from a single account at a time.
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns>The log in result. Throws an exception on serious failure.</returns>
    public async Task<RequestResult> LogInToLocalAccount(string username, string password)
    {
        try
        {
            var res = await Client.LoginLocalAccountAsync(new LoginLocalAccountRequest()
            {
                Username = username,
                Password = password
            });

            ActiveAccount = new Account(res.SessionToken, res.Username);
            
            return new RequestResult("Success", true);
        }
        catch (ApiException<ErrorResponse> ex)
        {
            return new RequestResult(ex.Result.Error, false);
        }
    }

    /// <summary>
    /// Update the Account's password. Must have a logged in account to do this.
    /// Only works for a local account (NOT Oauth2)
    /// </summary>
    /// <param name="current">Current password</param>
    /// <param name="updated">New password</param>
    /// <returns>The change password result.</returns>
    public async Task ChangeLocalAccountPassword(string current, string updated)
    {
        throw new NotImplementedException();
    }

    private static void OpenUrl(string url)
    {
        // hack because of this: https://github.com/dotnet/corefx/issues/10361
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            throw new PlatformNotSupportedException(RuntimeInformation.OSDescription);
        }
    }
    
    public async Task<RequestResult> LogInWithDiscord()
    {
        if (_signingIn)
        {
            return new RequestResult("Already started signing in, please finish on that window first.", false);
        }

        try
        {
            _signingIn = true;

            var response = await Client.InitDiscordOauthAsync();

            var uri = new Uri(response.Url);
            if (uri.Scheme != "https")
            {
                throw new Exception("Invalid URL scheme for Discord OAuth: " + uri.Scheme);
            }
        
            OpenUrl(uri.AbsoluteUri);

            var pollCount = 0;
            const int maxPollCount = 120;

            while (pollCount < maxPollCount)
            {
                var oauth = await Client.PollOauthAsync(response.PollId);
                if (oauth.Status == "login")
                {
                    ActiveAccount = new Account(oauth.Payload!, ""); // TODO...
                    return new RequestResult("Success", true);
                }
                else if (oauth.Status == "error")
                {
                    return new RequestResult(oauth.Payload ?? "Unknown error", false);
                }
            
                await Task.Delay(1000);
                pollCount++;
            }

            return new RequestResult("Timed out", false);
        }
        finally
        {
            _signingIn = false;
        }
    }
}