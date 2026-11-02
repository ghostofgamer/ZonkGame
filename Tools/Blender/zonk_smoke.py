# -*- coding: utf-8 -*-
"""
Текстуры дымной тени соперника (шейдер Zonk/ShadowSmoke).

Shadow_Noise: бесшовный шум 256×256, RG — изгиб дыма, B — мелкие клубы, A — хребты для струек.
Shadow_Body:  форма фигуры 128×128 как поле расстояний (R), считается один раз: в игре это одно чтение текстуры.
Новая фигура (крупный, худой, кот) — ещё одна текстура формы, шейдер тот же. Руки шейдер считает сам: они тянутся к рукавам.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_smoke.py
Результат: Art/Textures/Smoke/*.png. Подключает генератор (ZonkSetup.ShadowSmoke): линейный цвет, повтор у шума.
Границы формы и кодировка расстояния совпадают с шейдером: менять вместе.
"""

import os
import sys

import bpy
import numpy as np

sys.dont_write_bytecode = True

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ZonkContent", "Art", "Textures", "Smoke")

# Форма в единицах шейдера: голова в нуле, радиус около 0.5.
MASK_X = 3.0
MASK_Y_MIN = -3.5
MASK_Y_MAX = 1.5
DIST_MIN = -1.0
DIST_MAX = 2.0


def save(name, image):
    height, width = image.shape[:2]
    bitmap = bpy.data.images.new(name, width, height, alpha=True)
    bitmap.colorspace_settings.name = "Non-Color"
    bitmap.pixels.foreach_set(np.flipud(image).astype(np.float32).ravel())
    bitmap.filepath_raw = os.path.join(OUT, name + ".png")
    bitmap.file_format = "PNG"
    bitmap.save()
    bpy.data.images.remove(bitmap)
    print("[zonk_smoke]", name, width, "x", height)


def tile_fbm(size, cells, octaves, seed):
    """Бесшовный шум: случайное поле, отфильтрованное в частотах (FFT повторяется по краям сам), 0..1."""
    rng = np.random.RandomState(seed)
    result = np.zeros((size, size))
    amplitude = 0.5
    freq_y, freq_x = np.meshgrid(np.fft.fftfreq(size) * size, np.fft.fftfreq(size) * size, indexing="ij")
    radius = np.hypot(freq_x, freq_y)
    for octave in range(octaves):
        target = cells * 2 ** octave
        band = np.exp(-((radius - target) / (target * 0.55)) ** 2)
        field = np.fft.ifft2(np.fft.fft2(rng.normal(size=(size, size))) * band).real
        result += field / field.std() * amplitude
        amplitude *= 0.5
    low, high = np.percentile(result, 1), np.percentile(result, 99)
    return np.clip((result - low) / (high - low), 0, 1)


def noise():
    size = 256
    warp_x = tile_fbm(size, 4, 3, 11)
    warp_y = tile_fbm(size, 4, 3, 23)
    puffs = tile_fbm(size, 6, 4, 37)
    ridges = 1 - np.abs(tile_fbm(size, 5, 3, 53) * 2 - 1)
    return np.dstack([warp_x, warp_y, puffs, ridges])


def seg(px, py, ax, ay, bx, by):
    pax, pay = px - ax, py - ay
    bax, bay = bx - ax, by - ay
    h = np.clip((pax * bax + pay * bay) / (bax * bax + bay * bay), 0, 1)
    return np.hypot(pax - bax * h, pay - bay * h)


def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def body():
    """Капюшон-голова, сутулые плечи, широкий торс, уходящий за стол."""
    size = 128
    y, x = np.mgrid[0:size, 0:size]
    px = (x + 0.5) / size * MASK_X * 2 - MASK_X
    py = MASK_Y_MAX - (y + 0.5) / size * (MASK_Y_MAX - MASK_Y_MIN)
    d = np.hypot(px, (py - 0.02) * 0.92) - 0.52
    d = smin(d, seg(px, py, 0, -0.3, 0, -0.9) - 0.26, 0.28)
    d = smin(d, seg(px, py, -1.02, -1.02, 1.02, -1.02) - 0.42, 0.5)
    d = smin(d, seg(px, py, 0, -1.1, 0, -3.0) - 1.05, 0.5)
    value = np.clip((d - DIST_MIN) / (DIST_MAX - DIST_MIN), 0, 1)
    return np.dstack([value, value, value, np.ones_like(value)])


def main():
    os.makedirs(OUT, exist_ok=True)
    save("Shadow_Noise", noise())
    save("Shadow_Body", body())


main()
