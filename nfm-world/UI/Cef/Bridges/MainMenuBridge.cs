using System.Text.Json;
using MemoryPack;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Bridge for MainMenuPhase — handles navigation, account state, and hosts
/// the SettingsHandler as a sub-handler for embedded settings UI.
/// </summary>
public sealed class MainMenuBridge : PhaseBridge
{
    public override bool EnableInput => true;

    private readonly SettingsHandler _settings = new();

    public MainMenuBridge() : base("main-menu")
    {
        AddSubHandler(_settings);
        _settings.CloseRequested += () => SettingsCloseRequested?.Invoke();
        _settings.RestartConfirmed += () => SettingsRestartConfirmed?.Invoke();
    }

    /// <summary>
    /// The settings sub-handler, exposed so the phase can query capture state
    /// or push settings state programmatically.
    /// </summary>
    public SettingsHandler Settings => _settings;

    protected override void OnMessage(string type, JsonElement? args)
    {
        switch (type)
        {
            case "navigate":
                if (args is { } a && a.TryGetProperty("page", out var page))
                {
                    NavigateRequested?.Invoke(page.GetString() ?? "");
                }
                break;
            case "logout":
                LogoutRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Push account state to the menu. Call whenever the active account changes.
    /// </summary>
    public void PushAccount(string? name, bool isLoggedIn)
    {
        Push("account", new AccountData(name ?? "", isLoggedIn, (string?)null));
    }

    public event Action<string>? NavigateRequested;
    public event Action? LogoutRequested;

    /// <summary>Forwarded from <see cref="SettingsHandler.CloseRequested"/>.</summary>
    public event Action? SettingsCloseRequested;

    /// <summary>Forwarded from <see cref="SettingsHandler.RestartConfirmed"/>.</summary>
    public event Action? SettingsRestartConfirmed;
}

[MemoryPackable]
[GenerateTypeScript]
public partial record AccountData(string Name, bool IsLoggedIn, string? AvatarUrl);