# Blitting a struct with private fields

`LayoutProver` was never the blocker. Two independent gates in `TypeParser.cs` decide it.

## 1. Private members are not serialized at all

`ParseMembers` skips any member whose `DeclaredAccessibility != Public` unless it carries
`[TightPackInclude]`. A struct built entirely from private fields (e.g. `CompactIpAddress`) therefore
produced `WriteObjectHeader(0)` — it compiled, round-tripped, and silently wrote nothing. That is the
failure mode to look for: zero members, not a compile error.

## 2. Blit shape required an *assignable* member list

`IsBlitShaped` requires the serialized members to be exactly the instance fields in declaration order,
so `[TightPackInclude]` on each private field fixes the shape. But `ParseConstructor` ran first and
rejected a `readonly` field (no assignment, no matching ctor) with TPK0002 — even though a blitted
value is rebuilt from its image and needs no assignment at all. The blit proof now runs **before**
`ParseConstructor`, which is skipped entirely when `Blittable` is set.

## Soundness rules kept

- A struct with an excluded (`[TightPackIgnore]` or non-public, non-included) field is **not** blittable:
  the image contains a field the payload would skip, so it stays on the member path.
- A type with any `[TightPackOnSerializing/…]` callback is **not** blittable: the blit path writes the
  image and returns without running them.
- `TightPackBlit.VerifySize<T>` (emitted into the static ctor) still fails loudly if the analyzer's
  padding-free proof disagrees with `Unsafe.SizeOf`.

## InlineArray arity

`IsProviderResolved` / `BuildFormatterExpression` / `DependentArguments` listed only `InlineArray2/3/4`
while `LayoutProver` already modelled any `[InlineArray(n)]` and the runtime provider (now) has 2–16.
`InlineArray8<ushort>` therefore failed with TPK0004 "not a primitive, a [TightPackable] type, or a known
collection". All three lists now cover 2–16, and `PluginPolicy.GeneratedFormatterTypes` must name every
one of them.
