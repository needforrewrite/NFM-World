---@meta

---@class fixed64
---@field raw number -- The underlying raw long value cast to double
---@operator add(fixed64): fixed64
---@operator sub(fixed64): fixed64
---@operator mul(fixed64): fixed64
---@operator div(fixed64): fixed64
---@operator mod(fixed64): fixed64
---@operator unm(): fixed64

---@class fixed64vector3
---@field x fixed64
---@field y fixed64
---@field z fixed64
---@operator add(fixed64vector3): fixed64vector3
---@operator sub(fixed64vector3): fixed64vector3
---@operator mul(fixed64vector3): fixed64vector3        -- component-wise
---@operator div(fixed64vector3): fixed64vector3        -- component-wise
---@operator mul(fixed64): fixed64vector3       -- scalar multiplication
---@operator div(fixed64): fixed64vector3       -- scalar division
---@operator unm(): fixed64vector3

--- Creates a Fixed64 value.
---
--- Accepts a Lua number (double), a string (parsed via Fixed64.TryParse),
--- or an existing Fixed64 (returned as-is).
---
--- Cross-type arithmetic (Fixed64 + Number) is intentionally not supported —
--- convert both sides to the same type first.
---
---@param value number|string|fixed64
---@return fixed64
function fixed64(value) end

--- Creates a Fixed64Vector3 value.
---
--- Each argument is converted to Fixed64 (accepts both Lua numbers and Fixed64).
---
---@param x number|fixed64
---@param y number|fixed64
---@param z number|fixed64
---@return fixed64vector3
function fixed64vector3(x, y, z) end

fixed64vec3 = {}

--- Returns a normalized (unit-length) copy of the vector.
---@param v fixed64vector3
---@return fixed64vector3
function fixed64vec3.normalized(v) end

--- Returns the cross product of two vectors.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64vector3
function fixed64vec3.cross(a, b) end

--- Returns the dot product of two vectors.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64
function fixed64vec3.dot(a, b) end

--- Returns the Euclidean distance between two points.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64
function fixed64vec3.distance(a, b) end

--- Returns the squared Euclidean distance between two points.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64
function fixed64vec3.sqrdistance(a, b) end

--- Returns the magnitude (length) of the vector.
---@param v fixed64vector3
---@return fixed64
function fixed64vec3.magnitude(v) end

--- Returns the squared magnitude (length) of the vector.
---@param v fixed64vector3
---@return fixed64
function fixed64vec3.sqrmagnitude(v) end

--- Returns the component-wise maximum of two vectors.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64vector3
function fixed64vec3.max(a, b) end

--- Returns the component-wise minimum of two vectors.
---@param a fixed64vector3
---@param b fixed64vector3
---@return fixed64vector3
function fixed64vec3.min(a, b) end

--- Linearly interpolates between two vectors.
---@param a fixed64vector3
---@param b fixed64vector3
---@param t fixed64 -- interpolation factor (0 = a, 1 = b)
---@return fixed64vector3
function fixed64vec3.lerp(a, b, t) end

--- Returns the component-wise absolute value of the vector.
---@param v fixed64vector3
---@return fixed64vector3
function fixed64vec3.abs(v) end

--- Returns the component-wise sign of the vector (1, -1, or 0 per component).
---@param v fixed64vector3
---@return fixed64vector3
function fixed64vec3.sign(v) end

--- `type()` overloads for the new types.
---
--- type(fixed64(...))       → "fixed64"
--- type(fixed64vector3(...)) → "fixed64vector3"
---@overload fun(v: fixed64): "fixed64"
---@overload fun(v: fixed64vector3): "fixed64vector3"
function type(v) end

---@meta

---@class f64angle
---@field deg fixed64 -- Angle in degrees
---@field rad fixed64 -- Angle in radians
---@operator add(f64angle): f64angle
---@operator sub(f64angle): f64angle
---@operator mul(f64angle): f64angle
---@operator div(f64angle): f64angle
---@operator unm(): f64angle
---@operator lt(f64angle): boolean
---@operator le(f64angle): boolean
---@operator eq(f64angle): boolean

---@class f64euler
---@field yaw f64angle
---@field pitch f64angle
---@field roll f64angle
---@operator add(f64euler): f64euler
---@operator sub(f64euler): f64euler
---@operator unm(): f64euler
---@operator eq(f64euler): boolean
---@operator mul(f64angle): f64euler    -- scalar multiply (wrapped)
---@operator div(f64angle): f64euler    -- scalar divide (wrapped)

--- Creates an f64AngleSingle from degrees.
---
--- Accepts a Lua number, a Fixed64 (interpreted as degrees), a string,
--- or an existing f64angle (returned as-is).
---
--- Cross-type arithmetic (f64angle + Number) is intentionally not supported —
--- convert both sides to the same type first.
---
---@param value number|string|fixed64|f64angle
---@return f64angle
function f64angle(value) end

--- Creates an f64Euler from three angles or numbers.
---
--- Each argument is converted to f64AngleSingle:
--- numbers/Fixed64 are interpreted as degrees; existing f64angle values pass through.
---
---@param yaw number|fixed64|f64angle
---@param pitch number|fixed64|f64angle
---@param roll number|fixed64|f64angle
---@return f64euler
function f64euler(yaw, pitch, roll) end

f64anglelib = {}

--- Creates an f64AngleSingle from a radian value.
---@param radians fixed64
---@return f64angle
function f64anglelib.from_radians(radians) end

--- Creates an f64AngleSingle from a degree value.
---@param degrees fixed64
---@return f64angle
function f64anglelib.from_degrees(degrees) end

--- Wraps the angle to the range [-180°, 180°] ([-π, π] rad).
---@param a f64angle
---@return f64angle
function f64anglelib.wrap(a) end

--- Wraps the angle to the range [0°, 360°) ([0, 2π) rad).
---@param a f64angle
---@return f64angle
function f64anglelib.wrap_positive(a) end

--- Returns the smaller of two angles.
---@param a f64angle
---@param b f64angle
---@return f64angle
function f64anglelib.min(a, b) end

--- Returns the larger of two angles.
---@param a f64angle
---@param b f64angle
---@return f64angle
function f64anglelib.max(a, b) end

--- Returns the angle's value in degrees.
---@param a f64angle
---@return fixed64
function f64anglelib.degrees(a) end

--- Returns the angle's value in radians.
---@param a f64angle
---@return fixed64
function f64anglelib.radians(a) end

f64eulerlib = {}

--- Wraps each component (yaw, pitch, roll) to [-180°, 180°] ([-π, π] rad).
---@param e f64euler
---@return f64euler
function f64eulerlib.wrap(e) end

--- Wraps each component (yaw, pitch, roll) to [0°, 360°) ([0, 2π) rad).
---@param e f64euler
---@return f64euler
function f64eulerlib.wrap_positive(e) end

--- `type()` overloads for the new types.
---
--- type(f64angle(...))  → "f64angle"
--- type(f64euler(...))  → "f64euler"
---@overload fun(v: f64angle): "f64angle"
---@overload fun(v: f64euler): "f64euler"
function type(v) end

f64math = {}

--- The largest representable fixed64 value.
---@type fixed64
f64math.maxValue = nil

--- The smallest representable fixed64 value.
---@type fixed64
f64math.minValue = nil

--- pi (π).
---@type fixed64
f64math.pi = nil

--- Half of pi (π/2).
---@type fixed64
f64math.halfpi = nil

--- Two times pi (2π).
---@type fixed64
f64math.twopi = nil

--- Returns the sine of x (radians).
---@param x fixed64
---@return fixed64
function f64math.sin(x) end

--- Returns the cosine of x (radians).
---@param x fixed64
---@return fixed64
function f64math.cos(x) end

--- Returns the tangent of x (radians).
---@param x fixed64
---@return fixed64
function f64math.tan(x) end

--- Returns the arc-sine of x (radians).
---@param x fixed64
---@return fixed64
function f64math.asin(x) end

--- Returns the arc-cosine of x (radians).
---@param x fixed64
---@return fixed64
function f64math.acos(x) end

--- Returns the arc-tangent of x (radians).
---@param x fixed64
---@return fixed64
function f64math.atan(x) end

--- Returns the angle whose tangent is y / x (radians).
---@param y fixed64
---@param x fixed64
---@return fixed64
function f64math.atan2(y, x) end

--- Returns the square root of x.
---@param x fixed64
---@return fixed64
function f64math.sqrt(x) end

--- Returns b raised to the power e.
---@param b fixed64
---@param e fixed64
---@return fixed64
function f64math.pow(b, e) end

--- Returns the natural logarithm of x.
---@param x fixed64
---@return fixed64
function f64math.ln(x) end

--- Returns the base-2 logarithm of x.
---@param x fixed64
---@return fixed64
function f64math.log2(x) end

--- Returns the absolute value of x.
---@param x fixed64
---@return fixed64
function f64math.abs(x) end

--- Returns the largest integral value less than or equal to x.
---@param x fixed64
---@return fixed64
function f64math.floor(x) end

--- Returns the smallest integral value greater than or equal to x.
---@param x fixed64
---@return fixed64
function f64math.ceil(x) end

--- Rounds x to the nearest integral value (banker's rounding).
---@param x fixed64
---@return fixed64
function f64math.round(x) end

--- Returns the smaller of two values.
---@param a fixed64
---@param b fixed64
---@return fixed64
function f64math.min(a, b) end

--- Returns the larger of two values.
---@param a fixed64
---@param b fixed64
---@return fixed64
function f64math.max(a, b) end

--- Clamps v to the inclusive range [min, max].
---@param v fixed64
---@param min fixed64
---@param max fixed64
---@return fixed64
function f64math.clamp(v, min, max) end

--- Clamps v to the range [0, 1].
---@param v fixed64
---@return fixed64
function f64math.clamp01(v) end

--- Returns the sign of x (-1, 0, or 1).
---@param x fixed64
---@return fixed64
function f64math.sign(x) end

--- Linearly interpolates between a and b by t (0 = a, 1 = b).
---@param a fixed64
---@param b fixed64
---@param t fixed64
---@return fixed64
function f64math.lerp(a, b, t) end

--- Returns sqrt(a^2 + b^2).
---@param a fixed64
---@param b fixed64
---@return fixed64
function f64math.hypot(a, b) end

--- Converts degrees to radians.
---@param x fixed64
---@return fixed64
function f64math.deg2rad(x) end

--- Converts radians to degrees.
---@param x fixed64
---@return fixed64
function f64math.rad2deg(x) end

---@class RaceStandings {[integer]: Standing}

---@class Standing
---@field playerId string
---@field position integer
---@field finished boolean