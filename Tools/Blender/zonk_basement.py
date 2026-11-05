# -*- coding: utf-8 -*-
"""
Локация «Подвал», глава 7: подпольный клуб Крота. Кирпичные своды, трубы, стальная дверь с глазком, неон туза и костей на дальней стене,
бар, музыкальный автомат, игровой автомат, столик с фишками, бархатные канаты. Живое: крот в норе (zonk_basement_life.py), мерцание неона и пар — генератор.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_basement.py -- --render <папка> [--samples 48]
    blender --background --factory-startup --python Tools/Blender/zonk_basement.py -- --bake <папка превью> [--samples 64]
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, пол на -1.6.
Камеры меню и магазина внутри свода (самая дальняя от центра: x 5.2, высота 3.3). Рука соперника на x −2.2, z 2.3 (рукав до z 3.5): выше стола там ничего нет.
Кадр партии: на z 4 видно до высоты 1.4, на z 5.5 до 1.0.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_basement_life as life  # noqa: E402
import zonk_beach as bc  # noqa: E402
import zonk_garden as zg  # noqa: E402
import zonk_room as kit  # noqa: E402
import zonk_ship as zs  # noqa: E402
import zonk_tavern as tv  # noqa: E402

from zonk_beach import box, ground, pole  # noqa: E402
from zonk_room import FLOOR, blob, cyl, image_mat, mat  # noqa: E402
from zonk_ship import set_wind, smooth_path, strip, tube  # noqa: E402
from zonk_tavern import lathe, torus  # noqa: E402

BACK = 5.5
FRONT = -9.5
SIDE = 8.0
WALL_TOP = 1.8
VAULT = 3.5
MOLE = (1.7, 4.35)
rng = np.random.RandomState(7)


# ---------- Картинки ----------

def brick_image():
    """Кирпич плиткой: ровные тона, тёмные швы, редкие светлые и тёмные кирпичи. Без шума: стиль комикса."""
    size = 512
    local = np.random.RandomState(12)
    image = np.zeros((size, size, 3)) + np.array([0.15, 0.12, 0.11])
    rows, columns = 8, 4
    h, w = size // rows, size // columns
    for r in range(rows):
        shift = w // 2 if r % 2 else 0
        for c in range(columns + 1):
            tone = 0.46 + local.rand() * 0.14
            color = np.array([tone, tone * 0.6, tone * 0.47])
            if local.rand() < 0.1:
                color = color * 0.72
            elif local.rand() < 0.1:
                color = color * 1.2
            x0, x1 = c * w - shift + 4, (c + 1) * w - shift - 4
            xs = np.arange(max(x0, 0), min(x1, size))
            if xs.size:
                image[r * h + 4:(r + 1) * h - 4, xs[0]:xs[-1] + 1] = color
    return np.clip(image, 0, 1)


def floor_image():
    """Шахматная плитка: чёрная и бордовая, потёрта у стола, тёмные швы, лужи с отблеском неона."""
    size = 2048
    x0, x1, y0, y1 = -SIDE - 0.1, SIDE + 0.1, FRONT - 3, BACK + 0.1
    py, px = np.mgrid[0:size, 0:size]
    wx = x0 + (x1 - x0) * px / size
    wy = y1 + (y0 - y1) * py / size
    cell = 0.95
    check = (np.floor(wx / cell).astype(int) + np.floor(wy / cell).astype(int)) % 2 == 0
    image = np.zeros((size, size, 3))
    image[check] = (0.34, 0.31, 0.33)
    image[~check] = (0.54, 0.2, 0.22)
    grout = (np.abs(((wx / cell) % 1) - 0.5) > 0.485) | (np.abs(((wy / cell) % 1) - 0.5) > 0.485)
    image[grout] = (0.08, 0.07, 0.07)
    worn = zs.cloud_noise(size, size, 8, 1.0, 1.9)
    faded = worn > 0.6
    image[faded] = image[faded] * 0.8 + np.array([0.08, 0.075, 0.075])
    # Лужи: под протекающей трубой и у двери. Ровные пятна с отблеском неона.
    for cx, cy, rx, ry in ((4.5, 3.25, 0.95, 0.6), (-4.6, 0.8, 1.1, 0.65), (4.4, -3.6, 0.75, 0.55)):
        d = ((wx - cx) / rx) ** 2 + ((wy - cy) / ry) ** 2 + 0.25 * np.sin(np.arctan2(wy - cy, wx - cx) * 5) * ((wx - cx) ** 2 + (wy - cy) ** 2 > 0.1)
        image[d < 1] = image[d < 1] * 0.42 + np.array([0.13, 0.11, 0.19])
        image[d < 0.3] = image[d < 0.3] * 0.75 + np.array([0.1, 0.05, 0.14])
    return np.clip(image, 0, 1)


# ---------- Комната ----------

def room(bricks, concrete, dark):
    samples = [FRONT + (BACK - FRONT) * k / 2 for k in range(3)]
    for name, sign in (("Left", -1), ("Right", 1)):
        strip(name, [[(sign * SIDE, y, FLOOR), (sign * SIDE, y, WALL_TOP)] for y in samples], bricks, 1.0, facing=(-sign, 0, 0))
        steps = 8
        arc = [(sign * SIDE * math.cos(k * math.pi / 2 / steps), WALL_TOP + VAULT * math.sin(k * math.pi / 2 / steps)) for k in range(steps + 1)]
        strip("Ceiling", [[(x, y, z) for x, z in arc] for y in samples], bricks, 1.0, facing=(-sign, 0, -1))
    columns = [-SIDE + 2 * SIDE * k / 16 for k in range(17)]

    def vault(x):
        return WALL_TOP + VAULT * math.sqrt(max(1 - (x / SIDE) ** 2, 0))

    strip("Back", [[(x, BACK, FLOOR), (x, BACK, vault(x))] for x in columns], bricks, 1.0, facing=(0, -1, 0))

    # Подпружные арки и пилястры: своды держатся на них, ритм вместо плоской стены.
    y = FRONT + 1.0
    while y < BACK + 0.1:
        for sign in (-1, 1):
            box("WallRib", (sign * (SIDE - 0.14), y, FLOOR), (0.3, 0.42, WALL_TOP - FLOOR), concrete, bevel=0.025)
            box("WallRib", (sign * (SIDE - 0.14), y, FLOOR), (0.42, 0.54, 0.34), dark, bevel=0.02)
        path = [(SIDE * 0.985 * math.cos(k * math.pi / 14), y, WALL_TOP + VAULT * 0.985 * math.sin(k * math.pi / 14)) for k in range(15)]
        tube("WallRib", path, 0.17, concrete, sides=6)
        y += 3.35


def steel_door(x, steel, dark, brass, red):
    """Стальная дверь клуба: заклёпки, усиленные полосы, смотровая щель, запорное колесо, лампа над дверью."""
    y = BACK - 0.08
    box("WallDoor", (x, y, FLOOR), (1.72, 0.14, 2.62), dark, bevel=0.02)
    box("WallDoor", (x, y - 0.08, FLOOR + 0.04), (1.46, 0.06, 2.44), steel, bevel=0.015)
    for z in (0.3, 1.2, 2.15):
        box("WallDoor", (x, y - 0.12, FLOOR + z), (1.46, 0.024, 0.14), dark, bevel=0)
        for k in range(7):
            blob("WallDoor", (x - 0.66 + k * 0.22, y - 0.14, FLOOR + z + 0.07), (0.026, 0.016, 0.026), brass, 1)
    box("WallDoor", (x, y - 0.12, FLOOR + 1.62), (0.62, 0.026, 0.22), dark, bevel=0)
    box("WallDoor", (x, y - 0.135, FLOOR + 1.67), (0.46, 0.012, 0.09), mat("BasementSlit", "#f0b440", 1, emit=1.3), bevel=0)
    wheel_at = (x + 0.46, y - 0.17, FLOOR + 1.0)
    torus("WallDoor", wheel_at, 0.17, 0.024, red, 14, 5, axis="Y")
    for k in range(3):
        a = k * math.pi / 3
        pole("WallDoor", (wheel_at[0] + math.cos(a) * 0.16, wheel_at[1], wheel_at[2] + math.sin(a) * 0.16), (wheel_at[0] - math.cos(a) * 0.16, wheel_at[1], wheel_at[2] - math.sin(a) * 0.16), 0.014, red, verts=4)
    lathe("WallDoor", (x, y - 0.1, FLOOR + 2.72), [(0.17, 0), (0.17, 0.05), (0.12, 0.14), (0.0, 0.17)], dark, segments=8)
    blob("WallDoor", (x, y - 0.1, FLOOR + 2.7), (0.08, 0.08, 0.06), mat("BasementBulbWarm", "#ffcf7a", 1, emit=2.2), 1)


def neon_sign(x, z, board, pink, cyan, yellow):
    """Неон на стене: пиковый туз и две кости. Без слов: игра на восьми языках. Главная цветная точка сцены."""
    y = BACK - 0.1
    box("WallSign", (x, y, z - 1.0), (3.35, 0.08, 2.0), board, bevel=0.03)

    def glow(points, material, radius=0.033):
        tube("WallNeon", smooth_path([(x + px, y - 0.09, z + pz) for px, pz in points], 3), radius, material, sides=5)

    # Пика.
    spade_l = [(0, 0.62), (-0.2, 0.34), (-0.45, 0.08), (-0.42, -0.18), (-0.22, -0.26), (-0.06, -0.16)]
    spade_r = [(-px, pz) for px, pz in spade_l]
    glow(spade_l, pink)
    glow(spade_r, pink)
    glow([(-0.06, -0.16), (-0.08, -0.42), (-0.2, -0.58)], pink)
    glow([(0.06, -0.16), (0.08, -0.42), (0.2, -0.58)], pink)
    glow([(-0.2, -0.58), (0.2, -0.58)], pink)

    # Две кости по бокам: квадраты и точки.
    for cx, turn, dots in ((-1.12, 0.14, ((0, 0), (-0.14, 0.14), (0.14, -0.14))), (1.12, -0.14, ((-0.14, 0.14), (0.14, 0.14), (-0.14, -0.14), (0.14, -0.14)))):
        corners = []
        for px, pz in ((-0.29, -0.29), (0.29, -0.29), (0.29, 0.29), (-0.29, 0.29), (-0.29, -0.29)):
            corners.append((x + cx + px * math.cos(turn) - pz * math.sin(turn), y - 0.09, z + px * math.sin(turn) + pz * math.cos(turn)))
        tube("WallNeon", corners, 0.03, cyan, sides=5)
        for dx, dz in dots:
            blob("WallNeon", (x + cx + dx * math.cos(turn) - dz * math.sin(turn), y - 0.09, z + dx * math.sin(turn) + dz * math.cos(turn)), (0.05, 0.03, 0.05), yellow, 1)


def pipes(iron, rust, red, brass):
    """Трубы под сводом и по задней стене: сырость и подполье. Вентили, фланцы, манометр. С колена капает и идёт пар."""
    z = WALL_TOP + 0.5
    run = [(SIDE - 0.3, BACK - 0.35, z), (5.2, BACK - 0.35, z), (5.05, BACK - 0.35, z - 0.12), (5.05, BACK - 0.35, -0.3), (5.05, 3.45, -0.3), (4.6, 3.45, -0.3), (4.5, 3.45, -0.42)]
    tube("WallPipe", run, 0.1, iron, sides=7)
    for point in ((6.6, BACK - 0.35, z), (5.05, BACK - 0.35, 0.8), (5.05, 4.5, -0.3)):
        horizontal = abs(point[2] - z) < 0.01
        ring = torus("WallPipe", point, 0.115, 0.032, rust, 10, 4, axis="X" if horizontal or point[1] == 3.45 else "Z")
        if point[1] == 4.5:
            ring.rotation_euler = (math.pi / 2, 0, 0)
    tube("WallPipe", [(-SIDE + 0.3, BACK - 0.3, 2.65), (-2.7, BACK - 0.3, 2.65)], 0.075, rust, sides=6)
    tube("WallPipe", [(-SIDE + 0.3, BACK - 0.5, 2.3), (-2.9, BACK - 0.5, 2.3)], 0.055, iron, sides=6)
    for x in (-6.8, -4.9, -3.1):
        box("WallPipe", (x, BACK - 0.1, 2.15), (0.08, 0.5, 0.6), iron, bevel=0)
    # Вентиль на трубе и манометр.
    valve = (5.05, BACK - 0.5, 0.25)
    pole("WallPipe", (5.05, BACK - 0.35, 0.25), valve, 0.03, brass, verts=5)
    torus("WallPipe", valve, 0.17, 0.025, red, 14, 5, axis="Y")
    pole("WallPipe", (valve[0] - 0.16, valve[1], valve[2]), (valve[0] + 0.16, valve[1], valve[2]), 0.016, red, verts=4)
    pole("WallPipe", (valve[0], valve[1], valve[2] - 0.16), (valve[0], valve[1], valve[2] + 0.16), 0.016, red, verts=4)
    gauge = cyl("WallPipe", (5.3, BACK - 0.45, 0.7), 0.13, 0.05, brass, axis="Y", verts=14)
    gauge.name = "WallPipe"
    cyl("WallPipe", (5.3, BACK - 0.48, 0.7), 0.105, 0.012, mat("BasementDial", "#efe6cc", 0.7), axis="Y", verts=12)
    pole("WallPipe", (5.3, BACK - 0.495, 0.7), (5.36, BACK - 0.495, 0.77), 0.008, red, verts=3)


def bar(wood, dark, brass, red, glass):
    """Бар в левой нише: стойка с медной трубкой, полки с бутылками и подсветкой, высокие табуреты."""
    x0, x1 = -7.6, -3.5
    y = 3.75
    tv.world_uv(box("ClubBar", ((x0 + x1) / 2, y, FLOOR), (x1 - x0, 0.72, 1.1), wood, bevel=0.012), 0.9)
    box("ClubBar", ((x0 + x1) / 2, y - 0.06, FLOOR + 1.1), (x1 - x0 + 0.24, 0.92, 0.09), dark, bevel=0.02)
    tube("ClubBar", [(x0, y - 0.46, FLOOR + 0.25), (x1, y - 0.46, FLOOR + 0.25)], 0.024, brass, sides=6)
    x = x0 + 0.55
    while x < x1 - 0.3:
        tube("ClubBar", [(x, y - 0.46, FLOOR + 0.25), (x, y - 0.37, FLOOR + 0.25)], 0.014, brass, sides=4)
        x += 1.0
    # Панель-филёнки на стойке.
    x = x0 + 0.45
    while x < x1 - 0.4:
        box("ClubBar", (x, y - 0.37, FLOOR + 0.42), (0.68, 0.02, 0.5), dark, bevel=0)
        x += 0.92
    for k, x in enumerate((-6.7, -5.5, -4.3)):
        lathe("ClubStool", (x, y - 0.92, FLOOR), [(0.2, 0), (0.17, 0.03), (0.035, 0.08), (0.035, 0.62), (0.18, 0.65), (0.2, 0.72), (0.17, 0.77), (0, 0.79)], brass if k % 2 else mat("BasementStool", "#7a1f2c", 0.5), segments=10)
    # Полки на задней стене с подсветкой снизу.
    bx0, bx1 = -7.5, -3.6
    for z in (0.05, 0.75):
        box("ClubBar", ((bx0 + bx1) / 2, BACK - 0.24, z), (bx1 - bx0, 0.34, 0.06), dark, bevel=0.01)
        box("ClubBar", ((bx0 + bx1) / 2, BACK - 0.08, z + 0.06), (bx1 - bx0, 0.03, 0.03), mat("BasementShelfLight", "#ffa94d", 1, emit=2.4), bevel=0)
    for z in (0.11, 0.81):
        x = bx0 + 0.2
        while x < bx1 - 0.15:
            tv.bottle(x, BACK - 0.24, z, glass[rng.randint(0, len(glass))])
            x += 0.28 + rng.rand() * 0.18
    for x, z in ((-5.9, FLOOR + 1.19), (-4.9, FLOOR + 1.19)):
        lathe("ClubBar", (x, y - 0.12, z), [(0.05, 0), (0.06, 0.02), (0.01, 0.05), (0.01, 0.13), (0.09, 0.26), (0.09, 0.27)], mat("BasementCocktail", "#d67238", 0.2), segments=8)


def jukebox(x, y, turn, body, trim, brass, glow_a, glow_b, glow_c, dark):
    """Музыкальный автомат: арка из цветных светящихся трубок, решётка, окно с пластинками, кнопки. Яркое пятно у правого края партии."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    box("ClubJuke", at(0, 0, 0), (1.2, 0.72, 1.28), body, bevel=0.03).rotation_euler = (0, 0, turn)
    # Верх аркой: боковые дуги цветные.
    for scale, color, depth, thick in ((0.6, glow_a, -0.38, 0.045), (0.5, glow_b, -0.4, 0.04), (0.4, glow_c, -0.42, 0.036)):
        arc = [at(-scale, depth, 0.1)] + [at(math.cos(a) * scale, depth, 1.28 + math.sin(a) * scale * 0.9) for a in (math.pi - k * math.pi / 12 for k in range(13))] + [at(scale, depth, 0.1)]
        tube("ClubJuke", arc, thick, color, sides=5)
    from_arc = [(math.cos(math.pi * k / 12) * 0.62, math.sin(math.pi * k / 12) * 0.56) for k in range(13)]
    strip("ClubJuke", [[at(px, dy, 1.28 + pz) for px, pz in from_arc] for dy in (-0.36, 0.36)], trim, facing=(0, 0, 1))
    # Передняя панель арки: тёмная с пластинками.
    box("ClubJuke", at(0, -0.37, 0.92), (0.68, 0.02, 0.42), dark, bevel=0)
    for dx in (-0.2, 0.0, 0.2):
        record = cyl("ClubJuke", at(dx, -0.385, 1.13), 0.09, 0.012, mat("BasementRecord", "#151218", 0.3), axis="Y", verts=12)
        record.rotation_euler = (math.pi / 2, 0, turn)
        label = cyl("ClubJuke", at(dx, -0.394, 1.13), 0.032, 0.006, glow_b, axis="Y", verts=8)
        label.rotation_euler = (math.pi / 2, 0, turn)
    # Решётка динамика и ряд кнопок.
    for k in range(7):
        box("ClubJuke", at(0, -0.375, 0.16 + k * 0.075), (0.84, 0.018, 0.028), brass, bevel=0)
    for k in range(6):
        blob("ClubJuke", at(-0.27 + k * 0.108, -0.39, 0.77), (0.03, 0.016, 0.03), glow_a if k % 2 else glow_c, 1)


def slot_machine(x, y, turn, body, chrome, cream, red, glow, ink):
    """Игровой автомат: корпус, три барабана с символами, рычаг с шаром, лампа наверху, лоток с монетами."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    def piece(center, size, material, bevel=0.015):
        box("ClubSlot", at(*center), size, material, bevel=bevel).rotation_euler = (0, 0, turn)

    piece((0, 0, 0), (0.92, 0.72, 0.9), chrome)
    piece((0, 0, 0.9), (0.84, 0.62, 0.72), body)
    piece((0, -0.02, 1.62), (0.9, 0.68, 0.14), chrome)
    piece((0, -0.33, 1.02), (0.66, 0.02, 0.44), ink, bevel=0)
    for k, dx in enumerate((-0.21, 0, 0.21)):
        piece((dx, -0.345, 1.05), (0.18, 0.015, 0.38), cream, bevel=0)
        color = (red, glow, red)[k]
        blob("ClubSlot", at(dx, -0.36, 1.24), (0.065, 0.012, 0.065), color, 1)
    piece((0, -0.34, 0.84), (0.66, 0.02, 0.08), red, bevel=0)
    piece((0, -0.38, 0.14), (0.64, 0.1, 0.17), mat("BasementTray", "#3a3438", 0.4))
    for k in range(5):
        coin = cyl("ClubSlot", at(-0.2 + k * 0.1, -0.4, 0.3), 0.045, 0.014, glow, verts=8)
        coin.rotation_euler = (rng.rand() * 0.4, rng.rand() * 0.4, 0)
    lathe("ClubSlot", at(0, -0.02, 1.76), [(0.14, 0), (0.17, 0.08), (0.13, 0.22), (0.0, 0.29)], glow, segments=10)
    pole("ClubSlot", at(0.47, 0, 0.85), at(0.57, 0, 0.85), 0.03, chrome, verts=6)
    pole("ClubSlot", at(0.58, 0, 0.82), at(0.62, 0.1, 1.52), 0.024, chrome, verts=6)
    blob("ClubSlot", at(0.62, 0.1, 1.58), (0.08, 0.08, 0.08), red, 2)


def card_table(x, y, felt, wood, chips, card, ink, brass):
    """Столик: зелёное сукно, раскрытые карты, стопки фишек, пепельница с сигарой, стул отодвинут. Игра тут идёт и без нас."""
    lathe("ClubTable", (x, y, FLOOR), [(0.42, 0), (0.28, 0.05), (0.07, 0.12), (0.07, 0.78), (0.1, 0.8)], wood, segments=10)
    lathe("ClubTable", (x, y, FLOOR + 0.8), [(0.82, 0), (0.86, 0.03), (0.86, 0.07), (0.8, 0.09), (0, 0.09)], felt, segments=18)
    torus("ClubTable", (x, y, FLOOR + 0.875), 0.85, 0.03, wood, 18, 4, axis="Z")
    top = FLOOR + 0.895
    for k, (dx, dy) in enumerate(((-0.34, -0.2), (-0.1, -0.42), (0.36, 0.22))):
        h = 0.0
        for n in range(3 + k * 2):
            cyl("ClubTable", (x + dx, y + dy, top + h + 0.012), 0.075, 0.024, chips[(n + k) % len(chips)], verts=10)
            h += 0.026
    for k in range(4):
        a = 0.25 + k * 0.22
        c = box("ClubTable", (x + 0.12 + k * 0.07, y - 0.1 - k * 0.015, top), (0.17, 0.25, 0.006), card, bevel=0)
        c.rotation_euler = (0, 0, a - 0.5)
    pole("ClubTable", (x + 0.15, y - 0.13, top + 0.009), (x + 0.15, y - 0.09, top + 0.009), 0.024, ink, verts=4)
    lathe("ClubTable", (x - 0.36, y + 0.32, top), [(0.1, 0), (0.12, 0.05), (0.07, 0.05), (0.07, 0.02), (0, 0.02)], brass, segments=10)
    pole("ClubTable", (x - 0.36, y + 0.32, top + 0.06), (x - 0.15, y + 0.42, top + 0.065), 0.019, mat("BasementCigar", "#5d3a22", 0.9), verts=6)
    blob("ClubTable", (x - 0.15, y + 0.42, top + 0.065), (0.021, 0.021, 0.021), mat("BasementEmber", "#f05a26", 1, emit=2.0), 1)


def chair(x, y, turn, wood, seat):
    ca, sa = math.cos(turn), math.sin(turn)
    w = 0.5
    box("ClubChair", (x, y, FLOOR + 0.5), (w, w, 0.07), seat, bevel=0.012).rotation_euler = (0, 0, turn)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        a = x + ca * dx * w * 0.42 - sa * dy * w * 0.42
        b = y + sa * dx * w * 0.42 + ca * dy * w * 0.42
        pole("ClubChair", (a, b, FLOOR), (a, b, FLOOR + 0.5), 0.024, wood, verts=5)
        if dy > 0:
            pole("ClubChair", (a, b, FLOOR + 0.55), (a, b, FLOOR + 1.08), 0.024, wood, verts=5)
    bx, by = x - sa * w * 0.42, y + ca * w * 0.42
    for z in (0.78, 1.0):
        box("ClubChair", (bx, by, FLOOR + z), (w * 0.86, 0.03, 0.09), wood, bevel=0.005).rotation_euler = (0, 0, turn)


def velvet_rope(x0, y0, x1, y1, brass, velvet, posts=2):
    """Бархатный канат на латунных столбиках: вход в клуб, всё серьёзно."""
    points = [(x0 + (x1 - x0) * k / (posts - 1), y0 + (y1 - y0) * k / (posts - 1)) for k in range(posts)]
    for px, py in points:
        lathe("ClubRope", (px, py, FLOOR), [(0.2, 0), (0.18, 0.04), (0.04, 0.09), (0.03, 0.72), (0.05, 0.86), (0.075, 0.92), (0.04, 0.98), (0, 1.0)], brass, segments=9)
    for a, b in zip(points, points[1:]):
        path = [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, FLOOR + 0.84 - 0.3 * (1 - (2 * t - 1) ** 2)) for t in (k / 8 for k in range(9))]
        tube("ClubRope", path, 0.038, velvet, sides=6)


def crates(x, y, wood, dark, glass):
    """Ящики с бутылками: контрабанда, горлышки торчат из гнёзд."""
    box("ClubCrate", (x, y, FLOOR), (1.15, 0.78, 0.44), wood, bevel=0.012).rotation_euler = (0, 0, 0.2)
    box("ClubCrate", (x + 0.07, y + 0.02, FLOOR + 0.44), (0.98, 0.7, 0.34), dark, bevel=0.012).rotation_euler = (0, 0, -0.14)
    for dx in (-0.3, -0.05, 0.2, 0.42):
        for dy in (-0.17, 0.11):
            pole("ClubCrate", (x + dx, y + dy, FLOOR + 0.78), (x + dx, y + dy, FLOOR + 0.99), 0.032, glass[rng.randint(0, len(glass))], verts=6)
    for z in (0.14, 0.32):
        box("ClubCrate", (x, y - 0.4, FLOOR + z), (1.17, 0.02, 0.07), dark, bevel=0).rotation_euler = (0, 0, 0.2)


def poster(x, y, turn, paper, shapes):
    """Афиша на кирпиче: большая кость и полосы вместо букв, чтобы подходила на всех языках. dz — от низа листа."""
    bottom = -0.3
    ca, sa = math.cos(turn), math.sin(turn)
    box("WallPoster", (x, y, bottom), (1.0, 0.016, 1.42), paper, bevel=0).rotation_euler = (0, 0, turn)
    for dx, dz, w, h, material, lift in shapes:
        part = box("WallPoster", (x + ca * dx + sa * lift, y + sa * dx - ca * lift, bottom + dz), (w, 0.008, h), material, bevel=0)
        part.rotation_euler = (0, 0, turn)


def poster_die(face, dot, dots):
    """Кость на афише: квадрат и точки. Возвращает детали для poster."""
    size = 0.62
    base = 0.56
    parts = [(0, base, size, size, face, 0.018)]
    for dx, dz in dots:
        parts.append((dx * 0.19, base + size / 2 + dz * 0.19 - 0.05, 0.1, 0.1, dot, 0.03))
    return parts


def hanging_bulb(x, y, top, length, iron, bulb, phase):
    """Голая лампочка на проводе: качается от сквозняка, тёплая точка в темноте."""
    hinge = Vector((x, y, top))
    z = top - length

    def wind(point):
        return (min((hinge - point).length / length, 1.0), phase, 0.0)

    set_wind(pole("SwayBulb", (x, y, top), (x, y, z + 0.09), 0.009, iron, verts=3), wind)
    set_wind(lathe("SwayBulb", (x, y, z + 0.04), [(0.04, 0), (0.046, 0.08)], iron, segments=6), wind)
    set_wind(blob("SwayBulb", (x, y, z - 0.03), (0.065, 0.065, 0.08), bulb, 1), wind)


def mole_burrow(x, y, dirt, rubble):
    """Нора крота: дыра в плитке, горка земли и битые плитки. В игре из неё вылезает живой крот."""
    mesh = bpy.data.meshes.new("ClubBurrow")
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((x + math.cos(k * 2 * math.pi / 14) * 0.42, y + math.sin(k * 2 * math.pi / 14) * 0.35, FLOOR + 0.012)) for k in range(14)])
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("ClubBurrow", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat("BasementHole", "#0a0706", 1.0))
    for k in range(9):
        a = k * 2 * math.pi / 9 + 0.15
        s = 0.11 + rng.rand() * 0.09
        blob("ClubBurrow", (x + math.cos(a) * (0.48 + rng.rand() * 0.1), y + math.sin(a) * (0.42 + rng.rand() * 0.1), FLOOR + s * 0.3), (s * 1.25, s, s * 0.65), dirt, 2)
    for k in range(5):
        a = 0.6 + k * 1.2
        s = 0.1 + rng.rand() * 0.06
        chip = box("ClubBurrow", (x + math.cos(a) * 0.72, y + math.sin(a) * 0.6, FLOOR + 0.002), (s * 1.6, s, 0.024), rubble[k % len(rubble)], bevel=0)
        chip.rotation_euler = (rng.rand() * 0.12, rng.rand() * 0.12, a)


def basement():
    bricks = image_mat("BasementBrick", brick_image(), rough=0.95)
    concrete = mat("BasementConcrete", "#6c6660", 0.95)
    dark = mat("BasementDark", "#231a18", 0.85)
    wood = mat("BasementWood", "#7b4b2a", 0.75)
    iron = mat("BasementIron", "#40434a", 0.5)
    rust = mat("BasementRust", "#7e4a2c", 0.8)
    brass = mat("BasementBrass", "#d2a23a", 0.4)
    red = mat("BasementRed", "#b82f2f", 0.5)
    steel = mat("BasementSteel", "#5c666d", 0.45)
    velvet = mat("BasementVelvet", "#8a1e38", 0.8)
    cream = mat("BasementCream", "#e9dfc4", 0.8)
    felt = mat("BasementFelt", "#26755a", 0.95)
    ink = mat("BasementInk", "#17151a", 0.6)
    gold = mat("BasementGold", "#f2bd32", 0.4)
    pink = mat("BasementNeonPink", "#ff3f9c", 1, emit=4.2)
    cyan = mat("BasementNeonCyan", "#3fe6ff", 1, emit=3.6)
    amber = mat("BasementNeonAmber", "#ffb238", 1, emit=3.4)
    bulb = mat("BasementBulb", "#ffd98c", 1, emit=3.2)
    glass = (mat("BasementGlassG", "#2f7f54", 0.25), mat("BasementGlassA", "#b56c20", 0.25), mat("BasementGlassR", "#962a2a", 0.25), mat("BasementGlassB", "#2f6596", 0.25))
    chips = (red, cream, mat("BasementChipB", "#2f5f9c", 0.6), ink)

    ground("Floor", -SIDE - 0.1, SIDE + 0.1, FRONT - 3, BACK + 0.1, floor_image())
    room(bricks, concrete, dark)

    # Дальняя стена: вывеска по центру, бар слева, дверь справа.
    neon_sign(0.0, 2.25, mat("BasementBoard", "#0a080d", 0.6), pink, cyan, amber)
    bar(wood, dark, brass, red, glass)
    steel_door(6.2, steel, dark, brass, red)
    pipes(iron, rust, red, brass)

    # За столом: бархатные канаты, нора крота, музыкальный автомат.
    velvet_rope(-1.15, 4.2, 0.9, 4.45, brass, velvet, posts=3)
    mole_burrow(MOLE[0], MOLE[1], mat("BasementDirt", "#5c3f27", 1.0), (mat("BasementTileA", "#5b2224", 0.8), mat("BasementTileB", "#3c383a", 0.8)))
    jukebox(3.55, 4.55, math.radians(-8), mat("BasementJukeBody", "#7a2a22", 0.45), mat("BasementJukeTop", "#c8c8c0", 0.35), brass, pink, amber, cyan, ink)

    # По бокам и спереди: столик с картами и стулом, автомат, ящики, афиши.
    card_table(6.05, 0.95, felt, wood, chips, cream, ink, brass)
    chair(5.05, 1.95, math.radians(32), wood, velvet)
    chair(7.0, 1.95, math.radians(-40), wood, velvet)
    slot_machine(-6.45, -1.05, math.radians(68), mat("BasementSlotBody", "#b83a2c", 0.4), mat("BasementChrome", "#a7adb2", 0.3), cream, red, gold, ink)
    slot_machine(-6.75, 0.55, math.radians(84), mat("BasementSlotBody2", "#2d5f8a", 0.4), mat("BasementChrome", "#a7adb2", 0.3), cream, red, gold, ink)
    crates(6.2, -2.85, wood, dark, glass)
    poster(-2.7, BACK - 0.03, 0, cream, poster_die(red, cream, ((-1, 1), (0, 0), (1, -1))) + [(0, 0.26, 0.76, 0.1, ink, 0.018), (0, 0.1, 0.46, 0.07, ink, 0.018)])
    poster(2.15, BACK - 0.03, 0, mat("BasementPosterB", "#d9b15a", 0.9), poster_die(ink, gold, ((-1, 1), (1, 1), (0, 0), (-1, -1), (1, -1))) + [(0, 0.26, 0.76, 0.1, red, 0.018), (0, 0.1, 0.46, 0.07, ink, 0.018)])

    # Лампочки на проводах: живые точки над столом и у стойки.
    for x, y, length, phase in ((-4.9, 2.8, 2.3, 0.2), (4.4, 1.9, 2.5, 0.55), (-3.4, -3.0, 2.9, 0.8), (6.5, -0.9, 2.2, 0.35)):
        top = WALL_TOP + VAULT * math.sqrt(max(1 - (x / SIDE) ** 2, 0)) - 0.02
        hanging_bulb(x, y, top, top - 0.95 if top - 0.95 < length else length, iron, bulb, phase)


def lights():
    def add(name, kind, energy, color, location, rotation=None, soft=0.3, spot=None):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = color
        data.shadow_soft_size = soft
        if spot:
            data.spot_size, data.spot_blend = spot
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        if rotation:
            obj.rotation_mode = "QUATERNION"
            obj.rotation_quaternion = rotation

    down = Vector((0, 0, -1)).to_track_quat("-Z", "Y")
    add("TableLamp", "SPOT", 420, (1.0, 0.8, 0.56), (0, 0, 3.4), down, 0.35, (math.radians(105), 0.65))

    # Неон красит кирпич и пол цветом.
    add("NeonPink", "POINT", 120, (1.0, 0.24, 0.62), (0, BACK - 0.9, 2.1), None, 0.8)
    add("NeonCyanL", "POINT", 55, (0.25, 0.9, 1.0), (-1.2, BACK - 0.9, 2.2), None, 0.5)
    add("NeonCyanR", "POINT", 55, (0.25, 0.9, 1.0), (1.2, BACK - 0.9, 2.2), None, 0.5)
    add("Juke", "POINT", 45, (1.0, 0.5, 0.45), (3.55, 3.6, -0.5), None, 0.6)
    add("BarShelf", "POINT", 80, (1.0, 0.62, 0.28), (-5.5, BACK - 0.9, 0.5), None, 1.0)
    add("BarFront", "POINT", 60, (1.0, 0.7, 0.42), (-5.5, 2.3, 0.3), None, 1.0)
    add("Door", "POINT", 45, (1.0, 0.76, 0.42), (6.2, BACK - 0.7, 1.05), None, 0.3)
    # Лампочки на проводах и слабый заполняющий: тьма, но видно.
    for x, y, z, energy in ((-4.9, 2.8, 1.2, 55), (4.4, 1.9, 1.4, 70), (-3.4, -3.0, 1.0, 90), (6.5, -0.9, 1.1, 90)):
        add("Bulb", "POINT", energy, (1.0, 0.76, 0.46), (x, y, z), None, 0.2)
    for x, y, z, energy in ((0, -4.4, 1.9, 110), (-5, -5.4, 1.8, 60), (5, -5.4, 1.8, 60), (0, 3.2, 1.0, 26)):
        add("Fill", "POINT", energy, (0.86, 0.74, 0.86), (x, y, z), None, 1.6)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.1, 0.09, 0.12, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.22
    bpy.context.scene.world = world


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        kit.UV_ANGLE = 66
        kit.UV_MARGIN = 0.006
        texture_groups = (
            ("Wall", 1024, lambda name: name.startswith("Wall")),
            ("Club", 1024, lambda name: name.startswith(("Club", "Bottle"))),
            ("Rig", 256, lambda name: name.startswith("Sway")),
        )

        def build():
            basement()
            bc.matte()

        views = (("podval_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("podval_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42), ("podval_igra_sopernik.png", (0, 1.55, -1.4), (0, 0.72, 2.9), 45))
        folder = argv[argv.index("--bake") + 1]
        os.makedirs(folder, exist_ok=True)
        kit.bake_location("Basement", "Room_Basement.fbx", build, lights, views, samples, folder, groups=texture_groups)
        return

    folder = argv[argv.index("--render") + 1]
    os.makedirs(folder, exist_ok=True)
    basement()
    bc.matte()
    zg.report_triangles()
    # Крот для снимка: в игре его добавляет генератор.
    armature, _ = life.mole()
    armature.location = (MOLE[0], MOLE[1], FLOOR + 0.42)
    kit.table()
    lights()
    kit.render(os.path.join(folder, "podval_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "podval_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)
    kit.render(os.path.join(folder, "podval_magazin.png"), (5.2, 3.3, -8.4), (0, -1.3, 1.2), 50, samples)
    kit.render(os.path.join(folder, "podval_sopernik.png"), (0, 1.55, -1.4), (0, 0.72, 2.9), 45, samples)


if __name__ == "__main__":
    main()
