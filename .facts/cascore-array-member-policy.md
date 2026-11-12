# CasCore fork: array intrinsic members and spans

`CasCore/` submodule (`uwx/CasCore`, branch `nfmw`) carries three fork fixes on top of 0.3.0:
`base.Foo()` through the static guard, JSON bindings .NET 11 removed, and array member policy.

## The array oversight

Every array type exposes intrinsic members `.ctor`, `Get`, `Set`, `Address`. For *any* array type and
*any* of the four, `MetadataToken` is `0x06000000` and `Module` is `System.Private.CoreLib`. So
`MemberId` (module handle + token) cannot distinguish them — `ResolveMethod(0x6000000)` throws
`ArgumentOutOfRangeException`. `WithSandboxedSystem()` bound `System.Array`, which covers members
*declared on* `Array`, not the pseudo-members on a closed array type. Consequence: `new T[i, j]`
and `arr[i, j]` were refused in every sandboxed plugin, while `new T[i]`/`arr[i]` worked because
those compile to `newarr`/`ldelem`, not a member call.

## Fix

- `CasPolicyBuilder.AllowArrayMembers()` sets a `bool`.
- `CasPolicy` stores it; `CanAccess(MethodBase)` returns it when
  `method.DeclaringType.IsArray` and the name is one of `.ctor`/`Get`/`Set`/`Address`, **before** the
  token lookup.
- `WithSandboxedSystem()` calls `.AllowArrayMembers()` next to the `System.Array` binding.
- A default `CasPolicyBuilder()` still denies arrays.

Granularity is deliberately wholesale: tokens cannot separate array types, so a per-element-type
restriction is not expressible.

## Array -> ReadOnlySpan

`ReadOnlySpan<T>.op_Implicit(T[])` was also commented out, but only because `typeof(T[])` cannot
compile outside a generic context. Build it from the open generic's own parameter:

```csharp
.WithMethod("op_Implicit", [typeof(System.ReadOnlySpan<>).GetGenericArguments()[0].MakeArrayType(),],
    Accessibility.Protected)
```

Safe: `ReadOnlySpan<T>` is a ref struct (no boxing, no heap field, no `await` survival), every access
is bounds-checked, the GC tracks the interior pointer, and it grants nothing beyond the array the
plugin already holds. It is what lets a plugin pass a `byte[]` to
`TightPackSerializer.Deserialize<T>(ReadOnlySpan<byte>)` — without it plugins could serialise but
never deserialise. `Span<T>`'s mutable equivalent stays commented out (same safety argument, no
current need).

## Tests

`CasCore.Tests.Csharp/TestArrays.cs` and `TestSpans.cs` (`[TestSuccessful]` attributes; runner is
`CasCore.Tests.Host`). Run:

```
cd CasCore && dotnet build CasCore.sln && dotnet run --project CasCore.Tests.Host
```

87 pass / 2 fail (89 total). **The 2 failures are pre-existing and unrelated** —
`TestReflection.TestAccess*CreateInstanceViaInvoke` both throw "No policy set for assembly
DouglasDwyer.CasCore". Do not chase them.

## Separate limitation found

`TypeFormatter` writes `FullName, AssemblyName`, and `Type.GetType` does not search a collectible
load context — so a plugin cannot round-trip a `Type` member whose value names the plugin's own
types. A corelib type round-trips fine.
