# -*- coding: utf-8 -*-
"""
Сукно стола для игры «Зонк»: узоры в комиксовом стиле (ровные пятна, тёмные линии, без мелкого шума).

Плитки 256×256 — бесшовные, повторяются по сукну (материал с тайлингом, как сукна ZonkSetup.ShopV5):
    Felt_Leather         кожа с тиснением ромбами и пуговками (стёжка «честерфилд»)
    Felt_Club            клубное зелёное сукно с тонкой сеткой ромбов
    Felt_Suits           тёмное сукно с мастями карт
Целые полотна 1024×640 (пропорции сукна 3.2 × 2), без повтора: ковёр, бархат и поле симметричные, небо и карта —
картины, у каждого игрока своя половина:
    Felt_Rug             восточный ковёр: бордюры, медальон в центре
    Felt_Velvet          бархат с отливом и золотым кантом по краю
    Felt_Board           игровое поле: золотистое сукно с разметкой и шестью точками в центре
    Felt_Constellations  ночное небо: Млечный Путь, созвездия у краёв, луна, звёздный круг в середине (05.10: была плитка)
    Felt_TreasureMap     карта сокровищ: остров с кладом, остров отплытия, маршрут, роза ветров (05.10: была плитка)

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_felt.py
    blender --background --factory-startup --python Tools/Blender/zonk_felt.py -- --render <папка>   (превью)
Результат: Assets/ZonkContent/Art/Textures/Felt/<Имя>.png. Подключает генератор ZonkSetup.LampsFeltV2.
"""

import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402

OUT_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Felt")
TILE = 256
WIDE_W, WIDE_H = 1024, 640


def col(r, g, b):
    return np.array([r, g, b], dtype=np.float32)


def mix(a, b, t):
    t = np.asarray(t, dtype=np.float32)
    if t.ndim == 2:
        t = t[..., None]
    return a * (1 - t) + b * t


def grid(w, h):
    """u, v в 0..1 (центры пикселей). Строка 0 — низ картинки."""
    u = (np.arange(w)[None, :] + 0.5) / w * np.ones((h, 1))
    v = (np.arange(h)[:, None] + 0.5) / h * np.ones((1, w))
    return u.astype(np.float32), v.astype(np.float32)


def wrap_d(a, b):
    """Расстояние на окружности 0..1 (для бесшовных узоров)."""
    d = np.abs(a - b) % 1.0
    return np.minimum(d, 1.0 - d)


def soft(d, r, px):
    """Покрытие круга радиуса r (в долях) с мягким краем в px пикселей."""
    return np.clip((r - d) * px + 0.5, 0.0, 1.0)


def nap(img, v, k=0.03, rows=24):
    """Ворс сукна: едва заметные полосы, чтобы не было пластика."""
    f = 1.0 - k + k * np.sin(v * np.pi * 2 * rows)
    return img * f[..., None]


# ---------------------------------------------------------------------------
# Плитки
# ---------------------------------------------------------------------------

def leather():
    u, v = grid(TILE, TILE)
    base = col(0.52, 0.12, 0.10)
    # Ромбы стёжки: 2×2 ромба на плитку. Расстояние до швов — по диагоналям.
    a = (u + v) * 2.0
    b = (u - v) * 2.0
    da = np.abs(a - np.round(a))
    db = np.abs(b - np.round(b))
    seam = np.minimum(da, db)
    # Подушка: светлее в центре ромба, темнее у шва.
    cushion = np.clip(seam * 2.2, 0, 1)
    img = mix(base * 0.62, base * 1.18, cushion)
    img = mix(img, col(0.95, 0.55, 0.42), np.clip((cushion - 0.85) * 4, 0, 1) * 0.35)
    img = mix(img, col(0.16, 0.03, 0.02), (seam < 0.012).astype(np.float32))
    # Пуговки на пересечениях швов.
    cu = (np.round(a) + np.round(b)) / 4.0
    cv = (np.round(a) - np.round(b)) / 4.0
    d = np.hypot(wrap_d(u, cu), wrap_d(v, cv))
    img = mix(img, col(0.2, 0.04, 0.03), soft(d, 0.028, TILE))
    img = mix(img, col(0.7, 0.3, 0.22), soft(d, 0.012, TILE) * 0.6)
    return img


def club():
    u, v = grid(TILE, TILE)
    img = nap(mix(col(0.07, 0.36, 0.18), col(0.1, 0.42, 0.22), 0.5 + 0.5 * np.sin(u * np.pi * 2) * np.sin(v * np.pi * 2)), v)
    a = (u + v) * 4.0
    b = (u - v) * 4.0
    seam = np.minimum(np.abs(a - np.round(a)), np.abs(b - np.round(b)))
    img = mix(img, col(0.18, 0.55, 0.32), (seam < 0.025).astype(np.float32) * 0.8)
    # Маленькие золотые ромбики в центрах клеток.
    ca = np.abs(a - np.floor(a) - 0.5) + np.abs(b - np.floor(b) - 0.5)
    img = mix(img, col(0.9, 0.72, 0.3), (ca < 0.16).astype(np.float32) * ((np.floor(a) + np.floor(b)) % 2 == 0))
    return img


def suit_mask(u, v, kind, cx, cy, s):
    """Маска масти в точке (cx, cy) размера s (бесшовно)."""
    x = (u - cx + 0.5) % 1.0 - 0.5
    y = (v - cy + 0.5) % 1.0 - 0.5
    x, y = x / s, y / s
    if kind == "diamond":
        return (np.abs(x) * 0.8 + np.abs(y) * 0.6 < 0.5).astype(np.float32)
    if kind == "heart":
        lobes = (np.hypot(x - 0.24, y - 0.15) < 0.27) | (np.hypot(x + 0.24, y - 0.15) < 0.27)
        tip = (y < 0.18) & (y > -0.55) & (np.abs(x) < (y + 0.55) * 0.68)
        return (lobes | tip).astype(np.float32)
    if kind == "spade":
        lobes = (np.hypot(x - 0.24, y + 0.08) < 0.27) | (np.hypot(x + 0.24, y + 0.08) < 0.27)
        tip = (y > -0.1) & (y < 0.55) & (np.abs(x) < (0.55 - y) * 0.68)
        stem = (y < -0.1) & (y > -0.55) & (np.abs(x) < 0.06 + (-0.1 - y) * 0.35)
        return (lobes | tip | stem).astype(np.float32)
    # club
    leaves = (np.hypot(x, y - 0.25) < 0.22) | (np.hypot(x - 0.24, y - 0.0) < 0.22) | (np.hypot(x + 0.24, y - 0.0) < 0.22)
    stem = (y < 0.0) & (y > -0.55) & (np.abs(x) < 0.05 + (-y) * 0.3)
    return (leaves | stem).astype(np.float32)


def suits():
    u, v = grid(TILE, TILE)
    img = nap(col(0.08, 0.16, 0.12) * np.ones((TILE, TILE, 1), dtype=np.float32), v)
    red, dark = col(0.55, 0.14, 0.12), col(0.03, 0.06, 0.05)
    layout = [("spade", 0.25, 0.25, dark), ("heart", 0.75, 0.25, red), ("diamond", 0.25, 0.75, red), ("club", 0.75, 0.75, dark)]
    for kind, x, y, c in layout:
        outline = suit_mask(u, v, kind, x, y, 0.26)
        img = mix(img, col(0.75, 0.62, 0.3), outline * 0.9)
        img = mix(img, c, suit_mask(u, v, kind, x, y, 0.22))
    return img


# ---------------------------------------------------------------------------
# Целые полотна
# ---------------------------------------------------------------------------

def frame_d(u, v):
    """Расстояние до края полотна в долях высоты (по ширине — с учётом пропорций)."""
    aspect = WIDE_W / WIDE_H
    return np.minimum(np.minimum(u, 1 - u) * aspect, np.minimum(v, 1 - v))


# ---------------------------------------------------------------------------
# Рисование на полотне: координаты x (−0.8…0.8 вдоль стола), y (−0.5…0.5), центр — середина сукна.
# Каждый элемент считается только в своём окне пикселей — полотно с сотнями звёзд собирается за секунды.
# ---------------------------------------------------------------------------

ASPECT = WIDE_W / WIDE_H


def _window(x0, y0, x1, y1, pad):
    """Окно пикселей полотна вокруг прямоугольника (в координатах x, y) и координаты пикселей в нём."""
    c0 = max(0, int(((min(x0, x1) - pad) / ASPECT + 0.5) * WIDE_W) - 1)
    c1 = min(WIDE_W, int(((max(x0, x1) + pad) / ASPECT + 0.5) * WIDE_W) + 2)
    r0 = max(0, int((min(y0, y1) - pad + 0.5) * WIDE_H) - 1)
    r1 = min(WIDE_H, int((max(y0, y1) + pad + 0.5) * WIDE_H) + 2)
    if c0 >= c1 or r0 >= r1:
        return None
    xs = ((np.arange(c0, c1) + 0.5) / WIDE_W - 0.5) * ASPECT
    ys = (np.arange(r0, r1) + 0.5) / WIDE_H - 0.5
    return (slice(r0, r1), slice(c0, c1)), xs[None, :], ys[:, None]


def disc(img, x, y, r, c, a=1.0):
    win = _window(x, y, x, y, r)
    if win is None:
        return
    sl, xs, ys = win
    img[sl] = mix(img[sl], c, soft(np.hypot(xs - x, ys - y), r, WIDE_H) * a)


def ring(img, x, y, r, w, c, a=1.0):
    win = _window(x, y, x, y, r + w)
    if win is None:
        return
    sl, xs, ys = win
    img[sl] = mix(img[sl], c, soft(np.abs(np.hypot(xs - x, ys - y) - r), w, WIDE_H) * a)


def segment(img, x0, y0, x1, y1, w, c, a=1.0):
    win = _window(x0, y0, x1, y1, w)
    if win is None:
        return
    sl, xs, ys = win
    dx, dy = x1 - x0, y1 - y0
    t = np.clip(((xs - x0) * dx + (ys - y0) * dy) / max(dx * dx + dy * dy, 1e-9), 0, 1)
    d = np.hypot(xs - (x0 + t * dx), ys - (y0 + t * dy))
    img[sl] = mix(img[sl], c, soft(d, w, WIDE_H) * a)


def polyline(img, pts, w, c, a=1.0):
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        segment(img, x0, y0, x1, y1, w, c, a)


def sparkle(img, x, y, size, c, core):
    """Звезда-искра как в комиксе: четыре луча крестом и круглая сердцевина."""
    win = _window(x, y, x, y, size)
    if win is None:
        return
    sl, xs, ys = win
    dx, dy = np.abs(xs - x), np.abs(ys - y)
    rays = (dx / size + dy / (size * 0.16) < 1) | (dy / size + dx / (size * 0.16) < 1)
    img[sl] = mix(img[sl], c, rays.astype(np.float32))
    disc(img, x, y, size * 0.28, core)


def polygon_mask(xs, ys, pts):
    """Маска выпуклого или звёздного многоугольника (чётно-нечётное правило)."""
    inside = np.zeros(np.broadcast(xs, ys).shape, dtype=bool)
    n = len(pts)
    for i in range(n):
        (xa, ya), (xb, yb) = pts[i], pts[(i + 1) % n]
        cond = ((ya > ys) != (yb > ys))
        xcross = (xb - xa) * (ys - ya) / ((yb - ya) if yb != ya else 1e-9) + xa
        inside ^= cond & (xs < xcross)
    return inside


def fill_polygon(img, pts, c, outline=None, w=0.003):
    xs_ = [p[0] for p in pts]
    ys_ = [p[1] for p in pts]
    win = _window(min(xs_), min(ys_), max(xs_), max(ys_), w * 2)
    if win is None:
        return
    sl, xs, ys = win
    img[sl] = mix(img[sl], c, polygon_mask(xs, ys, pts).astype(np.float32))
    if outline is not None:
        polyline(img, pts + [pts[0]], w, outline)


def frame(img, d, dark, line):
    """Рамка полотна: тёмная кайма и тонкая светлая линия внутри."""
    img = mix(img, dark, (d < 0.035).astype(np.float32))
    return mix(img, line, ((d > 0.035) & (d < 0.042)).astype(np.float32))


def sky_wide():
    """
    Ночное небо целиком на сукне: Млечный Путь наискосок, у краёв — знакомые созвездия (у каждого игрока свои),
    в углу луна, падающая звезда; середина спокойная (там катятся кости), в ней тонкий звёздный круг.
    """
    u, v = grid(WIDE_W, WIDE_H)
    x = (u - 0.5) * ASPECT
    y = v - 0.5
    rng = np.random.default_rng(21)
    n = zm.periodic_noise(rng, WIDE_H, WIDE_W, 140.0, power=2.4)

    # Небо тремя ровными тонами: туманности — пятна, а не каша.
    img = mix(col(0.03, 0.04, 0.13), col(0.05, 0.07, 0.2), zm.smoothstep(-0.35, -0.25, n))
    img = mix(img, col(0.09, 0.08, 0.27), zm.smoothstep(0.85, 0.95, n))

    # Млечный Путь: широкая полоса наискосок, внутри — светлая сердцевина.
    k = 0.42
    band = np.abs(y - k * x - 0.03 + 0.03 * n) / math.sqrt(1 + k * k)
    img = mix(img, col(0.16, 0.13, 0.38), zm.smoothstep(0.15, 0.14, band))
    img = mix(img, col(0.26, 0.22, 0.52), zm.smoothstep(0.055, 0.045, band + 0.02 * n))
    for _ in range(420):
        t = rng.uniform(-0.95, 0.95)
        off = rng.normal(0, 0.055)
        sx = t - off * k / math.sqrt(1 + k * k)
        sy = k * t + 0.03 + off / math.sqrt(1 + k * k)
        disc(img, sx, sy, rng.uniform(0.0012, 0.0024), col(0.85, 0.85, 1.0), rng.uniform(0.5, 1.0))

    # Россыпь звёзд по всему небу.
    for _ in range(320):
        sx, sy = rng.uniform(-0.78, 0.78), rng.uniform(-0.48, 0.48)
        disc(img, sx, sy, rng.uniform(0.0012, 0.003), col(0.82, 0.85, 1.0), rng.uniform(0.45, 1.0))
    for _ in range(26):
        sx, sy = rng.uniform(-0.75, 0.75), rng.uniform(-0.45, 0.45)
        sparkle(img, sx, sy, rng.uniform(0.008, 0.013), col(0.75, 0.8, 1.0), col(1, 1, 1))

    # Звёздный круг в середине: две тонкие окружности и 12 рисок — как на старой звёздной карте.
    gold_dim = col(0.62, 0.52, 0.3)
    ring(img, 0, 0, 0.24, 0.0018, gold_dim, 0.7)
    ring(img, 0, 0, 0.262, 0.0014, gold_dim, 0.55)
    for i in range(12):
        a = i * math.pi / 6
        segment(img, math.cos(a) * 0.24, math.sin(a) * 0.24, math.cos(a) * 0.262, math.sin(a) * 0.262, 0.0018, gold_dim, 0.7)
        disc(img, math.cos(a + math.pi / 12) * 0.251, math.sin(a + math.pi / 12) * 0.251, 0.003, gold_dim, 0.7)

    # Созвездия у краёв: линии бледно-голубые, звёзды — золотые искры.
    line = col(0.55, 0.62, 0.95)
    gold = col(1.0, 0.85, 0.45)
    figures = [
        # Большая Медведица (ковш) — слева сверху
        [(-0.62, 0.17), (-0.52, 0.2), (-0.43, 0.18), (-0.35, 0.22), (-0.33, 0.33), (-0.22, 0.34), (-0.22, 0.23), (-0.35, 0.22)],
        # Орион: плечи, пояс, ноги — слева снизу
        [(-0.62, -0.12), (-0.52, -0.23)], [(-0.44, -0.11), (-0.5, -0.24)],
        [(-0.52, -0.23), (-0.505, -0.235), (-0.49, -0.24), (-0.5, -0.24)],
        [(-0.52, -0.23), (-0.6, -0.37)], [(-0.5, -0.24), (-0.42, -0.36)],
        # Кассиопея (W) — справа снизу
        [(0.22, -0.36), (0.3, -0.27), (0.38, -0.34), (0.46, -0.25), (0.56, -0.31)],
        # Лебедь (крест) — справа сверху
        [(0.56, 0.36), (0.55, 0.2), (0.54, 0.06)], [(0.44, 0.24), (0.55, 0.2), (0.66, 0.15)],
        # Малый ковш — сверху посередине у края
        [(0.02, 0.38), (0.1, 0.35), (0.17, 0.37), (0.22, 0.32), (0.29, 0.34), (0.28, 0.41), (0.22, 0.4), (0.22, 0.32)],
    ]
    for pts in figures:
        polyline(img, pts, 0.0016, line, 0.75)
    for pts in figures:
        for (sx, sy) in pts:
            sparkle(img, sx, sy, 0.016, gold, col(1.0, 0.95, 0.8))

    # Луна-серп в левом верхнем углу и падающая звезда.
    disc(img, -0.66, 0.36, 0.06, col(0.98, 0.9, 0.6))
    disc(img, -0.635, 0.375, 0.052, col(0.03, 0.04, 0.13))
    for i in range(16):
        t = i / 15.0
        segment(img, 0.02 + 0.22 * t, -0.3 + 0.08 * t, 0.02 + 0.22 * (t + 0.07), -0.3 + 0.08 * (t + 0.07),
                0.0012 + 0.003 * t, col(0.9, 0.92, 1.0), 0.15 + 0.85 * t)
    sparkle(img, 0.26, -0.215, 0.02, col(1, 1, 1), col(1, 1, 0.9))

    img = frame(img, frame_d(u, v), col(0.02, 0.02, 0.07), col(0.8, 0.66, 0.36))
    return nap(img, v, k=0.02, rows=60)


def map_wide():
    """
    Карта сокровищ во всё сукно: остров с кладом (горы, крест), остров отплытия, пунктир маршрута через море,
    роза ветров, волны-«тильды», рамка как у старой карты. Середина — море с маршрутом: кости на нём читаются.
    """
    u, v = grid(WIDE_W, WIDE_H)
    x = (u - 0.5) * ASPECT
    y = v - 0.5
    rng = np.random.default_rng(31)
    n = zm.periodic_noise(rng, WIDE_H, WIDE_W, 60.0, power=2.2)

    # Острова: поле «насколько внутри» (1 в центре, 0 на берегу), берега неровные от шума.
    islands = [(0.47, 0.06, 0.25, 0.2), (-0.52, -0.16, 0.14, 0.11), (-0.18, 0.31, 0.055, 0.04),
               (0.08, -0.35, 0.05, 0.035), (0.68, -0.33, 0.04, 0.032)]
    f = np.full(x.shape, -9.0, dtype=np.float32)
    for (cx, cy, rx, ry) in islands:
        f = np.maximum(f, 1.0 - np.hypot((x - cx) / rx, (y - cy) / ry))
    f = f + 0.09 * n

    sea = mix(col(0.06, 0.33, 0.42), col(0.08, 0.4, 0.48), zm.smoothstep(-0.2, -0.1, n))
    img = mix(sea, col(0.32, 0.62, 0.62), zm.smoothstep(-0.17, -0.16, f))            # мель у берега
    img = mix(img, col(0.18, 0.12, 0.08), zm.smoothstep(-0.012, -0.004, f))          # линия берега
    img = mix(img, col(0.92, 0.8, 0.55), zm.smoothstep(0.004, 0.012, f))             # песок
    img = mix(img, col(0.52, 0.66, 0.32), zm.smoothstep(0.3, 0.31, f))               # трава
    img = mix(img, col(0.38, 0.52, 0.25), zm.smoothstep(0.6, 0.61, f))               # холмы
    shallow_line = np.abs(f + 0.165) < 0.004
    img = mix(img, col(0.45, 0.72, 0.72), shallow_line.astype(np.float32) * 0.8)

    # Горы на острове клада.
    for (mx, my, s) in ((0.41, 0.13, 0.05), (0.48, 0.16, 0.06), (0.55, 0.12, 0.045)):
        fill_polygon(img, [(mx - s, my - s * 0.6), (mx + s, my - s * 0.6), (mx, my + s * 0.8)],
                     col(0.55, 0.48, 0.4), col(0.18, 0.12, 0.08), 0.0025)
        fill_polygon(img, [(mx - s * 0.32, my + s * 0.4), (mx + s * 0.32, my + s * 0.4), (mx, my + s * 0.8)], col(0.95, 0.95, 0.95))
    # Пальмы на острове отплытия: ствол и три листа.
    for (px, py) in ((-0.56, -0.14), (-0.48, -0.18)):
        segment(img, px, py, px + 0.01, py + 0.05, 0.004, col(0.4, 0.26, 0.12))
        for (lx, ly) in ((-0.03, 0.0), (0.035, 0.005), (0.0, 0.025)):
            segment(img, px + 0.01, py + 0.05, px + 0.01 + lx, py + 0.05 + ly - 0.012, 0.005, col(0.22, 0.48, 0.2))

    # Пунктир маршрута: кривая Безье от острова отплытия через море к кладу.
    p0, p1, p2, p3 = (-0.42, -0.12), (-0.15, -0.45), (0.15, -0.38), (0.42, -0.04)
    pts = []
    for i in range(121):
        t = i / 120.0
        a, b, c_, d_ = (1 - t) ** 3, 3 * t * (1 - t) ** 2, 3 * t * t * (1 - t), t ** 3
        pts.append((a * p0[0] + b * p1[0] + c_ * p2[0] + d_ * p3[0], a * p0[1] + b * p1[1] + c_ * p2[1] + d_ * p3[1]))
    for i in range(0, 120, 4):
        polyline(img, pts[i:i + 3], 0.004, col(0.75, 0.12, 0.08))

    # Крест «здесь клад» с тёмной обводкой.
    for sgn in (-1, 1):
        segment(img, 0.42 - 0.03, -0.04 - 0.03 * sgn, 0.42 + 0.03, -0.04 + 0.03 * sgn, 0.011, col(0.2, 0.05, 0.03))
    for sgn in (-1, 1):
        segment(img, 0.42 - 0.028, -0.04 - 0.028 * sgn, 0.42 + 0.028, -0.04 + 0.028 * sgn, 0.0075, col(0.85, 0.12, 0.06))

    # Волны-«тильды» в открытом море.
    for (wx, wy) in ((-0.2, 0.05), (-0.05, 0.18), (0.12, 0.3), (-0.36, 0.12), (0.2, -0.18), (-0.25, -0.33),
                     (0.38, -0.3), (-0.65, 0.05), (0.08, 0.02), (0.68, 0.32), (-0.42, 0.36)):
        wave = [(wx + 0.05 * (i / 12.0 - 0.5), wy + 0.007 * math.sin(i / 12.0 * math.pi * 2)) for i in range(13)]
        polyline(img, wave, 0.0022, col(0.6, 0.85, 0.85), 0.9)

    # Роза ветров в левом верхнем углу: четыре длинных луча, четыре коротких, кольцо.
    cx, cy = -0.6, 0.3
    ring(img, cx, cy, 0.075, 0.002, col(0.95, 0.88, 0.65))
    for i in range(8):
        a = i * math.pi / 4 + math.pi / 2
        length = 0.1 if i % 2 == 0 else 0.055
        side = a + math.pi / 2
        tip = (cx + math.cos(a) * length, cy + math.sin(a) * length)
        left = (cx + math.cos(side) * 0.016, cy + math.sin(side) * 0.016)
        right = (cx - math.cos(side) * 0.016, cy - math.sin(side) * 0.016)
        fill_polygon(img, [left, tip, right], col(0.95, 0.88, 0.65) if i % 2 == 0 else col(0.75, 0.6, 0.35),
                     col(0.18, 0.12, 0.08), 0.0018)
    fill_polygon(img, [(cx - 0.016, cy), (cx, cy + 0.1), (cx + 0.016, cy)], col(0.8, 0.15, 0.08), col(0.18, 0.12, 0.08), 0.0018)
    disc(img, cx, cy, 0.008, col(0.18, 0.12, 0.08))

    img = frame(img, frame_d(u, v), col(0.25, 0.16, 0.08), col(0.9, 0.8, 0.55))
    return nap(img, v, k=0.02, rows=60)


def rug():
    u, v = grid(WIDE_W, WIDE_H)
    aspect = WIDE_W / WIDE_H
    d = frame_d(u, v)
    field = col(0.38, 0.08, 0.14)
    img = nap(mix(field, field * 1.15, 0.5 + 0.5 * np.cos((u - 0.5) * np.pi * 2)), v, rows=60)
    # Бордюры: тёмный край, золотая линия, полоса с ромбами, золотая линия.
    img = mix(img, col(0.12, 0.05, 0.12), (d < 0.05).astype(np.float32))
    img = mix(img, col(0.92, 0.7, 0.3), ((d > 0.05) & (d < 0.062)).astype(np.float32))
    band = (d > 0.062) & (d < 0.16)
    img = mix(img, col(0.15, 0.2, 0.42), band.astype(np.float32))
    along = np.where(np.minimum(u, 1 - u) * aspect < np.minimum(v, 1 - v), v * 1.0, u * aspect)
    across = (d - 0.062) / (0.16 - 0.062)
    diamonds = (np.abs((along * 9) % 1.0 - 0.5) * 1.0 + np.abs(across - 0.5) * 0.9 < 0.32) & band
    img = mix(img, col(0.9, 0.72, 0.35), diamonds.astype(np.float32))
    img = mix(img, col(0.92, 0.7, 0.3), ((d > 0.16) & (d < 0.172)).astype(np.float32))
    # Медальон: ромб с вложенными ромбами, в центре — розетка.
    x = (u - 0.5) * aspect
    y = v - 0.5
    rh = np.abs(x) / 0.62 + np.abs(y) / 0.28
    for r, c in ((1.0, col(0.15, 0.2, 0.42)), (0.86, col(0.92, 0.7, 0.3)), (0.8, col(0.6, 0.14, 0.18)), (0.5, col(0.15, 0.2, 0.42)),
                 (0.42, col(0.92, 0.7, 0.3)), (0.36, col(0.38, 0.08, 0.14))):
        img = mix(img, c, (rh < r).astype(np.float32))
    ang = np.arctan2(y, x)
    rr = np.hypot(x, y)
    petals = rr < 0.07 + 0.025 * np.cos(ang * 8)
    img = mix(img, col(0.92, 0.7, 0.3), petals.astype(np.float32))
    img = mix(img, col(0.6, 0.14, 0.18), (rr < 0.03).astype(np.float32))
    return img


def velvet():
    u, v = grid(WIDE_W, WIDE_H)
    aspect = WIDE_W / WIDE_H
    d = frame_d(u, v)
    x = (u - 0.5) * aspect
    y = v - 0.5
    sheen = np.clip(1.0 - np.hypot(x / 1.4, y / 0.8), 0, 1)
    img = nap(mix(col(0.22, 0.03, 0.08), col(0.55, 0.1, 0.18), sheen ** 1.5), v, k=0.04, rows=80)
    # Кант: витой золотой шнур (полоски по диагонали) и тёмная тень под ним.
    rope = (d > 0.03) & (d < 0.06)
    twist = (np.sin((u * aspect + v) * np.pi * 70) > 0)
    img = mix(img, col(0.12, 0.01, 0.04), ((d > 0.06) & (d < 0.072)).astype(np.float32))
    img = mix(img, np.where(twist[..., None], col(1.0, 0.82, 0.38), col(0.72, 0.5, 0.16)), rope.astype(np.float32))
    img = mix(img, col(0.16, 0.02, 0.05), (d < 0.03).astype(np.float32))
    # Уголки: золотые лилии-ромбы.
    for sx in (0.0, 1.0):
        for sy in (0.0, 1.0):
            cx = np.abs(u - sx) * aspect - 0.13
            cy = np.abs(v - sy) - 0.13
            img = mix(img, col(1.0, 0.82, 0.38), (np.abs(cx) + np.abs(cy) * 1.6 < 0.045).astype(np.float32))
            img = mix(img, col(1.0, 0.82, 0.38), (np.abs(cx) * 1.6 + np.abs(cy) < 0.045).astype(np.float32))
    return img


def board():
    u, v = grid(WIDE_W, WIDE_H)
    aspect = WIDE_W / WIDE_H
    d = frame_d(u, v)
    x = (u - 0.5) * aspect
    y = v - 0.5
    img = nap(mix(col(0.62, 0.45, 0.16), col(0.78, 0.6, 0.24), np.clip(1 - np.hypot(x / 1.6, y), 0, 1)), v, rows=60)
    line = col(0.32, 0.18, 0.05)
    # Рамка-дорожка со скруглёнными углами.
    img = mix(img, line, ((d > 0.07) & (d < 0.082)).astype(np.float32))
    img = mix(img, line, ((d > 0.1) & (d < 0.106)).astype(np.float32))
    # Центр: круг и шесть точек как на грани кости.
    rr = np.hypot(x, y)
    img = mix(img, line, ((rr > 0.2) & (rr < 0.212)).astype(np.float32))
    img = mix(img, col(0.85, 0.66, 0.28), (rr < 0.2).astype(np.float32) * 0.5)
    for px in (-0.06, 0.06):
        for py in (-0.08, 0.0, 0.08):
            img = mix(img, line, (np.hypot(x - px, y - py) < 0.026).astype(np.float32))
    # Дуги у длинных сторон — «места игроков».
    for side in (-1.0, 1.0):
        ry = np.hypot(x, y - side * 0.5)
        img = mix(img, line, ((ry > 0.3) & (ry < 0.312) & (np.abs(y) < 0.5)).astype(np.float32))
    # Лучи из углов к центру (тонкие).
    for sx in (-1, 1):
        for sy in (-1, 1):
            k = (0.5 * sy) / (0.5 * aspect * sx)
            dist = np.abs(y - k * x) / math.sqrt(1 + k * k)
            ray = (dist < 0.004) & (rr > 0.212) & (np.sign(x) == sx) & (np.sign(y) == sy) & (d > 0.106)
            img = mix(img, line, ray.astype(np.float32) * 0.8)
    return img


TILES = {"Felt_Leather": leather, "Felt_Club": club, "Felt_Suits": suits}
WIDE = {"Felt_Rug": rug, "Felt_Velvet": velvet, "Felt_Board": board, "Felt_Constellations": sky_wide, "Felt_TreasureMap": map_wide}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    os.makedirs(OUT_DIR, exist_ok=True)
    made = {}
    for name, make in list(TILES.items()) + list(WIDE.items()):
        img = np.clip(make(), 0, 1).astype(np.float32)
        zm.save_png(name, img, os.path.join(OUT_DIR, name + ".png"))
        made[name] = img
        print("  %-20s %dx%d" % (name, img.shape[1], img.shape[0]))
    if render_dir:
        # Превью: плитки 2×2 (видно шов, если он есть) в ряд, полотна — уменьшенные.
        os.makedirs(render_dir, exist_ok=True)
        row = [np.tile(made[n], (2, 2, 1)) for n in TILES]
        zm.save_png("tiles_preview", np.concatenate(row, axis=1), os.path.join(render_dir, "felt_tiles.png"))
        wide = [made[n][::2, ::2] for n in WIDE]
        zm.save_png("wide_preview", np.concatenate(wide, axis=1), os.path.join(render_dir, "felt_wide.png"))


if __name__ == "__main__":
    main()
