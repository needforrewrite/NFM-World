using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal sealed class NotificationCenter : IDisposable
{
    private const string SoundPath = "data/sound/ui/notif.wav";
    private readonly Signal<IReadOnlyList<Notification>> _visible = new([]);
    private ISoundClip? _sound;
    private long _nextId;
    private bool _soundChecked;

    public sealed record NotificationAction(string Label, Action Invoke, Func<bool> IsAvailable, string UnavailableMessage);

    private sealed record Notification(
        long Id,
        object Owner,
        string Title,
        string Message,
        string? Key,
        long ExpiresAtUnixMs,
        NotificationAction? Action);

    public void Show(
        object owner,
        string title,
        string message,
        string? key = null,
        long? expiresAtUnixMs = null,
        NotificationAction? action = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = expiresAtUnixMs ?? now.AddSeconds(8).ToUnixTimeMilliseconds();
        if (expiry <= now.ToUnixTimeMilliseconds()) return;

        var notice = new Notification(++_nextId, owner, title, message, key, expiry, action);
        var previous = _visible.Value.Where(item => key is null || item.Owner != owner || item.Key != key);
        _visible.Value = [notice, .. previous.Take(2)];

        if (!_soundChecked)
        {
            _soundChecked = true;
            try
            {
                if (VFS.FileExists(SoundPath)) _sound = IBackend.Backend.GetSound(SoundPath);
            }
            catch (Exception error)
            {
                Logging.Warning($"Could not load the notification sound: {error.Message}");
            }
        }

        _sound?.Play();
    }

    public void Clear(object owner)
        => RemoveWhere(notice => ReferenceEquals(notice.Owner, owner));

    public void Update()
    {
        if (_visible.Value.Count == 0) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        RemoveWhere(notice => notice.ExpiresAtUnixMs <= now);
    }

    private void RemoveWhere(Func<Notification, bool> predicate)
    {
        if (_visible.Value.Any(predicate))
            _visible.Value = [.. _visible.Value.Where(notice => !predicate(notice))];
    }

    public Node BuildUi() => Sx.View(
        style: () => Sx.Styles(
            position: Position.Absolute,
            top: 16,
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
                backgroundColor: new Color(12, 22, 42),
                borderRadius: 8,
                borderTop: 1,
                borderRight: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderColor: Theme.MenuBorder
            ),
            onmousedown: e => e.StopPropagation(),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(
                    fontSize: 14,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: Theme.MenuBlue
                ), text: () => notice.Title
                ),
                Sx.Text(
                    style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.MenuTextBlue),
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
                            () => RemoveWhere(item => ReferenceEquals(item, notice)),
                            size: ButtonSize.Sm,
                            name: () => "Dismiss notification"
                        )
                    ]
                )
            ]
        );

    private void Complete(Notification notice)
    {
        if (notice.Action is not { } action || !action.IsAvailable()
            || notice.ExpiresAtUnixMs <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return;
        RemoveWhere(item => ReferenceEquals(item, notice));
        action.Invoke();
    }

    public void Dispose()
    {
        _visible.Value = [];
        _sound?.Dispose();
    }
}
