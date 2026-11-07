using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

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
        Action<string, string> Register,
        Action SignOut,
        Action Close);

    private static readonly Signal<bool> Open = new(false);
    private static readonly Signal<int> Tab = new(0);
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
        Tab.Value = 0;
        Password.Value = "";
        Status.Value = "";
        Busy.Value = false;
    }

    public static Node Build(Actions actions, Func<bool> signedIn, Func<string> username)
    {
        return Sx.Show(
            when: () => Open.Value,
            then: () => Card(actions, signedIn, username));
    }

    private static Node Card(Actions actions, Func<bool> signedIn, Func<string> username)
    {
        return Sx.View(
            style: () => Sx.Styles(
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
                borderRadius: 10),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 20, fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold, foregroundColor: MenuBlue),
                    text: () => signedIn() ? "ACCOUNT" : Tab.Value == 0 ? "SIGN IN" : "CREATE ACCOUNT"),

                // Signed in: show who it is and offer sign out.
                Sx.Show(() => signedIn(),
                    () => Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                        children:
                        [
                            Sx.Text(style: () => Sx.TextStyles(fontSize: 14, foregroundColor: MenuBlue),
                                text: () => username()),
                            ButtonRow("SIGN OUT", actions.SignOut, primary: false),
                            ButtonRow("CLOSE", actions.Close, primary: true)
                        ])),

                Sx.Show(() => !signedIn(),
                    () => Sx.View(
                        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 10),
                        children:
                        [
                            Field("USERNAME", Username, password: false),
                            Field("PASSWORD", Password, password: true),
                            // Word-wrap: an archive error is a full sentence, and without it the
                            // line ran out of the card's width.
                            Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 12, breakType: BreakType.Word,
                                    foregroundColor: Status.Value.StartsWith("!") ? ErrorRed : MenuBlue),
                                layout: () => Sx.Styles(width: MeasurementWidthHeight.Percent(100)),
                                text: () => Status.Value.TrimStart('!')),
                            Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 10),
                                children:
                                [
                                    Sx.View(style: () => Sx.Styles(flexGrow: 1),
                                        children: [ButtonRow(
                                            () => Busy.Value ? "..." : Tab.Value == 0 ? "SIGN IN" : "CREATE",
                                            () => Submit(actions), primary: true)]),
                                    Sx.View(style: () => Sx.Styles(flexGrow: 1),
                                        children: [ButtonRow(
                                            () => Tab.Value == 0 ? "CREATE ACCOUNT" : "BACK TO SIGN IN",
                                            () => Tab.Value = Tab.Value == 0 ? 1 : 0)]),
                                    Sx.View(style: () => Sx.Styles(flexGrow: 1),
                                        children: [ButtonRow("CANCEL", actions.Close)])
                                ])
                        ]))
            ]);
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

        if (Tab.Value == 0)
            actions.SignIn(Username.Value, Password.Value);
        else
            actions.Register(Username.Value, Password.Value);
    }

    /// <summary>Room for one line of the field's own font plus its padding.</summary>
    private const float FieldHeight = 34;

    private static Node Field(string label, Signal<string> value, bool password)
    {
        return Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 11, foregroundColor: MenuBlue), text: () => label),
                Sx.TextInput(
                    style: () => Sx.Styles(
                        width: MeasurementWidthHeight.Percent(100),
                        // The inner text collapses to ~0 high when the field is empty, so the
                        // input's own size has to come from minHeight, not its content.
                        minHeight: FieldHeight,
                        paddingTop: 8, paddingBottom: 8, paddingLeft: 10, paddingRight: 10,
                        backgroundColor: new Color(255, 255, 255, 16),
                        borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                        borderColor: new Color(128, 170, 255, 90),
                        borderRadius: 6),
                    text: () => value.Value,
                    onchange: v => value.Value = v,
                    maskCharacter: password ? '*' : null,
                    // Enter submits, so the form behaves like a form.
                    onsubmit: _ => value.Value = value.Value)
            ]);
    }

    private static Node ButtonRow(Func<string> label, Action onPress, bool primary = false)
    {
        return Sx.View(
            style: () => Sx.Styles(
                paddingTop: 8, paddingBottom: 8, paddingLeft: 12, paddingRight: 12,
                alignItems: Align.Center,
                backgroundColor: primary ? new Color(50, 90, 160, 220) : new Color(22, 42, 80, 160),
                borderTop: 1, borderBottom: 1, borderLeft: 1, borderRight: 1,
                borderColor: new Color(128, 170, 255, 170),
                borderRadius: 6),
            onmousedown: _ => onPress(),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 13, fontStyle: FontStyle.Bold,
                    foregroundColor: MenuBlue), text: label)
            ]);
    }

    private static Node ButtonRow(string label, Action onPress, bool primary = false)
        => ButtonRow(() => label, onPress, primary);
}
