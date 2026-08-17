---Defines a property.
---@class ConfigProperty
---@field name string
---@field type 'string'|'number'|'boolean'
---@field description string?
---@field options { [integer]: { label: string, value: string|number|boolean } }?

---@class Config
---@field name string
---@field description string
---@field properties ConfigProperty[]

---Defines the config for a gamemode
---@param config Config
function DefineGamemodeConfig(config) end