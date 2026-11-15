namespace NFMWorldLibrary.Util;

public static class CarColorCopy
{
    public static IReadOnlyList<CarColorSlot> Match(
        IReadOnlyList<CarColorSlot> destination,
        IReadOnlyList<CarColorSlot> source
    )
        => destination.Where(slot => slot.Slot >= 0 && slot.Slot < CarPaint.SlotCount)
            .Join(
                source,
                slot => slot.Slot,
                slot => slot.Slot,
                (target, color) => new CarColorSlot(target.Slot, CarPaint.ClampColor(color.Color))
            )
            .ToArray();
}
