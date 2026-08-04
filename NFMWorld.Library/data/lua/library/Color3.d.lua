---@class Color3Instance : Color3Instance
---@field r number
---@field g number
---@field b number
Color3Instance = {}

---@class (exact) Color3
---@field factor number

---Creates a new Color3
---@param R number
---@param G number
---@param B number
---@return Color3Instance
function Color3.new(R, G, B) end

---@param span ReadOnlySpan_shortInstance
---@return Color3Instance
function Color3.fromSpan(span) end

---@param hue number
---@param saturation number
---@param brightness number
---@return Color3Instance
function Color3.fromHSB(hue, saturation, brightness) end

---@param self Color3Instance
---@return Color3Instance
function Color3Instance:darker() end

---@param self Color3Instance
---@return Color3Instance
function Color3Instance:brighter() end
