--!strict
-- Garage view (Sx port of preact garage.luau), ported from
-- nfm-world/data/html/src/pages/Garage.tsx. Fine-grained: collections/currentCar/
-- currentCollection/searchQuery are signals; the collection list is a keyed For; a car
-- click updates only the cards whose selection actually changed (stable style tables +
-- reference-compare in the renderer skip redundant setProperty).

local function __luaux_read(v) return if type(v) == "function" then (v :: () -> any)() else v end; local Sx = require("../../library/sx/index")
local styled = Sx.styled
local x = Sx.x
local Button = require("../../library/ui/button")
local glasscard = require("../components/glasscard")
local GlassCard = glasscard.GlassCard
local StatBar = glasscard.StatBar

local Root = styled(Sx.View) {
	width = "100%",
	height = "100%",
	display = "flex",
	flexDirection = "row",
}

local Sidebar = styled(Sx.View) {
	width = 340,
	height = "100%",
	paddingTop = 32,
	paddingBottom = 32,
	paddingLeft = 24,
	paddingRight = 24,
	display = "flex",
	flexDirection = "column",
	gap = 16,
	backgroundColor = "rgba(0,0,0,0.3)",
	borderRightWidth = 1,
	borderColor = "rgba(255,255,255,0.06)",
}

local Header = styled(Sx.Text) {
	fontSize = 28,
	fontStyle = "bold",
}

local CollectionList = styled(Sx.View) {
	flex = 1,
	display = "flex",
	flexDirection = "column",
	overflow = "scroll",
	gap = 8,
}

local StatsPanel = styled(Sx.View) {
	flexShrink = 0,
	display = "flex",
	alignItems = "center",
	justifyContent = "center",
}

local StatsCard = styled(Sx.View) {
	width = 280,
	padding = 24,
	flexDirection = "column",
}

local CarName = styled(Sx.Text) {
	fontSize = 18,
	fontStyle = "bold",
	color = "#4fc3f7",
	marginBottom = 16,
}

local Placeholder = styled(Sx.Text) {
	fontSize = 18,
	color = "rgba(255,255,255,0.3)",
}

local CarCardName = styled(Sx.Text) {
	fontSize = 14,
	fontStyle = "bold",
	marginBottom = 4,
}

local CarCardCollection = styled(Sx.Text) {
	fontSize = 11,
	color = "rgba(255,255,255,0.4)",
}

local NoResults = styled(Sx.Text) {
	fontSize = 14,
	color = "rgba(255,255,255,0.3)",
	textAlign = "center",
	padding = 24,
}

-- Module-level constant style for the collection wrapper View (stable identity).
local collectionViewStyle: Sx.Styles = { flexDirection = "column", flexShrink = 0 }

-- Stable card styles: selection change swaps the reference, so unchanged cards skip
-- setProperty (the renderer's reactive-prop effect compares references).
local cardStyleSelected: Sx.Styles = { marginBottom = 8, opacity = 1 }
local cardStyleUnselected: Sx.Styles = { marginBottom = 8, opacity = 0.7 }

-- Card component. `selected` is an accessor; only cards whose selection changes update.
local function CarCard(props: { name: Sx.Signalish<string>, collectionName: Sx.Signalish<string>, fileName: Sx.Signalish<string>, selected: Sx.Signalish<boolean>, onSelect: (string, string) -> () })
	local style = Sx.createMemo(function(): Sx.Styles
		if Sx.read(props.selected) then
			return cardStyleSelected
		end
		return cardStyleUnselected
	end)

	return x(GlassCard)({
		name = function() return "glasscard " .. Sx.read(props.name) end,
		color = function() return Sx.read(props.selected) and "#4fc3f7" or "rgba(255,255,255,0.15)" end,
		style = style,
		onmousedown = function()
			props.onSelect(Sx.read(props.collectionName), Sx.read(props.fileName))
		end,
		x(CarCardName)({ name = function() return Sx.read(props.name) end, props.name }),
		x(CarCardCollection)({ name = function() return Sx.read(props.collectionName) end, props.collectionName }),
	})
end

local function CollectionSection(props: { name: Sx.Signalish<string>, active: Sx.Signalish<boolean>, onSelect: (string) -> () })
	return x(Sx.View)({
		name = function() return "inner view " .. Sx.read(props.name) end,
		style = function() return Sx.createMemo(function(): Sx.Styles
			local active = Sx.read(props.active)
			return {
				marginBottom = 6,
				paddingTop = 4,
				paddingBottom = 4,
				paddingLeft = 8,
				paddingRight = 8,
				borderRadius = 4,
				borderLeftWidth = 2,
				borderColor = active and "#4fc3f7" or "transparent",
				backgroundColor = active and "rgba(79,195,247,0.08)" or "transparent",
				flexGrow = 1,
				flexShrink = 0,
			}
		end) end,
		onmousedown = function() props.onSelect(Sx.read(props.name)) end,
			x(Sx.Text)({
				style = function() return Sx.createMemo(function(): Sx.TextStyles
					local active = Sx.read(props.active)
					return { fontSize = 12, color = active and "#4fc3f7" or "rgba(255,255,255,0.4)" }
				end) end,
				props.name,
			}),
	})
end

local function Garage()
	local currentCar, setCurrentCar = Sx.createSignal<<CarStatsData | nil>>(nil)
	local collections, setCollections = Sx.createSignal<<CarCollectionsData | nil>>(nil)
	local currentCollection, setCurrentCollection = Sx.createSignal<<CarCollectionData | nil>>(nil)
	local searchQuery, setSearchQuery = Sx.createSignal<<string>>("")
	local rootRef: { current: Component? } = { current = nil }

	Sx.createEffect(function()
		local u1 = UiLib.onEvent("garage:currentCar", function(data)
			setCurrentCar(data)
		end)
		local u2 = UiLib.onEvent("garage:collections", function(data)
			setCollections(data)
		end)
		local u3 = UiLib.onEvent("garage:currentCollection", function(data)
			setCurrentCollection(data)
		end)

		UiLib.defer(function()
			UiLib.call("requestGarageData", {})
		end)

		Sx.onCleanup(function()
			u1()
			u2()
			u3()
		end)
	end)

	local function handleSelectCar(collection, fileName)
		UiLib.call("selectCar", { collection = collection, fileName = fileName })
	end

	local function handleSelectCollection(collection)
		UiLib.call("selectCollection", { collection = collection })
	end

	local function handleGlobalKey(event)
		print(tostring(event.keyCode))
		if event.keyCode == Key.left then
			UiLib.call("cycleCar", { direction = "left" })
		elseif event.keyCode == Key.right then
			UiLib.call("cycleCar", { direction = "right" })
		elseif event.keyCode == Key.enter then
			UiLib.call("confirm", {})
		elseif event.keyCode == Key.escape then
			UiLib.call("cancel", {})
		end
	end

	-- Client-side search filter
	local query = Sx.createMemo(function()
		return searchQuery():lower():match("^%s*(.-)%s*$") or ""
	end)

	local filteredCollections = Sx.createMemo(function()
		local result: {(CarCollectionData | { id: string, cars: {CarStatsData} })} = {}
		local cols = collections()
		if cols ~= nil then
			local q = query()
			if q == "" then
				for i = 1, #cols.collections do
					result[#result + 1] = cols.collections[i]
				end
			else
				for i = 1, #cols.collections do
					local col = cols.collections[i]
					local matchedCars = {}
					for j = 1, #col.cars do
						local car = col.cars[j]
						if car.name:lower():find(q, 1, true) ~= nil then
							matchedCars[#matchedCars + 1] = car
						end
					end
					if #matchedCars > 0 then
						result[#result + 1] = {
							id = col.id,
							cars = matchedCars,
						}
					end
				end
			end
		end
		return result
	end)

	Sx.onMount(function()
		print('focusing!!!')
		if rootRef.current ~= nil then
			print('focusing inner!!!')
			rootRef.current:focus()
		end
	end)

	return x(Root)({ ref = function(it)
			print('setting root ref!!!')
			rootRef.current = it
			print('root ref set!!!')
		end, tabindex = 0, onkeydown = handleGlobalKey,
		x(Sidebar)({
			x(Header)({ "Garage" }),

			x(StatsPanel)({
				x(Sx.Show)({ when = function() return currentCar() ~= nil end, fallback = function() return x(Placeholder)({ "Select a car to view stats" }) end,
					x(StatsCard)({
						x(CarName)({ function() return (currentCar() :: CarStatsData).name end }),
						x(StatBar)({ label = "Top Speed", value = function() return (currentCar() :: CarStatsData).topSpeed end, color = "#ff6e40" }),
						x(StatBar)({ label = "Acceleration", value = function() return (currentCar() :: CarStatsData).acceleration end, color = "#ffd740" }),
						x(StatBar)({ label = "Handling", value = function() return (currentCar() :: CarStatsData).handling end, color = "#69f0ae" }),
						x(StatBar)({ label = "Power Save", value = function() return (currentCar() :: CarStatsData).powerSave end, color = "#40c4ff" }),
						x(StatBar)({ label = "Strength", value = function() return (currentCar() :: CarStatsData).strength end, color = "#ff4081" }),
						x(StatBar)({ label = "Max Health", value = function() return (currentCar() :: CarStatsData).maxHealth end, color = "#e040fb" }),
						x(StatBar)({ label = "Stunting", value = function() return (currentCar() :: CarStatsData).stunting end, color = "#ff6e40" }),
						x(StatBar)({ label = "Hypergliding", value = function() return (currentCar() :: CarStatsData).hypergliding end, color = "#7c4dff" }),
						x(StatBar)({ label = "AB'ing", value = function() return (currentCar() :: CarStatsData).abing end, color = "#448aff" }),
					}),
				}),
			}),

			x(Sx.TextInput)({
				style = {
					width = "100%",
					paddingTop = 10,
					paddingBottom = 10,
					paddingLeft = 14,
					paddingRight = 14,
					backgroundColor = "rgba(255,255,255,0.08)",
					borderWidth = 1,
					borderColor = "rgba(255,255,255,0.12)",
					borderRadius = 8,
					flexShrink = 0,
				},
				placeholder = "Search cars...",
				value = searchQuery,
				onchange = function(value)
					setSearchQuery(value)
				end,
				onsubmit = function(value)
					local fc = filteredCollections()
					if #fc > 0 then
						local firstCol = fc[1]
						if firstCol.cars ~= nil and #firstCol.cars > 0 then
							handleSelectCar(firstCol.id, firstCol.cars[1].fileName)
							setSearchQuery("")
							if rootRef.current ~= nil then
								rootRef.current:focus()
							end
						end
					end
				end,
				onkeydown = function(event)
					if event.keyCode == Key.escape then
						setSearchQuery("")
						if rootRef.current ~= nil then
							rootRef.current:focus()
						end
					end
				end,
			}),

			x(CollectionList)({
				x(Sx.For<<CarCollectionData | { id: string, cars: {CarStatsData} }>>)({ each = filteredCollections,
					function(item: () -> (CarCollectionData | { id: string, cars: {CarStatsData} }), index: number)
						return x(Sx.View)({
							name = function() return "view " .. item().id end,
							style = collectionViewStyle,
							x(CollectionSection)({
								name = function() return item().id end,
								active = function() return currentCollection() ~= nil and (currentCollection() :: CarCollectionData).id == item().id end,
								onSelect = handleSelectCollection,
							}),
							x(Sx.For<<CarStatsData>>)({ each = function() return item().cars end,
								function(car: () -> CarStatsData, index2: number)
									return x(CarCard)({
										name = function() return car().name end,
										collectionName = function() return item().id end,
										fileName = function() return car().fileName end,
										selected = function() return currentCar() ~= nil and (currentCar() :: CarStatsData).fileName == car().fileName end,
										onSelect = handleSelectCar,
									})
								end,
							}),
						})
					end,
				}),

				x(Sx.Show)({ when = function() return query() ~= "" and #filteredCollections() == 0 end,
					x(NoResults)({ function() return `No cars match match "{__luaux_read(searchQuery())}"` end,

					}),
				}),
			}),

			x(Button)({
				label = "← Back to Menu",
				onPress = function()
					UiLib.call("back", {})
				end,
			}),
		}),
	})
end

return Garage
