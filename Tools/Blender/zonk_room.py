# -*- coding: utf-8 -*-
"""
Локация «Родной дом» с настоящим светом. Пока образец: рендер с камер партии и меню, в игру не выгружается.

Свет считает Cycles: камин, лампа над столом, луна через настоящий проём окна, отражённый свет и тени в углах.
В игру это пойдёт запечённой текстурой: на телефоне стоит как обычная картинка, источников света в реальном времени нет.
Настоящий стол берётся из проекта, чтобы сразу видеть, как он сидит в комнате.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_room.py -- --render <папка> [--samples 64]
Результат: <папка>/room_match.png, room_menu.png.
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, пол на -1.6.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "ZonkContent", "Art")

FLOOR = -1.6
CEILING = 4.7
BACK = 5.2
FRONT = -9.5
SIDE = 7.4

rng = np.random.RandomState(11)


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def col(value):
    return (lin(int(value[1:3], 16) / 255), lin(int(value[3:5], 16) / 255), lin(int(value[5:7], 16) / 255), 1.0)


def mat(name, color, rough=0.75, emit=0.0):
    material = bpy.data.materials.get(name)
    if material:
        return material
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = col(color)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Specular"].default_value = 0.25
    if emit > 0:
        bsdf.inputs["Emission"].default_value = col(color)
        bsdf.inputs["Emission Strength"].default_value = emit
    return material


def image_mat(name, pixels, scale=None, rough=0.8):
    """Материал с текстурой из массива sRGB (сверху вниз). scale — повтор по мировым координатам XZ (стены) или XY (пол)."""
    height, width = pixels.shape[:2]
    image = bpy.data.images.new(name, width, height, alpha=False)
    linear = np.where(pixels <= 0.04045, pixels / 12.92, ((pixels + 0.055) / 1.055) ** 2.4)
    rgba = np.dstack([linear, np.ones((height, width))])
    image.pixels.foreach_set(np.flipud(rgba).astype(np.float32).ravel())
    image.pack()
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Specular"].default_value = 0.2
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    if scale:
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = scale
        links.new(coord.outputs["Object"], mapping.inputs[0])
        links.new(mapping.outputs[0], tex.inputs[0])
    return material


def link(obj, material=None):
    bpy.context.collection.objects.link(obj)
    if material:
        obj.data.materials.append(material)
    return obj


def box(name, center, size, material, bevel=0.025):
    """Ящик со скошенными рёбрами: скос ловит свет, и предмет читается как предмет. center — низ по z."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x = v.co.x * size[0] + center[0]
        v.co.y = v.co.y * size[1] + center[1]
        v.co.z = (v.co.z + 0.5) * size[2] + center[2]
    bm.to_mesh(mesh)
    bm.free()
    obj = link(bpy.data.objects.new(name, mesh), material)
    if bevel > 0:
        mod = obj.modifiers.new("b", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = "ANGLE"
    return obj


def cyl(name, loc, radius, depth, material, axis="Z", verts=24, smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    if axis == "X":
        obj.rotation_euler = (0, math.pi / 2, 0)
    elif axis == "Y":
        obj.rotation_euler = (math.pi / 2, 0, 0)
    obj.data.materials.append(material)
    if smooth:
        bpy.ops.object.shade_smooth()
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(45)
    return obj


def blob(name, loc, scale, material, subdiv=3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=1, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return obj


def cushion(name, center, size, material, puff=0.09):
    """Подушка: скруглённый объём для мебели."""
    obj = box(name, center, size, material, bevel=min(size) * 0.3)
    obj.modifiers[0].segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def curtain(name, x_from, x_to, y, z_low, z_high, material, folds=7):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    columns, rows = folds * 4, 2
    grid = []
    for row in range(rows + 1):
        t = row / rows
        pinch = 0.5 + 0.5 * t  # к низу складки глубже
        line = []
        for c in range(columns + 1):
            u = c / columns
            x = x_from + (x_to - x_from) * u
            depth = math.sin(u * folds * 2 * math.pi) * 0.09 * pinch
            line.append(bm.verts.new((x, y + depth, z_high + (z_low - z_high) * t)))
        grid.append(line)
    for row in range(rows):
        for c in range(columns):
            bm.faces.new((grid[row][c], grid[row][c + 1], grid[row + 1][c + 1], grid[row + 1][c]))
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = link(bpy.data.objects.new(name, mesh), material)
    # Вторая сторона: иначе изнанка складок запекается чёрной.
    obj.modifiers.new("s", "SOLIDIFY").thickness = 0.015
    return obj


def planks():
    """Широкие доски с разбежкой швов, ровными тонами и тонкими линиями волокон, как у столов."""
    size = 1536
    image = np.zeros((size, size, 3))
    local = np.random.RandomState(4)
    tones = [(0.48, 0.30, 0.15), (0.43, 0.26, 0.135), (0.52, 0.33, 0.17), (0.45, 0.28, 0.14), (0.41, 0.25, 0.12)]
    width = int(size * 0.62 / (SIDE * 2))
    per_metre = size / (BACK - FRONT)
    for column in range(size // width + 1):
        x0 = column * width
        y = -local.randint(0, int(per_metre * 4))
        while y < size:
            length = int(per_metre * (3.2 + local.rand() * 2.6))
            tone = np.array(tones[local.randint(0, len(tones))])
            top, bottom = max(0, y), min(size, y + length)
            if bottom > top:
                image[top:bottom, x0:x0 + width] = tone
                for _ in range(7):
                    gx = x0 + local.randint(3, max(4, width - 3))
                    if gx < size:
                        image[top:bottom, gx] = tone * 0.84
                image[max(0, bottom - 3):bottom, x0:x0 + width] = (0.07, 0.04, 0.025)
            y += length
        image[:, x0:x0 + 3] = (0.07, 0.04, 0.025)
    return image


def painting(kind):
    """Картины на стене: простые рисунки в стиле игры, читаются издали."""
    size = 128
    y, x = np.mgrid[0:size, 0:size] / size
    if kind == "boat":
        image = np.zeros((size, size, 3)) + np.array([0.68, 0.82, 0.88])
        image[y > 0.62] = (0.2, 0.42, 0.62)
        image[(np.abs(x - 0.5) < 0.24 - (0.74 - y) * 0.5) & (y > 0.54) & (y < 0.68)] = (0.25, 0.13, 0.07)
        image[(x > 0.5) & (x < 0.5 + (0.54 - y) * 0.75) & (y < 0.54) & (y > 0.2)] = (0.93, 0.88, 0.72)
        image[(x < 0.49) & (x > 0.49 - (0.54 - y) * 0.45) & (y < 0.54) & (y > 0.3)] = (0.78, 0.2, 0.17)
        return image
    if kind == "die":
        image = np.zeros((size, size, 3)) + np.array([0.56, 0.16, 0.14])
        image[(np.abs(x - 0.5) < 0.26) & (np.abs(y - 0.5) < 0.26)] = (0.93, 0.87, 0.72)
        for px, py in ((0.36, 0.36), (0.5, 0.5), (0.64, 0.64), (0.64, 0.36), (0.36, 0.64)):
            image[np.hypot(x - px, y - py) < 0.05] = (0.09, 0.06, 0.05)
        return image
    # Пейзаж: холмы и солнце.
    image = np.zeros((size, size, 3)) + np.array([0.87, 0.63, 0.32])
    image[np.hypot(x - 0.68, y - 0.3) < 0.12] = (0.98, 0.89, 0.55)
    image[y > 0.55 + 0.08 * np.sin(x * 7)] = (0.32, 0.45, 0.25)
    image[y > 0.72 + 0.06 * np.sin(x * 11 + 2)] = (0.2, 0.3, 0.17)
    return image


def wallpaper():
    """Обои: тёплый зелёный с негромким узором. Узор виден рядом, издали — фактура стены."""
    size = 256
    y, x = np.mgrid[0:size, 0:size] / size
    u, v = x % 1 - 0.5, y % 1 - 0.5
    diamond = np.abs(u) + np.abs(v)
    base = np.array([0.46, 0.55, 0.42])
    color = np.zeros((size, size, 3)) + base
    motif = ((diamond > 0.36) & (diamond < 0.40)) | (diamond < 0.07)
    color = np.where(motif[..., None], base * 1.22, color)
    return np.clip(color, 0, 1)


def bricks():
    """Камень камина: неровная кладка ровными тонами."""
    width, height = 512, 512
    image = np.zeros((height, width, 3)) + np.array([0.13, 0.10, 0.09])
    local = np.random.RandomState(3)
    row_h = 64
    for r in range(height // row_h):
        x = -local.randint(0, 80)
        while x < width:
            w = local.randint(90, 150)
            tone = 0.40 + local.rand() * 0.16
            tint = np.array([tone * 1.06, tone * 0.92, tone * 0.80])
            image[r * row_h + 5:(r + 1) * row_h - 5, max(0, x + 5):max(0, x + w - 5)] = tint
            x += w
    return image


def rug_image():
    size = 512
    y, x = np.mgrid[0:size, 0:size]
    u, v = (x - size / 2) / (size / 2), (y - size / 2) / (size / 2)
    r = np.hypot(u, v)
    a = np.arctan2(v, u)
    # Ковёр спокойный: не спорит со столом. Тёмное вино и приглушённая охра.
    wine, ochre, navy = np.array([0.46, 0.14, 0.13]), np.array([0.66, 0.48, 0.24]), np.array([0.2, 0.2, 0.33])
    color = np.zeros((size, size, 3)) + wine
    color = np.where((r > 0.88)[..., None], ochre, color)
    color = np.where(((r > 0.76) & (r < 0.82))[..., None], ochre, color)
    color = np.where(((r > 0.82) & (r < 0.88))[..., None], navy, color)
    teeth = (r > 0.58) & (r < 0.72) & (np.sin(a * 22) + (r - 0.65) * 12 > 0.3)
    color = np.where(teeth[..., None], navy, color)
    return np.clip(color, 0, 1)


def room():
    plaster = mat("Plaster", "#b9a98c", 0.9)
    beam = mat("Beam", "#4a2c17", 0.7)
    paper = image_mat("Paper", wallpaper(), scale=(1.6, 1.6, 1.6))
    wood = mat("Panel", "#5b3419", 0.55)
    wood_dark = mat("PanelDark", "#3f2311", 0.55)

    # Пол: доски вдоль взгляда нарисованы в текстуре, в модели одна плоскость. Свет ляжет сверху при запекании.
    mesh = bpy.data.meshes.new("Floor")
    mesh.from_pydata([(-SIDE, FRONT, FLOOR), (SIDE, FRONT, FLOOR), (SIDE, BACK, FLOOR), (-SIDE, BACK, FLOOR)], [], [(0, 1, 2, 3)])
    mesh.update()
    layer = mesh.uv_layers.new(name="UVMap")
    for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        layer.data[k].uv = uv
    link(bpy.data.objects.new("Floor", mesh), image_mat("Planks", planks(), rough=0.55))

    # Стены: левая и правая сплошные, задняя с настоящим проёмом окна.
    window_x, window_w, sill, top = -4.5, 2.5, 0.0, 2.9
    def back_wall(x0, x1, z0, z1):
        return box("Back", ((x0 + x1) / 2, BACK + 0.15, z0), (x1 - x0, 0.3, z1 - z0), paper, bevel=0)

    back_wall(-SIDE, window_x - window_w / 2, FLOOR, CEILING)
    back_wall(window_x + window_w / 2, SIDE, FLOOR, CEILING)
    back_wall(window_x - window_w / 2, window_x + window_w / 2, FLOOR, sill)
    back_wall(window_x - window_w / 2, window_x + window_w / 2, top, CEILING)
    box("Left", (-SIDE - 0.15, (BACK + FRONT) / 2, FLOOR), (0.3, BACK - FRONT, CEILING - FLOOR), paper, bevel=0)
    box("Right", (SIDE + 0.15, (BACK + FRONT) / 2, FLOOR), (0.3, BACK - FRONT, CEILING - FLOOR), paper, bevel=0)
    box("Front", (0, FRONT - 0.15, FLOOR), (SIDE * 2 + 0.6, 0.3, CEILING - FLOOR), paper, bevel=0)
    box("Ceiling", (0, (BACK + FRONT) / 2, CEILING), (SIDE * 2 + 0.6, BACK - FRONT + 0.6, 0.2), plaster, bevel=0)
    for y in (-6.5, -3.5, -0.5, 2.5):
        box("CeilBeam", (0, y, CEILING - 0.34), (SIDE * 2, 0.34, 0.36), beam, bevel=0.03)

    # Деревянные панели низа стены: рама и филёнки объёмом.
    panel_h = 1.35
    for name, axis, span, offset in (("B", "x", (-SIDE, SIDE), BACK - 0.03), ("L", "y", (FRONT, BACK), -SIDE + 0.03), ("R", "y", (FRONT, BACK), SIDE - 0.03)):
        def piece(a0, a1, z, h, depth, material):
            middle, length = (a0 + a1) / 2, a1 - a0
            sign = -1 if name != "L" else 1
            d = offset + sign * depth / 2
            if axis == "x":
                box("Wains", (middle, d, z), (length, depth, h), material, bevel=0)
            else:
                box("Wains", (d, middle, z), (depth, length, h), material, bevel=0)

        piece(span[0], span[1], FLOOR, panel_h, 0.05, wood_dark)
        piece(span[0], span[1], FLOOR, 0.26, 0.11, wood)  # плинтус
        piece(span[0], span[1], FLOOR + panel_h - 0.1, 0.14, 0.13, wood)  # верхний брус
        pos = span[0] + 0.12
        while pos + 1.15 < span[1]:
            if not (name == "B" and pos + 1.15 > window_x - window_w / 2 - 0.1 and pos < window_x + window_w / 2 + 0.1 and False):
                piece(pos, pos + 1.15, FLOOR + 0.38, panel_h - 0.6, 0.09, wood)
            pos += 1.4

    # Окно: рама, переплёт, подоконник, ночь за стеклом, шторы.
    frame = mat("Frame", "#3f2311", 0.6)
    for sign in (-1, 1):
        box("WinSide", (window_x + sign * window_w / 2, BACK - 0.03, sill), (0.16, 0.5, top - sill), frame)
    box("WinTop", (window_x, BACK - 0.03, top - 0.06), (window_w + 0.3, 0.5, 0.18), frame)
    box("Sill", (window_x, BACK - 0.14, sill - 0.04), (window_w + 0.5, 0.6, 0.12), mat("SillWood", "#6b3f1e", 0.5))
    box("Mull", (window_x, BACK + 0.1, sill), (0.07, 0.06, top - sill), frame)
    box("MullH", (window_x, BACK + 0.1, 1.5), (window_w, 0.06, 0.07), frame)
    # Небо и луна за окном не должны заслонять лунный свет.
    sky = box("Sky", (window_x, BACK + 3.2, -2), (9, 0.1, 9), mat("Night", "#1b2752", 1, emit=1.1), bevel=0)
    sky.visible_shadow = False
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.5, location=(window_x + 0.55, BACK + 3.0, 2.25))
    moon = bpy.context.object
    moon.name = "SkyMoon"
    moon.visible_shadow = False
    moon.data.materials.append(mat("MoonMat", "#fff3c4", 1, emit=4.0))
    curtain_mat = mat("CurtainRed", "#8e1f22", 0.9)
    curtain("CurtainL", window_x - window_w / 2 - 0.55, window_x - window_w / 2 + 0.25, BACK - 0.28, sill - 0.55, top + 0.25, curtain_mat)
    curtain("CurtainR", window_x + window_w / 2 - 0.25, window_x + window_w / 2 + 0.55, BACK - 0.28, sill - 0.55, top + 0.25, curtain_mat)
    cyl("Rod", (window_x, BACK - 0.28, top + 0.28), 0.04, window_w + 1.7, mat("Brass", "#c29a44", 0.3), axis="X", verts=10)

    # Камин: каменные косяки и свод, тяжёлая полка, чёрная топка, дрова. Огонь — те же кадры, что в интерфейсе.
    stone = image_mat("Stone", bricks(), scale=(0.55, 0.55, 0.55))
    fx = 4.5
    fy = BACK - 0.5
    box("JambL", (fx - 1.3, fy, FLOOR), (0.7, 1.0, 2.1), stone, bevel=0.05)
    box("JambR", (fx + 1.3, fy, FLOOR), (0.7, 1.0, 2.1), stone, bevel=0.05)
    box("Lintel", (fx, fy, FLOOR + 1.75), (1.95, 1.0, 0.55), stone, bevel=0.05)
    box("Hearth", (fx, fy - 0.4, FLOOR), (3.8, 1.8, 0.08), mat("HearthStone", "#4e4641", 0.8))
    box("FireBack", (fx, fy + 0.45, FLOOR), (1.95, 0.1, 1.8), mat("Soot", "#120d0b", 0.95), bevel=0)
    box("Mantel", (fx, fy - 0.1, FLOOR + 2.1), (3.5, 1.35, 0.2), mat("MantelWood", "#4a2813", 0.45), bevel=0.04)
    box("Chimney", (fx, fy + 0.15, FLOOR + 2.3), (2.6, 0.7, CEILING - FLOOR - 2.3), stone, bevel=0.04)
    bark = mat("Bark", "#2b1a10", 0.9)
    cyl("Log", (fx - 0.2, fy - 0.1, FLOOR + 0.2), 0.14, 1.3, bark, axis="X", verts=9)
    cyl("Log", (fx + 0.15, fy + 0.1, FLOOR + 0.2), 0.13, 1.25, bark, axis="X", verts=9)
    cyl("Log", (fx, fy, FLOOR + 0.42), 0.12, 1.1, bark, axis="X", verts=9)
    flames(fx, fy - 0.12)

    # Полка камина: часы, свечи, кубок, рамка.
    brass = mat("Brass", "#c29a44", 0.3)
    box("ClockBody", (fx, fy - 0.15, FLOOR + 2.3), (0.55, 0.28, 0.7), mat("ClockWood", "#6b3f1e", 0.45))
    cyl("ClockFace", (fx, fy - 0.3, FLOOR + 2.72), 0.2, 0.03, mat("Ivory", "#f1e6c8", 0.5), axis="Y", verts=20)
    for dx in (-1.35, 1.25):
        cyl("CandleBase", (fx + dx, fy - 0.2, FLOOR + 2.31), 0.11, 0.03, brass, verts=14)
        cyl("Candle", (fx + dx, fy - 0.2, FLOOR + 2.46), 0.05, 0.3, mat("Wax", "#ece1c1", 0.6), verts=10)
        blob("CandleFlame", (fx + dx, fy - 0.2, FLOOR + 2.66), (0.03, 0.03, 0.07), mat("FlameGlow", "#ffb54a", 1, emit=9), 1)
    box("Photo", (fx - 0.75, fy - 0.12, FLOOR + 2.3), (0.35, 0.05, 0.42), frame)

    # Левая стена: книжный шкаф до потолка, книги с цветными корешками.
    shelf_wood = mat("ShelfWood", "#4a2813", 0.55)
    sx, sy0, sy1 = -SIDE + 0.45, -0.2, 3.3
    box("CaseBack", (sx - 0.36, (sy0 + sy1) / 2, FLOOR), (0.08, sy1 - sy0, 3.6), shelf_wood, bevel=0)
    for y in (sy0, sy1):
        box("CaseSide", (sx, y, FLOOR), (0.8, 0.1, 3.6), shelf_wood)
    colors = ["#a82d24", "#2a5a8c", "#c9982f", "#2f7a4c", "#6a3a7c", "#d9c9a3", "#7a4c26"]
    for level in range(5):
        z = FLOOR + 0.12 + level * 0.72
        box("CaseShelf", (sx, (sy0 + sy1) / 2, z - 0.06), (0.8, sy1 - sy0, 0.07), shelf_wood, bevel=0.008)
        y = sy0 + 0.1
        while y < sy1 - 0.3:
            w = 0.1 + rng.rand() * 0.1
            h = 0.42 + rng.rand() * 0.18
            tilt = 0.0
            index = rng.randint(0, 7)
            box("Book", (sx + 0.05, y + w / 2, z), (0.5, w - 0.008, h), mat("Book%d" % index, colors[index], 0.6), bevel=0)
            y += w
            if rng.rand() < 0.07:
                y += 0.25 + rng.rand() * 0.25
    # Сундук у правой стены, кресло у камина, столик со свечой, кот на ковре.
    trunk = mat("Trunk", "#6b3f1e", 0.55)
    box("Chest", (-5.9, -3.2, FLOOR), (1.9, 1.15, 0.8), trunk)
    cyl("ChestLid", (-5.9, -3.2, FLOOR + 0.8), 0.58, 1.9, trunk, axis="X", verts=20)
    for dx in (-0.7, 0.7):
        box("Strap", (-5.9 + dx, -3.2, FLOOR), (0.14, 1.2, 1.32), brass, bevel=0.01)

    fabric = mat("Velvet", "#7d2a27", 0.95)
    arm = bpy.data.objects.new("ArmEmpty", None)
    bpy.context.collection.objects.link(arm)
    pieces = [
        cushion("Seat", (0, 0, FLOOR + 0.28), (1.5, 1.45, 0.42), fabric),
        cushion("Back", (0, 0.62, FLOOR + 0.28), (1.65, 0.42, 1.65), fabric),
        cushion("ArmL", (-0.78, 0, FLOOR + 0.28), (0.36, 1.5, 0.82), fabric),
        cushion("ArmR", (0.78, 0, FLOOR + 0.28), (0.36, 1.5, 0.82), fabric),
        box("Base", (0, 0, FLOOR), (1.9, 1.6, 0.3), mat("ChairWood", "#3f2311", 0.5)),
    ]
    for piece in pieces:
        piece.parent = arm
    arm.location = (5.6, 1.5, 0)
    arm.rotation_euler = (0, 0, math.radians(-155 + 180))

    table = mat("SideWood", "#5b3419", 0.45)
    cyl("SideTop", (-4.9, 2.3, FLOOR + 1.0), 0.6, 0.06, table, verts=24)
    cyl("SideLeg", (-4.9, 2.3, FLOOR + 0.5), 0.07, 1.0, table, verts=10)
    cyl("SideFoot", (-4.9, 2.3, FLOOR + 0.04), 0.36, 0.05, table, verts=16)
    cyl("Cup", (-5.0, 2.2, FLOOR + 1.11), 0.1, 0.13, mat("Ivory", "#f1e6c8", 0.5), verts=14)

    # Растение в горшке в углу: листья кучей.
    cyl("Pot", (-6.6, 4.3, FLOOR + 0.35), 0.42, 0.7, mat("Terra", "#a85a34", 0.8), verts=18)
    leaf = mat("Leaf", "#2f6f3f", 0.6)
    for i in range(12):
        a = i * 2.4
        r = 0.18 + (i % 4) * 0.13
        h = 0.9 + (i % 5) * 0.28
        leaf_obj = blob("Leaf", (-6.6 + math.cos(a) * r * 1.6, 4.3 + math.sin(a) * r, FLOOR + h), (0.13, 0.42, 0.05), leaf, 1)
        leaf_obj.rotation_euler = (0.9, 0.2, a)

    # Кот спит на ковре у кресла: простая округлая форма, а живая деталь.
    cat = mat("Cat", "#d27f33", 0.85)
    cat_x, cat_y = -4.6, 0.1
    blob("CatBody", (cat_x, cat_y, FLOOR + 0.2), (0.55, 0.38, 0.2), cat, 2)
    blob("CatHead", (cat_x + 0.46, cat_y - 0.12, FLOOR + 0.25), (0.21, 0.2, 0.18), cat, 2)
    blob("CatTail", (cat_x - 0.3, cat_y - 0.36, FLOOR + 0.07), (0.42, 0.08, 0.07), cat, 1)
    for dx in (-0.11, 0.11):
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.08, depth=0.16, location=(cat_x + 0.46 + dx, cat_y - 0.14, FLOOR + 0.44))
        bpy.context.object.data.materials.append(cat)

    # Картины на стене за соперником: рамы объёмом и настоящие рисунки. На нижних частях рисунок виден и из камеры партии.
    for (cx, z, w, h, kind) in ((-1.15, 0.05, 1.1, 1.1, "boat"), (0.2, 0.35, 0.8, 0.8, "die"), (1.45, -0.05, 1.05, 1.2, "hills")):
        box("PicFrame", (cx, BACK - 0.03, z), (w + 0.18, 0.07, h + 0.18), mat("Gilt", "#b58b3a", 0.35), bevel=0.02)
        picture = plane_image("Pic" + kind, cx, BACK - 0.075, z + 0.09, w, h, painting(kind))

    # Ковёр: толщина и мягкий край.
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=1.0, depth=0.03, location=(0, -0.6, FLOOR + 0.015))
    rug = bpy.context.object
    rug.scale = (5.2, 3.9, 1)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.uv.cube_project(cube_size=2)
    bpy.ops.object.mode_set(mode="OBJECT")
    rug.data.materials.append(image_mat("Rug", rug_image(), rough=1.0))
    bpy.ops.object.shade_smooth()


def plane_image(name, x, y, z, width, height, pixels):
    """Плоская картинка на стене."""
    mesh = bpy.data.meshes.new(name)
    half = width / 2
    mesh.from_pydata([(x - half, y, z), (x + half, y, z), (x + half, y, z + height), (x - half, y, z + height)], [], [(0, 1, 2, 3)])
    mesh.update()
    layer = mesh.uv_layers.new(name="UVMap")
    for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        layer.data[k].uv = uv
    return link(bpy.data.objects.new(name, mesh), image_mat(name, pixels))


def flames(x, y):
    """Огонь: кадры из игры на плоскостях. Не отбрасывает тени: светит отдельный источник."""
    for i, (dx, h) in enumerate(((-0.42, 0.75), (0.0, 1.15), (0.4, 0.82))):
        path = os.path.join(ART, "Sprites", "UI_Flame_%d.png" % ((i * 3) % 8))
        if not os.path.exists(path):
            continue
        material = bpy.data.materials.new("Flame%d" % i)
        material.use_nodes = True
        material.blend_method = "BLEND"
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path)
        emit = nodes.new("ShaderNodeEmission")
        emit.inputs["Strength"].default_value = 2.6
        clear = nodes.new("ShaderNodeBsdfTransparent")
        mix = nodes.new("ShaderNodeMixShader")
        out = nodes.new("ShaderNodeOutputMaterial")
        links.new(tex.outputs["Color"], emit.inputs[0])
        links.new(tex.outputs["Alpha"], mix.inputs[0])
        links.new(clear.outputs[0], mix.inputs[1])
        links.new(emit.outputs[0], mix.inputs[2])
        links.new(mix.outputs[0], out.inputs[0])
        half = h * 0.42
        mesh = bpy.data.meshes.new("FlameQuad")
        z0 = FLOOR + 0.28
        mesh.from_pydata([(x + dx - half, y, z0), (x + dx + half, y, z0), (x + dx + half, y, z0 + h), (x + dx - half, y, z0 + h)], [], [(0, 1, 2, 3)])
        mesh.update()
        layer = mesh.uv_layers.new()
        for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
            layer.data[k].uv = uv
        obj = link(bpy.data.objects.new("FlameQuad", mesh), material)
        obj.visible_shadow = False


def table():
    """Настоящий стол из игры: верх на нуле."""
    path = os.path.join(ART, "Models", "Tables", "Table_Royal.fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    tex_path = os.path.join(ART, "Textures", "Tables", "Table_Royal.png")
    material = bpy.data.materials.new("RoyalTable")
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.45
    tex = material.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(tex_path)
    material.node_tree.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    top = -1e9
    for obj in new:
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for corner in obj.bound_box:
            top = max(top, (obj.matrix_world @ Vector(corner)).z)
    for obj in new:
        obj.location.z -= top
    print("[zonk_room] table top was", top, "objects", len(new))

    # Лоток: красное сукно и борт, как в партии.
    felt = mat("TrayFelt", "#8e1320", 0.95)
    box("TrayFelt", (0, 0, 0.0), (3.2, 2.0, 0.02), felt, bevel=0)
    gold = mat("TrayRim", "#c89b3c", 0.4)
    for sy in (-1.03, 1.03):
        box("TrayRimX", (0, sy, 0.0), (3.34, 0.08, 0.1), gold)
    for sx in (-1.63, 1.63):
        box("TrayRimY", (sx, 0, 0.0), (0.08, 2.12, 0.1), gold)


def lights():
    # Лампа над столом: главное тёплое пятно.
    spot = bpy.data.lights.new("TableLamp", "SPOT")
    spot.energy = 620
    spot.color = (1.0, 0.82, 0.58)
    spot.spot_size = math.radians(112)
    spot.spot_blend = 0.65
    spot.shadow_soft_size = 0.35
    obj = bpy.data.objects.new("TableLamp", spot)
    bpy.context.collection.objects.link(obj)
    obj.location = (0, 0, 3.6)

    # Камин: живой оранжевый свет на пол и кресло.
    fire = bpy.data.lights.new("Fire", "POINT")
    fire.energy = 330
    fire.color = (1.0, 0.45, 0.14)
    fire.shadow_soft_size = 0.4
    obj = bpy.data.objects.new("Fire", fire)
    bpy.context.collection.objects.link(obj)
    obj.location = (4.5, BACK - 1.3, FLOOR + 0.8)

    # Луна через настоящий проём: холодная полоса на полу.
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy = 7.0
    moon.color = (0.55, 0.68, 1.0)
    moon.angle = math.radians(3)
    obj = bpy.data.objects.new("Moon", moon)
    bpy.context.collection.objects.link(obj)
    obj.rotation_euler = (math.radians(-52), 0, math.radians(-14))

    # Заполняющий тёплый свет: стены и ковёр не проваливаются в чёрное, под столом мягкая тень, а не дыра.
    # Спереди сильнее: стол снизу в кадре, а пол под ним не должен быть чёрной дырой.
    for (x, y, z, energy) in ((-3.6, -1.5, 3.4, 110), (3.2, -3.5, 3.4, 95), (0.4, BACK - 1.1, 3.0, 60), (-5.6, 2.2, 2.6, 45), (0, -5.5, 2.2, 130), (0, -1.2, 0.9, 26)):
        fill = bpy.data.lights.new("Fill", "POINT")
        fill.energy = energy
        fill.color = (1.0, 0.8, 0.6)
        fill.shadow_soft_size = 1.6
        obj = bpy.data.objects.new("Fill", fill)
        bpy.context.collection.objects.link(obj)
        obj.location = (x, y, z)

    # Свечи на камине и немного общего света, чтобы тени не проваливались в чёрное.
    for dx in (-1.35, 1.25):
        candle = bpy.data.lights.new("Candle", "POINT")
        candle.energy = 10
        candle.color = (1.0, 0.68, 0.32)
        obj = bpy.data.objects.new("Candle", candle)
        bpy.context.collection.objects.link(obj)
        obj.location = (4.5 + dx, BACK - 0.9, FLOOR + 2.8)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.09, 0.10, 0.15, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.3
    bpy.context.scene.world = world


def render(path, camera_unity, target_unity, fov, samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"
    scene.view_settings.exposure = 0.9
    camera = bpy.data.cameras.new("c")
    camera.sensor_fit = "VERTICAL"
    camera.angle_y = math.radians(fov)
    obj = bpy.data.objects.new("c", camera)
    bpy.context.collection.objects.link(obj)
    position = Vector((camera_unity[0], camera_unity[2], camera_unity[1]))
    target = Vector((target_unity[0], target_unity[2], target_unity[1]))
    obj.location = position
    obj.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
    scene.camera = obj
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(obj)
    print("[zonk_room] wrote", path)


# Задник уличной локации (zonk_places.backdrop): не запекается, в игру идёт своей картинкой.
SKY_PIXELS = None

ROOM_PARTS = ("Floor", "Back", "Left", "Right", "Front", "Ceiling", "CeilBeam", "Wains")
BAKE_EXPOSURE = 1.75


def tone_map(linear):
    """Запечённый свет в цвет текстуры: мягкие света как у плёнки, потом sRGB."""
    x = linear * BAKE_EXPOSURE
    x = np.clip(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14), 0, 1)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(np.maximum(x, 1e-6), 1 / 2.4) - 0.055)


def soften(image):
    """Лёгкое сглаживание остаточного шума запекания: крест 3×3."""
    return (image * 4 + np.roll(image, 1, 0) + np.roll(image, -1, 0) + np.roll(image, 1, 1) + np.roll(image, -1, 1)) / 8


def bake_group(objects, name, size, samples, folder):
    """Общая развёртка группы, запекание всего света в одну текстуру. Объекты ещё раздельные: небо не заслоняет луну."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        if "Bake" not in obj.data.uv_layers:
            if not obj.data.uv_layers:
                obj.data.uv_layers.new(name="UVMap")
            bake_layer = obj.data.uv_layers.new(name="Bake")
            obj.data.uv_layers.active = bake_layer
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, area_weight=0.6)
    bpy.ops.object.mode_set(mode="OBJECT")

    image = bpy.data.images.new("Bake_" + name, size, size, alpha=False, float_buffer=True)
    seen = set()
    for obj in objects:
        for slot in obj.material_slots:
            material = slot.material
            if material is None or material.name in seen:
                continue
            seen.add(material.name)
            node = material.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = image
            material.node_tree.nodes.active = node

    scene = bpy.context.scene
    scene.cycles.samples = samples
    scene.cycles.bake_type = "COMBINED"
    scene.render.bake.margin = 8
    bpy.ops.object.bake(type="COMBINED", use_clear=True, margin=8, target="IMAGE_TEXTURES")

    pixels = np.empty(size * size * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(size, size, 4)
    color = soften(tone_map(pixels[..., :3]))
    out = bpy.data.images.new("Out_" + name, size, size, alpha=False)
    out.colorspace_settings.name = "Non-Color"
    out.pixels.foreach_set(np.dstack([color, np.ones((size, size))]).astype(np.float32).ravel())
    path = os.path.join(folder, name + ".png")
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    print("[zonk_room] baked", path)
    return path


def flat_baked(name, path):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path)
    emit = nodes.new("ShaderNodeEmission")
    out = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(tex.outputs[0], emit.inputs[0])
    material.node_tree.links.new(emit.outputs[0], out.inputs[0])
    return material


def bake_and_export(samples, preview):
    views = (("mesto_dom_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("mesto_dom_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42))
    bake_location("Home", "Room_Home.fbx", room, lights, views, samples, preview)


def bake_location(prefix, model_file, build, build_lights, views, samples, preview, props_size=1024, groups=None):
    """
    Запекание локации: комната и предметы — в свои текстуры, выгрузка одной модели. Живые части добавляет генератор.
    groups — предметы по нескольким текстурам: список (имя, размер, условие по имени объекта). Без него — одна текстура Props.
    Больше текстур — больше пикселей на предмет; бюджет сцены: до 8 текстур, одна 2048 на пол, остальные 1024 и меньше.
    """
    models = os.path.join(ART, "Models", "Environments")
    textures = os.path.join(ART, "Textures", "Environments")
    os.makedirs(models, exist_ok=True)
    os.makedirs(textures, exist_ok=True)

    build()
    built = [o for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("FlameQuad") and not o.name.startswith("SkyBack")]
    for obj in list(bpy.data.objects):
        if obj.name.startswith("FlameQuad"):
            obj.hide_render = True
    table()
    build_lights()

    # Настоящие сетки: модификаторы и трансформы применены, родитель кресла снят.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in built:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = built[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.make_single_user(object=True, obdata=True)
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    def is_room(obj):
        return obj.name.split(".")[0] in ROOM_PARTS

    room_objects = [o for o in built if is_room(o)]
    prop_objects = [o for o in built if not is_room(o)]
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.max_bounces = 5

    room_path = bake_group(room_objects, prefix + "_Room", 2048, samples, textures)
    prop_sets = []
    if groups:
        rest = list(prop_objects)
        for group_name, size, wanted in groups:
            members = [o for o in rest if wanted(o.name.split(".")[0])]
            rest = [o for o in rest if o not in members]
            if members:
                prop_sets.append((group_name, members, bake_group(members, prefix + "_" + group_name, size, samples, textures)))
        if rest:
            raise Exception("Objects without a texture group: " + ", ".join(sorted({o.name.split(".")[0] for o in rest})))
    else:
        prop_sets.append(("Props", prop_objects, bake_group(prop_objects, prefix + "_Props", props_size, samples, textures)))

    # Склейка в две сетки: две текстуры, два вызова отрисовки. В модели только развёртка запекания.
    joined = []
    # Детали Sway* запечены вместе с предметами, но в игре отдельной сеткой: качаются в шейдере Zonk/BakedSway.
    meshes = [(room_objects, "Room", room_path)]
    for group_name, members, path in prop_sets:
        static = [o for o in members if not o.name.startswith("Sway")]
        sway = [o for o in members if o.name.startswith("Sway")]
        if static:
            meshes.append((static, group_name, path))
        if sway:
            meshes.append((sway, "Sway", path))
    for objects, name, path in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        merged = bpy.context.object
        merged.name = name
        merged.visible_shadow = True
        for layer in list(merged.data.uv_layers):
            if layer.name != "Bake":
                merged.data.uv_layers.remove(layer)
        merged.data.materials.clear()
        merged.data.materials.append(flat_baked("M_" + prefix + "_" + name, path))
        for polygon in merged.data.polygons:
            polygon.material_index = 0
        joined.append(merged)

    sky = [o for o in bpy.data.objects if o.name.startswith("SkyBack")]
    if sky and SKY_PIXELS is not None:
        height, width = SKY_PIXELS.shape[:2]
        out = bpy.data.images.new("Out_Sky", width, height, alpha=False)
        out.colorspace_settings.name = "Non-Color"
        out.pixels.foreach_set(np.flipud(np.dstack([SKY_PIXELS, np.ones((height, width))])).astype(np.float32).ravel())
        out.filepath_raw = os.path.join(textures, prefix + "_Sky.png")
        out.file_format = "PNG"
        out.save()
        joined.append(sky[0])

    triangles = 0
    for merged in joined:
        merged.data.calc_loop_triangles()
        print("[zonk_room]", merged.name, "triangles", len(merged.data.loop_triangles))
        triangles += len(merged.data.loop_triangles)
    print("[zonk_room] total triangles", triangles)

    # Проверка: то, что увидит игра — только текстуры, без света. Настоящий стол освещён сценой.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    for obj in bpy.data.objects:
        if obj.name.startswith("FlameQuad"):
            obj.hide_render = False
    for file_name, camera, target, fov in views:
        render_flat(os.path.join(preview, file_name), camera, target, fov)

    # Выгрузка. Unity = (-bx, bz, -by), поэтому разворот на 180° вокруг вертикали: x и z как в Unity-координатах этого файла.
    bpy.ops.object.select_all(action="DESELECT")
    for merged in joined:
        merged.select_set(True)
        for vertex in merged.data.vertices:
            vertex.co.x, vertex.co.y = -vertex.co.x, -vertex.co.y
        merged.data.update()
    bpy.context.view_layer.objects.active = joined[0]
    path = os.path.join(models, model_file)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             mesh_smooth_type="OFF", use_mesh_modifiers=False, bake_anim=False, path_mode="COPY", embed_textures=False)
    print("[zonk_room] exported", path)


def render_flat(path, camera_unity, target_unity, fov):
    scene = bpy.context.scene
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.image_settings.file_format = "PNG"
    camera = bpy.data.cameras.new("c")
    camera.sensor_fit = "VERTICAL"
    camera.angle_y = math.radians(fov)
    obj = bpy.data.objects.new("c", camera)
    bpy.context.collection.objects.link(obj)
    position = Vector((camera_unity[0], camera_unity[2], camera_unity[1]))
    target = Vector((target_unity[0], target_unity[2], target_unity[1]))
    obj.location = position
    obj.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
    scene.camera = obj
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(obj)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 64
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        bake_and_export(samples, argv[argv.index("--bake") + 1])
        return

    folder = argv[argv.index("--render") + 1]
    room()
    table()
    lights()
    render(os.path.join(folder, "room_match.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    render(os.path.join(folder, "room_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)


# Другие локации берут отсюда общие детали (zonk_tavern.py): при импорте ничего не строится.
if __name__ == "__main__":
    main()
