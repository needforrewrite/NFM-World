---@class CarPhysicsInstance
---@field halted boolean
---@field btab boolean
---@field capcnt number
---@field capsized boolean
---@field caught UnlimitedArray_boolInstance
---@field stat CarStatsInstance
---@field cn number
---@field cntdest number
---@field cntouch number
---@field colliding_with_client_player boolean
---@field _crank intArrayInstance
---@field _lcrank intArrayInstance
---@field cxz Fixed64
---@field static_camera_xz Fixed64
---@field dcnt number
---@field dcomp Fixed64
---@field lcomp Fixed64
---@field wasted boolean
---@field dominate UnlimitedArray_boolInstance
---@field drag Fixed64
---@field fixes number
---@field forca Fixed64
---@field ftab boolean
---@field turn_xz Fixed64
---@field gtouch boolean
---@field hitmag number
---@field im number
---@field lastcolido number
---@field loop number
---@field lxz Fixed64
---@field mtouch boolean
---@field mxz Fixed64
---@field num_roof_damage number
---@field newcar boolean
---@field newedcar number
---@field nmlt number
---@field nofocus boolean
---@field outshakedam number
---@field pd boolean
---@field pl boolean
---@field pmlt number
---@field point number
---@field power Fixed64
---@field powerup Fixed64
---@field pr boolean
---@field pu boolean
---@field pushed boolean
---@field pxy Fixed64
---@field pzy Fixed64
---@field rcomp Fixed64
---@field rtab boolean
---@field scx Fixed64Instance
---@field scy Fixed64Instance
---@field scz Fixed64Instance
---@field shakedam number
---@field skid number
---@field speed Fixed64
---@field roof_damage number
---@field surf_count number
---@field surfing boolean
---@field tilt Fixed64
---@field total_stunt_xy Fixed64
---@field total_stunt_xz Fixed64
---@field total_stunt_zy Fixed64
---@field tcnt number
---@field txz Fixed64
---@field ucomp Fixed64
---@field wtouch boolean
---@field xtpower number
---@field is_client_player boolean
---@field mtcount number
---@field py Fixed64
CarPhysicsInstance = {}

---@class (exact) CarPhysics
---@field up Vector3d
---@field forward Vector3d
---@field right Vector3d
---@field _tickRate Fixed64
---@field _oneOverTickRate Fixed64

---Creates a new CarPhysics
---@param stat CarStatsInstance
---@param im number
---@param isClientPlayer boolean
---@return CarPhysicsInstance
function CarPhysics.new(stat, im, isClientPlayer) end

---@param carPhysics CarPhysicsInstance
---@param conto ContOInstance
---@param bottomy Fixed64
---@return number
function CarPhysics.getWheelGround(carPhysics, conto, bottomy) end

---@param carPhysics CarPhysicsInstance
---@param conto ContOInstance
---@return Fixed64
function CarPhysics.getBottomY(carPhysics, conto) end

---@param self CarPhysicsInstance
---@param self IInGameCarInstance
---@param othermad CarPhysicsInstance
---@param other IInGameCarInstance
function CarPhysicsInstance:collide(self, othermad, other) end

---@param self CarPhysicsInstance
---@param wi number
---@param conto ContOInstance
---@param random DeterministicRandomInstance
function CarPhysicsInstance:bounceRebound(wi, conto, random) end

---@param self CarPhysicsInstance
---@param control ControlInstance
---@param car IInGameCarInstance
---@param stage IStageInstance
function CarPhysicsInstance:drive(control, car, stage) end

---@param self CarPhysicsInstance
---@param wasMtouch boolean
---@return Fixed64
function CarPhysicsInstance:getReboundMul(wasMtouch) end

---@param self CarPhysicsInstance
---@param i number
---@param f Fixed64
---@param conto ContOInstance
---@param random DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regx(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param f Fixed64
---@param conto ContOInstance
---@param random DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regy(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param f Fixed64
---@param conto ContOInstance
---@param random DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regz(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param conto ContOInstance
function CarPhysicsInstance:reseto(i, conto) end

---@param self CarPhysicsInstance
function CarPhysicsInstance:finishedFix() end

---@param self CarPhysicsInstance
---@param callback fun(sender: any, e: EventArgsInstance)
function CarPhysicsInstance:add_distruct(callback) end

---@param self CarPhysicsInstance
function CarPhysicsInstance:remove_distruct() end
