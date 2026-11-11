---
name: ask-user-to-verify-visuals-dont-inject-input
description: "Visual verification in NFM-World is mine to run (screenshots, readbacks, narrow visual checks); hand over to the user only when the check is broad enough to need human judgement — never drive the UI with simulated WinAPI/SendKeys input"
metadata:
  node_type: memory
  pinned: true
  originSessionId: 5ad88fbb-551f-4012-91b8-733df9894e73
  modified: 2026-11-05T19:21:04.194Z
---

Visual tests are mine to run. Take screenshots (`PrintWindow` is read-only and fine), read textures
back, and assert on pixels myself. Hand a check to the user only when it is broad enough to need
human judgement — "does this feel right", "is this the map you expected", art direction, anything
where the expected result is a matter of taste rather than a value I can assert.

What remains off-limits: synthesising mouse/keyboard input with WinAPI
(`SetCursorPos`/`mouse_event`), `SendKeys`, or similar to click through menus. That was the original
rule, and its reasons still hold — foreground focus is contended (`SetForegroundWindow` from a
background process usually fails silently, and SDL ignores mouse-button events for unfocused
windows, so clicks appear to do nothing while hover works), and injecting cursor movement steals the
machine from whoever is using it. Reach a screen through the game itself instead.

History: the earlier version of this memory said visual verification was the user's job outright.
They lifted that on 2026-11-05 — "i'm giving you standing authorization to do visual tests … except
when the visual test is broad enough that it needs a human's look" — after a run of hand-offs where
the useful evidence (a pixel readback, a screenshot) was something I could have collected myself.
