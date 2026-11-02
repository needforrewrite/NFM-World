using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.UI;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Settings;
using NFMWorld.UI.Hud;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Hud;
using SignalsDotnet;
using NFMWorldLibrary.Util;
using NFMWorld.DriverInterface;

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
    private sealed class FeedbackView : View
    {
        public Signal<bool> Pressed { get; } = new(false);
        private long _releasedAt;
        private bool _held;

        public void Press()
        {
            _held = true;
            Pressed.Value = true;
        }

        public void Release()
        {
            if (!_held) return;
            _held = false;
            _releasedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        protected override void OnMouseReleased(NFMWorld.ClayDom.Events.MouseEvent @event)
        {
            base.OnMouseReleased(@event);
            Release();
        }

        protected override bool OnPostLayout()
        {
            if (Pressed.Value && !_held
                && System.Diagnostics.Stopwatch.GetElapsedTime(_releasedAt).TotalSeconds >= 0.14)
                Pressed.Value = false;
            return base.OnPostLayout();
        }
    }

    private sealed class InputView(KeyboardNavigation navigation, Func<Key, bool> handleKey, Func<bool> capturing,
        Func<bool> previewActive, Func<bool> menuPreviewActive,
        Func<UiState.RaceState?> menuHudPreview, Func<bool> hudPreviewActive) : View
    {
        public bool HandleKey(Key key) => handleKey(key);
        public void HandleRepeatedKey(Key key, in Keys keys)
        {
            if (capturing()) return;
            if (!handleKey(key)) GameSparker.UiRenderer?.HandleKeyPressed(key, keys);
        }
        public bool PreviewActive => previewActive();
        public bool ScenePreviewActive => previewActive() || menuPreviewActive();
        public UiState.RaceState? MenuHudPreview => menuHudPreview();
        public bool HudPreviewActive => hudPreviewActive();

        protected override bool OnPostLayout()
        {
            var changed = base.OnPostLayout();
            if (!GameSparker.devConsole.IsOpen()) navigation.UpdateFocus(this);
            return changed;
        }
    }

    public static bool CameraPreviewActive => FindPreview(GameSparker.UiRenderer?.ActiveRoot);
    public static bool ScenePreviewActive => FindPreview(GameSparker.UiRenderer?.ActiveRoot, scene: true);
    public static bool HudPreviewActive => FindPreview(GameSparker.UiRenderer?.ActiveRoot, hud: true);
    public static UiState.RaceState? MenuHudPreviewData => FindMenuHudPreview(GameSparker.UiRenderer?.ActiveRoot);

    private static UiState.RaceState? FindMenuHudPreview(Node? node)
    {
        if (node is null || node is Component { IsDisplayed: false }) return null;
        if (node is InputView settings) return settings.MenuHudPreview;
        foreach (var child in node.VisualChildren)
            if (FindMenuHudPreview(child) is { } preview) return preview;
        return null;
    }

    private static bool FindPreview(Node? node, bool scene = false, bool hud = false)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView settings) return hud ? settings.HudPreviewActive : scene ? settings.ScenePreviewActive : settings.PreviewActive;
        return node.VisualChildren.Any(child => FindPreview(child, scene, hud));
    }

    public static bool HandleKeyPressed(Node? node, Key key)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView settings) return settings.HandleKey(key);
        foreach (var child in node.VisualChildren)
            if (HandleKeyPressed(child, key)) return true;
        return false;
    }

    public static bool HandleKeyRepeated(Node? node, Key key, in Keys keys)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView settings)
        {
            settings.HandleRepeatedKey(key, keys);
            return true;
        }
        foreach (var child in node.VisualChildren)
            if (HandleKeyRepeated(child, key, keys)) return true;
        return false;
    }

    private static readonly Color Blue = new(128, 170, 255);
    private static readonly Color TextBlue = new(185, 211, 255);
    private static readonly Color Panel = new(12, 22, 42, 245);
    private static readonly Color Border = new(128, 170, 255, 100);
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

    public static Node Build(Action onClose, bool menuPreviewAvailable = false)
    {
        SettingsMenu.RefreshSupportedResolutions();
        var tab = new Signal<Tab>(Tab.Video);
        var config = new Signal<SettingsSnapshot?>(SettingsMenu.GetCurrentSnapshot());
        var options = new Signal<AvailableOptions?>(SettingsMenu.GetAvailableOptions());
        var capturing = new Signal<string?>(null);
        var confirm = new Signal<(string Title, string Message, Action OnConfirm)?>(null);
        var restartRequired = new Signal<bool>(false);
        var cameraPreview = new Signal<bool>(false);
        var menuPreview = new Signal<bool>(false);
        var hudPreview = new Signal<bool>(false);
        var previewData = new UiState.RaceState();
        previewData.IsPreview = true;
        previewData.ArrowAngle = 0;
        previewData.Paused.Value = true;
        previewData.TotalLaps.Value = 1;
        var cameraPresetEditor = new Signal<bool>(false);
        var applied = new Signal<bool>(false);
        var closeAfterRestartPrompt = false;
        var navigation = new KeyboardNavigation();
        InputView? root = null;

        // Cancel restores the last applied settings.
        var original = SettingsMenu.SaveConfigToString();

        void Set(string key, object? value, string? action = null)
        {
            SettingsMenu.ApplySetting(key, value, action);
            Refresh();
        }

        void Refresh()
        {
            config.Value = SettingsMenu.GetCurrentSnapshot();
            applied.Value = false;
        }

        void ConfirmReset(string title, string message, Action reset)
        {
            capturing.Value = null;
            confirm.Value = (title, message, () =>
            {
                reset();
                confirm.Value = null;
                Refresh();
            });
        }

        bool HandleKey(Key key)
        {
            if (capturing.Value is { } action)
            {
                capturing.Value = null;
                if (key != Key.Escape && key != Key.None)
                    Set("keyBinding", (int)key, action);
                return true;
            }
            if (root is not null && navigation.HandleKey(root, key)) return true;
            if (confirm.Value is not null || restartRequired.Value)
            {
                if (key == Key.Escape)
                {
                    if (restartRequired.Value)
                        DismissRestart();
                    else
                        confirm.Value = null;
                }
                return true;
            }
            if (cameraPresetEditor.Value)
            {
                if (key == Key.Escape) cameraPresetEditor.Value = false;
                return key == Key.Escape;
            }
            if (key != Key.Escape) return false;
            if (hudPreview.Value)
            {
                hudPreview.Value = false;
                return true;
            }
            if (menuPreview.Value)
            {
                menuPreview.Value = false;
                return true;
            }
            if (cameraPreview.Value)
            {
                cameraPreview.Value = false;
                return true;
            }
            Cancel();
            return true;
        }

        void Cancel()
        {
            SettingsMenu.LoadConfigFromSnapshot(original);
            SettingsMenu.ApplySettings(out _);
            onClose();
        }

        void DismissRestart()
        {
            restartRequired.Value = false;
            if (closeAfterRestartPrompt)
                onClose();
        }

        void Save(bool close)
        {
            if (!SettingsMenu.SaveConfigAndCheckRestart(out var needsRestart))
            {
                GameSparker.MessageWindow.ShowMessage("Could not save settings",
                    "The configuration file could not be saved. Check the console for the error and try again.");
                return;
            }
            original = SettingsMenu.SaveConfigToString();
            applied.Value = true;
            if (needsRestart)
            {
                closeAfterRestartPrompt = close;
                restartRequired.Value = true;
            }
            else if (close)
                onClose();
        }

        Node Body() => Sx.View(
            style: () => new Styles
            {
                Position = Position.Relative,
                Visibility = cameraPreview.Value || menuPreview.Value || hudPreview.Value ? Visibility.Hidden : Visibility.Visible,
                PointerEvents = !cameraPreview.Value && !menuPreview.Value && !hudPreview.Value,
                Width = MeasurementWidthHeight.Percent(90),
                MaxWidth = 1000,
                Height = MeasurementWidthHeight.Percent(90),
                FlexDirection = FlexDirection.Column,
                BackgroundColor = Panel,
                BorderTop = 1,
                BorderBottom = 1,
                BorderLeft = 1,
                BorderRight = 1,
                BorderColor = Border,
                BorderRadius = 12,
            },
            children:
            [
                Sx.Text(
                    style: () => new TextStyles
                    {
                        FontSize = 30,
                        FontFamily = FontFamily.Adventure,
                        FontStyle = FontStyle.Bold,
                        ForegroundColor = Blue,
                    },
                    layout: () => new Styles { MarginTop = 20, MarginLeft = 24, MarginBottom = 4, FlexShrink = 0 },
                    text: () => "SETTINGS"),
                Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(marginLeft: 24, marginBottom: 16, flexShrink: 0),
                    text: () => "Controls, display, audio and gameplay."),
                TabBar(tab, next =>
                {
                    capturing.Value = null;
                    tab.Value = next;
                }),
                Sx.Switch(
                    tab,
                    new Dictionary<Tab, Func<Node>>
                    {
                        [Tab.Keyboard] = () => KeyboardTab(config, capturing, Set, () => ConfirmReset(
                            "Reset Key Bindings",
                            "Are you sure you want to reset all keyboard bindings to their defaults?",
                            SettingsMenu.ResetKeyBindingDefaults)),
                        [Tab.Video] = () => VideoTab(config, options, Set),
                        [Tab.Audio] = () => AudioTab(config, Set),
                        [Tab.Game] = () => GameTab(config, options, Set, () => hudPreview.Value = true,
                            Refresh, () => cameraPresetEditor.Value = true,
                            () => cameraPreview.Value = true, () => ConfirmReset(
                            "Reset Camera",
                            "Are you sure you want to reset all camera settings to their defaults?",
                            SettingsMenu.ResetCameraDefaults), () => MainMenuSettings(config, options, Set,
                                menuPreviewAvailable, () => menuPreview.Value = true))
                    }),
                BottomBar(() => Save(true), () => Save(false), Cancel, () => applied.Value),
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
                        () => "Some graphics changes require a restart. Restart now to apply them?",
                        () => GameSparker.Game.Restart(),
                        DismissRestart))
            ]);

        root = new InputView(navigation, HandleKey, () => capturing.Value is not null,
            () => cameraPreview.Value || hudPreview.Value, () => menuPreview.Value,
            () => menuPreviewAvailable && hudPreview.Value ? previewData : null, () => hudPreview.Value)
        {
            Styles = Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
                alignItems: Align.Center, justifyContent: Justify.Center, zIndex: 20)
        };
        root.AddChild(Sx.View(style: () => Sx.Styles(position: Position.Absolute,
            top: 0, left: 0, right: 0, bottom: 0, pointerEvents: false,
            backgroundColor: cameraPreview.Value || menuPreview.Value || hudPreview.Value ? Theme.Transparent : new Color(3, 8, 20, 150))));
        root.AddChild(Body());
        root.AddChild(Sx.Show(() => cameraPreview.Value,
            () => KeyboardNavigation.Scope(CameraPreviewPanel(config, Set, Refresh, () => cameraPresetEditor.Value = true,
                () => cameraPreview.Value = false))));
        root.AddChild(Sx.Show(() => cameraPresetEditor.Value,
            () => KeyboardNavigation.Scope(CameraPresetEditor(config, Refresh, () => cameraPresetEditor.Value = false))));
        root.AddChild(Sx.Show(() => menuPreview.Value,
            () => KeyboardNavigation.Scope(MenuPreviewPanel(config, options, Set, () => menuPreview.Value = false))));
        root.AddChild(Sx.Show(() => hudPreview.Value, () => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0),
            children:
            [
                menuPreviewAvailable
                    ? Sx.Switch(Sx.Computed(() => config.Value?.HudSkin ?? NFMWorldLibrary.Hud.HudSkinId.Default),
                        HudSkinRegistry.Branches(previewData), () => HudSkinRegistry.BuildDefault(previewData))
                    : null,
                new HudSafeZoneGuide { Styles = Sx.Styles(width: "100%", height: "100%",
                    position: Position.Absolute, left: 0, top: 0, pointerEvents: false) },
                KeyboardNavigation.Scope(HudPreviewPanel(config, options, Set, () => hudPreview.Value = false))
            ])));
        return root;
    }

    // ── Shell ─────────────────────────────────────────────────────────

    private static Node TabBar(Signal<Tab> tab, Action<Tab> selectTab) => KeyboardNavigation.Row(Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            JustifyContent = Justify.FlexStart,
            GapColumn = 0,
            GapRow = 0,
            PaddingLeft = 24,
            PaddingRight = 24,
            BackgroundColor = new Color(7, 14, 28, 180),
            BorderBottom = 1,
            BorderColor = Border,
            FlexShrink = 0,
        },
        children:
        [
            TabButton("Keyboard", () => tab.Value == Tab.Keyboard, () => selectTab(Tab.Keyboard)),
            TabButton("Video", () => tab.Value == Tab.Video, () => selectTab(Tab.Video)),
            TabButton("Audio", () => tab.Value == Tab.Audio, () => selectTab(Tab.Audio)),
            TabButton("Game", () => tab.Value == Tab.Game, () => selectTab(Tab.Game))
        ]));

    private static Node TabButton(string label, Func<bool> isActive, Action onPress)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var node = Sx.View(
            style: () => Sx.Styles(position: Position.Relative,
                paddingTop: 14, paddingBottom: 14, paddingLeft: 24, paddingRight: 24,
                minWidth: 110, alignItems: Align.Center, justifyContent: Justify.Center,
                backgroundColor: isActive() ? new Color(22, 42, 80, 220)
                    : hovered.Value || focused.Value ? new Color(22, 42, 80, 130) : Theme.Transparent),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: _ => onPress(),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold,
                        foregroundColor: isActive() ? new Color(205, 227, 255)
                            : hovered.Value ? TextBlue : new Color(130, 155, 195)),
                    text: () => label),
                Sx.View(style: () => Sx.Styles(position: Position.Absolute,
                    left: 0, right: 0, bottom: 0, height: 3,
                    backgroundColor: focused.Value ? new Color(230, 240, 255) : isActive() ? Blue : Theme.Transparent,
                    pointerEvents: false))
            ]);
        return KeyboardNavigation.Bind(node, key =>
        {
            if (key is not (Key.Enter or Key.Space)) return false;
            onPress();
            return true;
        }, value => focused.Value = value, isActive);
    }

    private static Node SettingsButton(string label, Action onPress, Func<bool>? isActive = null,
        Func<string>? feedbackLabel = null)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var feedback = new FeedbackView { Styles = Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0) };
        feedback.AddChild(Sx.View(
            style: () => Sx.Styles(paddingTop: feedback.Pressed.Value ? 9 : 8,
                paddingBottom: feedback.Pressed.Value ? 7 : 8, paddingLeft: 18, paddingRight: 18,
                minWidth: 90, minHeight: 36, flexShrink: 0, alignItems: Align.Center, justifyContent: Justify.Center,
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1, borderRadius: 6,
                borderColor: feedback.Pressed.Value ? new Color(230, 240, 255)
                    : hovered.Value || focused.Value ? new Color(205, 227, 255) : isActive?.Invoke() == true ? Blue : Border,
                backgroundColor: feedback.Pressed.Value ? new Color(80, 125, 205)
                    : hovered.Value || focused.Value ? new Color(55, 90, 150, 190)
                    : isActive?.Invoke() == true ? new Color(42, 68, 115) : new Color(22, 42, 80, 145)),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ =>
            {
                hovered.Value = false;
                feedback.Release();
            },
            onmousedown: _ =>
            {
                feedback.Press();
                onPress();
                feedback.Release();
            },
            children: Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold,
                    foregroundColor: hovered.Value ? new Color(205, 227, 255) : TextBlue),
                text: () => feedbackLabel?.Invoke() ?? label)));
        return KeyboardNavigation.Bind(feedback, key =>
        {
            if (key is not (Key.Enter or Key.Space)) return false;
            feedback.Press();
            onPress();
            feedback.Release();
            return true;
        }, value => focused.Value = value);
    }

    private static Node BottomBar(Action onSave, Action onApply, Action onClose, Func<bool> applied) => KeyboardNavigation.Row(Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = Justify.FlexEnd,
            GapColumn = 12,
            GapRow = 12,
            PaddingTop = 16,
            PaddingBottom = 16,
            PaddingLeft = 24,
            PaddingRight = 24,
            BorderTop = 1,
            BorderColor = Border,
            BackgroundColor = new Color(7, 14, 28, 180),
            FlexShrink = 0,
        },
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                layout: () => Sx.Styles(flexGrow: 1), text: () => applied() ? "✓ Settings applied" : ""),
            SettingsButton("OK", onSave, () => true),
            SettingsButton("Cancel", onClose),
            SettingsButton("Apply", onApply, feedbackLabel: () => applied() ? "Applied" : "Apply")
        ]));

    private static Node Content(params object?[] children) => Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Column,
            FlexGrow = 1,
            FlexShrink = 1,
            MinHeight = 0,
            Overflow = Overflow.Scroll,
            PaddingTop = 20,
            PaddingLeft = 24,
            PaddingRight = 24,
            PaddingBottom = 20,
        },
        children: Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, width: "100%"),
            children: children));

    // ── Tabs ──────────────────────────────────────────────────────────

    private static Node VideoTab(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options, Action<string, object?, string?> set)
        => Content(
            Section("Display"),
            Row("Renderer", Controls.Dropdown(() => options.Value?.Renderers ?? [], () => config.Value?.SelectedRenderer ?? 0, v => set("selectedRenderer", v, null))),
            Row("Resolution", Sx.Show(() => !string.IsNullOrEmpty(config.Value?.BorderlessResolution),
                () => Sx.View(
                    style: () => Sx.Styles(width: 240, paddingTop: 8, paddingBottom: 8,
                        paddingLeft: 12, paddingRight: 12, borderTop: 1, borderBottom: 1,
                        borderLeft: 1, borderRight: 1, borderColor: new Color(128, 170, 255, 45),
                        borderRadius: 6, backgroundColor: new Color(22, 42, 80, 80), pointerEvents: false),
                    children: Sx.Text(style: () => Sx.TextStyles(fontSize: 13,
                            foregroundColor: new Color(130, 155, 195)),
                        text: () => $"{config.Value!.BorderlessResolution} (Native)")),
                () => Controls.Dropdown(() => SettingsMenu.Resolutions,
                    () => config.Value?.SelectedResolution ?? 0, v => set("selectedResolution", v, null)))),
            Row("Display Mode", Controls.Dropdown(() => options.Value?.DisplayModes ?? [], () => config.Value?.SelectedDisplayMode ?? 0, v => set("selectedDisplayMode", v, null))),
            Row("VSync", Controls.Checkbox(() => config.Value?.Vsync ?? false, () => set("vsync", !(config.Value?.Vsync ?? false), null))),
            Row("FPS Limit",
                Controls.Slider(() => config.Value?.FpsLimit ?? 0, v => set("fpsLimit", (int)v == 62 ? 0 : (int)v, null), 62, 240, 1),
                Value(() => (config.Value?.FpsLimit ?? 0) == 0 ? "Unlimited" : $"{config.Value!.FpsLimit} FPS")),

            Divider(),
            Section("Quality"),
            Row("Antialiasing", Controls.Dropdown(() => options.Value?.AntialiasModes ?? [], () => config.Value?.Antialias ?? 0, v => set("antialias", v, null))),
            Sx.Show(() => config.Value?.AntialiasRequiresRestart == true,
                () => Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(marginTop: 4, marginBottom: 12, marginLeft: 12),
                    text: () => $"Active: {SettingsMenu.AntialiasModes[config.Value!.AppliedAntialias]}. Restart to apply your selection.")),
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

    private static Node GameTab(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options,
        Action<string, object?, string?> set, Action previewHud, Action refresh, Action manage,
        Action preview, Action onResetCamera, Func<Node> menuSettings)
        => Content(
            Section("HUD"),
            Row("Skin", HudSkinDropdown(config, options, set)),
            Description(() => options.Value?.HudSkins.FirstOrDefault(candidate => candidate.Id == config.Value?.HudSkin) is { } skin
                    ? $"{skin.Description}{(skin.Author.Length > 0 ? $" — {skin.Author}" : "")}" : ""),
            Sx.View(style: () => Sx.Styles(alignSelf: Align.FlexStart, flexShrink: 0),
                children: SettingsButton("Preview HUD", previewHud)),
            Divider(),
            Section("Camera"),
            Row("Camera Preset", CameraPresetDropdown(config, refresh)),
            Description(() => "Adjust your driving camera with a live view from behind the car."),
            KeyboardNavigation.Row(Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0,
                    flexWrap: Wrap.Wrap, gapColumn: 12, gapRow: 12),
                children: [SettingsButton("Adjust Camera", preview), SettingsButton("Manage Presets", manage)])),
            Divider(),
            ResetLink("Reset Camera Defaults", onResetCamera),
            Divider(),
            menuSettings());

    private static Node HudSkinDropdown(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options,
        Action<string, object?, string?> set) => Controls.Dropdown(
        () => options.Value?.HudSkins.Select(skin => skin.Name).ToArray() ?? [],
        () => options.Value?.HudSkins.ToList().FindIndex(skin => skin.Id == config.Value?.HudSkin) ?? -1,
        index =>
        {
            if (options.Value?.HudSkins is { } skins && index >= 0 && index < skins.Count)
                set("hudSkin", skins[index].Id, null);
        }, "Saved skin unavailable (using Classic)");

    private static Node HudPreviewPanel(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options,
        Action<string, object?, string?> set, Action back) => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, left: "3%", bottom: "3%",
            width: "36%", minWidth: 280, maxWidth: 420, flexDirection: FlexDirection.Column,
            flexShrink: 0, gapRow: 10, paddingTop: 12, paddingBottom: 12, paddingLeft: 16, paddingRight: 16,
            backgroundColor: new Color(12, 22, 42, 220), borderColor: Border, borderRadius: 10,
            borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure, foregroundColor: Blue),
                text: () => "HUD PREVIEW"),
            HudSkinDropdown(config, options, set),
            HudPreviewSlider("HUD Scale", () => config.Value?.HudScale ?? HudScaleSettings.Default,
                value => set("hudScale", (float)value, null), HudScaleSettings.Minimum, HudScaleSettings.Maximum, 0.05),
            HudPreviewSlider("Horizontal Safe Zone", () => config.Value?.HudSafeZoneHorizontal ?? HudSafeZoneSettings.Default,
                value => set("hudSafeZoneHorizontal", (float)value, null), HudSafeZoneSettings.Minimum, HudSafeZoneSettings.Maximum, 0.01),
            HudPreviewSlider("Vertical Safe Zone", () => config.Value?.HudSafeZoneVertical ?? HudSafeZoneSettings.Default,
                value => set("hudSafeZoneVertical", (float)value, null), HudSafeZoneSettings.Minimum, HudSafeZoneSettings.Maximum, 0.01),
            SettingsButton("Back to Settings", back)
        ]);

    private static Node HudPreviewSlider(string label, Func<double> value, Action<double> set,
        double min, double max, double step) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 6), children:
        [
            Sx.View(style: () => Sx.Styles(justifyContent: Justify.SpaceBetween), children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue), text: () => label),
                Value(() => $"{Math.Round(value() * 100)}%")
            ]),
            Controls.Slider(value, set, min, max, step)
        ]);

    private static Node MainMenuSettings(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options,
        Action<string, object?, string?> set, bool previewAvailable, Action preview) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0), children:
        [
            Section("Main Menu"),
            Description(() => previewAvailable
                    ? "Choose a background stage and soundtrack with a clear view of the menu."
                    : "These choices take effect when you return to the main menu. Preview is available there."),
            previewAvailable
                ? Sx.View(style: () => Sx.Styles(alignSelf: Align.FlexStart),
                    children: SettingsButton("Choose Stage & Music", preview))
                : Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column), children:
                [
                    Row("Background Stage", MenuChoiceDropdown(
                        () => options.Value?.MenuStages ?? [], () => config.Value?.MenuStage ?? MenuPreferences.LastPlayedStage,
                        "Last played stage", MenuPreferences.LastPlayedStage, v => set("menuStage", v, null))),
                    Row("Menu Music", MenuChoiceDropdown(
                        () => options.Value?.MenuTracks ?? [], () => config.Value?.MenuMusic ?? MenuPreferences.StageSoundtrack,
                        "Stage soundtrack", MenuPreferences.StageSoundtrack, v => set("menuMusic", v, null)))
                ])
        ]);

    private static Node MenuPreviewPanel(Signal<SettingsSnapshot?> config, Signal<AvailableOptions?> options,
        Action<string, object?, string?> set, Action back) => Sx.View(
        style: () => Sx.Styles(position: Position.Absolute, left: "3%", right: "3%", bottom: "3%",
            flexDirection: FlexDirection.Row, alignItems: Align.Center, justifyContent: Justify.SpaceBetween,
            flexWrap: Wrap.Wrap, gapColumn: 16, gapRow: 12,
            paddingTop: 12, paddingBottom: 12, paddingLeft: 18, paddingRight: 18,
            backgroundColor: new Color(12, 22, 42, 220),
            borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
            borderColor: Border, borderRadius: 10),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure,
                foregroundColor: Blue), text: () => "MENU PREVIEW"),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                alignItems: Align.Center, gapColumn: 16, gapRow: 12), children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4), children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue), text: () => "Background Stage"),
                    MenuChoiceDropdown(
                        () => options.Value?.MenuStages ?? [], () => config.Value?.MenuStage ?? MenuPreferences.LastPlayedStage,
                        "Last played stage", MenuPreferences.LastPlayedStage, v => set("menuStage", v, null))
                ]),
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4), children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue), text: () => "Menu Music"),
                    MenuChoiceDropdown(
                        () => options.Value?.MenuTracks ?? [], () => config.Value?.MenuMusic ?? MenuPreferences.StageSoundtrack,
                        "Stage soundtrack", MenuPreferences.StageSoundtrack, v => set("menuMusic", v, null))
                ])
            ]),
            SettingsButton("Back to Settings", back)
        ]);

    private static Node MenuChoiceDropdown(Func<IReadOnlyList<string>> choices, Func<string> selected,
        string defaultLabel, string defaultValue, Action<string> select) => Controls.Dropdown(
        () => new[] { defaultLabel }.Concat(choices()).ToArray(),
        () => selected() == defaultValue ? 0 : Math.Max(0, Array.IndexOf(choices().ToArray(), selected()) + 1),
        index =>
        {
            var items = choices();
            if (index == 0) select(defaultValue);
            else if (index > 0 && index <= items.Count) select(items[index - 1]);
        });

    private static Node CameraPreviewPanel(Signal<SettingsSnapshot?> config,
        Action<string, object?, string?> set, Action refresh, Action manage, Action back)
        => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, left: "3%", right: "3%", bottom: "3%",
                flexDirection: FlexDirection.Column, gapRow: 12,
                paddingTop: 12, paddingBottom: 16, paddingLeft: 18, paddingRight: 18,
                backgroundColor: new Color(12, 22, 42, 220),
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: Border, borderRadius: 10),
            children:
            [
                Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row,
                        alignItems: Align.Center, justifyContent: Justify.SpaceBetween, gapColumn: 16,
                        flexWrap: Wrap.Wrap, gapRow: 8),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 18, fontFamily: FontFamily.Adventure,
                                foregroundColor: Blue), text: () => "CAMERA PREVIEW"),
                        Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row,
                                alignItems: Align.Center, gapColumn: 12, flexWrap: Wrap.Wrap, gapRow: 8),
                            children:
                            [
                                Sx.View(style: () => Sx.Styles(width: 220), children: CameraPresetDropdown(config, refresh)),
                                SettingsButton("Manage Presets", manage),
                                Controls.Checkbox(() => config.Value?.SmoothFov ?? false,
                                    () => set("smoothFov", !(config.Value?.SmoothFov ?? false), null),
                                    () => "Smooth FOV"),
                                SettingsButton("Back to Settings", back)
                            ])
                    ]),
                CameraControls(config, set)
            ]);

    private static Node CameraPresetDropdown(Signal<SettingsSnapshot?> config, Action refresh) => Controls.Dropdown(
        () => (config.Value?.ModifiedCameraPreset is not null ? new[] { "Modified" } : Array.Empty<string>())
            .Concat(config.Value?.CameraPresets.Select(p => p.Name) ?? []).ToArray(),
        () => config.Value is { } current
            ? current.ModifiedCameraPreset is not null ? current.SelectedCameraPreset + 1 : current.SelectedCameraPreset
            : -1,
        index =>
        {
            var presets = config.Value?.CameraPresets;
            if (config.Value?.ModifiedCameraPreset is not null)
            {
                if (index == 0)
                {
                    SettingsMenu.SelectModifiedCameraPreset();
                    refresh();
                    return;
                }
                index--;
            }
            if (presets is null || index < 0 || index >= presets.Count) return;
            SettingsMenu.SelectCameraPreset(presets[index].Name);
            refresh();
        }, unselectedLabel: "Modified");

    private static Node CameraPresetEditor(Signal<SettingsSnapshot?> config, Action refresh, Action close)
    {
        var custom = config.Value?.CameraPresets.Skip(2).ToArray() ?? [];
        var initial = custom.FirstOrDefault(p => p.Name == config.Value?.CameraPresetName);
        var target = new Signal<string?>(initial?.Name);
        var name = new Signal<string>(initial?.Name ?? "");
        var message = new Signal<string>("");
        var deleting = new Signal<bool>(false);

        void Save(bool update)
        {
            try
            {
                SettingsMenu.SaveCameraPreset(name.Value, update ? target.Value : null);
                refresh();
                target.Value = config.Value!.CameraPresetName;
                name.Value = target.Value!;
                deleting.Value = false;
                message.Value = "Preset saved. Use Apply or OK to keep your changes.";
            }
            catch (ArgumentException ex) { message.Value = ex.Message; }
        }

        return Sx.View(style: () => Sx.Styles(position: Position.Absolute,
                top: 0, bottom: 0, left: 0, right: 0, zIndex: 25,
                alignItems: Align.Center, justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 150)),
            children: Sx.View(style: () => Sx.Styles(width: "90%", maxWidth: 520,
                    paddingTop: 24, paddingBottom: 24, paddingLeft: 24, paddingRight: 24,
                    backgroundColor: Panel, borderColor: Border, borderTop: 1, borderBottom: 1,
                    borderLeft: 1, borderRight: 1, borderRadius: 10, flexDirection: FlexDirection.Column, gapRow: 14),
                children:
                [
                    Section("Camera Presets"),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue),
                        text: () => "Save the current camera values, or update and rename a custom preset."),
                    Controls.Dropdown(
                        () => new[] { "New preset" }.Concat(config.Value?.CameraPresets.Skip(2).Select(p => p.Name) ?? []).ToArray(),
                        () => Math.Max(0, Array.FindIndex(config.Value?.CameraPresets.Skip(2).ToArray() ?? [], p => p.Name == target.Value) + 1),
                        index =>
                        {
                            var presets = config.Value?.CameraPresets.Skip(2).ToArray() ?? [];
                            target.Value = index > 0 && index <= presets.Length ? presets[index - 1].Name : null;
                            name.Value = target.Value ?? "";
                            message.Value = "";
                            deleting.Value = false;
                        }),
                    Controls.TextField(() => name.Value, value => name.Value = value, () => "Preset name"),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                        text: () => $"Current: FOV {config.Value?.Fov:F1}° / Y {config.Value?.FollowY} / Z {config.Value?.FollowZ} / Smooth FOV {(config.Value?.SmoothFov == true ? "On" : "Off")}"),
                    Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10, gapRow: 8, flexWrap: Wrap.Wrap),
                        children:
                        [
                            SettingsButton("Save as New", () => Save(false)),
                            Sx.Show(() => target.Value is not null, () => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    SettingsButton("Update / Rename", () => Save(true)),
                                    SettingsButton("Remove", () => deleting.Value = true)
                                ]))
                        ]),
                    Sx.Show(() => deleting.Value, () => Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                        children:
                        [
                            Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue),
                                text: () => $"Remove '{target.Value}'? Your current camera values will stay unchanged."),
                            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10), children:
                            [
                                SettingsButton("Remove Preset", () =>
                                {
                                    if (target.Value is not { } preset) return;
                                    SettingsMenu.RemoveCameraPreset(preset);
                                    target.Value = null;
                                    name.Value = "";
                                    deleting.Value = false;
                                    refresh();
                                    message.Value = "Preset removed. Use Apply or OK to keep your changes.";
                                }),
                                SettingsButton("Keep Preset", () => deleting.Value = false)
                            ])
                        ])),
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue), text: () => message.Value),
                    SettingsButton("Done", close)
                ]));
    }

    private static Node CameraControls(Signal<SettingsSnapshot?> config, Action<string, object?, string?> set)
        => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                gapColumn: 16, gapRow: 12),
            children:
            [
                CameraRow("Field of View",
                    Controls.Slider(() => config.Value?.Fov ?? 60, v => set("fov", (float)v, null), CameraPreset.MinFov, CameraPreset.MaxFov, 0.5),
                    CameraValue(() => $"{config.Value?.Fov ?? 0:F1}°")),
                CameraRow("Follow Y Offset",
                    Controls.Slider(() => config.Value?.FollowY ?? 0, v => set("followY", (int)v, null), CameraPreset.MinFollowY, CameraPreset.MaxFollowY, 1),
                    CameraValue(() => $"{config.Value?.FollowY ?? 0}")),
                CameraRow("Follow Z Offset",
                    Controls.Slider(() => config.Value?.FollowZ ?? 0, v => set("followZ", (int)v, null), CameraPreset.MinFollowZ, CameraPreset.MaxFollowZ, 1),
                    CameraValue(() => $"{config.Value?.FollowZ ?? 0}"))
            ]);

    private static Node CameraValue(Func<string> text) => Sx.Text(
        style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue,
            horizontalAlignment: TextHorizontalAlignment.Right),
        layout: () => Sx.Styles(minWidth: 44, flexShrink: 0), text: text);

    private static Node CameraRow(string label, params object?[] controls) => KeyboardNavigation.Row(Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1,
            width: 0, minWidth: 240, gapRow: 6),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue), text: () => label),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween, gapColumn: 12), children: controls)
        ]));

    private static Node KeyboardTab(Signal<SettingsSnapshot?> config, Signal<string?> capturing, Action<string, object?, string?> set, Action onReset)
        => Content(
            Section("Key Bindings"),
            ResetLink("Reset Key Bindings to Defaults", onReset),
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
                        ForegroundColor = Blue,
                        HorizontalAlignment = TextHorizontalAlignment.Center,
                    },
                    layout: () => new Styles { MarginTop = 10 },
                    text: () => "Press any key to bind, or ESC to cancel...")));

    // ── Primitives ────────────────────────────────────────────────────

    private static Node Description(Func<string> text) => Sx.Text(
        style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
        layout: () => Sx.Styles(flexShrink: 0, marginTop: 4, marginBottom: 12),
        text: text);

    private static Node Section(string label) => Sx.Text(
        style: () => new TextStyles
        {
            FontSize = 14,
            FontFamily = FontFamily.Adventure,
            FontStyle = FontStyle.Bold,
            ForegroundColor = Blue,
        },
        layout: () => new Styles { MarginBottom = 10, FlexShrink = 0 },
        text: () => label);

    private static Node Row(string label, params object?[] controls) => KeyboardNavigation.Row(Sx.View(
        style: () => new Styles
        {
            FlexDirection = FlexDirection.Row,
            AlignItems = Align.Center,
            JustifyContent = Justify.SpaceBetween,
            GapColumn = 16,
            GapRow = 16,
            PaddingTop = 10,
            PaddingBottom = 10,
            PaddingLeft = 12,
            PaddingRight = 12,
            MarginBottom = 4,
            BackgroundColor = new Color(22, 42, 80, 80),
            BorderRadius = 6,
            FlexShrink = 0,
        },
        children:
        [
            Sx.Text(
                style: () => new TextStyles { FontSize = 14, ForegroundColor = TextBlue },
                layout: () => new Styles { MinWidth = 160, FlexShrink = 0 },
                text: () => label),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexGrow: 1, minWidth: 0,
                    maxWidth: 420, alignItems: Align.Center, justifyContent: Justify.FlexEnd, gapColumn: 12),
                children: controls)
        ]));

    private static Node Value(Func<string> text) => Sx.Text(
        style: () => new TextStyles { FontSize = 12, ForegroundColor = TextBlue },
        layout: () => new Styles { MinWidth = 65, FlexShrink = 0, MarginLeft = 8 },
        text: text);

    private static Node Divider() => Sx.View(style: () => new Styles
    {
        Height = 1,
        MarginTop = 12,
        MarginBottom = 12,
        FlexShrink = 0,
        BackgroundColor = Border,
    });

    private static Node ResetLink(string label, Action onPress)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);

        var node = Sx.Text(
            style: () => new TextStyles
            {
                FontSize = 12,
                ForegroundColor = hovered.Value || focused.Value ? new Color(205, 227, 255) : Blue,
            },
            layout: () => new Styles { AlignSelf = Align.FlexStart, MarginTop = 8, MarginBottom = 8, FlexShrink = 0 },
            text: () => label);

        node.MousePressed = _ => onPress();
        node.MouseEntered = _ => hovered.Value = true;
        node.MouseLeft = _ => hovered.Value = false;
        return KeyboardNavigation.Bind(node, key =>
        {
            if (key is not (Key.Enter or Key.Space)) return false;
            onPress();
            return true;
        }, value => focused.Value = value);
    }

    private static Node CapturingButton(Func<bool> capturing, Func<string> label, Action onPress)
    {
        var focused = new Signal<bool>(false);
        var node = Button.Create(
            label: label,
            onPress: onPress,
            active: capturing,
            style: () => new Styles
            {
                MinWidth = 160,
                JustifyContent = Justify.Center,
                BorderColor = focused.Value ? Blue : Theme.SurfaceBorder
            });
        return KeyboardNavigation.Bind(node, key =>
        {
            if (key is not (Key.Enter or Key.Space)) return false;
            onPress();
            return true;
        }, value => focused.Value = value);
    }

    private static Node ConfirmDialog(
        Func<string> title,
        Func<string> message,
        Action onConfirm,
        Action onCancel)
        => KeyboardNavigation.Scope(Sx.View(
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
            // Presses bubble leaf→root, and the card's buttons stop propagation, so a
            // press inside the dialog never reaches this cancel.
            onmousedown: _ => onCancel(),
            children: Sx.View(
                style: () => new Styles
                {
                    BackgroundColor = Panel,
                    BorderTop = 1,
                    BorderBottom = 1,
                    BorderLeft = 1,
                    BorderRight = 1,
                    BorderColor = Border,
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
                        style: () => new TextStyles { FontSize = 16, FontStyle = FontStyle.Bold, ForegroundColor = Blue },
                        layout: () => new Styles { MarginBottom = 8 },
                        text: title),
                    Sx.Text(
                        style: () => new TextStyles
                        {
                            FontSize = 13,
                            ForegroundColor = TextBlue,
                            HorizontalAlignment = TextHorizontalAlignment.Center,
                        },
                        layout: () => new Styles { MarginBottom = 20 },
                        text: message),
                    KeyboardNavigation.Row(Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Row, JustifyContent = Justify.Center, GapColumn = 10, GapRow = 10 },
                        children:
                        [
                            SettingsButton("Yes", onConfirm, () => true),
                            SettingsButton("No", onCancel)
                        ]))
                ])));

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
