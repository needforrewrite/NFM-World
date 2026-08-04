---@class IInGameCarInstance : ICarInstance, ITransformInstance
---@field car_physics CarPhysicsInstance
---@field control ControlInstance
---@field current_checkpoint number
---@field nlaps number
---@field clear number
---@field last_checkpoint_node number
---@field placement number
---@field wasted boolean
---@field player PlayerParametersInstance
---@field rad Rad3dInstance
---@field stats CarStatsInstance
---@field groundAt number
---@field maxRadius number
---@field wheelAngle f64euler
---@field turningWheelAngle f64euler
---@field wheels Rad3dWheelDefInstance
---@field childTransforms ITransformInstance
---@field position fixed64vector3
---@field rotation f64euler
---@field parent ITransformInstance
IInGameCarInstance = {}

---@class (exact) IInGameCar

IInGameCar = {}

---@param self IInGameCarInstance
---@param wheelidx number
---@param x number
---@param y number
---@param z number
---@param scx number
---@param scz number
---@param simag number
---@param tilt number
---@param onRoof boolean
---@param wheelGround number
function IInGameCarInstance:addDust(wheelidx, x, y, z, scx, scz, simag, tilt, onRoof, wheelGround) end

---@param self IInGameCarInstance
---@param x number
---@param y number
---@param z number
---@param scx number
---@param scy number
---@param scz number
---@param type number
---@param wheelGround number
function IInGameCarInstance:spark(x, y, z, scx, scy, scz, type, wheelGround) end

---@param self IInGameCarInstance
---@param wheelnum number
---@param amount fixed64
function IInGameCarInstance:damageX(wheelnum, amount) end

---@param self IInGameCarInstance
---@param wheelnum number
---@param amount fixed64
---@param mtouch boolean
---@param nbsq number
---@param squash number
function IInGameCarInstance:damageY(wheelnum, amount, mtouch, nbsq, squash) end

---@param self IInGameCarInstance
---@param wheelnum number
---@param amount fixed64
function IInGameCarInstance:damageZ(wheelnum, amount) end

---@param self IInGameCarInstance
---@param stage IStageInstance
function IInGameCarInstance:drive(stage) end

---@param self IInGameCarInstance
---@param otherCar IInGameCarInstance
function IInGameCarInstance:collide(otherCar) end

---@param self IInGameCarInstance
function IInGameCarInstance:resetPosition() end

---@param self IInGameCarInstance
function IInGameCarInstance:fix() end
