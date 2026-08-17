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

---@class f64euler
---@field yaw f64angle
---@field pitch f64angle
---@field roll f64angle
---@operator add(f64euler): f64euler
---@operator sub(f64euler): f64euler
---@operator unm(): f64euler
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

---@class Standing
---@field playerId string
---@field position integer
---@field finished boolean

---@class RaceStandings: { [integer]: Standing }

Key = {
    -- Bit masks
    KeyCode   = 0x0000FFFF,
    Modifiers = 0xFFFF0000,

    None             = 0x00,
    LButton          = 0x01,
    RButton          = 0x02,
    Cancel           = 0x03,
    MButton          = 0x04,
    XButton1         = 0x05,
    XButton2         = 0x06,
    Back             = 0x08,
    Tab              = 0x09,
    LineFeed         = 0x0A,
    Clear            = 0x0C,
    Return           = 0x0D,
    Enter            = 0x0D,
    ShiftKey         = 0x10,
    ControlKey       = 0x11,
    Menu             = 0x12,
    Pause            = 0x13,
    Capital          = 0x14,
    CapsLock         = 0x14,
    KanaMode         = 0x15,
    HanguelMode      = 0x15,
    HangulMode       = 0x15,
    JunjaMode        = 0x17,
    FinalMode        = 0x18,
    HanjaMode        = 0x19,
    KanjiMode        = 0x19,
    Escape           = 0x1B,
    IMEConvert       = 0x1C,
    IMENonconvert    = 0x1D,
    IMEAccept        = 0x1E,
    IMEAceept        = 0x1E,
    IMEModeChange    = 0x1F,
    Space            = 0x20,
    Prior            = 0x21,
    PageUp           = 0x21,
    Next             = 0x22,
    PageDown         = 0x22,
    End              = 0x23,
    Home             = 0x24,
    Left             = 0x25,
    Up               = 0x26,
    Right            = 0x27,
    Down             = 0x28,
    Select           = 0x29,
    Print            = 0x2A,
    Execute          = 0x2B,
    Snapshot         = 0x2C,
    PrintScreen      = 0x2C,
    Insert           = 0x2D,
    Delete           = 0x2E,
    Help             = 0x2F,
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
    D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
    F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A,
    K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F,
    P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54,
    U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59,
    Z = 0x5A,
    LWin            = 0x5B,
    RWin            = 0x5C,
    Apps            = 0x5D,
    Sleep           = 0x5F,
    NumPad0         = 0x60, NumPad1 = 0x61, NumPad2 = 0x62,
    NumPad3         = 0x63, NumPad4 = 0x64, NumPad5 = 0x65,
    NumPad6         = 0x66, NumPad7 = 0x67, NumPad8 = 0x68,
    NumPad9         = 0x69,
    Multiply        = 0x6A,
    Add             = 0x6B,
    Separator       = 0x6C,
    Subtract        = 0x6D,
    Decimal         = 0x6E,
    Divide          = 0x6F,
    F1  = 0x70, F2  = 0x71, F3  = 0x72, F4  = 0x73,
    F5  = 0x74, F6  = 0x75, F7  = 0x76, F8  = 0x77,
    F9  = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,
    F13 = 0x7C, F14 = 0x7D, F15 = 0x7E, F16 = 0x7F,
    F17 = 0x80, F18 = 0x81, F19 = 0x82, F20 = 0x83,
    F21 = 0x84, F22 = 0x85, F23 = 0x86, F24 = 0x87,
    NumLock         = 0x90,
    Scroll          = 0x91,
    LShiftKey       = 0xA0,
    RShiftKey       = 0xA1,
    LControlKey     = 0xA2,
    RControlKey     = 0xA3,
    LMenu           = 0xA4,
    RMenu           = 0xA5,
    BrowserBack     = 0xA6,
    BrowserForward  = 0xA7,
    BrowserRefresh  = 0xA8,
    BrowserStop     = 0xA9,
    BrowserSearch   = 0xAA,
    BrowserFavorites= 0xAB,
    BrowserHome     = 0xAC,
    VolumeMute      = 0xAD,
    VolumeDown      = 0xAE,
    VolumeUp        = 0xAF,
    MediaNextTrack  = 0xB0,
    MediaPreviousTrack = 0xB1,
    MediaStop       = 0xB2,
    MediaPlayPause  = 0xB3,
    LaunchMail      = 0xB4,
    SelectMedia     = 0xB5,
    LaunchApplication1 = 0xB6,
    LaunchApplication2 = 0xB7,
    OemSemicolon    = 0xBA,
    Oem1            = 0xBA,
    Oemplus         = 0xBB,
    Oemcomma        = 0xBC,
    OemMinus        = 0xBD,
    OemPeriod       = 0xBE,
    OemQuestion     = 0xBF,
    Oem2            = 0xBF,
    Oem3            = 0xC0,
    Oemtilde        = 0xC0,
    OemOpenBrackets = 0xDB,
    Oem4            = 0xDB,
    OemPipe         = 0xDC,
    Oem5            = 0xDC,
    OemCloseBrackets= 0xDD,
    Oem6            = 0xDD,
    Oem7            = 0xDE,
    OemQuotes       = 0xDE,
    Oem8            = 0xDF,
    Oem102          = 0xE2,
    OemBackslash    = 0xE2,
    ProcessKey      = 0xE5,
    Packet          = 0xE7,
    Attn            = 0xF6,
    Crsel           = 0xF7,
    Exsel           = 0xF8,
    EraseEof        = 0xF9,
    Play            = 0xFA,
    Zoom            = 0xFB,
    NoName          = 0xFC,
    Pa1             = 0xFD,
    OemClear        = 0xFE,

    -- Modifier flags
    Shift   = 0x00010000,
    Control = 0x00020000,
    Alt     = 0x00040000,
}