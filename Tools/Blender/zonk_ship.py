# -*- coding: utf-8 -*-
"""
Локация «Корабль», глава 4: палуба пиратского корабля штормовой ночью. Холодная луна и море за бортом, тёплые фонари и окна каюты.
Настроение как у таверны: тёмное и плотное, но открытое небо. Дождь, молнии и туман — из профиля Lighting_Ship.
Море здесь только для снимков: в игре живой шейдер Zonk/Water добавит генератор.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_ship.py -- --render <папка> [--samples 48]
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, палуба на -1.6.
Рука соперника лежит на x −2.2, z 2.3 (рукав до z 3.5): там выше стола ничего нет. Мачта не за центром: иначе растёт из головы соперника.
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
import zonk_tavern as tv  # noqa: E402

from zonk_beach import box, ground, pole  # noqa: E402
from zonk_room import FLOOR, blob, cyl, image_mat, mat  # noqa: E402
from zonk_tavern import barrel, barrel_image, lathe, torus  # noqa: E402

FRONT = -9.5
BACK = 6.6
STERN = 12.0
SIDE = 6.3
RAIL = FLOOR + 1.15
MAST = (2.5, 5.0)
STAIR_X = -1.95
# Цвет дали моря: тот же в небе у горизонта и в материале M_Water_Storm (ZonkSetup.Ship), поэтому шва нет.
SEA_FAR = (0.07, 0.17, 0.25)
MOON_U, MOON_V = 0.417, 0.205
rng = np.random.RandomState(4)


# ---------- Картинки ----------

def deck_image():
    """Палуба: доски вдоль корабля, ровные тона, тёмные щели, стыки вразбежку, шляпки гвоздей, мокрые пятна."""
    size = 2048
    local = np.random.RandomState(14)
    image = np.zeros((size, size, 3))
    count = 28
    width = size // count
    for i in range(count + 1):
        x0, x1 = i * width, min((i + 1) * width, size)
        y = -local.randint(0, 500)
        while y < size:
            length = local.randint(420, 760)
            tone = 0.56 + local.rand() * 0.12
            color = np.array([tone, tone * 0.70, tone * 0.46])
            y0, y1 = max(y, 0), min(y + length, size)
            image[y0:y1, x0:x1] = color
            for _ in range(3):
                gx = local.randint(x0 + 6, max(x0 + 7, x1 - 4))
                image[y0:y1, gx:gx + 2] = color * 0.86
            if y1 < size:
                image[y1 - 4:y1, x0:x1] = (0.06, 0.04, 0.03)
                for nx in (x0 + 12, x1 - 14):
                    image[y1 - 20:y1 - 13, nx:nx + 7] = (0.09, 0.08, 0.08)
            y += length
        image[:, x0:x0 + 5] = (0.05, 0.035, 0.025)
    yy, xx = np.mgrid[0:size, 0:size] / size
    wet = np.sin(xx * 13 + np.sin(yy * 7) * 2.1) + np.sin(yy * 17 + np.sin(xx * 11) * 2.3)
    image[wet > 1.25] *= 0.8
    return image


def cloud_noise(width, height, seed, stretch, roughness=1.7):
    """Шум для туч: шире, чем выше, и бесшовный по кругу (спектр по периодичной сетке). Значения 0..1."""
    local = np.random.RandomState(seed)
    spectrum = np.fft.fft2(local.rand(height, width))
    fx = np.fft.fftfreq(width) * width
    fy = np.fft.fftfreq(height) * height
    radius = np.sqrt((fx[None, :] * stretch) ** 2 + fy[:, None] ** 2)
    radius[0, 0] = 1
    spectrum = spectrum / radius ** roughness
    spectrum[0, 0] = 0
    field = np.real(np.fft.ifft2(spectrum))
    low, high = np.percentile(field, 1), np.percentile(field, 99)
    return np.clip((field - low) / (high - low), 0, 1)


def smooth(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)


def sky_image():
    """
    Панорама штормовой ночи на круг 360°: бесшовная, без горизонтальной кромки. Внизу цвет дали моря (M_Water_Storm, _Far),
    выше сплошные рваные тучи двумя тонами, к луне края светлеют. Сама луна — отдельный диск (sky_dome).
    """
    height, width = 512, 1024
    y, x = np.mgrid[0:height, 0:width]
    u, v = x / width, 1 - y / height
    horizon = np.array(SEA_FAR)
    mid = np.array([0.10, 0.15, 0.25])
    top = np.array([0.045, 0.06, 0.12])
    a = smooth(0.0, 0.2, v)[..., None]
    b = smooth(0.2, 0.75, v)[..., None]
    image = (horizon * (1 - a) + mid * a) * (1 - b) + top * b

    # Свет луны в тучах: ореол на панораме, расстояние с учётом круга.
    du = np.abs(u - MOON_U)
    du = np.minimum(du, 1 - du) * (width / height)
    glow = np.clip(1 - np.sqrt(du ** 2 + ((v - MOON_V) * 1.5) ** 2) / 0.62, 0, 1)[..., None] ** 2

    far = cloud_noise(width, height, 21, 1.5, 2.0)
    near = cloud_noise(width, height, 7, 1.35, 2.0)
    lit = cloud_noise(width, height, 33, 1.5, 1.8)
    # Тучи начинаются чуть выше горизонта: снизу они растворяются в его цвете, линии нет.
    rise = smooth(0.03, 0.2, v)
    layer = smooth(0.34, 0.46, far)[..., None] * rise[..., None]
    image = image * (1 - layer * 0.8) + np.array([0.14, 0.17, 0.26]) * layer * 0.8
    edge = (smooth(0.5, 0.58, lit) * (1 - smooth(0.62, 0.74, lit)))[..., None] * rise[..., None]
    image = image + np.array([0.11, 0.13, 0.17]) * edge * 0.55
    heavy = smooth(0.46, 0.57, near)[..., None] * rise[..., None]
    image = image * (1 - heavy * 0.85) + np.array([0.05, 0.065, 0.115]) * heavy * 0.85
    image = image + np.array([0.3, 0.35, 0.42]) * glow * (0.35 + 0.65 * (1 - heavy))
    return np.clip(image, 0, 1)


def sky_dome(pixels):
    """
    Небо — круг вокруг корабля, а не три стены: стыков нет. Внизу уходит за край моря. Луна — диск перед ним, резкая при любой текстуре неба.
    """
    kit.SKY_PIXELS = pixels
    segments, radius = 32, 60.0
    z0, z1 = -3.7, 46.0
    mesh = bpy.data.meshes.new("SkyBack")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    for k in range(segments + 1):
        angle = 2 * math.pi * (k / segments - 0.5)
        rows.append([bm.verts.new((math.sin(angle) * radius, math.cos(angle) * radius, z)) for z in (z0, z1)])
    for k in range(segments):
        face = bm.faces.new((rows[k][0], rows[k][1], rows[k + 1][1], rows[k + 1][0]))
        face.normal_update()
        if face.normal.dot(face.calc_center_median()) > 0:
            bmesh.ops.reverse_faces(bm, faces=[face])
        for loop in face.loops:
            column = k if loop.vert in rows[k] else k + 1
            loop[uv].uv = (column / segments, 0.0 if loop.vert.co.z < 0 else 1.0)
    bm.to_mesh(mesh)
    bm.free()
    material = bpy.data.materials.new("Sky")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    h, w = pixels.shape[:2]
    image = bpy.data.images.new("SkyImage", w, h, alpha=False)
    linear = np.where(pixels <= 0.04045, pixels / 12.92, ((pixels + 0.055) / 1.055) ** 2.4)
    image.pixels.foreach_set(np.flipud(np.dstack([linear, np.ones((h, w))])).astype(np.float32).ravel())
    image.pack()
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    emit = nodes.new("ShaderNodeEmission")
    out = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(tex.outputs[0], emit.inputs[0])
    material.node_tree.links.new(emit.outputs[0], out.inputs[0])
    sky = bpy.data.objects.new("SkyBack", mesh)
    bpy.context.collection.objects.link(sky)
    mesh.materials.append(material)
    sky.visible_shadow = False

    # Луна: круг и кратеры, смотрят на корабль. Угол и высота совпадают с ореолом на панораме.
    angle = 2 * math.pi * (MOON_U - 0.5)
    center = Vector((math.sin(angle) * (radius - 4), math.cos(angle) * (radius - 4), z0 + MOON_V * (z1 - z0)))
    toward = (Vector((0, 0, 3)) - center).normalized()
    side = toward.cross(Vector((0, 0, 1))).normalized()
    up = side.cross(toward).normalized()

    def disc(name, offset, size, material_disc, lift):
        disc_mesh = bpy.data.meshes.new(name)
        disc_bm = bmesh.new()
        middle = center + side * offset[0] + up * offset[1] + toward * lift
        ring = [disc_bm.verts.new(middle + (side * math.cos(i * 2 * math.pi / 28) + up * math.sin(i * 2 * math.pi / 28)) * size) for i in range(28)]
        face = disc_bm.faces.new(ring)
        face.normal_update()
        if face.normal.dot(toward) < 0:
            bmesh.ops.reverse_faces(disc_bm, faces=[face])
        disc_bm.to_mesh(disc_mesh)
        disc_bm.free()
        obj = bpy.data.objects.new(name, disc_mesh)
        bpy.context.collection.objects.link(obj)
        disc_mesh.materials.append(material_disc)
        obj.visible_shadow = False
        return obj

    pale = mat("ShipMoon", "#dfe6e8", 1, emit=1.0)
    shade = mat("ShipMoonShade", "#aebdc6", 1, emit=1.0)
    disc("SkyMoon", (0, 0), 3.3, pale, 0.0)
    for offset, size in (((0.9, 0.8), 0.72), ((-1.2, -0.4), 0.5), ((0.4, -1.5), 0.34), ((-0.5, 1.5), 0.28)):
        disc("SkyMoon", offset, size, shade, 0.05)


# ---------- Детали ----------

def smooth_path(points, per_segment=4):
    """Плавная линия через точки (Катмулл-Ром)."""
    pts = [Vector(p) for p in points]
    pts = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for k in range(per_segment):
            t = k / per_segment
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t))
    out.append(pts[-2])
    return out


def tube(name, points, radius, material, sides=6):
    """Труба через точки: канат, поручень, цепь."""
    points = [Vector(p) for p in points]
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    side = None
    rings = []
    for i, point in enumerate(points):
        tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        side = tangent.orthogonal().normalized() if side is None else (side - tangent * side.dot(tangent)).normalized()
        other = tangent.cross(side)
        rings.append([bm.verts.new(point + (side * math.cos(k * 2 * math.pi / sides) + other * math.sin(k * 2 * math.pi / sides)) * radius) for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            n = (k + 1) % sides
            bm.faces.new((a[k], a[n], b[n], b[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def set_wind(obj, wind):
    """Цвет вершин для Zonk/BakedSway: wind(точка в мире) -> (сколько гнётся, фаза, дрожь)."""
    bpy.context.view_layer.update()
    mesh = obj.data
    layer = mesh.color_attributes.new("Wind", "FLOAT_COLOR", "CORNER")
    matrix = obj.matrix_world
    for loop in mesh.loops:
        r, g, b = wind(matrix @ mesh.vertices[loop.vertex_index].co)
        layer.data[loop.index].color = (r, g, b, 1.0)


def half_width(y):
    """Обвод корпуса: шире всего у стола, к корме и носу сужается. Прямые борта и выглядели декорацией."""
    stern = max(0.0, (y - 0.5) / (STERN - 0.5))
    bow = max(0.0, (-3.0 - y) / 9.5)
    return SIDE - 1.9 * stern ** 2 - 1.3 * bow ** 2


def rail_height(y):
    """Седловатость: планширь ниже всего посередине, к корме и носу поднимается."""
    stern = max(0.0, (y - 0.5) / (STERN - 0.5))
    bow = max(0.0, (-3.0 - y) / 9.5)
    return RAIL + 0.75 * stern ** 2 + 0.5 * bow ** 2


def strip(name, sections, material, world_tile=None, facing=None):
    """
    Лента по обводу: sections — списки точек поперечного сечения вдоль корпуса.
    facing — куда смотрят грани у открытой ленты. Запекается и видно только лицевую сторону (Zonk/Baked), поэтому внутрь сцены.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rows = [[bm.verts.new(p) for p in section] for section in sections]
    for a, b in zip(rows, rows[1:]):
        for k in range(len(a) - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if facing is not None:
        bm.normal_update()
        wrong = [face for face in bm.faces if face.normal.dot(Vector(facing)) < 0]
        if wrong:
            bmesh.ops.reverse_faces(bm, faces=wrong)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    if world_tile:
        tv.world_uv(obj, world_tile)
    return obj


def bulwark(sign, boards, wood, iron):
    """Борт по обводу: выгнутая стена из досок, стойки с шагом, широкий планширь. Идёт дальше каюты, к корме."""
    samples = [FRONT + (STERN - FRONT) * k / 22 for k in range(23)]
    strip("HullWall", [[(sign * half_width(y), y, FLOOR), (sign * half_width(y), y, rail_height(y))] for y in samples], boards, 0.9, facing=(-sign, 0, 0))
    rail = [[(sign * (half_width(y) - 0.2), y, rail_height(y)), (sign * (half_width(y) + 0.14), y, rail_height(y)),
             (sign * (half_width(y) + 0.14), y, rail_height(y) + 0.09), (sign * (half_width(y) - 0.2), y, rail_height(y) + 0.09), (sign * (half_width(y) - 0.2), y, rail_height(y))] for y in samples]
    strip("HullRail", rail, wood)
    y = FRONT + 0.8
    while y < BACK:
        box("HullPost", (sign * (half_width(y) - 0.08), y, FLOOR), (0.14, 0.16, rail_height(y) - FLOOR), wood, bevel=0.015)
        y += 2.35


def deck_floor(pixels):
    """Палуба по обводу корпуса, а не прямоугольник. Рисунок лежит по координатам: доски не тянутся."""
    y0, y1 = FRONT - 3, BACK + 0.1
    rows = [y0 + (y1 - y0) * k / 22 for k in range(23)]
    mesh = bpy.data.meshes.new("Floor")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = [[bm.verts.new((sign * half_width(y), y, FLOOR)) for sign in (-1, 1)] for y in rows]
    for r in range(len(rows) - 1):
        face = bm.faces.new((grid[r][0], grid[r][1], grid[r + 1][1], grid[r + 1][0]))
        for loop in face.loops:
            loop[uv].uv = (loop.vert.co.x / (2 * SIDE) + 0.5, (loop.vert.co.y - y0) / (y1 - y0))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Floor", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(image_mat("FloorTex", pixels, rough=0.9))
    return obj


def stairs(x, wood, dark, brass, rope):
    """
    Трап на верхнюю палубу: тетивы из досок, точёные столбики, перила дугой с завитком внизу, медные шары на столбах,
    канатная обмотка там, где берутся рукой. Корабль продолжается наверх.
    """
    width, height, run, steps = 1.0, 2.3, 1.75, 9
    y_top, y_low = BACK - 0.1, BACK - 0.1 - run

    def rise(y):
        return FLOOR + height * (y - y_low) / run

    for k in range(steps):
        t = (k + 1) / steps
        y = y_low + run * t
        box("StairStep", (x, y, rise(y) - 0.07), (width, 0.3, 0.07), wood, bevel=0.01)
        # Окантовка края ступени: тёмная полоса ловит свет.
        box("StairStep", (x, y - 0.15, rise(y) - 0.095), (width, 0.035, 0.05), dark, bevel=0)
    for sign in (-1, 1):
        px = x + sign * (width / 2 + 0.05)
        for dz in (0.0, 0.11):
            pole("StairRail", (px, y_low - 0.05, FLOOR + dz), (px, y_top, FLOOR + height + dz), 0.05, dark, verts=6)
        # Точёные столбики: низ и верх большие, с медным шаром; между ними тонкие.
        post = [(0.085, 0), (0.05, 0.1), (0.075, 0.3), (0.042, 0.52), (0.07, 0.72), (0.045, 0.92), (0.08, 1.0), (0.05, 1.03)]
        lathe("StairRail", (px, y_low + 0.02, FLOOR), post, wood, segments=8)
        blob("StairBrass", (px, y_low + 0.02, FLOOR + 1.1), (0.075, 0.075, 0.075), brass, 2)
        lathe("StairRail", (px, y_top, FLOOR + height), post, wood, segments=8)
        blob("StairBrass", (px, y_top, FLOOR + height + 1.1), (0.075, 0.075, 0.075), brass, 2)
        for k in range(1, 5):
            y = y_low + run * k / 5
            lathe("StairRail", (px, y, rise(y) + 0.1), [(0.03, 0), (0.05, 0.12), (0.024, 0.42), (0.045, 0.74), (0.03, 0.82)], dark, segments=6)
        # Перила: дуга над ступенями, внизу завиток наружу.
        rail = [(px, y_top + 0.02, FLOOR + height + 0.98), (px, y_top - run * 0.5, rise(y_top - run * 0.5) + 0.98), (px, y_low + 0.1, FLOOR + 1.02),
                (px + sign * 0.03, y_low - 0.13, FLOOR + 0.92), (px + sign * 0.12, y_low - 0.22, FLOOR + 0.8), (px + sign * 0.2, y_low - 0.15, FLOOR + 0.74),
                (px + sign * 0.2, y_low - 0.05, FLOOR + 0.8), (px + sign * 0.12, y_low - 0.07, FLOOR + 0.86)]
        tube("StairRail", smooth_path(rail, 5), 0.036, wood, sides=6)
        # Обмотка каната посередине перил.
        for k in range(7):
            y = y_low + run * (0.42 + k * 0.03)
            torus("StairRope", (px, y, rise(y) + 0.985), 0.042, 0.012, rope, 8, 3, axis="Y")


def flag_pixels():
    """Весёлый Роджер: череп и кости на чёрном, плоскими пятнами."""
    h, w = 256, 384
    y, x = np.mgrid[0:h, 0:w]
    image = np.zeros((h, w, 3)) + np.array([0.055, 0.05, 0.065])
    bone = np.array([0.92, 0.88, 0.76])
    cx, cy = w * 0.5, h * 0.42
    for sign in (-1, 1):
        line = np.abs((y - (h * 0.72)) - sign * (x - cx) * 0.45)
        image[(line < 9) & (np.abs(x - cx) < 130)] = bone
        for tip in (-1, 1):
            tx, ty = cx + tip * 130, h * 0.72 + sign * tip * 130 * 0.45
            image[((x - tx) ** 2 + (y - ty) ** 2) < 15 ** 2] = bone
    image[((x - cx) / 62) ** 2 + ((y - cy) / 56) ** 2 < 1] = bone
    image[(np.abs(x - cx) < 34) & (y > cy + 30) & (y < cy + 76)] = bone
    dark = (0.05, 0.05, 0.06)
    for sign in (-1, 1):
        image[((x - cx - sign * 25) / 16) ** 2 + ((y - cy + 2) / 18) ** 2 < 1] = dark
        image[(np.abs(x - cx - sign * 11) < 3) & (y > cy + 44) & (y < cy + 76)] = dark
    image[((x - cx) / 9) ** 2 + ((y - cy - 24) / 12) ** 2 < 1] = dark
    return image


def waving_flag(x, y, top, pixels, length=1.65, height=1.05):
    """Флаг: сетка 8×3 со своей развёрткой. Ветер в цвете вершин: от древка гнётся сильнее, волна бежит вдоль полотнища."""
    mesh = bpy.data.meshes.new("SwayFlag")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    wind = bm.loops.layers.float_color.new("Wind")
    columns, rows = 8, 3
    grid = []
    for c in range(columns + 1):
        t = c / columns
        grid.append([bm.verts.new((x + length * t, y + math.sin(t * 7.5) * 0.17 * t ** 0.6, top - height * r / rows - 0.14 * t * t + 0.04 * math.sin(t * 5))) for r in range(rows + 1)])
    for c in range(columns):
        for r in range(rows):
            face = bm.faces.new((grid[c][r], grid[c + 1][r], grid[c + 1][r + 1], grid[c][r + 1]))
            for loop, (cc, rr) in zip(face.loops, ((c, r), (c + 1, r), (c + 1, r + 1), (c, r + 1))):
                loop[uv].uv = (cc / columns, 1 - rr / rows)
                t = cc / columns
                loop[wind] = (t ** 0.8, (t * 0.75) % 1.0, 0.5 * t, 1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("SwayFlag", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(image_mat("ShipFlagTex", pixels, rough=0.95))
    return obj


def hanging_lantern(x, y, top, iron, glass, phase):
    """Фонарь на цепи качается: чем дальше от точки подвеса, тем сильнее."""
    hinge = Vector((x, y, top))
    z = top - 0.42

    def wind(point):
        return (min((hinge - point).length / 0.6, 1.0), phase, 0.0)

    parts = [lathe("SwayLantern", (x, y, z - 0.13), [(0.11, 0), (0.11, 0.26)], glass, segments=8),
             lathe("SwayLantern", (x, y, z + 0.13), [(0.15, 0), (0.09, 0.08), (0.03, 0.14)], iron, segments=8),
             cyl("SwayLantern", (x, y, z - 0.15), 0.13, 0.05, iron, verts=8),
             pole("SwayLantern", (x, y, z + 0.26), (x, y, top), 0.012, iron, verts=3)]
    for a in (0.8, 2.4, 3.9, 5.5):
        parts.append(pole("SwayLantern", (x + math.cos(a) * 0.11, y + math.sin(a) * 0.11, z - 0.13), (x + math.cos(a) * 0.11, y + math.sin(a) * 0.11, z + 0.13), 0.01, iron, verts=3))
    for part in parts:
        set_wind(part, wind)


def stern_deck(wood, dark, iron, brass, rope, canvas, deck_mat, lamp_glass):
    """
    Верхняя палуба за каютой: штурвал, бизань-мачта с флагом, кормовой фонарь. Сцена заканчивается не стеной:
    за стеной видно, что корабль идёт дальше.
    """
    top = FLOOR + 2.3
    rows = [BACK + (STERN - BACK) * k / 8 for k in range(9)]
    strip("SternFloor", [[(-half_width(y), y, top), (half_width(y), y, top)] for y in rows], deck_mat, facing=(0, 0, 1))
    # Перила по бортам верхней палубы и поперёк кормы.
    for sign in (-1, 1):
        for a, b in zip(rows, rows[1:]):
            pole("SternRail", (sign * (half_width(a) - 0.1), a, top + 0.85), (sign * (half_width(b) - 0.1), b, top + 0.85), 0.04, wood, verts=5)
        for y in rows[::2]:
            pole("SternRail", (sign * (half_width(y) - 0.1), y, top), (sign * (half_width(y) - 0.1), y, top + 0.85), 0.03, dark, verts=5)
    stern_half = half_width(STERN)
    box("SternWall", (0, STERN, top), (stern_half * 2, 0.14, 0.85), dark, bevel=0.01)
    # Штурвал на тумбе.
    wheel_x, wheel_y = 1.6, BACK + 1.15
    box("SternHelm", (wheel_x, wheel_y, top), (0.24, 0.5, 0.92), dark, bevel=0.02)
    tv.wheel(wheel_x, wheel_y - 0.36, top + 1.18, 0.56, wood, brass)
    # Бизань-мачта с реей и чёрным флагом.
    mx, my = -1.1, BACK + 2.5
    lathe("SternMast", (mx, my, top), [(0.2, 0), (0.17, 0.3), (0.11, 7.2), (0.08, 7.6)], wood, segments=10)
    pole("SternYard", (mx - 2.2, my, top + 4.0), (mx + 2.2, my, top + 4.0), 0.06, dark, verts=7)
    for k in range(6):
        px = mx - 1.9 + k * 0.76
        blob("SternSail", (px, my - 0.02, top + 3.9), (0.45, 0.15, 0.16), canvas, 2)
    # Флаг на корме: чёрный, с черепом, полощется. Ниже мачты, чтобы попасть в кадры соперника и магазина.
    # Древко левее бизань-мачты: полотнище не заходит за неё.
    staff_x, staff_y = -3.75, STERN - 0.15
    pole("SternStaff", (staff_x, staff_y, top + 0.2), (staff_x, staff_y, top + 3.3), 0.03, dark, verts=6)
    blob("SternStaff", (staff_x, staff_y, top + 3.34), (0.05, 0.05, 0.05), brass, 1)
    waving_flag(staff_x + 0.03, staff_y, top + 3.2, flag_pixels())
    # Кормовой фонарь: тёплая точка в самом дальнем конце.
    lx, ly, lz = 0.75, STERN - 0.05, top + 1.55
    pole("SternLamp", (lx, ly, top + 0.8), (lx, ly, lz), 0.04, iron, verts=6)
    lathe("SternLamp", (lx, ly, lz), [(0.16, 0), (0.22, 0.2), (0.22, 0.48), (0.16, 0.6)], lamp_glass, segments=8)
    lathe("SternLamp", (lx, ly, lz + 0.6), [(0.19, 0), (0.11, 0.1), (0.03, 0.2)], iron, segments=8)
    lathe("SternLamp", (lx, ly, lz - 0.03), [(0.2, 0), (0.17, 0.05)], iron, segments=8)
    for k in range(6):
        a = k * math.pi / 3
        pole("SternLamp", (lx + math.cos(a) * 0.17, ly + math.sin(a) * 0.17, lz), (lx + math.cos(a) * 0.235, ly + math.sin(a) * 0.235, lz + 0.22), 0.009, iron, verts=3)
        pole("SternLamp", (lx + math.cos(a) * 0.235, ly + math.sin(a) * 0.235, lz + 0.22), (lx + math.cos(a) * 0.235, ly + math.sin(a) * 0.235, lz + 0.46), 0.009, iron, verts=3)
        pole("SternLamp", (lx + math.cos(a) * 0.235, ly + math.sin(a) * 0.235, lz + 0.46), (lx + math.cos(a) * 0.17, ly + math.sin(a) * 0.17, lz + 0.6), 0.009, iron, verts=3)
    # Верхняя палуба не пустая: настил полосами и бухта каната.
    for k in range(-5, 6):
        box("SternPlank", (k * 0.72, (BACK + STERN) / 2 - 0.3, top + 0.004), (0.02, STERN - BACK - 0.8, 0.008), dark, bevel=0)
    for k in range(4):
        torus("SternCoil", (3.0, BACK + 0.9, top + 0.05 + k * 0.07), 0.3 - k * 0.018, 0.038, rope, 14, 4, axis="Z")


def cannon(x, y, sign, wood, iron, brass, rope):
    """Пушка на лафете смотрит за борт: толстый ствол с кольцами, колёса, у лафета ядра горкой."""
    bed = box("GunBed", (x, y, FLOOR + 0.17), (1.05, 0.56, 0.16), wood, bevel=0.015)
    bed.rotation_euler = (0, 0, 0)
    box("GunBed", (x - sign * 0.32, y, FLOOR + 0.33), (0.3, 0.56, 0.14), wood, bevel=0.015)
    for dx in (-0.36, 0.36):
        for dy in (-0.31, 0.31):
            wheel = cyl("GunWheel", (x + dx, y + dy, FLOOR + 0.15), 0.15, 0.08, wood, axis="Y", verts=10)
            wheel.name = "GunWheel"
    # Ствол: сетка вокруг своего нуля, иначе поворот уводит его вокруг центра сцены.
    barrel_mesh = lathe("GunBarrel", (0, 0, 0), [(0.0, 0), (0.15, 0.02), (0.2, 0.08), (0.2, 0.32), (0.235, 0.34), (0.235, 0.4), (0.175, 0.42), (0.16, 1.05), (0.195, 1.08), (0.195, 1.2), (0.13, 1.25), (0.09, 1.25), (0.09, 1.18)], iron, segments=12)
    barrel_mesh.location = (x - sign * 0.62, y, FLOOR + 0.55)
    barrel_mesh.rotation_euler = (0, sign * math.radians(87), 0)
    blob("GunBarrel", (x - sign * 0.68, y, FLOOR + 0.55), (0.075, 0.075, 0.075), brass, 1)
    # Орудийный порт в борту: ствол смотрит в него, а не в доски.
    wall = sign * half_width(y)
    box("GunPort", (wall - sign * 0.03, y, FLOOR + 0.22), (0.04, 0.82, 0.68), wood, bevel=0)
    box("GunPort", (wall - sign * 0.055, y, FLOOR + 0.28), (0.03, 0.62, 0.5), mat("ShipPort", "#0b0a0c", 0.9), bevel=0)
    # Лафет ступеньками, медные болты, откатные канаты от борта, прислонённый банник.
    box("GunBed", (x - sign * 0.14, y, FLOOR + 0.33), (0.12, 0.56, 0.1), wood, bevel=0.012)
    for dy in (-0.25, 0.25):
        for dx in (-0.38, -0.1, 0.32):
            blob("GunBolt", (x + sign * dx, y + dy + (0.035 if dy > 0 else -0.035), FLOOR + 0.25), (0.018, 0.012, 0.018), brass, 1)
        path = [(wall - sign * 0.05, y + dy * 2.0, FLOOR + 0.62), (x + sign * 0.3, y + dy * 2.1, FLOOR + 0.28), (x - sign * 0.15, y + dy * 1.25, FLOOR + 0.3), (x - sign * 0.6, y + dy * 0.5, FLOOR + 0.4)]
        tube("GunRope", smooth_path(path, 4), 0.02, rope, sides=4)
        torus("GunRing", (wall - sign * 0.05, y + dy * 2.0, FLOOR + 0.62), 0.04, 0.01, iron, 8, 3, axis="X")
    for dx in (-0.36, 0.36):
        for dy in (-0.355, 0.355):
            cyl("GunWheel", (x + dx, y + dy, FLOOR + 0.15), 0.035, 0.03, brass, axis="Y", verts=6)
    pole("GunRammer", (x - sign * 0.2, y + 0.85, FLOOR + 0.02), (wall - sign * 0.2, y + 0.7, FLOOR + 1.14), 0.02, wood, verts=5)
    blob("GunRammer", (wall - sign * 0.19, y + 0.69, FLOOR + 1.2), (0.05, 0.05, 0.1), mat("ShipSwab", "#57483a", 0.95), 1)
    for k, (dx, dy, z) in enumerate(((0, 0, 0.13), (0.27, 0, 0.13), (0.135, 0.235, 0.13), (0.135, 0.08, 0.35))):
        blob("GunBall", (x - sign * 1.05 + dx, y - 0.72 + dy, FLOOR + z), (0.135, 0.135, 0.135), iron, 2)


def mast(wood, dark, iron, rope, canvas, brass):
    """Мачта: толстое бревно с железными обручами, вокруг кофель-нагель с вымбовками и бухтой каната, сверху рея со свёрнутым парусом."""
    x, y = MAST
    lathe("Mast", (x, y, FLOOR), [(0.34, 0), (0.29, 0.25), (0.27, 2), (0.2, 7.6), (0.16, 8.0)], wood, segments=12)
    for z in (0.45, 1.3, 2.15, 3.0):
        torus("MastHoop", (x, y, FLOOR + z), 0.285 - z * 0.012, 0.03, iron, 12, 4, axis="Z")
    # Кольцо со штырями и намотанными канатами: сразу читается как корабль даже в нижней части кадра партии.
    torus("MastRail", (x, y, FLOOR + 0.92), 0.72, 0.055, dark, 16, 5, axis="Z")
    for k in range(4):
        a = k * math.pi / 2 + 0.78
        pole("MastRail", (x + math.cos(a) * 0.72, y + math.sin(a) * 0.72, FLOOR), (x + math.cos(a) * 0.72, y + math.sin(a) * 0.72, FLOOR + 0.92), 0.05, dark, verts=7)
    for k in range(10):
        a = k * 2 * math.pi / 10
        px, py = x + math.cos(a) * 0.72, y + math.sin(a) * 0.72
        pole("MastPin", (px, py, FLOOR + 0.78), (px, py, FLOOR + 1.16), 0.024, brass if k % 3 == 0 else wood, verts=5)
    for k in range(5):
        torus("MastCoil", (x - 0.82, y - 0.52, FLOOR + 0.05 + k * 0.075), 0.31 - k * 0.018, 0.04, rope, 14, 4, axis="Z")
    # Рея со свёрнутым парусом: видно в меню и магазине.
    z = FLOOR + 5.4
    pole("MastYard", (x - 3.5, y, z), (x + 3.5, y, z), 0.085, dark, verts=8)
    for k in range(9):
        px = x - 3.0 + k * 0.75
        lump = 0.2 + 0.05 * math.sin(k * 2.1)
        blob("MastSail", (px, y - 0.03, z - 0.13), (0.46, lump, lump * 1.05), canvas, 2)
    for k in range(5):
        torus("MastRope", (x - 2.4 + k * 1.2, y - 0.02, z - 0.12), 0.27, 0.018, rope, 10, 3, axis="X")
    # Ванты к ближнему борту, с выбленками-ступеньками. К дальнему не ведём: мачта стоит не по центру, канат перечёркивал бы весь кадр.
    top = (x, y, FLOOR + 6.3)
    feet = [(half_width(y + dy) - 0.08, y + dy, rail_height(y + dy) + 0.04) for dy in (-1.0, -0.2, 0.6)]
    for foot in feet:
        pole("MastShroud", foot, top, 0.02, rope, verts=4)
    for k in range(1, 9):
        t = k * 0.085
        points = [tuple(foot[i] + (top[i] - foot[i]) * t for i in range(3)) for foot in feet]
        pole("MastShroud", points[0], points[2], 0.013, rope, verts=3)


def cabin_door(x, wood, dark, iron, brass, glow):
    """Дверь: доски, кованые петли с заклёпками, кольцо, оконце с огнём внутри, над дверью череп с костями."""
    y = BACK - 0.11
    box("CabinDoor", (x, y, FLOOR), (1.24, 0.08, 2.04), dark, bevel=0.018)
    for k in range(5):
        box("CabinDoor", (x - 0.42 + k * 0.21, y - 0.06, FLOOR + 0.05), (0.195, 0.06, 1.86), wood if k % 2 else mat("ShipDoorDark", "#5c3a1d", 0.85), bevel=0.006)
    for z in (0.42, 1.48):
        box("CabinHinge", (x - 0.2, y - 0.1, FLOOR + z), (0.8, 0.024, 0.07), iron, bevel=0)
        blob("CabinHinge", (x + 0.22, y - 0.105, FLOOR + z + 0.035), (0.055, 0.018, 0.055), iron, 1)
        for dx in (-0.45, -0.2, 0.05):
            blob("CabinHinge", (x + dx, y - 0.118, FLOOR + z + 0.035), (0.018, 0.012, 0.018), brass, 1)
    box("CabinGlass", (x, y - 0.095, FLOOR + 1.08), (0.34, 0.02, 0.3), glow, bevel=0)
    for dx in (-0.08, 0.08):
        box("CabinHinge", (x + dx, y - 0.11, FLOOR + 1.06), (0.022, 0.018, 0.34), iron, bevel=0)
    torus("CabinRing", (x + 0.36, y - 0.12, FLOOR + 0.92), 0.06, 0.012, brass, 10, 4, axis="Y")
    # Череп с костями над дверью: узнаваемый пират виден и в партии.
    bone = mat("ShipBone", "#e6dcc0", 0.8)
    black = mat("ShipSocket", "#120e0c", 0.9)
    sx, sz = x, FLOOR + 2.0
    box("CabinSkullBoard", (sx, y - 0.02, sz - 0.03), (0.7, 0.05, 0.28), dark, bevel=0.01)
    blob("CabinSkull", (sx, y - 0.12, sz + 0.13), (0.1, 0.06, 0.092), bone, 2)
    blob("CabinSkull", (sx, y - 0.12, sz + 0.05), (0.06, 0.045, 0.04), bone, 1)
    for sign in (-1, 1):
        blob("CabinSkull", (sx + sign * 0.038, y - 0.175, sz + 0.13), (0.024, 0.012, 0.028), black, 1)
        pole("CabinSkull", (sx - sign * 0.27, y - 0.09, sz - 0.02), (sx + sign * 0.27, y - 0.09, sz + 0.2), 0.016, bone, verts=5)
        for dz in (-0.03, 0.03):
            blob("CabinSkull", (sx + sign * 0.28, y - 0.09, sz + 0.09 + sign * 0.11 + dz), (0.022, 0.018, 0.022), bone, 1)


def cabin_window(wx, wood, dark, iron, glow):
    """Окно: рама, переплёт, подоконник, открытые ставни с поперечинами."""
    y = BACK - 0.12
    box("CabinWindow", (wx, y, FLOOR + 0.82), (1.05, 0.06, 0.8), dark, bevel=0.012)
    box("CabinGlass", (wx, y - 0.04, FLOOR + 0.89), (0.89, 0.04, 0.66), glow, bevel=0)
    box("CabinWindow", (wx, y - 0.07, FLOOR + 1.18), (0.9, 0.03, 0.04), dark, bevel=0)
    box("CabinWindow", (wx, y - 0.07, FLOOR + 0.89), (0.04, 0.03, 0.66), dark, bevel=0)
    box("CabinWindow", (wx, y - 0.08, FLOOR + 0.74), (1.24, 0.14, 0.07), wood, bevel=0.012)
    for sign in (-1, 1):
        sx = wx + sign * 0.76
        box("CabinShutter", (sx, y - 0.07, FLOOR + 0.82), (0.46, 0.05, 0.8), wood, bevel=0.008)
        for z in (0.95, 1.42):
            box("CabinShutter", (sx, y - 0.105, FLOOR + z), (0.46, 0.022, 0.06), dark, bevel=0)
        box("CabinShutter", (sx - sign * 0.22, y - 0.11, FLOOR + 1.0), (0.03, 0.02, 0.44), iron, bevel=0)


def wall_details(wood, dark, iron, brass, rope):
    """Мелочи на стене: корабельный колокол на кронштейне (качается), канат на крюке, объявление с черепом."""
    y = BACK - 0.11
    # Колокол справа от двери: в кадре партии, медь заметна на тёмной стене.
    bx, bz = 1.45, FLOOR + 1.62
    box("CabinBracket", (bx - 0.14, y - 0.02, bz - 0.13), (0.12, 0.05, 0.44), dark, bevel=0.008)
    pole("CabinBracket", (bx - 0.14, y - 0.05, bz + 0.16), (bx + 0.03, y - 0.34, bz + 0.16), 0.024, iron, verts=6)
    hinge = (bx + 0.03, y - 0.34, bz + 0.16)

    def bell_wind(point):
        return (min((Vector(hinge) - point).length / 0.42, 1.0), 0.15, 0.0)

    bell = lathe("SwayBell", (0, 0, 0), [(0.0, -0.3), (0.125, -0.32), (0.115, -0.26), (0.085, -0.17), (0.072, -0.07), (0.055, -0.02), (0.02, 0.0)], brass, segments=12)
    bell.location = hinge
    clapper = pole("SwayBell", (hinge[0], hinge[1], hinge[2] - 0.05), (hinge[0], hinge[1], hinge[2] - 0.37), 0.009, iron, verts=4)
    knob = blob("SwayBell", (hinge[0], hinge[1], hinge[2] - 0.37), (0.022, 0.022, 0.03), iron, 1)
    lanyard = tube("SwayBell", smooth_path([(hinge[0], hinge[1], hinge[2] - 0.37), (hinge[0] + 0.02, hinge[1] - 0.02, hinge[2] - 0.6), (hinge[0] - 0.01, hinge[1] - 0.01, hinge[2] - 0.76)], 3), 0.011, rope, sides=4)
    for part in (bell, clapper, knob, lanyard):
        set_wind(part, bell_wind)
    # Канат на крюке слева от двери.
    px, pz = -0.85, FLOOR + 1.45
    blob("CabinHook", (px, y - 0.04, pz), (0.03, 0.03, 0.03), iron, 1)
    for k in range(4):
        loop = torus("CabinCoil", (px + (k - 1.5) * 0.012, y - 0.075 - k * 0.012, pz - 0.26), 0.25, 0.02, rope, 14, 3, axis="Y")
        loop.scale = (0.72, 1, 1.05)
    # Объявление на правой стене: бумага с чёрным пятном-портретом, прибита гвоздями.
    paper = mat("ShipPaper", "#d9c99c", 0.95)
    nx, nz = 2.25, FLOOR + 0.88
    box("CabinPoster", (nx, y - 0.012, nz), (0.52, 0.018, 0.68), paper, bevel=0)
    ink = mat("ShipInk", "#2a1f17", 0.9)
    blob("CabinPoster", (nx, y - 0.032, nz + 0.4), (0.11, 0.008, 0.13), ink, 1)
    for dz, wide in ((0.17, 0.36), (0.11, 0.28), (0.05, 0.2)):
        box("CabinPoster", (nx, y - 0.026, nz + dz), (wide, 0.006, 0.022), ink, bevel=0)
    for dx in (-0.22, 0.22):
        blob("CabinPoster", (nx + dx, y - 0.03, nz + 0.63), (0.014, 0.008, 0.014), iron, 1)


def cabin(boards, wood, dark, iron, brass, glow, rope):
    """Стена кормовой надстройки за столом: дверь, два горящих окна, перила наверху. Тёплый свет на фоне холодной ночи."""
    height = 2.3
    wide = half_width(BACK) * 2
    tv.world_uv(box("CabinWall", (0, BACK, FLOOR), (wide, 0.22, height), boards, bevel=0), 0.9)
    box("CabinTrim", (0, BACK - 0.13, FLOOR + height - 0.14), (wide, 0.08, 0.14), dark, bevel=0.01)
    box("CabinTrim", (0, BACK - 0.13, FLOOR), (wide, 0.08, 0.2), dark, bevel=0.01)
    cabin_door(-0.2, wood, dark, iron, brass, glow)
    for wx in (-3.4, 3.7):
        cabin_window(wx, wood, dark, iron, glow)
    wall_details(wood, dark, iron, brass, rope)
    deck_z = FLOOR + height
    # Перила с проёмом над трапом.
    for x0, x1 in ((-wide / 2, STAIR_X - 0.55), (STAIR_X + 0.55, wide / 2)):
        box("CabinRail", ((x0 + x1) / 2, BACK - 0.02, deck_z + 0.8), (x1 - x0, 0.12, 0.09), wood, bevel=0.015)
        x = x0 + 0.1
        while x < x1:
            lathe("CabinRail", (x, BACK - 0.02, deck_z), [(0.06, 0), (0.035, 0.12), (0.06, 0.4), (0.035, 0.7), (0.05, 0.8)], dark, segments=6)
            x += 0.72


def chest(x, y, turn, wood, iron, brass, gold):
    """Открытый сундук: крышка дугой с обручами, оковка углов, в золоте кубок, камни и нитка жемчуга."""
    width, depth, height = 1.0, 0.62, 0.42
    ca, sa = math.cos(turn), math.sin(turn)

    def at(dx, dy, dz=0.0):
        return (x + ca * dx - sa * dy, y + sa * dx + ca * dy, FLOOR + dz)

    body = box("ChestBox", (x, y, FLOOR), (width, depth, height), wood, bevel=0.015)
    body.rotation_euler = (0, 0, turn)
    for dx in (-0.36, 0.36, -0.49, 0.49):
        band = box("ChestBox", at(dx, 0), (0.07, depth + 0.03, height + 0.015), iron if abs(dx) < 0.4 else brass, bevel=0.005)
        band.rotation_euler = (0, 0, turn)

    # Крышка: дуга повёрнута вокруг задней кромки назад.
    radius = depth / 2
    hinge_angle = math.radians(102)

    def lid_point(dx, a):
        ly, lz = -math.cos(a) * radius + radius, math.sin(a) * radius * 0.95
        oy = ly * math.cos(hinge_angle) + lz * math.sin(hinge_angle)
        oz = -ly * math.sin(hinge_angle) + lz * math.cos(hinge_angle)
        return at(dx, depth / 2 - oy, height + oz)

    arc = [math.pi * k / 7 for k in range(8)]
    lid = strip("ChestBox", [[lid_point(dx, a) for a in arc] for dx in (-width / 2, width / 2)], wood)
    lid.modifiers.new("s", "SOLIDIFY").thickness = 0.03
    for dx in (-0.36, 0.36):
        tube("ChestBox", [lid_point(dx, a) for a in arc], 0.024, iron, sides=4)
    lx, ly, lz = at(0, -depth / 2 - 0.015, height - 0.17)
    box("ChestBox", (lx, ly, lz), (0.13, 0.03, 0.15), brass, bevel=0).rotation_euler = (0, 0, turn)

    mx, my, _ = at(0, -0.02)
    blob("ChestGold", (mx, my, FLOOR + height - 0.02), (width * 0.46, depth * 0.42, 0.17), gold, 2)
    # Кубок.
    cx, cy, cz = at(0.24, 0.02, height + 0.08)
    lathe("ChestGold", (cx, cy, cz), [(0.08, 0), (0.025, 0.04), (0.02, 0.13), (0.085, 0.2), (0.09, 0.32)], brass, segments=10)
    for dx, dy, color, name in ((-0.2, -0.07, "#d42c3a", "ShipRuby"), (-0.05, -0.15, "#2c8fd4", "ShipSapphire"), (0.08, -0.1, "#38c46c", "ShipEmerald")):
        gx, gy, gz = at(dx, dy, height + 0.13)
        blob("ChestGem", (gx, gy, gz), (0.05, 0.05, 0.04), mat(name, color, 0.2, emit=0.5), 1)
    # Жемчуг свешивается через край.
    pearl = mat("ShipPearl", "#efe9dd", 0.3)
    for k in range(9):
        t = k / 8
        px, py, pz = at(-0.36 + 0.14 * t, -0.2 - 0.14 * t, height + 0.1 - 0.42 * t * t)
        blob("ChestGem", (px, py, pz), (0.023, 0.023, 0.023), pearl, 1)
    for k in range(13):
        dx, dy = (rng.rand() - 0.5) * 1.7, -0.45 - rng.rand() * 0.55
        cx, cy, cz = at(dx, dy, 0.012)
        coin = cyl("ChestCoin", (cx, cy, cz), 0.065, 0.02, gold, verts=8)
        coin.rotation_euler = ((rng.rand() - 0.5) * 0.1, (rng.rand() - 0.5) * 0.1, 0)


def crate(x, y, z, size, turn, wood, dark):
    obj = box("CargoCrate", (x, y, z), (size, size, size * 0.9), wood, bevel=0.015)
    obj.rotation_euler = (0, 0, turn)
    ca, sa = math.cos(turn), math.sin(turn)
    half = size / 2 + 0.005
    for dx, dy, rotate in ((0, -half, 0), (0, half, 0), (-half, 0, 90), (half, 0, 90)):
        bx, by = x + ca * dx - sa * dy, y + sa * dx + ca * dy
        for sign in (-1, 1):
            strip = box("CargoCrate", (bx, by, z + size * 0.45 + sign * (size * 0.38) - 0.04), (size * 1.02, 0.03, 0.08), dark, bevel=0)
            strip.rotation_euler = (0, 0, turn + math.radians(rotate))


def cargo(wood, dark, barrels, rope):
    """Груз слева за столом: бочки и ящики, стянутые канатом. Мало, но крупно."""
    barrel((-4.45, 5.2, FLOOR), 1.1, 0.44, barrels)
    barrel((-3.55, 5.5, FLOOR), 1.1, 0.44, barrels)
    barrel((-4.05, 5.45, FLOOR + 1.1), 1.0, 0.4, barrels)
    crate(-5.45, 5.05, FLOOR, 0.95, 0.12, wood, dark)
    crate(-5.4, 5.1, FLOOR + 0.855, 0.62, -0.3, wood, dark)
    # Груз принайтован: канаты вокруг бочек и через ящик, к рыму в палубе. Рядом мешки.
    for cx, cy, z, major in ((-4.45, 5.2, 0.55, 0.46), (-3.55, 5.5, 0.55, 0.46), (-4.05, 5.45, 1.6, 0.42)):
        torus("CargoRope", (cx, cy, FLOOR + z), major, 0.02, rope, 18, 4, axis="Z")
    tube("CargoRope", smooth_path([(-3.1, 4.95, FLOOR + 0.03), (-3.55, 5.02, FLOOR + 0.62), (-4.45, 4.72, FLOOR + 0.7), (-4.95, 4.55, FLOOR + 0.4), (-5.3, 4.5, FLOOR + 0.03)], 4), 0.02, rope, sides=4)
    torus("CargoRope", (-3.1, 4.95, FLOOR + 0.04), 0.06, 0.016, mat("ShipIron", "#25262c", 0.5), 8, 3, axis="Z")
    sack = mat("ShipSack", "#bda877", 0.95)
    for cx, cy, z, sx, sz, tilt in ((-2.95, 4.25, 0.26, 0.36, 0.28, 0.2), (-3.5, 4.35, 0.24, 0.34, 0.26, -0.15), (-3.2, 4.3, 0.62, 0.32, 0.22, 0.35)):
        obj = blob("CargoSack", (cx, cy, FLOOR + z), (sx, 0.27, sz), sack, 2)
        obj.rotation_euler = (0.1, tilt, tilt * 2)
        pole("CargoSack", (cx + sx * 0.85, cy, FLOOR + z), (cx + sx * 1.15, cy + 0.03, FLOOR + z + 0.08), 0.045, rope, verts=5)


def ship():
    sky_dome(sky_image())
    deck_pixels = deck_image()
    deck_floor(deck_pixels)

    boards = image_mat("ShipBoards", bc.plank_image(), rough=0.8)
    barrels = image_mat("ShipBarrel", barrel_image(), rough=0.8)
    wood = mat("ShipWood", "#7a4d28", 0.8)
    dark = mat("ShipDark", "#3a2412", 0.85)
    iron = mat("ShipIron", "#25262c", 0.5)
    brass = mat("ShipBrass", "#c9982f", 0.45)
    rope = mat("ShipRope", "#b39865", 0.95)
    canvas = mat("ShipCanvas", "#d8cba8", 0.95)
    gold = mat("ShipGold", "#f2bd32", 0.4)
    glow = mat("ShipGlow", "#ff9d38", 1, emit=1.3)
    lamp_glass = mat("ShipLampGlass", "#ffb04f", 1, emit=1.8)

    for sign in (-1, 1):
        bulwark(sign, boards, wood, iron)
    cabin(boards, wood, dark, iron, brass, glow, rope)
    stairs(STAIR_X, wood, dark, brass, rope)
    upper = mat("ShipUpperDeck", "#6e4526", 0.85)
    stern_deck(wood, dark, iron, brass, rope, canvas, upper, lamp_glass)
    mast(wood, dark, iron, rope, canvas, brass)
    cargo(wood, dark, barrels, rope)
    cannon(-5.45, 1.4, -1, wood, iron, brass, rope)
    cannon(5.45, 1.8, 1, wood, iron, brass, rope)
    cannon(5.45, -3.2, 1, wood, iron, brass, rope)
    chest(4.25, 4.15, math.radians(-14), wood, iron, brass, gold)

    # Фонари: на мачте и на столбах над планширем. Столб с кронштейном держит фонарь, он не висит в воздухе.
    spots = ((-1, 3.0, 0.1), (1, -0.6, 0.45), (-1, -3.2, 0.75))
    for sign, y, phase in spots:
        x = sign * (half_width(y) - 0.1)
        head = rail_height(y) + 1.17
        pole("HullLampPost", (x, y, rail_height(y)), (x, y, head), 0.04, dark, verts=6)
        tube("HullLampPost", smooth_path([(x, y, head - 0.08), (x - sign * 0.14, y, head + 0.03), (x - sign * 0.32, y, head)], 3), 0.024, iron, sides=5)
        hanging_lantern(x - sign * 0.32, y, head - 0.02, iron, lamp_glass, phase)
    # Фонарь на мачте — на кронштейне, тоже на цепи.
    pole("MastLampArm", (MAST[0] - 0.22, MAST[1] - 0.1, 1.72), (MAST[0] - 0.56, MAST[1] - 0.22, 1.72), 0.024, iron, verts=5)
    hanging_lantern(MAST[0] - 0.56, MAST[1] - 0.22, 1.72, iron, lamp_glass, 0.3)

    # Планширь: кофель-планка с нагелями и бухта на ней по левому борту, в углу кадра партии. Дальше мелочи передней палубы.
    for k in range(7):
        y = -0.8 + k * 0.32
        x = -(half_width(y) - 0.26)
        pole("HullPin", (x, y, rail_height(y) - 0.2), (x, y, rail_height(y) + 0.24), 0.024, brass if k % 3 == 0 else wood, verts=5)
    for k in range(4):
        loop = torus("HullCoil", (-(half_width(0.6) - 0.2), 0.6 + k * 0.012, rail_height(0.6) - 0.28), 0.26, 0.022, rope, 12, 3, axis="X")
        loop.scale = (1, 0.7, 1.05)
    deck_clutter(wood, dark, iron, brass, rope)
    loose_rigging(rope, iron, wood)


def deck_clutter(wood, dark, iron, brass, rope):
    """Передний план палубы был пустым: люк-решётка с огнём снизу, ведро со шваброй, бухта каната, рымы."""
    # Люк слева перед столом: виден в партии в нижнем углу и крупно в магазине.
    hx, hy = -3.65, -3.1
    w, d = 1.5, 1.2
    box("DeckHatch", (hx, hy, FLOOR), (w + 0.2, d + 0.2, 0.13), dark, bevel=0.015)
    # Решётка тёмная и частая, огонь снизу только просвечивает: трюм, а не подсвеченная вафля.
    box("DeckHatchGlow", (hx, hy, FLOOR + 0.02), (w, d, 0.09), mat("ShipHold", "#c9691f", 1, emit=1.1), bevel=0)
    for k in range(-6, 7):
        box("DeckHatch", (hx + k * w / 13, hy, FLOOR + 0.05), (0.052, d, 0.1), dark, bevel=0)
    for k in range(-5, 6):
        box("DeckHatch", (hx, hy + k * d / 11, FLOOR + 0.07), (w, 0.052, 0.085), dark, bevel=0)
    for dx in (-1, 1):
        torus("DeckRing", (hx + dx * (w / 2 + 0.03), hy, FLOOR + 0.15), 0.07, 0.014, iron, 8, 3, axis="X")
    # Ведро и швабра у правого борта перед пушкой.
    bx, by = 4.35, -1.05
    lathe("DeckBucket", (bx, by, FLOOR), [(0.15, 0), (0.2, 0.34), (0.185, 0.34), (0.14, 0.05)], wood, segments=10)
    for z in (0.08, 0.27):
        torus("DeckBucket", (bx, by, FLOOR + z), 0.16 + z * 0.13, 0.012, iron, 10, 3, axis="Z")
    tube("DeckBucket", smooth_path([(bx - 0.19, by, FLOOR + 0.34), (bx, by + 0.02, FLOOR + 0.58), (bx + 0.19, by, FLOOR + 0.34)], 4), 0.009, iron, sides=4)
    blob("DeckWater", (bx, by, FLOOR + 0.27), (0.16, 0.16, 0.012), mat("ShipBucketWater", "#3f7f93", 0.1), 1)
    mop_top = (bx + 0.72, by + 0.35, RAIL + 0.18)
    pole("DeckMop", (bx + 0.1, by - 0.45, FLOOR + 0.18), mop_top, 0.022, wood, verts=5)
    # Швабра: плотная мокрая кисть, а не лучи.
    rag = mat("ShipMopRag", "#8f8468", 0.95)
    blob("DeckMop", (bx + 0.09, by - 0.47, FLOOR + 0.2), (0.06, 0.06, 0.05), rag, 1)
    for k in range(11):
        a = k * 2 * math.pi / 11
        reach = 0.13 + 0.05 * (k % 3)
        tube("DeckMop", smooth_path([(bx + 0.09, by - 0.47, FLOOR + 0.2), (bx + 0.09 + math.cos(a) * reach * 0.55, by - 0.47 + math.sin(a) * reach * 0.55, FLOOR + 0.11), (bx + 0.09 + math.cos(a) * reach, by - 0.47 + math.sin(a) * reach, FLOOR + 0.02)], 3), 0.034, rag, sides=4)
    # Рымы и бухта на переднем плане справа.
    for k in range(5):
        torus("DeckCoil", (2.6, -5.5, FLOOR + 0.05 + k * 0.075), 0.42 - k * 0.025, 0.04, rope, 16, 4, axis="Z")
    for rx, ry in ((-1.6, -6.2), (1.4, -3.0), (-5.2, -0.6)):
        cyl("DeckRing", (rx, ry, FLOOR + 0.01), 0.07, 0.02, iron, verts=8)
        torus("DeckRing", (rx, ry - 0.05, FLOOR + 0.04), 0.06, 0.012, iron, 8, 3, axis="Z")


def loose_rigging(rope, iron, wood):
    """Снасти качаются: блок на канате у мачты, провисшие концы с реи. Видно за спиной соперника и в магазине."""
    def hang(x, y, top, length, phase, with_block=False):
        hinge = Vector((x, y, top))

        def wind(point):
            return (min((hinge - point).length / length, 1.0) ** 1.4, phase, 0.0)

        drift = 0.05 + 0.04 * rng.rand()
        rope_part = tube("SwayRope", smooth_path([(x, y, top), (x + drift, y, top - length * 0.5), (x, y + drift * 0.4, top - length)], 3), 0.017, rope, sides=4)
        set_wind(rope_part, wind)
        if with_block:
            bottom = top - length
            parts = [blob("SwayRope", (x, y + drift * 0.4, bottom - 0.08), (0.075, 0.05, 0.11), wood, 1),
                     torus("SwayRope", (x, y + drift * 0.4 - 0.05, bottom - 0.08), 0.045, 0.012, iron, 8, 3, axis="Y"),
                     torus("SwayRope", (x, y + drift * 0.4, bottom - 0.24), 0.05, 0.014, iron, 8, 3, axis="X")]
            for part in parts:
                set_wind(part, wind)

    yard_z = FLOOR + 5.4
    # Концы заканчиваются выше стены каюты: иначе падают на дверь и колокол.
    hang(MAST[0] - 1.2, MAST[1], yard_z - 0.1, 2.0, 0.25, True)
    hang(MAST[0] - 2.4, MAST[1], yard_z - 0.1, 2.4, 0.6)
    hang(MAST[0] + 1.4, MAST[1], yard_z - 0.1, 2.2, 0.85, True)


def preview_sea():
    """Только для снимков: в игре море — шейдер Zonk/Water."""
    sea = mat("PreviewSea", "#123f5c", 0.6)
    obj = ground("PreviewSea", -60, 60, -30, 60, np.zeros((4, 4, 3)) + np.array([0.05, 0.18, 0.27]), z=-3.6)
    obj.data.materials[0] = sea
    return obj


def lights():
    def point(name, energy, color, location, soft):
        data = bpy.data.lights.new(name, "POINT")
        data.energy = energy
        data.color = color
        data.shadow_soft_size = soft
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location

    spot = bpy.data.lights.new("TableLamp", "SPOT")
    spot.energy = 480
    spot.color = (1.0, 0.8, 0.55)
    spot.spot_size = math.radians(105)
    spot.spot_blend = 0.65
    spot.shadow_soft_size = 0.35
    obj = bpy.data.objects.new("TableLamp", spot)
    bpy.context.collection.objects.link(obj)
    obj.location = (0, 0, 3.4)

    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy = 1.9
    moon.color = (0.55, 0.68, 1.0)
    moon.angle = math.radians(4)
    moon_obj = bpy.data.objects.new("Moon", moon)
    bpy.context.collection.objects.link(moon_obj)
    moon_obj.rotation_euler = (math.radians(55), 0, math.radians(150))

    warm = (1.0, 0.64, 0.28)
    point("Lantern", 45, warm, (MAST[0] - 0.6, MAST[1] - 0.35, 1.1), 0.15)
    point("Lantern", 90, warm, (-SIDE + 0.75, 3.0, 0.2), 0.15)
    point("Lantern", 90, warm, (SIDE - 0.75, -0.6, 0.2), 0.15)
    point("Lantern", 90, warm, (-SIDE + 0.75, -3.2, 0.2), 0.15)
    for wx in (-3.4, 3.7):
        point("Window", 60, (1.0, 0.68, 0.32), (wx, BACK - 0.8, -0.3), 0.5)
    point("Chest", 16, (1.0, 0.76, 0.34), (4.2, 3.7, -0.7), 0.3)
    # Верхняя палуба: кормовой фонарь и слабый свет на штурвал.
    point("Stern", 70, warm, (0.4, STERN - 1.0, 2.1), 0.3)
    point("Stern", 35, warm, (1.2, BACK + 0.6, 2.2), 0.3)
    # Заполняющий: палуба перед столом и края не проваливаются в чёрное. Вокруг стола теплее, к бортам холоднее.
    for x, y, z, energy, color in ((0, -4.2, 1.6, 150, (1.0, 0.84, 0.66)), (-3.6, -5.5, 2.2, 110, (0.72, 0.8, 1.0)), (3.6, -5.5, 2.2, 110, (0.72, 0.8, 1.0)),
                                   (-4, 2, 2.4, 60, (0.72, 0.8, 1.0)), (4, 3, 2.6, 40, (0.72, 0.8, 1.0)), (0, 3.6, 1.4, 45, (1.0, 0.8, 0.6))):
        point("Fill", energy, color, (x, y, z), 1.8)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.1, 0.14, 0.26, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.4
    bpy.context.scene.world = world


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        # Имя сетки = имя группы: по нему генератор (ZonkSetup.Ship) выбирает материал. Палуба — Floor, текстура 2048.
        texture_groups = (
            ("Cabin", 1024, lambda name: name.startswith(("Cabin", "Stair", "Stern", "Wheel"))),
            ("Hull", 1024, lambda name: name.startswith(("Hull", "Deck", "Lantern", "SkyMoon"))),
            ("Gear", 1024, lambda name: name.startswith(("Gun", "Chest", "Cargo", "Barrel", "Mast"))),
            # Флаг отдельно: в общей текстуре со снастями череп не читался.
            ("Flag", 256, lambda name: name.startswith("SwayFlag")),
            ("Rig", 512, lambda name: name.startswith("Sway")),
        )

        def build():
            ship()
            bc.matte()

        views = (("korabl_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("korabl_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42), ("korabl_igra_sopernik.png", (0, 1.55, -1.4), (0, 0.72, 2.9), 45))
        kit.bake_location("Ship", "Room_Ship.fbx", build, lights, views, samples, argv[argv.index("--bake") + 1], groups=texture_groups)
        return

    folder = argv[argv.index("--render") + 1]
    ship()
    bc.matte()
    tv.count_triangles()
    preview_sea()
    kit.table()
    lights()
    kit.render(os.path.join(folder, "korabl_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "korabl_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)
    kit.render(os.path.join(folder, "korabl_magazin.png"), (5.2, 3.3, -8.4), (0, -1.3, 1.2), 50, samples)
    kit.render(os.path.join(folder, "korabl_sopernik.png"), (0, 1.55, -1.4), (0, 0.72, 2.9), 45, samples)


if __name__ == "__main__":
    main()
