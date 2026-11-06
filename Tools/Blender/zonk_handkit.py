# -*- coding: utf-8 -*-
"""
Общее для облика руки «Зонка» (zonk_sleeves.py — рукава, zonk_wrist.py — часы и браслеты, zonk_hands.py — кисти):
замер предплечья руки автора, палитра плоских цветов и детали, которые ложатся на руку.

Стиль — как у самой руки: гранёные формы и плоские цвета. Каждая грань берёт цвет из палитры (ячейка 32×32 одного
цвета, UV в центр ячейки), текстура не растягивается по модели, поэтому вещь чёткая на любом расстоянии и не мылится
при сжатии. Узоры (полоски, клетка) — цвет отдельных граней, детали (швы, пуговицы, заклёпки, ремешки) — объёмом.

Координаты вещи — у руки: s — расстояние от запястья к локтю, t — угол вокруг руки (0 — к большому пальцу,
90° — тыльная сторона, 270° — ладонь), add — зазор над кожей. Сечение предплечья — эллипс, замеренный по модели
(Arm). Выгрузка — в осях руки Unity (пальцы +Z, ладонь −Y, мизинец +X, центр ладони в нуле): якорь в корне префаба
руки без смещения.

Зоны (чтобы вещи разных слотов не входили друг в друга):
    запястье  s 0.00…0.15, add до WRIST_MAX_ADD — часы и браслеты (zonk_wrist.py)
    рукав     край не ближе SLEEVE_MIN_EDGE к запястью; раструбы и оборки к кисти — шире WRIST_MAX_ADD
"""

import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

import zonk_models as zm
import zonk_arms as za

SEG = 48                          # граней по окружности (32 — рукава гранёные, видно полосы на свету)
POLAR = 96                        # лучей замера кожи по окружности (Arm.hug)
GAP = za.SLEEVE_GAP * za.SCALE    # зазор ткани над кожей
LINING = 0.05                     # глубина подкладки внутрь от края рукава
BACK_PAST_CUT = 0.35              # рукав заходит за срез к локтю: его конец у нижнего края кадра партии, а не в кадре
SLEEVE_MIN_EDGE = 0.16            # край рукава не ближе к запястью: там часы и браслеты
WRIST_FROM, WRIST_TO = 0.0, 0.15  # зона запястья
WRIST_MAX_ADD = 0.09              # браслеты и часы не выше этого над кожей (крупные: камера партии далеко)

PALETTE_GRID = 8                  # 8×8 ячеек
PALETTE_TEX = 256                 # ячейка 32 пикселя: мип-уровни до 16×16 не смешивают цвета

INSIDE = (0.08, 0.06, 0.06)       # подкладка и вход в рукав


# ---------------------------------------------------------------------------
# Рука
# ---------------------------------------------------------------------------

class Arm:
    """Предплечье руки после zonk_arms.place: ось от запястья к локтю, сечения-эллипсы по расстоянию s от запястья."""

    def __init__(self, armature, mesh):
        bones = armature.data.bones
        self.palm = bones["Palm"].head_local.copy()
        elbow = bones["Forearm"].head_local.copy()
        self.length = (elbow - self.palm).length
        self.axis = (elbow - self.palm).normalized()                          # к локтю
        self.up = (Vector((0, 0, 1)) - self.axis * self.axis.z).normalized()  # тыльная сторона кисти
        self.side = self.axis.cross(self.up)                                   # к большому пальцу
        self.cut = self.length * za.FOREARM_KEEP
        self.back = self.cut + BACK_PAST_CUT
        self.samples = []
        for k in range(19):
            s = self.length * 0.05 * k                                     # до 0.9 предплечья: рукав идёт к локтю
            self.samples.append((s,) + self._section(mesh, s))
        # поворот в оси руки Unity: тот же, что ZonkSetup.AlignHandModel делает с моделью руки по костям
        forward = (bones["Middle1"].head_local - self.palm).normalized()
        right = bones["Pinky1"].head_local - bones["Index1"].head_local
        right = (right - forward * right.dot(forward)).normalized()
        up = right.cross(forward)
        basis = Matrix((right, forward, up)).transposed()
        target = Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1))).transposed()   # right→−X, forward→−Y, up→+Z
        self.to_hand = (target @ basis.transposed()).to_4x4()

    def _section(self, mesh, s):
        p = self.palm + self.axis * s
        bm = bmesh.new()
        bm.from_mesh(mesh.data)
        cut = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=p, plane_no=self.axis)
        pts = [((v.co - p).dot(self.side), (v.co - p).dot(self.up))
               for v in cut["geom_cut"] if isinstance(v, bmesh.types.BMVert)]
        segs = []
        for e in cut["geom_cut"]:
            if isinstance(e, bmesh.types.BMEdge):
                q0, q1 = [((v.co - p).dot(self.side), (v.co - p).dot(self.up)) for v in e.verts]
                if math.hypot(*q0) < 0.6 and math.hypot(*q1) < 0.6:
                    segs.append((q0, q1))
        bm.free()
        pts = [q for q in pts if math.hypot(*q) < 0.6]
        xs, zs = [q[0] for q in pts], [q[1] for q in pts]
        cx, cz = (min(xs) + max(xs)) * 0.5, (min(zs) + max(zs)) * 0.5
        a, b = (max(xs) - min(xs)) * 0.5, (max(zs) - min(zs)) * 0.5
        # эллипс через крайние точки может срезать углы сечения: растянуть, чтобы все точки были внутри
        k = max(math.sqrt(((x - cx) / a) ** 2 + ((z - cz) / b) ** 2) for x, z in pts)
        a, b = a * max(k, 1.0), b * max(k, 1.0)
        return cx, cz, a, b, self._polar(segs, cx, cz, a, b)

    @staticmethod
    def _polar(segs, cx, cz, a, b):
        """Расстояние от центра до кожи по лучу к точке эллипса угла t (POLAR лучей): по нему вещи облегают гранёную руку."""
        out = []
        for j in range(POLAR):
            t = angle(j, POLAR)
            dx, dz = math.cos(t) * a, math.sin(t) * b
            n = math.hypot(dx, dz)
            dx, dz = dx / n, dz / n
            best = 0.0
            for (x0, z0), (x1, z1) in segs:
                ex, ez = x1 - x0, z1 - z0
                den = dx * ez - dz * ex
                if abs(den) < 1e-9:
                    continue
                ox, oz = x0 - cx, z0 - cz
                r = (ox * ez - oz * ex) / den
                u = (ox * dz - oz * dx) / den
                if r > 0 and -1e-6 <= u <= 1 + 1e-6:
                    best = max(best, r)
            out.append(best if best > 0 else n)
        # чуть сгладить углы граней, но не уходить внутрь кожи
        return [max(out[j], 0.5 * (out[j - 1] + out[(j + 1) % POLAR])) for j in range(POLAR)]

    def hug(self, s, t, add=0.0):
        """Точка на высоте add над кожей по лучу угла t: вплотную к граням руки, без зазора эллипса."""
        sm = self.samples
        i = min(max(int(s / (sm[1][0] - sm[0][0])), 0), len(sm) - 2)
        f = (s - sm[i][0]) / (sm[i + 1][0] - sm[i][0])
        cx, cz, a, b = [sm[i][j] + (sm[i + 1][j] - sm[i][j]) * f for j in range(1, 5)]
        x = ((t + math.pi / 2) / (2 * math.pi) * POLAR) % POLAR
        j0 = int(x)
        g = x - j0
        j1 = (j0 + 1) % POLAR
        r = 0.0
        for q, w in ((i, 1 - f), (i + 1, f)):
            polar = sm[q][5]
            r += w * (polar[j0] * (1 - g) + polar[j1] * g)
        d = self.side * (math.cos(t) * a) + self.up * (math.sin(t) * b)
        return self.palm + self.axis * s + self.side * cx + self.up * cz + d.normalized() * (r + add)

    def ellipse(self, s):
        """(центр, полуось к большому пальцу, полуось вверх) на расстоянии s; за краями замера — по прямой."""
        sm = self.samples
        i = min(max(int(s / (sm[1][0] - sm[0][0])), 0), len(sm) - 2)
        s0, s1 = sm[i][0], sm[i + 1][0]
        t = (s - s0) / (s1 - s0)
        cx, cz, a, b = [sm[i][j] + (sm[i + 1][j] - sm[i][j]) * t for j in range(1, 5)]
        return self.palm + self.axis * s + self.side * cx + self.up * cz, a, b

    def point(self, s, t, add=0.0, grow=1.0, drop=0.0):
        """Точка на высоте add над кожей; grow — шире эллипса, drop — центр ниже (свисающий рукав)."""
        c, a, b = self.ellipse(s)
        return (c - self.up * drop + self.side * math.cos(t) * (a * grow + add)
                + self.up * math.sin(t) * (b * grow + add))

    def normal(self, s, t):
        """Наружу от поверхности (нормаль эллипса)."""
        _, a, b = self.ellipse(s)
        return (self.side * (math.cos(t) / a) + self.up * (math.sin(t) / b)).normalized()

    def tangent(self, s, t):
        """Вдоль окружности (по росту t)."""
        return self.normal(s, t).cross(self.axis).normalized() * -1.0

    def centre(self, s):
        return self.ellipse(s)[0]

    def max_drop(self, s, add, grow):
        """Насколько можно опустить центр кольца, чтобы его верх не вошёл в руку."""
        _, a, b = self.ellipse(s)
        return max(0.0, b * (grow - 1.0) + add - GAP * 0.5)


def load_arm():
    """Рука как в zonk_arms.py: (арматура, меш руки со срезом, замер предплечья)."""
    armature, hand = za.load_source()
    za.keep_right_bones(armature)
    za.fix_weights(hand, armature)
    za.setup_materials(hand)
    za.place(armature, hand)
    arm = Arm(armature, hand)
    za.cut_forearm(hand, armature)
    return armature, hand, arm


def angle(j, seg=SEG):
    """Угол j-й грани; шов — снизу, на ладонной стороне."""
    return -math.pi / 2 + 2 * math.pi * j / seg


# ---------------------------------------------------------------------------
# Палитра и сборка
# ---------------------------------------------------------------------------

def C(r, g, b):
    return (r, g, b)


def shade(color, k):
    """Темнее (k < 1) или светлее (k > 1)."""
    return tuple(min(1.0, max(0.0, c * k)) for c in color)


class Kit:
    """Одна вещь: меш из граней плоских цветов. Цвет грани — ячейка палитры, UV в её центр."""

    def __init__(self, name, arm):
        self.name = name
        self.arm = arm
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.colors = []
        self.metallic = 0.0
        self.roughness = 0.6

    def cell(self, color):
        key = tuple(round(c, 3) for c in color)
        if key not in self.colors:
            assert len(self.colors) < PALETTE_GRID * PALETTE_GRID, "палитра переполнена"
            self.colors.append(key)
        i = self.colors.index(key)
        size = 1.0 / PALETTE_GRID
        return ((i % PALETTE_GRID + 0.5) * size, (i // PALETTE_GRID + 0.5) * size)

    def vert(self, co):
        return self.bm.verts.new(co)

    def face(self, verts, color, smooth=True):
        f = self.bm.faces.new(verts)
        uv = self.cell(color)
        for loop in f.loops:
            loop[self.uv].uv = uv
        f.smooth = smooth
        return f

    def quads(self, rings, color, closed=True):
        """Полосы граней между кольцами; color(ri, j) — цвет грани (номер полосы, номер грани по кругу)."""
        for ri, (r0, r1) in enumerate(zip(rings, rings[1:])):
            n = len(r0)
            for j in range(n if closed else n - 1):
                k = (j + 1) % n
                self.face((r0[j], r0[k], r1[k], r1[j]), color(ri, j) if callable(color) else color)

    def fan(self, ring, centre, color, flip=False):
        c = self.vert(centre)
        n = len(ring)
        for j in range(n):
            k = (j + 1) % n
            self.face((c, ring[j], ring[k]) if flip else (c, ring[k], ring[j]), color)

    def bake(self):
        size = PALETTE_TEX // PALETTE_GRID
        img = np.full((PALETTE_TEX, PALETTE_TEX, 3), 0.5, dtype=np.float32)
        for i, color in enumerate(self.colors):
            x, y = i % PALETTE_GRID, i // PALETTE_GRID
            img[y * size:(y + 1) * size, x * size:(x + 1) * size] = color
        return img

    def build(self, textures_dir):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(34)
        mesh.validate()
        mesh.update()
        image = zm.save_png(self.name, self.bake(), os.path.join(textures_dir, self.name + ".png"))
        image.colorspace_settings.name = "sRGB"
        mat = zm.make_material(self.name, image)
        nodes = mat.node_tree.nodes
        for node in nodes:
            if node.type == "TEX_IMAGE":
                node.interpolation = "Closest"
        bsdf = nodes.get("Principled BSDF")
        bsdf.inputs["Metallic"].default_value = self.metallic
        bsdf.inputs["Roughness"].default_value = self.roughness
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ---------------------------------------------------------------------------
# Детали на руке
# ---------------------------------------------------------------------------

def ring_at(k, s, add, grow=1.0, drop=0.0, wave=None, seg=SEG, clip=True):
    a = k.arm
    if drop > 0 and clip:
        drop = min(drop, a.max_drop(s, add, grow))
    return [k.vert(a.point(s, angle(j, seg), add + (wave(angle(j, seg)) if wave else 0.0), grow, drop))
            for j in range(seg)]


def sleeve(k, rings, color, edge_color=None, inside=INSIDE, lining_add=None, thin=None):
    """
    Рукав — замкнутая оболочка: снаружи кольца rings [dict(s, add, grow, drop, wave)] от края у запястья к локтю,
    на краю — торец до подкладки (edge_color), внутрь — подкладка, со стороны локтя — тёмный вход в рукав.
    color(ri, j) или цвет — грани снаружи.
    thin — толщина ткани широкого рукава: подкладка (inside) идёт по форме рукава до локтя, в раструбе видна она и рука,
    а не плоское кольцо торца.
    """
    if thin:
        # ткань и подкладка провисают одинаково (по пределу подкладки): иначе подкладка проступает сквозь рукав
        def sag(r):
            return min(r.get("drop", 0.0), k.arm.max_drop(r["s"], r.get("add", GAP) - thin, r.get("grow", 1.0)))

        outer = [ring_at(k, r["s"], r.get("add", GAP), r.get("grow", 1.0), sag(r), r.get("wave"), clip=False) for r in rings]
        inner = [ring_at(k, r["s"], r.get("add", GAP) - thin, r.get("grow", 1.0), sag(r), r.get("wave"), clip=False)
                 for r in rings]
        k.quads(outer, color)
        k.quads([inner[0], outer[0]], edge_color or (color(0, 0) if callable(color) else color))
        k.quads(list(reversed(inner)), inside)
        k.quads([outer[-1], inner[-1]], inside)
        # Тёмная перегородка у локтя (тонкая замкнутая пластина — видна с обеих сторон): изнутри рукав не просвечивает.
        back = rings[-1]
        args = (back.get("add", GAP) - thin, back.get("grow", 1.0), back.get("drop", 0.0))
        near = ring_at(k, back["s"] - 0.004, *args)
        far = ring_at(k, back["s"] - 0.01, *args)
        drop = min(args[2], k.arm.max_drop(back["s"], args[0], args[1]))
        centre = k.arm.centre(back["s"]) - k.arm.up * drop
        k.quads([near, far], shade(inside, 0.6))
        k.fan(near, centre - k.arm.axis * 0.004, shade(inside, 0.6))
        k.fan(far, centre - k.arm.axis * 0.01, shade(inside, 0.6), flip=True)
        return

    lining_add = GAP * 0.4 if lining_add is None else lining_add
    outer = [ring_at(k, r["s"], r.get("add", GAP), r.get("grow", 1.0), r.get("drop", 0.0), r.get("wave")) for r in rings]
    s_edge = rings[0]["s"]
    inner0 = ring_at(k, s_edge, lining_add)
    inner1 = ring_at(k, s_edge + LINING, lining_add)
    k.quads(outer, color)
    k.quads([inner0, outer[0]], edge_color or (color(0, 0) if callable(color) else color))
    k.quads([inner1, inner0], inside)
    back = rings[-1]
    k.fan(outer[-1], k.arm.centre(back["s"]) - k.arm.up * min(back.get("drop", 0.0),
                                                               k.arm.max_drop(back["s"], back.get("add", GAP), back.get("grow", 1.0))),
          inside)
    k.fan(inner1, k.arm.centre(s_edge + LINING), inside, flip=True)


def band(k, s0, s1, add_in, add_out, color, grow=1.0, drop=0.0, wave=None, rows=1):
    """Замкнутое кольцо поверх руки или рукава (манжета, ремешок, повязка): от s0 к s1, толщина add_in..add_out."""
    outer = []
    for r in range(rows + 1):
        s = s0 + (s1 - s0) * r / rows
        outer.append(ring_at(k, s, add_out, grow, drop, (lambda t, r=r: wave(t, r)) if wave else None))
    in1 = ring_at(k, s1, add_in, grow, drop)
    in0 = ring_at(k, s0, add_in, grow, drop)
    k.quads(outer + [in1, in0, outer[0]], color)


def roll(k, s, add, radius, color, grow=1.0, sides=8):
    """Закатанный край или кольцо-тор вокруг руки в точке s; color(ri, j) — по сечению и по кругу."""
    rings = []
    for q in range(sides):
        phi = 2 * math.pi * q / sides
        rings.append(ring_at(k, s + radius * math.cos(phi), add + radius + radius * math.sin(phi), grow))
    k.quads(rings + [rings[0]], color)


def flare(k, s0, s1, add0, add1, color, wave=None, thick=0.012, rows=3, grow=1.0, drop=0.0):
    """Раструб, оборка: тонкая оболочка от s0 (под рукавом) к s1 (к кисти), расходится; wave(t) — волна края."""
    outer, inner = [], []
    for r in range(rows + 1):
        q = r / rows
        s = s0 + (s1 - s0) * q
        add = add0 + (add1 - add0) * q * q
        w = (lambda t, q=q: wave(t) * q) if wave else None
        outer.append(ring_at(k, s, add, grow, drop * q, w))
        inner.append(ring_at(k, s, add - thick, grow, drop * q, w))
    k.quads(outer + list(reversed(inner)) + [outer[0]], color)


def frame_at(k, s, t, add, grow=1.0, drop=0.0):
    """Точка на руке (или на рукаве шириной grow) и её оси: (точка, нормаль наружу, вдоль руки к локтю, по кругу)."""
    a = k.arm
    n = a.normal(s, t)
    along = (a.axis - n * a.axis.dot(n)).normalized()
    across = n.cross(along).normalized()
    if drop > 0:
        drop = min(drop, a.max_drop(s, add, grow))
    return a.point(s, t, add, grow, drop), n, along, across


def box_on(k, s, t, add, width, length, height, color, top=None, tilt=0.0, grow=1.0, drop=0.0):
    """Брусок на руке: ширина по кругу, длина вдоль руки, высота наружу. Пуговица, пряжка, карта, заклёпка."""
    p, n, along, across = frame_at(k, s, t, add, grow, drop)
    if tilt:
        rot = Matrix.Rotation(tilt, 3, n)
        along, across = rot @ along, rot @ across
    hw, hl = width * 0.5, length * 0.5
    corners = []
    for h in (0.0, height):
        corners.append([k.vert(p + n * h + across * x + along * y) for x, y in ((-hw, -hl), (hw, -hl), (hw, hl), (-hw, hl))])
    b, tp = corners
    k.face(list(reversed(b)), color, smooth=False)
    k.face(tp, top or color, smooth=False)
    for j in range(4):
        q = (j + 1) % 4
        k.face((b[j], b[q], tp[q], tp[j]), color, smooth=False)


def slab(k, origin, length_dir, width_dir, up_dir, length, width, height, color, top=None):
    """Брусок от точки origin: длина по length_dir, ширина по width_dir (по центру), высота по up_dir."""
    hw = width * 0.5
    b = [k.vert(origin + width_dir * x + length_dir * y) for x, y in ((-hw, 0), (hw, 0), (hw, length), (-hw, length))]
    tp = [k.vert(v.co + up_dir * height) for v in b]
    k.face(list(reversed(b)), color, smooth=False)
    k.face(tp, top or color, smooth=False)
    for j in range(4):
        q = (j + 1) % 4
        k.face((b[j], b[q], tp[q], tp[j]), color, smooth=False)


def sticking_out(k, s, t, add, pitch):
    """Оси вещи, которая выезжает из рукава к кисти и отходит от руки на угол pitch: (начало, вдоль, поперёк, вверх)."""
    p, n, along, across = frame_at(k, s, t, add)
    out = -along * math.cos(pitch) + n * math.sin(pitch)
    up = n * math.cos(pitch) + along * math.sin(pitch)
    return p, out.normalized(), across, up.normalized()


def cyl_on(k, s, t, add, radius, height, color, top=None, seg=12, bevel=0.0, grow=1.0, drop=0.0):
    """Цилиндр на руке, ось — нормаль (корпус часов, пуговица, монета). bevel — скос верха."""
    p, n, along, across = frame_at(k, s, t, add, grow, drop)

    def ring(h, r):
        return [k.vert(p + n * h + (across * math.cos(2 * math.pi * q / seg) + along * math.sin(2 * math.pi * q / seg)) * r)
                for q in range(seg)]

    rings = [ring(0.0, radius), ring(height - bevel, radius)]
    if bevel > 0:
        rings.append(ring(height, radius - bevel))
    k.quads(rings, color)
    k.fan(rings[0], p, color, flip=False)
    k.fan(rings[-1], p + n * height, top or color, flip=True)


def stud(k, s, t, add, radius, height, color, seg=4, grow=1.0, drop=0.0):
    """Шип или заклёпка-пирамида."""
    p, n, along, across = frame_at(k, s, t, add, grow, drop)
    base = [k.vert(p + (across * math.cos(2 * math.pi * q / seg + math.pi / 4) +
                        along * math.sin(2 * math.pi * q / seg + math.pi / 4)) * radius) for q in range(seg)]
    tip = k.vert(p + n * height)
    for q in range(seg):
        k.face((base[q], base[(q + 1) % seg], tip), color, smooth=False)
    k.face(list(reversed(base)), color, smooth=False)


def gem(k, s, t, add, radius, color, height=None, grow=1.0, drop=0.0):
    """Гранёный камень: восьмиугольник с плоской площадкой."""
    height = height or radius * 0.8
    p, n, along, across = frame_at(k, s, t, add, grow, drop)
    seg = 8
    girdle = [k.vert(p + n * height * 0.35 + (across * math.cos(2 * math.pi * q / seg) +
                                               along * math.sin(2 * math.pi * q / seg)) * radius) for q in range(seg)]
    table = [k.vert(p + n * height + (across * math.cos(2 * math.pi * q / seg) +
                                      along * math.sin(2 * math.pi * q / seg)) * radius * 0.55) for q in range(seg)]
    bottom = k.vert(p)
    for q in range(seg):
        r = (q + 1) % seg
        k.face((bottom, girdle[r], girdle[q]), shade(color, 0.7), smooth=False)
        k.face((girdle[q], girdle[r], table[r], table[q]), color, smooth=False)
    k.face(table, shade(color, 1.25), smooth=False)


def strip(k, s0, s1, t0, t1, add, height, color, rows=2, cols=None, top=None, grow=1.0):
    """Выпуклая полоса на руке (шов, молния, полотенце, лампас): от s0 к s1, по углу t0..t1, высота height."""
    cols = cols or max(2, int(abs(t1 - t0) / (2 * math.pi) * SEG))
    a = k.arm

    def grid(h):
        return [[k.vert(a.point(s0 + (s1 - s0) * r / rows, t0 + (t1 - t0) * c / cols, add + h, grow)) for c in range(cols + 1)]
                for r in range(rows + 1)]

    lo, hi = grid(0.0), grid(height)
    for r in range(rows):
        for c in range(cols):
            k.face((hi[r][c], hi[r][c + 1], hi[r + 1][c + 1], hi[r + 1][c]), top(r, c) if callable(top) else (top or color))
            k.face((lo[r][c], lo[r + 1][c], lo[r + 1][c + 1], lo[r][c + 1]), color)
    for r in range(rows):                       # бока вдоль руки
        k.face((lo[r][0], hi[r][0], hi[r + 1][0], lo[r + 1][0]), color)
        k.face((lo[r][cols], lo[r + 1][cols], hi[r + 1][cols], hi[r][cols]), color)
    for c in range(cols):                       # торцы
        k.face((lo[0][c], lo[0][c + 1], hi[0][c + 1], hi[0][c]), color)
        k.face((lo[rows][c], hi[rows][c], hi[rows][c + 1], lo[rows][c + 1]), color)


def flap(k, s0, s1, t, add, length, color, thick=0.012):
    """Свисающий конец ткани (полотенце, шарф): от линии (s0..s1, угол t) вниз на length, толщина thick."""
    a = k.arm
    down = -a.up
    top = [a.point(s0, t, add), a.point(s1, t, add)]
    n = (a.normal(s0, t) + a.normal(s1, t)).normalized()
    pts = top + [top[1] + down * length, top[0] + down * length]
    outer = [k.vert(p + n * thick * 0.5) for p in pts]
    inner = [k.vert(p - n * thick * 0.5) for p in pts]
    k.face(outer, color, smooth=False)
    k.face(list(reversed(inner)), color, smooth=False)
    for j in range(4):
        q = (j + 1) % 4
        k.face((outer[j], inner[j], inner[q], outer[q]), color, smooth=False)


def ball(k, centre, radius, color, seg=8, rings=5, squash=None):
    """Шар (бусина, головка): squash = (ось, коэффициент) — приплюснуть вдоль оси."""
    verts = []
    for r in range(1, rings):
        phi = math.pi * r / rings
        verts.append([Vector((math.sin(phi) * math.cos(2 * math.pi * q / seg), math.sin(phi) * math.sin(2 * math.pi * q / seg),
                              math.cos(phi))) for q in range(seg)])
    pole_n, pole_s = Vector((0, 0, 1)), Vector((0, 0, -1))

    def place(v):
        if squash:
            axis, f = squash
            v = v + axis * (v.dot(axis) * (f - 1.0))
        return k.vert(centre + v * radius)

    rings_v = [[place(v) for v in ring] for ring in verts]
    top, bottom = place(pole_n), place(pole_s)
    k.quads(rings_v, color)
    for q in range(seg):
        p = (q + 1) % seg
        k.face((top, rings_v[0][q], rings_v[0][p]), color)
        k.face((bottom, rings_v[-1][p], rings_v[-1][q]), color)


def link(k, centre, e1, e2, radius, thick, color, stretch=1.4, seg=10, sides=5):
    """Звено цепи: тор в плоскости (e1, e2), вытянут вдоль e1."""
    normal = e1.cross(e2).normalized()
    rings = []
    for q in range(seg):
        u = 2 * math.pi * q / seg
        d = e1 * math.cos(u) * stretch + e2 * math.sin(u)
        rings.append([k.vert(centre + d * radius + (d.normalized() * math.cos(2 * math.pi * v / sides) +
                                                     normal * math.sin(2 * math.pi * v / sides)) * thick)
                      for v in range(sides)])
    k.quads(rings + [rings[0]], color)


def tube(k, path, radius, color, sides=6):
    """Трубка по точкам (шнурок, цепочка, нить), концы закрыты."""
    rings = []
    for q, p in enumerate(path):
        d = (path[min(q + 1, len(path) - 1)] - path[max(q - 1, 0)]).normalized()
        u = d.orthogonal().normalized()
        v = d.cross(u).normalized()
        rings.append([k.vert(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * radius)
                      for i in range(sides)])
    k.quads(rings, color)
    k.face(list(reversed(rings[0])), color)
    k.face(rings[-1], color)


def hang(k, s, t, add, width, length, color, thick=0.008, grow=1.0, drop=0.0):
    """Свисающая полоска (бахрома, кисточка): от точки на руке вниз (к ладони — при покое руки это вниз)."""
    p, n, along, across = frame_at(k, s, t, add, grow, drop)
    down = -k.arm.up
    hw = width * 0.5
    top = [p + across * -hw, p + across * hw]
    pts = top + [top[1] + down * length, top[0] + down * length]
    back = [q - n * thick for q in pts]
    f = [k.vert(q) for q in pts]
    b = [k.vert(q) for q in back]
    k.face(f, color, smooth=False)
    k.face(list(reversed(b)), color, smooth=False)
    for j in range(4):
        q = (j + 1) % 4
        k.face((f[j], b[j], b[q], f[q]), color, smooth=False)


# ---------------------------------------------------------------------------
# Проверка, превью, выгрузка
# ---------------------------------------------------------------------------

def inside_count(obj, arm, s_max=None, hugged=False):
    """
    Сколько вершин вещи внутри руки (кроме центров донц — они внутри по построению).
    hugged — сверять с настоящими гранями кожи (Arm.hug), а не с описанным эллипсом: для вещей вплотную к руке.
    """
    s_max = arm.cut if s_max is None else s_max
    inside = 0
    for v in obj.data.vertices:
        s = (v.co - arm.palm).dot(arm.axis)
        if s < 0 or s > s_max:
            continue
        c, a, b = arm.ellipse(s)
        q = v.co - c
        x, z = q.dot(arm.side), q.dot(arm.up)
        if hugged:
            t = math.atan2(z / b, x / a)
            skin = arm.hug(s, t, 0.0) - (arm.palm + arm.axis * s)
            centre = c - (arm.palm + arm.axis * s)
            if math.hypot(x, z) < (skin - centre).length * 0.99:
                inside += 1
        elif (x / a) ** 2 + (z / b) ** 2 < 0.995:
            inside += 1
    return inside


def render(out_dir, objs, armature, hand, prefix, cols=5):
    """Превью: рука с каждой вещью — общий вид сверху-сзади (как камера в игре) и крупно по одной."""
    scene = bpy.context.scene
    spacing = 1.3
    hands = []
    for q, obj in enumerate(objs):
        x = (q % cols - (cols - 1) / 2) * spacing
        y = -(q // cols) * spacing * 1.7
        obj.location = (x, y, 0)
        h = hand.copy()
        h.location = (x, y, 0)
        scene.collection.objects.link(h)
        for mod in list(h.modifiers):
            h.modifiers.remove(mod)
        hands.append(h)
    hand.hide_render = True
    armature.hide_render = True

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.44, 0.48, 1)
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(40), math.radians(15), math.radians(150))
    scene.collection.objects.link(sun)
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 16
    scene.view_settings.view_transform = "Standard"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.lens = 40

    rows = (len(objs) + cols - 1) // cols
    centre = Vector((0, -(rows - 1) * spacing * 0.85 + 0.2, 0))
    cam.location = centre + Vector((1.2, 6.5 + rows * 0.8, 6.0 + rows * 0.6))
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.resolution_x, scene.render.resolution_y = 2000, 1500
    scene.render.filepath = os.path.join(out_dir, prefix + "_all.png")
    bpy.ops.render.render(write_still=True)

    scene.render.resolution_x, scene.render.resolution_y = 800, 640
    for obj in objs:
        target = obj.location + Vector((0, 0.3, 0.0))
        # a — как камера партии (сзади-сверху, со стороны большого пальца), b — как ракурс магазина (сбоку от мизинца)
        for view, offset in (("a", Vector((0.8, 2.0, 1.8))), ("b", Vector((-1.7, 0.2, 0.9)))):
            cam.location = target + offset
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, "%s_%s_%s.png" % (prefix, obj.name, view))
            bpy.ops.render.render(write_still=True)
    for h in hands:
        bpy.data.objects.remove(h)
    hand.hide_render = False
    for obj in objs:
        obj.location = (0, 0, 0)


def export(obj, arm, models_dir):
    obj.data.transform(arm.to_hand)
    zm.export_fbx(obj, os.path.join(models_dir, obj.name + ".fbx"))
