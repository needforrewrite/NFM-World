---
name: luavalue-must-stay-unboxed-fixed64
description: "Never propose shrinking Lua-CSharp's LuaValue by boxing or moving Fixed64/Vector3d/f64Euler out of its inline union"
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 206b7d7e-90bb-4fb6-b9ff-4de59f6f1730
  modified: 2026-09-12T15:05:31.836Z
---

In the NFM-World Lua-CSharp interpreter, `LuaValue` is 40 bytes because its explicit-layout union inlines the NFMW fixed-point types (`Fixed64`, `Vector3d` at 24 bytes, `f64AngleSingle`, `f64Euler`). The user has ruled out shrinking `LuaValue` by boxing those types or storing them by reference: keeping fixed-point math allocation-free is a hard requirement for the game, so the 40-byte size is a fixed constraint.

Do not propose "make LuaValue 16 bytes by moving Vector3d/f64Euler out of the union" as a performance lever again. Performance work on table memory, stack memory, or `LuaValueDictionary.Entry` size must find gains elsewhere (e.g. fewer entries per table, a compact key representation that doesn't embed a full `LuaValue`, fewer wrapper objects per table).
