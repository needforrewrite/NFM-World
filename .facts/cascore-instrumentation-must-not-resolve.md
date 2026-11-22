# CasCore instrumentation must resolve nothing

A sandboxed plugin load rewrites the plugin's IL with Mono.Cecil. That rewrite must work with no
host assembly reachable on disk, because:

- `PublishSingleFile=true` (every release workflow) keeps the host assemblies inside the apphost;
  `Assembly.Location` is empty and Cecil's search path has only the plugin's own folder and the
  runtime's TPA, both empty of `NFMWorld.*`/`TightPack`.
- A browser/wasm host has no assembly directory at all.

Any `Resolve()` during instrumentation therefore throws `AssemblyResolutionException: Failed to
resolve assembly: 'NFMWorld.Library'`, and **every** gamemode load fails in a bundled build — not
just the one that touches the resolved type. The desktop build hides it: the runtime directory is
on Cecil's search path, so resolution succeeds there.

## The one lookup that broke it

`CasAssemblyLoader.PatchMethodCall` tested `target.DeclaringType.Resolve().IsValueType` for
`call` + `HasThis`, to choose between the base-call path and the struct instance-method path. That
is unnecessary: `call` never dispatches, so the declared-member path is correct whatever the
declaring type is. `PatchBaseMethod` is stack-neutral (it only reads a guard field and calls
`CanAccess`), so it is safe for both `this` kinds.

## Why a `ResolveFailure` hook is not enough

A test or probe that clears `AssemblyResolver` search directories and hooks `ResolveFailure` does
**not** catch a reintroduced `Resolve()` on desktop: the framework directory is not one of the
removable directories, so resolution succeeds and the hook never fires. Make `Resolve` throw —
`ReaderParameters { AssemblyResolver = throwingResolver }` — and instrument that definition.
`PluginInstrumentationTests.InstrumentingAPlugin_ResolvesNoAssemblies` does this; with the old
`Resolve()` restored it fails at `PatchMethodCall`.

## Second trap: the guard's member token on an open generic method

Routing every `call` + `HasThis` through `PatchBaseMethod` also routed open generic methods there
(the TightPack formatter calls). `GuardWriter.GetAccessConstant` built an `ldtoken` from the
declared method without normalising its generic arguments or its own generic parameters, and the
JIT rejected it (`ldtoken` on a member still carrying a generic parameter does not verify). It now
emits `OverriddenDeclaringTypeMethod`, the same open-form token `GetAccessibilityConstant` already
used: type = open generic definition, method = its open form.
