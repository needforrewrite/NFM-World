---@class CarStatsInstance : System_IEquatable_NFMWorldLibrary_CarStats_Instance
---@field swits Int3Instance
---@field acelf fixed64vector3
---@field handb number
---@field airs fixed64
---@field airc number
---@field _deprecated_Turn number
---@field grip fixed64
---@field bounce fixed64
---@field simag fixed64
---@field moment fixed64
---@field comprad fixed64
---@field push fixed64
---@field revpush fixed64
---@field lift number
---@field revlift number
---@field powerloss number
---@field flipy number
---@field msquash number
---@field clrad number
---@field dammult fixed64
---@field maxmag number
---@field dishandle fixed64
---@field outdam fixed64
---@field name string
---@field enginsignature number
---@field turnradius number
---@field roadgrip fixed64|nil
---@field offroadgrip fixed64|nil
---@field offtrackgrip fixed64|nil
---@field turn fixed64
CarStatsInstance = {}

---@class (exact) CarStats
---@field default CarStatsInstance

CarStats = {}

---Creates a new CarStats
---@return CarStatsInstance
function CarStats.new() end

---Creates a new CarStats
---@param Swits Int3Instance|nil
---@param Acelf fixed64vector3|nil
---@param Handb number
---@param Airs fixed64|nil
---@param Airc number
---@param Turn fixed64|nil
---@param Grip fixed64|nil
---@param Bounce fixed64|nil
---@param Simag fixed64|nil
---@param Moment fixed64|nil
---@param Comprad fixed64|nil
---@param Push fixed64|nil
---@param Revpush fixed64|nil
---@param Lift number
---@param Revlift number
---@param Powerloss number
---@param Flipy number
---@param Msquash number
---@param Clrad number
---@param Dammult fixed64|nil
---@param Maxmag number
---@param Dishandle fixed64|nil
---@param Outdam fixed64|nil
---@param Name string
---@param Enginsignature number
---@param TurnRadius number
---@param RoadGrip fixed64|nil
---@param OffRoadGrip fixed64|nil
---@param OffTrackGrip fixed64|nil
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
