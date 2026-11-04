# -*- coding: utf-8 -*-
"""
Локация «Пляж», глава 3: рыбацкий берег ранним утром: низкое мягкое солнце, бирюзовая вода, длинные тени, рядом с местом кипит работа.
Вода не здесь: живой шейдер Zonk/Water добавляет генератор (ZonkSetup.RoomBeach). Песок — под всей сценой, даль — задник.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_beach.py -- --render <папка> [--samples 48]
    blender --background --factory-startup --python Tools/Blender/zonk_beach.py -- --bake <папка превью> [--samples 64]
Координаты: x как в Unity, y Blender = z Unity, z Blender = y Unity. Верх стола на 0, пол на -1.6.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_room as kit  # noqa: E402
import zonk_tavern as tv  # noqa: E402

from zonk_room import FLOOR, blob, cyl, image_mat, link, mat  # noqa: E402
from zonk_tavern import barrel, barrel_image, bar, lathe, mug, plate, torus  # noqa: E402

FRONT = -9.5
rng = np.random.RandomState(47)


def box(name, center, size, material, bevel=0.025):
    """Ящик с точкой поворота в своём низу: rotation_euler крутит его на месте, а не вокруг центра сцены."""
    obj = kit.box(name, (0, 0, 0), size, material, bevel)
    obj.location = center
    return obj


# ---------- Картинки ----------

def sand_image():
    """Светлый утренний песок: дюны пятнами, следы ветра, следы ног к воде. Воды здесь нет: её рисует шейдер."""
    size = 2048
    y, x = np.mgrid[0:size, 0:size] / size
    top = 1 - y
    image = np.zeros((size, size, 3)) + np.array([0.90, 0.80, 0.60])
    dune = np.sin(x * 34 + np.sin(top * 9) * 2.2) + np.sin(top * 15 + np.sin(x * 21) * 2.0)
    image[dune > 0.8] = (0.86, 0.75, 0.55)
    image[dune < -1.0] = (0.93, 0.84, 0.65)
    return image


def sky_image():
    """Утро: персиковый край неба уходит в чистую голубизну, лёгкие высокие облака, вдали мыс."""
    height, width = 720, 1280
    y, x = np.mgrid[0:height, 0:width]
    u, v = x / width, 1 - y / height
    horizon = 0.05
    t = np.clip((v - horizon) / (1 - horizon), 0, 1)
    low = np.array([1.0, 0.86, 0.68])
    mid = np.array([0.72, 0.86, 0.92])
    high = np.array([0.36, 0.63, 0.88])
    a = np.clip(t / 0.22, 0, 1)[..., None]
    b = np.clip((t - 0.22) / 0.6, 0, 1)[..., None]
    image = (low * (1 - a) + mid * a) * (1 - b) + high * b
    wisp = np.sin(u * 9 + np.sin(v * 27) * 1.6) * np.sin(v * 46 + np.sin(u * 11) * 2.4)
    soft = np.clip((wisp - 0.4) / 0.35, 0, 1)[..., None] * (v > horizon + 0.03)[..., None]
    image = image * (1 - soft * 0.75) + np.array([1.0, 0.97, 0.93]) * soft * 0.75
    image[v < horizon] = (0.42, 0.74, 0.90)
    cape = (v >= horizon) & (v < horizon + 0.05 * np.exp(-((u - 0.2) / 0.11) ** 2) + 0.025 * np.exp(-((u - 0.88) / 0.08) ** 2))
    image[cape] = (0.42, 0.56, 0.62)
    return np.clip(image, 0, 1)


def plank_image():
    """Обветренные доски: выгоревшие тона, волокна линиями, тёмные щели."""
    size = 512
    image = np.zeros((size, size, 3))
    local = np.random.RandomState(8)
    count = 6
    width = size // count
    for i in range(count):
        tone = 0.34 + local.rand() * 0.16
        color = np.array([tone, tone * 0.72, tone * 0.5])
        image[:, i * width:(i + 1) * width] = color
        for _ in range(9):
            gx = i * width + local.randint(3, width - 2)
            image[:, gx] = color * 0.82
        image[:, i * width:i * width + 4] = (0.07, 0.045, 0.03)
    return image


def thatch_image():
    size = 256
    y, x = np.mgrid[0:size, 0:size] / size
    image = np.zeros((size, size, 3)) + np.array([0.62, 0.47, 0.22])
    lines = np.sin(x * 190 + np.sin(y * 9) * 2)
    image[lines > 0.3] = (0.74, 0.58, 0.29)
    image[lines < -0.55] = (0.40, 0.29, 0.13)
    return image


# ---------- Детали ----------

def ground(name, x0, x1, y0, y1, pixels, z=FLOOR, rough=0.9):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [], [(0, 1, 2, 3)])
    mesh.update()
    layer = mesh.uv_layers.new(name="UVMap")
    for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        layer.data[k].uv = uv
    return link(bpy.data.objects.new(name, mesh), image_mat(name + "Tex", pixels, rough=rough))


def backdrop(pixels, y=49.5, side=46.0, z0=-1.7, z1=34.0):
    """Даль с трёх сторон: своя текстура, сама светится, теней не даёт."""
    kit.SKY_PIXELS = pixels
    near = FRONT - 2
    mesh = bpy.data.meshes.new("SkyBack")
    mesh.from_pydata([(-side, y, z0), (side, y, z0), (side, y, z1), (-side, y, z1),
                      (-side, near, z0), (-side, y, z0), (-side, y, z1), (-side, near, z1),
                      (side, y, z0), (side, near, z0), (side, near, z1), (side, y, z1)],
                     [], [(0, 1, 2, 3), (4, 5, 6, 7), (8, 9, 10, 11)])
    mesh.update()
    layer = mesh.uv_layers.new(name="UVMap")
    for face in range(3):
        for k, uv in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
            layer.data[face * 4 + k].uv = uv
    material = bpy.data.materials.new("Sky")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    height, width = pixels.shape[:2]
    image = bpy.data.images.new("SkyImage", width, height, alpha=False)
    linear = np.where(pixels <= 0.04045, pixels / 12.92, ((pixels + 0.055) / 1.055) ** 2.4)
    image.pixels.foreach_set(np.flipud(np.dstack([linear, np.ones((height, width))])).astype(np.float32).ravel())
    image.pack()
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    emit = nodes.new("ShaderNodeEmission")
    out = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(tex.outputs[0], emit.inputs[0])
    material.node_tree.links.new(emit.outputs[0], out.inputs[0])
    obj = link(bpy.data.objects.new("SkyBack", mesh), material)
    obj.visible_shadow = False
    return obj


def pole(name, p0, p1, radius, material, verts=8):
    """Жердь между двумя точками."""
    a, b = Vector(p0), Vector(p1)
    obj = cyl(name, tuple((a + b) / 2), radius, (b - a).length, material, verts=verts)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = (b - a).to_track_quat("Z", "Y")
    return obj


def bound_pole(p0, p1, radius, material, rope):
    """Столб с верёвочной обмоткой у верха: мелочь, которая отличает вещь от цилиндра."""
    pole("Post", p0, p1, radius, material)
    for t in (0.84, 0.88, 0.92):
        c = (p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, p0[2] + (p1[2] - p0[2]) * t)
        torus("PostRope", c, radius + 0.012, 0.018, rope, 10, 3, axis="Z")


def thatch_roof(x0, x1, y0, y1, z_high, z_low, straw, strands=48):
    """
    Соломенный скат: основа и слои прядей внахлёст, нижний край неровный и висит бахромой.
    Скат идёт от конька (y1, z_high) вниз к краю (y0, z_low).
    """
    mesh = bpy.data.meshes.new("Thatch")
    bm = bmesh.new()
    rows = 4
    for row in range(rows):
        t0, t1 = row / rows, (row + 1.25) / rows
        for s in range(strands):
            cx = x0 + (x1 - x0) * (s + rng.rand()) / strands
            width = (x1 - x0) / strands * (1.7 + rng.rand())
            droop = rng.rand() * 0.28
            y_a = y1 + (y0 - y1) * t0
            z_a = z_high + (z_low - z_high) * t0
            y_b = y1 + (y0 - y1) * min(t1, 1.06) - rng.rand() * 0.05
            z_b = z_high + (z_low - z_high) * min(t1, 1.06) - droop
            y_m, z_m = (y_a + y_b) / 2, (z_a + z_b) / 2 + 0.05
            lean = (rng.rand() - 0.5) * 0.12
            v = [bm.verts.new((cx - width / 2, y_a, z_a)), bm.verts.new((cx + width / 2, y_a, z_a)),
                 bm.verts.new((cx + width * 0.42 + lean, y_m, z_m)), bm.verts.new((cx - width * 0.42 + lean, y_m, z_m)),
                 bm.verts.new((cx + lean * 2, y_b, z_b))]
            bm.faces.new((v[0], v[1], v[2], v[3]))
            bm.faces.new((v[3], v[2], v[4]))
    bm.to_mesh(mesh)
    bm.free()
    obj = link(bpy.data.objects.new("Thatch", mesh), straw)
    obj.modifiers.new("s", "SOLIDIFY").thickness = 0.03
    return obj


def palm(x, y, lean, height, bark, leaf, leaf2, rope):
    """Пальма: ствол кольцами, листья перистые — стебель и листочки, а не лента."""
    top = None
    previous = None
    segments = 9
    for i in range(segments + 1):
        t = i / segments
        centre = (x + lean[0] * t * t, y + lean[1] * t * t, FLOOR + height * t)
        if previous is not None:
            r = 0.21 - 0.09 * t
            pole("PalmTrunk", previous, centre, r, bark, verts=9)
            torus("PalmRing", centre, r + 0.006, 0.022, bark, 9, 3, axis="Z")
        previous = centre
        top = centre
    for f in range(9):
        angle = f * 2 * math.pi / 9 + rng.rand() * 0.25
        length = 2.3 + rng.rand() * 0.5
        rise = 0.35 + rng.rand() * 0.35
        material = leaf if f % 2 == 0 else leaf2
        mesh = bpy.data.meshes.new("Frond")
        bm = bmesh.new()
        dir_x, dir_y = math.cos(angle), math.sin(angle)
        side_x, side_y = -dir_y, dir_x
        steps = 12
        for s in range(steps):
            t = (s + 0.5) / steps
            r = length * t
            px, py = top[0] + dir_x * r, top[1] + dir_y * r
            pz = top[2] + rise * t * 2.4 - 1.7 * t * t * length * 0.55
            size = 0.62 * math.sin(math.pi * min(t * 1.12, 1)) + 0.1
            for sign in (-1, 1):
                tip = (px + side_x * sign * size + dir_x * 0.2, py + side_y * sign * size + dir_y * 0.2, pz - 0.26 * size - rng.rand() * 0.06)
                base_a = (px - dir_x * 0.07, py - dir_y * 0.07, pz)
                base_b = (px + dir_x * 0.07, py + dir_y * 0.07, pz)
                bm.faces.new((bm.verts.new(base_a), bm.verts.new(base_b), bm.verts.new(tip)))
        bm.to_mesh(mesh)
        bm.free()
        link(bpy.data.objects.new("SwayFrond", mesh), material)
    for dx, dy in ((0.15, 0.08), (-0.12, 0.12), (0.03, -0.16)):
        blob("Coconut", (top[0] + dx, top[1] + dy, top[2] - 0.2), (0.13, 0.13, 0.14), bark, 2)


def rowboat(x, y, angle, boards, trim, dark):
    """Лодка на песке: борта из досок (шпангоуты), банки, весло, уключины, носовая верёвка."""
    mesh = bpy.data.meshes.new("Boat")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    frames = [(-1.95, 0.03, 0.74), (-1.6, 0.28, 0.58), (-1.1, 0.5, 0.46), (-0.4, 0.68, 0.4), (0.4, 0.72, 0.4), (1.1, 0.64, 0.42), (1.6, 0.5, 0.5), (1.8, 0.42, 0.56)]
    sections = 7
    rings = []
    for along, half, rise in frames:
        ring = []
        for k in range(sections):
            s = k / (sections - 1) * 2 - 1
            ring.append(bm.verts.new((along, half * math.sin(s * math.pi / 2), rise * s * s)))
        rings.append(ring)
    for r in range(len(rings) - 1):
        for k in range(sections - 1):
            face = bm.faces.new((rings[r][k], rings[r][k + 1], rings[r + 1][k + 1], rings[r + 1][k]))
            for loop, value in zip(face.loops, ((r / 3, k / 2), (r / 3, (k + 1) / 2), ((r + 1) / 3, (k + 1) / 2), ((r + 1) / 3, k / 2))):
                loop[uv].uv = (value[1], value[0])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    hull = link(bpy.data.objects.new("Boat", mesh), boards)
    bench_paint = mat("BeachBoatPaint", "#2f6f8f", 0.8)
    hull.data.materials.append(bench_paint)
    for polygon in hull.data.polygons:
        if polygon.center.z > 0.2:
            polygon.material_index = 1
    hull.modifiers.new("s", "SOLIDIFY").thickness = 0.05
    hull.location = (x, y, FLOOR + 0.03)
    hull.rotation_euler = (0.13, -0.05, angle)
    ca, sa = math.cos(angle), math.sin(angle)
    for along in (-0.75, 0.15, 0.95):
        bench = box("BoatBench", (x + ca * along, y + sa * along, FLOOR + 0.27), (0.24, 1.15, 0.05), trim, bevel=0.01)
        bench.rotation_euler = (0, 0, angle)
    oar = box("BoatOar", (x, y, FLOOR + 0.45), (2.6, 0.05, 0.05), trim, bevel=0)
    oar.rotation_euler = (0.03, -0.09, angle + 0.16)
    blade = box("BoatOar", (x + ca * 1.35, y + sa * 1.35 + 0.12, FLOOR + 0.42), (0.55, 0.17, 0.025), trim, bevel=0)
    blade.rotation_euler = (0, 0, angle + 0.16)


def fish_rack(x, y, wood, rope, fish, fin):
    """Сушилка: две жерди с перекладиной, на верёвках рыба хвостом вниз."""
    width = 1.9
    for sign in (-1, 1):
        pole("Rack", (x + sign * width / 2, y + 0.3, FLOOR), (x + sign * width / 2, y, FLOOR + 2.15), 0.055, wood, verts=7)
        pole("Rack", (x + sign * width / 2, y - 0.3, FLOOR), (x + sign * width / 2, y, FLOOR + 2.15), 0.055, wood, verts=7)
    pole("Rack", (x - width / 2 - 0.2, y, FLOOR + 2.1), (x + width / 2 + 0.2, y, FLOOR + 2.1), 0.05, wood, verts=7)
    for i in range(6):
        fx = x - width / 2 + 0.2 + i * (width - 0.4) / 5
        hang = 0.2 + rng.rand() * 0.1
        pole("RackRope", (fx, y, FLOOR + 2.08), (fx, y, FLOOR + 2.08 - hang), 0.008, rope, verts=4)
        size = 0.34 + rng.rand() * 0.08
        z = FLOOR + 2.08 - hang - size
        blob("Fish", (fx, y, z), (0.09, 0.035, size), fish, 2)
        plate("Fish", [(fx, z - size * 0.9), (fx - 0.11, z - size * 1.28), (fx + 0.11, z - size * 1.28)], y + 0.01, 0.02, fin)


def bonfire(x, y, bark, char, stone):
    """Костёр: кольцо камней, поленья шалашом, угли. Огонь — кадры, те же, что в игре."""
    for i in range(12):
        a = i * 2 * math.pi / 12
        s = 0.17 + rng.rand() * 0.07
        rock = blob("FireStone", (x + math.cos(a) * 0.82, y + math.sin(a) * 0.72, FLOOR + s * 0.45), (s * (0.9 + rng.rand() * 0.6), s * (0.8 + rng.rand() * 0.4), s * (0.6 + rng.rand() * 0.3)), stone, 1)
        for polygon in rock.data.polygons:
            polygon.use_smooth = False
        rock.rotation_euler = (rng.rand(), rng.rand(), rng.rand())
    for i in range(7):
        a = i * 2 * math.pi / 7 + 0.3
        pole("FireLog", (x + math.cos(a) * 0.62, y + math.sin(a) * 0.55, FLOOR + 0.05), (x + math.cos(a) * 0.05, y + math.sin(a) * 0.05, FLOOR + 0.82), 0.07, bark if i % 2 else char, verts=7)
    embers = mat("BeachEmber", "#7a3a22", 1, emit=0.35)
    blob("Embers", (x, y, FLOOR + 0.05), (0.55, 0.48, 0.09), embers, 2)


def hanging_lantern(x, y, z, iron, chain_top):
    glow = mat("BeachLamp", "#d8c9a0", 0.3)
    cyl("Lantern", (x, y, z), 0.11, 0.26, glow, verts=8)
    lathe("Lantern", (x, y, z + 0.13), [(0.16, 0), (0.1, 0.08), (0.03, 0.14)], iron, segments=8)
    cyl("Lantern", (x, y, z - 0.15), 0.14, 0.05, iron, verts=8)
    for a in (0.8, 2.4, 3.9, 5.5):
        pole("Lantern", (x + math.cos(a) * 0.11, y + math.sin(a) * 0.11, z - 0.13), (x + math.cos(a) * 0.11, y + math.sin(a) * 0.11, z + 0.13), 0.01, iron, verts=3)
    pole("LanternChain", (x, y, z + 0.27), (x, y, chain_top), 0.012, iron, verts=3)


def parrot(x, y, z, turn):
    """Попугай на столбе: красный, с жёлтым и синим крылом."""
    red = mat("BeachParrot", "#d9362b", 0.7)
    blue = mat("BeachParrot2", "#2b6cc9", 0.7)
    yellow = mat("BeachParrot3", "#f3c130", 0.7)
    dark = mat("BeachBeak", "#24201c", 0.5)
    body = blob("Parrot", (x, y, z + 0.16), (0.075, 0.09, 0.15), red, 2)
    body.rotation_euler = (0.35, 0, turn)
    blob("Parrot", (x, y - 0.05, z + 0.33), (0.075, 0.075, 0.08), red, 2)
    blob("Parrot", (x, y - 0.13, z + 0.31), (0.03, 0.04, 0.035), dark, 1)
    for sign in (-1, 1):
        wing = blob("Parrot", (x + sign * 0.075, y + 0.02, z + 0.14), (0.025, 0.08, 0.12), blue if sign < 0 else yellow, 1)
        wing.rotation_euler = (0.4, 0, 0)
    tail = blob("Parrot", (x, y + 0.13, z + 0.0), (0.03, 0.05, 0.14), blue, 1)
    tail.rotation_euler = (-0.6, 0, 0)


def crab(x, y, material):
    blob("Crab", (x, y, FLOOR + 0.09), (0.17, 0.13, 0.08), material, 2)
    for sign in (-1, 1):
        blob("Crab", (x + sign * 0.25, y - 0.1, FLOOR + 0.1), (0.075, 0.06, 0.05), material, 1)
        pole("Crab", (x + sign * 0.14, y, FLOOR + 0.08), (x + sign * 0.26, y + 0.1, FLOOR + 0.01), 0.012, material, verts=3)


def beach():
    """Просторный берег: бар слева, костёр и рыба справа, лодка на песке, пальмы по углам. Лишнего нет."""
    backdrop(sky_image())
    ground("Floor", -32, 32, FRONT - 3, 7.6, sand_image())

    boards = image_mat("BeachBoards", plank_image(), rough=0.8)
    straw = image_mat("BeachStraw", thatch_image(), scale=(1.7, 1.7, 1.7), rough=1.0)
    barrels = image_mat("BeachBarrel", barrel_image(), rough=0.8)
    wood = mat("BeachWood", "#6d4524", 0.8)
    dark = mat("BeachDark", "#33200f", 0.85)
    driftwood = mat("BeachDrift", "#a6927a", 0.9)
    char = mat("BeachChar", "#17110c", 0.95)
    rope = mat("BeachRope", "#b39865", 0.95)
    iron = mat("BeachIron", "#1d1d21", 0.6)
    stone = mat("BeachStone", "#5b5650", 0.95)
    bark = mat("BeachBark", "#4d3420", 0.95)
    leaf = mat("BeachLeaf", "#26713a", 0.75)
    leaf2 = mat("BeachLeaf2", "#1b5c33", 0.75)
    fish = mat("BeachFish", "#92a9ad", 0.45)
    fin = mat("BeachFin", "#546b73", 0.6)
    red = mat("BeachRedPaint", "#8d2d25", 0.8)

    # Бар слева за столом: столбы, соломенная крыша, стойка из досок, полки с бутылками, кружки, фонари.
    sx0, sx1 = -7.2, -2.7
    for x, y in ((sx0, 2.0), (sx1, 2.0), (sx0, 4.6), (sx1, 4.6)):
        bound_pole((x, y, FLOOR), (x + (rng.rand() - 0.5) * 0.06, y, 2.1), 0.1, wood, rope)
    pole("Beam", (sx0 - 0.3, 2.0, 1.98), (sx1 + 0.3, 2.0, 2.02), 0.09, wood)
    pole("Beam", (sx0 - 0.3, 4.6, 2.75), (sx1 + 0.3, 4.6, 2.75), 0.09, wood)
    thatch_roof(sx0 - 0.5, sx1 + 0.5, 1.75, 4.8, 3.05, 1.72, straw)

    counter_y = 3.2
    tv.world_uv(box("BarFront", ((sx0 + sx1) / 2, counter_y, FLOOR), (sx1 - sx0 - 0.5, 0.5, 1.15), boards, bevel=0), 0.9)
    box("BarTop", ((sx0 + sx1) / 2, counter_y - 0.05, FLOOR + 1.15), (sx1 - sx0 - 0.3, 0.72, 0.09), wood, bevel=0.02)
    for y_shelf, z_shelf in ((4.45, 0.15), (4.45, 0.75)):
        box("BarShelf", ((sx0 + sx1) / 2, y_shelf, z_shelf), (sx1 - sx0 - 0.3, 0.32, 0.06), wood, bevel=0.01)
    glass = (("BeachBottleG", "#2c6a48"), ("BeachBottleA", "#a4641e"), ("BeachBottleR", "#8c2622"), ("BeachBottleB", "#28517a"))
    for z_shelf in (0.21, 0.81):
        bx = sx0 + 0.45
        while bx < sx1 - 0.3:
            name, color = glass[rng.randint(0, 4)]
            tv.bottle(bx, 4.45, z_shelf, mat(name, color, 0.25))
            bx += 0.24 + rng.rand() * 0.2
    for i in range(4):
        mug(sx0 + 0.9 + i * 0.85 + rng.rand() * 0.15, counter_y - 0.1, FLOOR + 1.24, wood, mat("BeachFoam", "#efe6cc", 0.8) if i % 2 else None)
    hanging_lantern(sx1 - 0.7, 2.25, 1.42, iron, 2.0)
    hanging_lantern(sx0 + 1.05, 2.25, 1.38, iron, 2.0)
    # При баре: два чурбака вместо табуретов, одна бочка, круг на столбе, попугай.
    for x in (-5.9, -4.4):
        lathe("Stump", (x, 2.4, FLOOR), [(0.27, 0), (0.24, 0.62), (0.26, 0.66)], driftwood, segments=9)
    barrel((-3.35, 2.15, FLOOR), 1.0, 0.4, barrels)
    white = mat("BeachBuoyW", "#ddd5c0", 0.8)
    torus("Buoy", (sx1 + 0.02, 1.86, 0.62), 0.27, 0.075, white, 18, 6)
    bpy.context.object.data.materials.append(red)
    for polygon in bpy.context.object.data.polygons:
        angle = math.atan2(polygon.center.y, polygon.center.x) + math.pi / 8
        polygon.material_index = int(math.floor(angle / (math.pi / 4))) % 2
    parrot(sx1 + 0.02, 2.0, 2.12, 0.2)

    # Справа: потухший костёр с котелком и чурбаком, за ним сушилка с рыбой.
    bonfire(5.0, 1.55, bark, char, stone)
    fish_rack(5.8, 3.85, driftwood, rope, fish, fin)
    lathe("Stump", (3.75, 2.9, FLOOR), [(0.27, 0), (0.24, 0.5), (0.26, 0.54)], driftwood, segments=9)
    lathe("Pot", (5.0, 0.42, FLOOR), [(0.14, 0), (0.25, 0.09), (0.27, 0.24), (0.21, 0.36), (0.25, 0.4)], iron, segments=12)

    # Лодка вытащена на берег слева.
    rowboat(-6.6, 0.0, math.radians(72), boards, wood, dark)

    # На всём берегу один краб: заметная живая деталь и никакого мусора.
    crab(1.0, 5.05, mat("BeachCrab", "#d23a24", 0.6))

    # Пальмы по углам: листья заходят в верхние углы кадра.
    palm(-9.0, 4.6, (2.0, -0.4), 5.6, bark, leaf, leaf2, rope)
    palm(8.6, 3.7, (-1.7, -0.3), 5.2, bark, leaf, leaf2, rope)


def lights():
    # Костёр — самый сильный источник, сильно греет песок и столбы.
    def point(name, energy, color, location, soft=0.4):
        data = bpy.data.lights.new(name, "POINT")
        data.energy = energy
        data.color = color
        data.shadow_soft_size = soft
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = location

    data = bpy.data.lights.new("TableLamp", "SPOT")
    data.energy = 90
    data.color = (1.0, 0.92, 0.78)
    data.spot_size = math.radians(105)
    data.spot_blend = 0.65
    data.shadow_soft_size = 0.35
    obj = bpy.data.objects.new("TableLamp", data)
    bpy.context.collection.objects.link(obj)
    obj.location = (0, 0, 3.4)

    sun = bpy.data.lights.new("Morning", "SUN")
    sun.energy = 3.4
    sun.color = (1.0, 0.82, 0.6)
    sun.angle = math.radians(2.5)
    sun_obj = bpy.data.objects.new("Morning", sun)
    bpy.context.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = (math.radians(62), 0, math.radians(-48))

    # Под навес заходит отражённый от песка свет.
    point("Bounce", 60, (1.0, 0.88, 0.7), (-5.0, 2.6, 0.2), 1.2)

    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.52, 0.72, 1.0, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.7
    bpy.context.scene.world = world


def matte():
    """Снимает пластиковый блеск и добавляет грязь в углах и стыках: остаётся в запечённой текстуре."""
    keep = ("Bottle", "Fish", "Iron", "Lamp")
    for material in bpy.data.materials:
        if not material.use_nodes:
            continue
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None or bsdf.inputs["Emission Strength"].default_value > 0:
            continue
        if not any(word in material.name for word in keep):
            bsdf.inputs["Specular"].default_value = 0.05
            bsdf.inputs["Roughness"].default_value = 0.95

        cavity = nodes.new("ShaderNodeAmbientOcclusion")
        cavity.inputs["Distance"].default_value = 0.45
        cavity.samples = 8
        ramp = nodes.new("ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value = 0.1
        ramp.inputs["To Min"].default_value = 0.25
        links.new(cavity.outputs["AO"], ramp.inputs["Value"])
        coord = nodes.new("ShaderNodeTexCoord")
        patches = nodes.new("ShaderNodeTexNoise")
        patches.inputs["Scale"].default_value = 2.5
        patches.inputs["Detail"].default_value = 5.0
        links.new(coord.outputs["Object"], patches.inputs["Vector"])
        patch_ramp = nodes.new("ShaderNodeMapRange")
        patch_ramp.inputs["From Min"].default_value = 0.33
        patch_ramp.inputs["From Max"].default_value = 0.67
        patch_ramp.inputs["To Min"].default_value = 0.72
        patch_ramp.inputs["To Max"].default_value = 1.1
        links.new(patches.outputs["Fac"], patch_ramp.inputs["Value"])
        total = nodes.new("ShaderNodeMath")
        total.operation = "MULTIPLY"
        links.new(ramp.outputs[0], total.inputs[0])
        links.new(patch_ramp.outputs[0], total.inputs[1])
        color = nodes.new("ShaderNodeVectorMath")
        color.operation = "SCALE"
        base = bsdf.inputs["Base Color"]
        if base.is_linked:
            links.new(base.links[0].from_socket, color.inputs[0])
        else:
            color.inputs[0].default_value = base.default_value[:3]
        links.new(total.outputs[0], color.inputs["Scale"])
        links.new(color.outputs[0], base)
        bevel = nodes.new("ShaderNodeBevel")
        bevel.samples = 4
        bevel.inputs["Radius"].default_value = 0.025
        links.new(bevel.outputs[0], bsdf.inputs["Normal"])


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    samples = int(argv[argv.index("--samples") + 1]) if "--samples" in argv else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--bake" in argv:
        def build():
            beach()
            matte()

        views = (("mesto_plyazh_igra_partiya.png", (0, 4.1, -4.9), (0, -0.2, 0.1), 48), ("mesto_plyazh_igra_menu.png", (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42))
        kit.bake_location("Beach", "Room_Beach.fbx", build, lights, views, samples, argv[argv.index("--bake") + 1])
        return

    folder = argv[argv.index("--render") + 1]
    beach()
    matte()
    tv.count_triangles()
    kit.table()
    lights()
    kit.render(os.path.join(folder, "mesto_plyazh_partiya.png"), (0, 4.1, -4.9), (0, -0.2, 0.1), 48, samples)
    kit.render(os.path.join(folder, "mesto_plyazh_menu.png"), (3.6, 3.2, -5.6), (-1.4, -0.2, 0.4), 42, samples)


if __name__ == "__main__":
    main()
