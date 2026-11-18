using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal static class RaceChatView
{
    public static Node Build(RaceChatState chat, Func<bool> classic, Action<string> send, Action close)
        => Sx.View(
            name: () => "Race chat",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: ((1 - SettingsMenu.HudSafeZoneHorizontal) * 50).Percent(),
                marginLeft: (classic() ? 8 : 16) * SettingsMenu.HudScale * RadarScale(classic()),
                bottom: ((1 - SettingsMenu.HudSafeZoneVertical) * 50).Percent(),
                marginBottom: 190 * SettingsMenu.HudScale * RadarScale(classic()),
                width: 340 * SettingsMenu.HudScale,
                maxWidth: 46.Percent(),
                maxHeight: 45.Percent(),
                height: chat.Open.Value ? 220 * SettingsMenu.HudScale : null,
                flexDirection: FlexDirection.Column,
                backgroundColor: chat.Open.Value
                    ? classic() ? new Color(22, 32, 42, 205) : Theme.MenuPanel : null,
                borderTop: chat.Open.Value ? 1 : 0,
                borderBottom: chat.Open.Value ? 1 : 0,
                borderLeft: chat.Open.Value ? 1 : 0,
                borderRight: chat.Open.Value ? 1 : 0,
                borderColor: !chat.Open.Value ? Color.Transparent
                    : classic() ? new Color(155, 215, 230, 200) : Theme.MenuBorder,
                borderRadius: classic() ? 3 : 8,
                paddingLeft: chat.Open.Value ? 8 : 0,
                paddingRight: chat.Open.Value ? 8 : 0,
                paddingTop: chat.Open.Value ? 6 : 0,
                paddingBottom: chat.Open.Value ? 6 : 0,
                gapRow: 6,
                pointerEvents: chat.Open.Value
            ),
            children: [
                Sx.Show(
                    () => chat.Open.Value,
                    () => Sx.View(style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0), children: [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 12,
                                fontFamily: classic() ? FontFamily.Adventure : FontFamily.NotoSans,
                            foregroundColor: classic() ? new Color(190, 245, 255) : Theme.MenuBlue
                            ),
                            text: () => "Chat"
                        ),
                        Sx.View(style: () => Sx.Styles(flexGrow: 1)),
                        Button.Create(() => "×", close, size: ButtonSize.Sm)
                    ])
                ),
                Sx.Show(
                    () => chat.Open.Value,
                    () => History(chat),
                    () => new RecentMessages(chat)
                ),
                Sx.Show(() => chat.Open.Value, () => Input(chat, classic, send))
            ]
        );

    private static float RadarScale(bool classic)
    {
        if (!classic) return 1;
        var scale = MathF.Min(G.Viewport.X / 960, G.Viewport.Y / 540) / MathF.Max(G.Scale, 0.01f);
        return scale > 0 ? scale : 1;
    }

    private sealed class RecentMessages : View
    {
        private readonly Signal<long> _now = new(System.Environment.TickCount64);

        public RecentMessages(RaceChatState chat)
        {
            Styles = Sx.Styles(
                flexDirection: FlexDirection.Column,
                maxHeight: 140 * SettingsMenu.HudScale,
                overflow: Overflow.Hidden,
                justifyContent: Justify.FlexEnd,
                gapRow: 3,
                pointerEvents: false
            );
            AddChild(Sx.For(
                () => chat.Lines.Value.TakeLast(4).Where(line => line.RecentOpacity(_now.Value) > 0).ToArray(),
                (line, _) => Line(line, () => line.RecentOpacity(_now.Value))
            )
            );
        }

        protected override void GameTick() => _now.Value = System.Environment.TickCount64;
    }

    private static Node History(RaceChatState chat)
    {
        var history = new ChatHistory(chat)
        {
            Name = "Race chat history",
            Styles = Sx.Styles(
                flexGrow: 1,
                minHeight: 0,
                overflow: Overflow.Scroll,
                flexDirection: FlexDirection.Column,
                paddingRight: 8
            )
        };
        history.AddChild(Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
            flexDirection: FlexDirection.Column,
                gapRow: 4
            ),
            children: Sx.For(() => chat.Lines.Value, (line, _) => Line(line))
        )
        );
        return history;
    }

    private static Node Line(RaceChatLine line, Func<float>? opacity = null)
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                paddingTop: ChatOutlinePadding(),
                paddingBottom: ChatOutlinePadding(),
                paddingLeft: ChatOutlinePadding(),
                paddingRight: ChatOutlinePadding(),
                opacity: opacity?.Invoke() ?? 1f
            ),
            children: Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 13 * SettingsMenu.HudScale,
                    foregroundColor: line.IsGame ? new Color(255, 230, 80) : Color.White,
                    strokeColor: new Color(0, 0, 0),
                    strokeWidth: ChatOutlineWidth(),
                    breakType: BreakType.Character,
                    overflowBehavior: OverflowBehavior.ContinueVertically
                ),
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                text: () => line.IsGame ? $"(GAME) {line.Message}" : $"{line.Sender}: {line.Message}"
            )
        );

    private static float ChatOutlinePadding()
        => MathF.Ceiling(ChatOutlineWidth() + 1) / Math.Max(G.Scale, 0.01f);

    private static float ChatOutlineWidth()
    {
        var scale = SettingsMenu.HudScale * Math.Max(G.Scale, 0.01f);
        // Keep the outline and its antialiasing inside the glyph's distance field.
        var available = Math.Max(0, 13 * scale / 6 - 0.75f);
        return Math.Min(1.5f * scale, available);
    }

    private static Node Input(RaceChatState chat, Func<bool> classic, Action<string> send)
    {
        TextInput? input = null;
        input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                minHeight: 30,
                flexShrink: 0,
                paddingLeft: 6,
                paddingRight: 6,
                paddingTop: 4,
                paddingBottom: 4,
                backgroundColor: classic() ? new Color(0, 10, 20, 160) : Theme.MenuFill,
                borderRadius: classic() ? 2 : 5
            ),
            text: () => chat.Draft.Value,
            onchange: value =>
            {
                var text = value[..Math.Min(value.Length, RaceChat.MaxMessageLength)];
                chat.Draft.Value = text;
                if (text != value) input!.SetText(text);
            },
            placeholder: () => "Say...  (Enter to send, Esc to close)",
            onsubmit: send
        );
        input.Name = "Race chat input";
        input.TextStyles = Sx.TextStyles(
            fontSize: 12 * SettingsMenu.HudScale,
            foregroundColor: new Color(235, 250, 255)
        ).Resolved;
        FocusManager.FocusedNode = input;
        return input;
    }

    private sealed class ChatHistory(RaceChatState chat) : ScrollView
    {
        private long _lastId = -1;

        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            // Scroll extent is refreshed immediately before painting.
            var id = chat.Lines.Value.LastOrDefault()?.Id ?? 0;
            if (id != _lastId)
            {
                ScrollTop = float.MaxValue;
                _lastId = id;
            }

            base.RenderContent(position, size);
        }
    }
}
