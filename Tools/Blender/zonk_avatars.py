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
            cx, cy, k = cel
            dark = color * 0.66 if dark is None else dark
            t = (X - cx) * 0.75 + (Y - cy) * 0.66 - k
            self.layers.append((dark, smooth(0, sdf) * smooth(0, -t)))
            band = (t > -14) & (t < 0) & (dots(X, Y, 6, 1.6) > 0.5)
            self.layers.append((dark, smooth(0, sdf) * band))

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
        p.eye(C0 + s * 36, 140, 13, lid=0.42, brow=22, side=s)
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
        p.eye(C0 + s * 36, 132, 19, lid=0.38, brow=24, iris=rgb(1.0, 0.75, 0.15), side=s)
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

def flame_tongues(p, cx, base, width, height, seed, outer, inner):
    """Языки пламени вверх от линии base: несколько вытянутых капель."""
    rng = np.random.RandomState(seed)
    shapes_o, shapes_i = [], []
    for q in range(7):
        x0 = cx - width / 2 + width * (q + 0.5) / 7 + rng.uniform(-6, 6)
        h = height * rng.uniform(0.6, 1.0)
        tip = (x0 + rng.uniform(-14, 14), base - h)
        shapes_o.append(poly((x0 - 18, base), (x0 + 18, base), tip))
        shapes_i.append(poly((x0 - 9, base), (x0 + 9, base), (tip[0] * 0.6 + x0 * 0.4, base - h * 0.6)))
    p.shape(union(*shapes_o), outer, ink=4)
    p.flat(union(*shapes_i), inner)


def avatar_skull_fire():
    """Горящий череп: пламя сверху, пустые глазницы с огоньками, оскал, кость-кубик в зубах."""
    bone = rgb(0.95, 0.92, 0.82)
    p = Portrait(rgb(0.2, 0.08, 0.06), rgb(0.05, 0.01, 0.01))
    flame_tongues(p, C0, 120, 190, 110, 3, rgb(1.0, 0.45, 0.08), rgb(1.0, 0.85, 0.3))
    p.shape(union(ell(C0, 128, 76, 68), np.maximum(np.abs(X - C0) - 44, np.abs(Y - 192) - 26)), bone, cel=(C0, 130, 25),
            dark=rgb(0.7, 0.66, 0.58))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 30, 132, 20, 22, rot=s * 10), BLACK, ink=2)
        p.flat(ell(C0 + s * 30, 136, 7, 9), rgb(1.0, 0.55, 0.1))
        p.flat(ell(C0 + s * 30, 138, 3, 4), rgb(1.0, 0.95, 0.5))
    p.shape(poly((C0 - 9, 168), (C0 + 9, 168), (C0, 152)), BLACK, ink=1)
    for q in range(-3, 4):
        p.ink(np.maximum(np.abs(X - (C0 + q * 12)) - 1.6, np.abs(Y - 196) - 18))
    p.ink(np.maximum(np.abs(Y - 196) - 1.6, np.abs(X - C0) - 44))
    p.shape(np.maximum(np.abs(X - (C0 + 52)) - 14, np.abs(Y - 204) - 14), WHITE, ink=3.5)       # кубик в зубах
    for dx, dy in ((-6, -6), (6, 6), (0, 0)):
        p.flat(ell(C0 + 52 + dx, 204 + dy, 2.6, 2.6), rgb(0.85, 0.1, 0.12))
    return p.image()


def avatar_vampire():
    """Вампир: бледное лицо, зачёс углом, высокий воротник, клыки, красные глаза, туз червей."""
    skin = rgb(0.86, 0.88, 0.92)
    p = Portrait(rgb(0.45, 0.06, 0.1), rgb(0.15, 0.0, 0.03))
    for s in (-1, 1):                                                                           # воротник
        p.shape(poly((C0 + s * 30, 196), (C0 + s * 118, 120), (C0 + s * 104, 240)), rgb(0.7, 0.05, 0.1), ink=4)
    p.shape(np.maximum(ell(C0, 150, 66, 82), Y - 214), skin, cel=(C0, 150, 25), dark=rgb(0.62, 0.64, 0.75))
    p.shape(union(np.maximum(ell(C0, 108, 72, 50), Y - 112), poly((C0 - 18, 96), (C0 + 18, 96), (C0, 128))), BLACK, ink=3)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 60, 134), (C0 + s * 80, 104), (C0 + s * 70, 150)), skin, ink=3)     # острые уши
        p.eye(C0 + s * 26, 146, 15, lid=0.45, brow=26, iris=rgb(0.9, 0.08, 0.1), side=s)
    p.ink(line(C0, 160, C0 - 4, 178, 2))
    p.smirk(C0, 196, 18, up=1)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 10 - 4, 190), (C0 + s * 10 + 4, 190), (C0 + s * 10, 204)), WHITE, ink=2)
    return p.image()


def avatar_zombie():
    """Зомби: зелёная кожа, шов на лбу, разные глаза (один белёсый), кривой оскал, бинт."""
    skin = rgb(0.55, 0.72, 0.45)
    p = Portrait(rgb(0.3, 0.42, 0.25), rgb(0.08, 0.15, 0.06))
    p.shape(union(ell(C0, 140, 80, 88), np.maximum(np.abs(X - C0) - 60, np.abs(Y - 214) - 14)), skin, cel=(C0, 140, 25))
    p.flat(ell(C0 - 40, 182, 22, 14), rgb(0.42, 0.58, 0.35))
    p.shape(np.maximum(ell(C0 + 20, 82, 70, 30), Y - 88), rgb(0.25, 0.22, 0.2), ink=3)            # клочья волос
    p.ink(line(C0 - 50, 108, C0 + 30, 96, 2.5))
    for q in range(6):
        x0 = C0 - 44 + q * 14
        p.ink(line(x0, 96 + q * -1.6, x0 + 4, 108 - q * 1.6, 2))
    p.eye(C0 - 30, 140, 18, lid=0.3, brow=-18, iris=rgb(0.85, 0.2, 0.1), side=-1)
    p.shape(ell(C0 + 32, 142, 17, 15), rgb(0.88, 0.92, 0.8), ink=3.5)                           # белёсый глаз
    p.flat(ell(C0 + 34, 144, 5, 5), rgb(0.6, 0.7, 0.6))
    p.shape(poly((C0 - 6, 160), (C0 + 6, 160), (C0, 172)), rgb(0.3, 0.4, 0.25), ink=2)
    p.grin(C0 + 4, 196, 30, 14)
    p.shape(np.maximum(np.abs(X - (C0 + 70)) - 16, np.abs(Y - 108) - 40), rgb(0.95, 0.92, 0.82), ink=3)  # бинт
    for q in range(4):
        p.ink(line(C0 + 56, 80 + q * 18, C0 + 84, 86 + q * 18, 1.3))
    return p.image()


def avatar_ghost():
    """Призрак: волнистый низ, чёрные глаза-провалы, рот «О», цепь с замком, полупрозрачный ореол."""
    sheet = rgb(0.92, 0.95, 1.0)
    p = Portrait(rgb(0.15, 0.3, 0.42), rgb(0.03, 0.08, 0.15))
    p.flat(ell(C0, 140, 104, 100), rgb(0.35, 0.55, 0.65))
    bottom = union(*[ell(C0 - 60 + q * 30, 214, 17, 18) for q in range(5)])
    body = np.minimum(np.maximum(union(ell(C0, 120, 76, 78), np.maximum(np.abs(X - C0) - 76, np.abs(Y - 168) - 46)),
                                 Y - 214), bottom)
    p.shape(body, sheet, cel=(C0, 130, 25), dark=rgb(0.65, 0.72, 0.85))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 28, 116, 15, 22, rot=s * -12), BLACK, ink=1)
        p.flat(ell(C0 + s * 28 - 4, 108, 4, 5), WHITE)
        p.ink(line(C0 + s * 14, 88 - 0, C0 + s * 44, 82 + 0, 4))
    p.shape(ell(C0, 160, 12, 16), BLACK, ink=1)
    for q in range(6):                                                                          # цепь
        p.shape(np.abs(ell(C0 - 80 + q * 26, 196 + q * 4, 10, 6)) - 2.5, rgb(0.6, 0.62, 0.66), ink=2)
    p.shape(np.maximum(np.abs(X - (C0 + 76)) - 14, np.abs(Y - 222) - 12), rgb(1.0, 0.75, 0.2), ink=3)
    p.flat(ell(C0 + 76, 222, 3, 4), BLACK)
    return p.image()


def avatar_witch():
    """Ведьма: остроконечная шляпа с пряжкой, зелёная кожа, ухмылка, бородавка, рыжие волосы."""
    skin = rgb(0.55, 0.78, 0.45)
    p = Portrait(rgb(0.4, 0.15, 0.55), rgb(0.12, 0.02, 0.2))
    for s in (-1, 1):
        p.shape(union(*[ell(C0 + s * (58 + q * 10), 150 + q * 22, 22, 26) for q in range(3)]), rgb(0.95, 0.4, 0.1), ink=4)
    p.shape(ell(C0, 156, 64, 72), skin, cel=(C0, 156, 22))
    p.shape(ell(C0 + 8, 112, 104, 18), rgb(0.15, 0.12, 0.2), ink=4)                              # поля
    p.shape(poly((C0 - 50, 108), (C0 + 56, 108), (C0 + 40, 30), (C0 + 70, 14), (C0 + 6, 20)), rgb(0.15, 0.12, 0.2), ink=4,
            cel=(C0, 70, 20))
    p.flat(np.maximum(np.abs(Y - 98) - 7, poly((C0 - 46, 110), (C0 + 52, 110), (C0 + 44, 86), (C0 - 36, 86))),
           rgb(0.55, 0.2, 0.7))
    p.shape(np.abs(np.maximum(np.abs(X - C0) - 10, np.abs(Y - 98) - 8)) - 2.5, rgb(1.0, 0.8, 0.25), ink=1.5)
    for s in (-1, 1):
        p.eye(C0 + s * 24, 146, 14, lid=0.4, brow=20, iris=rgb(0.95, 0.75, 0.1), side=s)
    p.shape(poly((C0 - 11, 144), (C0 + 9, 144), (C0 + 34, 188), (C0 + 6, 184)), skin, ink=3.5, cel=(C0, 160, 6))  # нос крючком
    p.shape(ell(C0 + 18, 172, 5, 5), rgb(0.4, 0.55, 0.3), ink=2)
    p.grin(C0 - 4, 202, 22, 10)
    return p.image()


def avatar_demon():
    """Демон: красная кожа, рога, жёлтые глаза, клыкастый оскал, бородка, огонь за спиной."""
    skin = rgb(0.85, 0.15, 0.12)
    p = Portrait(rgb(0.25, 0.05, 0.05), rgb(0.06, 0.0, 0.0))
    flame_tongues(p, C0, 230, 240, 150, 11, rgb(1.0, 0.4, 0.05), rgb(1.0, 0.75, 0.2))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 40, 96), (C0 + s * 62, 104), (C0 + s * 96, 28), (C0 + s * 70, 60)), rgb(0.2, 0.15, 0.15), ink=4,
                cel=(C0 + s * 70, 70, 0))
    p.shape(union(ell(C0, 148, 72, 76), poly((C0 - 20, 206), (C0 + 20, 206), (C0, 236))), skin, cel=(C0, 148, 25))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 64, 140), (C0 + s * 96, 120), (C0 + s * 70, 160)), skin, ink=3)
        p.eye(C0 + s * 28, 140, 16, lid=0.42, brow=30, iris=rgb(1.0, 0.85, 0.1), side=s)
    p.ink(line(C0 - 6, 162, C0 + 6, 162, 2.5))
    p.grin(C0, 190, 34, 15)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 22 - 5, 188), (C0 + s * 22 + 5, 188), (C0 + s * 22, 206)), WHITE, ink=2)
    return p.image()


def avatar_werewolf():
    """Оборотень: серая шерсть клочьями, луна за спиной, жёлтые глаза, оскал с клыками."""
    fur, dark = rgb(0.5, 0.5, 0.56), rgb(0.3, 0.3, 0.36)
    p = Portrait(rgb(0.12, 0.15, 0.35), rgb(0.03, 0.04, 0.14))
    p.shape(ell(C0 + 60, 70, 40, 40), rgb(1.0, 0.95, 0.75), ink=3)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 24, 96), (C0 + s * 72, 28), (C0 + s * 92, 110)), fur, cel=(C0, 90, 30))
    spikes = union(*[poly((C0 + s * 70, 120 + q * 26), (C0 + s * 112, 130 + q * 26), (C0 + s * 74, 146 + q * 26))
                     for s in (-1, 1) for q in range(3)])
    p.shape(union(ell(C0, 146, 80, 72), spikes), fur, cel=(C0, 146, 25), dark=dark)
    p.shape(ell(C0, 188, 46, 34), rgb(0.72, 0.72, 0.76), ink=4, cel=(C0, 188, 10), dark=dark)
    for s in (-1, 1):
        p.eye(C0 + s * 32, 134, 16, lid=0.4, brow=28, iris=rgb(1.0, 0.8, 0.1), side=s)
    p.shape(ell(C0, 168, 14, 9), BLACK, ink=2)
    p.grin(C0, 196, 30, 13)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 18 - 5, 194), (C0 + s * 18 + 5, 194), (C0 + s * 18, 212)), WHITE, ink=2)
    return p.image()


def avatar_pumpkin():
    """Тыква-голова: рёбра, вырезанные треугольные глаза и зубастая улыбка со свечением, шляпа-котелок."""
    orange = rgb(1.0, 0.52, 0.1)
    p = Portrait(rgb(0.28, 0.18, 0.38), rgb(0.08, 0.04, 0.14))
    body = union(ell(C0, 160, 96, 72), ell(C0 - 46, 160, 52, 66), ell(C0 + 46, 160, 52, 66))
    p.shape(body, orange, cel=(C0, 160, 30), dark=rgb(0.75, 0.3, 0.04))
    for rx in (22, 50):                                                                         # рёбра
        p.ink(np.maximum(np.abs(ell(C0, 160, rx, 70)) - 2.2, body + 4))
    p.shape(np.maximum(np.abs(X - C0) - 8, np.abs(Y - 88) - 12), rgb(0.35, 0.5, 0.2), ink=3)       # хвостик
    glow, hot = rgb(1.0, 0.85, 0.2), rgb(1.0, 0.98, 0.7)
    for s in (-1, 1):
        p.shape(poly((C0 + s * 14, 156), (C0 + s * 56, 156), (C0 + s * 30, 124)), glow, ink=3)
        p.flat(poly((C0 + s * 24, 152), (C0 + s * 44, 152), (C0 + s * 32, 138)), hot)
    mouth = np.maximum(ell(C0, 190, 54, 20), 182 - Y)
    teeth = union(*[poly((C0 - 40 + q * 20, 182), (C0 - 30 + q * 20, 182), (C0 - 35 + q * 20, 194)) for q in range(5)])
    p.shape(np.maximum(mouth, -teeth), glow, ink=3)
    p.shape(ell(C0, 100, 70, 12), rgb(0.12, 0.1, 0.12), ink=4)                                    # котелок
    p.shape(np.maximum(ell(C0, 82, 42, 30), Y - 100), rgb(0.12, 0.1, 0.12), ink=4, cel=(C0, 82, 10))
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
