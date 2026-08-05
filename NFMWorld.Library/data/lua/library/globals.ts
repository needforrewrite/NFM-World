declare class fixed64 {
    /**
     * The underlying raw long value cast to double
     */
    raw: number;
}
declare function fixed64(value: number | string | fixed64): fixed64;

declare class fixed64vector3 {
    /**
     * The underlying raw long value cast to double
     */
    x: fixed64;
    /**
     * The underlying raw long value cast to double
     */
    y: fixed64;
    /**
     * The underlying raw long value cast to double
     */
    z: fixed64;
}
declare function fixed64vector3(x: number | fixed64, y: number | fixed64, z: number | fixed64): fixed64vector3;
declare class fixed64vec3 {
    static normalized(v: fixed64vector3): fixed64vector3;
    static cross(a: fixed64vector3, b: fixed64vector3): fixed64vector3;
    static dot(a: fixed64vector3, b: fixed64vector3): fixed64;
    static distance(a: fixed64vector3, b: fixed64vector3): fixed64;
    static sqrdistance(a: fixed64vector3, b: fixed64vector3): fixed64;
    static magnitude(v: fixed64vector3): fixed64;
    static sqrmagnitude(v: fixed64vector3): fixed64;
    static max(a: fixed64vector3, b: fixed64vector3): fixed64vector3;
    static min(a: fixed64vector3, b: fixed64vector3): fixed64vector3;
    static lerp(a: fixed64vector3, b: fixed64vector3, t: fixed64): fixed64vector3;
    static abs(v: fixed64vector3): fixed64vector3;
    static sign(v: fixed64vector3): fixed64vector3;
}

declare function type(v: fixed64): 'fixed64';
declare function type(v: fixed64vector3): 'fixed64vector3';
declare function type(v: f64angle): 'f64angle';
declare function type(v: f64euler): 'f64euler';

declare class f64angle {
    deg: fixed64;
    rad: fixed64;
}
declare function f64angle(value: number | string | fixed64 | f64angle): f64angle;

declare class f64anglelib {
    static from_radians(radians: fixed64): f64angle;
    static from_degrees(degrees: fixed64): f64angle;
    static wrap(a: f64angle): f64angle;
    static wrap_positive(a: f64angle): f64angle;
    static min(a: f64angle, b: f64angle): f64angle;
    static max(a: f64angle, b: f64angle): f64angle;
    static degrees(a: f64angle): fixed64;
    static radians(a: f64angle): fixed64;
}

declare class f64euler {
    yaw: f64angle;
    pitch: f64angle;
    roll: f64angle;
}
declare function f64euler(yaw: number | fixed64 | f64angle, pitch: number | fixed64 | f64angle, roll: number | fixed64 | f64angle): f64euler;

declare class f64eulerlib {
    static wrap(e: f64euler): f64euler;
    static wrap_positive(e: f64euler): f64euler;
}
