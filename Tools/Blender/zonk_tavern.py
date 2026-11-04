# -*- coding: utf-8 -*-
"""
Локация «Таверна "Пьяный якорь"». Свет запекается в текстуры, как у «Родного дома»; живые части добавляет ZonkSetup.RoomTavern.

Морская таверна: очаг с котлом и якорем слева, стойка с бочками и бутылками справа, спасательный круг и штурвал на стене.
Посетители — такие же тени с жёлтыми глазами, как соперники: сидят у стойки и смотрят на игру.
Середина задней стены спокойная и тёмная: там сидит соперник. Общие детали берутся из zonk_room.py.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_tavern.py -- --render <папка> [--samples 48]
    blender --background --factory-startup --python Tools/Blender/zonk_tavern.py -- --bake <папка превью> [--samples 64]
Превью: <папка>/mesto_taverna_partiya.png, mesto_taverna_menu.png. Запекание: Art/Models/Environments/Room_Tavern.fbx, Art/Textures/Environments/Tavern_*.png.
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, пол на -1.6.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_room as kit  # noqa: E402

from zonk_room import FLOOR, blob, box, cyl, image_mat, link, mat  # noqa: E402

CEILING = 4.7
BACK = 5.0
FRONT = -9.5
SIDE = 6.8

rng = np.random.RandomState(23)


def lathe(name, loc, profile, material, segments=12, smooth=True):
    """Тело вращения: profile — (радиус, высота) снизу вверх. Развёртка: вокруг по u, по высоте по v."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    rings = []
    for radius, z in profile:
        ring = []
        for s in range(segments):
            angle = 2 * math.pi * s / segments
            ring.append(bm.verts.new((loc[0] + math.cos(angle) * radius, loc[1] + math.sin(angle) * radius, loc[2] + z)))
        rings.append(ring)
    bottom, top = profile[0][1], profile[-1][1]
    span = max(top - bottom, 0.001)
    for r in range(len(rings) - 1):
        for s in range(segments):
            n = (s + 1) % segments
            face = bm.faces.new((rings[r][s], rings[r][n], rings[r + 1][n], rings[r + 1][s]))
            u0, u1 = s / segments, (s + 1) / segments
            v0, v1 = (profile[r][1] - bottom) / span, (profile[r + 1][1] - bottom) / span
            for loop, uv in zip(face.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                loop[uv_layer].uv = uv
    bm.faces.new(rings[-1])
    bm.faces.new(list(reversed(rings[0])))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    if smooth:
        for polygon in mesh.polygons:
            polygon.use_smooth = True
    return link(bpy.data.objects.new(name, mesh), material)


def world_uv(obj, tile):
    """Развёртка по мировым осям: рисунок на стенах одного масштаба и не тянется полосами."""
    mesh = obj.data
    layer = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        n = polygon.normal
        for index in polygon.loop_indices:
            co = mesh.vertices[mesh.loops[index].vertex_index].co
            if abs(n.z) >= abs(n.x) and abs(n.z) >= abs(n.y):
                layer.data[index].uv = (co.x * tile, co.y * tile)
            elif abs(n.y) >= abs(n.x):
                layer.data[index].uv = (co.x * tile, co.z * tile)
            else:
                layer.data[index].uv = (co.y * tile, co.z * tile)
    return obj


def plaster_image():
    """Старая штукатурка: ровные пятна двух тонов и участки голой кладки. Плитка без швов."""
    size = 512
    y, x = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    blotch = np.sin(x * 2 + np.sin(y * 3) * 1.3) + np.sin(y * 3 + np.sin(x * 2) * 1.6) + 0.5 * np.sin((x + y) * 5)
    base = np.array([0.40, 0.31, 0.21])
    image = np.zeros((size, size, 3)) + base
    image[blotch > 0.7] = base * 0.88
    image[blotch < -1.0] = base * 1.1
    local = np.random.RandomState(9)
    for _ in range(3):
        cx, cy = local.randint(60, size - 130), local.randint(60, size - 90)
        for row in range(3):
            for col_index in range(3):
                if local.rand() < 0.3:
                    continue
                bx = cx + col_index * 40 + (row % 2) * 18
                by = cy + row * 22
                image[by:by + 17, bx:bx + 35] = (0.27, 0.17, 0.12)
    return image


def plate(name, points, y, depth, material):
    """Плоская деталь по контуру (x, z), толщина вдоль y."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    front = [bm.verts.new((px, y - depth, pz)) for px, pz in points]
    rear = [bm.verts.new((px, y, pz)) for px, pz in points]
    bm.faces.new(front)
    bm.faces.new(list(reversed(rear)))
    for i in range(len(points)):
        n = (i + 1) % len(points)
        bm.faces.new((front[i], rear[i], rear[n], front[n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    return link(bpy.data.objects.new(name, mesh), material)


def barrel_image():
    """Клёпки и обручи рисунком: геометрия бочки остаётся простой."""
    size = 256
    image = np.zeros((size, size, 3))
    local = np.random.RandomState(5)
    staves = 12
    width = size // staves
    for i in range(staves):
        tone = 0.40 + local.rand() * 0.12
        image[:, i * width:(i + 1) * width] = (tone, tone * 0.62, tone * 0.33)
        image[:, i * width:i * width + 3] = (0.08, 0.05, 0.03)
    for v in (0.16, 0.38, 0.62, 0.84):
        row = int(v * size)
        image[row - 7:row + 7] = (0.17, 0.15, 0.14)
        image[row - 7:row - 4] = (0.34, 0.31, 0.27)
    return image


def barrel(loc, height=1.15, radius=0.46, material=None):
    return lathe("Barrel", loc, [(radius * 0.8, 0), (radius * 0.97, height * 0.24), (radius, height * 0.5), (radius * 0.97, height * 0.76), (radius * 0.8, height)], material)


def barrel_side(loc, length, radius, material):
    """Бочка лёжа, ось вдоль x: на стеллаже у стойки."""
    obj = barrel((0, 0, 0), length, radius, material)
    obj.location = (loc[0] - length / 2, loc[1], loc[2])
    obj.rotation_euler = (0, math.pi / 2, 0)
    return obj


def bar(name, p0, p1, width, depth, material):
    """Брусок между двумя точками в плоскости стены (x, z)."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]
    length = math.hypot(dx, dz)
    obj = box(name, (0, 0, -length / 2), (width, depth, length), material, bevel=0)
    obj.location = ((p0[0] + p1[0]) / 2, p0[2], (p0[1] + p1[1]) / 2)
    obj.rotation_euler = (0, math.atan2(dx, dz), 0)
    return obj


def torus(name, loc, major, minor, material, segments=20, sides=6, axis="Y"):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=sides, major_radius=major, minor_radius=minor, location=loc)
    obj = bpy.context.object
    obj.name = name
    if axis == "Y":
        obj.rotation_euler = (math.pi / 2, 0, 0)
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return obj


def anchor(x, y, z, scale, iron, rope):
    """Якорь на стене: стержень, шток, кольцо, лапы дугой."""
    bar("Anchor", (x, z + 1.05 * scale, y), (x, z - 0.95 * scale, y), 0.17 * scale, 0.1, iron)
    bar("Anchor", (x - 0.5 * scale, z + 0.72 * scale, y), (x + 0.5 * scale, z + 0.72 * scale, y), 0.12 * scale, 0.1, iron)
    torus("Anchor", (x, y, z + 1.17 * scale), 0.15 * scale, 0.035 * scale, iron, 12, 5)
    previous = {-1: (x, z - 0.95 * scale), 1: (x, z - 0.95 * scale)}
    for sign in (-1, 1):
        for i in range(1, 6):
            angle = math.radians(i * 16)
            point = (x + sign * math.sin(angle) * 0.82 * scale, z - 0.95 * scale + (1 - math.cos(angle)) * 0.82 * scale)
            bar("Anchor", previous[sign] + (y,), point + (y,), 0.15 * scale, 0.1, iron)
            previous[sign] = point
        tip = previous[sign]
        plate("Anchor", [(tip[0] + sign * 0.2 * scale, tip[1] - 0.1 * scale), (tip[0] + sign * 0.14 * scale, tip[1] + 0.5 * scale), (tip[0] - sign * 0.3 * scale, tip[1] - 0.02 * scale)], y, 0.1, iron)
    # Канат обвивает стержень: несколько витков.
    for i in range(3):
        torus("AnchorRope", (x, y - 0.03, z + (0.35 - i * 0.2) * scale), 0.11 * scale, 0.03, rope, 8, 3, axis="Z")


def wheel(x, y, z, radius, wood, brass):
    """Штурвал на стене."""
    torus("Wheel", (x, y, z), radius, radius * 0.09, wood, 18, 5)
    for i in range(4):
        angle = i * math.pi / 4
        dx, dz = math.cos(angle) * radius, math.sin(angle) * radius
        bar("Wheel", (x - dx, z - dz, y), (x + dx, z + dz, y), radius * 0.1, 0.06, wood)
    cyl("WheelHub", (x, y - 0.04, z), radius * 0.2, 0.1, brass, axis="Y", verts=10)


def buoy(x, y, z, rope, iron):
    """Спасательный круг: белый с красными полосами, верёвка вокруг, висит на гвозде."""
    torus("Buoy", (x, y, z), 0.5, 0.13, mat("TavBuoyWhite", "#e8e0cc", 0.6), 24, 6)
    obj = bpy.context.object
    obj.data.materials.append(mat("TavBuoyRed", "#b3261f", 0.6))
    for polygon in obj.data.polygons:
        angle = math.atan2(polygon.center.y, polygon.center.x) + math.pi / 8
        polygon.material_index = int(math.floor(angle / (math.pi / 4))) % 2
    torus("BuoyRope", (x, y - 0.02, z), 0.66, 0.018, rope, 16, 3)
    cyl("BuoyNail", (x, y + 0.02, z + 0.68), 0.03, 0.14, iron, axis="Y", verts=6)


def lantern(x, y, z, iron, chain=True):
    """Фонарь: каркас и светящееся стекло. Подвешен к потолку."""
    glow = mat("LanternGlow", "#ff9a3a", 1, emit=5)
    cyl("LanternGlass", (x, y, z), 0.13, 0.32, glow, verts=8)
    cyl("LanternCap", (x, y, z + 0.2), 0.18, 0.08, iron, verts=8)
    cyl("LanternBase", (x, y, z - 0.19), 0.17, 0.06, iron, verts=8)
    if chain:
        box("LanternChain", (x, y, z + 0.24), (0.03, 0.03, CEILING - z - 0.5), iron, bevel=0)


def mug(x, y, z, wood, foam=None):
    cyl("Mug", (x, y, z + 0.1), 0.085, 0.2, wood, verts=8)
    box("MugHandle", (x + 0.11, y, z + 0.05), (0.04, 0.03, 0.12), wood, bevel=0)
    if foam:
        cyl("MugFoam", (x, y, z + 0.21), 0.09, 0.035, foam, verts=8)


def bottle(x, y, z, material):
    lathe("Bottle", (x, y, z), [(0.07, 0), (0.07, 0.2), (0.025, 0.31), (0.025, 0.4)], material, segments=6)


def patron(x, y, seat, robe, hat=None):
    """Посетитель-тень: тело, голова, жёлтые глаза смотрят на стол."""
    lathe("Patron", (x, y, seat), [(0.5, 0), (0.46, 0.3), (0.38, 0.62), (0.42, 0.8), (0.34, 0.95), (0.16, 1.05)], robe, segments=10)
    head_z = seat + 1.2
    blob("Patron", (x, y, head_z), (0.23, 0.23, 0.25), robe, 2)
    eye = mat("PatronEye", "#ffd23c", 1, emit=14)
    direction = np.array([-x, -2.0 - y])
    direction = direction / np.linalg.norm(direction)
    side = np.array([direction[1], -direction[0]])
    for sign in (-1, 1):
        ex = x + direction[0] * 0.22 + side[0] * sign * 0.09
        ey = y + direction[1] * 0.22 + side[1] * sign * 0.09
        blob("PatronEye", (ex, ey, head_z + 0.03), (0.05, 0.05, 0.035), eye, 1)
    if hat:
        cyl("PatronHat", (x, y, head_z + 0.2), 0.37, 0.04, hat, verts=10)
        lathe("PatronHat", (x, y, head_z + 0.22), [(0.22, 0), (0.17, 0.2)], hat, segments=8)


def tavern():
    plaster = image_mat("TavPlaster", plaster_image(), rough=0.95)
    timber = mat("TavTimber", "#3a2212", 0.7)
    timber_light = mat("TavTimberLight", "#5a3519", 0.6)
    iron = mat("TavIron", "#1b1b1f", 0.7)
    brass = mat("TavBrass", "#c29a44", 0.3)
    rope = mat("TavRope", "#a98a55", 0.9)
    stone = image_mat("TavStone", kit.bricks())
    barrels = image_mat("TavBarrel", barrel_image(), rough=0.6)
    mug_wood = mat("TavMug", "#7a4c26", 0.5)
    foam = mat("TavFoam", "#f3ead2", 0.8)

    # Пол: тёмные широкие доски. Стены: камень снизу, штукатурка и балки сверху.
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-SIDE, FRONT, FLOOR), (SIDE, FRONT, FLOOR), (SIDE, BACK, FLOOR), (-SIDE, BACK, FLOOR)], [], [(0, 1, 2, 3)])
    floor.update()
    layer = floor.uv_layers.new(name="UVMap")
    for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        layer.data[k].uv = uv
    link(bpy.data.objects.new("Floor", floor), image_mat("TavPlanks", np.clip(kit.planks() * np.array([0.8, 0.72, 0.66]), 0, 1), rough=0.6))

    world_uv(box("Back", (0, BACK + 0.15, FLOOR), (SIDE * 2 + 0.6, 0.3, CEILING - FLOOR), plaster, bevel=0), 0.28)
    world_uv(box("Left", (-SIDE - 0.15, (BACK + FRONT) / 2, FLOOR), (0.3, BACK - FRONT, CEILING - FLOOR), plaster, bevel=0), 0.28)
    world_uv(box("Right", (SIDE + 0.15, (BACK + FRONT) / 2, FLOOR), (0.3, BACK - FRONT, CEILING - FLOOR), plaster, bevel=0), 0.28)
    world_uv(box("Front", (0, FRONT - 0.15, FLOOR), (SIDE * 2 + 0.6, 0.3, CEILING - FLOOR), plaster, bevel=0), 0.28)
    box("Ceiling", (0, (BACK + FRONT) / 2, CEILING), (SIDE * 2 + 0.6, BACK - FRONT + 0.6, 0.2), mat("TavCeil", "#2e1c10", 0.9), bevel=0)

    # Каменный низ: задняя и боковые стены.
    wall_low = 1.5
    world_uv(box("Wains", (0, BACK - 0.06, FLOOR), (SIDE * 2, 0.12, wall_low), stone, bevel=0), 0.55)
    world_uv(box("Wains", (-SIDE + 0.06, (BACK + FRONT) / 2, FLOOR), (0.12, BACK - FRONT, wall_low), stone, bevel=0), 0.55)
    world_uv(box("Wains", (SIDE - 0.06, (BACK + FRONT) / 2, FLOOR), (0.12, BACK - FRONT, wall_low), stone, bevel=0), 0.55)
    box("Wains", (0, BACK - 0.1, FLOOR + wall_low - 0.08), (SIDE * 2, 0.2, 0.16), timber, bevel=0.02)
    box("Wains", (-SIDE + 0.1, (BACK + FRONT) / 2, FLOOR + wall_low - 0.08), (0.2, BACK - FRONT, 0.16), timber, bevel=0.02)
    box("Wains", (SIDE - 0.1, (BACK + FRONT) / 2, FLOOR + wall_low - 0.08), (0.2, BACK - FRONT, 0.16), timber, bevel=0.02)

    # Балки: стойки стены и потолок. Неровные расстояния и один косой раскос, чтобы не было похоже на сетку.
    for x in (-6.0, -1.5, 1.6, 6.0):
        box("CeilBeam", (x, BACK - 0.1, FLOOR + wall_low), (0.26, 0.2, CEILING - FLOOR - wall_low), timber, bevel=0.02)
    for y in (-6.0, -2.6, 0.6, 3.4):
        box("CeilBeam", (0, y, CEILING - 0.42), (SIDE * 2, 0.36, 0.42), timber, bevel=0.03)
    for y in (-3.0, 0.2, 2.8):
        box("CeilBeam", (-SIDE + 0.1, y, FLOOR + wall_low), (0.2, 0.24, CEILING - FLOOR - wall_low), timber, bevel=0.02)
        box("CeilBeam", (SIDE - 0.1, y, FLOOR + wall_low), (0.2, 0.24, CEILING - FLOOR - wall_low), timber, bevel=0.02)

    # Середина стены за соперником: большой якорь. Камера партии видит стену до высоты около 1: лапы якоря видны по бокам от соперника.
    anchor(0.0, BACK - 0.14, 1.35, 1.1, iron, rope)

    # Левее якоря — спасательный круг на гвозде, выше полки очага: ни во что не заходит. Правее — штурвал.
    buoy(-2.35, BACK - 0.14, 1.45, rope, iron)
    wheel(2.4, BACK - 0.12, 1.3, 0.72, timber_light, brass)

    # Бра на стойках стены: подсвечивают якорь и дают фон, на котором читается тёмная фигура соперника.
    for x in (-1.5, 1.6):
        box("Sconce", (x, BACK - 0.3, 0.68), (0.2, 0.22, 0.05), iron, bevel=0)
        cyl("SconceCandle", (x, BACK - 0.3, 0.8), 0.04, 0.2, mat("TavWax", "#ece1c1", 0.6), verts=6)
        blob("SconceFlame", (x, BACK - 0.3, 0.96), (0.03, 0.03, 0.07), mat("TavFlame", "#ffb54a", 1, emit=9), 1)

    # Очаг слева: каменный, с полкой, котлом на цепи и дровами. Огонь — те же кадры, что в интерфейсе.
    fx, fy = -4.6, BACK - 0.55
    world_uv(box("JambL", (fx - 1.25, fy, FLOOR), (0.7, 1.05, 2.15), stone, bevel=0.05), 0.55)
    world_uv(box("JambR", (fx + 1.25, fy, FLOOR), (0.7, 1.05, 2.15), stone, bevel=0.05), 0.55)
    world_uv(box("Lintel", (fx, fy, FLOOR + 1.8), (1.9, 1.05, 0.55), stone, bevel=0.05), 0.55)
    box("FireBack", (fx, fy + 0.45, FLOOR), (1.9, 0.1, 1.85), mat("TavSoot", "#100b09", 0.95), bevel=0)
    box("Hearth", (fx, fy - 0.45, FLOOR), (3.7, 1.8, 0.1), mat("TavHearth", "#3e3733", 0.85))
    box("Mantel", (fx, fy - 0.12, FLOOR + 2.15), (3.4, 1.3, 0.2), timber_light, bevel=0.04)
    world_uv(box("Chimney", (fx, fy + 0.15, FLOOR + 2.35), (2.5, 0.7, CEILING - FLOOR - 2.35), stone, bevel=0.04), 0.55)
    bark = mat("TavBark", "#2b1a10", 0.9)
    cyl("Log", (fx - 0.2, fy - 0.1, FLOOR + 0.22), 0.14, 1.25, bark, axis="X", verts=7)
    cyl("Log", (fx + 0.2, fy + 0.08, FLOOR + 0.22), 0.13, 1.2, bark, axis="X", verts=7)
    cyl("Log", (fx, fy, FLOOR + 0.44), 0.12, 1.05, bark, axis="X", verts=7)
    kit.flames(fx, fy - 0.12)
    # Котёл над огнём.
    lathe("Cauldron", (fx, fy - 0.1, FLOOR + 1.2), [(0.12, 0), (0.28, 0.08), (0.31, 0.2), (0.25, 0.33), (0.28, 0.37)], iron, segments=12)
    box("CauldronChain", (fx, fy - 0.1, FLOOR + 1.55), (0.03, 0.03, 0.3), iron, bevel=0)
    # На полке: бутылка, кружки, рыба на доске.
    bottle(fx - 1.1, fy - 0.2, FLOOR + 2.35, mat("TavBottleGreen", "#2c6a48", 0.25))
    mug(fx + 1.05, fy - 0.25, FLOOR + 2.35, mug_wood)
    mug(fx + 0.7, fy - 0.15, FLOOR + 2.35, mug_wood)
    box("FishBoard", (fx - 0.3, fy - 0.22, FLOOR + 2.35), (0.7, 0.05, 0.42), timber_light, bevel=0.01)
    blob("Fish", (fx - 0.3, fy - 0.26, FLOOR + 2.56), (0.26, 0.03, 0.09), mat("TavFish", "#7f9aa3", 0.4), 2)

    # Стойка справа: столешница, передняя стенка, за ней стеллаж с бочками и полки с бутылками.
    cx0, cx1, cy = 1.9, SIDE - 0.2, 3.25
    box("Counter", ((cx0 + cx1) / 2, cy, FLOOR), (cx1 - cx0, 0.72, 1.2), timber_light, bevel=0.02)
    box("Counter", ((cx0 + cx1) / 2, cy - 0.02, FLOOR + 1.2), (cx1 - cx0 + 0.25, 0.92, 0.12), timber, bevel=0.03)
    x = cx0 + 0.25
    while x < cx1:
        box("Counter", (x, cy - 0.38, FLOOR + 0.1), (0.12, 0.05, 1.0), timber, bevel=0)
        x += 0.75
    box("Shelf", (4.75, BACK - 0.22, FLOOR + 2.15), (2.9, 0.36, 0.08), timber, bevel=0.01)
    box("Shelf", (4.75, BACK - 0.22, FLOOR + 2.85), (2.9, 0.36, 0.08), timber, bevel=0.01)
    colors = [("TavBottleGreen", "#2c6a48"), ("TavBottleAmber", "#a4641e"), ("TavBottleRed", "#8c2622"), ("TavBottleBlue", "#28517a")]
    for level in (2.23, 2.93):
        bx = 3.45
        while bx < 6.0:
            name, color = colors[rng.randint(0, len(colors))]
            bottle(bx, BACK - 0.22, FLOOR + level, mat(name, color, 0.25))
            bx += 0.27 + rng.rand() * 0.25
    barrel_side((2.6, BACK - 0.55, FLOOR + 0.5), 1.0, 0.44, barrels)
    barrel_side((3.6, BACK - 0.55, FLOOR + 0.5), 1.0, 0.44, barrels)
    barrel_side((4.6, BACK - 0.55, FLOOR + 0.5), 1.0, 0.44, barrels)
    for i in range(5):
        mug(2.5 + i * 0.75 + rng.rand() * 0.25, cy - 0.15, FLOOR + 1.32, mug_wood, foam if i % 2 == 0 else None)

    # Посетители-тени на табуретах у стойки смотрят на игру.
    robe = mat("TavRobe", "#17131c", 0.95)
    hat = mat("TavHat", "#2e2233", 0.9)
    stool = mat("TavStool", "#4a2813", 0.6)
    for x, y, tricorn in ((2.75, 2.2, True), (4.55, 2.05, False)):
        cyl("Stool", (x, y, FLOOR + 0.85), 0.3, 0.07, stool, verts=10)
        cyl("Stool", (x, y, FLOOR + 0.42), 0.09, 0.8, stool, verts=6)
        patron(x, y, FLOOR + 0.88, robe, hat if tricorn else None)

    # Спереди по краям кадра: слева бочка-столик с кружками и картами, справа штабель бочек и бухта каната.
    barrel((-5.0, -0.9, FLOOR), 1.15, 0.5, barrels)
    cyl("BarrelTop", (-5.0, -0.9, FLOOR + 1.18), 0.66, 0.07, timber_light, verts=14)
    mug(-5.2, -1.0, FLOOR + 1.21, mug_wood, foam)
    mug(-4.75, -0.7, FLOOR + 1.21, mug_wood, foam)
    cyl("Stool", (-5.9, 0.2, FLOOR + 0.8), 0.3, 0.07, stool, verts=10)
    cyl("Stool", (-5.9, 0.2, FLOOR + 0.4), 0.09, 0.75, stool, verts=6)
    box("Crate", (-6.0, 1.7, FLOOR), (1.05, 1.05, 0.85), timber_light, bevel=0.03)

    barrel((5.6, -1.5, FLOOR), 1.2, 0.52, barrels)
    barrel((6.05, -0.4, FLOOR), 1.2, 0.52, barrels)
    barrel((5.8, -0.95, FLOOR + 1.2), 1.1, 0.46, barrels)
    for i in range(3):
        torus("RopeCoil", (4.25, -2.6, FLOOR + 0.07 + i * 0.12), 0.5 - i * 0.05, 0.07, rope, 10, 4, axis="Z")

    # Фонари на балках по краям: стол освещает своя лампа игрока.
    lantern(-2.6, 2.3, 2.5, iron)
    lantern(4.6, -0.4, 2.7, iron)
    lantern(-5.0, -2.2, 2.6, iron)


def lights():
    # Лампа над столом: главное тёплое пятно, как в игре.
    spot = bpy.data.lights.new("TableLamp", "SPOT")
    spot.energy = 520
    spot.color = (1.0, 0.8, 0.55)
    spot.spot_size = math.radians(105)
    spot.spot_blend = 0.65
    spot.shadow_soft_size = 0.35
    obj = bpy.data.objects.new("TableLamp", spot)
    bpy.context.collection.objects.link(obj)
    obj.location = (0, 0, 3.4)

    def point(name, energy, color, location, soft):
        data = bpy.data.lights.new(name, "POINT")
        data.energy = energy
        data.color = color
        data.shadow_soft_size = soft
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location

    # Очаг — самый сильный источник комнаты, оранжевый: слева на полу и стенах.
    point("Fire", 640, (1.0, 0.42, 0.12), (-4.6, 3.4, FLOOR + 0.85), 0.4)
    # Фонари: тёплые острова света.
    point("Lantern", 40, (1.0, 0.66, 0.3), (-2.6, 2.3, 2.3), 0.15)
    point("Lantern", 80, (1.0, 0.66, 0.3), (4.6, -0.4, 2.5), 0.15)
    point("Lantern", 40, (1.0, 0.66, 0.3), (-5.0, -2.2, 2.4), 0.15)
    for x in (-1.5, 1.6):
        point("Sconce", 42, (1.0, 0.68, 0.34), (x, BACK - 0.55, 1.0), 0.15)
    # Над стойкой: бутылки и бочки читаются.
    point("Bar", 70, (1.0, 0.72, 0.4), (4.4, 3.9, 1.4), 0.5)
    # Заполняющий: пол под столом и передний план не проваливаются в чёрное.
    for x, y, z, energy in ((0, -4.5, 2.2, 70), (3.8, -2.5, 2.6, 35), (5.0, -1.8, 1.4, 30), (0, 3.0, 2.4, 14), (0, -1.2, 0.9, 18)):
        point("Fill", energy, (1.0, 0.78, 0.55), (x, y, z), 1.6)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.09, 0.08, 0.1, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.25
    bpy.context.scene.world = world


def count_triangles():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    total = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.name.startswith("FlameQuad"):
            continue
        evaluated = obj.evaluated_get(depsgraph).to_mesh()
        evaluated.calc_loop_triangles()
        total += len(evaluated.loop_triangles)
        obj.evaluated_get(depsgraph).to_mesh_clear()
    print("[zonk_tavern] triangles", total)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 64
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        views = (("mesto_taverna_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("mesto_taverna_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42))
        kit.bake_location("Tavern", "Room_Tavern.fbx", tavern, lights, views, samples, argv[argv.index("--bake") + 1])
        return

    folder = argv[argv.index("--render") + 1]
    tavern()
    count_triangles()
    kit.table()
    lights()
    kit.render(os.path.join(folder, "mesto_taverna_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "mesto_taverna_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)


if __name__ == "__main__":
    main()
