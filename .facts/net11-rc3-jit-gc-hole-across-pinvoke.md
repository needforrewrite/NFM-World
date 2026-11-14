---
name: net11-rc3-jit-gc-hole-across-pinvoke
description: ".NET 11 rc.3's JIT kept a byref in a callee-saved register across inline P/Invokes, crashing the GC mid-draw on D3D11; fixed in 11.0.0 GA (dotnet/runtime#135374). CI's 'preview' quality kept installing rc.3 after GA shipped."
metadata:
  node_type: memory
  type: project
  modified: 2026-11-14T00:00:00.000Z
---

Crash 4320 (2026-11-13, D3D11, mid-race): a background GC failed fast in
`coreclr!PromoteCarefully` while walking the main thread, which was parked in
`D3D11CommandBuffer.DrawInternal` returning from `context->IASetIndexBuffer`. The Tier1 GC info
reported `+rsi(interior)` live at that inline P/Invoke return. A walk from an InlinedCallFrame
only recovers rsp/rbp, so the saved slot for rsi was null. The register held the
`ID3D11DeviceContext*` that TerraFX's `Unsafe.AsPointer(ref this)` wrappers turn into a byref.

This is dotnet/runtime#135374 ("JIT: byref in callee-saved register across unmanaged call crashes
GC (.NET 11)"). It was fixed by #135381, backported to release/11.0 as #136079 on 2026-10-19, and
shipped in 11.0.0 GA (runtime 11.0.26.53018). rc.3 (11.0.26.52348) does not have the fix.

**Why it shipped:** every workflow used `dotnet-version: '11.0.x'` with `dotnet-quality: 'preview'`.
`dotnet-install -Channel 11.0 -Quality preview` still resolved to `11.0.100-rc.3.26522.148` after GA
was released, so self-contained builds kept bundling the rc.3 runtime. The quality was dropped on
2026-11-14, and `11.0.x` now resolves to 11.0.100.

**How to apply:**
- Fatal GC crashes in COM/function-pointer interop on a self-contained build: check the bundled
  `coreclr.dll` version in the dump (`lmvm coreclr`) before suspecting game code.
- To find GC holes in a dump: for each jitted body, compare `dotnet-dump gcinfo <addr>` against
  cdb's `!U <addr>`. At every inline P/Invoke return (`mov byte ptr [rX+4],1`), no register may be
  GC-live. Fully interruptible bodies (OSR) print +/- deltas instead of safepoint lines, so live
  sets have to be replayed. cdb's own `!gcinfo` printed no safepoints for these bodies, and
  dotnet-dump has no disassembler, so both tools are needed.
- Repro: drive the real D3D11 backend in a tight draw loop while another thread calls `GC.Collect()`.
  On rc.3 that crashed 3/3 runs within seconds with `0x80131506`; on GA it survived 3/3.
