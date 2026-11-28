using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Util;
using SignalsDotnet;

namespace NFMWorld.UI.Components;

internal sealed class HsbColorPicker : View
{
    private readonly Func<Color3> _read;
    private readonly HsbColorState _state;

    public HsbColorPicker(Func<Color3> read, Action<Color3> write)
    {
        _read = read;
        _state = new HsbColorState(read());
        Styles = Sx.Styles(width: 100.Percent(), flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 2);
        string[] labels = ["Hue", "Saturation", "Brightness"];

        for (var i = 0; i < labels.Length; i++)
        {
            var channel = i;
            var focused = new Signal<bool>(false);

            void Change(double value)
            {
                Sync();
                _state.SetChannel(channel, value);
                write(_state.Color);
            }

            var slider = new ColorSpectrumSlider(_state, channel, Change)
            {
                Styles = Sx.Styles(height: 20, flexGrow: 1, minWidth: 0)
            };
            KeyboardNavigation.Bind(
                slider,
                key =>
                {
                    if (key is not (Key.Left or Key.Right or Key.Home or Key.End))

                        return false;
                    Change(key == Key.Home ? 0 : key == Key.End ? 1
                    : _state.Channel(channel) + (key == Key.Left ? -1 : 1) / 100.0);

                    return true;
                },
                value => focused.Value = value
            );
            AddChild(Sx.View(
                style: () => Sx.Styles(
                    width: 100.Percent(),
                    height: 20,
                    flexShrink: 0,
                    alignItems: Align.Center,
                    gapColumn: 4,
                    backgroundColor: focused.Value ? Theme.MenuHighlight : Theme.Transparent
                ),
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 9, foregroundColor: Theme.MenuTextBlue),
                        layout: () => Sx.Styles(width: 56, flexShrink: 0),
                        text: () => labels[channel]
                    ),
                    slider,
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 9, foregroundColor: Theme.MenuDim),
                        layout: () => Sx.Styles(width: 28, flexShrink: 0),
                        text: () => channel == 0 ? $"{Math.Round(_state.Hue.Value * 360)}°"
                            : $"{Math.Round(_state.Channel(channel) * 100)}%"
                    )
                ]
            )
            );
        }
    }

    private void Sync()
    {
        var color = _read();
        if (color != _state.Color)
            _state.Set(color);
    }

    protected override void GameTick() => Sync();
}
