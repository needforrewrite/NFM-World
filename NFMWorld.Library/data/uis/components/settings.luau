--!strict
-- Settings view (Sx port of preact settings.luau). Rendered by the main menu and the
-- pause menu. Fine-grained: `config`/`options`/`tab`/`capturingAction`/modals are
-- signals; tab content and controls read them reactively through accessors, so editing a
-- slider only re-sets that one control's props/text, never the whole tree.

local Sx = require("../../library/sx/index")
local styled = Sx.styled
local x = Sx.x

local Button = require("../../library/ui/button")
local theme = require("../../library/ui/theme")

local Checkbox = require("./checkbox")
local Dropdown = require("./dropdown")
local Slider = require("./slider")

-- ── Helpers ──────────────────────────────────────────────────────

local function keyCodeToName(code)
	if code == 0 then return "None" end
	local names = {
		[8] = "Backspace", [9] = "Tab", [13] = "Enter",
		[16] = "Shift", [17] = "Ctrl", [18] = "Alt",
		[27] = "Escape", [32] = "Space",
		[33] = "PageUp", [34] = "PageDown", [35] = "End", [36] = "Home",
		[37] = "Left", [38] = "Up", [39] = "Right", [40] = "Down",
		[44] = "PrintScreen", [45] = "Insert", [46] = "Delete",
		[91] = "LWin", [92] = "RWin",
		[112] = "F1", [113] = "F2", [114] = "F3", [115] = "F4",
		[116] = "F5", [117] = "F6", [118] = "F7", [119] = "F8",
		[120] = "F9", [121] = "F10", [122] = "F11", [123] = "F12",
		[144] = "NumLock", [145] = "ScrollLock",
		[160] = "LShift", [161] = "RShift",
		[162] = "LCtrl", [163] = "RCtrl",
		[164] = "LAlt", [165] = "RAlt",
		[186] = ";", [187] = "=", [188] = ",", [189] = "-", [190] = ".", [191] = "/",
		[192] = "`",
		[219] = "[", [220] = "\\", [221] = "]", [222] = "'",
	}
	local name = names[code]
	if name ~= nil then return name end
	if code >= 65 and code <= 90 then return string.char(code) end
	if code >= 48 and code <= 57 then return string.char(code) end
	if code >= 96 and code <= 105 then return ("NumPad%d"):format(code - 96) end
	if code == 106 then return "NumPad*" end
	if code == 107 then return "NumPad+" end
	if code == 109 then return "NumPad-" end
	if code == 110 then return "NumPad." end
	if code == 111 then return "NumPad/" end
	return ("Key(0x%x)"):format(code)
end

local DISTANT_OUTLINE_BEHAVIORS = {
	"Distance Falloff (With Cutoff)",
	"Distance Falloff",
	"Classic Cutoff (NFM)",
	"Always Render",
	"Hide Outlines",
}

-- ── Styled primitives ────────────────────────────────────────────

local Root = styled(Sx.View) {
	width = "100%",
	height = "100%",
	display = "flex",
	flexDirection = "column",
	backgroundColor = "rgba(0,0,0,0.65)",
}

local Header = styled(Sx.Text) {
	fontSize = 28,
	fontStyle = "bold",
	textAlign = "center",
	marginTop = 20,
	marginBottom = 20,
	flexShrink = 0,
}

local TabBar = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	justifyContent = "center",
	gap = 4,
	paddingTop = 16,
	paddingLeft = 40,
	paddingRight = 40,
	flexShrink = 0,
}

local Content = styled(Sx.View) {
	display = "flex",
	flexDirection = "column",
	flexGrow = 1,
	flexShrink = 1,
	minHeight = 0,
	overflow = "scroll",
	paddingTop = 20,
	paddingLeft = 40,
	paddingRight = 40,
	paddingBottom = 10,
}

local BottomBar = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	alignItems = "center",
	justifyContent = "center",
	gap = 12,
	paddingTop = 16,
	paddingBottom = 16,
	paddingLeft = 40,
	paddingRight = 40,
	borderTopWidth = 1,
	borderColor = "rgba(255,255,255,0.08)",
	flexShrink = 0,
}

local SectionLabel = styled(Sx.Text) {
	fontSize = 11,
	fontStyle = "bold",
	color = "rgba(255,255,255,0.35)",
	marginBottom = 10,
	flexShrink = 0,
}

local Row = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	alignItems = "center",
	justifyContent = "space-between",
	gap = 16,
	paddingTop = 6,
	paddingBottom = 6,
	flexShrink = 0,
}

local Label = styled(Sx.Text) {
	fontSize = 14,
	color = "rgba(255,255,255,0.8)",
	minWidth = 140,
}

local SliderValue = styled(Sx.Text) {
	fontSize = 12,
	color = "rgba(255,255,255,0.5)",
	marginLeft = 8,
	minWidth = 50,
}

local Divider = styled(Sx.View) {
	height = 1,
	backgroundColor = "rgba(255,255,255,0.06)",
	marginTop = 12,
	marginBottom = 12,
	flexShrink = 0,
}

local ResetLink = styled(Sx.Text) {
	color = "rgba(255,255,255,0.35)",
	fontSize = 12,
	paddingTop = 8,
	paddingBottom = 8,
	flexShrink = 0,
	hover = { color = "rgba(255,152,0,0.7)" },
}

local KeyBindingRow = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	alignItems = "center",
	justifyContent = "space-between",
	gap = 12,
	paddingTop = 4,
	paddingBottom = 4,
	flexShrink = 0,
}

local KeyBindingLabel = styled(Sx.Text) {
	fontSize = 13,
	color = "rgba(255,255,255,0.75)",
	minWidth = 160,
}

-- ── Interactive controls (approximations of HTML controls) ───────
-- Reactive props (`active`/`capturing`/`checked`/`value`) may be accessors.
-- Buttons share the unified `Button` (hover FX + palette). TabBtn/Btn are thin API
-- adapters; KeyBindingBtn is a specialized capture button using the same palette.

local function TabBtn(props: { active: Sx.Signalish<boolean?>, label: Sx.Signalish<string>, onPress: () -> () })
	return x(Button)({ variant = "ghost", active = props.active, label = props.label, onPress = props.onPress })
end

local function Btn(props: { variant: "primary" | nil, label: Sx.Signalish<string>, onPress: () -> () })
	return x(Button)({ variant = props.variant, label = props.label, onPress = props.onPress })
end

local function KeyBindingBtn(props: { capturing: Sx.Signalish<boolean?>, label: Sx.Signalish<string>, onPress: () -> () })
	local hovered, setHovered = Sx.createSignal(false)
	return x(Sx.View)({
		style = function(): Sx.Styles
			local capturing = Sx.read(props.capturing)
			local hov = hovered()
			return {
				paddingTop = 5,
				paddingBottom = 5,
				paddingLeft = 18,
				paddingRight = 18,
				minWidth = 140,
				alignItems = "center",
				backgroundColor = capturing and theme.warningSoft or (hov and theme.surfaceHover or theme.surface),
				borderWidth = 1,
				borderColor = capturing and theme.warningBorder or theme.surfaceBorder,
				borderRadius = 5,
			}
		end,
		onmousedown = function() props.onPress() end,
		onmouseenter = function() setHovered(true) end,
		onmouseleave = function() setHovered(false) end,
		x(Sx.Text)({ style = function(): Sx.TextStyles
			local capturing = Sx.read(props.capturing)
			return { fontSize = 13, fontStyle = "bold", color = capturing and theme.warning or theme.textPrimary }
		end,
			function() return Sx.read(props.label) end,
		}),
	})
end

-- ── Tabs ─────────────────────────────────────────────────────────

local function VideoTab(props: { config: Sx.Accessor<SettingsSnapshot>, options: Sx.Accessor<AvailableOptions>, set: ((key: string, value: any) -> ()) })
	local config = props.config
	local options = props.options
	local set = props.set

	return (
		x(Sx.Fragment)({
			x(SectionLabel)({ "Display" }),
			x(Row)({
				x(Label)({ "Renderer" }),
				x(Dropdown)({
					options = function() return options().renderers or {} end,
					value = function() return config().selectedRenderer end,
					onSelect = function(v) set("selectedRenderer", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "Resolution" }),
				x(Dropdown)({
					options = function() return options().resolutions or {} end,
					value = function() return config().selectedResolution end,
					onSelect = function(v) set("selectedResolution", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "Display Mode" }),
				x(Dropdown)({
					options = function() return options().displayModes or {} end,
					value = function() return config().selectedDisplayMode end,
					onSelect = function(v) set("selectedDisplayMode", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "VSync" }),
				x(Checkbox)({ checked = function() return config().vsync end, onToggle = function() set("vsync", not config().vsync) end }),
			}),
			x(Row)({
				x(Label)({ "FPS Limit" }),
				x(Slider)({
					min = 63, max = 240, step = 1,
					value = function() return config().fpsLimit end,
					onValue = function(v) set("fpsLimit", v) end,
				}),
				x(SliderValue)({
					function()
						local v = config().fpsLimit
						return v == 0 and "Unlimited" or ("%d FPS"):format(v)
					end,
				}),
			}),

			x(Divider)({}),
			x(SectionLabel)({ "Quality" }),
			x(Row)({
				x(Label)({ "Antialiasing" }),
				x(Dropdown)({
					options = function() return options().antialiasModes or {} end,
					value = function() return config().antialias end,
					onSelect = function(v) set("antialias", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "Shadow Distance" }),
				x(Dropdown)({
					options = function() return options().shadowCascadeLevels or {} end,
					value = function() return config().shadowCascadeLevel end,
					onSelect = function(v) set("shadowCascadeLevel", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "Shadow Resolution" }),
				x(Dropdown)({
					options = function() return options().shadowResolutions or {} end,
					value = function() return config().shadowResolution end,
					onSelect = function(v) set("shadowResolution", v) end,
				}),
			}),
			x(Row)({
				x(Label)({ "Render Distance" }),
				x(Dropdown)({
					options = function() return options().renderDistanceNames or {} end,
					value = function() return config().renderDistance end,
					onSelect = function(v) set("renderDistance", v) end,
				}),
			}),

			x(Divider)({}),
			x(SectionLabel)({ "Advanced" }),
			x(Row)({
				x(Label)({ "Low Latency" }),
				x(Checkbox)({ checked = function() return config().lowLatency end, onToggle = function() set("lowLatency", not config().lowLatency) end }),
			}),
			x(Row)({
				x(Label)({ "Outline Width" }),
				x(Slider)({
					min = 0.5, max = 4, step = 0.1,
					value = function() return config().lineWidth end,
					onValue = function(v) set("lineWidth", v) end,
				}),
				x(SliderValue)({ function() return ("%.1f"):format(config().lineWidth) end }),
			}),
			x(Row)({
				x(Label)({ "Distant Outlines" }),
				x(Dropdown)({
					options = DISTANT_OUTLINE_BEHAVIORS,
					value = function() return config().distantOutlineBehavior end,
					onSelect = function(v) set("distantOutlineBehavior", v) end,
				}),
			}),
		})
	)
end

local function AudioTab(props: { config: Sx.Accessor<SettingsSnapshot>, set: ((key: string, value: any) -> ()) })
	local config = props.config
	local set = props.set

	return (
		x(Sx.Fragment)({
			x(SectionLabel)({ "Volume" }),
			x(Row)({
				x(Label)({ "Mute All" }),
				x(Checkbox)({ checked = function() return config().muteAll end, onToggle = function() set("muteAll", not config().muteAll) end }),
			}),
			x(Row)({
				x(Label)({ "Master Volume" }),
				x(Slider)({
					min = 0, max = 1, step = 0.01,
					value = function() return config().masterVolume end,
					onValue = function(v) set("masterVolume", v) end,
				}),
				x(SliderValue)({ function() return ("%d%%"):format(math.floor(config().masterVolume * 100 + 0.5)) end }),
			}),
			x(Row)({
				x(Label)({ "Music Volume" }),
				x(Slider)({
					min = 0, max = 1, step = 0.01,
					value = function() return config().musicVolume end,
					onValue = function(v) set("musicVolume", v) end,
				}),
				x(SliderValue)({ function() return ("%d%%"):format(math.floor(config().musicVolume * 100 + 0.5)) end }),
			}),
			x(Row)({
				x(Label)({ "Effects Volume" }),
				x(Slider)({
					min = 0, max = 1, step = 0.01,
					value = function() return config().effectsVolume end,
					onValue = function(v) set("effectsVolume", v) end,
				}),
				x(SliderValue)({ function() return ("%d%%"):format(math.floor(config().effectsVolume * 100 + 0.5)) end }),
			}),

			x(Divider)({}),
			x(Row)({
				x(Label)({ "Remastered Music" }),
				x(Checkbox)({ checked = function() return config().remasteredMusic end, onToggle = function() set("remasteredMusic", not config().remasteredMusic) end }),
			}),
		})
	)
end

local function GameTab(props: { config: Sx.Accessor<SettingsSnapshot>, set: ((key: string, value: any) -> ()), onReset: () -> () })
	local config = props.config
	local set = props.set

	return (
		x(Sx.Fragment)({
			x(SectionLabel)({ "Camera" }),
			x(Row)({
				x(Label)({ "Field of View" }),
				x(Slider)({
					min = 58.7, max = 120, step = 0.5,
					value = function() return config().fov end,
					onValue = function(v) set("fov", v) end,
				}),
				x(SliderValue)({ function() return ("%.1f°"):format(config().fov) end }),
			}),
			x(Row)({
				x(Label)({ "Smooth FOV" }),
				x(Checkbox)({ checked = function() return config().smoothFov end, onToggle = function() set("smoothFov", not config().smoothFov) end }),
			}),
			x(Row)({
				x(Label)({ "Follow Y Offset" }),
				x(Slider)({
					min = -160, max = 500, step = 1,
					value = function() return config().followY end,
					onValue = function(v) set("followY", v) end,
				}),
				x(SliderValue)({ function() return ("%d"):format(config().followY) end }),
			}),
			x(Row)({
				x(Label)({ "Follow Z Offset" }),
				x(Slider)({
					min = -500, max = 500, step = 1,
					value = function() return config().followZ end,
					onValue = function(v) set("followZ", v) end,
				}),
				x(SliderValue)({ function() return ("%d"):format(config().followZ) end }),
			}),

			x(Divider)({}),
			x(ResetLink)({ onmousedown = function() props.onReset() end, "Reset Camera Defaults" }),
		})
	)
end

local function KeyboardTab(props: { config: Sx.Accessor<SettingsSnapshot>, onReset: () -> (), capturingAction: Sx.Accessor<string?>, startCapture: ((action: string) -> ()) })
	local config = props.config

	return (
		x(Sx.Fragment)({
			x(SectionLabel)({ "Key Bindings" }),

			x(ResetLink)({ onmousedown = function() props.onReset() end, "Reset All to Defaults" }),

			x(Divider)({}),

			function() return x(Sx.For<<KeyBindingData>>) {
				each = function(): {KeyBindingData}
					local c = config()
					return c ~= nil and c.keyBindings or {}
				end,
				function(item: () -> KeyBindingData, index: number)
					local b = item()
					return x(KeyBindingRow)({
						x(KeyBindingLabel)({ b.displayName ~= "" and b.displayName or b.action }),
						x(KeyBindingBtn)({
							capturing = function() return props.capturingAction() == b.action end,
							label = function()
								local cap = props.capturingAction() == b.action
								return cap and "Press any key..." or keyCodeToName(b.keyCode)
							end,
							onPress = function()
								props.startCapture(b.action)
							end,
						}),
					})
				end,
			} end,

			x(Sx.Show)({ when = function() return props.capturingAction() ~= nil end,
				x(Sx.Text)({ style = { fontSize = 12, color = "rgba(255,152,0,0.6)", marginTop = 10, textAlign = "center" }, "Press any key to bind, or ESC to cancel...",

				}),
			}),
		})
	)
end

-- ── Settings component ───────────────────────────────────────────

type ConfirmModal = {
	title: string,
	message: string,
	onConfirm: () -> (),
}

local function Settings(props)
	local onClose = props.onClose

	local tab, setTab = Sx.createSignal("video")
	local config, setConfig = Sx.createSignal<<SettingsSnapshot | nil>>(nil)
	local options, setOptions = Sx.createSignal<<AvailableOptions | nil>>(nil)
	local capturingAction, setCapturingAction = Sx.createSignal<<string | nil>>(nil)
	local confirmModal, setConfirmModal = Sx.createSignal<<ConfirmModal | nil>>(nil)
	local restartModal, setRestartModal = Sx.createSignal<<boolean>>(false)
	local pendingClose = { current = false }
	local rootRef: { current: Component? } = {}

	local function copySnapshot(c: SettingsSnapshot): SettingsSnapshot
		local t: SettingsSnapshot = {
			selectedRenderer = c.selectedRenderer,
			selectedResolution = c.selectedResolution,
			selectedDisplayMode = c.selectedDisplayMode,
			vsync = c.vsync,
			fpsLimit = c.fpsLimit,
			antialias = c.antialias,
			shadowCascadeLevel = c.shadowCascadeLevel,
			shadowResolution = c.shadowResolution,
			renderDistance = c.renderDistance,
			lowLatency = c.lowLatency,
			lineWidth = c.lineWidth,
			masterVolume = c.masterVolume,
			musicVolume = c.musicVolume,
			effectsVolume = c.effectsVolume,
			muteAll = c.muteAll,
			remasteredMusic = c.remasteredMusic,
			fov = c.fov,
			followY = c.followY,
			followZ = c.followZ,
			smoothFov = c.smoothFov,
			distantOutlineBehavior = c.distantOutlineBehavior,
			keyBindings = {}
		}
		if c.keyBindings ~= nil then
			for i = 1, #c.keyBindings do
				local b = c.keyBindings[i]
				t.keyBindings[i] = {
					action = b.action,
					displayName = b.displayName,
					keyCode = b.keyCode
				}
			end
		end
		return t
	end

	local function updateConfig(updater)
		local prev = config()
		if prev == nil then return end
		setConfig(updater(prev))
	end

	local function applySetting(key, value)
		UiLib.call("applySetting", { key = key, value = value })
	end

	local function set(key, value)
		updateConfig(function(c)
			local nextC = copySnapshot(c)
			;(nextC::any)[key] = value
			return nextC
		end)
		applySetting(key, value)
	end

	Sx.createEffect(function()
		local u1 = UiLib.onEvent("settings:config", function(data)
			if data == nil then
				setConfig(nil)
			else
				setConfig(copySnapshot(data))
			end
		end)

		local u2 = UiLib.onEvent("settings:options", function(data)
			setOptions(data)
		end)

		local u3 = UiLib.onEvent("settings:keyCaptured", function(data)
			setCapturingAction(nil)
			if not data.cancelled and data.action ~= nil then
				local prev = config()
				if prev ~= nil then
					local kb: {KeyBindingData} = {}
					for i = 1, #prev.keyBindings do
						local b = prev.keyBindings[i]
						local newKeyCode = b.keyCode
						if b.keyCode == data.keyCode and b.action ~= data.action then
							newKeyCode = 0
						elseif b.action == data.action then
							newKeyCode = data.keyCode
						end
						kb[i] = {
							action = b.action,
							displayName = b.displayName,
							keyCode = newKeyCode
						}
					end
					local nextC = copySnapshot(prev)
					nextC.keyBindings = kb
					setConfig(nextC)
				end
			end
		end)

		local u4 = UiLib.onEvent("settings:requireRestart", function()
			setRestartModal(true)
		end)

		local u5 = UiLib.onEvent("settings:saved", function()
			if pendingClose.current then
				pendingClose.current = false
				if onClose ~= nil then onClose() end
			end
		end)

		UiLib.call("getConfig", {})

		Sx.onCleanup(function()
			u1()
			u2()
			u3()
			u4()
			u5()
		end)
	end)

	Sx.createEffect(function()
		if rootRef.current ~= nil then rootRef.current:focus() end
	end)

	local function handleRootKey(event)
		if event.keyCode == Key.escape and onClose ~= nil then
			onClose()
		end
	end

	local function handleSave()
		UiLib.call("saveConfig", {})
	end

	local function handleClose()
		UiLib.call("close", {})
		if onClose ~= nil then onClose() end
	end

	local function handleOk()
		pendingClose.current = true
		UiLib.call("saveConfig", {})
	end

	local function handleResetDefaults(section, title, message)
		setConfirmModal {
			title = title,
			message = message,
			onConfirm = function()
				UiLib.call("resetDefaults", { section = section })
				setConfirmModal(nil)
			end,
		}
	end

	local function startCapture(action)
		setCapturingAction(action)
		UiLib.call("startCapture", { action = action })
	end

	return (
		x(Root)({ ref = rootRef, tabindex = 0, onkeydown = handleRootKey,
			x(Sx.Switch)({
				x(Sx.Match)({ when = function() return config() == nil or options() == nil end,
					x(Header)({ "Settings" }),
					x(Content)({
						x(Sx.Text)({ style = { textAlign = "center", color = "rgba(255,255,255,0.3)", marginTop = 80 },
							"Loading settings...",
						}),
					}),
				}),
				x(Sx.Match)({ when = function() return true end,
					x(Header)({ "Settings" }),
					x(TabBar)({
						x(TabBtn)({ label = "Keyboard", active = function() return tab() == "keyboard" end, onPress = function() setTab("keyboard") end }),
						x(TabBtn)({ label = "Video", active = function() return tab() == "video" end, onPress = function() setTab("video") end }),
						x(TabBtn)({ label = "Audio", active = function() return tab() == "audio" end, onPress = function() setTab("audio") end }),
						x(TabBtn)({ label = "Game", active = function() return tab() == "game" end, onPress = function() setTab("game") end }),
					}),
					x(Content)({
						x(Sx.Show)({ when = function() return tab() == "video" end,
							x(VideoTab)({ config = config :: Sx.Accessor<SettingsSnapshot>, options = options :: Sx.Accessor<AvailableOptions>, set = set }),
						}),
						x(Sx.Show)({ when = function() return tab() == "audio" end,
							x(AudioTab)({ config = config :: Sx.Accessor<SettingsSnapshot>, set = set }),
						}),
						x(Sx.Show)({ when = function() return tab() == "game" end,
							x(GameTab)({
								config = config :: Sx.Accessor<SettingsSnapshot>,
								set = set,
								onReset = function()
									handleResetDefaults("camera", "Reset Camera", "Are you sure you want to reset camera settings to default?")
								end,
							}),
						}),
						x(Sx.Show)({ when = function() return tab() == "keyboard" end,
							x(KeyboardTab)({
								config = config :: Sx.Accessor<SettingsSnapshot>,
								capturingAction = capturingAction,
								startCapture = startCapture,
								onReset = function()
									handleResetDefaults("keyboard", "Reset Key Binds", "Are you sure you want to reset key binds to default?")
								end,
							}),
						}),
					}),
					x(BottomBar)({
						x(Btn)({ variant = "primary", label = "OK", onPress = handleOk }),
						x(Btn)({ label = "Cancel", onPress = handleClose }),
						x(Btn)({ label = "Apply", onPress = handleSave }),
					}),

					x(Sx.Show)({ when = function() return confirmModal() ~= nil and not restartModal() end,
						x(Sx.View)({
							style = {
								position = "absolute", top = 0, left = 0, right = 0, bottom = 0,
								display = "flex", alignItems = "center", justifyContent = "center",
								backgroundColor = "rgba(0,0,0,0.5)",
							},
							onmousedown = function() setConfirmModal(nil) end,
							x(Sx.View)({
								style = {
									backgroundColor = "#1a1a2e",
									borderWidth = 1, borderColor = "rgba(255,255,255,0.12)",
									borderRadius = 10,
									paddingTop = 24, paddingBottom = 24, paddingLeft = 28, paddingRight = 28,
									minWidth = 320,
									alignItems = "center",
									flexDirection = "column",
								},
								onmousedown = function() end,
								x(Sx.Text)({ style = { fontSize = 16, fontStyle = "bold", marginBottom = 8 }, function() return (confirmModal() :: ConfirmModal).title end }),
								x(Sx.Text)({ style = { fontSize = 13, color = "rgba(255,255,255,0.5)", marginBottom = 20 }, function() return (confirmModal() :: ConfirmModal).message end }),
								x(Sx.View)({ style = { display = "flex", flexDirection = "row", justifyContent = "center", gap = 10 },
									x(Btn)({ variant = "primary", label = "Yes", onPress = function() return (confirmModal() :: ConfirmModal).onConfirm() end }),
									x(Btn)({ label = "No", onPress = function() setConfirmModal(nil) end }),
								}),
							}),
						}),
					}),

					x(Sx.Show)({ when = function() return restartModal() end,
						x(Sx.View)({ style = {
							position = "absolute", top = 0, left = 0, right = 0, bottom = 0,
							display = "flex", alignItems = "center", justifyContent = "center",
							backgroundColor = "rgba(0,0,0,0.5)",
						},
							x(Sx.View)({ style = {
								backgroundColor = "#1a1a2e",
								borderWidth = 1, borderColor = "rgba(255,255,255,0.12)",
								borderRadius = 10,
								paddingTop = 24, paddingBottom = 24, paddingLeft = 28, paddingRight = 28,
								minWidth = 320,
								alignItems = "center",
								flexDirection = "column",
							},
								x(Sx.Text)({ style = { fontSize = 16, fontStyle = "bold", marginBottom = 8 }, "Restart Required" }),
								x(Sx.Text)({ style = { fontSize = 13, color = "rgba(255,255,255,0.5)", marginBottom = 20, textAlign = "center" },
									"The renderer has been changed. A restart is required for this change to take effect.",
								}),
								x(Sx.View)({ style = { display = "flex", flexDirection = "row", justifyContent = "center", gap = 10 },
									x(Btn)({ variant = "primary", label = "Restart Now", onPress = function() UiLib.call("restartNow", {}) end }),
									x(Btn)({
										label = "Later",
										onPress = function()
											setRestartModal(false)
											if pendingClose.current then
												pendingClose.current = false
												if onClose ~= nil then onClose() end
											end
										end,
									}),
								}),
							}),
						}),
					}),
				}),
			}),
		})
	)
end

return Settings
