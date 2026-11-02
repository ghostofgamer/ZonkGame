# -*- coding: utf-8 -*-
"""
Текстуры интерфейса «Зонка» в стиле комикса: чернильный контур, жёсткая тень, растр точками, неровный край от руки.

Заливка белая: цвет детали задаёт Image.color (палитра UiConfig), контур и тень остаются чёрными.
Огонь и искра — цветные, их не красят.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_ui.py
Результат: Art/Sprites/UI_*.png (идут в общий атлас) и Art/Sprites/Comic/UI_Burst_*.png (большие, вне атласа).
Подключает генератор (ZonkSetup.Skin): тип Sprite, границы 9-slice.
Размеры кратны 4 (сжатие DXT в браузере). Границы заданы здесь и в ZonkSetup.Skin: менять вместе.
"""

import math
import os
import sys

import bpy
import numpy as np

sys.dont_write_bytecode = True

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITES = os.path.join(ROOT, "Assets", "ZonkContent", "Art", "Sprites")
OUT = SPRITES

INK = np.array([0.082, 0.063, 0.047])
RED = np.array([0.878, 0.212, 0.173])
ORANGE = np.array([0.961, 0.502, 0.102])
GOLD = np.array([1.0, 0.776, 0.180])
PALE = np.array([1.0, 0.93, 0.62])


def smooth(edge, value, width=1.0):
    """1 внутри (value < edge), 0 снаружи, край сглажен на width пикселей."""
    return np.clip((edge - value) / width + 0.5, 0.0, 1.0)


def grid(width, height):
    y, x = np.mgrid[0:height, 0:width]
    return x + 0.5, y + 0.5


def rounded_box(x, y, left, top, right, bottom, radius):
    """Расстояние до скруглённого прямоугольника: меньше нуля внутри."""
    cx, cy = (left + right) * 0.5, (top + bottom) * 0.5
    hx, hy = (right - left) * 0.5 - radius, (bottom - top) * 0.5 - radius
    qx, qy = np.abs(x - cx) - hx, np.abs(y - cy) - hy
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - radius


def dots(x, y, period, radius, angle=0.0):
    """Растр: 1 в кружке. При angle=0 узор повторяется с периодом period (нужно для плиток 9-slice)."""
    c, s = math.cos(angle), math.sin(angle)
    u, v = x * c + y * s, -x * s + y * c
    du = (u % period) - period * 0.5
    dv = (v % period) - period * 0.5
    return smooth(radius, np.hypot(du, dv))


def compose(*layers):
    """Слои снизу вверх: (цвет RGB или массив, альфа). Обычное наложение с непрозрачным результатом по альфе."""
    shape = layers[0][1].shape
    rgb = np.zeros(shape + (3,))
    alpha = np.zeros(shape)
    for color, a in layers:
        color = np.asarray(color)
        out = a + alpha * (1 - a)
        safe = np.where(out > 0, out, 1)
        rgb = (color * a[..., None] + rgb * (alpha * (1 - a))[..., None]) / safe[..., None]
        alpha = out
    return np.dstack([rgb, alpha])


def save(name, image, folder=None):
    """Сохранить массив (сверху вниз) в PNG. Цвет считается линейным значением sRGB: без пересчёта."""
    height, width = image.shape[:2]
    data = np.flipud(image).astype(np.float32).ravel()
    bitmap = bpy.data.images.new(name, width, height, alpha=True)
    bitmap.colorspace_settings.name = "Non-Color"
    bitmap.pixels.foreach_set(data)
    bitmap.filepath_raw = os.path.join(folder or OUT, name + ".png")
    bitmap.file_format = "PNG"
    bitmap.save()
    bpy.data.images.remove(bitmap)
    print("[zonk_ui]", name, width, "x", height)


def button():
    """Кнопка 128×128, граница 9-slice 40, плитка серединки кратна 8 (Image Tiled: точки не тянутся)."""
    size, ink, shadow, radius = 128, 8.0, 10.0, 24.0
    x, y = grid(size, size)
    # Неровность только в углах: края периодичны, иначе плитки дадут ступеньки.
    body = rounded_box(x, y, 5, 4, size - 5, size - 5 - shadow, radius)
    drop = rounded_box(x, y, 5, 4 + shadow, size - 5, size - 5, radius)
    outside = np.maximum(smooth(0, body), smooth(0, drop))
    fill_mask = smooth(-ink, body)

    shade = np.full((size, size), 0.90)
    shade = np.where(y > size - 5 - shadow - ink - 26, 0.76, shade)  # нижняя тень-ступень
    grey = shade * (1 - 0.34 * dots(x, y, 8, 2.1) * (y > size - 5 - shadow - ink - 26))
    grey = np.where((y > 4 + ink + 4) & (y < 4 + ink + 10), 1.0, grey)  # блик сверху
    color = np.dstack([grey, grey, grey])
    return compose((INK, outside), (color, fill_mask))


def panel():
    """Панель окна 192×192, граница 9-slice 64: растр только в углах, серединка ровная и тянется без искажений."""
    size, ink, radius = 192, 7.0, 22.0
    x, y = grid(size, size)
    body = rounded_box(x, y, 4, 4, size - 12, size - 12, radius)
    drop = rounded_box(x, y, 12, 12, size - 4, size - 4, radius)
    fill_mask = smooth(-ink, body)
    corner = np.maximum(np.clip(1 - (x + y) / 110, 0, 1), np.clip(((x + y) - (2 * size - 110)) / 110, 0, 1))
    grey = 1 - 0.32 * dots(x, y, 9, 3.2) * (corner > 0.05)
    color = np.dstack([grey, grey, grey])
    return compose((INK, np.maximum(smooth(0, body), smooth(0, drop))), (color, fill_mask))


def plate():
    """Табличка и строка списка 96×96, граница 9-slice 32: контур и короткая тень."""
    size, ink, radius = 96, 5.0, 15.0
    x, y = grid(size, size)
    body = rounded_box(x, y, 3, 3, size - 8, size - 8, radius)
    drop = rounded_box(x, y, 8, 8, size - 3, size - 3, radius)
    color = np.ones((size, size, 3))
    return compose((INK, np.maximum(smooth(0, body), smooth(0, drop))), (color, smooth(-ink, body)))


def ribbon():
    """Лента заголовка 256×96, граница 9-slice 72 слева и справа: концы скошены, с тенью."""
    width, height, ink = 256, 96, 6.0
    x, y = grid(width, height)

    def shape(dx, dy):
        slant = (y - dy) * 0.22
        left = (14 + dx) + slant - x
        right = x - (width - 22 + dx) + slant - height * 0.22
        top, bottom = (10 + dy) - y, y - (height - 18 + dy)
        return np.maximum.reduce([left, right, top, bottom])

    body = shape(0, 0)
    drop = shape(7, 8)
    grey = 1 - 0.25 * dots(x, y, 8, 2.0) * (y > height - 18 - ink - 14)
    color = np.dstack([grey, grey, grey])
    return compose((INK, np.maximum(smooth(0, body), smooth(0, drop))), (color, smooth(-ink, body)))


def star_points(seed, rays, cx, cy, rx, ry):
    rng = np.random.RandomState(seed)
    points = []
    for i in range(rays * 2):
        angle = math.pi * 2 * i / (rays * 2) + (rng.rand() - 0.5) * 0.07
        k = (0.86 + rng.rand() * 0.14) if i % 2 == 0 else (0.62 + rng.rand() * 0.08)
        points.append((cx + math.cos(angle) * rx * k, cy + math.sin(angle) * ry * k))
    return np.array(points)


def polygon_sdf(x, y, points):
    """Знаковое расстояние до многоугольника (меньше нуля внутри)."""
    best = np.full(x.shape, 1e9)
    inside = np.zeros(x.shape, dtype=bool)
    count = len(points)
    for i in range(count):
        ax, ay = points[i]
        bx, by = points[(i + 1) % count]
        ex, ey = bx - ax, by - ay
        wx, wy = x - ax, y - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        best = np.minimum(best, np.hypot(wx - ex * t, wy - ey * t))
        crossing = ((ay <= y) & (y < by)) | ((by <= y) & (y < ay))
        inside ^= crossing & (x < ax + (y - ay) / (ey if ey != 0 else 1e-6) * ex)
    return np.where(inside, -best, best)


def burst(seed):
    """Плашка-взрыв 1024×440: заливка белая (цвет — Image.color), растр, контур, сдвинутая тень."""
    width, height = 1024, 440
    x, y = grid(width, height)
    points = star_points(seed, 17, width * 0.49, height * 0.48, width * 0.48, height * 0.46)
    sdf = polygon_sdf(x, y, points)
    drop_sdf = polygon_sdf(x - 13, y - 17, points)
    ink = 9.0
    mask = smooth(-ink, sdf)
    grey = 1 - 0.26 * dots(x, y, 13, 3.6, math.radians(30))
    color = np.dstack([grey, grey, grey])
    return compose((INK, np.maximum(smooth(0, sdf), smooth(0, drop_sdf))), (color, mask))


FLAME_W, FLAME_H, FLAME_FRAMES = 96, 120, 8


def tongue(x, y, cx, base, height, width, frame, seed, tilt):
    """Язык пламени: 1 внутри. Форма зациклена по кадрам."""
    phase = math.pi * 2 * frame / FLAME_FRAMES
    t = np.clip((base - y) / height, 0.0, 1.0)
    wobble = (np.sin(phase + t * 6 + seed) * 0.26 + np.sin(phase * 2 + t * 11 + seed * 2) * 0.12) * width * t + tilt * t * t * width
    half = width * (1 - t) ** 0.8 * (t + 0.05) ** 0.32 * 1.35
    distance = np.abs(x - cx - wobble) - half
    return np.where((y <= base) & (y >= base - height), distance, 1e3)


def flame_frame(x, y, frame):
    h = FLAME_H
    phase = math.pi * 2 * frame / FLAME_FRAMES
    tall = h * (0.84 + 0.09 * math.sin(phase))
    left_h = h * (0.60 + 0.10 * math.sin(phase + 2))
    right_h = h * (0.54 + 0.10 * math.sin(phase + 4))
    outer = np.minimum.reduce([
        tongue(x, y, FLAME_W * 0.30, h - 8, left_h, 15, frame, 1.7, -0.5),
        tongue(x, y, FLAME_W * 0.70, h - 8, right_h, 14, frame, 4.1, 0.5),
        tongue(x, y, FLAME_W * 0.50, h - 6, tall, 25, frame, 0.4, 0.0),
    ])
    mid = tongue(x, y, FLAME_W * 0.50, h - 8, tall * 0.72, 17, frame, 0.4, 0.0)
    core = tongue(x, y, FLAME_W * 0.50, h - 8, tall * 0.42, 9, frame, 0.9, 0.0)
    return compose((INK, smooth(4.5, outer)), (RED, smooth(0, outer)), (ORANGE, smooth(0, mid)), (GOLD, smooth(0, core)))


def flame_frames():
    """Огонь: 8 кадров по 96×120, отдельными файлами. Цветной, не красится."""
    for frame in range(FLAME_FRAMES):
        x, y = grid(FLAME_W, FLAME_H)
        save("UI_Flame_%d" % frame, flame_frame(x, y, frame))


def spark():
    """Искра 32×32: ромб с контуром (огонёк, монетка на лету, блик)."""
    size = 32
    x, y = grid(size, size)
    star = (np.abs(x - 16) ** 0.7 + np.abs(y - 16) ** 0.7)
    outer = smooth(6.6, star, 0.4)
    inner = smooth(4.6, star, 0.4)
    centre = smooth(2.8, star, 0.4)
    return compose((INK, outer), (ORANGE, inner), (PALE, centre))


def main():
    comic = os.path.join(SPRITES, "Comic")
    os.makedirs(comic, exist_ok=True)
    save("UI_Button", button())
    save("UI_Panel", panel())
    save("UI_Plate", plate())
    save("UI_Ribbon", ribbon())
    save("UI_Burst_A", burst(7), comic)
    save("UI_Burst_B", burst(31), comic)
    flame_frames()
    save("UI_Spark", spark())


main()
