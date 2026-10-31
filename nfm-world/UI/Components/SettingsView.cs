using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.UI;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Settings;
using SignalsDotnet;
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

    private sealed class InputView(Func<Key, bool> handleKey, Func<bool> previewActive) : View
    {
        public bool HandleKey(Key key) => handleKey(key);
        public bool PreviewActive => previewActive();
    }

    public static bool CameraPreviewActive => FindPreview(GameSparker.UiRenderer?.ActiveRoot);

    private static bool FindPreview(Node? node)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView settings) return settings.PreviewActive;
        return node.VisualChildren.Any(FindPreview);
    }

    public static bool HandleKeyPressed(Node? node, Key key)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is InputView settings) return settings.HandleKey(key);
        foreach (var child in node.VisualChildren)
            if (HandleKeyPressed(child, key)) return true;
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

    public static Node Build(Action onClose)
    {
        var tab = new Signal<Tab>(Tab.Video);
        var config = new Signal<SettingsSnapshot?>(SettingsMenu.GetCurrentSnapshot());
        var options = new Signal<AvailableOptions?>(SettingsMenu.GetAvailableOptions());
        var capturing = new Signal<string?>(null);
        var confirm = new Signal<(string Title, string Message, Action OnConfirm)?>(null);
        var restartRequired = new Signal<bool>(false);
        var cameraPreview = new Signal<bool>(false);
        var applied = new Signal<bool>(false);
        var closeAfterRestartPrompt = false;

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
            if (capturing.Value is { } action)
            {
                capturing.Value = null;
                if (key != Key.Escape && key != Key.None)
                    Set("keyBinding", (int)key, action);
                return true;
            }
            if (key != Key.Escape) return false;
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
                Visibility = cameraPreview.Value ? Visibility.Hidden : Visibility.Visible,
                PointerEvents = !cameraPreview.Value,
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
                        [Tab.Game] = () => GameTab(() => cameraPreview.Value = true, () => ConfirmReset(
                            "Reset Camera",
                            "Are you sure you want to reset all camera settings to their defaults?",
                            SettingsMenu.ResetCameraDefaults))
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

        var root = new InputView(HandleKey, () => cameraPreview.Value)
        {
            Styles = Sx.Styles(position: Position.Absolute, top: 0, left: 0, right: 0, bottom: 0,
                alignItems: Align.Center, justifyContent: Justify.Center, zIndex: 20)
        };
        root.AddChild(Sx.View(style: () => Sx.Styles(position: Position.Absolute,
            top: 0, left: 0, right: 0, bottom: 0, pointerEvents: false,
            backgroundColor: cameraPreview.Value ? Theme.Transparent : new Color(3, 8, 20, 150))));
        root.AddChild(Body());
        root.AddChild(Sx.Show(() => cameraPreview.Value,
            () => CameraPreviewPanel(config, Set, () => cameraPreview.Value = false)));
        return root;
    }

    // ── Shell ─────────────────────────────────────────────────────────

    private static Node TabBar(Signal<Tab> tab, Action<Tab> selectTab) => Sx.View(
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
        ]);

    private static Node TabButton(string label, Func<bool> isActive, Action onPress)
    {
        var hovered = new Signal<bool>(false);
        return Sx.View(
            style: () => Sx.Styles(position: Position.Relative,
                paddingTop: 14, paddingBottom: 14, paddingLeft: 24, paddingRight: 24,
                minWidth: 110, alignItems: Align.Center, justifyContent: Justify.Center,
                backgroundColor: isActive() ? new Color(22, 42, 80, 220)
                    : hovered.Value ? new Color(22, 42, 80, 130) : Theme.Transparent),
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
                    backgroundColor: isActive() ? Blue : Theme.Transparent,
                    pointerEvents: false))
            ]);
    }

    private static Node SettingsButton(string label, Action onPress, Func<bool>? isActive = null,
        Func<string>? feedbackLabel = null)
    {
        var hovered = new Signal<bool>(false);
        var feedback = new FeedbackView { Styles = Sx.Styles(flexDirection: FlexDirection.Column) };
        feedback.AddChild(Sx.View(
            style: () => Sx.Styles(paddingTop: feedback.Pressed.Value ? 9 : 8,
                paddingBottom: feedback.Pressed.Value ? 7 : 8, paddingLeft: 18, paddingRight: 18,
                minWidth: 90, alignItems: Align.Center, justifyContent: Justify.Center,
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1, borderRadius: 6,
                borderColor: feedback.Pressed.Value ? new Color(230, 240, 255)
                    : hovered.Value ? new Color(205, 227, 255) : isActive?.Invoke() == true ? Blue : Border,
                backgroundColor: feedback.Pressed.Value ? new Color(80, 125, 205)
                    : hovered.Value ? new Color(55, 90, 150, 190)
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
        return feedback;
    }

    private static Node BottomBar(Action onSave, Action onApply, Action onClose, Func<bool> applied) => Sx.View(
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
            PaddingLeft = 24,
            PaddingRight = 24,
            PaddingBottom = 20,
        },
        children: children);

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
                () => Controls.Dropdown(() => options.Value?.Resolutions ?? [],
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

    private static Node GameTab(Action preview, Action onResetCamera)
        => Content(
            Section("Camera"),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 14, foregroundColor: TextBlue),
                layout: () => Sx.Styles(marginBottom: 16),
                text: () => "Adjust your driving camera with a live view from behind the car."),
            SettingsButton("Adjust Camera", preview),
            Divider(),
            ResetLink("Reset Camera Defaults", onResetCamera));

    private static Node CameraPreviewPanel(Signal<SettingsSnapshot?> config,
        Action<string, object?, string?> set, Action back)
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
                                alignItems: Align.Center, gapColumn: 20),
                            children:
                            [
                                Controls.Checkbox(() => config.Value?.SmoothFov ?? false,
                                    () => set("smoothFov", !(config.Value?.SmoothFov ?? false), null),
                                    () => "Smooth FOV"),
                                SettingsButton("Back to Settings", back)
                            ])
                    ]),
                CameraControls(config, set)
            ]);

    private static Node CameraControls(Signal<SettingsSnapshot?> config, Action<string, object?, string?> set)
        => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexWrap: Wrap.Wrap,
                gapColumn: 16, gapRow: 12),
            children:
            [
                CameraRow("Field of View",
                    Controls.Slider(() => config.Value?.Fov ?? 60, v => set("fov", (float)v, null), 58.7, 120, 0.5),
                    CameraValue(() => $"{config.Value?.Fov ?? 0:F1}°")),
                CameraRow("Follow Y Offset",
                    Controls.Slider(() => config.Value?.FollowY ?? 0, v => set("followY", (int)v, null), -160, 500, 1),
                    CameraValue(() => $"{config.Value?.FollowY ?? 0}")),
                CameraRow("Follow Z Offset",
                    Controls.Slider(() => config.Value?.FollowZ ?? 0, v => set("followZ", (int)v, null), -500, 500, 1),
                    CameraValue(() => $"{config.Value?.FollowZ ?? 0}"))
            ]);

    private static Node CameraValue(Func<string> text) => Sx.Text(
        style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue,
            horizontalAlignment: TextHorizontalAlignment.Right),
        layout: () => Sx.Styles(minWidth: 44, flexShrink: 0), text: text);

    private static Node CameraRow(string label, params object?[] controls) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1,
            width: 0, minWidth: 240, gapRow: 6),
        children:
        [
            Sx.Text(style: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue), text: () => label),
            Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center,
                justifyContent: Justify.SpaceBetween, gapColumn: 12), children: controls)
        ]);

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

    private static Node Row(string label, params object?[] controls) => Sx.View(
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
        ]);

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

        var node = Sx.Text(
            style: () => new TextStyles
            {
                FontSize = 12,
                ForegroundColor = hovered.Value ? new Color(205, 227, 255) : Blue,
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
                    Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Row, JustifyContent = Justify.Center, GapColumn = 10, GapRow = 10 },
                        children:
                        [
                            SettingsButton("Yes", onConfirm, () => true),
                            SettingsButton("No", onCancel)
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
