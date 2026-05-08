using nfm_world.account.oauth2;
using nfm_world.api;

namespace nfm_world.account;

public class AccountManager
{
    public Account? ActiveAccount;

    public bool LoggedIn { get { return ActiveAccount is not null; } }

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
    public async Task<CreateLocalAccountResult> CreateLocalAccount(string username, string password)
    {
        var res = await UserApi.CreateLocalAccount(username, password);
        
        return new CreateLocalAccountResult(res.Item2?.Status ?? "Unknown Status", res.Item1);
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
    public async Task<LocalLogInResult> LogInToLocalAccount(string username, string password)
    {
        var res = await UserApi.LocalLogIn(username, password);
        var inner_res = new LocalLogInResult(res.Item2?.Status ?? "Unknown Status", res.Item1);

        if(!inner_res.Success)
        {
            return inner_res;
        }

        string token = res.Item2?.Token ?? throw new Exception("token was null in api response");
        ActiveAccount = new Account(token, username);

        return inner_res;
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

    /// <summary>
    /// Attempt to log in using a Discord Oauth2 authorization code. The code must be valid.
    /// If this code (when converted to a token) is associated with an existing user account, a session code for that user is created and returned.
    /// If not, this call will fail and instead should call into DiscordOauth2CreateAccount, which accepts both a code and a username. 
    /// 
    /// Please note - if the user changes their Discord password, the session token is *not* invalidated. The user needs to manually revoke
    /// all session tokens after resetting the Discord password.
    /// 
    /// See LogInToLocalAccount for session token retention policy.
    /// </summary>
    /// <param name="code">The token to log in from.</param>
    /// <param name="redirectUri">The escaped redirect URI used for accessing the code.</param>
    /// <returns>Either a session token or an error state describing actions the user must take.</returns>
    public async Task<Oauth2LogInResult> DiscordOauth2AttemptLogIn(string code, string redirectUri)
    {
        var res = await UserApi.DiscordOauth2LogIn(code, redirectUri);
        var inner_res = new Oauth2LogInResult(res.Item2?.Status ?? "Unknown Status", res.Item1);

        if(!inner_res.Success)
        {

            inner_res.TempToken = res.Item2?.TempToken;
            return inner_res;
        }

        // todo: dont throw here
        string username = res.Item2?.Username ?? throw new Exception("username was null in api response");
        string token = res.Item2?.Token ?? throw new Exception("token was null in api response");
        ActiveAccount = new Account(token, username);

        return inner_res;
    }

    /// <summary>
    /// Create an account based on the provided Oauth2 code. The code must be associated with a Discord user that has not already registered.
    /// The username must also be unique and follow the username policy.
    /// </summary>
    /// <param name="tempToken">The temporary session token returned by /discord/login</param>
    /// <param name="username">The username to create the account under</param>
    /// <returns></returns>
    public async Task<Oauth2CreateAccountResult> DiscordOauth2CreateAccount(string tempToken, string username)
    {
        // When we create an account via oauth, we automatically log in
        var res = await UserApi.CreateDiscordOauth2Account(tempToken, username);
        var inner_res = new Oauth2CreateAccountResult(res.Item2?.Status ?? "Unknown Status", res.Item1);

        if(!inner_res.Success)
        {
            return inner_res;
        }

        string token = res.Item2?.Token ?? throw new Exception("token was null in api response");
        ActiveAccount = new Account(token, username);

        return inner_res;
    }
}