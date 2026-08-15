---@class ClientSidePlayer
---@field parameters ClientSidePlayerParameters
---@field index integer
---@field car IInGameCar|nil
---@field bot NFMWorldLibrary.Backend.AI.BaseAi|nil
---@field isFake boolean

ClientSidePlayer = {}


---Creates a new ClientSidePlayer
---@param parameters ClientSidePlayerParameters
---@param index integer
---@param isFake boolean
---@return ClientSidePlayer
function ClientSidePlayer.new(parameters, index, isFake) end
