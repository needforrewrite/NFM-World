# Serialization is TightPack, not MemoryPack

NFMWorld's wire and on-disk format is **TightPack** (`TightPack/`, three projects in
`nfm-world.slnx`). MemoryPack still builds inside the solution because `FixedMathSharp`,
`Maxine.Extensions` and the `MemoryPack/` submodule reference it, but **no NFMWorld code uses it**.
Anything describing a MemoryPack formatter as load-bearing for NFM-World is stale.

## Shape

- `[TightPackable]` replaces `[MemoryPackable]`; `TightPackOrder`/`TightPackUnion`/
  `TightPackIgnore`/`TightPackConstructor`/`TightPackOnDeserialized` are the direct renames. Counts
  matched one-for-one, so a grep for `MemoryPack[A-Z]` under the game projects should return only
  comments.
- `ITightPackable<TSelf>` replaces `IMemoryPackable<TSelf>`. `IReadableWritable<TSelf>` is
  unchanged in shape but now serializes through `TightPackSerializer` and no longer takes an
  options object (`MemoryPackHelpers` was deleted).
- `TightPackSerializer.Serialize(in T)` / `Deserialize<T>(ReadOnlySpan<byte>)` are the entry points.
  Generic overloads **constrain on `ITightPackable<T>`**, so any generic helper that used to
  serialize `T` freely now needs `where T : ITightPackable<T>` — that was the single most common
  migration error (`SendServerEvent<T>`, `BroadcastEvent<T>`, `RpcMessage.Create/Deserialize<T>`,
  `LobbyPacketTests.RoundTrip<T>`).
- The wire format carries no struct padding. The generator only blits a struct when it proves the
  layout has no padding at all. `StructLayout(Pack = n)` lowers the alignment cap for the struct that
  declares it but is not passed into nested types, so `CarFrame` — which holds `(byte, ushort)` and
  `sbyte`+`fix64` tuples — is still written member-by-member.

## Migration traps (each one cost a build)

| Trap | Rule |
|---|---|
| Untyped serialization | `TightPackSerializer.SerializeUntyped(Type, object)` / `DeserializeUntyped(...)`, and `writer.WriteValue(Type, object?)` / `reader.ReadValue(Type, ref object?)`, exist only for code that reaches a type purely by reflection (plugin event types in tests, `DynamicUnionFormatter<T>`). They go through `IUntypedValueFormatter`; the provider builds an `UntypedFormatterAdapter<T>` by reflection once per closed type and drops it from `s_untyped` on `UnregisterAssembly`. |
| Union case vs. union base | Serializing a derived event type directly writes no tag. Code that must round-trip through the union has to serialize as `type.BaseType`. |
| Perf hot path | Generated code writes a tuple member element by element (`MemberWriteKind.Tuple`) rather than through `ValueTupleFormatter`, which resolves an element formatter per element. Tuple/collection formatters of their own cache element formatters in static fields for the same reason. Measured: a provider lookup is ~2.7ns, `WriteValue<T>` ~6.4ns, a cached formatter field ~1.9ns, a direct `WriteInt64` ~1.1ns, and a `typeof(T)` ~0.3ns. Watch out for per-element provider lookups in any new formatter. |
| Formatter lifetime | Lookups are memoised per closed type in `TightPackFormatterProvider.Cache<T>` — safe for collectible contexts, since an instantiation over a plugin's types is allocated in that context's loader allocator and dies with it; a `Type`-keyed cache would pin. Any registration change bumps a generation counter that invalidates every memo. Generated types additionally hold their formatters in their own static fields, and `UnregisterAssembly` drops a plugin's registrations. |
| Value types from other assemblies | `fix64`, `f64Vector3`, `Int3`, `f64Euler`, `Nibble<T>` live in projects that do not reference TightPack, so they cannot carry `[TightPackable]`. They get hand-written formatters in `NFMWorld.Library/Serialization/TightPackFormatters.cs`, registered from a `[ModuleInitializer]`. |
| Opting a member in | `[TightPackAllowSerialize]` on a **member** or on the **declaring type** defers to the provider at runtime. Applied to a type it also covers `required`/`init` members the generator cannot otherwise classify. |
| Wrapped packable types | A packable type reached only through a wrapper (`Nullable<T>`, `ReadOnlyMemory<T>`, `List<T>`, `ImmutableArray<T>`, `InlineArray2/3/4<T>`) has no generated code of its own in that position, so the **wrapping type registers it** (`DependentFormatters` → `AddDependentFormatters`). Without that the provider's only source is a reflection-built formatter: correct under JIT, invisible to the AOT compiler (`NFMWorld.Library` is AOT-compiled), so only a published build would fail. `GameStateSnapshot.Results` (`RaceResults?`) was the real case. `GeneratorEmitTests` asserts the emitted registration, because a static constructor runs once per process and no runtime test can re-observe it. |
| `required` members | A `required` member cannot be left unset by a plain `new`, so the generator must rebuild the value through an object initializer. `UsesInPlace` checks `IsRequired` for this. |
| Nested packable types | A nested `[TightPackable]` struct inside a struct declaration needs its own attribute — the outer attribute does not reach it. `S2C_RaceStarted.GameJoinInfo` was missed on the first pass. |
| Containers must be partial | The generator emits the enclosing types too, so every container needs `partial`, and the emitted declaration has to match the real one (`struct` containers were being emitted as `class`). |
| Plugin sandbox | `PluginPolicy.AllowTightPack` replaced `AllowMemoryPack`; it admits `TightPackReader`/`TightPackWriter<>` methods by name prefix, all of which route through `GetSpan` and its bounds check. `Unsafe` stays denied. `GeneratedFormatterTypes` additionally names every formatter the generator can emit, because generated code constructs it in the plugin's own static initializer — without it a plugin declaring a `Stack<T>` member throws in its type initializer. Multi-dimensional arrays go through the same path: their `.ctor`/`Get`/`Set` report one shared metadata token, so CasCore authorises them via `CasPolicyBuilder.AllowArrayMembers()` (set by `WithSandboxedSystem`) rather than `MemberId`. |

## What was bumped

- `SavedTimeTrial.CURRENT_VERSION` 6 → **7** — the recording format changed wholesale.
- `Rad3dPoly.CurrentTriangulationVersion` 2 → **3** — the serialized polygon layout changed.

`RadpackType` enum byte values are part of the on-disk radpack header and were **not** renumbered;
neither were the union tags.
