# -*- coding: utf-8 -*-
"""
Аватарки игрока для «Зонка» в стиле комикса (как интерфейс, zonk_ui.py): портреты персонажей на круглом фоне
с лучами, чернильный контур, растр в тенях, жёсткая тень, блики в глазах.

Наборы (добавляются по одному, после приёма автором):
    Милые       avatar_cat, avatar_dog, avatar_fox, avatar_panda, avatar_hamster, avatar_owl, avatar_frog, avatar_bunny

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
    """Слои портрета снизу вверх: фон с лучами, фигуры с контуром, детали без контура. Всё обрезано кругом."""

    def __init__(self, bg, bg_dark):
        self.layers = []
        circle = np.hypot(X - C0, Y - C0) - RADIUS
        ang = np.arctan2(Y - C0, X - C0)
        rays = (np.floor((ang + math.pi) / (2 * math.pi) * 16) % 2 == 0)
        back = np.where(rays[..., None], bg, bg * 0.9 + bg_dark * 0.1)
        halftone = ((X - C0) + (Y - C0) > 70) & (dots(X, Y, 9, 2.4) > 0.5)
        back = np.where(halftone[..., None], bg_dark, back)
        self.clip = smooth(0, circle + 6)
        self.layers.append((INK, smooth(0, circle)))
        self.layers.append((back, smooth(0, circle + 6)))

    def shape(self, sdf, color, ink=5.0, shade=None):
        """Фигура с чернильным контуром; shade — (маска, цвет) растр-тень внутри фигуры."""
        self.layers.append((INK, smooth(0, sdf - ink)))
        self.layers.append((color, smooth(0, sdf)))
        if shade is not None:
            mask, dark = shade
            self.layers.append((dark, smooth(0, sdf) * (mask & (dots(X, Y, 7, 1.8) > 0.5))))

    def flat(self, sdf, color):
        """Деталь без контура (румянец, блик, полоска)."""
        self.layers.append((color, smooth(0, sdf)))

    def eye(self, cx, cy, r, look=(0.0, 0.0), lid=None):
        """Большой мультяшный глаз: белок с контуром, крупный зрачок, два блика."""
        self.shape(ell(cx, cy, r, r * 1.1), WHITE, ink=4)
        px, py = cx + look[0] * r * 0.25, cy + look[1] * r * 0.25 + r * 0.12
        self.flat(ell(px, py, r * 0.68, r * 0.76), BLACK)
        self.flat(ell(px - r * 0.25, py - r * 0.3, r * 0.24, r * 0.24), WHITE)
        self.flat(ell(px + r * 0.22, py + r * 0.25, r * 0.11, r * 0.11), WHITE)
        if lid is not None:
            self.flat(np.maximum(ell(cx, cy, r + 1, r * 1.1 + 1), Y - (cy - r * 1.1 + lid * r * 2.2)), lid_color(self))

    def blush(self, cx, cy):
        self.flat(ell(cx, cy, 14, 8), PINK * 0.95 + WHITE * 0.05)

    def image(self):
        layers = [(c, a * self.clip) for c, a in self.layers]
        return compose(*layers)


def lid_color(p):
    return BLACK


def smile_w(p, cx, cy, s=1.0):
    """Ротик «w»: две дуги."""
    p.flat(union(arc(cx - 9 * s, cy, 9 * s, 10, 170, 2.6 * s), arc(cx + 9 * s, cy, 9 * s, 10, 170, 2.6 * s)), BLACK)


# ---------------------------------------------------------------------------
# Милые
# ---------------------------------------------------------------------------

def avatar_cat():
    """Рыжий котик: острые уши с розовым, полоски на лбу, глаза, носик, ротик «w», усы."""
    orange, dark = rgb(1.0, 0.62, 0.22), rgb(0.82, 0.38, 0.1)
    p = Portrait(rgb(0.3, 0.72, 0.72), rgb(0.12, 0.42, 0.45))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 30, 92), (C0 + s * 88, 40), (C0 + s * 86, 120)), orange)
        p.flat(poly((C0 + s * 44, 96), (C0 + s * 80, 58), (C0 + s * 78, 110)), PINK)
    p.shape(ell(C0, 150, 86, 72), orange, shade=((X - C0) + (Y - 150) > 50, dark))
    for dx in (-16, 0, 16):
        p.flat(line(C0 + dx, 84, C0 + dx * 0.7, 104, 4.5), dark)
    p.flat(ell(C0, 178, 40, 26), rgb(1.0, 0.92, 0.8))
    for s in (-1, 1):
        p.eye(C0 + s * 34, 142, 17, look=(0.2 * -s, 0))
        p.blush(C0 + s * 56, 170)
        for dy in (-6, 6):
            p.flat(line(C0 + s * 42, 178 + dy, C0 + s * 92, 172 + dy * 2, 1.6), BLACK)
    p.shape(poly((C0 - 8, 165), (C0 + 8, 165), (C0, 174)), PINK, ink=3)
    smile_w(p, C0, 176)
    return p.image()


def avatar_dog():
    """Щенок: висячие уши, светлая мордочка, чёрный нос, язычок, пятно у глаза."""
    fur, dark, ear = rgb(0.96, 0.82, 0.58), rgb(0.72, 0.52, 0.3), rgb(0.55, 0.32, 0.16)
    p = Portrait(rgb(1.0, 0.58, 0.25), rgb(0.75, 0.3, 0.1))
    p.shape(ell(C0, 140, 84, 80), fur, shade=((X - C0) + (Y - 140) > 50, dark))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 78, 140, 26, 56, rot=s * -18), ear, shade=(Y > 150, ear * 0.7))
    p.flat(ell(C0 + 34, 128, 24, 22), rgb(0.82, 0.6, 0.36))
    p.shape(ell(C0, 178, 46, 34), rgb(1.0, 0.95, 0.86), ink=4)
    for s in (-1, 1):
        p.eye(C0 + s * 32, 130, 15)
        p.blush(C0 + s * 58, 162)
    p.shape(ell(C0, 163, 13, 9), BLACK, ink=2)
    p.flat(ell(C0 - 4, 160, 4, 2.5), WHITE)
    p.shape(np.maximum(ell(C0, 198, 11, 14), 186 - Y), rgb(1.0, 0.45, 0.5), ink=3)
    smile_w(p, C0, 182)
    return p.image()


def avatar_fox():
    """Лисёнок: высокие уши с тёмными кончиками, белые щёчки, острая мордочка."""
    orange, dark = rgb(1.0, 0.5, 0.15), rgb(0.78, 0.28, 0.06)
    p = Portrait(rgb(0.55, 0.38, 0.8), rgb(0.3, 0.16, 0.5))
    for s in (-1, 1):
        p.shape(poly((C0 + s * 24, 96), (C0 + s * 78, 26), (C0 + s * 92, 112)), orange)
        p.flat(poly((C0 + s * 62, 48), (C0 + s * 78, 26), (C0 + s * 84, 62)), rgb(0.2, 0.12, 0.1))
        p.flat(poly((C0 + s * 40, 98), (C0 + s * 74, 50), (C0 + s * 82, 106)), rgb(1.0, 0.85, 0.75))
    head = union(ell(C0, 138, 82, 64), poly((C0 - 96, 150), (C0 + 96, 150), (C0, 214)))
    p.shape(head, orange, shade=((X - C0) + (Y - 140) > 50, dark))
    p.flat(union(poly((C0 - 90, 152), (C0 - 6, 164), (C0, 210)), poly((C0 + 90, 152), (C0 + 6, 164), (C0, 210))),
           rgb(1.0, 0.97, 0.92))
    for s in (-1, 1):
        p.eye(C0 + s * 34, 134, 16, look=(0.2, 0))
        p.blush(C0 + s * 58, 160)
    p.shape(ell(C0, 196, 10, 7), BLACK, ink=2)
    p.flat(ell(C0 - 3, 194, 3, 2), WHITE)
    return p.image()


def avatar_panda():
    """Панда: круглые чёрные уши, пятна вокруг глаз, носик, улыбка."""
    p = Portrait(rgb(0.45, 0.78, 0.4), rgb(0.2, 0.48, 0.2))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 66, 72, 28, 28), BLACK)
    p.shape(ell(C0, 146, 90, 78), WHITE, shade=((X - C0) + (Y - 146) > 55, rgb(0.78, 0.8, 0.82)))
    for s in (-1, 1):
        p.flat(ell(C0 + s * 36, 140, 26, 32, rot=s * -30), BLACK)
        p.eye(C0 + s * 36, 140, 12)
        p.blush(C0 + s * 64, 172)
    p.shape(ell(C0, 172, 12, 8), BLACK, ink=2)
    smile_w(p, C0, 184)
    return p.image()


def avatar_hamster():
    """Хомячок: широкая голова, пухлые щёки, круглые ушки, белая мордочка, зубки."""
    fur, dark = rgb(0.98, 0.72, 0.38), rgb(0.82, 0.5, 0.2)
    p = Portrait(rgb(1.0, 0.62, 0.75), rgb(0.8, 0.32, 0.48))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 62, 70, 22, 20), fur)
        p.flat(ell(C0 + s * 62, 72, 11, 10), PINK)
    p.shape(union(ell(C0, 140, 96, 80), ell(C0 - 70, 168, 42, 38), ell(C0 + 70, 168, 42, 38)), fur,
            shade=((X - C0) + (Y - 140) > 60, dark))
    p.flat(ell(C0, 178, 50, 40), rgb(1.0, 0.96, 0.9))
    for s in (-1, 1):
        p.eye(C0 + s * 36, 132, 14)
        p.blush(C0 + s * 72, 168)
    p.shape(ell(C0, 160, 9, 6), PINK, ink=3)
    smile_w(p, C0, 172, 0.9)
    p.shape(union(ell(C0 - 5, 190, 5, 8), ell(C0 + 5, 190, 5, 8)), WHITE, ink=2.5)
    return p.image()


def avatar_owl():
    """Совёнок: пёрышки-ушки, огромные глаза в жёлтых кругах, клювик, пёрышки на груди."""
    brown, dark, cream = rgb(0.62, 0.42, 0.26), rgb(0.42, 0.26, 0.14), rgb(0.98, 0.9, 0.72)
    p = Portrait(rgb(0.18, 0.22, 0.5), rgb(0.08, 0.1, 0.28))
    p.shape(union(ell(C0, 150, 86, 88), poly((C0 - 84, 110), (C0 - 70, 46), (C0 - 30, 80)),
                  poly((C0 + 84, 110), (C0 + 70, 46), (C0 + 30, 80))), brown, shade=((X - C0) + (Y - 150) > 55, dark))
    p.flat(ell(C0, 196, 48, 40), cream)
    for q in range(5):
        for r in range(2):
            p.flat(arc(C0 - 30 + q * 15 + r * 7, 186 + r * 14, 7, 20, 160, 1.8), dark)
    for s in (-1, 1):
        p.shape(ell(C0 + s * 38, 130, 34, 34), rgb(1.0, 0.82, 0.25), ink=4)
        p.eye(C0 + s * 38, 130, 22)
    p.shape(poly((C0 - 10, 152), (C0 + 10, 152), (C0, 172)), rgb(1.0, 0.6, 0.15), ink=3)
    # звёздочки на небе
    for sx, sy in ((48, 60), (210, 74), (60, 200)):
        p.flat(poly(*ui.regular_star(sx, sy, 8, 3)), rgb(1.0, 0.92, 0.5))
    return p.image()


def avatar_frog():
    """Лягушонок: широкая зелёная голова, глаза на бугорках, большая улыбка, румянец."""
    green, dark = rgb(0.45, 0.82, 0.32), rgb(0.24, 0.55, 0.18)
    p = Portrait(rgb(1.0, 0.85, 0.3), rgb(0.85, 0.55, 0.1))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 46, 96, 34, 32), green)
    p.shape(ell(C0, 158, 100, 66), green, shade=((X - C0) + (Y - 158) > 50, dark))
    for s in (-1, 1):
        p.eye(C0 + s * 46, 94, 21, look=(0, 0.3))
        p.blush(C0 + s * 66, 172)
        p.flat(ell(C0 + s * 12, 146, 3, 2.5), dark)
    p.flat(arc(C0, 140, 52, 30, 150, 3.5), BLACK)
    p.flat(np.maximum(ell(C0, 178, 26, 12), 176 - Y), rgb(1.0, 0.45, 0.5))
    return p.image()


def avatar_bunny():
    """Зайка: длинные уши с розовым, белая голова, розовый носик, зубки."""
    p = Portrait(rgb(0.45, 0.72, 1.0), rgb(0.2, 0.4, 0.75))
    for s in (-1, 1):
        p.shape(ell(C0 + s * 38, 54, 22, 62, rot=s * 12), WHITE, ink=5)
        p.flat(ell(C0 + s * 38, 58, 11, 46, rot=s * 12), PINK)
    p.shape(ell(C0, 158, 82, 72), WHITE, shade=((X - C0) + (Y - 158) > 50, rgb(0.82, 0.84, 0.9)))
    for s in (-1, 1):
        p.eye(C0 + s * 32, 150, 16)
        p.blush(C0 + s * 56, 178)
    p.shape(poly((C0 - 9, 172), (C0 + 9, 172), (C0, 181)), PINK, ink=3)
    smile_w(p, C0, 183, 0.85)
    p.shape(union(ell(C0 - 5, 200, 5, 8), ell(C0 + 5, 200, 5, 8)), WHITE, ink=2.5)
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
