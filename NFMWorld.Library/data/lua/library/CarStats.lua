---@class CarStats : System.IEquatable_CarStats
---@field swits Int3
---@field acelf fixed64vector3
---@field handb integer
---@field airs fixed64
---@field airc integer
---@field _deprecated_Turn integer
---@field grip fixed64
---@field bounce fixed64
---@field simag fixed64
---@field moment fixed64
---@field comprad fixed64
---@field push fixed64
---@field revpush fixed64
---@field lift integer
---@field revlift integer
---@field powerloss integer
---@field flipy integer
---@field msquash integer
---@field clrad integer
---@field dammult fixed64
---@field maxmag integer
---@field dishandle fixed64
---@field outdam fixed64
---@field name string
---@field enginsignature integer
---@field turnradius integer
---@field roadgrip fixed64|nil
---@field offroadgrip fixed64|nil
---@field offtrackgrip fixed64|nil
---@field turn fixed64
---@field validate fun(self: CarStats, fileName: string): string

CarStats = {}

---@type CarStats
CarStats.default = nil

---Creates a new CarStats
---@return CarStats
function CarStats.new() end

---Creates a new CarStats
---@param Swits Int3|nil
---@param Acelf fixed64vector3|nil
---@param Handb integer
---@param Airs fixed64|nil
---@param Airc integer
---@param Turn fixed64|nil
---@param Grip fixed64|nil
---@param Bounce fixed64|nil
---@param Simag fixed64|nil
---@param Moment fixed64|nil
---@param Comprad fixed64|nil
---@param Push fixed64|nil
---@param Revpush fixed64|nil
---@param Lift integer
---@param Revlift integer
---@param Powerloss integer
---@param Flipy integer
---@param Msquash integer
---@param Clrad integer
---@param Dammult fixed64|nil
---@param Maxmag integer
---@param Dishandle fixed64|nil
---@param Outdam fixed64|nil
---@param Name string
---@param Enginsignature integer
---@param TurnRadius integer
---@param RoadGrip fixed64|nil
---@param OffRoadGrip fixed64|nil
---@param OffTrackGrip fixed64|nil
---@return CarStats
function CarStats.new(Swits, Acelf, Handb, Airs, Airc, Turn, Grip, Bounce, Simag, Moment, Comprad, Push, Revpush, Lift, Revlift, Powerloss, Flipy, Msquash, Clrad, Dammult, Maxmag, Dishandle, Outdam, Name, Enginsignature, TurnRadius, RoadGrip, OffRoadGrip, OffTrackGrip) end

---@param stats CarStats
---@param fileName string
---@return CarStats
function CarStats.validateStats(stats, fileName) end
