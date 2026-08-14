---@class Color3 : System.IEquatable_Color3
---@field r integer
---@field g integer
---@field b integer
---@field darker fun(self: Color3): Color3
---@field brighter fun(self: Color3): Color3

Color3 = {}


---Creates a new Color3
---@param R integer
---@param G integer
---@param B integer
---@return Color3
function Color3.new(R, G, B) end

---Creates a new Color3
---@return Color3
function Color3.new() end

---@param hue number
---@param saturation number
---@param brightness number
---@return Color3
function Color3.fromHSB(hue, saturation, brightness) end
