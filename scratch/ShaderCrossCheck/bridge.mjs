#!/usr/bin/env node
// Apply the spirv-cross -> sokol-shdc transformations worked out in FINDINGS.md.
//
// Reads a pair of spirv-cross -V (Vulkan-flavoured GLSL) outputs and writes one
// sokol-shdc annotated .glsl program.
//
// Every transformation here is mechanical and lossless; none changes what the
// shader computes. They exist because spirv-cross emits Vulkan GLSL, whose
// descriptor-set model and stage-linking rules differ from what sokol-shdc's
// glslang front-end accepts.

import { readFileSync, writeFileSync } from "node:fs";
import { parseArgs } from "node:util";
import { basename } from "node:path";

const { values: opt } = parseArgs({
  options: {
    vs: { type: "string" },
    fs: { type: "string" },
    out: { type: "string" },
    "vs-block": { type: "string", default: "type_Globals" },
    "fs-block": { type: "string", default: "fs_Globals" },
    "vs-block-binding": { type: "string", default: "0" },
    "fs-block-binding": { type: "string", default: "1" },
    program: { type: "string", default: "shader" },
  },
});

for (const req of ["vs", "fs", "out"]) {
  if (!opt[req]) {
    console.error(`missing --${req}`);
    process.exit(1);
  }
}

// spirv-cross writes these ahead of the first real declaration; sokol-shdc
// supplies its own via the program header.
const DROP_LINES = new Set([
  "#version 450",
  "#extension GL_EXT_spirv_intrinsics : require",
]);

// Remove the `out gl_PerVertex { ... };` block: spirv-cross emits it to declare
// gl_Position's block, and glslang declares it implicitly, so the redeclaration
// is an error.
const dropGlPerVertex = (s) =>
  s.replace(/^[ \t]*out gl_PerVertex[ \t]*\{[^}]*\}[ \t]*;[ \t]*\n/gm, "");

const clean = (src) =>
  dropGlPerVertex(
    src
      .replace(/\r\n/g, "\n")
      .split("\n")
      .filter((l) => !DROP_LINES.has(l.trim()))
      .join("\n"),
  );

// `layout(set = N, binding = M)` -> `layout(binding = M)`. sokol_gfx has a single
// descriptor set, and DXC's output puts everything in set 0.
const dropSets = (s) => s.replace(/set[ \t]*=[ \t]*\d+[ \t]*,[ \t]*/g, "");

// sokol-shdc requires distinct uniform block names per stage -- the same name in
// both @vs and @fs is rejected outright ("conflicting uniform block definitions
// found for 'X'"). This renames the block declaration, its instance, and every
// `_Block.field` access in the body.
const renameBlock = (s, from, to) =>
  s
    .replace(new RegExp(`\\buniform[ \t]+${from}\\b`, "g"), `uniform ${to}`)
    .replace(new RegExp(`\\}[ \t]*_${from}[ \t]*;`, "g"), `} _${to};`)
    .split(`_${from}.`)
    .join(`_${to}.`);

// sokol-shdc links stages by varying NAME, not location. spirv-cross names the
// two sides `out_var_X` / `in_var_X`, which never match, so both become `vX`.
const renameVaryings = (s, prefix) =>
  s.replace(new RegExp(`\\b${prefix}_var_(\\w+)\\b`, "g"), "v$1");

// DXC puts both stages' blocks on the same binding, and sokol-shdc rejects two
// uniform blocks sharing one. Texture bindings are a separate namespace in its
// model, so only the blocks need moving. (sokol-shdc reassigns every slot in the
// generated header anyway; these numbers only have to be mutually distinct.)
const rebindBlock = (s, block, binding) =>
  s.replace(
    new RegExp(`(layout\\()[^)]*(binding[ \\t]*=[ \\t]*\\d+)[^)]*(\\)[ \\t]*uniform[ \\t]+${block}\\b)`),
    `$1binding = ${binding}$3`,
  );

const vs = rebindBlock(
  renameVaryings(dropSets(clean(readFileSync(opt.vs, "utf8"))), "out"),
  opt["vs-block"],
  opt["vs-block-binding"],
);
const fs = rebindBlock(
  renameVaryings(dropSets(clean(readFileSync(opt.fs, "utf8"))), "in"),
  opt["vs-block"],
  opt["fs-block-binding"],
);

// Last: dropSets leaves `binding = N` in place, and the block rename would
// otherwise also fire on `uniform texture2D ...` lines if names collided.
const fsFinal = renameBlock(fs, opt["vs-block"], opt["fs-block"]);

writeFileSync(
  opt.out,
  `@vs vs\n${vs}\n@end\n\n@fs fs\n${fsFinal}\n@end\n\n@program ${opt.program} vs fs\n`,
  "utf8",
);

console.error(`wrote ${opt.out} (program: ${opt.program})`);
