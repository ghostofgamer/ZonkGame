# -*- coding: utf-8 -*-
"""
Мультяшная вода: текстура ячеек Art/Textures/Water_Cells.png и проверочные кадры той же формулой, что в шейдере Zonk/Water.
Кадр считается без Unity: можно смотреть вид воды с камер партии и меню и подбирать числа. Меняешь формулу — меняй и шейдер.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_water.py -- --texture
    blender --background --factory-startup --python Tools/Blender/zonk_water.py -- --preview <папка> [--time 0 3]
"""

import math
import os
import sys

import bpy
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TEXTURE = os.path.join(ROOT, "Assets", "ZonkContent", "Art", "Textures", "Water_Cells.png")

# Те же значения, что в материале M_Water.
SHALLOW = np.array([0.46, 0.93, 0.86])
MIDDLE = np.array([0.13, 0.72, 0.83])
DEEP = np.array([0.05, 0.42, 0.74])
FAR = np.array([0.42, 0.74, 0.90])
LINES = np.array([0.80, 0.98, 0.97])
FOAM = np.array([1.0, 1.0, 0.98])
SHORE_Z = 5.6
TIDE_SIZE = 0.45
TIDE_SPEED = 0.5
CELL_SCALE_A = 0.3
CELL_SCALE_B = 0.19
HEIGHT = -1.59


def srgb_save(path, pixels):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new("out", width, height, alpha=False)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(np.flipud(np.dstack([pixels, np.ones((height, width))])).astype(np.float32).ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def make_cells(size=256, points=26, seed=5):
    """Бесшовные ячейки: яркие тонкие линии на стыках клеток (разность двух ближайших расстояний)."""
    rng = np.random.RandomState(seed)
    centres = rng.rand(points, 2)
    y, x = np.mgrid[0:size, 0:size] / size
    first = np.full((size, size), 9.0)
    second = np.full((size, size), 9.0)
    for cx, cy in centres:
        dx = np.abs(x - cx)
        dy = np.abs(y - cy)
        dx = np.minimum(dx, 1 - dx)
        dy = np.minimum(dy, 1 - dy)
        d = np.hypot(dx, dy)
        mask = d < first
        second = np.where(mask, first, np.minimum(second, d))
        first = np.where(mask, d, first)
    edge = (second - first) * math.sqrt(points)
    return np.clip(1.0 - edge / 0.55, 0.0, 1.0) ** 2.2


def bilinear(texture, u, v):
    size = texture.shape[0]
    fx = (u % 1.0) * size
    fy = (v % 1.0) * size
    x0 = np.floor(fx).astype(int) % size
    y0 = np.floor(fy).astype(int) % size
    x1 = (x0 + 1) % size
    y1 = (y0 + 1) % size
    ax = fx - np.floor(fx)
    ay = fy - np.floor(fy)
    return (texture[y0, x0] * (1 - ax) + texture[y0, x1] * ax) * (1 - ay) + (texture[y1, x0] * (1 - ax) + texture[y1, x1] * ax) * ay


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def water(x, z, t, cells):
    """Цвет и прозрачность воды в точке. Формула один в один с frag шейдера."""
    swing = np.sin(x * 0.55 + t * 0.25) * 0.28 + np.sin(x * 1.35 - t * 0.2) * 0.13
    tide = math.sin(t * TIDE_SPEED) * TIDE_SIZE
    depth = z - SHORE_Z + swing * 0.6
    edge = z - SHORE_Z - tide + swing

    a = bilinear(cells, x * CELL_SCALE_A + t * 0.018, z * CELL_SCALE_A + t * 0.03)
    b = bilinear(cells, x * CELL_SCALE_B - t * 0.024 + 0.37, z * CELL_SCALE_B + t * 0.014 + 0.61)

    color = SHALLOW + (MIDDLE - SHALLOW) * smooth(0.0, 2.6, depth)[..., None]
    color = color + (DEEP - color) * smooth(2.0, 9.0, depth)[..., None]

    # Светлые линии: где обе сетки близко к стыку, линия ярче. Вдали слабеют, у берега сильнее.
    line = smooth(0.34, 0.52, a) * 0.65 + smooth(0.40, 0.62, b) * 0.4
    line = np.clip(line, 0, 1) * (0.85 - 0.75 * smooth(2.0, 14.0, depth))
    color = color + (LINES - color) * line[..., None]

    # Вторая волна: тонкая линия пены ходит параллельно берегу и гаснет, когда уходит далеко.
    wave_phase = (t * 0.17) % 1.0
    wave_at = 0.5 + wave_phase * 3.0
    wave_fade = math.sin(wave_phase * math.pi)
    ragged = (a + b) * 0.25
    wave = (1 - smooth(0.04, 0.17, np.abs(depth - wave_at + ragged))) * wave_fade
    wave_cut = smooth(0.12, 0.28, b)
    color = color + (FOAM - color) * (wave * (1 - wave_cut * 0.6))[..., None]

    # Прибой у самой кромки: толстая рваная пена.
    surf_width = 0.55 + 0.22 * np.sin(x * 2.3 + t * 0.6) * np.sin(x * 0.9 - t * 0.3)
    surf = 1 - smooth(surf_width - 0.05, surf_width + 0.04, edge + a * 0.42)
    color = color + (FOAM - color) * surf[..., None]

    color = color + (FAR - color) * smooth(24.0, 60.0, depth)[..., None]
    alpha = smooth(-0.05, 0.05, edge)
    return np.clip(color, 0, 1), alpha


def sand_at(x, z, t):
    """Песок на кадре проверки и мокрая полоса: как в сцене."""
    sand = np.zeros(x.shape + (3,)) + np.array([0.90, 0.80, 0.60])
    wet_edge = z - SHORE_Z + TIDE_SIZE + np.sin(x * 0.55 + t * 0.1) * 0.2
    wet = smooth(-1.3, -0.1, wet_edge)
    wet_color = np.array([0.62, 0.50, 0.36])
    return sand + (wet_color - sand) * (wet * 0.85)[..., None]


def view(camera, target, fov, t, cells, width=1280, height=720):
    camera = np.array(camera, dtype=float)
    target = np.array(target, dtype=float)
    forward = target - camera
    forward /= np.linalg.norm(forward)
    right = np.cross(forward, (0, 1, 0))
    right /= np.linalg.norm(right)
    up = np.cross(right, forward)
    half = math.tan(math.radians(fov) / 2)
    py, px = np.mgrid[0:height, 0:width]
    sx = (px + 0.5) / width * 2 - 1
    sy = 1 - (py + 0.5) / height * 2
    rays = forward + right * (sx * half * width / height)[..., None] + up * (sy * half)[..., None]
    distance = (HEIGHT - camera[1]) / np.minimum(rays[..., 1], -1e-4)
    x = camera[0] + rays[..., 0] * distance
    z = camera[2] + rays[..., 2] * distance
    above = rays[..., 1] > -0.004
    color, alpha = water(x, z, t, cells)
    sand = sand_at(x, z, t)
    picture = sand + (color - sand) * alpha[..., None]
    picture[above] = (0.72, 0.88, 0.96)
    return picture


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cells = make_cells()
    if "--texture" in argv:
        os.makedirs(os.path.dirname(TEXTURE), exist_ok=True)
        srgb_save(TEXTURE, np.dstack([cells, cells, cells]))
        print("[zonk_water] wrote", TEXTURE)

    if "--preview" in argv:
        folder = argv[argv.index("--preview") + 1]
        times = [0.0, 3.0]
        for i, t in enumerate(times):
            frame = view((0, 4.1, -4.9), (0, -0.2, 0.1), 48, t, cells)
            srgb_save(os.path.join(folder, "voda_partiya_%d.png" % i), frame)
        srgb_save(os.path.join(folder, "voda_menu.png"), view((3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, 1.5, cells))
        print("[zonk_water] previews in", folder)


if __name__ == "__main__":
    main()
