# The plugin sandbox runs on the wasm interpreter — there is no JIT requirement

`PluginPolicy.Supported` gates the CasCore sandbox: false means plugins load through the plain
`PluginLoadContext` and a downloaded gamemode package is refused.

It used to be `RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled`, on
the theory that CasCore's verification compiles the rewritten method so the JIT's own verifier
judges it. That theory is wrong and the extra conjunct disabled the sandbox in the browser for no
reason.

## Why there is no JIT requirement

Decompiled `DouglasDwyer.JitIlVerification` 0.1.4:

- `VerifiableAssemblyLoader.InstrumentAssembly` inserts `ldsfld GuardField; pop` at the head of
  every method and emits a `JitIlVerification.Guard.<Method>_<n>` type whose `.cctor` calls
  `VerifiableAssemblyLoader.Verify(RuntimeTypeHandle, RuntimeMethodHandle)`.
- `Verify` is **managed** — 4,800 lines of `ILImporter` over `MethodBase.GetMethodBody()`,
  `ILReader`, a stack-state machine and `ErrorArgument[]`. No QCalls, no `GetFunctionPointer`, no
  `DynamicMethod`, no `Expression.Compile`. It is an IL re-interpreter, not a JIT pass.
- The `.cctor` fires lazily on the first call to each guarded method, so a method that is never
  called is never verified.

So the runtime requirement is **dynamic code** (`IsDynamicCodeSupported`), not a JIT. Mono's
interpreter has it.

## Measured — browser, trimmed, no AOT

Probe: a real compiled plugin assembly embedded as a resource, loaded through `CasAssemblyLoader`
with the same policy group list `PluginPolicy.DefaultSandbox` uses, in headless Edge.

| check | result |
|---|---|
| `IsDynamicCodeSupported` / `IsDynamicCodeCompiled` | true / **false** |
| policy built under trimming | ok (needed the linker roots, below) |
| plugin load + instrument + verify | ok, ~100 ms for a 4-method assembly |
| `Add(20,22)` | 42 |
| admitted host member | ok |
| `File.ReadAllText` | `SecurityException` |
| interface virtual call (`string.Join`) | `a,b,c` — `LateBindingResolver` works |
| body corrupted to a stack underflow | `InvalidProgramException` — verification **is** active |

That last row is the one that matters: verification is not silently skipped, and a method body
rewritten by the sandbox's own machinery is judged before it runs.

## What actually blocked the browser, and what did not

Blocked, now fixed:

1. **`WithSandboxedRuntimeReflection` NREd on Mono** because it bound `System.SZArrayHelper`
   unconditionally, and Mono has no such type. `CasPolicyBuilderExtensions.AllowIfPresent` now
   skips a name that is absent instead of failing the whole default sandbox.
2. **`LateBindingResolver`** bound `RuntimeTypeHandle.GetInterfaceMethodImplementation` and
   `RuntimeMethodHandle.GetMethodFromCanonical` through `GetFunctionPointer()` in its static
   initializer, which threw `TypeInitialization_Type` on the first `callvirt` and took every plugin
   load with it. It now answers dispatch through reflection (`Type.GetInterfaceMap`, a
   base-definition walk, the delegate trick for arrays), memoised in a
   `ConditionalWeakTable<Type, DispatchMap>` — weakly keyed, or a collectible plugin never unloads.
3. **Trimming dropped the sandbox's framework surface.** `NFMWorld/PluginSandbox.linker.xml` was
   desktop-only; the browser head now imports it too. Without it the probe died at policy
   construction with `FileNotFoundException: System.Collections.Concurrent`.

Did not block, contrary to the earlier note:

- **Verification** — managed, see above.
- **Instrumentation at load** — Cecil runs fine on the interpreter.

## Collectible contexts are still desktop-only

`PluginLoadContext.IsCollectibleSupported` is `!OperatingSystem.IsBrowser()`. On wasm the plugin
context is non-collectible, so one context per id is reused for the life of the page.
`PluginAssembly.Load`'s sandbox branch used to hardcode `isCollectible: true`; it now follows that
flag, or the browser would ask for a collectible context it cannot create.
