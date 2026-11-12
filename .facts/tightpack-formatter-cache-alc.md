# TightPack formatter cache and ALC unload safety

`TightPackFormatterProvider.TryGetFormatter<T>` memoises per closed type in a private
`static class Cache<T> { TightPackFormatter<T>? Formatter; int Generation; }`.

## Why a generic static field is safe where a `Type`-keyed dictionary is not

A generic instantiation whose type arguments come from a **collectible** `AssemblyLoadContext` is
itself allocated in that context's loader allocator, so the memo dies with the plugin. A plain
`ConcurrentDictionary<Type, object>` keyed by a plugin type pins it (the dictionary lives in the
non-collectible default context), which is what `UnregisterAssembly` exists to clean up.

Proven both ways with `NFMWorld.Library.Test/GamemodePluginUnloadTests`:
`End_ReleasesTheAssembly_AfterItsTypesWereSerialized` also resolves the plugin event type through
`TightPackFormatterProvider.GetFormatter<T>` (via `MakeGenericMethod`) so the memo is actually
populated. Swapping the memo for a `Type`-keyed dictionary makes it fail; the real implementation
passes.

## Invalidation

Every registration change bumps `s_generation` (`Register`, `Unregister`, and `UnregisterAssembly`
when it dropped something). A memo is trusted only when `Cache<T>.Generation == s_generation`.
After resolving, the generation is re-read: if it moved during the resolve the result is served but
**not** memoised. `EnsureBuiltinsRegistered()` therefore runs *before* the generation read, outside
`Resolve` — otherwise the first lookup in a fresh process could never be memoised.

`TryGetFormatter` order: ensure built-ins → read generation → memo hit → `Resolve` → memoise.
`Resolve` is the `s_formatters` dictionary lookup then `DefaultFormatters.TryCreate`.

## Tests that guard this

- `TightPack.Tests/InfrastructureTests.cs`: `Provider_BuildsAFallbackFormatterOnlyOnce`
  (`AreSame` across two calls) and `Provider_ResolvesAgainAfterTheRegistrationChanges`. Both were
  falsified by removing the memo / the generation bumps respectively.
- `GamemodePluginUnloadTests`: the ALC guard.
