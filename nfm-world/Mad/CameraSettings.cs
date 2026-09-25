namespace NFMWorld;

public class CameraSettings
{
    public static float Fov { get; set; } = PerspectiveCamera.DefaultFov;
    public static bool SmoothFov { get; set; } = true;

    /// <summary>
    /// Squared cull distance for <see cref="RenderQueue.AddInstanced"/>, compared against
    /// <c>Vector3.DistanceSquared(camera, object)</c>.
    ///
    /// <see cref="float.MaxValue"/> is the "effectively unlimited" sentinel, and the *squared*
    /// form is why it has to be that rather than <see cref="int.MaxValue"/>. A stage is laid out
    /// on a ~100 000-unit grid and the camera sits thousands of units back from the car, so a
    /// squared distance routinely reaches several times 1e9. <c>int.MaxValue</c> is 2.147e9, which
    /// squared-space is only ~46 341 units - not unlimited at all, but a real cut-off that removes
    /// distant stage pieces, scenery and cars while leaving the sky, ground, clouds and mountains
    /// (@see RenderQueue.AddImmediate, which is not culled) untouched on screen.
    /// </summary>
    public static float RenderDistanceSqr = float.MaxValue;
}