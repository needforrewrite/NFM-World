using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal sealed class NotificationCenter(Action? requestAttention = null, TimeProvider? clock = null) : IDisposable
{
    private const string SoundPath = "data/sound/ui/notif.wav";
    private const int MaxVisible = 3;
    private const float FlashDurationMs = 300f;
    private const float FadeDurationMs = 1000f;
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(8);
    private static readonly Color CardFill = new(12, 22, 42);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Signal<IReadOnlyList<Notification>> _visible = new([]);
    private readonly List<Notification> _pending = [];
    private ISoundClip? _sound;
    private bool _soundChecked;

    public sealed record NotificationAction(string Label, Action Invoke, Func<bool> IsAvailable, string UnavailableMessage);

    // Sx.For keys cards by notification identity. The flash signal's hash changes as it fades.
    private sealed class Notification(
        object owner,
        string title,
        string message,
        string? key,
        long? expiresAtUnixMs,
        NotificationAction? action)
    {
        public object Owner { get; } = owner;
        public string Title { get; } = title;
        public string Message { get; } = message;
        public string? Key { get; } = key;
        public long? ExpiresAtUnixMs { get; } = expiresAtUnixMs;
        public NotificationAction? Action { get; } = action;
        public long ShownAt { get; set; }
        public long? FadeStartedAt { get; set; }
        public Signal<float> Flash { get; } = new(1f);
        public Signal<float> Opacity { get; } = new(1f);
    }

    public void Show(
        object owner,
        string title,
        string message,
        string? key = null,
        long? expiresAtUnixMs = null,
        NotificationAction? action = null
    )
    {
        if (expiresAtUnixMs <= _clock.GetUtcNow().ToUnixTimeMilliseconds())

            return;

        var notice = new Notification(owner, title, message, key, expiresAtUnixMs, action);

        if (key is not null)
        {
            bool Matches(Notification item) => ReferenceEquals(item.Owner, owner) && item.Key == key;

            if (_visible.Value.Any(Matches))
            {
                notice.ShownAt = _clock.GetTimestamp();
                _visible.Value = _visible.Value.Select(item => Matches(item) ? notice : item).ToArray();
                Alert();

                return;
            }

            var queuedIndex = _pending.FindIndex(Matches);

            if (queuedIndex >= 0)
            {
                _pending[queuedIndex] = notice;

                return;
            }
        }

        _pending.Add(notice);
        FillVisible();
    }

    private void FillVisible()
    {
        if (_pending.Count == 0 || _visible.Value.Count >= MaxVisible)

            return;

        var now = _clock.GetUtcNow().ToUnixTimeMilliseconds();
        var visible = _visible.Value.ToList();
        var consumed = 0;
        var shown = 0;

        while (consumed < _pending.Count && visible.Count < MaxVisible)
        {
            var notice = _pending[consumed++];

            if (notice.ExpiresAtUnixMs <= now)

                continue;

            notice.ShownAt = _clock.GetTimestamp();
            visible.Insert(0, notice);
            shown++;
        }

        _pending.RemoveRange(0, consumed);

        if (shown == 0)

            return;
        _visible.Value = visible;
        Alert();
    }

    private void Alert()
    {
        requestAttention?.Invoke();

        if (!_soundChecked)
        {
            _soundChecked = true;

            try
            {
                if (TheVFS.VFS.FileExists(SoundPath))
                    _sound = IBackend.Backend.GetSound(SoundPath);
            }
            catch (Exception error)
            {
                Logging.Warning($"Could not load the notification sound: {error.Message}");
            }
        }

        _sound?.Play();
    }

    public void Clear(object owner)
    {
        _pending.RemoveAll(notice => ReferenceEquals(notice.Owner, owner));
        RemoveWhere(notice => ReferenceEquals(notice.Owner, owner));
        FillVisible();
    }

    public void Update()
    {
        if (_visible.Value.Count == 0)

            return;
        var now = _clock.GetUtcNow().ToUnixTimeMilliseconds();
        var tick = _clock.GetTimestamp();

        foreach (var notice in _visible.Value)
        {
            var elapsed = _clock.GetElapsedTime(notice.ShownAt, tick);
            if (notice.Flash.Value > 0f)
                notice.Flash.Value = Math.Max(0f, 1f - (float)elapsed.TotalMilliseconds / FlashDurationMs);

            if (notice.ExpiresAtUnixMs is { } expiry ? expiry <= now : elapsed >= DisplayDuration)
                notice.FadeStartedAt ??= tick;
            if (notice.FadeStartedAt is { } fade)
                notice.Opacity.Value = Math.Max(
                    0f,
                    1f - (float)_clock.GetElapsedTime(fade, tick).TotalMilliseconds / FadeDurationMs
                );
        }

        RemoveWhere(notice => notice.Opacity.Value == 0f);
        FillVisible();
    }

    private void RemoveWhere(Func<Notification, bool> predicate)
    {
        if (_visible.Value.Any(predicate))
            _visible.Value = [.. _visible.Value.Where(notice => !predicate(notice))];
    }

    public Node BuildUi() => Sx.View(
        style: () => Sx.Styles(
            position: Position.Absolute,
            top: 60,
            right: 16,
            width: 320,
            maxWidth: 90.Percent(),
            flexDirection: FlexDirection.Column,
            gapRow: 8,
            zIndex: 11000
        ),
        children: Sx.For(() => _visible.Value, (notice, _) => Card(notice))
    );

    private Node Card(Notification notice)
        => Sx.View(
            name: () => $"Notification {notice.Title}",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 8,
                paddingTop: 12,
                paddingBottom: 12,
                paddingLeft: 14,
                paddingRight: 14,
                opacity: notice.Opacity.Value,
                pointerEvents: notice.Opacity.Value >= 1f,
                backgroundColor: Color.Lerp(CardFill, Color.White, notice.Flash.Value),
                borderRadius: 8,
                borderTop: 1,
                borderRight: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderColor: Color.Lerp(Theme.MenuBorder, Color.White, notice.Flash.Value)
            ),
            onmousedown: e => e.StopPropagation(),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 14,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: notice.Flash.Value > 0.45f ? CardFill : Theme.MenuBlue
                    ),
                    text: () => notice.Title
                ),
                Sx.Text(
                    style: () => Sx.WrappingText(
                        fontSize: 12,
                        foregroundColor: notice.Flash.Value > 0.45f ? CardFill : Theme.MenuTextBlue
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => notice.Message
                ),
                Sx.Show(
                    () => notice.Action?.IsAvailable() == false,
                    () => Sx.Text(
                        style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                        text: () => notice.Action?.UnavailableMessage
                    )
                ),
                Sx.View(
                    style: () => Sx.Styles(justifyContent: Justify.FlexEnd, gapColumn: 6),
                    children:
                    [
                        Sx.Show(
                            () => notice.Action?.IsAvailable() == true,
                            () => Button.Create(
                                () => notice.Action!.Label,
                                () => Complete(notice),
                                variant: ButtonVariant.Primary,
                                size: ButtonSize.Sm
                            )
                        ),
                        Button.Create(
                            () => "DISMISS",
                            () => StartFade(notice),
                            size: ButtonSize.Sm,
                            name: () => "Dismiss notification"
                        )
                    ]
                )
            ]
        );

    private void Complete(Notification notice)
    {
        if (notice.FadeStartedAt.HasValue
            || !_visible.Value.Contains(notice)
            || notice.Action is not { } action
            || !action.IsAvailable()
            || notice.ExpiresAtUnixMs <= _clock.GetUtcNow().ToUnixTimeMilliseconds())

            return;
        StartFade(notice);
        action.Invoke();
    }

    private void StartFade(Notification notice) => notice.FadeStartedAt ??= _clock.GetTimestamp();

    public void Dispose()
    {
        _pending.Clear();
        _visible.Value = [];
        _sound?.Dispose();
    }
}
