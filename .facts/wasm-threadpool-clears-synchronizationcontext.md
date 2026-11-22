# On wasm, running any ThreadPool work clears the main thread's SynchronizationContext

The browser runtime executes ThreadPool work on the **main thread** inside its own dispatch, and
afterwards `SynchronizationContext.Current` is **null** — not restored, not something else. The
thread id never changes (`t1` throughout), so nothing about thread identity explains it.

Measured with a minimal AOT app (`SetSynchronizationContext(new MyCtx())` then each probe in turn):

|Probe|`Current` after|
|---|---|
|JSExport returning normally, `requestAnimationFrame`, `setTimeout`|`MyCtx` — survives|
|`ExecutionContext.Run` with a captured EC|`MyCtx` — survives|
|`ThreadPool.QueueUserWorkItem(_ => {})`, then drain|**null**|
|`Task.Run(...)`, then drain|**null**|
|`await Task.Delay(1)` / `Task.Yield()` / `Task.Delay(1).ConfigureAwait(false)`|**null**|

So it is not that a context is being replaced with another one; the ThreadPool's dispatch leaves the
slot empty. Anything that relied on a context installed **once** at boot breaks as soon as some
`Task.Run` runs — which is why the initial whole-boot sequence looks healthy (`Install` → … →
`LoadContent` all report `GameThreadContext`) and the very next frame throws
`Current SynchronizationContext is not a GameThreadContext`.

The game's fix is `GameThreadContext.EnsureInstalled()`, called at the top of `WorldGame.Tick` and
once in the constructor. It reuses the previously installed instance rather than allocating a new one,
because the context owns the queue of posts the frame drains — a fresh instance would strand work
already queued on the old one.

Do not "fix" this by catching the exception or by making `Current` fall back to a new instance: the
queue is the state that matters, and a silent fallback loses it. Note that desktop is unaffected
(its ThreadPool is on other threads), so a desktop-only reproduction will not show this.
