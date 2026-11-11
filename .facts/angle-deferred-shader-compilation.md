---
name: angle-deferred-shader-compilation
description: "ANGLE's glCompileShader/glLinkProgram return in ~0.2 ms and the real ~3 ms cost lands at the first draw, so a naive compile benchmark shows inverted numbers."
metadata: 
  node_type: memory
  type: reference
  originSessionId: efe2f2c0-486b-44c6-a2eb-5a9bf2a7896e
  modified: 2026-09-22T19:56:28.567Z
---

ANGLE defers its GLSL ES -> HLSL translation and the subsequent D3DCompile. `glCompileShader`
and `glLinkProgram` each report roughly 0.2 ms; the actual cost lands at the **first draw after
a link**. A benchmark that times only compile and link therefore reports the first iteration as
fast and every repeat as slow, which is backwards.

Measured on this machine (RTX 4070 SUPER, ANGLE 2.1.39741, D3D11 backend) with
`scratch/AngleProbe`:

- 15 distinct shader pairs compile+link in ~13 ms total, worst pair 1.4 ms
- the identical source rebuilt is ~1 ms (ANGLE's content cache)
- `GL_OES_get_program_binary` is available, so a translated program can be baked to disk once
- draw overhead is ~0.05 us/draw static and ~0.24 us/draw with a uniform upload plus texture
  rebind each; NFMW is low-hundreds of draws per frame
- MSAA needs a multisampled **renderbuffer** + resolve blit, because
  `glTexImage2DMultisample` is ES 3.1

**Why:** a benchmark that does not separate the deferred draw from the compile step will
conclude the opposite of the truth about whether runtime shader compilation is affordable.

**How to apply:** time compile, link, use, and first draw as four separate steps. Don't
conclude anything about ANGLE shader cost from compile+link timings alone.

Note on the probe's ANGLE binaries: the `libEGL.dll`/`libGLESv2.dll` throughout this repo are
CEF **stubs** (462 KB, zero PE exports, `.text` = `31c0c3`). The probe sources a real ANGLE from
`C:\Program Files\Common Files\Adobe\Microsoft\EdgeWebView\`.

Related: [[gles3-links-varyings-by-name]], [[sokol-backend-bundle-shaders-need-hlsl]]
