using NFMWorldLibrary.Util;
using SignalsDotnet;

namespace NFMWorld.UI.ViewModels;

public class HsbColorState
{
    public int MinimumChannel { get; }
    public Signal<short> R { get; } = new(0);
    public Signal<short> G { get; } = new(0);
    public Signal<short> B { get; } = new(0);
    public Signal<float> Hue { get; } = new(0);
    public Signal<float> Saturation { get; } = new(0);
    public Signal<float> Brightness { get; } = new(1);
    public Color3 Color => new(R.Value, G.Value, B.Value);

    public HsbColorState(Color3 color, int minimumChannel = 0)
    {
        MinimumChannel = minimumChannel;
        Set(color);
    }

    public float Channel(int channel) => channel switch
    {
        0 => Hue.Value,
        1 => Saturation.Value,
        _ => Brightness.Value
    };

    public void Set(Color3 color)
    {
        R.Value = color.R;
        G.Value = color.G;
        B.Value = color.B;
        color.ToHSB(out var hue, out var saturation, out var brightness);
        Hue.Value = hue;
        Saturation.Value = saturation;
        Brightness.Value = brightness;
    }

    public Color3 Clamp(Color3 color) => new(
        (short)Math.Clamp((int)color.R, MinimumChannel, 255),
        (short)Math.Clamp((int)color.G, MinimumChannel, 255),
        (short)Math.Clamp((int)color.B, MinimumChannel, 255)
    );

    public void SetChannel(int channel, double value)
    {
        var normalized = (float)Math.Clamp(value, channel == 2 ? MinimumChannel / 255.0 : 0, 1);
        switch (channel)
        {
            case 0: Hue.Value = normalized; break;
            case 1: Saturation.Value = normalized; break;
            default: Brightness.Value = normalized; break;
        }

        var color = Clamp(Color3.FromHSB(Hue.Value, Saturation.Value, Brightness.Value));
        R.Value = color.R;
        G.Value = color.G;
        B.Value = color.B;
    }
}
