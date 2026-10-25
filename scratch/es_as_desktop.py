"""Answers whether the desktop backend can run at GLSL 3.30, and why the shipped 4.1 form cannot.

Two questions, one run:

  - The desktop GLSL the compiler already emits is `#version 410`, and measured against glslang it
    fails *every* stage at 330 and 400 while passing at 410. So a 3.3 desktop backend cannot use
    that form as it stands, and it is worth knowing what it is that pins it to 4.1.
  - But GLSL ES 3.00 is itself derived from desktop 3.30, and the compiler emits an ES form as well.
    If the ES form compiles as `#version 330` with nothing but the version line changed, then the
    desktop backend needs no new compiler work and can stay at 3.3 - the more portable target, and
    the one that keeps a single shader model across ANGLE, desktop GL and (later) GLES platforms.

The ES form is the one `GlShaderProgram` compiles today, so a pass here is a real, shipped artifact
rather than a constructed one.
"""
import glob
import os
import re
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
GLSLANG = r'C:\VulkanSDK\1.4.357.0\Bin\glslangValidator.exe'
OUT = os.path.join(HERE, 'vercheck')
os.makedirs(OUT, exist_ok=True)


def compile_glsl(path, stage):
    p = subprocess.run([GLSLANG, '-S', stage, path], capture_output=True, text=True, errors='replace')
    return p.returncode == 0, (p.stdout or '') + (p.stderr or '')


def field(text, name):
    m = re.search(r'private const string ' + name + r' =\s*\n"""(.*?)\n""";', text, re.S)
    return m.group(1) if m else None


def rewrite_version(source, version):
    """Replaces the `#version` line, whatever sits on it (`300 es`, `410`)."""
    return re.sub(r'^#version [^\n]*', '#version ' + version, source, count=1, flags=re.M)


for label, source_field, target in (
    ('desktop form @ 330', 'VertexGlsl', '330'),
    ('ES form @ 330     ', 'VertexGlslEs', '330'),
):
    runs = []
    for f in sorted(glob.glob(os.path.join(ROOT, 'nfm-world/obj/Release/net11.0/ShaderCompiler/*.g.cs'))):
        text = open(f, encoding='utf-8').read()
        name = os.path.basename(f)[:-5]
        for sf, ext, stage in ((source_field, 'vert', 'vert'), (source_field.replace('Vertex', 'Pixel'), 'frag', 'frag')):
            src = field(text, sf)
            if src is None:
                continue
            src = rewrite_version(src, target)
            path = os.path.join(OUT, '%s.%s.%s' % (source_field, name, ext))
            open(path, 'w', encoding='utf-8').write(src)
            ok, output = compile_glsl(path, stage)
            runs.append((name + '.' + stage, ok, output))

    failed = [n for n, ok, _ in runs if not ok]
    print('%s : %d/%d ok' % (label, len(runs) - len(failed), len(runs)))
    if failed:
        sample = next((o for n, ok, o in runs if not ok), '')
        # The first few distinct diagnostics name what pins the form to its version.
        lines = [l.strip() for l in sample.splitlines() if 'error' in l.lower() or 'ERROR' in l]
        for line in lines[:6]:
            print('    ' + line)
        print('    (failing stages: %s%s)'
              % (', '.join(failed[:3]), ' ...' if len(failed) > 3 else ''))
    print()
