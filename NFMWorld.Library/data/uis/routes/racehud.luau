--!strict
-- In-race HUD overlay (Sx port of preact racehud.luau), ported from
-- nfm-world/data/html/src/pages/RaceHud.tsx. Fine-grained: each telemetry value is a
-- signal; a per-frame `race:hudState` push updates only the affected leaf text/bar via
-- commitTextUpdate/setProperty.

local function __luaux_read(v) return if type(v) == "function" then (v :: () -> any)() else v end; local Sx = require("../../library/sx/index")
local styled = Sx.styled
local x = Sx.x
local glasscard = require("../components/glasscard")
local GlassCard = glasscard.GlassCard
local StatBar = glasscard.StatBar
local CenterText = glasscard.CenterText
local PauseMenu = require("../components/pausemenu")

local Root = styled(Sx.View) {
	width = "100%",
	height = "100%",
	position = "relative",
}

local CornerLabel = styled(Sx.View) {
	marginBottom = 4,
}

local CornerLabelText = styled(Sx.Text) {
	fontSize = 11,
	color = "rgba(255,255,255,0.5)",
	strokeColor = "rgba(100,100,100,0.4)",
}

local function PowerDamageBars()
	local power, setPower = Sx.createSignal(1)
	local damage, setDamage = Sx.createSignal(0)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:hudState", function(data)
			if data ~= nil then
				setPower(data.power)
				setDamage(data.damage)
			end
		end)
		Sx.onCleanup(unsub)
	end)

	return x(Sx.View)({
		style = {
			position = "absolute",
			top = 16,
			right = 16,
			width = 200,
			display = "flex",
			flexDirection = "column",
			gap = 8,
		},
		x(GlassCard)({ color = "#ff5252",
			x(Sx.View)({ style = { flexDirection = "column" },
				x(CornerLabel)({ x(CornerLabelText)({ "Power" }) }),
				x(StatBar)({
					label = "",
					value = function() return power() / 0.98 end,
					color = "#ff5252",
					height = 10,
				}),
			}),
		}),
		x(GlassCard)({ color = "#4fc3f7",
			x(Sx.View)({ style = { flexDirection = "column" },
				x(CornerLabel)({ x(CornerLabelText)({ "Damage" }) }),
				x(StatBar)({
					label = "",
					value = function() return damage() end,
					color = "#4fc3f7",
					height = 10,
				}),
			}),
		}),
	})
end

function formatTime(ms: number): string
	local timeMins = math.floor(ms / (1000 * 60))
	local timeMinsFmt = string.format("%02d", timeMins)
	local timeSecs = math.floor(ms / 1000 % 60)
	local timeMs = ms % 1000
	local fmt = (timeMins > 0 and (timeMinsFmt .. ":") or "")
		.. string.format("%02d", timeSecs)
		.. "."
		.. string.format("%03d", timeMs)
	return fmt
end

local function Splits()
	local lapDiffMs, setLapDiffMs = Sx.createSignal(0)
	local lastLapDiffMs, setLastLapDiffMs = Sx.createSignal(0)
	local lap, setLap = Sx.createSignal(1)
	local totalLaps, setTotalLaps = Sx.createSignal(3)
	local chkDiffMs, setChkDiffMs = Sx.createSignal(0)
	local lastChkDiffMs, setLastChkDiffMs = Sx.createSignal(0)
	local lapTime, setLapTime = Sx.createSignal(0)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:hudState", function(data)
			if data ~= nil then
				setLapDiffMs(data.lapDiffMs or 0)
				setLastLapDiffMs(data.lastLapDiffMs or 0)
				setLap(data.lap)
				setLapTime(data.lapTime or 0)
				setTotalLaps(data.totalLaps)
				setChkDiffMs(data.chkDiffMs or 0)
				setLastChkDiffMs(data.lastChkDiffMs or 0)
			end
		end)
		Sx.onCleanup(unsub)
	end)

	return x(Sx.View)({
		style = {
			position = "absolute",
			top = 16,
			left = 16,
			width = 180,
		},
		x(GlassCard)({ color = "#69f0ae",
			x(Sx.View)({ style = { flexDirection = "column" },
				x(Sx.View)({
					style = {
						marginBottom = 4,
						letterSpacing = 1,
					},
					x(Sx.Text)({ style = { fontSize = 11, color = "rgba(255,255,255,0.5)" }, function() return `Lap {__luaux_read(lap())}/{__luaux_read(totalLaps())}` end,

					}),
				}),
				x(Sx.View)({
					x(Sx.Text)({
						style = {
							fontSize = 28,
							fontStyle = "bold",
							strokeColor = "black",
						},
						function() return formatTime(lapTime()) end,
					}),
				}),
				x(Sx.Show)({ when = function() return chkDiffMs() and lastChkDiffMs() end,
					x(Sx.Text)({
						style = {
							fontSize = 11,
							color = "rgba(255,255,255,0.4)",
							marginTop = 4,
							strokeColor = "rgba(100,100,100,0.4)",
						}, function() return `CHK Diff: {__luaux_read(formatTime(chkDiffMs()))} (Last: {__luaux_read(formatTime(lastChkDiffMs()))})` end,

					}),
				}),
				x(Sx.Show)({ when = function() return lapDiffMs() and lastLapDiffMs() end,
					x(Sx.Text)({
						style = {
							fontSize = 11,
							color = "rgba(255,255,255,0.4)",
							marginTop = 4,
							strokeColor = "rgba(100,100,100,0.4)",
						}, function() return `Lap Diff: {__luaux_read(formatTime(lapDiffMs()))} (Last: {__luaux_read(formatTime(lastLapDiffMs()))})` end,

					}),
				}),
			}),
		}),
	})
end

local function Speed()
	local speed, setSpeed = Sx.createSignal(0)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:hudState", function(data)
			if data ~= nil then
				setSpeed(data.speed)
			end
		end)
		Sx.onCleanup(unsub)
	end)

	local speedKmh = Sx.createMemo(function()
		return math.floor(speed() * 1.4 * 21.0 * 60.0 * 60.0 / 100000.0 + 0.5)
	end)

	return x(Sx.View)({
		style = {
			position = "absolute",
			bottom = 24,
			left = 0,
			right = 0,
			display = "flex",
			flexDirection = "column",
			alignItems = "center",
		},
		x(GlassCard)({ color = "#c3f74f",
			x(Sx.View)({
				style = {
					flexDirection = "column",
				},
				x(Sx.Text)({ style = { fontSize = 56, fontStyle = "bold", strokeColor = "black" },
					function() return ("%d"):format(speedKmh()) end,
				}),
				x(Sx.Text)({ style = { fontSize = 14, color = "rgba(255,255,255,0.5)", strokeColor = "rgba(100,100,100,0.4)" }, "KM/H" }),
			}),
		}),
	})
end

local function RaceHud()
	local position, setPosition = Sx.createSignal(1)
	local totalRacers, setTotalRacers = Sx.createSignal(1)
	local countdownTimer, setCountdownTimer = Sx.createSignal(0)
	local stateText, setStateText = Sx.createSignal("")
	local stateTextEndsAt, setStateTextEndsAt = Sx.createSignal(nil)
	local isPaused, setIsPaused = Sx.createSignal(false)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:hudState", function(data)
			if data ~= nil then
				setPosition(data.position)
				setTotalRacers(data.totalRacers)
				setCountdownTimer(data.countdownTimer)
				setStateText(data.stateText)
				setStateTextEndsAt(data.stateTextEndsAt)
			end
		end)
		Sx.onCleanup(unsub)
	end)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("race:paused", function(data)
			setIsPaused(data)
		end)
		Sx.onCleanup(unsub)
	end)

	local actualStateText = Sx.createMemo(function()
		local stEndsAt = stateTextEndsAt()
		local stText = stateText()
		if stEndsAt ~= nil and os.time() >= stEndsAt then
			stText = nil
		end
		local countdown = countdownTimer()
		if countdown > 0 then
			return ("Starting in %d"):format(countdown)
		end
		return stText or ""
	end)

	return x(Root)({
		x(Sx.Show)({ when = function()
			return actualStateText() ~= ""
		end,
			x(CenterText)({
				text = function()
					return actualStateText()
				end,
				size = 24,
			}),
		}),

		x(PowerDamageBars)({}),

		x(Splits)({}),

		x(Speed)({}),

		x(Sx.View)({
			style = {
				position = "absolute",
				bottom = 24,
				right = 24,
				display = "flex",
				alignItems = "flex-end",
			},
			x(GlassCard)({ color = "#69f0ae", style = {},
				x(Sx.View)({ style = { flexDirection = "column", alignItems = "center", justifyContent = "center" },
					x(Sx.Text)({ style = { fontSize = 36, fontStyle = "bold", strokeColor = "black" },
						function() return `{__luaux_read(position())}/{__luaux_read(totalRacers())}` end,
					}),
					x(Sx.Text)({ style = { fontSize = 12, color = "rgba(255,255,255,0.5)", strokeColor = "rgba(100,100,100,0.4)" }, "POSITION",

					}),
				}),
			}),
		}),

		x(Sx.Show)({ when = isPaused,
			x(PauseMenu)({}),
		}),
	})
end

return RaceHud
