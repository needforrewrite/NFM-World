namespace ShaderPoc;

/// <summary>
/// Reassigns texture and sampler slots across a program's merged binding lists.
///
/// This is sokol-shdc's <c>bind_slot_map</c> pass. It exists because the two stages
/// reflect independently, but a slot is a *pipeline-wide* index in sokol_gfx, not a
/// per-stage one: a vertex shader that only reads <c>ShadowMap0</c> may report it at
/// slot 0 while the fragment shader reports the same texture at slot 2, and forwarding
/// both verbatim would leave the stages disagreeing about one resource.
///
/// The rule is greedy and declaration-preserving:
/// <list type="bullet">
///   <item>a declared slot is kept if nothing has claimed it yet;</item>
///   <item>otherwise (including the common collision at 0) the next free slot is used.</item>
/// </list>
/// That keeps explicitly distinct <c>register(tN)</c> assignments intact -- which the
/// host-side binding may depend on -- while repairing sources that all declare the same
/// register, as the D3D9-era ones did, since there the texture and sampler were a
/// single object and every shadow map could be written <c>register(t0)</c>.
/// </summary>
public static class BindingSlots
{
    /// <summary>Renumbers both lists into disjoint, gap-free slot spaces.</summary>
    public static (IReadOnlyList<TextureBinding> Textures, IReadOnlyList<SamplerBinding> Samplers) Assign(
        IReadOnlyList<TextureBinding> textures,
        IReadOnlyList<SamplerBinding> samplers)
        => (Renumber(textures, t => t.Slot, (t, s) => t with { Slot = s }),
            Renumber(samplers, s => s.Slot, (s, n) => s with { Slot = n }));

    private static IReadOnlyList<T> Renumber<T>(
        IReadOnlyList<T> bindings,
        Func<T, int> slotOf,
        Func<T, int, T> withSlot)
    {
        var taken = new HashSet<int>();
        var result = new List<T>(bindings.Count);
        var next = 0;

        foreach (var binding in bindings)
        {
            var declared = slotOf(binding);
            var slot = declared >= 0 && taken.Add(declared) ? declared : TakeNext(taken, ref next);
            result.Add(withSlot(binding, slot));
        }
        return result;
    }

    private static int TakeNext(HashSet<int> taken, ref int next)
    {
        while (!taken.Add(next)) next++;
        return next;
    }
}
