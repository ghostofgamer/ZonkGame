# -*- coding: utf-8 -*-
"""
Столы для игры «Зонк»: четырнадцать разных, с одной посадкой — заменяют Table_Oak без правки сцены.

    Table_Tavern    трактирный: скруглённый, доски с гвоздями и следами кружек, четыре крепкие ножки с проножками
    Table_Gambling  игорный восьмиугольник: тёмное дерево, кайма с мастями, кожаный бортик, толстые точёные ножки
    Table_Barrel    доски на двух бочках: ровные концы, поперечины-брусья, обручи
    Table_Stone     каменная плита на двух каменных стойках с основанием: сколы, трещины, руны, мох
    Table_Deck      палубный: доски на грузовых ящиках (рамка из досок, железные стяжки), канат с узлами по краю
    Table_Royal     королевский: красное дерево, золотая кайма и розетки, фартук с фестонами, гнутые ножки
    Table_Stump     пень: толстый спил бревна с годичными кольцами на узком пеньке с корнями
    Table_Pirate    капитанский: морская карта на столешнице, роза ветров, штурвал спереди, латунные уголки
    Table_Dark      тёмный: тяжёлый дуб в морилке, кованые уголки, обвязка ножек, массивные проножки
    Table_Marble    мраморный: плита с прожилками и золотым кантом на четырёх колоннах, подиум
    Table_Walnut    ореховый: резные балясины, фартук с полукружиями, светлая инкрустация по краю
    Table_Birch     берёзовый: светлые доски на «лыжах», разведённые ножки-брёвнышки в бересте
    Table_Gold      золотой: чёрный лак с золотым узором, позолоченные балясины, медальоны на фартуке
    Table_Alchemy   алхимический: тёмное дерево в пятнах реактивов, нижняя полка с колбами и книгами

Посадка (из генератора ZonkSetup.Placeholders/Scenes, единицы — метры Unity):
    верх столешницы y = 0 (точка опоры префаба в центре верха), пол y = -1.6;
    Table_Oak — плита 5.6 × 4 (X × Z), толщина 0.2, ножки в (±2.5, ±1.7);
    лоток Felt 3.2 × 2 (рамка до 3.4 × 2.2, высота 0.18) в центре; стаканы в (±2.15, 0, ∓1.25);
    отложенные кости z = ±1.45; рука игрока у края z = -2.3 на высоте 0.3; соперник за столом z = +2.9.
Поэтому у всех столов: плоский верх на 0 в зоне |X| ≤ 2.55, |Z| ≤ 1.65 (проверяется лучами), край не дальше
|X| ≤ 3.0, |Z| ≤ 2.15, бортики не выше 0.12, низ до пола -1.6.

Оси как в zonk_models.py: Unity = (-bx, bz, -by). В Blender: X — длина стола, Y — ширина (камера игрока со
стороны +Y), Z — вверх.

Текстура — атлас 1024: верхняя половина — столешница (по координатам мира), нижняя — 8 плиток 256 по видам
материала (кромка, ножки, металл, золото и т.д.), детали проецируются в свою плитку.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_tables.py
    blender --background --factory-startup --python Tools/Blender/zonk_tables.py -- --render <папка>
Результат: Assets/ZonkContent/Art/Models/Tables/<Имя>.fbx и Art/Textures/Tables/<Имя>.png.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_cups as zc  # noqa: E402  comic_flatten, draw_cracks, lerp, col

# ---------------------------------------------------------------------------
# Параметры посадки
# ---------------------------------------------------------------------------

PROJECT_ROOT = zm.PROJECT_ROOT
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Tables")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Tables")

TOP = 0.0                 # верх столешницы
FLOOR = -1.6              # пол
HX, HY = 2.8, 2.0         # полуразмеры столешницы Table_Oak (Blender X, Y)
PLAY_X, PLAY_Y = 2.55, 1.65   # зона, которая обязана быть плоской (лоток, стаканы, отложенные кости)
MAX_X, MAX_Y = 3.0, 2.15  # край стола не дальше
MAX_RIM = 0.12            # бортики не выше
UPPER_Z = -0.6            # габарит MAX_X/MAX_Y проверяется у этой высоты и выше (низ может быть шире: корни, лапы)
PLAY_DIAG = 3.95          # стакан в (2.15, 1.25) радиусом 0.38 по диагонали: |x| + |y| до 3.95 должно быть плоским
MAX_TRIS = 5000
TEX = 1024

# Атлас: столешница — верхняя половина по миру
TOP_U = (0.0, 1.0)
TOP_V = (0.5, 1.0)
TOP_X = (-3.0, 3.0)
TOP_Y = (-2.5, 2.5)
TILE = 0.25               # плитки нижней половины: 4 × 2

# Плитки (номера)
EDGE, LEG, METAL, GOLD, ACC1, ACC2, DARK, ACC3 = range(8)


def col(*c):
    return np.array(c, dtype=np.float32)


lerp = zc.lerp
ss = zm.smoothstep


# ---------------------------------------------------------------------------
# Геометрия
# ---------------------------------------------------------------------------

class Builder:
    """Один меш стола: части добавляются в bmesh, каждой сразу задаётся UV в своей плитке атласа."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()

    def _new_faces(self, before):
        return [f for f in self.bm.faces if f not in before]

    def part(self, make, tile, top=False, bottom_tile=None, normals=True):
        """make(bm) строит часть; UV: верх (нормаль вверх у z = TOP) — в столешницу, остальное — в плитку."""
        before = set(self.bm.faces)
        make(self.bm)
        faces = self._new_faces(before)
        if normals:
            bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self.assign(faces, tile, top, bottom_tile)
        return faces

    def assign(self, faces, tile, top=False, bottom_tile=None):
        if not faces:
            return
        verts = {v for f in faces for v in f.verts}
        lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
        hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
        size = hi - lo
        for f in faces:
            n = f.normal
            if top and n.z > 0.9 and abs(f.calc_center_median().z - TOP) < 0.02:
                for loop in f.loops:
                    p = loop.vert.co
                    loop[self.uv].uv = top_uv(p.x, p.y)
                continue
            t = bottom_tile if (bottom_tile is not None and n.z < -0.9) else tile
            u0, v0 = (t % 4) * TILE, (t // 4) * TILE
            ax = abs(n.x), abs(n.y), abs(n.z)
            if ax[2] >= ax[0] and ax[2] >= ax[1]:
                a, b = 0, 1
            elif ax[0] >= ax[1]:
                a, b = 1, 2
            else:
                a, b = 0, 2
            for loop in f.loops:
                p = loop.vert.co
                su = (p[a] - lo[a]) / max(size[a], 1e-4)
                sv = (p[b] - lo[b]) / max(size[b], 1e-4)
                loop[self.uv].uv = (u0 + (0.02 + 0.96 * su) * TILE, v0 + (0.02 + 0.96 * sv) * TILE)

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = True
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(35.0)
        mesh.validate()
        mesh.update()
        return mesh


def top_uv(x, y):
    u = TOP_U[0] + (x - TOP_X[0]) / (TOP_X[1] - TOP_X[0]) * (TOP_U[1] - TOP_U[0])
    v = TOP_V[0] + (y - TOP_Y[0]) / (TOP_Y[1] - TOP_Y[0]) * (TOP_V[1] - TOP_V[0])
    return (u, v)


# --- контуры (против часовой стрелки) ---

def rounded_rect(hx, hy, r, per_corner=5):
    pts = []
    for cx, cy, a0 in ((hx - r, hy - r, 0.0), (-hx + r, hy - r, 90.0), (-hx + r, -hy + r, 180.0), (hx - r, -hy + r, 270.0)):
        for k in range(per_corner + 1):
            a = math.radians(a0 + 90.0 * k / per_corner)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def chamfer_rect(hx, hy, c):
    return [(hx, -hy + c), (hx, hy - c), (hx - c, hy), (-hx + c, hy), (-hx, hy - c), (-hx, -hy + c), (-hx + c, -hy), (hx - c, -hy)]


def superellipse(a, b, p, n, wobble=None):
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n
        c, s = math.cos(t), math.sin(t)
        x = a * math.copysign(abs(c) ** (2.0 / p), c)
        y = b * math.copysign(abs(s) ** (2.0 / p), s)
        if wobble:
            w = wobble(t)
            x *= w
            y *= w
        pts.append((x, y))
    return pts


def offset(outline, d):
    """Сдвиг контура внутрь на d (по биссектрисам)."""
    n = len(outline)
    out = []
    for i in range(n):
        x0, y0 = outline[i - 1]
        x1, y1 = outline[i]
        x2, y2 = outline[(i + 1) % n]
        e0 = Vector((x1 - x0, y1 - y0)).normalized()
        e1 = Vector((x2 - x1, y2 - y1)).normalized()
        n0 = Vector((-e0.y, e0.x))
        n1 = Vector((-e1.y, e1.x))
        bis = (n0 + n1)
        if bis.length < 1e-6:
            bis = n0
        bis.normalize()
        k = d / max(0.3, bis.dot(n0))
        out.append((x1 + bis.x * k, y1 + bis.y * k))
    return out


def ring(bm, outline, z):
    return [bm.verts.new((x, y, z)) for x, y in outline]


def bridge(bm, r0, r1):
    n = len(r0)
    for i in range(n):
        bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i]))


def slab(outline, z0, z1, bevel=0.03, top_inset=None):
    """Плита по контуру: низ z0, верх z1 с фаской bevel (верхний контур сдвинут внутрь)."""
    def make(bm):
        b = ring(bm, outline, z0)
        inset = top_inset if top_inset is not None else bevel
        bm.faces.new(list(reversed(b)))
        if bevel > 0.0:
            m = ring(bm, outline, z1 - bevel)
            bridge(bm, b, m)
            b = m
        t = ring(bm, offset(outline, inset) if inset > 0.0 else outline, z1)
        bridge(bm, b, t)
        bm.faces.new(t)
    return make


def frame(outline, inner_d, z0, z1):
    """Кольцо-бортик: между контуром и контуром, сдвинутым внутрь на inner_d."""
    def make(bm):
        inner = offset(outline, inner_d)
        ob, ot = ring(bm, outline, z0), ring(bm, outline, z1)
        ib, it = ring(bm, inner, z0), ring(bm, inner, z1)
        bridge(bm, ob, ot)
        bridge(bm, it, ib)
        bridge(bm, ot, it)
        bridge(bm, ib, ob)
    return make


def box(center, size, rot_z=0.0):
    def make(bm):
        res = bmesh.ops.create_cube(bm, size=1.0)
        m = Matrix.Translation(center) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    return make


def lathe(profile, center=(0.0, 0.0), segs=12, scale=(1.0, 1.0)):
    """Тело вращения: профиль [(r, z)] снизу вверх, торцы закрыты."""
    def make(bm):
        rings = []
        for r, z in profile:
            rings.append([bm.verts.new((center[0] + r * scale[0] * math.cos(2 * math.pi * k / segs),
                                         center[1] + r * scale[1] * math.sin(2 * math.pi * k / segs), z))
                          for k in range(segs)])
        for r0, r1 in zip(rings, rings[1:]):
            bridge(bm, r0, r1)
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return make


def tube(path, radii, sides=6):
    def make(bm):
        zc.add_tube(bm, [Vector(p) for p in path], radii, sides)
    return make


def dome(pos, normal, radius, height, segs=6):
    def make(bm):
        zc.add_dome(bm, Vector(pos), Vector(normal), radius, height, segs)
    return make


def blob(center, size, seed, subdiv=2, zmin=FLOOR, zmax=None):
    """Валун: икосфера, сплющенная и с шумом."""
    def make(bm):
        res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        rng = np.random.default_rng(seed)
        phases = rng.uniform(0, 6.28, 6)
        for v in res["verts"]:
            d = v.co.normalized()
            k = 1.0 + 0.12 * math.sin(3 * d.x + phases[0]) * math.sin(2 * d.y + phases[1]) + 0.08 * math.sin(5 * d.z + phases[2])
            z = d.z * size[2] * k + center[2]
            z = max(zmin, z) if zmax is None else min(zmax, max(zmin, z))
            v.co = Vector((d.x * size[0] * k + center[0], d.y * size[1] * k + center[1], z))
    return make


def torus(center, radius, minor, segs=24, sides=6, axis="Y"):
    """Тор в вертикальной плоскости (ось — Y по умолчанию: колесо смотрит на камеру)."""
    def make(bm):
        rings = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            c = Vector((math.cos(a), 0.0, math.sin(a)))
            rows = []
            for j in range(sides):
                b = 2 * math.pi * j / sides
                p = c * (radius + minor * math.cos(b)) + Vector((0.0, minor * math.sin(b), 0.0))
                rows.append(bm.verts.new(Vector(center) + p))
            rings.append(rows)
        for i in range(segs):
            r0, r1 = rings[i], rings[(i + 1) % segs]
            for j in range(sides):
                bm.faces.new((r0[j], r1[j], r1[(j + 1) % sides], r0[(j + 1) % sides]))
    return make


def turned_leg(x, y, top_z, profile_r=0.13, segs=10):
    """Точёная ножка от низа столешницы до пола: шары, шейки, раструб."""
    h = top_z - FLOOR
    prof = [(0.75, 0.0), (0.85, 0.04), (0.6, 0.12), (0.45, 0.2), (0.9, 0.32), (0.55, 0.42), (0.5, 0.62),
            (0.95, 0.72), (0.6, 0.82), (0.9, 0.92), (1.0, 1.0)]
    return lathe([(r * profile_r, FLOOR + z * h) for r, z in prof], (x, y), segs)


def tapered_post(x, y, z0, z1, w0, w1, d0=None, d1=None):
    """Брус от z0 (низ, сечение w0 × d0) до z1 (верх, w1 × d1), грани по осям X/Y; торцы закрыты."""
    d0 = w0 if d0 is None else d0
    d1 = w1 if d1 is None else d1

    def make(bm):
        lo = [bm.verts.new((x + sx * w0 / 2, y + sy * d0 / 2, z0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        hi = [bm.verts.new((x + sx * w1 / 2, y + sy * d1 / 2, z1)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bridge(bm, lo, hi)
        bm.faces.new(list(reversed(lo)))
        bm.faces.new(hi)
    return make


def rail(p0, p1, w, h):
    """Горизонтальный брус между двумя точками (проножка, царга): ширина w, высота h."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    return box((p0 + p1) / 2, Vector((d.length, w, h)), math.atan2(d.y, d.x))


def column(x, y, z0, z1, r, segs=12, flutes=True):
    """Колонна: база, ствол с каннелюрами (чередование радиуса по кругу), капитель."""
    def make(bm):
        h = z1 - z0
        prof = [(1.45, 0.0), (1.45, 0.05), (1.2, 0.08), (1.12, 0.11), (1.0, 0.14),
                (0.92, 0.86), (1.05, 0.89), (1.25, 0.93), (1.45, 0.95), (1.45, 1.0)]
        rings = []
        for k, (pr, pz) in enumerate(prof):
            ring_verts = []
            for s in range(segs):
                a = 2 * math.pi * s / segs
                rr = pr * r
                if flutes and 3 <= k <= 5 and s % 2 == 1:
                    rr *= 0.9
                ring_verts.append(bm.verts.new((x + rr * math.cos(a), y + rr * math.sin(a), z0 + pz * h)))
            rings.append(ring_verts)
        for r0, r1 in zip(rings, rings[1:]):
            bridge(bm, r0, r1)
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return make


# ---------------------------------------------------------------------------
# Рисование атласа
# ---------------------------------------------------------------------------

class Atlas:
    def __init__(self, seed):
        self.img = np.zeros((TEX, TEX, 3), dtype=np.float32)
        self.rng = np.random.default_rng(seed)
        h = TEX // 2
        ys = (np.arange(h) + 0.5) / h
        xs = (np.arange(TEX) + 0.5) / TEX
        self.X = TOP_X[0] + xs[None, :] * (TOP_X[1] - TOP_X[0]) * np.ones((h, 1))
        self.Y = TOP_Y[0] + ys[:, None] * (TOP_Y[1] - TOP_Y[0]) * np.ones((1, TEX))

    def set_top(self, rgb):
        self.img[TEX // 2:, :] = rgb

    def tile_coords(self):
        n = TEX // 4
        u = (np.arange(n)[None, :] + 0.5) / n * np.ones((n, 1))
        v = (np.arange(n)[:, None] + 0.5) / n * np.ones((1, n))
        return u, v

    def set_tile(self, t, rgb):
        n = TEX // 4
        r0, c0 = (t // 4) * n, (t % 4) * n
        self.img[r0:r0 + n, c0:c0 + n] = rgb

    def noise(self, scale, scale_v=None, h=None, w=None):
        return zm.periodic_noise(self.rng, h or TEX // 2, w or TEX, scale, scale_v=scale_v)


def wood_tile(atlas, light, dark, vertical, stripes=7, knots=True):
    u, v = atlas.tile_coords()
    n = TEX // 4
    warp = atlas.noise(30.0, h=n, w=n)
    a = u if vertical else v
    grain = 0.5 + 0.5 * np.sin(2 * np.pi * (a * stripes + 0.08 * warp))
    img = lerp(light, dark, np.clip(0.25 + 0.45 * grain ** 3, 0, 1))
    if knots:
        for _ in range(2):
            cx, cy = atlas.rng.uniform(0.2, 0.8, 2)
            d = np.sqrt(((u - cx) * (1.0 if vertical else 3.0)) ** 2 + ((v - cy) * (3.0 if vertical else 1.0)) ** 2)
            img = lerp(img, dark * 0.7, (1 - ss(0.02, 0.05, d)) * 0.9)
    return img


def flat_tile(atlas, c, spread=0.08, scale=20.0):
    n = TEX // 4
    nz = atlas.noise(scale, h=n, w=n)
    return np.clip(c[None, None, :] * (1.0 + spread * nz)[..., None], 0, 1)


def metal_tile(atlas, c, highlight=0.25):
    u, v = atlas.tile_coords()
    img = flat_tile(atlas, c, 0.06, 30.0)
    return lerp(img, np.minimum(c * 1.6, 1.0), ss(0.6, 1.0, v) * highlight)


def planks_top(atlas, light, dark, seam, count, along_x=True, offset_seed=0, nails=True, length=1.6):
    """Доски по длине стола: свой оттенок, волокна, тёмные щели, торцевые стыки, гвозди."""
    X, Y = atlas.X, atlas.Y
    across = Y if along_x else X
    along = X if along_x else Y
    width = 2 * (HY if along_x else HX) / count
    idx = np.floor((across + 10 * width) / width).astype(int)
    pos = ((across + 10 * width) / width) % 1.0
    rng = np.random.default_rng(31 + offset_seed)
    tints = rng.uniform(-0.12, 0.12, 64)
    shifts = rng.uniform(0, length, 64)
    tint = tints[idx % 64]
    warp = atlas.noise(40.0, 200.0)
    grain = 0.5 + 0.5 * np.sin(2 * np.pi * (across / width * 4.0 + 0.25 * warp + 0.02 * along))
    img = lerp(light, dark, np.clip(0.3 + 0.35 * grain ** 3 + tint, 0, 1))
    img = lerp(img, seam, (1 - ss(0.0, 0.035, np.minimum(pos, 1 - pos))) * 0.95)
    joint = ((along + shifts[idx % 64]) % length) / length
    jd = np.minimum(joint, 1 - joint) * length
    img = lerp(img, seam, (1 - ss(0.0, 0.012, jd)) * 0.9)
    if nails:
        for side in (0.2, 0.8):
            nd = np.sqrt(((pos - side) * width) ** 2 + (jd - 0.06) ** 2)
            img = lerp(img, col(0.15, 0.13, 0.12), (1 - ss(0.012, 0.02, nd)) * 0.9)
    return img


def dist_to_rect(X, Y, hx, hy):
    return np.minimum(hx - np.abs(X), hy - np.abs(Y))


def zc_star(x, y, outer, inner, n=5):
    ang = np.arctan2(x, y)
    rad = np.sqrt(x * x + y * y)
    sector = 2 * np.pi / n
    local = np.mod(ang, sector) - sector / 2
    t = np.abs(local) / (sector / 2)
    edge = outer + (inner - outer) * t ** 0.9
    return np.clip((edge - rad) * 200.0, 0, 1)


def suit(X, Y, kind, size):
    """Масти карт простыми фигурами: 0 пики, 1 червы, 2 бубны, 3 трефы. 1 внутри."""
    x, y = X / size, Y / size
    if kind == 2:
        return (np.abs(x) + np.abs(y) * 0.75 < 0.75).astype(np.float32)
    heart = ((np.hypot(x + 0.28, y - 0.18) < 0.33) | (np.hypot(x - 0.28, y - 0.18) < 0.33) |
             ((y < 0.25) & (y > -0.75) & (np.abs(x) < (y + 0.75) * 0.6)))
    if kind == 1:
        return heart.astype(np.float32)
    if kind == 0:
        spade = ((np.hypot(x + 0.28, -y - 0.05) < 0.33) | (np.hypot(x - 0.28, -y - 0.05) < 0.33) |
                 ((-y < 0.12) & (-y > -0.85) & (np.abs(x) < (-y + 0.85) * 0.6)))
        stem = (np.abs(x) < 0.08) & (y < -0.1) & (y > -0.75)
        base = (y < -0.62) & (y > -0.78) & (np.abs(x) < 0.25)
        return (spade | stem | base).astype(np.float32)
    club = ((np.hypot(x, y - 0.35) < 0.3) | (np.hypot(x + 0.32, y - 0.0) < 0.3) | (np.hypot(x - 0.32, y - 0.0) < 0.3))
    stem = (np.abs(x) < 0.08) & (y < 0.0) & (y > -0.7)
    base = (y < -0.6) & (y > -0.76) & (np.abs(x) < 0.25)
    return (club | stem | base).astype(np.float32)


# ---------------------------------------------------------------------------
# 1. Трактирный
# ---------------------------------------------------------------------------

class Table:
    def __init__(self, name):
        self.name = name
        self.metallic = 0.0
        self.smoothness = 0.35


def make_tavern():
    t = Table("Table_Tavern")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.45, 4)
    b.part(slab(out, -0.2, TOP, 0.04), EDGE, top=True, bottom_tile=DARK)
    # царга: доски под столешницей по периметру
    lx, ly = HX - 0.5, HY - 0.42
    for p0, p1 in (((-lx, -ly), (lx, -ly)), ((-lx, ly), (lx, ly)), ((-lx, -ly), (-lx, ly)), ((lx, -ly), (lx, ly))):
        b.part(rail((p0[0], p0[1], -0.34), (p1[0], p1[1], -0.34), 0.12, 0.28), EDGE)
    # четыре крепкие ножки, чуть сужаются книзу
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.part(tapered_post(sx * lx, sy * ly, FLOOR, -0.2, 0.26, 0.34), LEG, bottom_tile=DARK)
    # проножки: по длинным сторонам и поперечина посередине — на них ставят ноги
    for sy in (-1, 1):
        b.part(rail((-lx, sy * ly, -1.25), (lx, sy * ly, -1.25), 0.14, 0.16), LEG)
    b.part(rail((0.0, -ly, -1.25), (0.0, ly, -1.25), 0.14, 0.16), LEG)
    for sx in (-1, 1):
        b.part(rail((sx * lx, -ly, -1.25), (sx * lx, ly, -1.25), 0.14, 0.16), LEG)
    mesh = b.finish()

    def paint(a):
        light, dark = col(0.66, 0.44, 0.24), col(0.40, 0.24, 0.12)
        img = planks_top(a, light, dark, col(0.16, 0.09, 0.05), 6, True, 1, True, 1.9)
        # следы кружек: кольца
        rng = np.random.default_rng(3)
        for _ in range(5):
            cx = rng.uniform(-2.5, 2.5) * (1 if rng.random() < 0.5 else 1)
            cy = rng.choice([-1.85, 1.75]) if rng.random() < 0.7 else rng.uniform(-1.8, 1.8)
            r = rng.uniform(0.16, 0.22)
            d = np.abs(np.hypot(a.X - cx, a.Y - cy) - r)
            img = lerp(img, dark * 0.75, (1 - ss(0.006, 0.02, d)) * 0.6)
        # кромка темнее (затёртая)
        img = lerp(img, dark * 0.8, (1 - ss(0.0, 0.12, dist_to_rect(a.X, a.Y, HX, HY))) * 0.5)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light * 0.95, dark, False, 3))
        a.set_tile(LEG, wood_tile(a, light * 0.9, dark, True, 5))
        a.set_tile(DARK, flat_tile(a, dark * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.3
    return t


# ---------------------------------------------------------------------------
# 2. Игорный восьмиугольник
# ---------------------------------------------------------------------------

def make_gambling():
    t = Table("Table_Gambling")
    b = Builder(t.name)
    out = chamfer_rect(HX, HY, 0.5)
    rim_w = 0.2
    b.part(slab(out, -0.22, TOP, 0.02), EDGE, top=True, bottom_tile=DARK)
    # кожаный бортик по краю (низкий, мягкий) с пуговицами
    b.part(frame(out, rim_w, TOP, TOP + 0.09), ACC1)
    inner = offset(out, rim_w * 0.5)
    for i, (x0, y0) in enumerate(inner):
        x1, y1 = inner[(i + 1) % len(inner)]
        steps = max(1, int(math.hypot(x1 - x0, y1 - y0) / 0.55))
        for k in range(steps):
            q = (k + 0.5) / steps
            b.part(dome((x0 + (x1 - x0) * q, y0 + (y1 - y0) * q, TOP + 0.09), (0, 0, 1), 0.035, 0.02, 5), GOLD)
    # фартук и ножки: толстые точёные, со стопками-проножками крест-накрест
    b.part(slab(offset(out, 0.25), -0.52, -0.22, 0.0, 0.0), EDGE)
    for x in (-2.1, 2.1):
        for y in (-1.4, 1.4):
            b.part(turned_leg(x, y, -0.52, 0.22, 12), LEG)
    for sy in (-1, 1):
        b.part(rail((-2.1, sy * 1.4, -1.18), (2.1, sy * 1.4, -1.18), 0.1, 0.1), LEG)
    b.part(rail((-2.1, 0.0, -1.18), (2.1, 0.0, -1.18), 0.12, 0.12), LEG)
    for sx in (-1, 1):
        b.part(rail((sx * 2.1, -1.4, -1.18), (sx * 2.1, 1.4, -1.18), 0.1, 0.1), LEG)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        dark_wood, mid = col(0.24, 0.10, 0.06), col(0.42, 0.20, 0.10)
        warp = a.noise(60.0, 300.0)
        grain = 0.5 + 0.5 * np.sin(2 * np.pi * (Y * 6.0 + 0.3 * warp))
        img = lerp(mid, dark_wood, np.clip(0.4 + 0.4 * grain ** 2, 0, 1))
        # светлая кайма с мастями вокруг игровой зоны
        d = np.minimum(np.minimum(HX - np.abs(X), HY - np.abs(Y)), (HX + HY - 0.5 - np.abs(X) - np.abs(Y)) / 1.414)
        band = ss(0.20, 0.23, d) * (1 - ss(0.50, 0.53, d))
        inlay = col(0.86, 0.72, 0.46)
        img = lerp(img, inlay, band)
        img = lerp(img, col(0.75, 0.6, 0.3), (1 - ss(0.0, 0.012, np.abs(d - 0.6))) * 0.9)
        kinds = [(0, -2.15, -1.7), (1, 2.15, -1.7), (2, -2.15, 1.7), (3, 2.15, 1.7), (1, 0.0, -1.67), (0, 0.0, 1.67)]
        for kind, x, y in kinds:
            m = suit(X - x, Y - y, kind, 0.13)
            color = col(0.62, 0.06, 0.08) if kind in (1, 2) else col(0.08, 0.06, 0.06)
            img = lerp(img, color, m)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, mid, dark_wood, False, 2, False))
        a.set_tile(LEG, wood_tile(a, mid, dark_wood, True, 3, False))
        u, v = a.tile_coords()
        leather = flat_tile(a, col(0.45, 0.10, 0.08), 0.06, 10.0)
        leather = lerp(leather, col(0.25, 0.05, 0.04), (1 - ss(0.0, 0.06, np.minimum(v, 1 - v))) * 0.8)
        a.set_tile(ACC1, leather)
        a.set_tile(GOLD, metal_tile(a, col(1.0, 0.78, 0.3)))
        a.set_tile(DARK, flat_tile(a, dark_wood * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.55
    return t


# ---------------------------------------------------------------------------
# 3. Доски на бочке
# ---------------------------------------------------------------------------

def make_barrel():
    t = Table("Table_Barrel")
    b = Builder(t.name)
    count = 6
    width = 2 * HY / count
    rng = np.random.default_rng(12)
    for k in range(count):
        y0 = -HY + k * width
        y1 = -HY + (k + 1) * width
        # концы досок почти ровные: разница в пару сантиметров, без «рваного» края
        ext0 = HX + rng.uniform(-0.04, 0.04)
        ext1 = HX + rng.uniform(-0.04, 0.04)
        outline = [(-ext0, y0), (ext1, y0), (ext1, y1), (-ext0, y1)]
        b.part(slab(outline, -0.15, TOP, 0.0), EDGE, top=True, bottom_tile=DARK)
    # две поперечины-бруса под досками, лежат на бочках
    for x in (-1.75, 1.75):
        b.part(box(Vector((x, 0, -0.23)), Vector((0.3, 2 * HY - 0.06, 0.16))), LEG)
    # две бочки стоймя: стол стоит на них
    top_z = -0.31
    for x in (-1.75, 1.75):
        prof = []
        for z in np.linspace(FLOOR, top_z, 7):
            tt = (z - FLOOR) / (top_z - FLOOR)
            prof.append((0.72 + 0.12 * math.sin(math.pi * tt), float(z)))
        b.part(lathe(prof, (x, 0.0), 20), ACC1, bottom_tile=DARK)
        for zz in (FLOOR + 0.18, -1.05, top_z - 0.18):
            r = 0.72 + 0.12 * math.sin(math.pi * (zz - FLOOR) / (top_z - FLOOR)) + 0.02
            b.part(lathe([(r, zz - 0.055), (r, zz + 0.055)], (x, 0.0), 20), METAL)
    mesh = b.finish()

    def paint(a):
        light, dark = col(0.72, 0.52, 0.30), col(0.44, 0.28, 0.14)
        img = planks_top(a, light, dark, col(0.12, 0.07, 0.04), 6, True, 7, True, 9.0)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light, dark, False, 3))
        a.set_tile(LEG, wood_tile(a, light * 0.85, dark, False, 3))
        u, v = a.tile_coords()
        barrel = wood_tile(a, col(0.62, 0.40, 0.20), col(0.36, 0.21, 0.10), True, 9, False)
        barrel = lerp(barrel, col(0.18, 0.1, 0.05), np.clip(1 - ss(0.0, 0.04, np.minimum((u * 16) % 1.0, 1 - (u * 16) % 1.0)), 0, 1) * 0.9)
        a.set_tile(ACC1, barrel)
        a.set_tile(METAL, metal_tile(a, col(0.30, 0.29, 0.30), 0.15))
        a.set_tile(DARK, flat_tile(a, dark * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.25
    return t


# ---------------------------------------------------------------------------
# 4. Каменная плита на валунах
# ---------------------------------------------------------------------------

def make_stone():
    t = Table("Table_Stone")
    b = Builder(t.name)
    rng = np.random.default_rng(21)
    base = rounded_rect(HX + 0.05, HY + 0.05, 0.35, 4)
    chipped = []
    for x, y in base:
        k = 1.0 + rng.uniform(-0.025, 0.012)
        chipped.append((x * k, y * k))
    b.part(slab(chipped, -0.3, TOP, 0.06), EDGE, top=True, bottom_tile=DARK)
    # две каменные стойки во всю ширину, чуть сужаются кверху, с плитой-основанием: видны из меню
    for x in (-1.85, 1.85):
        b.part(tapered_post(x, 0.0, FLOOR + 0.18, -0.3, 0.9, 0.72, 3.5, 3.2), ACC3, bottom_tile=DARK)
        b.part(tapered_post(x, 0.0, FLOOR, FLOOR + 0.18, 1.15, 1.08, 3.75, 3.65), ACC3, bottom_tile=DARK)
    # пара камней у подножия — для живости
    b.part(blob((-2.75, 1.3, FLOOR + 0.12), (0.26, 0.2, 0.16), 9, 1), ACC3)
    b.part(blob((2.6, -1.45, FLOOR + 0.1), (0.2, 0.16, 0.13), 4, 1), ACC3)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        # яркость в середине ступени комикс-обработки (6 ступеней): иначе слабый шум даёт пятна
        stone, stone_d = col(0.62, 0.61, 0.58), col(0.50, 0.49, 0.47)
        n1 = a.noise(50.0)
        n2 = a.noise(8.0)
        img = lerp(stone, stone_d, np.clip(0.3 + 0.04 * n1 + 0.015 * n2, 0, 1))
        # кайма рун у края
        d = dist_to_rect(X, Y, HX, HY)
        img = lerp(img, stone_d * 0.9, (ss(0.08, 0.1, d) * (1 - ss(0.32, 0.34, d))) * 0.35)
        for line_d in (0.09, 0.33):
            img = lerp(img, col(0.15, 0.15, 0.16), (1 - ss(0.004, 0.012, np.abs(d - line_d))) * 0.9)
        runes = np.zeros(X.shape, dtype=np.float32)
        r2 = np.random.default_rng(5)
        h = TEX // 2
        perim_pts = []
        for k in range(30):
            q = k / 30.0
            L = 2 * (2 * HX - 0.4) + 2 * (2 * HY - 0.4)
            s = q * L
            ex, ey = HX - 0.21, HY - 0.21
            if s < 2 * ex:
                perim_pts.append((-ex + s, -ey))
            elif s < 2 * ex + 2 * ey:
                perim_pts.append((ex, -ey + (s - 2 * ex)))
            elif s < 4 * ex + 2 * ey:
                perim_pts.append((ex - (s - 2 * ex - 2 * ey), ey))
            else:
                perim_pts.append((-ex, ey - (s - 4 * ex - 2 * ey)))
        for (cx, cy) in perim_pts:
            for _ in range(3):
                p0 = (cx + r2.uniform(-0.07, 0.07), cy + r2.uniform(-0.07, 0.07))
                p1 = (cx + r2.uniform(-0.07, 0.07), cy + r2.uniform(-0.07, 0.07))
                vx, vy = p1[0] - p0[0], p1[1] - p0[1]
                ln = max(math.hypot(vx, vy), 1e-4)
                tt = np.clip(((X - p0[0]) * vx + (Y - p0[1]) * vy) / (ln * ln), 0, 1)
                dist = np.hypot(X - p0[0] - tt * vx, Y - p0[1] - tt * vy)
                runes = np.maximum(runes, 1 - ss(0.006, 0.014, dist))
        img = lerp(img, col(0.13, 0.13, 0.14), runes * 0.9)
        # трещины на столешнице
        cracks = np.zeros((h, TEX), dtype=np.float32)
        zc.draw_cracks(cracks, r2, [(r2.uniform(0, TEX), r2.uniform(0, h), r2.uniform(0, 6.28)) for _ in range(4)],
                       length=220, width=1.4)
        img = lerp(img, col(0.1, 0.1, 0.1), cracks * 0.85)
        a.set_top(img)
        n = TEX // 4
        a.set_tile(EDGE, flat_tile(a, stone * 0.9, 0.05, 40.0))
        side = flat_tile(a, stone * 0.92, 0.06, 40.0)
        u, v = a.tile_coords()
        moss = zm.periodic_noise(a.rng, n, n, 18.0)
        side = lerp(side, col(0.30, 0.42, 0.16), np.clip(ss(0.4, 1.0, moss + 0.3 - v * 1.8), 0, 1) * 0.85)
        tcr = np.zeros((n, n), dtype=np.float32)
        zc.draw_cracks(tcr, r2, [(r2.uniform(0, n), r2.uniform(0, n), r2.uniform(0, 6.28)) for _ in range(3)], length=90, width=1.2)
        side = lerp(side, col(0.12, 0.12, 0.12), tcr * 0.8)
        a.set_tile(ACC3, side)
        a.set_tile(DARK, flat_tile(a, stone_d * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.1
    return t


# ---------------------------------------------------------------------------
# 5. Палубный на ящиках
# ---------------------------------------------------------------------------

def make_deck():
    t = Table("Table_Deck")
    b = Builder(t.name)
    out = [(-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)]
    b.part(slab(out, -0.2, TOP, 0.02), EDGE, top=True, bottom_tile=DARK)
    # канат по краю: ровно по боковой грани (скруглённые углы), чуть ниже верха
    rope_out = rounded_rect(HX + 0.055, HY + 0.055, 0.12, 3)
    dense = [(x, y, -0.1) for x, y in rope_out]
    dense.append(dense[0])
    b.part(tube(dense, [0.055] * len(dense), 6), ACC1)
    # на углах — узлы и свисающие концы каната
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (HX + 0.05), sy * (HY + 0.05)
            b.part(blob((cx, cy, -0.12), (0.08, 0.08, 0.08), 3 + sx + 2 * sy, 1, zmin=-0.3, zmax=0.0), ACC1)
            b.part(tube([(cx, cy, -0.14), (cx + sx * 0.03, cy + sy * 0.03, -0.38), (cx + sx * 0.02, cy + sy * 0.05, -0.55)],
                        [0.04, 0.035, 0.03], 5), ACC1)
    # железные уголки с заклёпками
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (HX - 0.18), sy * (HY - 0.18)
            b.part(box(Vector((cx, cy, TOP + 0.004)), Vector((0.36, 0.36, 0.012))), METAL)
            for dx, dy in ((0.1, 0.1), (-0.1, 0.1), (0.1, -0.1)):
                b.part(dome((cx + dx * sx, cy + dy * sy, TOP + 0.01), (0, 0, 1), 0.025, 0.015, 5), METAL)
    # два грузовых ящика вместо ножек: рамка из досок по рёбрам и железные стяжки
    ch = -0.2 - FLOOR
    cz = (FLOOR - 0.2) / 2
    for x in (-1.75, 1.75):
        b.part(box(Vector((x, 0, cz)), Vector((1.6, 3.2, ch))), ACC2)
        for y in (-1.62, 1.62):
            for z in (FLOOR + 0.07, -0.27):
                b.part(box(Vector((x, y, z)), Vector((1.66, 0.06, 0.14))), LEG)
            for dx in (-0.76, 0.76):
                b.part(box(Vector((x + dx, y, cz)), Vector((0.14, 0.06, ch))), LEG)
        for dy in (-0.9, 0.9):
            b.part(box(Vector((x, dy, cz)), Vector((1.64, 0.08, ch))), METAL)
    mesh = b.finish()

    def paint(a):
        light, dark = col(0.70, 0.55, 0.36), col(0.45, 0.32, 0.18)
        img = planks_top(a, light, dark, col(0.07, 0.06, 0.05), 9, True, 4, True, 2.3)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light * 0.9, dark, False, 3))
        a.set_tile(LEG, wood_tile(a, col(0.46, 0.33, 0.19), col(0.30, 0.20, 0.11), False, 3, False))
        u, v = a.tile_coords()
        rope = flat_tile(a, col(0.78, 0.64, 0.40), 0.05)
        stripes = 0.5 + 0.5 * np.sin(2 * np.pi * (u * 10 + v * 6))
        rope = lerp(rope, col(0.48, 0.36, 0.20), ss(0.55, 0.9, stripes))
        a.set_tile(ACC1, rope)
        crate = wood_tile(a, col(0.62, 0.48, 0.30), col(0.40, 0.28, 0.16), False, 4)
        boards = (v * 5) % 1.0
        crate = lerp(crate, col(0.16, 0.10, 0.06), (1 - ss(0.0, 0.04, np.minimum(boards, 1 - boards))) * 0.9)
        brace = np.minimum(np.abs(u - v), np.abs(u - (1 - v)))
        crate = lerp(crate, col(0.50, 0.36, 0.20), (1 - ss(0.04, 0.06, brace)))
        crate = lerp(crate, col(0.16, 0.10, 0.06), (1 - ss(0.0, 0.01, np.abs(brace - 0.05))) * 0.8)
        a.set_tile(ACC2, crate)
        a.set_tile(METAL, metal_tile(a, col(0.28, 0.27, 0.28), 0.2))
        a.set_tile(DARK, flat_tile(a, dark * 0.5))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.25
    return t


# ---------------------------------------------------------------------------
# 6. Королевский
# ---------------------------------------------------------------------------

def make_royal():
    t = Table("Table_Royal")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.45, 4)
    b.part(slab(out, -0.12, TOP, 0.025), EDGE, top=True, bottom_tile=DARK)
    # золотая кайма-обвязка
    b.part(frame(offset(out, -0.03), 0.06, -0.2, -0.1), GOLD)
    # фартук с фестонами (по контуру, нижний край волной)
    apron = offset(out, 0.12)
    def apron_make(bm):
        dense = []
        n = len(apron)
        for i in range(n):
            x0, y0 = apron[i]
            x1, y1 = apron[(i + 1) % n]
            steps = max(1, int(math.hypot(x1 - x0, y1 - y0) / 0.25))
            for k in range(steps):
                q = k / steps
                dense.append((x0 + (x1 - x0) * q, y0 + (y1 - y0) * q))
        inner = offset(dense, 0.04)
        wave = [-0.42 + 0.08 * abs(math.sin(i * math.pi / 3)) for i in range(len(dense))]
        ot = ring(bm, dense, -0.2)
        ob = [bm.verts.new((x, y, wave[i])) for i, (x, y) in enumerate(dense)]
        ib = [bm.verts.new((x, y, wave[i])) for i, (x, y) in enumerate(inner)]
        it = ring(bm, inner, -0.2)
        bridge(bm, ob, ot)
        bridge(bm, it, ib)
        bridge(bm, ot, it)
        bridge(bm, ib, ob)
    b.part(apron_make, EDGE)
    # гнутые ножки с золотыми лапами
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (HX - 0.55), sy * (HY - 0.45)
            out_dir = Vector((sx, sy, 0)).normalized()
            p = [Vector((x, y, -0.2)), Vector((x, y, -0.5)) + out_dir * 0.12, Vector((x, y, -0.9)) - out_dir * 0.05,
                 Vector((x, y, -1.3)) - out_dir * 0.08, Vector((x, y, -1.5)) + out_dir * 0.1]
            b.part(tube(p, [0.2, 0.17, 0.12, 0.1, 0.09], 10), LEG)
            b.part(blob((p[-1].x + out_dir.x * 0.04, p[-1].y + out_dir.y * 0.04, FLOOR + 0.09), (0.14, 0.14, 0.09), 4 + sx + 2 * sy, 1), GOLD)
            b.part(blob((x + out_dir.x * 0.1, y + out_dir.y * 0.1, -0.45), (0.12, 0.12, 0.12), 8 + sx, 1), GOLD)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        mahogany, mahog_d = col(0.55, 0.16, 0.10), col(0.30, 0.07, 0.05)
        warp = a.noise(80.0, 300.0)
        grain = 0.5 + 0.5 * np.sin(2 * np.pi * (Y * 5.0 + 0.4 * warp))
        img = lerp(mahogany, mahog_d, np.clip(0.3 + 0.45 * grain ** 2, 0, 1))
        gold = col(1.0, 0.78, 0.28)
        d = dist_to_rect(X, Y, HX, HY)
        for line_d, w in ((0.06, 0.012), (0.12, 0.006), (0.28, 0.008)):
            img = lerp(img, gold, 1 - ss(w * 0.5, w, np.abs(d - line_d)))
        # розетки по углам и центрам сторон
        for cx, cy in ((-2.45, -1.68), (2.45, -1.68), (-2.45, 1.68), (2.45, 1.68), (0.0, -1.78), (0.0, 1.78)):
            rr = np.hypot(X - cx, Y - cy)
            ang = np.arctan2(Y - cy, X - cx)
            petal = 0.15 * (0.7 + 0.3 * np.cos(8 * ang))
            img = lerp(img, gold, 1 - ss(petal - 0.006, petal, rr))
            img = lerp(img, mahog_d, 1 - ss(0.035, 0.042, rr))
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, mahogany, mahog_d, False, 2, False))
        a.set_tile(LEG, wood_tile(a, mahogany, mahog_d, True, 3, False))
        a.set_tile(GOLD, metal_tile(a, gold, 0.3))
        a.set_tile(DARK, flat_tile(a, mahog_d * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness, t.metallic = 0.6, 0.0
    return t


# ---------------------------------------------------------------------------
# 7. Пень
# ---------------------------------------------------------------------------

STUMP_A, STUMP_B, STUMP_P = 2.9, 2.08, 4.0


def stump_wobble(t):
    return 1.0 + 0.018 * math.sin(5 * t + 0.4) + 0.012 * math.sin(11 * t + 1.3)


def make_stump():
    t = Table("Table_Stump")
    b = Builder(t.name)
    n = 40
    top = superellipse(STUMP_A, STUMP_B, STUMP_P, n, stump_wobble)

    # толстый спил бревна сверху (кольца — столешница, кора — бок) ...
    slice_bottom = -0.3

    def log_slice(bm):
        rows = [ring(bm, [(x * k, y * k) for x, y in top], z)
                for z, k in ((slice_bottom, 0.97), (slice_bottom + 0.05, 1.0), (TOP - 0.03, 1.0), (TOP, 0.985))]
        for r0, r1 in zip(rows, rows[1:]):
            bridge(bm, r0, r1)
        bm.faces.new(rows[-1])
        bm.faces.new(list(reversed(rows[0])))
    b.part(log_slice, ACC1, top=True, bottom_tile=DARK)

    # ... на пне заметно уже стола: не глыба, а стол на пеньке
    trunk = superellipse(1.55, 1.05, 2.6, 28, stump_wobble)

    def stump(bm):
        rows = []
        for z, k in ((slice_bottom, 0.92), (-0.7, 0.9), (-1.15, 0.95), (-1.45, 1.06), (FLOOR, 1.16)):
            rows.append(ring(bm, [(x * k, y * k) for x, y in trunk], z))
        for r0, r1 in zip(rows, rows[1:]):
            bridge(bm, r1, r0)
        bm.faces.new(list(reversed(rows[-1])))
    b.part(stump, ACC1, bottom_tile=DARK)
    # корни у пенька
    rng = np.random.default_rng(8)
    for k in range(5):
        tt = 2 * math.pi * k / 5 + 0.3 + rng.uniform(-0.2, 0.2)
        d = Vector((1.55 * math.cos(tt), 1.05 * math.sin(tt), 0.0))
        start = Vector((d.x * 0.95, d.y * 0.95, -1.25))
        d.normalize()
        b.part(tube([start, start + d * 0.3 + Vector((0, 0, -0.2)), start + d * 0.6 + Vector((0, 0, -0.32))],
                    [0.2, 0.13, 0.05], 6), ACC1)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        # годичные кольца по «расстоянию» в форме сверхэллипса
        q = (np.abs(X / STUMP_A) ** STUMP_P + np.abs(Y / STUMP_B) ** STUMP_P) ** (1.0 / STUMP_P)
        warp = a.noise(60.0)
        rings = 0.5 + 0.5 * np.sin(2 * np.pi * (q * 22 + 0.15 * warp))
        light, dark = col(0.86, 0.68, 0.44), col(0.62, 0.43, 0.24)
        img = lerp(light, dark, np.clip(0.25 + 0.5 * rings ** 4, 0, 1))
        img = lerp(img, col(0.48, 0.30, 0.14), (1 - ss(0.0, 0.04, q - 0.02)) * 0.7)
        # трещины от сердцевины
        ang = np.arctan2(Y, X)
        for c in (0.4, 2.1, 3.9, 5.2):
            da = np.abs((ang - c + np.pi) % (2 * np.pi) - np.pi)
            crack = (1 - ss(0.0, 0.012 + 0.01 * q, da * q * 3)) * ss(0.05, 0.12, q) * (1 - ss(0.45, 0.6, q))
            img = lerp(img, col(0.2, 0.12, 0.06), crack * 0.95)
        # кора по краю сверху
        img = lerp(img, col(0.32, 0.20, 0.11), ss(0.94, 0.97, q))
        a.set_top(img)
        u, v = a.tile_coords()
        nn = TEX // 4
        warp = zm.periodic_noise(a.rng, nn, nn, 20.0, scale_v=60.0)
        ridges = np.abs(np.sin(np.pi * (u * 9.0 + 0.1 * warp)))
        bark = lerp(col(0.44, 0.30, 0.17), col(0.30, 0.19, 0.10), np.clip(0.3 + 0.15 * warp, 0, 1))
        bark = lerp(bark, col(0.12, 0.07, 0.04), 1 - ss(0.0, 0.18, ridges))
        a.set_tile(ACC1, bark)
        a.set_tile(DARK, flat_tile(a, col(0.25, 0.15, 0.08)))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.15
    return t


# ---------------------------------------------------------------------------
# 8. Капитанский с картой и штурвалом
# ---------------------------------------------------------------------------

def make_pirate():
    t = Table("Table_Pirate")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.25, 3)
    b.part(slab(out, -0.2, TOP, 0.025), EDGE, top=True, bottom_tile=DARK)
    # латунные уголки
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.part(box(Vector((sx * (HX - 0.14), sy * (HY - 0.14), TOP + 0.003)), Vector((0.28, 0.28, 0.008))), GOLD)
    # царга и толстые ножки
    b.part(slab(offset(out, 0.2), -0.45, -0.2, 0.0, 0.0), EDGE)
    for x in (-2.35, 2.35):
        for y in (-1.55, 1.55):
            b.part(lathe([(0.2, FLOOR), (0.24, FLOOR + 0.05), (0.15, FLOOR + 0.2), (0.17, -0.9), (0.22, -0.8),
                          (0.16, -0.65), (0.2, -0.45)], (x, y), 10), LEG)
    # штурвал спереди (со стороны игрока, +Y): между ножками под столешницей
    wheel_c = (0.0, HY - 0.15, -0.95)
    b.part(torus(wheel_c, 0.42, 0.045, 24, 6), LEG)
    def hub(bm):
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.11, radius2=0.11, depth=0.12)
        m = Matrix.Translation(Vector(wheel_c)) @ Matrix.Rotation(math.pi / 2, 4, "X")
        bmesh.ops.transform(bm, matrix=m, verts=res["verts"])
    b.part(hub, GOLD)
    for k in range(8):
        a = 2 * math.pi * k / 8
        d = Vector((math.cos(a), 0.0, math.sin(a)))
        c = Vector(wheel_c)
        b.part(tube([c + d * 0.08, c + d * 0.42, c + d * 0.62], [0.03, 0.03, 0.045], 5), LEG)
    # ось штурвала к царге
    b.part(box(Vector((0.0, HY - 0.32, -0.7)), Vector((0.12, 0.12, 0.5))), LEG)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        wood, wood_d = col(0.50, 0.30, 0.16), col(0.30, 0.17, 0.08)
        img = planks_top(a, wood, wood_d, col(0.14, 0.08, 0.04), 7, True, 9, False, 9.0)
        # карта-пергамент на всю игровую зону, с рваным краем
        rough = a.noise(12.0) * 0.03
        inside = ss(-0.01, 0.01, np.minimum(2.45 - np.abs(X), 1.6 - np.abs(Y)) + rough)
        parchment = lerp(col(0.93, 0.84, 0.62), col(0.84, 0.71, 0.48), np.clip(0.4 + 0.15 * a.noise(80.0), 0, 1))
        # обожжённый край
        edge_d = np.minimum(2.45 - np.abs(X), 1.6 - np.abs(Y)) + rough
        parchment = lerp(parchment, col(0.45, 0.30, 0.15), (1 - ss(0.0, 0.08, edge_d)) * 0.8)
        # море и острова
        isl = np.zeros(X.shape, dtype=np.float32)
        for cx, cy, r in ((-1.6, -0.9, 0.35), (1.2, 0.8, 0.45), (1.9, -1.0, 0.22), (-1.1, 1.0, 0.25)):
            dd = np.hypot(X - cx, (Y - cy) * 1.3) - r * (1 + 0.25 * a.noise(25.0) * 0.6)
            isl = np.maximum(isl, 1 - ss(-0.01, 0.01, dd))
            parchment = lerp(parchment, col(0.35, 0.26, 0.14), (1 - ss(0.0, 0.012, np.abs(dd))) * 0.9)
        parchment = lerp(parchment, col(0.70, 0.62, 0.38), isl * 0.6)
        # пунктир маршрута и крестик
        route_y = 0.6 * np.sin(X * 1.3) - 0.2
        dash = (np.sin(X * 40) > 0).astype(np.float32)
        parchment = lerp(parchment, col(0.55, 0.12, 0.08), (1 - ss(0.008, 0.016, np.abs(Y - route_y))) * dash * (np.abs(X) < 2.0))
        xd = np.minimum(np.abs((X - 1.9) - (Y + 1.0)), np.abs((X - 1.9) + (Y + 1.0))) / 1.414
        parchment = lerp(parchment, col(0.6, 0.08, 0.06), (1 - ss(0.01, 0.02, xd)) * (np.hypot(X - 1.9, Y + 1.0) < 0.12))
        # роза ветров в углу карты
        rx, ry = -2.0, -1.2
        parchment = lerp(parchment, col(0.55, 0.40, 0.22), zc_star(X - rx, Y - ry, 0.24, 0.05, 4))
        parchment = lerp(parchment, col(0.30, 0.20, 0.10), (1 - ss(0.004, 0.01, np.abs(np.hypot(X - rx, Y - ry) - 0.2))))
        img = lerp(img, parchment, inside)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, wood, wood_d, False, 3))
        a.set_tile(LEG, wood_tile(a, wood * 1.1, wood_d, True, 4))
        a.set_tile(GOLD, metal_tile(a, col(0.85, 0.62, 0.28), 0.3))
        a.set_tile(DARK, flat_tile(a, wood_d * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.35
    return t


# ---------------------------------------------------------------------------
# 9. Тёмный: тяжёлый дуб в тёмной морилке, кованые уголки и стяжки
# ---------------------------------------------------------------------------

def make_dark():
    t = Table("Table_Dark")
    b = Builder(t.name)
    out = chamfer_rect(HX, HY, 0.12)
    b.part(slab(out, -0.26, TOP, 0.03), EDGE, top=True, bottom_tile=DARK)
    lx, ly = HX - 0.42, HY - 0.36
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.part(tapered_post(sx * lx, sy * ly, FLOOR, -0.26, 0.42, 0.42), LEG, bottom_tile=DARK)
            # кованый уголок, загнутый на кромку: пластина сверху и на двух боках
            cx, cy = sx * (HX - 0.2), sy * (HY - 0.2)
            b.part(box(Vector((cx, cy, TOP + 0.005)), Vector((0.4, 0.4, 0.014))), METAL)
            b.part(box(Vector((sx * (HX + 0.006), cy, -0.13)), Vector((0.014, 0.4, 0.26))), METAL)
            b.part(box(Vector((cx, sy * (HY + 0.006), -0.13)), Vector((0.4, 0.014, 0.26))), METAL)
            for dx, dy in ((0.12, 0.12), (-0.08, 0.12), (0.12, -0.08)):
                b.part(dome((cx + dx * sx, cy + dy * sy, TOP + 0.012), (0, 0, 1), 0.03, 0.018, 5), METAL)
            # железная обвязка ножки у пола и под царгой
            for z in (FLOOR + 0.2, -0.55):
                b.part(tapered_post(sx * lx, sy * ly, z - 0.05, z + 0.05, 0.46, 0.46), METAL)
    # царга и массивная проножка
    for sy in (-1, 1):
        b.part(rail((-lx, sy * ly, -0.42), (lx, sy * ly, -0.42), 0.18, 0.32), EDGE)
    for sx in (-1, 1):
        b.part(rail((sx * lx, -ly, -0.42), (sx * lx, ly, -0.42), 0.18, 0.32), EDGE)
    b.part(rail((-lx, 0.0, -1.2), (lx, 0.0, -1.2), 0.22, 0.2), LEG)
    for sx in (-1, 1):
        b.part(rail((sx * lx, -ly, -1.2), (sx * lx, ly, -1.2), 0.18, 0.18), LEG)
    mesh = b.finish()

    def paint(a):
        light, dark = col(0.34, 0.22, 0.14), col(0.17, 0.10, 0.06)
        img = planks_top(a, light, dark, col(0.06, 0.035, 0.02), 4, True, 13, False, 9.0)
        img = lerp(img, dark * 0.7, (1 - ss(0.0, 0.1, dist_to_rect(a.X, a.Y, HX, HY))) * 0.5)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light, dark, False, 3))
        a.set_tile(LEG, wood_tile(a, light * 1.05, dark, True, 4))
        a.set_tile(METAL, metal_tile(a, col(0.22, 0.21, 0.22), 0.25))
        a.set_tile(DARK, flat_tile(a, dark * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.3
    return t


# ---------------------------------------------------------------------------
# 10. Мраморный: плита с прожилками и золотой кантом на четырёх колоннах
# ---------------------------------------------------------------------------

def make_marble():
    t = Table("Table_Marble")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.2, 3)
    b.part(slab(out, -0.22, TOP, 0.05), EDGE, top=True, bottom_tile=DARK)
    # фриз под плитой и колонны
    b.part(slab(offset(out, 0.3), -0.42, -0.22, 0.0, 0.0), EDGE)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.part(column(sx * (HX - 0.65), sy * (HY - 0.55), FLOOR + 0.16, -0.42, 0.2, 12), LEG)
    # общее основание — низкий подиум
    b.part(slab(rounded_rect(HX - 0.25, HY - 0.2, 0.15, 2), FLOOR, FLOOR + 0.16, 0.03), ACC3, bottom_tile=DARK)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        white, grey = col(0.92, 0.91, 0.88), col(0.70, 0.70, 0.70)
        img = lerp(white, grey, np.clip(0.12 + 0.06 * a.noise(60.0), 0, 1))
        # прожилки: тонкие изломанные линии по «шуму расстояния»
        warp = a.noise(25.0, 60.0)
        for f, w in ((1.3, 0.02), (2.1, 0.012), (3.4, 0.008)):
            vein = np.abs(np.sin(f * (X * 0.8 + Y * 1.4) + 2.5 * warp))
            img = lerp(img, col(0.45, 0.45, 0.48), (1 - ss(w * 0.5, w, vein)) * 0.75)
        d = dist_to_rect(X, Y, HX, HY)
        img = lerp(img, col(0.85, 0.66, 0.28), 1 - ss(0.006, 0.012, np.abs(d - 0.12)))
        a.set_top(img)
        # ровный цвет: мелкий шум на почти белом комикс-обработка превращает в пятна
        a.set_tile(EDGE, flat_tile(a, white * 0.95, 0.0))
        u, v = a.tile_coords()
        a.set_tile(LEG, lerp(flat_tile(a, white * 0.93, 0.0), col(0.62, 0.62, 0.64), (1 - ss(0.0, 0.04, np.abs(((u * 6) % 1.0) - 0.5) - 0.42)) * 0.7))
        a.set_tile(ACC3, flat_tile(a, grey * 1.05, 0.0))
        a.set_tile(DARK, flat_tile(a, grey * 0.7))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.7
    return t


# ---------------------------------------------------------------------------
# 11. Ореховый резной: тёплый орех, резные ножки-балясины, фартук с полукружиями
# ---------------------------------------------------------------------------

def make_walnut():
    t = Table("Table_Walnut")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.32, 4)
    b.part(slab(out, -0.16, TOP, 0.03), EDGE, top=True, bottom_tile=DARK)
    # фартук с вырезанными полукружиями: по каждой стороне — доска, нижний край волной
    apron = offset(out, 0.16)

    def apron_make(bm):
        dense = []
        n = len(apron)
        for i in range(n):
            x0, y0 = apron[i]
            x1, y1 = apron[(i + 1) % n]
            steps = max(1, int(math.hypot(x1 - x0, y1 - y0) / 0.2))
            for k in range(steps):
                q = k / steps
                dense.append((x0 + (x1 - x0) * q, y0 + (y1 - y0) * q))
        inner = offset(dense, 0.05)
        wave = [-0.44 + 0.1 * abs(math.cos(i * math.pi / 4)) for i in range(len(dense))]
        ot = ring(bm, dense, -0.16)
        ob = [bm.verts.new((x, y, wave[i])) for i, (x, y) in enumerate(dense)]
        ib = [bm.verts.new((x, y, wave[i])) for i, (x, y) in enumerate(inner)]
        it = ring(bm, inner, -0.16)
        bridge(bm, ob, ot)
        bridge(bm, it, ib)
        bridge(bm, ot, it)
        bridge(bm, ib, ob)
    b.part(apron_make, EDGE)
    # резные балясины: вздутая «груша» и кольца
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (HX - 0.45), sy * (HY - 0.4)
            prof = [(0.17, FLOOR), (0.2, FLOOR + 0.06), (0.12, FLOOR + 0.16), (0.14, -1.25), (0.24, -1.05), (0.27, -0.85),
                    (0.2, -0.65), (0.12, -0.55), (0.18, -0.48), (0.13, -0.4), (0.19, -0.16)]
            b.part(lathe(prof, (x, y), 12), LEG)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        light, dark = col(0.52, 0.33, 0.19), col(0.30, 0.17, 0.09)
        warp = a.noise(70.0, 260.0)
        grain = 0.5 + 0.5 * np.sin(2 * np.pi * (Y * 4.0 + 0.5 * warp))
        img = lerp(light, dark, np.clip(0.3 + 0.45 * grain ** 3, 0, 1))
        # «шпон в ёлочку» в центре и светлая полоса-инкрустация по краю
        d = dist_to_rect(X, Y, HX, HY)
        img = lerp(img, col(0.72, 0.52, 0.30), (ss(0.14, 0.15, d) * (1 - ss(0.2, 0.21, d))) * 0.85)
        img = lerp(img, dark * 0.8, (1 - ss(0.0, 0.08, d)) * 0.4)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light, dark, False, 3, False))
        a.set_tile(LEG, wood_tile(a, light * 1.05, dark, True, 3, False))
        a.set_tile(DARK, flat_tile(a, dark * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.45
    return t


# ---------------------------------------------------------------------------
# 12. Берёзовый: светлый деревенский, ножки-брёвнышки в бересте, разведены в стороны
# ---------------------------------------------------------------------------

def make_birch():
    t = Table("Table_Birch")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.6, 4)
    b.part(slab(out, -0.14, TOP, 0.03), EDGE, top=True, bottom_tile=DARK)
    # две поперечные «лыжи» под столешницей, в них вставлены ножки
    for x in (-1.9, 1.9):
        b.part(box(Vector((x, 0.0, -0.22)), Vector((0.36, 2 * HY - 0.3, 0.16))), EDGE)
    # ножки-брёвнышки, разведены наружу
    for sx in (-1, 1):
        for sy in (-1, 1):
            top = Vector((sx * 1.9, sy * 1.35, -0.28))
            foot = Vector((sx * 2.35, sy * 1.75, FLOOR + 0.062))
            mid = top.lerp(foot, 0.5)
            b.part(tube([foot, mid, top], [0.15, 0.14, 0.13], 8), ACC1, bottom_tile=DARK)
    mesh = b.finish()

    def paint(a):
        light, dark = col(0.90, 0.80, 0.62), col(0.76, 0.63, 0.44)
        img = planks_top(a, light, dark, col(0.48, 0.36, 0.22), 5, True, 17, False, 9.0)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light * 0.95, dark, False, 3, False))
        # береста: белая с чёрными чечевичками
        u, v = a.tile_coords()
        bark = flat_tile(a, col(0.93, 0.92, 0.88), 0.03, 20.0)
        nn = TEX // 4
        spots = zm.periodic_noise(a.rng, nn, nn, 10.0, scale_v=40.0)
        bark = lerp(bark, col(0.16, 0.15, 0.14), ss(0.68, 0.72, spots) * ss(0.8, 0.9, np.abs(np.sin(v * 23.0))) * 0.85)
        a.set_tile(ACC1, bark)
        a.set_tile(DARK, flat_tile(a, col(0.55, 0.45, 0.3)))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.3
    return t


# ---------------------------------------------------------------------------
# 13. Золотой: чёрный лак, золотой узор по краю, позолоченные ножки-балясины и лапы
# ---------------------------------------------------------------------------

def make_gold():
    t = Table("Table_Gold")
    b = Builder(t.name)
    out = chamfer_rect(HX, HY, 0.35)
    b.part(slab(out, -0.16, TOP, 0.03), EDGE, top=True, bottom_tile=DARK)
    b.part(frame(offset(out, -0.02), 0.07, -0.26, -0.14), GOLD)
    b.part(slab(offset(out, 0.2), -0.42, -0.16, 0.0, 0.0), EDGE)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (HX - 0.55), sy * (HY - 0.45)
            b.part(turned_leg(x, y, -0.42, 0.2, 12), GOLD)
            b.part(blob((x, y, FLOOR + 0.08), (0.22, 0.22, 0.09), 6 + sx + 2 * sy, 1), GOLD)
    # медальоны на фартуке спереди и сзади
    for sy in (-1, 1):
        b.part(dome((0.0, sy * (HY - 0.2 + 0.01), -0.29), (0, sy, 0), 0.16, 0.05, 10), GOLD)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        lacquer = col(0.10, 0.09, 0.10)
        img = lerp(lacquer, col(0.18, 0.16, 0.17), np.clip(0.3 + 0.1 * a.noise(80.0), 0, 1))
        gold = col(1.0, 0.8, 0.3)
        d = np.minimum(dist_to_rect(X, Y, HX, HY), (HX + HY - 0.35 - np.abs(X) - np.abs(Y)) / 1.414)
        img = lerp(img, gold, 1 - ss(0.006, 0.014, np.abs(d - 0.07)))
        img = lerp(img, gold, 1 - ss(0.003, 0.007, np.abs(d - 0.16)))
        # меандр-волна между линиями
        s = np.where(np.abs(X) / HX > np.abs(Y) / HY, Y, X)
        wave_d = np.abs(d - 0.115 - 0.025 * np.sin(s * 10.0))
        img = lerp(img, gold, 1 - ss(0.004, 0.009, wave_d))
        a.set_top(img)
        a.set_tile(EDGE, flat_tile(a, lacquer, 0.08))
        a.set_tile(GOLD, metal_tile(a, gold, 0.35))
        a.set_tile(DARK, flat_tile(a, lacquer))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.65
    return t


# ---------------------------------------------------------------------------
# 14. Алхимический: тёмное дерево в пятнах, нижняя полка с колбами и книгами
# ---------------------------------------------------------------------------

def make_alchemy():
    t = Table("Table_Alchemy")
    b = Builder(t.name)
    out = rounded_rect(HX, HY, 0.18, 3)
    b.part(slab(out, -0.18, TOP, 0.025), EDGE, top=True, bottom_tile=DARK)
    lx, ly = HX - 0.35, HY - 0.3
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.part(tapered_post(sx * lx, sy * ly, FLOOR, -0.18, 0.24, 0.24), LEG, bottom_tile=DARK)
    # нижняя полка
    shelf_z = -1.25
    b.part(slab(rounded_rect(lx + 0.05, ly + 0.05, 0.1, 2), shelf_z - 0.07, shelf_z, 0.0, 0.0), EDGE, bottom_tile=DARK)
    # колбы: круглая с горлышком, высокая, пузатая
    rng = np.random.default_rng(44)
    flasks = ((-1.6, 0.6, 0.26), (-1.1, -0.5, 0.2), (0.9, 0.7, 0.3), (1.5, -0.4, 0.22), (0.2, 0.9, 0.18))
    for k, (x, y, r) in enumerate(flasks):
        prof = [(r * 0.55, shelf_z), (r, shelf_z + r * 0.5), (r * 0.95, shelf_z + r * 1.2), (r * 0.35, shelf_z + r * 1.7),
                (r * 0.28, shelf_z + r * 2.6), (r * 0.36, shelf_z + r * 2.75)]
        b.part(lathe(prof, (x, y), 10), ACC1 if k % 2 == 0 else ACC2)
    # стопка книг
    for k in range(3):
        b.part(box(Vector((-0.2 + 0.03 * k, -0.6, shelf_z + 0.07 + 0.13 * k)), Vector((0.6, 0.42, 0.12)), 0.15 * k), ACC3)
    mesh = b.finish()

    def paint(a):
        X, Y = a.X, a.Y
        light, dark = col(0.40, 0.28, 0.18), col(0.22, 0.14, 0.08)
        img = planks_top(a, light, dark, col(0.08, 0.05, 0.03), 5, True, 23, False, 9.0)
        # пятна от реактивов и прожжённые круги у краёв
        rng = np.random.default_rng(7)
        for color in (col(0.2, 0.45, 0.25), col(0.42, 0.22, 0.5), col(0.12, 0.08, 0.05)):
            for _ in range(2):
                cx = rng.choice([-1, 1]) * rng.uniform(2.6, 2.75)
                cy = rng.uniform(-1.8, 1.8)
                dd = np.hypot(X - cx, Y - cy) - rng.uniform(0.08, 0.14) * (1 + 0.3 * a.noise(15.0))
                img = lerp(img, color, (1 - ss(-0.01, 0.01, dd)) * 0.6)
        a.set_top(img)
        a.set_tile(EDGE, wood_tile(a, light, dark, False, 3))
        a.set_tile(LEG, wood_tile(a, light, dark, True, 3))
        glass_g = flat_tile(a, col(0.35, 0.75, 0.45), 0.05)
        glass_p = flat_tile(a, col(0.62, 0.35, 0.78), 0.05)
        u, v = a.tile_coords()
        a.set_tile(ACC1, lerp(glass_g, col(0.85, 1.0, 0.9), ss(0.75, 0.95, v) * 0.6))
        a.set_tile(ACC2, lerp(glass_p, col(0.95, 0.85, 1.0), ss(0.75, 0.95, v) * 0.6))
        books = flat_tile(a, col(0.55, 0.12, 0.1), 0.05)
        a.set_tile(ACC3, lerp(books, col(0.9, 0.85, 0.7), 1 - ss(0.0, 0.08, np.minimum(v, 1 - v))))
        a.set_tile(DARK, flat_tile(a, dark * 0.6))

    t.mesh, t.paint = mesh, paint
    t.smoothness = 0.3
    return t


TABLES = [make_tavern, make_gambling, make_barrel, make_stone, make_deck, make_royal, make_stump, make_pirate,
          make_dark, make_marble, make_walnut, make_birch, make_gold, make_alchemy]


# ---------------------------------------------------------------------------
# Сборка, проверка, экспорт
# ---------------------------------------------------------------------------

def build(table):
    atlas = Atlas(sum(ord(ch) for ch in table.name))
    # плитки по умолчанию — нейтральные, чтобы пустые не были чёрными
    for k in range(8):
        atlas.set_tile(k, flat_tile(atlas, col(0.4, 0.3, 0.2), 0.05))
    atlas.set_top(np.full((TEX // 2, TEX, 3), 0.5, dtype=np.float32))
    table.paint(atlas)
    img = zc.comic_flatten(atlas.img)
    image = zm.save_png(table.name, img, os.path.join(TEXTURES_DIR, table.name + ".png"))
    mat = zm.make_material(table.name, image)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = table.metallic
    bsdf.inputs["Roughness"].default_value = 1.0 - table.smoothness
    table.mesh.materials.append(mat)
    obj = bpy.data.objects.new(table.name, table.mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def check(table, obj):
    """Проверка посадки: верх на 0 и плоский в игровой зоне, низ на полу, габариты, треугольники."""
    from mathutils.bvhtree import BVHTree
    mesh = obj.data
    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    tris = zm.triangle_count(mesh)
    upper = [v.co for v in mesh.vertices if v.co.z > UPPER_Z]
    upper_x = max(abs(c.x) for c in upper)
    upper_y = max(abs(c.y) for c in upper)
    tree = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    flat = True
    for gx in np.linspace(-PLAY_X, PLAY_X, 11):
        for gy in np.linspace(-PLAY_Y, PLAY_Y, 9):
            if abs(gx) + abs(gy) > PLAY_DIAG:
                continue
            hit = tree.ray_cast(Vector((gx, gy, 2.0)), Vector((0, 0, -1)))
            if hit[0] is None or abs(hit[0].z - TOP) > 0.006:
                flat = False
    ok = (flat and abs(min(zs) - FLOOR) < 0.03 and max(zs) <= TOP + MAX_RIM + 1e-3 and
          upper_x <= MAX_X + 1e-3 and upper_y <= MAX_Y + 1e-3 and tris <= MAX_TRIS)
    print("  %-15s треуг. %5d  X %.2f  Y %.2f  верх %.3f  низ %.3f  плоско %s  %s" % (
        table.name, tris, 2 * upper_x, 2 * upper_y, max(zs), min(zs),
        "да" if flat else "НЕТ", "OK" if ok else "ПРОВЕРИТЬ"))
    return ok


def render(out_dir, objs):
    """Превью каждого стола в ракурсе меню (сверху-сбоку от игрока) с лотком, стаканами и костями."""
    scene = bpy.context.scene
    models = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models")
    props = []
    cup_path = os.path.join(models, "Cups", "Cup_Barrel.fbx")
    die_path = os.path.join(models, "Die.fbx")
    for pos in ((-2.15, 1.25), (2.15, -1.25)):
        if os.path.exists(cup_path):
            bpy.ops.import_scene.fbx(filepath=cup_path)
            o = [x for x in bpy.context.selected_objects if x.type == "MESH" and x.parent is None][0]
            o.location = (pos[0], pos[1], 0.0)
            props.append(o)
    if os.path.exists(die_path):
        for k, (x, y) in enumerate(((-0.6, 0.2), (0.1, -0.3), (0.7, 0.4), (-0.9, 1.45), (-0.5, 1.45))):
            bpy.ops.import_scene.fbx(filepath=die_path)
            o = [x for x in bpy.context.selected_objects if x.type == "MESH"][0]
            o.location = (x, y, 0.02 + 0.15 * 0.8)
            o.scale = (0.8, 0.8, 0.8)
            o.rotation_euler = (0, 0, 0.3 * k)
            props.append(o)
    tray = bpy.data.meshes.new("Tray")
    hx, hy = 1.6, 1.0
    tray.from_pydata([(-hx, -hy, 0.01), (hx, -hy, 0.01), (hx, hy, 0.01), (-hx, hy, 0.01)], [], [(0, 1, 2, 3)])
    tm = bpy.data.materials.new("TrayMat")
    tm.use_nodes = True
    tm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.25, 0.1, 1)
    tray.materials.append(tm)
    to = bpy.data.objects.new("Tray", tray)
    scene.collection.objects.link(to)
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-20, -20, FLOOR), (20, -20, FLOOR), (20, 20, FLOOR), (-20, 20, FLOOR)], [], [(0, 1, 2, 3)])
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.22, 0.2, 0.18, 1)
    floor.materials.append(fm)
    scene.collection.objects.link(bpy.data.objects.new("Floor", floor))

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.52, 0.56, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.5
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(45), math.radians(10), math.radians(-30))
    scene.collection.objects.link(sun)
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_gtao = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.view_settings.view_transform = "Standard"
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 26
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    # ракурс меню из сцены: Unity (3.6, 3.2, -5.6) → Blender (-3.6, 5.6, 3.2); цель чуть ниже центра
    views = (("menu", Vector((-4.2, 6.6, 3.6)), Vector((0.6, -0.2, -0.5))),
             ("side", Vector((8.5, 1.5, 0.4)), Vector((0.0, 0.0, -0.7))))
    for obj in objs:
        obj.hide_render = True
    for obj in objs:
        obj.hide_render = False
        for name, loc, target in views:
            cam.location = loc
            cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, obj.name + "_" + name + ".png")
            bpy.ops.render.render(write_still=True)
        obj.hide_render = True
        print("Превью: " + obj.name)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

    zm.reset_scene()
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, all_ok = [], True
    print("Столы:")
    for make in TABLES:
        table = make()
        if only and table.name not in only:
            continue
        obj = build(table)
        all_ok &= check(table, obj)
        zm.export_fbx(obj, os.path.join(MODELS_DIR, obj.name + ".fbx"))
        print("    Smoothness %.2f  Metallic %.2f" % (table.smoothness, table.metallic))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not all_ok:
        raise SystemExit("Есть столы с ошибками")


if __name__ == "__main__":
    main()
