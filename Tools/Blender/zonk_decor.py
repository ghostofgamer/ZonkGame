# -*- coding: utf-8 -*-
"""
Безделушки на стол для игры «Зонк» (вкладка магазина «Безделушки»): двенадцать предметов в теме
«игра в кости, таверна, пираты». Ставятся в якорь на краю стола (передний левый угол, зеркально стакану игрока).

    Decor_AleMug      кружка эля с шапкой пены
    Decor_Candle      свеча в блюдце с потёками воска (пламя — второй материал, светится)
    Decor_CoinStack   стопки золотых монет и монеты россыпью
    Decor_Skull       череп
    Decor_Cards       колода и карты веером
    Decor_Hourglass   песочные часы
    Decor_RumBottle   бутылка рома с этикеткой и пробкой
    Decor_MapScroll   карта сокровищ, наполовину свёрнутая
    Decor_Compass     открытый компас
    Decor_Dagger      кинжал, воткнутый в стол
    Decor_CoinPouch   мешочек с монетами
    Decor_Spyglass    подзорная труба на подставке

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_decor.py
    blender --background --factory-startup --python Tools/Blender/zonk_decor.py -- --render <папка>

Результат: Assets/ZonkContent/Art/Models/Decor/<Имя>.fbx и Art/Textures/Decor/<Имя>.png (атлас 512, один материал;
у свечи второй материал «пламя» на отдельных гранях). Опора — низ в центре (0, 0, 0), высота 0.15–0.6
(стакан 0.9, кость на столе 0.24). Оси как в zonk_models.py: Unity = (-bx, bz, -by).
Текстуры под комиксовый шейдер Zonk/Toon: ровные пятна и тёмные линии (comic_flatten из zonk_cups.py).
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
import zonk_cups as zc    # noqa: E402  comic_flatten, add_tube

PROJECT_ROOT = zm.PROJECT_ROOT
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Decor")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Decor")

TEX = 512
GRID = 4                  # атлас 4×4 ячейки по 128 пикселей
CELL_MARGIN = 0.08        # отступ раскладки от края ячейки (доля ячейки)
MAX_TRIS = 1500
FLAME = 1                 # индекс материала пламени

zc.TEX = TEX              # comic_flatten не зависит от размера, но держим согласованно


# ---------------------------------------------------------------------------
# Цвета
# ---------------------------------------------------------------------------

def C(r, g, b):
    return np.array((r, g, b), dtype=np.float32)


WOOD = C(0.55, 0.33, 0.16)
WOOD_DARK = C(0.33, 0.19, 0.09)
PEWTER = C(0.62, 0.64, 0.66)
GOLD = C(1.0, 0.76, 0.25)
GOLD_DARK = C(0.72, 0.48, 0.1)
BRASS = C(0.86, 0.66, 0.3)
FOAM = C(0.98, 0.95, 0.86)
WAX = C(0.96, 0.9, 0.74)
BONE = C(0.93, 0.89, 0.78)
PAPER = C(0.93, 0.84, 0.62)
GLASS_GREEN = C(0.18, 0.42, 0.24)
GLASS_BLUE = C(0.62, 0.82, 0.88)
SAND = C(0.92, 0.75, 0.42)
CORK = C(0.72, 0.52, 0.3)
LEATHER = C(0.45, 0.26, 0.14)
CLOTH = C(0.6, 0.45, 0.28)
STEEL = C(0.75, 0.78, 0.82)
RED = C(0.75, 0.1, 0.1)
INK = C(0.08, 0.06, 0.05)
WHITE = C(0.97, 0.96, 0.92)


# ---------------------------------------------------------------------------
# Атлас: ячейка = краска (функция от локальных координат 0..1)
# ---------------------------------------------------------------------------

def cell_rect(index):
    """UV-прямоугольник ячейки с отступом: (u0, v0, u1, v1)."""
    cx, cy = index % GRID, index // GRID
    m = CELL_MARGIN / GRID
    return (cx / GRID + m, cy / GRID + m, (cx + 1) / GRID - m, (cy + 1) / GRID - m)


def paint_solid(color, top=0.12, bottom=-0.12):
    """Ровный цвет, чуть светлее вверху (по V)."""
    def paint(x, y):
        k = 1.0 + bottom + (top - bottom) * y
        return color[None, None, :] * k[..., None]
    return paint


def paint_wood(color, dark):
    """Доски: волокна вдоль V, тёмные швы по U."""
    def paint(x, y):
        grain = 0.5 + 0.5 * np.sin(x * 40.0 + np.sin(y * 9.0) * 1.5)
        base = zc.lerp(color[None, None, :], dark[None, None, :], grain * 0.35)
        seam = (np.abs(((x * 4.0) % 1.0) - 0.5) > 0.47).astype(np.float32)
        return zc.lerp(base, INK[None, None, :] * 1.5, seam * 0.8)
    return paint


def paint_metal(color, dark):
    """Металл: блик полосой, тёмные края."""
    def paint(x, y):
        band = np.exp(-((y - 0.62) ** 2) / 0.01)
        out = zc.lerp(dark[None, None, :], color[None, None, :], 0.55 + 0.45 * y)
        return zc.lerp(out, WHITE[None, None, :], band * 0.45)
    return paint


def paint_stripes(color, stripe, count=4, width=0.12):
    """Обмотка (рукоять, мешочек): полосы по V."""
    def paint(x, y):
        s = (np.abs(((y * count) % 1.0) - 0.5) < width).astype(np.float32)
        return zc.lerp(color[None, None, :], stripe[None, None, :], s)
    return paint


def disc(x, y, cx, cy, r):
    return (np.hypot(x - cx, y - cy) <= r).astype(np.float32)


def ring(x, y, cx, cy, r0, r1):
    d = np.hypot(x - cx, y - cy)
    return ((d >= r0) & (d <= r1)).astype(np.float32)


def poly(x, y, pts):
    """Маска многоугольника (чётно-нечётно)."""
    inside = np.zeros_like(x, dtype=bool)
    n = len(pts)
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
        cond = (y0 > y) != (y1 > y)
        xint = (x1 - x0) * (y - y0) / ((y1 - y0) + 1e-9) + x0
        inside ^= cond & (x < xint)
    return inside.astype(np.float32)


def paint_card_face(x, y):
    out = np.broadcast_to(WHITE, x.shape + (3,)).copy()
    border = ((x < 0.06) | (x > 0.94) | (y < 0.05) | (y > 0.95)).astype(np.float32)
    out = zc.lerp(out, INK[None, None, :], border * 0.9)
    heart = np.maximum(np.maximum(disc(x, y, 0.4, 0.58, 0.13), disc(x, y, 0.6, 0.58, 0.13)),
                       poly(x, y, [(0.28, 0.55), (0.72, 0.55), (0.5, 0.25)]))
    out = zc.lerp(out, RED[None, None, :], heart)
    corner = np.maximum(disc(x, y, 0.16, 0.87, 0.05), disc(x, y, 0.84, 0.13, 0.05))
    return zc.lerp(out, RED[None, None, :], corner)


def paint_card_back(x, y):
    out = np.broadcast_to(C(0.55, 0.08, 0.1), x.shape + (3,)).copy()
    lattice = ((np.abs(((x + y) * 6.0) % 1.0 - 0.5) < 0.08) | (np.abs(((x - y) * 6.0) % 1.0 - 0.5) < 0.08)).astype(np.float32)
    out = zc.lerp(out, GOLD[None, None, :], lattice * 0.8)
    border = ((x < 0.07) | (x > 0.93) | (y < 0.06) | (y > 0.94)).astype(np.float32)
    return zc.lerp(out, WHITE[None, None, :], border)


def paint_map(x, y):
    out = PAPER[None, None, :] * (0.92 + 0.08 * np.sin(x * 7.0) * np.sin(y * 5.0))[..., None]
    land = np.clip(disc(x, y, 0.35, 0.55, 0.22) + disc(x, y, 0.55, 0.42, 0.16) + disc(x, y, 0.75, 0.65, 0.1), 0, 1)
    coast = np.clip(ring(x, y, 0.35, 0.55, 0.21, 0.23) * (1 - disc(x, y, 0.55, 0.42, 0.16))
                    + ring(x, y, 0.55, 0.42, 0.15, 0.17) * (1 - disc(x, y, 0.35, 0.55, 0.22))
                    + ring(x, y, 0.75, 0.65, 0.09, 0.11), 0, 1)
    out = zc.lerp(out, C(0.62, 0.72, 0.42)[None, None, :], land * 0.6)
    out = zc.lerp(out, INK[None, None, :], coast)
    path = ((np.abs(y - (0.3 + 0.5 * x)) < 0.012) & (((x * 30.0) % 1.0) < 0.5) & (x > 0.15) & (x < 0.6)).astype(np.float32)
    out = zc.lerp(out, RED[None, None, :], path)
    cross = ((np.abs((x - 0.6) - (y - 0.6)) < 0.018) | (np.abs((x - 0.6) + (y - 0.6)) < 0.018)).astype(np.float32) \
        * (np.abs(x - 0.6) < 0.06) * (np.abs(y - 0.6) < 0.06)
    return zc.lerp(out, RED[None, None, :], cross)


def paint_compass(x, y):
    out = np.broadcast_to(C(0.95, 0.9, 0.78), x.shape + (3,)).copy()
    out = zc.lerp(out, INK[None, None, :], ring(x, y, 0.5, 0.5, 0.42, 0.46))
    out = zc.lerp(out, INK[None, None, :], ring(x, y, 0.5, 0.5, 0.3, 0.31))
    star = poly(x, y, [(0.5, 0.88), (0.56, 0.56), (0.88, 0.5), (0.56, 0.44), (0.5, 0.12), (0.44, 0.44), (0.12, 0.5), (0.44, 0.56)])
    out = zc.lerp(out, INK[None, None, :] * 2.5, star)
    north = poly(x, y, [(0.5, 0.88), (0.56, 0.56), (0.44, 0.56)])
    out = zc.lerp(out, RED[None, None, :], north)
    return zc.lerp(out, GOLD_DARK[None, None, :], disc(x, y, 0.5, 0.5, 0.04))


def paint_label(x, y):
    out = np.broadcast_to(C(0.93, 0.86, 0.68), x.shape + (3,)).copy()
    lines = ((np.abs(y - 0.12) < 0.03) | (np.abs(y - 0.88) < 0.03)).astype(np.float32)
    out = zc.lerp(out, INK[None, None, :], lines)
    # скрещённые кости на каждой четверти окружности
    xr = (x * 4.0) % 1.0
    bones = ((np.abs((xr - 0.5) - (y - 0.5)) < 0.05) | (np.abs((xr - 0.5) + (y - 0.5)) < 0.05)).astype(np.float32) \
        * (np.abs(xr - 0.5) < 0.22) * (np.abs(y - 0.5) < 0.22)
    return zc.lerp(out, INK[None, None, :], bones)


def paint_skull_face(x, y):
    out = BONE[None, None, :] * (0.9 + 0.1 * y)[..., None]
    eyes = np.maximum(disc(x, y, 0.32, 0.5, 0.13), disc(x, y, 0.68, 0.5, 0.13))
    out = zc.lerp(out, INK[None, None, :], eyes)
    nose = poly(x, y, [(0.5, 0.38), (0.44, 0.24), (0.56, 0.24)])
    out = zc.lerp(out, INK[None, None, :], nose)
    cracks = ((np.abs(x - (0.78 + 0.05 * np.sin(y * 30))) < 0.012) & (y > 0.62) & (y < 0.9)).astype(np.float32)
    return zc.lerp(out, INK[None, None, :], cracks)


def paint_teeth(x, y):
    out = BONE[None, None, :] * 0.95
    gaps = ((np.abs(((x * 6.0) % 1.0) - 0.5) > 0.42) | (np.abs(y - 0.5) < 0.04)).astype(np.float32)
    return zc.lerp(out, INK[None, None, :], gaps)


def paint_hourglass_glass(x, y):
    """Стекло: снизу песок горкой, сверху остаток."""
    out = GLASS_BLUE[None, None, :] * (0.95 + 0.1 * y)[..., None]
    sand = np.clip(((y < 0.3) | ((y > 0.52) & (y < 0.6))).astype(np.float32), 0, 1)
    out = zc.lerp(out, SAND[None, None, :], sand)
    shine = (np.abs(x - 0.2) < 0.03).astype(np.float32)
    return zc.lerp(out, WHITE[None, None, :], shine * 0.7)


def paint_pouch(x, y):
    out = CLOTH[None, None, :] * (0.85 + 0.25 * y)[..., None]
    folds = (np.abs(np.sin(x * 2 * np.pi * 7.0 + y * 3.0)) > 0.97).astype(np.float32) * (y > 0.78) * 0.6
    out = zc.lerp(out, CLOTH[None, None, :] * 0.5, folds)
    patch = poly(x, y, [(0.3, 0.2), (0.48, 0.18), (0.5, 0.36), (0.31, 0.38)])
    out = zc.lerp(out, C(0.42, 0.3, 0.2)[None, None, :], patch)
    stitch = ring(x, y, 0.4, 0.28, 0.1, 0.112) * (((np.arctan2(y - 0.28, x - 0.4) * 6.0) % 1.0) < 0.5)
    return zc.lerp(out, INK[None, None, :], np.clip(stitch, 0, 1))


def paint_coin_face(x, y):
    out = GOLD[None, None, :] * (0.95 + 0.1 * y)[..., None]
    out = zc.lerp(out, GOLD_DARK[None, None, :], ring(x, y, 0.5, 0.5, 0.36, 0.41))
    star = poly(x, y, [(0.5, 0.72), (0.55, 0.55), (0.72, 0.5), (0.55, 0.45), (0.5, 0.28), (0.45, 0.45), (0.28, 0.5), (0.45, 0.55)])
    return zc.lerp(out, C(1.0, 0.92, 0.6)[None, None, :], star)


def paint_foam(x, y):
    out = FOAM[None, None, :] * (0.92 + 0.1 * y)[..., None]
    bubbles = np.zeros_like(x)
    rng = np.random.default_rng(5)
    for _ in range(18):
        cx, cy, r = rng.random(), rng.random(), rng.uniform(0.03, 0.07)
        bubbles = np.maximum(bubbles, ring(x, y, cx, cy, r * 0.75, r))
    return zc.lerp(out, C(0.82, 0.72, 0.52)[None, None, :], bubbles * 0.7)


def paint_ale(x, y):
    return zc.lerp(C(0.95, 0.65, 0.15)[None, None, :], C(0.7, 0.4, 0.08)[None, None, :], (1 - y) * 0.6)


class Atlas:
    """Набор краски для модели: ячейка → функция. Текстура собирается, затем комикс-упрощение."""

    def __init__(self):
        self.paints = []

    def add(self, paint):
        self.paints.append(paint)
        assert len(self.paints) <= GRID * GRID, "мало ячеек атласа"
        return len(self.paints) - 1

    def bake(self):
        img = np.zeros((TEX, TEX, 3), dtype=np.float32)
        img[:] = C(0.5, 0.5, 0.5)
        size = TEX // GRID
        local = (np.arange(size) + 0.5) / size
        x = local[None, :] * np.ones((size, 1))
        y = local[:, None] * np.ones((1, size))
        for i, paint in enumerate(self.paints):
            cx, cy = i % GRID, i // GRID
            region = paint(x, y)
            img[cy * size:(cy + 1) * size, cx * size:(cx + 1) * size] = np.clip(region, 0, 1)
        return zc.comic_flatten(img)


# ---------------------------------------------------------------------------
# Сборка меша по частям
# ---------------------------------------------------------------------------

class Model:
    """Меш из частей; каждая часть получает UV в свою ячейку атласа (ровный цвет, плоская или цилиндрическая раскладка)."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.atlas = Atlas()
        self.metallic = 0.0
        self.roughness = 0.6
        self.has_flame = False

    def _new_faces(self, before):
        return [f for f in self.bm.faces if f not in before]

    def part(self, build, cell, mode="flat", axes=None, center=None, axis=Vector((0, 0, 1)), facing=None, fallback=None,
             material=0, smooth=True):
        """build(bm) добавляет геометрию. mode: flat | planar (axes = (ось U, ось V)) | cyl (вокруг axis через center).
        facing: только грани, смотрящие туда, получают раскладку, остальные — ровный цвет ячейки fallback."""
        before = set(self.bm.faces)
        build(self.bm)
        faces = self._new_faces(before)
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        u0, v0, u1, v1 = cell_rect(cell)
        verts = {v for f in faces for v in f.verts}
        if mode == "planar":
            a, b = axes
            pa = [v.co.dot(a) for v in verts]
            pb = [v.co.dot(b) for v in verts]
            amin, amax, bmin, bmax = min(pa), max(pa), min(pb), max(pb)
        elif mode == "cyl":
            c = center if center is not None else Vector((0, 0, 0))
            hs = [(v.co - c).dot(axis) for v in verts]
            hmin, hmax = min(hs), max(hs)
            ref = axis.orthogonal().normalized()
            ref2 = axis.cross(ref)
        for f in faces:
            f.smooth = smooth
            f.material_index = material
            use_fallback = facing is not None and f.normal.dot(facing) < 0.2
            fu0, fv0, fu1, fv1 = cell_rect(fallback) if use_fallback and fallback is not None else (u0, v0, u1, v1)
            for loop in f.loops:
                co = loop.vert.co
                if mode == "flat" or use_fallback:
                    loop[self.uv].uv = ((fu0 + fu1) / 2, (fv0 + fv1) * 0.5 + (fv1 - fv0) * 0.3 * (co.z > 0.0))
                elif mode == "planar":
                    s = (co.dot(axes[0]) - amin) / max(amax - amin, 1e-6)
                    t = (co.dot(axes[1]) - bmin) / max(bmax - bmin, 1e-6)
                    loop[self.uv].uv = (u0 + (u1 - u0) * s, v0 + (v1 - v0) * t)
                else:
                    d = co - c
                    ang = math.atan2(d.dot(ref2), d.dot(ref)) % (2 * math.pi)
                    s = ang / (2 * math.pi)
                    t = (d.dot(axis) - hmin) / max(hmax - hmin, 1e-6)
                    loop[self.uv].uv = (u0 + (u1 - u0) * s, v0 + (v1 - v0) * t)
        # шов цилиндрической раскладки: петли грани, перескочившей через 0/1 по U, сдвигаем
        if mode == "cyl":
            for f in faces:
                us = [l[self.uv].uv.x for l in f.loops]
                if max(us) - min(us) > (u1 - u0) * 0.5:
                    for l in f.loops:
                        if l[self.uv].uv.x < (u0 + u1) / 2:
                            l[self.uv].uv.x += (u1 - u0)
                            l[self.uv].uv.x = min(l[self.uv].uv.x, u1 + (u1 - u0) * 0.02)
        return faces


# --- примитивы -------------------------------------------------------------

def tf(bm, verts, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    m = Matrix.Translation(Vector(loc)) @ Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") \
        @ Matrix.Rotation(rot[0], 4, "X") @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=verts)


def cyl(r1, r2, h, seg=12, loc=(0, 0, 0), rot=(0, 0, 0), caps=True):
    """Цилиндр/конус от z=0 до h (до поворота)."""
    def build(bm):
        res = bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h)
        tf(bm, res["verts"], (0, 0, h / 2))
        tf(bm, res["verts"], loc, rot)
    return build


def box(sx, sy, sz, loc=(0, 0, 0), rot=(0, 0, 0)):
    def build(bm):
        res = bmesh.ops.create_cube(bm, size=1.0)
        tf(bm, res["verts"], (0, 0, 0), (0, 0, 0), (sx, sy, sz))
        tf(bm, res["verts"], loc, rot)
    return build


def sphere(r, loc=(0, 0, 0), scale=(1, 1, 1), seg=12, rings=8, rot=(0, 0, 0)):
    def build(bm):
        res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
        tf(bm, res["verts"], (0, 0, 0), (0, 0, 0), scale)
        tf(bm, res["verts"], loc, rot)
    return build


def lathe(profile, seg=16, loc=(0, 0, 0), cap_bottom=True, cap_top=True, rot=(0, 0, 0)):
    """Тело вращения по профилю [(r, z)] снизу вверх."""
    def build(bm):
        rings_ = []
        for r, z in profile:
            rings_.append([bm.verts.new((r * math.cos(2 * math.pi * j / seg), r * math.sin(2 * math.pi * j / seg), z))
                           for j in range(seg)])
        made = [v for ring_ in rings_ for v in ring_]
        for r0, r1 in zip(rings_, rings_[1:]):
            for j in range(seg):
                bm.faces.new((r0[j], r0[(j + 1) % seg], r1[(j + 1) % seg], r1[j]))
        if cap_bottom:
            c = bm.verts.new((0, 0, profile[0][1]))
            made.append(c)
            for j in range(seg):
                bm.faces.new((c, rings_[0][(j + 1) % seg], rings_[0][j]))
        if cap_top:
            c = bm.verts.new((0, 0, profile[-1][1]))
            made.append(c)
            for j in range(seg):
                bm.faces.new((c, rings_[-1][j], rings_[-1][(j + 1) % seg]))
        tf(bm, made, loc, rot)
    return build


def tube(path, radii, sides=6):
    def build(bm):
        zc.add_tube(bm, [Vector(p) for p in path], radii, sides)
    return build


def torus_arc(center, radius, tube_r, a0, a1, steps=8, sides=6, plane="xz"):
    """Дуга-трубка (ручка кружки)."""
    pts = []
    for k in range(steps + 1):
        a = a0 + (a1 - a0) * k / steps
        if plane == "xz":
            pts.append((center[0] + radius * math.cos(a), center[1], center[2] + radius * math.sin(a)))
        else:
            pts.append((center[0] + radius * math.cos(a), center[1] + radius * math.sin(a), center[2]))
    return tube(pts, [tube_r] * len(pts), sides)


# ---------------------------------------------------------------------------
# Предметы
# ---------------------------------------------------------------------------

def make_ale_mug():
    m = Model("Decor_AleMug")
    wood = m.atlas.add(paint_wood(WOOD, WOOD_DARK))
    metal = m.atlas.add(paint_metal(PEWTER, C(0.35, 0.36, 0.38)))
    foam = m.atlas.add(paint_foam)
    ale = m.atlas.add(paint_ale)
    m.part(lathe([(0.15, 0.0), (0.16, 0.02), (0.165, 0.18), (0.16, 0.32)], seg=16, cap_top=False), wood, "cyl")
    m.part(lathe([(0.145, 0.3), (0.145, 0.0 + 0.04)], seg=16, cap_top=False, cap_bottom=False), wood, "flat")
    m.part(cyl(0.142, 0.142, 0.005, 16, loc=(0, 0, 0.29)), ale, "flat")
    for z in (0.05, 0.27):
        m.part(lathe([(0.168, z), (0.172, z + 0.01), (0.172, z + 0.03), (0.168, z + 0.04)], seg=16, cap_top=False,
                     cap_bottom=False), metal, "flat")
    m.part(torus_arc((0.17, 0, 0.17), 0.1, 0.022, -math.pi / 2 + 0.25, math.pi / 2 - 0.25, steps=8, sides=6), metal, "flat")
    # шапка пены: купол с наплывами через край
    m.part(sphere(0.165, loc=(0, 0, 0.31), scale=(1, 1, 0.45), seg=16, rings=6), foam, "planar",
           axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
    for a, z in ((0.4, 0.27), (2.3, 0.25), (4.1, 0.28)):
        m.part(sphere(0.05, loc=(0.15 * math.cos(a), 0.15 * math.sin(a), z), scale=(1, 1, 1.4), seg=8, rings=5), foam, "flat")
    m.roughness = 0.65
    return m


def make_candle():
    m = Model("Decor_Candle")
    brass = m.atlas.add(paint_metal(BRASS, C(0.45, 0.3, 0.1)))
    wax = m.atlas.add(paint_solid(WAX, 0.1, -0.15))
    wick = m.atlas.add(paint_solid(INK * 2, 0, 0))
    flame = m.atlas.add(paint_solid(C(1.0, 0.75, 0.3), 0.2, -0.1))
    m.part(lathe([(0.0, 0.0), (0.2, 0.0), (0.22, 0.02), (0.21, 0.035), (0.08, 0.03)], seg=16, cap_bottom=False,
                 cap_top=False), brass, "flat")
    m.part(torus_arc((0.215, 0, 0.035), 0.045, 0.011, -math.pi / 2, math.pi / 2, steps=6, sides=5), brass, "flat")
    m.part(lathe([(0.065, 0.03), (0.062, 0.2), (0.06, 0.33), (0.045, 0.345), (0.0, 0.35)], seg=12, cap_top=False), wax, "flat")
    for a, z, s in ((0.3, 0.29, 2.2), (2.0, 0.26, 2.8), (4.2, 0.31, 1.8)):
        m.part(sphere(0.014, loc=(0.058 * math.cos(a), 0.058 * math.sin(a), z), scale=(1, 1, s), seg=6, rings=4), wax, "flat")
    m.part(cyl(0.006, 0.004, 0.035, 5, loc=(0, 0, 0.345)), wick, "flat")
    m.part(sphere(0.03, loc=(0, 0, 0.41), scale=(1, 1, 2.0), seg=8, rings=6), flame, "flat", material=FLAME)
    m.has_flame = True
    m.metallic = 0.0
    return m


def make_coin_stack():
    m = Model("Decor_CoinStack")
    gold = m.atlas.add(paint_metal(GOLD, GOLD_DARK))
    face = m.atlas.add(paint_coin_face)
    rng = np.random.default_rng(11)
    for (sx, sy, n) in ((0.0, 0.0, 12), (0.17, 0.06, 7), (-0.06, 0.16, 4)):
        for k in range(n):
            ox, oy = rng.uniform(-0.008, 0.008), rng.uniform(-0.008, 0.008)
            m.part(cyl(0.075, 0.075, 0.021, 14, loc=(sx + ox, sy + oy, k * 0.021)), gold, "flat")
        m.part(cyl(0.074, 0.074, 0.002, 14, loc=(sx, sy, n * 0.021)), face, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
    for (x, y, tilt) in ((0.18, -0.12, 0.0), (-0.16, -0.08, 0.35)):
        m.part(cyl(0.075, 0.075, 0.021, 14, loc=(x, y, 0.0 + tilt * 0.05), rot=(tilt, 0, 0)), gold, "flat")
    m.metallic, m.roughness = 0.85, 0.35
    return m


def make_skull():
    m = Model("Decor_Skull")
    face = m.atlas.add(paint_skull_face)
    bone = m.atlas.add(paint_solid(BONE, 0.08, -0.15))
    teeth = m.atlas.add(paint_teeth)
    front = Vector((0, -1, 0))
    m.part(sphere(0.17, loc=(0, 0.02, 0.22), scale=(0.9, 1.05, 0.95), seg=14, rings=9), face, "planar",
           axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), facing=front, fallback=bone)
    m.part(sphere(0.1, loc=(0, -0.07, 0.085), scale=(0.85, 0.75, 0.55), seg=10, rings=6), teeth, "planar",
           axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), facing=front, fallback=bone)
    m.part(sphere(0.06, loc=(0, -0.08, 0.04), scale=(1.4, 1.0, 0.6), seg=8, rings=5), bone, "flat")
    m.roughness = 0.7
    return m


def make_cards():
    m = Model("Decor_Cards")
    face = m.atlas.add(paint_card_face)
    back = m.atlas.add(paint_card_back)
    edge = m.atlas.add(paint_solid(WHITE, 0, -0.1))
    up = Vector((0, 0, 1))
    # колода
    for k in range(6):
        m.part(box(0.2, 0.28, 0.008, loc=(-0.1, 0.0, 0.004 + k * 0.009), rot=(0, 0, 0.05 * (k % 2))), back, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 1, 0))), facing=up, fallback=edge, smooth=False)
    # веер из пяти карт
    for k in range(5):
        a = -0.6 + k * 0.3
        cx, cy = 0.12 + math.sin(a) * 0.08, -0.02 + math.cos(a) * 0.08 - 0.08
        m.part(box(0.2, 0.28, 0.006, loc=(cx, cy, 0.003 + k * 0.007), rot=(0, 0, -a)), face, "planar",
               axes=(Vector((math.cos(-a), math.sin(-a), 0)), Vector((-math.sin(-a), math.cos(-a), 0))), facing=up,
               fallback=edge, smooth=False)
    m.roughness = 0.5
    return m


def make_hourglass():
    m = Model("Decor_Hourglass")
    wood = m.atlas.add(paint_wood(WOOD, WOOD_DARK))
    glass = m.atlas.add(paint_hourglass_glass)
    for z in (0.0, 0.47):
        m.part(cyl(0.16, 0.16, 0.04, 6, loc=(0, 0, z), rot=(0, 0, math.pi / 6)), wood, "flat", smooth=False)
    for k in range(3):
        a = k * 2 * math.pi / 3
        m.part(cyl(0.016, 0.016, 0.43, 6, loc=(0.125 * math.cos(a), 0.125 * math.sin(a), 0.04)), wood, "flat")
    m.part(lathe([(0.02, 0.04), (0.09, 0.07), (0.095, 0.14), (0.05, 0.22), (0.015, 0.255), (0.05, 0.29), (0.095, 0.37),
                  (0.09, 0.44), (0.02, 0.47)], seg=12), glass, "cyl", center=Vector((0, 0, 0)))
    m.roughness = 0.4
    return m


def make_rum_bottle():
    m = Model("Decor_RumBottle")
    glass = m.atlas.add(paint_solid(GLASS_GREEN, 0.2, -0.1))
    label = m.atlas.add(paint_label)
    cork = m.atlas.add(paint_solid(CORK, 0.05, -0.1))
    m.part(lathe([(0.1, 0.0), (0.115, 0.015), (0.12, 0.05), (0.12, 0.27), (0.1, 0.33), (0.05, 0.38), (0.04, 0.45),
                  (0.045, 0.47), (0.04, 0.49)], seg=14, cap_top=False), glass, "flat")
    m.part(lathe([(0.123, 0.09), (0.123, 0.21)], seg=14, cap_top=False, cap_bottom=False), label, "cyl")
    m.part(cyl(0.034, 0.038, 0.07, 8, loc=(0, 0, 0.475)), cork, "flat")
    m.roughness = 0.2
    return m


def make_map_scroll():
    m = Model("Decor_MapScroll")
    paper = m.atlas.add(paint_map)
    roll = m.atlas.add(paint_solid(PAPER * 0.92, 0.05, -0.1))
    ribbon = m.atlas.add(paint_solid(RED, 0.05, -0.1))
    up = Vector((0, 0, 1))
    # раскрытый лист, чуть волнистый
    def sheet(bm):
        nx, ny = 6, 4
        vs = [[bm.verts.new((-0.24 + 0.4 * i / nx, -0.15 + 0.3 * j / ny, 0.006 + 0.012 * math.sin(i * 1.3) * (i < nx)))
               for j in range(ny + 1)] for i in range(nx + 1)]
        for i in range(nx):
            for j in range(ny):
                bm.faces.new((vs[i][j], vs[i + 1][j], vs[i + 1][j + 1], vs[i][j + 1]))
    m.part(sheet, paper, "planar", axes=(Vector((1, 0, 0)), Vector((0, 1, 0))), facing=up, fallback=roll, smooth=False)
    m.part(cyl(0.05, 0.05, 0.32, 10, loc=(0.18, -0.16, 0.05), rot=(-math.pi / 2, 0, 0)), roll, "flat")
    m.part(cyl(0.054, 0.054, 0.03, 10, loc=(0.18, -0.02, 0.05), rot=(-math.pi / 2, 0, 0)), ribbon, "flat")
    m.part(tube([(0.18, 0.0, 0.0), (0.24, 0.06, 0.004), (0.28, 0.14, 0.004)], [0.012, 0.012, 0.008], 4), ribbon, "flat")
    m.roughness = 0.8
    return m


def make_compass():
    m = Model("Decor_Compass")
    brass = m.atlas.add(paint_metal(BRASS, C(0.42, 0.27, 0.08)))
    face = m.atlas.add(paint_compass)
    glass = m.atlas.add(paint_solid(GLASS_BLUE, 0.1, 0))
    m.part(lathe([(0.0, 0.0), (0.15, 0.0), (0.165, 0.02), (0.165, 0.055), (0.15, 0.06), (0.14, 0.05)], seg=16,
                 cap_bottom=False, cap_top=False), brass, "flat")
    m.part(cyl(0.14, 0.14, 0.002, 16, loc=(0, 0, 0.048)), face, "planar", axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
    # открытая крышка стоит сзади на петле
    m.part(lathe([(0.0, 0.0), (0.15, 0.0), (0.165, 0.015), (0.16, 0.03), (0.0, 0.03)], seg=16, cap_bottom=False, cap_top=False,
                 loc=(0, 0.165, 0.06), rot=(-math.pi / 2 + 0.25, 0, 0)), brass, "flat")
    m.part(cyl(0.12, 0.12, 0.004, 16, loc=(0, 0.17, 0.06), rot=(-math.pi / 2 + 0.25, 0, 0)), glass, "flat")
    m.part(sphere(0.022, loc=(0, -0.17, 0.04), seg=8, rings=5), brass, "flat")
    m.metallic, m.roughness = 0.8, 0.35
    return m


def make_dagger():
    m = Model("Decor_Dagger")
    steel = m.atlas.add(paint_metal(STEEL, C(0.4, 0.42, 0.46)))
    grip = m.atlas.add(paint_stripes(LEATHER, C(0.25, 0.13, 0.06), 6, 0.1))
    gold = m.atlas.add(paint_metal(GOLD, GOLD_DARK))
    gem = m.atlas.add(paint_solid(RED, 0.3, -0.2))
    # клинок воткнут: виден от стола, ромб в сечении, сужается вниз
    def blade(bm):
        top, bottom = 0.3, -0.02
        sec = [(0.045, 0), (0, 0.012), (-0.045, 0), (0, -0.012)]
        r_top = [bm.verts.new((x, y, top)) for x, y in sec]
        r_bot = [bm.verts.new((x * 0.25, y * 0.5, bottom)) for x, y in sec]
        for i in range(4):
            bm.faces.new((r_bot[i], r_bot[(i + 1) % 4], r_top[(i + 1) % 4], r_top[i]))
        bm.faces.new(list(reversed(r_bot)))
        bm.faces.new(r_top)
    m.part(blade, steel, "flat", smooth=False)
    m.part(box(0.2, 0.04, 0.03, loc=(0, 0, 0.315)), gold, "flat", smooth=False)
    for s in (-1, 1):
        m.part(sphere(0.022, loc=(0.1 * s, 0, 0.315), seg=6, rings=4), gold, "flat")
    m.part(cyl(0.022, 0.026, 0.15, 8, loc=(0, 0, 0.33)), grip, "cyl")
    m.part(sphere(0.035, loc=(0, 0, 0.5), scale=(1, 1, 0.9), seg=8, rings=6), gold, "flat")
    m.part(sphere(0.012, loc=(0, -0.03, 0.315), seg=6, rings=4), gem, "flat")
    m.metallic, m.roughness = 0.7, 0.3
    return m


def make_coin_pouch():
    m = Model("Decor_CoinPouch")
    cloth = m.atlas.add(paint_pouch)
    rope = m.atlas.add(paint_stripes(C(0.75, 0.62, 0.38), C(0.45, 0.33, 0.18), 8, 0.2))
    gold = m.atlas.add(paint_metal(GOLD, GOLD_DARK))

    def sack(bm):
        res = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=9, radius=1.0)
        for v in res["verts"]:
            x, y, z = v.co
            lump = 1.0 + 0.06 * math.sin(math.atan2(y, x) * 5) * (1 - abs(z))
            r = (0.16 if z < 0.3 else 0.16 - (z - 0.3) * 0.17) * lump
            v.co = Vector((x * r, y * r, 0.14 + z * 0.14 if z < 0 else 0.14 + z * 0.15))
        tf(bm, res["verts"], (0, 0, 0))
    m.part(sack, cloth, "cyl", center=Vector((0, 0, 0)))
    def ruffle(bm):
        lathe([(0.05, 0.27), (0.045, 0.3), (0.06, 0.33), (0.075, 0.355), (0.05, 0.365)], seg=14, cap_bottom=False)(bm)
    m.part(ruffle, cloth, "flat")
    for k in range(9):
        a = k * 2 * math.pi / 9
        m.part(sphere(0.022, loc=(0.062 * math.cos(a), 0.062 * math.sin(a), 0.352), scale=(1.2, 1.2, 0.7), seg=6, rings=4),
               cloth, "flat")
    m.part(lathe([(0.055, 0.285), (0.06, 0.3), (0.055, 0.315)], seg=12, cap_bottom=False, cap_top=False), rope, "cyl")
    m.part(tube([(0.04, -0.04, 0.3), (0.07, -0.09, 0.27), (0.08, -0.12, 0.2), (0.085, -0.125, 0.15)], [0.011, 0.011, 0.01, 0.009], 5), rope, "flat")
    for (x, y, t) in ((0.2, -0.05, 0.3), (0.16, -0.18, -0.2), (-0.14, -0.17, 0.0)):
        m.part(cyl(0.06, 0.06, 0.016, 14, loc=(x, y, 0.0), rot=(t * 0.4, 0, t)), gold, "flat")
    m.roughness = 0.85
    return m


def make_spyglass():
    m = Model("Decor_Spyglass")
    brass = m.atlas.add(paint_metal(BRASS, C(0.42, 0.27, 0.08)))
    leather = m.atlas.add(paint_stripes(LEATHER, C(0.3, 0.16, 0.07), 10, 0.08))
    wood = m.atlas.add(paint_wood(WOOD, WOOD_DARK))
    glass = m.atlas.add(paint_solid(GLASS_BLUE, 0.2, -0.1))
    tilt = 0.18
    rot = (0, math.pi / 2 - tilt, 0)
    axis = Vector((math.cos(tilt), 0, math.sin(tilt)))
    base = Vector((-0.27, 0, 0.18))

    def at(dist):
        p = base + axis * dist
        return (p.x, p.y, p.z)
    m.part(cyl(0.065, 0.06, 0.22, 12, loc=at(0.0), rot=rot), leather, "cyl", center=base, axis=axis)
    m.part(cyl(0.05, 0.05, 0.16, 12, loc=at(0.22), rot=rot), brass, "flat")
    m.part(cyl(0.038, 0.038, 0.14, 12, loc=at(0.38), rot=rot), brass, "flat")
    for d, r in ((0.0, 0.072), (0.21, 0.068), (0.37, 0.056)):
        m.part(cyl(r, r, 0.02, 12, loc=at(d), rot=rot), brass, "flat")
    m.part(cyl(0.058, 0.058, 0.004, 12, loc=at(-0.004), rot=rot), glass, "flat")
    # подставка: две скрещённые ножки и поперечина
    for s in (-1, 1):
        m.part(box(0.03, 0.03, 0.26, loc=(0.0, 0.07 * s, 0.11), rot=(0.45 * s, 0, 0)), wood, "flat", smooth=False)
    m.part(box(0.05, 0.22, 0.03, loc=(0.0, 0.0, 0.015)), wood, "flat", smooth=False)
    m.metallic, m.roughness = 0.6, 0.4
    return m


DECOR = [make_ale_mug, make_candle, make_coin_stack, make_skull, make_cards, make_hourglass, make_rum_bottle,
         make_map_scroll, make_compass, make_dagger, make_coin_pouch, make_spyglass]


# ---------------------------------------------------------------------------
# Сборка, проверка, экспорт
# ---------------------------------------------------------------------------

def flame_material():
    mat = bpy.data.materials.get("Decor_Flame")
    if mat is None:
        mat = bpy.data.materials.new("Decor_Flame")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (1.0, 0.7, 0.25, 1)
        bsdf.inputs["Emission"].default_value = (1.0, 0.6, 0.15, 1)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat


def build(model):
    # опора: низ на z = 0
    min_z = min(v.co.z for v in model.bm.verts)
    bmesh.ops.translate(model.bm, verts=list(model.bm.verts), vec=(0, 0, -min_z))
    mesh = bpy.data.meshes.new(model.name)
    model.bm.to_mesh(mesh)
    model.bm.free()
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.radians(40)
    mesh.validate()
    mesh.update()

    img = model.atlas.bake()
    image = zm.save_png(model.name, img, os.path.join(TEXTURES_DIR, model.name + ".png"))
    mat = zm.make_material(model.name, image)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = model.metallic
    bsdf.inputs["Roughness"].default_value = model.roughness
    mesh.materials.append(mat)
    if model.has_flame:
        mesh.materials.append(flame_material())
    obj = bpy.data.objects.new(model.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def check(obj):
    mesh = obj.data
    zs = [v.co.z for v in mesh.vertices]
    tris = zm.triangle_count(mesh)
    width = 2 * max(math.hypot(v.co.x, v.co.y) for v in mesh.vertices)
    ok = tris <= MAX_TRIS and abs(min(zs)) < 1e-4 and 0.05 < max(zs) < 0.7
    print("  %-16s треуг. %5d  высота %.3f  ширина %.2f  материалов %d  %s" % (
        obj.name, tris, max(zs), width, len(mesh.materials), "OK" if ok else "ПРОВЕРИТЬ"))
    return ok, tris


def render(out_dir, objs):
    """Превью: все предметы в ряд, рядом стакан (кожаный) и кость для масштаба."""
    scene = bpy.context.scene
    spacing = 0.75
    n = len(objs)
    for k, obj in enumerate(objs):
        obj.location = ((k - (n - 1) / 2) * spacing, 0, 0)
    models = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models")
    cup_path = os.path.join(models, "Cup_Leather.fbx")
    die_path = os.path.join(models, "Die.fbx")
    x_end = ((n - 1) / 2) * spacing
    def imported_root(path):
        bpy.ops.import_scene.fbx(filepath=path)
        sel = bpy.context.selected_objects
        return [o for o in sel if o.parent is None or o.parent not in sel][0]
    if os.path.exists(cup_path):
        imported_root(cup_path).location = (x_end + 0.85, 0.1, 0)
    if os.path.exists(die_path):
        die = imported_root(die_path)
        die.scale = (0.8, 0.8, 0.8)   # в игре кость 0.24 при модели 0.3
        die.location = (x_end + 0.42, -0.3, 0.12)
        die.rotation_euler = (0, 0, 0.4)

    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    fo = bpy.data.objects.new("Floor", floor)
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.3, 0.18, 1)
    floor.materials.append(fm)
    scene.collection.objects.link(fo)

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.58, 0.62, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.5
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(sun)

    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_gtao = True
    scene.eevee.use_soft_shadows = True
    scene.eevee.use_bloom = True
    scene.render.resolution_x, scene.render.resolution_y = 2600, 620
    scene.view_settings.view_transform = "Standard"

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    width = n * spacing + 1.2
    for name, elev, dist_k in (("decor_side.png", 10, 1.25), ("decor_top.png", 40, 1.3)):
        e = math.radians(elev)
        dist = width * dist_k
        cam.location = (0.5, -dist * math.cos(e), 0.25 + dist * math.sin(e))
        target = Vector((0.5, 0, 0.22))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)

    # крупно по четыре
    cam_data.lens = 50
    for g in range(0, n, 4):
        group = objs[g:g + 4]
        cx = sum(o.location.x for o in group) / len(group)
        cam.location = (cx, -3.4, 1.5)
        target = Vector((cx, 0, 0.2))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 1400, 700
        scene.render.filepath = os.path.join(out_dir, "decor_close_%d.png" % (g // 4 + 1))
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None

    zm.reset_scene()
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, all_ok = [], True
    print("Безделушки:")
    for make in DECOR:
        model = make()
        obj = build(model)
        ok, _ = check(obj)
        all_ok &= ok
        zm.export_fbx(obj, os.path.join(MODELS_DIR, obj.name + ".fbx"))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not all_ok:
        raise SystemExit("Есть предметы с ошибками")


if __name__ == "__main__":
    main()
