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


def top_point(p, lift=0.0):
    """
    Точка тыльной стороны над p лучом сверху на кожу. back_point ищет по вершинам рядом, а у большой грани низкополигональной
    руки их рядом нет — точка проваливалась вниз и деталь пряталась внутри (зомби, 06.11).
    """
    hit, loc, _, _ = HAND.ray_cast(Vector((p.x, p.y, 2.0)), Vector((0, 0, -1)))
    return Vector((loc.x, loc.y, loc.z + lift)) if hit else back_point(p, lift)


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


def cuff(k, s0, s1, add, thick, color, bevel=0.005):
    """Манжета вплотную к руке (как браслеты запястья, zonk_wrist.sweep): не висит кольцом над гранями."""
    k.bone = "Forearm"
    zw.sweep(k, (s0 + s1) / 2, zw.band_prof(s1 - s0, add, thick, bevel),
             color if callable(color) else zw.band_colors(color))


def seam(k, a, b, color, lift=0.012, radius=0.007, steps=6):
    """Выпуклый шов по тыльной стороне кисти от точки a к b."""
    k.bone = "Palm"
    hk.tube(k, [top_point(a.lerp(b, i / (steps - 1)), lift) for i in range(steps)], radius, color, sides=5)


def finger_line(k, f, color, lift=0.0, radius=0.006, start=0.0, end=1.0, side=0.0):
    """Линия по верху пальца (трещина, кант): по фалангам, каждая часть на своей кости — гнётся вместе с пальцем."""
    pts = chain(f)
    r = finger_radius(f)
    across = (head("Pinky1") - head("Index1")).normalized()
    for i in range(3):
        a0, a1 = i / 3, (i + 1) / 3
        if a1 <= start or a0 >= end:
            continue
        u0, u1 = (max(a0, start) - a0) * 3, (min(a1, end) - a0) * 3
        k.bone = f + str(i + 1)
        a = pts[i].lerp(pts[i + 1], u0) + UP * (r + lift) + across * side * (1 if i % 2 else -1)
        b = pts[i].lerp(pts[i + 1], u1) + UP * (r + lift) - across * side * (1 if i % 2 else -1)
        hk.tube(k, [a, a.lerp(b, 0.5) + UP * 0.002, b], radius, color, sides=4)


def make_leather():
    """Гоночная перчатка: чёрная кожа, красные вставки, жёсткие накладки на костяшках и пальцах, ремешок на липучке."""
    k = HandKit("Hand_Leather")
    black, red, carbon, light = C(0.12, 0.12, 0.13), C(0.8, 0.1, 0.1), C(0.26, 0.27, 0.3), C(0.5, 0.52, 0.56)
    glove(k, black, inflate=0.014)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    shell = []                                                                    # накладка на костяшках
    for f in ("Index", "Middle", "Ring", "Pinky"):
        p = head(f + "1")
        for d in (-0.035, 0.035):
            shell.append(top_point(p + along * d, 0.016))
            shell.append(top_point(p + along * d, 0.05))
    zw.hull(k, shell, lambda nrm: light if nrm.z > 0.9 else carbon)
    for x in (-0.1, 0.0, 0.1):                                                    # рёбра на накладке
        p = top_point(mid + side * x, 0.052)
        hk.slab(k, p - along * 0.03, along, side, UP, 0.06, 0.012, 0.006, light)
    for f in ("Index", "Middle", "Ring", "Pinky"):                                # накладки на средних фалангах
        pts = chain(f)
        k.bone = f + "2"
        c = pts[1].lerp(pts[2], 0.5) + UP * (finger_radius(f) + 0.016)
        zw.chamfer_box(k, c, side, (pts[2] - pts[1]).normalized(), UP, 0.05, 0.035, 0.008, 0.005, carbon, light)
    seam(k, palm.lerp(head("Index1"), 0.05), palm.lerp(head("Index1"), 0.7), red, lift=0.018, radius=0.012)
    seam(k, palm.lerp(head("Pinky1"), 0.05), palm.lerp(head("Pinky1"), 0.7), red, lift=0.018, radius=0.012)
    cuff(k, -0.025, 0.045, 0.012, 0.02, lambda ri, j: red if ri == 3 else black)
    p, n, al, ac = zw.frame(0.01, math.pi / 2, 0.032)                            # ремешок на липучке с нашивкой
    zw.chamfer_box(k, p, ac, al, n, 0.06, 0.03, 0.005, 0.004, black, carbon)
    zw.disc(k, p + n * 0.005, n, al, 0.018, 0.002, red, seg=10)
    k.roughness = 0.5
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
    """Белая перчатка дворецкого: манжета с чёрным кантом и жемчужной пуговицей, три шва, вышитая золотая корона."""
    k = HandKit("Hand_Butler")
    white, line, gold = C(0.98, 0.98, 0.97), C(0.66, 0.68, 0.76), C(1.0, 0.8, 0.3)
    glove(k, white, inflate=0.012)
    cuff(k, -0.025, 0.045, 0.01, 0.02, lambda ri, j: BLACK if ri in (2, 4) else white if ri == 3 else line)
    p, n, along, across = zw.frame(0.01, math.radians(70), 0.03)
    zw.lathe(k, p, n, along, across, [(0, 0), (0.02, 0), (0.02, 0.006), (0.014, 0.01), (0, 0.01)],
             C(0.95, 0.93, 0.88), seg=10)
    palm, mid, along, side = palm_frame()
    for f in ("Index", "Middle", "Ring"):
        seam(k, palm.lerp(head(f + "1"), 0.5), palm.lerp(head(f + "1"), 0.85), line, lift=0.018, radius=0.007)
    k.bone = "Palm"
    c = top_point(palm.lerp(mid, 0.28), 0.016)                                    # корона
    hk.slab(k, c - side * 0.05 - along * 0.012, side, along, UP, 0.1, 0.024, 0.006, gold)
    for x in (-0.04, 0.0, 0.04):
        hk.slab(k, c + side * x - along * 0.0, -along, side, UP, 0.04 if x == 0 else 0.03, 0.014, 0.006, gold)
        hk.ball(k, c + side * x + along * (0.04 if x == 0 else 0.03) * -1 + UP * 0.004, 0.009, gold, seg=6, rings=4)
    return k


def make_gardener():
    """Садовая перчатка (Графиня Роза): холщовая, кожаная ладонь, плотная манжета-раструб, большая роза с листьями."""
    k = HandKit("Hand_Gardener")
    canvas, leather, rose, leaf = C(0.4, 0.58, 0.3), C(0.62, 0.42, 0.22), C(0.88, 0.12, 0.22), C(0.22, 0.52, 0.2)
    copy_hand(k, canvas, inflate=0.018, keep=lambda f, s, b: s < 0.035,
              shade_by=lambda f: leather if f.normal.z < -0.35 else canvas)
    k.bone = "Forearm"
    zw.sweep(k, 0.04, [(-0.06, 0.016), (0.08, 0.016), (0.08, 0.06), (0.07, 0.066), (-0.06, 0.034), (-0.064, 0.026)],
             lambda ri, j: shade(leather, 1.15) if ri in (2, 3) else leather)
    p, n, al, ac = zw.frame(0.05, math.radians(60), 0.07)                         # роза на манжете
    for ring, (count, r, h) in enumerate(((7, 0.05, 0.0), (6, 0.034, 0.016), (4, 0.018, 0.03))):
        for q in range(count):
            a = 2 * math.pi * (q + ring * 0.5) / count
            d = al * math.cos(a) + ac * math.sin(a)
            hk.ball(k, p + d * r + n * h, 0.034 - ring * 0.006, shade(rose, 1.0 - ring * 0.12), seg=6, rings=4,
                    squash=(n, 0.6))
    for a in (0.6, 2.4, 4.2):                                                    # листья
        d = al * math.cos(a) + ac * math.sin(a)
        e = d.cross(n).normalized()
        zw.hull(k, [p + d * 0.045 - n * 0.008, p + d * 0.14 - n * 0.006, p + d * 0.09 + e * 0.034 - n * 0.006,
                    p + d * 0.09 - e * 0.034 - n * 0.006, p + d * 0.09 - n * 0.0], leaf)
    k.roughness = 0.85
    return k


def make_hook():
    """Пиратский протез: кожаный ремень с пряжками, деревянная гильза, латунное кольцо, толстый стальной крюк."""
    k = HandKit("Hand_Hook")
    forearm_skin(k, 0.03)
    wood, wood_dark, leather, brass = C(0.5, 0.3, 0.14), C(0.36, 0.2, 0.09), C(0.35, 0.2, 0.1), C(0.9, 0.7, 0.3)
    steel, steel_dark = C(0.78, 0.8, 0.85), C(0.45, 0.47, 0.52)
    cuff(k, 0.03, 0.17, 0.004, 0.026, leather)
    for s in (0.06, 0.14):                                                          # ремни с пряжками
        cuff(k, s - 0.012, s + 0.012, 0.028, 0.008, shade(leather, 0.65))
        p, n, along, across = zw.frame(s, math.pi / 2, 0.036)
        for x in (-1, 1):
            hk.slab(k, p + across * x * 0.02 - along * 0.016, along, across, n, 0.032, 0.006, 0.006, brass)
            hk.slab(k, p + along * x * 0.013 - across * 0.02, across, along, n, 0.04, 0.006, 0.006, brass)
    # деревянная гильза: от манжеты к кисти сужается, кольцами волокон
    k.bone = "Palm"
    c, a, b = ARM.ellipse(0.03)
    fwd = -ARM.axis
    r0 = (a + b) / 2 + 0.01
    e1, e2 = ARM.side, ARM.up
    zw.lathe(k, c - fwd * 0.0, fwd, e1, e2, [(0, 0), (r0, 0), (r0 * 0.92, 0.06), (r0 * 0.7, 0.13), (r0 * 0.5, 0.18),
                                              (0, 0.19)],
             lambda i, q: wood_dark if (q % 5 == 0 or i == 2) else wood, seg=16)
    tip = c + fwd * 0.17
    zw.lathe(k, tip, fwd, e1, e2, [(0, 0), (r0 * 0.56, 0), (r0 * 0.6, 0.012), (r0 * 0.56, 0.03), (0, 0.03)],
             lambda i, q: brass if q % 2 else shade(brass, 1.15), seg=14)
    # крюк: толстый у основания, загнут вверх и назад, острый кончик
    base = tip + fwd * 0.03
    pts, radii = [], []
    for q in range(12):
        u = q / 11
        ang = math.pi * 1.15 * max(0.0, (u - 0.25) / 0.75)
        if u <= 0.25:
            p = base + fwd * (0.2 * u / 0.25)
        else:
            centre = base + fwd * 0.2 + UP * 0.1
            p = centre + fwd * math.sin(ang) * 0.1 - UP * math.cos(ang) * 0.1
        pts.append(p)
        radii.append(0.04 * (1 - u) + 0.006)
    taper(k, pts, radii, steel, sides=8)
    zw.glint(k, pts[3] + UP * 0.024, UP, fwd, 0.05, 0.008)
    k.metallic, k.roughness = 0.3, 0.45
    return k


def make_gauntlet():
    """Рука рыцаря: пальцы в стальных сегментах, пластины внахлёст, шипы на костяшках, раструб с золотым кантом и гербом."""
    k = HandKit("Hand_Gauntlet")
    gold, red = C(0.95, 0.76, 0.3), C(0.75, 0.1, 0.12)
    steel, steel_l, steel_d = C(0.74, 0.77, 0.82), C(0.9, 0.92, 0.95), C(0.45, 0.47, 0.52)
    copy_hand(k, steel_d, inflate=0.01, keep=lambda f, s, b: s < 0.04)          # основа — тёмная сталь, не чернота
    forearm_skin(k, 0.04)
    for f in FINGERS:                                                              # сегменты: шире у основания, внахлёст
        pts = chain(f)
        r = finger_radius(f) * 1.18
        for i in range(3):
            k.bone = f + str(i + 1)
            a, b = pts[i] - (pts[i + 1] - pts[i]) * 0.1, pts[i + 1]
            taper(k, [a + UP * 0.008, a.lerp(b, 0.15) + UP * 0.01, a.lerp(b, 0.85) + UP * 0.01, b + UP * 0.006],
                  [r * 0.95, r * 1.05, r, r * 0.9], steel if i % 2 == 0 else shade(steel, 1.08), sides=10, flat=0.85)
        k.bone = f + "3"
        hk.ball(k, pts[3] + UP * 0.006, r * 0.88, steel, seg=8, rings=5, squash=(UP, 0.8))   # скруглённый кончик
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    for q, (f0, f1) in enumerate(((0.0, 0.42), (0.32, 0.72), (0.62, 1.02))):      # три пластины внахлёст
        pts = []
        for f in (f0, f1):
            for x in (-0.2, -0.1, 0.0, 0.1, 0.2):
                lift = 0.026 + 0.012 * q + (0.016 if abs(x) < 0.15 else 0.0)
                pts.append(top_point(palm.lerp(mid, f) + side * x, lift))
                pts.append(top_point(palm.lerp(mid, f) + side * x, 0.006))
        zw.hull(k, pts, lambda nrm: steel_l if nrm.z > 0.93 else steel if nrm.z > 0.3 else steel_d)
    for f in ("Index", "Middle", "Ring", "Pinky"):                                # шипы на костяшках
        p = top_point(head(f + "1"), 0.06)
        cone(k, p - UP * 0.02, p + UP * 0.035 - along * 0.01, 0.022, steel_d, seg=4)
    for f, x in ((0.15, -0.16), (0.15, 0.16), (0.5, -0.17), (0.5, 0.17)):          # заклёпки
        hk.ball(k, top_point(palm.lerp(mid, f) + side * x, 0.04), 0.011, gold, seg=6, rings=4)
    k.bone = "Forearm"
    zw.sweep(k, 0.07, [(-0.075, 0.006), (0.1, 0.006), (0.1, 0.075), (0.085, 0.086), (-0.075, 0.03), (-0.08, 0.02)],
             lambda ri, j: gold if ri == 2 else steel if ri == 3 else steel_l if ri == 4 else steel_d)
    p, n, al, ac = zw.frame(0.075, math.pi / 2, 0.062)                           # щит-герб на раструбе
    tilt = (n + al * 0.55).normalized()
    e1 = -al
    zw.lathe(k, p, tilt, e1, tilt.cross(e1).normalized(), [(0, 0), (0.052, 0), (0.052, 0.006), (0, 0.006)], gold, seg=16)
    zw.lathe(k, p + tilt * 0.006, tilt, e1, tilt.cross(e1).normalized(), [(0, 0), (0.042, 0), (0.042, 0.003), (0, 0.003)],
             red, seg=16)
    for d in (e1, tilt.cross(e1).normalized()):
        hk.slab(k, p + tilt * 0.009 - d * 0.036, d, d.cross(tilt).normalized(), tilt, 0.072, 0.012, 0.003, gold)
    k.metallic, k.roughness = 0.7, 0.35
    return k


def make_skeleton():
    """Рука скелета: толстые кости с шишками на суставах, пястные кости, запястье, лучевая и локтевая."""
    k = HandKit("Hand_Skeleton")
    bone_c, joint = C(0.95, 0.91, 0.8), C(0.82, 0.76, 0.62)
    for f in FINGERS:
        pts = chain(f)
        for i in range(3):
            k.bone = f + str(i + 1)
            taper(k, [pts[i], pts[i].lerp(pts[i + 1], 0.5), pts[i + 1]], [0.036, 0.027, 0.034], bone_c, sides=7)
            hk.ball(k, pts[i], 0.042, joint, seg=7, rings=5)
        k.bone = f + "3"
        hk.ball(k, pts[3], 0.034, bone_c, seg=7, rings=5)
    palm = head("Palm")
    k.bone = "Palm"
    for f in FINGERS:                                                            # пястные кости
        a = palm.lerp(head(f + "1"), 0.22)
        taper(k, [a, a.lerp(head(f + "1"), 0.5), head(f + "1")], [0.034, 0.026, 0.036], bone_c, sides=7)
        hk.ball(k, a, 0.036, joint, seg=6, rings=4)
    for q in range(7):                                                           # запястье
        a = 2 * math.pi * q / 7
        hk.ball(k, palm + Vector((math.cos(a) * 0.07, math.sin(a) * 0.035, 0.0)) + ARM.axis * 0.02, 0.045, joint,
                seg=7, rings=5)
    k.bone = "Forearm"
    for x in (-0.07, 0.07):                                                      # лучевая и локтевая
        a = ARM.centre(0.05) + ARM.side * x
        b = ARM.centre(ARM.cut + 0.2) + ARM.side * x * 1.2
        taper(k, [a, a.lerp(b, 0.5), b], [0.05, 0.038, 0.045], bone_c, sides=8)
        hk.ball(k, a, 0.058, joint, seg=7, rings=5)
    return k


def make_zombie():
    """Рваная рука зомби: лоскуты кожи разной зелени со швами, вырванный кусок с костями, косточка вместо кончика мизинца."""
    k = HandKit("Hand_Zombie")
    rot, rot2, rot3 = C(0.55, 0.66, 0.45), C(0.62, 0.72, 0.5), C(0.45, 0.56, 0.4)
    flesh, bone_c, thread = C(0.2, 0.1, 0.14), C(0.95, 0.92, 0.82), C(0.12, 0.1, 0.1)
    # лоскуты: цвет куска зависит от того, по какую сторону шва (косые полосы) лежит грань
    def patch(f):
        c = f.calc_center_median()
        band = int((c.x * 2.2 + c.y * 3.0 + 3) * 1.6) % 3
        tone = (rot, rot2, rot3)[band]
        return shade(tone, 0.85) if f.normal.z < -0.35 else tone
    copy_hand(k, rot, keep=lambda f, s, b: b != "Pinky3", shade_by=patch)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    # швы между лоскутами: крестики по косой линии через тыльную сторону
    for q in range(9):
        u = q / 8
        p = top_point(palm.lerp(mid, 0.1 + 0.8 * u) + side * (0.2 - 0.4 * u), 0.004)
        for d in (-1, 1):
            e = (along + side * d).normalized()
            hk.slab(k, p - e * 0.016, e, e.cross(UP).normalized(), UP, 0.032, 0.007, 0.006, thread)
    # вырванный кусок: тёмная плоть, видны две пястные кости, по краю — торчащие лоскуты
    c = top_point(palm.lerp(mid, 0.55) + side * 0.09, 0.004)
    flat = lambda a: 0.1 * (1.0 + 0.22 * math.sin(a * 3) + 0.1 * math.cos(a * 5))
    k.fan([k.vert(c + (along * math.cos(2 * math.pi * q / 14) + side * math.sin(2 * math.pi * q / 14)) *
                  flat(2 * math.pi * q / 14) * 0.95) for q in range(14)], c - UP * 0.004, flesh)
    zw.disc(k, c - UP * 0.006, UP, along, 0.085, 0.006, flesh, seg=14)
    for x in (-0.04, 0.04):
        hk.tube(k, [c + side * x - along * 0.08 + UP * 0.002, c + side * x + along * 0.08 + UP * 0.002], 0.017, bone_c,
                sides=6)
    for q in range(7):
        a = 2 * math.pi * q / 7 + 0.3
        d = along * math.cos(a) + side * math.sin(a)
        base = c + d * flat(a) * 0.9
        zw.hull(k, [base + d.cross(UP) * 0.02, base - d.cross(UP) * 0.02, base + d * 0.035 + UP * 0.018,
                    base + UP * 0.006], rot2 if q % 2 else rot)
    # мизинец: вместо кончика косточка
    pts = chain("Pinky")
    k.bone = "Pinky3"
    hk.tube(k, [pts[2], pts[3]], 0.022, bone_c, sides=6)
    hk.ball(k, pts[3], 0.026, bone_c, seg=6, rings=4)
    zw.lathe(k, pts[2], (pts[3] - pts[2]).normalized(), side, UP,
             [(0, 0), (finger_radius("Pinky") * 1.02, 0), (finger_radius("Pinky") * 0.9, 0.02), (0, 0.02)], flesh, seg=8)
    # предплечье: рваный край кожи, из-под него видна кость
    k.bone = "Forearm"
    p, n, al, ac = zw.frame(0.08, math.radians(70), 0.0)
    zw.disc(k, p + n * 0.002, n, al, 0.05, 0.004, flesh, seg=10)
    hk.tube(k, [p + n * 0.008 - al * 0.04, p + n * 0.008 + al * 0.04], 0.016, bone_c, sides=6)
    for q in range(6):
        a = 2 * math.pi * q / 6
        d = al * math.cos(a) + ac * math.sin(a)
        zw.hull(k, [p + d * 0.05 + d.cross(n) * 0.015, p + d * 0.05 - d.cross(n) * 0.015, p + d * 0.075 + n * 0.012,
                    p + d * 0.045 + n * 0.004], rot if q % 2 else rot3)
    pts = chain("Middle")
    k.bone = "Middle2"
    for q in range(3):                                                           # бинт на пальце
        hk.tube(k, [pts[1].lerp(pts[2], 0.12 + q * 0.3), pts[1].lerp(pts[2], 0.3 + q * 0.3)], 0.088,
                C(0.92, 0.9, 0.8) if q % 2 else C(0.85, 0.82, 0.72), sides=7)
    return k


def make_cat_paw():
    """Кошачья лапа: рыжая с широкими полосами, розовые подушечки, белые когти, пушистая манжета."""
    k = HandKit("Hand_CatPaw")
    fur, stripe, pad, cream = C(0.97, 0.62, 0.24), C(0.78, 0.4, 0.12), C(0.98, 0.62, 0.68), C(0.99, 0.93, 0.82)
    copy_hand(k, fur, inflate=0.03, keep=lambda f, s, b: s < 0.04,
              shade_by=lambda f: cream if f.normal.z < -0.4 else
              stripe if int((s_of(f.calc_center_median()) + 0.5) * 9) % 2 == 0 and f.normal.z > 0.2 else fur)
    forearm_skin(k, 0.04)
    for f in ("Index", "Middle", "Ring", "Pinky"):
        pts = chain(f)
        k.bone = f + "3"
        hk.ball(k, pts[2].lerp(pts[3], 0.5) - UP * 0.07, 0.042, pad, seg=8, rings=4, squash=(UP, 0.5))
        cone(k, pts[3] - UP * 0.02, pts[3] + (pts[3] - pts[2]).normalized() * 0.06 - UP * 0.03, 0.018, WHITE)
    k.bone = "Palm"
    palm, mid, along, side = palm_frame()
    hk.ball(k, palm.lerp(mid, 0.55) - UP * 0.17, 0.09, pad, seg=10, rings=5, squash=(UP, 0.45))
    k.bone = "Forearm"
    for j in range(24):                                                          # пушистая манжета: клочки шерсти
        t = hk.angle(j, 24)
        p, n, al, ac = zw.frame(0.035 + 0.012 * (j % 2), t, 0.03)
        cone(k, p - n * 0.01, p + n * 0.035 - al * 0.03, 0.026, cream if j % 3 else fur, seg=5)
    return k


def make_robot():
    """Рука робота: светлые панели, светящиеся кольца в суставах, ядро на тыльной стороне, поршни."""
    k = HandKit("Hand_Robot")
    metal, dark, glow, panel = C(0.7, 0.74, 0.8), C(0.22, 0.24, 0.28), C(0.25, 0.95, 1.0), C(0.86, 0.89, 0.93)
    forearm_skin(k, 0.45)
    fingers_parts(k, lambda f, i: panel if i != 1 else metal, width=0.95,
                  joints=lambda p, f, i: hk.ball(k, p, 0.07 if f != "Thumb" else 0.075, dark, seg=8, rings=4))
    for f in FINGERS:                                                            # светящиеся кольца в суставах
        pts = chain(f)
        for i in range(3):
            k.bone = f + str(i + 1)
            ring_around(k, pts[i], (pts[i + 1] - pts[i]).normalized(), (0.07 if f != "Thumb" else 0.075) + 0.004, 0.009,
                        glow)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    beam(k, palm.lerp(mid, -0.1), mid, 0.42, 0.24, metal)                      # ладонь-короб
    top = palm.lerp(mid, 0.45) + UP * 0.12
    for x in (-0.15, 0.15):                                                      # полосы панелей
        beam(k, top + side * x - along * 0.2, top + side * x + along * 0.2, 0.012, 0.012, dark)
    zw.lathe(k, top, UP, along, side, [(0, 0), (0.07, 0), (0.07, 0.012), (0.05, 0.018), (0, 0.018)],
             lambda i, q: dark if i < 2 else panel, seg=16)                     # ядро
    zw.lathe(k, top + UP * 0.016, UP, along, side, [(0, 0), (0.042, 0), (0.036, 0.012), (0, 0.016)], glow, seg=12)
    zw.glint(k, top + UP * 0.033 - along * 0.012 + side * 0.012, UP, along, 0.022, 0.006)
    k.bone = "Forearm"
    c0, c1 = ARM.centre(0.02), ARM.centre(0.5)
    hk.tube(k, [c0, c1], 0.15, metal, sides=10)                                  # предплечье
    for x in (-0.12, 0.12):
        hk.tube(k, [c0 + ARM.side * x + UP * 0.12, c1 + ARM.side * x + UP * 0.12], 0.025, dark, sides=6)   # поршни
        hk.tube(k, [c0 + ARM.side * x + UP * 0.12, c0.lerp(c1, 0.45) + ARM.side * x + UP * 0.12], 0.034, panel, sides=6)
    hk.ball(k, c0, 0.17, dark, seg=12, rings=6)                                  # шарнир запястья
    ring_around(k, c0 - ARM.axis * 0.0, ARM.axis, 0.172, 0.012, glow, seg=16)
    k.metallic, k.roughness = 0.6, 0.35
    return k


def make_gold():
    """Золотая рука: узор на тыльной стороне, рубин в оправе, перстни с камнями, браслет с камнями."""
    k = HandKit("Hand_Gold")
    gold, gold_dark, gold_light = GOLD, GOLD_DARK, C(1.0, 0.9, 0.55)
    copy_hand(k, gold, shade_by=lambda f: shade(gold, 0.85) if f.normal.z < -0.35 else gold)
    palm, mid, along, side = palm_frame()
    for f, gem in (("Ring", C(0.85, 0.08, 0.2)), ("Index", C(0.15, 0.45, 0.95)), ("Pinky", C(0.1, 0.75, 0.35))):
        pts = chain(f)
        k.bone = f + "1"
        c = pts[0].lerp(pts[1], 0.55)
        ring_around(k, c, (pts[1] - pts[0]).normalized(), finger_radius(f) + 0.012, 0.018, gold_dark)
        top = c + UP * (finger_radius(f) + 0.03)
        zw.bezel_setting(k, top - UP * 0.01, UP, along, 0.03, 0.016, gold_light)
        zw.gem_cut(k, top + UP * 0.003, UP, along, 0.026, gem, seg=8)
    k.bone = "Palm"
    centre = top_point(palm.lerp(mid, 0.45), 0.004)
    zw.bezel_setting(k, centre, UP, along, 0.06, 0.02, gold_light)
    zw.gem_cut(k, centre + UP * 0.016, UP, along, 0.05, C(0.9, 0.06, 0.18), seg=10)
    for q in range(8):                                                           # лучи узора вокруг рубина
        a = 2 * math.pi * q / 8
        d = along * math.cos(a) + side * math.sin(a)
        seam(k, centre + d * 0.075, centre + d * (0.13 if q % 2 else 0.17), gold_dark, lift=0.008, radius=0.008, steps=3)
    cuff(k, -0.02, 0.05, 0.004, 0.022, lambda ri, j: gold_light if ri == 3 else gold if ri in (2, 4) else gold_dark)
    for j in range(0, 48, 6):
        t = hk.angle(j, 48)
        if math.sin(t) < -0.2:
            continue
        p, n, al, ac = zw.frame(0.015, t, 0.026)
        zw.gem_cut(k, p, n, al, 0.014, (C(0.9, 0.06, 0.18), C(0.15, 0.45, 0.95))[j // 6 % 2], seg=6)
    k.metallic, k.roughness = 0.8, 0.25
    return k


def make_demon():
    """Рука демона: тёмно-красная кожа, длинные чёрные когти, рога на костяшках, трещины лавы, шипы на предплечье."""
    k = HandKit("Hand_Demon")
    red, dark, lava, lava_hot = C(0.6, 0.09, 0.08), C(0.3, 0.03, 0.04), C(1.0, 0.45, 0.08), C(1.0, 0.85, 0.3)
    copy_hand(k, red, shade_by=lambda f: dark if f.normal.z < -0.35 else red)
    for f in FINGERS:
        pts = chain(f)
        k.bone = f + "3"
        d = (pts[3] - pts[2]).normalized()
        taper(k, [pts[3] - d * 0.03 + UP * 0.02, pts[3] + d * 0.04 + UP * 0.01, pts[3] + d * 0.11 - UP * 0.04],
              [0.032, 0.022, 0.003], BLACK, sides=6)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    for f in ("Index", "Middle", "Ring", "Pinky"):                                # рога на костяшках
        p = top_point(head(f + "1").lerp(palm, 0.1), -0.01)
        taper(k, [p, p + UP * 0.05 - along * 0.015, p + UP * 0.11 - along * 0.05], [0.032, 0.02, 0.003], BLACK, sides=6)
    # трещины лавы: ветвятся от запястья к костяшкам
    cracks = [((0.05, 0.0), (0.4, 0.04), (0.75, -0.02)), ((0.4, 0.04), (0.62, 0.15)), ((0.25, -0.02), (0.5, -0.14)),
              ((0.75, -0.02), (0.9, 0.06))]
    for path in cracks:
        pts = [top_point(palm.lerp(mid, f) + side * x, 0.004) for f, x in path]
        for a, b in zip(pts, pts[1:]):
            hk.tube(k, [a, a.lerp(b, 0.5) + UP * 0.003, b], 0.017, lava, sides=5)
            hk.tube(k, [a + UP * 0.006, b + UP * 0.006], 0.005, lava_hot, sides=4)
    k.bone = "Forearm"
    for s in (0.08, 0.16, 0.24):                                                 # шипы по предплечью
        p, n, al, ac = zw.frame(s, math.pi / 2, -0.004)
        taper(k, [p, p + n * 0.05 + al * 0.02, p + n * 0.09 + al * 0.06], [0.03, 0.018, 0.003], BLACK, sides=6)
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
    """Рука Фортуны — фарфоровая рука статуи: синяя роспись цветком, золотые трещины (кинцуги) по руке и пальцам."""
    k = HandKit("Hand_Fortune")
    porcelain, blue, gold = C(0.97, 0.96, 0.93), C(0.18, 0.32, 0.72), C(1.0, 0.78, 0.25)
    copy_hand(k, porcelain, shade_by=lambda f: C(0.9, 0.89, 0.86) if f.normal.z < -0.35 else porcelain)
    palm, mid, along, side = palm_frame()
    k.bone = "Palm"
    c = top_point(palm.lerp(mid, 0.4) - side * 0.04, 0.003)                       # роспись: цветок и завитки
    for q in range(6):
        a = 2 * math.pi * q / 6
        d = along * math.cos(a) + side * math.sin(a)
        zw.disc(k, c + d * 0.034, UP, along, 0.026, 0.003, blue, seg=10)
    zw.disc(k, c + UP * 0.002, UP, along, 0.018, 0.003, porcelain, seg=10)
    for a0 in (0.4, 2.6, 4.4):
        pts = [top_point(c + (along * math.cos(a0 + u * 0.9) + side * math.sin(a0 + u * 0.9)) * (0.07 + u * 0.06), 0.004)
               for u in (0.0, 0.3, 0.6, 1.0)]
        hk.tube(k, pts, 0.007, blue, sides=4)
    # кинцуги: ветвистые золотые трещины по тыльной стороне и дальше по пальцам
    cracks = [((0.0, 0.18), (0.3, 0.1), (0.55, 0.15), (0.85, 0.06)), ((0.3, 0.1), (0.5, -0.12), (0.8, -0.16)),
              ((0.62, 0.15), (0.9, 0.2))]
    for path in cracks:
        pts = [top_point(palm.lerp(mid, f) + side * x, 0.005) for f, x in path]
        for a, b in zip(pts, pts[1:]):
            hk.tube(k, [a, a.lerp(b, 0.5) + UP * 0.003 + side * 0.008, b], 0.009, gold, sides=5)
    finger_line(k, "Index", gold, lift=0.004, radius=0.008, start=0.0, end=0.75, side=0.012)
    finger_line(k, "Ring", gold, lift=0.004, radius=0.008, start=0.2, end=1.0, side=0.01)
    finger_line(k, "Thumb", gold, lift=0.004, radius=0.008, start=0.0, end=0.6, side=0.01)
    for f in ("Middle", "Pinky"):                                                # тонкие золотые кольца
        pts = chain(f)
        k.bone = f + "1"
        ring_around(k, pts[0].lerp(pts[1], 0.6), (pts[1] - pts[0]).normalized(), finger_radius(f) + 0.008, 0.012, gold)
    k.metallic, k.roughness = 0.15, 0.25
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
