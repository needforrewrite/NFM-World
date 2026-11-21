using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld;

internal static class StageMeshCache
{
    private static readonly IdleResourceCache<Rad3d, Mesh> Cache = new(
        64,
        TimeSpan.FromMinutes(2),
        mesh => mesh.Dispose(),
        Rad3d.VisualEqualityComparer.Instance
    );

    private static readonly IdleResourceCache<Mesh, Mesh> DisabledFixHoops = new(
        64,
        TimeSpan.FromMinutes(2),
        mesh => mesh.Dispose()
    );

    public static ResourceLease<Mesh> Acquire(Rad3d model, Mesh.Prepared? prepared = null)
        => Cache.Acquire(model, _ => prepared is null ? new Mesh(model) : new Mesh(prepared));

    public static ResourceLease<Mesh> AcquireDisabledFixHoop(Mesh mesh)
        => DisabledFixHoops.Acquire(mesh, static original => new Mesh(original) { Darken = 0.45f });

    public static bool TryAcquire(Rad3d model, out ResourceLease<Mesh>? lease) => Cache.TryAcquire(model, out lease);

    public static void Trim(bool clearUnused = false)
    {
        DisabledFixHoops.Trim(clearUnused);
        Cache.Trim(clearUnused);
    }
}
