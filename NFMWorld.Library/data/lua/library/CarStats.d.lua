---@class CarStatsInstance : CarStatsInstance
---@field swits Int3Instance
---@field acelf Vector3d
---@field handb number
---@field airs Fixed64
---@field airc number
---@field _deprecated_Turn number
---@field grip Fixed64
---@field bounce Fixed64
---@field simag Fixed64
---@field moment Fixed64
---@field comprad Fixed64
---@field push Fixed64
---@field revpush Fixed64
---@field lift number
---@field revlift number
---@field powerloss number
---@field flipy number
---@field msquash number
---@field clrad number
---@field dammult Fixed64
---@field maxmag number
---@field dishandle Fixed64
---@field outdam Fixed64
---@field name string
---@field enginsignature number
---@field turnradius number
---@field roadgrip Fixed64|nil
---@field offroadgrip Fixed64|nil
---@field offtrackgrip Fixed64|nil
---@field turn Fixed64
CarStatsInstance = {}

---@class (exact) CarStats
---@field default CarStatsInstance

---Creates a new CarStats
---@return CarStatsInstance
function CarStats.new() end

---Creates a new CarStats
---@param Swits Int3Instance|nil
---@param Acelf Vector3d|nil
---@param Handb number
---@param Airs Fixed64|nil
---@param Airc number
---@param Turn Fixed64|nil
---@param Grip Fixed64|nil
---@param Bounce Fixed64|nil
---@param Simag Fixed64|nil
---@param Moment Fixed64|nil
---@param Comprad Fixed64|nil
---@param Push Fixed64|nil
---@param Revpush Fixed64|nil
---@param Lift number
---@param Revlift number
---@param Powerloss number
---@param Flipy number
---@param Msquash number
---@param Clrad number
---@param Dammult Fixed64|nil
---@param Maxmag number
---@param Dishandle Fixed64|nil
---@param Outdam Fixed64|nil
---@param Name string
---@param Enginsignature number
---@param TurnRadius number
---@param RoadGrip Fixed64|nil
---@param OffRoadGrip Fixed64|nil
---@param OffTrackGrip Fixed64|nil
---@return CarStatsInstance
function CarStats.new_int3n_vector3dn_int_fixed64n_int_fixed64n_fixed64n_fixed64n_fixed64n_fixed64n_fixed64n_fixed64n_fixed64n_int_int_int_int_int_int_fixed64n_int_fixed64n_fixed64n_str_sbyte_int_fixed64n_fixed64n_fixed64n(Swits, Acelf, Handb, Airs, Airc, Turn, Grip, Bounce, Simag, Moment, Comprad, Push, Revpush, Lift, Revlift, Powerloss, Flipy, Msquash, Clrad, Dammult, Maxmag, Dishandle, Outdam, Name, Enginsignature, TurnRadius, RoadGrip, OffRoadGrip, OffTrackGrip) end

---@param stats CarStatsInstance
---@param fileName string
---@return CarStatsInstance
function CarStats.validateStats(stats, fileName) end

---@param self CarStatsInstance
---@param fileName string
---@return string
function CarStatsInstance:validate(fileName) end

---@param self CarStatsInstance
---@param fileName string
---@return string
function CarStatsInstance:validateFailName(fileName) end

---@param self CarStatsInstance
---@param property string
---@return string
function CarStatsInstance:validateFail(property) end
