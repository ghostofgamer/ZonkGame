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


# ---------------------------------------------------------------------------
# Характеры
# ---------------------------------------------------------------------------

SKIN = rgb(0.98, 0.78, 0.62)
SKIN_TAN = rgb(0.86, 0.6, 0.42)


def face(p, cx=C0, cy=146, rx=66, ry=74, skin=SKIN):
    p.shape(union(ell(cx, cy, rx, ry), ell(cx - rx + 4, cy + 4, 12, 18), ell(cx + rx - 4, cy + 4, 12, 18)), skin, cel=(0, 0, 0))


def nose(p, cx, cy, size=1.0):
    p.ink(arc(cx + 3 * size, cy, 8 * size, -30, 110, 2.4))


def avatar_pirate():
    """Пират: треуголка с черепом, повязка на глазу, щетина, золотая серьга, ухмылка с золотым зубом."""
    p = Portrait(rgb(0.1, 0.4, 0.6), rgb(0.02, 0.14, 0.26))
    face(p, skin=SKIN_TAN)
    p.flat(np.maximum(np.maximum(ell(C0, 186, 58, 40), 160 - Y), -ell(C0, 192, 26, 12)), rgb(0.74, 0.52, 0.38))  # щетина, рот открыт
    p.shape(union(ell(C0, 98, 104, 26), poly((C0 - 80, 92), (C0, 30), (C0 + 80, 92))), rgb(0.12, 0.1, 0.12), ink=4,
            cel=(0, 0, 0))
    p.flat(np.maximum(np.abs(np.hypot(X - C0, Y - 96) - 98) - 2.5, ell(C0, 98, 104, 26)), rgb(1.0, 0.8, 0.25))
    p.shape(ell(C0, 68, 13, 12), WHITE, ink=2.5)                                                   # череп на шляпе
    for s in (-1, 1):
        p.flat(ell(C0 + s * 5, 67, 3, 3), BLACK)
    p.eye(C0 - 26, 142, 15, lid=0.25, brow=-20, iris=rgb(0.3, 0.55, 0.8), side=-1)
    p.shape(ell(C0 + 28, 142, 18, 15), BLACK, ink=2)                                               # повязка
    p.ink(line(C0 - 66, 118, C0 + 70, 160, 3))
    nose(p, C0, 162)
    p.smirk(C0, 190, 20, up=1)
    p.shape(np.maximum(np.abs(X - (C0 + 8)) - 4, np.abs(Y - 192) - 5), rgb(1.0, 0.8, 0.25), ink=2)
    p.shape(np.abs(ell(C0 - 68, 176, 7, 9)) - 2.2, rgb(1.0, 0.8, 0.25), ink=1.5)
    return p.image()


def avatar_cowboy():
    """Ковбой: широкополая шляпа, прищур, усы подковой, шейный платок, соломинка во рту."""
    p = Portrait(rgb(0.95, 0.6, 0.2), rgb(0.6, 0.25, 0.05))
    p.shape(poly((C0 - 60, 220), (C0 + 60, 220), (C0 + 90, 260), (C0 - 90, 260)), rgb(0.8, 0.12, 0.12), ink=4)
    face(p, skin=SKIN_TAN)
    p.shape(ell(C0, 96, 112, 22), rgb(0.55, 0.32, 0.15), ink=4, cel=(0, 0, 0))
    p.shape(np.maximum(union(ell(C0, 70, 54, 36), poly((C0 - 54, 80), (C0 + 54, 80), (C0 + 46, 50), (C0 - 46, 50))), Y - 94),
            rgb(0.55, 0.32, 0.15), ink=4, cel=(0, 0, 0))
    p.flat(np.maximum(np.abs(Y - 84) - 5, ell(C0, 72, 54, 34)), rgb(0.3, 0.16, 0.06))
    for s in (-1, 1):
        p.eye(C0 + s * 26, 140, 14, lid=0.5, brow=-12, iris=rgb(0.35, 0.5, 0.25), side=s)
    nose(p, C0, 162)
    p.shape(union(ell(C0 - 16, 182, 18, 8, rot=10), ell(C0 + 16, 182, 18, 8, rot=-10),
                  np.maximum(np.abs(X - (C0 - 32)) - 5, np.abs(Y - 196) - 14), np.maximum(np.abs(X - (C0 + 32)) - 5, np.abs(Y - 196) - 14)),
            rgb(0.45, 0.28, 0.14), ink=3)
    p.shape(line(C0 + 6, 196, C0 + 46, 210, 2.5), rgb(0.95, 0.85, 0.45), ink=2)
    return p.image()


def avatar_gangster():
    """Гангстер: шляпа-федора с лентой, тень на глазах, шрам, полоска усов, костюм и белый воротник."""
    p = Portrait(rgb(0.2, 0.2, 0.24), rgb(0.05, 0.05, 0.07))
    p.shape(poly((C0 - 110, 256), (C0 - 50, 210), (C0 + 50, 210), (C0 + 110, 256)), rgb(0.18, 0.18, 0.22), ink=4)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 6, 214), (C0 + s * 40, 210), (C0 + s * 20, 240)), WHITE, ink=3)
    p.shape(poly((C0 - 8, 214), (C0 + 8, 214), (C0 + 6, 250), (C0 - 6, 250)), rgb(0.8, 0.1, 0.12), ink=2.5)
    face(p)
    p.shape(ell(C0, 100, 100, 20), rgb(0.25, 0.25, 0.3), ink=4, cel=(0, 0, 0))
    p.shape(np.maximum(union(ell(C0, 76, 56, 34)), Y - 100), rgb(0.25, 0.25, 0.3), ink=4, cel=(0, 0, 0))
    p.flat(np.maximum(np.abs(Y - 92) - 6, ell(C0, 78, 56, 32)), rgb(0.08, 0.08, 0.1))
    p.flat(line(C0 - 30, 56, C0 + 10, 50, 3), rgb(0.5, 0.5, 0.56))
    p.flat(np.maximum(np.abs(Y - 132) - 16, ell(C0, 146, 64, 72)), rgb(0.62, 0.42, 0.32))          # тень полей шляпы
    for s in (-1, 1):
        p.eye(C0 + s * 26, 140, 14, lid=0.35, brow=-24, iris=rgb(0.5, 0.35, 0.2), side=s)
    scar(p, C0 - 46, 150, C0 - 30, 180)
    nose(p, C0, 162)
    p.shape(np.maximum(np.abs(X - C0) - 20, np.abs(Y - 180) - 3.5), rgb(0.2, 0.14, 0.1), ink=1.5)
    p.smirk(C0, 198, 16, up=-1)
    return p.image()


def avatar_clown():
    """Злой клоун: белый грим, рыжие кудри, красный нос, ромбы на глазах, хищный оскал."""
    p = Portrait(rgb(0.15, 0.6, 0.3), rgb(0.03, 0.25, 0.1))
    for s in (-1, 1):
        p.shape(union(*[ell(C0 + s * (64 + q * 6), 108 + q * 26, 30, 26) for q in range(3)]), rgb(1.0, 0.4, 0.1), ink=4,
                cel=(0, 0, 0))
    face(p, skin=rgb(0.97, 0.96, 0.95))
    for s in (-1, 1):
        p.flat(poly((C0 + s * 26, 112), (C0 + s * 40, 140), (C0 + s * 26, 168), (C0 + s * 12, 140)), rgb(0.25, 0.4, 0.9))
        p.eye(C0 + s * 26, 140, 13, lid=0.2, brow=-30, iris=rgb(0.95, 0.85, 0.1), side=s)
    p.shape(ell(C0, 166, 15, 15), rgb(0.9, 0.08, 0.1), ink=3.5, cel=(0, 0, 0))
    p.flat(ell(C0 - 5, 161, 4, 4), WHITE)
    p.flat(np.maximum(ell(C0, 198, 46, 20), 186 - Y), rgb(0.85, 0.1, 0.12))
    fang_mouth(p, C0, 198, 34, 11, top=7, bottom=6)
    p.shape(np.maximum(ell(C0, 60, 30, 26), Y - 70), rgb(0.85, 0.1, 0.12), ink=3, cel=(0, 0, 0))      # колпачок
    p.shape(ell(C0, 36, 9, 9), rgb(1.0, 0.85, 0.2), ink=3)
    return p.image()


def avatar_granny():
    """Бабуля-картёжница: пучок со спицами, очки на цепочке, хитрая улыбка, жемчуг, карта в руке."""
    p = Portrait(rgb(0.85, 0.45, 0.6), rgb(0.5, 0.15, 0.3))
    p.shape(ell(C0, 64, 34, 28), rgb(0.85, 0.85, 0.9), ink=4, cel=(0, 0, 0))                         # пучок
    p.ink(line(C0 - 40, 40, C0 + 30, 80, 2.5))
    p.shape(ell(C0 + 34, 82, 5, 5), rgb(0.9, 0.2, 0.3), ink=2)
    face(p, rx=64, ry=72)
    p.shape(np.maximum(ell(C0, 116, 72, 44), Y - 122), rgb(0.85, 0.85, 0.9), ink=4, cel=(0, 0, 0))
    for s in (-1, 1):
        p.ink(line(C0 + s * 42, 172, C0 + s * 50, 178, 1.6))                                     # морщинка
        p.eye(C0 + s * 26, 146, 12, lid=0.45, brow=-6, iris=rgb(0.35, 0.55, 0.8), side=s)
        p.shape(np.abs(ell(C0 + s * 26, 146, 19, 15)) - 2.6, rgb(0.85, 0.65, 0.25), ink=1.5)
    p.ink(line(C0 - 7, 146, C0 + 7, 146, 2.2))
    p.shape(arc(C0, 150, 80, 30, 150, 1.4), rgb(0.85, 0.65, 0.25), ink=1)
    nose(p, C0, 166)
    p.smirk(C0, 192, 16, up=1)
    for q in range(9):
        p.shape(ell(C0 - 48 + q * 12, 222 + abs(q - 4) * -2.4, 6, 6), rgb(0.98, 0.96, 0.92), ink=2)
    p.shape(np.maximum(np.abs(X - 204) - 18, np.abs(Y - 196) - 26), WHITE, ink=3.5)                    # туз червей
    p.flat(union(ell(198, 192, 6, 6), ell(210, 192, 6, 6), poly((192, 194), (216, 194), (204, 208))), rgb(0.85, 0.1, 0.15))
    return p.image()


def avatar_robot():
    """Робот: голова-коробка с болтами, антенна-лампочка, глаза-экраны, рот-решётка динамика."""
    metal, dark = rgb(0.68, 0.72, 0.78), rgb(0.38, 0.42, 0.48)
    p = Portrait(rgb(0.15, 0.55, 0.65), rgb(0.03, 0.22, 0.3))
    p.shape(line(C0, 70, C0, 40, 3), dark, ink=2.5)
    p.shape(ell(C0, 36, 10, 10), rgb(1.0, 0.3, 0.3), ink=3)
    p.flat(ell(C0 - 3, 33, 3, 3), WHITE)
    for s in (-1, 1):
        p.shape(np.maximum(np.abs(X - (C0 + s * 84)) - 12, np.abs(Y - 146) - 22), dark, ink=3.5)
    head = np.maximum(np.abs(X - C0) - 76, np.abs(Y - 146) - 76) + 0
    head = np.maximum(head, np.hypot(np.maximum(np.abs(X - C0) - 60, 0), np.maximum(np.abs(Y - 146) - 60, 0)) - 16)
    p.shape(head, metal, cel=(0, 0, 0), dark=dark)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.shape(ell(C0 + sx * 62, 146 + sy * 62, 5, 5), dark, ink=2)
    for s in (-1, 1):
        p.shape(np.maximum(np.abs(X - (C0 + s * 30)) - 20, np.abs(Y - 132) - 14), rgb(0.05, 0.08, 0.1), ink=3.5)
        p.flat(np.maximum(np.abs(X - (C0 + s * 30)) - 14, np.maximum(np.abs(Y - 134) - 4, (Y - 134) * s * 0.7 - (X - (C0 + s * 30)) * 0)),
               rgb(0.3, 1.0, 0.9))
    p.ink(union(line(C0 - 50, 108, C0 - 12, 118, 4), line(C0 + 50, 108, C0 + 12, 118, 4)))
    grill = np.maximum(np.abs(X - C0) - 40, np.abs(Y - 190) - 14)
    p.shape(grill, rgb(0.12, 0.14, 0.16), ink=3.5)
    for q in range(-3, 4):
        p.flat(np.maximum(np.abs(X - (C0 + q * 11)) - 2, grill + 3), rgb(0.5, 0.55, 0.6))
    return p.image()


def avatar_ninja():
    """Ниндзя: чёрная маска с прорезью, красная повязка с хвостами, злой прищур, сюрикен."""
    p = Portrait(rgb(0.85, 0.12, 0.1), rgb(0.4, 0.02, 0.02))
    p.shape(ell(C0, 150, 74, 84), rgb(0.12, 0.12, 0.15), cel=(0, 0, 0), dark=rgb(0.05, 0.05, 0.07))
    p.shape(np.maximum(np.abs(Y - 140) - 18, ell(C0, 150, 72, 82)), SKIN, ink=3)
    p.shape(np.maximum(np.abs(Y - 104) - 10, ell(C0, 150, 76, 86)), rgb(0.85, 0.1, 0.12), ink=3.5)
    p.shape(union(poly((C0 + 66, 100), (C0 + 118, 84), (C0 + 106, 104)), poly((C0 + 66, 108), (C0 + 112, 126), (C0 + 96, 132))),
            rgb(0.85, 0.1, 0.12), ink=3.5)
    for s in (-1, 1):
        p.eye(C0 + s * 26, 142, 14, lid=0.3, brow=-34, iris=rgb(0.4, 0.3, 0.2), side=s)
    star = poly((70, 206), (78, 196), (86, 206), (96, 214), (86, 222), (78, 232), (70, 222), (60, 214))
    p.shape(union(star, poly(*ui.regular_star(78, 214, 22, 7, rays=4))), rgb(0.7, 0.74, 0.8), ink=3)
    p.flat(ell(78, 214, 4, 4), BLACK)
    return p.image()


def avatar_knight():
    """Рыцарь: шлем с забралом-прорезью, плюмаж, глаза светятся из щели, заклёпки, щит с кубиком."""
    steel, dark = rgb(0.72, 0.75, 0.8), rgb(0.42, 0.45, 0.52)
    p = Portrait(rgb(0.2, 0.3, 0.6), rgb(0.06, 0.1, 0.28))
    p.shape(union(*[ell(C0 + 14 + q * 10, 54 - q * 6, 18, 14) for q in range(4)]), rgb(0.85, 0.12, 0.15), ink=4,
            cel=(0, 0, 0))
    helm = union(ell(C0, 136, 70, 80), np.maximum(np.abs(X - C0) - 64, np.abs(Y - 196) - 28))
    p.shape(helm, steel, cel=(0, 0, 0), dark=dark)
    p.flat(np.maximum(np.abs(X - C0) - 4, helm + 6), rgb(0.88, 0.9, 0.94))
    p.shape(np.maximum(np.abs(X - C0) - 54, np.abs(Y - 138) - 8), rgb(0.06, 0.06, 0.08), ink=3)
    for s in (-1, 1):
        p.flat(ell(C0 + s * 24, 138, 9, 4), rgb(0.4, 0.9, 1.0))
    for q in range(4):
        for s in (-1, 1):
            p.flat(np.maximum(np.abs(X - (C0 + s * (12 + q * 12))) - 2.5, np.abs(Y - 186) - 9), rgb(0.06, 0.06, 0.08))
    for a in range(8):
        ang = math.pi * (0.15 + 0.7 * a / 7)
        p.shape(ell(C0 + math.cos(ang) * 60, 136 - math.sin(ang) * 66, 4, 4), rgb(0.95, 0.78, 0.3), ink=1.5)
    shield = union(np.maximum(np.abs(X - 196) - 26, np.abs(Y - 196) - 18), poly((170, 210), (222, 210), (196, 246)))
    p.shape(shield, rgb(0.85, 0.12, 0.15), ink=4, cel=(0, 0, 0))
    p.shape(np.maximum(np.abs(X - 196) - 11, np.abs(Y - 208) - 11), WHITE, ink=2.5)
    for dx, dy in ((-5, -5), (5, 5), (0, 0)):
        p.flat(ell(196 + dx, 208 + dy, 2, 2), BLACK)
    return p.image()


# ---------------------------------------------------------------------------
# Азарт
# ---------------------------------------------------------------------------

def avatar_dice_guy():
    """Кубик-персонаж: белая кость с фаской, глаза-точки со злыми бровями, ухмылка, на боковых гранях точки."""
    p = Portrait(rgb(0.15, 0.55, 0.3), rgb(0.03, 0.25, 0.1))
    front = np.maximum(np.abs(X - (C0 - 8)) - 66, np.abs(Y - 150) - 66)
    front = np.maximum(front, np.hypot(np.maximum(np.abs(X - (C0 - 8)) - 50, 0), np.maximum(np.abs(Y - 150) - 50, 0)) - 16)
    side = poly((C0 + 58, 96), (C0 + 86, 70), (C0 + 86, 186), (C0 + 58, 214))
    topf = poly((C0 - 66, 84), (C0 - 38, 58), (C0 + 86, 58), (C0 + 58, 84))
    p.shape(union(side, topf), rgb(0.82, 0.82, 0.86), ink=4)
    p.flat(ell(C0 + 72, 110, 5, 7), rgb(0.85, 0.1, 0.12))
    p.flat(ell(C0 + 72, 160, 5, 7), rgb(0.85, 0.1, 0.12))
    p.flat(ell(C0 + 10, 71, 8, 5), BLACK)
    p.shape(front, rgb(0.98, 0.97, 0.94), cel=(0, 0, 0), dark=rgb(0.78, 0.78, 0.82))
    for s in (-1, 1):
        p.shape(ell(C0 - 8 + s * 28, 132, 14, 14), BLACK, ink=1)
        p.flat(ell(C0 - 8 + s * 28 - 4, 128, 4, 4), WHITE)
        p.ink(line(C0 - 8 + s * 12, 118, C0 - 8 + s * 46, 108, 4.5))
    p.smirk(C0 - 8, 186, 22, up=1)
    return p.image()


def avatar_chip_guy():
    """Фишка-персонаж: красная фишка с белыми вставками по краю, лицо в центральном круге."""
    p = Portrait(rgb(0.95, 0.75, 0.2), rgb(0.6, 0.38, 0.04))
    chip = ell(C0, 146, 92, 92)
    p.shape(chip, rgb(0.82, 0.1, 0.12), cel=(0, 0, 0))
    for q in range(8):
        a = q * math.pi / 4
        p.flat(np.maximum(np.abs(np.hypot(X - C0, Y - 146) - 78) - 12,
                          np.abs(np.arctan2(Y - 146, X - C0) - math.atan2(math.sin(a), math.cos(a))) * 80 - 14), WHITE)
    p.shape(ell(C0, 146, 58, 58), rgb(0.96, 0.94, 0.88), ink=3.5, cel=(0, 0, 0))
    for s in (-1, 1):
        p.eye(C0 + s * 20, 136, 11, lid=0.3, brow=-24, iris=rgb(0.85, 0.1, 0.12), side=s)
    p.grin(C0, 170, 20, 8)
    return p.image()


def avatar_ace():
    """Туз-персонаж: карта, в центре большая пика с лицом, уголки «A» знаками-пиками."""
    p = Portrait(rgb(0.1, 0.35, 0.25), rgb(0.02, 0.12, 0.08))
    card = np.maximum(np.abs(X - C0) - 70, np.abs(Y - 140) - 96)
    card = np.maximum(card, np.hypot(np.maximum(np.abs(X - C0) - 56, 0), np.maximum(np.abs(Y - 140) - 82, 0)) - 14)
    p.shape(card, WHITE, cel=(0, 0, 0), dark=rgb(0.82, 0.84, 0.88))
    spade = union(ell(C0 - 22, 150, 30, 28), ell(C0 + 22, 150, 30, 28), poly((C0 - 50, 140), (C0 + 50, 140), (C0, 80)),
                  poly((C0 - 14, 200), (C0 + 14, 200), (C0, 170)))
    p.shape(spade, rgb(0.12, 0.12, 0.15), ink=2)
    for s in (-1, 1):
        p.shape(ell(C0 + s * 16, 140, 9, 8), WHITE, ink=1)
        p.flat(ell(C0 + s * 16 + 2, 142, 4, 4), rgb(0.85, 0.1, 0.12))
        p.flat(line(C0 + s * 6, 126, C0 + s * 28, 122, 3), WHITE)
    p.flat(arc(C0, 154, 14, 30, 150, 2.5), WHITE)
    for sx, sy in ((-1, -1), (1, 1)):
        p.flat(poly(*ui.regular_star(C0 + sx * 50, 140 + sy * 74, 10, 4, rays=4)), rgb(0.85, 0.1, 0.12))
    return p.image()


def avatar_joker():
    """Джокер: колпак с бубенцами, маска-полумаска, безумная улыбка, ромбы."""
    p = Portrait(rgb(0.45, 0.15, 0.6), rgb(0.15, 0.02, 0.25))
    face(p, skin=rgb(0.97, 0.95, 0.92))
    for s, col in ((-1, rgb(0.85, 0.1, 0.15)), (1, rgb(0.15, 0.55, 0.3))):
        p.shape(poly((C0 + s * 10, 104), (C0 + s * 70, 100), (C0 + s * 112, 40), (C0 + s * 40, 70)), col, ink=4, cel=(0, 0, 0))
        p.shape(ell(C0 + s * 112, 40, 10, 10), rgb(1.0, 0.82, 0.2), ink=3)
    p.shape(np.maximum(np.abs(Y - 102) - 10, ell(C0, 146, 70, 78)), rgb(1.0, 0.82, 0.2), ink=3.5)
    p.shape(union(ell(C0 - 28, 140, 26, 18), ell(C0 + 28, 140, 26, 18), np.maximum(np.abs(X - C0) - 6, np.abs(Y - 140) - 6)),
            rgb(0.12, 0.12, 0.15), ink=3)
    for s in (-1, 1):
        p.eye(C0 + s * 26, 140, 12, lid=0.15, brow=-30, iris=rgb(0.2, 0.85, 0.4), side=s)
    p.flat(poly((C0 - 6, 176), (C0 + 6, 176), (C0, 160)), rgb(0.85, 0.1, 0.15))
    p.shape(np.maximum(ell(C0, 196, 44, 18), 186 - Y), rgb(0.35, 0.05, 0.1), ink=3.5)
    fang_mouth(p, C0, 196, 40, 13, top=8, bottom=0)
    p.ink(union(line(C0 - 48, 184, C0 - 60, 172, 3), line(C0 + 48, 184, C0 + 60, 172, 3)))
    return p.image()


def avatar_moneybag():
    """Мешок денег: завязанный мешок с лицом-ухмылкой, монеты высыпаются, золотая звезда-чекан на мешке."""
    sack, sack_dark = rgb(0.85, 0.7, 0.45), rgb(0.6, 0.45, 0.25)
    p = Portrait(rgb(0.2, 0.6, 0.3), rgb(0.05, 0.28, 0.1))
    bag = union(ell(C0, 168, 80, 70), poly((C0 - 30, 110), (C0 + 30, 110), (C0 + 44, 130), (C0 - 44, 130)),
                poly((C0 - 34, 100), (C0 - 50, 70), (C0, 88), (C0 + 50, 70), (C0 + 34, 100)))
    p.shape(bag, sack, cel=(0, 0, 0), dark=sack_dark)
    p.shape(np.maximum(np.abs(Y - 106) - 7, np.abs(X - C0) - 36), rgb(0.6, 0.15, 0.12), ink=3)
    for s in (-1, 1):
        p.eye(C0 + s * 24, 158, 12, lid=0.4, brow=-14, iris=rgb(0.2, 0.6, 0.3), side=s)
    p.smirk(C0, 196, 20, up=1)
    for cx, cy in ((46, 200), (40, 214), (52, 226), (210, 206), (216, 220), (200, 232)):            # стопки монет
        p.shape(ell(cx, cy, 22, 9), rgb(1.0, 0.8, 0.25), ink=3)
        p.flat(np.abs(ell(cx, cy, 14, 5)) - 1.2, rgb(0.8, 0.55, 0.1))
    p.shape(ell(C0 + 40, 128, 18, 18), rgb(1.0, 0.8, 0.25), ink=3, cel=(0, 0, 0))                # монета на мешке
    p.flat(poly(*ui.regular_star(C0 + 40, 129, 10, 4.5)), rgb(0.75, 0.5, 0.1))
    return p.image()


def avatar_lucky7():
    """Счастливая семёрка: толстая золотая «7» с контуром и бликом на фоне барабана слота, искры."""
    p = Portrait(rgb(0.8, 0.1, 0.12), rgb(0.35, 0.02, 0.04))
    p.shape(np.maximum(np.abs(X - C0) - 76, np.abs(Y - 140) - 92), rgb(0.97, 0.95, 0.9), ink=4, cel=(0, 0, 0),
            dark=rgb(0.8, 0.8, 0.82))
    seven = union(np.maximum(np.abs(X - C0) - 54, np.abs(Y - 76) - 16), poly((C0 + 54, 62), (C0 + 54, 92), (C0 - 6, 220), (C0 - 40, 220)))
    p.ink(seven - 7)                                                                            # толстый контур
    p.shape(seven, rgb(1.0, 0.75, 0.15), ink=0, cel=(0, 0, 0), dark=rgb(0.85, 0.45, 0.05))
    p.flat(line(C0 - 44, 70, C0 + 10, 70, 3.5), rgb(1.0, 0.95, 0.65))
    for cx, cy, r in ((54, 60, 12), (206, 196, 10), (50, 200, 8)):
        p.flat(poly(*ui.regular_star(cx, cy, r, r * 0.3, rays=4)), rgb(1.0, 0.95, 0.6))
    return p.image()


# ---------------------------------------------------------------------------
# Боссы: портрет соперника (Art/Sprites/Portraits) в круге, растр по краю, корона — знак босса
# ---------------------------------------------------------------------------

BOSSES = ["captain", "claw", "krot", "madame", "rose", "sych", "tikhon", "agafya", "bo", "fortuna"]
# Цвет дыма у каждого босса свой: портреты одного тона (красный дым), в ряду аватарок их было не различить.
BOSS_TINT = {"captain": (0.25, 0.55, 1.0), "claw": (1.0, 0.45, 0.15), "krot": (0.65, 0.45, 0.3), "madame": (0.6, 0.3, 1.0),
             "rose": (1.0, 0.35, 0.6), "sych": (0.2, 0.85, 0.75), "tikhon": (0.85, 0.7, 0.35), "agafya": (0.75, 0.4, 0.9),
             "bo": (1.0, 0.65, 0.2), "fortuna": (1.0, 0.85, 0.3)}


def tint_smoke(img, tint):
    """Дым (красноватые не яркие пиксели) — в цвет босса по яркости; светящиеся глаза и шапка не трогаются."""
    rgb_ = img[..., :3]
    lum = rgb_.max(axis=2)
    redness = rgb_[..., 0] - np.maximum(rgb_[..., 1], rgb_[..., 2])
    smoke = (redness > 0.12) & (lum < 0.85)
    tinted = lum[..., None] * np.array(tint) * 1.15
    out = img.copy()
    out[..., :3] = np.where(smoke[..., None], tinted, rgb_)
    return out
PORTRAITS = os.path.join(ui.SPRITES, "Portraits")


def load_rgba(path):
    import bpy
    image = bpy.data.images.load(path, check_existing=False)
    w, h = image.size
    data = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    bpy.data.images.remove(image)
    return data


def resample(img, size, crop, box=(0.0, 0.0, 1.0, 1.0)):
    """Часть портрета (без рамки crop пикселей; box — доли x0, y0, x1, y1) в size×size, усреднение 2×2."""
    h, w = img.shape[:2]
    core = img[crop:h - crop, crop:w - crop]
    ch0, cw0 = core.shape[:2]
    core = core[int(box[1] * ch0):int(box[3] * ch0), int(box[0] * cw0):int(box[2] * cw0)]
    ch, cw = core.shape[:2]
    ys = (np.arange(size) + 0.5) * ch / size
    xs = (np.arange(size) + 0.5) * cw / size
    yi = np.clip(ys.astype(int), 0, ch - 1)
    xi = np.clip(xs.astype(int), 0, cw - 1)
    yj = np.clip(yi + 1, 0, ch - 1)
    xj = np.clip(xi + 1, 0, cw - 1)
    return (core[yi][:, xi] + core[yj][:, xi] + core[yi][:, xj] + core[yj][:, xj]) * 0.25


def boss_avatar(name):
    def make():
        p = Portrait(rgb(0.3, 0.06, 0.06), rgb(0.08, 0.0, 0.0))
        # крупный план: шапка и глаза — главное, обрывки предметов со стола внизу отрезаны
        img = resample(load_rgba(os.path.join(PORTRAITS, "Portrait_opp_%s.png" % name)), SIZE, 16, (0.14, 0.0, 0.86, 0.72))
        img = tint_smoke(img, BOSS_TINT[name])
        inner = np.hypot(X - C0, Y - C0) - (RADIUS - 6)
        p.layers.append((img[..., :3], smooth(0, inner) * img[..., 3]))
        r = np.hypot(X - C0, Y - C0)
        p.layers.append((rgb(0.05, 0.0, 0.0), smooth(0, inner) * ((r > 96) & (dots(X, Y, 7, 1.0 + 2.2 * np.clip((r - 96) / 26, 0, 1)) > 0.5))))
        crown = union(poly((C0 - 30, 40), (C0 - 30, 20), (C0 - 15, 32), (C0, 16), (C0 + 15, 32), (C0 + 30, 20), (C0 + 30, 40)))
        p.shape(crown + 0, rgb(1.0, 0.8, 0.22), ink=4, cel=(0, 0, 0))
        for dx in (-15, 0, 15):
            p.flat(ell(C0 + dx, 34, 3.5, 3.5), rgb(0.85, 0.1, 0.15))
        return p.image()
    return make


# ---------------------------------------------------------------------------
# Старые значки (ID прежние: у игроков уже есть) — в том же стиле
# ---------------------------------------------------------------------------

def glyph_avatar(bg, bg_dark, draw):
    def make():
        p = Portrait(bg, bg_dark)
        draw(p)
        return p.image()
    return make


def g_die(p, color=WHITE, pip=rgb(0.85, 0.1, 0.12), value=5):
    body = np.maximum(np.abs(X - C0) - 60, np.abs(Y - C0) - 60)
    body = np.maximum(body, np.hypot(np.maximum(np.abs(X - C0) - 44, 0), np.maximum(np.abs(Y - C0) - 44, 0)) - 16)
    p.shape(body, color, cel=(0, 0, 0), dark=color * 0.78)
    spots = {5: ((-1, -1), (1, 1), (-1, 1), (1, -1), (0, 0)), 6: ((-1, -1), (1, 1), (-1, 1), (1, -1), (-1, 0), (1, 0))}[value]
    for sx, sy in spots:
        p.shape(ell(C0 + sx * 30, C0 + sy * 30, 11, 11), pip, ink=2)


def g_star(p, color):
    p.shape(poly(*ui.regular_star(C0, C0 + 6, 80, 34)), color, cel=(0, 0, 0))


def g_moon(p, color, bg):
    p.shape(ell(C0, C0, 70, 70), color, cel=(0, 0, 0))
    p.flat(ell(C0 + 34, C0 - 22, 58, 58), bg)
    p.ink(np.maximum(np.abs(ell(C0 + 34, C0 - 22, 58, 58)) - 2.5, ell(C0, C0, 70, 70)))


def g_skull(p, bone=rgb(0.95, 0.92, 0.82)):
    p.shape(union(ell(C0, 118, 64, 58), np.maximum(np.abs(X - C0) - 36, np.abs(Y - 178) - 22)), bone, cel=(0, 0, 0),
            dark=rgb(0.66, 0.6, 0.52))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 26, 120, 17, 18, rot=s * 15), BLACK, ink=2)
    p.shape(poly((C0 - 8, 154), (C0 + 8, 154), (C0, 140)), BLACK, ink=1)
    for q in range(-2, 3):
        p.ink(np.maximum(np.abs(X - (C0 + q * 12)) - 1.6, np.abs(Y - 180) - 16))


def g_anchor(p):
    c = rgb(0.95, 0.93, 0.85)
    p.shape(union(np.maximum(np.abs(X - C0) - 9, np.abs(Y - 140) - 66), np.maximum(np.abs(X - C0) - 40, np.abs(Y - 92) - 8),
                  np.abs(np.hypot(X - C0, Y - 66) - 16) - 6,
                  np.maximum(np.abs(np.hypot(X - C0, Y - 150) - 56) - 9, 150 - Y),
                  poly((C0 - 70, 140), (C0 - 46, 160), (C0 - 62, 166)), poly((C0 + 70, 140), (C0 + 46, 160), (C0 + 62, 166))),
            c, cel=(0, 0, 0))


def g_bolt(p):
    p.shape(poly((C0 + 14, 40), (C0 - 46, 140), (C0 - 4, 140), (C0 - 18, 216), (C0 + 48, 106), (C0 + 4, 106), (C0 + 26, 40)),
            rgb(1.0, 0.86, 0.2), cel=(0, 0, 0))


def g_clover(p):
    g = rgb(0.15, 0.55, 0.22)
    p.shape(union(ell(C0, 92, 30, 30), ell(C0 - 34, 126, 30, 30), ell(C0 + 34, 126, 30, 30), ell(C0, 160, 30, 30),
                  np.maximum(np.abs(X - (C0 + 6)) - 6, np.maximum(Y - 220, 150 - Y))), g, cel=(0, 0, 0))
    p.flat(ell(C0, 126, 8, 8), rgb(0.3, 0.75, 0.35))


def g_crown(p):
    gold = rgb(1.0, 0.78, 0.2)
    p.shape(poly((C0 - 76, 180), (C0 - 76, 80), (C0 - 38, 120), (C0, 60), (C0 + 38, 120), (C0 + 76, 80), (C0 + 76, 180)), gold,
            cel=(0, 0, 0))
    for dx, col in ((-40, rgb(0.2, 0.45, 0.95)), (0, rgb(0.9, 0.1, 0.15)), (40, rgb(0.15, 0.75, 0.35))):
        p.shape(ell(C0 + dx, 156, 11, 11), col, ink=3)
    for dx, dy in ((-76, 80), (0, 60), (76, 80)):
        p.shape(ell(C0 + dx, dy, 9, 9), gold, ink=3)


def g_cup(p):
    wood = rgb(0.5, 0.3, 0.14)
    p.shape(poly((C0 - 56, 76), (C0 + 56, 76), (C0 + 44, 200), (C0 - 44, 200)), wood, cel=(0, 0, 0))
    p.shape(ell(C0, 76, 56, 14), rgb(0.15, 0.08, 0.04), ink=4)
    for y in (110, 170):
        p.flat(np.maximum(np.abs(Y - y) - 5, poly((C0 - 56, 76), (C0 + 56, 76), (C0 + 44, 200), (C0 - 44, 200))), rgb(0.35, 0.2, 0.1))
    p.shape(np.maximum(np.abs(X - (C0 + 40)) - 18, np.abs(Y - 52) - 18), WHITE, ink=3)
    for dx, dy in ((-7, -7), (7, 7), (0, 0)):
        p.flat(ell(C0 + 40 + dx, 52 + dy, 2.6, 2.6), rgb(0.85, 0.1, 0.12))


def g_diamond(p):
    c = rgb(0.55, 0.95, 1.0)
    p.shape(poly((C0, 44), (C0 + 70, 116), (C0, 214), (C0 - 70, 116)), c, cel=(0, 0, 0), dark=rgb(0.25, 0.7, 0.85))
    p.flat(poly((C0, 44), (C0 + 70, 116), (C0, 116)), rgb(0.85, 1.0, 1.0))
    p.ink(union(line(C0 - 70, 116, C0 + 70, 116, 2), line(C0, 44, C0, 214, 1.5)))


def g_eye(p):
    p.shape(np.maximum(ell(C0, C0, 90, 50), 0 * X - 1), rgb(0.98, 0.96, 0.9), cel=(0, 0, 0))
    p.shape(ell(C0, C0, 34, 34), rgb(0.2, 0.65, 0.35), ink=3)
    p.flat(ell(C0, C0, 16, 16), BLACK)
    p.flat(ell(C0 - 10, C0 - 10, 7, 7), WHITE)


def g_flame(p):
    p.shape(union(ell(C0, 160, 54, 54), poly((C0 - 52, 150), (C0 + 52, 150), (C0 + 10, 40))), rgb(1.0, 0.45, 0.08), cel=(0, 0, 0))
    p.flat(union(ell(C0, 172, 28, 28), poly((C0 - 26, 168), (C0 + 26, 168), (C0 + 4, 100))), rgb(1.0, 0.85, 0.3))


def g_heart(p):
    p.shape(union(ell(C0 - 36, 110, 44, 42), ell(C0 + 36, 110, 44, 42), poly((C0 - 78, 124), (C0 + 78, 124), (C0, 214))),
            rgb(0.9, 0.12, 0.2), cel=(0, 0, 0))
    p.flat(ell(C0 - 44, 96, 12, 8, rot=-30), rgb(1.0, 0.7, 0.75))


def g_horseshoe(p):
    shoe = np.maximum(np.abs(np.hypot(X - C0, Y - 120) - 58) - 16, 120 - Y)
    shoe = union(shoe, np.maximum(np.abs(X - C0) - 74, np.maximum(np.abs(np.abs(X - C0) - 58) - 16, np.maximum(Y - 120, 60 - Y))))
    p.shape(shoe, rgb(0.86, 0.88, 0.92), cel=(0, 0, 0))
    for a in range(5):
        ang = math.pi * (0.15 + 0.7 * a / 4)
        p.flat(ell(C0 + math.cos(ang) * 58, 120 + math.sin(ang) * 58, 4, 4), BLACK)


def g_key(p):
    gold = rgb(1.0, 0.8, 0.25)
    p.shape(union(np.abs(np.hypot(X - 84, Y - 168) - 30) - 10, line(108, 148, 196, 62, 9), line(176, 84, 196, 104, 8),
                  line(160, 100, 176, 116, 8)), gold, cel=(0, 0, 0))


def g_six(p):
    g_die(p, color=rgb(0.12, 0.1, 0.1), pip=rgb(1.0, 0.78, 0.25), value=6)


def g_sun(p):
    p.shape(union(poly(*ui.regular_star(C0, C0, 96, 62, rays=12)), ell(C0, C0, 60, 60)), rgb(1.0, 0.85, 0.3), cel=(0, 0, 0))
    p.flat(ell(C0, C0, 44, 44), rgb(1.0, 0.95, 0.6))


OLD = {
    "avatar_die": glyph_avatar(rgb(0.62, 0.2, 0.16), rgb(0.3, 0.06, 0.04), lambda p: g_die(p)),
    "avatar_star": glyph_avatar(rgb(0.2, 0.36, 0.7), rgb(0.06, 0.12, 0.35), lambda p: g_star(p, rgb(1.0, 0.84, 0.3))),
    "avatar_moon": glyph_avatar(rgb(0.12, 0.14, 0.32), rgb(0.03, 0.04, 0.12), lambda p: g_moon(p, rgb(0.95, 0.92, 0.7), rgb(0.12, 0.14, 0.32))),
    "avatar_clover": glyph_avatar(rgb(0.55, 0.78, 0.45), rgb(0.2, 0.42, 0.18), g_clover),
    "avatar_heart": glyph_avatar(rgb(0.98, 0.7, 0.76), rgb(0.7, 0.3, 0.42), g_heart),
    "avatar_diamond": glyph_avatar(rgb(0.15, 0.45, 0.5), rgb(0.04, 0.18, 0.24), g_diamond),
    "avatar_bolt": glyph_avatar(rgb(0.45, 0.22, 0.65), rgb(0.18, 0.06, 0.32), g_bolt),
    "avatar_eye": glyph_avatar(rgb(0.3, 0.55, 0.5), rgb(0.1, 0.24, 0.22), g_eye),
    "avatar_skull": glyph_avatar(rgb(0.25, 0.25, 0.28), rgb(0.05, 0.05, 0.07), lambda p: g_skull(p)),
    "avatar_crown": glyph_avatar(rgb(0.75, 0.12, 0.15), rgb(0.3, 0.03, 0.05), g_crown),
    "avatar_sun": glyph_avatar(rgb(1.0, 0.6, 0.2), rgb(0.7, 0.2, 0.06), g_sun),
    "avatar_six": glyph_avatar(rgb(0.25, 0.22, 0.2), rgb(0.05, 0.04, 0.03), g_six),
    "avatar_horseshoe": glyph_avatar(rgb(0.42, 0.3, 0.2), rgb(0.16, 0.1, 0.06), g_horseshoe),
    "avatar_anchor": glyph_avatar(rgb(0.25, 0.55, 0.75), rgb(0.07, 0.2, 0.36), g_anchor),
    "avatar_cup": glyph_avatar(rgb(0.78, 0.55, 0.3), rgb(0.42, 0.22, 0.08), g_cup),
    "avatar_key": glyph_avatar(rgb(0.35, 0.25, 0.5), rgb(0.12, 0.07, 0.2), g_key),
    "avatar_flame": glyph_avatar(rgb(0.3, 0.1, 0.12), rgb(0.07, 0.02, 0.03), g_flame),
    "avatar_s1_skull": glyph_avatar(rgb(0.75, 0.12, 0.12), rgb(0.28, 0.03, 0.03), lambda p: g_skull(p)),
    "avatar_s1_star": glyph_avatar(rgb(0.1, 0.4, 0.55), rgb(0.03, 0.14, 0.24), lambda p: g_star(p, rgb(1.0, 0.55, 0.35))),
    "avatar_s1_moon": glyph_avatar(rgb(0.05, 0.25, 0.3), rgb(0.01, 0.06, 0.1), lambda p: g_moon(p, rgb(1.0, 0.8, 0.3), rgb(0.05, 0.25, 0.3))),
}


AVATARS = {
    "avatar_cat": avatar_cat, "avatar_dog": avatar_dog, "avatar_fox": avatar_fox, "avatar_panda": avatar_panda,
    "avatar_hamster": avatar_hamster, "avatar_owl": avatar_owl, "avatar_frog": avatar_frog, "avatar_bunny": avatar_bunny,
    "avatar_skull_fire": avatar_skull_fire, "avatar_vampire": avatar_vampire, "avatar_zombie": avatar_zombie,
    "avatar_ghost": avatar_ghost, "avatar_witch": avatar_witch, "avatar_demon": avatar_demon, "avatar_werewolf": avatar_werewolf,
    "avatar_pumpkin": avatar_pumpkin,
    "avatar_pirate": avatar_pirate, "avatar_cowboy": avatar_cowboy, "avatar_gangster": avatar_gangster, "avatar_clown": avatar_clown,
    "avatar_granny": avatar_granny, "avatar_robot": avatar_robot, "avatar_ninja": avatar_ninja, "avatar_knight": avatar_knight,
    "avatar_dice_guy": avatar_dice_guy, "avatar_chip_guy": avatar_chip_guy, "avatar_ace": avatar_ace, "avatar_joker": avatar_joker,
    "avatar_moneybag": avatar_moneybag, "avatar_lucky7": avatar_lucky7,
}
AVATARS.update({"avatar_boss_" + _b: boss_avatar(_b) for _b in BOSSES})
AVATARS.update(OLD)


def main():
    os.makedirs(OUT, exist_ok=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(AVATARS)
    for name in only:
        ui.save(name, AVATARS[name](), OUT)


if __name__ == "__main__":
    main()
