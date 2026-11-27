using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

public static class EditorPalette
{
    public static readonly Color Blue = new(128, 170, 255);
    public static readonly Color TextBlue = new(185, 211, 255);
    public static readonly Color Panel = new(12, 22, 42, 245);
    public static readonly Color PanelSoft = new(22, 42, 80, 220);
    public static readonly Color Border = new(128, 170, 255, 100);
    public static readonly Color Bar = new(8, 16, 32, 235);
}

public enum EditorTabAppearance
{
    Flat,
    Rounded
}

public static class EditorControls
{
    public static Func<Styles> CompactStyle(float minWidth = 0, MeasurementWidthHeight? minHeight = null) => () => Sx.Styles(
        flexShrink: 0,
        minWidth: minWidth,
        minHeight: minHeight,
        paddingTop: 2,
        paddingBottom: 2,
        paddingLeft: 8,
        paddingRight: 8,
        borderRadius: 6
    );

    public static View CompactButton(
        Func<string?> label,
        Action action,
        Func<bool>? active = null,
        Func<Styles>? style = null,
        Func<TextStyles>? textStyle = null,
        Func<string?>? name = null,
        ButtonVariant variant = ButtonVariant.Default
    ) => Button.Create(
        label,
        action,
        variant,
        active,
        size: ButtonSize.Sm,
        style: style ?? CompactStyle(),
        textStyle: textStyle ?? (() => Sx.TextStyles(fontSize: 11, fontStyle: FontStyle.Bold)),
        name: name
    );

    public static View ActionButton(string label, Action action) => CompactButton(
        () => label,
        action,
        name: () => "Editor action " + label,
        style: CompactStyle(minHeight: 26)
    );

    public static View StepButton(string label, Action action, float minWidth = 30) => Button.Create(
        () => label,
        action,
        size: ButtonSize.Sm,
        style: () => Sx.Styles(
            minWidth: minWidth,
            paddingLeft: 6,
            paddingRight: 6,
            paddingTop: 3,
            paddingBottom: 3,
            flexShrink: 0
        )
    );

    public static View ToolbarButton(
        string label,
        Action action,
        ButtonVariant variant = ButtonVariant.Default,
        string? name = null
    )
        => Button.Create(
            () => label,
            action,
            variant,
            size: ButtonSize.Sm,
            style: () => Sx.Styles(minWidth: 70, flexShrink: 0),
            name: name is null ? null : () => name
        );

    public static TextInput Field(
        Func<string> value,
        Action<string> onChange,
        Func<string?>? placeholder = null,
        float minWidth = 160,
        Action? onSubmit = null,
        Func<Styles>? style = null,
        Func<TextStyles>? textStyle = null
    )
    {
        var input = Sx.TextInput(
            style: () =>
            {
                var result = Sx.Styles(
                    minWidth: minWidth,
                    paddingTop: 3,
                    paddingBottom: 3,
                    paddingLeft: 8,
                    paddingRight: 8,
                    backgroundColor: new Color(22, 34, 54),
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: new Color(128, 170, 255, 65),
                    borderRadius: 6
                );
                return style is null ? result : result.Over(style());
            },
            text: value,
            onchange: onChange,
            placeholder: placeholder,
            onsubmit: onSubmit is null ? null : _ => onSubmit(),
            textStyle: textStyle ?? (() => Sx.TextStyles(fontSize: 11))
        );
        return input;
    }

    public static TextInput NumberField(Func<string> value, Action<string> onChange, float width = 50, float height = 20)
        => Field(
            value,
            onChange,
            minWidth: 0,
            style: () => Sx.Styles(
                width: width,
                height: height,
                minHeight: 0,
                flexShrink: 0,
                paddingTop: 1,
                paddingBottom: 1,
                paddingLeft: 4,
                paddingRight: 4,
                backgroundColor: Theme.Surface,
                borderColor: Theme.SurfaceBorder,
                borderRadius: 4
            ),
            textStyle: () => Sx.TextStyles(fontSize: 10)
        );

    public static Node FieldRow(
        Func<string?> label,
        float labelWidth,
        IReadOnlyList<Node?> fields,
        Func<Styles>? style = null,
        Func<TextStyles>? labelStyle = null,
        Func<string?>? name = null
    ) => Sx.View(
        name: name,
        style: () =>
        {
            var result = Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 6, flexShrink: 0);
            return style is null ? result : result.Over(style());
        },
        children:
        [
            Sx.Text(
                style: labelStyle ?? (() => Sx.TextStyles(fontSize: 10, foregroundColor: EditorPalette.TextBlue)),
                layout: () => Sx.Styles(width: labelWidth, flexShrink: 0),
                text: label
            ),
            .. fields
        ]
    );

    public static Node Section(
        string title,
        IReadOnlyList<Node> rows,
        float width = 240,
        Func<Styles>? style = null,
        Func<TextStyles>? titleStyle = null
    ) => Sx.View(
        style: () =>
        {
            var result = Sx.Styles(width: width, flexShrink: 0, flexDirection: FlexDirection.Column, gapRow: 2);
            return style is null ? result : result.Over(style());
        },
        children:
        [
            Sx.Text(
                style: titleStyle ?? (() => Sx.TextStyles(
                    fontSize: 10,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: EditorPalette.Blue
                )),
                text: () => title
            ),
            .. rows
        ]
    );

    public static Node DocumentTab(
        Func<string?> label,
        Func<bool> active,
        Action select,
        Action close,
        EditorTabAppearance appearance = EditorTabAppearance.Flat,
        bool keyboardNavigation = true,
        Func<Styles>? style = null,
        Func<string?>? name = null
    )
    {
        var hovered = new Signal<bool>(false);
        var rounded = appearance == EditorTabAppearance.Rounded;
        var caption = Sx.Text(
            style: () => Sx.TextStyles(
                fontSize: rounded ? 12 : 11,
                fontStyle: !rounded || active() ? FontStyle.Bold : FontStyle.Plain,
                foregroundColor: rounded
                    ? active() ? EditorPalette.TextBlue : new Color(255, 255, 255, 170)
                    : active() ? new Color(205, 227, 255) : new Color(130, 155, 195)
            ),
            layout: rounded ? () => Sx.Styles(flexShrink: 0) : null,
            text: label
        );
        var tab = Sx.View(
            name: name,
            style: () =>
            {
                var result = rounded ? Sx.Styles(
                    position: Position.Relative,
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    gapColumn: 6,
                    gapRow: 6,
                    paddingLeft: 10,
                    paddingRight: 6,
                    maxWidth: 220,
                    minWidth: 100,
                    flexShrink: 0,
                    backgroundColor: active() ? EditorPalette.PanelSoft : hovered.Value ? new Color(22, 42, 80, 110) : Theme.Transparent,
                    borderTop: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: active() ? EditorPalette.Border : Theme.Transparent
                ).Over(new Styles { BorderTopLeftRadius = 6, BorderTopRightRadius = 6 }) : Sx.Styles(
                    position: Position.Relative,
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    justifyContent: Justify.SpaceBetween,
                    gapColumn: 12,
                    flexShrink: 0,
                    minWidth: 140,
                    maxWidth: 260,
                    paddingLeft: 14,
                    paddingRight: 6,
                    backgroundColor: active() ? new Color(22, 42, 80) : hovered.Value ? new Color(17, 32, 57) : Theme.Transparent,
                    borderRight: 1,
                    borderColor: new Color(128, 170, 255, 30)
                );
                return style is null ? result : result.Over(style());
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            onmousedown: e => { if (!keyboardNavigation || e.Button == MouseButton.Primary) select(); },
            children:
            [
                rounded ? Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexGrow: 1, minWidth: 0),
                    children: caption
                ) : caption,
                rounded ? Sx.View(
                    style: () => Sx.Styles(
                        width: 18,
                        height: 18,
                        alignItems: Align.Center,
                        justifyContent: Justify.Center,
                        borderRadius: 4
                    ),
                    onmousedown: e => { e.StopPropagation(); close(); },
                    children: Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 14, foregroundColor: new Color(255, 255, 255, 150)),
                        text: () => "×"
                    )
                ) : Button.Create(() => "×", close, variant: ButtonVariant.Ghost, size: ButtonSize.Sm),
                rounded ? null : Sx.View(
                    style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        height: 3,
                        backgroundColor: active() ? new Color(91, 163, 255) : Theme.Transparent,
                        pointerEvents: false
                    )
                )
            ]
        );
        return keyboardNavigation ? KeyboardNavigation.Bind(
            tab,
            key => { if (key is not (Key.Enter or Key.Space)) return false; select(); return true; },
            focused => hovered.Value = focused
        ) : tab;
    }

    // Factories run inside the visible branch so each reopening owns fresh effects.
    public static Node Modal(
        Func<bool> open,
        Func<Node> content,
        Action? onDismiss = null,
        Func<Styles>? style = null,
        Func<string?>? name = null
    ) => Sx.Show(open, () => Sx.View(
        name: name,
        style: () =>
        {
            var result = Sx.Styles(
                position: Position.Absolute,
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: new Color(3, 8, 20, 175),
                zIndex: 30
            );
            return style is null ? result : result.Over(style());
        },
        onmousedown: e => { if (onDismiss is null) e.StopPropagation(); else onDismiss(); },
        children: content()
    ));

    public static Node DialogPanel(Func<Node[]> content, Func<Styles>? style = null) => Sx.View(
        style: () =>
        {
            var result = Sx.Styles(
                width: 460,
                flexDirection: FlexDirection.Column,
                gapRow: 14,
                paddingTop: 24,
                paddingBottom: 24,
                paddingLeft: 26,
                paddingRight: 26,
                backgroundColor: EditorPalette.Panel,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: EditorPalette.Border,
                borderRadius: 12
            );
            return style is null ? result : result.Over(style());
        },
        onmousedown: e => e.StopPropagation(),
        children: content()
    );

    public static Node DialogTitle(string title, string subtitle) => Sx.View(
        style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 4),
        children:
        [
            Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 22,
                    fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: EditorPalette.Blue
                ),
                text: () => title
            ),
            Sx.Text(style: () => Sx.TextStyles(fontSize: 12, foregroundColor: EditorPalette.TextBlue), text: () => subtitle)
        ]
    );
}
