using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// MY CONTENT: the signed-in account's own uploads, reached from WORKSHOP.
///
/// It is a full-screen overlay over the scene, like the replay browser, and it edits the same
/// archive rows the website's edit page does. The rows are read from <see cref="UiState.MyContent"/>,
/// which the hosting controller owns and writes.
/// </summary>
public static class MyContentView
{
    /// <summary>What the page asks the game to do. The hosting phase fills these in.</summary>
    public sealed record Actions(
        Action Back,
        Action Refresh,
        Action<OwnedAsset> Edit,
        Action<OwnedAsset> ToggleVisibility,
        Action<OwnedAsset> Delete,
        Action Save,
        Action CancelEdit
    );

    private static readonly Color MenuBlue = Theme.MenuBlue;
    private static readonly Color TextBlue = Theme.MenuTextBlue;
    private static readonly Color MenuDim = Theme.MenuDim;

    internal sealed class InputView(Func<bool> canNavigate) : View
    {
        private readonly KeyboardNavigation _navigation = new();
        public bool HandleKey(Key key) => _navigation.HandleKey(this, key);

        protected override bool OnPostLayout()
        {
            if (canNavigate())
                _navigation.UpdateFocus(this);
            return base.OnPostLayout();
        }
    }

    public static Node Build(Actions actions, Func<bool>? canNavigate = null)
    {
        var content = Sx.View(
            name: () => "My content",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                paddingTop: 20,
                paddingBottom: 20,
                alignItems: Align.Center,
                justifyContent: Justify.Center
            ),
            children:
            [
                Panel(actions),
                Sx.Show(
                    when: () => UiState.MyContent.Editing.Value is not null,
                    then: () => Editor(actions)
                )
            ]
        );

        return new InputView(canNavigate ?? (() => true))
        {
            Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent()),
            Children = { content }
        };
    }

    private static Node Panel(Actions actions)
        => Sx.View(
            name: () => "My content panel",
            style: () => Sx.Styles(
                width: 860,
                maxWidth: 96.Percent(),
                height: 620,
                maxHeight: 100.Percent(),
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                gapRow: 10,
                paddingTop: 20,
                paddingBottom: 20,
                paddingLeft: 20,
                paddingRight: 20,
                borderRadius: 10,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                backgroundColor: Theme.MenuPanel
            ),
            children:
            [
                Header(actions),
                Status(),
                List(),
                Selection(actions),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "↑ ↓ navigate · Enter edit · H hide/show · Delete remove · Esc back"
                )
            ]
        );

    private static Node Header(Actions actions)
        => Sx.View(
            name: () => "My content header",
            style: () => Sx.Styles(alignItems: Align.Center, justifyContent: Justify.SpaceBetween, flexShrink: 0),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 32,
                        fontStyle: FontStyle.Bold,
                        fontFamily: FontFamily.Adventure,
                        foregroundColor: MenuBlue
                    ),
                    text: () => "MY CONTENT"
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8, flexShrink: 0),
                    children:
                    [
                        Button.Create(
                            label: () => "REFRESH",
                            onPress: actions.Refresh,
                            variant: ButtonVariant.Ghost,
                            size: ButtonSize.Sm,
                            name: () => "My content refresh"
                        ),
                        Button.Create(
                            label: () => "BACK",
                            onPress: actions.Back,
                            variant: ButtonVariant.Ghost,
                            size: ButtonSize.Sm,
                            name: () => "My content back"
                        )
                    ]
                )
            ]
        );

    private static Node Status()
        => Sx.Show(
            when: () => UiState.MyContent.Status.Value.Length > 0,
            then: () =>
            {
                var status = Sx.Text(
                    layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                    style: () => Sx.WrappingText(
                        fontSize: 11,
                        foregroundColor: UiState.MyContent.Busy.Value ? Theme.Accent : Theme.Warning
                    ),
                    text: () => UiState.MyContent.Status.Value
                );
                status.Name = "My content status";
                return status;
            }
        );

    private static Node List()
        => Sx.View(
            name: () => "My content list",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                paddingRight: 8,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll
            ),
            children:
            [
                Sx.View(
                    name: () => "My content list content",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 8
                    ),
                    children:
                    [
                        Sx.For(each: () => UiState.MyContent.Items.Value, row: (item, index) => Row(item, index)),
                        Sx.Show(
                            when: () => UiState.MyContent.Items.Value.Count == 0,
                            then: () => Sx.Text(
                                layout: () => Sx.Styles(width: 100.Percent(), paddingTop: 20, paddingLeft: 4),
                                style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.TextFaint),
                                text: () => UiState.MyContent.Busy.Value
                                    ? "Loading your uploads..."
                                    : "You have not published anything yet. Use the stage or model editor to publish."
                            )
                        )
                    ]
                )
            ]
        );

    private static View Row(OwnedAsset item, int index)
    {
        var hovered = new Signal<bool>(false);
        var selected = () => UiState.MyContent.Cursor.Value == index;

        return Sx.View(
            name: () => "My content row " + item.Id,
            style: () => Sx.Styles(
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 12,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                borderRadius: 6,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: selected() ? new Color(205, 227, 255) : Theme.MenuBorder,
                backgroundColor: selected() ? Theme.MenuHighlight
                    : hovered.Value ? Theme.SurfaceHover : Theme.MenuFill
            ),
            onmouseenter: _ =>
            {
                hovered.Value = true;
                UiState.MyContent.Cursor.Value = index;
            },
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexShrink: 1, minWidth: 0, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(
                                fontSize: 16,
                                fontStyle: FontStyle.Bold,
                                fontFamily: FontFamily.Adventure,
                                foregroundColor: MenuBlue
                            ),
                            text: () => item.Name
                        ),
                        Sx.Text(
                            layout: () => Sx.Styles(width: 100.Percent()),
                            style: () => Sx.WrappingText(
                                fontSize: 11,
                                foregroundColor: item.IsTakedown ? Theme.Danger : MenuDim
                            ),
                            text: () => $"{item.Type}  ·  {StateLabel(item)}  ·  {item.DownloadCount} download(s)"
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
                )
            ]
        );
    }

    private static string StateLabel(OwnedAsset item)
        => item.IsTakedown ? "Removed by moderation"
            : item.ReviewStatus is "Pending" or "Rejected" ? item.ReviewLabel!
            : !item.IsPublic ? "Hidden"
            : item.ReviewLabel ?? "Listed";

    private static Node Selection(Actions actions)
        => Sx.Show(
            when: () => UiState.MyContent.Selected is not null,
            then: () => Sx.View(
                name: () => "My content selection",
                style: () => Sx.Styles(
                    width: 100.Percent(),
                    flexShrink: 0,
                    flexDirection: FlexDirection.Column,
                    gapRow: 8,
                    paddingTop: 10,
                    paddingBottom: 10,
                    paddingLeft: 12,
                    paddingRight: 12,
                    backgroundColor: Theme.Surface,
                    borderRadius: 6
                ),
                children:
                [
                    Sx.Text(
                        layout: () => Sx.Styles(width: 100.Percent()),
                        style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.TextSecondary),
                        text: () => UiState.MyContent.Selected is { } item
                            ? string.IsNullOrWhiteSpace(item.Description)
                                ? $"{item.Type} · {FormatSize(item.SizeBytes)} · {StateLabel(item)}"
                                : $"{item.Type} · {FormatSize(item.SizeBytes)} · {StateLabel(item)}\n{item.Description}"
                            : ""
                    ),
                    KeyboardNavigation.Row(Sx.View(
                        name: () => "My content buttons",
                        style: () => Sx.Styles(
                            flexDirection: FlexDirection.Row,
                            flexWrap: Wrap.Wrap,
                            gapColumn: 8,
                            gapRow: 8
                        ),
                        children:
                        [
                            ActionButton(
                                () => "EDIT",
                                () =>
                                {
                                    if (UiState.MyContent.Selected is { } item)
                                        actions.Edit(item);
                                },
                                "My content edit"
                            ),
                            Sx.Show(
                                when: () => UiState.MyContent.Selected is { IsTakedown: false },
                                then: () => ActionButton(
                                    () => UiState.MyContent.Selected is { IsPublic: true } ? "HIDE" : "RE-LIST",
                                    () =>
                                    {
                                        if (UiState.MyContent.Selected is { } item)
                                            actions.ToggleVisibility(item);
                                    },
                                    "My content visibility"
                                )
                            ),
                            ActionButton(
                                () => "DELETE",
                                () =>
                                {
                                    if (UiState.MyContent.Selected is { } item)
                                        actions.Delete(item);
                                },
                                "My content delete",
                                ButtonVariant.Danger
                            )
                        ]
                    ))
                ]
            )
        );

    private static View ActionButton(
        Func<string> label,
        Action onPress,
        string name,
        ButtonVariant variant = ButtonVariant.Default
    )
    {
        var focused = new Signal<bool>(false);
        var hovered = new Signal<bool>(false);
        var node = Button.Create(
            label: label,
            onPress: onPress,
            variant: variant,
            size: ButtonSize.Sm,
            disabled: UiState.MyContent.Busy.Value,
            name: () => name,
            style: () => Sx.Styles(
                backgroundColor: focused.Value || hovered.Value ? Theme.MenuHighlight : Theme.MenuFill,
                borderColor: focused.Value || hovered.Value ? new Color(205, 227, 255) : Theme.MenuBorder,
                opacity: UiState.MyContent.Busy.Value ? 0.5f : 1
            ),
            textStyle: () => Sx.TextStyles(
                fontSize: 13,
                fontFamily: FontFamily.NotoSans,
                fontStyle: FontStyle.Plain,
                foregroundColor: focused.Value || hovered.Value ? new Color(235, 244, 255) : MenuBlue
            )
        );
        node.MouseEntered += _ => hovered.Value = true;
        node.MouseLeft += _ => hovered.Value = false;
        return KeyboardNavigation.Bind(
            node,
            key =>
            {
                if (key is not (Key.Enter or Key.Space)) return false;
                onPress();
                return true;
            },
            value => focused.Value = value
        );
    }

    private static string FormatSize(long bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
        };

    /// <summary>
    /// Renaming, re-describing and re-listing one upload. A modal over the panel rather than a
    /// separate page, so the list stays visible behind it.
    /// </summary>
    private static Node Editor(Actions actions)
    {
        var navigation = new KeyboardNavigation();
        EditorScope? root = null;
        bool HandleKey(Key key) => root is not null && navigation.HandleKey(root, key);

        root = new EditorScope(navigation, HandleKey)
        {
            Name = "My content editor",
            Styles = Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 0,
                bottom: 0,
                zIndex: 40,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 190)
            )
        };
        root.AddChild(
            Sx.View(
                style: () => Sx.Styles(
                    width: 90.Percent(),
                    maxWidth: 520,
                    maxHeight: 90.Percent(),
                    minHeight: 0,
                    flexDirection: FlexDirection.Column,
                    gapRow: 12,
                    paddingTop: 22,
                    paddingBottom: 22,
                    paddingLeft: 22,
                    paddingRight: 22,
                    overflow: Overflow.Scroll,
                    backgroundColor: new Color(12, 22, 42, 250),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: MenuBlue,
                    borderRadius: 10
                ),
                onmousedown: e => e.StopPropagation(),
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 24,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: MenuBlue
                        ),
                        text: () => "EDIT UPLOAD"
                    ),
                    Field("NAME", UiState.MyContent.EditName, multiline: false),
                    Field("DESCRIPTION", UiState.MyContent.EditDescription, multiline: true),
                    KeyboardNavigation.Row(
                        Sx.View(
                            name: () => "My content editor visibility",
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                alignItems: Align.Center,
                                gapColumn: 10,
                                paddingTop: 4,
                                flexShrink: 0
                            ),
                            children:
                            [
                                PublicCheckbox(),
                                Sx.Text(
                                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: TextBlue),
                                    layout: () => Sx.Styles(flexShrink: 1),
                                    text: () => "Listed publicly"
                                )
                            ]
                        )
                    ),
                    Sx.Text(
                        layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                        style: () => Sx.WrappingText(fontSize: 11, foregroundColor: MenuDim),
                        text: () => "Replacing the file is done from the editor that published it."
                    ),
                    KeyboardNavigation.Row(
                        Sx.View(
                            style: () => Sx.Styles(
                                flexDirection: FlexDirection.Row,
                                justifyContent: Justify.FlexEnd,
                                gapColumn: 10,
                                flexShrink: 0
                            ),
                            children:
                            [
                                ActionButton(
                                    () => "CANCEL",
                                    actions.CancelEdit,
                                    "My content edit cancel"
                                ),
                                ActionButton(
                                    () => "SAVE",
                                    actions.Save,
                                    "My content edit save"
                                )
                            ]
                        )
                    )
                ]
            )
        );

        return root;
    }

    private static Node Field(string label, Signal<string> value, bool multiline)
    {
        var focused = new Signal<bool>(false);
        var fieldStyle = Sx.Styles(
            width: 100.Percent(),
            minHeight: 34,
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
        );

        Node input = multiline
            ? Sx.TextArea(
                style: () => fieldStyle.Over(Sx.Styles(minHeight: 76)),
                text: () => value.Value,
                onchange: text => value.Value = text,
                textStyle: () => Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue)
            )
            : Sx.TextInput(
                style: () => fieldStyle,
                text: () => value.Value,
                onchange: text => value.Value = text
            );

        KeyboardNavigation.Bind(
            (Component)input,
            _ => false,
            isFocused => focused.Value = isFocused
        );

        return KeyboardNavigation.Row(
            Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4, flexShrink: 0),
                children:
                [
                    Sx.Text(style: () => Sx.TextStyles(fontSize: 10, foregroundColor: MenuDim), text: () => label),
                    input
                ]
            )
        );
    }

    /// <summary>The editor's "listed publicly" toggle: a box, because there is no checkbox control.</summary>
    private static Node PublicCheckbox()
    {
        var hovered = new Signal<bool>(false);
        var node = Sx.View(
            name: () => "My content editor public",
            style: () => Sx.Styles(
                width: 18,
                height: 18,
                flexShrink: 0,
                borderRadius: 4,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: hovered.Value ? new Color(205, 227, 255) : Theme.MenuBorder,
                backgroundColor: UiState.MyContent.EditPublic.Value
                    ? Theme.MenuHighlight
                    : new Color(255, 255, 255, 14)
            ),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: e =>
            {
                e.StopPropagation();
                UiState.MyContent.EditPublic.Value = !UiState.MyContent.EditPublic.Value;
            },
            children:
            [
                Sx.Show(
                    when: () => UiState.MyContent.EditPublic.Value,
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(235, 244, 255)),
                        text: () => "✓"
                    )
                )
            ]
        );

        return KeyboardNavigation.Bind(
            node,
            key =>
            {
                if (key is not (Key.Enter or Key.Space)) return false;
                UiState.MyContent.EditPublic.Value = !UiState.MyContent.EditPublic.Value;
                return true;
            },
            _ => { }
        );
    }

    /// <summary>The editor's keyboard owner: focus moves inside the dialog, not the list behind it.</summary>
    private sealed class EditorScope(KeyboardNavigation navigation, Func<Key, bool> handleKey) : View
    {
        public bool HandleKey(Key key) => handleKey(key);

        protected override bool OnPostLayout()
        {
            var changed = base.OnPostLayout();
            navigation.UpdateFocus(this);
            return changed;
        }
    }
}
