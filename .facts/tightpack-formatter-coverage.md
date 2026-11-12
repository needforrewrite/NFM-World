# TightPack formatter coverage (MemoryPack parity)

`TightPack/Formatters/` now covers the same CLR type surface MemoryPack does. The split matters,
because it decides whether a member "just works" or has to opt in.

## Auto-resolved (a member of this type needs nothing)

- Sequences: `List`, `Stack`, `Queue`, `LinkedList`, `SortedSet`, `HashSet`, `PriorityQueue`,
  `Collection`, `ObservableCollection`, `ConcurrentQueue/Stack/Bag`, `ReadOnlyCollection`,
  `ReadOnlyObservableCollection`, `BlockingCollection`, `ArraySegment`.
- Maps: `Dictionary`, `SortedDictionary`, `SortedList`, `ConcurrentDictionary`, `KeyValuePair`.
- Interfaces: `IEnumerable`, `IList`, `IReadOnlyList`, `ICollection`, `IReadOnlyCollection`,
  `ISet`, `IReadOnlySet`, `IDictionary`, `IReadOnlyDictionary`. These rebuild as the concrete
  `List`/`HashSet`/`Dictionary` that backs them.
- Arrays of rank 1–4; `Memory<T>`, `ReadOnlyMemory<T>`, `ImmutableArray<T>`, `InlineArray2/3/4<T>`,
  tuples (`ValueTuple<…>`), `Nullable<T>`, `BitArray`, `Type`.

## Opt-in only (a formatter for the same type already exists)

`StringFormatter` owns `string`, and `ArrayFormatter<bool>` owns `bool[]`, so these collide and are
never registered by default. Use `[TightPackCustomFormatter(typeof(...))]` on the member, or register
the instance yourself:

- `Utf8StringFormatter` — the default UTF-8 encoding.
- `Utf16StringFormatter` — UTF-16 code units; the only string formatter that accepts a lone surrogate.
- `InternStringFormatter` — `string.Intern` on read.
- `BrotliStringFormatter`, `BrotliFormatter` (`byte[]`), `BrotliFormatter<T>` — Brotli-compressed.
- `BitPackFormatter` — `bool[]` as one bit per element.
- `DynamicUnionFormatter<T>` — union cases declared at run time as `(tag, Type)` pairs, dispatching
  through `writer.WriteValue(Type, object?)`. Prefer `[TightPackUnion]`, which is generated.

Picking any of these makes that choice part of the member's wire format, so changing it later is a
wire change.

## Rules that are easy to get wrong

| Rule | Why |
|---|---|
| Brotli writes the uncompressed length *before* the payload, and the reader checks it against the size limit before allocating | The payload arrives from the network. `TryReadCollectionHeader` cannot be used for that length: it rejects a length larger than the remaining buffer, and a compressed payload is normally far smaller than the bytes it expands to. |
| `BitPackFormatter` uses `TryReadPackedCollectionHeader` | Same reason: eight elements share a byte, so the "one byte per element" bound is false. Bound the 32-bit group count instead. |
| `Stack` and `ConcurrentStack` write in reverse enumeration order | Both enumerate top-first and the constructors push in order, so writing enumeration order reverses the stack. |
| `ReadOnlyCollection`/`ReadOnlyObservableCollection`/`BlockingCollection`/`SortedList` rebuild from a `List`/`Dictionary` | None of them has a settable backing, so a round trip allocates a new one. Comparers are not preserved. |
| `ArraySegment<T>` writes only its elements | The offset refers to another array and is meaningless once the bytes leave the process. |
| `TypeFormatter` writes `FullName, AssemblyName` | Version, culture and token change on every rebuild. It cannot resolve a type from a collectible plugin load context, and a trimmed build cannot resolve a name it never saw. |
| `PluginPolicy.GeneratedFormatterTypes` must list every generatable formatter | The generated code constructs the formatter in the plugin's own type initializer, so a missing entry throws `SecurityException` there. `PluginSandboxTests.SerializingAnEventWithProviderResolvedMembers_Works` guards it. |
| Array members need an explicit CasCore switch | Every array's `.ctor`/`Get`/`Set`/`Address` reports `MetadataToken 0x06000000`, so `MemberId` cannot identify them. `CasPolicy` answers them from `AllowArrayMembers()` instead, which `WithSandboxedSystem()` sets. Plain `new T[i]` needs none of it — that is `newarr`, not a member call. |
| A plugin still cannot deserialize from a `byte[]` | `TightPackSerializer.Deserialize<T>(ReadOnlySpan<byte>)` needs `ReadOnlySpan<byte>.op_Implicit(byte[])`, which CasCore's default sandbox leaves commented out. Serializing works; plugins only ever deserialize through the host, so nothing is blocked today. |

## Test coverage

`TightPack/tests/TightPack.Tests/ParityFormatterTests.cs` — one test per formatter family: ordering
(`Stack` top-first, `Sorted*`, `PriorityQueue`), null round trips, set interfaces, array shapes,
`Type` (including null), `BitPack` wire length, UTF-16 lone surrogate, UTF-8 rejection, `Intern`
reference identity, Brotli round trip / empty / null / size limit, `BrotliFormatter<T>`, and
`DynamicUnionFormatter` for each case, an unmapped type and a duplicate tag. The two behaviours that
were wrong on the first pass — `ConcurrentStack` order and the bit-packed length header — are
asserted directly, not incidentally.
