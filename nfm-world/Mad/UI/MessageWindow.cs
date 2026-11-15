using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.UI;

public class MessageWindow
{
    public enum ButtonType
    {
        OK,
        YesNo,
        OKCancel,
        Custom
    }

    public enum MessageResult
    {
        None,
        OK,
        Yes,
        No,
        Cancel,
        Custom1,
        Custom2,
        Custom3
    }

    private sealed record Message(
        string Title,
        string Text,
        string[] Labels,
        MessageResult[] Results,
        Action<MessageResult>? Callback);

    private readonly Signal<Message?> _message = new(null);
    private readonly Signal<int> _selected = new(0);
    public bool IsOpen => _message.Value is not null;
    public View? ActiveView { get; private set; }

    public void ShowMessage(string title, string message, Action<MessageResult>? callback = null)
        => Show(title, message, ["OK"], [MessageResult.OK], callback);

    public void ShowYesNo(string title, string message, Action<MessageResult>? callback = null)
        => Show(title, message, ["Yes", "No"], [MessageResult.Yes, MessageResult.No], callback, 1);

    public void ShowOKCancel(string title, string message, Action<MessageResult>? callback = null)
        => Show(title, message, ["OK", "Cancel"], [MessageResult.OK, MessageResult.Cancel], callback, 1);

    public void ShowCustom(string title, string message, string[] buttonLabels, Action<MessageResult>? callback = null)
        => Show(title, message, buttonLabels.ToArray(), buttonLabels.Select((_, index) => index switch
        {
            0 => MessageResult.Custom1, 1 => MessageResult.Custom2, 2 => MessageResult.Custom3,
            _ => MessageResult.None
        }).ToArray(), callback);

    private void Show(
        string title,
        string text,
        string[] labels,
        MessageResult[] results,
        Action<MessageResult>? callback,
        int selected = 0
    )
    {
        FocusManager.ClearFocus();
        _selected.Value = selected;
        _message.Value = new Message(title, text, labels, results, callback);
    }

    public void Close()
    {
        _message.Value = null;
        ActiveView = null;
        FocusManager.ClearFocus();
    }

    private void Complete(MessageResult result)
    {
        var callback = _message.Value?.Callback;
        Close();
        callback?.Invoke(result);
    }

    public bool HandleKey(Key key)
    {
        if (_message.Value is not { } message) return false;
        if (key == Key.Escape) Complete(message.Results.Length == 1 ? message.Results[0] : MessageResult.Cancel);
        else if (key is (Key.Left or Key.Right or Key.Up or Key.Down or Key.Tab) && message.Labels.Length > 0)
            _selected.Value = (_selected.Value + (key is Key.Left or Key.Up ? -1 : 1)
                               + message.Labels.Length) % message.Labels.Length;
        else if (key is (Key.Enter or Key.Space) && message.Results.Length > 0)
            Complete(message.Results[Math.Clamp(_selected.Value, 0, message.Results.Length - 1)]);
        return true;
    }

    public Node BuildUi() => Sx.For(
        each: () => _message.Value is { } message ? new[] { message } : [],
        row: (message, _) => ActiveView = MessageDialog.Build(
            message.Title,
            message.Text,
            message.Labels.Select((label, index) => new MessageDialog.ActionButton(
                label,
                () => Complete(message.Results[index])
            )
            ).ToArray(),
            _selected
        )
    );
}
