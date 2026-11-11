---
name: luacsharp-table-enumerator-movenext-index-bug
description: "Lua-CSharp's LuaValueDictionary/LuaStringDictionary MoveNext had an index-advance bug causing infinite loops in C# foreach over LuaTable"
metadata: 
  node_type: memory
  pinned: false
  originSessionId: d09da698-5845-4c96-8fff-fa842632d987
  modified: 2026-09-16T00:47:28.111Z
---

In Lua-CSharp (`Lua-CSharp/src/Lua/Internal/LuaValueDictionary.cs` and `LuaStringDictionary.cs`), the static `MoveNext(dictionary, version, ref index, out current)` helpers used by `LuaTable.LuaTableEnumerator` (the type backing C# `foreach` over a `LuaTable`) had a bug: after finding a non-nil entry at slot `index + i`, they did `index += i` instead of `index += i + 1`. This left the enumerator's index parked *on* the entry it just returned rather than past it, so the very next `MoveNext()` call rescanned from the same slot, found the same entry again, and repeated forever. This was a genuine infinite loop (not just slowness) affecting any C# code that does `foreach (var (k, v) in someLuaTable)` over a table whose string-keyed or generic-keyed hash part is non-empty — which in this codebase includes `NFMWorld.Library/Util/Lua/LuaJson.cs`, `Gamemodes/Lua/LuaGamemodeConfig.cs`, and `Gamemodes/Lua/LuaServerGamemode.cs`. Lua-level `pairs()` loops were unaffected because they go through the key-based `TryGetNext`/`TryGetFirstFrom` path instead of this enumerator.

The lesson: when investigating a reported "hang" in this interpreter, don't stop at the first plausible-looking slow-but-technically-terminating explanation (e.g. an async test's `Task.Delay(1)` costing ~15ms/call due to Windows timer granularity, compounding across many loop iterations to look hang-like). The user pointed specifically at "LuaTable enumeration is infinitely looping" after I reported the delay-based explanation as the root cause, and that correction was right — there was a separate, real infinite loop in the enumerator's index arithmetic. Verify a hang's cause by checking whether the suspect loop actually terminates given enough real time (it did, in ~18s, for the delay explanation) versus tracing the actual index/counter bookkeeping in enumerator-style code before concluding "it's just slow."
