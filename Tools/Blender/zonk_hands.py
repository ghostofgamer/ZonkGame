# -*- coding: utf-8 -*-
"""
Кисти руки игрока для «Зонка» (слот «Кисть», 15 вещей; первая — «без перчатки», без модели). Каждая — меш на скелете
руки (те же кости, что у Hand.fbx из zonk_arms.py): пальцы гнутся кодом вместе с рукой (HandView), хват стакана
работает. В Unity HandView привязывает кости модели к костям руки по именам.

Два вида:
    перчатка  — оболочка поверх руки (копия руки, раздутая по нормалям); рука под ней видна (без пальцев — пальцы)
    замена    — своя кисть, рука прячется. Первый материал модели — кожа: его HandView заменяет кожей игрока
                (предплечье под рукавом, открытые места), второй — палитра вещи.

    Hand_Leather   кожаная перчатка со строчкой                перчатка
    Hand_Fingerless перчатка без пальцев с заклёпками            перчатка
    Hand_Butler    белая перчатка дворецкого                    перчатка
    Hand_Hook      пиратский крюк                               замена
    Hand_Gauntlet  латная перчатка                              замена
    Hand_Skeleton  рука скелета                                 замена
    Hand_Zombie    рука зомби: швы, бинт                        замена
    Hand_CatPaw    кошачья лапа с подушечками                   замена
    Hand_Robot     рука робота                                  замена
    Hand_Gold      золотая рука с перстнями                     замена
    Hand_Demon     рука демона: когти, шипы, лава               замена
    Hand_Claw      крабовая перчатка: панцирь, глазки, клешни (Мадам Клешня)  замена
    Hand_Fortune   рука Фортуны: перламутр, золото, звезда      замена
    Hand_Gardener  садовая перчатка с розой (Графиня Роза)      перчатка

Какие модели прячут руку — список HIDES_HAND (генератор берёт оттуда же: ZonkSetup.HandLook).

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_hands.py
    blender --background --factory-startup --python Tools/Blender/zonk_hands.py -- --render <папка> [--only Hand_Robot]

Результат: Assets/ZonkContent/Art/Models/HandLooks/<Имя>.fbx (скелет + меш) и Art/Textures/HandLooks/<Имя>.png.
"""

import glob
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_arms as za  # noqa: E402
import zonk_handkit as hk  # noqa: E402
import zonk_wrist as zw  # noqa: E402
from zonk_handkit import C, shade  # noqa: E402

MODELS_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "HandLooks")
TEXTURES_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "HandLooks")

FINGERS = ("Index", "Middle", "Ring", "Pinky", "Thumb")
HIDES_HAND = {"Hand_Hook", "Hand_Gauntlet", "Hand_Skeleton", "Hand_Zombie", "Hand_CatPaw", "Hand_Robot", "Hand_Gold",
              "Hand_Demon", "Hand_Claw", "Hand_Fortune"}

GOLD = C(1.0, 0.76, 0.25)
GOLD_DARK = C(0.78, 0.52, 0.12)
STEEL = C(0.72, 0.75, 0.8)
STEEL_DARK = C(0.42, 0.45, 0.5)
BLACK = C(0.1, 0.1, 0.11)
WHITE = C(0.96, 0.95, 0.92)
UP = Vector((0, 0, 1))

ARM = None
ARMATURE = None
HAND = None


class HandKit(hk.Kit):
    """Вещь на скелете руки: вершина привязана к текущей кости (bone), грань — кожа (материал 0) или палитра (1)."""

    def __init__(self, name):
        super().__init__(name, ARM)
        self.deform = self.bm.verts.layers.deform.verify()
        self.groups = []
        self.bone = "Palm"
        self.keep = set()          # грани копии руки: их ориентация уже верная, пересчёт нормалей не нужен
        self.skin_used = False

    def gi(self, name):
        if name not in self.groups:
            self.groups.append(name)
        return self.groups.index(name)

    def vert(self, co):
        v = self.bm.verts.new(co)
        if self.bone:
            v[self.deform][self.gi(self.bone)] = 1.0
        return v

    def face(self, verts, color, smooth=True, skin=False):
        f = super().face(verts, color if color is not None else WHITE, smooth)
        f.material_index = 0 if skin else 1
        self.skin_used |= skin
        return f

    def build(self, textures_dir):
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in self.keep])
        mesh = bpy.data.meshes.new(self.name)
        # Материалы до to_mesh/validate: иначе номер 1 (палитра) сбросится в 0.
        mesh.materials.append(bpy.data.materials.get("Hand_Skin"))
        image = zm.save_png(self.name, self.bake(), os.path.join(textures_dir, self.name + ".png"))
        palette = zm.make_material(self.name, image)
        for node in palette.node_tree.nodes:
            if node.type == "TEX_IMAGE":
                node.interpolation = "Closest"
        bsdf = palette.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Metallic"].default_value = self.metallic
        bsdf.inputs["Roughness"].default_value = self.roughness
        mesh.materials.append(palette)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(34)
        mesh.validate()
        mesh.update()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for name in self.groups:
            obj.vertex_groups.new(name=name)
        obj.parent = ARMATURE
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = ARMATURE
        return obj


# ---------------------------------------------------------------------------
# Кости и поверхность руки
# ---------------------------------------------------------------------------

def head(name):
    return ARMATURE.data.bones[name].head_local.copy()


def tail(name):
    return ARMATURE.data.bones[name].tail_local.copy()


def chain(finger):
    """Точки пальца от костяшки к кончику: head1, head2, head3, tail3."""
    return [head(finger + "1"), head(finger + "2"), head(finger + "3"), tail(finger + "3")]


def s_of(co):
    return (co - ARM.palm).dot(ARM.axis)


def copy_hand(k, color=None, keep=None, inflate=0.0, skin_where=None, shade_by=None):
    """
    Копия руки в вещь (те же веса): keep(грань, s, кость) — какие грани брать; color — цвет (None — кожа игрока);
    skin_where(s, кость) — где оставить кожу; inflate — раздуть по нормалям (перчатка); shade_by(грань) — свой цвет грани.
    """
    src = bmesh.new()
    src.from_mesh(HAND.data)
    src.normal_update()
    deform = src.verts.layers.deform.active
    names = {g.index: g.name for g in HAND.vertex_groups}
    made = {}
    bone_was = k.bone
    k.bone = None

    def bone_of(v):
        weights = v[deform] if deform else {}
        return names[max(weights.keys(), key=lambda g: weights[g])] if weights else "Forearm"

    for f in src.faces:
        if f.material_index == 1:           # заглушка среза
            continue
        centre = f.calc_center_median()
        s = s_of(centre)
        main = bone_of(max(f.verts, key=lambda v: max(v[deform].values()) if deform and v[deform] else 0))
        if keep and not keep(f, s, main):
            continue
        verts = []
        for v in f.verts:
            if v.index not in made:
                nv = k.vert(v.co + v.normal * inflate)
                for g, w in (v[deform].items() if deform else []):
                    nv[k.deform][k.gi(names[g])] = w
                made[v.index] = nv
            verts.append(made[v.index])
        skin = color is None or (skin_where is not None and skin_where(s, main))
        face = k.face(verts, shade_by(f) if shade_by else color, smooth=False, skin=skin)
        k.keep.add(face)
    src.free()
    k.bone = bone_was


def back_point(p, lift=0.0):
    """Точка на тыльной стороне руки над p (по вершинам руки рядом)."""
    zs = [v.co.z for v in HAND.data.vertices if (Vector((v.co.x, v.co.y, 0)) - Vector((p.x, p.y, 0))).length < 0.06]
    z = max(zs) if zs else p.z
    return Vector((p.x, p.y, z + lift))


def beam(k, a, b, width, height, color, up=UP, skin=False):
    """Брусок от точки a до b (сечение width × height, height — по up)."""
    d = (b - a)
    along = d.normalized()
    side = along.cross(up).normalized()
    upv = side.cross(along).normalized()
    hw, hh = width * 0.5, height * 0.5
    ring_a = [k.vert(a + side * x + upv * y) for x, y in ((-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh))]
    ring_b = [k.vert(v.co + d) for v in ring_a]
    for j in range(4):
        q = (j + 1) % 4
        k.face((ring_a[j], ring_a[q], ring_b[q], ring_b[j]), color, smooth=False, skin=skin)
    k.face(list(reversed(ring_a)), color, smooth=False, skin=skin)
    k.face(ring_b, color, smooth=False, skin=skin)


def cone(k, base, tip, radius, color, seg=6):
    d = (tip - base).normalized()
    u = d.orthogonal().normalized()
    v = d.cross(u)
    ring = [k.vert(base + (u * math.cos(2 * math.pi * q / seg) + v * math.sin(2 * math.pi * q / seg)) * radius)
            for q in range(seg)]
    t = k.vert(tip)
    for q in range(seg):
        k.face((ring[q], ring[(q + 1) % seg], t), color, smooth=False)
    k.face(list(reversed(ring)), color, smooth=False)


def ring_around(k, centre, axis, radius, thick, color, seg=10):
    """Кольцо (перстень) вокруг оси пальца."""
    u = axis.orthogonal().normalized()
    v = axis.cross(u).normalized()
    hk.link(k, centre, u, v, radius, thick, color, stretch=1.0, seg=seg, sides=5)


def finger_radius(finger):
    return 0.075 if finger != "Thumb" else 0.085


def fingers_parts(k, color_fn, width=1.0, joints=None):
    """Брусья по фалангам (робот, латы, скелет): color_fn(палец, фаланга); joints(точка, палец, фаланга) — шарниры."""
    for f in FINGERS:
        pts = chain(f)
        for i in range(3):
            k.bone = f + str(i + 1)
            r = finger_radius(f) * width
            beam(k, pts[i], pts[i + 1], r * 2, r * 1.7, color_fn(f, i))
            if joints:
                joints(pts[i], f, i)


def palm_frame():
    palm = head("Palm")
    knuckles = [head(f + "1") for f in ("Index", "Middle", "Ring", "Pinky")]
    mid = sum(knuckles, Vector()) / 4
    along = (mid - palm).normalized()
    side = (head("Pinky1") - head("Index1")).normalized()
    return palm, mid, along, side


def forearm_skin(k, from_s):
    """Предплечье кожей игрока от from_s (под рукавом и у запястья)."""
    copy_hand(k, None, keep=lambda f, s, b: s >= from_s)


# ---------------------------------------------------------------------------
# Перчатки
# ---------------------------------------------------------------------------

def glove(k, color, upto=0.035, inflate=0.014, fingers=True, cuff=None):
    """Перчатка до запястья (upto): дальше — зона браслетов и часов."""
    copy_hand(k, color, inflate=inflate,
              keep=lambda f, s, b: s < upto and (fingers or not (b[-1] in "23" and b[:-1] in FINGERS)))
    if cuff:
        k.bone = "Forearm"
        hk.band(k, upto - 0.04, upto + 0.006, inflate * 0.5, inflate + 0.016, cuff)


def back_lines(k, color, count=3, lift=0.016):
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    for f in ("Index", "Middle", "Ring")[:count]:
        a = back_point(palm.lerp(head(f + "1"), 0.15), lift)
        b = back_point(palm.lerp(head(f + "1"), 0.8), lift)
        beam(k, a, b, 0.012, 0.006, color)


def make_leather():
    k = HandKit("Hand_Leather")
    leather = C(0.18, 0.12, 0.09)
    glove(k, leather, cuff=shade(leather, 1.3))
    back_lines(k, C(0.5, 0.36, 0.2), lift=0.012)
    return k


def make_fingerless():
    k = HandKit("Hand_Fingerless")
    black = C(0.12, 0.12, 0.13)
    glove(k, black, fingers=False, cuff=shade(black, 1.4))
    k.bone = "Palm"
    for f in ("Index", "Middle", "Ring", "Pinky"):
        p = back_point(head(f + "1").lerp(head("Palm"), 0.2), 0.014)
        hk.ball(k, p, 0.022, C(0.82, 0.84, 0.88), seg=6, rings=4)
    return k


def make_butler():
    k = HandKit("Hand_Butler")
    glove(k, WHITE, inflate=0.012, cuff=C(0.98, 0.98, 0.98))
    back_lines(k, C(0.62, 0.64, 0.7), lift=0.01)
    return k


def make_gardener():
    k = HandKit("Hand_Gardener")
    canvas, leather = C(0.36, 0.52, 0.28), C(0.55, 0.38, 0.2)
    glove(k, canvas, inflate=0.018, cuff=leather)
    k.bone = "Forearm"
    hk.flare(k, 0.0, 0.05, 0.025, 0.06, leather, rows=2, thick=0.012)
    p = ARM.point(0.035, math.radians(60), 0.07)
    hk.ball(k, p, 0.032, C(0.85, 0.12, 0.2), seg=8, rings=5)                    # роза на манжете
    for q in range(3):
        a = 2 * math.pi * q / 3
        hk.ball(k, p + Vector((math.cos(a), math.sin(a), 0)) * 0.03 - Vector((0, 0, 0.01)), 0.02, C(0.25, 0.55, 0.2),
                seg=6, rings=4)
    return k


# ---------------------------------------------------------------------------
# Замены
# ---------------------------------------------------------------------------

def make_hook():
    k = HandKit("Hand_Hook")
    forearm_skin(k, 0.03)
    wood, steel = C(0.45, 0.28, 0.14), STEEL
    k.bone = "Forearm"
    hk.band(k, 0.0, 0.08, 0.004, 0.03, wood)                                   # деревянная гильза
    c, a, b = ARM.ellipse(0.0)
    tip = c - ARM.axis * 0.12
    k.bone = "Palm"
    cone(k, c - ARM.axis * 0.005, tip, max(a, b) * 0.85, wood, seg=10)
    pts = [tip, tip - ARM.axis * 0.12]
    for q in range(1, 8):                                                     # крюк загибается вверх и назад
        ang = math.pi * q / 7
        pts.append(tip - ARM.axis * (0.12 + 0.1 * math.sin(ang)) + UP * (0.1 - 0.1 * math.cos(ang)))
    hk.tube(k, pts, 0.022, steel, sides=6)
    cone(k, pts[-1], pts[-1] + ARM.axis * 0.05 - UP * 0.01, 0.022, steel)
    return k


def make_gauntlet():
    k = HandKit("Hand_Gauntlet")
    leather = C(0.22, 0.15, 0.1)
    copy_hand(k, leather, inflate=0.008, keep=lambda f, s, b: s < 0.04)
    forearm_skin(k, 0.04)
    fingers_parts(k, lambda f, i: STEEL if i % 2 == 0 else shade(STEEL, 0.85), width=1.12)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    a = back_point(palm.lerp(mid, 0.1), 0.012)
    b = back_point(palm.lerp(mid, 0.95), 0.012)
    beam(k, a, b, 0.36, 0.05, STEEL)                                           # пластина тыльной стороны
    beam(k, back_point(head("Index1"), 0.03) - side * 0.04, back_point(head("Pinky1"), 0.03) + side * 0.04,
         0.07, 0.05, STEEL_DARK, up=UP)                                        # костяшки
    for x in (-0.1, 0.0, 0.1):
        hk.ball(k, back_point(palm.lerp(mid, 0.5), 0.06) + side * x, 0.014, GOLD, seg=6, rings=4)
    k.bone = "Forearm"
    hk.flare(k, 0.12, -0.02, 0.012, 0.06, lambda r, j: STEEL if j % 2 else shade(STEEL, 0.9), rows=3, thick=0.014)
    k.metallic, k.roughness = 0.7, 0.35
    return k


def make_skeleton():
    k = HandKit("Hand_Skeleton")
    bone_c, joint = C(0.93, 0.89, 0.78), C(0.78, 0.72, 0.6)
    for f in FINGERS:
        pts = chain(f)
        for i in range(3):
            k.bone = f + str(i + 1)
            hk.tube(k, [pts[i], pts[i + 1]], 0.026, bone_c, sides=6)
            hk.ball(k, pts[i], 0.036, joint, seg=6, rings=4)
        k.bone = f + "3"
        hk.ball(k, pts[3], 0.03, bone_c, seg=6, rings=4)
    palm = head("Palm")
    k.bone = "Palm"
    for f in FINGERS:                                                          # пястные кости
        hk.tube(k, [palm.lerp(head(f + "1"), 0.25), head(f + "1")], 0.024, bone_c, sides=6)
    for q in range(6):                                                         # запястье
        a = 2 * math.pi * q / 6
        hk.ball(k, palm + Vector((math.cos(a) * 0.06, math.sin(a) * 0.03, 0.0)) + ARM.axis * 0.02, 0.04, joint, seg=6, rings=4)
    k.bone = "Forearm"
    for x in (-0.07, 0.07):                                                    # лучевая и локтевая
        a = ARM.centre(0.03) + ARM.side * x
        b = ARM.centre(ARM.cut + 0.2) + ARM.side * x * 1.2
        hk.tube(k, [a, b], 0.04, bone_c, sides=6)
        hk.ball(k, a, 0.05, joint, seg=6, rings=4)
    return k


def make_zombie():
    k = HandKit("Hand_Zombie")
    rot, dark = C(0.55, 0.66, 0.45), C(0.4, 0.5, 0.33)
    copy_hand(k, rot, shade_by=lambda f: dark if (f.index * 7) % 11 < 3 else rot)
    k.bone = "Forearm"
    for q in range(9):                                                         # шов поперёк запястья
        t = math.radians(30 + q * 15)
        p, n, along, across = hk.frame_at(k, 0.06, t, 0.0)
        hk.slab(k, p - along * 0.025, along, across, n, 0.05, 0.008, 0.008, BLACK)
    pts = chain("Middle")
    k.bone = "Middle2"
    for q in range(3):                                                         # бинт на пальце
        hk.tube(k, [pts[1].lerp(pts[2], 0.15 + q * 0.3) + UP * 0.0, pts[1].lerp(pts[2], 0.25 + q * 0.3)], 0.085,
                C(0.9, 0.88, 0.8), sides=7)
    return k


def make_cat_paw():
    k = HandKit("Hand_CatPaw")
    fur, stripe, pad = C(0.95, 0.62, 0.25), C(0.78, 0.42, 0.12), C(0.95, 0.6, 0.65)
    copy_hand(k, fur, inflate=0.03, keep=lambda f, s, b: s < 0.04,
              shade_by=lambda f: stripe if int(s_of(f.calc_center_median()) * 25) % 3 == 0 else fur)
    forearm_skin(k, 0.04)
    for f in ("Index", "Middle", "Ring", "Pinky"):
        pts = chain(f)
        k.bone = f + "3"
        hk.ball(k, pts[2].lerp(pts[3], 0.5) - UP * 0.07, 0.04, pad, seg=8, rings=4, squash=(UP, 0.5))
        cone(k, pts[3] - UP * 0.02, pts[3] + (pts[3] - pts[2]).normalized() * 0.06 - UP * 0.03, 0.016, WHITE)
    k.bone = "Palm"
    palm, mid, along, side = palm_frame()
    hk.ball(k, palm.lerp(mid, 0.55) - UP * 0.17, 0.09, pad, seg=10, rings=5, squash=(UP, 0.45))
    return k


def make_robot():
    k = HandKit("Hand_Robot")
    metal, dark, glow = C(0.62, 0.66, 0.72), C(0.25, 0.27, 0.3), C(0.25, 0.9, 1.0)
    forearm_skin(k, 0.45)
    fingers_parts(k, lambda f, i: metal if i != 1 else shade(metal, 0.9), width=0.95,
                  joints=lambda p, f, i: hk.ball(k, p, 0.07 if f != "Thumb" else 0.075, dark, seg=8, rings=4))
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    beam(k, palm.lerp(mid, -0.1), mid, 0.42, 0.24, metal)                      # ладонь-короб
    beam(k, back_point(palm.lerp(mid, 0.2), 0.0), back_point(palm.lerp(mid, 0.75), 0.0), 0.16, 0.02, glow)
    for x in (-0.14, 0.14):
        hk.ball(k, back_point(palm.lerp(mid, 0.1), 0.0) + side * x, 0.018, dark, seg=6, rings=4)
    k.bone = "Forearm"
    c0, c1 = ARM.centre(0.02), ARM.centre(0.5)
    hk.tube(k, [c0, c1], 0.15, metal, sides=8)                                # предплечье
    for x in (-0.12, 0.12):
        hk.tube(k, [c0 + ARM.side * x + UP * 0.12, c1 + ARM.side * x + UP * 0.12], 0.025, dark, sides=6)   # поршни
    hk.ball(k, c0, 0.17, dark, seg=10, rings=6)                                # шарнир запястья
    k.metallic, k.roughness = 0.6, 0.35
    return k


def make_gold():
    k = HandKit("Hand_Gold")
    copy_hand(k, GOLD, shade_by=lambda f: GOLD if f.index % 3 else shade(GOLD, 0.88))
    for f, gem in (("Ring", C(0.85, 0.1, 0.15)), ("Index", C(0.1, 0.45, 0.9))):
        pts = chain(f)
        k.bone = f + "1"
        c = pts[0].lerp(pts[1], 0.55)
        ring_around(k, c, (pts[1] - pts[0]).normalized(), finger_radius(f) + 0.01, 0.016, GOLD_DARK)
        hk.ball(k, c + UP * (finger_radius(f) + 0.03), 0.026, gem, seg=6, rings=4)
    k.metallic, k.roughness = 0.8, 0.25
    return k


def make_demon():
    k = HandKit("Hand_Demon")
    red, dark, lava = C(0.55, 0.08, 0.08), C(0.32, 0.04, 0.05), C(1.0, 0.55, 0.12)
    copy_hand(k, red, shade_by=lambda f: dark if (f.index * 5) % 9 < 2 else red)
    for f in FINGERS:
        pts = chain(f)
        k.bone = f + "3"
        d = (pts[3] - pts[2]).normalized()
        cone(k, pts[3] - d * 0.02, pts[3] + d * 0.08 - UP * 0.03, 0.03, BLACK)      # когти
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    for f in ("Index", "Middle", "Ring", "Pinky"):
        p = back_point(head(f + "1").lerp(palm, 0.1), -0.01)
        cone(k, p, p + UP * 0.08 - along * 0.02, 0.026, BLACK)                    # шипы на костяшках
    for a, b in ((0.15, 0.55), (0.35, 0.8)):                                     # трещины с лавой
        beam(k, back_point(palm.lerp(head("Middle1"), a), 0.004) + side * 0.05,
             back_point(palm.lerp(head("Ring1"), b), 0.004), 0.016, 0.006, lava)
    return k


def taper(k, pts, radii, color, sides=8, flat=1.0):
    """Сужающаяся трубка по точкам (клешня, лапа): радиус на каждую точку, концы закрыты; flat — сплющить по UP."""
    rings = []
    for q, (p, r) in enumerate(zip(pts, radii)):
        d = (pts[min(q + 1, len(pts) - 1)] - pts[max(q - 1, 0)]).normalized()
        u = d.cross(UP).normalized()
        v = u.cross(d).normalized()
        rings.append([k.vert(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides) * flat) * r)
                      for i in range(sides)])
    k.quads(rings, color)
    k.fan(rings[0], pts[0], color, flip=True)
    k.fan(rings[-1], pts[-1], color)


def make_claw():
    """
    Крабовая перчатка (награда за Мадам Клешню): краба выиграли и сшили из него перчатку. Рука обычной формы —
    стакан держится как у всех; панцирь на тыльной стороне с глазками на стебельках, пальцы в сегментах, как лапки,
    ладонь светлая, как брюшко, по бокам запястья — маленькие клешни-трофеи.
    """
    k = HandKit("Hand_Claw")
    shell, light, dark, belly = C(0.92, 0.3, 0.14), C(1.0, 0.56, 0.3), C(0.5, 0.1, 0.05), C(0.99, 0.86, 0.66)
    copy_hand(k, shell, inflate=0.016, keep=lambda f, s, b: s < 0.04,
              shade_by=lambda f: belly if f.normal.z < -0.35 else shell)
    forearm_skin(k, 0.04)
    k.bone = "Forearm"
    zw.sweep(k, 0.022, zw.band_prof(0.056, 0.004, 0.026, 0.008),                    # манжета полосами
             lambda ri, j: (shell if j % 2 else light) if ri in (2, 3, 4) else dark)

    # пальцы — сегменты лапок: пластина сверху каждой фаланги, на кончике тёмный коготок
    for f in FINGERS:
        pts = chain(f)
        r = finger_radius(f)
        for i in range(3):
            k.bone = f + str(i + 1)
            a, b = pts[i].lerp(pts[i + 1], 0.1), pts[i].lerp(pts[i + 1], 0.88)
            beam(k, a + UP * r * 0.95, b + UP * r * 0.95, r * 1.75, 0.03, shell if i % 2 == 0 else light)
            if i == 2:
                d = (pts[3] - pts[2]).normalized()
                cone(k, pts[3] + UP * r * 0.6 - d * 0.02, pts[3] + d * 0.05 + UP * r * 0.75, 0.02, dark)

    # панцирь на тыльной стороне: купол с гребнем, шипы по переднему краю, пятна, глазки на стебельках
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    c0 = palm.lerp(mid, 0.5)
    L = (mid - palm).length
    pts = []
    for q in range(14):
        a = 2 * math.pi * q / 14
        for scale, lift in ((1.0, 0.014), (0.82, 0.075), (0.45, 0.115)):
            p = c0 + (along * math.cos(a) * 0.62 * L + side * math.sin(a) * 0.19) * scale
            pts.append(back_point(p, 0.0) + UP * lift)
    zw.hull(k, pts, lambda nrm: light if nrm.z > 0.85 else shell if nrm.z > 0.2 else dark)
    top = max(p.z for p in pts)
    for q in range(-2, 3):                                                    # шипы спереди
        p = c0 + along * 0.55 * L * math.cos(q * 0.32) + side * 0.17 * math.sin(q * 0.32)
        p = Vector((p.x, p.y, top - 0.035))
        cone(k, p, p + along * 0.05 + side * q * 0.012 + UP * 0.02, 0.016, dark)
    for e in (-1, 1):                                                         # шипы по бокам
        for t in (0.2, -0.25):
            p = c0 + side * e * 0.19 + along * t * L
            p = back_point(p, 0.02)
            cone(k, p, p + side * e * 0.045 + UP * 0.012, 0.014, dark)
    for e in (-1, 1):                                                         # глазки
        base = c0 + along * 0.42 * L + side * e * 0.055
        base = Vector((base.x, base.y, top - 0.012))
        eye = base + UP * 0.06 + along * 0.015 + side * e * 0.01
        hk.tube(k, [base, eye], 0.011, shell, sides=6)
        hk.ball(k, eye, 0.022, BLACK, seg=8, rings=5)
        hk.ball(k, eye + UP * 0.012 + along * 0.008 - side * e * 0.004, 0.007, WHITE, seg=6, rings=4)

    # маленькие клешни по бокам запястья, смотрят к пальцам
    for e in (-1, 1):
        root = back_point(palm + side * e * 0.2 - along * 0.02, -0.03)
        d = (along - side * e * 0.15).normalized()
        taper(k, [root, root + d * 0.05, root + d * 0.1], [0.032, 0.04, 0.03], shell, sides=8, flat=0.7)
        tip = root + d * 0.1
        for half, col in ((1, shell), (-1, light)):
            fin = (d + UP * 0.25 * half).normalized()
            taper(k, [tip, tip + fin * 0.045, tip + fin * 0.075 - UP * 0.012 * half], [0.022, 0.016, 0.004], col,
                  sides=6, flat=0.7)
    k.roughness = 0.45
    return k


def make_fortune():
    k = HandKit("Hand_Fortune")
    pearl, vein = C(0.96, 0.94, 0.99), C(0.95, 0.78, 0.3)
    copy_hand(k, pearl, shade_by=lambda f: vein if (f.index * 13) % 17 < 2 else pearl)
    for f in FINGERS:
        pts = chain(f)
        k.bone = f + "1"
        ring_around(k, pts[0].lerp(pts[1], 0.6), (pts[1] - pts[0]).normalized(), finger_radius(f) + 0.008, 0.012, GOLD)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    c = back_point(palm.lerp(mid, 0.45), 0.008)
    for q in range(4):                                                         # звезда на тыльной стороне
        a = q * math.pi / 4
        d = side * math.cos(a) + along * math.sin(a)
        beam(k, c - d * 0.07, c + d * 0.07, 0.022, 0.008, GOLD)
    k.metallic, k.roughness = 0.2, 0.3
    return k


HANDS = [make_leather, make_fingerless, make_butler, make_hook, make_gauntlet, make_skeleton, make_zombie, make_cat_paw,
         make_robot, make_gold, make_demon, make_claw, make_fortune, make_gardener]


def export(obj):
    za.export(ARMATURE, obj, [], os.path.join(MODELS_DIR, obj.name + ".fbx"))


def remove_orphans(names):
    for path in glob.glob(os.path.join(MODELS_DIR, "*.fbx")) + glob.glob(os.path.join(TEXTURES_DIR, "*.png")):
        if os.path.splitext(os.path.basename(path))[0] in names:
            continue
        os.remove(path)
        if os.path.exists(path + ".meta"):
            os.remove(path + ".meta")
        print("  удалён " + os.path.basename(path))


def render(out_dir, objs):
    """Превью: каждая кисть на руке (рука видна, если модель её не прячет)."""
    scene = bpy.context.scene
    cols, spacing = 5, 1.3
    for q, obj in enumerate(objs):
        x = (q % cols - (cols - 1) / 2) * spacing
        y = -(q // cols) * spacing * 1.7
        obj.parent = None
        for mod in list(obj.modifiers):
            obj.modifiers.remove(mod)
        obj.location = (x, y, 0)
        if obj.name not in HIDES_HAND:
            h = HAND.copy()
            h.parent = None
            for mod in list(h.modifiers):
                h.modifiers.remove(mod)
            h.location = (x, y, 0)
            scene.collection.objects.link(h)
    HAND.hide_render = True
    ARMATURE.hide_render = True
    hk.render(out_dir, [], ARMATURE, HAND, "hands")       # свет и камера
    HAND.hide_render = True
    cam = scene.camera
    from mathutils import Vector as V
    scene.render.resolution_x, scene.render.resolution_y = 800, 640
    for obj in objs:
        target = obj.location + V((0, 0.0, 0.0))
        for view, offset in (("a", V((0.8, 2.0, 1.8))), ("b", V((-1.7, 0.2, 0.9))), ("c", V((0.6, -1.6, 0.9)))):
            cam.location = target + offset
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, "hands_%s_%s.png" % (obj.name, view))
            bpy.ops.render.render(write_still=True)


def main():
    global ARM, ARMATURE, HAND
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

    ARMATURE, HAND, ARM = hk.load_arm()
    zw.ARM = ARM
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, ok = [], True
    print("Кисти:")
    for make in HANDS:
        kit = make()
        if only and kit.name not in only:
            kit.bm.free()
            continue
        obj = kit.build(TEXTURES_DIR)
        tris = zm.triangle_count(obj.data)
        good = tris <= 6000 and len(obj.vertex_groups) > 0
        ok &= good
        print("  %-16s треуг. %5d  цветов %2d  костей %2d  кожа %s  %s  %s" % (
            obj.name, tris, len(kit.colors), len(obj.vertex_groups), "да" if kit.skin_used else "нет",
            "прячет руку" if obj.name in HIDES_HAND else "поверх руки", "OK" if good else "ПРОВЕРИТЬ"))
        objs.append(obj)
    for obj in objs:
        export(obj)
    if not only:
        remove_orphans({obj.name for obj in objs})
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not ok:
        raise SystemExit("Есть кисти с ошибками")


if __name__ == "__main__":
    main()
