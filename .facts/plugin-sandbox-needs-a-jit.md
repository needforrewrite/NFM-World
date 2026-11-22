# The plugin sandbox needs a JIT, not just dynamic-code support

`PluginPolicy.Supported` gates the CasCore sandbox: false means plugins load through the plain
`PluginLoadContext` (resident on browser) and a downloaded gamemode package is refused.

It was `RuntimeFeature.IsDynamicCodeSupported`, which is **true on Mono's wasm interpreter**. The
sandbox therefore ran in the browser and died on the first plugin load:
`TypeInitialization_Type, DouglasDwyer.CasCore.LateBindingResolver` — its static initializer binds
`RuntimeTypeHandle.GetInterfaceMethodImplementation` and `RuntimeMethodHandle.GetMethodFromCanonical`
by name and calls `GetFunctionPointer()`; Mono has neither. A race start surfaced it as
"Could not load stage: TypeInitialization_Type, …". Every gamemode and AI load failed.

Measured in the browser (probe app on `Microsoft.NET.Sdk.WebAssembly`, interpreter, no AOT):

| member | desktop CoreCLR | browser-wasm |
|---|---|---|
| `RuntimeFeature.IsDynamicCodeSupported` | true | **true** |
| `RuntimeFeature.IsDynamicCodeCompiled` | true | **false** |
| `RuntimeTypeHandle.GetInterfaceMethodImplementation` | present | missing |
| `RuntimeMethodHandle.GetMethodFromCanonical` | present | missing |
| `System.SZArrayHelper` | present | missing |

`IsDynamicCodeCompiled` is the discriminator: Mono marks it `[Intrinsic]` and the interpreter and
the AOT compiler set it false, so it is exactly "this runtime compiles code it did not see at build
time". `IsDynamicCodeSupported` means "can generate code at all", which the interpreter satisfies.

Desktop behaviour is unchanged (`PluginSandboxTests` still refuses every Hostile probe); the
browser takes the unsandboxed path it used before CasCore landed.
