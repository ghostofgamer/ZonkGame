# -*- coding: utf-8 -*-
"""
Локация «Чердак», глава 6: пыльный чердак старьёвщика Тихона. Косые скаты крыши и стропила, тёмная древесина, луч дня из круглого окна,
гора старых вещей, у каждой своя история. Живое: кот на сундуке (zonk_attic_life.py), пыль и луч добавляет генератор.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_attic.py -- --render <папка> [--samples 48]
    blender --background --factory-startup --python Tools/Blender/zonk_attic.py -- --bake <папка превью> [--samples 64]
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, пол на -1.6.
Рука соперника на x −2.2, z 2.3 (рукав до z 3.5): выше стола там ничего нет. За столом сидит соперник: высокое — в стороны.
Кадр партии: на z 4 видно до высоты 1.4, на z 5.6 до 0.95.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_attic_life as life  # noqa: E402
import zonk_beach as bc  # noqa: E402
import zonk_garden as zg  # noqa: E402
import zonk_room as kit  # noqa: E402
import zonk_ship as zs  # noqa: E402
import zonk_tavern as tv  # noqa: E402

from zonk_beach import box, ground, pole  # noqa: E402
from zonk_room import FLOOR, blob, cyl, image_mat, mat  # noqa: E402
from zonk_ship import set_wind, smooth_path, strip, tube  # noqa: E402
from zonk_tavern import barrel_image, lathe, torus  # noqa: E402

BACK = 5.7
FRONT = -9.5
# Крыша выше камер меню и магазина (самая дальняя от центра: x 5.2, высота 3.3): они стоят внутри чердака и балки не закрывают вид.
EAVE_X = 9.2
EAVE_Z = -0.2
RIDGE_Z = 8.0
rng = np.random.RandomState(6)


def roof_height(x):
    """Высота ската над точкой x: от карниза вверх к коньку."""
    return EAVE_Z + (RIDGE_Z - EAVE_Z) * (1 - abs(x) / EAVE_X)


# ---------- Картинки ----------

def floor_image():
    """Широкие старые половицы вдоль комнаты, щели, сучки, пыльные светлые пятна. Под столом вытертый круглый ковёр."""
    size = 2048
    x0, x1, y0, y1 = -EAVE_X - 0.1, EAVE_X + 0.1, FRONT - 3, BACK + 0.1
    local = np.random.RandomState(16)
    image = np.zeros((size, size, 3))
    count = 22
    width = size // count
    for i in range(count + 1):
        c0, c1 = i * width, min((i + 1) * width, size)
        row = -local.randint(0, 600)
        while row < size:
            length = local.randint(620, 1100)
            tone = 0.54 + local.rand() * 0.12
            color = np.array([tone, tone * 0.76, tone * 0.56])
            r0, r1 = max(row, 0), min(row + length, size)
            image[r0:r1, c0:c1] = color
            for _ in range(4):
                gx = local.randint(c0 + 6, max(c0 + 7, c1 - 4))
                image[r0:r1, gx:gx + 2] = color * 0.84
            if local.rand() < 0.45 and r1 - r0 > 60:
                kr, kc = local.randint(r0 + 20, r1 - 20), local.randint(c0 + 16, max(c0 + 17, c1 - 16))
                yy, xx = np.mgrid[max(kr - 16, 0):min(kr + 16, size), max(kc - 16, 0):min(kc + 16, size)]
                d = ((xx - kc) / 13.0) ** 2 + ((yy - kr) / 8.0) ** 2
                image[yy[d < 1], xx[d < 1]] = color * 0.62
            if r1 < size:
                image[r1 - 4:r1, c0:c1] = (0.07, 0.05, 0.04)
            row += length
        image[:, c0:c0 + 5] = (0.05, 0.038, 0.03)

    py, px = np.mgrid[0:size, 0:size]
    wx = x0 + (x1 - x0) * px / size
    wy = y1 + (y0 - y1) * py / size
    dust = zs.cloud_noise(size, size, 9, 1.0, 2.2)
    image[dust > 0.56] = image[dust > 0.56] * 0.86 + np.array([0.11, 0.105, 0.095])
    image[dust > 0.72] = image[dust > 0.72] * 0.86 + np.array([0.1, 0.1, 0.09])

    # Круглый ковёр: выцветший, с каймой и узором, край вытерт. Цветное пятно посреди коричневого.
    a = np.arctan2(wy - 0.2, wx)
    d = np.sqrt((wx / 4.7) ** 2 + ((wy - 0.2) / 3.7) ** 2) + 0.005 * np.sin(a * 23)
    worn = zs.cloud_noise(size, size, 4, 1.0, 1.6)
    rug = d < 1.0
    image[rug] = (0.56, 0.36, 0.32)
    image[(d > 0.93) & (d < 0.975)] = (0.68, 0.58, 0.42)
    image[(d > 0.86) & (d < 0.875)] = (0.4, 0.46, 0.46)
    pattern = (np.sin(a * 60) > 0.4) & (d > 0.885) & (d < 0.92)
    image[pattern] = (0.68, 0.58, 0.42)
    faded = rug & (worn > 0.55)
    image[faded] = image[faded] * 0.72 + np.array([0.17, 0.155, 0.13])
    return np.clip(image, 0, 1)


# ---------- Комната ----------

def window_view():
    """Вид из окна: дневное небо с облаком, ряд крыш с трубами и шпиль вдали. Светится сам, плоскими пятнами."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w]
    u, v = x / w, 1 - y / h
    image = np.array([0.72, 0.88, 0.96]) * (1 - v[..., None]) + np.array([0.42, 0.68, 0.92]) * v[..., None]
    cloud = (((u - 0.3) / 0.2) ** 2 + ((v - 0.72) / 0.07) ** 2 < 1) | (((u - 0.42) / 0.16) ** 2 + ((v - 0.77) / 0.09) ** 2 < 1) | (((u - 0.72) / 0.14) ** 2 + ((v - 0.62) / 0.05) ** 2 < 1)
    image[cloud] = (0.97, 0.98, 0.97)
    # Шпиль и дальние крыши, потом ближние.
    far = 0.3 + 0.07 * np.abs(np.sin(u * 15))
    image[v < far] = (0.56, 0.68, 0.76)
    spire = (np.abs(u - 0.68) < 0.11 * np.clip(0.6 - v, 0, 1)) & (v < 0.6)
    image[spire] = (0.5, 0.62, 0.72)
    near = 0.2 + 0.13 * np.abs(((u * 3.2) % 1) * 2 - 1)
    image[v < near] = (0.62, 0.34, 0.26)
    chimney = (np.abs(u - 0.24) < 0.03) & (v < 0.37)
    image[chimney] = (0.42, 0.26, 0.22)
    return np.clip(image, 0, 1)


def fan_window(x, wall_y, z, radius, dark, wood, iron):
    """
    Круглое окно-веер: панорама в стекле, свинцовые лучи от центра, цветные стёкла в ободе, полукруглая ступица, толстая рама в три ступени
    с тремя камнями, подоконник. Самая светлая точка сцены.
    """
    y = wall_y - 0.05

    def ring_point(a, r, depth):
        return (x + math.cos(a) * r, wall_y - depth, z + math.sin(a) * r)

    # Стекло: диск с панорамой, светится.
    mesh = bpy.data.meshes.new("WindowGlass")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = [bm.verts.new(ring_point(k * 2 * math.pi / 28, radius, 0.05)) for k in range(28)]
    face = bm.faces.new(verts)
    for loop in face.loops:
        loop[uv].uv = ((loop.vert.co.x - x) / (2 * radius) + 0.5, (loop.vert.co.z - z) / (2 * radius) + 0.5)
    face.normal_update()
    if face.normal.y > 0:
        bmesh.ops.reverse_faces(bm, faces=[face])
    bm.to_mesh(mesh)
    bm.free()
    material = image_mat("AtticWindowView", window_view(), rough=1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    texture = next(n for n in nodes if n.type == "TEX_IMAGE")
    emit = nodes.new("ShaderNodeEmission")
    emit.inputs["Strength"].default_value = 1.7
    links.new(texture.outputs[0], emit.inputs[0])
    output = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
    links.new(emit.outputs[0], output.inputs[0])
    obj = bpy.data.objects.new("WindowGlass", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(material)

    # Цветные стёкла по ободу: тёплая кайма, пятна цвета на тёмной стене.
    colors = ("#d9a13a", "#c8553f", "#3d8f8a", "#d9a13a", "#7a5aa6", "#c8553f", "#3d8f8a", "#d9a13a")
    spokes = 16
    inner, outer = radius * 0.72, radius * 0.97
    for k in range(spokes):
        a0, a1 = k * 2 * math.pi / spokes, (k + 1) * 2 * math.pi / spokes
        pane_mesh = bpy.data.meshes.new("WindowPane")
        pane_bm = bmesh.new()
        points = [ring_point(a0 + (a1 - a0) * t, inner, 0.06) for t in (0, 0.5, 1)] + [ring_point(a1 - (a1 - a0) * t, outer, 0.06) for t in (0, 0.5, 1)]
        pane = pane_bm.faces.new([pane_bm.verts.new(q) for q in points])
        pane.normal_update()
        if pane.normal.y > 0:
            bmesh.ops.reverse_faces(pane_bm, faces=[pane])
        pane_bm.to_mesh(pane_mesh)
        pane_bm.free()
        pane_obj = bpy.data.objects.new("WindowPane", pane_mesh)
        bpy.context.collection.objects.link(pane_obj)
        pane_mesh.materials.append(mat("AtticPane%d" % (k % 8), colors[k % 8], 1, emit=1.25))

    # Свинец: лучи, кольца, ступица-полукруг снизу не нужна: круг в центре.
    for k in range(spokes):
        a = k * 2 * math.pi / spokes
        r0 = radius * 0.2 if k % 2 == 0 else inner
        pole("WindowFrame", ring_point(a, r0, 0.075), ring_point(a, radius, 0.075), 0.018, iron, verts=4)
    for r in (radius * 0.2, inner):
        tube("WindowFrame", [ring_point(k * 2 * math.pi / 24, r, 0.075) for k in range(25)], 0.019, iron, sides=4)

    # Рама тремя ступенями и камни на сторонах света, верхний — замковый.
    for r, thick, depth in ((radius + 0.06, 0.1, 0.1), (radius + 0.2, 0.075, 0.07), (radius + 0.31, 0.05, 0.04)):
        tube("WindowFrame", [ring_point(k * 2 * math.pi / 28, r, depth) for k in range(29)], thick, wood if r < radius + 0.1 else dark, sides=6)
    for a, w, h in ((math.pi / 2, 0.3, 0.42), (0.0, 0.28, 0.24), (math.pi, 0.28, 0.24)):
        px, _, pz = ring_point(a, radius + 0.2, 0)
        box("WindowFrame", (px, wall_y - 0.13, pz - h / 2), (w, 0.1, h), wood, bevel=0.018)
    # Подоконник на двух кронштейнах и старая банка на нём.
    sill_z = z - radius - 0.44
    box("WindowSill", (x, wall_y - 0.2, sill_z), (1.75, 0.34, 0.08), wood, bevel=0.018)
    for sign in (-1, 1):
        tube("WindowSill", smooth_path([(x + sign * 0.55, wall_y - 0.04, sill_z - 0.28), (x + sign * 0.55, wall_y - 0.08, sill_z - 0.07), (x + sign * 0.55, wall_y - 0.3, sill_z)], 3), 0.024, dark, sides=4)
    lathe("WindowSill", (x + 0.55, wall_y - 0.2, sill_z + 0.08), [(0.09, 0), (0.1, 0.05), (0.08, 0.2), (0.04, 0.26), (0.05, 0.3), (0, 0.3)], mat("AtticJar", "#4d8079", 0.3), segments=8)
    lathe("WindowSill", (x - 0.55, wall_y - 0.2, sill_z + 0.08), [(0.08, 0), (0.1, 0.08), (0.06, 0.14), (0.0, 0.14)], mat("AtticClay", "#a7553a", 0.8), segments=8)


def room(boards, rafter, dark, glow, iron):
    samples = [FRONT + (BACK - FRONT) * k / 2 for k in range(3)]
    for name, sign in (("Left", -1), ("Right", 1)):
        strip(name, [[(sign * EAVE_X, y, EAVE_Z), (0.0, y, RIDGE_Z)] for y in samples], boards, 0.9, facing=(-sign, 0, -0.8))
        # Низкая стенка под скатом.
        strip(name, [[(sign * EAVE_X, y, FLOOR), (sign * EAVE_X, y, EAVE_Z)] for y in samples], boards, 0.9, facing=(-sign, 0, 0))

    # Задняя стена — фронтон с круглым окном.
    columns = [-EAVE_X + 2 * EAVE_X * k / 8 for k in range(9)]
    strip("Back", [[(x, BACK, FLOOR), (x, BACK, roof_height(x))] for x in columns], boards, 0.9, facing=(0, -1, 0))

    fan_window(0.0, BACK, 2.05, 0.9, dark, rafter, iron)

    # Стропила по скатам, затяжки поперёк, стойка до конька, конёк. Несущие балки: чердак держится на них.
    y = FRONT + 0.8
    while y < BACK:
        for sign in (-1, 1):
            pole("Rafter", (sign * EAVE_X, y, EAVE_Z - 0.02), (0.0, y, RIDGE_Z - 0.02), 0.085, rafter, verts=5)
        y += 2.5
    pole("Rafter", (0, FRONT, RIDGE_Z - 0.04), (0, BACK, RIDGE_Z - 0.04), 0.12, rafter, verts=6)
    for sign in (-1, 1):
        pole("Rafter", (sign * 3.6, FRONT, roof_height(3.6) - 0.08), (sign * 3.6, BACK, roof_height(3.6) - 0.08), 0.08, rafter, verts=5)
    beam_y = 3.3
    beam_z = 2.75
    half = EAVE_X * (RIDGE_Z - beam_z) / (RIDGE_Z - EAVE_Z)
    box("Beam", (0, beam_y, beam_z - 0.11), (half * 2, 0.22, 0.22), rafter, bevel=0.02)
    box("Beam", (0, beam_y, beam_z + 0.11), (0.2, 0.2, RIDGE_Z - beam_z - 0.2), rafter, bevel=0.02)
    for sign in (-1, 1):
        pole("Beam", (sign * 0.08, beam_y, beam_z + 1.25), (sign * 2.1, beam_y, beam_z + 0.08), 0.06, rafter, verts=5)

    # Гвозди и железные скобы на затяжке: крупная мелочь.
    for x in (-2.8, -1.5, 1.5, 2.8):
        box("Beam", (x, beam_y - 0.12, beam_z - 0.13), (0.1, 0.03, 0.26), iron, bevel=0)
    # Паутина в углах: тонкие нити.
    web = mat("AtticWeb", "#c2bdae", 0.9)
    for sign, corner_y in ((-1, BACK - 0.05), (1, BACK - 0.05)):
        cx, cz = sign * 5.4, roof_height(5.4) - 0.05
        for k in range(6):
            a = 0.3 + k * 0.26
            end = (cx - sign * math.cos(a) * 1.3, corner_y, cz - math.sin(a) * 1.3)
            pole("BeamWeb", (cx, corner_y, cz), end, 0.006, web, verts=3)
        for r in (0.45, 0.85, 1.2):
            arc = [(cx - sign * math.cos(0.3 + k * 0.26) * r, corner_y, cz - math.sin(0.3 + k * 0.26) * r) for k in range(6)]
            tube("BeamWeb", arc, 0.005, web, sides=3)


# ---------- Вещи ----------

def grandfather_clock(x, y, turn, wood, dark, brass, cream, iron):
    """Напольные часы: узкий корпус, резной верх, циферблат, окно с маятником (качается). Заметны в меню и за плечом соперника."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    def piece(center, size, material):
        box("GearClock", at(*center), size, material, bevel=0.012).rotation_euler = (0, 0, turn)

    piece((0, 0, 0), (0.78, 0.5, 0.3), dark)
    piece((0, 0, 0.3), (0.64, 0.42, 1.25), wood)
    piece((0, -0.215, 0.42), (0.42, 0.02, 0.92), mat("AtticClockGlass", "#2a1d17", 0.3))
    piece((0, 0, 1.55), (0.78, 0.5, 0.74), dark)
    piece((0, 0, 2.29), (0.62, 0.38, 0.14), wood)
    piece((0, 0, 2.43), (0.86, 0.46, 0.05), dark)
    for dx in (-0.31, 0.31):
        lathe("GearClock", at(dx, -0.22, 2.46), [(0.03, 0), (0.05, 0.05), (0.01, 0.16)], brass, segments=6)
    face = cyl("GearClock", at(0, -0.25, 1.92), 0.29, 0.02, cream, axis="Y", verts=20)
    face.rotation_euler = (math.pi / 2, 0, turn)
    torus("GearClock", at(0, -0.26, 1.92), 0.3, 0.018, brass, 20, 4, axis="Y")
    for k in range(12):
        a = k * math.pi / 6
        fx, fz = math.sin(a) * 0.235, 1.92 + math.cos(a) * 0.235
        blob("GearClock", at(fx, -0.272, fz), (0.015, 0.008, 0.015), iron, 1)
    pole("GearClock", at(0, -0.272, 1.92), at(-0.11, -0.272, 2.04), 0.009, iron, verts=3)
    pole("GearClock", at(0, -0.272, 1.92), at(0.04, -0.272, 2.1), 0.007, iron, verts=3)
    # Резная верхушка: две завитые дуги, точёные колонки на капители, медные накладки, замочек дверцы.
    for sign in (-1, 1):
        scroll = [at(sign * (0.4 - 0.34 * t), -0.2, 2.5 + 0.34 * math.sin(t * 2.0)) for t in (k / 6 for k in range(7))] + [at(sign * 0.02, -0.2, 2.82), at(sign * 0.09, -0.2, 2.76)]
        tube("GearClock", scroll, 0.035, dark, sides=5)
        lathe("GearClock", at(sign * 0.33, -0.22, 1.56), [(0.04, 0), (0.024, 0.06), (0.034, 0.35), (0.024, 0.66), (0.04, 0.72)], wood, segments=6)
        lathe("GearClock", at(sign * 0.31, -0.2, 0.28), [(0.03, 0), (0.018, 0.04), (0.026, 0.62), (0.018, 1.2), (0.03, 1.26)], dark, segments=5)
        blob("GearClock", at(sign * 0.27, -0.26, 0.06), (0.05, 0.015, 0.05), brass, 1)
    blob("GearClock", at(0, -0.2, 2.84), (0.05, 0.05, 0.055), brass, 1)
    lathe("GearClock", at(0, -0.2, 2.87), [(0.03, 0), (0.012, 0.1), (0.0, 0.17)], brass, segments=6)
    box("GearClock", at(0, -0.265, 1.05), (0.06, 0.012, 0.09), brass, bevel=0)
    # Луна над циферблатом.
    moon = cyl("GearClock", at(0, -0.255, 2.25), 0.1, 0.012, mat("AtticMoon", "#283e6b", 0.5), axis="Y", verts=12)
    moon.rotation_euler = (math.pi / 2, 0, turn)
    blob("GearClock", at(0.035, -0.265, 2.26), (0.042, 0.008, 0.042), brass, 1)
    hinge = Vector(at(0, -0.235, 1.3))

    def wind(point):
        return (min((hinge - point).length / 0.82, 1.0), 0.12, 0.0)

    set_wind(pole("SwayClock", tuple(hinge), at(0, -0.235, 0.62), 0.014, brass, verts=4), wind)
    bob = cyl("SwayClock", at(0, -0.235, 0.6), 0.13, 0.025, brass, axis="Y", verts=14)
    bob.rotation_euler = (math.pi / 2, 0, turn)
    set_wind(bob, wind)


def dress_form(x, y, turn, cloth, wood, measure):
    """Манекен портнихи: торс на треноге, сантиметр на плече. Читается силуэтом."""
    body = lathe("GearForm", (x, y, FLOOR + 0.85), [(0.0, 0), (0.18, 0.02), (0.235, 0.17), (0.17, 0.42), (0.225, 0.68), (0.26, 0.78), (0.2, 0.92), (0.06, 1.0), (0.06, 1.08), (0.075, 1.1), (0.0, 1.12)], cloth, segments=12)
    body.scale = (1.05, 0.72, 1.0)
    pole("GearForm", (x, y, FLOOR + 0.2), (x, y, FLOOR + 0.87), 0.025, wood, verts=6)
    for k in range(3):
        a = turn + k * 2 * math.pi / 3
        pole("GearForm", (x, y, FLOOR + 0.26), (x + math.cos(a) * 0.36, y + math.sin(a) * 0.36, FLOOR), 0.024, wood, verts=5)
    tube("GearForm", smooth_path([(x - 0.16, y - 0.04, FLOOR + 1.66), (x - 0.13, y - 0.15, FLOOR + 1.6), (x - 0.11, y - 0.17, FLOOR + 1.3), (x - 0.14, y - 0.18, FLOOR + 1.06)], 3), 0.014, measure, sides=4)


def rocking_horse(x, y, turn, paint, saddle, mane, wood, dark):
    """Лошадка-качалка: тело и голова бочонками, грива, седло, дуги-полозья. Покачивается. Главное яркое пятно справа."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    def wind(point):
        return (min((point.z - FLOOR) / 1.5, 1.0) ** 0.8 * 0.55, 0.65, 0.0)

    parts = []
    parts.append(tube("SwayHorse", smooth_path([at(-0.58, 0, 0.85), at(-0.2, 0, 0.9), at(0.28, 0, 0.92), at(0.56, 0, 0.98)], 5), 0.205, paint, sides=12))
    parts.append(tube("SwayHorse", smooth_path([at(0.48, 0, 1.0), at(0.68, 0, 1.3), at(0.82, 0, 1.5)], 4), 0.105, paint, sides=10))
    parts.append(tube("SwayHorse", smooth_path([at(0.74, 0, 1.53), at(0.95, 0, 1.46), at(1.1, 0, 1.33)], 4), 0.095, paint, sides=10))
    parts.append(blob("SwayHorse", at(1.13, 0, 1.31), (0.085, 0.085, 0.08), dark, 1))
    for sign in (-1, 1):
        parts.append(blob("SwayHorse", at(0.88, sign * 0.1, 1.53), (0.038, 0.025, 0.038), mat("AtticHorseEye", "#17131a", 0.3), 1))
        parts.append(tube("SwayHorse", [at(0.72, sign * 0.06, 1.63), at(0.7, sign * 0.075, 1.78)], 0.03, paint, sides=4))
        for dx, end in ((0.42, 0.72), (-0.42, -0.72)):
            parts.append(tube("SwayHorse", [at(dx, sign * 0.14, 0.76), at(end, sign * 0.2, 0.25)], 0.056, paint, sides=6))
            parts.append(blob("SwayHorse", at(end, sign * 0.2, 0.25), (0.07, 0.07, 0.05), dark, 1))
    for k in range(8):
        t = k / 7
        p = (0.72 - t * 0.3, 0, 1.68 - t * 0.6)
        parts.append(blob("SwayHorse", at(p[0] - 0.09, (k % 2 - 0.5) * 0.04, p[2]), (0.085, 0.05, 0.1), mane, 2 if k < 3 else 1))
    parts.append(tube("SwayHorse", smooth_path([at(-0.72, 0, 1.06), at(-0.94, 0, 0.92), at(-0.97, 0, 0.55)], 3), 0.06, mane, sides=5))
    parts.append(tube("SwayHorse", smooth_path([at(-0.14, -0.22, 1.02), at(-0.14, 0, 1.13), at(-0.14, 0.22, 1.02)], 3), 0.14, saddle, sides=6))
    parts.append(pole("SwayHorse", at(0.62, -0.18, 1.27), at(0.62, 0.18, 1.27), 0.02, wood, verts=5))
    # Яблоки на крупе и шее, узда, поводья, чепрак, звезда на лбу.
    dapple = mat("AtticDapple", "#a98c62", 0.85)
    for k, (dx, z_part) in enumerate(((-0.42, 0.97), (-0.26, 0.83), (-0.5, 0.82), (0.25, 0.8), (0.38, 0.97), (0.1, 0.77), (0.6, 1.16))):
        for sign in (-1, 1):
            parts.append(blob("SwayHorse", at(dx, sign * 0.185, z_part), (0.05, 0.02, 0.04), dapple, 1))
    rein = mat("AtticRein", "#a4312a", 0.7)
    for sign in (-1, 1):
        parts.append(tube("SwayHorse", smooth_path([at(1.05, sign * 0.1, 1.36), at(0.82, sign * 0.13, 1.28), at(0.6, sign * 0.16, 1.22)], 3), 0.012, rein, sides=3))
        parts.append(blob("SwayHorse", at(1.03, sign * 0.1, 1.37), (0.03, 0.016, 0.03), mat("AtticBrass", "#c9982f", 0.45), 1))
    parts.append(tube("SwayHorse", [at(1.0, -0.1, 1.42), at(1.0, 0, 1.47), at(1.0, 0.1, 1.42)], 0.012, rein, sides=3))
    parts.append(tube("SwayHorse", [at(0.86, -0.1, 1.58), at(0.82, 0, 1.64), at(0.86, 0.1, 1.58)], 0.012, rein, sides=3))
    parts.append(tube("SwayHorse", smooth_path([at(-0.42, -0.21, 0.93), at(-0.14, -0.235, 0.84), at(0.14, -0.21, 0.92)], 3), 0.022, saddle, sides=3))
    parts.append(tube("SwayHorse", smooth_path([at(-0.42, 0.21, 0.93), at(-0.14, 0.235, 0.84), at(0.14, 0.21, 0.92)], 3), 0.022, saddle, sides=3))
    parts.append(blob("SwayHorse", at(0.94, 0, 1.56), (0.03, 0.05, 0.022), mat("AtticCream", "#e8dcbc", 0.8), 1))
    for part in parts:
        set_wind(part, wind)
    for sign in (-1, 1):
        rocker = [at(-1.15 + 2.3 * k / 12, sign * 0.24, 0.05 + 0.32 * ((-1.15 + 2.3 * k / 12) / 1.15) ** 2) for k in range(13)]
        tube("GearHorse", rocker, 0.052, wood, sides=5)
    for dx in (-0.72, 0.0, 0.72):
        pole("GearHorse", at(dx, -0.24, 0.05 + 0.32 * (dx / 1.15) ** 2 + 0.02), at(dx, 0.24, 0.05 + 0.32 * (dx / 1.15) ** 2 + 0.02), 0.03, wood, verts=5)
    for sign in (-1, 1):
        pole("GearHorse", at(-0.72, sign * 0.24, 0.25), at(0.72, sign * 0.24, 0.25), 0.02, wood, verts=4)


def covered_mirror(x, y, turn, sheet, frame, wood):
    """Высокое зеркало под простынёй: виден край резной рамы, складки ниспадают, нижний край колышется от сквозняка."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    box("GearMirror", at(0, 0.05, 0), (1.3, 0.14, 2.42), frame, bevel=0.02).rotation_euler = (0, 0, turn)
    # Резной верх рамы торчит над простынёй.
    arch = [at(-0.65 + 1.3 * k / 10, 0.02, 2.42 + 0.34 * math.sin(math.pi * k / 10) ** 1.4) for k in range(11)]
    tube("GearMirror", arch, 0.07, frame, sides=5)
    blob("GearMirror", at(0, 0.0, 2.84), (0.07, 0.05, 0.1), mat("AtticBrass", "#c9982f", 0.45), 1)
    for dx in (-0.45, 0.45):
        pole("GearMirror", at(dx, 0.07, 0), at(dx, 0.68, 0.72), 0.04, wood, verts=5)
    mesh = bpy.data.meshes.new("SwaySheet")
    bm = bmesh.new()
    wind_layer = bm.loops.layers.float_color.new("Wind")
    columns, rows = 14, 8
    grid = []
    for c in range(columns + 1):
        u = c / columns - 0.5
        row = []
        for r in range(rows + 1):
            v = r / rows
            fold = (math.sin(u * 21 + 1.0) * 0.08 + math.sin(u * 47 + v * 3) * 0.03) * (0.2 + v)
            row.append(bm.verts.new(at(u * (1.24 + 0.46 * v), -0.05 - v * 0.34 + fold, 2.48 - v * 2.46 + (0.09 * abs(math.sin(u * 13)) - 0.02) * v)))
        grid.append(row)
    for c in range(columns):
        for r in range(rows):
            face = bm.faces.new((grid[c][r], grid[c + 1][r], grid[c + 1][r + 1], grid[c][r + 1]))
            for loop, (cc, rr) in zip(face.loops, ((c, r), (c + 1, r), (c + 1, r + 1), (c, r + 1))):
                loop[wind_layer] = (((rr / rows) ** 2) * 0.45, (0.1 + cc * 0.07) % 1.0, 0.12 * rr / rows, 1.0)
    bm.normal_update()
    wrong = [face for face in bm.faces if face.normal.y > 0]
    if wrong:
        bmesh.ops.reverse_faces(bm, faces=wrong)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new("SwaySheet", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(sheet)


def gramophone(x, y, z, turn, wood, brass, dark):
    """Граммофон: ящик, пластинка, трубка и большая медная труба-цветок."""
    box("GearGramo", (x, y, z), (0.62, 0.62, 0.3), wood, bevel=0.015).rotation_euler = (0, 0, turn)
    cyl("GearGramo", (x, y, z + 0.32), 0.25, 0.018, dark, verts=16)
    cyl("GearGramo", (x, y, z + 0.335), 0.07, 0.01, mat("AtticLabel", "#c2422f", 0.7), verts=10)
    tip = (x + 0.14, y + 0.17, z + 0.36)
    neck = (x + 0.26, y + 0.28, z + 0.58)
    tube("GearGramo", smooth_path([tip, neck, (x + 0.17, y + 0.25, z + 0.8)], 3), 0.03, brass, sides=6)
    direction = Vector((-0.42, -0.5, 0.56)).normalized()
    # Труба-цветок: лепестки по краю, ребро на каждый стык, внутри темнее.
    quat = direction.to_track_quat("Z", "Y")
    origin = Vector((x + 0.17, y + 0.25, z + 0.8))
    profile = ((0.035, 0.0), (0.05, 0.24), (0.11, 0.48), (0.24, 0.68), (0.42, 0.82))
    petals, per_petal = 8, 3
    count = petals * per_petal
    mesh = bpy.data.meshes.new("GearGramo")
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        ring = []
        for k in range(count):
            a = k * 2 * math.pi / count
            scallop = abs(math.sin(a * petals / 2))
            grow = (h / 0.82) ** 2
            radius = r * (0.84 + 0.2 * scallop * grow + 0.16 * (1 - grow))
            lip = 0.05 * scallop * grow
            ring.append(bm.verts.new(origin + quat @ Vector((math.cos(a) * radius, math.sin(a) * radius, h + lip))))
        rings.append(ring)
    for a_ring, b_ring in zip(rings, rings[1:]):
        for k in range(count):
            n = (k + 1) % count
            bm.faces.new((a_ring[k], a_ring[n], b_ring[n], b_ring[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    horn = bpy.data.objects.new("GearGramo", mesh)
    bpy.context.collection.objects.link(horn)
    mesh.materials.append(brass)
    horn.modifiers.new("s", "SOLIDIFY").thickness = 0.012
    for k in range(petals):
        a = k * 2 * math.pi / petals
        path = [origin + quat @ Vector((math.cos(a) * r * 0.86, math.sin(a) * r * 0.86, h)) for r, h in profile[1:]]
        tube("GearGramo", path, 0.008, mat("AtticBrassDark", "#8d6b1f", 0.5), sides=3)
    # Ручка завода и резные углы ящика.
    tube("GearGramo", smooth_path([(x + 0.31, y - 0.1, z + 0.14), (x + 0.42, y - 0.1, z + 0.14), (x + 0.42, y - 0.22, z + 0.14), (x + 0.42, y - 0.22, z + 0.26)], 2), 0.012, brass, sides=4)
    blob("GearGramo", (x + 0.42, y - 0.22, z + 0.28), (0.028, 0.028, 0.03), dark, 1)
    for ring_r in (0.13, 0.19):
        torus("GearGramo", (x, y, z + 0.33), ring_r, 0.0035, mat("AtticGroove", "#34302c", 0.4), 16, 3, axis="Z")
    pole("GearGramo", (x + 0.16, y + 0.2, z + 0.37), (x + 0.03, y + 0.1, z + 0.345), 0.011, brass, verts=4)


def old_chest(x, y, turn, wood, iron, brass):
    """Сундук с выпуклой крышкой и обручами: на нём спит кот."""
    width, depth, height = 1.45, 0.82, 0.5
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    box("GearChest", at(0, 0, 0), (width, depth, height), wood, bevel=0.012).rotation_euler = (0, 0, turn)
    arc = [math.pi * k / 6 for k in range(7)]
    sections = [[at(dx, math.cos(a) * depth / 2, height + math.sin(a) * 0.3) for a in arc] for dx in (-width / 2, width / 2)]
    lid = strip("GearChest", sections, wood, facing=(0, 0, 1))
    cap = []
    for dx in (-width / 2, width / 2):
        mesh = bpy.data.meshes.new("GearChest")
        bm = bmesh.new()
        bm.faces.new([bm.verts.new(at(dx, math.cos(a) * depth / 2, height + math.sin(a) * 0.3)) for a in arc])
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("GearChest", mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(wood)
        cap.append(obj)
    for dx in (-0.48, 0.0, 0.48):
        tube("GearChest", [at(dx, math.cos(a) * (depth / 2 + 0.012), height + math.sin(a) * 0.312) for a in arc], 0.02, iron, sides=4)
        box("GearChest", at(dx, 0, 0), (0.07, depth + 0.024, height), iron, bevel=0.003).rotation_euler = (0, 0, turn)
    box("GearChest", at(0, -depth / 2 - 0.015, height - 0.15), (0.17, 0.03, 0.2), brass, bevel=0).rotation_euler = (0, 0, turn)
    blob("GearChest", at(0, -depth / 2 - 0.03, height - 0.09), (0.028, 0.012, 0.035), iron, 1)
    # Оковка углов, заклёпки, кольца-ручки, дорожные наклейки.
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("GearChest", at(sx * (width / 2 - 0.07), sy * (depth / 2 - 0.0), 0), (0.17, 0.035, 0.17), brass, bevel=0.004).rotation_euler = (0, 0, turn)
        torus("GearChest", at(sx * (width / 2 + 0.03), 0, height * 0.6), 0.075, 0.012, iron, 10, 3, axis="X")
        for dx in (-0.48, 0.0, 0.48):
            blob("GearChest", at(dx, sx * (depth / 2 + 0.018), height * 0.25), (0.016, 0.008, 0.016), brass, 1)
    for dx, dz, w, h, color in ((-0.26, 0.26, 0.26, 0.15, "#bf4b3a"), (0.25, 0.14, 0.2, 0.18, "#3f7f8a")):
        box("GearChest", at(dx, -depth / 2 - 0.006, dz - h / 2), (w, 0.008, h), mat("AtticTag" + color[1:], color, 0.8), bevel=0).rotation_euler = (0, 0, turn)
    return lid


def suitcase(x, y, z, w, d, h, turn, leather, strap, brass):
    box("GearCase", (x, y, z), (w, d, h), leather, bevel=0.022).rotation_euler = (0, 0, turn)
    ca, sa = math.cos(turn), math.sin(turn)
    for dx in (-w * 0.3, w * 0.3):
        box("GearCase", (x + ca * dx, y + sa * dx, z), (0.05, d + 0.015, h + 0.01), strap, bevel=0.003).rotation_euler = (0, 0, turn)
    hx, hy = x + sa * d / 2, y - ca * d / 2 - 0.01
    box("GearCase", (hx, hy, z + h * 0.72), (0.2, 0.03, 0.04), strap, bevel=0).rotation_euler = (0, 0, turn)
    # Углы, защёлки и наклейки со всего света.
    for dx in (-w * 0.46, w * 0.46):
        for dz in (0.03, h - 0.03):
            blob("GearCase", (x + ca * dx + sa * d / 2, y + sa * dx - ca * d / 2, z + dz), (0.04, 0.022, 0.034), brass, 1)
        box("GearCase", (x + ca * dx * 0.35 + sa * (d / 2 + 0.005), y + sa * dx * 0.35 - ca * (d / 2 + 0.005), z + h * 0.56), (0.05, 0.012, 0.05), brass, bevel=0).rotation_euler = (0, 0, turn)
    label = rng.randint(0, 3)
    color = ("#d6c489", "#c8553f", "#3d7f8a")[label]
    box("GearCase", (x + ca * w * 0.13 + sa * (d / 2 + 0.006), y + sa * w * 0.13 - ca * (d / 2 + 0.006), z + h * 0.18), (w * 0.2, 0.006, h * 0.42), mat("AtticTag" + color[1:], color, 0.8), bevel=0).rotation_euler = (0, 0, turn)


def book_stack(x, y, z, turn, count, palette):
    for k in range(count):
        w, d, h = 0.4 + rng.rand() * 0.18, 0.28 + rng.rand() * 0.08, 0.055 + rng.rand() * 0.04
        book = box("GearBook", (x + (rng.rand() - 0.5) * 0.05, y + (rng.rand() - 0.5) * 0.05, z), (w, d, h), palette[k % len(palette)], bevel=0.006)
        spin = turn + (rng.rand() - 0.5) * 0.5
        book.rotation_euler = (0, 0, spin)
        box("GearBook", (x + math.sin(spin) * (d / 2 + 0.004), y - math.cos(spin) * (d / 2 + 0.004), z + h * 0.34), (w * 0.8, 0.006, h * 0.28), mat("AtticGilt", "#d6ac4e", 0.45), bevel=0).rotation_euler = (0, 0, spin)
        z += h
    return z


def rolled_rug(x, y, turn, color, edge):
    """Свёрнутый ковёр лежит на полу: цилиндр, на торце спираль."""
    ca, sa = math.cos(turn), math.sin(turn)
    length, radius = 2.1, 0.2
    a = (x - ca * length / 2, y - sa * length / 2, FLOOR + radius)
    b = (x + ca * length / 2, y + sa * length / 2, FLOOR + radius)
    pole("GearRug", a, b, radius, color, verts=9)
    for t in (0.1, 0.9):
        px, py = x + ca * (t - 0.5) * length, y + sa * (t - 0.5) * length
        ring = torus("GearRug", (px, py, FLOOR + radius), radius + 0.002, 0.012, edge, 12, 3, axis="X")
        ring.rotation_euler = (0, math.pi / 2, turn)
    for t in (0.34, 0.66):
        px, py = x + ca * (t - 0.5) * length, y + sa * (t - 0.5) * length
        ring = torus("GearRug", (px, py, FLOOR + radius), radius + 0.006, 0.016, mat("AtticRope", "#b39865", 0.95), 12, 3, axis="X")
        ring.rotation_euler = (0, math.pi / 2, turn)


def hanging_cage(x, y, top, wire, perch):
    """Клетка на верёвке с затяжки: качается, пустая дверца открыта. Птица улетела давным-давно."""
    hinge = Vector((x, y, top))
    length = 1.25
    z = top - length

    def wind(point):
        return (min((hinge - point).length / 1.7, 1.0), 0.8, 0.0)

    parts = [pole("SwayCage", (x, y, top), (x, y, z + 0.62), 0.01, wire, verts=3)]
    for level, r in ((0.0, 0.3), (0.42, 0.3)):
        parts.append(torus("SwayCage", (x, y, z + level), r, 0.012, wire, 12, 3, axis="Z"))
    parts.append(cyl("SwayCage", (x, y, z - 0.012), 0.3, 0.02, perch, verts=12))
    for k in range(10):
        a = k * 2 * math.pi / 10
        px, py = x + math.cos(a) * 0.3, y + math.sin(a) * 0.3
        parts.append(tube("SwayCage", smooth_path([(px, py, z), (px, py, z + 0.42), (x + math.cos(a) * 0.1, y + math.sin(a) * 0.1, z + 0.6)], 2), 0.006, wire, sides=3))
    parts.append(pole("SwayCage", (x - 0.26, y, z + 0.2), (x + 0.26, y, z + 0.2), 0.012, perch, verts=4))
    for part in parts:
        set_wind(part, wind)


def herb_bunches(y, z, xs, dry, tie):
    """Сушёные пучки на затяжке: чуть качаются. Живое у верхнего края кадров."""
    for k, x in enumerate(xs):
        phase = (0.13 + k * 0.23) % 1.0
        top = (x, y - 0.1, z)

        def wind(point, hinge=Vector(top), phase=phase):
            return (min((hinge - point).length / 0.7, 1.0) ** 1.2 * 0.7, phase, 0.0)

        set_wind(pole("SwayHerb", top, (x, y - 0.1, z - 0.2), 0.008, tie, verts=3), wind)
        for s in range(5):
            a = s * 1.3
            spread = 0.05 + s * 0.015
            set_wind(tube("SwayHerb", [(x, y - 0.1, z - 0.2), (x + math.cos(a) * spread, y - 0.1 + math.sin(a) * spread, z - 0.42), (x + math.cos(a) * spread * 1.6, y - 0.1 + math.sin(a) * spread * 1.6, z - 0.62 - rng.rand() * 0.1)], 0.018, dry[s % len(dry)], sides=3), wind)


def hat_boxes(x, y, z, palette, lid):
    """Шляпные коробки горкой: круглые, полосатые, с лентой."""
    for k, (r, h, dx) in enumerate(((0.42, 0.34, 0.0), (0.34, 0.28, 0.05), (0.26, 0.22, -0.03))):
        lathe("GearHat", (x + dx, y, z), [(r, 0), (r, h - 0.05), (r + 0.015, h - 0.05), (r + 0.015, h), (0, h)], palette[k % len(palette)], segments=14)
        torus("GearHat", (x + dx, y, z + h * 0.45), r + 0.005, 0.009, lid, 14, 3, axis="Z")
        z += h
    return z


def framed_picture(x, y, width, height, lean, turn, frame, colors):
    """Старая картина, прислонена к стене: рама и плоский пейзаж двумя-тремя пятнами."""
    ca, sa = math.cos(turn), math.sin(turn)
    root = (x, y, FLOOR)

    def piece(dx, dz, w, h, depth, material, name="GearPicture"):
        obj = box(name, (root[0] + ca * dx + sa * (-depth), root[1] + sa * dx - ca * (-depth), root[2] + dz), (w, 0.03, h), material, bevel=0)
        obj.rotation_euler = (lean, 0, turn)
        return obj

    back = piece(0, 0, width, height, 0.0, frame)
    sky_h, land_h = height * 0.5, height * 0.34
    lift = math.cos(lean)
    piece(0, 0.07 * lift, width - 0.14, land_h, 0.018, colors[0])
    piece(0, (0.07 + land_h) * lift, width - 0.14, sky_h, 0.018 - (land_h * math.sin(lean)), colors[1])
    return back


def upturned_chair(x, y, z, turn, wood, seat):
    """Стул кверху ножками на ящиках: сразу видно, что здесь склад старья. Спинка с планками, ножки точёные."""
    ca, sa = math.cos(turn), math.sin(turn)
    w = 0.5
    box("GearChair", (x, y, z + 0.5), (w, w, 0.05), seat, bevel=0.01).rotation_euler = (0, 0, turn)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        a = x + ca * dx * w * 0.42 - sa * dy * w * 0.42
        b = y + sa * dx * w * 0.42 + ca * dy * w * 0.42
        pole("GearChair", (a, b, z + 0.53), (a + ca * dx * 0.05, b + sa * dx * 0.05, z + 1.0), 0.025, wood, verts=5)
        if dy > 0:
            pole("GearChair", (a, b, z + 0.5), (a, b, z), 0.025, wood, verts=5)
    bx, by = x - sa * w * 0.42, y + ca * w * 0.42
    box("GearChair", (bx, by, z), (w * 0.95, 0.035, 0.22), wood, bevel=0.006).rotation_euler = (0, 0, turn)


def skis_and_sled(x, y, wood, strap, rope):
    """Старые деревянные лыжи: плоские, с загнутыми носами и креплениями, палки с кольцами. Прислонены к стене."""
    ski_mat = mat("AtticSki", "#b5793f", 0.7)
    for dx in (-0.16, 0.16):
        path = smooth_path([(x + dx, y + 0.34, FLOOR), (x + dx, y + 0.2, FLOOR + 0.9), (x + dx, y + 0.06, FLOOR + 1.75), (x + dx, y - 0.1, FLOOR + 2.02), (x + dx, y - 0.2, FLOOR + 2.06)], 3)
        rows = [[Vector(q) + Vector((-0.055, 0, 0)), Vector(q) + Vector((0.055, 0, 0))] for q in path]
        strip("GearSki", [[tuple(v) for v in row] for row in rows], ski_mat, facing=(0, -1, 0.2)).modifiers.new("s", "SOLIDIFY").thickness = 0.02
        for t, h in ((0.75, 0.06), (0.86, 0.04)):
            box("GearSki", (x + dx, y + 0.34 - t * 0.17, FLOOR + t), (0.13, 0.028, h), strap, bevel=0)
    for dx, tilt in ((-0.42, 0.1), (0.42, -0.06)):
        top = (x + dx + tilt, y + 0.08, FLOOR + 1.62)
        foot = (x + dx, y + 0.4, FLOOR)
        pole("GearSki", foot, top, 0.014, wood, verts=4)
        torus("GearSki", (x + dx + tilt * 0.08, y + 0.375, FLOOR + 0.14), 0.09, 0.009, strap, 10, 3, axis="Z")
        tube("GearSki", [top, (top[0], top[1] - 0.02, top[2] + 0.14)], 0.024, strap, sides=4)


def globe(x, y, z, ocean, land, stand):
    """Глобус на подставке: синее пятно среди коричневого."""
    lathe("GearGlobe", (x, y, z), [(0.15, 0), (0.12, 0.03), (0.03, 0.08), (0.03, 0.16)], stand, segments=10)
    ball = blob("GearGlobe", (x, y, z + 0.37), (0.21, 0.21, 0.21), ocean, 2)
    ball.rotation_euler = (0.3, 0.2, 0)
    for dx, dy, dz, sx, sz in ((-0.14, -0.14, 0.06, 0.1, 0.09), (0.05, -0.19, -0.03, 0.07, 0.11), (0.14, -0.13, 0.08, 0.06, 0.05)):
        blob("GearGlobe", (x + dx, y + dy, z + 0.37 + dz), (sx, 0.035, sz), land, 1)
    arc = [(x + math.cos(a) * 0.24, y, z + 0.37 + math.sin(a) * 0.24) for a in (math.radians(-70 + 28 * k) for k in range(9))]
    tube("GearGlobe", arc, 0.009, stand, sides=3)


def teddy(x, y, z, turn, fur, snout, eye, ribbon):
    """Плюшевый медведь сидит на чемоданах: забытый, с бантом. Самая тёплая мелочь сцены."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, dz):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, z + dz)

    blob("GearTeddy", at(0, 0, 0.17), (0.15, 0.13, 0.17), fur, 2)
    blob("GearTeddy", at(0, -0.02, 0.41), (0.125, 0.115, 0.115), fur, 2)
    blob("GearTeddy", at(0, -0.115, 0.385), (0.05, 0.04, 0.042), snout, 1)
    blob("GearTeddy", at(0, -0.15, 0.4), (0.017, 0.012, 0.014), eye, 1)
    for sign in (-1, 1):
        blob("GearTeddy", at(sign * 0.1, 0, 0.51), (0.05, 0.03, 0.05), fur, 1)
        blob("GearTeddy", at(sign * 0.05, -0.105, 0.44), (0.014, 0.01, 0.014), eye, 1)
        blob("GearTeddy", at(sign * 0.15, -0.06, 0.2), (0.05, 0.05, 0.11), fur, 1)
        blob("GearTeddy", at(sign * 0.09, -0.17, 0.06), (0.06, 0.1, 0.06), fur, 1)
        blob("GearTeddy", at(sign * 0.07, -0.13, 0.3), (0.055, 0.02, 0.035), ribbon, 1)


def old_lantern(x, y, iron, glass):
    lathe("GearLamp", (x, y, FLOOR), [(0.12, 0), (0.1, 0.03), (0.07, 0.05), (0.07, 0.3), (0.1, 0.32), (0.03, 0.38)], iron, segments=8)
    torus("GearLamp", (x, y, FLOOR + 0.42), 0.07, 0.008, iron, 8, 3, axis="X")


def small_barrel(x, y, material):
    lathe("GearBarrel", (x, y, FLOOR), [(0.26, 0), (0.33, 0.28), (0.33, 0.46), (0.26, 0.74), (0.0, 0.74)], material, segments=12)


def attic():
    boards = image_mat("AtticBoards", bc.plank_image(), rough=0.85)
    barrels = image_mat("AtticBarrel", barrel_image(), rough=0.8)
    rafter = mat("AtticRafter", "#5a3a20", 0.85)
    wood = mat("AtticWood", "#8a5a30", 0.8)
    dark = mat("AtticDark", "#3e2614", 0.85)
    iron = mat("AtticIron", "#34353a", 0.5)
    brass = mat("AtticBrass", "#c9982f", 0.45)
    cream = mat("AtticCream", "#e8dcbc", 0.8)
    sheet = mat("AtticSheet", "#b8b09c", 0.95)
    glow = mat("AtticGlow", "#dff0f7", 1, emit=2.4)
    rope = mat("AtticRope", "#b39865", 0.95)
    leather_a = mat("AtticLeatherA", "#8c4a24", 0.7)
    leather_b = mat("AtticLeatherB", "#3f6450", 0.7)
    leather_c = mat("AtticLeatherC", "#8a2f2a", 0.7)
    strap = mat("AtticStrap", "#2e1e12", 0.8)

    ground("Floor", -EAVE_X - 0.1, EAVE_X + 0.1, FRONT - 3, BACK + 0.1, floor_image())
    room(boards, rafter, dark, glow, iron)

    # За столом в центре: сундук с котом, стопки книг, граммофон на ящике — низко, в кадре партии.
    old_chest(-0.65, 4.45, math.radians(4), wood, iron, brass)
    book_stack(0.65, 4.55, FLOOR, 0.15, 6, (leather_c, leather_b, cream, leather_a, strap, leather_b))
    box("GearGramo", (1.6, 4.65, FLOOR), (0.85, 0.75, 0.55), dark, bevel=0.015).rotation_euler = (0, 0, -0.1)
    gramophone(1.6, 4.65, FLOOR + 0.55, -0.1, wood, brass, strap)

    # Слева за столом: часы, манекен, чемоданы горкой.
    grandfather_clock(-5.35, 4.65, math.radians(10), wood, dark, brass, cream, iron)
    dress_form(-3.85, 4.75, 0.5, mat("AtticForm", "#a56a55", 0.9), wood, mat("AtticMeasure", "#e8c64a", 0.7))
    suitcase(-5.95, 2.7, FLOOR, 1.1, 0.7, 0.34, 0.2, leather_a, strap, brass)
    suitcase(-5.85, 2.75, FLOOR + 0.34, 0.92, 0.6, 0.3, -0.12, leather_b, strap, brass)
    suitcase(-5.95, 2.7, FLOOR + 0.64, 0.68, 0.46, 0.25, 0.28, leather_c, strap, brass)

    # Справа: лошадка-качалка, зеркало под простынёй, бочонок.
    rocking_horse(4.2, 2.65, math.radians(205), mat("AtticHorse", "#d8c6a0", 0.75), mat("AtticSaddle", "#b83a2c", 0.6), mat("AtticMane", "#4d3322", 0.9), wood, strap)
    covered_mirror(5.2, 4.75, math.radians(-14), sheet, dark, wood)
    small_barrel(3.3, 4.85, barrels)

    # Гора хлама. Слева у часов: коробки и картины, на чемоданах медведь.
    box("GearCrate", (-4.75, 2.85, FLOOR), (0.62, 0.62, 0.42), wood, bevel=0.012).rotation_euler = (0, 0, 0.5)
    hat_boxes(-4.75, 2.85, FLOOR + 0.42, (mat("AtticHatA", "#a8504a", 0.8), mat("AtticHatB", "#5f8087", 0.8), mat("AtticHatC", "#c9a664", 0.8)), cream)
    teddy(-5.95, 2.66, FLOOR + 0.89, math.radians(28), mat("AtticTeddy", "#b07a43", 0.95), cream, strap, mat("AtticRibbon", "#b83a2c", 0.6))
    framed_picture(-7.0, 1.2, 1.1, 1.35, -0.2, math.radians(-82), mat("AtticFrame", "#a57b37", 0.5), (mat("AtticPicLand", "#7d8f5c", 0.9), mat("AtticPicSky", "#c0c8ba", 0.9)))
    framed_picture(-6.9, 0.0, 0.8, 1.0, -0.22, math.radians(-96), dark, (mat("AtticPicSea", "#3f6f83", 0.9), mat("AtticPicDusk", "#d29a68", 0.9)))
    globe(-4.75, 4.1 - 0.4, FLOOR, mat("AtticOcean", "#3f7d9f", 0.45), mat("AtticLand", "#c4b27a", 0.7), brass)

    # Справа у стены: ящики до потолка с перевёрнутым стулом, лыжи.
    box("GearCrate", (6.3, 4.55, FLOOR), (1.15, 0.95, 0.8), wood, bevel=0.015).rotation_euler = (0, 0, -0.1)
    box("GearCrate", (6.2, 4.6, FLOOR + 0.8), (0.9, 0.8, 0.62), dark, bevel=0.015).rotation_euler = (0, 0, 0.2)
    upturned_chair(6.25, 4.55, FLOOR + 1.42, 0.35, wood, mat("AtticSeat", "#7a3a30", 0.85))
    skis_and_sled(7.45, 3.1, wood, strap, rope)
    old_lantern(2.55, 3.75, iron, glow)

    # Передний план: свёрнутый ковёр слева, стопка книг и ящик справа.
    rolled_rug(-5.65, -1.6, math.radians(78), mat("AtticRugRoll", "#7c2f2d", 0.95), mat("AtticRugEdge", "#c7a254", 0.95))
    crate_x, crate_y = 5.85, -1.85
    box("GearCrate", (crate_x, crate_y, FLOOR), (1.05, 0.85, 0.62), wood, bevel=0.015).rotation_euler = (0, 0, 0.32)
    top = book_stack(crate_x + 0.03, crate_y - 0.04, FLOOR + 0.62, 0.5, 4, (leather_b, leather_c, strap, cream))
    lathe("GearCrate", (crate_x + 0.02, crate_y - 0.05, top), [(0.1, 0), (0.13, 0.05), (0.03, 0.12), (0.03, 0.2), (0.14, 0.34), (0.14, 0.36)], brass, segments=10)

    # Сверху: клетка и травы на затяжке.
    hanging_cage(3.05, 3.15, 2.53, iron, wood)
    herb_bunches(3.3, 2.53, (-3.3, -2.45, -1.7), (mat("AtticHerbA", "#91935a", 0.95), mat("AtticHerbB", "#a68a4e", 0.95)), rope)


def lights():
    def add(name, kind, energy, color, location, rotation=None, soft=0.3, spot=None):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = color
        if kind != "SUN":
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
    add("TableLamp", "SPOT", 380, (1.0, 0.82, 0.58), (0, 0, 3.4), down, 0.35, (math.radians(108), 0.65))

    # Луч из окна: холодноватый дневной, ложится на пол левее стола и на вещи у задней стены.
    target = Vector((-1.9, -0.8, FLOOR)) - Vector((0, BACK - 0.5, 2.2))
    add("Window", "SPOT", 1500, (0.86, 0.94, 1.0), (0, BACK - 0.5, 2.2), target.to_track_quat("-Z", "Y"), 0.5, (math.radians(46), 0.45))
    add("WindowFill", "POINT", 130, (0.78, 0.88, 1.0), (0, BACK - 1.0, 2.0), None, 1.4)

    # Заполняющий тёплый: края не проваливаются, дерево читается.
    for x, y, z, energy in ((-4.6, 2.8, 1.8, 190), (4.6, 2.8, 1.8, 190), (0, -4.6, 2.4, 300), (-5, -3.2, 1.8, 110), (5, -3.2, 1.8, 110), (0, 3.6, 1.0, 90)):
        add("Fill", "POINT", energy, (1.0, 0.8, 0.58), (x, y, z), None, 1.6)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.2, 0.18, 0.16, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.34
    bpy.context.scene.world = world


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        kit.UV_ANGLE = 66
        kit.UV_MARGIN = 0.006
        texture_groups = (
            ("Frame", 1024, lambda name: name.startswith(("Window", "Rafter", "Beam"))),
            ("Gear", 1024, lambda name: name.startswith("Gear")),
            ("Rig", 1024, lambda name: name.startswith("Sway")),
        )

        def build():
            attic()
            bc.matte()

        views = (("cherdak_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("cherdak_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42), ("cherdak_igra_sopernik.png", (0, 1.55, -1.4), (0, 0.72, 2.9), 45))
        folder = argv[argv.index("--bake") + 1]
        os.makedirs(folder, exist_ok=True)
        kit.bake_location("Attic", "Room_Attic.fbx", build, lights, views, samples, folder, groups=texture_groups)
        return

    folder = argv[argv.index("--render") + 1]
    os.makedirs(folder, exist_ok=True)
    attic()
    bc.matte()
    zg.report_triangles()
    # Кот для снимка: в игре его добавляет генератор.
    armature, _ = life.cat()
    armature.location = (-0.65, 4.45, FLOOR + 0.8)
    kit.table()
    lights()
    kit.render(os.path.join(folder, "cherdak_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "cherdak_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)
    kit.render(os.path.join(folder, "cherdak_magazin.png"), (5.2, 3.3, -8.4), (0, -1.3, 1.2), 50, samples)
    kit.render(os.path.join(folder, "cherdak_sopernik.png"), (0, 1.55, -1.4), (0, 0.72, 2.9), 45, samples)


if __name__ == "__main__":
    main()
