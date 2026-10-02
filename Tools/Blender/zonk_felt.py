# -*- coding: utf-8 -*-
"""
Сукно стола для игры «Зонк»: узоры в комиксовом стиле (ровные пятна, тёмные линии, без мелкого шума).

Плитки 256×256 — бесшовные, повторяются по сукну (материал с тайлингом, как сукна ZonkSetup.ShopV5):
    Felt_Leather         кожа с тиснением ромбами и пуговками (стёжка «честерфилд»)
    Felt_Constellations  ночное небо: звёзды и созвездия
    Felt_TreasureMap     море с островами, пунктир маршрута и крестики
    Felt_Club            клубное зелёное сукно с тонкой сеткой ромбов
    Felt_Suits           тёмное сукно с мастями карт
Целые полотна 1024×640 (пропорции сукна 3.2 × 2), без повтора, симметричные — ориентация UV верха куба не важна:
    Felt_Rug             восточный ковёр: бордюры, медальон в центре
    Felt_Velvet          бархат с отливом и золотым кантом по краю
    Felt_Board           игровое поле: золотистое сукно с разметкой и шестью точками в центре

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


def constellations():
    u, v = grid(TILE, TILE)
    rng = np.random.default_rng(5)
    img = mix(col(0.04, 0.06, 0.18), col(0.08, 0.1, 0.28), np.clip(0.5 + 0.25 * zm.periodic_noise(rng, TILE, TILE, 60.0), 0, 1))
    gold = col(1.0, 0.85, 0.45)
    # Созвездия: ломаные линии между звёздами (внутри плитки, без пересечения края).
    figures = [
        [(0.12, 0.2), (0.22, 0.34), (0.33, 0.3), (0.42, 0.42), (0.38, 0.58)],
        [(0.6, 0.12), (0.7, 0.22), (0.82, 0.18), (0.88, 0.3)],
        [(0.58, 0.62), (0.66, 0.74), (0.78, 0.7), (0.74, 0.86), (0.6, 0.84)],
        [(0.14, 0.72), (0.24, 0.8), (0.3, 0.92)],
    ]
    for pts in figures:
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            t = np.clip(((u - x0) * dx + (v - y0) * dy) / (dx * dx + dy * dy), 0, 1)
            d = np.hypot(u - (x0 + t * dx), v - (y0 + t * dy))
            img = mix(img, col(0.55, 0.6, 0.85), soft(d, 0.0035, TILE) * 0.7)
        for (x, y) in pts:
            d = np.hypot(wrap_d(u, x), wrap_d(v, y))
            img = mix(img, gold, soft(d, 0.012, TILE))
    # Россыпь мелких звёзд.
    for _ in range(26):
        x, y, r = rng.uniform(0, 1), rng.uniform(0, 1), rng.uniform(0.003, 0.006)
        d = np.hypot(wrap_d(u, x), wrap_d(v, y))
        img = mix(img, col(0.85, 0.88, 1.0), soft(d, r, TILE))
    return img


def treasure_map():
    u, v = grid(TILE, TILE)
    rng = np.random.default_rng(11)
    n = zm.periodic_noise(rng, TILE, TILE, 34.0, power=2.6) + 0.15 * zm.periodic_noise(rng, TILE, TILE, 16.0)
    sea = mix(col(0.07, 0.36, 0.42), col(0.1, 0.46, 0.5), np.clip(0.5 + 0.2 * n, 0, 1))
    land = (n > 1.0).astype(np.float32)
    shore = ((n > 0.82) & (n <= 1.0)).astype(np.float32)
    img = mix(sea, col(0.55, 0.78, 0.74), shore)
    img = mix(img, col(0.88, 0.76, 0.5), land)
    img = mix(img, col(0.48, 0.62, 0.3), (n > 1.6).astype(np.float32))
    edge = (np.abs(n - 1.0) < 0.05).astype(np.float32)
    img = mix(img, col(0.25, 0.18, 0.1), edge)
    # Пунктир маршрута: синусоида, бесшовная по u.
    path = 0.5 + 0.18 * np.sin(u * np.pi * 2) + 0.06 * np.sin(u * np.pi * 6)
    dash = (np.sin(u * np.pi * 36) > 0).astype(np.float32)
    img = mix(img, col(0.7, 0.12, 0.08), (np.abs(v - path) < 0.008).astype(np.float32) * dash)
    # Крестики «здесь клад».
    for (x, y) in ((0.3, 0.2), (0.78, 0.82)):
        du, dv = u - x, v - y
        cross = ((np.abs(du - dv) < 0.007) | (np.abs(du + dv) < 0.007)) & (np.abs(du) < 0.03) & (np.abs(dv) < 0.03)
        img = mix(img, col(0.75, 0.08, 0.05), cross.astype(np.float32))
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


TILES = {"Felt_Leather": leather, "Felt_Constellations": constellations, "Felt_TreasureMap": treasure_map,
         "Felt_Club": club, "Felt_Suits": suits}
WIDE = {"Felt_Rug": rug, "Felt_Velvet": velvet, "Felt_Board": board}


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
