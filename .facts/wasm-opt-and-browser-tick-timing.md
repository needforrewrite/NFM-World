# wasm-opt buys ~5% on the browser, and cross-tab timing is invalid here

`NFMWorld.Browser.csproj` sets `WasmRunWasmOpt=true`. Binaryen `-O2` rewrites only the composite
R2R image (`PublishReadyToRunComposite=true` means all app code is in that one file; the other 143
`.wasm` are framework stubs, so **1 stamp of 144 is correct**, not a failure).

Measured on a 7-car race (`mp/evil_clown`, WebGPU, 1600x900):

| | no-opt | wasm-opt |
|---|---|---|
| composite | 58,927,072 B | 56,175,508 B (-4.7%) |
| tick min | 2.600 ms | 2.450 ms (-5.8%) |
| tick p1 | 2.725 ms | 2.600 ms (-4.6%) |
| tick p10 | 3.025 ms | 2.875 ms (-5.0%) |

The gain tracks the code-size reduction almost exactly (icache), so it is real but small: ~0.1 ms
on a ~2 ms frame. It **cannot** touch the ~1.2-1.45 ms per-frame floor, which is main-thread
UI/layout and stays invariant to renderer, AA, shadow distance/resolution, render distance and
resolution. The floor is what needs the `Component.LayoutAndRender` dirty check
(`NFMWorld.UI/Component.cs`), not a codegen flag.

## Why browser tick timing is so easy to get wrong

Three traps, each of which produced a confident wrong answer first:

- **Cross-tab A/B is invalid.** Two tabs in one browser get unequal rAF cadence (measured 8.4 ms vs
  4.2 ms), and they share the host CPU. Measure one tab at a time and alternate.
- **`IsFixedTimeStep` is false** (`NFMWorld/WorldGame.cs`, the `Tick` overload), so each frame
  consumes *real* elapsed time. rAF cadence therefore changes the work done per frame, and a
  paused-vs-driving comparison is not simulation-vs-nothing.
- **p50 is noise under host load.** Discord/Firefox/Edge were enough to swing the same binary from
  1.2 ms to 3.8 ms. Use `min`/`p1`/`p10`, which are preemption-free; those agreed to within 0.5%
  across rounds while p50 did not.

Drive it with a synchronous hook in the **publish** output's `main.js` (gitignored, so no repo
change, but re-apply after every publish), paused race, one call per sample:

```js
globalThis.__bench = (n) => {
    const d = new Array(n);
    for (let i = 0; i < n; i++) {
        const a = performance.now();
        exports.NFMWorld.Browser.Program.Tick();
        d[i] = performance.now() - a;
    }
    return d;
};
```

Compare code sizes against a **clean** no-opt publish. An incremental publish dir keeps stale
fingerprinted composites, and reading the wrong one made an earlier run report the optimized image
as *smaller* than its baseline.
