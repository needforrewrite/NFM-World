-- Sx timetrial-HUD repro — the fine-grained counterpart to hud_render.luau.
--
-- Same 17-host-node HUD tree (Speed / PowerDamageBars / Splits / Position), but built
-- with Sx: signals feed dynamic text children, and a couple of bar widths are reactive
-- style props. The tree is mounted ONCE; per-frame `race:hudState` pushes only write
-- signals, so each flush issues a handful of leaf host ops (commitTextUpdate/setProperty)
-- instead of a full-tree reconcile.
--
-- Globals injected by the host: Sx, UiLib, __bench_push(evt, payload), __bench_flush(),
-- __bench_reset_stats().
--
-- Scheduling mirrors the game: Sx.setScheduler(UiLib.defer) coalesces all signal writes
-- in a frame into ONE end-of-frame effect pass (GameThreadContext.Post ->
-- ExecutePendingTasks via __bench_flush).
--
-- Contract: chunk returns run(frames) -> os.clock() CPU delta (seconds). Host op
-- counters come from LuaUiHostStats (read by Program.cs).

local Sx = Sx
local x = Sx.x
local createSignal = Sx.createSignal

-- module-constant styles (stable references — never re-set after mount)
local STYLE_ROOT   = { width = "100%", height = "100%", position = "relative" }
local STYLE_PDB    = { position = "absolute", top = 16, right = 16, width = 200, display = "flex", flexDirection = "column", gap = 8 }
local STYLE_CARD   = { display = "flex", flexDirection = "column", gap = 4 }
local STYLE_LABEL  = { fontSize = 11, color = "rgba(255,255,255,0.5)", marginBottom = 4 }
local STYLE_BAR    = { height = 10 }
local STYLE_BOTTOM = { position = "absolute", bottom = 24, left = 0, right = 0, display = "flex", flexDirection = "column", alignItems = "center" }
local STYLE_BIG    = { fontSize = 56, fontStyle = "bold" }
local STYLE_SMALL  = { fontSize = 14, color = "rgba(255,255,255,0.5)" }

local function run(frames)
    frames = frames or 100

    local speed, setSpeed = createSignal(0)
    local power, setPower = createSignal(1)
    local damage, setDamage = createSignal(0)
    local lapDiff, setLapDiff = createSignal(0)
    local lastLap, setLastLap = createSignal(0)
    local position, setPosition = createSignal(1)
    local total, setTotal = createSignal(1)

    -- Captured and unsubscribed at the end of run() -- the benchmark harness (BenchmarkHost
    -- .RunScriptDetached) calls run() repeatedly on the same LuaState for its best-of-N loop,
    -- and UiLib.onEvent listeners are never cleaned up on their own the way GUIDE.md's
    -- "unsubscribe on unmount" guidance describes. Without this, every extra run() call left
    -- a stale listener from the PREVIOUS run still live, so __bench_push kept invoking all of
    -- them -- each correctly doing its own 2 setProp + 2 commitText per frame, but the
    -- aggregate count (and wall time) grew by one listener's worth on every repeated call.
    local unsubHudState = UiLib.onEvent("race:hudState", function(data)
        if data ~= nil then
            setSpeed(data.speed or 0)
            setPower(data.power or 1)
            setDamage(data.damage or 0)
            setLapDiff(data.lapDiffMs or 0)
            setLastLap(data.lastLapDiffMs or 0)
            setPosition(data.position or 1)
            setTotal(data.totalRacers or 1)
        end
    end)

    -- defer scheduling is auto-wired by the framework (Sx.setScheduler(UiLib.defer) via
    -- Host.defer at dom.luau load) — no explicit call needed here.

    local speedKmh = function()
        return tostring(math.floor(speed() * 1.4 * 21.0 * 60.0 * 60.0 / 100000.0 + 0.5))
    end

    Sx.render(x("view") { style = STYLE_ROOT,
        -- PowerDamageBars: reactive bar widths (leaf setProperty per change)
        x("view") { style = STYLE_PDB,
            x("view") { style = STYLE_CARD,
                x("text") { style = STYLE_LABEL, "Power" },
                x("view") { style = function()
                    local w = ("%d%%"):format(math.floor(power() * 100))
                    return { height = 10, width = w }
                end },
            },
            x("view") { style = STYLE_CARD,
                x("text") { style = STYLE_LABEL, "Damage" },
                x("view") { style = function()
                    local w = ("%d%%"):format(math.floor(damage() * 100))
                    return { height = 10, width = w }
                end },
            },
        },
        -- Splits
        x("view") { style = STYLE_BOTTOM,
            x("text") { style = STYLE_BIG, function()
                return ("%d"):format(lapDiff())
            end },
            x("text") { style = STYLE_SMALL, function()
                return ("%d"):format(lastLap())
            end },
        },
        -- Speed
        x("view") { style = STYLE_BOTTOM,
            x("text") { style = STYLE_BIG, speedKmh },
            x("text") { style = STYLE_SMALL, "KM/H" },
        },
        -- Position
        x("view") { style = STYLE_BOTTOM,
            x("text") { style = STYLE_BIG, function()
                return ("%d/%d"):format(position(), total())
            end },
            x("text") { style = STYLE_SMALL, "POSITION" },
        },
    })

    -- Warm-up: a few frames so the deferred event subscriptions + initial mount settle.
    for i = 1, 3 do
        __bench_push("race:hudState", { speed = 50, power = 0.8, damage = 0.1, lapDiffMs = 100, lastLapDiffMs = 50, position = 1, totalRacers = 1 })
        __bench_flush()
    end

    __bench_reset_stats()
    local t0 = os.clock()
    for i = 1, frames do
        -- Fresh payload every frame (a freshly-built HudStateData); speed/power/damage/
        -- lapDiff move, so exactly those leaves update per frame.
        __bench_push("race:hudState", {
            speed = i,
            power = 0.8 + (i % 5) * 0.01,
            damage = 0.1 + (i % 7) * 0.005,
            lapDiffMs = i,
            lastLapDiffMs = 50,
            position = 1,
            totalRacers = 1,
        })
        __bench_flush()
    end
    local t1 = os.clock()
    unsubHudState()
    return t1 - t0
end

return run
