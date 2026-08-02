namespace NFMWorldLibrary;

public enum DistantOutlineBehavior
{
    // Same perspective-like sizing as DistanceFalloff, with a short linear fade to zero at its final cutoff.
    DistanceFalloffWithCutoff = 0,
    // Perspective-like mode: outlines keep shrinking with distance, but never get hard-hidden by the minimum visible thickness.
    DistanceFalloff = 1,
    // Original NFM-style behavior render outlines at full width until a fixed distance, then hide them.
    ClassicCutoff = 2,
    // debug / simple mode, always render outlines
    AlwaysRender = 3,
    // Do not render outlines
    HideOutlines = 4
}

public static partial class World
{
    public static DistantOutlineBehavior DistantOutlineBehavior = DistantOutlineBehavior.DistanceFalloffWithCutoff;

    // The default follow camera is 838 units from the car, 900 prevents shrinking too early
    // Past this point, falloff modes use inverse-depth sizing
    public static float OutlineFalloffStartDistance = 900f;

    // ClassicCutoff is a sharp cutoff that matches the original game's approximate outline cutoff distance.
    // It does not scale with user outline width; thicker lines only survive farther in falloff with cutoff mode.
    public static float OutlineClassicCutoffDistance = 3000f;

    // Defines the width-dependent depth where falloff with cutoff reaches zero and stops drawing the line.
    public static float OutlineMinimumVisibleThickness = 0.1f;

    // FalloffWithCutoff switches from inverse-depth sizing to a linear fade this far before the line
    // would reach OutlineMinimumVisibleThickness, then reaches zero at the original cutoff distance.
    public static float OutlineLinearFadeDistance = 1000f;
}
