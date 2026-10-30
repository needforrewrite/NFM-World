using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.UI;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Settings;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

/// <summary>
/// The settings screen, shown by the main menu and the pause menu.
/// Port of <c>data/uis/components/settings.luau</c>.
/// </summary>
/// <remarks>
/// The live config lives here rather than in <c>UiState</c>: the screen is either open
/// or not, and it reloads from <see cref="SettingsMenu"/> each time it mounts.
/// </remarks>
public static class SettingsView
{
    private enum Tab
    {
        Keyboard,
        Video,
        Audio,
        Game
    }

    private static readonly string[] DistantOutlineBehaviours =
    [
        "Distance Falloff (With Cutoff)",
        "Distance Falloff",
        "Classic Cutoff (NFM)",
        "Always Render",
        "Hide Outlines"
    ];

    public static Node Build(Action onClose)
    {
        var tab = new Signal<Tab>(Tab.Video);
        var config = new Signal<SettingsSnapshot?>(SettingsMenu.GetCurrentSnapshot());
        var options = new Signal<AvailableOptions?>(SettingsMenu.GetAvailableOptions());
        var capturing = new Signal<string?>(null);
        var confirm = new Signal<(string Title, string Message, Action OnConfirm)?>(null);
        var restartRequired = new Signal<bool>(false);

        // Captured before any edit, so Cancel has something to go back to. Snapshotting at
        // close time would capture the edits themselves and revert to nothing.
        var original = SettingsMenu.SaveConfigToString();

        void Set(string key, object? value, string? action = null)
        {
            SettingsMenu.ApplySetting(key, value, action);
            Refresh();
        }

        void Refresh() => config.Value = SettingsMenu.GetCurrentSnapshot();

        void Cancel()
        {
            SettingsMenu.LoadConfigFromSnapshot(original);
            SettingsMenu.ApplySettings(out _);
            onClose();
        }

        void Save()
        {
            if (SettingsMenu.SaveConfigAndCheckRestart())
                restartRequired.Value = true;
            else
                onClose();
        }

        // Absolutely positioned and full-bleed: the screen is hosted inside a column (the
        // main menu's item list, or the pause menu's card), so as a flow child it would
        // render underneath that content instead of over it.
        Node Body() => Sx.View(
            style: () => new Styles
            {
                Position = Position.Absolute,
                Top = 0,
                Left = 0,
                Right = 0,
                Bottom = 0,
                FlexDirection = FlexDirection.Column,
                BackgroundColor = new Color(0, 0, 0, 166),
                ZIndex = 20,
            },
            children:
            [
                Sx.Text(
                    style: () => new TextStyles
                    {
                        FontSize = 28,
                        FontStyle = FontStyle.Bold,
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                    },
                    layout: () => new Styles { MarginTop = 20, MarginBottom = 20, FlexShrink = 0 },
                    text: () => "Settings"),
                TabBar(tab),
                Sx.Switch(
                    tab,
                    new Dictionary<Tab, Func<Node>>
                    {
                        [Tab.Keyboard] = () => KeyboardTab(config, capturing, Set, () =>
                        {
                            SettingsMenu.ResetKeyBindingDefaults();
                            Refresh();
                        }),
                        [Tab.Video] = () => VideoTab(config, options, Set),
                        [Tab.Audio] = () => AudioTab(config, Set),
                        [Tab.Game] = () => GameTab(config, Set, () => confirm.Value = (
                            "Reset Camera",
                            "Are you sure you want to reset camera settings to default?",
                            () =>
                            {
                                SettingsMenu.ResetCameraDefaults();
                                confirm.Value = null;
                                Refresh();
                            }))
                    }),
                BottomBar(Save, Cancel),
                Sx.Show(
                    when: () => confirm.Value is not null,
                    then: () => ConfirmDialog(
                        () => confirm.Value!.Value.Title,
                        () => confirm.Value!.Value.Message,
                        () => confirm.Value!.Value.OnConfirm(),
                        () => confirm.Value = null)),
                Sx.Show(
                    when: () => restartRequired.Value,
                    then: () => ConfirmDialog(
                        () => "Restart Required",
                        () => "The renderer has been changed. A restart is required for this change to take effect.",
                        () => System.Environment.Exit(0),
                        () => restartRequired.Value = false))
            ]);

        return Body();
    }

    // ── Shell ─────────────────────────────────────────────────────────

    private static Node TabBar(Signal<Tab> tab) => Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            JustifyContent = Justify.Center,
            GapColumn = 4,
            GapRow = 4,
            PaddingTop = 16,
            PaddingLeft = 40,
            PaddingRight = 40,
            FlexShrink = 0,
        },
        children:
        [
            TabButton("Keyboard", () => tab.Value == Tab.Keyboard, () => tab.Value = Tab.Keyboard),
            TabButton("Video", () => tab.Value == Tab.Video, () => tab.Value = Tab.Video),
            TabButton("Audio", () => tab.Value == Tab.Audio, () => tab.Value = Tab.Audio),
            TabButton("Game", () => tab.Value == Tab.Game, () => tab.Value = Tab.Game)
        ]);

    private static Node TabButton(string label, Func<bool> isActive, Action onPress)
        => Button.Create(label: () => label, onPress: onPress, active: isActive, size: ButtonSize.Sm);

    private static Node BottomBar(Action onSave, Action onClose) => Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = Justify.Center,
            GapColumn = 12,
            GapRow = 12,
            PaddingTop = 16,
            PaddingBottom = 16,
            PaddingLeft = 40,
            PaddingRight = 40,
            BorderTop = 1,
            BorderColor = Theme.SurfaceBorder,
            FlexShrink = 0,
        },
        children:
        [
            Button.Create(label: () => "OK", onPress: onSave, variant: ButtonVariant.Primary),
            Button.Create(label: () => "Cancel", onPress: onClose),
            Button.Create(label: () => "Apply", onPress: onSave)
        ]);

    private static Node Content(params object?[] children) => Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Column,
            FlexGrow = 1,
            FlexShrink = 1,
            MinHeight = 0,
            Overflow = Overflow.Scroll,
            PaddingTop = 20,
            PaddingLeft = 40,
            PaddingRight = 40,
            PaddingBottom = 10,
        },
        children: children);

    // ── Tabs ──────────────────────────────────────────────────────────

    private static Node VideoTab(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options, Action<string, object?, string?> set)
        => Content(
            Section("Display"),
            Row("Renderer", Controls.Dropdown(() => options.Value?.Renderers ?? [], () => config.Value?.SelectedRenderer ?? 0, v => set("selectedRenderer", v, null))),
            Row("Resolution", Controls.Dropdown(() => options.Value?.Resolutions ?? [], () => config.Value?.SelectedResolution ?? 0, v => set("selectedResolution", v, null))),
            Row("Display Mode", Controls.Dropdown(() => options.Value?.DisplayModes ?? [], () => config.Value?.SelectedDisplayMode ?? 0, v => set("selectedDisplayMode", v, null))),
            Row("VSync", Controls.Checkbox(() => config.Value?.Vsync ?? false, () => set("vsync", !(config.Value?.Vsync ?? false), null))),
            Row("FPS Limit",
                Controls.Slider(() => config.Value?.FpsLimit ?? 0, v => set("fpsLimit", (int)v == 62 ? 0 : (int)v, null), 62, 240, 1),
                Value(() => (config.Value?.FpsLimit ?? 0) == 0 ? "Unlimited" : $"{config.Value!.FpsLimit} FPS")),

            Divider(),
            Section("Quality"),
            Row("Antialiasing", Controls.Dropdown(() => options.Value?.AntialiasModes ?? [], () => config.Value?.Antialias ?? 0, v => set("antialias", v, null))),
            Row("Shadow Distance", Controls.Dropdown(() => options.Value?.ShadowCascadeLevels ?? [], () => config.Value?.ShadowCascadeLevel ?? 0, v => set("shadowCascadeLevel", v, null))),
            Row("Shadow Resolution", Controls.Dropdown(() => options.Value?.ShadowResolutions ?? [], () => config.Value?.ShadowResolution ?? 0, v => set("shadowResolution", v, null))),
            Row("Render Distance", Controls.Dropdown(() => options.Value?.RenderDistanceNames ?? [], () => config.Value?.RenderDistance ?? 0, v => set("renderDistance", v, null))),

            Divider(),
            Section("Advanced"),
            Row("Low Latency", Controls.Checkbox(() => config.Value?.LowLatency ?? false, () => set("lowLatency", !(config.Value?.LowLatency ?? false), null))),
            Row("Outline Width",
                Controls.Slider(() => config.Value?.LineWidth ?? 1, v => set("lineWidth", (float)v, null), 0.5, 4, 0.1),
                Value(() => $"{config.Value?.LineWidth ?? 0:F1}")),
            Row("Distant Outlines", Controls.Dropdown(() => DistantOutlineBehaviours, () => config.Value?.DistantOutlineBehavior ?? 0, v => set("distantOutlineBehavior", v, null))));

    private static Node AudioTab(Signal<SettingsSnapshot?> config, Action<string, object?, string?> set)
        => Content(
            Section("Volume"),
            Row("Mute All", Controls.Checkbox(() => config.Value?.MuteAll ?? false, () => set("muteAll", !(config.Value?.MuteAll ?? false), null))),
            Row("Master Volume",
                Controls.Slider(() => config.Value?.MasterVolume ?? 0, v => set("masterVolume", (float)v, null), 0, 1, 0.01),
                Value(() => $"{Math.Floor((config.Value?.MasterVolume ?? 0) * 100 + 0.5)}%")),
            Row("Music Volume",
                Controls.Slider(() => config.Value?.MusicVolume ?? 0, v => set("musicVolume", (float)v, null), 0, 1, 0.01),
                Value(() => $"{Math.Floor((config.Value?.MusicVolume ?? 0) * 100 + 0.5)}%")),
            Row("Effects Volume",
                Controls.Slider(() => config.Value?.EffectsVolume ?? 0, v => set("effectsVolume", (float)v, null), 0, 1, 0.01),
                Value(() => $"{Math.Floor((config.Value?.EffectsVolume ?? 0) * 100 + 0.5)}%")),

            Divider(),
            Row("Remastered Music", Controls.Checkbox(() => config.Value?.RemasteredMusic ?? false, () => set("remasteredMusic", !(config.Value?.RemasteredMusic ?? false), null))));

    private static Node GameTab(Signal<SettingsSnapshot?> config, Action<string, object?, string?> set, Action onResetCamera)
        => Content(
            Section("Camera"),
            Row("Field of View",
                Controls.Slider(() => config.Value?.Fov ?? 60, v => set("fov", (float)v, null), 58.7, 120, 0.5),
                Value(() => $"{config.Value?.Fov ?? 0:F1}°")),
            Row("Smooth FOV", Controls.Checkbox(() => config.Value?.SmoothFov ?? false, () => set("smoothFov", !(config.Value?.SmoothFov ?? false), null))),
            Row("Follow Y Offset",
                Controls.Slider(() => config.Value?.FollowY ?? 0, v => set("followY", (int)v, null), -160, 500, 1),
                Value(() => $"{config.Value?.FollowY ?? 0}")),
            Row("Follow Z Offset",
                Controls.Slider(() => config.Value?.FollowZ ?? 0, v => set("followZ", (int)v, null), -500, 500, 1),
                Value(() => $"{config.Value?.FollowZ ?? 0}")),

            Divider(),
            ResetLink("Reset Camera Defaults", onResetCamera));

    private static Node KeyboardTab(Signal<SettingsSnapshot?> config, Signal<string?> capturing, Action<string, object?, string?> set, Action onReset)
        => Content(
            Section("Key Bindings"),
            ResetLink("Reset All to Defaults", onReset),
            Divider(),
            Sx.For(
                each: () => config.Value?.KeyBindings ?? [],
                row: (binding, _) => Row(
                    binding.DisplayName.Length > 0 ? binding.DisplayName : binding.Action,
                    CapturingButton(
                        () => capturing.Value == binding.Action,
                        () => capturing.Value == binding.Action ? "Press any key..." : KeyCodeToName(binding.KeyCode),
                        () => capturing.Value = binding.Action))),
            Sx.Show(
                when: () => capturing.Value is not null,
                then: () => Sx.Text(
                    style: () => new TextStyles
                    {
                        FontSize = 12,
                        ForegroundColor = new Color(255, 152, 0, 153),
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                    },
                    layout: () => new Styles { MarginTop = 10 },
                    text: () => "Press any key to bind, or ESC to cancel...")));

    // ── Primitives ────────────────────────────────────────────────────

    private static Node Section(string label) => Sx.Text(
        style: () => new TextStyles
        {
            FontSize = 11,
            FontStyle = FontStyle.Bold,
            ForegroundColor = new Color(255, 255, 255, 89),
        },
        layout: () => new Styles { MarginBottom = 10, FlexShrink = 0 },
        text: () => label);

    private static Node Row(string label, params object?[] controls) => Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = Justify.SpaceBetween,
            GapColumn = 16,
            GapRow = 16,
            PaddingTop = 6,
            PaddingBottom = 6,
            FlexShrink = 0,
        },
        children:
        [
            Sx.Text(
                style: () => new TextStyles { FontSize = 14, ForegroundColor = new Color(255, 255, 255, 204) },
                layout: () => new Styles { MinWidth = 140 },
                text: () => label),
            .. controls
        ]);

    private static Node Value(Func<string> text) => Sx.Text(
        style: () => new TextStyles { FontSize = 12, ForegroundColor = new Color(255, 255, 255, 128) },
        layout: () => new Styles { MinWidth = 50, MarginLeft = 8 },
        text: text);

    private static Node Divider() => Sx.View(style: () => new Styles
    {
        Height = 1,
        MarginTop = 12,
        MarginBottom = 12,
        FlexShrink = 0,
        BackgroundColor = new Color(255, 255, 255, 15),
    });

    private static Node ResetLink(string label, Action onPress)
    {
        var hovered = new Signal<bool>(false);

        var node = Sx.Text(
            style: () => new TextStyles
            {
                FontSize = 12,
                ForegroundColor = hovered.Value ? new Color(255, 152, 0, 178) : new Color(255, 255, 255, 89),
            },
            layout: () => new Styles { PaddingTop = 8, PaddingBottom = 8, FlexShrink = 0 },
            text: () => label);

        node.MousePressed = _ => onPress();
        node.MouseEntered = _ => hovered.Value = true;
        node.MouseLeft = _ => hovered.Value = false;
        return node;
    }

    private static Node CapturingButton(Func<bool> capturing, Func<string> label, Action onPress)
        => Button.Create(
            label: label,
            onPress: onPress,
            active: capturing,
            style: () => new Styles { MinWidth = 160, JustifyContent = Justify.Center });

    private static Node ConfirmDialog(
        Func<string> title,
        Func<string> message,
        Action onConfirm,
        Action onCancel)
        => Sx.View(
            style: () => new Styles
            {
                Position = Position.Absolute,
                Top = 0,
                Left = 0,
                Right = 0,
                Bottom = 0,
                AlignItems = Align.Center,
                JustifyContent = Justify.Center,
                BackgroundColor = new Color(0, 0, 0, 128),
                ZIndex = 30,
            },
            onmousedown: _ => onCancel(),
            children: Sx.View(
                style: () => new Styles
                {
                    BackgroundColor = Theme.Panel,
                    BorderTop = 1,
                    BorderBottom = 1,
                    BorderLeft = 1,
                    BorderRight = 1,
                    BorderColor = Theme.SurfaceBorder,
                    BorderRadius = 10,
                    PaddingTop = 24,
                    PaddingBottom = 24,
                    PaddingLeft = 28,
                    PaddingRight = 28,
                    MinWidth = 320,
                    AlignItems = Align.Center,
                    FlexDirection = FlexDirection.Column,
                },
                children:
                [
                    Sx.Text(
                        style: () => new TextStyles { FontSize = 16, FontStyle = FontStyle.Bold },
                        layout: () => new Styles { MarginBottom = 8 },
                        text: title),
                    Sx.Text(
                        style: () => new TextStyles
                        {
                            FontSize = 13,
                            ForegroundColor = Theme.TextMuted,
                            HorizontalAlignment = TextHorizontalAlignment.Center,
                        },
                        layout: () => new Styles { MarginBottom = 20 },
                        text: message),
                    Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Row, JustifyContent = Justify.Center, GapColumn = 10, GapRow = 10 },
                        children:
                        [
                            Button.Create(label: () => "Yes", onPress: onConfirm, variant: ButtonVariant.Primary),
                            Button.Create(label: () => "No", onPress: onCancel)
                        ])
                ]));

    /// <summary>SDL key code as a readable name, for the key-binding rows.</summary>
    private static string KeyCodeToName(int code)
    {
        if (code == 0)
            return "None";

        var name = code switch
        {
            8 => "Backspace", 9 => "Tab", 13 => "Enter",
            16 => "Shift", 17 => "Ctrl", 18 => "Alt",
            27 => "Escape", 32 => "Space",
            33 => "PageUp", 34 => "PageDown", 35 => "End", 36 => "Home",
            37 => "Left", 38 => "Up", 39 => "Right", 40 => "Down",
            44 => "PrintScreen", 45 => "Insert", 46 => "Delete",
            91 => "LWin", 92 => "RWin",
            112 => "F1", 113 => "F2", 114 => "F3", 115 => "F4",
            116 => "F5", 117 => "F6", 118 => "F7", 119 => "F8",
            120 => "F9", 121 => "F10", 122 => "F11", 123 => "F12",
            144 => "NumLock", 145 => "ScrollLock",
            160 => "LShift", 161 => "RShift", 162 => "LCtrl", 163 => "RCtrl",
            164 => "LAlt", 165 => "RAlt",
            186 => ";", 187 => "=", 188 => ",", 189 => "-", 190 => ".", 191 => "/", 192 => "`",
            219 => "[", 220 => "\\", 221 => "]", 222 => "'",
            106 => "NumPad*", 107 => "NumPad+", 109 => "NumPad-", 110 => "NumPad.", 111 => "NumPad/",
            _ => null
        };

        if (name is not null)
            return name;
        if (code is >= 65 and <= 90)
            return ((char)code).ToString();
        if (code is >= 48 and <= 57)
            return ((char)code).ToString();
        if (code is >= 96 and <= 105)
            return $"NumPad{code - 96}";

        return $"Key(0x{code:x})";
    }
}
