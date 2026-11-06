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
    band = y >= 90  # только в нижней границе 9-slice: в плитке серединки полоса повторялась бы на всю кнопку
    shade = np.where(band, 0.76, shade)  # нижняя тень-ступень
    grey = shade * (1 - 0.34 * dots(x, y, 8, 2.1) * band)
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


def capsule(x, y, ax, ay, bx, by, r):
    """Расстояние до отрезка-капсулы (толстой линии): меньше нуля внутри."""
    px, py, vx, vy = x - ax, y - ay, bx - ax, by - ay
    t = np.clip((px * vx + py * vy) / (vx * vx + vy * vy), 0, 1)
    return np.hypot(px - vx * t, py - vy * t) - r


def question_mark(x, y, cx, cy, s):
    """Знак вопроса высотой ~s: дуга сверху, крючок к середине, ножка, точка. Расстояние (меньше нуля внутри)."""
    w = 0.1 * s
    ox, oy, r = cx, cy - 0.2 * s, 0.2 * s
    ring = np.abs(np.hypot(x - ox, y - oy) - r) - w
    angle = np.arctan2(y - oy, x - ox)
    ring = np.where(angle > 0.7, 1e3, ring)                                   # без нижней левой четверти
    ex, ey = ox + r * np.cos(0.7), oy + r * np.sin(0.7)
    hook = capsule(x, y, ex, ey, cx, cy + 0.06 * s, w)
    stem = capsule(x, y, cx, cy + 0.06 * s, cx, cy + 0.14 * s, w)
    dot = np.hypot(x - cx, y - (cy + 0.42 * s)) - w * 1.15
    return np.minimum(np.minimum(np.minimum(ring, hook), stem), dot)


def rules_book():
    """Книжка правил 256×256: обложка с растром в тени, корешок, обрез страниц, золотой «?» с контуром, жёсткая тень."""
    size, ink = 256, 7.0
    x, y = grid(size, size)
    cover = rounded_box(x, y, 52, 26, 206, 214, 14)
    shadow = rounded_box(x, y, 64, 38, 218, 226, 14)
    pages = rounded_box(x, y, 190, 36, 224, 204, 8)
    spine = (x < 80)
    red, red_dark, cream = np.array([0.84, 0.2, 0.16]), np.array([0.55, 0.1, 0.08]), np.array([0.98, 0.93, 0.8])
    shade = (x + y) > 300                                                     # тень обложки — растр
    cover_rgb = np.where(spine[..., None], red_dark, red)
    cover_rgb = np.where((shade & (dots(x, y, 9, 2.6) > 0.5))[..., None], red_dark, cover_rgb)
    lines = ((y.astype(int) // 8) % 2 == 0)
    page_rgb = np.where(lines[..., None], cream, cream * 0.86)
    mark = question_mark(x, y, 132, 112, 128)
    gold_rgb = np.where((y < 80)[..., None], np.array([1.0, 0.92, 0.5]), GOLD)                # блик сверху
    return compose((INK, smooth(0, shadow)),
                   (INK, smooth(0, pages)), (page_rgb, smooth(-5, pages)),
                   (INK, smooth(0, cover)), (cover_rgb, smooth(-ink, cover)),
                   (INK, smooth(0, mark - 7)), (gold_rgb, smooth(0, mark)))


def close_icon():
    """Кнопка «закрыть» 128×128: красный круг с контуром и жёсткой тенью, белый крест с контуром, блик. Без текста."""
    size = 128
    x, y = grid(size, size)
    circle = np.hypot(x - 61, y - 59) - 50
    drop = np.hypot(x - 67, y - 66) - 50
    a, b = 17, 17
    cross = np.minimum(capsule(x, y, 61 - a, 59 - b, 61 + a, 59 + b, 7.5), capsule(x, y, 61 - a, 59 + b, 61 + a, 59 - b, 7.5))
    shine = np.abs(np.hypot(x - 61, y - 59) - 38) - 3.5
    shine = np.where((np.arctan2(y - 59, x - 61) > -2.7) & (np.arctan2(y - 59, x - 61) < -1.9), shine, 1e3)
    red = np.where((((x - 61) + (y - 59)) > 30)[..., None] & (dots(x, y, 7, 1.9) > 0.5)[..., None],
                   np.array([0.62, 0.12, 0.09]), np.array([0.88, 0.2, 0.15]))
    return compose((INK, smooth(0, drop)), (INK, smooth(0, circle)), (red, smooth(-6, circle)),
                   (np.array([1.0, 0.75, 0.68]), smooth(0, shine)),
                   (INK, smooth(0, cross - 5)), (np.array([1.0, 1.0, 1.0]), smooth(0, cross)))


def plus_icon():
    """Значок «есть свободные очки» 128×128: зелёный круг с контуром и тенью, белый плюс с контуром, блик."""
    size = 128
    x, y = grid(size, size)
    circle = np.hypot(x - 61, y - 59) - 50
    drop = np.hypot(x - 67, y - 66) - 50
    plus = np.minimum(capsule(x, y, 61, 59 - 25, 61, 59 + 25, 9), capsule(x, y, 61 - 25, 59, 61 + 25, 59, 9))
    shine = np.abs(np.hypot(x - 61, y - 59) - 38) - 3.5
    shine = np.where((np.arctan2(y - 59, x - 61) > -2.7) & (np.arctan2(y - 59, x - 61) < -1.9), shine, 1e3)
    green = np.where((((x - 61) + (y - 59)) > 30)[..., None] & (dots(x, y, 7, 1.9) > 0.5)[..., None],
                     np.array([0.12, 0.45, 0.2]), np.array([0.25, 0.72, 0.3]))
    return compose((INK, smooth(0, drop)), (INK, smooth(0, circle)), (green, smooth(-6, circle)),
                   (np.array([0.75, 1.0, 0.75]), smooth(0, shine)),
                   (INK, smooth(0, plus - 5)), (np.array([1.0, 1.0, 1.0]), smooth(0, plus)))


def regular_star(cx, cy, r_out, r_in, rays=5, rot=-math.pi / 2):
    pts = []
    for i in range(rays * 2):
        r = r_out if i % 2 == 0 else r_in
        a = rot + math.pi * i / rays
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def star_icon(fill, dark, light):
    """Звезда 64×64: острые лучи, чернильный контур, жёсткая тень, светлая грань сверху-слева."""
    size = 64
    x, y = grid(size, size)
    star = polygon_sdf(x, y, regular_star(31, 33, 28, 12))
    drop = polygon_sdf(x, y, regular_star(34, 36, 28, 12))
    left = (x - 31) * 0.8 + (y - 33) < -2                                      # верхне-левая половина светлее
    rgb = np.where(left[..., None], light, fill)
    rgb = np.where(((x - 31) + (y - 33) > 10)[..., None] & (dots(x, y, 5, 1.2) > 0.5)[..., None], dark, rgb)
    return compose((INK, smooth(0, drop)), (INK, smooth(0, star)), (rgb, smooth(-3.5, star)))


def gold_star():
    return star_icon(GOLD, GOLD * 0.7, np.array([1.0, 0.92, 0.55]))


def gray_star():
    return star_icon(np.array([0.58, 0.58, 0.62]), np.array([0.42, 0.42, 0.46]), np.array([0.74, 0.74, 0.78]))


def coin_icon():
    """Монета 64×64: золотой круг с контуром, внутренний ободок, звезда-чекан, блик, жёсткая тень."""
    size = 64
    x, y = grid(size, size)
    r = np.hypot(x - 30, y - 30)
    disc, drop = r - 26, np.hypot(x - 33, y - 33) - 26
    rim = np.abs(r - 18) - 1.6
    mark = polygon_sdf(x, y, regular_star(30, 31, 11, 5))
    shine = capsule(x, y, 17, 25, 22, 16, 2.4)
    gold = np.where(((x - 30) + (y - 30) > 14)[..., None] & (dots(x, y, 5, 1.2) > 0.5)[..., None], GOLD * 0.72, GOLD)
    return compose((INK, smooth(0, drop)), (INK, smooth(0, disc)), (gold, smooth(-3.5, disc)),
                   (GOLD * 0.62, smooth(0, rim)), (GOLD * 0.62, smooth(0, mark)), (np.array([1.0, 0.97, 0.8]), smooth(0, shine)))


def trophy_icon():
    """Кубок 128×128 белый (цвет редкости задаёт Image.color): чаша, ручки, ножка, подставка, контур, растр в тени."""
    size = 128
    x, y = grid(size, size)
    bowl = np.maximum(rounded_box(x, y, 34, 14, 94, 74, 26), y - 72)                 # чаша, сверху срезана ровно
    bowl = np.minimum(bowl, rounded_box(x, y, 34, 14, 94, 40, 4))
    handles = np.minimum(np.abs(np.hypot(x - 32, y - 36) - 13) - 4, np.abs(np.hypot(x - 96, y - 36) - 13) - 4)
    stem = rounded_box(x, y, 56, 70, 72, 96, 3)
    base = rounded_box(x, y, 38, 94, 90, 110, 5)
    body = np.minimum(np.minimum(np.minimum(bowl, handles), stem), base)
    drop = np.minimum(np.minimum(np.minimum(rounded_box(x - 5, y - 5, 34, 14, 94, 74, 26), stem + 0), base), handles)
    shade = (x > 70)
    grey = np.where((shade & (dots(x, y, 6, 1.6) > 0.5))[..., None], np.array([0.72, 0.72, 0.72]), np.array([1.0, 1.0, 1.0]))
    shine = capsule(x, y, 46, 24, 46, 52, 3.5)
    star = polygon_sdf(x, y, regular_star(64, 42, 12, 5))
    return compose((INK, smooth(0, np.minimum(body, rounded_box(x, y, 40, 99, 96, 116, 5)))), (INK, smooth(0, body)),
                   (grey, smooth(-4.5, body)), (np.array([0.8, 0.8, 0.8]), smooth(0, star)),
                   (np.array([1.0, 1.0, 1.0]), smooth(0, shine)))


def chest_icon():
    """Сундук 256×256: доски с линиями, золотые полосы, замок, крышка дугой, контур, жёсткая тень, растр в тени."""
    size = 256
    x, y = grid(size, size)
    wood, wood_dark, gold, gold_dark = np.array([0.6, 0.34, 0.16]), np.array([0.4, 0.21, 0.09]), GOLD, GOLD * 0.7
    box = rounded_box(x, y, 30, 110, 226, 222, 10)
    lid = np.maximum(np.hypot((x - 128) / 1.0, (y - 118) * 1.55) - 100, y - 112)
    body = np.minimum(box, lid)
    drop = np.minimum(rounded_box(x, y, 40, 120, 236, 232, 10), np.maximum(np.hypot(x - 138, (y - 128) * 1.55) - 100, y - 122))
    planks = (((y - 110).astype(int) // 28) % 2 == 0) & (y > 112)
    rgb = np.where(planks[..., None], wood, wood * 0.88)
    rgb = np.where(((x > 150) & (dots(x, y, 9, 2.4) > 0.5))[..., None], wood_dark, rgb)
    seams = np.abs(((y - 110) % 28) - 14) > 12.8
    rgb = np.where((seams & (y > 112))[..., None], wood_dark, rgb)
    bands = np.minimum(np.abs(x - 70) - 11, np.abs(x - 186) - 11)
    rim = np.abs(y - 112) - 7
    lock = rounded_box(x, y, 108, 98, 148, 146, 6)
    hole = np.minimum(np.hypot(x - 128, y - 116) - 6, rounded_box(x, y, 125, 116, 131, 132, 2))
    band_rgb = np.where((x > 150)[..., None], gold_dark, gold)
    inside = smooth(-6, body)
    return compose((INK, smooth(0, drop)), (INK, smooth(0, body)), (rgb, inside),
                   (INK, smooth(0, np.maximum(bands - 3, body))), (band_rgb, smooth(0, np.maximum(bands, body + 6))),
                   (INK, smooth(0, np.maximum(rim - 3, body))), (gold, smooth(0, np.maximum(rim, body + 6))),
                   (INK, smooth(0, lock + 4)), (gold, smooth(0, lock)), (INK, smooth(0, hole)))


def level_badge():
    """Медаль уровня 128×128 белая (золото задаёт Image.color): зубчатый край, контур, ободок, растр в тени, тень."""
    size = 128
    x, y = grid(size, size)
    a = np.arctan2(y - 61, x - 61)
    r = np.hypot(x - 61, y - 61)
    edge = 52 + 4.5 * np.cos(a * 14)
    medal = r - edge
    drop = np.hypot(x - 66, y - 66) - (52 + 4.5 * np.cos(np.arctan2(y - 66, x - 66) * 14))
    ring = np.abs(r - 38) - 2.5
    grey = np.where((((x - 61) + (y - 61)) > 34)[..., None] & (dots(x, y, 7, 1.9) > 0.5)[..., None],
                    np.array([0.7, 0.7, 0.7]), np.array([1.0, 1.0, 1.0]))
    shine = np.abs(r - 45) - 2.5
    shine = np.where((a > -2.6) & (a < -1.9), shine, 1e3)
    return compose((INK, smooth(0, drop)), (INK, smooth(0, medal)), (grey, smooth(-5, medal)),
                   (np.array([0.72, 0.72, 0.72]), smooth(0, ring)), (np.array([1.0, 1.0, 1.0]), smooth(0, shine)))


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
    save("Icon_RulesBook", rules_book())          # знак вопроса в самой картинке: кнопка без текста
    save("UI_Icon_Close", close_icon())          # закрыть окно: рисунок вместо символа «×» (он стоял не по центру)
    save("UI_Icon_Plus", plus_icon())            # свободные очки талантов у уровня игрока
    save("Star_Gold", gold_star())
    save("Star_Gray", gray_star())
    save("Coin", coin_icon())
    save("Trophy", trophy_icon())                # белый: цвет редкости задаёт Image.color
    save("Chest", chest_icon())
    save("UI_Badge_Level", level_badge())       # подложка номера уровня (Parts/PlayerLevel)


if __name__ == "__main__" and "--only" in sys.argv:
    # Только названные спрайты (через запятую): остальные не перерисовываются.
    _map = {"Icon_RulesBook": rules_book, "UI_Icon_Close": close_icon, "UI_Icon_Plus": plus_icon, "Star_Gold": gold_star,
            "Star_Gray": gray_star, "Coin": coin_icon, "Trophy": trophy_icon, "Chest": chest_icon, "UI_Badge_Level": level_badge}
    for _name in sys.argv[sys.argv.index("--only") + 1].split(","):
        save(_name, _map[_name]())
elif __name__ == "__main__":
    main()
