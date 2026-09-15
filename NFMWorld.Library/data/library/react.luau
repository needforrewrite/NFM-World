--- A tiny React / Preact-style UI library for Lua 5.1.
---
---     local React = require("react")
---     local h = React.h
---
---     React.setHostConfig({ ... })          -- plug in a DOM/RmlUI adapter
---     React.render(h(App), container)       -- mount / update
---     React.unmount(container)              -- tear down
---
--- Function components + hooks only. Rendering is synchronous: state updates
--- flush immediately (no scheduler, no time-slicing). See README.md for the
--- host config contract and the list of supported hooks.

local preact = require('./preact-luau/src/index')
local hooks = require('./preact-luau/hooks/src/index')
local compat = require('./preact-luau/compat/src/index')
local debug = require('./preact-luau/debug/src/index')
local coreTypes = require('./preact-luau/types/core.d');
local hooksTypes = require('./preact-luau/types/hooks.d');
local debugTypes = require('./preact-luau/types/debug.d');
local compatTypes = require('./preact-luau/types/compat.d');

export type FunctionComponent<T> = coreTypes.FunctionComponent<T>
export type VNode = coreTypes.VNode
export type Options = coreTypes.Options
export type RefObject<T> = coreTypes.RefObject<T>
export type Ref<T> = coreTypes.Ref<T>
export type Context<T> = coreTypes.Context<T>
export type ComponentChild = coreTypes.ComponentChild
export type ComponentChildren = coreTypes.ComponentChildren

local render = preact.render :: coreTypes.render
local createElement = preact.createElement :: coreTypes.createElement
local h = preact.h :: coreTypes.h
local Fragment = preact.Fragment :: coreTypes.Fragment
local createRef = preact.createRef :: coreTypes.createRef
local cloneElement = preact.cloneElement :: coreTypes.cloneElement
local isValidElement = preact.isValidElement :: coreTypes.isValidElement
local createContext = preact.createContext :: coreTypes.createContext
local createPortal = preact.createPortal :: coreTypes.createPortal
local toChildArray = preact.toChildArray :: coreTypes.toChildArray
local options = preact.options :: coreTypes.options

local useState = hooks.useState :: hooksTypes.useState
local useReducer = hooks.useReducer :: hooksTypes.useReducer
local useEffect = hooks.useEffect :: hooksTypes.useEffect
local useLayoutEffect = hooks.useLayoutEffect :: hooksTypes.useLayoutEffect
local useRef = hooks.useRef :: hooksTypes.useRef
local useImperativeHandle = hooks.useImperativeHandle :: hooksTypes.useImperativeHandle
local useMemo = hooks.useMemo :: hooksTypes.useMemo
local useCallback = hooks.useCallback :: hooksTypes.useCallback
local useContext = hooks.useContext :: hooksTypes.useContext
local useDebugValue = hooks.useDebugValue :: hooksTypes.useDebugValue
local useErrorBoundary = hooks.useErrorBoundary :: hooksTypes.useErrorBoundary
local useId = hooks.useId :: hooksTypes.useId

local memo = compat.memo :: compatTypes.memo

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
}

export type TextStyles = Styles & {
	color: string?,
	stroke: string?,
	fontFamily: string?,
	fontSize: string | number | nil,
	fontStyle: string?,
	verticalAlign: "top" | "bottom" | "middle" | nil,
	textAlign: "left" | "right" | "center" | nil,
}

export type ViewProps = {
	name: string?,
	key: any?,
	ref: { current: Component? }?,
	style: Styles?,
	onanimationframebegan: (() -> ())?,
	active: boolean?,
	focused: boolean?,
	tabindex: integer?,
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
	children: ComponentChildren?,
}

export type ImageProps = ViewProps & {
	src: string?,
	scale: number?,
}

export type TextProps = ViewProps & {
	style: TextStyles?,
}

export type TextInputStyles = TextStyles & {
	cursorColor: string?,
	selectionColor: string?,
	placeholderColor: string?,
}

export type TextInputProps = ViewProps & {
	style: TextInputStyles?,
	value: string?,
	placeholder: string?,
	onsubmit: ((value: string) -> ())?,
	onchange: ((value: string) -> ())?,
}

local View = function(props: ViewProps): VNode
	-- NOTE: do NOT forward varargs to createElement. The preact-luau port invokes
	-- function components as (props, context, self), so a `...` here would capture
	-- `context` and `self` as fake children and REPLACE props.children (dropping the
	-- real children). Children always arrive via props.children (set by `x()`).
	return (createElement :: any)("view", props) :: VNode
end
local Text = function(props: TextProps): VNode
	return (createElement :: any)("text", props) :: VNode
end
local Image = function(props: ImageProps): VNode
	return (createElement :: any)("image", props) :: VNode
end
local TextInput = function(props: TextInputProps): VNode
	return (createElement :: any)("textinput", props) :: VNode
end

-- P & { [number]: R.Child } will misbehave in Luau so we need a type function to augment P with a numeric indexer for children.
type function WithChildren(tbl, childType)
	local props = {}

	local function collect(t)
		if t:is("table") then
			for k, v in t:properties() do
				props[k] = v
			end
		elseif t:is("intersection") then
			for _, part in t:components() do
				collect(part)
			end
		else
			-- error("expected a table or intersection of tables")
			return
		end
	end

	collect(tbl)

	local tbl1 = types.newtable(props, { index = types.number, readresult = childType })

    tbl1:setproperty(types.singleton("key"), types.any)

	return tbl1
end

--- Luau DSL helper function to create a virtual element.
---
--- Use like:
---
---     return x(View) {
---       style = {
---         flexDirection = "row",
---         alignItems = "stretch",
---         justifyContent = "center",
---         width = 100,
---         height = 100,
---       },
---       x(TextInput) {
---         style = {
---           width = 100,
---           height = 20,
---         }
---       }
---     }
local function x<P>(vtype: FunctionComponent<P> | coreTypes.Fragment): (WithChildren<P, ComponentChildren>) -> VNode
	return function(props: WithChildren<P, ComponentChildren>): VNode
		local children = {}
		local props1 = (props :: any) :: { [number]: ComponentChildren } -- type fix
		for i = 1, #props1 do
			children[#children + 1] = props1[i] -- add to children
			props1[i] = nil -- remove from props so it doesn't get passed to the component
		end

		props1.children = children

		return (createElement :: any)(vtype, props) :: VNode
	end
end

return {
	render = render,
	createElement = createElement,
	h = h,
	Fragment = Fragment,
	createRef = createRef,
	cloneElement = cloneElement,
	isValidElement = isValidElement,
	createContext = createContext,
	createPortal = createPortal,
	toChildArray = toChildArray,
	options = options,
	memo = memo,
	useState = useState,
	useReducer = useReducer,
	useEffect = useEffect,
	useLayoutEffect = useLayoutEffect,
	useRef = useRef,
	useImperativeHandle = useImperativeHandle,
	useMemo = useMemo,
	useCallback = useCallback,
	useContext = useContext,
	useDebugValue = useDebugValue,
	useErrorBoundary = useErrorBoundary,
	useId = useId,
	x = x,
	View = View,
	Text = Text,
	Image = Image,
	TextInput = TextInput,
}
