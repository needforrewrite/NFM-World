using NFMWorld.Reactor;

namespace NFMWorld.UI;

public static class Extensions
{
    public static PercentValue Percent(this int percent) => new(percent);
    public static PercentValue Percent(this float percent) => new(percent);
    public static Pixels Pixels(this int px) => new(px);
    public static Pixels Pixels(this float px) => new(px);
}

public readonly record struct PercentValue(float Percent)
{
    public static implicit operator MeasurementWidthHeight(PercentValue percent)
        => MeasurementWidthHeight.Percent(percent.Percent);

    public static implicit operator MeasurementMarginPosition(PercentValue percent)
        => MeasurementMarginPosition.Percent(percent.Percent);

    public static implicit operator MeasurementGap(PercentValue percent) => MeasurementGap.Percent(percent.Percent);
}
