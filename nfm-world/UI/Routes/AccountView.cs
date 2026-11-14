using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;
using Maxine.Extensions.Mathematics;

namespace NFMWorld.UI.Routes;

/// <summary>
/// Sign in / create account, shown over the main menu.
///
/// The fields and status are signals local to the view, so typing re-renders nothing but the
/// field it changes; the async calls run through the actions the phase supplies.
/// </summary>
public static class AccountView
{
    public sealed record Actions(
        Action<string, string> SignIn,
        Action Register,
        Action SignOut,
        Action Close
    );

    private static readonly Signal<bool> Open = new(false);
    private static readonly Signal<string> Username = new("");
    private static readonly Signal<string> Password = new("");
    private static readonly Signal<string> Status = new("");
    private static readonly Signal<bool> Busy = new(false);

    private static readonly Color MenuBlue = new(128, 170, 255);
    private static readonly Color ErrorRed = new(255, 140, 140);

    public static bool IsOpen => Open.Value;

    public static void Show()
    {
        Status.Value = "";
        Password.Value = "";
        Open.Value = true;
    }

    public static void Hide()
    {
        Open.Value = false;
        Password.Value = "";
        Status.Value = "";
    }

    /// <summary>Called by the phase when a request finishes.</summary>
    public static void SetStatus(string message, bool isError)
    {
        Status.Value = message;
        Busy.Value = false;
    }

    public static void SetBusy()
    {
        Busy.Value = true;
        Status.Value = "";
    }

    public static void Reset()
    {
        Open.Value = false;
        Password.Value = "";
        Status.Value = "";
        Busy.Value = false;
    }

    public static Node Build(Actions actions, Func<bool> signedIn, Func<string> username)
    {
        return Sx.Show(
            when: () => Open.Value,
            then: () => Card(actions, signedIn, username)
        );
    }

    private static Node Card(Actions actions, Func<bool> signedIn, Func<string> username)
    {
        var navigation = new KeyboardNavigation();
        KeyboardScope? root = null;

        bool HandleKey(Key key) => root is not null && navigation.HandleKey(root, key);

        root = new KeyboardScope(navigation, HandleKey)
        {
            Name = "Account dialog",
            Styles = Sx.Styles(
                position: Position.Absolute,
                left: "50%",
                top: "50%",
                width: 420,
                marginLeft: -210,
                marginTop: -150,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Stretch,
                paddingTop: 18,
                paddingBottom: 18,
                paddingLeft: 22,
                paddingRight: 22,
                gapRow: 10,
                backgroundColor: new Color(14, 28, 56, 235),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: new Color(128, 170, 255, 195),
                borderRadius: 10
            )
        };

        root.AddChild(
            Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 20,
                    fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: MenuBlue
                ),
                text: () => signedIn() ? "ACCOUNT" : "SIGN IN"
            )
        );

        // Signed in: show who it is and offer sign out.
        root.AddChild(
            Sx.Show(
                () => signedIn(),
                () => Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 14, foregroundColor: MenuBlue),
                            text: () => username()
                        ),
                        KeyboardNavigation.Row(
                            Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    ButtonRow("SIGN OUT", actions.SignOut, primary: false, name: "Account sign out"),
                                    ButtonRow("CLOSE", actions.Close, primary: true, name: "Account close")
                                ]
                            )
                        )
                    ]
                )
            )
        );

        root.AddChild(
            Sx.Show(
                () => !signedIn(),
                () => Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                    children:
                    [
                        Field("USERNAME", Username, password: false, () => Submit(actions)),
                        Field("PASSWORD", Password, password: true, () => Submit(actions)),
                        // Word-wrap: an archive error is a full sentence, and without it the
                        // line ran out of the card's width.
                        Sx.Text(
                            style: () => Sx.WrappingText(
                                fontSize: 12,
                                foregroundColor: Status.Value.StartsWith("!") ? ErrorRed : MenuBlue
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => Status.Value.TrimStart('!')
                        ),
                        KeyboardNavigation.Row(
                            Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    Sx.View(
                                        style: () => Sx.Styles(flexGrow: 1),
                                        children:
                                        [
                                            ButtonRow(
                                                () => Busy.Value ? "..." : "SIGN IN",
                                                () => Submit(actions),
                                                primary: true,
                                                name: "Account sign in"
                                            )
                                        ]
                                    ),
                                    Sx.View(
                                        style: () => Sx.Styles(flexGrow: 1),
                                        children:
                                        [
                                            ButtonRow(
                                                "REGISTER IN BROWSER",
                                                actions.Register,
                                                name: "Account register"
                                            )
                                        ]
                                    ),
                                    Sx.View(
                                        style: () => Sx.Styles(flexGrow: 1),
                                        children: [ButtonRow("CANCEL", actions.Close, name: "Account cancel")]
                                    )
                                ]
                            )
                        )
                    ]
                )
            )
        );

        return root;
    }

    private static void Submit(Actions actions)
    {
        if (Busy.Value)
            return;

        if (string.IsNullOrWhiteSpace(Username.Value) || string.IsNullOrWhiteSpace(Password.Value))
        {
            SetStatus("!Enter a username and password.", isError: true);
            Busy.Value = false;
            return;
        }

        actions.SignIn(Username.Value, Password.Value);
    }

    /// <summary>Room for one line of the field's own font plus its padding.</summary>
    private const float FieldHeight = 34;

    private static Node Field(string label, Signal<string> value, bool password, Action submit)
    {
        var focused = new Signal<bool>(false);

        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                // The inner text collapses to ~0 high when the field is empty, so the
                // input's own size has to come from minHeight, not its content.
                minHeight: FieldHeight,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                backgroundColor: new Color(255, 255, 255, 16),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: focused.Value ? new Color(205, 227, 255) : new Color(128, 170, 255, 90),
                borderRadius: 6
            ),
            text: () => value.Value,
            onchange: v => value.Value = v,
            maskCharacter: password ? '*' : null,
            // Enter submits, so the form behaves like a form.
            onsubmit: _ => submit()
        );
        // Arrows belong to the caret; the row's up/down still reaches the other controls.
        KeyboardNavigation.Bind(input, _ => false, isFocused => focused.Value = isFocused);

        return KeyboardNavigation.Row(
            Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: MenuBlue), text: () => label),
                    input
                ]
            )
        );
    }

    private static Node ButtonRow(Func<string> label, Action onPress, bool primary = false, string? name = null)
    {
        var hovered = new Signal<bool>(false);
        var focused = new Signal<bool>(false);
        var lit = () => hovered.Value || focused.Value;

        var button = Sx.View(
            name: () => name,
            style: () => Sx.Styles(
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                alignItems: Align.Center,
                backgroundColor: primary
                    ? lit() ? new Color(70, 115, 190, 230) : new Color(50, 90, 160, 220)
                    : lit() ? new Color(55, 90, 150, 190) : new Color(22, 42, 80, 160),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: lit() ? new Color(205, 227, 255) : new Color(128, 170, 255, 170),
                borderRadius: 6
            ),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: e =>
            {
                e.StopPropagation();
                onPress();
            },
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 13,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: lit() ? new Color(205, 227, 255) : MenuBlue
                    ),
                    text: label
                )
            ]
        );

        return KeyboardNavigation.Bind(
            button,
            key =>
            {
                if (key is not (Key.Enter or Key.Space)) return false;
                onPress();
                return true;
            },
            isFocused => focused.Value = isFocused
        );
    }

    private static Node ButtonRow(string label, Action onPress, bool primary = false, string? name = null)
        => ButtonRow(() => label, onPress, primary, name);

    /// <summary>The dialog's keyboard owner: focus moves inside the card, not the menu behind it.</summary>
    private sealed class KeyboardScope(KeyboardNavigation navigation, Func<Key, bool> handleKey) : View
    {
        public bool HandleKey(Key key) => handleKey(key);

        protected override bool OnPostLayout()
        {
            var changed = base.OnPostLayout();
            navigation.UpdateFocus(this);
            return changed;
        }
    }

    /// <summary>Focus movement and button activation for the open dialog; false lets a key travel on.</summary>
    public static bool HandleKeyPressed(Node? node, Key key)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is KeyboardScope scope) return scope.HandleKey(key);
        foreach (var child in node.VisualChildren)
            if (HandleKeyPressed(child, key))
                return true;
        return false;
    }

    public static bool HandleKeyRepeated(Node? node, Key key)
    {
        if (node is null || node is Component { IsDisplayed: false }) return false;
        if (node is KeyboardScope scope) return scope.HandleKey(key);
        foreach (var child in node.VisualChildren)
            if (HandleKeyRepeated(child, key))
                return true;
        return false;
    }
}
