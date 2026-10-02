# -*- coding: utf-8 -*-
"""
Лампы над столом для игры «Зонк»: шесть подвесных светильников в комиксовом стиле.

    Lamp_Candle      кованая тарелка на трёх цепях, толстая оплывшая свеча
    Lamp_Oil         латунная масляная лампа на цепи, стеклянный колпак с огнём
    Lamp_Lantern6    шестигранный кованый фонарь со свечой, островерхая крыша
    Lamp_Skull       череп на цепи, на макушке оплывшая свеча, воск течёт по черепу
    Lamp_Chandelier  кованое колесо на трёх цепях, шесть свечей по кругу
    Lamp_Orb         магический шар в железных когтях

Как лампа стоит в игре (ZonkSetup.Placeholders, Lamp_Basic / Lamp_Lantern / Lamp_Cage):
    - якорь лампы "lamp" висит над центром стола на высоте 3.6 (Unity Y);
    - начало координат лампы = точка якоря; корпус лампы — вокруг начала, примерно от -0.5 до +0.45 по высоте;
    - провод/цепь уходит вверх от корпуса до +2.0 (в потолок, в кадре обычно не видна);
    - ширина корпуса 0.5 (фонарь) … 1.2 (абажур Lamp_Basic); люстра здесь — 1.2;
    - свет (Point, без теней) у старых ламп на 0.1–0.35 ниже начала координат.
Здесь свет — пустой объект "LightPoint" в модели: генератор ставит туда Light.

Материалы: слот 0 "<Имя>" — основная текстура (атлас 1024, ячейки 256: железо, латунь, воск, кость, череп);
слот 1 "<Имя>_Glow" — светящиеся части (пламя, стекло колпака, шар), UV в ячейках пламени/стекла/шара
того же атласа. В Unity светящийся материал — не Zonk/Toon, а с эмиссией (как "Bulb" / "Candle" у старых ламп).

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_lamps.py
    blender --background --factory-startup --python Tools/Blender/zonk_lamps.py -- --render <папка>

Результат: Assets/ZonkContent/Art/Models/Lamps/<Имя>.fbx и Art/Textures/Lamps/<Имя>.png.
Оси как в zonk_models.py: Unity = (-bx, bz, -by). Лицом к камере — +Y Blender (−Z Unity... см. FRONT).
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_cups as zc    # noqa: E402  comic_flatten, lerp, col

# ---------------------------------------------------------------------------
# Параметры
# ---------------------------------------------------------------------------

PROJECT_ROOT = zm.PROJECT_ROOT
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Lamps")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Lamps")

TEX = 1024            # атлас
GRID = 4              # 4×4 ячейки по 256
CELL_MARGIN = 0.04    # отступ UV внутри ячейки
CEILING = 2.0         # до какой высоты идёт цепь/провод
FRONT = math.pi / 2   # угол «лица» (черепа) — к камере
MAX_TRIS = 2000

# Ячейки атласа: (столбец, строка)
CELLS = {
    "iron": (0, 0), "brass": (1, 0), "wax": (2, 0), "bone": (3, 0),
    "skull": (0, 1), "chain": (1, 1), "copper": (2, 1), "dark": (3, 1),
    "enamel": (0, 2), "fabric": (1, 2), "paper": (2, 2), "emerald": (3, 2),
    "flame": (0, 3), "glass": (1, 3), "orb": (2, 3), "crystal": (3, 3),
}
GLOW = {"flame", "glass", "orb", "paper", "emerald", "crystal"}


def cell_uv(paint, u, v):
    c, r = CELLS[paint]
    m = CELL_MARGIN
    return ((c + m + (1 - 2 * m) * min(max(u, 0.0), 1.0)) / GRID, (r + m + (1 - 2 * m) * min(max(v, 0.0), 1.0)) / GRID)


# ---------------------------------------------------------------------------
# Геометрия
# ---------------------------------------------------------------------------

class Builder:
    """Один меш лампы: грани с UV в ячейке атласа и номером материала (0 — основной, 1 — свечение)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def face(self, verts, uvs, paint):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = 1 if paint in GLOW else 0
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def lathe(self, profile, paint, seg=12, center=(0.0, 0.0, 0.0), dr=None, cap_bottom=True, cap_top=True, u_repeat=1.0):
        """Тело вращения: профиль (r, z) снизу вверх. r = 0 — полюс. dr(a, z) — смещение радиуса."""
        cx, cy, cz = center
        lengths = [0.0]
        for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
            lengths.append(lengths[-1] + math.hypot(r1 - r0, z1 - z0))
        total = lengths[-1] or 1.0
        rings = []
        for (r, z) in profile:
            if r < 1e-6:
                rings.append([self.bm.verts.new((cx, cy, cz + z))])
                continue
            ring = []
            for j in range(seg):
                a = 2 * math.pi * j / seg
                rr = r + (dr(a, z) if dr else 0.0)
                ring.append(self.bm.verts.new((cx + rr * math.cos(a), cy + rr * math.sin(a), cz + z)))
            rings.append(ring)
        for i in range(len(rings) - 1):
            r0, r1 = rings[i], rings[i + 1]
            v0, v1 = lengths[i] / total, lengths[i + 1] / total
            for j in range(seg):
                u0, u1 = (j / seg * u_repeat) % 1.0, ((j / seg * u_repeat) % 1.0) + u_repeat / seg
                if len(r0) == 1 and len(r1) == 1:
                    continue
                if len(r0) == 1:
                    self.face([r0[0], r1[j], r1[(j + 1) % seg]],
                              [cell_uv(paint, (u0 + u1) / 2, v0), cell_uv(paint, u0, v1), cell_uv(paint, u1, v1)], paint)
                elif len(r1) == 1:
                    self.face([r0[j], r0[(j + 1) % seg], r1[0]],
                              [cell_uv(paint, u0, v0), cell_uv(paint, u1, v0), cell_uv(paint, (u0 + u1) / 2, v1)], paint)
                else:
                    self.face([r0[j], r0[(j + 1) % seg], r1[(j + 1) % seg], r1[j]],
                              [cell_uv(paint, u0, v0), cell_uv(paint, u1, v0), cell_uv(paint, u1, v1), cell_uv(paint, u0, v1)],
                              paint)
        for ring, cap, v in ((rings[0], cap_bottom, 0.0), (rings[-1], cap_top, 1.0)):
            if cap and len(ring) > 2:
                verts = list(reversed(ring)) if v == 0.0 else ring
                self.face(verts, [cell_uv(paint, 0.5 + 0.45 * math.cos(2 * math.pi * k / len(ring)), v) for k in range(len(ring))],
                          paint)

    def tube(self, path, radii, paint, sides=6, caps=True):
        rings = []
        n = len(path)
        for k, (p, rad) in enumerate(zip(path, radii)):
            t = (path[min(k + 1, n - 1)] - path[max(k - 1, 0)]).normalized()
            a = t.orthogonal().normalized()
            b = t.cross(a).normalized()
            rings.append([self.bm.verts.new(p + (a * math.cos(2 * math.pi * s / sides) + b * math.sin(2 * math.pi * s / sides)) * rad)
                          for s in range(sides)])
        for k in range(n - 1):
            r0, r1 = rings[k], rings[k + 1]
            for s in range(sides):
                u0, u1 = s / sides, (s + 1) / sides
                v0, v1 = k / (n - 1), (k + 1) / (n - 1)
                self.face([r0[s], r0[(s + 1) % sides], r1[(s + 1) % sides], r1[s]],
                          [cell_uv(paint, u0, v0), cell_uv(paint, u1, v0), cell_uv(paint, u1, v1), cell_uv(paint, u0, v1)], paint)
        if caps:
            self.face(list(reversed(rings[0])), [cell_uv(paint, 0.5, 0.0)] * sides, paint)
            self.face(rings[-1], [cell_uv(paint, 0.5, 1.0)] * sides, paint)

    def torus(self, center, radius, thick, paint, seg=24, sides=6, z_axis=True):
        cx, cy, cz = center
        rings = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            d = Vector((math.cos(a), math.sin(a), 0.0)) if z_axis else Vector((math.cos(a), 0.0, math.sin(a)))
            up = Vector((0.0, 0.0, 1.0)) if z_axis else Vector((0.0, 1.0, 0.0))
            ring = []
            for s in range(sides):
                b = 2 * math.pi * s / sides
                p = Vector(center) + d * (radius + thick * math.cos(b)) + up * thick * math.sin(b)
                ring.append(self.bm.verts.new(p))
            rings.append(ring)
        for j in range(seg):
            r0, r1 = rings[j], rings[(j + 1) % seg]
            for s in range(sides):
                u0, u1 = j / seg, (j + 1) / seg
                v0, v1 = s / sides, (s + 1) / sides
                self.face([r0[s], r1[s], r1[(s + 1) % sides], r0[(s + 1) % sides]],
                          [cell_uv(paint, u0, v0), cell_uv(paint, u1, v0), cell_uv(paint, u1, v1), cell_uv(paint, u0, v1)], paint)

    def chain(self, start, end, paint="chain", link=0.07, thick=0.012, full=0.7, rod=0.012):
        """Цепь от start к end: звенья на первых full метрах, дальше тонкий прут (выше кадра)."""
        start, end = Vector(start), Vector(end)
        axis = end - start
        length = axis.length
        d = axis.normalized()
        side = d.orthogonal().normalized()
        side2 = d.cross(side).normalized()
        n = int(min(full, length) / (link * 0.8))
        for k in range(n):
            c = start + d * (link * 0.8 * (k + 0.5))
            w = side if k % 2 == 0 else side2
            pts = []
            for s in range(6):
                a = 2 * math.pi * s / 5
                pts.append(c + d * (math.sin(a) * link * 0.5) + w * (math.cos(a) * link * 0.28))
            self.tube(pts, [thick] * len(pts), paint, sides=3, caps=False)
        rest = start + d * (link * 0.8 * n)
        if (end - rest).length > 0.02:
            self.tube([rest, end], [rod, rod], paint, sides=5)

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = True
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(40)
        mesh.validate()
        mesh.update()
        return mesh


def flame(b, center, height=0.13, width=0.035, seg=8):
    prof = [(0.0, 0.0), (width * 0.7, height * 0.12), (width, height * 0.35), (width * 0.75, height * 0.65),
            (width * 0.3, height * 0.9), (0.0, height)]
    b.lathe(prof, "flame", seg=seg, center=center)


def candle(b, center, radius, height, drips=5, seed=0, flame_h=0.13, seg=10, light=False):
    """Свеча с потёками воска: потёки — выступы радиуса у верха, стекают вниз на разную длину. Возвращает верх фитиля."""
    rng = np.random.default_rng(seed)
    drip = [(float(rng.uniform(0, 2 * math.pi)), float(rng.uniform(0.25, 0.75)) * height, float(rng.uniform(0.25, 0.45)))
            for _ in range(drips)]

    def dr(a, z):
        out = 0.0
        for c, length, w in drip:
            da = abs((a - c + math.pi) % (2 * math.pi) - math.pi)
            if da < w and z > height - length:
                t = (z - (height - length)) / length
                out = max(out, radius * 0.18 * (1 - da / w) * min(1.0, t * 3))
        return out

    # light: облегчённая (люстра — шесть свечей): меньше рядов и граней
    rows = [(radius * 1.05, 0.0)] + ([(radius, height * 0.5)] if light else [(radius, height * k / 5) for k in range(1, 5)])
    rows += [(radius, height * 0.95), (radius * 0.85, height), (radius * 0.55, height * 0.97), (0.0, height * 0.96)]
    b.lathe(rows, "wax", seg=seg, center=center, dr=dr, cap_top=False)
    cx, cy, cz = center
    top = (cx, cy, cz + height * 0.96)
    b.tube([Vector(top), Vector((cx, cy, cz + height * 0.96 + 0.03))], [0.006, 0.004], "dark", sides=4)
    flame(b, (cx, cy, cz + height * 0.96 + 0.015), flame_h, flame_h * 0.27, seg=6 if light else 8)
    return Vector((cx, cy, cz + height * 0.96 + 0.015 + flame_h * 0.4))


# ---------------------------------------------------------------------------
# Лампы
# ---------------------------------------------------------------------------

def make_candle():
    """Кованая тарелка на трёх цепях и толстая оплывшая свеча."""
    b = Builder()
    dish = [(0.0, -0.32), (0.26, -0.32), (0.30, -0.29), (0.31, -0.26), (0.28, -0.25), (0.24, -0.27), (0.0, -0.27)]
    b.lathe(dish, "iron", seg=16)
    b.torus((0, 0, -0.27), 0.3, 0.018, "iron", seg=20, sides=5)
    light = candle(b, (0, 0, -0.27), 0.085, 0.34, drips=6, seed=3, flame_h=0.15)
    hook = Vector((0, 0, 0.42))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.3
        b.chain(Vector((0.29 * math.cos(a), 0.29 * math.sin(a), -0.26)), hook, link=0.075, thick=0.009, full=0.45)
    b.torus((0, 0, 0.45), 0.035, 0.01, "iron", seg=10, sides=4, z_axis=False)
    b.chain(Vector((0, 0, 0.48)), Vector((0, 0, CEILING)), full=0.5)
    return "Lamp_Candle", b, light, (0.0, 0.0)


def make_oil():
    """Латунная масляная лампа: резервуар, стеклянный колпак с огнём, крышка-дымник, цепь."""
    b = Builder()
    body = [(0.0, -0.42), (0.06, -0.42), (0.16, -0.38), (0.2, -0.30), (0.19, -0.22), (0.12, -0.18), (0.10, -0.15),
            (0.12, -0.13), (0.0, -0.13)]
    b.lathe(body, "brass", seg=16)
    b.lathe([(0.0, -0.44), (0.03, -0.44), (0.0, -0.5)], "brass", seg=8, cap_bottom=False)
    glass = [(0.0, -0.13), (0.09, -0.13), (0.14, -0.05), (0.15, 0.03), (0.11, 0.12), (0.06, 0.2), (0.0, 0.2)]
    b.lathe(glass, "glass", seg=14)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        path = [Vector((0.105 * math.cos(a), 0.105 * math.sin(a), -0.14)), Vector((0.165 * math.cos(a), 0.165 * math.sin(a), 0.0)),
                Vector((0.11 * math.cos(a), 0.11 * math.sin(a), 0.16))]
        b.tube(path, [0.008] * 3, "brass", sides=4)
    cap = [(0.0, 0.16), (0.16, 0.16), (0.18, 0.18), (0.1, 0.24), (0.05, 0.3), (0.05, 0.34), (0.0, 0.34)]
    b.lathe(cap, "brass", seg=14)
    b.torus((0, 0, 0.38), 0.035, 0.009, "brass", seg=10, sides=4, z_axis=False)
    b.chain(Vector((0, 0, 0.42)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Oil", b, Vector((0, 0, -0.04)), (0.75, 0.1)


def make_lantern6():
    """Шестигранный кованый фонарь: дно, стойки, островерхая крыша, кольцо, внутри свеча."""
    b = Builder()
    b.lathe([(0.0, -0.40), (0.24, -0.40), (0.25, -0.36), (0.23, -0.34), (0.0, -0.34)], "iron", seg=6)
    for k in range(6):
        a = 2 * math.pi * k / 6
        p0 = Vector((0.23 * math.cos(a), 0.23 * math.sin(a), -0.34))
        p1 = Vector((0.23 * math.cos(a), 0.23 * math.sin(a), 0.12))
        b.tube([p0, p1], [0.014, 0.014], "iron", sides=4)
    for z in (-0.33, -0.12, 0.11):
        b.torus((0, 0, z), 0.232, 0.009, "iron", seg=6, sides=4)
    roof = [(0.0, 0.11), (0.29, 0.11), (0.30, 0.14), (0.16, 0.27), (0.05, 0.36), (0.0, 0.38)]
    b.lathe(roof, "iron", seg=6)
    b.torus((0, 0, 0.44), 0.05, 0.012, "iron", seg=10, sides=4, z_axis=False)
    light = candle(b, (0, 0, -0.34), 0.055, 0.2, drips=4, seed=8, flame_h=0.11)
    b.chain(Vector((0, 0, 0.49)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Lantern6", b, light, (0.6, 0.4)


def make_skull():
    """Череп на цепи; на макушке свеча, воск стекает по черепу."""
    b = Builder()

    def skull_dr(a, z):
        # чуть сплюснут с боков, вытянут вперёд-назад; скулы
        front = math.cos(a - FRONT)
        return -0.025 * abs(math.cos(a)) + 0.012 * front + 0.008 * max(0.0, front) * (1.0 if -0.2 < z < -0.1 else 0.0)

    cranium = [(0.0, -0.30), (0.10, -0.29), (0.15, -0.24), (0.165, -0.17), (0.20, -0.08), (0.215, 0.0),
               (0.205, 0.08), (0.17, 0.15), (0.10, 0.2), (0.0, 0.215)]
    b.lathe(cranium, "skull", seg=16, dr=skull_dr)
    # нижняя челюсть: дуга спереди
    jaw = []
    for k in range(7):
        a = FRONT + math.radians(-55 + 110 * k / 6)
        jaw.append(Vector((0.13 * math.cos(a), 0.13 * math.sin(a), -0.33 + 0.02 * abs(k - 3) / 3)))
    b.tube(jaw, [0.035] * len(jaw), "bone", sides=5)
    # оплывшая свеча на макушке и воск по черепу
    light = candle(b, (0, 0.0, 0.19), 0.055, 0.17, drips=4, seed=12, flame_h=0.12)
    for k, (a, length) in enumerate(((0.4, 0.17), (2.6, 0.24), (4.2, 0.13))):
        path = []
        for s in range(5):
            t = s / 4
            z = 0.2 - length * t
            r = 0.1 + 0.105 * math.sin(min(1.0, t * 1.6) * math.pi / 2)
            path.append(Vector((r * math.cos(a), r * math.sin(a), z)))
        b.tube(path, [0.022, 0.02, 0.018, 0.015, 0.012], "wax", sides=5)
    # железный обруч и цепь
    b.torus((0, 0, 0.05), 0.215, 0.012, "iron", seg=16, sides=4)
    for k in range(2):
        a = math.pi * k
        b.chain(Vector((0.215 * math.cos(a), 0.215 * math.sin(a), 0.06)), Vector((0, 0, 0.55)), link=0.075, thick=0.009, full=0.6)
    b.chain(Vector((0, 0, 0.55)), Vector((0, 0, CEILING)), full=0.4)
    return "Lamp_Skull", b, light, (0.95, 0.9)


def make_chandelier():
    """Кованое колесо: обод, спицы, шесть свечей в чашках, три цепи к крюку."""
    b = Builder()
    ring_z = -0.3
    b.torus((0, 0, ring_z), 0.55, 0.022, "iron", seg=20, sides=4)
    b.torus((0, 0, ring_z), 0.12, 0.018, "iron", seg=10, sides=4)
    lights = []
    for k in range(6):
        a = 2 * math.pi * k / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        b.tube([d * 0.12 + Vector((0, 0, ring_z)), d * 0.55 + Vector((0, 0, ring_z))], [0.012, 0.012], "iron", sides=4)
        c = d * 0.55
        b.lathe([(0.0, -0.01), (0.045, -0.01), (0.06, 0.02), (0.0, 0.02)], "iron", seg=6,
                center=(c.x, c.y, ring_z))
        lights.append(candle(b, (c.x, c.y, ring_z + 0.03), 0.032, 0.15, drips=2, seed=20 + k, flame_h=0.09, seg=6, light=True))
    hook = Vector((0, 0, 0.4))
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 6
        b.chain(Vector((0.55 * math.cos(a), 0.55 * math.sin(a), ring_z + 0.02)), hook, link=0.09, thick=0.01, full=0.4)
    b.lathe([(0.0, 0.36), (0.05, 0.38), (0.03, 0.44), (0.0, 0.46)], "iron", seg=8)
    b.chain(Vector((0, 0, 0.46)), Vector((0, 0, CEILING)), full=0.4)
    # один свет в центре: шесть точечных источников дороги для телефона
    return "Lamp_Chandelier", b, Vector((0, 0, ring_z + 0.12)), (0.75, 0.15)


def make_orb():
    """Магический шар в железных когтях на цепи."""
    b = Builder()
    sphere = [(0.0, -0.36)] + [(0.19 * math.sin(math.pi * k / 8), -0.17 - 0.19 * math.cos(math.pi * k / 8)) for k in range(1, 8)] + [(0.0, 0.02)]
    b.lathe(sphere, "orb", seg=16)
    # верхняя чашка и когти, охватывающие шар
    b.lathe([(0.0, 0.0), (0.09, 0.0), (0.11, 0.04), (0.07, 0.09), (0.04, 0.14), (0.0, 0.15)], "iron", seg=10)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        path = []
        for s in range(6):
            t = s / 5
            ang = math.pi * (0.18 + 0.62 * t)
            r = 0.205 * math.sin(ang)
            z = -0.17 + 0.205 * math.cos(ang)
            path.append(Vector((r * math.cos(a), r * math.sin(a), z)))
        b.tube(path, [0.02, 0.018, 0.016, 0.014, 0.012, 0.008], "iron", sides=5)
    # внизу капля-подвеска
    b.lathe([(0.0, -0.36), (0.03, -0.38), (0.02, -0.43), (0.0, -0.47)], "iron", seg=6)
    b.torus((0, 0, 0.19), 0.04, 0.01, "iron", seg=10, sides=4, z_axis=False)
    b.chain(Vector((0, 0, 0.23)), Vector((0, 0, CEILING)), full=0.7)
    return "Lamp_Orb", b, Vector((0, 0, -0.17)), (0.35, 0.15)


LAMPS = [make_candle, make_oil, make_lantern6, make_skull, make_chandelier, make_orb]


# ---------------------------------------------------------------------------
# Лампы, волна 2 (02.10.2026): вместо перекрасок абажура и заглушек из примитивов — свои модели
# ---------------------------------------------------------------------------

def bulb(b, z, radius=0.085):
    """Лампочка: стеклянный шар (светится) и латунный цоколь над ним."""
    sphere = [(0.0, z - radius)] + [(radius * math.sin(math.pi * k / 6), z - radius * math.cos(math.pi * k / 6)) for k in range(1, 6)]
    sphere += [(radius * 0.45, z + radius * 0.95), (0.0, z + radius * 1.0)]
    b.lathe(sphere, "glass", seg=10)
    b.lathe([(0.0, z + radius * 0.9), (radius * 0.42, z + radius * 0.9), (radius * 0.45, z + radius * 1.7), (0.0, z + radius * 1.75)],
            "brass", seg=8)


def make_shade():
    """Эмалевый абажур-конус (снаружи тёмно-зелёный, изнутри кремовый), лампочка, шнур. Базовая лампа."""
    b = Builder()
    # Замкнутый профиль: снаружи сверху вниз до края, изнутри обратно вверх — одна оболочка с толщиной.
    shell = [(0.0, 0.27), (0.08, 0.27), (0.12, 0.21), (0.34, -0.04), (0.52, -0.19), (0.56, -0.23),
             (0.54, -0.245), (0.50, -0.215), (0.32, -0.065), (0.10, 0.17), (0.0, 0.19)]
    b.lathe(shell, "enamel", seg=22)
    bulb(b, -0.1)
    b.lathe([(0.0, 0.26), (0.05, 0.26), (0.05, 0.33), (0.0, 0.35)], "brass", seg=10)
    b.tube([Vector((0, 0, 0.34)), Vector((0, 0, CEILING))], [0.011, 0.011], "dark", sides=5)
    return "Lamp_Shade", b, Vector((0, 0, -0.14)), (0.4, 0.1)


def make_storm():
    """Штормовой фонарь: бачок, стеклянная колба (светится), проволочная защита, колпак, ручка-дужка."""
    b = Builder()
    tank = [(0.0, -0.44), (0.16, -0.44), (0.19, -0.40), (0.19, -0.33), (0.14, -0.30), (0.0, -0.30)]
    b.lathe(tank, "copper", seg=14)
    globe = [(0.0, -0.30), (0.09, -0.30), (0.15, -0.22), (0.17, -0.12), (0.15, -0.02), (0.09, 0.07), (0.0, 0.08)]
    b.lathe(globe, "glass", seg=14)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        path = [Vector((0.15 * math.cos(a), 0.15 * math.sin(a), -0.31)),
                Vector((0.2 * math.cos(a), 0.2 * math.sin(a), -0.12)),
                Vector((0.13 * math.cos(a), 0.13 * math.sin(a), 0.08))]
        b.tube(path, [0.009] * 3, "iron", sides=4)
    b.torus((0, 0, -0.12), 0.195, 0.008, "iron", seg=16, sides=4)
    cap = [(0.0, 0.06), (0.17, 0.06), (0.19, 0.09), (0.12, 0.15), (0.06, 0.2), (0.07, 0.25), (0.0, 0.26)]
    b.lathe(cap, "iron", seg=12)
    b.torus((0, 0, 0.12), 0.21, 0.012, "iron", seg=18, sides=4, z_axis=False)
    b.chain(Vector((0, 0, 0.33)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Storm", b, Vector((0, 0, -0.14)), (0.5, 0.2)


def make_brass():
    """Латунный купол на заклёпках, лампочка снизу, цепь."""
    b = Builder()
    dome = [(0.0, 0.2), (0.07, 0.2), (0.16, 0.16), (0.3, 0.05), (0.4, -0.08), (0.43, -0.15), (0.41, -0.165),
            (0.38, -0.1), (0.28, 0.02), (0.14, 0.12), (0.0, 0.15)]
    b.lathe(dome, "brass", seg=20)
    b.torus((0, 0, -0.155), 0.425, 0.016, "brass", seg=20, sides=5)
    for k in range(12):
        a = 2 * math.pi * k / 12
        c = (0.36 * math.cos(a), 0.36 * math.sin(a), -0.06)
        b.lathe([(0.0, -0.012), (0.018, -0.006), (0.0, 0.012)], "dark", seg=5, center=c)
    bulb(b, -0.14, 0.08)
    b.lathe([(0.0, 0.19), (0.04, 0.19), (0.04, 0.27), (0.0, 0.29)], "brass", seg=8)
    b.chain(Vector((0, 0, 0.29)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Brass", b, Vector((0, 0, -0.2)), (0.7, 0.1)


def make_crystal():
    """Латунное кольцо с хрустальными подвесками (светятся), пять свечей, три цепи."""
    b = Builder()
    ring_z = -0.12
    b.torus((0, 0, ring_z), 0.42, 0.018, "brass", seg=20, sides=4)
    b.torus((0, 0, ring_z + 0.1), 0.2, 0.014, "brass", seg=14, sides=4)
    b.lathe([(0.0, ring_z - 0.06), (0.05, ring_z - 0.03), (0.04, ring_z + 0.2), (0.0, ring_z + 0.24)], "brass", seg=8)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        c = Vector((0.42 * math.cos(a), 0.42 * math.sin(a), ring_z))
        b.tube([Vector((0, 0, ring_z + 0.1)), c], [0.01, 0.01], "brass", sides=4)
        b.lathe([(0.0, -0.01), (0.04, -0.01), (0.05, 0.02), (0.0, 0.02)], "brass", seg=6, center=(c.x, c.y, ring_z))
        candle(b, (c.x, c.y, ring_z + 0.02), 0.026, 0.12, drips=2, seed=40 + k, flame_h=0.08, seg=6, light=True)

    def drop(center, size):
        x, y, z = center
        b.tube([Vector((x, y, z)), Vector((x, y, z - size * 0.6))], [0.004, 0.004], "brass", sides=3, caps=False)
        b.lathe([(0.0, -size * 0.6), (size * 0.32, -size * 1.0), (0.0, -size * 1.9)], "crystal", seg=4, center=(x, y, z))

    for k in range(10):
        a = 2 * math.pi * (k + 0.5) / 10
        drop((0.42 * math.cos(a), 0.42 * math.sin(a), ring_z - 0.015), 0.07 + 0.02 * (k % 2))
    for k in range(5):
        a = 2 * math.pi * k / 5
        drop((0.2 * math.cos(a), 0.2 * math.sin(a), ring_z + 0.085), 0.06)
    hook = Vector((0, 0, 0.42))
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 5
        b.chain(Vector((0.42 * math.cos(a), 0.42 * math.sin(a), ring_z + 0.02)), hook, link=0.08, thick=0.008, full=0.3)
    b.chain(Vector((0, 0, 0.44)), Vector((0, 0, CEILING)), full=0.3)
    return "Lamp_Crystal", b, Vector((0, 0, ring_z + 0.05)), (0.3, 0.1)


def make_paper():
    """Красный бумажный фонарь (светится изнутри), чёрные крышки, кисть снизу, шнур."""
    b = Builder()
    body = [(0.0, -0.33)] + [(0.27 * math.sin(math.pi * k / 10), -0.07 - 0.26 * math.cos(math.pi * k / 10)) for k in range(1, 10)]
    body += [(0.0, 0.19)]
    b.lathe(body, "paper", seg=18)
    b.lathe([(0.0, -0.36), (0.1, -0.36), (0.12, -0.32), (0.08, -0.3), (0.0, -0.3)], "dark", seg=12)
    b.lathe([(0.0, 0.16), (0.08, 0.16), (0.12, 0.18), (0.1, 0.22), (0.0, 0.22)], "dark", seg=12)
    b.tube([Vector((0, 0, -0.36)), Vector((0, 0, -0.45))], [0.008, 0.008], "fabric", sides=4)
    b.lathe([(0.0, -0.44), (0.025, -0.46), (0.042, -0.56), (0.0, -0.58)], "fabric", seg=8)
    b.tube([Vector((0, 0, 0.22)), Vector((0, 0, CEILING))], [0.009, 0.009], "dark", sides=4)
    return "Lamp_Paper", b, Vector((0, 0, -0.08)), (0.2, 0.1)


def make_emerald():
    """Восьмигранный кованый фонарь с изумрудными стёклами (светятся): тёмное железо против яркого стекла."""
    b = Builder()
    b.lathe([(0.0, -0.40), (0.26, -0.40), (0.27, -0.36), (0.24, -0.33), (0.0, -0.33)], "iron", seg=8)
    b.lathe([(0.0, -0.33), (0.2, -0.33), (0.2, 0.08), (0.0, 0.08)], "emerald", seg=8)
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        b.tube([Vector((0.215 * math.cos(a), 0.215 * math.sin(a), -0.34)), Vector((0.215 * math.cos(a), 0.215 * math.sin(a), 0.09))],
               [0.016, 0.016], "iron", sides=4)
    for z in (-0.33, 0.08):
        b.torus((0, 0, z), 0.22, 0.014, "iron", seg=8, sides=4)
    roof = [(0.0, 0.08), (0.3, 0.08), (0.31, 0.11), (0.14, 0.25), (0.04, 0.33), (0.0, 0.35)]
    b.lathe(roof, "iron", seg=8)
    b.lathe([(0.0, 0.34), (0.035, 0.36), (0.02, 0.42), (0.0, 0.44)], "brass", seg=6)
    b.torus((0, 0, 0.48), 0.04, 0.011, "iron", seg=10, sides=4, z_axis=False)
    b.chain(Vector((0, 0, 0.52)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Emerald", b, Vector((0, 0, -0.12)), (0.7, 0.2)


def make_fringe():
    """Тканевый абажур-барабан с золотой тесьмой и бахромой, лампочка внутри."""
    b = Builder()
    shell = [(0.0, 0.06), (0.30, 0.06), (0.42, -0.24), (0.40, -0.245), (0.285, 0.045), (0.0, 0.045)]
    b.lathe(shell, "fabric", seg=24)
    b.torus((0, 0, 0.06), 0.3, 0.014, "brass", seg=20, sides=4)
    b.torus((0, 0, -0.245), 0.42, 0.016, "brass", seg=24, sides=4)
    for k in range(28):
        a = 2 * math.pi * k / 28
        top = Vector((0.42 * math.cos(a), 0.42 * math.sin(a), -0.255))
        b.tube([top, top + Vector((0, 0, -0.07 - 0.02 * (k % 2)))], [0.008, 0.005], "brass", sides=3)
    bulb(b, -0.13, 0.075)
    b.tube([Vector((0, 0, 0.05)), Vector((0, 0, CEILING))], [0.01, 0.01], "dark", sides=5)
    for k in range(3):
        a = 2 * math.pi * k / 3
        b.tube([Vector((0, 0, -0.02)), Vector((0.3 * math.cos(a), 0.3 * math.sin(a), 0.05))], [0.006, 0.006], "brass", sides=3)
    return "Lamp_Fringe", b, Vector((0, 0, -0.15)), (0.3, 0.1)


def make_ship():
    """Корабельный фонарь: медное основание и колпак, стекло (светится) за рёбрами-защитой, ручка."""
    b = Builder()
    b.lathe([(0.0, -0.42), (0.17, -0.42), (0.2, -0.38), (0.19, -0.3), (0.16, -0.28), (0.0, -0.28)], "copper", seg=14)
    b.lathe([(0.0, -0.28), (0.15, -0.28), (0.155, -0.1), (0.15, 0.06), (0.0, 0.06)], "glass", seg=14)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.tube([Vector((0.165 * math.cos(a), 0.165 * math.sin(a), -0.29)), Vector((0.165 * math.cos(a), 0.165 * math.sin(a), 0.07))],
               [0.012, 0.012], "copper", sides=4)
    for z in (-0.18, -0.04):
        b.torus((0, 0, z), 0.17, 0.011, "copper", seg=14, sides=4)
    top = [(0.0, 0.06), (0.19, 0.06), (0.2, 0.09), (0.13, 0.15), (0.08, 0.2), (0.1, 0.22), (0.1, 0.27), (0.0, 0.29)]
    b.lathe(top, "copper", seg=14)
    b.torus((0, 0, 0.36), 0.1, 0.015, "brass", seg=14, sides=5, z_axis=False)
    b.chain(Vector((0, 0, 0.46)), Vector((0, 0, CEILING)), full=0.5)
    return "Lamp_Ship", b, Vector((0, 0, -0.11)), (0.8, 0.2)


def make_fireflies():
    """Кованая клетка-купол, внутри порхают светлячки (светятся)."""
    b = Builder()
    b.lathe([(0.0, -0.40), (0.26, -0.40), (0.28, -0.37), (0.25, -0.35), (0.0, -0.35)], "iron", seg=14)
    b.torus((0, 0, -0.35), 0.25, 0.012, "iron", seg=16, sides=4)
    b.torus((0, 0, -0.08), 0.25, 0.01, "iron", seg=16, sides=4)
    for k in range(10):
        a = 2 * math.pi * k / 10
        path = [Vector((0.25 * math.cos(a), 0.25 * math.sin(a), -0.35)), Vector((0.25 * math.cos(a), 0.25 * math.sin(a), -0.02))]
        for s in range(1, 5):
            t = s / 4
            ang = t * math.pi / 2
            path.append(Vector((0.25 * math.cos(ang) * math.cos(a), 0.25 * math.cos(ang) * math.sin(a), -0.02 + 0.25 * math.sin(ang))))
        b.tube(path, [0.008] * len(path), "iron", sides=3, caps=False)
    b.torus((0, 0, 0.27), 0.04, 0.011, "iron", seg=10, sides=4, z_axis=False)
    rng = np.random.default_rng(77)
    for k in range(11):
        r = float(rng.uniform(0.03, 0.18))
        a = float(rng.uniform(0, 2 * math.pi))
        z = float(rng.uniform(-0.3, 0.08))
        s = float(rng.uniform(0.016, 0.026))
        b.lathe([(0.0, -s), (s, 0.0), (0.0, s)], "flame", seg=6, center=(r * math.cos(a), r * math.sin(a), z))
    b.chain(Vector((0, 0, 0.31)), Vector((0, 0, CEILING)), full=0.6)
    return "Lamp_Fireflies", b, Vector((0, 0, -0.12)), (0.2, 0.3)


LAMPS_V2 = [make_shade, make_storm, make_brass, make_crystal, make_paper, make_emerald, make_fringe, make_ship, make_fireflies]

# Как светит каждая лампа (для генератора; в Unity — Light у LightPoint): цвет, яркость, дальность.
LIGHT_SETTINGS = {
    "Lamp_Candle": ((1.0, 0.68, 0.38), 3.2, 8.0),
    "Lamp_Oil": ((1.0, 0.78, 0.5), 3.4, 9.0),
    "Lamp_Lantern6": ((1.0, 0.66, 0.36), 3.4, 8.0),
    "Lamp_Skull": ((1.0, 0.6, 0.32), 3.0, 8.0),
    "Lamp_Chandelier": ((1.0, 0.72, 0.42), 3.8, 10.0),
    "Lamp_Orb": ((0.6, 0.65, 1.0), 3.2, 8.0),
    "Lamp_Shade": ((1.0, 0.86, 0.66), 3.4, 9.0),
    "Lamp_Storm": ((1.0, 0.72, 0.42), 3.2, 8.0),
    "Lamp_Brass": ((1.0, 0.8, 0.55), 3.6, 9.0),
    "Lamp_Crystal": ((0.85, 0.92, 1.0), 3.6, 9.0),
    "Lamp_Paper": ((1.0, 0.5, 0.32), 3.0, 8.0),
    "Lamp_Emerald": ((0.55, 1.0, 0.62), 3.2, 8.0),
    "Lamp_Fringe": ((1.0, 0.7, 0.5), 3.2, 8.0),
    "Lamp_Ship": ((1.0, 0.74, 0.44), 3.4, 9.0),
    "Lamp_Fireflies": ((0.85, 1.0, 0.5), 2.8, 7.0),
}


# ---------------------------------------------------------------------------
# Текстура: атлас ячеек
# ---------------------------------------------------------------------------

def paint_cells(seed, tint):
    """Атлас 1024: ячейки материалов (procedural), потом упрощение под комикс. tint — оттенок железа/воска лампы."""
    rng = np.random.default_rng(seed)
    s = TEX // GRID
    img = np.zeros((TEX, TEX, 3), dtype=np.float32) + 0.5
    u = (np.arange(s)[None, :] + 0.5) / s * np.ones((s, 1))
    v = (np.arange(s)[:, None] + 0.5) / s * np.ones((1, s))
    lerp, col = zc.lerp, zc.col

    def put(name, cell):
        c, r = CELLS[name]
        img[r * s:(r + 1) * s, c * s:(c + 1) * s] = cell

    n1 = zm.periodic_noise(rng, s, s, 20.0)
    n2 = zm.periodic_noise(rng, s, s, 3.0)
    brushed = zm.periodic_noise(rng, s, s, 1.0, scale_v=30.0)

    iron = lerp(col(0.24, 0.24, 0.27) * tint[0], col(0.14, 0.14, 0.16), np.clip(0.4 + 0.2 * n1 + 0.1 * n2, 0, 1))
    rust = zm.smoothstep(1.3, 2.3, n1 + 0.6 * n2)
    iron = lerp(iron, col(0.45, 0.24, 0.12), rust * 0.6)
    iron = lerp(iron, col(0.42, 0.42, 0.46), zm.smoothstep(0.85, 1.0, np.abs(np.sin(u * np.pi * 6))) * 0.25)
    put("iron", iron)
    put("chain", lerp(iron * 1.1, col(0.5, 0.5, 0.54), zm.smoothstep(0.3, 0.9, v) * 0.3))
    put("dark", np.zeros((s, s, 3), dtype=np.float32) + col(0.08, 0.07, 0.06))

    brass = lerp(col(0.88, 0.66, 0.30), col(0.55, 0.38, 0.14), np.clip(0.3 + 0.12 * brushed + 0.15 * n1, 0, 1))
    brass = lerp(brass, col(1.0, 0.86, 0.55), zm.smoothstep(0.75, 0.95, np.abs(np.sin(v * np.pi * 3))) * 0.4)
    put("brass", brass)
    put("copper", lerp(col(0.8, 0.45, 0.26), col(0.3, 0.6, 0.5), zm.smoothstep(1.4, 2.4, n1) * 0.7))

    wax = lerp(col(0.97, 0.92, 0.78) * tint[1], col(0.85, 0.74, 0.55) * tint[1], np.clip(0.3 + 0.2 * n2 + 0.2 * (1 - v), 0, 1))
    put("wax", wax)

    bone = lerp(col(0.92, 0.87, 0.74), col(0.68, 0.58, 0.40), np.clip(0.3 + 0.2 * n1 + 0.1 * n2 + 0.25 * (1 - v), 0, 1))
    cracks = np.zeros((s, s), dtype=np.float32)
    zc.draw_cracks(cracks, rng, [(rng.uniform(0, s), rng.uniform(0, s), rng.uniform(0, 6.28)) for _ in range(3)], length=60, width=1.0)
    bone = lerp(bone, col(0.25, 0.18, 0.1), cracks * 0.8)
    put("bone", bone)

    # Череп: u — угол вокруг (0.25 — лицо), v — высота по профилю (0 — низ, 1 — макушка).
    skull = bone.copy()
    fu = FRONT / (2 * np.pi)
    dark = col(0.06, 0.05, 0.05)
    for du in (-0.055, 0.055):
        eye = ((u - fu - du) / 0.045) ** 2 + ((v - 0.5) / 0.09) ** 2
        skull = lerp(skull, dark, 1 - zm.smoothstep(0.8, 1.0, eye))
    nose = (np.abs(u - fu) < 0.022 * (1 - (v - 0.32) / 0.1)) & (v > 0.32) & (v < 0.42)
    skull = lerp(skull, dark, nose.astype(np.float32))
    teeth_band = (np.abs(u - fu) < 0.07) & (v > 0.1) & (v < 0.2)
    gaps = zm.smoothstep(0.85, 0.95, np.abs(np.sin((u - fu) * np.pi * 50)))
    skull = lerp(skull, dark, (teeth_band * (0.25 + 0.75 * gaps)).astype(np.float32) * 0.8)
    put("skull", skull)

    # Светящиеся: пламя (снизу голубое, потом белое, жёлтое, оранжевое), стекло (янтарь), шар (вихри).
    flame_c = np.where((v < 0.15)[..., None], lerp(col(0.5, 0.7, 1.0), col(1.0, 0.95, 0.8), v / 0.15),
                       lerp(col(1.0, 0.92, 0.55), col(1.0, 0.55, 0.15), np.clip((v - 0.15) / 0.85, 0, 1)))
    put("flame", flame_c)
    put("glass", lerp(col(1.0, 0.85, 0.55), col(0.95, 0.6, 0.25), np.clip(0.5 + 0.3 * n2, 0, 1)))
    swirl = np.sin((u * 6 + v * 3 + 0.4 * n1) * np.pi)
    put("orb", lerp(col(0.35, 0.3, 0.95), col(0.75, 0.6, 1.0), zm.smoothstep(-0.3, 0.9, swirl)))

    # Эмаль абажура: v по профилю (0 — макушка снаружи, ~0.5 — край, дальше — изнутри вверх).
    # Снаружи тёмно-зелёная эмаль с бликом, у края — тёмная кромка, изнутри — светлая кремовая.
    outside = lerp(col(0.10, 0.32, 0.22), col(0.22, 0.55, 0.38), zm.smoothstep(0.1, 0.45, v))
    outside = lerp(outside, col(0.55, 0.8, 0.62), zm.smoothstep(0.92, 1.0, np.abs(np.sin(u * np.pi * 2))) * 0.35)
    inside = lerp(col(0.98, 0.95, 0.86), col(0.85, 0.8, 0.68), np.clip((v - 0.52) * 1.5, 0, 1))
    enamel = np.where((v < 0.5)[..., None], outside, inside)
    enamel = lerp(enamel, col(0.06, 0.08, 0.07), (np.abs(v - 0.5) < 0.025).astype(np.float32))
    put("enamel", enamel)

    # Ткань абажура: бордовая, складки полосами, золотая тесьма по краям.
    pleats = 0.5 + 0.5 * np.sin(u * np.pi * 24)
    fabric = lerp(col(0.42, 0.08, 0.12), col(0.62, 0.14, 0.18), pleats * 0.7)
    trim = ((v < 0.1) | (v > 0.9)).astype(np.float32)
    fabric = lerp(fabric, col(0.95, 0.72, 0.28), trim)
    fabric = lerp(fabric, col(0.35, 0.22, 0.06), ((np.abs(v - 0.1) < 0.012) | (np.abs(v - 0.9) < 0.012)).astype(np.float32))
    put("fabric", fabric)

    # Бумажный фонарь (светится): красный, тёмные рёбра каркаса, золотая полоса с узором посередине.
    paper = lerp(col(1.0, 0.42, 0.22), col(0.9, 0.22, 0.12), np.clip(np.abs(v - 0.5) * 2, 0, 1))
    ribs = zm.smoothstep(0.9, 0.97, np.abs(np.sin(v * np.pi * 9)))
    paper = lerp(paper, col(0.35, 0.06, 0.03), ribs * 0.8)
    band = (np.abs(v - 0.5) < 0.09).astype(np.float32)
    paper = lerp(paper, col(1.0, 0.82, 0.35), band)
    waves = (np.abs(np.sin(u * np.pi * 16) * 0.05 - (v - 0.5)) < 0.012) & (band > 0)
    paper = lerp(paper, col(0.75, 0.2, 0.08), waves.astype(np.float32))
    put("paper", paper)

    # Изумрудное стекло (светится): яркая середина, тёмные свинцовые переплёты ромбами — читается на любом фоне.
    glow_c = lerp(col(0.55, 1.0, 0.65), col(0.08, 0.55, 0.28), np.clip(np.abs(v - 0.5) * 1.6 + np.abs(np.sin(u * np.pi * 4)) * 0.3, 0, 1))
    lead = (np.abs(np.sin((u * 4 + v * 3) * np.pi)) < 0.08) | (np.abs(np.sin((u * 4 - v * 3) * np.pi)) < 0.08)
    put("emerald", lerp(glow_c, col(0.04, 0.12, 0.07), lead.astype(np.float32)))

    # Хрусталь (светится): холодный бело-голубой с гранями-бликами.
    facets = np.abs(np.sin(u * np.pi * 8)) * np.abs(np.cos(v * np.pi * 5))
    put("crystal", lerp(col(0.62, 0.82, 1.0), col(1.0, 1.0, 1.0), zm.smoothstep(0.3, 0.9, facets)))

    return np.clip(zc.comic_flatten(img), 0, 1)


# ---------------------------------------------------------------------------
# Сборка, проверка, экспорт, превью
# ---------------------------------------------------------------------------

def build(make):
    name, b, light, tint = make()
    mesh = b.finish(name)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    img = paint_cells(sum(ord(ch) for ch in name), (1.0 + tint[0] * 0.1, 1.0 - tint[1] * 0.05))
    image = zm.save_png(name, img, os.path.join(TEXTURES_DIR, name + ".png"))

    main = zm.make_material(name, image)
    bsdf = main.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = 0.5
    bsdf.inputs["Roughness"].default_value = 0.55
    glow = zm.make_material(name + "_Glow", image)
    gb = glow.node_tree.nodes.get("Principled BSDF")
    tex = [n for n in glow.node_tree.nodes if n.type == "TEX_IMAGE"][0]
    glow.node_tree.links.new(tex.outputs["Color"], gb.inputs["Emission"])
    gb.inputs["Emission Strength"].default_value = 3.0
    mesh.materials.append(main)
    mesh.materials.append(glow)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    marker = bpy.data.objects.new("LightPoint", None)
    marker.location = light
    marker.parent = obj
    bpy.context.scene.collection.objects.link(marker)
    return obj


def check(obj):
    mesh = obj.data
    tris = zm.triangle_count(mesh)
    body = [v.co for v in mesh.vertices if v.co.z < 0.6]
    width = 2 * max(math.hypot(c.x, c.y) for c in body)
    zmin = min(c.z for c in body)
    zmax_body = max(c.z for c in body)
    glow = sum(1 for p in mesh.polygons if p.material_index == 1)
    ok = tris <= MAX_TRIS and glow > 0 and zmin > -0.6
    print("  %-16s треуг. %5d  ширина %.2f  низ %.2f  верх корпуса %.2f  светящихся граней %d  %s" % (
        obj.name, tris, width, zmin, zmax_body, glow, "OK" if ok else "ПРОВЕРИТЬ"))
    return ok


def render(out_dir, objs):
    """Превью: лампы в ряд над полом, ракурс как из-за стола (чуть снизу) и сверху."""
    scene = bpy.context.scene
    spacing = 1.5
    for k, obj in enumerate(objs):
        obj.location = ((k - (len(objs) - 1) / 2) * spacing, 0, 1.4)
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    fo = bpy.data.objects.new("Floor", floor)
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.2, 0.26, 0.2, 1)
    floor.materials.append(fm)
    scene.collection.objects.link(fo)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.37, 0.42, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    scene.world = world
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 2.5
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(55), math.radians(10), math.radians(150))   # со стороны камеры
    scene.collection.objects.link(sun)
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_gtao = True
    scene.eevee.use_bloom = True
    scene.render.resolution_x, scene.render.resolution_y = 2400, 700
    scene.view_settings.view_transform = "Standard"
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    width = len(objs) * spacing
    for name, elev, dist_k in (("lamps_side.png", 6, 1.35), ("lamps_top.png", 35, 1.4)):
        e = math.radians(elev)
        dist = width * dist_k
        cam.location = (0, dist * math.cos(e), 1.4 + dist * math.sin(e))   # со стороны +Y — как камера игры (Unity −Z)
        target = Vector((0, 0, 1.4))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
    zm.reset_scene()
    os.makedirs(MODELS_DIR, exist_ok=True)
    objs, all_ok = [], True
    print("Лампы:")
    for make in LAMPS + LAMPS_V2:
        if only is not None and make.__name__ not in only and ("Lamp_" + make.__name__[5:].capitalize()) not in only:
            continue
        obj = build(make)
        all_ok &= check(obj)
        zm.export_fbx(obj, os.path.join(MODELS_DIR, obj.name + ".fbx"), with_children=True)
        for child in obj.children:
            child.name = child.name + "_" + obj.name
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not all_ok:
        raise SystemExit("Есть лампы с ошибками")


if __name__ == "__main__":
    main()
