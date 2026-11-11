---
name: signalsdotnet-subscription-leak
description: "SignalsDotnet 3.4.1 leaked one observer per compute on reused scopes; vendored fork fixes it, and how the leak looked in profiling."
metadata:
  node_type: memory
  type: project
  originSessionId: 801be4ee-65e8-4734-a738-e776e3e84940
  modified: 2026-11-07T03:06:39.928Z
---

NFM-World vendors **SignalsDotnet** as a submodule (`SignalsDotnet/`, `uwx/SignalsDotnet` branch `nfmw`) instead of the
NuGet package, because upstream 3.4.1 has an unbounded listener leak.

- **The bug**: `SignalsRequestedObservable.SubscribeCore` did `subject.Subscribe(...)` and **discarded the returned
  disposable**; its `SignalsRequestedDisposable` only cleared the ambient `_currentScope` AsyncLocal. So any compute
  that ran while a scope was already installed left an observer on that scope's subject **permanently**. 3.4.2 still
  has it; the fork carries a two-line fix (store the subscription, dispose it with the scope).
- **The symptom** (this is what makes it hard to find): not a high per-frame cost — a cost that **aggregates over
  time**. After ~30 min the game fell to 2 fps. A dotTrace snapshot showed `WorldSidebar.Build` accessors →
  `WorldHud.Tick` → `Signal.Value` → `Subject.OnNext`; `UiScheduler.Flush` was ~90% of the main thread. A heap dump
  was the giveaway: ~1.1M `R3.AnonymousObserver<INotifySignalChanged>` and ~558K `Subject+ObserverNode` on **one**
  subject, while only a few hundred `RenderScope.Bind` effects existed. R3's `Subject` field is `root` (not `head`)
  and the chain is `<Next>k__BackingField`.
- **Repro outside the game**: `new Effect(() => { _ = computed.Value; })` where `computed = Signal.Computed(...)`.
  336 B per write at one computed level, doubling per level. A plain signal read does not leak. **Don't** be lured by
  the pinned `_currentScope` you see in a heap root — that's a symptom; clearing it does nothing, and clearing it
  inside the effect callback silently disables dependency tracking (6 `SxRendererTests` fail).
- **Guard**: `NFMWorld.Library.Test/SignalsDotnetLeakTests.cs` pins a scope, mounts slots, writes 200 frames and
  asserts the observer chain stays under 40. Verified it reads 805 on unpatched 3.4.1.

**Why:** a dependency bump that drops the fork silently reintroduces a slow-motion slowdown that looks like a
per-frame perf problem and would be chased in the HUD code again.

**How to apply:** on any SignalsDotnet bump, rebase the `nfmw` commit and run that test. See [[sx-csharp-ui-traps]]
and [[gl-smoke-tests-never-switch-pipelines]] for the same lesson: a green suite is not proof for a path the suite
does not exercise.
