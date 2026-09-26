--!strict

-- Pause menu overlay (Sx port of preact pausemenu.luau).
-- Controlled by a `visible` prop from RaceHud (driven by the "race:paused" event).

local Sx = require("../../library/sx/index")
local styled = Sx.styled
local x = Sx.x
local Button = require("../../library/ui/button")
local Settings = require("./settings")

-- ── Styled components ────────────────────────────────────────────

local Overlay = styled(Sx.View) {
	position = "absolute",
	top = 0,
	left = 0,
	right = 0,
	bottom = 0,
	display = "flex",
	alignItems = "center",
	justifyContent = "center",
	backgroundColor = "rgba(0,0,0,0.65)",
}

local MenuCard = styled(Sx.View) {
	width = 380,
	paddingTop = 36,
	paddingBottom = 36,
	paddingLeft = 40,
	paddingRight = 40,
	backgroundColor = "rgba(20,20,40,0.85)",
	borderWidth = 1,
	borderColor = "rgba(255,255,255,0.1)",
	borderRadius = 16,
	display = "flex",
	flexDirection = "column",
	alignItems = "center",
	gap = 6,
}

local Title = styled(Sx.Text) {
	fontSize = 22,
	fontStyle = "bold",
	marginBottom = 4,
}

local Subtitle = styled(Sx.Text) {
	fontSize = 12,
	color = "rgba(255,255,255,0.35)",
	marginBottom = 20,
}

local RaceInfo = styled(Sx.View) {
	width = "100%",
	display = "flex",
	flexDirection = "row",
	justifyContent = "center",
	gap = 32,
	marginBottom = 20,
	paddingTop = 10,
	paddingBottom = 10,
	borderTopWidth = 1,
	borderBottomWidth = 1,
	borderColor = "rgba(255,255,255,0.06)",
}

local InfoItem = styled(Sx.View) {
	display = "flex",
	flexDirection = "column",
	alignItems = "center",
	gap = 2,
}

local InfoLabel = styled(Sx.Text) {
	fontSize = 10,
	color = "rgba(255,255,255,0.35)",
}

local InfoValue = styled(Sx.Text) {
	fontSize = 18,
	fontStyle = "bold",
}

local ConfirmBox = styled(Sx.View) {
	backgroundColor = "#1a1a2e",
	borderWidth = 1,
	borderColor = "rgba(255,255,255,0.12)",
	borderRadius = 10,
	paddingTop = 24,
	paddingBottom = 24,
	paddingLeft = 28,
	paddingRight = 28,
	minWidth = 320,
	alignItems = "center",
}

local ConfirmTitle = styled(Sx.Text) {
	fontSize = 16,
	fontStyle = "bold",
	marginBottom = 8,
}

local ConfirmMsg = styled(Sx.Text) {
	fontSize = 13,
	color = "rgba(255,255,255,0.45)",
	marginBottom = 20,
	textAlign = "center",
}

local ConfirmBtns = styled(Sx.View) {
	display = "flex",
	flexDirection = "row",
	justifyContent = "center",
	gap = 10,
}

-- ── Button helpers ───────────────────────────────────────────────
-- MenuBtn / ConfirmBtn are thin adapters over the shared `Button` (hover FX + palette).

local function MenuBtn(props: { variant: 'primary' | 'danger' | nil, label: Sx.Signalish<string>, onPress: () -> () })
	return x(Button)({
		variant = props.variant,
		label = props.label,
		onPress = props.onPress,
		size = "lg",
		fullWidth = true,
		style = { borderRadius = 8 },
		textStyle = { fontSize = 15 },
	})
end

local function ConfirmBtn(props: { variant: 'primary' | 'danger' | nil, label: Sx.Signalish<string>, onPress: () -> () })
	return x(Button)({
		variant = props.variant,
		label = props.label,
		onPress = props.onPress,
		style = { paddingTop = 10, paddingBottom = 10, paddingLeft = 28, paddingRight = 28 },
	})
end

-- ── Component ────────────────────────────────────────────────────

local function PauseMenu()
	local view, setView = Sx.createSignal<<string>>("menu")
	local pauseState, setPauseState = Sx.createSignal<<PauseState | nil>>(nil)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:pauseState", function(data)
			setPauseState(data)
		end)
		Sx.onCleanup(unsub)
	end)

	Sx.createEffect(function()
		if view() == "settings" then
			UiLib.call("settingsOpened", {})
		else
			UiLib.call("settingsClosed", {})
		end
	end)

	return (
		x(Sx.Switch)({
			--[[ Settings ]]
			x(Sx.Match)({ when = function() return view() == "settings" end,
				x(Sx.View)({ style = { position = "absolute", top = 0, left = 0, right = 0, bottom = 0 },
					x(Settings)({ onClose = function() setView("menu") end }),
				}),
			}),
			--[[ Confirm quit ]]
			x(Sx.Match)({ when = function() return view() == "confirmQuit" end,
				x(Overlay)({
					x(Sx.View)({
						style = {
							position = "absolute", top = 0, left = 0, right = 0, bottom = 0,
							display = "flex", alignItems = "center", justifyContent = "center",
							backgroundColor = "rgba(0,0,0,0.7)",
						},
						onmousedown = function() setView("menu") end,

						x(ConfirmBox)({ onmousedown = function() end,
							x(ConfirmTitle)({ "Quit to Main Menu?" }),
							x(ConfirmMsg)({ "All race progress will be lost." }),
							x(ConfirmBtns)({
								x(ConfirmBtn)({ label = "Cancel", onPress = function() setView("menu") end }),
								x(ConfirmBtn)({ variant = "danger", label = "Quit", onPress = function() UiLib.call("quit", {}) end }),
							}),
						}),
					}),
				}),
			}),
			--[[ Confirm restart ]]
			x(Sx.Match)({ when = function() return view() == "confirmRestart" end,
				x(Overlay)({
					x(Sx.View)({
						style = {
							position = "absolute", top = 0, left = 0, right = 0, bottom = 0,
							display = "flex", alignItems = "center", justifyContent = "center",
							backgroundColor = "rgba(0,0,0,0.7)",
						},
						onmousedown = function() setView("menu") end,

						x(ConfirmBox)({ onmousedown = function() end,
							x(ConfirmTitle)({ "Restart Race?" }),
							x(ConfirmMsg)({ "Your current lap time and position will be reset." }),
							x(ConfirmBtns)({
								x(ConfirmBtn)({ label = "Cancel", onPress = function() setView("menu") end }),
								x(ConfirmBtn)({ variant = "danger", label = "Restart", onPress = function() UiLib.call("restart", {}) end }),
							}),
						}),
					}),
				}),
			}),
			--[[ Main menu ]]
			x(Sx.Match)({ when = true,
				x(Overlay)({
					x(MenuCard)({
						x(Title)({ "Paused" }),
						x(Subtitle)({ function()
							local ps = pauseState()
							return ps ~= nil and ps.stageName or "Race"
						end }),

						x(Sx.Show)({ when = function() return pauseState() ~= nil end,
							x(RaceInfo)({
								x(InfoItem)({
									x(InfoLabel)({ "Lap" }),
									x(InfoValue)({ function()
										local ps = pauseState() :: PauseState
										return ("%d/%d"):format(ps.lap, ps.totalLaps)
									end }),
								}),
								x(InfoItem)({
									x(InfoLabel)({ "Position" }),
									x(InfoValue)({ function()
										local ps = pauseState() :: PauseState
										return ("%d/%d"):format(ps.position, ps.totalRacers)
									end }),
								}),
							}),
						}),

						x(MenuBtn)({ variant = "primary", label = "Resume", onPress = function() UiLib.call("resume", {}) end }),
						x(MenuBtn)({ label = "Restart", onPress = function() setView("confirmRestart") end }),
						x(MenuBtn)({ label = "Settings", onPress = function() setView("settings") end }),
						x(MenuBtn)({ variant = "danger", label = "Quit to Main Menu", onPress = function() setView("confirmQuit") end }),
					}),
				}),
			}),
		})
	)
end

return PauseMenu
