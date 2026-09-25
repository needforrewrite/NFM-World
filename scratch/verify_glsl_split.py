import re, glob, os, sys

ALIGN = {'float':4,'int':4,'uint':4,'vec2':8,'ivec2':8,'uvec2':8,
         'vec3':16,'vec4':16,'ivec3':16,'ivec4':16,'uvec3':16,'uvec4':16,'mat4':16}
SIZE  = {'float':4,'int':4,'uint':4,'vec2':8,'ivec2':8,'uvec2':8,
         'vec3':12,'ivec3':12,'uvec3':12,'vec4':16,'ivec4':16,'uvec4':16,'mat4':64}
UT    = {'Float':'FLOAT','Vector2':'FLOAT2','Vector3':'FLOAT3','Vector4':'FLOAT4',
         'Matrix4x4':'MAT4','Int':'INT'}
USZ   = {'Float':4,'Int':4,'Vector2':8,'Vector3':12,'Vector4':16,'Matrix4x4':64}

def isid(s):
    return bool(re.fullmatch(r'[A-Za-z_]\w*', s))

def align(v, a):
    return (v + a - 1) // a * a

def scan(src):
    structs = []; uniforms = []; off = 0; opened = -1
    for ln in src.split('\n'):
        ls = off; off += len(ln) + 1
        t = ln.strip(); c = ls + (len(ln) - len(ln.lstrip()))
        if opened >= 0:
            if t == '};':
                structs[opened]['end'] = c + len(t); opened = -1
            else:
                structs[opened]['body'].append((t, c))
            continue
        if t in ('', '{', '};'):
            continue
        if t.startswith('struct '):
            rest = t[7:].strip()
            r = rest[:-1].strip() if rest.endswith('{') else rest
            if not isid(r):
                continue
            structs.append(dict(name=r, start=ls, end=0, body=[])); opened = len(structs) - 1
            continue
        if not (t.startswith('uniform ') and t.endswith(';')):
            continue
        w = t[8:-1].split()
        if len(w) != 2 or not isid(w[0]) or not isid(w[1]):
            continue
        uniforms.append(dict(type=w[0], name=w[1], start=ls, end=c + len(t)))
    return structs, uniforms

def find_block(src, stage):
    structs, uniforms = scan(src)
    found = None; decl = None
    for u in uniforms:
        m = next((s for s in structs if s['name'] == u['type'] and s['start'] < u['start']), None)
        if m is None:
            continue
        if decl is not None:
            raise AssertionError('ambiguous block in ' + stage)
        found = m; decl = u
    if found is None:
        return None
    members = []
    for (line, o) in found['body']:
        if line in ('', '{'):
            continue
        if line.endswith(';'):
            w = line[:-1].split()
            if len(w) == 2 and isid(w[0]) and isid(w[1]):
                members.append((w[0], w[1])); continue
        raise AssertionError('unreadable member ' + repr(line) + ' in ' + found['name'])
    assert members, 'no members'
    return dict(name=found['name'], inst=decl['name'], members=members,
                si=found['start'], sl=found['end'] - found['start'],
                ui=decl['start'], ul=decl['end'] - decl['start'])

def transform(src, refl, stage):
    b = find_block(src, stage)
    if not b:
        return None, None, None, None
    R = {n: (o, t) for n, o, s, t in refl}
    N = len(b['members']); starts = [0] * N; ext = 0
    for i, (ty, nm) in enumerate(b['members']):
        ext = align(ext, ALIGN[ty]); starts[i] = ext; ext += SIZE[ty]
    for i, (ty, nm) in enumerate(b['members']):
        o, t = R[nm]
        assert starts[i] == o and SIZE[ty] == USZ[t], (
            'layout %s.%s got %d/%d want %d/%d' % (b['name'], nm, starts[i], SIZE[ty], o, USZ[t]))
    groups = []; st = 0
    while st < N:
        lim = min(st + 16, N); gs = starts[st]; end = 0
        for cand in range(lim, st, -1):
            if cand == N or (starts[cand] - gs) % 16 == 0:
                end = cand; break
        assert end, 'no 16-aligned boundary at member %d' % st
        groups.append((st, end)); st = end
    blocks = []; refs = {}
    for g, (a, z) in enumerate(groups):
        inst = b['inst'] if g == 0 else '%s_g%d' % (b['inst'], g)
        ms = []
        for i in range(a, z):
            nm = b['members'][i][1]; q = '%s_%s' % (inst, nm)
            ms.append((nm, UT[R[nm][1]], q))
            refs['%s.%s' % (b['inst'], nm)] = q
        ge = ext if z == N else starts[z]
        blocks.append(dict(off=starts[a], size=align(ge - starts[a], 16), m=ms))
    out = src
    for f, t in refs.items():
        out = out.replace(f, t)
    decl = ''
    for g, (a, z) in enumerate(groups):
        inst = b['inst'] if g == 0 else '%s_g%d' % (b['inst'], g)
        decl += ''.join('uniform %s %s_%s;\n' % (b['members'][i][0], inst, b['members'][i][1])
                        for i in range(a, z))
    text = out[:b['ui']] + out[b['ui'] + b['ul']:]
    return text[:b['si']] + decl + text[b['si'] + b['sl']:], blocks, b, (groups, starts, ext)

tot = fails = multi = 0
for f in sorted(glob.glob('nfm-world/obj/Release/net11.0/ShaderCompiler/*.g.cs')):
    s = open(f, encoding='utf-8').read(); name = os.path.basename(f)[:-5]
    arrs = re.findall(r'Uniforms = new UniformParam\[\]\s*\{(.*?)\n\s*\},', s, re.S)
    refl = [(m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)) for m in
            re.finditer(r'new\("([^"]+)",\s*(\d+),\s*(\d+),\s*UniformType\.(\w+)\)', arrs[0])] if arrs else []
    for stage, mn in (('Vertex', 'VertexGlsl'), ('Pixel', 'PixelGlsl')):
        m = re.search(r'private const string ' + mn + r' =\s*\n"""(.*?)\n""";', s, re.S)
        if not m:
            continue
        src = m.group(1); tot += 1
        try:
            res, blocks, b, extra = transform(src, refl, stage)
        except AssertionError as e:
            print('FAIL %-22s %-7s %s' % (name, stage, e)); fails += 1; continue
        if res is None:
            print('  -- %-22s %-7s NO BLOCK' % (name, stage)); continue
        groups, starts, ext = extra
        gs = [len(x['m']) for x in blocks]
        assert all(g <= 16 for g in gs), 'a group exceeds 16 members'

        # the rewritten source must declare one loose uniform per member of the whole block, under
        # the group prefix, and nothing that still looks like a struct block. The samplers the
        # stage declares separately are not block members and are left alone, so only the names
        # carrying a group prefix are compared.
        rs, ru = scan(res)
        assert not rs, 'rewritten source still declares structs: %s' % [x['name'] for x in rs]
        want = []
        for g, (a, z) in enumerate(groups):
            inst = b['inst'] if g == 0 else '%s_g%d' % (b['inst'], g)
            want += ['%s_%s' % (inst, b['members'][i][1]) for i in range(a, z)]
        got = [u['name'] for u in ru if u['name'].startswith(b['inst'])]
        assert sorted(got) == sorted(want), (
            'loose uniforms %s vs %s' % (sorted(got), sorted(want)))
        assert len(set(want)) == len(want), 'duplicate loose uniform names'

        # sokol assigns offsets itself: recompute them per group and require they equal the
        # absolute offsets minus the group start, which is what makes the split uploadable
        for g, (a, z) in enumerate(groups):
            run = 0
            for i in range(a, z):
                ty = b['members'][i][0]
                run = align(run, ALIGN[ty])
                assert run == starts[i] - starts[a], (
                    'group %d %s: sokol says %d, buffer says %d' % (g, b['members'][i][1], run, starts[i] - starts[a]))
                run += SIZE[ty]
            assert align(run, 16) == blocks[g]['size'], (
                'group %d size: sokol %d vs declared %d' % (g, align(run, 16), blocks[g]['size']))

        if len(blocks) > 1:
            multi += 1
        print('  ok %-22s %-7s members=%-3d groups=%-11s sizes=%s' % (name, stage, len(refl), gs, [x['size'] for x in blocks]))

print('\nstages: %d  failures: %d  split stages: %d' % (tot, fails, multi))
