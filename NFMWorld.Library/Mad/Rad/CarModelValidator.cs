using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Rad;

/// <summary>
/// Whether a model is a car the physics and the renderer can actually run.
/// </summary>
/// <remarks>
/// The garage's structural gate is <see cref="HasWheelsAndGeometry"/>: the physics builds a
/// four-wheel contact set from <c>Wheels[0..3]</c> and reads each position as an <c>int</c>, so a
/// model with fewer wheels throws in the constructor. Publishing needs more than that — an
/// upload is handed to every player who joins a race — so <see cref="Validate"/> also rejects
/// values nothing else checks: the divisors the physics divides by, magnitudes that wrap the
/// fixed-point math, and vertices/triangle indices that would index outside the mesh.
/// </remarks>
public static class CarModelValidator
{
    /// <summary>Model and wheel coordinates. A car is a few hundred units across.</summary>
    private const double MaxGeometryMagnitude = 100_000;

    /// <summary>
    /// Any single <c>fix64</c> stat. The type is 32.32 fixed point and the physics cubes some
    /// stats, so this leaves room for the products without wrapping. The <c>int</c> stats are not
    /// capped: <c>powerloss</c> and <c>maxmag</c> legitimately sit at <see cref="int.MaxValue"/>.
    /// </summary>
    private const double MaxStatMagnitude = 1_000_000;

    /// <summary>
    /// The garage's rule: four wheels for the contact set, and polygons to draw. Cheap, so it is
    /// what the pickers filter with.
    /// </summary>
    public static bool HasWheelsAndGeometry(Rad3d rad)
        => rad.Wheels is { Length: >= 4 } && rad.Polys is { Length: > 0 };

    /// <summary>
    /// Full check before a model is published or accepted. Returns the reason it is not a usable
    /// car, or null when it is.
    /// </summary>
    public static string? Validate(Rad3d rad)
    {
        if (rad.Wheels is not { Length: >= 4 } wheels)

            return "A car needs at least four wheels.";

        if (rad.Polys is not { Length: > 0 } polys)

            return "The model has no polygons.";

        for (var i = 0; i < wheels.Length; i++)
        {
            var wheel = wheels[i];

            if (OutOfRange(wheel.Position.X) || OutOfRange(wheel.Position.Y) || OutOfRange(wheel.Position.Z))

                return $"Wheel {i + 1} is positioned outside the model.";
            if (OutOfRange(wheel.Width) || OutOfRange(wheel.Height))

                return $"Wheel {i + 1} has an unusable size.";
        }

        for (var i = 0; i < polys.Length; i++)
        {
            if (ValidatePoly(polys[i]) is { } polyError)

                return $"Polygon {i + 1} {polyError}";
        }

        if (ValidateStats(rad.Stats) is { } statError)

            return statError;

        if (rad.RechargedStats is { } recharged && ValidateStats(recharged) is { } rechargedError)

            return $"The recharged stats are unusable: {rechargedError}";

        return null;
    }

    private static string? ValidatePoly(Rad3dPoly poly)
    {
        if (poly.Points is not { Length: >= 3 } points)

            return "has fewer than three points.";

        foreach (var point in points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))

                return "has a point that is not a number.";

            if (Math.Abs(point.X) > MaxGeometryMagnitude
                || Math.Abs(point.Y) > MaxGeometryMagnitude
                || Math.Abs(point.Z) > MaxGeometryMagnitude)

                return "has a point far outside the model.";
        }

        var triangles = poly.Triangles;

        if (triangles.IsDefaultOrEmpty)

            return null;

        if (triangles.Length % 3 != 0)

            return "has an incomplete triangle.";

        foreach (var index in triangles)
        {
            if (index >= points.Length)

                return "has a triangle pointing outside itself.";
        }

        return null;
    }

    /// <summary>
    /// The stats a car is driven with. <see cref="CarStats.Validate"/> covers "every value was
    /// actually written"; the rest are values the physics cannot survive.
    /// </summary>
    private static string? ValidateStats(CarStats stats)
    {
        // CarStats.ValidateStats substitutes Tornado Shark stats for a partly-written set, so a
        // published car would not be the author's car.

        if (stats.InvalidStat is { } missing)

            return $"The model is missing its {missing} stat.";
        if (string.IsNullOrWhiteSpace(stats.Name) || stats.Name == "hogan rewish")

            return "The model has no car name.";

        // The physics divides by these.

        if (stats.Simag <= fix64.Zero || OutOfStatRange(stats.Simag))

            return "Simag must be positive and finite: the physics divides by it.";

        // Shipped cars use int.MaxValue here to mean "no drag"; only zero is fatal.

        if (stats.Powerloss < 1)

            return "Powerloss must be at least 1: the physics divides by it.";
        if (stats.Grip <= fix64.Zero || OutOfStatRange(stats.Grip))

            return "Grip must be positive and finite: the physics divides by it.";
        if (stats.Swits[2] <= 0 || OutOfStatRange(stats.Swits[2]))

            return "Top speed must be positive: the physics divides by it.";

        if (OutOfStatRange(stats.Bounce) || stats.Bounce < fix64.Zero)

            return "Bounce is out of range.";
        if (OutOfStatRange(stats.Moment) || stats.Moment < fix64.Zero)

            return "Moment is out of range.";
        if (OutOfStatRange(stats.Comprad) || stats.Comprad < fix64.Zero)

            return "Comprad is out of range.";
        if (OutOfStatRange(stats.Push) || stats.Push < fix64.Zero)

            return "Push is out of range.";
        if (OutOfStatRange(stats.Revpush) || stats.Revpush < fix64.Zero)

            return "Revpush is out of range.";
        if (OutOfStatRange(stats.Airs) || stats.Airs < fix64.Zero)

            return "Airs is out of range.";
        if (OutOfStatRange(stats.Dammult) || stats.Dammult < fix64.Zero)

            return "Dammult is out of range.";
        if (OutOfStatRange(stats.Outdam) || stats.Outdam < fix64.Zero)

            return "Outdam is out of range.";
        if (OutOfStatRange(stats.Dishandle) || stats.Dishandle < fix64.Zero)

            return "Dishandle is out of range.";
        if (OutOfStatRange(stats.Turn) || stats.Turn < fix64.Zero)

            return "Turn is out of range.";
        if (OutOfStatRange(stats.Flipy))

            return "Flipy is out of range.";
        if (OutOfStatRange(stats.Acelf.X) || OutOfStatRange(stats.Acelf.Y) || OutOfStatRange(stats.Acelf.Z))

            return "Acelf is out of range.";
        if (stats.Swits[0] < 0 || stats.Swits[1] < 0)

            return "Swits is out of range.";

        if (stats.Maxmag <= 0)

            return "Maxmag must be positive: it is the car's health.";
        if (stats.Clrad < 0)

            return "Clrad cannot be negative.";
        if (stats.TurnRadius < 0)

            return "Turn radius cannot be negative.";
        if (stats.Airc < 0 || stats.Handb < 0 || stats.Lift < 0 || stats.Revlift < 0)

            return "Handb, Airc, Lift and Revlift cannot be negative.";

        return null;
    }

    private static bool OutOfRange(fix64 value)
        => Math.Abs((double)value) > MaxGeometryMagnitude;

    /// <summary>
    /// Only the <c>fix64</c> stats are capped: the physics multiplies and cubes those, so a huge
    /// value wraps the 32.32 fixed point. The <c>int</c> stats are used directly (Powerloss and
    /// Maxmag legitimately sit at <see cref="int.MaxValue"/>) and are only checked for sign.
    /// </summary>
    private static bool OutOfStatRange(fix64 value)
        => Math.Abs((double)value) > MaxStatMagnitude;
}
