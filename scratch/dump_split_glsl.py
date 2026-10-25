"""Dumps the GLSL the sokol GLCORE split produces, and compiles it with glslangValidator.

verify_glsl_split.py proves the *layout* of the split is right by recomputing sokol's offsets. It
never asks a compiler whether the text is legal GLSL - which is the one thing the failing
GLCORE run says is wrong. This script reuses that file's transform (by exec'ing everything above
its reporting loop) and adds the compile step.

The rewritten source is what SokolShaderBindings.VertexSource/PixelSource hands sokol, so a
compile failure here is the same failure the game logs as GL_SHADER_COMPILATION_FAILED.
"""
import os
import re
import subprocess
import sys
import glob
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

# Everything in verify_glsl_split.py above its reporting loop is the transform, already reviewed
# and already agreeing with the C# - so reuse it rather than keeping a second copy in step.
source = open(os.path.join(HERE, 'verify_glsl_split.py'), encoding='utf-8').read()
cut = source.index('tot = fails = multi = 0')
ns = {}
exec(compile(source[:cut], 'verify_glsl_split.py', 'exec'), ns)
transform = ns['transform']

GLSLANG = r'C:\VulkanSDK\1.4.357.0\Bin\glslangValidator.exe'
outdir = os.path.join(HERE, 'glsl-split-out')
os.makedirs(outdir, exist_ok=True)

def compile_glsl(path, stage):
    """Returns (ok, output).

    With no target flag glslang parses and semantically checks the source and reports errors,
    which is what a GL driver does when it compiles. Deliberately *not* -G: that asks for SPIR-V
    for OpenGL, and SPIR-V's staging rules (a layout(location) on every non-opaque uniform) are
    requirements GLSL itself does not have. Validating with -G rejects shaders that a GL driver
    accepts, which is a false failure rather than a stricter check.
    """
    proc = subprocess.run(
        [GLSLANG, '-S', stage, path],
        capture_output=True, text=True, errors='replace')
    return proc.returncode == 0, (proc.stdout or '') + (proc.stderr or '')

failures = 0
tot = 0
for f in sorted(glob.glob(os.path.join(ROOT, 'nfm-world/obj/Release/net11.0/ShaderCompiler/*.g.cs'))):
    text = open(f, encoding='utf-8').read()
    name = os.path.basename(f)[:-5]
    arrs = re.findall(r'Uniforms = new UniformParam\[\]\s*\{(.*?)\n\s*\},', text, re.S)
    refl = [(m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)) for m in
            re.finditer(r'new\("([^"]+)",\s*(\d+),\s*(\d+),\s*UniformType\.(\w+)\)', arrs[0])] if arrs else []

    for stage, field, ext in (('Vertex', 'VertexGlsl', 'vert'), ('Pixel', 'PixelGlsl', 'frag')):
        m = re.search(r'private const string ' + field + r' =\s*\n"""(.*?)\n""";', text, re.S)
        if not m:
            continue
        tot += 1
        original = m.group(1)
        try:
            rewritten, blocks, block, extra = transform(original, refl, stage)
        except AssertionError as e:
            print('TRANSFORM FAIL %-22s %-7s %s' % (name, stage, e))
            failures += 1
            continue

        if rewritten is None:
            # No uniform block: the stage is handed to sokol unmodified, so compile that.
            rewritten = original
            groups = []
        else:
            groups = [len(b['m']) for b in blocks]

        path = os.path.join(outdir, '%s.%s.%s' % (name, stage.lower(), ext))
        open(path, 'w', encoding='utf-8').write(rewritten)

        ok, output = compile_glsl(path, ext)
        status = 'ok' if ok else 'COMPILE FAILED'
        print('%-8s %-22s %-7s groups=%-8s' % (status, name, stage, groups or '-'))
        if not ok:
            failures += 1
            for line in output.strip().splitlines():
                print('           ' + line)

print()
print('stages: %d  failures: %d  (rewritten sources in %s)'
      % (tot, failures, os.path.relpath(outdir, ROOT)))
sys.exit(1 if failures else 0)
