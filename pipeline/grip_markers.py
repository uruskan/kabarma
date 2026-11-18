"""
Tutamak ve namlu isaretleri (oyuncu akisi, 18 Kasim 2026). Elde tutulan modellere adli bos dugumler koyar; oyun
(grips.json, Player.Grip.cs) nesneyi ele bu dugumlerle oturtur. Model degisince bu betik yeniden kosar, elle sayi
ayarlanmaz. KREDI HARCAMAZ.

  blender -b --factory-startup -P pipeline/grip_markers.py -- capa el_arabasi     # olc, isaretleri GLB'ye yaz
  blender -b --factory-startup -P pipeline/grip_markers.py -- capa --check        # yalniz olc ve yaz(dir)ma

Olcum modelin gercek geometrisinden (Godot ekseni: +Y yukari):
  capa
    grip    tutamagin avuc ortasi. Tutamak govdenin altindaki dar parca: ust ucu govdenin genislemeye basladigi yukseklik
            (yan yari genislik >= govdeninkinin BODY_WIDEN'i), eksen alt yarisindaki dilim merkezlerinden (en arkadaki
            bitisik kume: tetik ve korkuluk ayri kalir) cizgi olarak oturtulur; avuc ortasi ust uctan FIST_HALF asagida.
            Dugumun +Y'si eksen (basparmaga, yukari), +Z parmak bogumlari (namluya dogru, eksene dik), +X = Y x Z.
    muzzle  namlu ucu: en ondeki 1 cm'lik kesitin ortasi; +Z namlu ekseni (uc kesit ile 5 cm gerideki kesitten), +Y yukari.
            Namlu ekseni modelin en uzun yatay ekseni; yonu tutamagin karsi tarafi.
  el_arabasi
    once pipeline/fix_cart_handles.py: saplar capraz koselerdeyse (Tripo f2eb82e) kova arka sapli yarisindan
            aynalanir, iki sap arkaya simetrik gelir; zaten simetrikse dokunulmaz.
    handle_r, handle_l   arkadaki iki sapin avuc ortasi: sap ucunun ana ekseni uzerinde, uctan HANDLE_IN iceride.
            +Z parmaklar (bilekten bogumlara): asagi ve FINGER_OUT kadar disa (kol yanda sarkar, bilek duz);
            +Y sapin buna dik izdusumu (one dogru; sap uca dogru yukselir, avucun icinden capraz gecer).
            On, tekerlegin oldugu yon; sag = ileri x yukari. Sap kesiti avuc boyunda olmali (2-6 cm).
            Arkada simetrik bir sap cifti yoksa isaret YAZILMAZ ve betik basarisiz olur (model kusuru raporlanir).

Varsayimlar dogrulanir; tutmazsa "MARKER fail <ad>: neden" yazar, o modele dokunmaz ve cikis kodu 1 olur.
Dugumler GLB'nin JSON bolumune eklenir (agac ve dokular yeniden yazilmaz, ikili veri aynen kalir). Bu betigin
yazdigi dugum extras.kabarma = "grip_markers" tasir; yeniden kosunca yalniz onlar guncellenir.
"""
import json, os, struct, sys

import bpy
import numpy as np
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_cart_handles  # noqa: E402  El Arabasi'nin capraz saplari (olcmeden once duzeltilir)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MODELS = os.path.join(ROOT, "game", "assets", "models")
TAG = "grip_markers"
UP = np.array([0.0, 1.0, 0.0])

STEP = 0.01          # dilim kalinligi (m)
BODY_WIDEN = 0.8     # tutamagin ust ucu: dilimin yan yari genisligi govdeninkinin bu oranina ulasinca
FIST_HALF = 0.045    # avuc ortasi tutamak ust ucundan bu kadar asagida (oyuncu elinin yari yuksekligi, m)
GAP = 0.025          # dilimde tutamagi tetikten ayiran bosluk (m; tutamak ici bos bir kabuk, yan yuzunde koseler seyrek)
HANDLE_IN = 0.06     # arabada avuc ortasi sap ucundan bu kadar iceride (yari avuc + 1,5 cm, m)
FINGER_OUT = 0.15    # arabada parmaklar asagi ve bu oranda disa (saplar omuzlardan genis: kol hafif aciktir)


class Fail(Exception):
    pass


def req(ok, why):
    if not ok:
        raise Fail(why)


def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v


def frame_quat(y, z):
    """+Y ve +Z verilen cerceve -> glTF donusu [x, y, z, w]."""
    y = unit(y)
    z = unit(z - y * np.dot(z, y))
    x = np.cross(y, z)
    m = Matrix(((x[0], y[0], z[0]), (x[1], y[1], z[1]), (x[2], y[2], z[2])))
    q = m.to_quaternion()
    return [round(q.x, 6), round(q.y, 6), round(q.z, 6), round(q.w, 6)]


def load_points(path):
    """Modelin butun mesh koseleri, glTF/Godot ekseninde (Blender (x, y, z) -> glTF (x, z, -y)); ad -> nokta da doner."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    per = {}
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        b = np.array([o.matrix_world @ v.co for v in o.data.vertices])
        per[o.name] = np.stack([b[:, 0], b[:, 2], -b[:, 1]], 1)
    req(per, "mesh yok")
    return np.vstack(list(per.values())), per


def clusters_1d(vals, gap):
    """Sirali degerleri gap'ten buyuk bosluklarda boler; her kume (min, max, indeksler)."""
    order = np.argsort(vals)
    out, start = [], 0
    for i in range(1, len(order) + 1):
        if i == len(order) or vals[order[i]] - vals[order[i - 1]] > gap:
            idx = order[start:i]
            out.append((vals[idx].min(), vals[idx].max(), idx))
            start = i
    return out


# ---------------------------------------------------------------- Capa

def measure_capa(P):
    lo, hi = P.min(0), P.max(0)
    ext = hi - lo
    H = ext[1]
    ax = 2 if ext[2] >= ext[0] else 0
    fwd = np.zeros(3)
    fwd[ax] = 1.0
    lower = P[P[:, 1] < lo[1] + 0.4 * H]
    s_mid = ((lo + hi) / 2) @ fwd
    if (lower @ fwd).mean() > s_mid:
        fwd = -fwd
        s_mid = -s_mid
    L = ext[ax]
    req((lower @ fwd).mean() < s_mid - 0.1 * L, "tutamak namlunun arkasinda degil (alt kisim ortada)")
    side = np.cross(UP, fwd)
    s, lat, y = P @ fwd, P @ side, P[:, 1]

    # Dilimler: yan yari genislik; tutamagin ust ucu govdenin genisledigi yer
    ys = np.arange(lo[1], hi[1], STEP)
    half = []
    for y0 in ys:
        m = (y >= y0) & (y < y0 + STEP)
        half.append((lat[m].max() - lat[m].min()) / 2 if m.any() else 0.0)
    half = np.array(half)
    body = half.max()
    top = None
    for y0, h in zip(ys, half):
        if y0 >= lo[1] + 0.3 * H and h >= BODY_WIDEN * body:
            top = y0
            break
    req(top is not None, "govde bulunamadi (genis ust kisim yok)")
    req(top - lo[1] > 0.08, f"tutamak cok kisa ({top - lo[1]:.3f} m)")

    # Eksen: tutamagin alt yarisindaki dilimlerde en arkadaki bitisik kumenin ortasi
    a, b = lo[1] + 0.25 * (top - lo[1]), lo[1] + 0.65 * (top - lo[1])
    cy, cs, cl, depth, width = [], [], [], [], []
    for y0 in np.arange(a, b, STEP):
        m = (y >= y0) & (y < y0 + STEP) & (s < s_mid)
        if m.sum() < 6:
            continue
        ss, ll = s[m], lat[m]
        c = min(clusters_1d(ss, GAP), key=lambda k: k[0])
        idx = c[2]
        cy.append(y0 + STEP / 2)
        cs.append((ss[idx].min() + ss[idx].max()) / 2)
        cl.append((ll[idx].min() + ll[idx].max()) / 2)
        depth.append(ss[idx].max() - ss[idx].min())
        width.append(ll[idx].max() - ll[idx].min())
    req(len(cy) >= 4, f"tutamak dilimi az ({len(cy)})")
    d, w = float(np.median(depth)), float(np.median(width))
    req(0.03 < d < 0.13 and 0.03 < w < 0.10, f"tutamak avuc boyunda degil (derinlik {d:.3f}, genislik {w:.3f} m)")
    ks, bs = np.polyfit(cy, cs, 1)
    kl, bl = np.polyfit(cy, cl, 1)
    bar = unit(fwd * ks + side * kl + UP)
    lean = np.degrees(np.arccos(np.clip(bar @ UP, -1, 1)))
    req(lean < 40, f"tutamak cok yatik ({lean:.0f} derece)")
    yf = top - FIST_HALF
    grip = fwd * (ks * yf + bs) + side * (kl * yf + bl) + UP * yf
    fingers = fwd - bar * (bar @ fwd)

    # Namlu: en ondeki kesit ve 5 cm gerisi
    smax = s.max()
    tip = s > smax - 0.01
    t_lat = (lat[tip].min() + lat[tip].max()) / 2
    t_y = (y[tip].min() + y[tip].max()) / 2
    r = max(lat[tip].max() - lat[tip].min(), y[tip].max() - y[tip].min()) / 2 + 0.02
    back = (s > smax - 0.06) & (s < smax - 0.04) & (np.abs(lat - t_lat) < r) & (np.abs(y - t_y) < r)
    req(back.sum() >= 6, "namlu kesiti bulunamadi")
    b_lat = (lat[back].min() + lat[back].max()) / 2
    b_y = (y[back].min() + y[back].max()) / 2
    muzzle = fwd * smax + side * t_lat + UP * t_y
    axis = unit(fwd * 0.05 + side * (t_lat - b_lat) + UP * (t_y - b_y))
    tilt = np.degrees(np.arccos(np.clip(axis @ fwd, -1, 1)))
    req(tilt < 12, f"namlu ekseni modelin uzun ekseninden {tilt:.0f} derece sapiyor")
    req((muzzle - grip) @ fwd > 0.15, "namlu tutamagin yeterince onunde degil")
    info = (f"eksen={'XZ'[ax // 2]}{'+' if fwd[ax] > 0 else '-'} tutamak_ust={top:.3f} egim={lean:.0f}d "
            f"derinlik={d:.3f} genislik={w:.3f} namlu_sapma={tilt:.1f}d")
    return {
        "grip": (grip, bar, fingers),
        "muzzle": (muzzle, UP, axis),
    }, info


# ---------------------------------------------------------------- El Arabasi

def measure_cart(P, per):
    lo, hi = P.min(0), P.max(0)
    ext = hi - lo
    H = ext[1]
    ax = 0 if ext[0] >= ext[2] else 2
    fwd = np.zeros(3)
    fwd[ax] = 1.0
    req("wheel" in per, "tekerlek parcasi ('wheel') yok: on yon bilinemez")
    if per["wheel"].mean(0) @ fwd < ((lo + hi) / 2) @ fwd:
        fwd = -fwd
    right = np.cross(fwd, UP)
    s, lat, y = P @ fwd, P @ right, P[:, 1]
    L = ext[ax]
    band = np.abs(s - (s.min() + s.max()) / 2) < 0.25 * L
    mid = (lat[band].min() + lat[band].max()) / 2      # kovanin yana ortasi
    rear = (s < s.min() + 0.15 * L) & (y > lo[1] + 0.5 * H)
    cl = clusters_1d(lat[rear], 0.05)
    found = ", ".join(f"yan {(a + b) / 2 - mid:+.2f} m" for a, b, _ in cl)
    # Arka disindaki ust uclar (sap gibi cikan parcalar) bilgi icin
    top = y > hi[1] - 0.1 * H
    tops = clusters_1d(s[top], 0.08)
    where = "; ".join(f"ileri {(a + b) / 2:+.2f} m yan {lat[top][i].mean() - mid:+.2f} m" for a, b, i in tops)
    req(len(cl) == 2, f"arkada simetrik sap cifti yok: arkada {len(cl)} sap ucu ({found}); en ust uclar: {where}")
    (a1, b1, i1), (a2, b2, i2) = sorted(cl, key=lambda k: k[0])
    m1, m2 = (a1 + b1) / 2 - mid, (a2 + b2) / 2 - mid
    req(abs(m1 + m2) < 0.05 and 0.25 < m2 - m1 < 0.9, f"saplar simetrik degil ({m1:+.2f}, {m2:+.2f} m)")
    R = P[rear]
    out, info = {}, []
    for name, idx, sgn in (("handle_l", i1, -1.0), ("handle_r", i2, 1.0)):
        # Sap ucu bir cubuk: arka bolgedeki koselerin ana ekseni (one dogru), kesiti eksene uzaklik
        pts = R[idx]
        c = pts.mean(0)
        _, sv, vt = np.linalg.svd(pts - c)
        req(sv[0] > 2.5 * sv[1], f"{name}: sap ucu cubuk degil (tekil degerler {sv[0]:.2f}, {sv[1]:.2f})")
        stick = unit(vt[0] if vt[0] @ fwd > 0 else -vt[0])
        t = (pts - c) @ stick
        diam = 2 * float(np.median(np.linalg.norm((pts - c) - np.outer(t, stick), axis=1)))
        req(0.02 < diam < 0.06, f"{name}: sap avuc boyunda degil (cap {diam:.3f} m)")
        req(t.max() - t.min() > 2 * HANDLE_IN, f"{name}: sap ucu kisa ({t.max() - t.min():.3f} m)")
        slope = np.degrees(np.arcsin(np.clip(-stick[1], -1, 1)))
        req(slope < 45, f"{name}: sap cok dik ({slope:.0f} derece)")
        # Avuc ortasi sapin ekseninde, uctan HANDLE_IN iceride. Parmaklar (bilekten bogumlara) asagi ve biraz disa:
        # kol yanda sarkar, bilek duz kalir; sap avucun icinden capraz gecer (bar, sapin parmaklara dik izdusumu).
        grip = c + stick * (t.min() + HANDLE_IN)
        fingers = unit(-UP + right * sgn * FINGER_OUT)
        bar = unit(stick - fingers * (stick @ fingers))
        out[name] = (grip, bar, fingers)
        info.append(f"{name[-1]}: cap {diam * 100:.1f} cm egim {slope:.0f}d avucta capraz {np.degrees(np.arccos(np.clip(bar @ stick, -1, 1))):.0f}d "
                    f"yukseklik {grip[1]:.3f}")
    gl_, gr_ = out["handle_l"][0], out["handle_r"][0]
    req(abs(gl_[1] - gr_[1]) < 0.01 and abs((gl_ - gr_) @ fwd) < 0.01, "iki sapin avuc ortasi ayni yukseklikte ve hizada degil")
    return out, f"saplar yan {m1:+.2f} / {m2:+.2f} m; " + "; ".join(info)


# ---------------------------------------------------------------- GLB

GLB_MAGIC, JSON_T = 0x46546C67, 0x4E4F534A


def read_glb(path):
    b = open(path, "rb").read()
    magic, _ver, length = struct.unpack_from("<III", b, 0)
    req(magic == GLB_MAGIC, "GLB degil")
    off, chunks = 12, []
    while off < length:
        n, t = struct.unpack_from("<II", b, off)
        chunks.append([t, b[off + 8: off + 8 + n]])
        off += 8 + n
    req(chunks and chunks[0][0] == JSON_T, "GLB JSON bolumu yok")
    return chunks


def write_glb(path, chunks):
    body = bytearray()
    for t, data in chunks:
        data = bytes(data) + (b" " if t == JSON_T else b"\0") * ((4 - len(data) % 4) % 4)
        body += struct.pack("<II", len(data), t) + data
    with open(path, "wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, 12 + len(body)) + body)


def patch(path, markers):
    chunks = read_glb(path)
    gl = json.loads(chunks[0][1].decode("utf-8").rstrip(" \0"))
    nodes = gl.setdefault("nodes", [])
    scene = gl["scenes"][gl.get("scene", 0)].setdefault("nodes", [])
    for name, (pos, ydir, zdir) in markers.items():
        node = {"name": name, "translation": [round(float(v), 5) for v in pos], "rotation": frame_quat(ydir, zdir),
                "extras": {"kabarma": TAG}}
        idx = next((i for i, n in enumerate(nodes) if n.get("name") == name), None)
        if idx is None:
            nodes.append(node)
            scene.append(len(nodes) - 1)
        else:
            req((nodes[idx].get("extras") or {}).get("kabarma") == TAG, f"modelde '{name}' adli baska bir dugum var")
            req(idx in scene, f"'{name}' kok dugum degil")
            nodes[idx] = node
    chunks[0][1] = json.dumps(gl, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    write_glb(path, chunks)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    check = "--check" in argv
    keys = [a for a in argv if not a.startswith("--")] or ["capa", "el_arabasi"]
    bad = 0
    for key in keys:
        path = os.path.join(MODELS, key, f"{key}.glb")
        try:
            req(os.path.exists(path), f"dosya yok {path}")
            if key == "el_arabasi":
                # Saplar capraz koselerdeyse (Tripo f2eb82e) once arkaya simetrik getirilir; zaten simetrikse dokunulmaz.
                changed, msg = fix_cart_handles.fix(path, check)
                print(f"CARTFIX ok {key} {'duzeltildi' if changed else 'degismedi'}: {msg}", flush=True)
            P, per = load_points(path)
            if key == "capa":
                markers, info = measure_capa(P)
            elif key == "el_arabasi":
                markers, info = measure_cart(P, per)
            else:
                raise Fail("bu model icin kural yok")
            desc = " ".join(f"{n}=({', '.join(f'{v:+.3f}' for v in m[0])})" for n, m in markers.items())
            if not check:
                patch(path, markers)
            print(f"MARKER ok {key} {desc} {info}{' (yalniz olcum)' if check else ''}", flush=True)
        except (Fail, fix_cart_handles.Fail) as e:
            bad += 1
            print(f"MARKER fail {key}: {e}", flush=True)
    sys.stdout.flush()
    os._exit(1 if bad else 0)


main()
