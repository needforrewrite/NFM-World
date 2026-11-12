# TightPack vs MemoryPack: measured, and what the numbers mean

Harness: `TightPack/benchmarks/TightPack.Benchmarks` (not in the solution — BenchmarkDotNet 0.15.8 has no
`net11.0` moniker, so it is a hand-rolled stopwatch harness with `GC.GetAllocatedBytesForCurrentThread`).
Run `dotnet run -c Release --project TightPack/benchmarks/TightPack.Benchmarks`.

## Per-primitive costs (the numbers that drive the design)

| operation | cost |
|---|---|
| `writer.WriteInt64` direct | ~1.1 ns |
| formatter field (cached), `.Serialize` | ~1.9 ns |
| `TightPackFormatterProvider.GetFormatter<T>()` | ~2.7 ns |
| `writer.WriteValue<T>` (lookup + virtual) | ~6.4 ns |
| `typeof(T)` alone | ~0.3 ns |
| `TightPackBufferWriter.Rent+Return` | ~6 ns (was ~24 ns with a lock) |

**The provider lookup is the expensive part, not the virtual dispatch.** A base-typed vs concrete-typed
`static readonly` formatter field measured 1.90 vs 1.91 ns/op — the JIT already devirtualizes it, so a
"generated code holds a concretely-typed formatter" API buys nothing. Do not add one.

## What was actually wasteful (all fixed)

1. `TightPackWriter`/`TightPackReader` allocated a `TightPackWriterState`/`ReaderState` per call (56 B/op),
   used only by `CircularReference` types. Now created lazily; serialize is **0 B/op** in steady state.
2. `TightPackBufferWriter.Rent`/`Return` took a static `lock` — ~24 ns, more than encoding the payload.
   Replaced with a `[ThreadStatic]` free list (~6 ns).
3. `ValueTupleFormatter<...>` called `writer.WriteValue(in item)` per element → a provider lookup each.
   Now caches element formatters in static fields.
4. The generator routed every tuple member through `ValueTupleFormatter`. It now emits the elements
   directly (`MemberWriteKind.Tuple` + `MemberModel.TupleElements`), reaching `WriteInt64` etc.

## Where we stand after the fixes (vs raw MemoryPack, same shapes)

- Deserialize is generally **faster** than MemoryPack (0.38–0.6x on structs) except strings-heavy objects.
- Serialize of scalar-only structs ~1.6–4x slower; tuple-heavy structs (CarFrame shape) ~6–8x;
  a 63-frame packet ~7x serialize / ~4–5x deserialize.
- Wire size is smaller whenever the struct has padding, which is the whole point: 148 vs 160 bytes for the
  CarFrame shape, 10 vs 16 for a padded struct.
- **The remaining gap is structural, not waste.** MemoryPack serializes an unmanaged struct by blitting
  the whole image (`Unsafe.WriteUnaligned`), one memcpy, *including padding*. TightPack writes members
  individually unless the layout is proven padding-free. `InMemoryPack` also blits a raw struct array as
  one block, which is why the 63-frame packet is the worst ratio.

## Blitting is limited by the nested types, not by `Pack`

`LayoutProver` now honours `StructLayout(Pack = n)`: the pack is the alignment cap for the struct that
declares it, so a packed struct with padding-free *nested* types blits (e.g. `struct { byte A; long B; }`
with `Pack = 1` is 9 bytes and blitted). It is deliberately **not** passed down — a `Pack` on the outer
struct does not change the layout of the structs inside it, so the proof still recurses into each field
type at the host's own alignment and refuses a padded one.

That is why `CarFrame` and `PlayerState` still do not blit. `Pack = 1` removes the *outer* struct's
padding, but `CarFrame` holds padded tuples: `(byte Lap, ushort CheckpointInlap)` keeps one padding
byte, and the `sbyte`+`fix64` tuples keep several. Blitting them would put those bytes on the wire, so
the prover refuses the whole struct and it stays member-by-member. Realising the win needs the tuples
redefined as custom packed structs — a `CarFrame` change and a wire-format change, deliberately not
made. This does not affect wire compatibility: member-by-member output is byte-identical to the packed
image of a padding-free struct, and no released type's representation changed.

Two prover defects were fixed alongside:

- `System.DateTime` was listed as blittable, but `WriteDateTime` uses `ToBinary()`, which differs from
  the raw image for `DateTimeKind.Local`. A padding-free struct holding one (`{ long A; DateTime B; }`)
  would have blitted a non-image. It is no longer blittable, and `StructHoldingADateTime_IsNotBlitted`
  covers it.
- Structs holding an `[InlineArray(n)]` field were refused outright rather than sized `n * element`.
  They can now blit, which is what makes `CarFrame.WheelVelocities` expressible at all.
