---@meta NFMWorld.Library

---@class CarPhysics
---@field halted boolean
---@field btab boolean
---@field capcnt integer
---@field capsized boolean
---@field caught UnlimitedArray<boolean> --- (read-only)
---@field stat CarStats
---@field cn integer
---@field cntdest integer
---@field cntouch integer
---@field colliding_with_client_player boolean
---@field cxz fixed64
---@field static_camera_xz fixed64
---@field dcnt integer
---@field dcomp fixed64
---@field lcomp fixed64
---@field wasted boolean
---@field dominate UnlimitedArray<boolean> --- (read-only)
---@field drag fixed64 --- (read-only)
---@field fixes integer
---@field forca fixed64
---@field ftab boolean
---@field turn_xz fixed64
---@field gtouch boolean
---@field hitmag integer
---@field im integer
---@field lastcolido integer
---@field loop integer
---@field lxz fixed64
---@field mtouch boolean
---@field mxz fixed64
---@field num_roof_damage integer
---@field newcar boolean
---@field newedcar integer
---@field nmlt integer
---@field nofocus boolean
---@field outshakedam integer
---@field pd boolean
---@field pl boolean
---@field pmlt integer
---@field point integer
---@field power fixed64
---@field powerup fixed64
---@field pr boolean
---@field pu boolean
---@field pushed boolean
---@field pxy fixed64
---@field pzy fixed64
---@field rcomp fixed64
---@field rtab boolean
---@field scx LuaArray<fixed64>
---@field scy LuaArray<fixed64>
---@field scz LuaArray<fixed64>
---@field shakedam integer
---@field skid integer
---@field speed fixed64
---@field roof_damage integer
---@field surf_count integer
---@field surfing boolean
---@field tilt fixed64
---@field total_stunt_xy fixed64
---@field total_stunt_xz fixed64
---@field total_stunt_zy fixed64
---@field tcnt integer
---@field txz fixed64
---@field ucomp fixed64
---@field wtouch boolean
---@field xtpower integer
---@field is_client_player boolean
---@field mtcount integer
---@field py fixed64
CarPhysics = {}

---@param self IInGameCar
---@param othermad CarPhysics
---@param other IInGameCar
function CarPhysics:collide(self, othermad, other) end
---@param control Control
---@param car IInGameCar
---@param stage IStage
function CarPhysics:drive(control, car, stage) end

---@class CarStats
---@field swits Int3 --- (read-only)
---@field acelf fixed64vector3 --- (read-only)
---@field handb integer --- (read-only)
---@field airs fixed64 --- (read-only)
---@field airc integer --- (read-only)
---@field grip fixed64 --- (read-only)
---@field bounce fixed64 --- (read-only)
---@field simag fixed64 --- (read-only)
---@field moment fixed64 --- (read-only)
---@field comprad fixed64 --- (read-only)
---@field push fixed64 --- (read-only)
---@field revpush fixed64 --- (read-only)
---@field lift integer --- (read-only)
---@field revlift integer --- (read-only)
---@field powerloss integer --- (read-only)
---@field flipy integer --- (read-only)
---@field msquash integer --- (read-only)
---@field clrad integer --- (read-only)
---@field dammult fixed64 --- (read-only)
---@field maxmag integer --- (read-only)
---@field dishandle fixed64 --- (read-only)
---@field outdam fixed64 --- (read-only)
---@field name string --- (read-only)
---@field enginsignature integer --- (read-only)
---@field turnradius integer
---@field roadgrip fixed64?
---@field offroadgrip fixed64?
---@field offtrackgrip fixed64?
---@field turn fixed64 --- (read-only)
CarStats = {}


---@class Control
---@field arrace boolean
---@field chatup integer
---@field down boolean
---@field enter boolean
---@field exit boolean
---@field handb boolean
---@field multion integer
---@field mutem boolean
---@field mutes boolean
---@field radar boolean
---@field right boolean
---@field up boolean
---@field left boolean
---@field lookback integer
---@field wall integer
---@field zyinv boolean
Control = {}

function Control:reset() end

---@class PlayerParameters
---@field player_name string --- (read-only)
---@field car_name string --- (read-only)
---@field color Color3 --- (read-only)
---@field is_bot boolean --- (read-only)
---@field is_client_player boolean --- (read-only)
PlayerParameters = {}


---@class IInGameCar
---@field stat CarStats --- (read-only)
---@field grat integer --- (read-only)
---@field maxr integer --- (read-only)
---@field car_physics CarPhysics --- (read-only)
---@field control Control --- (read-only)
---@field current_checkpoint integer
---@field nlaps integer
---@field clear integer
---@field last_checkpoint_node integer
---@field placement integer
---@field wasted boolean --- (read-only)
---@field player PlayerParameters --- (read-only)
IInGameCar = {}


---@class Int3
---@operator eq(Int3, Int3):boolean
Int3 = {}


---@class Color3
---@field r integer
---@field g integer
---@field b integer
---@operator add(Color3, Color3):Color3
---@operator sub(Color3, Color3):Color3
Color3 = {}


---@class DeterministicRandom
DeterministicRandom = {}

---@param value fixed64
---@return DeterministicRandom
function DeterministicRandom.create(value) end
---@return integer
function DeterministicRandom:next() end
---@return fixed64
function DeterministicRandom:nextf64() end

