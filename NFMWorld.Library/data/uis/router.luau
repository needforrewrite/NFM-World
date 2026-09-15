--!strict
-- Sx hash-based router. Subscribes to C# navigation (nfmw:navigate) via a signal and
-- renders the matching page through Sx.Switch. Only the main-menu route is ported to Sx
-- yet; the other routes are commented out.

local Sx = require("../library/sx/index")
local x = Sx.x
local View = Sx.View
local Switch = Sx.Switch
local Match = Sx.Match

local MainMenu = require("./routes/mainmenu")
local Garage = require("./routes/garage")
local RaceHud = require("./routes/racehud")
local TestPage = require("./routes/test")
local DevTools = require("./components/devtools")

local defaultRoute = "main-menu"

local function Router()
	local route, setRoute = Sx.createSignal(defaultRoute)

	Sx.createEffect(function()
		local unsub = UiLib.onEvent("nfmw:navigate", function(hash)
			setRoute(hash)
		end)
		Sx.onCleanup(unsub)
	end)

	return (
		x(View)({
			style = {
				width = "100%",
				height = "100%",
				position = "relative",
				display = "flex",
				flexDirection = "column",
			},
			x(Switch)({
				x(Match)({ when = function() return route() == "main-menu" end,
					x(MainMenu)({}),
				}),
				x(Match)({ when = function() return route() == "garage" end,
					x(Garage)({}),
				}),
				x(Match)({ when = function() return route() == "race" end,
					x(RaceHud)({}),
				}),
				x(Match)({ when = function() return route() == "test" end,
					x(TestPage)({}),
				}),
				--[[ Fallback: any unhandled route shows the default page (main menu) ]]
				x(Match)({ when = true,
					x(MainMenu)({}),
				}),
			}),

			--[[ DevTools overlay (top-right inspector for the Sx renderer). ]]
			x(DevTools)({}),
		})
	)
end

Sx.render(x(Router)({}))
