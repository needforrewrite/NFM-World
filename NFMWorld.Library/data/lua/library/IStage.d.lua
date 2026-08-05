---@class IStageInstance
---@field pieces System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_ITransform_Instance
---@field nodes System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_Instance
---@field checkpoints System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_Instance
---@field fixHoops System_Collections_Generic_IReadOnlyList_NFMWorldLibrary_IAiNode_Instance
---@field nlaps number
IStageInstance = {}

---@class (exact) IStage

IStage = {}

---@param self IStageInstance
---@param objectName string
---@param x number
---@param y number
---@param z number
---@param xz number
---@return ITransformInstance
function IStageInstance:createObject(objectName, x, y, z, xz) end
