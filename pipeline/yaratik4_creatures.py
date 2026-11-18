"""yaratik4 (M4, ICERIK §3.3-3.6): yeni biyomlarin 15 yaratiginin veri satirlari.

Uretir ve yalniz EKLER (ayni kimlik varsa atlar):
  game/data/creatures.json  -> 15 tur satiri (creatures dizisinin sonuna)
  game/data/bodies.json     -> 14 koddan govde + suluk_surusu uye govdesi (yer tutucu, URETIM §3; Tripo modeli gelene kadar)
  game/data/poses.json      -> "arthropod" govde plani (6/8 bacak: Kiskac, Kivrik, Billur)

Kosu:  python -I pipeline/yaratik4_creatures.py   (kok dizinden)
Kredi harcamaz; hicbir sey indirmez.
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "game", "data")
PH = "Tripo modeli bekleniyor (yaratik4 uretim istegi); koddan parca govde"


def P(bone, shape, size, pos, color, rot=None, rnd=None, mirror=False):
    d = {"bone": bone, "shape": shape, "size": size, "pos": pos}
    if rot:
        d["rot"] = rot
    d["color"] = color
    if rnd is not None:
        d["round"] = rnd
    if mirror:
        d["mirror"] = True
    return d


def B(name, parent, pos):
    return {"name": name, "parent": parent, "pos": pos}


# ---------- iskelet kaliplari (poses.json rol adlari) ----------

def quad_bones(y, bl, w, leg_y, head_y, head_z, ear=(0.15, 0.1), tail_z=None):
    tz = tail_z if tail_z is not None else -bl * 0.62
    return [
        B("root", "", [0, y, -bl * 0.3]),
        B("spine", "root", [0, y + 0.02, bl * 0.28]),
        B("head", "spine", [0, head_y, head_z]),
        B("top", "spine", [0, y + 0.25, 0.0]),
        B("ear_l", "head", [ear[0], head_y + ear[1], head_z + 0.04]),
        B("tail", "root", [0, y + 0.06, tz]),
        B("front_l", "spine", [w, y - 0.08, bl * 0.36]),
        B("frontlow_l", "front_l", [w, leg_y, bl * 0.38]),
        B("back_l", "root", [w, y - 0.08, -bl * 0.38]),
        B("backlow_l", "back_l", [w, leg_y, -bl * 0.4]),
    ]


QUAD_ROLES = ["root", "spine", "head", "top", "ear_l", "ear_r", "tail", "front_l", "frontlow_l", "front_r", "frontlow_r",
              "back_l", "backlow_l", "back_r", "backlow_r"]


def bird_bones(body_y, head_y, head_z, wing_x, leg_x, knee_y, tail_z=-0.2):
    return [
        B("root", "", [0, body_y, 0]),
        B("body", "root", [0, body_y, 0.0]),
        B("head", "body", [0, head_y, head_z]),
        B("comb", "head", [0, head_y + 0.2, head_z]),
        B("tail", "body", [0, body_y - 0.03, tail_z]),
        B("wing_l", "body", [wing_x, body_y + 0.12, -0.02]),
        B("thigh_l", "body", [leg_x, body_y - 0.08, 0.02]),
        B("shin_l", "thigh_l", [leg_x, knee_y, 0.04]),
        B("foot_l", "shin_l", [leg_x, 0.04, 0.06]),
    ]


BIRD_ROLES = ["root", "body", "head", "comb", "tail", "wing_l", "wing_r", "thigh_l", "shin_l", "foot_l", "thigh_r", "shin_r", "foot_r"]


def rept_bones(y, bl, w, legs=True, head_z=None, tail_z=None):
    hz = head_z if head_z is not None else bl * 0.62
    tz = tail_z if tail_z is not None else -bl * 0.62
    b = [
        B("root", "", [0, y, -bl * 0.15]),
        B("spine", "root", [0, y + 0.01, bl * 0.25]),
        B("head", "spine", [0, y + 0.03, hz]),
        B("tail", "root", [0, y, tz]),
        B("tail2", "tail", [0, y - 0.02, tz * 1.6]),
    ]
    if legs:
        b += [
            B("front_l", "spine", [w, y - 0.03, bl * 0.3]),
            B("frontlow_l", "front_l", [w * 1.8, 0.05, bl * 0.32]),
            B("back_l", "root", [w, y - 0.03, -bl * 0.25]),
            B("backlow_l", "back_l", [w * 1.8, 0.05, -bl * 0.27]),
        ]
    return b


REPT_ROLES = ["root", "spine", "head", "tail", "tail2", "front_l", "frontlow_l", "front_r", "frontlow_r", "back_l", "backlow_l",
              "back_r", "backlow_r"]


def arth_bones(y, w, zs, head_z, tail_z, claw=None, tail2_y=None):
    b = [
        B("root", "", [0, y, -0.05]),
        B("spine", "root", [0, y + 0.02, 0.15]),
        B("head", "spine", [0, y + 0.02, head_z]),
        B("tail", "root", [0, y + 0.04, tail_z]),
        B("tail2", "tail", [0, tail2_y if tail2_y is not None else y + 0.04, tail_z * 1.7]),
    ]
    if claw:
        b.append(B("claw_l", "spine", claw))
    for i, z in enumerate(zs, start=1):
        b.append(B(f"leg{i}_l", "spine" if z > 0 else "root", [w, y - 0.02, z]))
        b.append(B(f"leg{i}low_l", f"leg{i}_l", [w * 2.1, 0.06, z * 1.05]))
    return b


def arth_roles(n_legs, claw):
    r = ["root", "spine", "head", "tail", "tail2"]
    if claw:
        r += ["claw_l", "claw_r"]
    for i in range(1, n_legs + 1):
        r += [f"leg{i}_l", f"leg{i}low_l", f"leg{i}_r", f"leg{i}low_r"]
    return r


def legs_arth(bones, color, low_color, thick=0.06):
    """Ust bacak kemikten diz kemigine uzanan kapsul, uc kisim sivri ayak; konumlar kemiklerden hesaplanir."""
    import math
    bp = {b["name"]: b["pos"] for b in bones}
    out = []
    i = 1
    while f"leg{i}_l" in bp:
        a, c = bp[f"leg{i}_l"], bp[f"leg{i}low_l"]
        dx, dy, dz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
        ln = math.sqrt(dx * dx + dy * dy + dz * dz)
        rz = round(math.degrees(math.atan2(dx, -dy)), 1)
        mid = [round((a[0] + c[0]) / 2, 3), round((a[1] + c[1]) / 2, 3), round((a[2] + c[2]) / 2, 3)]
        out.append(P(f"leg{i}_l", "capsule", [thick, round(ln, 3), thick], mid, color, rot=[0, 0, rz], mirror=True))
        out.append(P(f"leg{i}low_l", "cone", [thick * 0.9, 0.12, thick * 0.9], [c[0], round(c[1] - 0.02, 3), c[2]], low_color, rot=[180, 0, 0], mirror=True))
        i += 1
    return out

def fix_part_pos(bones, parts):
    """Parcalarin pos'u kemik konumuna gore verilmissa (rel=True isaretli) model uzayina cevrilir."""
    bp = {b["name"]: b["pos"] for b in bones}
    for p in parts:
        if p.pop("_rel", False):
            o = bp[p["bone"]]
            p["pos"] = [round(o[0] + p["pos"][0], 3), round(o[1] + p["pos"][1], 3), round(o[2] + p["pos"][2], 3)]
    return parts


def rel(p):
    p["_rel"] = True
    return p


def eyes(head_pos, dx, dy, dz, r=0.06, pupil="eye", ball="eyeball"):
    x, y, z = head_pos
    return [
        P("head", "sphere", [r, r, r], [x + dx, y + dy, z + dz], ball, mirror=True),
        P("head", "sphere", [r * 0.45, r * 0.55, r * 0.3], [x + dx, y + dy + 0.005, z + dz + r * 0.45], pupil, mirror=True),
    ]


# ---------- turler ----------
BODIES = {}
CREATURES = []

SWELL_M = {"push": 0.04, "wobble": 0.03, "glow": 0.9, "veinScale": 4.0, "seconds": 1.2, "stepJiggle": 0.012, "hitJiggle": 0.03}
SWELL_L = {"push": 0.05, "wobble": 0.03, "glow": 0.8, "veinScale": 3.0, "seconds": 1.5, "stepJiggle": 0.016, "hitJiggle": 0.03}


def creature(id_, name, arch, weight, hp, dmg, speed, biome, plan, roles, height, wild, swell=SWELL_M, scale=2.4, extra=None):
    c = {"id": id_, "name": name, "archetype": arch, "weight": weight, "hp": hp, "damage": dmg, "speed": speed,
         "swollenScale": scale, "weakPointMult": 2.0, "biome": biome,
         "rig": {"plan": plan, "model": "code:" + id_, "height": height, "yaw": 0, "triBudget": 8000,
                 "bones": {r: r for r in roles}, "placeholder": ["walk"]},
         "swell": swell, "wild": wild}
    if extra:
        c.update(extra)
    CREATURES.append(c)


def body(id_, colors, bones, parts, sac_bone, sac_pos, sac_r=0.09):
    BODIES[id_] = {"placeholder": PH, "colors": colors, "bones": bones, "parts": fix_part_pos(bones, parts),
                   "sacBone": sac_bone, "sacPos": sac_pos, "sacRadius": sac_r}


# --- Isil Sazlik ---
# Vrak: iri, isildayan bir kurbaga; dilini firlatir.
bn = quad_bones(0.32, 0.7, 0.2, 0.08, 0.42, 0.4, ear=(0.13, 0.12), tail_z=-0.3)
body("vrak", {"skin": "3FA36B", "belly": "D8F0B0", "glow": "7FF2D2", "eye": "1A1A22", "eyeball": "F4E36A", "mouth": "E06A7A"}, bn, [
    P("root", "pillow", [0.62, 0.42, 0.62], [0, 0.32, -0.12], "skin", rnd=0.9),
    P("spine", "pillow", [0.58, 0.38, 0.48], [0, 0.34, 0.2], "skin", rnd=0.9),
    P("root", "pillow", [0.5, 0.14, 0.8], [0, 0.17, 0.02], "belly", rnd=0.9),
    P("head", "pillow", [0.56, 0.24, 0.36], [0, 0.42, 0.42], "skin", rnd=0.85),
    P("head", "pillow", [0.46, 0.04, 0.06], [0, 0.36, 0.6], "mouth", rnd=0.8),
    P("head", "sphere", [0.16, 0.16, 0.16], [0.17, 0.56, 0.42], "skin", mirror=True),
    P("head", "sphere", [0.12, 0.12, 0.12], [0.17, 0.58, 0.47], "eyeball", mirror=True),
    P("head", "sphere", [0.05, 0.08, 0.03], [0.17, 0.59, 0.53], "eye", mirror=True),
    P("top", "sphere", [0.1, 0.05, 0.1], [0.14, 0.53, 0.0], "glow", mirror=True),
    P("top", "sphere", [0.08, 0.04, 0.08], [0.0, 0.55, 0.16], "glow"),
    P("front_l", "capsule", [0.1, 0.2, 0.1], [0.24, 0.18, 0.27], "skin", rot=[0, 0, 30], mirror=True),
    P("frontlow_l", "pillow", [0.16, 0.04, 0.16], [0.26, 0.03, 0.3], "belly", rnd=0.7, mirror=True),
    P("back_l", "pillow", [0.2, 0.26, 0.36], [0.27, 0.2, -0.28], "skin", rnd=0.85, mirror=True),
    P("backlow_l", "pillow", [0.2, 0.05, 0.3], [0.3, 0.03, -0.18], "belly", rnd=0.7, mirror=True),
], "spine", [0.0, 0.55, 0.05], 0.1)
creature("vrak", "Vrak", "jumper", 3, 110, 9, 3.4, "isil_sazlik", "quadruped", QUAD_ROLES, 0.7, {
    "stance": "predator", "active": "always", "wanderRadius": 8, "walkSpeedMult": 0.3, "runSpeedMult": 0.9,
    "alertRadius": 10, "chargeRadius": 7, "attackRange": 4.5, "attackInterval": 2.2, "attackDamage": 5, "warnSeconds": 1.0,
    "giveUpRadius": 16, "projectile": "tongue", "pauseMin": 3, "pauseMax": 7, "idleShare": 0.6, "grazeShare": 0.2})

# Kiskac: alti bacakli, zirhli, iki dev kiskacli yengec.
zs = [0.2, 0.0, -0.2]
bn = arth_bones(0.3, 0.26, zs, 0.42, -0.35, claw=[0.36, 0.34, 0.5])
body("kiskac", {"shell": "6A7F8C", "rim": "C8B07A", "under": "D9C9A4", "claw": "8C5A4A", "eye": "16161C", "eyeball": "F2F2E6", "glow": "6FE3E0"}, bn, [
    P("root", "pillow", [0.86, 0.3, 0.8], [0, 0.36, 0.0], "shell", rnd=0.7),
    P("root", "pillow", [0.9, 0.08, 0.84], [0, 0.24, 0.0], "rim", rnd=0.6),
    P("root", "pillow", [0.7, 0.12, 0.66], [0, 0.18, 0.0], "under", rnd=0.7),
    P("root", "sphere", [0.12, 0.06, 0.12], [0.2, 0.52, -0.05], "glow", mirror=True),
    P("head", "cylinder", [0.04, 0.16, 0.04], [0.1, 0.5, 0.4], "shell", mirror=True),
    P("head", "sphere", [0.08, 0.08, 0.08], [0.1, 0.6, 0.41], "eyeball", mirror=True),
    P("head", "sphere", [0.035, 0.045, 0.02], [0.1, 0.6, 0.45], "eye", mirror=True),
    P("claw_l", "capsule", [0.1, 0.3, 0.1], [0.32, 0.32, 0.42], "claw", rot=[90, 0, 30], mirror=True),
    P("claw_l", "pillow", [0.22, 0.16, 0.3], [0.4, 0.34, 0.62], "claw", rnd=0.6, mirror=True),
    P("claw_l", "cone", [0.1, 0.22, 0.08], [0.36, 0.38, 0.8], "rim", rot=[-90, 0, 0], mirror=True),
] + legs_arth(bn, "shell", "rim", 0.07), "root", [0.0, 0.56, 0.0], 0.12)
creature("kiskac", "Kıskaç", "tank", 5, 260, 12, 1.6, "isil_sazlik", "arthropod", arth_roles(3, True), 0.6, {
    "stance": "calm", "active": "day", "wanderRadius": 6, "walkSpeedMult": 0.4, "runSpeedMult": 0.8, "fleeOnHit": False,
    "pauseMin": 4, "pauseMax": 9, "idleShare": 0.5, "grazeShare": 0.4}, swell=SWELL_L, scale=2.0)

# Tukurgen: uzun bacakli, boynu kivrik bataklik kusu; asit tukurur.
bn = bird_bones(0.95, 1.35, 0.18, 0.17, 0.08, 0.5, tail_z=-0.3)
body("tukurgen", {"feather": "5C8C7A", "belly": "D6E2C4", "beak": "E2B04A", "acid": "B8F04A", "leg": "D88A4A", "eye": "1A1A1A", "eyeball": "F7F2C0"}, bn, [
    P("body", "pillow", [0.34, 0.3, 0.52], [0, 0.98, -0.04], "feather", rnd=0.85),
    P("body", "pillow", [0.26, 0.18, 0.34], [0, 0.9, 0.04], "belly", rnd=0.85),
    P("body", "capsule", [0.08, 0.36, 0.08], [0, 1.18, 0.12], "feather", rot=[25, 0, 0]),
    P("head", "sphere", [0.18, 0.16, 0.2], [0, 1.36, 0.18], "feather"),
    P("head", "cone", [0.07, 0.26, 0.07], [0, 1.34, 0.38], "beak", rot=[-90, 0, 0]),
    P("head", "sphere", [0.07, 0.05, 0.05], [0, 1.3, 0.3], "acid"),
] + eyes([0, 1.39, 0.2], 0.07, 0, 0.04, 0.06) + [
    P("wing_l", "pillow", [0.08, 0.22, 0.42], [0.17, 1.0, -0.06], "feather", rnd=0.7, mirror=True),
    P("tail", "pillow", [0.22, 0.05, 0.22], [0, 0.94, -0.36], "feather", rnd=0.7),
    P("thigh_l", "capsule", [0.05, 0.4, 0.05], [0.08, 0.68, 0.03], "leg", mirror=True),
    P("shin_l", "capsule", [0.04, 0.46, 0.04], [0.08, 0.27, 0.05], "leg", mirror=True),
    P("foot_l", "pillow", [0.12, 0.03, 0.16], [0.08, 0.03, 0.09], "leg", rnd=0.6, mirror=True),
], "body", [0.0, 1.12, -0.05])
creature("tukurgen", "Tükürgen", "ranged", 2, 70, 7, 4.0, "isil_sazlik", "bird", BIRD_ROLES, 1.45, {
    "stance": "predator", "active": "day", "wanderRadius": 12, "walkSpeedMult": 0.35, "runSpeedMult": 1.0,
    "alertRadius": 14, "chargeRadius": 11, "attackRange": 9, "attackInterval": 2.6, "attackDamage": 6, "warnSeconds": 1.2,
    "giveUpRadius": 22, "projectile": "acid", "pauseMin": 2, "pauseMax": 5, "idleShare": 0.5, "grazeShare": 0.3})

# Cizbiz: elektrikli yilan baligi; bacaksiz, yerde suzulerek kayar, sirtinda isik yuzgeci.
bn = rept_bones(0.18, 0.9, 0.12, legs=False, head_z=0.5, tail_z=-0.45)
body("cizbiz", {"skin": "2C4C8C", "belly": "9FD0E8", "spark": "BDF4FF", "fin": "6FB8FF", "eye": "101018", "eyeball": "F8F870"}, bn, [
    P("root", "capsule", [0.22, 0.6, 0.22], [0, 0.18, -0.1], "skin", rot=[90, 0, 0]),
    P("spine", "capsule", [0.24, 0.5, 0.24], [0, 0.19, 0.25], "skin", rot=[90, 0, 0]),
    P("root", "pillow", [0.14, 0.06, 0.8], [0, 0.08, 0.05], "belly", rnd=0.8),
    P("head", "pillow", [0.26, 0.2, 0.3], [0, 0.21, 0.52], "skin", rnd=0.8),
    P("tail", "capsule", [0.16, 0.42, 0.16], [0, 0.17, -0.52], "skin", rot=[90, 0, 0]),
    P("tail2", "cone", [0.14, 0.4, 0.06], [0, 0.16, -0.86], "fin", rot=[-90, 0, 0]),
    P("spine", "pillow", [0.03, 0.14, 0.6], [0, 0.34, 0.05], "fin", rnd=0.5),
    P("spine", "sphere", [0.05, 0.05, 0.05], [0.1, 0.27, 0.22], "spark", mirror=True),
    P("root", "sphere", [0.05, 0.05, 0.05], [0.1, 0.26, -0.12], "spark", mirror=True),
    P("head", "cylinder", [0.015, 0.16, 0.015], [0.08, 0.22, 0.68], "spark", rot=[60, 0, -20], mirror=True),
] + eyes([0, 0.26, 0.56], 0.09, 0, 0.04, 0.06), "spine", [0.0, 0.33, 0.15], 0.08)
creature("cizbiz", "Cızbız", "ranged", 3, 100, 8, 2.6, "isil_sazlik", "reptile", REPT_ROLES[:5], 0.35, {
    "stance": "predator", "active": "always", "wanderRadius": 6, "walkSpeedMult": 0.35, "runSpeedMult": 0.9,
    "alertRadius": 7, "chargeRadius": 5, "attackRange": 3.5, "attackInterval": 2.0, "attackDamage": 7, "warnSeconds": 1.4,
    "giveUpRadius": 12, "projectile": "shock", "pauseMin": 3, "pauseMax": 6, "idleShare": 0.6, "grazeShare": 0.2})

# Suluk Surusu: suda bekleyen sulukler (suru uyesi govdesi).
BODIES["suluk_surusu"] = {"placeholder": "Suru uyesi; Tripo modeli bekleniyor (yaratik4 uretim istegi), koddan",
                          "colors": {"body": "3A2A3E", "band": "A0384A", "mouth": "E0607A"},
                          "bones": [B("root", "", [0, 0, 0])],
                          "parts": [P("root", "capsule", [0.07, 0.24, 0.07], [0, 0, 0], "body", rot=[90, 0, 0]),
                                    P("root", "cylinder", [0.075, 0.03, 0.075], [0, 0, 0.04], "band", rot=[90, 0, 0]),
                                    P("root", "sphere", [0.05, 0.05, 0.03], [0, 0, 0.12], "mouth")],
                          "sacBone": "", "sacPos": [], "sacRadius": 0.05}
CREATURES.append({"id": "suluk_surusu", "name": "Sülük Sürüsü", "archetype": "swarm", "weight": 4, "hp": 90, "damage": 4, "speed": 2.2,
                  "swollenScale": 1.7, "biome": "isil_sazlik",
                  "swell": {"push": 0.004, "wobble": 0.004, "glow": 1.2, "veinScale": 30.0, "seconds": 1.0, "stepJiggle": 0.0, "hitJiggle": 0.01},
                  "wild": {"stance": "swarm", "active": "always", "wanderRadius": 4, "walkSpeedMult": 0.4, "runSpeedMult": 1.6,
                           "retargetMin": 5, "retargetMax": 10},
                  "swarm": {"members": 26, "radius": 1.4, "memberSize": 0.14, "height": 0.08, "orbit": 0.2, "bob": 0.04, "hpPool": 90,
                            "scatterSeconds": 2, "scatterMult": 1.8, "swollenMembers": 3, "swollenRadius": 2.4,
                            "guardRadius": 3, "stingDamage": 4, "stingInterval": 1.2}})

# --- Kizil Kanyon ---
# Kosar: cok hizli, uzun boyunlu kosucu kus.
bn = bird_bones(0.85, 1.3, 0.3, 0.2, 0.1, 0.45, tail_z=-0.32)
body("kosar", {"feather": "D2603A", "belly": "F2D2A0", "crest": "F2A23A", "beak": "3A2A22", "leg": "6A4A3A", "eye": "16161A", "eyeball": "FFF4D0"}, bn, [
    P("body", "pillow", [0.36, 0.32, 0.6], [0, 0.88, -0.04], "feather", rnd=0.85),
    P("body", "pillow", [0.28, 0.2, 0.4], [0, 0.8, 0.06], "belly", rnd=0.85),
    P("body", "capsule", [0.09, 0.42, 0.09], [0, 1.1, 0.2], "feather", rot=[20, 0, 0]),
    P("head", "pillow", [0.16, 0.16, 0.24], [0, 1.3, 0.32], "feather", rnd=0.8),
    P("head", "cone", [0.06, 0.14, 0.06], [0, 1.28, 0.48], "beak", rot=[-90, 0, 0]),
    P("comb", "cone", [0.04, 0.2, 0.12], [0, 1.44, 0.26], "crest", rot=[-40, 0, 0]),
] + eyes([0, 1.33, 0.34], 0.07, 0, 0.03, 0.055) + [
    P("wing_l", "pillow", [0.07, 0.18, 0.36], [0.19, 0.92, -0.06], "feather", rnd=0.7, mirror=True),
    P("tail", "cone", [0.2, 0.36, 0.06], [0, 0.94, -0.46], "crest", rot=[-110, 0, 0]),
    P("thigh_l", "capsule", [0.09, 0.38, 0.09], [0.1, 0.62, 0.03], "feather", mirror=True),
    P("shin_l", "capsule", [0.05, 0.42, 0.05], [0.1, 0.25, 0.05], "leg", mirror=True),
    P("foot_l", "pillow", [0.12, 0.03, 0.18], [0.1, 0.03, 0.1], "leg", rnd=0.6, mirror=True),
], "body", [0.0, 1.02, -0.05])
creature("kosar", "Koşar", "runner", 2, 60, 6, 7.0, "kizil_kanyon", "bird", BIRD_ROLES, 1.45, {
    "stance": "shy", "active": "day", "wanderRadius": 16, "walkSpeedMult": 0.25, "runSpeedMult": 1.0, "alertRadius": 14,
    "fleeRadius": 10, "fleeSeconds": 4, "pauseMin": 2, "pauseMax": 5, "idleShare": 0.35, "grazeShare": 0.45})

# Kivrik: sekiz bacakli akrep; kuyrugundan zehir firlatir.
zs = [0.22, 0.08, -0.06, -0.2]
bn = arth_bones(0.22, 0.2, zs, 0.38, -0.3, claw=[0.26, 0.24, 0.42], tail2_y=0.62)
body("kivrik", {"shell": "8C3A2A", "dark": "4A1E18", "under": "D89A6A", "sting": "B04AE0", "eye": "0E0E10", "eyeball": "FFB04A"}, bn, [
    P("root", "pillow", [0.46, 0.2, 0.62], [0, 0.26, 0.04], "shell", rnd=0.7),
    P("root", "pillow", [0.38, 0.08, 0.5], [0, 0.15, 0.04], "under", rnd=0.7),
    P("spine", "pillow", [0.34, 0.16, 0.24], [0, 0.27, 0.34], "dark", rnd=0.7),
    P("tail", "sphere", [0.18, 0.16, 0.2], [0, 0.32, -0.32], "shell"),
    P("tail", "sphere", [0.15, 0.14, 0.17], [0, 0.42, -0.44], "shell"),
    P("tail2", "sphere", [0.13, 0.13, 0.15], [0, 0.56, -0.5], "shell"),
    P("tail2", "pillow", [0.14, 0.12, 0.18], [0, 0.66, -0.44], "dark", rnd=0.7),
    P("tail2", "cone", [0.06, 0.18, 0.06], [0, 0.66, -0.3], "sting", rot=[-120, 0, 0]),
    P("claw_l", "capsule", [0.07, 0.24, 0.07], [0.22, 0.23, 0.42], "shell", rot=[90, 0, 30], mirror=True),
    P("claw_l", "pillow", [0.14, 0.1, 0.2], [0.28, 0.24, 0.58], "dark", rnd=0.6, mirror=True),
] + eyes([0, 0.33, 0.44], 0.05, 0, 0.0, 0.04) + legs_arth(bn, "shell", "dark", 0.045),
     "root", [0.0, 0.4, 0.05], 0.08)
creature("kivrik", "Kıvrık", "ranged", 3, 95, 9, 3.6, "kizil_kanyon", "arthropod", arth_roles(4, True), 0.6, {
    "stance": "predator", "active": "always", "wanderRadius": 8, "walkSpeedMult": 0.35, "runSpeedMult": 1.0,
    "alertRadius": 11, "chargeRadius": 8, "attackRange": 6, "attackInterval": 2.2, "attackDamage": 7, "warnSeconds": 1.2,
    "giveUpRadius": 18, "projectile": "poison", "pauseMin": 2.5, "pauseMax": 6, "idleShare": 0.55, "grazeShare": 0.25})

# Kizgin: sirti korlu, agzindan alev puskuren dort ayakli kertenkele-boga.
bn = quad_bones(0.5, 0.9, 0.24, 0.14, 0.62, 0.6, ear=(0.14, 0.14), tail_z=-0.7)
body("kizgin", {"skin": "6A2A20", "ember": "FF7A2A", "belly": "D88A5A", "horn": "2A1A16", "eye": "1A0A08", "eyeball": "FFD84A", "mouth": "FFB04A"}, bn, [
    P("root", "pillow", [0.6, 0.46, 0.7], [0, 0.56, -0.18], "skin", rnd=0.75),
    P("spine", "pillow", [0.62, 0.5, 0.5], [0, 0.6, 0.24], "skin", rnd=0.75),
    P("root", "pillow", [0.46, 0.14, 0.9], [0, 0.34, 0.0], "belly", rnd=0.8),
    P("top", "cone", [0.12, 0.2, 0.12], [0, 0.88, 0.2], "ember"),
    P("top", "cone", [0.1, 0.17, 0.1], [0, 0.85, -0.02], "ember"),
    P("top", "cone", [0.09, 0.15, 0.09], [0, 0.8, -0.24], "ember"),
    P("head", "pillow", [0.42, 0.3, 0.36], [0, 0.64, 0.64], "skin", rnd=0.75),
    P("head", "pillow", [0.3, 0.08, 0.1], [0, 0.55, 0.82], "mouth", rnd=0.7),
    P("ear_l", "cone", [0.06, 0.2, 0.06], [0.15, 0.82, 0.62], "horn", rot=[-20, 0, 25], mirror=True),
] + eyes([0, 0.7, 0.7], 0.13, 0.03, 0.08, 0.07) + [
    P("tail", "cone", [0.2, 0.5, 0.18], [0, 0.52, -0.82], "skin", rot=[-100, 0, 0]),
    P("tail", "sphere", [0.12, 0.12, 0.12], [0, 0.48, -1.04], "ember"),
    P("front_l", "capsule", [0.15, 0.32, 0.15], [0.26, 0.3, 0.32], "skin", mirror=True),
    P("frontlow_l", "pillow", [0.17, 0.06, 0.2], [0.26, 0.04, 0.36], "horn", rnd=0.6, mirror=True),
    P("back_l", "capsule", [0.16, 0.34, 0.16], [0.26, 0.3, -0.38], "skin", mirror=True),
    P("backlow_l", "pillow", [0.18, 0.06, 0.2], [0.26, 0.04, -0.38], "horn", rnd=0.6, mirror=True),
], "spine", [0.0, 0.88, 0.1], 0.11)
creature("kizgin", "Kızgın", "ranged", 4, 170, 11, 2.8, "kizil_kanyon", "quadruped", QUAD_ROLES, 0.95, {
    "stance": "predator", "active": "always", "wanderRadius": 10, "walkSpeedMult": 0.3, "runSpeedMult": 0.9,
    "alertRadius": 12, "chargeRadius": 9, "attackRange": 7, "attackInterval": 2.4, "attackDamage": 9, "warnSeconds": 1.5,
    "giveUpRadius": 20, "projectile": "fire", "pauseMin": 3, "pauseMax": 7, "idleShare": 0.5, "grazeShare": 0.3}, swell=SWELL_L, scale=2.2)

# Lesci: tasinan yuku kapan, gokte suzulen akbaba.
bn = bird_bones(0.6, 0.98, 0.2, 0.22, 0.1, 0.3, tail_z=-0.28)
body("lesci", {"feather": "4A3A34", "ruff": "E8DCC8", "skin": "D87A6A", "beak": "E8C87A", "leg": "8A7A6A", "eye": "101010", "eyeball": "FFE08A"}, bn, [
    P("body", "pillow", [0.42, 0.4, 0.56], [0, 0.62, -0.04], "feather", rnd=0.85),
    P("body", "pillow", [0.36, 0.14, 0.24], [0, 0.84, 0.14], "ruff", rnd=0.9),
    P("head", "sphere", [0.18, 0.16, 0.2], [0, 1.0, 0.24], "skin"),
    P("head", "cone", [0.07, 0.18, 0.08], [0, 0.97, 0.38], "beak", rot=[-100, 0, 0]),
] + eyes([0, 1.03, 0.26], 0.07, 0, 0.04, 0.05) + [
    P("wing_l", "pillow", [0.62, 0.05, 0.36], [0.4, 0.74, -0.04], "feather", rot=[0, 0, -10], rnd=0.5, mirror=True),
    P("wing_l", "pillow", [0.3, 0.04, 0.2], [0.74, 0.7, -0.08], "ruff", rot=[0, 0, -15], rnd=0.5, mirror=True),
    P("tail", "pillow", [0.26, 0.04, 0.26], [0, 0.56, -0.4], "feather", rnd=0.6),
    P("thigh_l", "capsule", [0.08, 0.2, 0.08], [0.1, 0.42, 0.02], "feather", mirror=True),
    P("shin_l", "capsule", [0.04, 0.26, 0.04], [0.1, 0.18, 0.04], "leg", mirror=True),
    P("foot_l", "pillow", [0.12, 0.03, 0.14], [0.1, 0.03, 0.08], "leg", rnd=0.6, mirror=True),
], "body", [0.0, 0.8, -0.05])
creature("lesci", "Leşçi", "flyer", 2, 65, 6, 4.8, "kizil_kanyon", "bird", BIRD_ROLES, 1.05, {
    "stance": "raider", "active": "day", "wanderRadius": 18, "walkSpeedMult": 0.5, "runSpeedMult": 1.0, "alertRadius": 10,
    "fleeRadius": 5, "fleeSeconds": 3, "target": "crop", "targetSense": 36, "targetStop": 1.2, "targetSeconds": 5,
    "flyHeight": 2.6, "pauseMin": 2, "pauseMax": 5, "idleShare": 0.4, "grazeShare": 0.3})

# Dehliz: yerden pusu kuran, kazici pullu surungen.
bn = rept_bones(0.24, 1.0, 0.16, legs=True)
body("dehliz", {"scale": "8A5A3A", "plate": "C88A4A", "belly": "E2C29A", "claw": "2A1E18", "eye": "1A1A10", "eyeball": "E8F04A"}, bn, [
    P("root", "pillow", [0.52, 0.3, 0.72], [0, 0.26, -0.12], "scale", rnd=0.8),
    P("spine", "pillow", [0.5, 0.3, 0.56], [0, 0.27, 0.28], "scale", rnd=0.8),
    P("root", "pillow", [0.4, 0.1, 1.0], [0, 0.12, 0.05], "belly", rnd=0.85),
    P("spine", "pillow", [0.36, 0.08, 0.3], [0, 0.42, 0.28], "plate", rnd=0.6),
    P("root", "pillow", [0.36, 0.08, 0.3], [0, 0.41, -0.06], "plate", rnd=0.6),
    P("head", "pillow", [0.36, 0.22, 0.42], [0, 0.27, 0.68], "scale", rnd=0.75),
    P("head", "cone", [0.18, 0.2, 0.12], [0, 0.26, 0.94], "plate", rot=[-90, 0, 0]),
] + eyes([0, 0.33, 0.7], 0.12, 0.02, 0.06, 0.06) + [
    P("tail", "pillow", [0.3, 0.2, 0.44], [0, 0.22, -0.66], "scale", rnd=0.8),
    P("tail2", "cone", [0.2, 0.46, 0.14], [0, 0.2, -1.04], "scale", rot=[-90, 0, 0]),
    P("front_l", "capsule", [0.12, 0.24, 0.12], [0.24, 0.17, 0.3], "scale", rot=[0, 0, 60], mirror=True),
    P("frontlow_l", "cone", [0.16, 0.12, 0.2], [0.3, 0.04, 0.38], "claw", rot=[-90, 0, 0], mirror=True),
    P("back_l", "capsule", [0.12, 0.24, 0.12], [0.24, 0.17, -0.25], "scale", rot=[0, 0, 60], mirror=True),
    P("backlow_l", "pillow", [0.16, 0.05, 0.18], [0.3, 0.03, -0.27], "claw", rnd=0.6, mirror=True),
], "spine", [0.0, 0.48, 0.1], 0.1)
creature("dehliz", "Dehliz", "digger", 4, 150, 12, 3.4, "kizil_kanyon", "reptile", REPT_ROLES, 0.45, {
    "stance": "predator", "active": "always", "wanderRadius": 5, "walkSpeedMult": 0.25, "runSpeedMult": 1.1,
    "alertRadius": 8, "chargeRadius": 6.5, "attackRange": 1.9, "attackInterval": 1.6, "attackDamage": 10, "warnSeconds": 0.6,
    "giveUpRadius": 14, "burrow": True, "pauseMin": 4, "pauseMax": 9, "idleShare": 0.6, "grazeShare": 0.1}, swell=SWELL_L, scale=2.2)

# --- Ayaz Yaylasi ---
# Tosun: dev, yunlu, dislari kivrik yayla oküzu.
bn = quad_bones(1.15, 2.0, 0.5, 0.25, 1.15, 1.3, ear=(0.36, 0.2), tail_z=-1.4)
body("tosun", {"wool": "E8E2D4", "dark": "6A5A4E", "tusk": "F8F0D8", "nose": "4A3A34", "hoof": "2E2620", "eye": "141414", "eyeball": "F0EAD8"}, bn, [
    P("root", "pillow", [1.4, 1.2, 1.5], [0, 1.25, -0.42], "wool", rnd=0.9),
    P("spine", "pillow", [1.5, 1.4, 1.2], [0, 1.4, 0.5], "wool", rnd=0.9),
    P("top", "pillow", [1.0, 0.5, 0.9], [0, 2.02, 0.4], "wool", rnd=0.95),
    P("head", "pillow", [0.8, 0.7, 0.7], [0, 1.15, 1.34], "dark", rnd=0.8),
    P("head", "pillow", [0.86, 0.3, 0.5], [0, 1.52, 1.2], "wool", rnd=0.9),
    P("head", "pillow", [0.4, 0.24, 0.14], [0, 0.98, 1.68], "nose", rnd=0.7),
    P("head", "cone", [0.14, 0.62, 0.14], [0.36, 0.85, 1.6], "tusk", rot=[-140, 0, -20], mirror=True),
    P("ear_l", "pillow", [0.24, 0.1, 0.16], [0.46, 1.38, 1.3], "dark", rnd=0.6, mirror=True),
] + eyes([0, 1.3, 1.6], 0.24, 0.0, 0.06, 0.08) + [
    P("tail", "capsule", [0.14, 0.4, 0.14], [0, 1.4, -1.48], "dark", rot=[30, 0, 0]),
    P("front_l", "capsule", [0.4, 0.9, 0.4], [0.5, 0.62, 0.72], "wool", mirror=True),
    P("frontlow_l", "cylinder", [0.32, 0.2, 0.32], [0.5, 0.1, 0.76], "hoof", mirror=True),
    P("back_l", "capsule", [0.42, 0.9, 0.42], [0.5, 0.62, -0.76], "wool", mirror=True),
    P("backlow_l", "cylinder", [0.32, 0.2, 0.32], [0.5, 0.1, -0.8], "hoof", mirror=True),
], "spine", [0.0, 2.15, 0.2], 0.2)
creature("tosun", "Tosun", "tank", 8, 520, 18, 2.4, "ayaz_yaylasi", "quadruped", QUAD_ROLES, 2.3, {
    "stance": "calm", "active": "day", "wanderRadius": 12, "walkSpeedMult": 0.3, "runSpeedMult": 0.9, "fleeOnHit": False,
    "pauseMin": 4, "pauseMax": 9, "idleShare": 0.4, "grazeShare": 0.5}, swell=SWELL_L, scale=1.8)

# Ayaz Kurdu: suru halinde avlanan, buz yeleli kurt.
bn = quad_bones(0.56, 1.0, 0.18, 0.1, 0.72, 0.68, ear=(0.1, 0.18), tail_z=-0.74)
body("ayaz_kurdu", {"fur": "B8C8D8", "dark": "5A6A80", "mane": "E4F4FF", "ice": "8FE8FF", "nose": "1A1E26", "eye": "0E1420", "eyeball": "9FF0FF"}, bn, [
    P("root", "pillow", [0.4, 0.4, 0.66], [0, 0.6, -0.18], "fur", rnd=0.85),
    P("spine", "pillow", [0.44, 0.46, 0.5], [0, 0.64, 0.24], "fur", rnd=0.85),
    P("spine", "pillow", [0.5, 0.36, 0.3], [0, 0.82, 0.36], "mane", rnd=0.9),
    P("top", "cone", [0.08, 0.2, 0.08], [0, 0.94, 0.1], "ice"),
    P("top", "cone", [0.07, 0.17, 0.07], [0, 0.9, -0.06], "ice"),
    P("head", "pillow", [0.3, 0.26, 0.3], [0, 0.74, 0.66], "fur", rnd=0.8),
    P("head", "pillow", [0.16, 0.14, 0.26], [0, 0.68, 0.84], "dark", rnd=0.8),
    P("head", "sphere", [0.07, 0.06, 0.05], [0, 0.72, 0.97], "nose"),
    P("ear_l", "cone", [0.09, 0.18, 0.05], [0.1, 0.92, 0.66], "dark", mirror=True),
] + eyes([0, 0.79, 0.76], 0.08, 0, 0.0, 0.05) + [
    P("tail", "capsule", [0.14, 0.5, 0.14], [0, 0.62, -0.84], "mane", rot=[-60, 0, 0]),
    P("front_l", "capsule", [0.11, 0.42, 0.11], [0.18, 0.32, 0.36], "fur", mirror=True),
    P("frontlow_l", "pillow", [0.12, 0.05, 0.16], [0.18, 0.03, 0.4], "dark", rnd=0.6, mirror=True),
    P("back_l", "capsule", [0.12, 0.44, 0.12], [0.18, 0.32, -0.38], "fur", mirror=True),
    P("backlow_l", "pillow", [0.12, 0.05, 0.16], [0.18, 0.03, -0.4], "dark", rnd=0.6, mirror=True),
], "spine", [0.0, 0.92, 0.2], 0.09)
creature("ayaz_kurdu", "Ayaz Kurdu", "runner", 2, 75, 8, 5.6, "ayaz_yaylasi", "quadruped", QUAD_ROLES, 0.95, {
    "stance": "predator", "active": "always", "wanderRadius": 16, "walkSpeedMult": 0.3, "runSpeedMult": 1.0,
    "alertRadius": 16, "chargeRadius": 11, "attackRange": 1.8, "attackInterval": 1.4, "attackDamage": 6, "warnSeconds": 1.0,
    "giveUpRadius": 26, "packMin": 3, "packMax": 4, "pauseMin": 2, "pauseMax": 5, "idleShare": 0.4, "grazeShare": 0.2})

# Paytak: karninin ustunde kayan tombul penguen-ordek.
bn = bird_bones(0.42, 0.78, 0.06, 0.22, 0.09, 0.12, tail_z=-0.24)
body("paytak", {"back": "2A3A5A", "belly": "F4F4EE", "beak": "F2A23A", "cheek": "F2B8C8", "foot": "F2A23A", "eye": "101018", "eyeball": "FFFFFF"}, bn, [
    P("body", "pillow", [0.46, 0.6, 0.44], [0, 0.44, -0.02], "back", rnd=0.95),
    P("body", "pillow", [0.36, 0.48, 0.2], [0, 0.42, 0.14], "belly", rnd=0.95),
    P("head", "sphere", [0.3, 0.26, 0.28], [0, 0.8, 0.04], "back"),
    P("head", "pillow", [0.2, 0.14, 0.1], [0, 0.76, 0.16], "belly", rnd=0.9),
    P("head", "pillow", [0.12, 0.04, 0.14], [0, 0.76, 0.24], "beak", rnd=0.7),
    P("head", "sphere", [0.05, 0.03, 0.02], [0.09, 0.76, 0.17], "cheek", mirror=True),
] + eyes([0, 0.84, 0.12], 0.07, 0, 0.03, 0.05) + [
    P("wing_l", "pillow", [0.06, 0.34, 0.16], [0.24, 0.48, 0.0], "back", rot=[0, 0, -12], rnd=0.8, mirror=True),
    P("tail", "pillow", [0.16, 0.05, 0.12], [0, 0.18, -0.24], "back", rnd=0.7),
    P("thigh_l", "capsule", [0.08, 0.1, 0.08], [0.09, 0.15, 0.03], "back", mirror=True),
    P("foot_l", "pillow", [0.13, 0.03, 0.17], [0.09, 0.03, 0.1], "foot", rnd=0.6, mirror=True),
], "body", [0.0, 0.62, 0.0])
creature("paytak", "Paytak", "jumper", 2, 60, 5, 4.4, "ayaz_yaylasi", "bird", BIRD_ROLES, 0.95, {
    "stance": "shy", "active": "day", "wanderRadius": 10, "walkSpeedMult": 0.25, "runSpeedMult": 1.0, "alertRadius": 9,
    "fleeRadius": 6, "fleeSeconds": 3, "bellySlide": True, "pauseMin": 2, "pauseMax": 5, "idleShare": 0.45, "grazeShare": 0.35})

# Billur: sirtinda kristal buyuyen alti bacakli kabuklu.
zs = [0.24, 0.0, -0.24]
bn = arth_bones(0.36, 0.28, zs, 0.52, -0.45)
body("billur", {"shell": "5A6A8A", "under": "C8D0E0", "crystal": "B8F0FF", "crystal2": "E0C8FF", "eye": "10121A", "eyeball": "D8F8FF"}, bn, [
    P("root", "pillow", [0.86, 0.36, 1.0], [0, 0.42, 0.0], "shell", rnd=0.8),
    P("root", "pillow", [0.7, 0.12, 0.86], [0, 0.22, 0.0], "under", rnd=0.8),
    P("spine", "pillow", [0.5, 0.26, 0.3], [0, 0.4, 0.52], "shell", rnd=0.75),
    P("root", "cone", [0.2, 0.56, 0.2], [0, 0.86, 0.06], "crystal"),
    P("root", "cone", [0.16, 0.42, 0.16], [0.2, 0.78, -0.12], "crystal2", rot=[0, 0, 18], mirror=True),
    P("root", "cone", [0.14, 0.36, 0.14], [0.12, 0.74, 0.24], "crystal", rot=[16, 0, 12], mirror=True),
    P("root", "cone", [0.12, 0.3, 0.12], [0, 0.72, -0.32], "crystal2", rot=[-20, 0, 0]),
    P("tail", "pillow", [0.4, 0.2, 0.3], [0, 0.38, -0.56], "shell", rnd=0.75),
] + eyes([0, 0.46, 0.64], 0.12, 0, 0.03, 0.06) + legs_arth(bn, "shell", "under", 0.08),
     "root", [0.0, 0.62, -0.1], 0.12)
creature("billur", "Billur", "tank", 4, 230, 9, 2.0, "ayaz_yaylasi", "arthropod", arth_roles(3, False), 0.95, {
    "stance": "calm", "active": "day", "wanderRadius": 8, "walkSpeedMult": 0.35, "runSpeedMult": 0.8, "fleeOnHit": False,
    "pauseMin": 4, "pauseMax": 8, "idleShare": 0.45, "grazeShare": 0.45}, swell=SWELL_L, scale=2.0)

# --- Gobek ---
# Hamurcuk: sekilsiz maya hamuru; vuruldukca bolunur. Bacaksiz surungen plani (govde dalgalanir).
bn = [B("root", "", [0, 0.2, -0.05]), B("spine", "root", [0, 0.28, 0.08]), B("head", "spine", [0, 0.36, 0.2]),
      B("tail", "root", [0, 0.16, -0.22]), B("tail2", "tail", [0, 0.12, -0.36])]
body("hamurcuk", {"dough": "E8C8F0", "maya": "B05AE0", "glow": "F8E0FF", "eye": "2A1030", "eyeball": "FFFFFF"}, bn, [
    P("root", "sphere", [0.62, 0.42, 0.62], [0, 0.21, -0.02], "dough"),
    P("spine", "sphere", [0.46, 0.38, 0.46], [0, 0.34, 0.08], "dough"),
    P("head", "sphere", [0.3, 0.26, 0.3], [0, 0.48, 0.16], "dough"),
    P("tail", "sphere", [0.3, 0.2, 0.3], [0, 0.12, -0.26], "dough"),
    P("tail2", "sphere", [0.18, 0.12, 0.18], [0, 0.08, -0.4], "dough"),
    P("spine", "sphere", [0.1, 0.1, 0.1], [0.14, 0.4, -0.04], "maya", mirror=True),
    P("root", "sphere", [0.08, 0.08, 0.08], [-0.2, 0.26, 0.12], "maya"),
    P("head", "sphere", [0.12, 0.12, 0.12], [0, 0.6, 0.12], "glow"),
] + eyes([0, 0.5, 0.26], 0.07, 0, 0.0, 0.07), "spine", [0.0, 0.44, -0.08], 0.1)
creature("hamurcuk", "Hamurcuk", "swarm", 2, 70, 6, 2.6, "gobek", "reptile", REPT_ROLES[:5], 0.6, {
    "stance": "calm", "active": "always", "wanderRadius": 10, "walkSpeedMult": 0.4, "runSpeedMult": 1.0, "fleeRadius": 3,
    "fleeSeconds": 2, "splitInto": 2, "splitHpMult": 0.5, "splitGenerations": 2, "pauseMin": 1.5, "pauseMax": 4,
    "idleShare": 0.4, "grazeShare": 0.4},
    swell={"push": 0.07, "wobble": 0.06, "glow": 1.1, "veinScale": 5.0, "seconds": 1.0, "stepJiggle": 0.03, "hitJiggle": 0.06}, scale=2.2)

# ---------- arthropod plani (6/8 bacak) ----------

def arth_plan():
    legs_walk = {}
    legs_run = {}
    legs_rest = {}
    for i in range(1, 5):
        if i % 2 == 1:  # tripod: tek numarali ciftler bir fazda, cift numarali karsi fazda
            legs_walk[f"leg{i}_l"] = [[0, 0, 22, 0], [0.4, 0, -22, 0]]
            legs_walk[f"leg{i}low_l"] = [[0, 0, 0, 0], [0.1, 0, 0, 18], [0.2, 0, 0, 0]]
            legs_run[f"leg{i}_l"] = [[0, 0, 30, 0], [0.225, 0, -30, 0]]
            legs_run[f"leg{i}low_l"] = [[0, 0, 0, 0], [0.06, 0, 0, 24], [0.12, 0, 0, 0]]
        else:
            legs_walk[f"leg{i}_l"] = [[0, 0, -22, 0], [0.4, 0, 22, 0]]
            legs_walk[f"leg{i}low_l"] = [[0, 0, 0, 0], [0.4, 0, 0, 0], [0.5, 0, 0, 18], [0.6, 0, 0, 0]]
            legs_run[f"leg{i}_l"] = [[0, 0, -30, 0], [0.225, 0, 30, 0]]
            legs_run[f"leg{i}low_l"] = [[0, 0, 0, 0], [0.225, 0, 0, 0], [0.285, 0, 0, 24], [0.345, 0, 0, 0]]
        legs_rest[f"leg{i}_l"] = [[0, 0, 0, -14]]
    return {
        "_": "Eklem bacakli (yaratik4: Kiskac ve Billur alti, Kivrik sekiz bacak; URETIM §2.1 yedi plandan 'cok bacakli'). Bacak ciftleri leg1 (on) .. leg4 (arka); tek ve cift numarali ciftler karsi fazda adim atar (tripod). claw_l/r kiskac, tail/tail2 kuyruk (Kivrik'in igne kuyrugu). Olmayan rol atlanir.",
        "roles": ["root", "spine", "head", "tail", "tail2", "claw_l", "claw_r",
                  "leg1_l", "leg1low_l", "leg1_r", "leg1low_r", "leg2_l", "leg2low_l", "leg2_r", "leg2low_r",
                  "leg3_l", "leg3low_l", "leg3_r", "leg3low_r", "leg4_l", "leg4low_l", "leg4_r", "leg4low_r"],
        "moveRole": "root",
        "clips": {
            "idle": {"length": 2.4, "loop": True, "mirror": "same", "tracks": {
                "spine": [[0, 0, 0, 0], [1.2, 2, 0, 0]],
                "head": [[0, 0, 0, 0], [0.6, 0, 6, 0], [1.2, 0, 0, 0], [1.8, 0, -6, 0]],
                "claw_l": [[0, 0, 0, 0], [0.8, 0, -8, 0], [1.6, 0, 0, 0]],
                "tail": [[0, 0, 0, 0], [1.2, -4, 0, 0]],
                "tail2": [[0, 0, 0, 0], [1.2, -6, 0, 0]]},
                "move": [[0, 0, 0, 0], [1.2, 0, -0.01, 0]]},
            "walk": {"length": 0.8, "loop": True, "mirror": "phase", "tracks": dict({
                "spine": [[0, 0, 3, 0], [0.4, 0, -3, 0]],
                "claw_l": [[0, 0, 6, 0], [0.4, 0, -6, 0]],
                "tail": [[0, 0, -4, 0], [0.4, 0, 4, 0]]}, **legs_walk),
                "move": [[0, 0, 0, 0], [0.2, 0, 0.015, 0], [0.4, 0, 0, 0], [0.6, 0, 0.015, 0]]},
            "run": {"length": 0.45, "loop": True, "mirror": "phase", "tracks": dict({
                "spine": [[0, 0, 5, 0], [0.225, 0, -5, 0]],
                "claw_l": [[0, -10, 0, 0], [0.225, -10, 0, 0]],
                "tail": [[0, 6, 0, 0], [0.225, 6, 0, 0]]}, **legs_run),
                "move": [[0, 0, 0, 0], [0.11, 0, 0.03, 0], [0.225, 0, 0, 0], [0.34, 0, 0.03, 0]]},
            "attack1": {"length": 0.7, "loop": False, "mirror": "same", "tracks": {
                "spine": [[0, 0, 0, 0], [0.2, 8, 0, 0], [0.35, -12, 0, 0], [0.7, 0, 0, 0]],
                "claw_l": [[0, 0, 0, 0], [0.2, -20, -30, 0], [0.35, 10, 25, 0], [0.7, 0, 0, 0]],
                "head": [[0, 0, 0, 0], [0.35, -10, 0, 0], [0.7, 0, 0, 0]]},
                "move": [[0, 0, 0, 0], [0.2, 0, 0, -0.05], [0.35, 0, 0, 0.12], [0.7, 0, 0, 0]]},
            "attack2": {"length": 0.8, "loop": False, "mirror": "same", "tracks": {
                "tail": [[0, 0, 0, 0], [0.3, 30, 0, 0], [0.45, -35, 0, 0], [0.8, 0, 0, 0]],
                "tail2": [[0, 0, 0, 0], [0.3, 40, 0, 0], [0.45, -50, 0, 0], [0.8, 0, 0, 0]],
                "spine": [[0, 0, 0, 0], [0.3, -6, 0, 0], [0.8, 0, 0, 0]]},
                "move": [[0, 0, 0, 0], [0.3, 0, 0.02, 0], [0.8, 0, 0, 0]]},
            "hit": {"length": 0.45, "loop": False, "mirror": "same", "tracks": {
                "spine": [[0, 0, 0, 0], [0.08, 0, 0, 12], [0.25, 0, 0, -4], [0.45, 0, 0, 0]],
                "claw_l": [[0, 0, 0, 0], [0.08, 20, 0, 0], [0.45, 0, 0, 0]],
                "head": [[0, 0, 0, 0], [0.08, -12, 8, 0], [0.45, 0, 0, 0]]},
                "move": [[0, 0, 0, 0], [0.08, 0, 0.02, -0.04], [0.45, 0, 0, 0]]},
            "eat": {"length": 1.2, "loop": True, "mirror": "phase", "tracks": {
                "head": [[0, 14, 0, 0], [0.3, 22, 0, 0], [0.6, 14, 0, 0], [0.9, 22, 0, 0]],
                "claw_l": [[0, 20, 0, 0], [0.3, -10, 0, 0], [0.6, 20, 0, 0]],
                "spine": [[0, 5, 0, 0], [0.6, 6, 0, 0]]},
                "move": [[0, 0, -0.01, 0], [0.6, 0, -0.015, 0]]},
            "rest": {"length": 3.0, "loop": True, "mirror": "same", "tracks": dict({
                "head": [[0, 8, 0, 0], [1.5, 10, 0, 0]],
                "claw_l": [[0, 10, 20, 0], [1.5, 12, 22, 0]],
                "tail": [[0, 10, 0, 0], [1.5, 12, 0, 0]]}, **legs_rest),
                "move": [[0, 0, -0.06, 0], [1.5, 0, -0.065, 0]]},
        },
    }


# ---------- yazim (yalniz ekleme) ----------

def dump(obj):
    return json.dumps(obj, ensure_ascii=False, separators=(", ", ": "))


def append_array_items(path, items, key_ids):
    raw = open(path, "rb").read().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"
    end = raw.rstrip().rfind("]")
    added = 0
    text = ""
    for it in items:
        if f'"id": "{it["id"]}"' in raw or f'"id":"{it["id"]}"' in raw:
            continue
        lines = ["    {"]
        keys = list(it.keys())
        for i, k in enumerate(keys):
            comma = "," if i < len(keys) - 1 else ""
            lines.append(f'      "{k}": {dump(it[k])}{comma}')
        lines.append("    }")
        text += "," + nl + nl.join(lines)
        added += 1
    if not added:
        return 0
    head = raw[:end].rstrip()
    raw = head + text + nl + "  " + raw[end:]
    open(path, "wb").write(raw.encode("utf-8"))
    return added


def append_object_members(path, members, anchor_close):
    """bodies.json 'bodies' ya da poses.json 'plans' nesnesinin sonuna (dosyanin son iki kapanisindan once) uye ekler."""
    raw = open(path, "rb").read().decode("utf-8")
    nl = "\r\n" if "\r\n" in raw else "\n"
    s = raw.rstrip()
    last = s.rfind("}")           # dosya kapanisi
    inner = s.rfind("}", 0, last)  # bodies/plans kapanisi
    added = 0
    text = ""
    for k, v in members.items():
        if f'"{k}": {{' in raw:
            continue
        if anchor_close == "pretty":
            body_txt = json.dumps(v, ensure_ascii=False, indent=2).replace("\n", nl + "    ")
            text += "," + nl + f'    "{k}": ' + body_txt
        else:
            text += "," + nl + f'    "{k}": ' + dump(v)
        added += 1
    if not added:
        return 0
    head = s[:inner].rstrip()
    out = head + text + nl + "  " + s[inner:] + nl
    open(path, "wb").write(out.encode("utf-8"))
    return added


def main():
    for c in CREATURES:
        roles = set(c.get("rig", {}).get("bones", {}).keys())
        b = BODIES.get(c["id"])
        if b and c.get("rig"):
            names = {x["name"] for x in b["bones"]}
            names |= {n[:-2] + "_r" for n in names if n.endswith("_l")}
            missing = roles - names
            if missing:
                sys.exit(f"{c['id']}: govdede olmayan roller {sorted(missing)}")
    n1 = append_array_items(os.path.join(DATA, "creatures.json"), CREATURES, "id")
    n2 = append_object_members(os.path.join(DATA, "bodies.json"), BODIES, "compact")
    n3 = append_object_members(os.path.join(DATA, "poses.json"), {"arthropod": arth_plan()}, "pretty")
    print(f"creatures +{n1}, bodies +{n2}, poses +{n3} (toplam tur {len(CREATURES)})")


if __name__ == "__main__":
    main()
