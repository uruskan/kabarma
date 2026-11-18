"""Bitki hatti: agaclar ve sus bitkileri, dokulu (PBR) kaynaklardan oyun modeline (bitki-pbr akisi).

Calistirma (Blender 5.3, bassiz; kredi harcamaz):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_bitki.py -- [--only agac,cali] [--manifest pipeline/bitki_manifest.json] [--no_render]

Kaynaklar F:\\Assets'ten (salt okunur) ana kopyanin .tools/bitki_src/ klasorune bir kez acilir; tekrar kosturulabilir.
Her varlik icin cikti game/assets/models/<ad>/: <ad>.glb (tek mesh dugumu, en cok 3 yuzey), <ad>_lod1.glb,
<ad>_lod2.glb, <ad>_hull.tres (ConvexPolygonShape3D; agacta yalniz govde), <ad>_icon.png, <ad>.report.json;
inceleme goruntusu progress/bitki/render/<ad>.png.

Turler (manifest "kind"):
  tree : Geometry Nodes ile yapraklanan agac (Poly Haven). Yaprak/dal kumesi ornekleri (instance) tek tek karta
         donusur: her kume prototipi ustten ortografik olarak bir atlas karesine pisirilir (albedo + alfa, teget uzayi
         normal; kenar tasmasi ile mip halesi olmaz), her ornek kendi donusumuyle bir dortgen olur. Ornekler
         butceye gore seyreltilir, kalanlar kapsamayi korumak icin buyutulur. Kart normalleri tac hacmine (elipsoit)
         egilir: tac tek bir hacim gibi golgelenir. Govde ve dallar seyreltilir (decimate).
  plant: hazir mesh (glb/gltf/blend); parcalar (kume) birlestirilir, yukari eksen duzeltilir, boya oturtulur.
Ortak: taban y=0, orta x=z=0, hedef boy; dokular 1024 / 512 / 256 (LOD0/1/2); yaprak malzemesi alfa makasi (MASK),
iki yuzlu; renk ayari (HSV) dokuyu korur.
"""
import bpy, bmesh, sys, os, json, math, zipfile, fnmatch, random, subprocess, shutil, tempfile
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

MANIFEST = arg('--manifest', os.path.join(HERE, 'bitki_manifest.json'))
ONLY = [s for s in str(arg('--only', '')).split(',') if s]
NO_RENDER = bool(arg('--no_render', False))


def main_checkout():
    try:
        common = subprocess.check_output(['git', '-C', ROOT, 'rev-parse', '--git-common-dir'], text=True).strip()
        if not os.path.isabs(common):
            common = os.path.join(ROOT, common)
        return os.path.dirname(os.path.abspath(common))
    except Exception:
        return ROOT


CACHE = arg('--cache', os.path.join(main_checkout(), '.tools', 'bitki_src'))
TMP = tempfile.mkdtemp(prefix='bitki_')
PREFIXES = set()
LOG = []


def log(*a):
    s = ' '.join(str(x) for x in a)
    print('BITKI', s, flush=True)
    LOG.append(s)


# ---------------------------------------------------------------- kaynak

def source_dir(src):
    """Kaynak klasoru: zip ise .tools/bitki_src/<ad>/ altina (bir kez) acilir; klasor/dosya ise oldugu yer."""
    if 'zip' in src:
        name = src.get('cache', os.path.splitext(os.path.basename(src['zip']))[0].replace('.blend', ''))
        out = os.path.join(CACHE, name)
        z = zipfile.ZipFile(src['zip'])
        for n in z.namelist():
            if any(fnmatch.fnmatch(n, p) for p in src.get('members', ['*'])):
                if not os.path.exists(os.path.join(out, n)):
                    z.extract(n, out)
        return out
    return src['dir'].replace('{main}', main_checkout())


def tex_path(base, p):
    if not p:
        return None
    p = p.split('|')[0]
    full = p if os.path.isabs(p) else os.path.join(base, p)
    if not os.path.exists(full):
        raise FileNotFoundError(full)
    return full


# ---------------------------------------------------------------- goruntu

def load_px(path, size, raw=True):
    img = bpy.data.images.load(path, check_existing=False)
    if raw:
        img.colorspace_settings.name = 'Non-Color'
    if tuple(img.size) != (size, size):
        img.scale(size, size)
    a = np.empty(size * size * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(size, size, 4)


def channel(spec, base, size):
    """'dosya.png' -> RGBA; 'dosya.png|G' -> tek kanal (H, W)."""
    path = tex_path(base, spec)
    a = load_px(path, size)
    if '|' in spec:
        return a[:, :, 'RGBA'.index(spec.split('|')[1])]
    return a


def save_png(a, path, colorspace='sRGB'):
    h, w = a.shape[:2]
    if a.ndim == 2:
        a = np.dstack([a, a, a, np.ones_like(a)])
    name = os.path.splitext(os.path.basename(path))[0]
    for pre in sorted(PREFIXES, key=len, reverse=True):  # Godot dosya adi <glb>_<goruntu>: on ek tekrarlanmasin
        if name.startswith(pre + '_'):
            name = name[len(pre) + 1:]
            break
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.clip(a, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


def resize(a, size):
    """Kare dizi boyutlandirma (alan ortalamasi; yalniz kucultme 2'nin kuvveti)."""
    while a.shape[0] > size:
        a = 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])
    return a


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, 12.92 * c, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def dilate(rgba, iters=24):
    """Saydam piksellere en yakin opak rengi tasir (mip halesi olmasin). rgba duz (premultiply degil)."""
    a = (rgba[:, :, 3] > 0.5).astype(np.float32)
    c = rgba[:, :, :3] * a[:, :, None]
    w = a.copy()
    for _ in range(iters):
        if w.min() > 0:
            break
        pc = np.pad(c, ((1, 1), (1, 1), (0, 0)), mode='edge')
        pw = np.pad(w, 1, mode='edge')
        sc = sum(pc[1 + dy:pc.shape[0] - 1 + dy, 1 + dx:pc.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        sw = sum(pw[1 + dy:pw.shape[0] - 1 + dy, 1 + dx:pw.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        fill = (w == 0) & (sw > 0)
        c[fill] = sc[fill] / sw[fill][:, None]
        w = np.where(fill, 1.0, w)
    out = rgba.copy()
    hole = a == 0
    out[:, :, :3][hole] = c[hole]
    return out


def hsv_adjust(rgb, hue=0.0, sat=1.0, val=1.0, tint=None):
    """sRGB renk ayari: ton kaydirma (tur), doygunluk ve parlaklik carpani; tint carpimi. Doku korunur."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.max(rgb, -1); mn = np.min(rgb, -1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rc = np.where(m, (mx - r) / np.where(m, d, 1), 0); gc = np.where(m, (mx - g) / np.where(m, d, 1), 0); bc = np.where(m, (mx - b) / np.where(m, d, 1), 0)
    h = np.where(r == mx, bc - gc, np.where(g == mx, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(m, (h / 6.0) % 1.0, 0)
    s = np.where(mx > 1e-6, d / np.where(mx > 1e-6, mx, 1), 0)
    v = mx
    h = (h + hue) % 1.0; s = np.clip(s * sat, 0, 1); v = np.clip(v * val, 0, 1)
    i = np.floor(h * 6).astype(int) % 6; f = h * 6 - np.floor(h * 6)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros_like(rgb)
    for k, (R, G, B) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k
        out[..., 0][sel] = R[sel]; out[..., 1][sel] = G[sel]; out[..., 2][sel] = B[sel]
    if tint:
        out = out * np.array(tint[:3], np.float32)
    return np.clip(out, 0, 1)


def hue_sat(rgb):
    """(..., 3) -> ton (derece 0-360), doygunluk."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1); mn = rgb.min(-1); d = np.maximum(mx - mn, 1e-6)
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return h, (mx - mn) / np.maximum(mx, 1e-6)


def hue_map(a, maps):
    """Secici ton kaydirma: [{from: [lo, hi] derece, to: derece, sat: carpan, val: carpan, feather: derece}].
    Yalniz araliktaki tonlar hedefe kayar (kenarda yumusak gecis); diger renkler (yosun, kabuk) aynen kalir."""
    if not maps:
        return a
    a = a.copy()
    rgb = a[..., :3]
    for m in maps:
        h, s = hue_sat(rgb)
        v = rgb.max(-1)
        lo, hi = m['from']; fe = m.get('feather', 12.0)
        w = np.clip(np.minimum(h - (lo - fe), (hi + fe) - h) / fe, 0, 1) * np.clip(s / 0.15, 0, 1)
        nh = (h + (m['to'] - (lo + hi) / 2) * w) % 360 if m.get('shift', True) else h * (1 - w) + m['to'] * w
        ns = np.clip(s * (1 - w + w * m.get('sat', 1.0)), 0, 1)
        nv = np.clip(v * (1 - w + w * m.get('val', 1.0)), 0, 1)
        i = np.floor(nh / 60).astype(int) % 6; f = nh / 60 - np.floor(nh / 60)
        p = nv * (1 - ns); q = nv * (1 - ns * f); t = nv * (1 - ns * (1 - f))
        out = np.zeros_like(rgb)
        for k, (R, G, B) in enumerate([(nv, t, p), (q, nv, p), (p, nv, t), (p, q, nv), (t, p, nv), (nv, p, q)]):
            sel = i == k
            out[..., 0][sel] = R[sel]; out[..., 1][sel] = G[sel]; out[..., 2][sel] = B[sel]
        rgb = out
    a[..., :3] = rgb
    return a


def grade(a, g):
    """Manifest 'grade': {hue, sat, val, tint, hue_map}. Yalniz RGB; alfa korunur."""
    if not g:
        return a
    a = hue_map(a, g.get('hue_map'))
    a = a.copy()
    a[:, :, :3] = hsv_adjust(a[:, :, :3], g.get('hue', 0.0), g.get('sat', 1.0), g.get('val', 1.0), g.get('tint'))
    return a


# ---------------------------------------------------------------- malzeme

def make_material(name, albedo, normal, rough, clip, roughness_value=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get('Principled BSDF')
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = albedo
    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
    if clip:
        r = nt.nodes.new('ShaderNodeMath'); r.operation = 'ROUND'
        nt.links.new(t.outputs['Alpha'], r.inputs[0])
        nt.links.new(r.outputs[0], bsdf.inputs['Alpha'])
        try:
            m.surface_render_method = 'DITHERED'
        except Exception:
            pass
    m.use_backface_culling = not clip
    if normal is not None:
        normal.colorspace_settings.name = 'Non-Color'
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = normal
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if rough is not None:
        rough.colorspace_settings.name = 'Non-Color'
        tr = nt.nodes.new('ShaderNodeTexImage'); tr.image = rough
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tr.outputs['Color'], sep.inputs['Color'])
        nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
    else:
        bsdf.inputs['Roughness'].default_value = roughness_value
    bsdf.inputs['Specular IOR Level'].default_value = 0.35
    return m


def texture_set(mname, spec, base, size, outdir, prefix, grade_spec=None):
    """Malzeme dokulari: albedo(+alfa), normal, puruzluluk -> PNG (size) ve Blender goruntuleri."""
    alb = channel(spec['albedo'], base, size).copy()
    if spec.get('alpha'):
        al = channel(spec['alpha'], base, size)
        alb[:, :, 3] = al if al.ndim == 2 else al[:, :, 0]
    clip = bool(spec.get('clip', bool(spec.get('alpha')) or spec.get('albedo_alpha', False)))
    if not clip:
        alb[:, :, 3] = 1.0
    else:
        alb = dilate(alb)
    alb = grade(alb, grade_spec or spec.get('grade'))
    imgs = {'albedo': save_png(alb, os.path.join(TMP, f'{prefix}_{mname}_albedo.png'))}
    imgs['normal'] = None
    if spec.get('normal'):
        n = channel(spec['normal'], base, size).copy()
        if spec.get('normal_flip_g'):
            n[:, :, 1] = 1.0 - n[:, :, 1]
        n[:, :, 3] = 1
        imgs['normal'] = save_png(n, os.path.join(TMP, f'{prefix}_{mname}_normal.png'), 'Non-Color')
    imgs['rough'] = None
    if spec.get('rough'):
        r = channel(spec['rough'], base, size)
        if r.ndim == 3:
            r = r[:, :, 0]
        r = np.clip(r * spec.get('rough_mul', 1.0), 0.05, 1.0)
        imgs['rough'] = save_png(r, os.path.join(TMP, f'{prefix}_{mname}_rough.png'), 'Non-Color')
    return imgs, clip


# ---------------------------------------------------------------- mesh yardimcilari

def tris_of(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def activate(objs):
    bpy.ops.object.select_all(action='DESELECT') if bpy.context.view_layer.objects else None
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def cull_islands(obj, target):
    """Ince dal adaciklari: en kisa (sinir kutusu kosegeni) adaciklar silinir, ucgen sayisi hedefe inene dek.
    Collapse decimate kopuk ince tuplerde durur; once kucukler gider, govde ve ana dallar kalir."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    seen = set(); islands = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]; seen.add(f.index); faces = []
        while stack:
            g = stack.pop(); faces.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        co = [v.co for g in faces for v in g.verts]
        mn = Vector(map(min, *co)) if len(co) > 1 else co[0]
        mx = Vector(map(max, *co)) if len(co) > 1 else co[0]
        islands.append(((mx - mn).length, faces))
    islands.sort(key=lambda x: x[0])
    total = sum(len(g.verts) - 2 for g in bm.faces)
    kill = []
    for size, faces in islands[:-1]:
        if total <= target:
            break
        kill += faces
        total -= sum(len(g.verts) - 2 for g in faces)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(obj.data); bm.free()
    log('adacik', len(islands), 'silinen', len(kill), 'kalan ucgen', total)


def apply_decimate(obj, target, solid=False):
    """solid: kapali stilize mesh (Tripo): adacik ve ince yuz silme yok (delik acar), yalniz collapse."""
    t = tris_of(obj.data)
    if t <= target or target <= 0:
        return
    if t > target * 2 and not solid:
        cull_islands(obj, int(target * 1.6))
        t = tris_of(obj.data)
    # UV dikisleri ve acik kenarlar collapse'i durdurur: once yakin koseler kaynar
    bm = bmesh.new(); bm.from_mesh(obj.data)
    mn, mx = bbox([obj])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=(mx - mn).length * 2e-4)
    bm.to_mesh(obj.data); bm.free()
    for _ in range(4 if solid else 3):
        t = tris_of(obj.data)
        if t <= target * 1.05:
            return
        md = obj.modifiers.new('dec', 'DECIMATE')
        md.ratio = max(0.002, target / t)
        md.use_collapse_triangulate = True
        activate([obj])
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_apply(modifier=md.name)
        if tris_of(obj.data) > t * 0.95:
            break
    # Ince tup dallar collapse'ta takilir (kesit 3-4 kenara inince devam etmez). En ince halkali yuzler (en kisa
    # kenari en kucuk) silinir: ince ucler gider, yapraklar zaten ortuyor; kalin govde ve ana dallar kalir.
    t = tris_of(obj.data)
    if t > target * 1.15 and not solid:
        bm = bmesh.new(); bm.from_mesh(obj.data)
        faces = sorted(bm.faces, key=lambda f: min(e.calc_length() for e in f.edges))
        kill = []
        for f in faces:
            if t <= target:
                break
            kill.append(f); t -= len(f.verts) - 2
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(obj.data); bm.free()
        log('ince yuz silindi', len(kill))
    return
    md = obj.modifiers.new('dec', 'DECIMATE')
    md.ratio = max(0.002, target / t)
    md.use_collapse_triangulate = True
    activate([obj])
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier=md.name)


def triangulate(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(obj.data); bm.free()


def join(objs, name):
    objs = [o for o in objs if o]
    if len(objs) > 1:
        activate(objs)
        with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs, object=objs[0]):
            bpy.ops.object.join()
    objs[0].name = name
    objs[0].data.name = name
    return objs[0]


def new_obj(name, me):
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def world_copy(src, name):
    """Kaynak nesnenin (degistiricisiz) mesh'i, dunya donusumu uygulanmis kopya."""
    me = src.data.copy()
    me.transform(src.matrix_world)
    if src.matrix_world.determinant() < 0:
        me.flip_normals()
    return new_obj(name, me)


def bbox(objs):
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.data.vertices:
            p = o.matrix_world @ v.co
            mn = Vector(map(min, mn, p)); mx = Vector(map(max, mx, p))
    return mn, mx


# ---------------------------------------------------------------- kart pisirme (agac)

def emission_material(name, spec, base, normal_pass, src=None):
    """Pisirme malzemesi. src (kaynak malzeme) verilirse kopyasi kullanilir: doku dugumleri UV esleme zincirleriyle
    (Mapping) birlikte kalir, yalniz cikis isinim + saydamliga baglanir."""
    m = src.copy() if src is not None else bpy.data.materials.new(name)
    m.name = name
    m.use_nodes = True
    nt = m.node_tree
    found = {}
    if src is not None:
        for n in nt.nodes:
            if n.bl_idname == 'ShaderNodeTexImage' and n.image:
                bn = os.path.basename(bpy.path.abspath(n.image.filepath)).lower()
                for k in ('albedo', 'alpha', 'normal'):
                    if spec.get(k) and os.path.basename(spec[k].split('|')[0]).lower() == bn:
                        found[k] = n
        for n in list(nt.nodes):
            if n.bl_idname == 'ShaderNodeOutputMaterial':
                nt.nodes.remove(n)
    else:
        for n in list(nt.nodes):
            nt.nodes.remove(n)

    def tex(k, noncolor=False):
        if k in found:
            return found[k]
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = bpy.data.images.load(tex_path(base, spec[k]), check_existing=True)
        if noncolor:
            t.image.colorspace_settings.name = 'Non-Color'
        return t
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    out.is_active_output = True
    em = nt.nodes.new('ShaderNodeEmission')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    alb = tex('albedo')
    alpha_out = None
    if spec.get('alpha'):
        ta = tex('alpha', True)
        alpha_out = ta.outputs['Color']
    elif spec.get('albedo_alpha'):
        alpha_out = alb.outputs['Alpha']
    if alpha_out is not None:
        g = nt.nodes.new('ShaderNodeMath'); g.operation = 'GREATER_THAN'; g.inputs[1].default_value = 0.5
        nt.links.new(alpha_out, g.inputs[0])
        nt.links.new(g.outputs[0], mix.inputs[0])
    else:
        mix.inputs[0].default_value = 1.0
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    if not normal_pass:
        nt.links.new(alb.outputs['Color'], em.inputs['Color'])
        return m
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    nvec = geo.outputs['Normal']
    if spec.get('normal'):
        tn = tex('normal', True)
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nvec = nm.outputs['Normal']
    # kamera donussuz, -Z'ye bakiyor: kamera uzayi = dunya uzayi (kameraya bakan normal +Z)
    # arka yuz: normal kameraya donsun (yaprak iki yuzlu)
    sign = nt.nodes.new('ShaderNodeMath'); sign.operation = 'MULTIPLY_ADD'
    nt.links.new(geo.outputs['Backfacing'], sign.inputs[0]); sign.inputs[1].default_value = -2.0; sign.inputs[2].default_value = 1.0
    sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'
    nt.links.new(nvec, sc.inputs[0]); nt.links.new(sign.outputs[0], sc.inputs['Scale'])
    ma = nt.nodes.new('ShaderNodeVectorMath'); ma.operation = 'MULTIPLY_ADD'
    nt.links.new(sc.outputs[0], ma.inputs[0]); ma.inputs[1].default_value = (0.5, 0.5, 0.5); ma.inputs[2].default_value = (0.5, 0.5, 0.5)
    nt.links.new(ma.outputs[0], em.inputs['Color'])
    return m


def bake_proto(proto, mats, base, res):
    """Prototip kumesini ustten (yerel -Z) ortografik cizer: (albedo RGBA sRGB, normal RGBA) ve kare kutusu."""
    me = proto.data
    mn = Vector((min(v.co.x for v in me.vertices), min(v.co.y for v in me.vertices), min(v.co.z for v in me.vertices)))
    mx = Vector((max(v.co.x for v in me.vertices), max(v.co.y for v in me.vertices), max(v.co.z for v in me.vertices)))
    c = (mn + mx) / 2
    half = max(mx.x - mn.x, mx.y - mn.y) / 2 * 1.02
    hx = (mx.x - mn.x) / 2 * 1.02; hy = (mx.y - mn.y) / 2 * 1.02  # kart siki dikdortgen: bos alan cizilmesin
    sc = bpy.data.scenes.new('bake_' + proto.name)
    ob = bpy.data.objects.new('bake', me.copy())
    sc.collection.objects.link(ob)
    cam = bpy.data.objects.new('bcam', bpy.data.cameras.new('bcam'))
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2 * half
    cam.data.clip_start = 0.001; cam.data.clip_end = (mx.z - mn.z) + 10
    cam.location = (c.x, c.y, mx.z + 1.0)
    sc.collection.objects.link(cam)
    sc.camera = cam
    r = sc.render
    r.engine = 'CYCLES'
    sc.cycles.samples = 32
    sc.cycles.device = 'CPU'
    sc.cycles.transparent_max_bounces = 64
    sc.cycles.max_bounces = 0
    r.film_transparent = True
    r.resolution_x = r.resolution_y = res
    r.resolution_percentage = 100
    r.image_settings.file_format = 'OPEN_EXR'
    r.image_settings.color_mode = 'RGBA'
    r.image_settings.color_depth = '16'
    try:
        sc.compositing_node_group = None
    except Exception:
        pass
    outs = []
    idx = [p.material_index for p in ob.data.polygons]  # materials.clear() indeksleri sifirlar
    for npass in (False, True):
        ob.data.materials.clear()
        for ms in proto.material_slots:  # nesneye bagli malzeme mesh'tekini ezer
            slot = ms.material
            key = slot.name if slot else ''
            spec = mats.get(key) or next((v for k, v in mats.items() if fnmatch.fnmatch(key, k)), None)
            if spec is None:
                raise KeyError(f'malzeme tanimi yok: {key} ({proto.name})')
            ob.data.materials.append(emission_material(f'{key}_{"n" if npass else "a"}', spec, base, npass, slot))
        ob.data.polygons.foreach_set('material_index', idx)
        ob.data.update()
        path = os.path.join(TMP, f'bake_{proto.name}_{"n" if npass else "a"}.exr')
        r.filepath = path
        with bpy.context.temp_override(scene=sc):
            bpy.ops.render.render(write_still=True, scene=sc.name)
        img = bpy.data.images.load(path, check_existing=False)
        a = np.empty(res * res * 4, np.float32); img.pixels.foreach_get(a); bpy.data.images.remove(img)
        a = a.reshape(res, res, 4)
        alpha = np.clip(a[:, :, 3], 0, 1)
        rgb = a[:, :, :3] / np.maximum(alpha, 1e-4)[:, :, None]
        if not npass:
            rgb = lin_to_srgb(rgb)
        outs.append(np.dstack([np.clip(rgb, 0, 1), alpha]))
    bpy.data.scenes.remove(sc)
    return outs[0], outs[1], (c, half, hx, hy)


def build_tree(name, it, outdir):
    src = it['source']
    base = source_dir(src)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(base, src['file']))
    for g in bpy.data.node_groups:
        for n in g.nodes:
            if n.bl_idname == 'GeometryNodeRealizeInstances':
                n.mute = True
    for oname, inputs in it.get('gn_inputs', {}).items():
        md = next(m for m in bpy.data.objects[oname].modifiers if m.type == 'NODES')
        for key, val in inputs.items():
            sock = next((i for i in md.node_group.interface.items_tree if getattr(i, 'name', '') == key), None)
            if sock is not None:
                md[sock.identifier] = val
        bpy.data.objects[oname].update_tag()
    # sahnedeki tum nesneler gizli: yalniz bizimkiler degerlendirilsin
    dg = bpy.context.evaluated_depsgraph_get()
    emitters = set(it['instancers'])
    insts = []
    for inst in dg.object_instances:
        if inst.is_instance and inst.parent and inst.parent.original.name in emitters:
            if inst.object.original.name not in it.get('skip_protos', []):
                insts.append((inst.object.original.name, inst.matrix_world.copy()))
    protos = sorted({p for p, _ in insts})
    log(name, 'ornek', len(insts), 'prototip', protos)
    mats = it['materials']
    A = it.get('atlas', 2048)
    tiles = {}
    g = math.ceil(math.sqrt(len(protos)))
    res = A // g
    atlas_a = np.zeros((A, A, 4), np.float32)
    atlas_n = np.zeros((A, A, 4), np.float32); atlas_n[:, :, :3] = (0.5, 0.5, 1.0)
    for k, pn in enumerate(protos):
        a, n, frame = bake_proto(bpy.data.objects[pn], mats, base, res)
        ty, tx = divmod(k, g)
        atlas_a[ty * res:(ty + 1) * res, tx * res:(tx + 1) * res] = a
        atlas_n[ty * res:(ty + 1) * res, tx * res:(tx + 1) * res] = n
        tiles[pn] = (frame, ((tx * res + 1) / A, (ty * res + 1) / A, ((tx + 1) * res - 1) / A, ((ty + 1) * res - 1) / A))
        log(name, 'pisirildi', pn, 'opak', round(float((a[:, :, 3] > 0.5).mean()), 3))
    atlas_a = grade(dilate(atlas_a), it.get('leaf_grade'))
    atlas_n[:, :, 3] = 1.0
    rdir = os.path.join(ROOT, 'progress', 'bitki', 'render'); os.makedirs(rdir, exist_ok=True)
    save_png(resize(atlas_a, 1024), os.path.join(rdir, f'{name}_atlas.png'))
    save_png(resize(atlas_n, 1024), os.path.join(rdir, f'{name}_atlas_normal.png'), 'Non-Color')

    # govde ve dallar: kok nesnenin yerel cercevesinde (sahnedeki egiklik atilir, Z yukari)
    R = bpy.data.objects[it['wood'][0]].matrix_world.inverted()
    wood = [world_copy(bpy.data.objects[n], 'wood_' + n) for n in it['wood']]
    for o in wood:
        o.data.transform(R)
    wmn, wmx = bbox(wood)
    zmin = wmn.z
    near = [o.matrix_world @ v.co for o in wood for v in o.data.vertices]
    span = max(1e-3, wmx.z - zmin)
    foot = [p for p in near if p.z < zmin + span * 0.04]
    basept = Vector((sum(p.x for p in foot) / len(foot), sum(p.y for p in foot) / len(foot), zmin))

    # kart kaynaklari (dunya uzayi)
    cards = []
    for pn, M in insts:
        (c, half, hx, hy), (u0, v0, u1, v1) = tiles[pn]
        corners = [Vector((c.x + sx * hx, c.y + sy * hy, c.z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        um, vm, du, dv = (u0 + u1) / 2, (v0 + v1) / 2, (u1 - u0) / 2 * hx / half, (v1 - v0) / 2 * hy / half
        uv = (um - du, vm - dv, um + du, vm + dv)
        L = R @ M
        w = [L @ p for p in corners]
        cards.append((w, uv, (L.to_3x3() @ Vector((0, 0, 1))).normalized()))
    allz = [p.z for cd in cards for p in cd[0]] + [wmx.z]
    height = max(allz) - zmin
    s = it['height'] / height
    xf = Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(-basept)
    log(name, 'kaynak boy', round(height, 2), 'olcek', round(s, 4))
    ctr = Vector((0, 0, 0));
    pts = [xf @ ((cd[0][0] + cd[0][2]) / 2) for cd in cards]
    ctr = sum(pts, Vector()) / max(1, len(pts))
    rad = Vector((max(abs(p.x - ctr.x) for p in pts), max(abs(p.y - ctr.y) for p in pts), max(abs(p.z - ctr.z) for p in pts)))
    for o in wood:
        o.data.transform(xf)

    rng = random.Random(it.get('seed', 7))
    order = list(range(len(cards)))
    rng.shuffle(order)
    vol = it.get('volume_normals', 0.65)

    def card_mesh(keep_n, grow):
        bm = bmesh.new()
        uvl = bm.loops.layers.uv.new('UVMap')
        normals = []
        for idx in order[:keep_n]:
            w, (u0, v0, u1, v1), nc = cards[idx]
            q = [xf @ p for p in w]
            cc = sum(q, Vector()) / 4
            q = [cc + (p - cc) * grow for p in q]
            verts = [bm.verts.new(p) for p in q]
            f = bm.faces.new(verts)
            for loop, uv in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                loop[uvl].uv = uv
            d = cc - ctr
            e = Vector((d.x / max(rad.x, 1e-3), d.y / max(rad.y, 1e-3), d.z / max(rad.z, 1e-3) * 0.8 + 0.25))
            ns = e.normalized() if e.length > 1e-4 else Vector((0, 0, 1))
            ncw = (xf.to_3x3() @ nc).normalized()
            if ncw.dot(ns) < 0:
                ncw = -ncw
            n = (ncw * (1 - vol) + ns * vol).normalized()
            normals.append(n)
        me = bpy.data.meshes.new('cards')
        bm.to_mesh(me); bm.free()
        loop_normals = []
        for poly, n in zip(me.polygons, normals):
            loop_normals += [n] * poly.loop_total
        me.normals_split_custom_set(loop_normals)
        return new_obj('cards', me)

    wood_tris = sum(tris_of(o.data) for o in wood)
    wood_w = sum(math.sqrt(tris_of(o.data)) for o in wood)  # pay: koke yakin ve kalin govde de pay alsin
    lods = it['lods']
    built = []
    for li, lod in enumerate(lods):
        wt = int(lod['wood_tris'])
        ws = []
        for o in wood:
            c = new_obj('w', o.data.copy())
            apply_decimate(c, int(wt * math.sqrt(tris_of(o.data)) / max(1, wood_w)))
            log('odun parca', o.name, tris_of(o.data), '->', tris_of(c.data), 'hedef', int(wt * math.sqrt(tris_of(o.data)) / max(1, wood_w)))
            ws.append(c)
        ncards = max(8, int((lod['tris'] - sum(tris_of(c.data) for c in ws)) / 2))
        keep = min(len(cards), ncards)
        grow = math.sqrt(len(cards) / keep) ** it.get('grow_power', 0.85) * it.get('card_scale', 1.0)
        cm = card_mesh(keep, grow)
        built.append((ws, cm, lod))
        log(name, f'lod{li}', 'odun', sum(tris_of(c.data) for c in ws), 'kart', keep, 'buyutme', round(grow, 2))

    # carpisma kabugu yalniz govde (ilk odun nesnesi), tac altindan yurunebilsin
    hull_pts = [v.co.copy() for v in wood[0].data.vertices if v.co.z < min(1.8, it['height'] * 0.35)]
    for o in wood:
        bpy.data.objects.remove(o)

    results = []
    for li, (ws, cm, lod) in enumerate(built):
        size = lod['tex']
        sfx = '' if li == 0 else f'_lod{li}'
        prefix = name + sfx
        PREFIXES.add(prefix)
        # yaprak atlasi
        la = resize(atlas_a, size)
        ln = resize(atlas_n, size)
        leaf = make_material(f'{name}_yaprak', save_png(la, os.path.join(TMP, f'{prefix}_yaprak_albedo.png')),
                             save_png(ln, os.path.join(TMP, f'{prefix}_yaprak_normal.png'), 'Non-Color'), None, True,
                             it.get('leaf_roughness', 0.75))
        cm.data.materials.append(leaf)
        # odun malzemeleri
        for c in ws:
            for i, slot in enumerate(c.data.materials):
                key = slot.name if slot else ''
                spec = mats.get(key) or next(v for k, v in mats.items() if fnmatch.fnmatch(key, k))
                tgt = spec.get('as', key)
                mat = bpy.data.materials.get(f'{name}_{tgt}{sfx}')
                if mat is None:
                    imgs, clip = texture_set(tgt, mats[tgt] if tgt in mats else spec, base, min(size, mats.get(tgt, spec).get('max_tex', size)), outdir, prefix)
                    mat = make_material(f'{name}_{tgt}{sfx}', imgs['albedo'], imgs['normal'], imgs['rough'], clip)
                c.data.materials[i] = mat
        obj = join(ws + [cm], name + sfx)
        results.append(obj)
    return results, hull_pts, {'instances': len(cards), 'prototypes': protos}


# ---------------------------------------------------------------- hazir bitki

def import_any(path):
    before = set(bpy.data.objects)
    if path.endswith('.blend'):
        with bpy.data.libraries.load(path) as (src, dst):
            dst.objects = src.objects
        for o in dst.objects:
            if o and o.type == 'MESH':
                bpy.context.scene.collection.objects.link(o)
    elif path.endswith('.fbx'):
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before and o.type == 'MESH']


def build_plant(name, it, outdir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = it['source']
    base = source_dir(src)
    objs = []
    for fn in [src['file']] + src.get('extra_files', []):
        objs += import_any(os.path.join(base, fn))
    byname = {o.name: o for o in objs}
    up = Matrix.Rotation(math.radians(90), 4, 'X') if it.get('up', 'z') == 'y' else Matrix.Identity(4)

    def part_obj(pname, lodmap):
        for a, b in lodmap:
            pname = pname.replace(a, b)
        o = byname.get(pname) or next((x for n, x in byname.items() if n.startswith(pname)), None)
        if o is None:
            raise KeyError(f'{name}: parca yok {pname}; var: {sorted(byname)[:12]}')
        return o

    def assemble(lodmap):
        parts = []
        for k, p in enumerate(it['parts']):
            src_o = part_obj(p['obj'], lodmap)
            o = new_obj(f'p{k}', src_o.data.copy())
            o.data.transform(up @ src_o.matrix_world)
            mn, mx = bbox([o])
            o.data.transform(Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
            sc = p.get('scale', 1.0)
            at = p.get('at', [0, 0])
            o.data.transform(Matrix.Translation(Vector((at[0], at[1], p.get('z', 0)))) @ Matrix.Rotation(math.radians(p.get('rot', 0)), 4, 'Z') @ Matrix.Diagonal((sc, sc, sc, 1)))
            parts.append(o)
        ob = join(parts, 'plant')
        mn, mx = bbox([ob])
        ob.data.transform(Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
        return ob

    lod0 = assemble([])
    mn, mx = bbox([lod0])
    s = it['height'] / (mx.z - mn.z)
    log(name, 'kaynak boy', round(mx.z - mn.z, 3), 'olcek', round(s, 3), 'ucgen', tris_of(lod0.data))
    results = []
    hull_pts = None
    for li, lod in enumerate(it['lods']):
        ob = lod0 if li == 0 else (assemble(lod['replace']) if lod.get('replace') else new_obj('plant', lod0.data.copy()))
        if li > 0 and lod.get('replace'):
            pass
        if li == 0 or lod.get('replace'):
            ob.data.transform(Matrix.Diagonal((s, s, s, 1)))
        triangulate(ob)
        apply_decimate(ob, lod['tris'])
        if li == 0:
            hull_pts = [v.co.copy() for v in ob.data.vertices if v.co.z < it['height'] * it.get('hull_height', 1.0)]
        size = lod['tex']
        sfx = '' if li == 0 else f'_lod{li}'
        PREFIXES.add(name + sfx)
        for i, slot in enumerate(ob.data.materials):
            key = (slot.get('src_key') or slot.name) if slot else ''
            spec = it['materials'].get(key) or next((v for k, v in it['materials'].items() if fnmatch.fnmatch(key, k)), None)
            if spec is None:
                raise KeyError(f'{name}: malzeme tanimi yok: {key}')
            tgt = spec.get('as', key.split('.')[0])
            mat = bpy.data.materials.get(f'{name}_{tgt}{sfx}')
            if mat is None:
                imgs, clip = texture_set(tgt, spec, base, min(size, spec.get('max_tex', size)), outdir, name + sfx, it.get('grade'))
                mat = make_material(f'{name}_{tgt}{sfx}', imgs['albedo'], imgs['normal'], imgs['rough'], clip, spec.get('roughness', 0.8))
                mat['src_key'] = key
            ob.data.materials[i] = mat
        ob.name = name + sfx
        ob.data.name = name + sfx
        results.append(ob)
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o)
    return results, hull_pts, {}


# ---------------------------------------------------------------- stilize (Tripo GLB)

def image_from_socket(sock):
    """Bir BSDF girisini besleyen goruntu dugumu (arada Normal Map / Separate Color olabilir)."""
    seen = 0
    while sock and sock.is_linked and seen < 6:
        n = sock.links[0].from_node
        if n.type == 'TEX_IMAGE':
            return n.image
        sock = next((i for i in n.inputs if i.is_linked), None)
        seen += 1
    return None


def dump_image(img, path):
    """Gomulu (packed) goruntuyu duz PNG'ye yazar; dokular texture_set'e dosya yolu olarak girer."""
    a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    a = a.reshape(img.size[1], img.size[0], 4)
    out = bpy.data.images.new(os.path.basename(path), img.size[0], img.size[1], alpha=True)
    out.colorspace_settings.name = img.colorspace_settings.name
    out.pixels.foreach_set(a.ravel())
    out.filepath_raw = path; out.file_format = 'PNG'; out.save()
    bpy.data.images.remove(out)
    return path


def tripo_spec(mat, key):
    """Tripo/glTF malzemesinden doku tanimi: albedo, normal, puruzluluk (metallicRoughness G)."""
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    spec = {}
    if bsdf is None:
        return spec
    for k, inp in (('albedo', 'Base Color'), ('normal', 'Normal'), ('rough', 'Roughness')):
        img = image_from_socket(bsdf.inputs[inp])
        if img is not None:
            spec[k] = dump_image(img, os.path.join(TMP, f'src_{key}_{k}.png'))
    if 'rough' in spec:
        spec['rough'] += '|G'
    if 'albedo' not in spec:
        c = bsdf.inputs['Base Color'].default_value
        a = np.ones((4, 4, 4), np.float32); a[:, :, :3] = lin_to_srgb(np.array(c[:3]))
        spec['albedo'] = save_png(a, os.path.join(TMP, f'src_{key}_albedo.png')) and os.path.join(TMP, f'src_{key}_albedo.png')
    return spec


def leaf_shell(obj, sh, height, count, seed):
    """Stilize tac kitlesinin ustune ince yaprak kartlari: tac yuzeyinden (crown_start * boy ustu) alana gore
    ornekler, yuzey normaline egik kucuk kartlar, disari offset. UV atlas karelerinden (grid x grid) rastgele."""
    rng = random.Random(seed)
    me = obj.data
    me.calc_loop_triangles()
    zmin = height * sh.get('crown_start', 0.45)
    tris = []
    for t in me.loop_triangles:
        c = sum((me.vertices[i].co for i in t.vertices), Vector()) / 3
        if c.z >= zmin and t.normal.z > -0.6:
            tris.append((t.area, c, t.normal.copy(), [me.vertices[i].co.copy() for i in t.vertices]))
    if not tris:
        return None
    acc = np.cumsum([a for a, *_ in tris]); tot = acc[-1]
    g = sh.get('grid', 1)
    smin, smax = sh.get('size', [0.35, 0.6])
    off = sh.get('offset', 0.04)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for _ in range(count):
        a, c, n, vs = tris[int(np.searchsorted(acc, rng.random() * tot))]
        u, v = rng.random(), rng.random()
        if u + v > 1:
            u, v = 1 - u, 1 - v
        p = vs[0] + (vs[1] - vs[0]) * u + (vs[2] - vs[0]) * v + n * off
        nn = (n + Vector((rng.uniform(-.5, .5), rng.uniform(-.5, .5), rng.uniform(0, .6)))).normalized()
        t1 = nn.orthogonal().normalized()
        t1.rotate(Matrix.Rotation(rng.uniform(0, math.tau), 3, nn))
        t2 = nn.cross(t1)
        s = rng.uniform(smin, smax) * 0.5
        corners = [p + (t1 * x + t2 * y) * s + nn * s * 0.25 * (y + 1) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        f = bm.faces.new([bm.verts.new(q) for q in corners])
        tx, ty = rng.choice(sh['tiles']) if sh.get('tiles') else (rng.randrange(g), rng.randrange(g))
        for lp, (x, y) in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            lp[uvl].uv = ((tx + x) / g, (ty + y) / g)
    shell = new_obj('shell', bpy.data.meshes.new('shell'))
    bm.to_mesh(shell.data); bm.free()
    # kart normali tac hacmine (elipsoit) egik: tac tek kitle gibi golgelenir, kartlar ayri ayri parlamaz
    mn, mx = bbox([obj]); ctr = (mn + mx) / 2; half = (mx - mn) / 2
    ns = []
    for lp in shell.data.loops:
        q = shell.data.vertices[lp.vertex_index].co - ctr
        ns.append(Vector((q.x / max(half.x, 1e-3) ** 2, q.y / max(half.y, 1e-3) ** 2, q.z / max(half.z, 1e-3) ** 2)).normalized())
    shell.data.normals_split_custom_set(ns)
    return shell


def build_stylized(name, it, outdir):
    """Hazir stilize model (Tripo web/API GLB): tek parca, gomulu dokular. Olcek, eksen, orta; LOD'lar collapse
    (delik acmaz); dokular LOD boyutuna, renk ayari; istege bagli yaprak kabugu (it['leaf_shell'])."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = it['source']
    base = source_dir(src)
    objs = import_any(os.path.join(base, src['file']))
    parts = [world_copy(o, f'p{k}') for k, o in enumerate(objs)]
    for o in objs:
        bpy.data.objects.remove(o)
    ob = join(parts, 'model')
    if it.get('rot'):
        ob.data.transform(Matrix.Rotation(math.radians(it['rot']), 4, 'Z'))
    mn, mx = bbox([ob])
    s = it['height'] / (mx.z - mn.z)
    ob.data.transform(Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
    triangulate(ob)
    log(name, 'kaynak ucgen', tris_of(ob.data), 'olcek', round(s, 3))
    specs = {}
    for i, m in enumerate(ob.data.materials):
        key = f'm{i}'
        spec = dict(tripo_spec(m, key)) if m else {}
        spec.update(it.get('materials', {}).get(key, {}))  # elle: roughness, grade, rough_mul
        specs[key] = spec
    sh = it.get('leaf_shell')
    results, hull_pts = [], None
    src_me = ob.data.copy()  # her LOD kaynak cozunurlukten (kabuk ve malzeme eklenmeden)
    for li, lod in enumerate(it['lods']):
        o = ob if li == 0 else new_obj('model', src_me.copy())
        apply_decimate(o, lod['tris'], solid=True)
        if li == 0:
            hh = it.get('hull_height', min(1.8, it['height'] * 0.35) if it.get('tree') else it['height'])
            hull_pts = [v.co.copy() for v in o.data.vertices if v.co.z < hh]
            if it.get('tree'):  # taban dar gövde: alt kisimda yatay uzak noktalar (kok, cimen) disarida
                ds = sorted(math.hypot(p.x, p.y) for p in hull_pts)
                lim = ds[int(len(ds) * 0.9)] if ds else 0
                hull_pts = [p for p in hull_pts if math.hypot(p.x, p.y) <= lim]
        size = lod['tex']; sfx = '' if li == 0 else f'_lod{li}'
        PREFIXES.add(name + sfx)
        for i, slot in enumerate(o.data.materials):
            key = f'm{i}'
            imgs, _ = texture_set(key, specs[key], TMP, size, outdir, name + sfx, it.get('grade'))
            o.data.materials[i] = make_material(f'{name}_{key}{sfx}', imgs['albedo'], imgs['normal'], imgs['rough'], False,
                                                specs[key].get('roughness', 0.85))
        n_shell = (sh or {}).get('counts', [0, 0, 0])[li] if sh else 0
        if n_shell:
            shell = leaf_shell(o, sh, it['height'], n_shell, sh.get('seed', 7))
            if shell is not None:
                spec = {'albedo': sh['albedo'], 'normal': sh.get('normal'), 'rough': sh.get('rough'),
                        'alpha': sh.get('alpha'), 'albedo_alpha': True, 'clip': True}
                imgs, clip = texture_set('yaprak', spec, ROOT, min(size, sh.get('max_tex', 1024)), outdir, name + sfx,
                                         sh.get('grade', it.get('grade')))
                shell.data.materials.append(make_material(f'{name}_yaprak{sfx}', imgs['albedo'], imgs['normal'], imgs['rough'], clip))
                o = join([o, shell], 'model')
        o.name = o.data.name = name + sfx
        results.append(o)
    return results, hull_pts, {'leaf_shell': bool(sh)}


# ---------------------------------------------------------------- Tripo agaci (kaynasmis yuksek poligon)

def kmeans(X, w, K, iters, rng):
    """Agirlikli k-ortalama (orneklemle egitim, parca parca atama)."""
    def assign(P, C):
        out = np.empty(len(P), np.int64)
        c2 = (C ** 2).sum(1)
        for i in range(0, len(P), 40000):
            out[i:i + 40000] = (c2[None, :] - 2 * P[i:i + 40000] @ C.T).argmin(1)
        return out
    si = rng.choice(len(X), min(len(X), 250000), replace=False)
    S, Sw = X[si], w[si]
    C = S[rng.choice(len(S), K, replace=False)].copy()
    for _ in range(iters):
        lab = assign(S, C)
        tot = np.bincount(lab, Sw, K)
        for d in range(X.shape[1]):
            C[:, d] = np.where(tot > 0, np.bincount(lab, Sw * S[:, d], K) / np.maximum(tot, 1e-12), C[:, d])
        empty = np.where(tot == 0)[0]
        if len(empty):
            C[empty] = S[rng.choice(len(S), len(empty), replace=False)]
    return assign(X, C)


def mesh_from_tris(name, co, uv=None):
    """co: (3T, 3) koseler (paylasimsiz), uv: (3T, 2)."""
    me = bpy.data.meshes.new(name)
    n = len(co)
    me.from_pydata(co.astype(np.float32), [], np.arange(n).reshape(-1, 3))
    if uv is not None:
        lay = me.uv_layers.new(name='UVMap')
        lay.data.foreach_set('uv', uv.astype(np.float32).ravel())
    me.update()
    return new_obj(name, me)


def src_images(mat):
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    return {k: image_from_socket(bsdf.inputs[s]) for k, s in (('albedo', 'Base Color'), ('normal', 'Normal'), ('rough', 'Roughness'))}


def pass_material(name, imgs, kind):
    """Pisirme gecisi: albedo / rough -> isinim; normal -> kamera uzayi normali (ust gorunus) ya da NORMAL pisirmesi icin
    yayilim + normal haritasi."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    def tex(k):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = imgs[k]
        if k != 'albedo':
            t.image.colorspace_settings.name = 'Non-Color'
        return t
    if kind in ('albedo', 'rough'):
        em = nt.nodes.new('ShaderNodeEmission')
        if imgs.get(kind) is None:  # yeniden dokunan Tripo modelinde puruzluluk haritasi olmayabilir: sabit
            v = 0.85 if kind == 'rough' else 0.5
            em.inputs['Color'].default_value = (v, v, v, 1)
            nt.links.new(em.outputs[0], out.inputs['Surface'])
            return m
        t = tex(kind)
        if kind == 'rough':
            sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(t.outputs['Color'], sep.inputs['Color'])
            nt.links.new(sep.outputs['Green'], em.inputs['Color'])
        else:
            nt.links.new(t.outputs['Color'], em.inputs['Color'])
        nt.links.new(em.outputs[0], out.inputs['Surface'])
        return m
    if imgs.get('normal') is not None:
        nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tex('normal').outputs['Color'], nm.inputs['Color'])
    else:  # normal haritasi yok: geometri normali
        nm = nt.nodes.new('ShaderNodeNewGeometry')
    if kind == 'normal_bake':
        d = nt.nodes.new('ShaderNodeBsdfDiffuse'); nt.links.new(nm.outputs['Normal'], d.inputs['Normal'])
        nt.links.new(d.outputs[0], out.inputs['Surface'])
        return m
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sign = nt.nodes.new('ShaderNodeMath'); sign.operation = 'MULTIPLY_ADD'
    nt.links.new(geo.outputs['Backfacing'], sign.inputs[0]); sign.inputs[1].default_value = -2.0; sign.inputs[2].default_value = 1.0
    sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'
    nt.links.new(nm.outputs['Normal'], sc.inputs[0]); nt.links.new(sign.outputs[0], sc.inputs['Scale'])
    ma = nt.nodes.new('ShaderNodeVectorMath'); ma.operation = 'MULTIPLY_ADD'
    nt.links.new(sc.outputs[0], ma.inputs[0]); ma.inputs[1].default_value = (0.5, 0.5, 0.5); ma.inputs[2].default_value = (0.5, 0.5, 0.5)
    em = nt.nodes.new('ShaderNodeEmission'); nt.links.new(ma.outputs[0], em.inputs['Color'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def cycles_scene(sc, samples):
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.max_bounces = 0
    sc.cycles.transparent_max_bounces = 8
    try:
        sc.cycles.filter_width = 1.0
        sc.compositing_node_group = None
    except Exception:
        pass


def bake_cards_atlas(obj, imgs, g, A):
    """Kartlarin ust gorunus atlasi: obj zaten kare dizilimli (her kume bir birim kare). (albedo sRGB, normal, rough)."""
    sc = bpy.data.scenes.new('atlas')
    sc.collection.objects.link(obj)
    cam = bpy.data.objects.new('acam', bpy.data.cameras.new('acam'))
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = g
    cam.data.clip_start = 0.01; cam.data.clip_end = 40
    cam.location = (g / 2, g / 2, 20)
    sc.collection.objects.link(cam); sc.camera = cam
    cycles_scene(sc, 8)
    r = sc.render
    r.film_transparent = True
    r.resolution_x = r.resolution_y = A; r.resolution_percentage = 100
    r.image_settings.file_format = 'OPEN_EXR'; r.image_settings.color_mode = 'RGBA'; r.image_settings.color_depth = '16'
    outs = {}
    for kind in ('albedo', 'normal', 'rough'):
        obj.data.materials.clear()
        obj.data.materials.append(pass_material('atlas_' + kind, imgs, kind))
        path = os.path.join(TMP, f'atlas_{kind}.exr'); r.filepath = path
        with bpy.context.temp_override(scene=sc):
            bpy.ops.render.render(write_still=True, scene=sc.name)
        im = bpy.data.images.load(path, check_existing=False)
        a = np.empty(A * A * 4, np.float32); im.pixels.foreach_get(a); bpy.data.images.remove(im)
        a = a.reshape(A, A, 4)
        al = np.clip(a[:, :, 3], 0, 1)
        rgb = np.clip(a[:, :, :3] / np.maximum(al, 1e-4)[:, :, None], 0, 1)
        if kind == 'albedo':
            rgb = lin_to_srgb(rgb)
        outs[kind] = np.dstack([rgb, al])
        log('atlas', kind, 'opak', round(float((al > 0.5).mean()), 3))
    bpy.data.scenes.remove(sc)
    return outs['albedo'], outs['normal'], outs['rough']


def bake_to_low(hi, lo, imgs, res, prefix):
    """Gövde: seyreltilmis kopyaya yeni UV, yuksek poligondan secili->etkin pisirme (albedo, normal, puruzluluk)."""
    while lo.data.uv_layers:
        lo.data.uv_layers.remove(lo.data.uv_layers[0])
    lo.data.uv_layers.new(name='UVMap')
    lo.data.materials.clear()
    activate([lo])
    with bpy.context.temp_override(active_object=lo, object=lo, selected_objects=[lo], selected_editable_objects=[lo]):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.008)
        bpy.ops.object.mode_set(mode='OBJECT')
    sc = bpy.context.scene
    cycles_scene(sc, 4)
    mn, mx = bbox([hi]); ext = (mx - mn).length * 0.006
    out = {}
    for kind, btype in (('albedo', 'EMIT'), ('normal_bake', 'NORMAL'), ('rough', 'EMIT')):
        img = bpy.data.images.new(f'{prefix}_{kind}', res, res, alpha=False, float_buffer=True)
        img.colorspace_settings.name = 'Non-Color' if kind != 'albedo' else 'Linear Rec.709'
        lm = bpy.data.materials.new('lo_' + kind); lm.use_nodes = True
        t = lm.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img
        lm.node_tree.nodes.active = t
        lo.data.materials.clear(); lo.data.materials.append(lm)
        hi.data.materials.clear(); hi.data.materials.append(pass_material('hi_' + kind, imgs, kind))
        for o in sc.objects:
            o.select_set(o in (hi, lo))
        bpy.context.view_layer.objects.active = lo
        bpy.ops.object.bake(type=btype, use_selected_to_active=True, cage_extrusion=ext, max_ray_distance=ext * 3,
                            margin=8, normal_space='TANGENT')
        a = np.empty(res * res * 4, np.float32); img.pixels.foreach_get(a)
        a = a.reshape(res, res, 4); a[:, :, 3] = 1
        if kind == 'albedo':
            a[:, :, :3] = lin_to_srgb(a[:, :, :3])
        out[kind] = a
    lo.data.materials.clear(); hi.data.materials.clear()
    return out


def build_tripo_prop(name, it, outdir):
    """Tripo statik model (mantar, kutuk, maden, kesif noktasi): tek yuksek poligon mesh. Kaynak UV'si seyreltmede
    bozulacagi icin her LOD'a yeni UV acilir ve albedo + normal + puruzluluk kaynaktan pisirilir. Boy: 'size_axis'
    z (yukseklik, varsayilan) ya da max (en uzun kenar: yatik kutuk, yuva capi). Taban z=0, orta x=y=0."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = it['source']
    objs = import_any(os.path.join(source_dir(src), src['file']))
    o = objs[0]
    imgs = src_images(o.data.materials[0])
    hi = world_copy(o, 'hi')
    for x in objs:
        bpy.data.objects.remove(x)
    if it.get('rot'):
        hi.data.transform(Matrix.Rotation(math.radians(it['rot']), 4, 'Z'))
    mn, mx = bbox([hi])
    ext = mx - mn
    size = ext.z if it.get('size_axis', 'z') == 'z' else max(ext)
    s = it['height'] / size
    hi.data.transform(Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(Vector((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z))))
    bm = bmesh.new(); bm.from_mesh(hi.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=it['height'] * 2e-6)
    bm.to_mesh(hi.data); bm.free()
    hi.data.materials.clear()
    log(name, 'kaynak ucgen', tris_of(hi.data), 'olcek', round(s, 3))
    results, hull_pts = [], None
    for li, lod in enumerate(it['lods']):
        sfx = '' if li == 0 else f'_lod{li}'
        prefix = name + sfx
        PREFIXES.add(prefix)
        lo = new_obj('lo', hi.data.copy())
        if it.get('remesh_voxel'):  # acik kenarli sert yuzey (enkaz): collapse takilir; once kapali hacme voksel ag
            md = lo.modifiers.new('rm', 'REMESH'); md.mode = 'VOXEL'; md.voxel_size = it['remesh_voxel']
            activate([lo])
            with bpy.context.temp_override(object=lo, active_object=lo):
                bpy.ops.object.modifier_apply(modifier=md.name)
            triangulate(lo)
            log(name, 'voksel ag', tris_of(lo.data))
        apply_decimate(lo, lod['tris'], solid=True)
        if tris_of(lo.data) > lod['tris'] * 1.3:  # sert yuzeyli (enkaz): kucuk parcalar, duzlemsel seyreltme, collapse
            cull_islands(lo, int(lod['tris'] * 1.1))
            md = lo.modifiers.new('plan', 'DECIMATE'); md.decimate_type = 'DISSOLVE'
            md.angle_limit = math.radians(it.get('planar_deg', 8)); md.delimit = set()
            activate([lo])
            with bpy.context.temp_override(object=lo, active_object=lo):
                bpy.ops.object.modifier_apply(modifier=md.name)
            triangulate(lo)
            apply_decimate(lo, lod['tris'], solid=True)
            log(name, 'duzlemsel seyreltme ->', tris_of(lo.data))
        b = bake_to_low(hi, lo, imgs, lod['tex'], prefix)
        b['albedo'] = grade(b['albedo'], it.get('grade'))
        lo.data.materials.append(make_material(f'{name}_govde{sfx}', save_png(b['albedo'], os.path.join(TMP, f'{prefix}_govde_albedo.png')),
                                               save_png(b['normal_bake'], os.path.join(TMP, f'{prefix}_govde_normal.png'), 'Non-Color'),
                                               save_png(b['rough'], os.path.join(TMP, f'{prefix}_govde_rough.png'), 'Non-Color'), False))
        if li == 0:
            hull_pts = [v.co.copy() for v in lo.data.vertices]
        lo.name = lo.data.name = prefix
        results.append(lo)
        log(name, f'lod{li}', tris_of(lo.data))
    bpy.data.objects.remove(hi)
    return results, hull_pts, {}


def build_tripo_tree(name, it, outdir):
    """Tripo agaci: yapraklar ve govde tek kaynasmis mesh (1-2 M ucgen). Yuzler doku rengine gore yaprak / odun ayrilir.
    Odun: seyreltilir, yeni UV, dokusu yuksek poligondan pisirilir. Yapraklar: konum + yon ile kumelenir (k-ortalama);
    her kume kendi duzleminden (ortalama normal) tek bir atlasa pisirilir ve o duzlemde alfa makasli bir kart olur
    (bilboard bulutu). Kart normali tac hacmine egilir. Kalin, yuvarlak Tripo yapraklarinin gorunusu dokuda kalir."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src = it['source']
    base = source_dir(src)
    objs = import_any(os.path.join(base, src['file']))
    o = objs[0]
    imgs = src_images(o.data.materials[0])
    me = o.data
    me.transform(o.matrix_world)
    P = len(me.polygons)
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    lv = np.empty(len(me.loops), np.int64); me.loops.foreach_get('vertex_index', lv)
    ls = np.empty(P, np.int64); me.polygons.foreach_get('loop_start', ls)
    lt = np.empty(P, np.int64); me.polygons.foreach_get('loop_total', lt)
    assert (lt == 3).all(), 'Tripo mesh ucgen degil'
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.data.foreach_get('uv', uv); uv = uv.reshape(-1, 2)
    li = ls[:, None] + np.arange(3)[None, :]          # (P, 3) dongu indeksleri
    tri = co[lv[li]]                                    # (P, 3, 3)
    tuv = uv[li]                                        # (P, 3, 2)
    # yaprak / odun: yuz UV merkezindeki albedo
    al = imgs['albedo']; W, H = al.size
    px = np.empty(W * H * 4, np.float32); al.pixels.foreach_get(px); px = px.reshape(H, W, 4)
    cu = tuv.mean(1)
    c = px[np.clip((cu[:, 1] * H).astype(int), 0, H - 1), np.clip((cu[:, 0] * W).astype(int), 0, W - 1), :3]
    lr = it.get('leaf_rule', {'g_over_r': 0.92, 'g_over_b': 1.4})
    if 'hue' in lr:  # ton araligi (derece) + en az doygunluk: turuncu / camgobegi yapraklar
        hh, ss = hue_sat(c)
        lo_h, hi_h = lr['hue']
        leaf = ((hh >= lo_h) & (hh <= hi_h) if lo_h <= hi_h else ((hh >= lo_h) | (hh <= hi_h))) & (ss >= lr.get('sat_min', 0.0))
    else:
        leaf = (c[:, 1] >= c[:, 0] * lr['g_over_r']) & (c[:, 1] > c[:, 2] * lr['g_over_b'])
    if lr.get('min_z'):  # govde dibindeki yesil yosun yaprak sayilmasin: bagil yukseklik alti hep odun
        tz = tri[..., 2].mean(1); z0, z1 = tri[..., 2].min(), tri[..., 2].max()
        leaf &= tz >= z0 + (z1 - z0) * lr['min_z']
    # olcek ve taban: odun tabaninin merkezi (0,0,0), hedef boy
    zmin, zmax = tri[..., 2].min(), tri[..., 2].max()
    wz = tri[~leaf].reshape(-1, 3)
    foot = wz[wz[:, 2] < zmin + (zmax - zmin) * 0.03]
    basept = np.array([foot[:, 0].mean(), foot[:, 1].mean(), zmin], np.float32)
    s = it['height'] / (zmax - zmin)
    tri = (tri - basept) * s
    bpy.data.objects.remove(o)
    log(name, 'ucgen', P, 'yaprak', int(leaf.sum()), 'odun', int((~leaf).sum()), 'olcek', round(float(s), 3))

    # odun: kaynak (pisirme hedefi) ve kucuk adaciklar yapraga
    wood_hi = mesh_from_tris('wood_hi', tri[~leaf].reshape(-1, 3), tuv[~leaf].reshape(-1, 2))
    bm = bmesh.new(); bm.from_mesh(wood_hi.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=it['height'] * 2e-6)
    bm.faces.ensure_lookup_table()
    seen = bytearray(len(bm.faces)); small = []
    for f in bm.faces:
        if seen[f.index]:
            continue
        stack = [f]; seen[f.index] = 1; comp = []
        while stack:
            q = stack.pop(); comp.append(q)
            for e in q.edges:
                for h in e.link_faces:
                    if not seen[h.index]:
                        seen[h.index] = 1; stack.append(h)
        if len(comp) < it.get('wood_min_faces', 2000):
            small += comp
    bmesh.ops.delete(bm, geom=small, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(wood_hi.data); bm.free()
    log(name, 'odun yuz', len(wood_hi.data.polygons), 'kucuk odun adacigi (yapraga):', len(small))
    hull_pts = [v.co.copy() for v in wood_hi.data.vertices if v.co.z < min(1.8, it['height'] * 0.35)]
    if hull_pts:
        ds = sorted(math.hypot(p.x, p.y) for p in hull_pts)
        lim = ds[int(len(ds) * it.get('hull_keep', 0.85))]
        hull_pts = [p for p in hull_pts if math.hypot(p.x, p.y) <= lim]

    # yaprak kumeleri (kucuk odun parcalari ince saplar: yaprak kartina pisirilir)
    ltri, luv = tri[leaf], tuv[leaf]
    e1 = ltri[:, 1] - ltri[:, 0]; e2 = ltri[:, 2] - ltri[:, 0]
    cr = np.cross(e1, e2); area = np.linalg.norm(cr, axis=1) * 0.5
    nrm = cr / np.maximum(area * 2, 1e-12)[:, None]
    ctr = ltri.mean(1)
    K = it.get('clusters', 900)
    rng = np.random.default_rng(it.get('seed', 7))
    lab = kmeans(np.hstack([ctr, nrm * it.get('normal_weight', 0.25) * it['height'] / 5.0]), area, K, 14, rng)
    order = np.argsort(lab, kind='stable'); bounds = np.searchsorted(lab[order], np.arange(K + 1))
    crown_c = (ctr * area[:, None]).sum(0) / area.sum()
    g = math.ceil(math.sqrt(K))
    clusters = []
    bake_co, bake_uv = [], []
    for k in range(K):
        idx = order[bounds[k]:bounds[k + 1]]
        if len(idx) < 3:
            continue
        a = area[idx]
        c0 = (ctr[idx] * a[:, None]).sum(0) / a.sum()
        nm = (nrm[idx] * a[:, None]).sum(0)
        pts = ltri[idx].reshape(-1, 3)
        d = pts - c0
        if np.linalg.norm(nm) < 0.35 * a.sum():   # iki yuzlu kalin yaprak: ortalama sonuk; PCA en ince eksen
            w_, v_ = np.linalg.eigh(d.T @ d); nm = v_[:, 0]
        n = nm / np.linalg.norm(nm)
        if np.dot(c0 - crown_c, n) < 0 and n[2] < 0.5:
            n = -n
        dp = d - np.outer(d @ n, n)
        w_, v_ = np.linalg.eigh(dp.T @ dp); t1 = v_[:, 2]
        t2 = np.cross(n, t1)
        F = np.stack([t1, t2, n])                    # yerel = F @ (p - c0)
        loc = d @ F.T
        lo_, hi_ = loc.min(0), loc.max(0)
        mid = (lo_ + hi_) / 2
        c0 = c0 + F.T @ np.array([mid[0], mid[1], 0.0])
        hx, hy = (hi_[0] - lo_[0]) / 2 * 1.02, (hi_[1] - lo_[1]) / 2 * 1.02
        half = max(hx, hy, 1e-4)
        tx, ty = len(clusters) % g, len(clusters) // g
        sc_ = 0.96 / (2 * half)
        q = (ltri[idx] - c0) @ F.T * sc_ + np.array([tx + 0.5, ty + 0.5, 0.0])
        bake_co.append(q.reshape(-1, 3)); bake_uv.append(luv[idx].reshape(-1, 2))
        um, vm = (tx + 0.5) / g, (ty + 0.5) / g
        du, dv = hx * sc_ / g, hy * sc_ / g
        clusters.append({'c': c0, 't1': t1, 't2': t2, 'n': n, 'hx': hx, 'hy': hy, 'uv': (um - du, vm - dv, um + du, vm + dv),
                         'area': float(a.sum())})
    log(name, 'kume', len(clusters), 'izgara', g)
    A = it.get('atlas', 4096)
    bo = mesh_from_tris('atlas_src', np.vstack(bake_co), np.vstack(bake_uv))
    bpy.context.scene.collection.objects.unlink(bo)
    atlas_a, atlas_n, atlas_r = bake_cards_atlas(bo, imgs, g, A)
    bpy.data.objects.remove(bo)
    atlas_a = grade(dilate(atlas_a, 8), it.get('leaf_grade'))
    atlas_n = dilate(atlas_n, 8); atlas_n[:, :, 3] = 1
    atlas_r = dilate(atlas_r, 8); atlas_r[:, :, 3] = 1
    rdir = os.path.join(ROOT, 'progress', 'bitki', 'render'); os.makedirs(rdir, exist_ok=True)
    save_png(resize(atlas_a, 1024), os.path.join(rdir, f'{name}_atlas.png'))

    # kart normalleri: tac hacmi (elipsoit) ile kart normali karisimi
    cs = np.array([cl['c'] for cl in clusters])
    cc = cs.mean(0); rad = np.maximum(np.abs(cs - cc).max(0), 1e-3)
    vol = it.get('volume_normals', 0.5)

    def card_mesh(keep, grow):
        sel = sorted(range(len(clusters)), key=lambda i: -clusters[i]['area'])[:keep]
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); normals = []
        for i in sel:
            cl = clusters[i]
            c0, t1, t2 = Vector(cl['c']), Vector(cl['t1']), Vector(cl['t2'])
            hx, hy = cl['hx'] * grow, cl['hy'] * grow
            u0, v0, u1, v1 = cl['uv']
            f = bm.faces.new([bm.verts.new(c0 + t1 * sx * hx + t2 * sy * hy) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
            um, vm, du, dv = (u0 + u1) / 2, (v0 + v1) / 2, (u1 - u0) / 2, (v1 - v0) / 2
            for lp, (sx, sy) in zip(f.loops, ((-1, -1), (1, -1), (1, 1), (-1, 1))):
                lp[uvl].uv = (um + sx * du, vm + sy * dv)
            e = (np.array(cl['c']) - cc) / rad
            e = Vector((e[0], e[1], e[2] * 0.8 + 0.25))
            ns = e.normalized() if e.length > 1e-4 else Vector((0, 0, 1))
            nc = Vector(cl['n'])
            if nc.dot(ns) < 0:
                nc = -nc
            normals.append((nc * (1 - vol) + ns * vol).normalized())
        m = bpy.data.meshes.new('cards'); bm.to_mesh(m); bm.free()
        ln = []
        for poly, nn in zip(m.polygons, normals):
            ln += [nn] * poly.loop_total
        m.normals_split_custom_set(ln)
        return new_obj('cards', m)

    results = []
    for li, lod in enumerate(it['lods']):
        sfx = '' if li == 0 else f'_lod{li}'
        prefix = name + sfx
        PREFIXES.add(prefix)
        wl = new_obj('wood', wood_hi.data.copy())
        apply_decimate(wl, lod['wood_tris'], solid=True)
        if tris_of(wl.data) > lod['wood_tris'] * 1.3:  # collapse takildi (cok parcali odun): en kucuk parcalar gider
            cull_islands(wl, lod['wood_tris'])
            apply_decimate(wl, lod['wood_tris'], solid=True)
        wtex = lod.get('wood_tex', lod['tex'] // 2)
        b = bake_to_low(wood_hi, wl, imgs, wtex, prefix)
        b['albedo'] = grade(b['albedo'], it.get('wood_grade'))
        wm = make_material(f'{name}_govde{sfx}', save_png(b['albedo'], os.path.join(TMP, f'{prefix}_govde_albedo.png')),
                           save_png(b['normal_bake'], os.path.join(TMP, f'{prefix}_govde_normal.png'), 'Non-Color'),
                           save_png(b['rough'], os.path.join(TMP, f'{prefix}_govde_rough.png'), 'Non-Color'), False)
        wl.data.materials.append(wm)
        keep = min(len(clusters), max(lod['tris'] // 6, (lod['tris'] - tris_of(wl.data)) // 2))  # uzakta tac govdeden onemli
        grow = math.sqrt(len(clusters) / keep) ** it.get('grow_power', 0.85) * it.get('card_scale', 1.0)
        cm = card_mesh(keep, grow)
        size = lod['tex']
        lm = make_material(f'{name}_yaprak{sfx}', save_png(resize(atlas_a, size), os.path.join(TMP, f'{prefix}_yaprak_albedo.png')),
                           save_png(resize(atlas_n, size), os.path.join(TMP, f'{prefix}_yaprak_normal.png'), 'Non-Color'),
                           save_png(resize(atlas_r, size), os.path.join(TMP, f'{prefix}_yaprak_rough.png'), 'Non-Color'), True)
        cm.data.materials.append(lm)
        log(name, f'lod{li}', 'odun', tris_of(wl.data), 'kart', keep, 'buyutme', round(grow, 2))
        results.append(join([wl, cm], name + sfx))
    bpy.data.objects.remove(wood_hi)
    return results, hull_pts, {'clusters': len(clusters), 'leaf_faces': int(leaf.sum())}


# ---------------------------------------------------------------- cikti

def export_glb(obj, path):
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    bpy.context.view_layer.objects.active = obj
    obj.location = (0, 0, 0)
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True,
                              export_yup=True, export_tangents=True, export_normals=True, export_materials='EXPORT',
                              export_image_format='AUTO', export_texcoords=True, export_attributes=False,
                              export_extras=False, export_cameras=False, export_lights=False, export_animations=False)


def write_hull(points, path, max_points=64):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    hv = [v.co.copy() for v in bm.verts if v.is_valid and v.link_faces]
    bm.free()
    if len(hv) > max_points:  # en uzak nokta orneklemesi
        chosen = [hv[0]]
        d = [ (p - hv[0]).length for p in hv]
        while len(chosen) < max_points:
            i = max(range(len(hv)), key=lambda k: d[k])
            chosen.append(hv[i])
            d = [min(d[k], (hv[k] - hv[i]).length) for k in range(len(hv))]
        hv = chosen
    flat = ', '.join(f'{p.x:.4f}, {p.z:.4f}, {-p.y:.4f}' for p in hv)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('[gd_resource type="ConvexPolygonShape3D" format=3]\n\n[resource]\npoints = PackedVector3Array(' + flat + ')\n')
    return len(hv)


def render_views(obj, icon_path, review_path, height):
    sc = bpy.context.scene
    for o in sc.objects:
        o.hide_render = o != obj
    mn, mx = bbox([obj])
    ctr = (mn + mx) / 2
    size = max((mx - mn).x, (mx - mn).y, (mx - mn).z)
    cam = bpy.data.objects.new('rcam', bpy.data.cameras.new('rcam'))
    sc.collection.objects.link(cam)
    cam.data.lens = 50
    d = size * 2.1
    cam.location = ctr + Vector((d * 0.75, -d * 0.75, d * 0.28))
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sun = bpy.data.objects.new('rsun', bpy.data.lights.new('rsun', 'SUN'))
    sun.data.energy = 3.5; sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(45), 0, math.radians(35))
    sc.collection.objects.link(sun)
    w = bpy.data.worlds.new('rw'); w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.55, 0.66, 0.82, 1)
    w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
    sc.world = w
    sc.render.engine = 'BLENDER_EEVEE'
    try:
        sc.compositing_node_group = None
    except Exception:
        pass
    sc.view_settings.view_transform = 'AgX'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    # oyuncu olcegi (1,8 m) cubugu yalniz inceleme goruntusunde
    bar = new_obj('olcek', bpy.data.meshes.new('olcek'))
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bm.to_mesh(bar.data); bm.free()
    bar.scale = (0.25, 0.25, 1.8); bar.location = (mx.x + 0.6, 0, 0.9)
    sc.render.film_transparent = False
    sc.render.resolution_x = 900; sc.render.resolution_y = 700
    sc.render.filepath = review_path
    bpy.ops.render.render(write_still=True)
    bar.hide_render = True
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 256
    cam.location = ctr + Vector((d * 0.6, -d * 0.6, d * 0.22))
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = icon_path
    bpy.ops.render.render(write_still=True)


def main():
    man = json.load(open(MANIFEST, encoding='utf-8-sig'))
    items = man['items']
    names = ONLY or [n for n, v in items.items() if not v.get('disabled')]
    for name in names:
        it = items[name]
        outdir = os.path.join(ROOT, arg('--out') or man.get('out', 'game/assets/models'), it.get('dir', name))
        os.makedirs(outdir, exist_ok=True)
        log('==', name, it['kind'])
        if it['kind'] == 'tree':
            objs, hull_pts, extra = build_tree(name, it, outdir)
        elif it['kind'] == 'tripo_tree':
            objs, hull_pts, extra = build_tripo_tree(name, it, outdir)
        elif it['kind'] == 'tripo_prop':
            objs, hull_pts, extra = build_tripo_prop(name, it, outdir)
        elif it['kind'] == 'stylized':
            objs, hull_pts, extra = build_stylized(name, it, outdir)
        else:
            objs, hull_pts, extra = build_plant(name, it, outdir)
        # sahnede yalniz ciktilar kalsin
        keep = set(objs)
        for o in list(bpy.context.scene.objects):
            if o not in keep:
                bpy.data.objects.remove(o)
        rep = {'name': name, 'label': it.get('label', name), 'kind': it['kind'], 'source': it['source'],
               'license': it.get('license', ''), 'height_target': it['height'], 'lods': []}
        for li, o in enumerate(objs):
            sfx = '' if li == 0 else f'_lod{li}'
            path = os.path.join(outdir, f'{name}{sfx}.glb')
            export_glb(o, path)
            mn, mx = bbox([o])
            rep['lods'].append({'path': f'res://{os.path.relpath(path, os.path.join(ROOT, "game")).replace(os.sep, "/")}',
                                'tris': tris_of(o.data), 'tex': it['lods'][li]['tex'],
                                'surfaces': len(o.data.materials)})
            if li == 0:
                rep['size_m'] = [round(mx.x - mn.x, 3), round(mx.y - mn.y, 3), round(mx.z - mn.z, 3)]
            log(name, f'lod{li}', tris_of(o.data), 'ucgen', len(o.data.materials), 'yuzey', path)
        if hull_pts:
            n = write_hull(hull_pts, os.path.join(outdir, f'{name}_hull.tres'))
            rep['hull_points'] = n
            if it['kind'] == 'tree' or it.get('tree'):
                ds = sorted(math.hypot(p.x, p.y) for p in hull_pts if p.z < 1.0)
                r = ds[int(len(ds) * 0.8)] if ds else 0.0  # taban kok cikintilari disarida
                rep['trunk_radius_m'] = round(r, 3)
        rep.update(extra)
        if not NO_RENDER:
            for o in objs[1:]:
                o.hide_render = True
            rdir = os.path.join(ROOT, 'progress', 'bitki', 'render')
            os.makedirs(rdir, exist_ok=True)
            render_views(objs[0], os.path.join(outdir, f'{name}_icon.png'), os.path.join(rdir, f'{name}.png'), it['height'])
        rep['tris'] = rep['lods'][0]['tris']
        with open(os.path.join(outdir, f'{name}.report.json'), 'w', encoding='utf-8', newline='\n') as f:
            json.dump(rep, f, indent=2, ensure_ascii=False)
    shutil.rmtree(TMP, ignore_errors=True)


main()
