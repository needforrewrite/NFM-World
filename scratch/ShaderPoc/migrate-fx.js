// Migrates a D3D9-era FX/HLSL source tree to the D3D11/glslang dialect that the
// shader compiler consumes. Applied to *copies* under /tmp/fxmig so the repo's
// shader sources stay untouched. Not part of the shipped tool -- it exists to show
// that every remaining compile failure is a source-dialect issue, not a pipeline one.
//
//   node migrate-fx.js <dir>
//
// Rewrites:
//   texture NAME;                          -> Texture2D NAME : register(tN);
//   sampler NAME = sampler_state { ... };  -> SamplerState NAME : register(sN);
//   tex2D(SAMPLER, uv)                     -> TEXTURE.Sample(SAMPLER, uv)
//   f(..., in sampler S, ...)              -> f(..., in Texture2D T, in SamplerState S, ...)
//     ... and inserts T before S at every call site of f.
//
// D3D9 merged texture and sampler into one object, so the two have to be separated
// here. `Texture = <NAME>;` inside a sampler_state is what pairs them; that pairing is
// the only way to recover the texture operand of tex2D, and to know which texture to
// thread through a function that took a bare `sampler` parameter.

const fs = require("fs");
const path = require("path");

const dir = process.argv[2];
if (!dir) {
    console.error("usage: node migrate-fx.js <dir>");
    process.exit(1);
}

const files = fs.readdirSync(dir).filter(f => /\.(fx|fxh)$/i.test(f));
const read = f => fs.readFileSync(path.join(dir, f), "utf8");
const write = (f, s) => fs.writeFileSync(path.join(dir, f), s);

/** Splits an argument list on top-level commas (parens/brackets aware). */
function splitArgs(s) {
    const out = [];
    let depth = 0, start = 0;
    for (let i = 0; i < s.length; i++) {
        const c = s[i];
        if (c === "(" || c === "[" || c === "{") depth++;
        else if (c === ")" || c === "]" || c === "}") depth--;
        else if (c === "," && depth === 0) { out.push(s.slice(start, i)); start = i + 1; }
    }
    if (s.trim().length) out.push(s.slice(start));
    return out;
}

const pairBySampler = new Map();

// Pass 1: split texture/sampler declarations, assigning sequential registers.
for (const f of files) {
    let s = read(f);
    const pairs = [];
    let texIdx = 0, smpIdx = 0;

    // The sampler_state body is multi-line, so match across lines up to the closing
    // brace; there is no nesting inside, so a lazy match is safe here.
    s = s.replace(/sampler\s+(\w+)\s*=\s*sampler_state\s*\{([\s\S]*?)\}\s*;/g,
        (whole, name, body) => {
            const tm = body.match(/Texture\s*=\s*<\s*(\w+)\s*>/);
            if (tm) { pairs.push([name, tm[1]]); pairBySampler.set(name, tm[1]); }
            return `SamplerState ${name} : register(s${smpIdx++});`;
        });

    s = s.replace(/^([ \t]*)texture\s+(\w+)\s*;/gm,
        (whole, indent, name) => `${indent}Texture2D ${name} : register(t${texIdx++});`);

    write(f, s);
    if (pairs.length) console.log(`${f}: ${texIdx} textures, ${smpIdx} samplers`);
}

// Pass 2: functions that take a bare `sampler` parameter. For each, split the
// parameter into (texture, sampler) and remember it so call sites can be fixed up --
// splitting one parameter into two changes every call's arity.
//
// The function's parameter name (`shadowMapSampler`) is unrelated to what callers pass
// (`ShadowMapSampler0`), so call sites are matched by *ordinal*: the k-th sampler
// argument at the call corresponds to the k-th split parameter, whatever it's named.
const samplerParamFns = new Map(); // fnName -> sampler parameter count
const paramTex = new Map();        // parameter sampler name -> texture name, for the body

for (const f of files) {
    let s = read(f);
    const before = s;

    s = s.replace(/\b(\w+)\s*\(([^)]*)\)\s*(?=[{:])/g, (whole, fnName, args) => {
        if (!/\bin\s+sampler\b/.test(args)) return whole;
        const positions = [];
        const newArgs = splitArgs(args).map((a, i) => {
            const m = a.match(/^\s*in\s+sampler\s+(\w+)\s*$/);
            if (!m) return a;
            const sampler = m[1];
            const tex = pairBySampler.get(sampler) ?? sampler + "Texture";
            paramTex.set(sampler, tex);
            positions.push(i);
            return `${a.slice(0, a.indexOf("in"))}in Texture2D ${tex}, in SamplerState ${sampler}`;
        });
        samplerParamFns.set(fnName, positions);
        return `${fnName}(${newArgs.join(",")})`;
    });

    write(f, s);
    if (s !== before)
        console.log(`${f}: split params of ${[...samplerParamFns.keys()].join(", ")}`);
}

// Pass 3: insert the texture before each sampler argument at call sites of the
// functions rewritten in pass 2. Matched by ordinal, because the argument name at the
// call site is a global sampler (`ShadowMapSampler0`), not the parameter's own name.
// Argument splitting has to be paren-aware: the argument before the sampler is
// `LightViewProj0` but others are expressions.
for (const f of files) {
    if (!samplerParamFns.size) break;
    let s = read(f);
    let updated = 0;

    for (const [fnName, positions] of samplerParamFns) {
        const callRe = new RegExp(`\\b${fnName}\\s*\\(`, "g");
        let m;
        while ((m = callRe.exec(s)) !== null) {
            const open = m.index + m[0].length - 1;
            let depth = 0, close = -1;
            for (let i = open; i < s.length; i++) {
                if (s[i] === "(") depth++;
                else if (s[i] === ")" && --depth === 0) { close = i; break; }
            }
            if (close < 0) continue;

            // The declaration of `applyShadowingSingle` also matches `name(`, so skip
            // anything whose body follows immediately -- only statements are calls.
            const after = s.slice(close + 1).match(/^\s*./)?.[0].trim();
            if (after === "{" || after === ":") continue;

            const args = splitArgs(s.slice(open + 1, close));
            // Positions recorded on the *declaration*; expand for the arguments already
            // inserted to its left in this same call. A call passes a global sampler
            // object, so the argument must be a bare identifier naming a known sampler.
            let emitted = 0;
            for (const pos of positions) {
                const at = pos + emitted;
                if (at >= args.length) break;
                const bare = args[at].trim();
                if (!/^\w+$/.test(bare) || !pairBySampler.has(bare)) continue;
                args[at] = args[at].replace(bare, `${pairBySampler.get(bare)}, ${bare}`);
                emitted++;
            }
            if (!emitted) continue;

            const rebuilt = `${fnName}(${args.join(",")})`;
            s = s.slice(0, m.index) + rebuilt + s.slice(close + 1);
            callRe.lastIndex = m.index + rebuilt.length;
            updated++;
        }
    }

    write(f, s);
    if (updated) console.log(`${f}: ${updated} call sites updated`);
}

// Pass 4: tex2D -> .Sample(). Runs last so it sees the split parameter names. Any
// surviving tex2D is renamed to a marker token so the miss is loud rather than silent.
for (const f of files) {
    let s = read(f);
    const before = s;
    // Globals first, then the split parameters of applyShadowingSingle -- inside that
    // function the sampler is the parameter, not one of the global sampler objects.
    for (const [smp, tex] of [...pairBySampler, ...paramTex])
        s = s.replace(new RegExp(`tex2D\\s*\\(\\s*${smp}\\s*,`, "g"), `${tex}.Sample(${smp},`);
    s = s.replace(/\btex2D\s*\(/g, "UNCONVERTED_tex2D(");
    write(f, s);
    if (s !== before) {
        const misses = (s.match(/UNCONVERTED_tex2D\(/g) || []).length;
        console.log(`${f}: tex2D -> .Sample()${misses ? `  (${misses} UNCONVERTED)` : ""}`);
    }
}
