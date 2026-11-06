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


AVATARS = {
    "avatar_cat": avatar_cat, "avatar_dog": avatar_dog, "avatar_fox": avatar_fox, "avatar_panda": avatar_panda,
    "avatar_hamster": avatar_hamster, "avatar_owl": avatar_owl, "avatar_frog": avatar_frog, "avatar_bunny": avatar_bunny,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(AVATARS)
    for name in only:
        ui.save(name, AVATARS[name](), OUT)


if __name__ == "__main__":
    main()
