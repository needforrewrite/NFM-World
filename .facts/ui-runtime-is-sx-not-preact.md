---
name: ui-runtime-is-sx-not-preact
description: "NFM-World UI code runs on the Sx signals runtime (data/library/sx), not preact-luau; read sx/GUIDE.md first"
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 82654b4b-5d87-4a00-ad71-91d984ebd93a
  modified: 2026-09-15T11:17:37.955Z
---

The `.luaux`/`.luau` UI code under `NFMWorld.Library/data/uis/` runs on **Sx**, the
SolidJS-style signals renderer in `NFMWorld.Library/data/library/sx/`
(`signals.luau`, `dom.luau`, `h.luau`, `host.luau`, `styled.luau`). The user
corrected me when I started investigating a UI bug in
`NFMWorld.Library/data/library/preact-luau/` and pointed me at
`data/library/sx/GUIDE.md` instead.

`preact-luau/` and `data/library/react.luau` are a separate, legacy React-style
implementation and are not what the game's UIs use. When debugging component
behavior — refs, effects, lifecycle, prop application — start from the Sx sources
and `sx/GUIDE.md`, not from the preact tree, even though the preact directory is
larger and matches React vocabulary more obviously.
