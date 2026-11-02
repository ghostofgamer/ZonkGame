# -*- coding: utf-8 -*-
"""
Рамки лотка для игры «Зонк» (вкладка магазина «Рамка»): бортик вокруг сукна, куда бросают кости.

    Rim_Wood       дубовый бортик, латунные уголки и заклёпки (по умолчанию)
    Rim_Rope       морской канат, кнехты на свободных углах
    Rim_Dominoes   домино цепочкой вдоль сторон, в углу несколько костяшек уже упали
    Rim_Books      старые книги разных цветов, лежащие вдоль края
    Rim_Garden     клумба: травяной бортик, цветы и камешки
    Rim_Fence      плетень: прутья, вплетённые между кольями, горшки на кольях, подсолнухи
    Rim_GreatWall  китайская стена: зубцы, сторожевые башни с пагодными крышами, фонарики
    Rim_Castle     крепостная стена с зубцами, башенки с флажками на углах
    Rim_Rails      железная дорога: тоннель, мост, шлагбаум, семафор; паровоз с вагонами ездит (Rim_Rails_Cars.fbx)
    Rim_Candy      карамельная трость в полоску, чупа-чупсы на углах
    Rim_Crystal    тёмный камень, из которого растут аметисты
    Rim_Serpent    уроборос: змей вокруг лотка кусает свой хвост
    Rim_Coins      казна: столбики золотых монет
    Rim_Bones      кости вдоль края, черепа на свободных углах
    Rim_Cards      игральные карты внахлёст, рубашками и мастями

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_rims.py
    blender --background --factory-startup --python Tools/Blender/zonk_rims.py -- --render <папка> [--only Rim_Rope,Rim_Castle]
Результат: Assets/ZonkContent/Art/Models/Rims/<Имя>.fbx и Art/Textures/Rims/<Имя>.png (атлас 512 из ячеек, как у безделушек).

Посадка (Blender: X — длина, Y — ширина, Z — вверх; Unity = (-bx, bz, -by)), опора — низ в z = 0, центр лотка в (0, 0):
  * внутренний край не ближе |X| = 1.6, |Y| = 1.0 — там невидимые стенки лотка, кости отскакивают от них;
  * снаружи не дальше |X| = 1.75, |Y| = 1.15: у углов (−X, +Y) и (+X, −Y) стоят стаканы, за |Y| = 1.3 — ряды отложенных костей;
  * всё выше 0.18 — только украшения (башни, леденцы), до 0.42.
Свободные углы (без стакана): (+X, +Y) и (−X, −Y).
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
import zonk_cups as zc    # noqa: E402
import zonk_decor as zd   # noqa: E402  Model, Atlas, ячейки, краски, примитивы

PROJECT_ROOT = zm.PROJECT_ROOT
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Rims")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Rims")

IX, IY = 1.6, 1.0         # внутренний край (стенки лотка)
OX, OY = 1.75, 1.15       # наружный предел
CX, CY = (IX + OX) / 2, (IY + OY) / 2   # средняя линия бортика
BAND = OX - IX            # ширина бортика
MAX_TRIS = 4500          # одна рамка в кадре: бюджет как у сложного стола
MAX_HEIGHT = 0.5
TOLERANCE = 0.02         # скруглённые углы трубок заходят на миллиметры — стенки лотка всё равно отдельные
FREE_CORNERS = ((1, 1), (-1, -1))       # знаки (X, Y) углов без стакана
CUP_CORNERS = ((-1, 1), (1, -1))

C = zd.C
INK = zd.INK
WHITE = zd.WHITE
GOLD, GOLD_DARK, BRASS = zd.GOLD, zd.GOLD_DARK, zd.BRASS
WOOD, WOOD_DARK = zd.WOOD, zd.WOOD_DARK
RED = zd.RED
BONE = zd.BONE
lerp = zc.lerp


# ---------------------------------------------------------------------------
# Краски для ячеек атласа (x, y — 0..1 внутри ячейки)
# ---------------------------------------------------------------------------

def paint_twist(color, stripe, turns=2.0, width=0.32, ink=0.35):
    """Витьё каната и полоски леденца: диагональные полосы, бесшовно по обеим осям."""
    def paint(x, y):
        t = (x + y * turns) % 1.0
        s = (np.abs(t - 0.5) < width).astype(np.float32)
        out = lerp(color[None, None, :], stripe[None, None, :], s)
        edge = (np.abs(np.abs(t - 0.5) - width) < 0.025).astype(np.float32)
        return lerp(out, INK[None, None, :], edge * ink)
    return paint


def paint_domino(top, bottom, landscape=False):
    """Лицо костяшки 1 × 2: две половины с точками (top, bottom — число точек), черта посередине.
    Ячейка квадратная, а костяшка вытянута вдвое: рисуем в координатах самой костяшки (по длине — 0..2), тогда на модели
    точки круглые. landscape — костяшка лёжа вдоль U (половинки слева и справа), иначе стоя (половинки сверху и снизу)."""
    layouts = {0: [], 1: [(0, 0)], 2: [(-1, 1), (1, -1)], 3: [(-1, 1), (0, 0), (1, -1)],
               4: [(-1, -1), (-1, 1), (1, -1), (1, 1)], 5: [(-1, -1), (-1, 1), (0, 0), (1, -1), (1, 1)],
               6: [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)]}

    def paint(x, y):
        # u — поперёк костяшки (0..1), v — вдоль (0..2)
        u, v = (y, x * 2.0) if landscape else (x, y * 2.0)
        aa = 2.5 / x.shape[0]
        out = np.broadcast_to(C(0.97, 0.94, 0.86), x.shape + (3,)).copy()
        edge = np.minimum(np.minimum(u, 1.0 - u), np.minimum(v, 2.0 - v))
        out = lerp(out, C(0.55, 0.5, 0.42)[None, None, :], np.clip((0.045 - edge) / aa + 0.5, 0, 1))
        out = lerp(out, INK[None, None, :], np.clip((0.022 - np.abs(v - 1.0)) / aa + 0.5, 0, 1) * (np.abs(u - 0.5) < 0.38))
        for count, cv in ((top, 1.5), (bottom, 0.5)):
            for pu, pv in layouts[count]:
                d = np.hypot(u - (0.5 + pu * 0.26), v - (cv + pv * 0.26))
                out = lerp(out, INK[None, None, :], np.clip((0.095 - d) / aa + 0.5, 0, 1))
        return out
    return paint


def paint_bricks(color, mortar, rows=4, cols=3):
    def paint(x, y):
        row = np.floor(y * rows)
        xs = (x * cols + 0.5 * (row % 2)) % 1.0
        joint = ((np.abs(((y * rows) % 1.0) - 0.5) > 0.44) | (np.abs(xs - 0.5) > 0.46)).astype(np.float32)
        shade = 0.9 + 0.1 * np.sin(row * 2.3 + np.floor(x * cols + 0.5 * (row % 2)) * 1.7)
        out = color[None, None, :] * shade[..., None]
        return lerp(out, mortar[None, None, :], joint)
    return paint


def paint_pages(x, y):
    out = np.broadcast_to(C(0.95, 0.9, 0.76), x.shape + (3,)).copy()
    lines = (np.abs(((y * 9.0) % 1.0) - 0.5) > 0.42).astype(np.float32)
    return lerp(out, C(0.7, 0.62, 0.48)[None, None, :], lines * 0.7)


def paint_spine(color):
    def paint(x, y):
        out = color[None, None, :] * (0.85 + 0.25 * y)[..., None]
        bands = ((np.abs(x - 0.18) < 0.035) | (np.abs(x - 0.82) < 0.035)).astype(np.float32)
        out = lerp(out, GOLD[None, None, :], bands)
        label = ((np.abs(x - 0.5) < 0.16) & (np.abs(y - 0.5) < 0.22)).astype(np.float32)
        out = lerp(out, C(0.93, 0.85, 0.62)[None, None, :], label)
        return lerp(out, INK[None, None, :], ((np.abs(x - 0.5) < 0.1) & (np.abs(y - 0.5) < 0.03)).astype(np.float32))
    return paint


def paint_gravel(x, y):
    out = np.broadcast_to(C(0.45, 0.42, 0.38), x.shape + (3,)).copy()
    rng = np.random.default_rng(11)
    for _ in range(40):
        cx, cy, r = rng.random(), rng.random(), rng.uniform(0.03, 0.07)
        out = lerp(out, C(0.6, 0.57, 0.52)[None, None, :] * rng.uniform(0.8, 1.15), zd.disc(x, y, cx, cy, r))
    return out


def paint_scales(color, belly):
    """Чешуя змея: по U — вдоль тела (полукруги), по V — вокруг; брюхо (v около 0) светлое, в полоску."""
    def paint(x, y):
        out = color[None, None, :] * (0.85 + 0.2 * np.abs(np.sin(np.pi * y)))[..., None]
        sx, sy = (x * 3.0) % 1.0, (y * 6.0 + 0.5 * np.floor(x * 3.0)) % 1.0
        arc = (np.abs(np.hypot(sx - 0.5, sy) - 0.45) < 0.06).astype(np.float32)
        out = lerp(out, color[None, None, :] * 0.55, arc)
        bel = ((y < 0.2) | (y > 0.8)).astype(np.float32)
        out = lerp(out, belly[None, None, :], bel)
        return lerp(out, belly[None, None, :] * 0.7, bel * (np.abs(((x * 6.0) % 1.0) - 0.5) > 0.42))
    return paint


def paint_coin_side(x, y):
    out = C(1.0, 0.82, 0.28)[None, None, :] * (0.92 + 0.12 * y)[..., None]
    rims = (np.abs(((y * 5.0) % 1.0) - 0.5) > 0.4).astype(np.float32)
    out = lerp(out, C(0.9, 0.6, 0.12)[None, None, :], rims)
    out = lerp(out, WHITE[None, None, :], (np.abs(x - 0.3) < 0.035).astype(np.float32) * 0.55)
    ridges = (np.abs(((x * 24.0) % 1.0) - 0.5) > 0.38).astype(np.float32)
    return lerp(out, GOLD_DARK[None, None, :] * 1.1, ridges * 0.5)


def paint_facets(light, dark):
    def paint(x, y):
        out = lerp(dark[None, None, :], light[None, None, :], np.clip(0.3 + 0.7 * y, 0, 1))
        facet = (np.abs(((x * 3.0) % 1.0) - 0.5) > 0.44).astype(np.float32)
        out = lerp(out, WHITE[None, None, :], facet * 0.5)
        return lerp(out, WHITE[None, None, :], (np.abs(x - 0.3) < 0.03).astype(np.float32) * (y > 0.4) * 0.6)
    return paint


def paint_rock(x, y):
    out = np.broadcast_to(C(0.22, 0.2, 0.24), x.shape + (3,)).copy()
    cracks = (np.abs(np.sin(x * 17.0 + np.sin(y * 9.0) * 2.0)) < 0.06).astype(np.float32)
    out = lerp(out, C(0.32, 0.3, 0.36)[None, None, :], (np.sin(x * 5.0 + y * 7.0) > 0.6).astype(np.float32))
    return lerp(out, INK[None, None, :], cracks * 0.8)


def paint_card(suit, red):
    """Карта лицом: белая, рамка, масть в середине и по углам."""
    def shape(x, y, cx, cy, s):
        if suit == "heart":
            return np.maximum(np.maximum(zd.disc(x, y, cx - 0.5 * s, cy + 0.35 * s, 0.55 * s),
                                         zd.disc(x, y, cx + 0.5 * s, cy + 0.35 * s, 0.55 * s)),
                              zd.poly(x, y, [(cx - 1.0 * s, cy + 0.2 * s), (cx + 1.0 * s, cy + 0.2 * s), (cx, cy - 1.0 * s)]))
        if suit == "diamond":
            return zd.poly(x, y, [(cx, cy + s), (cx + 0.7 * s, cy), (cx, cy - s), (cx - 0.7 * s, cy)])
        if suit == "spade":
            return np.maximum(np.maximum(zd.disc(x, y, cx - 0.5 * s, cy - 0.2 * s, 0.55 * s),
                                         zd.disc(x, y, cx + 0.5 * s, cy - 0.2 * s, 0.55 * s)),
                              np.maximum(zd.poly(x, y, [(cx - 1.0 * s, cy - 0.05 * s), (cx + 1.0 * s, cy - 0.05 * s), (cx, cy + 1.0 * s)]),
                                         zd.poly(x, y, [(cx, cy - 0.3 * s), (cx + 0.35 * s, cy - 1.0 * s), (cx - 0.35 * s, cy - 1.0 * s)])))
        return np.maximum(np.maximum(zd.disc(x, y, cx, cy + 0.5 * s, 0.42 * s), zd.disc(x, y, cx - 0.5 * s, cy - 0.15 * s, 0.42 * s)),
                          np.maximum(zd.disc(x, y, cx + 0.5 * s, cy - 0.15 * s, 0.42 * s),
                                     zd.poly(x, y, [(cx, cy), (cx + 0.3 * s, cy - 1.0 * s), (cx - 0.3 * s, cy - 1.0 * s)])))

    ink = RED if red else INK

    def paint(x, y):
        out = np.broadcast_to(WHITE, x.shape + (3,)).copy()
        border = ((x < 0.05) | (x > 0.95) | (y < 0.04) | (y > 0.96)).astype(np.float32)
        out = lerp(out, INK[None, None, :], border * 0.85)
        out = lerp(out, ink[None, None, :], shape(x, y, 0.5, 0.5, 0.27))
        out = lerp(out, ink[None, None, :], shape(x, y, 0.18, 0.84, 0.07))
        return lerp(out, ink[None, None, :], shape(x, y, 0.82, 0.16, 0.07))
    return paint


def paint_grass(x, y):
    out = lerp(C(0.18, 0.42, 0.12)[None, None, :], C(0.42, 0.7, 0.22)[None, None, :], np.clip(y, 0, 1))
    blades = (np.abs(((x * 14.0 + np.sin(y * 6.0) * 0.4) % 1.0) - 0.5) > 0.4).astype(np.float32) * (y > 0.3)
    return lerp(out, C(0.12, 0.3, 0.08)[None, None, :], blades * 0.7)


def paint_swirl(a, b):
    """Леденец: спираль двух цветов."""
    def paint(x, y):
        r = np.hypot(x - 0.5, y - 0.5)
        ang = np.arctan2(y - 0.5, x - 0.5)
        s = (np.sin(ang * 3.0 + r * 30.0) > 0).astype(np.float32)
        out = lerp(a[None, None, :], b[None, None, :], s)
        return lerp(out, INK[None, None, :], (np.abs(r - 0.47) < 0.03).astype(np.float32))
    return paint


def paint_leaves(color):
    """Листва куста: пятна листьев светлее и темнее."""
    def paint(x, y):
        out = np.broadcast_to(color, x.shape + (3,)).copy() * (0.85 + 0.25 * y)[..., None]
        rng = np.random.default_rng(int(color[1] * 100))
        for _ in range(30):
            cx, cy, r = rng.random(), rng.random(), rng.uniform(0.05, 0.1)
            out = lerp(out, color[None, None, :] * rng.choice((0.65, 1.3)), zd.disc(x, y, cx, cy, r) * 0.8)
        return out
    return paint


def paint_amanita(x, y):
    out = np.broadcast_to(C(0.9, 0.1, 0.08), x.shape + (3,)).copy()
    for cx, cy, r in ((0.3, 0.3, 0.08), (0.7, 0.35, 0.07), (0.5, 0.65, 0.09), (0.2, 0.7, 0.06), (0.78, 0.75, 0.06), (0.5, 0.15, 0.05)):
        out = lerp(out, WHITE[None, None, :], zd.disc(x, y, cx, cy, r))
    return out


def paint_pot(x, y):
    out = lerp(C(0.62, 0.3, 0.14)[None, None, :], C(0.8, 0.45, 0.22)[None, None, :], y)
    band = (np.abs(y - 0.55) < 0.05).astype(np.float32)
    out = lerp(out, C(0.95, 0.85, 0.6)[None, None, :], band)
    dots = (np.abs(y - 0.55) < 0.025).astype(np.float32) * (((x * 12.0) % 1.0) < 0.4)
    return lerp(out, C(0.2, 0.35, 0.6)[None, None, :], dots)


def paint_sunflower(x, y):
    r = np.hypot(x - 0.5, y - 0.5)
    ang = np.arctan2(y - 0.5, x - 0.5)
    petals = (r < 0.32 + 0.16 * np.abs(np.cos(ang * 6.0))).astype(np.float32)
    out = np.broadcast_to(C(0.2, 0.5, 0.15), x.shape + (3,)).copy()
    out = lerp(out, C(1.0, 0.8, 0.1)[None, None, :], petals)
    out = lerp(out, C(0.35, 0.2, 0.08)[None, None, :], (r < 0.22).astype(np.float32))
    return lerp(out, C(0.2, 0.12, 0.05)[None, None, :], (r < 0.22).astype(np.float32) * (np.sin(x * 60) * np.sin(y * 60) > 0.3))


def paint_roof_tiles(color, dark):
    def paint(x, y):
        row = np.floor(y * 6.0)
        tile = (((x * 8.0 + 0.5 * row) % 1.0) - 0.5)
        scallop = (np.abs(((y * 6.0) % 1.0) - 0.15 - 0.6 * tile ** 2) < 0.08).astype(np.float32)
        out = color[None, None, :] * (0.85 + 0.25 * ((y * 6.0) % 1.0))[..., None]
        return lerp(out, dark[None, None, :], scallop)
    return paint


def paint_lantern(x, y):
    out = lerp(C(0.95, 0.15, 0.1)[None, None, :], C(1.0, 0.5, 0.2)[None, None, :], np.abs(np.sin(np.pi * y)))
    ribs = (np.abs(((x * 6.0) % 1.0) - 0.5) > 0.45).astype(np.float32)
    caps = ((y < 0.12) | (y > 0.88)).astype(np.float32)
    out = lerp(out, C(0.6, 0.05, 0.05)[None, None, :], ribs * 0.7)
    return lerp(out, GOLD[None, None, :], caps)


def paint_banner(color, emblem):
    def paint(x, y):
        out = np.broadcast_to(color, x.shape + (3,)).copy()
        trim = ((x < 0.08) | (x > 0.92) | (y > 0.92)).astype(np.float32)
        out = lerp(out, emblem[None, None, :], trim)
        lion = zd.poly(x, y, [(0.5, 0.75), (0.68, 0.55), (0.5, 0.3), (0.32, 0.55)])
        out = lerp(out, emblem[None, None, :], lion)
        tail = zd.poly(x, y, [(0.0, 0.0), (0.5, 0.18), (1.0, 0.0), (1.0, 0.06), (0.0, 0.06)])
        return lerp(out, color[None, None, :] * 0.6, tail)
    return paint


def paint_portcullis(x, y):
    out = np.broadcast_to(C(0.05, 0.04, 0.04), x.shape + (3,)).copy()
    bars = ((np.abs(((x * 5.0) % 1.0) - 0.5) > 0.38) | (np.abs(((y * 5.0) % 1.0) - 0.5) > 0.4)).astype(np.float32)
    out = lerp(out, C(0.4, 0.38, 0.36)[None, None, :], bars)
    arch = (np.hypot(x - 0.5, (y - 0.6) * 1.0) > 0.5).astype(np.float32) * (y > 0.6)
    return lerp(out, C(0.7, 0.68, 0.62)[None, None, :], arch)


def paint_mountain(x, y):
    """Горка: серо-бурый камень с тёмными трещинами, сверху шапка травы с рваным краем."""
    out = lerp(C(0.42, 0.36, 0.3)[None, None, :], C(0.58, 0.52, 0.45)[None, None, :], (np.sin(x * 23.0 + y * 7.0) > 0.3).astype(np.float32))
    cracks = (np.abs(np.sin(x * 13.0 + np.sin(y * 11.0) * 2.0)) < 0.05).astype(np.float32) * (y < 0.75)
    out = lerp(out, C(0.2, 0.17, 0.14)[None, None, :], cracks)
    edge = 0.72 + 0.05 * np.sin(x * 40.0)
    return lerp(out, C(0.3, 0.62, 0.22)[None, None, :], (y > edge).astype(np.float32))


def paint_water(x, y):
    out = lerp(C(0.15, 0.45, 0.8)[None, None, :], C(0.3, 0.65, 0.95)[None, None, :], 0.5 + 0.5 * np.sin(y * 9.0 + x * 3.0))
    waves = (np.abs(np.sin(y * 25.0 + np.sin(x * 12.0) * 1.5)) < 0.12).astype(np.float32)
    return lerp(out, WHITE[None, None, :], waves * 0.8)


def paint_truss(x, y):
    out = np.broadcast_to(C(0.55, 0.15, 0.12), x.shape + (3,)).copy()
    frame = ((y < 0.15) | (y > 0.85)).astype(np.float32)
    diag = ((np.abs(((x * 4.0 + y) % 1.0) - 0.5) < 0.06) | (np.abs(((x * 4.0 - y) % 1.0) - 0.5) < 0.06)).astype(np.float32)
    hole = 1.0 - np.clip(frame + diag, 0, 1)
    out = lerp(out, C(0.08, 0.06, 0.06)[None, None, :], hole * 0.85)
    rivets = (np.abs(((x * 16.0) % 1.0) - 0.5) < 0.12).astype(np.float32) * frame * (np.abs(y - 0.5) > 0.42)
    return lerp(out, C(0.85, 0.75, 0.6)[None, None, :], rivets)


def paint_velvet(x, y):
    out = lerp(C(0.42, 0.04, 0.1)[None, None, :], C(0.62, 0.1, 0.18)[None, None, :], y)
    return lerp(out, GOLD[None, None, :], ((y > 0.85) | (y < 0.08)).astype(np.float32))


def paint_gold_bar(x, y):
    out = lerp(GOLD_DARK[None, None, :], C(1.0, 0.88, 0.45)[None, None, :], y)
    stamp = ((np.abs(x - 0.5) < 0.22) & (np.abs(y - 0.55) < 0.14)).astype(np.float32)
    out = lerp(out, GOLD_DARK[None, None, :] * 1.05, stamp)
    return lerp(out, WHITE[None, None, :], (np.abs(y - 0.85) < 0.03).astype(np.float32) * 0.7)


def paint_banknote(x, y):
    out = np.broadcast_to(C(0.55, 0.78, 0.5), x.shape + (3,)).copy()
    border = ((x < 0.06) | (x > 0.94) | (y < 0.1) | (y > 0.9)).astype(np.float32)
    out = lerp(out, C(0.2, 0.45, 0.25)[None, None, :], border)
    portrait = zd.disc(x, y, 0.5, 0.5, 0.2)
    out = lerp(out, C(0.85, 0.92, 0.75)[None, None, :], portrait)
    out = lerp(out, C(0.2, 0.45, 0.25)[None, None, :], zd.ring(x, y, 0.5, 0.5, 0.18, 0.21))
    nums = np.maximum(zd.disc(x, y, 0.15, 0.75, 0.06), zd.disc(x, y, 0.85, 0.25, 0.06))
    return lerp(out, C(0.2, 0.45, 0.25)[None, None, :], nums)


def paint_card_back_blue(x, y):
    out = np.broadcast_to(C(0.12, 0.22, 0.6), x.shape + (3,)).copy()
    dots = (np.hypot(((x * 6.0) % 1.0) - 0.5, ((y * 8.0) % 1.0) - 0.5) < 0.22).astype(np.float32)
    out = lerp(out, C(0.4, 0.55, 0.95)[None, None, :], dots)
    border = ((x < 0.07) | (x > 0.93) | (y < 0.06) | (y > 0.94)).astype(np.float32)
    return lerp(out, WHITE[None, None, :], border)


def paint_spine_plain(color):
    """Корешок без наклейки: цвет книги, золотые полоски поперёк у концов и посередине."""
    def paint(x, y):
        out = color[None, None, :] * (0.85 + 0.25 * y)[..., None]
        bands = ((np.abs(x - 0.12) < 0.03) | (np.abs(x - 0.88) < 0.03) | (np.abs(x - 0.5) < 0.012)).astype(np.float32)
        return lerp(out, GOLD[None, None, :], bands)
    return paint


def paint_dirt(x, y):
    out = np.broadcast_to(C(0.34, 0.27, 0.2), x.shape + (3,)).copy()
    rng = np.random.default_rng(31)
    for _ in range(25):
        cx, cy, r = rng.random(), rng.random(), rng.uniform(0.03, 0.08)
        out = lerp(out, C(0.25, 0.42, 0.18)[None, None, :] if rng.random() < 0.4 else C(0.45, 0.38, 0.3)[None, None, :],
                   zd.disc(x, y, cx, cy, r) * 0.9)
    return out


def paint_tomb(x, y):
    out = lerp(C(0.45, 0.47, 0.5)[None, None, :], C(0.62, 0.64, 0.66)[None, None, :], y)
    cross = (((np.abs(x - 0.5) < 0.05) & (y > 0.3) & (y < 0.8)) | ((np.abs(y - 0.62) < 0.05) & (np.abs(x - 0.5) < 0.18))).astype(np.float32)
    out = lerp(out, INK[None, None, :], cross * 0.8)
    moss = (np.sin(x * 21.0) * np.sin(y * 13.0) > 0.55).astype(np.float32) * (y < 0.35)
    return lerp(out, C(0.3, 0.5, 0.2)[None, None, :], moss)


def paint_tricorn(x, y):
    out = np.broadcast_to(C(0.12, 0.1, 0.12), x.shape + (3,)).copy()
    trim = (np.abs(y - 0.85) < 0.05).astype(np.float32)
    return lerp(out, GOLD[None, None, :], trim)


def paint_coin_bright(x, y):
    out = lerp(C(1.0, 0.78, 0.2)[None, None, :], C(1.0, 0.93, 0.5)[None, None, :], y)
    out = lerp(out, C(0.85, 0.55, 0.08)[None, None, :], zd.ring(x, y, 0.5, 0.5, 0.36, 0.42))
    star = zd.poly(x, y, [(0.5, 0.74), (0.56, 0.56), (0.74, 0.5), (0.56, 0.44), (0.5, 0.26), (0.44, 0.44), (0.26, 0.5), (0.44, 0.56)])
    out = lerp(out, C(1.0, 1.0, 0.85)[None, None, :], star)
    return lerp(out, WHITE[None, None, :], zd.disc(x, y, 0.32, 0.68, 0.05))


RAIL_RADIUS = 0.14       # радиус поворота колеи по средней линии (ZonkSetup.Rims: тот же путь у RimTrain)
RAIL_GAUGE = 0.07         # колея
RAIL_TOP = 0.064          # верх рельса: на этой высоте едут колёса


# ---------------------------------------------------------------------------
# Геометрия по контуру
# ---------------------------------------------------------------------------

def rect_path(hx, hy, radius, step):
    """Замкнутый контур скруглённого прямоугольника (по часовой со стороны +Y), точки через step."""
    pts = []
    corners = ((hx - radius, hy - radius, 0.0), (-(hx - radius), hy - radius, 0.5 * math.pi),
               (-(hx - radius), -(hy - radius), math.pi), (hx - radius, -(hy - radius), 1.5 * math.pi))
    for k, (cx, cy, a0) in enumerate(corners):
        arc = max(2, int(0.5 * math.pi * radius / step))
        for j in range(arc + 1):
            a = a0 + 0.5 * math.pi * j / arc
            pts.append((cx + radius * math.cos(a), cy + radius * math.sin(a)))
        nx, ny, _ = corners[(k + 1) % 4]
        ex, ey = pts[-1]
        tx = nx + radius * math.cos(a0 + 0.5 * math.pi)
        ty = ny + radius * math.sin(a0 + 0.5 * math.pi)
        seg = math.hypot(tx - ex, ty - ey)
        n = max(1, int(seg / step))
        for j in range(1, n):
            pts.append((ex + (tx - ex) * j / n, ey + (ty - ey) * j / n))
    return pts


def side_slots(spacing, margin=0.12, hx=CX, hy=CY):
    """Места вдоль четырёх сторон (без углов): (x, y, угол стороны). Шаг подгоняется под длину стороны."""
    out = []
    for sx, sy, length, horizontal in ((0, hy, 2 * hx, True), (0, -hy, 2 * hx, True), (hx, 0, 2 * hy, False), (-hx, 0, 2 * hy, False)):
        usable = length - 2 * margin
        n = max(1, int(round(usable / spacing)))
        for j in range(n):
            t = -usable / 2 + usable * (j + 0.5) / n
            if horizontal:
                out.append((t, sy, 0.0))
            else:
                out.append((sx, t, 0.5 * math.pi))
    return out


def path_slots(spacing, radius=0.12, hx=CX, hy=CY, phase=0.5):
    """Места по скруглённому контуру через равные расстояния, углы тоже заполнены: (x, y, угол касательной).
    Шаг подгоняется, чтобы целое число мест закрыло контур без пропуска."""
    dense = rect_path(hx, hy, radius, 0.01)
    n = len(dense)
    cum = [0.0]
    for k in range(n):
        x0, y0 = dense[k]
        x1, y1 = dense[(k + 1) % n]
        cum.append(cum[-1] + math.hypot(x1 - x0, y1 - y0))
    total = cum[-1]
    count = max(1, int(round(total / spacing)))
    out, j = [], 0
    for i in range(count):
        s = (i + phase) * total / count
        while cum[j + 1] < s:
            j += 1
        x0, y0 = dense[j]
        x1, y1 = dense[(j + 1) % n]
        t = (s - cum[j]) / max(cum[j + 1] - cum[j], 1e-9)
        out.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, math.atan2(y1 - y0, x1 - x0)))
    return out


def rect_posts(spacing, hx=CX, hy=CY):
    """Столбики по прямоугольнику: в каждом углу и через равные промежутки по сторонам (против часовой) —
    прямые пролёты между ними не срезают углы."""
    corners = ((hx, hy), (-hx, hy), (-hx, -hy), (hx, -hy))
    out = []
    for i in range(4):
        x0, y0 = corners[i]
        x1, y1 = corners[(i + 1) % 4]
        n = max(1, int(round(math.hypot(x1 - x0, y1 - y0) / spacing)))
        a = math.atan2(y1 - y0, x1 - x0)
        for k in range(n):
            out.append((x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n, a))
    return out


def sag_path(posts, top, sag, steps):
    """Провисающий канат между столбиками: точки (x, y, z) по прямой между соседями, z — парабола."""
    pts = []
    n = len(posts)
    for i in range(n):
        x0, y0 = posts[i][0], posts[i][1]
        x1, y1 = posts[(i + 1) % n][0], posts[(i + 1) % n][1]
        for k in range(steps):
            t = k / steps
            pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, top - sag * 4.0 * t * (1.0 - t)))
    return pts


def tube_loop(model, pts, radii, sides, cell, z, closed=True, turns_per_segment=1.0):
    """Трубка по пути с раскладкой в ячейку: U повторяется на каждом сегменте (узор витья бесшовный), V — вокруг."""
    bm = model.bm
    u0, v0, u1, v1 = zd.cell_rect(cell)
    path = [Vector((p[0], p[1], z if len(p) < 3 else p[2])) for p in pts]
    n = len(path)
    rings = []
    for k in range(n):
        prev_p = path[(k - 1) % n] if closed or k > 0 else path[k]
        next_p = path[(k + 1) % n] if closed or k < n - 1 else path[k]
        t = (next_p - prev_p).normalized()
        a = Vector((0, 0, 1)).cross(t).normalized() if abs(t.z) < 0.95 else t.orthogonal().normalized()
        b = t.cross(a).normalized()
        rad = radii[k] if isinstance(radii, (list, tuple)) else radii
        rings.append([bm.verts.new(path[k] + (a * math.cos(2 * math.pi * s / sides) + b * math.sin(2 * math.pi * s / sides)) * rad)
                      for s in range(sides)])
    segs = n if closed else n - 1
    faces = []
    for k in range(segs):
        r0, r1 = rings[k], rings[(k + 1) % n]
        for s in range(sides):
            f = bm.faces.new((r0[s], r1[s], r1[(s + 1) % sides], r0[(s + 1) % sides]))
            f.smooth = True
            # Узор на N сегментов (turns = 1/N): каждый сегмент берёт свою долю ячейки — полоса идёт без скачков.
            steps = max(1, int(round(1.0 / turns_per_segment)))
            ua = (k % steps) / steps
            ub = ua + 1.0 / steps
            uvs = ((ua, s / sides), (ub, s / sides), (ub, (s + 1) / sides), (ua, (s + 1) / sides))
            for loop, (uu, vv) in zip(f.loops, uvs):
                loop[model.uv].uv = (u0 + (u1 - u0) * uu, v0 + (v1 - v0) * vv)
            faces.append(f)
    if not closed:
        for ring_, flip in ((rings[0], True), (rings[-1], False)):
            f = bm.faces.new(list(reversed(ring_)) if flip else ring_)
            for loop in f.loops:
                loop[model.uv].uv = ((u0 + u1) / 2, (v0 + v1) / 2)
            faces.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces


def band_box(model, width, height, cell, mode="flat", inset=0.0, z0=0.0, segment=0.24, grain_along=False):
    """Сплошной бортик по контуру (углы внахлёст). С рисунком (planar) — из отрезков ~segment: ячейка повторяется
    на каждом, и кирпич или доска не растягиваются на всю сторону (раньше одна ячейка шла на 3.4 м — «растянуто»).
    grain_along: рисунок повёрнут, волокна (по V ячейки) — вдоль бортика."""
    w = width
    hx, hy = IX + inset + w / 2, IY + inset + w / 2
    for horizontal, s in ((True, 1), (True, -1), (False, 1), (False, -1)):
        length = 2 * (hx + w / 2) if horizontal else 2 * (hy - w / 2)
        count = max(1, int(round(length / segment))) if mode == "planar" else 1
        along = Vector((1, 0, 0)) if horizontal else Vector((0, 1, 0))
        axes = (Vector((0, 0, 1)), along) if grain_along else (along, Vector((0, 0, 1)))
        for i in range(count):
            c = -length / 2 + length * (i + 0.5) / count
            piece = length / count
            if horizontal:
                build = zd.box(piece, w, height, (c, s * hy, z0 + height / 2))
            else:
                build = zd.box(w, piece, height, (s * hx, c, z0 + height / 2))
            model.part(build, cell, mode, axes=axes if mode == "planar" else None)


def model_new(name):
    m = zd.Model(name)
    return m


# ---------------------------------------------------------------------------
# Рамки
# ---------------------------------------------------------------------------

def make_wood():
    m = model_new("Rim_Wood")
    wood = m.atlas.add(zd.paint_wood(C(0.6, 0.38, 0.19), C(0.38, 0.22, 0.1)))
    top = m.atlas.add(zd.paint_wood(C(0.68, 0.45, 0.24), C(0.45, 0.28, 0.13)))
    brass = m.atlas.add(zd.paint_metal(BRASS, C(0.5, 0.36, 0.12)))
    band_box(m, BAND, 0.14, wood, "planar", segment=0.55, grain_along=True)
    band_box(m, BAND * 0.8, 0.025, top, "planar", inset=BAND * 0.1, z0=0.14, segment=0.55, grain_along=True)
    for sx in (1, -1):
        for sy in (1, -1):
            m.part(zd.box(0.17, 0.17, 0.02, (sx * (OX - 0.085), sy * (OY - 0.085), 0.17)), brass)
            m.part(zd.box(0.02, 0.17, 0.12, (sx * (OX + 0.0), sy * (OY - 0.085), 0.08)), brass)
    for x, y, a in side_slots(0.45, 0.2):
        m.part(zd.cyl(0.018, 0.014, 0.012, 8, (x, y, 0.165)), brass)
    m.roughness = 0.6
    return m


def make_rope():
    """Морской леер: канат провисает между столбиками с латунными шапками, на свободных углах — бухты каната."""
    m = model_new("Rim_Rope")
    rope = m.atlas.add(paint_twist(C(0.9, 0.76, 0.5), C(0.66, 0.5, 0.28), 3.0, 0.2, 0.7))
    post = m.atlas.add(zd.paint_wood(C(0.45, 0.28, 0.14), C(0.28, 0.16, 0.07)))
    deck = m.atlas.add(zd.paint_wood(C(0.62, 0.42, 0.22), C(0.42, 0.27, 0.12)))
    brass = m.atlas.add(zd.paint_metal(BRASS, C(0.5, 0.36, 0.12)))
    band_box(m, BAND, 0.035, deck, "planar", segment=0.55, grain_along=True)
    posts = rect_posts(0.5)
    for x, y, a in posts:
        m.part(zd.cyl(0.022, 0.02, 0.2, 5, (x, y, 0.03)), post, "cyl", center=Vector((x, y, 0.03)))
        m.part(zd.cyl(0.03, 0.014, 0.03, 6, (x, y, 0.22)), brass)
    tube_loop(m, sag_path(posts, 0.205, 0.06, 6), 0.026, 5, rope, 0.0, turns_per_segment=0.5)
    tube_loop(m, sag_path(posts, 0.12, 0.035, 5), 0.017, 4, rope, 0.0, turns_per_segment=0.5)
    for sx, sy in FREE_CORNERS:
        cx, cy = sx * (CX + 0.012), sy * (CY + 0.012)
        coil = []
        for k in range(36):
            ang = k * 0.5
            r = 0.016 + 0.0011 * k
            coil.append((cx + r * math.cos(ang), cy + r * math.sin(ang), 0.05 + 0.0004 * k))
        tube_loop(m, coil, 0.011, 4, rope, 0.0, closed=False, turns_per_segment=0.5)
    return m


def make_dominoes():
    """Домино «воротами», как карточный домик: пары костяшек ∧ слева направо, сверху костяшки плашмя и второй ярус."""
    m = model_new("Rim_Dominoes")
    m.comic_blur = 0.6
    m.tex = 1024   # костяшка — 256 пикселей: точки чёткие (у остальных рамок 512)
    faces = [m.atlas.add(paint_domino(a, b)) for a, b in ((6, 1), (3, 3), (5, 2), (4, 6), (2, 0), (1, 5), (6, 6), (4, 1))]
    lying = [m.atlas.add(paint_domino(a, b, True)) for a, b in ((5, 5), (3, 1), (6, 2), (4, 4))]
    ivory = m.atlas.add(zd.paint_solid(C(0.93, 0.9, 0.82)))
    black = m.atlas.add(zd.paint_solid(C(0.12, 0.1, 0.1)))
    band_box(m, BAND, 0.012, black)
    gate_house(m, 0.085, 0.17, 0.03, 0.3, lambda k: faces[k % len(faces)], ivory, lambda k: lying[k % len(lying)], 0.012,
               flat_size=(0.18, 0.09))
    return m


def make_books():
    """Книги хаотичными стопками: корешки с золотыми полосками, белые обрезы страниц; между стопками —
    стоящие и прислонённые книги."""
    m = model_new("Rim_Books")
    colors = (C(0.75, 0.12, 0.12), C(0.1, 0.48, 0.25), C(0.15, 0.3, 0.68), C(0.55, 0.3, 0.12), C(0.48, 0.15, 0.52),
              C(0.9, 0.62, 0.12), C(0.12, 0.12, 0.14))
    spines = [m.atlas.add(paint_spine_plain(c)) for c in colors]
    covers = [m.atlas.add(zd.paint_solid(c)) for c in colors]
    pages = m.atlas.add(paint_pages)
    board = m.atlas.add(zd.paint_wood(C(0.5, 0.32, 0.16), C(0.32, 0.19, 0.08)))
    band_box(m, BAND + 0.01, 0.02, board, "planar", inset=-0.005, segment=0.55, grain_along=True)
    rng = np.random.default_rng(12)

    def book(x, y, z, length, width, th, rz, ci, standing=False, lean=0.0):
        """Книга: обложка цвета ci, корешок (с полосками) с одной длинной стороны, страницы белые с трёх сторон."""
        dir_ = Vector((math.cos(rz), math.sin(rz), 0))
        side = Vector((-math.sin(rz), math.cos(rz), 0))
        if standing:
            # стоит на обрезе: высота = длина книги, корешок к игроку (+Y) или наружу
            rot = (lean, 0, rz)
            m.part(zd.box(th, width, length, (x, y, z + length / 2), rot), spines[ci], "planar",
                   axes=(side, Vector((0, 0, 1))), facing=Vector((0, 1, 0)) if abs(side.y) > 0.5 else side, fallback=covers[ci])
            return
        # обложка: корешок (−side) — полосками, остальное цветом книги; страницы — белый блок, чуть выступает с обреза
        m.part(zd.box(length, width, th, (x, y, z + th / 2), (0, 0, rz)), spines[ci], "planar",
               axes=(dir_, Vector((0, 0, 1))), facing=-side, fallback=covers[ci])
        off = side * 0.007
        m.part(zd.box(length * 0.97, width - 0.004, th - 0.012, (x + off.x, y + off.y, z + th / 2), (0, 0, rz)), pages, "planar",
               axes=(dir_, Vector((0, 0, 1))))

    for i, (x, y, a) in enumerate(path_slots(0.24, 0.12)):
        if i % 4 == 3:
            # стоящие книги: 2–3, одна прислонена
            for j in range(int(rng.integers(2, 4))):
                d = (j - 1) * 0.045
                bx, by = x + math.cos(a) * d, y + math.sin(a) * d
                book(bx, by, 0.02, rng.uniform(0.17, 0.24), 0.11, rng.uniform(0.035, 0.05), a, int(rng.integers(len(colors))),
                     True, 0.35 if j == 0 else rng.uniform(-0.04, 0.04))
            continue
        z = 0.02
        for j in range(int(rng.integers(2, 6))):
            th = rng.uniform(0.03, 0.055)
            length = rng.uniform(0.13, 0.17)
            width = rng.uniform(0.09, 0.105)
            rz = a + rng.uniform(-0.25, 0.25) + (math.pi if rng.random() < 0.5 else 0.0)
            jx, jy = rng.uniform(-0.015, 0.015), rng.uniform(-0.015, 0.015)
            book(x + jx, y + jy, z, length, width, th, rz, int(rng.integers(len(colors))))
            z += th
    return m


def make_garden():
    """Клумба: густой кустарник по кругу, яркие цветы в несколько ярусов, мухоморы на свободных углах."""
    m = model_new("Rim_Garden")
    leaves = [m.atlas.add(paint_leaves(c)) for c in (C(0.2, 0.55, 0.18), C(0.28, 0.65, 0.2), C(0.16, 0.45, 0.2))]
    soil = m.atlas.add(zd.paint_solid(C(0.36, 0.22, 0.12)))
    petals = [m.atlas.add(zd.paint_solid(c)) for c in (C(1.0, 0.3, 0.45), C(1.0, 0.85, 0.2), C(0.65, 0.4, 1.0), WHITE,
                                                       C(1.0, 0.5, 0.15), C(0.35, 0.6, 1.0))]
    middle = m.atlas.add(zd.paint_solid(C(1.0, 0.75, 0.1)))
    cap = m.atlas.add(paint_amanita)
    stem = m.atlas.add(zd.paint_solid(C(0.96, 0.94, 0.86)))
    band_box(m, BAND, 0.03, soil)
    rng = np.random.default_rng(9)
    for k, (x, y, a) in enumerate(path_slots(0.165, 0.12)):
        r = rng.uniform(0.07, 0.09)
        m.part(zd.sphere(r, (x, y, 0.03 + r * 0.55), (1.0, 1.0, 0.85), 6, 4, (0, 0, rng.uniform(0, 3))),
               leaves[k % len(leaves)], "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    for k, (x, y, a) in enumerate(path_slots(0.2, 0.12, phase=0.0)):
        px, py = x + rng.uniform(-0.025, 0.025), y + rng.uniform(-0.025, 0.025)
        z = 0.03 + rng.uniform(0.11, 0.16)
        size = rng.uniform(0.032, 0.045)
        m.part(zd.cyl(size, size * 0.8, 0.012, 5, (px, py, z), (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 1))),
               petals[k % len(petals)])
        m.part(zd.cyl(size * 0.4, size * 0.3, 0.02, 5, (px, py, z + 0.008)), middle)
    for sx, sy in FREE_CORNERS:
        for j, (dx, dy, s) in enumerate(((0.0, 0.0, 1.0), (0.05 * -sx, 0.04 * -sy, 0.65))):
            x, y = sx * (CX - 0.015) + dx, sy * (CY - 0.015) + dy
            h = 0.16 * s
            m.part(zd.cyl(0.02 * s, 0.017 * s, h, 6, (x, y, 0.03)), stem)
            m.part(zd.sphere(0.07 * s, (x, y, 0.03 + h), (1, 1, 0.55), 8, 4), cap, "planar",
                   axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
    return m


def make_fence():
    """Плетень: прутья, вплетённые между кольями (через кол — с другой стороны), на кольях — горшки вверх дном."""
    m = model_new("Rim_Fence")
    stake = m.atlas.add(zd.paint_wood(C(0.5, 0.36, 0.22), C(0.32, 0.22, 0.12)))
    twig = m.atlas.add(paint_twist(C(0.62, 0.48, 0.3), C(0.45, 0.33, 0.2), 1.0, 0.15, 0.6))
    pot = m.atlas.add(paint_pot)
    soil = m.atlas.add(zd.paint_solid(C(0.4, 0.3, 0.18)))
    sunflower = m.atlas.add(paint_sunflower)
    band_box(m, BAND, 0.02, soil)
    # колья через 0.2; прутья плотно, ряд к ряду, каждый следующий — с другой стороны кола (настоящее плетение)
    spacing = 0.22
    stakes = path_slots(spacing, 0.12, phase=0.0)
    for x, y, a in stakes:
        m.part(zd.cyl(0.015, 0.011, 0.25, 4, (x, y, 0.0)), stake, "cyl", center=Vector((x, y, 0)))
    dense = path_slots(spacing / 2, 0.12, phase=0.0)
    for row in range(5):
        z = 0.05 + row * 0.034
        pts = []
        for k, (x, y, a) in enumerate(dense):
            s = 1.0 if (k + row) % 2 == 0 else -1.0
            nx, ny = -math.sin(a), math.cos(a)
            pts.append((x + nx * 0.016 * s, y + ny * 0.016 * s, z))
        tube_loop(m, pts, 0.0135, 3, twig, 0.0, turns_per_segment=0.5)
    for i, (x, y, a) in enumerate(stakes):
        if i % 7 == 3 and not (x * y < 0 and abs(x) > 1.2):
            m.part(zd.lathe([(0.0, 0.0), (0.03, 0.005), (0.045, 0.035), (0.04, 0.07), (0.022, 0.085), (0.026, 0.095),
                             (0.0, 0.096)], 7, (x, y, 0.33), rot=(math.pi, 0, 0)), pot, "cyl", center=Vector((x, y, 0.33)),
                   axis=Vector((0, 0, -1)))
    for sx, sy in FREE_CORNERS:
        x, y = sx * (CX - 0.02), sy * (CY - 0.02)
        m.part(zd.cyl(0.008, 0.008, 0.3, 4, (x + 0.04 * sx, y, 0.0)), soil)
        m.part(zd.cyl(0.06, 0.06, 0.015, 10, (x + 0.04 * sx, y, 0.31), (1.2, 0, 0.4 * sx)), sunflower, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    return m


def make_castle():
    """Крепость: светлый камень с бойницами, круглые башни с синими шатрами и знамёнами, надвратная башня с решёткой
    на дальней стороне, факелы у ворот."""
    m = model_new("Rim_Castle")
    brick = m.atlas.add(paint_bricks(C(0.78, 0.76, 0.7), C(0.45, 0.43, 0.4)))
    brick_v = m.atlas.add(paint_bricks(C(0.74, 0.72, 0.66), C(0.42, 0.4, 0.37), 6, 5))
    merlon = m.atlas.add(zd.paint_solid(C(0.86, 0.84, 0.78)))
    roof = m.atlas.add(paint_roof_tiles(C(0.2, 0.38, 0.85), C(0.1, 0.2, 0.55)))
    banners = [m.atlas.add(paint_banner(a, b)) for a, b in ((C(0.85, 0.1, 0.12), GOLD), (C(0.15, 0.3, 0.75), WHITE))]
    gold = m.atlas.add(zd.paint_metal(GOLD, GOLD_DARK))
    gate = m.atlas.add(paint_portcullis)
    pole = m.atlas.add(zd.paint_solid(C(0.3, 0.2, 0.12)))
    flame = m.atlas.add(zd.paint_solid(C(1.0, 0.6, 0.1)))
    band_box(m, BAND, 0.13, brick, "planar")
    for k, (x, y, a) in enumerate(path_slots(0.15, 0.12)):
        m.part(zd.box(0.075, BAND, 0.05, (x, y, 0.155), (0, 0, a)), merlon)
    corners = [(sx * CX, sy * CY) for sx in (1, -1) for sy in (1, -1)]
    for k, (x, y) in enumerate(corners):
        m.part(zd.cyl(0.08, 0.074, 0.27, 10, (x, y, 0)), brick_v, "cyl", center=Vector((x, y, 0)))
        m.part(zd.cyl(0.088, 0.088, 0.03, 10, (x, y, 0.27)), merlon)
        m.part(zd.cyl(0.09, 0.0, 0.15, 10, (x, y, 0.3)), roof)
        m.part(zd.sphere(0.013, (x, y, 0.455), (1, 1, 1.3), 6, 4), gold)
        m.part(zd.box(0.004, 0.06, 0.1, (x + 0.082 * (1 if x > 0 else -1), y, 0.2)), banners[k % 2], "planar",
               axes=(Vector((0, 1, 0)), Vector((0, 0, 1))))
    # надвратная башня на дальней стороне (−Y): две башенки, решётка, знамя, факелы
    gy = -CY
    for gx in (-0.13, 0.13):
        m.part(zd.cyl(0.065, 0.06, 0.24, 8, (gx, gy, 0)), brick_v, "cyl", center=Vector((gx, gy, 0)))
        m.part(zd.cyl(0.075, 0.0, 0.12, 8, (gx, gy, 0.24)), roof)
    m.part(zd.box(0.2, BAND, 0.21, (0, gy, 0.105)), brick, "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    m.part(zd.box(0.12, 0.01, 0.13, (0, gy + BAND / 2 + 0.002, 0.065)), gate, "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    m.part(zd.box(0.07, 0.004, 0.12, (0, gy + BAND / 2 + 0.006, 0.2)), banners[0], "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    for gx in (-0.2, 0.2):
        m.part(zd.cyl(0.006, 0.008, 0.06, 4, (gx, gy + 0.075, 0.1)), pole)
        m.part(zd.sphere(0.014, (gx, gy + 0.075, 0.17), (1, 1, 1.5), 6, 4), flame)
    return m


def make_rails():
    """Железная дорога: рельсы на шпалах по кругу, тоннель в горке, мост над ручьём, шлагбаум, семафор, станция.
    Паровоз и вагоны — отдельные части (Rim_Rails_Cars.fbx), едут по кругу компонентом RimTrain."""
    m = model_new("Rim_Rails")
    gravel = m.atlas.add(paint_gravel)
    sleeper = m.atlas.add(zd.paint_wood(C(0.42, 0.28, 0.16), C(0.28, 0.18, 0.1)))
    steel = m.atlas.add(zd.paint_metal(C(0.72, 0.74, 0.78), C(0.35, 0.36, 0.4)))
    hill = m.atlas.add(paint_mountain)
    rock = m.atlas.add(paint_bricks(C(0.66, 0.6, 0.52), C(0.4, 0.36, 0.3), 5, 4))
    water = m.atlas.add(paint_water)
    bridge = m.atlas.add(paint_truss)
    stripes = m.atlas.add(zd.paint_stripes(WHITE, C(0.9, 0.12, 0.12), 6, 0.25))
    roof = m.atlas.add(paint_roof_tiles(C(0.7, 0.18, 0.12), C(0.45, 0.1, 0.06)))
    black = m.atlas.add(zd.paint_solid(C(0.1, 0.1, 0.11)))
    loco = m.atlas.add(zd.paint_solid(C(0.15, 0.45, 0.25)))
    red = m.atlas.add(zd.paint_solid(C(0.8, 0.15, 0.12)))
    brass = m.atlas.add(zd.paint_metal(BRASS, C(0.5, 0.36, 0.12)))
    wagon = m.atlas.add(zd.paint_wood(C(0.72, 0.45, 0.2), C(0.5, 0.3, 0.12)))
    lamp = m.atlas.add(zd.paint_solid(C(1.0, 0.85, 0.3)))
    band_box(m, BAND, 0.03, gravel, "planar")
    for x, y, a in path_slots(0.085, RAIL_RADIUS):
        m.part(zd.box(0.025, BAND * 0.9, 0.015, (x, y, 0.037), (0, 0, a)), sleeper)
    for off in (-RAIL_GAUGE / 2, RAIL_GAUGE / 2):
        tube_loop(m, rect_path(CX + off, CY + off, RAIL_RADIUS + off, 0.1), 0.009, 4, steel, 0.055)
    # тоннель в горке на дальней стороне (−Y), слева от середины
    tx = -0.55
    # тоннель: каменная арка-свод над путём (внутри тёмная), порталы с проёмом, сверху глыбы горы с травой.
    # Проём выше и шире поезда — вагоны въезжают внутрь, а не проходят сквозь гору.
    dark = m.atlas.add(zd.paint_solid(C(0.06, 0.05, 0.05)))
    arch = arch_tunnel(m, tx - 0.3, tx + 0.3, -CY, (0.066, 0.26), (0.078, 0.275), rock, dark)
    for dx, r, sz in ((0.0, 0.26, 0.075), (-0.16, 0.16, 0.06), (0.15, 0.18, 0.065)):
        m.part(zd.sphere(r, (tx + dx, -CY, 0.335 + (sz - 0.075)), (1.0, 0.08 / r, sz / r), 8, 5), hill, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    for end in (-1, 1):
        m.part(zd.box(0.03, 0.05, 0.04, (tx + end * 0.3, -CY, 0.285)), rock)
    # мост над ручьём на стороне +X
    m.part(zd.box(BAND, 0.3, 0.012, (CX, 0.0, 0.031)), water, "planar", axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
    for side in (-1, 1):
        m.part(zd.box(0.012, 0.36, 0.07, (CX + side * 0.07, 0.0, 0.09)), bridge, "planar",
               axes=(Vector((0, 1, 0)), Vector((0, 0, 1))))
    # шлагбаум и семафор у игрока (+Y)
    gx = 0.9
    m.part(zd.cyl(0.012, 0.012, 0.13, 6, (gx, CY + 0.06, 0.0)), black)
    m.part(zd.box(0.34, 0.012, 0.016, (gx - 0.15, CY + 0.06, 0.17), (0, -0.35, 0)), stripes, "planar",
           axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
    sx_ = -1.0
    m.part(zd.cyl(0.008, 0.008, 0.26, 6, (sx_, CY + 0.06, 0.0)), black)
    m.part(zd.box(0.03, 0.02, 0.06, (sx_, CY + 0.06, 0.26)), black)
    m.part(zd.sphere(0.009, (sx_, CY + 0.07, 0.275), (1, 1, 1), 6, 4), red)
    m.part(zd.sphere(0.009, (sx_, CY + 0.07, 0.25), (1, 1, 1), 6, 4), loco)
    # водокачка в свободном дальнем углу, за поворотом рельсов
    hx_, hy_ = -(OX - 0.025), -(OY - 0.025)
    m.part(zd.cyl(0.008, 0.008, 0.14, 4, (hx_, hy_, 0.0)), black)
    m.part(zd.cyl(0.032, 0.032, 0.06, 10, (hx_, hy_, 0.14)), wagon, "cyl", center=Vector((hx_, hy_, 0.14)))
    m.part(zd.cyl(0.038, 0.0, 0.04, 10, (hx_, hy_, 0.2)), roof)
    # вагоны: отдельные части, тот же атлас; стоят в начале координат, вперёд — к −Y (в Unity — +Z)
    m.extras = [("Loco", make_loco(m, loco, black, red, brass, lamp)), ("Tender", make_tender(m, black, red)),
                ("Wagon", make_wagon(m, wagon, black, red))]
    return m


def make_candy():
    m = model_new("Rim_Candy")
    cane = m.atlas.add(paint_twist(WHITE, C(0.9, 0.12, 0.15), 1.0, 0.18, 0.15))
    cane_g = m.atlas.add(paint_twist(WHITE, C(0.2, 0.7, 0.3), 1.0, 0.12, 0.1))
    swirl = m.atlas.add(paint_swirl(C(0.95, 0.35, 0.65), WHITE))
    swirl2 = m.atlas.add(paint_swirl(C(0.35, 0.6, 0.98), C(0.98, 0.9, 0.3)))
    stick = m.atlas.add(zd.paint_solid(C(0.97, 0.95, 0.9)))
    wrap = m.atlas.add(zd.paint_solid(C(0.98, 0.75, 0.2)))
    tube_loop(m, rect_path(CX, CY, 0.12, 0.08), 0.065, 8, cane, 0.065, turns_per_segment=0.34)
    tube_loop(m, rect_path(CX, CY, 0.12, 0.08), 0.022, 5, cane_g, 0.14)
    for (sx, sy), sw in zip(FREE_CORNERS, (swirl, swirl2)):
        x, y = sx * (CX - 0.02), sy * (CY - 0.02)
        m.part(zd.cyl(0.012, 0.012, 0.3, 6, (x, y, 0)), stick)
        m.part(zd.cyl(0.11, 0.11, 0.035, 16, (x, y, 0.36), (math.pi / 2, 0, -0.6 if sx > 0 else 0.6)), sw, "planar",
               axes=(Vector((math.cos(-0.6 if sx > 0 else 0.6), math.sin(-0.6 if sx > 0 else 0.6), 0)), Vector((0, 0, 1))))
    for sx, sy in CUP_CORNERS:
        x, y = sx * (CX - 0.04), sy * (CY - 0.04)
        m.part(zd.sphere(0.06, (x, y, 0.12), (1.0, 1.0, 1.0), 8, 6), wrap)
        for d in (-1, 1):
            m.part(zd.cyl(0.0, 0.05, 0.06, 6, (x + d * 0.06, y, 0.12), (0, d * math.pi / 2, 0)), wrap)
    return m


def make_crystal():
    m = model_new("Rim_Crystal")
    rock = m.atlas.add(paint_rock)
    amethyst = m.atlas.add(paint_facets(C(0.78, 0.55, 0.98), C(0.35, 0.15, 0.55)))
    pale = m.atlas.add(paint_facets(C(0.85, 0.75, 1.0), C(0.5, 0.35, 0.7)))
    band_box(m, BAND, 0.07, rock, "planar")
    rng = np.random.default_rng(21)
    for k, (x, y, a) in enumerate(side_slots(0.22, 0.14)):
        out = Vector((0, 1 if y > 0.5 else -1, 0)) if abs(y) > 0.5 else Vector((1 if x > 0 else -1, 0, 0))
        for c in range(int(rng.integers(2, 4))):
            h = rng.uniform(0.07, 0.15)
            r = rng.uniform(0.018, 0.03)
            tilt_out = rng.uniform(0.1, 0.25)
            # кристаллы растут из середины бортика и чуть наружу — не в лоток и не за край
            px, py = x + rng.uniform(-0.06, 0.06) * abs(out.y), y + rng.uniform(-0.06, 0.06) * abs(out.x)
            rx = -out.y * tilt_out + rng.uniform(-0.06, 0.06) * abs(out.x)
            ry = out.x * tilt_out + rng.uniform(-0.06, 0.06) * abs(out.y)
            cell = amethyst if rng.random() < 0.7 else pale
            m.part(zd.cyl(r, r * 0.9, h, 6, (px, py, 0.05), (rx, ry, 0)), cell, "cyl", center=Vector((px, py, 0.05)))
            tip = Vector((0, 0, h))
            tip.rotate(__import__("mathutils").Euler((rx, ry, 0)))
            m.part(zd.cyl(r * 0.9, 0.0, r * 2.2, 6, (px + tip.x, py + tip.y, 0.05 + tip.z), (rx, ry, 0)), cell)
    for sx, sy in FREE_CORNERS:
        x, y = sx * (CX - 0.03), sy * (CY - 0.03)
        for c, (h, r, rx, ry) in enumerate(((0.3, 0.045, 0.1 * sy, -0.1 * sx), (0.2, 0.035, -0.35 * sy, 0.1 * sx),
                                             (0.17, 0.03, 0.3 * sy, 0.3 * sx))):
            m.part(zd.cyl(r, r * 0.9, h, 6, (x, y, 0.04), (rx, ry, 0)), amethyst, "cyl", center=Vector((x, y, 0.04)))
            tip = Vector((0, 0, h))
            tip.rotate(__import__("mathutils").Euler((rx, ry, 0)))
            m.part(zd.cyl(r * 0.9, 0.0, r * 2.4, 6, (x + tip.x, y + tip.y, 0.04 + tip.z), (rx, ry, 0)), amethyst)
    return m


def make_serpent():
    """Уроборос: змей обходит лоток и съедает свой хвост — хвост уходит в раскрытую пасть на ближней стороне."""
    m = model_new("Rim_Serpent")
    scales = m.atlas.add(paint_scales(C(0.2, 0.6, 0.25), C(0.95, 0.85, 0.4)))
    head_c = m.atlas.add(paint_scales(C(0.2, 0.6, 0.25), C(0.95, 0.85, 0.4)))
    eye = m.atlas.add(zd.paint_solid(C(1.0, 0.85, 0.1)))
    pupil = m.atlas.add(zd.paint_solid(INK))
    mouth = m.atlas.add(zd.paint_solid(C(0.55, 0.08, 0.12)))
    fang = m.atlas.add(zd.paint_solid(WHITE))
    pts = rect_path(CX, CY, 0.15, 0.07)
    # начало тела — на ближней стороне (+Y) у x = 0.5; тело идёт вокруг и возвращается хвостом к пасти
    start = min(range(len(pts)), key=lambda i: abs(pts[i][0] - 0.5) + abs(pts[i][1] - CY) * 10)
    pts = pts[start:] + pts[:start]
    n = len(pts)
    head_gap = 3                          # хвост кончается в пасти, за несколько точек до начала тела
    body = pts[head_gap:] + pts[:1]
    nb = len(body)
    radii = [0.068 * min(1.0, 0.45 + (nb - 1 - k) / 22.0) for k in range(nb)]
    radii[-1] = 0.022
    path = [(p[0], p[1], 0.062 + 0.01 * math.sin(k * 0.35)) for k, p in enumerate(body)]
    tube_loop(m, path, radii, 7, scales, 0.06, closed=False, turns_per_segment=0.25)
    # голова: на начале тела (body[0]), смотрит назад по контуру — на хвост
    hx, hy = body[0]
    tx, ty = pts[0]
    ang = math.atan2(ty - hy, tx - hx)
    fwd = Vector((math.cos(ang), math.sin(ang), 0))
    right = Vector((-fwd.y, fwd.x, 0))
    c = Vector((hx, hy, 0.085)) + fwd * 0.03
    m.part(zd.sphere(0.085, tuple(c), (1.3, 0.85, 0.75), 10, 7, (0, 0, ang)), head_c, "planar",
           axes=(fwd, Vector((0, 0, 1))))
    upper = c + fwd * 0.085 + Vector((0, 0, 0.025))
    lower = c + fwd * 0.075 + Vector((0, 0, -0.04))
    m.part(zd.sphere(0.06, tuple(upper), (1.3, 0.8, 0.38), 8, 5, (0, -0.25, ang)), head_c)
    m.part(zd.sphere(0.052, tuple(lower), (1.2, 0.75, 0.3), 8, 5, (0, 0.25, ang)), head_c)
    m.part(zd.sphere(0.045, tuple(c + fwd * 0.08 + Vector((0, 0, -0.008))), (1.0, 0.8, 0.5), 8, 5, (0, 0, ang)), mouth)
    for s in (-1, 1):
        e = c + right * (0.055 * s) + fwd * 0.02 + Vector((0, 0, 0.05))
        m.part(zd.sphere(0.024, tuple(e), (1, 1, 1), 8, 6), eye)
        m.part(zd.sphere(0.012, tuple(e + right * (0.016 * s)), (1, 0.4, 1.3), 6, 4, (0, 0, ang)), pupil)
        f = c + fwd * 0.12 + right * (0.03 * s)
        m.part(zd.cyl(0.009, 0.0, 0.035, 4, tuple(f + Vector((0, 0, 0.02))), (math.pi, 0, 0)), fang)
    return m


def make_coins():
    """Казна: яркое золото — столбики монет, пирамидки слитков, пачки купюр, крупные самоцветы, искры; короны на углах."""
    m = model_new("Rim_Coins")
    side = m.atlas.add(paint_coin_side)
    face = m.atlas.add(paint_coin_bright)
    velvet = m.atlas.add(paint_velvet)
    bar = m.atlas.add(paint_gold_bar)
    cash = m.atlas.add(paint_banknote)
    band_paper = m.atlas.add(zd.paint_solid(C(0.98, 0.95, 0.85)))
    gems = [m.atlas.add(paint_facets(a, b)) for a, b in ((C(1.0, 0.35, 0.45), C(0.75, 0.02, 0.15)), (C(0.45, 0.8, 1.0), C(0.05, 0.3, 0.85)),
                                                         (C(0.45, 1.0, 0.6), C(0.02, 0.6, 0.25)))]
    gold = m.atlas.add(zd.paint_metal(C(1.0, 0.85, 0.3), C(0.85, 0.55, 0.1)))
    pearl = m.atlas.add(zd.paint_solid(C(1.0, 0.98, 0.92)))
    band_box(m, BAND, 0.025, velvet, "planar", segment=0.4)
    rng = np.random.default_rng(15)
    kinds = ("coins", "bars", "coins", "cash", "gem", "coins", "bars", "cash", "pearls")
    for k, (x, y, a) in enumerate(path_slots(0.16, 0.12)):
        kind = kinds[k % len(kinds)]
        z = 0.025
        if kind == "coins":
            for j in range(int(rng.integers(1, 3))):
                h = rng.uniform(0.06, 0.16)
                px, py = x + (j - 0.5) * 0.05 * math.cos(a), y + (j - 0.5) * 0.05 * math.sin(a)
                m.part(zd.cyl(0.046, 0.046, h, 7, (px, py, z), caps=False), side, "cyl", center=Vector((px, py, z)))
                m.part(zd.cyl(0.047, 0.047, 0.01, 7, (px, py, z + h)), face, "planar", axes=(Vector((1, 0, 0)), Vector((0, 1, 0))))
        elif kind == "bars":
            for j, (dx, dz) in enumerate(((-0.042, 0.0), (0.042, 0.0), (0.0, 0.042))):
                m.part(zd.cyl(0.06, 0.044, 0.042, 4, (x + dx * math.cos(a), y + dx * math.sin(a), z + dz), (0, 0, a + math.pi / 4)), bar,
                       "planar", axes=(Vector((math.cos(a), math.sin(a), 0)), Vector((0, 0, 1))))
        elif kind == "cash":
            for j in range(int(rng.integers(2, 4))):
                rz = a + rng.uniform(-0.25, 0.25)
                m.part(zd.box(0.15, 0.075, 0.032, (x, y, z + 0.016), (0, 0, rz)), cash, "planar",
                       axes=(Vector((math.cos(rz), math.sin(rz), 0)), Vector((-math.sin(rz), math.cos(rz), 0))),
                       facing=Vector((0, 0, 1)), fallback=band_paper)
                m.part(zd.box(0.03, 0.077, 0.034, (x, y, z + 0.016), (0, 0, rz)), band_paper)
                z += 0.032
        elif kind == "pearls":
            for j in range(4):
                ang = j * 1.6
                m.part(zd.sphere(0.024, (x + 0.035 * math.cos(ang), y + 0.035 * math.sin(ang), 0.047 + 0.012 * (j % 2)), (1, 1, 1), 5, 3),
                       pearl)
        else:
            g = gems[k % len(gems)]
            m.part(zd.cyl(0.042, 0.056, 0.026, 6, (x, y, z)), g, "cyl", center=Vector((x, y, z)))
            m.part(zd.cyl(0.056, 0.0, 0.065, 6, (x, y, z + 0.026)), g, "cyl", center=Vector((x, y, z + 0.026)))
    for sx, sy in FREE_CORNERS:
        x, y = sx * (CX - 0.015), sy * (CY - 0.015)
        m.part(zd.cyl(0.06, 0.065, 0.05, 12, (x, y, 0.025), caps=False), gold, "cyl", center=Vector((x, y, 0.025)))
        for j in range(5):
            ang = 2 * math.pi * j / 5
            m.part(zd.cyl(0.018, 0.0, 0.05, 4, (x + 0.055 * math.cos(ang), y + 0.055 * math.sin(ang), 0.075)), gold)
            m.part(zd.sphere(0.011, (x + 0.055 * math.cos(ang), y + 0.055 * math.sin(ang), 0.128), (1, 1, 1), 6, 4),
                   gems[j % len(gems)])
    return m


def make_bones():
    """Кладбище пиратов: скрещённые кости, черепа (один в треуголке), рёбра, воткнутая сабля, надгробия со свечой, цепь."""
    m = model_new("Rim_Bones")
    bone = m.atlas.add(zd.paint_solid(BONE))
    skull = m.atlas.add(zd.paint_skull_face)
    teeth = m.atlas.add(zd.paint_teeth)
    dirt = m.atlas.add(paint_dirt)
    stone = m.atlas.add(paint_tomb)
    steel = m.atlas.add(zd.paint_metal(C(0.75, 0.78, 0.82), C(0.35, 0.36, 0.4)))
    hilt = m.atlas.add(zd.paint_metal(GOLD, GOLD_DARK))
    hat = m.atlas.add(paint_tricorn)
    wax = m.atlas.add(zd.paint_solid(C(0.96, 0.92, 0.78)))
    flame = m.atlas.add(zd.paint_solid(C(1.0, 0.65, 0.15)))
    iron = m.atlas.add(zd.paint_solid(C(0.3, 0.28, 0.27)))
    band_box(m, BAND, 0.02, dirt, "planar", segment=0.4)
    s = 0.55
    prof = [(0.0, -0.17 * s), (0.04 * s, -0.15 * s), (0.026 * s, -0.115 * s), (0.019 * s, -0.08 * s), (0.019 * s, 0.08 * s),
            (0.026 * s, 0.115 * s), (0.04 * s, 0.15 * s), (0.0, 0.17 * s)]

    def skull_at(x, y, z, face_dir, big=1.0, tricorn=False):
        m.part(zd.sphere(0.075 * big, (x, y, z), (1.0, 1.1, 0.95), 7, 5), skull, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), facing=Vector((0, face_dir, 0)), fallback=bone)
        m.part(zd.box(0.09 * big, 0.06 * big, 0.045 * big, (x, y + face_dir * 0.03 * big, z - 0.065 * big)), teeth, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), facing=Vector((0, face_dir, 0)), fallback=bone)
        if tricorn:
            m.part(zd.cyl(0.085, 0.055, 0.045, 3, (x, y, z + 0.05), (0, 0, math.pi / 2)), hat)
            m.part(zd.cyl(0.06, 0.045, 0.05, 10, (x, y, z + 0.06)), hat)

    rng = np.random.default_rng(5)
    for i, (x, y, a) in enumerate(path_slots(0.23, 0.12)):
        if i % 2 == 1:
            # россыпь между находками: одна-две кости вдоль бортика
            m.part(zd.lathe(prof, 4, (x, y, 0.035), cap_bottom=False, cap_top=False, rot=(math.pi / 2, 0, a + math.pi / 2 + (0.3 if i % 4 == 1 else -0.3))), bone)
            continue
        k = i // 2
        kind = k % 6
        if kind in (0, 3):
            # скрещённые кости
            for d in (-0.6, 0.6):
                m.part(zd.lathe(prof, 4, (x, y, 0.045), cap_bottom=False, cap_top=False, rot=(math.pi / 2, 0, a + d)), bone)
        elif kind == 1:
            face = 1 if y > 0.5 else -1
            skull_at(x, y, 0.1, face if abs(y) > 0.5 else 1, 0.9, tricorn=(k % 12 == 1))
        elif kind == 2:
            # рёбра: дуги поперёк бортика
            for j in range(3):
                d = (j - 1) * 0.045
                bx, by = x + math.cos(a) * d, y + math.sin(a) * d
                nx, ny = -math.sin(a), math.cos(a)
                arc = [(bx + nx * 0.06 * math.cos(t), by + ny * 0.06 * math.cos(t), 0.02 + 0.09 * math.sin(t))
                       for t in np.linspace(0.15, math.pi - 0.15, 6)]
                tube_loop(m, arc, 0.009, 4, bone, 0.0, closed=False)
        elif kind == 4:
            # воткнутая сабля
            m.part(zd.box(0.012, 0.035, 0.22, (x, y, 0.1), (0.25, 0, a)), steel)
            m.part(zd.box(0.07, 0.02, 0.012, (x, y + 0.03 * math.cos(a), 0.215), (0.25, 0, a)), hilt)
            m.part(zd.sphere(0.016, (x, y + 0.035 * math.cos(a), 0.25), (1, 1, 1), 6, 4), hilt)
        else:
            # цепь из колец поперёк
            for j in range(4):
                d = (j - 1.5) * 0.035
                bx, by = x + math.cos(a) * d, y + math.sin(a) * d
                m.part(zd.cyl(0.018, 0.018, 0.008, 6, (bx, by, 0.03), (math.pi / 2 * (j % 2), 0, a)), iron)
    for sx, sy in FREE_CORNERS:
        x, y = sx * (CX - 0.02), sy * (CY - 0.02)
        face = 1 if sy > 0 else -1
        m.part(zd.box(0.11, 0.035, 0.2, (x, y, 0.1), (0, 0, 0.1 * sx)), stone, "planar",
               axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), facing=Vector((0, face, 0)), fallback=stone)
        m.part(zd.cyl(0.06, 0.06, 0.035, 10, (x, y, 0.2), (math.pi / 2, 0, 0.1 * sx)), stone)
        cx_, cy_ = x - sx * 0.05, y + face * 0.05
        m.part(zd.cyl(0.014, 0.014, 0.06, 6, (cx_, cy_, 0.02)), wax)
        m.part(zd.sphere(0.012, (cx_, cy_, 0.095), (1, 1, 1.6), 6, 4), flame)
    for sx, sy in CUP_CORNERS:
        skull_at(sx * (CX - 0.01), sy * (CY - 0.01), 0.1, 1 if sy > 0 else -1, 0.85)
    return m


def make_cards():
    """Карточный домик «воротами»: пары карт ∧ слева направо, сверху карты плашмя и второй ярус. Лица — масти,
    обороты и рёбра — красная рубашка."""
    m = model_new("Rim_Cards")
    m.comic_blur = 0.6
    suits = [m.atlas.add(paint_card(s, r)) for s, r in (("heart", True), ("spade", False), ("diamond", True), ("club", False))]
    back = m.atlas.add(zd.paint_card_back)
    felt = m.atlas.add(zd.paint_solid(C(0.1, 0.3, 0.16)))
    band_box(m, BAND, 0.015, felt)
    gate_house(m, 0.1, 0.14, 0.004, 0.36, lambda k: suits[k % 4], back, lambda k: suits[(k + 1) % 4] if k % 3 else back)
    return m


def make_greatwall():
    """Китайская стена: песочный камень, зубцы по наружному краю, сторожевые башни с двухъярусными пагодными крышами,
    красные фонарики."""
    m = model_new("Rim_GreatWall")
    stone = m.atlas.add(paint_bricks(C(0.82, 0.7, 0.5), C(0.55, 0.45, 0.32), 4, 3))
    stone_v = m.atlas.add(paint_bricks(C(0.78, 0.66, 0.46), C(0.52, 0.42, 0.3), 5, 4))
    path = m.atlas.add(zd.paint_solid(C(0.66, 0.58, 0.44)))
    roof = m.atlas.add(paint_roof_tiles(C(0.12, 0.5, 0.42), C(0.06, 0.3, 0.26)))
    pillar = m.atlas.add(zd.paint_solid(C(0.8, 0.12, 0.1)))
    gold = m.atlas.add(zd.paint_metal(GOLD, GOLD_DARK))
    lantern = m.atlas.add(paint_lantern)
    band_box(m, BAND, 0.11, stone, "planar")
    band_box(m, BAND * 0.7, 0.01, path, inset=BAND * 0.15, z0=0.11)
    for x, y, a in path_slots(0.13, 0.12):
        out = Vector((x, y, 0)).normalized()
        nx, ny = -math.sin(a), math.cos(a)
        side = 1 if nx * x + ny * y > 0 else -1
        ox, oy = x + nx * side * 0.055, y + ny * side * 0.055
        m.part(zd.box(0.075, 0.03, 0.06, (ox, oy, 0.14), (0, 0, a)), stone, "planar",
               axes=(Vector((math.cos(a), math.sin(a), 0)), Vector((0, 0, 1))))
    towers = [(sx * CX, sy * CY) for sx in (1, -1) for sy in (1, -1)] + [(0.0, -CY), (CX, 0.0), (-CX, 0.0)]
    for x, y in towers:
        m.part(zd.box(0.15, 0.15, 0.2, (x, y, 0.1)), stone_v, "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))))
        for px in (-0.05, 0.05):
            for py in (-0.05, 0.05):
                m.part(zd.box(0.014, 0.014, 0.06, (x + px, y + py, 0.23)), pillar)
        m.part(zd.cyl(0.125, 0.03, 0.055, 4, (x, y, 0.255), (0, 0, math.pi / 4)), roof)
        m.part(zd.box(0.07, 0.07, 0.03, (x, y, 0.3)), pillar)
        m.part(zd.cyl(0.085, 0.015, 0.05, 4, (x, y, 0.315), (0, 0, math.pi / 4)), roof)
        m.part(zd.sphere(0.012, (x, y, 0.37), (1, 1, 1.4), 6, 4), gold)
        for px, py in ((-0.075, -0.075), (0.075, 0.075)):
            m.part(zd.sphere(0.018, (x + px, y + py, 0.235), (1, 1, 1.3), 6, 4), lantern)
    return m


def arch_tunnel(m, x0, x1, yc, inner, outer, cell, dark, steps=10):
    """Каменный свод над путём: толстая полуэллиптическая арка от x0 до x1 (замкнутое тело — нормали наружу).
    inner/outer — полуоси (по ширине, по высоте); грани внутри свода красятся тёмной ячейкой."""
    ai, bi = inner
    ao, bo = outer

    def build(bm):
        rings = {}
        for x in (x0, x1):
            outer_ = [bm.verts.new((x, yc + ao * math.cos(math.pi * k / steps), bo * math.sin(math.pi * k / steps))) for k in range(steps + 1)]
            inner_ = [bm.verts.new((x, yc + ai * math.cos(math.pi * k / steps), bi * math.sin(math.pi * k / steps))) for k in range(steps + 1)]
            rings[x] = (outer_, inner_)
        o0, i0 = rings[x0]
        o1, i1 = rings[x1]
        for k in range(steps):
            bm.faces.new((o0[k], o0[k + 1], o1[k + 1], o1[k]))
            bm.faces.new((i0[k], i1[k], i1[k + 1], i0[k + 1]))
            bm.faces.new((o0[k], i0[k], i0[k + 1], o0[k + 1]))
            bm.faces.new((o1[k], o1[k + 1], i1[k + 1], i1[k]))
        for k in (0, steps):
            bm.faces.new((o0[k], o1[k], i1[k], i0[k]))

    faces = m.part(build, cell, "planar", axes=(Vector((1, 0, 0)), Vector((0, 0, 1))), smooth=False)
    u0, v0, u1, v1 = zd.cell_rect(dark)
    for f in faces:
        c = f.calc_center_median()
        if abs(c.x - x0) > 1e-4 and abs(c.x - x1) > 1e-4 and ((c.y - yc) / ai) ** 2 + (c.z / bi) ** 2 < 1.02:
            for loop in f.loops:
                loop[m.uv].uv = ((u0 + u1) / 2, (v0 + v1) / 2)
    return faces


def car_model(m, name):
    c = zd.Model(name)
    c.atlas = m.atlas
    return c


def wheels(c, cell, length, gauge=RAIL_GAUGE, r=0.026):
    for wy in (-length * 0.32, length * 0.32):
        for side in (-1, 1):
            c.part(zd.cyl(r, r, 0.01, 10, (side * (gauge / 2 + 0.005), wy, r), (0, math.pi / 2, 0)), cell)


def make_loco(m, body, black, red, brass, lamp):
    c = car_model(m, "Loco")
    c.part(zd.box(0.08, 0.26, 0.022, (0, 0, 0.045)), red)
    c.part(zd.cyl(0.045, 0.045, 0.16, 10, (0, 0.02, 0.1), (math.pi / 2, 0, 0)), body, "cyl",
           center=Vector((0, 0.02, 0.1)), axis=Vector((0, 1, 0)))
    c.part(zd.box(0.075, 0.08, 0.1, (0, 0.08, 0.105)), body)
    c.part(zd.box(0.085, 0.09, 0.015, (0, 0.08, 0.162)), black)
    c.part(zd.cyl(0.016, 0.022, 0.07, 8, (0, -0.09, 0.13)), black)
    c.part(zd.cyl(0.02, 0.02, 0.01, 8, (0, -0.12, 0.1), (math.pi / 2, 0, 0)), lamp)
    c.part(zd.sphere(0.02, (0, -0.02, 0.15), (1, 1, 0.9), 6, 4), brass)
    c.part(zd.cyl(0.0, 0.045, 0.04, 4, (0, -0.135, 0.04), (math.pi / 2, 0, math.pi / 4)), red)
    wheels(c, red, 0.24)
    return c


def make_tender(m, black, red):
    c = car_model(m, "Tender")
    c.part(zd.box(0.08, 0.13, 0.022, (0, 0, 0.045)), red)
    c.part(zd.box(0.08, 0.12, 0.06, (0, 0, 0.085)), black)
    c.part(zd.sphere(0.04, (0, 0, 0.115), (1.0, 1.4, 0.45), 6, 4), black)
    wheels(c, red, 0.12)
    return c


def make_wagon(m, wood, black, red):
    c = car_model(m, "Wagon")
    c.part(zd.box(0.08, 0.18, 0.022, (0, 0, 0.045)), black)
    c.part(zd.box(0.078, 0.17, 0.085, (0, 0, 0.098)), wood, "planar", axes=(Vector((0, 1, 0)), Vector((0, 0, 1))))
    c.part(zd.cyl(0.044, 0.044, 0.18, 8, (0, -0.09, 0.14), (-math.pi / 2, 0, 0)), red, "flat")
    wheels(c, black, 0.17)
    return c



def house_of(m, w, h, thick, tilt, faces_for, flat_cell, flat_len, tier2_every=4, base=0.015):
    """Домик вдоль сторон: пары плиток (карт, костяшек) домиком, сверху плоские плитки через пару и второй ярус.
    faces_for(k, outward) — ячейка лица и ячейка остальных граней для плитки k (outward — лицом наружу)."""
    k = 0

    def tent(x, y, a, z):
        nonlocal k
        along = Vector((math.cos(a), math.sin(a), 0))
        normal = Vector((-math.sin(a), math.cos(a), 0))
        lean = h * math.sin(tilt) / 2 + thick / 2
        for side in (-1, 1):
            cx_, cy_ = x + normal.x * side * lean, y + normal.y * side * lean
            face, rest = faces_for(k, side)
            m.part(zd.box(w, thick, h, (cx_, cy_, z + h * math.cos(tilt) / 2), (side * tilt, 0, a)), face, "planar",
                   axes=(along, Vector((0, 0, 1))), facing=normal * side, fallback=rest)
            k += 1

    top = base + h * math.cos(tilt) + thick * math.sin(tilt)
    for sx, sy, length, horizontal in ((0, CY, 2 * CX, True), (0, -CY, 2 * CX, True), (CX, 0, 2 * CY, False), (-CX, 0, 2 * CY, False)):
        a = 0.0 if horizontal else math.pi / 2
        n = int((length - 0.26) / (w * 1.04))
        step = w * 1.04
        start = -(n - 1) * step / 2
        for i in range(n):
            t = start + i * step
            x, y = (t, sy) if horizontal else (sx, t)
            tent(x, y, a, base)
            if i % 2 == 1:
                tm = t - step / 2
                x2, y2 = (tm, sy) if horizontal else (sx, tm)
                m.part(zd.box(flat_len, BAND * 0.75, thick * 0.6, (x2, y2, top + thick * 0.3), (0, 0, a)), flat_cell)
                if i % tier2_every == 1:
                    tent(x2, y2, a, top + thick * 0.6)
    for sx, sy in FREE_CORNERS:
        x, y = sx * (CX - 0.01), sy * (CY - 0.01)
        tent(x, y, math.pi / 4, base)
    return top




def gate_house(m, w, h, thick, tilt, face_for, back, flat_for, base=0.015, tiers=2, yaw=0.56, flat_size=None):
    """Домик «воротами», как на фото: две плитки (карты, костяшки) наклонены друг к другу (∧), ворота стоят слева
    направо по всем сторонам; на стыках сверху плитки плашмя, на них второй ярус.
    На длинных сторонах камера смотрит вдоль ворот — там они повёрнуты на yaw («ёлочкой»), и лицо (масть, точки)
    каждой плитки — на стороне, повёрнутой к игроку (+Y), иначе видны одни рёбра. face_for(k) — ячейка лица,
    back — рубашка и рёбра, flat_for(k) — ячейка плитки плашмя."""
    k = 0
    lean = h * math.sin(tilt) / 2 + thick / 2
    pitch = 2 * lean * 2 + 0.008
    height = h * math.cos(tilt) + thick * math.sin(tilt)
    toward_player = Vector((0, 1, 0))

    def gate(x, y, a, z, turn):
        nonlocal k
        ga = a + turn
        along = Vector((math.cos(ga), math.sin(ga), 0))
        across = Vector((-math.sin(ga), math.cos(ga), 0))
        for side in (-1, 1):
            cx_, cy_ = x + along.x * side * lean, y + along.y * side * lean
            normal = along * side
            # лицо — на стороне к игроку; на коротких сторонах — наружу из ворот, как раньше
            facing = normal if (abs(turn) < 1e-6 or normal.dot(toward_player) >= 0) else -normal
            m.part(zd.box(thick, w, h, (cx_, cy_, z + h * math.cos(tilt) / 2), (0, -side * tilt, ga)), face_for(k), "planar",
                   axes=(across, Vector((0, 0, 1))), facing=facing, fallback=back)
            k += 1

    for sx, sy, length, horizontal in ((0, CY, 2 * CX, True), (0, -CY, 2 * CX, True), (CX, 0, 2 * CY, False), (-CX, 0, 2 * CY, False)):
        a = 0.0 if horizontal else math.pi / 2
        turn = yaw if horizontal else 0.0
        step = pitch / math.cos(turn) * 0.92 if horizontal else pitch
        n = int((length - 0.24) / step)
        start = -(n - 1) * step / 2
        for i in range(n):
            t = start + i * step
            x, y = (t, sy) if horizontal else (sx, t)
            gate(x, y, a, base, turn)
            if tiers > 1 and i % 2 == 1:
                tm = t - step / 2
                x2, y2 = (tm, sy) if horizontal else (sx, tm)
                along = Vector((math.cos(a), math.sin(a), 0))
                across = Vector((-math.sin(a), math.cos(a), 0))
                fl, fw = flat_size if flat_size else (step * 1.6, w)
                m.part(zd.box(fl, fw, thick, (x2, y2, base + height + thick / 2), (0, 0, a)), flat_for(k), "planar",
                       axes=(along, across), facing=Vector((0, 0, 1)), fallback=back)
                k += 1
                if i % 4 == 1:
                    gate(x2, y2, a, base + height + thick, turn)
    for sx in (1, -1):
        for sy in (1, -1):
            gate(sx * (CX - 0.02), sy * (CY - 0.02), math.pi / 4 * sx * sy, base, 0.0)
    return height




RIMS = [make_wood, make_rope, make_dominoes, make_books, make_garden, make_fence, make_greatwall, make_castle, make_rails, make_candy,
        make_crystal, make_serpent, make_coins, make_bones, make_cards]


# ---------------------------------------------------------------------------
# Сборка, проверка, экспорт
# ---------------------------------------------------------------------------

def build(model):
    # без подъёма: рамки строятся от уровня стола (z = 0), а что ниже (горка тоннеля, низ кустов) уходит в стол
    mesh = bpy.data.meshes.new(model.name)
    model.bm.to_mesh(mesh)
    model.bm.free()
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.radians(40)
    mesh.validate()
    mesh.update()
    # мелкий рисунок (точки домино, масти) — меньше размытия при упрощении под комикс, иначе точки съедаются
    blur, tex = zc.COMIC_BLUR, zd.TEX
    zc.COMIC_BLUR = getattr(model, "comic_blur", blur)
    zd.TEX = getattr(model, "tex", tex)
    img = model.atlas.bake()
    zc.COMIC_BLUR, zd.TEX = blur, tex
    image = zm.save_png(model.name, img, os.path.join(TEXTURES_DIR, model.name + ".png"))
    mat = zm.make_material(model.name, image)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = model.metallic
    bsdf.inputs["Roughness"].default_value = model.roughness
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(model.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj["extras"] = build_extras(model, mat)
    return obj


def build_extras(model, mat):
    """Подвижные части (вагоны): отдельные объекты с тем же материалом, ребёнком пустышки <Имя>_Cars — свой FBX."""
    extras = getattr(model, "extras", None)
    if not extras:
        return ""
    root = bpy.data.objects.new(model.name + "_Cars", None)
    bpy.context.scene.collection.objects.link(root)
    for name, car in extras:
        mesh = bpy.data.meshes.new(name)
        car.bm.to_mesh(mesh)
        car.bm.free()
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(40)
        mesh.validate()
        mesh.update()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = root
        print("    %-8s треуг. %4d" % (name, zm.triangle_count(mesh)))
    return root.name


def check(obj):
    """Внутрь лотка ничего не заходит (выше сукна), наружу — не дальше предела, высота и треугольники в бюджете."""
    mesh = obj.data
    tris = zm.triangle_count(mesh)
    xs = [v.co for v in mesh.vertices]
    inside = sum(1 for p in xs if abs(p.x) < IX - TOLERANCE and abs(p.y) < IY - TOLERANCE and p.z > 0.02)
    outside = sum(1 for p in xs if abs(p.x) > OX + TOLERANCE or abs(p.y) > OY + TOLERANCE)
    height = max(p.z for p in xs)
    ok = tris <= MAX_TRIS and inside == 0 and outside == 0 and height <= MAX_HEIGHT
    print("  %-14s треуг. %5d  высота %.2f  внутри %d  снаружи %d  %s" % (obj.name, tris, height, inside, outside,
                                                                           "OK" if ok else "ПРОВЕРИТЬ"))
    return ok


def render(out_dir, objs):
    """Превью: каждая рамка на столе-заглушке с сукном, стаканами и костями, ракурс игрока."""
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
    if os.path.exists(die_path):
        for k, (x, y) in enumerate(((-0.6, 0.2), (0.1, -0.3), (0.7, 0.4), (-0.9, 1.45), (-0.5, 1.45))):
            bpy.ops.import_scene.fbx(filepath=die_path)
            o = [x for x in bpy.context.selected_objects if x.type == "MESH"][0]
            o.location = (x, y, 0.02 + 0.12)
            o.scale = (0.8, 0.8, 0.8)
            o.rotation_euler = (0, 0, 0.3 * k)

    def plane(name, hx, hy, z, color):
        me = bpy.data.meshes.new(name)
        me.from_pydata([(-hx, -hy, z), (hx, -hy, z), (hx, hy, z), (-hx, hy, z)], [], [(0, 1, 2, 3)])
        mt = bpy.data.materials.new(name + "Mat")
        mt.use_nodes = True
        mt.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color
        me.materials.append(mt)
        scene.collection.objects.link(bpy.data.objects.new(name, me))

    plane("Felt", IX, IY, 0.01, (0.05, 0.3, 0.12, 1))
    plane("Table", 2.8, 2.0, 0.0, (0.45, 0.28, 0.14, 1))
    plane("Floor", 20, 20, -1.6, (0.22, 0.2, 0.18, 1))
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
    scene.eevee.taa_render_samples = 24
    scene.eevee.use_gtao = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.view_settings.view_transform = "Standard"
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 28
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = (("play", Vector((0.0, 4.4, 2.9)), Vector((0.0, 0.2, -0.2))),
             ("close", Vector((1.2, 2.6, 1.0)), Vector((0.3, 0.4, 0.05))),
             ("top", Vector((0.0, 0.6, 4.2)), Vector((0.0, 0.3, 0.0))))
    def cars_of(obj):
        name = obj.get("extras")
        return list(bpy.data.objects[name].children) if name else []

    for obj in objs:
        obj.hide_render = True
        for car in cars_of(obj):
            car.hide_render = True
    for obj in objs:
        obj.hide_render = False
        # вагоны на ближней стороне, едут к −X (вперёд у модели — −Y): поворот на −90°
        x = 0.1
        for car, length in zip(cars_of(obj), (0.26, 0.13, 0.18)):
            car.hide_render = False
            car.location = (x + length / 2, CY, RAIL_TOP)
            car.rotation_euler = (0, 0, -math.pi / 2)
            x += length + 0.02
        for name, loc, target in views:
            cam.location = loc
            cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, obj.name + "_" + name + ".png")
            bpy.ops.render.render(write_still=True)
        for car in cars_of(obj):
            car.hide_render = True
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
    print("Рамки лотка:")
    for make in RIMS:
        model = make()
        if only and model.name not in only:
            model.bm.free()
            continue
        obj = build(model)
        all_ok &= check(obj)
        zm.export_fbx(obj, os.path.join(MODELS_DIR, obj.name + ".fbx"))
        if obj.get("extras"):
            cars = bpy.data.objects[obj["extras"]]
            zm.export_fbx(cars, os.path.join(MODELS_DIR, cars.name + ".fbx"), with_children=True)
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not all_ok:
        raise SystemExit("Есть рамки с ошибками")


if __name__ == "__main__":
    main()
