using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI.Components;

internal sealed class ColorSpectrumSlider(
    HsbColorState slot,
    int channel,
    Action<double> onChange,
    Action<bool>? interactionChanged = null) : View
{
    private static readonly Color[] Spectrum =
        [new(255, 15, 15), new(255, 255, 15), new(15, 255, 15), new(15, 255, 255),
            new(15, 15, 255), new(255, 15, 255), new(255, 15, 15)];

    private double Minimum => channel == 2 ? slot.MinimumChannel / 255.0 : 0;

    protected override void OnMousePressed(MouseEvent e)
    {
        if (e.Button == MouseButton.Primary)
        {
            interactionChanged?.Invoke(true);
            SetFromPointer(e.Position.X);
        }
    }

    protected override void OnMouseDragged(MouseDragEvent e)
    {
        if (e.Button == (byte)MouseButton.Primary)
            SetFromPointer(e.Position.X);
    }

    protected override void OnMouseReleased(MouseEvent e)
    {
        if (e.Button == MouseButton.Primary)
            interactionChanged?.Invoke(false);
    }

    private void SetFromPointer(float x)
    {
        var inset = 4 * G.Scale;
        var width = LayoutContentSize.X - inset * 2;

        if (width <= 0)

            return;
        var progress = Math.Clamp((x - LayoutContentPosition.X - inset) / width, 0, 1);
        onChange(Minimum + progress * (1 - Minimum));
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var inset = 4 * G.Scale;
        var x = position.X + inset;
        var width = MathF.Max(0, size.X - inset * 2);
        var height = 10 * G.Scale;
        var y = position.Y + (size.Y - height) / 2;
        Color Sample(float saturation, float brightness)
            => slot.Clamp(Color3.FromHSB(slot.Hue.Value, saturation, brightness));

        Color[] colors = channel switch
        {
            0 => Spectrum,
            1 => [Sample(0, slot.Brightness.Value), Sample(1, slot.Brightness.Value)],
            _ => [Sample(slot.Saturation.Value, (float)Minimum), Sample(slot.Saturation.Value, 1)]
        };
        G.SaveState();

        // Draw two-colour segments: the graphics adapter truncates longer gradients.

        for (var i = 0; i < colors.Length - 1; i++)
        {
            var start = (int)(x + width * i / (colors.Length - 1));
            var end = (int)(x + width * (i + 1) / (colors.Length - 1));
            G.SetLinearGradient(start, (int)y, end - start, 0, [colors[i], colors[i + 1]], null);
            G.FillRect(start, (int)y, end - start, (int)height);
        }

        var progress = Math.Clamp((slot.Channel(channel) - Minimum) / (1 - Minimum), 0, 1);
        var handleX = x + (float)progress * width - 3 * G.Scale;
        G.SetColor(new Color(235, 244, 255));
        G.SetStrokeWidth(MathF.Max(1, G.Scale));
        G.DrawRect((int)handleX, (int)(y - 2 * G.Scale), (int)(6 * G.Scale), (int)(height + 4 * G.Scale));
        G.RestoreState();
    }
}
