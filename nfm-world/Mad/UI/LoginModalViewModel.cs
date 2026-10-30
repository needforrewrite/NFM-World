using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NFMWorld.UI.Hud;

public partial class LoginModalViewModel : ObservableObject
{
    // ── Tab state ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignInVisible))]
    [NotifyPropertyChangedFor(nameof(SignUpVisible))]
    [NotifyPropertyChangedFor(nameof(SignInTabColor))]
    [NotifyPropertyChangedFor(nameof(SignUpTabColor))]
    [NotifyPropertyChangedFor(nameof(SignInTabBg))]
    [NotifyPropertyChangedFor(nameof(SignUpTabBg))]
    public partial int ActiveTab { get; set; } = 0;

    public bool SignInVisible => ActiveTab == 0;
    public bool SignUpVisible => ActiveTab == 1;

    public Color SignInTabColor => ActiveTab == 0
        ? new Color(255, 140, 0, 255)
        : new Color(160, 160, 160, 255);

    public Color SignUpTabColor => ActiveTab == 1
        ? new Color(255, 140, 0, 255)
        : new Color(160, 160, 160, 255);

    public Color SignInTabBg => ActiveTab == 0
        ? new Color(30, 20, 50, 255)
        : new Color(15, 5, 30, 255);

    public Color SignUpTabBg => ActiveTab == 1
        ? new Color(30, 20, 50, 255)
        : new Color(15, 5, 30, 255);

    // ── Sign In fields ────────────────────────────────────────────

    [ObservableProperty]
    public partial string SignInUsername { get; set; } = "";

    [ObservableProperty]
    public partial string SignInPassword { get; set; } = "";

    // ── Sign Up fields ────────────────────────────────────────────

    [ObservableProperty]
    public partial string SignUpUsername { get; set; } = "";

    [ObservableProperty]
    public partial string SignUpEmail { get; set; } = "";

    [ObservableProperty]
    public partial string SignUpPassword { get; set; } = "";

    // ── Error message ─────────────────────────────────────────────

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = "";

    // ── Events for hosting code ───────────────────────────────────

    /// <summary>Raised when the user submits the Sign In form.</summary>
    public event Action<string, string>? SignInRequested;

    /// <summary>Raised when the user submits the Sign Up form.</summary>
    public event Action<string, string, string>? SignUpRequested;

    /// <summary>Raised when the user clicks "Sign in with Discord".</summary>
    public event Action? DiscordSignInRequested;

    /// <summary>Raised when the user wants to close the modal.</summary>
    public event Action? CloseRequested;

    // ── Commands ──────────────────────────────────────────────────

    [RelayCommand]
    private void SwitchToSignIn()
    {
        ActiveTab = 0;
        ErrorMessage = "";
    }

    [RelayCommand]
    private void SwitchToSignUp()
    {
        ActiveTab = 1;
        ErrorMessage = "";
    }

    [RelayCommand]
    private void SignIn()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(SignInUsername) || string.IsNullOrWhiteSpace(SignInPassword))
        {
            ErrorMessage = "Please enter both username and password.";
            return;
        }
        SignInRequested?.Invoke(SignInUsername, SignInPassword);
    }

    [RelayCommand]
    private void SignUp()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(SignUpUsername) || string.IsNullOrWhiteSpace(SignUpEmail) || string.IsNullOrWhiteSpace(SignUpPassword))
        {
            ErrorMessage = "Please fill in all fields.";
            return;
        }
        SignUpRequested?.Invoke(SignUpUsername, SignUpEmail, SignUpPassword);
    }

    [RelayCommand]
    private void DiscordSignIn()
    {
        ErrorMessage = "";
        DiscordSignInRequested?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }
}