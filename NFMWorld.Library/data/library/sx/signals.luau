-- Sx reactive core — a Lua port of SolidJS's fine-grained reactivity primitives.
--
-- Provides: createSignal / createMemo / createEffect / createRoot / batch / untrack /
-- onCleanup / onMount / getOwner / runWithOwner / setScheduler / flushSync.
--
-- Model (push-pull graph, ported from Solid's reactive/signal.js):
--   * A SIGNAL is a mutable cell { value, observers }.
--   * A COMPUTATION (memo or effect) is a node { fn, value, state, sources, observers,
--     owned, cleanups, owner, disposed, isEffect }.
--   * Reading a signal/memo while a computation is running registers that computation as
--     an observer (dependency tracking).
--   * Writing a signal marks its observers STALE and propagates staleness transitively
--     through memos, queueing effects. Effects run on a batched, dependency-ordered
--     scheduler; memos recompute lazily on read (pull).
--
-- The scheduler is injectable (default: synchronous flush, used by tests). The game /
-- benchmark injects UiLib.defer so a frame's worth of signal writes coalesce into ONE
-- end-of-frame effect pass.

local Sx = {}

-- ------------------------------------------------------------------ state
local STALE = 0
local PENDING = 1
local CLEAN = 2

-- A computation is a memo or effect node in the reactive graph.
export type Computation = {
    fn: (any) -> any,
    value: any,
    state: number,
    sources: { SignalNode<any> | Computation }?,
    observers: { Computation }?,
    owned: { Computation }?,
    cleanups: { (() -> ()) }?,
    owner: Computation?,
    disposed: boolean,
    isEffect: boolean,
}

-- A signal cell: { value, observers }.
export type SignalNode<T> = {
    kind: "signal",
    value: T?,
    observers: { Computation }?,
    owner: Computation?,
}

-- Any node that can be read (and thus observed): a signal or a computation.
export type ReactiveNode<T> = SignalNode<T> | Computation

local Owner: Computation? = nil    -- currently executing owner (root or computation)
local Listener: Computation? = nil -- currently executing computation (memo/effect)
local batching = 0
local Flushing: boolean = false
local Scheduled: boolean = false
local Scheduler: (((() -> ())) -> ())? = nil
local PendingEffects: { Computation } = {}
local effectQueueLen = 0
local PendingRootName: string? = nil -- devtools: name to tag the next createRoot's root computation

-- ------------------------------------------------------------------ devtools hooks
-- Optional introspection used by the Sx devtools. Everything here defaults to inert so
-- the hot path is untouched unless devtools installs a hook / reads the registry.
local DebugHooks = {
    onChange = nil, -- (node) -> () called after a signal write that marks observers stale
}
local LiveNodes: { { node: any, kind: string }? } = {} -- registry of live signal/computation nodes
local StyledNames = setmetatable({}, { __mode = "k" }) -- devtools: styled component fn -> name (weak keys; Lua-CSharp fns can't hold props)

local function registerLive(node: any, kind: string)
    LiveNodes[#LiveNodes + 1] = { node = node, kind = kind }
end

local function unregisterLive(node: any)
    for i = 1, #LiveNodes do
        if LiveNodes[i] and LiveNodes[i].node == node then
            local last = LiveNodes[#LiveNodes]
            LiveNodes[i] = last
            LiveNodes[#LiveNodes] = nil
            break
        end
    end
end

-- ------------------------------------------------------------------ forward decls
local updateEffect: (node: Computation) -> ()
local updateMemo: (node: Computation) -> ()
local disposeComputation: (node: Computation) -> ()

-- ------------------------------------------------------------------ graph helpers

-- Register `comp` (the current Listener) as an observer of node `n`, and `n` as a
-- source of `comp`. No-op if `n` is already a source of `comp`.
local function trackRead(n: ReactiveNode<any>)
    local comp = Listener
    if comp == nil then
        return
    end
    local sources = comp.sources
    if sources then
        for i = 1, #sources do
            if sources[i] == n then
                return
            end
        end
    end
    if comp.sources == nil then
        comp.sources = {}
    end
    table.insert(comp.sources, n)
    if n.observers == nil then
        n.observers = {}
    end
    table.insert(n.observers, comp)
end

-- Remove `comp` from `n.observers`.
local function untrackSource(n: SignalNode<any> | Computation, comp: Computation)
    if n.observers then
        local obs = n.observers
        for i = #obs, 1, -1 do
            if obs[i] == comp then
                table.remove(obs, i)
                break
            end
        end
    end
end

-- Tear down a computation: run cleanups, dispose owned children (marking them disposed so
-- the effect queue skips them even if they were already queued STALE), unsubscribe sources.
local function cleanNode(node: Computation)
    if node.cleanups then
        local cleanups = node.cleanups
        for i = #cleanups, 1, -1 do
            cleanups[i]()
        end
        node.cleanups = nil
    end
    if node.owned then
        local owned = node.owned
        for i = 1, #owned do
            local child = owned[i]
            child.disposed = true
            cleanNode(child)
        end
        node.owned = nil
    end
    if node.sources then
        local sources = node.sources
        for _, n in ipairs(sources) do
            untrackSource(n, node)
        end
        node.sources = nil
    end
end

disposeComputation = function(node: Computation)
    if node.disposed then
        return
    end
    node.disposed = true
    unregisterLive(node)
    cleanNode(node)
    if node.owner and node.owner.owned then
        local owned = node.owner.owned
        for i = 1, #owned do
            if owned[i] == node then
                table.remove(owned, i)
                break
            end
        end
    end
end

-- ------------------------------------------------------------------ scheduling

local function queueEffect(node: Computation)
    effectQueueLen = effectQueueLen + 1
    PendingEffects[effectQueueLen] = node
end

-- Mark a computation STALE and propagate upward to its observers. Effects get queued;
-- memos are lazy (recomputed on read) but their staleness still reaches downstream effects.
local function markStale(comp: Computation)
    if comp.disposed or comp.state == STALE then
        return
    end
    comp.state = STALE
    if comp.isEffect then
        queueEffect(comp)
    end
    if comp.observers then
        local observers = comp.observers
        for i = 1, #observers do
            markStale(observers[i])
        end
    end
end

-- Recompute a memo (pull). Only runs when STALE. Stores the result into node.value.
updateMemo = function(node: Computation)
    if node.state ~= STALE then
        return
    end
    cleanNode(node)
    local prevOwner, prevListener = Owner, Listener
    Owner = node
    Listener = node
    local ok, result = pcall(node.fn, node.value)
    Owner, Listener = prevOwner, prevListener
    if not ok then
        error(result)
    end
    node.value = result
    node.state = CLEAN
end

-- Run an effect's fn, resubscribing dependencies.
updateEffect = function(node: Computation)
    cleanNode(node)
    local prevOwner, prevListener = Owner, Listener
    Owner = node
    Listener = node
    local ok, result = pcall(node.fn, node.value)
    Owner, Listener = prevOwner, prevListener
    node.state = CLEAN
    if not ok then
        error(result)
    end
end

-- Drain the effect queue. Re-entrant writes (effects writing signals) push onto the
-- same queue and are processed in the same flush (dependency order, FIFO).
local function flushEffects(): ()
    local guard = 0
    while effectQueueLen > 0 do
        guard = guard + 1
        if guard > 10000 then
            error("Sx: effect cycle detected (too many update passes)")
        end
        local q = PendingEffects
        local qlen = effectQueueLen
        PendingEffects = {}
        effectQueueLen = 0
        for i = 1, qlen do
            local node = q[i]
            q[i] = nil
            if node.state == STALE and not node.disposed then
                updateEffect(node)
            end
        end
    end
end

local function scheduleFlush(): ()
    if batching > 0 then
        return
    end
    if Flushing then
        return
    end
    if Scheduler ~= nil then
        if not Scheduled then
            Scheduled = true
            Scheduler(function()
                Scheduled = false
                flushEffects()
            end)
        end
    else
        flushEffects()
    end
end

-- ------------------------------------------------------------------ public API

export type Accessor<T> = () -> T

export type Signal<T> = { get: Accessor<T>, set: (T) -> () }

local function createSignal<T>(value: T): (Accessor<T>, (T) -> ())
    local s: SignalNode<T> = {
        kind = "signal",
        value = value,
        observers = nil,
        owner = Owner,
    }
    registerLive(s, "signal")
    local read: Accessor<T> = function()
        trackRead(s)
        return s.value
    end
    local write: (T) -> () = function(newValue)
        if newValue == s.value then
            return
        end
        s.value = newValue
        if s.observers then
            local observers = s.observers
            for i = 1, #observers do
                markStale(observers[i])
            end
        end
        scheduleFlush()
        if DebugHooks.onChange and s.observers and #s.observers > 0 then
            DebugHooks.onChange(s)
        end
    end
    return read, write
end

local function createComputation(fn: (any) -> any, isEffect: boolean): Computation
    local node: Computation = {
        fn = fn,
        value = nil,
        state = STALE,
        sources = nil,
        observers = nil,
        owned = nil,
        cleanups = nil,
        owner = Owner,
        disposed = false,
        isEffect = isEffect,
    }
    if Owner then
        if Owner.owned == nil then
            Owner.owned = {}
        end
        table.insert(Owner.owned, node)
    end
    registerLive(node, "computation")
    return node
end

local function createMemo<T>(fn: () -> T, value: T?): Accessor<T>
    local node = createComputation(fn, false)
    node.value = value
    local read: Accessor<T> = function()
        if node.state ~= CLEAN then
            updateMemo(node)
        end
        trackRead(node)
        return node.value
    end
    return read
end

local function createEffect(fn: () -> ()): () -> ()
    local node = createComputation(fn, true)
    updateEffect(node)
    return function()
        disposeComputation(node)
    end
end

local function createRoot<T>(fn: ((() -> ())) -> T): T
    local prevOwner, prevListener = Owner, Listener
    local root: Computation = {
        fn = nil,
        value = nil,
        state = CLEAN,
        sources = nil,
        observers = nil,
        owned = nil,
        cleanups = nil,
        owner = nil,
        disposed = false,
        isEffect = false,
    }
    if PendingRootName ~= nil then
        -- devtools: tag this root so signals/memos created under it can be attributed to
        -- the owning component via Sx.debug.ownerName().
        root.__devName = PendingRootName
        PendingRootName = nil
    end
    Owner = root
    Listener = nil
    local ok, result = pcall(function()
        return fn(function()
            disposeComputation(root)
        end)
    end)
    Owner, Listener = prevOwner, prevListener
    if not ok then
        error(result)
    end
    return result
end

local function batch<T>(fn: () -> T): T
    batching = batching + 1
    local ok, result = pcall(fn)
    batching = batching - 1
    if not ok then
        error(result)
    end
    if batching == 0 then
        scheduleFlush()
    end
    return result
end

local function untrack<T>(fn: () -> T): T
    local prevListener = Listener
    Listener = nil
    local ok, result = pcall(fn)
    Listener = prevListener
    if not ok then
        error(result)
    end
    return result
end

local function onCleanup(fn: () -> ()): ()
    if Owner then
        if Owner.cleanups == nil then
            Owner.cleanups = {}
        end
        table.insert(Owner.cleanups, fn)
    end
end

-- Run `fn` once, AFTER the current mount pass. createEffect runs its body eagerly, so
-- calling it here would run `fn` while the component body is still executing — before the
-- descriptor it returns has been turned into host nodes, so refs are still empty and
-- nothing is in the tree yet. Instead the computation is queued (never tracked: it is
-- created STALE, run once by the flush, and then stays CLEAN since it subscribes to
-- nothing under untrack). The queue is drained by the flush that follows the mount —
-- render()'s flushSync for the initial tree, or the in-progress flushEffects pass for a
-- subtree mounted later by Show/For/Switch, whose while-loop picks up newly queued nodes.
local function onMount(fn: () -> ()): ()
    local node = createComputation(function()
        untrack(fn)
    end, true)
    node.state = STALE
    queueEffect(node)
    scheduleFlush()
end

local function getOwner(): any?
    return Owner
end

local function runWithOwner<T>(owner: any, fn: () -> T): T
    local prevOwner, prevListener = Owner, Listener
    Owner = owner
    Listener = nil
    local ok, result = pcall(fn)
    Owner, Listener = prevOwner, prevListener
    if not ok then
        error(result)
    end
    return result
end

local function setScheduler(fn: ((() -> ())) -> ()): ()
    Scheduler = fn
end

local function flushSync(): ()
    flushEffects()
end

Sx.debug = {
    setOnChange = function(fn: ((any) -> ())?)
        DebugHooks.onChange = fn
    end,
    list = function(): { { kind: string, node: any } }
        local out: { { kind: string, node: any } } = {}
        for _, entry in ipairs(LiveNodes) do
            if entry and (entry.node.disposed == nil or not entry.node.disposed) then
                out[#out + 1] = { kind = entry.kind, node = entry.node }
            end
        end
        return out
    end,
    -- devtools: name to tag the next component root (set by the renderer before a mount).
    setPendingRootName = function(name: string?)
        PendingRootName = name
    end,
    -- devtools: register/read a readable name for a styled component function. Uses a
    -- weak-keyed table because Lua-CSharp functions can't hold properties (unlike Luau).
    setStyledName = function(fn: any, name: string)
        StyledNames[fn] = name
    end,
    styledName = function(fn: any): string?
        return StyledNames[fn]
    end,
    -- devtools: walk the reactive owner chain up to the nearest component root and return
    -- the component that owns this signal/memo (or nil for module-level / unnamed scopes).
    ownerName = function(node: any): string?
        local cur = node
        while cur do
            if cur.__devName ~= nil then
                return cur.__devName
            end
            cur = cur.owner
        end
        return nil
    end,
}

Sx.createSignal = createSignal
Sx.createMemo = createMemo
Sx.createEffect = createEffect
Sx.createRoot = createRoot
Sx.batch = batch
Sx.untrack = untrack
Sx.onCleanup = onCleanup
Sx.onMount = onMount
Sx.getOwner = getOwner
Sx.runWithOwner = runWithOwner
Sx.setScheduler = setScheduler
Sx.flushSync = flushSync

return Sx
