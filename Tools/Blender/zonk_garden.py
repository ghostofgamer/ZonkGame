# -*- coding: utf-8 -*-
"""
Локация «Сад», глава 5: розарий графини Розы солнечным днём. Самая светлая и спокойная сцена после ночного шторма.
Глубина вместо декорации: дорожка уводит за калитку, за оградой холмы и деревья, сверху круглое небо без стыков.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_garden.py -- --render <папка> [--samples 48]
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, земля на -1.6.
Рука соперника лежит на x −2.2, z 2.3 (рукав до z 3.5): там выше стола ничего нет. За центром стола стоит соперник: высокое ставить в стороны.
Кадр партии: на z 4 видно до высоты 1.4, на z 6.5 до 0.7. Крона яблони ниже этого, иначе в кадре останется один ствол.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_beach as bc  # noqa: E402
import zonk_room as kit  # noqa: E402
import zonk_ship as zs  # noqa: E402
import zonk_tavern as tv  # noqa: E402

from zonk_beach import box, ground, pole  # noqa: E402
from zonk_room import FLOOR, blob, cyl, mat  # noqa: E402
from zonk_ship import set_wind, smooth, smooth_path, strip, tube  # noqa: E402
from zonk_tavern import lathe, plate, torus  # noqa: E402

FENCE_Y = 6.7
GATE_X = 0.9
GROUND = (-22.0, 22.0, -12.5, 14.0)
rng = np.random.RandomState(5)
GRASS = (0.52, 0.67, 0.39)


# ---------- Картинки ----------

def path_point(t):
    """Дорожка: от переднего края мимо стола слева, за столом к калитке."""
    points = [(-2.6, -12.5), (-4.0, -7.0), (-4.7, -2.5), (-4.1, 1.6), (-2.4, 4.3), (GATE_X, 5.5), (GATE_X, 8.0), (GATE_X + 0.9, 13.5)]
    return smooth_path([(x, y, 0) for x, y in points], 40)


def lawn_image():
    """Газон: ровные пятна двух-трёх зелёных, полосы от косилки, плитняк дорожки. Без мелкого шума: стиль комикса."""
    size = 2048
    x0, x1, y0, y1 = GROUND
    py, px = np.mgrid[0:size, 0:size]
    wx = x0 + (x1 - x0) * px / size
    wy = y1 + (y0 - y1) * py / size
    # Цвета как у дальней травы (GardenFarGrass), поэтому край газона растворяется.
    far = np.array(GRASS)
    image = np.zeros((size, size, 3)) + far
    patches = zs.cloud_noise(size, size, 11, 1.0, 2.3)
    image[patches > 0.6] = far * 0.93
    image[patches < 0.38] = far * 1.06
    image[patches < 0.2] = far * 1.12

    # Мелочь на траве: пучки тёмных штрихов и россыпи маргариток. Редкие и крупные: без мелкого шума.
    marks = np.random.RandomState(23)
    for _ in range(620):
        col, row = marks.randint(16, size - 16), marks.randint(16, size - 16)
        for k in (-1, 0, 1):
            for t in range(9):
                r, c = row - t, col + k * (2 + t // 3)
                image[r:r + 2, c:c + 2] = far * 0.74
    for _ in range(46):
        col0, row0 = marks.randint(40, size - 40), marks.randint(40, size - 40)
        for _ in range(9):
            col, row = col0 + marks.randint(-36, 36), row0 + marks.randint(-36, 36)
            yy, xx = np.mgrid[max(row - 6, 0):min(row + 6, size), max(col - 6, 0):min(col + 6, size)]
            d = (xx - col) ** 2 + (yy - row) ** 2
            image[yy[d < 22], xx[d < 22]] = (0.97, 0.97, 0.9)
            image[yy[d < 5], xx[d < 5]] = (0.96, 0.78, 0.2)

    # Плитняк: плоские камни вдоль кривой, тёмная земля в стыках.
    local = np.random.RandomState(17)
    path = path_point(0)
    scale = size / (x1 - x0)
    scale_y = size / (y1 - y0)
    distance = 0.0
    last = None
    for point in path:
        if last is not None:
            distance += (point - last).length
        last = point
        if distance < 0.62:
            continue
        distance = 0.0
        for side in (-1, 1) if local.rand() < 0.5 else (0,):
            cx = point.x + side * 0.3 + (local.rand() - 0.5) * 0.14
            cy = point.y + (local.rand() - 0.5) * 0.14
            rx, ry = 0.3 + local.rand() * 0.11, 0.25 + local.rand() * 0.09
            col = int((cx - x0) * scale)
            row = int((y1 - cy) * scale_y)
            half = int(max(rx * scale, ry * scale_y) * 1.25)
            r0, r1, c0, c1 = max(row - half, 0), min(row + half, size), max(col - half, 0), min(col + half, size)
            if r0 >= r1 or c0 >= c1:
                continue
            yy, xx = np.mgrid[r0:r1, c0:c1]
            d = ((xx - col) / (rx * scale)) ** 2 + ((yy - row) / (ry * scale_y)) ** 2
            image[r0:r1, c0:c1][d < 1.28] = (0.33, 0.27, 0.19)
            tone = 0.68 + local.rand() * 0.12
            image[r0:r1, c0:c1][d < 1.0] = (tone, tone * 0.94, tone * 0.82)

    # Края к цвету дальней травы: границы газона не видно.
    edge = np.minimum.reduce([wx - x0, x1 - wx, wy - y0, y1 - wy])
    fade = smooth(0.0, 5.0, edge)[..., None]
    image = image * fade + far * (1 - fade)
    return np.clip(image, 0, 1)


def sky_image():
    """Панорама на 360°: у горизонта светлая дымка, выше чистая синева, пухлые облака с тенью снизу."""
    height, width = 512, 1024
    y, x = np.mgrid[0:height, 0:width]
    v = 1 - y / height
    low = np.array([0.90, 0.95, 0.93])
    mid = np.array([0.56, 0.80, 0.95])
    top = np.array([0.30, 0.60, 0.92])
    a = smooth(0.02, 0.2, v)[..., None]
    b = smooth(0.2, 0.7, v)[..., None]
    image = (low * (1 - a) + mid * a) * (1 - b) + top * b
    clouds = zs.cloud_noise(width, height, 3, 1.25, 1.75)
    shape = smooth(0.1, 0.2, v) * (1 - smooth(0.42, 0.62, v))
    body = smooth(0.62, 0.66, clouds + (shape - 1) * 0.6)[..., None]
    shade = smooth(0.62, 0.66, np.roll(clouds, -8, axis=0) + (shape - 1) * 0.6)[..., None]
    image = image * (1 - body) + np.array([0.80, 0.86, 0.93]) * body
    image = image * (1 - shade * body) + np.array([0.99, 0.99, 0.97]) * shade * body
    return np.clip(image, 0, 1)


# ---------- Детали ----------

def dab_mat(name, dark, light, scale=5.5):
    """Листва комиксом: тёмный тон и светлые мазки пятнами. Гладкий шар с таким цветом читается кроной, а не пластиком."""
    material = bpy.data.materials.get(name)
    if material:
        return material
    material = mat(name, dark, 0.9)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes["Principled BSDF"]
    coord = nodes.new("ShaderNodeTexCoord")
    voronoi = nodes.new("ShaderNodeTexVoronoi")
    voronoi.inputs["Scale"].default_value = scale
    links.new(coord.outputs["Object"], voronoi.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = kit.col(light)
    ramp.color_ramp.elements[1].position = 0.34
    ramp.color_ramp.elements[1].color = kit.col(dark)
    links.new(voronoi.outputs["Distance"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    return material


def bloom(point, size, color, heart, name="BedRose"):
    """
    Роза куполом: круглый край и тёмная середина, смотрит вперёд и вверх, к камерам. Округлый силуэт при 50 гранях:
    шар при таком числе граней выглядит кристаллом.
    """
    center = Vector(point)
    toward = Vector((0, -0.72, 0.7)).normalized()
    side = toward.cross(Vector((0, 0, 1))).normalized()
    up = side.cross(toward).normalized()

    def ring(bm, radius, lift, count):
        return [bm.verts.new(center + (side * math.cos(k * 2 * math.pi / count) + up * math.sin(k * 2 * math.pi / count)) * radius + toward * lift) for k in range(count)]

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    count = 14
    rim = ring(bm, size * 1.08, -size * 0.3, count)
    mid = ring(bm, size * 0.8, size * 0.2, count)
    tip = bm.verts.new(center + toward * size * 0.44)
    for k in range(count):
        n = (k + 1) % count
        bm.faces.new((rim[k], rim[n], mid[n], mid[k]))
        bm.faces.new((mid[k], mid[n], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(color)

    inner = bpy.data.meshes.new(name)
    bm = bmesh.new()
    hub = bm.verts.new(center + toward * size * 0.56)
    around = ring(bm, size * 0.4, size * 0.38, 8)
    for k in range(8):
        bm.faces.new((hub, around[k], around[(k + 1) % 8]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(inner)
    bm.free()
    heart_obj = bpy.data.objects.new(name, inner)
    bpy.context.collection.objects.link(heart_obj)
    inner.materials.append(heart)


def picket_fence(x0, x1, y, white, shade):
    """Белый штакетник: острые дощечки, две поперечины. В кадре партии это задний край сада."""
    height = 1.05
    for z in (0.32, 0.8):
        pole("FencePost", (x0, y + 0.05, FLOOR + z), (x1, y + 0.05, FLOOR + z), 0.028, shade, verts=5)
    x = x0
    while x < x1 - 0.05:
        wobble = rng.rand() * 0.04
        top = FLOOR + height + wobble
        plate("FencePicket", [(x - 0.065, FLOOR), (x + 0.065, FLOOR), (x + 0.065, top - 0.09), (x, top), (x - 0.065, top - 0.09)], y, 0.03, white)
        x += 0.235


def gate(x, y, white, shade, iron, leaf, rose):
    """Калитка с аркой, увитой розами: дорожка уходит под неё, за оградой сад продолжается."""
    half = 0.72
    height = 2.35
    for sign in (-1, 1):
        px = x + sign * half
        box("GatePost", (px, y, FLOOR), (0.16, 0.16, height - 0.28), white, bevel=0.015)
        blob("GatePost", (px, y, FLOOR + height - 0.25), (0.11, 0.11, 0.11), shade, 2)
    arc = [(x + math.cos(a) * half, y, FLOOR + height - 0.32 + math.sin(a) * 0.72) for a in (math.pi * k / 10 for k in range(11))]
    tube("GateArch", arc, 0.07, white, sides=6)
    # Открытая створка: повёрнута на участок, калитка приглашает пройти.
    leaf_x = x - half + 0.08
    for k in range(6):
        t = k * 0.2
        px, py = leaf_x + t * 0.5, y - t * 0.95
        plate("GatePicket", [(px - 0.05, FLOOR + 0.1), (px + 0.05, FLOOR + 0.1), (px + 0.05, FLOOR + 0.98), (px, FLOOR + 1.06), (px - 0.05, FLOOR + 0.98)], py, 0.03, white)
    pole("GatePicket", (leaf_x, y, FLOOR + 0.35), (leaf_x + 0.5, y - 0.95, FLOOR + 0.35), 0.024, shade, verts=5)
    pole("GatePicket", (leaf_x, y, FLOOR + 0.82), (leaf_x + 0.5, y - 0.95, FLOOR + 0.82), 0.024, shade, verts=5)
    # Розы плетутся по столбам и арке.
    climb = [(x + sign * half + (rng.rand() - 0.5) * 0.14, y - 0.09, FLOOR + 0.3 + k * 0.3) for sign in (-1, 1) for k in range(6)] + [(p[0] + (rng.rand() - 0.5) * 0.16, y - 0.1, p[2] + (rng.rand() - 0.2) * 0.12) for p in arc]
    for point in climb:
        size = 0.15 + rng.rand() * 0.07
        blob("GateLeaf", point, (size * 1.3, size * 0.85, size * 1.2), leaf, 2)
        if rng.rand() < 0.7:
            color, heart = rose[rng.randint(0, len(rose))]
            bloom((point[0] + (rng.rand() - 0.5) * 0.1, point[1] - size * 0.72, point[2] + 0.02), 0.095, color, heart, "GateRose")


def gazebo(x, y, white, shade, roof_a, roof_b, wood, leaf, rose):
    """Беседка: шесть точёных столбов, ажурные кронштейны, двухцветная купольная крыша, скамья внутри, розы на столбе."""
    radius, height, sides = 1.45, 2.15, 6
    lathe("GazeboBase", (x, y, FLOOR), [(radius + 0.28, 0), (radius + 0.28, 0.16), (radius + 0.1, 0.18), (radius + 0.1, 0.27), (0, 0.27)], shade, segments=sides, smooth=False)
    floor_top = FLOOR + 0.27
    tops = []
    for k in range(sides):
        a = (k + 0.5) * 2 * math.pi / sides
        px, py = x + math.cos(a) * radius, y + math.sin(a) * radius
        tops.append((px, py))
        lathe("GazeboColumn", (px, py, floor_top), [(0.11, 0), (0.07, 0.12), (0.055, 0.4), (0.075, 1.0), (0.05, 1.65), (0.07, height - 0.14), (0.12, height)], white, segments=8)
    ring = floor_top + height
    for k in range(sides):
        a, b = tops[k], tops[(k + 1) % sides]
        pole("GazeboBeam", (a[0], a[1], ring), (b[0], b[1], ring), 0.06, white, verts=6)
        # Перильца и дуги, только где нет входа (вход со стороны стола).
        side_angle = (k + 1) * 2 * math.pi / sides
        arch = [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, ring - 0.34 * math.sin(math.pi * t) ** 0.6 * 0 - 0.42 + 0.42 * (2 * t - 1) ** 2) for t in (i / 8 for i in range(9))]
        tube("GazeboArch", arch, 0.022, white, sides=4)
        if not -2.1 < side_angle - math.pi * 1.5 < 0.6:
            pole("GazeboRail", (a[0], a[1], floor_top + 0.78), (b[0], b[1], floor_top + 0.78), 0.03, white, verts=5)
            for t in (0.2, 0.4, 0.6, 0.8):
                qx, qy = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
                lathe("GazeboRail", (qx, qy, floor_top), [(0.03, 0), (0.016, 0.2), (0.03, 0.5), (0.016, 0.78)], white, segments=5)
    # Крыша секторами двух цветов, с загнутым краем и шпилем.
    roof_profile = [(radius + 0.42, -0.06), (radius + 0.3, 0.05), (radius - 0.2, 0.42), (0.75, 0.86), (0.2, 1.22), (0.05, 1.32)]
    mesh = bpy.data.meshes.new("GazeboRoof")
    bm = bmesh.new()
    rows = []
    segments = sides * 2
    for k in range(segments):
        a = k * 2 * math.pi / segments
        rows.append([bm.verts.new((x + math.cos(a) * r, y + math.sin(a) * r, ring + 0.04 + h)) for r, h in roof_profile])
    for k in range(segments):
        n = (k + 1) % segments
        for r in range(len(roof_profile) - 1):
            face = bm.faces.new((rows[k][r], rows[n][r], rows[n][r + 1], rows[k][r + 1]))
            face.material_index = (k // 2) % 2
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    roof = bpy.data.objects.new("GazeboRoof", mesh)
    bpy.context.collection.objects.link(roof)
    mesh.materials.append(roof_a)
    mesh.materials.append(roof_b)
    lathe("GazeboRoof", (x, y, ring + 1.32), [(0.09, 0), (0.05, 0.1), (0.08, 0.2), (0.02, 0.5)], shade, segments=8)
    # Скамья внутри у дальней стены.
    box("GazeboBench", (x, y + 0.85, floor_top + 0.36), (1.5, 0.42, 0.07), wood, bevel=0.012)
    box("GazeboBench", (x, y + 1.05, floor_top + 0.43), (1.5, 0.06, 0.45), wood, bevel=0.012)
    for dx in (-0.62, 0.62):
        box("GazeboBench", (x + dx, y + 0.85, floor_top), (0.08, 0.36, 0.36), wood, bevel=0.01)
    # Плетущаяся роза на столбе у входа.
    px, py = tops[4]
    for k in range(9):
        z = floor_top + 0.18 + k * 0.23
        a = k * 1.15
        p = (px + math.cos(a) * 0.1, py + math.sin(a) * 0.1, z)
        size = 0.15 + rng.rand() * 0.05
        blob("GazeboLeaf", p, (size * 1.3, size * 1.1, size), leaf, 2)
        if k % 2 == 0:
            color, heart = rose[k % len(rose)]
            bloom((p[0] + 0.03, p[1] - size * 0.8, p[2] + 0.02), 0.09, color, heart, "GazeboRose")


def crown_wind(point, hinge, bend_at):
    """
    Ветер кроны зависит только от положения точки, поэтому соседние шары движутся вместе и не расходятся.
    С отдельной фазой у каждого шара открывались щели, а внутри запечено чёрное: туда не попадает свет.
    Фаза плавно меняется по кроне: волна ветра идёт через неё.
    """
    bend = min((point - hinge).length / bend_at, 1.0) * 0.55
    return (bend, (0.3 + (point.x + point.y) * 0.05) % 1.0, 0.0)


def lumpy_leaf(name, location, size, material, phase, anchor, bend_at):
    """Куст листвы: шар с буграми, силуэт рваный. Ветер — общий на всю крону (crown_wind)."""
    obj = blob(name, location, (size[0], size[1], size[2]), material, 3)
    mesh = obj.data
    for vertex in mesh.vertices:
        direction = vertex.co.normalized()
        bump = 1 + 0.11 * math.sin(direction.x * 7.3 + phase * 11) * math.sin(direction.y * 6.1) + 0.08 * math.sin(direction.z * 9.1 + direction.x * 4.3 + phase * 7) + 0.05 * math.sin(direction.x * 19 + direction.z * 17)
        vertex.co = direction * bump
    hinge = Vector(anchor)

    set_wind(obj, lambda point: crown_wind(point, hinge, bend_at))
    return obj


def apple_tree(x, y, bark, bark_dark, leaves, apple, rope, wood):
    """Яблоня: толстый изогнутый ствол, две ветви, низкая пышная крона с яблоками, качели на ветке. Крона в кадре партии."""
    trunk_top = (x - 0.3, y + 0.05, FLOOR + 2.05)
    tube("TreeTrunk", smooth_path([(x, y, FLOOR - 0.02), (x + 0.08, y, FLOOR + 0.8), (x - 0.12, y + 0.03, FLOOR + 1.5), trunk_top], 3), 0.26, bark, sides=9)
    lathe("TreeTrunk", (x, y, FLOOR), [(0.48, 0), (0.33, 0.1), (0.26, 0.42)], bark_dark, segments=9)
    # Боковая ветка влево: на ней висят качели.
    branch_end = (x - 2.25, y - 0.1, FLOOR + 2.32)
    tube("TreeBranch", smooth_path([(x - 0.12, y + 0.03, FLOOR + 1.62), (x - 1.0, y - 0.03, FLOOR + 2.15), branch_end], 4), 0.1, bark, sides=7)
    tube("TreeBranch", smooth_path([(x - 0.1, y, FLOOR + 1.65), (x + 0.75, y - 0.2, FLOOR + 2.2), (x + 1.2, y - 0.3, FLOOR + 2.4)], 3), 0.1, bark, sides=7)

    crown = ((-0.3, 0.1, 2.75, 1.38, 1.15, 0.95), (-1.5, -0.1, 2.6, 1.0, 0.9, 0.75), (0.85, -0.2, 2.6, 1.02, 0.95, 0.8), (-0.65, -0.65, 2.4, 0.85, 0.72, 0.62),
             (0.15, -0.72, 2.25, 0.8, 0.68, 0.55), (-2.15, -0.25, 2.5, 0.68, 0.6, 0.5), (1.55, -0.35, 2.4, 0.7, 0.62, 0.52), (-0.3, 0.55, 3.25, 0.9, 0.75, 0.6))
    hinge = (x - 0.3, y, FLOOR + 1.9)
    for k, (dx, dy, z, sx, sy, sz) in enumerate(crown):
        lumpy_leaf("SwayTree", (x + dx, y + dy, FLOOR + z), (sx, sy, sz), leaves[k % len(leaves)], k * 0.13, hinge, 2.6)
    # Яблоки снаружи кроны, больше спереди, куда смотрит камера.
    for k in range(14):
        dx, dy, z, sx, sy, sz = crown[k % len(crown)]
        a = rng.rand() * 6.283
        lift = rng.rand() * 1.15 - 0.7
        direction = Vector((math.cos(a) * 0.82, -abs(math.sin(a)) - 0.25, lift)).normalized()
        point = (x + dx + direction.x * sx * 0.96, y + dy + direction.y * sy * 0.96, FLOOR + z + direction.z * sz * 0.96)
        item = blob("SwayApple", point, (0.105, 0.105, 0.1), apple, 2)

        set_wind(item, lambda p: crown_wind(p, Vector(hinge), 2.6))
    # Качели: две верёвки и доска. Покачиваются.
    sx = x - 1.5
    top_z = FLOOR + 2.16
    seat_z = FLOOR + 0.55

    def swing_wind(p):
        return (min((top_z - p.z) / (top_z - seat_z), 1.0), 0.42, 0.0)

    for sign in (-1, 1):
        set_wind(pole("SwaySwing", (sx + sign * 0.3, y - 0.05, top_z), (sx + sign * 0.3, y - 0.05, seat_z), 0.018, rope, verts=4), swing_wind)
        torus("TreeBranch", (sx + sign * 0.3, y - 0.05, top_z + 0.05), 0.11, 0.018, rope, 8, 3, axis="X")
    set_wind(box("SwaySwing", (sx, y - 0.05, seat_z - 0.03), (0.78, 0.26, 0.05), wood, bevel=0.012), swing_wind)


def rose_bush(x, y, width, height, leaf, roses):
    """Куст роз у дорожки: тёмно-зелёная масса и крупные простые цветы. Читается пятнами цвета, а не мелочью."""
    count = max(int(width * 3.2), 2)
    for k in range(count):
        t = (k + 0.5) / count - 0.5
        px, py = x + t * width, y + (rng.rand() - 0.5) * 0.22
        size = height * (0.45 + rng.rand() * 0.2)
        blob("BedLeaf", (px, py, FLOOR + size * 0.82), (size * 0.98, size * 0.78, size * 0.92), leaf[k % len(leaf)], 2)
        for _ in range(3):
            a = rng.rand() * math.pi * 2
            rise = rng.rand() * 0.95 - 0.1
            p = (px + math.cos(a) * size * 0.68, py - abs(math.sin(a)) * size * 0.62 - 0.03, FLOOR + size * 0.82 + rise * size * 0.72)
            color, heart = roses[rng.randint(0, len(roses))]
            bloom(p, 0.11 + rng.rand() * 0.03, color, heart)


def flower_head(center, radius, material, spin, points=5, inner=0.5):
    """Шапка цветка плоской звездой: 10 граней вместо сотни, сверху не отличить от лепестков."""
    mesh = bpy.data.meshes.new("SwayFlower")
    bm = bmesh.new()
    hub = bm.verts.new((center[0], center[1], center[2] + radius * 0.14))
    ring = []
    for k in range(points * 2):
        a = spin + k * math.pi / points
        r = radius if k % 2 == 0 else radius * inner
        ring.append(bm.verts.new((center[0] + math.cos(a) * r, center[1] + math.sin(a) * r, center[2])))
    for k in range(points * 2):
        bm.faces.new((hub, ring[k], ring[(k + 1) % (points * 2)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("SwayFlower", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(material)
    return obj


def flower_clump(x, y, spread, count, stem, foliage, heads, center, height=0.55):
    """Группа цветов: низкая масса листвы, над ней стебли и крупные шапки из лепестков. Качается верх."""
    blob("BedLeaf", (x, y, FLOOR + height * 0.22), (spread * 1.05, spread * 0.95, height * 0.38), foliage, 2)
    for k in range(count):
        a = rng.rand() * 6.283
        r = spread * 0.85 * math.sqrt(rng.rand())
        px, py = x + math.cos(a) * r, y + math.sin(a) * r * 0.85
        h = height * (0.78 + rng.rand() * 0.42)
        phase = rng.rand()
        base = FLOOR + 0.05

        def wind(p, base=base, h=h, phase=phase):
            return (max(min((p.z - base) / h, 1.0), 0.0) ** 1.4 * 0.5, phase, 0.0)

        top = (px + (rng.rand() - 0.5) * 0.08, py, FLOOR + h)
        set_wind(pole("SwayFlower", (px, py, base), top, 0.014, stem, verts=3), wind)
        set_wind(flower_head(top, 0.125, heads[k % len(heads)], phase * 6), wind)
        set_wind(flower_head((top[0], top[1], top[2] + 0.012), 0.05, center, phase * 3, points=5, inner=1.0), wind)


def flower_bed(x, y, radius_x, radius_y, soil, stone):
    """Клумба: тёмная земля и каменный бордюр по эллипсу."""
    count = 20
    for k in range(count):
        a = k * 2 * math.pi / count
        px, py = x + math.cos(a) * radius_x, y + math.sin(a) * radius_y
        size = 0.13 + rng.rand() * 0.05
        rock = blob("BedStone", (px, py, FLOOR + size * 0.45), (size * 1.25, size, size * 0.7), stone, 1)
        rock.rotation_euler = (rng.rand() * 0.4, rng.rand() * 0.4, a)
    mesh = bpy.data.meshes.new("BedSoil")
    bm = bmesh.new()
    verts = [bm.verts.new((x + math.cos(k * 2 * math.pi / 22) * radius_x, y + math.sin(k * 2 * math.pi / 22) * radius_y, FLOOR + 0.04)) for k in range(22)]
    bm.faces.new(verts)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("BedSoil", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(soil)


def pond(x, y, stone, water, lily, flower, reed):
    """Прудик: каменный край, кувшинки, камыш, камень для лягушки. Рябь на воде — живая плоскость Zonk/Water от генератора."""
    rx, ry = 1.75, 1.2
    mesh = bpy.data.meshes.new("PondWater")
    bm = bmesh.new()
    verts = [bm.verts.new((x + math.cos(k * 2 * math.pi / 26) * rx, y + math.sin(k * 2 * math.pi / 26) * ry, FLOOR + 0.03)) for k in range(26)]
    bm.faces.new(verts)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("PondWater", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(water)
    for k in range(22):
        a = k * 2 * math.pi / 22
        size = 0.2 + rng.rand() * 0.11
        rock = blob("PondStone", (x + math.cos(a) * (rx + 0.08), y + math.sin(a) * (ry + 0.08), FLOOR + size * 0.4), (size * 1.25, size, size * 0.72), stone, 2)
        rock.rotation_euler = (rng.rand() * 0.4, rng.rand() * 0.4, a)
    for dx, dy, size, bloom in ((-0.6, -0.2, 0.3, True), (0.45, 0.28, 0.24, False), (0.75, -0.3, 0.2, True), (-0.1, 0.45, 0.2, False)):
        leaf_obj = cyl("PondLily", (x + dx, y + dy, FLOOR + 0.045), size, 0.012, lily, verts=12)
        leaf_obj.scale = (1, 0.82, 1)
        if bloom:
            blob("PondLily", (x + dx, y + dy, FLOOR + 0.09), (0.1, 0.1, 0.055), flower, 1)
            blob("PondLily", (x + dx, y + dy, FLOOR + 0.125), (0.04, 0.04, 0.025), mat("GardenLilyEye", "#f4cf45", 0.7), 1)
    for k in range(9):
        a = 2.2 + k * 0.13
        px, py = x + math.cos(a) * (rx - 0.05), y + math.sin(a) * (ry - 0.05)
        h = 0.75 + rng.rand() * 0.45
        phase = rng.rand()

        def wind(p, h=h, phase=phase):
            return (max(min((p.z - FLOOR) / h, 1.0), 0.0) ** 1.5 * 0.6, phase, 0.0)

        top = (px + (rng.rand() - 0.5) * 0.14, py, FLOOR + h)
        set_wind(pole("SwayReed", (px, py, FLOOR), top, 0.016, reed, verts=4), wind)
        if k % 3 == 0:
            set_wind(blob("SwayReed", (top[0], top[1], top[2] - 0.13), (0.035, 0.035, 0.11), mat("GardenCattail", "#694325", 0.9), 1), wind)
    # Камень для лягушки: сама она живая (zonk_garden_life.py), её добавляет генератор.
    fx, fy = x + rx + 0.06, y - 0.28
    blob("PondStone", (fx, fy, FLOOR + 0.17), (0.4, 0.34, 0.24), stone, 2)


def wheelbarrow(x, y, turn, wood, iron, soil, pumpkin, leaf):
    """Тачка с тыквами: крупно и понятно, у правого края кадра партии."""
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, z):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + z)

    def piece(center, size, material, bevel=0.012, tilt=0.0):
        obj = box("CartBody", at(*center), size, material, bevel=bevel)
        obj.rotation_euler = (tilt, 0, turn)
        return obj

    # Кузов сужается к колесу, борта наклонены наружу. Колесо спереди, две рукояти и две ножки сзади.
    bottom = [(-0.28, -0.42), (0.28, -0.42), (0.34, 0.52), (-0.34, 0.52)]
    top = [(-0.38, -0.56), (0.38, -0.56), (0.47, 0.6), (-0.47, 0.6)]
    z0, z1 = 0.4, 0.74
    bx = [at(px, py, z0) for px, py in bottom]
    tx = [at(px, py, z1) for px, py in top]
    tray = bpy.data.meshes.new("CartBody")
    bm = bmesh.new()
    low = [bm.verts.new(v) for v in bx]
    high = [bm.verts.new(v) for v in tx]
    bm.faces.new(low)
    for k in range(4):
        n = (k + 1) % 4
        bm.faces.new((low[k], low[n], high[n], high[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(tray)
    bm.free()
    tray_obj = bpy.data.objects.new("CartBody", tray)
    bpy.context.collection.objects.link(tray_obj)
    tray.materials.append(wood)
    tray_obj.modifiers.new("s", "SOLIDIFY").thickness = 0.04
    for k in range(4):
        pa, pb = tx[k], tx[(k + 1) % 4]
        pole("CartBody", pa, pb, 0.024, iron, verts=5)
    wheel_center = at(0, -0.92, 0.27)
    hub = cyl("CartWheel", wheel_center, 0.26, 0.09, wood, axis="X", verts=16)
    hub.rotation_euler = (0, math.pi / 2, turn)
    for k in range(3):
        a = k * math.pi / 3
        pole("CartWheel", at(math.cos(a) * 0.0, -0.92 + math.cos(a) * 0.25, 0.27 + math.sin(a) * 0.25), at(0, -0.92 - math.cos(a) * 0.25, 0.27 - math.sin(a) * 0.25), 0.015, iron, verts=4)
    for sign in (-1, 1):
        pole("CartFrame", at(sign * 0.2, -0.92, 0.27), at(sign * 0.3, 0.1, 0.42), 0.03, wood, verts=5)
        pole("CartFrame", at(sign * 0.3, 0.1, 0.42), at(sign * 0.38, 1.3, 0.72), 0.03, wood, verts=5)
        pole("CartFrame", at(sign * 0.32, 0.42, 0.44), at(sign * 0.34, 0.5, 0.0), 0.028, wood, verts=5)
        tube("CartFrame", [at(sign * 0.38, 1.28, 0.715), at(sign * 0.385, 1.45, 0.76)], 0.04, mat("GardenGrip", "#3b2a1c", 0.8), sides=6)
    blob("CartSoil", at(0, 0.02, 0.71), (0.36, 0.52, 0.1), soil, 2)
    for dx, dy, size, z in ((-0.14, 0.2, 0.2, 0.85), (0.14, -0.15, 0.24, 0.87), (-0.12, -0.28, 0.14, 0.82)):
        lathe("CartPumpkin", at(dx, dy, z - size * 0.72), [(0, 0), (size * 0.55, size * 0.1), (size, size * 0.55), (size, size * 0.95), (size * 0.5, size * 1.35), (0.02, size * 1.42)], pumpkin, segments=10)
        pole("CartPumpkin", at(dx, dy, z + size * 0.65), at(dx + 0.02, dy, z + size * 0.86), 0.022, leaf, verts=4)


def watering_can(x, y, zinc, dark):
    """Лейка рядом с клумбой: цилиндр, носик с ситечком, ручка дугой."""
    lathe("CartCan", (x, y, FLOOR), [(0.17, 0), (0.18, 0.04), (0.16, 0.36), (0.16, 0.38)], zinc, segments=12)
    tube("CartCan", smooth_path([(x + 0.13, y, FLOOR + 0.11), (x + 0.32, y, FLOOR + 0.24), (x + 0.44, y, FLOOR + 0.42)], 3), 0.032, zinc, sides=6)
    lathe("CartCan", (x + 0.45, y, FLOOR + 0.41), [(0.035, 0), (0.075, 0.08), (0.075, 0.1)], dark, segments=8)
    tube("CartCan", smooth_path([(x - 0.12, y, FLOOR + 0.36), (x, y, FLOOR + 0.55), (x + 0.12, y, FLOOR + 0.36)], 4), 0.016, zinc, sides=4)


def laundry(x0, x1, y, wood, rope, cloths):
    """Верёвка с бельём: цветные пятна, которые качаются на ветру. Живое на заднем плане партии."""
    high = FLOOR + 1.95
    for x in (x0, x1):
        pole("LinePost", (x, y, FLOOR), (x, y, high + 0.1), 0.055, wood, verts=7)
        pole("LinePost", (x - 0.22, y, high - 0.1), (x + 0.22, y, high - 0.1), 0.03, wood, verts=5)
    sag = 0.17
    line = [(x0 + (x1 - x0) * t, y, high - 0.08 - sag * (1 - (2 * t - 1) ** 2)) for t in (k / 10 for k in range(11))]
    tube("LinePost", line, 0.012, rope, sides=4)

    def line_z(x):
        t = (x - x0) / (x1 - x0)
        return high - 0.08 - sag * (1 - (2 * t - 1) ** 2)

    # Кусок ткани: сетка, край волной, верх закреплён на верёвке. Ветер — от верха вниз, волна по ширине.
    def cloth(cx, width, height, material, phase):
        mesh = bpy.data.meshes.new("SwayCloth")
        bm = bmesh.new()
        wind_layer = bm.loops.layers.float_color.new("Wind")
        columns, rows = 5, 4
        grid = []
        for c in range(columns + 1):
            u = c / columns
            px = cx - width / 2 + width * u
            top = line_z(px)
            grid.append([bm.verts.new((px, y + math.sin(u * 7 + phase * 5) * 0.05 * (r / rows), top - height * r / rows)) for r in range(rows + 1)])
        for c in range(columns):
            for r in range(rows):
                face = bm.faces.new((grid[c][r], grid[c + 1][r], grid[c + 1][r + 1], grid[c][r + 1]))
                for loop, (cc, rr) in zip(face.loops, ((c, r), (c + 1, r), (c + 1, r + 1), (c, r + 1))):
                    loop[wind_layer] = ((rr / rows) ** 1.2, (phase + cc * 0.09) % 1.0, 0.3 * rr / rows, 1.0)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new("SwayCloth", mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(material)
        for dx in (-width / 2 + 0.05, width / 2 - 0.05):
            box("LinePeg", (cx + dx, y, line_z(cx + dx) - 0.05), (0.03, 0.04, 0.09), wood, bevel=0)

    for cx, width, height, material, phase in cloths:
        cloth(cx, width, height, material, phase)


def far_world(grass, hill_a, hill_b, trees):
    """Даль кругом: плоская трава до неба, два кольца холмов, шары деревьев. За оградой мир продолжается."""
    far = bpy.data.meshes.new("FarGrass")
    bm = bmesh.new()
    verts = [bm.verts.new((math.cos(k * 2 * math.pi / 24) * 62, math.sin(k * 2 * math.pi / 24) * 62, FLOOR - 0.03)) for k in range(24)]
    bm.faces.new(verts)
    bm.to_mesh(far)
    bm.free()
    obj = bpy.data.objects.new("FarGrass", far)
    bpy.context.collection.objects.link(obj)
    far.materials.append(grass)

    def ring(name, inner, outer, height, material, seed):
        steps = 40
        rows = []
        for k in range(steps + 1):
            a = 2 * math.pi * k / steps
            wave = 0.55 + 0.3 * math.sin(a * 3 + seed) + 0.15 * math.sin(a * 7 + seed * 2)
            top = height * wave
            mid = (inner + outer) / 2
            rows.append([(math.cos(a) * inner, math.sin(a) * inner, FLOOR), (math.cos(a) * mid, math.sin(a) * mid, FLOOR + top * 0.75), (math.cos(a) * outer, math.sin(a) * outer, FLOOR + top)])
        # Запекается и видна только лицевая сторона: грани наружу вверх, к камере.
        return strip(name, rows, material, facing=(0, 0, 1))

    for hill in (ring("FarHill", 30, 40, 3.4, hill_a, 1.2), ring("FarHill", 42, 54, 6.5, hill_b, 2.6)):
        for polygon in hill.data.polygons:
            polygon.use_smooth = True
    # Деревья группами на дальнем плане: шар на шаре, небольшие, чтобы даль читалась далью.
    placed = 0
    while placed < 14:
        a = rng.rand() * 2 * math.pi
        r = 24 + rng.rand() * 7
        px, py = math.cos(a) * r, math.sin(a) * r
        if py < 2:
            continue
        placed += 1
        size = 0.85 + rng.rand() * 0.8
        material = trees[placed % len(trees)]
        pole("FarTree", (px, py, FLOOR), (px, py, FLOOR + size * 1.2), 0.11, mat("GardenFarBark", "#6a4c33", 0.9), verts=5)
        blob("FarTree", (px, py, FLOOR + size * 1.85), (size, size, size * 1.05), material, 2)
        blob("FarTree", (px + size * 0.6, py, FLOOR + size * 1.45), (size * 0.72, size * 0.72, size * 0.7), material, 1)
        blob("FarTree", (px - size * 0.58, py, FLOOR + size * 1.5), (size * 0.68, size * 0.68, size * 0.66), material, 1)


def garden():
    zs.sky_dome(sky_image(), moon=False)
    ground("Floor", GROUND[0], GROUND[1], GROUND[2], GROUND[3], lawn_image())

    white = mat("GardenWhite", "#f2efe4", 0.8)
    white_shade = mat("GardenShade", "#b7b9a8", 0.8)
    wood = mat("GardenWood", "#9a6a3b", 0.8)
    bark = mat("GardenBark", "#6d4a2c", 0.95)
    bark_dark = mat("GardenBarkDark", "#4f331d", 0.95)
    iron = mat("GardenIron", "#37393d", 0.6)
    stone = mat("GardenStone", "#a79e8d", 0.95)
    soil = mat("GardenSoil", "#533824", 1.0)
    rope = mat("GardenRope", "#d4bf8a", 0.95)
    leaf_a = dab_mat("GardenLeaf", "#347f3c", "#55a447")
    leaf_b = dab_mat("GardenLeaf2", "#27693a", "#3f8c42")
    leaf_c = dab_mat("GardenLeaf3", "#44923c", "#73ba4c")
    stem = mat("GardenStem", "#4f9a3c", 0.8)
    rose_red = mat("GardenRoseRed", "#d4293a", 0.5)
    rose_pink = mat("GardenRosePink", "#ee6f94", 0.5)
    rose_white = mat("GardenRoseWhite", "#fbeee4", 0.5)
    heart_red = mat("GardenRoseRedD", "#8f1626", 0.6)
    heart_pink = mat("GardenRosePinkD", "#c33e6b", 0.6)
    heart_white = mat("GardenRoseWhiteD", "#e7bdb3", 0.6)
    roses = ((rose_red, heart_red), (rose_pink, heart_pink), (rose_white, heart_white))
    apple = mat("GardenApple", "#d8332b", 0.45)
    pumpkin = mat("GardenPumpkin", "#e8872b", 0.6)
    water = mat("GardenWater", "#5cc6d6", 0.1)
    lily = mat("GardenLily", "#4ba34b", 0.6)
    reed = mat("GardenReed", "#6d9a43", 0.8)
    daisy = mat("GardenDaisy", "#fdfbf0", 0.6)
    tulip = mat("GardenTulip", "#f29c1f", 0.6)
    violet = mat("GardenViolet", "#8e5bd0", 0.6)
    yellow = mat("GardenYellow", "#f7cb2b", 0.6)
    roof_a = mat("GardenRoofA", "#c8473d", 0.7)
    roof_b = mat("GardenRoofB", "#efe5d0", 0.7)

    # Ограда по кругу заднего плана, с разрывом под калитку.
    picket_fence(-10.0, GATE_X - 0.8, FENCE_Y, white, white_shade)
    picket_fence(GATE_X + 0.8, 10.5, FENCE_Y, white, white_shade)
    gate(GATE_X, FENCE_Y, white, white_shade, iron, leaf_b, roses)

    gazebo(-5.45, 4.2, white, white_shade, roof_a, roof_b, wood, leaf_b, roses)
    apple_tree(4.5, 4.55, bark, bark_dark, (leaf_a, leaf_b, leaf_c), apple, rope, wood)

    # Розы вдоль ограды и у дорожки: графиня разводит розы, это главное в сцене.
    rose_bush(-1.85, 6.05, 2.7, 0.95, (leaf_a, leaf_b), roses)
    rose_bush(3.4, 6.15, 2.7, 0.95, (leaf_a, leaf_b), roses[:2])
    rose_bush(-7.1, -2.35, 2.6, 0.9, (leaf_a, leaf_b), (roses[0], roses[2]))

    # Правая клумба с цветами, тачка, лейка. Левый передний план — пруд с лягушкой.
    flower_bed(6.2, 0.55, 1.45, 1.75, soil, stone)
    foliage = dab_mat("GardenFoliage", "#37843a", "#62ad45", 9.0)
    for cx, cy, spread, count, heads, height in (
            (5.55, -0.45, 0.62, 6, (tulip, yellow), 0.5), (6.75, -0.5, 0.58, 6, (violet, daisy), 0.48),
            (5.2, 0.55, 0.6, 6, (rose_red, tulip), 0.62), (6.25, 0.45, 0.68, 7, (daisy, yellow), 0.68), (7.25, 0.65, 0.5, 5, (violet, tulip), 0.6),
            (5.6, 1.55, 0.62, 6, (violet, rose_pink), 0.82), (6.75, 1.6, 0.62, 6, (tulip, daisy), 0.86)):
        flower_clump(cx, cy, spread, count, stem, foliage, heads, yellow, height)
    wheelbarrow(4.6, -2.75, math.radians(-68), wood, iron, soil, pumpkin, stem)
    watering_can(5.15, -1.85, mat("GardenZinc", "#9db1b7", 0.45), iron)
    pond(-6.9, 0.65, stone, water, lily, daisy, reed)
    laundry(-1.45, 2.45, 7.75, wood, rope, ((-0.75, 0.95, 1.05, mat("GardenClothBlue", "#5aa4dc", 0.9), 0.15), (0.5, 0.8, 0.82, mat("GardenClothCream", "#f6ecd7", 0.9), 0.5), (1.65, 0.86, 1.0, mat("GardenClothRed", "#e16358", 0.9), 0.8)))

    far_world(kit.image_mat("GardenFarGrass", np.zeros((4, 4, 3)) + np.array(GRASS), rough=0.9), mat("GardenHill", "#6aa64a", 1.0), mat("GardenHillFar", "#a6cd9b", 1.0), (mat("GardenFarLeaf", "#4b955a", 0.9), mat("GardenFarLeaf2", "#67ac62", 0.9)))


def lights():
    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.7
    sun.color = (1.0, 0.94, 0.8)
    sun.angle = math.radians(3)
    sun_obj = bpy.data.objects.new("Sun", sun)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = (math.radians(50), 0, math.radians(-42))
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.8, 1.0, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.85
    bpy.context.scene.world = world


def report_triangles():
    """Сколько весит каждый вид предметов: по нему видно, что упрощать."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    totals = {}
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph).to_mesh()
        evaluated.calc_loop_triangles()
        key = obj.name.split(".")[0]
        totals[key] = totals.get(key, 0) + len(evaluated.loop_triangles)
        obj.evaluated_get(depsgraph).to_mesh_clear()
    for key, value in sorted(totals.items(), key=lambda item: -item[1])[:16]:
        print("[zonk_garden]", key, value)
    print("[zonk_garden] total", sum(totals.values()))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        # Имя сетки = имя группы: по нему генератор (ZonkSetup.Garden) выбирает материал. Газон — Floor, текстура 2048.
        # Листве свои текстуры: в общей ей не хватало места, мазки размывались и были видны стыки.
        # Дальняя трава отдельно от деревьев: огромные грани забирали почти всю текстуру, деревьям оставались чёрные пятна.
        kit.UV_ANGLE = 80
        kit.UV_MARGIN = 0.01
        kit.BAKE_MARGIN_TYPE = "EXTEND"
        texture_groups = (
            ("Crown", 1024, lambda name: name.startswith(("SwayTree", "SwayApple"))),
            ("Bush", 1024, lambda name: name.startswith(("BedLeaf", "BedRose", "GateLeaf", "GateRose", "GazeboLeaf", "GazeboRose"))),
            ("Arbor", 1024, lambda name: name.startswith(("Gazebo", "Gate", "Fence", "Line"))),
            ("Beds", 1024, lambda name: name.startswith(("Bed", "Cart", "Pond", "Tree", "FarTree"))),
            ("Far", 128, lambda name: name.startswith("Far")),
            ("Rig", 512, lambda name: name.startswith("Sway")),
        )

        def build():
            garden()
            bc.matte()

        views = (("sad_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("sad_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42), ("sad_igra_sopernik.png", (0, 1.55, -1.4), (0, 0.72, 2.9), 45))
        os.makedirs(argv[argv.index("--bake") + 1], exist_ok=True)
        kit.bake_location("Garden", "Room_Garden.fbx", build, lights, views, samples, argv[argv.index("--bake") + 1], groups=texture_groups)
        return

    folder = argv[argv.index("--render") + 1]
    os.makedirs(folder, exist_ok=True)
    garden()
    bc.matte()
    report_triangles()
    kit.table()
    lights()
    kit.render(os.path.join(folder, "sad_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "sad_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)
    kit.render(os.path.join(folder, "sad_magazin.png"), (5.2, 3.3, -8.4), (0, -1.3, 1.2), 50, samples)
    kit.render(os.path.join(folder, "sad_sopernik.png"), (0, 1.55, -1.4), (0, 0.72, 2.9), 45, samples)


if __name__ == "__main__":
    main()
