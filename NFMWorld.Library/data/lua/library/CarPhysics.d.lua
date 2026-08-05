---@class CarPhysicsInstance
---@field halted boolean
---@field btab boolean
---@field capcnt number
---@field capsized boolean
---@field caught NFMWorldLibrary_Util_UnlimitedArray_bool_Instance
---@field stat CarStatsInstance
---@field cn number
---@field cntdest number
---@field cntouch number
---@field colliding_with_client_player boolean
---@field _crank intArrayInstance
---@field _lcrank intArrayInstance
---@field cxz fixed64
---@field static_camera_xz fixed64
---@field dcnt number
---@field dcomp fixed64
---@field lcomp fixed64
---@field wasted boolean
---@field dominate NFMWorldLibrary_Util_UnlimitedArray_bool_Instance
---@field drag fixed64
---@field fixes number
---@field forca fixed64
---@field ftab boolean
---@field turn_xz fixed64
---@field gtouch boolean
---@field hitmag number
---@field im number
---@field lastcolido number
---@field loop number
---@field lxz fixed64
---@field mtouch boolean
---@field mxz fixed64
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
---@field power fixed64
---@field powerup fixed64
---@field pr boolean
---@field pu boolean
---@field pushed boolean
---@field pxy fixed64
---@field pzy fixed64
---@field rcomp fixed64
---@field rtab boolean
---@field scx NFMWorldLibrary_Util_LuaArray_FixedMathSharp_Fixed64_Instance
---@field scy NFMWorldLibrary_Util_LuaArray_FixedMathSharp_Fixed64_Instance
---@field scz NFMWorldLibrary_Util_LuaArray_FixedMathSharp_Fixed64_Instance
---@field shakedam number
---@field skid number
---@field speed fixed64
---@field roof_damage number
---@field surf_count number
---@field surfing boolean
---@field tilt fixed64
---@field total_stunt_xy fixed64
---@field total_stunt_xz fixed64
---@field total_stunt_zy fixed64
---@field tcnt number
---@field txz fixed64
---@field ucomp fixed64
---@field wtouch boolean
---@field xtpower number
---@field is_client_player boolean
---@field mtcount number
---@field py fixed64
CarPhysicsInstance = {}

---@class (exact) CarPhysics
---@field up fixed64vector3
---@field forward fixed64vector3
---@field right fixed64vector3
---@field _tickRate fixed64
---@field _oneOverTickRate fixed64

CarPhysics = {}

---Creates a new CarPhysics
---@param stat CarStatsInstance
---@param im number
---@param isClientPlayer boolean
---@return CarPhysicsInstance
function CarPhysics.new(stat, im, isClientPlayer) end

---@param carPhysics CarPhysicsInstance
---@param conto NFMWorldLibrary_ContOInstance
---@param bottomy fixed64
---@return number
function CarPhysics.getWheelGround(carPhysics, conto, bottomy) end

---@param carPhysics CarPhysicsInstance
---@param conto NFMWorldLibrary_ContOInstance
---@return fixed64
function CarPhysics.getBottomY(carPhysics, conto) end

---@param self CarPhysicsInstance
---@param self IInGameCarInstance
---@param othermad CarPhysicsInstance
---@param other IInGameCarInstance
function CarPhysicsInstance:collide(self, othermad, other) end

---@param self CarPhysicsInstance
---@param wi number
---@param conto NFMWorldLibrary_ContOInstance
---@param random FixedMathSharp_Utility_DeterministicRandomInstance
function CarPhysicsInstance:bounceRebound(wi, conto, random) end

---@param self CarPhysicsInstance
---@param control ControlInstance
---@param car IInGameCarInstance
---@param stage IStageInstance
function CarPhysicsInstance:drive(control, car, stage) end

---@param self CarPhysicsInstance
---@param wasMtouch boolean
---@return fixed64
function CarPhysicsInstance:getReboundMul(wasMtouch) end

---@param self CarPhysicsInstance
---@param i number
---@param f fixed64
---@param conto NFMWorldLibrary_ContOInstance
---@param random FixedMathSharp_Utility_DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regx(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param f fixed64
---@param conto NFMWorldLibrary_ContOInstance
---@param random FixedMathSharp_Utility_DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regy(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param f fixed64
---@param conto NFMWorldLibrary_ContOInstance
---@param random FixedMathSharp_Utility_DeterministicRandomInstance
---@return number
function CarPhysicsInstance:regz(i, f, conto, random) end

---@param self CarPhysicsInstance
---@param i number
---@param conto NFMWorldLibrary_ContOInstance
function CarPhysicsInstance:reseto(i, conto) end

---@param self CarPhysicsInstance
function CarPhysicsInstance:finishedFix() end

---@param self CarPhysicsInstance
---@param callback fun(sender: any, e: System_EventArgsInstance)
function CarPhysicsInstance:add_distruct(callback) end

---@param self CarPhysicsInstance
function CarPhysicsInstance:remove_distruct() end
