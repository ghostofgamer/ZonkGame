# -*- coding: utf-8 -*-
"""
Рамки аватарки для «Зонка» в стиле комикса (как аватарки zonk_avatars.py): кольцо поверх края аватарки (r 102…124),
украшения уходят в углы квадрата — ушки, рога, паутина, щупальца видны за кругом. Середина прозрачная.

Новые (с характером):   frame_cat_ears, frame_hearts, frame_flowers, frame_horns, frame_web, frame_bones,
                        frame_tentacles, frame_slime, frame_crown_laurel, frame_lightning, frame_ice, frame_neon,
                        frame_pirate, frame_cards
Старые (ID прежние, перерисованы): frame_wood, frame_silver, frame_rope, frame_dots, frame_double, frame_emerald,
                        frame_ruby, frame_gold, frame_fire, frame_royal, frame_chain, frame_laurel, frame_pips, frame_stars,
                        frame_s1_captain, frame_s1_rope, frame_s1_sea

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_frames.py [-- --only frame_horns,frame_web]
Результат: Assets/ZonkContent/Art/Sprites/Frames/<id>.png, 256×256.
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_ui as ui  # noqa: E402
import zonk_avatars as av  # noqa: E402
from zonk_ui import INK, smooth, compose, dots  # noqa: E402
from zonk_avatars import X, Y, C0, rgb, ell, poly, line, arc, union, WHITE, BLACK  # noqa: E402

OUT = os.path.join(ui.SPRITES, "Frames")
R = np.hypot(X - C0, Y - C0)
A = np.arctan2(Y - C0, X - C0)
GOLD = rgb(1.0, 0.78, 0.22)


class Frame:
    """Слои рамки снизу вверх: фигуры с контуром и тенью-полумесяцем, детали без контура; фон прозрачный."""

    def __init__(self):
        self.layers = []

    def shape(self, sdf, color, ink=5.0, cel=True, dark=None):
        self.layers.append((INK, smooth(0, sdf - ink)))
        self.layers.append((color, smooth(0, sdf)))
        if cel:
            dark = color * 0.66 if dark is None else dark
            inside = smooth(0, sdf)
            lit = smooth(0, np.roll(sdf, (-8, -8), axis=(0, 1)))
            self.layers.append((dark, inside * (1 - lit)))

    def flat(self, sdf, color):
        self.layers.append((color, smooth(0, sdf)))

    def ink(self, sdf):
        self.layers.append((INK, smooth(0, sdf)))

    def image(self):
        return compose(*self.layers)


def band(r0=104.0, r1=122.0):
    """Кольцо рамки: расстояние (меньше нуля внутри)."""
    return np.abs(R - (r0 + r1) / 2) - (r1 - r0) / 2


def at(angle_deg, radius):
    a = math.radians(angle_deg)
    return C0 + math.cos(a) * radius, C0 + math.sin(a) * radius


def ring(f, color, r0=104.0, r1=122.0, dark=None):
    f.shape(band(r0, r1), color, dark=dark)


def ring_pattern(f, color, alt, count, r0=104.0, r1=122.0):
    """Кольцо с полосками поперёк (верёвка, лавр, сегменты)."""
    ring(f, color, r0, r1)
    stripes = (np.floor((A + math.pi) / (2 * math.pi) * count * 2) % 2 == 0)
    f.layers.append((alt, smooth(0, band(r0 + 3, r1 - 3)) * stripes))


def gem(f, cx, cy, r, color):
    f.shape(ell(cx, cy, r, r), color, ink=3)
    f.flat(ell(cx - r * 0.35, cy - r * 0.35, r * 0.3, r * 0.3), rgb(1.0, 1.0, 1.0))


# ---------------------------------------------------------------------------
# Новые: с характером
# ---------------------------------------------------------------------------

def frame_cat_ears():
    """Кошачьи ушки в верхних углах, розовое кольцо, лапки-подушечки внизу."""
    f = Frame()
    pink, inner = rgb(1.0, 0.6, 0.72), rgb(1.0, 0.82, 0.88)
    for s in (-1, 1):
        f.shape(poly((C0 + s * 52, 36), (C0 + s * 118, 4), (C0 + s * 104, 82)), pink)
        f.flat(poly((C0 + s * 64, 38), (C0 + s * 108, 16), (C0 + s * 98, 66)), inner)
    ring(f, pink)
    for cx in (C0 - 30, C0 + 30):
        f.shape(ell(cx, 236, 13, 11), inner, ink=3)
        for dx in (-9, 0, 9):
            f.flat(ell(cx + dx, 224, 3.5, 3.5), pink * 0.8)
    return f.image()


def frame_hearts():
    """Кольцо из сердечек разного размера."""
    f = Frame()
    ring(f, rgb(0.95, 0.35, 0.5), 108, 118)
    for q in range(12):
        cx, cy = at(q * 30 - 90, 113)
        s = 13 if q % 2 == 0 else 9
        f.shape(union(ell(cx - s * 0.5, cy - s * 0.2, s * 0.6, s * 0.6), ell(cx + s * 0.5, cy - s * 0.2, s * 0.6, s * 0.6),
                      poly((cx - s * 1.05, cy), (cx + s * 1.05, cy), (cx, cy + s * 1.2))), rgb(0.9, 0.1, 0.25), ink=3)
    return f.image()


def frame_flowers():
    """Зелёный стебель кольцом, ромашки и листья, в углах крупные цветы."""
    f = Frame()
    ring(f, rgb(0.3, 0.65, 0.3), 109, 117)
    for q in range(10):
        cx, cy = at(q * 36 - 72, 113)
        for k in range(5):
            a = 2 * math.pi * k / 5 + q
            f.shape(ell(cx + math.cos(a) * 8, cy + math.sin(a) * 8, 6, 6), WHITE, ink=2.5, cel=False)
        f.flat(ell(cx, cy, 5, 5), rgb(1.0, 0.8, 0.2))
    for cx, cy in ((34, 34), (222, 222)):
        for k in range(6):
            a = 2 * math.pi * k / 6
            f.shape(ell(cx + math.cos(a) * 14, cy + math.sin(a) * 14, 11, 11), rgb(1.0, 0.55, 0.7), ink=3, cel=False)
        f.shape(ell(cx, cy, 10, 10), rgb(1.0, 0.85, 0.25), ink=3)
    return f.image()


def frame_horns():
    """Рога демона растут из кольца в верхние углы (изогнутые, с кольцами), красное кольцо, язычки огня внизу."""
    f = Frame()
    red = rgb(0.8, 0.1, 0.1)
    ring(f, red)
    for s in (-1, 1):
        bx, by = C0 + s * 70, 38
        pts = []
        for q in range(8):
            t = q / 7
            x = bx + s * (52 * t + 10 * math.sin(t * math.pi))
            y = by - 40 * t + 18 * t * t
            pts.append((x, y, 17 * (1 - t) + 3))
        horn = union(*[ell(x, y, w, w) for x, y, w in pts])
        f.shape(horn, rgb(0.3, 0.22, 0.2))
        for x, y, w in pts[1:6]:
            f.ink(np.maximum(np.abs((X - x) * 0.7 - (Y - y) * 0.7 * s) - 1.4, ell(x, y, w + 1, w + 1)))
    for q in range(9):
        cx, cy = at(40 + q * 12.5, 118)
        f.shape(poly((cx - 10, cy - 2), (cx + 10, cy - 2), (cx + (q % 3 - 1) * 5, cy + 22)), rgb(1.0, 0.45, 0.08), ink=3)
    return f.image()


def frame_web():
    """Паутина в левом верхнем углу, паук на нити справа, тёмное кольцо."""
    f = Frame()
    ring(f, rgb(0.25, 0.22, 0.3))
    web_ink = rgb(0.92, 0.92, 0.96)
    for k in range(6):
        a = math.radians(180 + k * 18)
        f.flat(np.maximum(line(8, 8, 8 + math.cos(a + math.pi) * 120, 8 + math.sin(a + math.pi) * 120, 1.4), 106 - R), web_ink)
    for rr in (30, 55, 80, 105):
        f.flat(np.maximum(np.maximum(np.abs(np.hypot(X - 8, Y - 8) - rr) - 1.2, -np.minimum(X, Y)), 106 - R), web_ink)  # только снаружи кольца
    f.flat(line(218, 0, 218, 52, 1.2), web_ink)
    f.shape(ell(218, 62, 11, 13), BLACK, ink=2)
    for s in (-1, 1):
        for k in range(4):
            f.ink(line(218, 60 + k * 4, 218 + s * 20, 52 + k * 8, 1.8))
    f.flat(ell(214, 58, 2.5, 2.5), rgb(1.0, 0.2, 0.2))
    f.flat(ell(222, 58, 2.5, 2.5), rgb(1.0, 0.2, 0.2))
    return f.image()


def frame_bones():
    """Кольцо из косточек, внизу череп, в углах скрещённые кости."""
    f = Frame()
    bone = rgb(0.95, 0.92, 0.82)

    def one_bone(ax, ay, bx, by, w=6):
        return union(line(ax, ay, bx, by, w), ell(ax + (ay - by) * 0.0, ay, w * 1.3, w * 1.3), ell(bx, by, w * 1.3, w * 1.3))

    bones = []
    for q in range(10):
        a0, a1 = q * 36 - 90 + 6, q * 36 - 90 + 30
        bones.append(one_bone(*at(a0, 113), *at(a1, 113)))
    f.shape(union(*bones), bone, dark=rgb(0.66, 0.6, 0.52))
    for cx, cy in ((26, 230), (230, 230)):
        f.shape(union(one_bone(cx - 20, cy - 20, cx + 20, cy + 20, 5), one_bone(cx - 20, cy + 20, cx + 20, cy - 20, 5)), bone, ink=3)
    f.shape(union(ell(C0, 226, 22, 19), np.maximum(np.abs(X - C0) - 12, np.abs(Y - 246) - 8)), bone, ink=3.5)
    for s in (-1, 1):
        f.flat(ell(C0 + s * 9, 226, 6, 7), BLACK)
    return f.image()


def frame_tentacles():
    """Фиолетовые щупальца с присосками обвивают кольцо и закручиваются в углах."""
    f = Frame()
    purple, light = rgb(0.55, 0.25, 0.7), rgb(0.85, 0.65, 0.95)
    ring(f, purple, 106, 120)
    for corner, (cx, cy) in enumerate(((30, 30), (226, 30), (30, 226), (226, 226))):
        pts = []
        for k in range(14):
            t = k / 13
            a = math.radians([225, 315, 135, 45][corner]) + t * 4.2
            rr = 30 * (1 - t) + 4
            pts.append((cx + math.cos(a) * rr * (1 - 0.3 * t), cy + math.sin(a) * rr * (1 - 0.3 * t), 9 * (1 - t) + 2.5))
        body = union(*[ell(x, y, w, w) for x, y, w in pts])
        f.shape(body, purple)
        for x, y, w in pts[1:-3:2]:
            f.flat(ell(x, y, w * 0.4, w * 0.4), light)
    for q in range(16):
        cx, cy = at(q * 22.5, 113)
        f.flat(ell(cx, cy, 3.5, 3.5), light)
    return f.image()


def frame_slime():
    """Зелёная слизь: кольцо с каплями, стекающими вниз."""
    f = Frame()
    green = rgb(0.45, 0.88, 0.25)
    shape = band(104, 120)
    for q, ang in enumerate((20, 50, 75, 105, 130, 160)):
        x0, y0 = at(ang, 116)
        length = [24, 36, 18, 40, 28, 22][q]
        shape = union(shape, np.maximum(np.abs(X - x0) - 6, np.maximum(y0 - Y, Y - (y0 + length))), ell(x0, y0 + length, 8, 9))
    f.shape(shape, green, dark=rgb(0.25, 0.6, 0.12))
    for q in range(8):
        cx, cy = at(q * 45 + 200, 112)
        f.flat(ell(cx, cy, 3, 2), rgb(0.85, 1.0, 0.7))
    return f.image()


def frame_crown_laurel():
    """Золотой лавр двумя ветвями и корона сверху — статус."""
    f = Frame()
    leaves_ring(f, (GOLD, rgb(1.0, 0.88, 0.45)))
    f.shape(poly((C0 - 40, 34), (C0 - 40, 4), (C0 - 20, 20), (C0, 0), (C0 + 20, 20), (C0 + 40, 4), (C0 + 40, 34)), GOLD)
    for dx, col in ((-20, rgb(0.2, 0.45, 0.95)), (0, rgb(0.9, 0.1, 0.15)), (20, rgb(0.15, 0.75, 0.35))):
        f.flat(ell(C0 + dx, 26, 4, 4), col)
    return f.image()


def frame_flames():
    """Огненное кольцо: языки пламени наружу, ярче у основания."""
    f = Frame()
    tongues_o, tongues_i = [], []
    rng = np.random.RandomState(5)
    for q in range(18):
        ang = q * 20 + rng.uniform(-4, 4)
        base = at(ang, 112)
        tip = at(ang + rng.uniform(-8, 8), 140 + rng.uniform(-6, 12))
        n = math.radians(ang + 90)
        tongues_o.append(poly((base[0] + math.cos(n) * 14, base[1] + math.sin(n) * 14),
                              (base[0] - math.cos(n) * 14, base[1] - math.sin(n) * 14), tip))
        mid = at(ang, 126)
        tongues_i.append(poly((base[0] + math.cos(n) * 7, base[1] + math.sin(n) * 7),
                              (base[0] - math.cos(n) * 7, base[1] - math.sin(n) * 7), mid))
    f.shape(union(band(104, 120), *tongues_o), rgb(1.0, 0.42, 0.06), dark=rgb(0.8, 0.2, 0.02))
    f.flat(union(band(108, 116), *tongues_i), rgb(1.0, 0.85, 0.3))
    return f.image()


def frame_ice():
    """Ледяное кольцо с кристаллами и сосульками внизу."""
    f = Frame()
    ice, light = rgb(0.65, 0.88, 1.0), rgb(0.92, 0.98, 1.0)
    crystals = []
    for q in range(8):
        ang = q * 45 - 67.5
        cx, cy = at(ang, 116)
        a = math.radians(ang)
        crystals.append(poly((cx - math.sin(a) * 9, cy + math.cos(a) * 9), (cx + math.sin(a) * 9, cy - math.cos(a) * 9),
                             (cx + math.cos(a) * 26, cy + math.sin(a) * 26)))
    for q, ang in enumerate((60, 80, 100, 120)):
        x0, y0 = at(ang, 120)
        crystals.append(poly((x0 - 7, y0 - 2), (x0 + 7, y0 - 2), (x0, y0 + [18, 26, 22, 14][q])))
    f.shape(union(band(104, 120), *crystals), ice, dark=rgb(0.4, 0.65, 0.88))
    f.flat(np.maximum(band(107, 110), -np.sin(A * 3) - 0.2), light)
    return f.image()


def frame_neon():
    """Неон: два кольца — розовое и голубое — с ореолом свечения."""
    f = Frame()
    f.flat(np.abs(R - 113) - 14, rgb(1.0, 0.4, 0.8) * 0.35)
    for rr, col in ((108, rgb(1.0, 0.3, 0.75)), (119, rgb(0.3, 0.95, 1.0))):
        f.shape(np.abs(R - rr) - 3.5, col, ink=2.5, cel=False)
        f.flat(np.abs(R - rr) - 1.2, rgb(1.0, 1.0, 1.0))
    for cx, cy in (at(-45, 113), at(135, 113)):
        f.flat(av.poly(*ui.regular_star(cx, cy, 12, 3, rays=4)), WHITE)
    return f.image()


def frame_pirate():
    """Пиратская: канат кольцом, внизу якорь, сверху скрещённые кости и череп поверх них."""
    f = Frame()
    ring_pattern(f, rgb(0.82, 0.68, 0.42), rgb(0.6, 0.45, 0.24), 28)
    anchor = union(np.maximum(np.abs(X - C0) - 5, np.abs(Y - 226) - 22), np.maximum(np.abs(X - C0) - 18, np.abs(Y - 210) - 4),
                   np.maximum(np.abs(np.hypot(X - C0, Y - 228) - 20) - 5, 228 - Y))
    f.shape(anchor, rgb(0.7, 0.72, 0.78), ink=3.5)
    for s in (-1, 1):
        f.shape(union(line(C0 + s * 32, 6, C0 - s * 32, 42, 4.5), ell(C0 + s * 34, 6, 6, 6), ell(C0 - s * 34, 42, 6, 6)), WHITE,
                ink=2.5, cel=False)
    f.shape(union(ell(C0, 22, 17, 15), np.maximum(np.abs(X - C0) - 9, np.abs(Y - 36) - 6)), WHITE, ink=3.5)
    for s in (-1, 1):
        f.flat(ell(C0 + s * 6, 22, 4.5, 5), BLACK)
    return f.image()


def frame_cards():
    """Карты по углам — четыре масти, золотое кольцо."""
    f = Frame()
    ring(f, GOLD, 108, 118)
    for (ang, kind, col) in ((-135, "spade", rgb(0.1, 0.1, 0.12)), (-45, "heart", rgb(0.85, 0.1, 0.15)),
                             (45, "club", rgb(0.1, 0.1, 0.12)), (135, "diamond", rgb(0.85, 0.1, 0.15))):
        cx, cy = at(ang, 128)
        a = math.radians(ang + 90)
        card = np.maximum(np.abs((X - cx) * math.cos(a) + (Y - cy) * math.sin(a)) - 18,
                          np.abs(-(X - cx) * math.sin(a) + (Y - cy) * math.cos(a)) - 26)
        f.shape(card, WHITE, ink=3.5, dark=rgb(0.82, 0.84, 0.88))
        f.flat(suit(kind, cx, cy, 10), col)
    return f.image()


def plain(color):
    def make():
        f = Frame()
        ring(f, color)
        f.flat(np.abs(R - 108) - 1.5, color * 1.15)
        return f.image()
    return make


def rope(color, dark):
    def make():
        f = Frame()
        ring(f, color)
        twist = np.sin((A * 24) + R * 0.35) > 0.3
        f.layers.append((dark, smooth(0, band(105, 121)) * twist))
        return f.image()
    return make


def dotted(color, dot):
    def make():
        f = Frame()
        ring(f, color)
        for q in range(12):
            cx, cy = at(q * 30, 113)
            f.shape(ell(cx, cy, 5, 5), dot, ink=2.5, cel=False)
        return f.image()
    return make


def double(color, dark):
    def make():
        f = Frame()
        ring(f, color, 116, 124)
        ring(f, dark, 104, 111)
        return f.image()
    return make


def gems(color, stone, count=4):
    def make():
        f = Frame()
        ring(f, color)
        for q in range(count):
            cx, cy = at(q * 360 / count - 90, 113)
            gem(f, cx, cy, 12 if count == 4 else 9, stone)
        return f.image()
    return make


def frame_fire_old():
    return frame_flames()


def frame_royal():
    f = Frame()
    ring(f, rgb(0.45, 0.15, 0.6))
    f.flat(np.abs(R - 106) - 2, GOLD)
    f.flat(np.abs(R - 120) - 2, GOLD)
    f.shape(poly((C0 - 32, 26), (C0 - 32, 2), (C0 - 16, 14), (C0, 0), (C0 + 16, 14), (C0 + 32, 2), (C0 + 32, 26)), GOLD, ink=4)
    return f.image()


def frame_chain():
    """Цепь: звенья по касательной через одно — плашмя и ребром, внахлёст."""
    f = Frame()
    for q in range(20):
        ang = q * 18
        cx, cy = at(ang, 113)
        if q % 2 == 0:
            link = np.abs(ell(cx, cy, 13, 8, rot=ang + 90)) - 3
        else:
            link = ell(cx, cy, 11, 3.5, rot=ang + 90)
        f.shape(link, rgb(0.75, 0.77, 0.82), ink=2.5)
    return f.image()


def frame_laurel():
    f = Frame()
    leaves_ring(f, (rgb(0.3, 0.6, 0.25), rgb(0.45, 0.75, 0.3)))
    return f.image()


def frame_pips():
    f = Frame()
    ring(f, rgb(0.95, 0.92, 0.84))
    for q in range(6):
        cx, cy = at(q * 60 - 90, 113)
        f.shape(np.maximum(np.abs(X - cx) - 12, np.abs(Y - cy) - 12), WHITE, ink=3)
        for k in range(q % 3 + 1):
            f.flat(ell(cx - 5 + k * 5, cy - 5 + k * 5, 2.4, 2.4), rgb(0.7, 0.1, 0.1))
    return f.image()


def frame_stars():
    f = Frame()
    ring(f, rgb(0.1, 0.12, 0.35))
    f.flat(np.abs(R - 104) - 1.5, GOLD)
    for q in range(8):
        cx, cy = at(q * 45 - 90, 113)
        f.shape(av.poly(*ui.regular_star(cx, cy, 10, 4)), GOLD, ink=2.5, cel=False)
    return f.image()


def suit(kind, cx, cy, s):
    """Масть: spade, heart, club, diamond — фигура размера s."""
    if kind == "heart":
        return union(ell(cx - s * 0.5, cy - s * 0.25, s * 0.55, s * 0.55), ell(cx + s * 0.5, cy - s * 0.25, s * 0.55, s * 0.55),
                     poly((cx - s * 1.0, cy - s * 0.05), (cx + s * 1.0, cy - s * 0.05), (cx, cy + s * 1.05)))
    if kind == "spade":
        return union(ell(cx - s * 0.5, cy + s * 0.2, s * 0.55, s * 0.55), ell(cx + s * 0.5, cy + s * 0.2, s * 0.55, s * 0.55),
                     poly((cx - s * 1.0, cy + s * 0.1), (cx + s * 1.0, cy + s * 0.1), (cx, cy - s * 1.05)),
                     poly((cx - s * 0.3, cy + s * 1.0), (cx + s * 0.3, cy + s * 1.0), (cx, cy + s * 0.3)))
    if kind == "club":
        return union(ell(cx, cy - s * 0.5, s * 0.48, s * 0.48), ell(cx - s * 0.55, cy + s * 0.15, s * 0.48, s * 0.48),
                     ell(cx + s * 0.55, cy + s * 0.15, s * 0.48, s * 0.48),
                     poly((cx - s * 0.3, cy + s * 1.0), (cx + s * 0.3, cy + s * 1.0), (cx, cy)))
    return poly((cx, cy - s), (cx + s * 0.7, cy), (cx, cy + s), (cx - s * 0.7, cy))


def frame_lightning():
    """Молнии: тёмно-фиолетовое кольцо, крупные зигзаги в углах, мелкие искры по кольцу."""
    f = Frame()
    ring(f, rgb(0.3, 0.15, 0.5))
    bolts = []
    for ang in (45, 135, 225, 315):
        a = math.radians(ang)
        n = a + math.pi / 2
        steps = ((112, 0), (132, 14), (138, -10), (160, 8), (176, 0))
        pts = [(C0 + math.cos(a) * rr + math.cos(n) * side, C0 + math.sin(a) * rr + math.sin(n) * side) for rr, side in steps]
        bolts.append(union(*[line(pts[k][0], pts[k][1], pts[k + 1][0], pts[k + 1][1], 7 - k * 1.3) for k in range(4)]))
    f.shape(union(*bolts), rgb(1.0, 0.88, 0.2), ink=3.5)
    for q in range(16):
        cx, cy = at(q * 22.5, 113)
        f.flat(ell(cx, cy, 2.5, 2.5), rgb(0.85, 0.75, 1.0))
    return f.image()


def leaves_ring(f, colors, radius=115, count=14, size=(15, 7)):
    """Две ветви лавра снизу вверх: стебель по кольцу, листья внахлёст по касательной, кончиками вверх."""
    stem = np.maximum(np.abs(R - radius) - 2.2, -np.sin(A) - 0.92)                        # без самого верха
    f.ink(stem - 1.5)
    f.flat(stem, colors[0] * 0.7)
    for s in (-1, 1):
        for q in range(count):
            ang = 90 + s * (14 + q * (156 / count))
            for side in (-1, 1):
                cx, cy = at(ang, radius + side * 9)
                f.shape(ell(cx, cy, size[0], size[1], rot=ang + 90 + s * side * 35), colors[(q + (side > 0)) % 2], ink=2.5,
                        cel=False)


FRAMES = {
    "frame_cat_ears": frame_cat_ears, "frame_hearts": frame_hearts, "frame_flowers": frame_flowers, "frame_horns": frame_horns,
    "frame_web": frame_web, "frame_bones": frame_bones, "frame_tentacles": frame_tentacles, "frame_slime": frame_slime,
    "frame_crown_laurel": frame_crown_laurel, "frame_lightning": frame_lightning, "frame_ice": frame_ice, "frame_neon": frame_neon,
    "frame_pirate": frame_pirate, "frame_cards": frame_cards,
    "frame_wood": plain(rgb(0.5, 0.32, 0.16)), "frame_silver": plain(rgb(0.85, 0.87, 0.92)),
    "frame_rope": rope(rgb(0.82, 0.68, 0.42), rgb(0.55, 0.42, 0.22)), "frame_dots": dotted(rgb(0.25, 0.45, 0.85), WHITE),
    "frame_double": double(rgb(0.3, 0.75, 0.55), rgb(0.12, 0.35, 0.25)),
    "frame_emerald": gems(rgb(0.75, 0.78, 0.82), rgb(0.15, 0.8, 0.4)), "frame_ruby": gems(rgb(0.35, 0.3, 0.3), rgb(0.9, 0.12, 0.2)),
    "frame_gold": gems(GOLD, rgb(0.3, 0.6, 1.0), 8), "frame_fire": frame_fire_old, "frame_royal": frame_royal,
    "frame_chain": frame_chain, "frame_laurel": frame_laurel, "frame_pips": frame_pips, "frame_stars": frame_stars,
    "frame_s1_captain": gems(GOLD, rgb(0.85, 0.1, 0.12), 8), "frame_s1_rope": rope(rgb(0.45, 0.3, 0.15), rgb(0.25, 0.15, 0.06)),
    "frame_s1_sea": dotted(rgb(0.1, 0.45, 0.6), rgb(0.85, 0.95, 1.0)),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(FRAMES)
    for name in only:
        ui.save(name, FRAMES[name](), OUT)


if __name__ == "__main__":
    main()
