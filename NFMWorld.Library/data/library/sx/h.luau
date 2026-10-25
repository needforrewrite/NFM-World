-- Sx hyperscript builder. Curried like the existing preact `x`:
--
--     x(Sx.View){ style = s, x(Sx.Text){ "hi" } }
--     x(MyComponent){ name = "x", someSignalFn }
--
-- A call returns a plain descriptor table:
--     { type = <string|component fn|flow marker>, props = {named...}, children = {n...} }
-- where `children` holds the positional (numeric-key) entries — plain values (mounted
-- statically), functions (mounted reactively as accessors), or nested descriptors.

local t = require('./types')

local h = {}

-- devtools: when enabled, `x` records the "short_src:line" of the `x(...){...}` call site
-- onto each descriptor so the devtools tree can show where an element/component was
-- created in the code (instead of the component's declaration site). Off by default —
-- mounting builds descriptors once, but we still avoid the debug.getinfo cost when the
-- devtools are not in use.
local captureCallSites = false

-- Capture the "short_src:line" of the caller of the returned builder — i.e. the
-- `x(...){...}` creation site. Uses debug.traceback because debug.getinfo's currentline
-- is 0 for the DoFile'd entry chunk in this VM, while traceback reports "file:line"
-- reliably for every frame. We walk up the traceback and return the FIRST frame that is
-- NOT inside the Sx library. That is the user's creation site even when flow/effect
-- machinery (For/Show/Switch, signals, styled) sits between the render function and the
-- `x` call — e.g. a `<For>` row function lives in the user's module, not in dom.luau.
local function captureCallSite(): string
    local ok, tb = pcall(debug.traceback, "", 2)
    if not ok or type(tb) ~= "string" then
        return ""
    end
    for line in tb:gmatch("[^\n]+") do
        local path, ln = line:match("^%s*([^:]+):(%d+):")
        if path and ln and not path:find("sx/", 1, true) then
            return path .. ":" .. ln
        end
    end
    return ""
end

local function x<P>(vtype: t.Component<P>): ((Propify<P, any>) -> t.NodeDescriptor)
    -- Capture the creation site in the OUTER builder, not the returned function:
    -- `x(ItemBtn){...}` is `x(ItemBtn)({...})`, so the `x(ItemBtn)` call is never in a
    -- tail position and its caller (the component's render fn) is still on the stack.
    -- Capturing inside the returned builder would lose that frame for a `<For>` row's
    -- `return x(ItemBtn){...}` (tail call elimination), resolving the site to the mount
    -- line instead of the user's row fn.
    local callSite = ""
    if captureCallSites then
        callSite = captureCallSite()
    end
    return function(props: P?)
        props = props or {}
        local named: { [string]: any } = {}
        local children: { any } = {}
        for k: string | number, v: any in pairs(props) do
            if type(k) == "number" then
                children[#children + 1] = v
            else
                named[k] = v
            end
        end
        local desc = {
            type = vtype,
            props = named,
            children = children,
        }
        desc.callSite = callSite
        return desc
    end
end

export type Signalish<T> = T | (() -> T)

export type Styles = {
	direction: "ltr" | "rtl" | "inherit" | nil,
	flexDirection: "row" | "row-reverse" | "column" | "column-reverse" | nil,
	justifyContent: "flex-start" | "center" | "flex-end" | "space-between" | "space-around" | "space-evenly" | nil,
	-- YGAlign exposes more items than this but they don't seem to be valid.
	alignItems: "flex-start" | "center" | "flex-end" | "stretch" | "baseline" | nil,
	-- YGAlign exposes more items than this but they don't seem to be valid.
	alignSelf: "flex-start" | "center" | "flex-end" | "stretch" | "baseline" | nil,
	-- YGAlign exposes more items than this but they don't seem to be valid.
	alignContent: "flex-start"
		| "center"
		| "flex-end"
		| "stretch"
		| "space-between"
		| "space-around"
		| "space-evenly"
		| nil,
	position: "static" | "relative" | "absolute" | nil,
	flexWrap: "nowrap" | "wrap" | "wrap-reverse" | nil,
	overflow: "visible" | "hidden" | "scroll" | nil,
	display: "flex" | "none" | "contents" | nil,
	boxSizing: "border-box" | "content-box" | nil,
	visibility: "visible" | "hidden" | "collapse" | nil,
	flex: number | nil,
	flexGrow: number | nil,
	flexShrink: number | nil,
	flexBasis: string | nil,
	--- accepts % and px
	left: string | number | nil,
	--- accepts % and px
	top: string | number | nil,
	--- accepts % and px
	right: string | number | nil,
	--- accepts % and px
	bottom: string | number | nil,
	--- accepts % and px
	marginTop: string | number | "undefined" | "none" | "auto" | nil,
	--- accepts % and px
	marginBottom: string | number | "undefined" | "none" | "auto" | nil,
	--- accepts % and px
	marginLeft: string | number | "undefined" | "none" | "auto" | nil,
	--- accepts % and px
	marginRight: string | number | "undefined" | "none" | "auto" | nil,
	--- accepts % and px
	margin: string | number | "undefined" | "none" | "auto" | nil,
	--- accepts % and px
	paddingTop: string | number | "undefined" | "none" | nil,
	--- accepts % and px
	paddingBottom: string | number | "undefined" | "none" | nil,
	--- accepts % and px
	paddingLeft: string | number | "undefined" | "none" | nil,
	--- accepts % and px
	paddingRight: string | number | "undefined" | "none" | nil,
	--- accepts % and px
	padding: string | number | "undefined" | "none" | nil,
	--- accepts px
	borderTopWidth: string | number | "undefined" | "none" | nil,
	--- accepts px
	borderBottomWidth: string | number | "undefined" | "none" | nil,
	--- accepts px
	borderLeftWidth: string | number | "undefined" | "none" | nil,
	--- accepts px
	borderRightWidth: string | number | "undefined" | "none" | nil,
	--- accepts px
	borderWidth: string | number | "undefined" | "none" | nil,
	--- accepts % and px
	columnGap: string | number | "undefined" | nil,
	--- accepts % and px
	rowGap: string | number | "undefined" | nil,
	--- accepts % and px
	gap: string | number | "undefined" | nil,
	--- accepts % and px
	width: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- accepts % and px
	height: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- accepts % and px
	minWidth: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- accepts % and px
	minHeight: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- accepts % and px
	maxWidth: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- accepts % and px
	maxHeight: string | number | "undefined" | "auto" | "stretch" | "fit-content" | "max-content" | nil,
	--- a/b or number
	aspectRatio: string | number | nil,
	borderColor: string | nil,
	backgroundColor: string | nil,
	--- accepts px
	borderTopLeftRadius: string | number | nil,
	--- accepts px
	borderTopRightRadius: string | number | nil,
	--- accepts px
	borderBottomLeftRadius: string | number | nil,
	--- accepts px
	borderBottomRightRadius: string | number | nil,
	--- accepts px
	borderRadius: string | number | nil,
	pointerEvents: 'auto' | 'none' | nil,
	zIndex: number | nil,
}

-- Luau checks an object literal against each side of `A & B` independently, so
-- a plain `Styles & { textOnlyField: T }` rejects a literal that only sets
-- `textOnlyField` (it isn't a `Styles`) and loses fields entirely when it flows
-- through a further generic (e.g. `Propify` below only forwards a field's raw
-- declared type, so an unflattened intersection carries its two-sided shape
-- into every consumer instead of behaving like one merged table). Flatten it
-- into a single table type up front so every consumer — Propify, WithVariants,
-- a plain `: TextStyles` annotation — sees the same flat shape a literal can
-- actually match.
type function flattenTable(t)
	local result = types.newtable()

	local function collect(ty)
		if ty:is("table") then
			for k, v in ty:properties() do
				result:setproperty(k, v.read)
			end
		elseif ty:is("intersection") then
			for _, part in ty:components() do
				collect(part)
			end
		end
	end

	collect(t)

	return result
end

export type TextStyles = flattenTable<Styles & {
	color: string?,
	strokeColor: string?,
	fontFamily: string?,
	fontSize: string | number | nil,
	fontStyle: 'bold' | 'italic' | 'plain' | 'bold italic' | nil,
	verticalAlign: "top" | "bottom" | "middle" | nil,
	textAlign: "left" | "right" | "center" | nil,
}>

type ViewPropsWithStyle<T> = {
	name: Signalish<string?>,
	ref: Signalish<{ current: Component? }?> | ((ref: Component) -> ())?,
	style: Signalish<T?>,
	onanimationframebegan: Signalish<(() -> ())?>,
	active: Signalish<boolean?>,
	focused: Signalish<boolean?>,
	tabindex: Signalish<integer?>,
	onmousedown: ((event: MouseEvent) -> ())?,
	onmouseup: ((event: MouseEvent) -> ())?,
	onmousedrag: ((event: MouseDragEvent) -> ())?,
	onmousescroll: ((event: MouseWheelEvent) -> ())?,
	onmousemove: ((event: MouseMoveEvent) -> ())?,
	onmouseenter: ((event: MouseMoveEvent) -> ())?,
	onmouseleave: ((event: MouseMoveEvent) -> ())?,
	onkeytype: ((event: KeyboardTypingEvent) -> ())?,
	onkeydown: ((event: KeyboardEvent) -> ())?,
	onkeyup: ((event: KeyboardEvent) -> ())?,
	onfocus: (() -> ())?,
	onblur: (() -> ())?,
	children: Signalish<{any}?>,
}

export type ViewProps = ViewPropsWithStyle<Styles>;

export type ImageProps = ViewPropsWithStyle<Styles> & {
	src: Signalish<string?>,
	scale: Signalish<number?>,
}

export type TextProps = ViewPropsWithStyle<TextStyles>;

export type TextInputStyles = flattenTable<TextStyles & {
	cursorColor: string?,
	selectionColor: string?,
	placeholderColor: string?,
}>

export type TextInputProps = ViewPropsWithStyle<TextInputStyles> & {
	value: Signalish<string?>,
	placeholder: Signalish<string?>,
	onsubmit: ((value: string) -> ())?,
	onchange: ((value: string) -> ())?,
}

-- Converts p to
-- P & { [number]: R.Child }
-- and removes children from P
type function Propify(tbl: typeof(types.newtable()), childType)
    local props = {}

    local function collect(t)
        if t:is("table") then
            for k, v in t:properties() do
                local isChildren = k:is("singleton") and k:value() == "children"

                if not isChildren then
                    -- v is a property descriptor — pull the real Type out of it
                    local ty = v.read or v.write
                    if ty then
                        props[k] = ty  -- k stays as the Type singleton here
                    end
                end
            end
        elseif t:is("intersection") then
            for _, part in t:components() do
                collect(part)
            end
        end
    end

    collect(tbl)

    local tbl1 = types.newtable(props, { index = types.number, readresult = childType })
    tbl1:setproperty(types.singleton("key"), types.any)

    return tbl1
end

local function View(props: ViewProps): t.NodeDescriptor
    return {
        type = "view",
        props = props,
        children = props.children or {},
    }
end

local function Text(props: TextProps): t.NodeDescriptor
    return {
        type = "text",
        props = props,
        children = props.children or {},
    }
end

local function Image(props: ImageProps): t.NodeDescriptor
    return {
        type = "image",
        props = props,
        children = props.children or {},
    }
end

local function TextInput(props: TextInputProps): t.NodeDescriptor
    return {
        type = "textinput",
        props = props,
        children = props.children or {},
    }
end

h.setCaptureCallSites = function(v: boolean)
    captureCallSites = v
end

h.x = x
h.View = View
h.Text = Text
h.Image = Image
h.TextInput = TextInput

return h
