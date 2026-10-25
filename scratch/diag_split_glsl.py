"""Reproduces the GLCORE shader-compilation failure offline, and checks the fix against it.

The failing run named undefined variables like `_325_View` and `_325_g1_HalfThickness` - names
carrying a group prefix the GLSL never declares - while the ones the reflection knows (`_325.View`)
appear nowhere in the message. The cause was `TryReadUniformDeclaration` reading the uniform's type
and never its name, so a block's instance was always the empty string. That made group 0's prefix
empty, declaring `_View` while the body said `_325_View`, and group 1's prefix `_g1`, declaring
`_g1_HalfThickness` while the body said `_325_g1_HalfThickness`. The body's names still looked
plausible because the reference keys degenerated to `.Member`, which matches inside `_325.Member`.

So the bug is reproduced by forcing the instance name to "", and the fix by leaving it as parsed.
Both are compiled with glslangValidator: the buggy run is expected to name exactly the variables the
game logged, and the fixed run to compile clean. The buggy run is the check on the diagnosis, not on
the C# - it confirms an empty instance name accounts for every name in the log rather than only the
first line of it.
"""
import glob
import os
import re
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

# The transform is the one in verify_glsl_split.py, up to its reporting loop.
source = open(os.path.join(HERE, 'verify_glsl_split.py'), encoding='utf-8').read()
cut = source.index('tot = fails = multi = 0')
ns = {}
exec(compile(source[:cut], 'verify_glsl_split.py', 'exec'), ns)
transform = ns['transform']
parse_block = ns['find_block']

GLSLANG = r'C:\VulkanSDK\1.4.357.0\Bin\glslangValidator.exe'
OUT = os.path.join(HERE, 'glsl-diag-out')
os.makedirs(OUT, exist_ok=True)

UNDECLARED = re.compile(r"'([^']+)' : (?:error: )?undeclared identifier")


def with_instance(forced):
    """`find_block`, with the block's instance name replaced by `forced`, or left as parsed when
    `forced` is None. Substituting None rather than skipping would write the string "None" into
    every prefix, which is a different bug that hides the one under test."""
    def find(src, stage):
        b = parse_block(src, stage)
        return b if b is None or forced is None else dict(b, inst=forced)
    return find


def compile_glsl(path, stage):
    """Plain parse and semantic check, with no target flag - see dump_split_glsl.py for why -G is
    the wrong check for a GL driver's compile."""
    p = subprocess.run([GLSLANG, '-S', stage, path], capture_output=True, text=True, errors='replace')
    return p.returncode == 0, (p.stdout or '') + (p.stderr or '')


for label, forced in (('buggy', ''), ('fixed', None)):
    ns['find_block'] = with_instance(forced)
    undeclared = set()
    failed = []
    stages = 0

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
            stages += 1
            try:
                res, blocks, b, extra = transform(m.group(1), refl, stage)
            except AssertionError as e:
                print('%s: TRANSFORM FAIL %s %s: %s' % (label, name, stage, e))
                failed.append(name + '.' + stage)
                continue
            if res is None:
                stages -= 1
                continue

            path = os.path.join(OUT, '%s.%s.%s.%s' % (label, name, stage.lower(), ext))
            open(path, 'w', encoding='utf-8').write(res)
            ok, output = compile_glsl(path, ext)
            if not ok:
                failed.append(name + '.' + stage)
                undeclared |= set(UNDECLARED.findall(output))

    print('%s: %d/%d stages failed' % (label, len(failed), stages))
    if failed:
        print('  %s' % ', '.join(failed[:8]))
    if undeclared:
        names = sorted(undeclared)
        print('  undeclared, %d distinct: %s' % (len(names), ', '.join(names[:12])))

print('\nrewritten sources in %s' % os.path.relpath(OUT, ROOT))
