---@class AccountData : System.IEquatable_AccountData
---@field name string
---@field isLoggedIn boolean
---@field avatarUrl string

AccountData = {}


---@class AvailableOptions
---@field renderers { string }
---@field resolutions { string }
---@field displayModes { string }
---@field antialiasModes { string }
---@field shadowCascadeLevels { string }
---@field shadowResolutions { string }
---@field renderDistanceNames { string }

AvailableOptions = {}


---@class CapturedKey
---@field action string
---@field keyCode integer
---@field cancelled boolean

CapturedKey = {}


---@class CarCollectionData
---@field id string
---@field cars { CarStatsData }

CarCollectionData = {}


---@class CarCollectionsData
---@field collections { CarCollectionData }

CarCollectionsData = {}


---@class CarStatsData
---@field fileName string
---@field name string
---@field collection string
---@field topSpeed number
---@field acceleration number
---@field handling number
---@field powerSave number
---@field strength number
---@field maxHealth number
---@field stunting number
---@field hypergliding number
---@field abing number

CarStatsData = {}


---@class CounterData
---@field value integer

CounterData = {}


---@class CurrentCollectionData
---@field id string

CurrentCollectionData = {}


---@class PauseState
---@field lap integer
---@field totalLaps integer
---@field position integer
---@field totalRacers integer
---@field stageName string

PauseState = {}


