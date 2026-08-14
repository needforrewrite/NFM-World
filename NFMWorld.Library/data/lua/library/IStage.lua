---@class IStage
---@field pieces System.Collections.Generic.IReadOnlyList_ITransform
---@field nodes System.Collections.Generic.IReadOnlyList_IAiNode
---@field checkpoints System.Collections.Generic.IReadOnlyList_IAiNode
---@field fixHoops System.Collections.Generic.IReadOnlyList_IAiNode
---@field nlaps integer
---@field createObject fun(self: IStage, objectName: string, x: integer, y: integer, z: integer, xz: integer): ITransform

IStage = {}

