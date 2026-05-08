using Hexa.NET.ImGui;
using nfm_world_library.util;
using nfm_world.account.oauth2;
using nfm_world.account.oauth2.discord;

namespace nfm_world.account;


/// <summary>
/// FLoating menu for handling logins from various sources.
/// Can be called from a state and queried until logged in.
/// 
/// Can also be used to create an account if one does not exist.
/// </summary>
public class AccountManagerFloatingMenu
{
    public enum AccountManagerFloatingMenuState
    {
        /// <summary>
        /// Any state between logged out and logged in, including creating an account, logging in via Oauth2, etc.
        /// </summary>
        Processing,
        /// <summary>
        /// The user has logged in. The menu can be closed. GameSparker.AccountManager.ActiveAccount should now be non-null
        /// and based on the account that logged in.
        /// </summary>
        LoggedIn,
        /// <summary>
        /// The user closed the menu without logging in. The callsite may wish to cancel the overarchign operation.
        /// </summary>
        Canceled,
    }

    private readonly static string _clientId = "526861829872549898";

    private bool _isOpen;
    private string _localUsername = string.Empty;
    private string _localPassword = string.Empty;

    private bool _showCreateMenu = false;
    private string _createUsername = string.Empty;
    private string _createPassword = string.Empty;
    private string _createPasswordConfirm = string.Empty;

    // OAuth state
    private Task<DiscordOauth2Manager.DiscordOauthResult>? _oauthTask;
    private DiscordOauth2Manager.DiscordOauthResult? _oauthResult;
    private string _statusMessage = string.Empty;
    private bool _loggedIn = false;
    private CancellationTokenSource? _oauthCts;
    private bool _oauthHandled = false;
    private string? _pendingOauthCode;
    private string? _pendingOauthRedirect;
    // See Oauth2LogInResult - TempToken
    private string? _pendingOauthTempToken;

    // Disable all buttons when running in a thread context to prevent weirdness...
    private bool _buttonsDisabled = false;

    public AccountManagerFloatingMenu()
    {
        _isOpen = true;
        _loggedIn = GameSparker.AccountManager.ActiveAccount is not null;
    }

    public void Close()
    {
        _isOpen = false;

        try
        {
            if (_oauthCts != null && !(_oauthTask?.IsCompleted ?? true))
            {
                _oauthCts.Cancel();
            }
        }
        catch { }
        finally
        {
            try { _oauthCts?.Dispose(); } catch { }
            _oauthCts = null;
        }
    }

    private void ShowLocalLoginArea()
    {
        ImGui.Text("Local login");
        ImGui.InputText("Username", ref _localUsername, 128);
        ImGui.InputText("Password", ref _localPassword, 128, ImGuiInputTextFlags.Password);
        if (ImGui.Button("Login") && !_buttonsDisabled)
        {
            _statusMessage = "Logging in...";
            var username = _localUsername;
            var password = _localPassword;

            // Run network login off the game thread; callback runs on game thread
            _buttonsDisabled = true;
            GameThreadContext.Current.Run(async () =>
            {
                var res = await GameSparker.AccountManager.LogInToLocalAccount(username, password);
                return res;
            }, res =>
            {
                _buttonsDisabled = false;
                if (res.Success)
                {
                    _statusMessage = "Logged in via local account.";
                    _loggedIn = true;
                }
                else
                {
                    _statusMessage = res.ErrorString() ?? "Unknown login error.";
                }
            });
        }

        ImGui.NewLine();
        ImGui.Spacing();
        if (ImGui.Button("Create Account") && !_buttonsDisabled)
        {
            _showCreateMenu = true;
        }
    }

    private void ShowCreateAccountArea()
    {
        // When in Create mode hide the login controls and show a Log In toggle
        ImGui.Text("Create Account");

        ImGui.InputText("New username", ref _createUsername, 128);

        if (string.IsNullOrEmpty(_pendingOauthCode))
        {
            ImGui.InputText("New password", ref _createPassword, 128, ImGuiInputTextFlags.Password);
            ImGui.InputText("Confirm password", ref _createPasswordConfirm, 128, ImGuiInputTextFlags.Password);
        }
        else
        {
            _createPassword = "";
            _createPasswordConfirm = "";
        }

        if (ImGui.Button("Create") && !_buttonsDisabled)
        {
            if (string.IsNullOrWhiteSpace(_createUsername))
                _statusMessage = "Username required";
            else if (_createPassword != _createPasswordConfirm && string.IsNullOrEmpty(_pendingOauthCode))
                _statusMessage = "Passwords do not match";
            else
            {
                var un = _createUsername;
                var local_pw = _createPassword;
                _statusMessage = "Creating account...";

                if (!string.IsNullOrEmpty(_pendingOauthCode))
                {
                    // Create account using the pending OAuth code path
                    var code = _pendingOauthCode;
                    var redirect = _pendingOauthRedirect;
                    _buttonsDisabled = true;
                    var tt = _pendingOauthTempToken ?? throw new Exception("Temp token was null");
                    GameThreadContext.Current.Run(async () =>
                    {
                        var res = await GameSparker.AccountManager.DiscordOauth2CreateAccount(tt, un);
                        return res;
                    }, res =>
                    {
                        _buttonsDisabled = false;
                        if (res.Success)
                        {
                            _statusMessage = "Logged in via OAuth after creation.";
                            _loggedIn = true;
                            _showCreateMenu = false;
                            _pendingOauthCode = null;
                            _pendingOauthRedirect = null;
                            _pendingOauthTempToken = null;
                        }
                        else
                        {
                            _statusMessage = "*** " + (res.ErrorString() ?? "Unknown Error") + " ***";
                        }
                    });
                }
                else
                {
                    _buttonsDisabled = true;
                    GameThreadContext.Current.Run(async () =>
                    {
                        var res = await GameSparker.AccountManager.CreateLocalAccount(un, local_pw);
                        return res;
                    }, res =>
                    {
                        // TODO: Extend RequestResult for this
                        _buttonsDisabled = false;
                        if (res.Success)
                        {
                            _statusMessage = "Account created (awaiting approval).";
                            _showCreateMenu = false;
                        }
                        else
                        {
                            _statusMessage = res.ErrorString() ?? "Unknown error.";
                        }
                    });
                }
            }
        }

        // Only allow switching back to login if we don't have a pending code
        if (string.IsNullOrEmpty(_pendingOauthCode))
        {
            ImGui.NewLine();
            ImGui.Spacing();
            if (ImGui.Button("Log In") && !_buttonsDisabled)
            {
                _showCreateMenu = false;
            }
        }
    }

    private void ShowSocialLoginArea()
    {
        ImGui.Separator();
        ImGui.Text("Social login");
        if (ImGui.Button("Login with Discord") && !_buttonsDisabled)
        {
            if (_oauthTask == null || _oauthTask.IsCompleted)
            {
                // Cancel any existing CTS and create a new one for this flow
                try { _oauthCts?.Cancel(); } catch { }
                try { _oauthCts?.Dispose(); } catch { }
                _oauthCts = new CancellationTokenSource();
                var token = _oauthCts.Token;

                // Kick off OAuth2 flow in background. Replace CLIENT_ID placeholder with real value.
                var clientId = _clientId;
                _statusMessage = "Opening Discord in web browser.";
                _oauthResult = null;
                _buttonsDisabled = true;
                var manager = new DiscordOauth2Manager();
                _oauthTask = manager.AuthorizeAsync(clientId, ["identify", "email"], token);
            }
        }
    }

    private void HandleOauthTaskDisplay()
    {
        if ((!(_oauthTask?.IsCompleted)) ?? false)
        {
            ImGui.Text("OAuth: in progress...");
        }
        else
        {
            _oauthResult ??= _oauthTask?.Result;
            if (_oauthResult != null)
            {
                _oauthCts?.Dispose();
                _oauthCts = null;
                _buttonsDisabled = false;
                if (_oauthResult.Success)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "OAuth succeeded");
                    ImGui.Text($"Code: {_oauthResult.Code}");
                    ImGui.Text($"RedirectUri: {_oauthResult.RedirectUri}");
                    // Avoid handling more than once
                    if (!_oauthHandled)
                    {
                        _oauthHandled = true;
                        _pendingOauthCode = _oauthResult.Code;
                        _pendingOauthRedirect = _oauthResult.RedirectUri;
                        _statusMessage = "Verifying OAuth code with server...";

                        // Exchange / verify code via AccountManager on a background thread; callback runs on game thread
                        _buttonsDisabled = true;
                        GameThreadContext.Current.Run(async () =>
                        {
                            var res = await GameSparker.AccountManager.DiscordOauth2AttemptLogIn(_pendingOauthCode!, _pendingOauthRedirect!);
                            return res;
                        }, (Oauth2LogInResult res) =>
                        {
                            _buttonsDisabled = false;
                            if (res.Success)
                            {
                                _statusMessage = "Logged in via OAuth.";
                                _loggedIn = true;
                            }
                            else if (res.NoSuchAccount())
                            {
                                // Save pending code/redirect and use them when creating
                                _pendingOauthTempToken = res.TempToken;
                                _statusMessage = "No server account for this Discord identity. Enter username to create one.";
                                _showCreateMenu = true;
                            }
                            else
                            {
                                _statusMessage = res.ErrorString() ?? "Unknown login error.";
                                _pendingOauthCode = null;
                                _pendingOauthRedirect = null;
                            }
                        });
                    }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "OAuth failed");
                    ImGui.Text($"Error: {_oauthResult.Error}");
                    _statusMessage = "OAuth failed or cancelled.";
                }
            }
        }
    }

    /// <summary>
    /// Render and process the create account/login menu, using ImGui.
    /// </summary>
    /// <returns>The current state of this menu. LoggedIn means the menu can be disposed.</returns>
    /// <exception cref="NotImplementedException"></exception>
    public AccountManagerFloatingMenuState Process()
    {
        // Close window handler: if the UI was closed while OAuth is running, cancel it
        if (!_isOpen)
        {
            try
            {
                if (_oauthCts != null && !(_oauthTask?.IsCompleted ?? true))
                {
                    _oauthCts.Cancel();
                }
            }
            catch { }
            finally
            {
                try { _oauthCts?.Dispose(); } catch { }
                _oauthCts = null;
            }

            return _loggedIn ? AccountManagerFloatingMenuState.LoggedIn : AccountManagerFloatingMenuState.Canceled;
        }

        // Auto size to fit content
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0));
        if (ImGui.Begin("Login", ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
        {
            if (!_showCreateMenu)
            {
                //ShowLocalLoginArea();
            }
            else
            {
                ShowCreateAccountArea();
            }

            if (_pendingOauthCode is null && _pendingOauthTempToken is null)
            {
                ShowSocialLoginArea();
            }

            if (_oauthTask != null)
            {
                HandleOauthTaskDisplay();
            }

            ImGui.Separator();
            if (!string.IsNullOrEmpty(_statusMessage))
                ImGui.Text(_statusMessage);

            ImGui.End();
        }

        // If still open and not yet logged in, remain in Processing
        if (_loggedIn)
            return AccountManagerFloatingMenuState.LoggedIn;
        return AccountManagerFloatingMenuState.Processing;
    }
}