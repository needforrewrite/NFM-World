--!strict
-- Shared glassmorphism primitives (Sx port of preact glasscard.luau).
--
-- GlassCard: card with a 3px colored top accent bar. `color` and `style` may be plain
-- values or accessors (fine-grained); a reactive color/style is applied via a single
-- setProperty when it changes. StatBar / CenterText are pure (static) components.

local Sx = require("../../library/sx/index")
local x = Sx.x

local function clamp01(v)
	if v < 0 then return 0 end
	if v > 1 then return 1 end
	return v
end

local function copyStyle(dst, src)
	for k, v in pairs(src) do
		dst[k] = v
	end
end

-- ── GlassCard ────────────────────────────────────────────────────

local function GlassCard(props: { color: Sx.Signalish<string>?, children: { any }? } & Sx.ViewProps)
	local wrapperStyle: Sx.Styles = {
		backgroundColor = "rgba(0,0,0,0.2)",
		borderRadius = 0,
		borderWidth = 1,
		borderColor = "transparent",
		paddingTop = 16,
		paddingBottom = 16,
		paddingLeft = 20,
		paddingRight = 20,
		position = "relative",
		overflow = "hidden",
	}
	
	local computedStyle = function(): Sx.Styles
		local s: Sx.Styles | {[string]: string} = {}
		for k, v in pairs(wrapperStyle :: any) do
			s[k] = v
		end
		if props.style then
			local st = Sx.read(props.style)
			if st then
				for k, v in pairs(st :: any) do
					s[k] = v
				end
			end
		end
		local c = Sx.read(props.color) or "transparent"
		s.borderColor = c
		return s :: Sx.Styles
	end

	-- Forward every unconsumed prop (name, onmousedown, tabindex, ...) to the wrapper
	-- View; only color/style/children are handled here.
	local args: any = {}
	for k, v in pairs(props :: any) do
		if k ~= "color" and k ~= "style" and k ~= "children" and k ~= "key" then
			args[k] = v
		end
	end
	args.style = computedStyle

	for _, c in ipairs(props.children or {}) do
		args[#args + 1] = c
	end

	return x(Sx.View)(args)
end

-- ── StatBar ──────────────────────────────────────────────────────
-- Labeled progress bar (value is 0..1). `value` may be a plain number or an accessor;
-- a reactive value updates only this bar's width text + fill width.

local function StatBar(props: { label: string, value: Sx.Signalish<number>?, color: string?, height: number? })
	local color = props.color or "#4fc3f7"
	local height = props.height or 8
	local label = props.label or ""

	return x(Sx.View)({ style = { marginBottom = 8, flexDirection = "column", width = "100%" },
		x(Sx.View)({
			style = {
				display = "flex",
				justifyContent = "space-between",
				marginBottom = 4,
			},
			x(Sx.Text)({ style = { fontSize = 12, color = "rgba(255,255,255,0.7)", strokeColor = "rgba(100,100,100,0.4)", flexShrink = 0 },
				label,
			}),
			x(Sx.Text)({ style = { fontSize = 12, color = "rgba(255,255,255,0.7)", strokeColor = "rgba(100,100,100,0.4)", flexShrink = 0 },
				function()
					local v = clamp01(Sx.read(props.value) or 0)
					return ("%d%%"):format(math.floor(v * 100 + 0.5))
				end,
			}),
		}),
		x(Sx.View)({
			style = {
				width = "100%",
				height = height,
				backgroundColor = "rgba(255,255,255,0.1)",
				borderRadius = height / 2,
				overflow = "hidden",
			},
			x(Sx.View)({
				style = function(): Sx.Styles
					local v = clamp01(Sx.read(props.value) or 0)
					return {
						width = ("%d%%"):format(math.floor(v * 100 + 0.5)),
						height = "100%",
						backgroundColor = color,
						borderRadius = height / 2,
					}
				end,
			}),
		}),
	})
end

-- ── CenterText ───────────────────────────────────────────────────
-- Large center-screen text overlay. Splits on "\n". `text` may be a plain string or an
-- accessor; lines are rendered through a keyed For so a changing text updates in place.

local function splitLines(s: string): { string }
	local t = {}
	local start = 1
	while true do
		local i = string.find(s, "\n", start, true)
		if i == nil then
			t[#t + 1] = s:sub(start)
			break
		end
		t[#t + 1] = s:sub(start, i - 1)
		start = i + 1
	end
	return t
end

local function CenterText(props: { text: Sx.Signalish<string>?, size: number?, color: string? })
	local size = props.size or 64
	local color = props.color or "rgba(255,255,255,0.9)"

	return x(Sx.View)({
		style = {
			position = "absolute",
			top = 0,
			left = 0,
			right = 0,
			bottom = 0,
			display = "flex",
			flexDirection = "column",
			alignItems = "center",
			justifyContent = "center",
		},
		x(Sx.For<<string>>)({
			each = function() return splitLines(Sx.read(props.text) or "") end,
			function(item, index)
				return x(Sx.Text)({ style = { fontSize = size, color = color, fontStyle = "bold", textAlign = "center" },
					function() return item() end,
				})
			end,
		}),
	})
end

return {
	GlassCard = GlassCard,
	StatBar = StatBar,
	CenterText = CenterText,
}
