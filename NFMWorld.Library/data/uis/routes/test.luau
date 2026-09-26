--!strict
-- Test page (Sx port of preact test.luau), ported from
-- nfm-world/data/html/src/pages/Test.tsx.

local Sx = require("../../library/sx/index")
local styled = Sx.styled
local x = Sx.x

local Button = require("../../library/ui/button")

local Checkbox = require("../components/checkbox")
local Radio = require("../components/radio")
local Slider = require("../components/slider")
local Dropdown = require("../components/dropdown")
local TextField = require("../components/textfield")
local ToggleSwitch = require("../components/toggleswitch")

local Root = styled(Sx.View) {
	width = "100%",
	height = "100%",
	display = "flex",
	flexDirection = "column",
	alignItems = "center",
	justifyContent = "center",
	gap = 16,
}

local Label = styled(Sx.Text) {
	fontSize = 14,
	color = "rgba(255,255,255,0.5)",
}

local Counter = styled(Sx.Text) {
	fontSize = 64,
	fontStyle = "bold",
	color = "#4fc3f7",
}

local SectionTitle = styled(Sx.Text) {
	fontSize = 18,
	fontStyle = "bold",
	color = "#4fc3f7",
	marginBottom = 4,
}

local DemoRow = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	alignItems = "center",
	justifyContent = "space-between",
	gap = 16,
	width = "100%",
}

local DemoLabel = styled(Sx.Text) {
	fontSize = 13,
	color = "rgba(255,255,255,0.5)",
	minWidth = 90,
}

local DemoValue = styled(Sx.Text) {
	fontSize = 12,
	color = "rgba(79,195,247,0.8)",
	minWidth = 80,
	textAlign = "right",
}

local function TestPage()
	local counter, setCounter = Sx.createSignal(0)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("test:counter", function(data)
			if data ~= nil then
				setCounter(data.value or 0)
			else
				setCounter(0)
			end
		end)
		Sx.onCleanup(unsub)
	end)

	-- Component demo state.
	local demoChecked, setDemoChecked = Sx.createSignal(false)
	local demoRadio, setDemoRadio = Sx.createSignal(1)
	local demoSlider, setDemoSlider = Sx.createSignal(50)
	local demoDropdown, setDemoDropdown = Sx.createSignal(0)
	local demoText, setDemoText = Sx.createSignal("")
	local demoToggle, setDemoToggle = Sx.createSignal(false)

	return x(Root)({
		x(Label)({ "CEF + Preact Test" }),
		x(Counter)({ function() return ("%d"):format(counter()) end }),
		x(Button)({
			variant = "primary",
			label = "Increment (JS → C#)",
			onPress = function() UiLib.call("increment", {}) end,
		}),
		x(Button)({
			label = "← Back to Menu",
			onPress = function() UiLib.call("back", {}) end,
		}),
		x(Sx.View)({
			style = {
				display = "flex",
				flexDirection = "column",
				gap = 14,
				width = "100%",
				maxWidth = 560,
				paddingTop = 20,
				paddingBottom = 20,
				paddingLeft = 28,
				paddingRight = 28,
				backgroundColor = "rgba(255,255,255,0.04)",
				borderWidth = 1,
				borderColor = "rgba(255,255,255,0.1)",
				borderRadius = 10,
			},
			x(SectionTitle)({ "Component Demos" }),
			x(DemoRow)({
				x(DemoLabel)({ "Checkbox" }),
				x(Checkbox)({
					checked = demoChecked,
					onToggle = function() setDemoChecked(not demoChecked()) end,
					label = "Enable cheats",
				}),
			}),
			x(DemoRow)({
				x(DemoLabel)({ "Radio" }),
				x(Radio.RadioGroup<<number>>)({
					value = function() return demoRadio() end,
					onChange = setDemoRadio,
					direction = "row",
					options = {
						{ label = "Low", value = 0 },
						{ label = "Medium", value = 1 },
						{ label = "High", value = 2 },
					},

					x(DemoValue)({ function() return ("%d"):format(demoRadio()) end }),
				}),
			}),
			x(DemoRow)({
				x(DemoLabel)({ "Slider" }),
				x(Slider)({
					min = 0,
					max = 100,
					step = 5,
					value = demoSlider,
					onValue = setDemoSlider,
					width = 220,
				}),
				x(DemoValue)({ function() return ("%d"):format(demoSlider()) end }),
			}),
			x(DemoRow)({
				x(DemoLabel)({ "Dropdown" }),
				x(Dropdown)({
					options = { "Option A", "Option B", "Option C" },
					value = demoDropdown,
					onSelect = setDemoDropdown,
					width = 180,
				}),
			}),
			x(DemoRow)({
				x(DemoLabel)({ "Text" }),
				x(TextField)({
					value = demoText,
					onChange = setDemoText,
					placeholder = "Type something...",
					width = 200,
				}),
				x(DemoValue)({
					function()
						local t = demoText()
						if t == nil or t == "" then return "(empty)" end
						return t
					end,
				}),
			}),
			x(DemoRow)({
				x(DemoLabel)({ "Toggle" }),
				x(ToggleSwitch)({
					checked = demoToggle,
					onToggle = function() setDemoToggle(not demoToggle()) end,
					label = "Fullscreen",
				}),
			}),
		}),
	})
end

return TestPage
