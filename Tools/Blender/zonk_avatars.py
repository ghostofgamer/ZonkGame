# -*- coding: utf-8 -*-
"""
Аватарки игрока для «Зонка» в стиле комикса (как интерфейс, zonk_ui.py): портреты персонажей на круглом фоне
с лучами, чернильный контур, растр в тенях, жёсткая тень, блики в глазах.

Наборы (добавляются по одному, после приёма автором):
    Звери-игроки  avatar_cat (шулер), avatar_dog (вышибала), avatar_fox (аферист), avatar_panda (громила),
                  avatar_hamster (богач), avatar_owl (крупье), avatar_frog (везунчик), avatar_bunny (фокусник)

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_avatars.py [-- --only avatar_cat,avatar_fox]
Результат: Assets/ZonkContent/Art/Sprites/Avatars/<id>.png, 256×256, вне круга прозрачно (рамка кладётся поверх).
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_ui as ui  # noqa: E402
from zonk_ui import INK, grid, smooth, compose, dots, capsule, polygon_sdf  # noqa: E402

OUT = os.path.join(ui.SPRITES, "Avatars")
SIZE = 256
C0 = 128.0
RADIUS = 122.0
WHITE = np.array([1.0, 1.0, 1.0])
BLACK = np.array([0.08, 0.06, 0.05])
PINK = np.array([1.0, 0.55, 0.62])

X, Y = grid(SIZE, SIZE)


def rgb(r, g, b):
    return np.array([r, g, b])


def ell(cx, cy, rx, ry, rot=0.0):
    """Эллипс (приближённое расстояние, меньше нуля внутри); rot — поворот в градусах."""
    a = math.radians(rot)
    u = (X - cx) * math.cos(a) + (Y - cy) * math.sin(a)
    v = -(X - cx) * math.sin(a) + (Y - cy) * math.cos(a)
    return (np.hypot(u / rx, v / ry) - 1.0) * min(rx, ry)


def poly(*points):
    return polygon_sdf(X, Y, list(points))


def line(ax, ay, bx, by, w):
    return capsule(X, Y, ax, ay, bx, by, w)


def arc(cx, cy, r, a0, a1, w):
    """Дуга окружности от угла a0 до a1 (градусы, 0 — вправо, по часовой — вниз)."""
    d = np.abs(np.hypot(X - cx, Y - cy) - r) - w
    ang = np.degrees(np.arctan2(Y - cy, X - cx))
    lo, hi = min(a0, a1), max(a0, a1)
    return np.where((ang >= lo) & (ang <= hi), d, 1e3)


def union(*shapes):
    out = shapes[0]
    for s in shapes[1:]:
        out = np.minimum(out, s)
    return out


class Portrait:
    """
    Слои портрета снизу вверх: фон-взрыв, фигуры с контуром и жёсткой тенью двумя тонами (свет сверху-слева),
    детали без контура. Всё обрезано кругом.
    """

    def __init__(self, bg, bg_dark):
        self.layers = []
        circle = np.hypot(X - C0, Y - C0) - RADIUS
        ang = np.arctan2(Y - C0, X - C0)
        r = np.hypot(X - C0, Y - C0)
        rays = (np.floor((ang + math.pi) / (2 * math.pi) * 22) % 2 == 0)
        back = np.where(rays[..., None], bg, bg * 0.82 + bg_dark * 0.18)
        back = np.where((r > 96)[..., None], back * 0.85 + bg_dark * 0.15, back)               # темнее к краю
        halftone = (r > 88) & (dots(X, Y, 8, 2.2 * np.clip((r - 88) / 34, 0, 1) + 0.4) > 0.5)
        back = np.where(halftone[..., None], bg_dark, back)
        self.clip = smooth(0, circle + 6)
        self.layers.append((INK, smooth(0, circle)))
        self.layers.append((back, smooth(0, circle + 6)))

    def shape(self, sdf, color, ink=5.0, cel=None, dark=None):
        """
        Фигура с чернильным контуром. cel — центр (cx, cy) и радиус: всё правее-ниже линии терминатора уходит в
        жёсткую тень цвета dark (по умолчанию — цвет, затемнённый на треть), у края тени — полоска растра.
        """
        self.layers.append((INK, smooth(0, sdf - ink)))
        self.layers.append((color, smooth(0, sdf)))
        if cel is not None:
            # Тень полумесяцем по форме: часть фигуры, которая не попадает в ту же фигуру, сдвинутую к свету
            # (вверх-влево). Прямая диагональ через всё лицо выглядела наклейкой (разбор 06.11).
            dark = color * 0.66 if dark is None else dark
            d = 18
            inside = smooth(0, sdf)
            lit = smooth(0, np.roll(sdf, (-d, -d), axis=(0, 1)))
            self.layers.append((dark, inside * (1 - lit)))
            lit_more = smooth(0, np.roll(sdf, (-d - 10, -d - 10), axis=(0, 1)))
            band = (dots(X, Y, 6, 1.6) > 0.5)
            self.layers.append((dark, inside * lit * (1 - lit_more) * band))       # растр у края тени

    def flat(self, sdf, color):
        """Деталь без контура (полоска, блик, пятно)."""
        self.layers.append((color, smooth(0, sdf)))

    def ink(self, sdf):
        self.layers.append((INK, smooth(0, sdf)))

    def eye(self, cx, cy, r, lid=0.35, brow=15.0, look=(0.0, 0.0), iris=None, side=1):
        """
        Глаз с характером: миндалина (не круглая детская), веко срезает верх на lid, зрачок небольшой, один блик,
        бровь — толстый штрих под углом brow (плюс — хмурится к переносице). side — левый (-1) или правый (1).
        """
        white = ell(cx, cy, r, r * 0.72)
        lid_y = cy - r * 0.72 + lid * r * 1.44
        self.shape(white, rgb(0.98, 0.96, 0.9), ink=3.5)
        px, py = cx + look[0] * r * 0.35, cy + look[1] * r * 0.25 + r * 0.05
        if iris is not None:
            self.layers.append((iris, smooth(0, ell(px, py, r * 0.5, r * 0.55)) * smooth(0, white)))
        self.layers.append((BLACK, smooth(0, ell(px, py, r * 0.3, r * 0.36)) * smooth(0, white)))
        self.layers.append((WHITE, smooth(0, ell(px - r * 0.12, py - r * 0.14, r * 0.1, r * 0.1)) * smooth(0, white)))
        self.ink(np.maximum(white - 0.5, Y - lid_y))                         # веко: верх глаза закрашен чернилом
        self.ink(np.maximum(np.abs(Y - lid_y) - 2.4, white - 2))
        a = math.radians(brow) * side
        bx, by = cx, cy - r * 1.05
        dx, dy = math.cos(a) * r * 1.05, math.sin(a) * r * 1.05
        self.ink(line(bx - dx, by - dy, bx + dx, by + dy, 5.0))

    def wink(self, cx, cy, r, side=1):
        """Подмигивание: глаз-дуга и бровь."""
        self.ink(arc(cx, cy - r * 0.2, r * 0.9, 20, 160, 3.5))
        self.ink(line(cx - r, cy - r * 1.0 + side * 3, cx + r, cy - r * 1.1 - side * 3, 4.5))

    def smirk(self, cx, cy, w, up=1):
        """Ухмылка: кривая линия, один уголок вверх (up = 1 — правый)."""
        self.ink(arc(cx, cy - w * 0.9, w * 1.2, 55, 125, 3.2))
        self.ink(line(cx + up * w * 0.62, cy - w * 0.15, cx + up * w * 0.95, cy - w * 0.45, 3.2))

    def grin(self, cx, cy, w, h=14.0):
        """Оскал: рот-полумесяц с зубами и чёрными промежутками."""
        mouth = np.maximum(ell(cx, cy, w, h), cy - 2 - Y)
        self.shape(mouth, rgb(0.98, 0.96, 0.9), ink=3.5)
        for q in range(-3, 4):
            self.layers.append((INK, smooth(0, np.abs(X - (cx + q * w / 3.5)) - 1.2) * smooth(0, mouth)))
        self.layers.append((INK, smooth(0, np.abs(Y - (cy + h * 0.35)) - 1.2) * smooth(0, mouth)))

    def shades(self, cx, cy, w, h, color=None):
        """Тёмные очки: две линзы с бликом, перемычка."""
        color = BLACK if color is None else color
        for s in (-1, 1):
            lens = ell(cx + s * w, cy, w * 0.92, h)
            self.shape(lens, color, ink=4)
            self.flat(line(cx + s * w - w * 0.5, cy - h * 0.3, cx + s * w - w * 0.1, cy - h * 0.6, 2.6), rgb(0.85, 0.9, 1.0))
        self.ink(line(cx - w * 0.2, cy - h * 0.4, cx + w * 0.2, cy - h * 0.4, 3.5))

    def image(self):
        layers = [(c, a * self.clip) for c, a in self.layers]
        return compose(*layers)


def top_hat(p, cx, cy, w, h, color, band):
    """Цилиндр: поля, тулья, лента."""
    p.shape(ell(cx, cy, w * 1.45, w * 0.32), color, cel=(cx, cy, 10))
    p.shape(np.maximum(np.abs(X - cx) - w, np.maximum(Y - cy, cy - h - Y)), color, cel=(cx, cy - h / 2, 12))
    p.flat(np.maximum(np.abs(X - cx) - w + 1, np.abs(Y - (cy - h * 0.22)) - h * 0.1), band)


# ---------------------------------------------------------------------------
# Звери-игроки (вместо «Милых»: автор — «слишком детские»)
# ---------------------------------------------------------------------------

def avatar_cat():
    """Кот-шулер: зелёный козырёк крупье, прищур, ухмылка, зубочистка, карта за ухом."""
    fur = rgb(1.0, 0.6, 0.2)
    p = Portrait(rgb(0.78, 0.15, 0.15), rgb(0.35, 0.04, 0.06))
    p.shape(np.maximum(poly((C0 + 70, 52), (C0 + 110, 20), (C0 + 118, 96), (C0 + 80, 100)), 0 * X - 1), WHITE, ink=4)
    p.flat(poly(*ui.regular_star(C0 + 96, 52, 9, 4)), rgb(0.85, 0.1, 0.12))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 26, 92), (C0 + s * 84, 34), (C0 + s * 88, 118)), fur, cel=(C0, 110, 30))
        p.flat(poly((C0 + s * 42, 96), (C0 + s * 76, 56), (C0 + s * 78, 108)), rgb(0.9, 0.45, 0.45))
    p.shape(ell(C0, 152, 88, 70), fur, cel=(C0, 152, 30))
    p.shape(np.maximum(ell(C0, 108, 92, 40), Y - 110), rgb(0.15, 0.62, 0.3), ink=4, cel=(C0, 100, 40))   # козырёк
    p.flat(np.maximum(np.abs(Y - 104) - 3, ell(C0, 108, 90, 38)), rgb(0.4, 0.85, 0.5))
    p.flat(ell(C0, 184, 40, 22), rgb(1.0, 0.9, 0.78))
    for s in (-1, 1):
        p.eye(C0 + s * 34, 146, 18, lid=0.45, brow=10, look=(0.4, 0), iris=rgb(0.3, 0.75, 0.25), side=s)
        for dy in (-5, 6):
            p.ink(line(C0 + s * 44, 182 + dy, C0 + s * 96, 176 + dy * 2, 1.4))
    p.shape(poly((C0 - 7, 170), (C0 + 7, 170), (C0, 178)), rgb(0.9, 0.4, 0.45), ink=2.5)
    p.smirk(C0 + 4, 192, 16, up=1)
    p.shape(line(C0 + 14, 190, C0 + 50, 200, 2.6), rgb(0.95, 0.85, 0.6), ink=2)
    return p.image()


def avatar_dog():
    """Пёс-вышибала: тёмные очки, оскал, золотая цепь, обвислые уши."""
    fur, ear = rgb(0.85, 0.66, 0.42), rgb(0.42, 0.25, 0.14)
    p = Portrait(rgb(0.5, 0.25, 0.7), rgb(0.18, 0.06, 0.3))
    p.shape(union(ell(C0, 238, 100, 50)), rgb(0.15, 0.15, 0.18), cel=(C0, 230, 20))           # плечи-пиджак
    for q in range(9):
        p.shape(ell(C0 - 64 + q * 16, 214 + abs(q - 4) * -3 + 6, 9, 6), rgb(1.0, 0.8, 0.25), ink=2.5)
    p.shape(ell(C0, 140, 86, 82), fur, cel=(C0, 140, 30))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 80, 140, 26, 58, rot=s * -14), ear, cel=(C0 + s * 80, 140, 0))
    p.shape(ell(C0, 182, 48, 32), rgb(0.95, 0.85, 0.7), ink=4, cel=(C0, 182, 10))
    p.shades(C0, 128, 32, 18)
    p.ink(line(C0 - 66, 104, C0 - 18, 112, 5))
    p.ink(line(C0 + 66, 104, C0 + 18, 112, 5))
    p.shape(ell(C0, 165, 15, 10), BLACK, ink=2)
    p.flat(ell(C0 - 5, 162, 4, 2.5), WHITE)
    p.grin(C0, 188, 28, 13)
    return p.image()


def avatar_fox():
    """Лис-аферист: монокль на цепочке, поднятая бровь, хитрая ухмылка, высокие уши."""
    fur = rgb(1.0, 0.48, 0.12)
    p = Portrait(rgb(0.1, 0.45, 0.48), rgb(0.02, 0.16, 0.2))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 24, 96), (C0 + s * 80, 22), (C0 + s * 94, 112)), fur, cel=(C0, 90, 30))
        p.flat(poly((C0 + s * 64, 44), (C0 + s * 80, 22), (C0 + s * 86, 58)), rgb(0.15, 0.08, 0.06))
    head = union(ell(C0, 138, 84, 62), poly((C0 - 98, 148), (C0 + 98, 148), (C0, 216)))
    p.shape(head, fur, cel=(C0, 140, 30))
    p.flat(union(poly((C0 - 92, 150), (C0 - 8, 164), (C0, 212)), poly((C0 + 92, 150), (C0 + 8, 164), (C0, 212))),
           rgb(1.0, 0.96, 0.9))
    p.eye(C0 - 34, 132, 17, lid=0.5, brow=12, look=(0.5, 0), iris=rgb(0.95, 0.75, 0.15), side=-1)
    p.eye(C0 + 34, 132, 17, lid=0.2, brow=-14, look=(0.5, 0), iris=rgb(0.95, 0.75, 0.15), side=1)
    p.shape(np.abs(np.hypot(X - (C0 + 34), Y - 132) - 24) - 3, rgb(1.0, 0.8, 0.25), ink=2.5)          # монокль
    p.shape(arc(C0 + 70, 174, 36, -60, 90, 1.8), rgb(1.0, 0.8, 0.25), ink=1.5)
    p.shape(ell(C0, 196, 10, 7), BLACK, ink=2)
    p.smirk(C0 + 6, 210, 14, up=1)
    return p.image()


def avatar_panda():
    """Панда-громила: красная бандана, хмурые брови, пластырь, оскал."""
    p = Portrait(rgb(1.0, 0.55, 0.15), rgb(0.6, 0.18, 0.04))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 66, 74, 28, 28), BLACK)
    p.shape(ell(C0, 148, 90, 78), WHITE, cel=(C0, 148, 35), dark=rgb(0.72, 0.74, 0.8))
    p.shape(np.maximum(np.abs(Y - 92) - 14, ell(C0, 148, 92, 80)), rgb(0.85, 0.1, 0.12), ink=4)       # бандана
    for q in range(5):
        p.flat(ell(C0 - 60 + q * 30, 92, 4, 4), WHITE)
    p.shape(union(poly((C0 + 80, 92), (C0 + 118, 74), (C0 + 112, 96)), poly((C0 + 80, 98), (C0 + 116, 112), (C0 + 104, 120))),
            rgb(0.85, 0.1, 0.12), ink=3.5)
    for s in (-1, 1):
        p.flat(ell(C0 + s * 36, 140, 26, 30, rot=s * -30), BLACK)
        p.eye(C0 + s * 36, 140, 13, lid=0.25, brow=-22, side=s)
        p.flat(line(C0 + s * 20, 128, C0 + s * 52, 116, 3.2), rgb(0.85, 0.85, 0.9))           # брови светлые: на чёрном пятне
    p.shape(np.maximum(np.abs(X - (C0 + 62)) - 14, np.abs(Y - 170) - 6), rgb(0.95, 0.8, 0.62), ink=2.5)  # пластырь
    p.shape(ell(C0, 168, 12, 8), BLACK, ink=2)
    p.grin(C0, 190, 26, 12)
    return p.image()


def avatar_hamster():
    """Хомяк-богач: очки на лбу, золотой зуб, кусает монету, пухлые щёки."""
    fur = rgb(0.95, 0.68, 0.34)
    p = Portrait(rgb(0.25, 0.65, 0.3), rgb(0.06, 0.3, 0.1))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 62, 70, 22, 20), fur)
        p.flat(ell(C0 + s * 62, 72, 10, 9), rgb(0.85, 0.45, 0.45))
    p.shape(union(ell(C0, 140, 94, 78), ell(C0 - 70, 170, 42, 38), ell(C0 + 70, 170, 42, 38)), fur, cel=(C0, 140, 35))
    p.shades(C0, 96, 28, 14, color=rgb(0.2, 0.2, 0.25))
    p.flat(ell(C0, 180, 48, 38), rgb(1.0, 0.94, 0.86))
    for s in (-1, 1):
        p.eye(C0 + s * 34, 138, 14, lid=0.4, brow=-8, look=(0, 0.4), side=s)
    p.shape(ell(C0, 162, 9, 6), rgb(0.85, 0.4, 0.45), ink=2.5)
    p.shape(ell(C0, 200, 22, 22), rgb(1.0, 0.8, 0.25), ink=4, cel=(C0, 200, 6))                  # монета в зубах
    p.flat(poly(*ui.regular_star(C0, 201, 10, 4.5)), rgb(0.75, 0.55, 0.12))
    p.shape(np.maximum(np.abs(X - (C0 - 6)) - 5, np.abs(Y - 180) - 7), rgb(1.0, 0.82, 0.3), ink=2.5)   # золотой зуб
    p.shape(np.maximum(np.abs(X - (C0 + 6)) - 5, np.abs(Y - 180) - 7), WHITE, ink=2.5)
    return p.image()


def avatar_owl():
    """Сова-крупье: строгие брови, круглые очки, бабочка, пёрышки-ушки."""
    brown, cream = rgb(0.55, 0.38, 0.24), rgb(0.95, 0.88, 0.72)
    p = Portrait(rgb(0.15, 0.2, 0.45), rgb(0.04, 0.06, 0.2))
    p.shape(union(ell(C0, 150, 86, 88), poly((C0 - 86, 110), (C0 - 74, 40), (C0 - 32, 80)),
                  poly((C0 + 86, 110), (C0 + 74, 40), (C0 + 32, 80))), brown, cel=(C0, 150, 30))
    p.flat(ell(C0, 200, 44, 34), cream)
    for s in (-1, 1):
        p.eye(C0 + s * 36, 132, 19, lid=0.38, brow=-18, iris=rgb(1.0, 0.75, 0.15), side=s)
        p.shape(np.abs(np.hypot(X - (C0 + s * 36), Y - 132) - 26) - 2.5, rgb(0.85, 0.65, 0.25), ink=2)
    p.shape(poly((C0 - 9, 150), (C0 + 9, 150), (C0, 170)), rgb(1.0, 0.6, 0.15), ink=3)
    for s in (-1, 1):                                                                          # бабочка
        p.shape(poly((C0, 218), (C0 + s * 34, 204), (C0 + s * 34, 234)), rgb(0.85, 0.1, 0.12), ink=3.5)
    p.shape(ell(C0, 219, 8, 8), rgb(0.6, 0.05, 0.08), ink=3)
    return p.image()


def avatar_frog():
    """Жаба-везунчик: цилиндр, ленивый прищур, широкая ухмылка, язык набок."""
    green = rgb(0.42, 0.75, 0.3)
    p = Portrait(rgb(0.95, 0.78, 0.2), rgb(0.6, 0.38, 0.04))
    top_hat(p, C0, 70, 34, 52, rgb(0.15, 0.15, 0.18), rgb(0.85, 0.1, 0.12))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 48, 104, 32, 28), green, cel=(C0 + s * 48, 104, 8))
    p.shape(ell(C0, 162, 100, 62), green, cel=(C0, 162, 30))
    for s in (-1, 1):
        p.eye(C0 + s * 48, 104, 19, lid=0.55, brow=-6, look=(0.3, 0.3), iris=rgb(0.95, 0.8, 0.2), side=s)
    p.ink(arc(C0, 140, 56, 28, 152, 3.5))
    p.shape(np.maximum(ell(C0 + 34, 194, 12, 18, rot=-20), 186 - Y), rgb(0.95, 0.4, 0.45), ink=3)
    return p.image()


def avatar_bunny():
    """Заяц-фокусник: цилиндр между ушами, подмигивание, ухмылка, туз в лапе."""
    p = Portrait(rgb(0.7, 0.15, 0.55), rgb(0.3, 0.03, 0.25))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 44, 56, 20, 58, rot=s * 16), rgb(0.96, 0.96, 0.98), cel=(C0 + s * 44, 56, 6),
                dark=rgb(0.75, 0.75, 0.85))
        p.flat(ell(C0 + s * 44, 60, 9, 42, rot=s * 16), rgb(0.95, 0.6, 0.7))
    top_hat(p, C0, 96, 30, 40, rgb(0.12, 0.12, 0.15), rgb(0.95, 0.75, 0.2))
    p.shape(ell(C0, 160, 80, 66), rgb(0.96, 0.96, 0.98), cel=(C0, 160, 30), dark=rgb(0.75, 0.75, 0.85))
    p.eye(C0 - 30, 152, 15, lid=0.4, brow=-10, look=(0.3, 0), iris=rgb(0.6, 0.3, 0.8), side=-1)
    p.wink(C0 + 30, 154, 14)
    p.shape(poly((C0 - 8, 174), (C0 + 8, 174), (C0, 182)), rgb(0.95, 0.55, 0.65), ink=2.5)
    p.smirk(C0 - 2, 196, 15, up=-1)
    p.shape(np.maximum(np.abs(X - 196) - 20, np.abs(Y - 204) - 28), WHITE, ink=4)                # туз
    p.flat(poly((196, 192), (186, 206), (206, 206)), BLACK)
    p.flat(ell(196, 206, 7, 5), BLACK)
    p.flat(line(196, 206, 196, 216, 2), BLACK)
    return p.image()


# ---------------------------------------------------------------------------
# Страшные (с игровой деталью)
# ---------------------------------------------------------------------------

def flame_tongues(p, cx, base, width, height, seed, outer, inner, count=7):
    """Языки пламени вверх от линии base: изогнутые капли (круг снизу, острый кончик), внутри — светлое ядро."""
    rng = np.random.RandomState(seed)
    outs, ins = [], []
    for q in range(count):
        x0 = cx - width / 2 + width * (q + 0.5) / count + rng.uniform(-5, 5)
        h = height * rng.uniform(0.55, 1.0)
        lean = rng.uniform(-18, 18)
        w = width / count * 0.8
        body = ell(x0, base - w * 0.4, w, w * 0.9)
        tip = poly((x0 - w * 0.95, base - w * 0.5), (x0 + w * 0.95, base - w * 0.5), (x0 + lean, base - h))
        outs.append(union(body, tip))
        ins.append(union(ell(x0, base - w * 0.3, w * 0.5, w * 0.45),
                         poly((x0 - w * 0.45, base - w * 0.4), (x0 + w * 0.45, base - w * 0.4), (x0 + lean * 0.5, base - h * 0.55))))
    p.shape(union(*outs), outer, ink=4)
    p.flat(union(*ins), inner)


def fang_mouth(p, cx, cy, w, h, top=5, bottom=0, gaps=(), inside=None, tooth=None):
    """
    Пасть: тёмное нутро, острые зубы сверху (top штук) и снизу (bottom), gaps — номера выбитых верхних зубов.
    Вместо одинаковой «решётки» у всех — у каждого свой рот.
    """
    inside = rgb(0.35, 0.05, 0.08) if inside is None else inside
    tooth = rgb(0.98, 0.96, 0.88) if tooth is None else tooth
    mouth = ell(cx, cy, w, h)
    p.shape(mouth, inside, ink=4)
    teeth = []
    for q in range(top):
        if q in gaps:
            continue
        x0 = cx - w * 0.8 + w * 1.6 * (q + 0.5) / top
        tw = w * 1.6 / top * 0.5
        teeth.append(poly((x0 - tw, cy - h), (x0 + tw, cy - h), (x0, cy - h + h * (0.9 if q in (0, top - 1) else 0.7))))
    for q in range(bottom):
        x0 = cx - w * 0.6 + w * 1.2 * (q + 0.5) / bottom
        tw = w * 1.2 / bottom * 0.5
        teeth.append(poly((x0 - tw, cy + h), (x0 + tw, cy + h), (x0, cy + h - h * 0.75)))
    if teeth:
        p.layers.append((tooth, smooth(0, union(*teeth)) * smooth(0, mouth + 1)))


def scar(p, ax, ay, bx, by):
    """Шрам: розовая полоса и стежки поперёк."""
    p.flat(line(ax, ay, bx, by, 3.5), rgb(0.85, 0.45, 0.5))
    for q in range(4):
        t = (q + 0.5) / 4
        mx, my = ax + (bx - ax) * t, ay + (by - ay) * t
        nx, ny = -(by - ay), bx - ax
        n = math.hypot(nx, ny)
        p.ink(line(mx - nx / n * 6, my - ny / n * 6, mx + nx / n * 6, my + ny / n * 6, 1.4))


# Брови в eye(): отрицательный угол — злые (внутренний край ниже), положительный — грустные/скучающие.

def avatar_skull_fire():
    """Горящий череп: корона пламени языками, трещина, скулы, угли в глазницах, кубик в зубах."""
    bone, bone_dark = rgb(0.95, 0.92, 0.82), rgb(0.66, 0.6, 0.52)
    p = Portrait(rgb(0.25, 0.08, 0.05), rgb(0.06, 0.01, 0.01))
    flame_tongues(p, C0, 132, 200, 120, 3, rgb(1.0, 0.42, 0.06), rgb(1.0, 0.85, 0.3), count=6)
    head = union(ell(C0, 126, 74, 66), np.maximum(np.abs(X - C0) - 42, np.abs(Y - 192) - 26))
    p.shape(head, bone, cel=(0, 0, 0), dark=bone_dark)
    for s in (-1, 1):
        p.flat(ell(C0 + s * 52, 170, 14, 9), bone_dark)                                           # скулы
        p.shape(ell(C0 + s * 30, 128, 21, 20, rot=s * 18), BLACK, ink=2)
        p.flat(ell(C0 + s * 30, 132, 8, 10), rgb(1.0, 0.5, 0.08))
        p.flat(ell(C0 + s * 30, 134, 3.5, 4.5), rgb(1.0, 0.95, 0.5))
        p.ink(line(C0 + s * 12, 114, C0 + s * 48, 100, 4.5))                                        # злой надбровный край
    p.ink(union(line(C0 + 10, 66, C0 + 2, 84, 2), line(C0 + 2, 84, C0 + 12, 98, 2), line(C0 + 12, 98, C0 + 4, 108, 2)))
    p.shape(poly((C0 - 9, 166), (C0 + 9, 166), (C0, 150)), BLACK, ink=1)
    for q in range(-3, 4):
        p.ink(np.maximum(np.abs(X - (C0 + q * 12)) - 1.6, np.abs(Y - 196) - 18))
    p.ink(np.maximum(np.abs(Y - 196) - 1.6, np.abs(X - C0) - 42))
    p.shape(np.maximum(np.abs(X - (C0 + 50)) - 15, np.abs(Y - 204) - 15), WHITE, ink=3.5, cel=(0, 0, 0),
            dark=rgb(0.8, 0.8, 0.82))
    for dx, dy in ((-6, -6), (6, 6), (0, 0)):
        p.flat(ell(C0 + 50 + dx, 204 + dy, 2.8, 2.8), rgb(0.85, 0.1, 0.12))
    return p.image()


def avatar_vampire():
    """Вампир: зализанные волосы с мысом и бликом, скулы, злые красные глаза, крупные клыки, воротник."""
    skin, skin_dark = rgb(0.86, 0.88, 0.94), rgb(0.6, 0.62, 0.74)
    p = Portrait(rgb(0.5, 0.06, 0.1), rgb(0.15, 0.0, 0.03))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 26, 200), (C0 + s * 120, 112), (C0 + s * 104, 244)), rgb(0.72, 0.05, 0.1), ink=4,
                cel=(0, 0, 0))
        p.flat(poly((C0 + s * 40, 204), (C0 + s * 108, 136), (C0 + s * 98, 230)), rgb(0.25, 0.02, 0.05))
    p.shape(np.maximum(union(ell(C0, 146, 64, 74), poly((C0 - 40, 200), (C0 + 40, 200), (C0, 226))), Y - 226), skin,
            cel=(0, 0, 0), dark=skin_dark)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 58, 134), (C0 + s * 84, 98), (C0 + s * 70, 152)), skin, ink=3)
    hair = union(np.maximum(ell(C0, 104, 70, 46), Y - 116), poly((C0 - 22, 100), (C0 + 22, 100), (C0, 132)))
    p.shape(hair, rgb(0.08, 0.07, 0.1), ink=3)
    p.flat(line(C0 - 44, 92, C0 - 12, 80, 4), rgb(0.35, 0.35, 0.45))                               # блик на волосах
    for s in (-1, 1):
        p.eye(C0 + s * 26, 150, 15, lid=0.25, brow=-26, iris=rgb(0.95, 0.06, 0.08), side=s)
    p.ink(line(C0 - 3, 164, C0 + 3, 180, 2.2))
    p.ink(arc(C0, 186, 22, 30, 150, 3))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 12 - 5, 204), (C0 + s * 12 + 5, 204), (C0 + s * 12, 222)), WHITE, ink=2.5)
    p.flat(ell(C0 + 13, 224, 2.5, 4), rgb(0.85, 0.05, 0.08))                                      # капля
    return p.image()


def avatar_zombie():
    """Зомби: шов на лбу, разные глаза, бинт, кривая пасть с выбитыми зубами, торчащая косточка."""
    skin = rgb(0.55, 0.72, 0.45)
    p = Portrait(rgb(0.3, 0.42, 0.25), rgb(0.08, 0.15, 0.06))
    p.shape(union(ell(C0, 140, 80, 88), np.maximum(np.abs(X - C0) - 60, np.abs(Y - 214) - 14)), skin, cel=(0, 0, 0))
    p.flat(ell(C0 - 42, 184, 22, 14), rgb(0.4, 0.56, 0.32))
    p.shape(np.maximum(ell(C0 + 20, 82, 70, 30), Y - 88), rgb(0.25, 0.22, 0.2), ink=3)
    p.ink(line(C0 - 50, 108, C0 + 30, 96, 2.5))
    for q in range(6):
        x0 = C0 - 44 + q * 14
        p.ink(line(x0, 96 + q * -1.6, x0 + 4, 108 - q * 1.6, 2))
    p.eye(C0 - 30, 140, 19, lid=0.1, brow=-22, iris=rgb(0.85, 0.2, 0.1), side=-1)
    p.shape(ell(C0 + 32, 144, 17, 15), rgb(0.88, 0.92, 0.8), ink=3.5)
    p.flat(ell(C0 + 34, 146, 5, 5), rgb(0.6, 0.7, 0.6))
    p.ink(line(C0 + 16, 124, C0 + 50, 130, 4))
    p.shape(poly((C0 - 6, 162), (C0 + 6, 162), (C0, 174)), rgb(0.3, 0.4, 0.25), ink=2)
    fang_mouth(p, C0 + 4, 198, 34, 14, top=6, bottom=3, gaps=(1, 4), inside=rgb(0.2, 0.08, 0.08), tooth=rgb(0.9, 0.86, 0.6))
    p.shape(np.maximum(np.abs(X - (C0 + 70)) - 16, np.abs(Y - 108) - 40), rgb(0.95, 0.92, 0.82), ink=3)
    for q in range(4):
        p.ink(line(C0 + 56, 80 + q * 18, C0 + 84, 86 + q * 18, 1.3))
    return p.image()


def avatar_ghost():
    """Призрак: провалы глаз со светящимися зрачками, злые брови, рот-вопль, хвост вбок, звенья цепи и замок."""
    sheet, sheet_dark = rgb(0.9, 0.95, 1.0), rgb(0.6, 0.7, 0.86)
    p = Portrait(rgb(0.12, 0.26, 0.38), rgb(0.02, 0.06, 0.14))
    p.flat(ell(C0, 138, 108, 104), rgb(0.3, 0.5, 0.62))
    tail = poly((C0 - 78, 168), (C0 + 78, 168), (C0 + 94, 236), (C0 + 40, 214), (C0 + 6, 240), (C0 - 30, 212), (C0 - 70, 232))
    body = union(ell(C0, 118, 76, 78), np.maximum(np.abs(X - C0) - 76, np.abs(Y - 160) - 40), tail)
    p.shape(body, sheet, cel=(0, 0, 0), dark=sheet_dark)
    for s in (-1, 1):
        p.shape(ell(C0 + s * 28, 116, 17, 21, rot=s * -18), BLACK, ink=1)
        p.flat(ell(C0 + s * 28, 120, 4.5, 4.5), rgb(0.5, 1.0, 0.95))
        p.ink(line(C0 + s * 10, 92, C0 + s * 46, 82, 4.5))
    p.shape(union(ell(C0, 166, 16, 22), poly((C0 - 14, 160), (C0 + 14, 160), (C0, 196))), BLACK, ink=1)
    for q in range(7):                                                                          # звенья цепи
        cx, cy = C0 - 96 + q * 26, 196 + q * 4
        link = np.abs(ell(cx, cy, 13 if q % 2 == 0 else 6, 7 if q % 2 == 0 else 9)) - 2.6
        p.shape(link, rgb(0.62, 0.64, 0.7), ink=2)
    p.shape(np.maximum(np.abs(X - (C0 + 88)) - 16, np.abs(Y - 230) - 13), rgb(1.0, 0.75, 0.2), ink=3, cel=(0, 0, 0))
    p.shape(np.abs(ell(C0 + 88, 214, 9, 10)) - 2.5, rgb(0.62, 0.64, 0.7), ink=2)
    p.flat(ell(C0 + 88, 230, 3, 4), BLACK)
    return p.image()


def avatar_witch():
    """Ведьма: шляпа с загнутым кончиком и пряжкой, рыжие космы, нос крючком, хохочущий рот с одним зубом."""
    skin = rgb(0.55, 0.78, 0.45)
    p = Portrait(rgb(0.42, 0.15, 0.58), rgb(0.12, 0.02, 0.2))
    for s in (-1, 1):
        p.shape(union(*[ell(C0 + s * (58 + q * 10), 150 + q * 22, 22, 26) for q in range(3)]), rgb(0.95, 0.4, 0.1), ink=4,
                cel=(0, 0, 0))
    p.shape(ell(C0, 156, 64, 72), skin, cel=(0, 0, 0))
    p.shape(ell(C0 + 8, 112, 104, 18), rgb(0.15, 0.12, 0.2), ink=4)
    p.shape(union(poly((C0 - 50, 108), (C0 + 56, 108), (C0 + 30, 40)), poly((C0 + 18, 50), (C0 + 40, 36), (C0 + 84, 34), (C0 + 92, 52))),
            rgb(0.15, 0.12, 0.2), ink=4, cel=(0, 0, 0))
    p.flat(np.maximum(np.abs(Y - 98) - 7, poly((C0 - 46, 110), (C0 + 52, 110), (C0 + 44, 86), (C0 - 36, 86))),
           rgb(0.55, 0.2, 0.7))
    p.shape(np.abs(np.maximum(np.abs(X - C0) - 10, np.abs(Y - 98) - 8)) - 2.5, rgb(1.0, 0.8, 0.25), ink=1.5)
    for s in (-1, 1):
        p.eye(C0 + s * 26, 140, 13, lid=0.3, brow=-24, iris=rgb(0.95, 0.75, 0.1), side=s)
    p.shape(poly((C0 - 8, 160), (C0 + 8, 160), (C0 + 30, 192), (C0 + 6, 188)), skin, ink=3.5, cel=(0, 0, 0))
    p.shape(ell(C0 + 17, 178, 5, 5), rgb(0.4, 0.55, 0.3), ink=2)
    p.shape(np.maximum(ell(C0 - 6, 204, 24, 16), 196 - Y), rgb(0.35, 0.05, 0.1), ink=3.5)       # хохочет
    p.flat(np.maximum(ell(C0 - 6, 214, 12, 6), 210 - Y), rgb(0.9, 0.35, 0.4))
    p.shape(poly((C0 - 14, 197), (C0 - 4, 197), (C0 - 9, 208)), rgb(0.95, 0.9, 0.6), ink=1.5)
    return p.image()


def avatar_demon():
    """Демон: угловатая челюсть с бородкой, изогнутые рога с кольцами, злые жёлтые глаза, пасть клыками, огонь."""
    skin = rgb(0.85, 0.15, 0.12)
    p = Portrait(rgb(0.25, 0.05, 0.05), rgb(0.06, 0.0, 0.0))
    flame_tongues(p, C0, 250, 250, 150, 11, rgb(1.0, 0.4, 0.05), rgb(1.0, 0.75, 0.2), count=8)
    for s in (-1, 1):                                                                           # изогнутые рога
        horn = union(*[ell(C0 + s * (46 + q * 9), 94 - q * 15 + q * q * 1.2, 15 - q * 2.2, 13 - q * 1.8) for q in range(6)])
        p.shape(horn, rgb(0.25, 0.18, 0.16), ink=4, cel=(0, 0, 0))
        for q in range(1, 5):
            p.ink(np.maximum(np.abs(X - (C0 + s * (46 + q * 9))) - 1.5, ell(C0 + s * (46 + q * 9), 94 - q * 15 + q * q * 1.2, 16, 14)))
    face = union(ell(C0, 138, 70, 62), poly((C0 - 66, 150), (C0 + 66, 150), (C0 + 30, 210), (C0, 238), (C0 - 30, 210)))
    p.shape(face, skin, cel=(0, 0, 0))
    p.flat(poly((C0 - 16, 212), (C0 + 16, 212), (C0, 238)), rgb(0.2, 0.05, 0.05))                 # бородка
    for s in (-1, 1):
        p.shape(poly((C0 + s * 60, 136), (C0 + s * 100, 112), (C0 + s * 70, 158)), skin, ink=3)
        p.eye(C0 + s * 27, 136, 16, lid=0.2, brow=-32, iris=rgb(1.0, 0.85, 0.1), side=s)
    p.ink(union(line(C0 - 8, 160, C0 - 2, 164, 2.5), line(C0 + 8, 160, C0 + 2, 164, 2.5)))
    fang_mouth(p, C0, 188, 34, 13, top=7, bottom=4)
    return p.image()


def avatar_werewolf():
    """Оборотень: шерсть клочьями, луна, злые глаза, шрам через глаз, рык с клыками сверху и снизу."""
    fur, dark = rgb(0.5, 0.5, 0.56), rgb(0.3, 0.3, 0.36)
    p = Portrait(rgb(0.12, 0.15, 0.35), rgb(0.03, 0.04, 0.14))
    p.shape(ell(C0 + 60, 66, 40, 40), rgb(1.0, 0.95, 0.75), ink=3)
    p.flat(ell(C0 + 48, 58, 8, 6), rgb(0.88, 0.82, 0.6))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 24, 96), (C0 + s * 72, 28), (C0 + s * 92, 110)), fur, cel=(0, 0, 0), dark=dark)
        p.flat(poly((C0 + s * 40, 96), (C0 + s * 70, 50), (C0 + s * 82, 104)), rgb(0.7, 0.55, 0.55))
    spikes = union(*[poly((C0 + s * 70, 120 + q * 26), (C0 + s * 114, 130 + q * 26), (C0 + s * 74, 146 + q * 26))
                     for s in (-1, 1) for q in range(3)])
    p.shape(union(ell(C0, 146, 80, 72), spikes), fur, cel=(0, 0, 0), dark=dark)
    p.shape(ell(C0, 190, 48, 36), rgb(0.72, 0.72, 0.76), ink=4, cel=(0, 0, 0), dark=dark)
    for s in (-1, 1):
        p.eye(C0 + s * 32, 134, 16, lid=0.15, brow=-30, iris=rgb(1.0, 0.8, 0.1), side=s)
    scar(p, C0 + 18, 108, C0 + 46, 160)
    p.shape(ell(C0, 166, 15, 9), BLACK, ink=2)
    p.flat(ell(C0 - 5, 163, 4, 2.5), WHITE)
    fang_mouth(p, C0, 198, 32, 15, top=6, bottom=5)
    return p.image()


def avatar_pumpkin():
    """Тыква-голова: рёбра по бокам, злые скошенные глаза, зубастая улыбка со свечением, котелок."""
    orange = rgb(1.0, 0.52, 0.1)
    p = Portrait(rgb(0.28, 0.18, 0.38), rgb(0.08, 0.04, 0.14))
    body = union(ell(C0, 160, 96, 72), ell(C0 - 46, 160, 52, 66), ell(C0 + 46, 160, 52, 66))
    p.shape(body, orange, cel=(0, 0, 0), dark=rgb(0.75, 0.3, 0.04))
    for s in (-1, 1):                                                                           # рёбра только по бокам
        p.ink(np.maximum(np.abs(ell(C0 + s * 58, 160, 34, 66)) - 2.2, np.maximum(body + 4, s * (C0 + s * 60 - X))))
    p.shape(np.maximum(np.abs(X - C0) - 8, np.abs(Y - 88) - 12), rgb(0.35, 0.5, 0.2), ink=3)
    glow, hot = rgb(1.0, 0.85, 0.2), rgb(1.0, 0.98, 0.7)
    for s in (-1, 1):                                                                           # злые глаза
        p.shape(poly((C0 + s * 12, 150), (C0 + s * 58, 130), (C0 + s * 42, 160)), glow, ink=3)
        p.flat(poly((C0 + s * 22, 150), (C0 + s * 48, 140), (C0 + s * 40, 154)), hot)
    p.shape(poly((C0 - 8, 172), (C0 + 8, 172), (C0, 162)), glow, ink=2.5)
    mouth = np.maximum(ell(C0, 194, 58, 22), 184 - Y)
    teeth = union(*[poly((C0 - 44 + q * 22, 184), (C0 - 33 + q * 22, 184), (C0 - 38 + q * 22, 197)) for q in range(5)],
                  *[poly((C0 - 30 + q * 22, 216), (C0 - 20 + q * 22, 216), (C0 - 25 + q * 22, 205)) for q in range(3)])
    p.shape(np.maximum(mouth, -teeth), glow, ink=3)
    p.shape(ell(C0, 100, 70, 12), rgb(0.12, 0.1, 0.12), ink=4)
    p.shape(np.maximum(ell(C0, 82, 42, 30), Y - 100), rgb(0.12, 0.1, 0.12), ink=4, cel=(0, 0, 0))
    p.flat(np.maximum(np.abs(Y - 94) - 4, ell(C0, 82, 41, 29)), rgb(0.85, 0.1, 0.12))
    return p.image()


AVATARS = {
    "avatar_cat": avatar_cat, "avatar_dog": avatar_dog, "avatar_fox": avatar_fox, "avatar_panda": avatar_panda,
    "avatar_hamster": avatar_hamster, "avatar_owl": avatar_owl, "avatar_frog": avatar_frog, "avatar_bunny": avatar_bunny,
    "avatar_skull_fire": avatar_skull_fire, "avatar_vampire": avatar_vampire, "avatar_zombie": avatar_zombie,
    "avatar_ghost": avatar_ghost, "avatar_witch": avatar_witch, "avatar_demon": avatar_demon, "avatar_werewolf": avatar_werewolf,
    "avatar_pumpkin": avatar_pumpkin,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(AVATARS)
    for name in only:
        ui.save(name, AVATARS[name](), OUT)


if __name__ == "__main__":
    main()
