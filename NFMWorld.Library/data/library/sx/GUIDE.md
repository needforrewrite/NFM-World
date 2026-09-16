# Sx UI framework — a beginner's guide

Sx is the fine-grained reactive UI framework used by the NFM World Lua UI. If you've never
used React, SolidJS, "signals", or web dev in general, this guide is for you. It builds up
from zero and explains *why* things are the way they are, not just what to type.

If you already know SolidJS/dom-expressions, skim the **Quick reference** at the end and the
**Gotchas** section; the rest will be familiar.

---

## 1. The problem this framework solves

Older UI code in this project (the "preact-luau" engine) worked like this: every time *any*
piece of data changed, it re-ran the whole screen's code and diffed the old vs new result to
figure out what to redraw. That works, but re-running and diffing an entire tree **every
frame** is expensive — and in a racing game the HUD changes every frame (speed, lap times,
damage bars).

Sx flips the model: instead of "recompute everything and diff", it says

> **Only recompute the tiny piece that actually depends on the value that changed.**

So a per-frame speed update ends up touching *one* number on *one* text node, not a whole
tree. That's why this framework exists.

---

## 2. The mental model: boxes with wires

Think of a **signal** as a box that holds a value. The box has two handles:

```lua
local speed, setSpeed = Sx.createSignal(0)
--   ↑ read handle       ↑ write handle
```

- **Read** it by calling the first handle: `speed()` → returns `0`.
- **Write** it by calling the second handle: `setSpeed(50)` → puts `50` in the box.

Now the interesting part: **wires**. When your code *reads* a signal while it is inside a
reactive scope (more on that below), Sx lays a wire from the box to that scope. When
something *writes* the box, Sx yanks the wire and the scope re-runs — just that scope, not
everything.

That's the entire framework, honestly. Everything else is convenience built on top of this
one idea: **reads subscribe, writes notify.**

---

## 3. Reactive scopes: `createEffect`

A reactive scope is a chunk of code that automatically re-runs when any signal it reads
changes:

```lua
Sx.createEffect(function()
    print("speed is now", speed())
end)
```

- The first time it runs, it reads `speed()`, so it subscribes to `speed`.
- Every time `setSpeed(x)` is called with a different value, this exact function runs again.
- If the effect reads two signals, it re-runs when *either* changes.
- If it reads no signals, it runs exactly once and never again.

That's it. `createEffect` is your "when this changes, do that" tool.

---

## 4. Components run ONCE — this is the big mental shift

In the old framework, a "component" was a function that got **re-run** every time its data
changed. In Sx, **a component function runs exactly once**. It describes the screen, and the
dynamic parts of the screen are themselves reactive scopes.

Compare:

```lua
-- ❌ Old mindset: the function re-runs when the value changes.
local function Speed()
    local s = speed()          -- read once, baked in forever
    return x(Sx.Text) { ("%d"):format(s) }
end

-- ✅ Sx mindset: the function runs once; the changing part is a function.
local function Speed()
    return x(Sx.Text) { function()
        return ("%d"):format(speed())
    end }
end
```

In the second version, `Speed()` builds the text node once, and the inner `function()` is
the reactive scope that re-runs when `speed` changes. Sx updates just that text.

**Rule of thumb:** if you want a value on screen to react to a signal, wrap the expression
in a function. If it's a static label, just write it.

---

## 5. Building UI: the `x` helper

There's no HTML. You build the UI with a function called `x` (short for "element"). It takes
a tag name, then a table with the element's properties and children:

```lua
x(Sx.View) {                         -- a container
    style = { flexDirection = "column", gap = 8 },
    x(Sx.Text) { "Hello" },          -- a text element
    x(Sx.View) {                     -- nested container
        x(Sx.Text) { "World" },
    },
}
```

Available host elements:

| Tag | What it is |
|---|---|
| `Sx.View` | A box/container. Use it for layout (flex, spacing, backgrounds). |
| `Sx.Text` | A piece of text. Its children are the text content. |
| `Sx.Image` | An image (`src`, `scale`). |
| `Sx.TextInput` | A text entry box (`value`, `placeholder`, `onchange`, `onsubmit`). |

**How the table is read:**

- **Named entries** (string keys like `style = ...`) are *properties*.
- **Positional entries** (just values in order) are *children*.

```lua
x(Sx.View) {
    style = { padding = 8 },        -- a property (named)
    x(Sx.Text) { "child one" },      -- a child (positional)
    x(Sx.Text) { "child two" },      -- another child
}
```

You can also define your own reusable "components" — just a function that returns an `x(...)`
tree:

```lua
local function SpeedReadout()
    return x(Sx.View) {
        x(Sx.Text) { function() return ("%d"):format(speed()) end },
        x(Sx.Text) { "KM/H" },
    }
end

-- use it:
x(Sx.View) { x(SpeedReadout) {} }
```

---

## 6. Text that updates

To make text react to a signal, pass a **function** as the text child:

```lua
x(Sx.Text) { function()
    return ("%d"):format(speed())
end }
```

- The function is a reactive scope. It re-runs when `speed` changes.
- Sx updates the existing text node in place — one tiny change, nothing else redraws.

This is the single most important pattern in the framework.

---

## 7. Lists: `For` and `Index`

To render a list, you use `For`. Its `each` is a function returning the array, and its child
is a function that gets each item:

```lua
local items = { { name = "Skyline" }, { name = "Silvia" } }

x(Sx.View) {
    x(Sx.For) {
        each = function() return items end,
        function(item, index)
            local it = item()
            return x(Sx.Text) { it.name }
        end,
    },
}
```

**Read this carefully:**

- `each` is a function returning the array (so the list re-renders if the array signal changes).
- The child function receives `item` — but `item` is itself a *getter*. Call `item()` to get
  the actual row data. (In Sx, list rows are kept separate so one row can update without
  rebuilding the others.)
- `For` keys rows by the item's identity, so if your list changes, rows that didn't change
  are reused, not rebuilt.

`Index` is the same but matches rows by *position* instead of by identity. Use `For` when
your rows have stable identities (e.g. car objects); use `Index` when you only care about
position.

> If a row's content needs to react to a signal, use the accessor pattern inside the row,
> just like with text: `x(Sx.Text) { function() return item().name end }`.

---

## 8. Conditionals: `Show`, `Switch`/`Match`

### Show (show this, or show that)

```lua
x(Sx.Show) {
    when = function() return isLoggedIn() end,
    fallback = x(Sx.Text) { "Sign in" },
    x(Sx.Text) { "Welcome back" },
}
```

- When `when()` is true, the children mount.
- When it's false, the `fallback` mounts instead (optional).
- The children/fallback mount and unmount automatically as the condition flips.

### Switch / Match (pick the first true case)

```lua
x(Sx.Switch) {
    x(Sx.Match) { when = function() return route() == "menu" end, x(MainMenu) {} },
    x(Sx.Match) { when = function() return route() == "garage" end, x(Garage) {} },
    x(Sx.Match) { when = function() return true end, x(MainMenu) {} }, -- fallback
}
```

`Switch` looks at each `Match` in order and mounts the children of the **first** one whose
`when()` is true. The trailing `when = true` is the "default" case.

### Fragment (group without a wrapper)

If a component needs to return *several* top-level children with no wrapping box, use
`Fragment`:

```lua
return x(Sx.Fragment) {
    x(Sx.Text) { "first" },
    x(Sx.Text) { "second" },
}
```

---

## 9. Events: the `on` prefix

Anything that starts with `on` is an **event handler** (a function called when something
happens). They're wired up once and stay put:

```lua
x(Sx.View) {
    onmousedown = function(evt)
        UiLib.call("navigate", { page = "garage" })
    end,
    x(Sx.Text) { "Garage" },
}
```

Available events: `onmousedown`, `onmouseup`, `onmousedrag`, `onmousescroll`, `onmousemove`,
`onmouseenter`, `onmouseleave`, `onkeytype`, `onkeydown`, `onkeyup`, `onfocus`, `onblur`,
`onanimationframebegan`, plus `onsubmit`/`onchange` on text inputs.

> **Gotcha:** an `on`-prefixed property is a handler. Any **other** property whose value is a
> function is treated as a *reactive value* (section 6 applies). So if you want a plain
> callback that isn't an event, name it with `on` anyway (e.g. `onClose`).

---

## 10. Styles: `styled`

Inline styles are just tables:

```lua
x(Sx.View) { style = { width = "100%", height = "100%", flexDirection = "column" } }
```

For reusable styled components, use `styled`:

```lua
local MenuItem = styled(Sx.View) {
    padding = 14,
    backgroundColor = "rgba(60,60,60,0.12)",
    borderRadius = 8,
    hover = {
        backgroundColor = "rgba(79,195,247,0.24)",
    },
}

-- use it:
x(MenuItem) {
    onmousedown = function() print("clicked") end,
    x(Sx.Text) { "Label" },
}
```

- `styled(tag)(baseStyle)` returns a component you use with `x(...)`.
- A `hover` block is applied automatically while the cursor is over it (and Sx only re-sets
  that one node's style on hover — it doesn't redraw anything else).
- Caller styles can still override via a `style` prop if you pass one through.

---

## 11. Derived values: `createMemo`

A memo is a cached, derived value that recomputes only when the signals it reads change:

```lua
local speedKmh = Sx.createMemo(function()
    return math.floor(speed() * 1.4 * 21.0 * 60.0 * 60.0 / 100000.0 + 0.5)
end)

x(Sx.Text) { function() return ("%d"):format(speedKmh()) end }
```

- Like a signal, you read it with `speedKmh()`.
- Unlike `createEffect`, a memo **doesn't run eagerly** — it only computes when something
  reads it, and only re-computes when its inputs changed.
- Use memos to factor out repeated calculations or derived state so you don't recompute the
  same thing in five places.

**Don't wrap a single-consumer prop in a memo.** `style={Sx.createMemo(function() ... end)}`
buys nothing over `style={function() ... end}`. A function-valued prop already gets its own
effect in the renderer (section 9), and that effect is itself a computation that re-runs only
when a signal it read changes — so both forms run the body exactly as often. The memo just
adds a second node between the signal and the effect. It pays off when *several* consumers
read the same derived value, or when an expensive computation is read from more than one
place. And it does not make the renderer's reference-compare skip a `setProperty`: a body
that returns a fresh `{ ... }` produces a new table on every recompute, memo or not. Only a
body that can hand back an *unchanged* table (e.g. picking between constant style tables)
gets that skip.

---

## 12. Typing reactive values: `Signalish`, `Accessor`, and `Sx.read`

Sx ships three type helpers for describing values that may or may not be reactive. They
show up in `--!strict` Lua whenever you write a reusable component that accepts either a
plain value or a reactive getter.

### `Sx.Signalish<T>`

`Signalish<T>` is `T | (() -> T)` — "a `T`, or a function that returns a `T`." Use it for a
prop that callers may pass either as a static value **or** as a reactive accessor:

```lua
local function SpeedBadge(props: { value: Sx.Signalish<number> })
    return x(Sx.Text) { function() return ("%d"):format(props.value()) end }
end
```

Because the prop is a union, Sx can't know whether `props.value` is a function or a plain
number — that's exactly what `Sx.read` is for (below). A `Signalish` prop is also how the
built-in host props work (`style`, `value`, `src`, ... are all `Signalish`), which is why
you can pass either a table or a function to `style = ...`.

### `Sx.Accessor<T>`

`Accessor<T>` is just `() -> T` — the read handle of a signal. Use it (instead of
`Signalish`) when you want to **require** callers to hand you a reactive getter, so you
never have to unwrap a union:

```lua
local function SettingsPage(props: { config: Sx.Accessor<SettingsSnapshot> })
    -- props.config is *always* a function; no union to unwrap.
    return x(Sx.Text) { function() return props.config().name end }
end
```

A signal's read handle *is* an `Accessor`: `local v, setV = Sx.createSignal(0)` gives you
`v: Sx.Accessor<number>`.

### `Sx.read(v)`

`Sx.read(v)` takes a `Signalish<T>` and returns the plain `T` — if `v` is a function it
calls it, otherwise it returns `v` as-is. It's the safe way to unwrap a `Signalish` prop
*inside a reactive scope* so the scope subscribes to the underlying signal:

```lua
local function StatBar(props: { value: Sx.Signalish<number>? })
    return x(Sx.View) {
        x(Sx.Text) { function()
            local v = Sx.read(props.value) or 0   -- subscribes if value is a getter
            return ("%.0f"):format(clamp01(v) * 100)
        end },
    }
end
```

Read it inside an effect, memo, or function-child — **never** at the top of a component,
which would read it once and bake it in (the "component runs once" trap from section 4).
`Sx.read` also sidesteps the `and/or` falsy trap from the gotchas: it only special-cases
function-ness, not the value, so a getter that returns `false`/`nil` still resolves
correctly.

**Return a stable reference when you can.** The renderer's per-prop effect compares the new
value to the last one and skips the host call when they're identical, so the win comes from
handing back the *same table*, not from memoizing. `CarCard` in `routes/garage.luaux` shows
the pattern: its style accessor picks between two module-level constant tables, so a card
whose selection didn't change returns the identical table and the style prop skips its
`setProperty`. An accessor that builds a fresh `{ ... }` each run can't get that skip.

---

## 13. Typing lists: `For<<T>>` and `Index<<T>>`

`For` and `Index` are *generic* components — you tell Luau what the row type is by writing
`x(Sx.For<<T>>)`. This types both the `each`/`of` list and the `item` getter your row
function receives:

```lua
type CarStatsData = { name: string, topSpeed: number }

x(Sx.For<<CarStatsData>>) {
    each = function() return collections[0].cars end,  -- {CarStatsData}
    function(item: () -> CarStatsData, index: number)
        local car = item()                              -- item() is CarStatsData
        return x(CarCard) { name = car.name, topSpeed = car.topSpeed }
    end,
}
```

- The type argument `<<T>>` is the **row** type. `each` must then be `{T}` (or a
  `() -> {T}`), and your row function's first parameter is `item: () -> T` — a getter you
  call with `item()` to get the row, never the row itself.
- `Index<<T>>` is identical but uses `of` instead of `each`, and keys rows by position
  instead of identity:

  ```lua
  x(Sx.Index<<string>>) {
      of = function() return splitLines(text) end,
      function(line: () -> string, i: number)
          return x(Sx.Text) { line() }
      end,
  }
  ```

- If you leave the type argument off, Luau falls back to `T = any` and you lose
  autocomplete and type-checking on `item()` — always write the `<<T>>`.
- Annotate the row callback parameters (`item: () -> T, index: number`) explicitly. It
  makes intent clear and lets Luau catch you if you treat `item` as a plain value.

---

## 14. Lifecycle: `onMount`, `onCleanup`, `createRoot`

These are for when a component or effect starts and stops.

```lua
local function Page()
    -- Subscribe to a game event when mounted; unsubscribe when unmounted.
    Sx.createEffect(function()
        local unsub = UiLib.onEvent("main-menu:account", function(data)
            setAccount(data)
        end)
        Sx.onCleanup(unsub)   -- runs when this scope is torn down
    end)
    ...
end
```

- `Sx.onCleanup(fn)` — registers a teardown for the current reactive scope (runs on unmount
  or when the effect is disposed).
- `Sx.onMount(fn)` — runs `fn` once when the component mounts.
- `Sx.createRoot(fn)` — creates a disposable "root" scope; the dispose function it hands you
  tears down everything inside. You rarely need this directly (components get their own root
  automatically), but it's how disposal works under the hood.

---

## 15. Grouping writes: `batch`

If you write several signals at once, wrap them in `batch` so Sx only does **one** update
pass instead of one per write:

```lua
Sx.batch(function()
    setPosition(2)
    setTotal(6)
    setStateText("Lap 2")
end)
```

On its own this is a small optimization, but it's good practice when updating multiple values
that belong together.

---

## 16. Talking to the game: `UiLib.onEvent` and `UiLib.call`

The Lua UI talks to the C# game through two functions:

- **C# → Lua (incoming data):** `UiLib.onEvent(eventName, handler)` — register a handler
  that's called when the game pushes data. It returns an unregister function (call it on
  cleanup, see section 14).

  ```lua
  UiLib.onEvent("race:hudState", function(data)
      setSpeed(data.speed)
      setPower(data.power)
      setDamage(data.damage)
  end)
  ```

- **Lua → C# (outgoing requests):** `UiLib.call(methodName, payloadTable)` — ask the game to
  do something.

  ```lua
  UiLib.call("navigate", { page = "garage" })
  ```

Event names follow `"phase:thing"` (e.g. `main-menu:account`, `race:hudState`,
`garage:collections`). Your phase's bridge tells you which events exist and what payloads they
carry.

---

## 17. Putting it together: a router and a page

The app is entered through `data/uis/router.luau`, which subscribes to navigation and renders
the current page:

```lua
local Sx = require("../library/sx/index")
local x = Sx.x
local MainMenu = require("./routes/mainmenu")

local function Router()
    local route, setRoute = Sx.createSignal("main-menu")

    Sx.createEffect(function()
        local unsub = UiLib.onEvent("nfmw:navigate", function(page)
            setRoute(page)
        end)
        Sx.onCleanup(unsub)
    end)

    return x(Sx.Switch) {
        x(Sx.Match) { when = function() return route() == "main-menu" end, x(MainMenu) {} },
        x(Sx.Match) { when = function() return true end, x(MainMenu) {} }, -- fallback
    }
end

Sx.render(x(Router) {})
```

A page is just a function returning `x(...)`, wired to its phase's events:

```lua
local Sx = require("../../library/sx/index")
local x = Sx.x

local function MainMenu()
    local account, setAccount = Sx.createSignal(nil)

    Sx.createEffect(function()
        local unsub = UiLib.onEvent("main-menu:account", function(data)
            setAccount(data)
        end)
        Sx.onCleanup(unsub)
    end)

    return x(Sx.View) {
        style = { flexDirection = "column", alignItems = "center", justifyContent = "center" },
        x(Sx.Text) {
            style = { fontSize = 48, fontStyle = "bold" },
            "NFM WORLD",
        },
        x(Sx.Show) {
            when = function()
                local a = account()
                return a ~= nil and a.isLoggedIn
            end,
            x(Sx.Text) { function()
                return ("Welcome, %s"):format(account().name)
            end },
        },
    }
end

return MainMenu
```

Read the whole flow: `Router` builds a signal for the page; the effect subscribes to
navigation; the `Switch` mounts the matching page. Each page subscribes to its own data and
builds its tree once, with reactive bits inside functions.

---

## 18. Gotchas (worth reading twice)

1. **Reactive text must be a function.** `x(Sx.Text) { value }` sets it once; to react, use
   `x(Sx.Text) { function() return value() end }`.

2. **Non-`on` function props are reactive.** `onmousedown = fn` is a handler; `color = fn`
   would be treated as a reactive value. Name plain callbacks with `on`.

3. **`For` rows give you getters.** Inside a `For` child, `item` is a getter — call `item()`.

4. **`and/or` is a trap.** `x and f() or y` returns `y` when `f()` returns `false`/`nil`.
   Write explicit `if`s when the value can be falsy.

5. **We don't re-run components.** Components run once. If something isn't updating, you're
   probably reading a signal outside a function instead of inside one.

6. **`%d` wants exact integers.** `("%d%%"):format(0.8 * 100)` throws; use
   `math.floor(0.8 * 100 + 0.5)` first.

7. **Lists rebuild when the array changes.** If you pass a brand-new array to `each` every
   time, `For` rebuilds. Keep the array reference stable unless the list really changed
   (e.g. keep it in a signal/memo, don't allocate a new one in a reactive scope).

8. **Unsubscribe on unmount.** If you `UiLib.onEvent(...)` inside an effect, pass the returned
   unregister to `Sx.onCleanup` so a navigated-away page doesn't keep receiving events.

9. **Always write `<<T>>` on `For`/`Index`.** Without it Luau infers `T = any`, so you lose
   checking on `item()`. Write `x(Sx.For<<Car>>)` and annotate the callback as
   `function(item: () -> Car, index)`. Type signal-able props with `Sx.Signalish<T>` and
   read them with `Sx.read` inside a reactive scope — never at the top of a component.

---

## 19. Glossary (web terms → plain words)

| Term | Plain meaning |
|---|---|
| Signal | A box that holds a value, with a reader and a writer. Reading subscribes; writing notifies. |
| Reactive scope | A chunk of code (an effect, a memo, a `function()` child) that re-runs when a signal it reads changes. |
| Memo | A cached derived value that recomputes lazily when its inputs change. |
| Component | A function that returns an `x(...)` tree. It runs once; dynamic parts are functions. |
| Hyperscript / `x` | The way you write UI in Lua (like HTML, but as function calls). |
| Prop / property | A named entry in an element's table (`style`, `onmousedown`, ...). |
| Child | A positional entry in an element's table (nested elements or text). |
| Mount / unmount | A node appearing in / being removed from the tree. |
| Cleanup / dispose | Code that runs when a scope is torn down (unsubscribe, remove listeners). |
| Batch | Grouping several writes so Sx does one update pass. |
| HUD | Head-Up Display — the in-race overlay (speed, laps, position). |
| Phase / bridge | A game screen (main menu, garage, race) and the C# code that feeds it data. |
| Signalish | A type `T | (() -> T)` for a prop that may be a plain value or a getter. |
| Accessor | The read handle of a signal (`() -> T`). |
| `Sx.read` | Unwraps a `Signalish` to its plain value (calls it if it's a function). |

---

## Quick reference

```lua
local Sx = require("../../library/sx/index")
local x = Sx.x
local styled = Sx.styled

-- state
local value, setValue = Sx.createSignal(0)
local doubled = Sx.createMemo(function() return value() * 2 end)

-- side effects / lifecycle
Sx.createEffect(function() ... end)        -- re-runs when deps change
Sx.onCleanup(fn)                            -- teardown for current scope
Sx.onMount(fn)                              -- run once on mount
Sx.batch(function() ... end)                -- group writes

-- tree
x(Sx.View)  { style = {...}, x(Sx.Text) { "hi" } }
x(MyComponent) { someProp = 1, x(Sx.Text) { "child" } }

-- types (--!strict)
Sx.Signalish<T>   -- T | (() -> T): a prop that's a value OR a getter
Sx.Accessor<T>    -- () -> T: the read handle of a signal (requires a getter)
Sx.read(v)        -- unwrap a Signalish<T> to T inside a reactive scope

-- flow
x(Sx.Show)         { when = fn, fallback = ..., children }
x(Sx.Switch)       { x(Sx.Match) { when = fn, children }, ... }
x(Sx.For<<T>>)     { each = fn, function(item: () -> T, index) return ... end }
x(Sx.Index<<T>>)   { of = fn, function(item: () -> T, index) return ... end }
x(Sx.Fragment)     { child1, child2 }

-- events (once) vs reactive props (functions)
onmousedown = function(evt) ... end
color       = function() return selected() and "#4fc3f7" or "#fff" end

-- game bridge
UiLib.onEvent("phase:event", function(data) ... end)  -- returns unregister
UiLib.call("method", { ... })

-- entry point
Sx.render(x(Router) {})
```
