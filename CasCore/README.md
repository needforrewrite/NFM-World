Vendored from https://github.com/DouglasDwyer/CasCore at `488193c` (MIT, see `LICENSE`).
The NuGet package is 0.3.0 and has no newer release.

Patched here, because a plugin sandbox cannot work without them on this runtime:

- `CasAssemblyLoader.PatchMethodCall` sends a non-virtual instance `call` (a `base.Foo()`
  call) to `PatchStaticMethod`, whose guard refuses every overridable method - so every
  plugin override that calls its base threw `SecurityException`. It now has its own check
  (`PatchBaseMethod` / `CanCallDeclared`), which asks the policy about the declared member.
- `GuardWriter` keys its guard dictionaries with Cecil's reference equality; its generated
  `OverriddenDeclaringTypeMethod` hashes to a malformed dictionary, which surfaced as a
  `BadImageFormatException` from the guard's static constructor.
- `CasPolicyBuilderExtensions.WithDefaultSandbox` throws on .NET 11: its
  `JsonSerializerOptions` bindings name members the BCL no longer has. Fixed with the
  parameter overloads; `PluginPolicy` still builds the sandbox group by group so a future
  drift takes one group down instead of the whole policy.

Re-apply these when updating.
