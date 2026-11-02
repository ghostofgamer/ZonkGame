# -*- coding: utf-8 -*-
"""
Соперник-тень: чёрная фигура без света (как дыра в сцене) и цветные аксессуары на голове.

Фигура — не капсула: широкие сутулые плечи, шея, голова, без света и кромки. Аксессуары — плоская заливка
с чернильным контуром, как в стиле Zonk/Toon. Пока только образец: рендер с прозрачным фоном для сравнения в игре.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_shadow.py -- --render <папка>
Результат: <папка>/shadow_<аксессуар>.png, 768×768.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

INK = (0.082, 0.063, 0.047, 1.0)
SHADOW = (0.006, 0.005, 0.009, 1.0)
RIM = (0.006, 0.005, 0.009, 1.0)


def srgb(r, g, b):
    def line(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    return (line(r), line(g), line(b), 1.0)


def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def flat(name, color):
    """Плоская заливка: не зависит от света."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = color
    material.node_tree.links.new(emit.outputs[0], out.inputs[0])
    return material


def toon(name, color, shade=0.62):
    """Две ступени от положения поверхности к свету сверху слева: нормаль задаёт тень, как у Zonk/Toon."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    geo = nodes.new("ShaderNodeNewGeometry")
    dot = nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    dot.inputs[1].default_value = Vector((-0.45, -0.55, 0.7)).normalized()
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (color[0] * shade, color[1] * shade, color[2] * shade, 1)
    ramp.color_ramp.elements[1].position = 0.45
    ramp.color_ramp.elements[1].color = color
    links.new(geo.outputs["Normal"], dot.inputs[0])
    links.new(dot.outputs["Value"], ramp.inputs[0])
    links.new(ramp.outputs[0], emit.inputs[0])
    links.new(emit.outputs[0], out.inputs[0])
    return material


def rim_black():
    """Чёрная фигура с тонкой кромкой: без неё глаз не видит объём на тёмном фоне."""
    material = bpy.data.materials.new("Shadow")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    fresnel = nodes.new("ShaderNodeLayerWeight")
    fresnel.inputs[0].default_value = 0.35
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].color = SHADOW
    ramp.color_ramp.elements[1].position = 0.86
    ramp.color_ramp.elements[1].color = RIM
    links.new(fresnel.outputs["Facing"], ramp.inputs[0])
    links.new(ramp.outputs[0], emit.inputs[0])
    links.new(emit.outputs[0], out.inputs[0])
    return material


def outlined(obj, width=0.022):
    """Чернильный контур: вывернутая оболочка."""
    ink = bpy.data.materials.get("OutlineInk")
    if ink is None:
        ink = flat("OutlineInk", INK)
        ink.use_backface_culling = True
    obj.data.materials.append(ink)
    mod = obj.modifiers.new("outline", "SOLIDIFY")
    mod.thickness = width
    mod.offset = 1
    mod.use_flip_normals = True
    mod.material_offset = len(obj.data.materials) - 1


def loft(name, rings, material, segments=48, power=2.0):
    """Поверхность по кольцам (z, ширина x, глубина y, сдвиг y). power > 2 даёт более квадратное сечение."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    loops = []
    for z, rx, ry, dy in rings:
        loop = []
        for i in range(segments):
            a = i * 2 * math.pi / segments
            c, s = math.cos(a), math.sin(a)
            x = rx * math.copysign(abs(c) ** (2 / power), c)
            y = ry * math.copysign(abs(s) ** (2 / power), s) + dy
            loop.append(bm.verts.new((x, y, z)))
        loops.append(loop)
    for a, b in zip(loops, loops[1:]):
        for i in range(segments):
            bm.faces.new((a[i], a[(i + 1) % segments], b[(i + 1) % segments], b[i]))
    bm.faces.new(loops[0][::-1])
    bm.faces.new(loops[-1])
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(material)
    return obj


def body():
    """Сутулый торс, покатые плечи, шея и голова чуть вперёд. Низ уходит за стол, поэтому без ног."""
    shadow = rim_black()
    loft("Torso", [
        (0.00, 0.95, 0.5, 0.10),
        (0.55, 1.05, 0.52, 0.06),
        (1.00, 1.18, 0.52, 0.02),
        (1.28, 1.2, 0.48, -0.02),
        (1.47, 0.95, 0.38, -0.1),
        (1.56, 0.5, 0.28, -0.16),
        (1.64, 0.24, 0.2, -0.2),
        (1.72, 0.2, 0.19, -0.22),
    ], shadow, power=2.6)
    rings = []
    for i in range(17):
        t = i / 16
        angle = math.pi * (t - 0.5)
        k = max(0.02, math.cos(angle))
        rings.append((1.98 + math.sin(angle) * 0.36, 0.30 * k ** 0.8, 0.31 * k ** 0.8, -0.24))
    loft("Head", rings, shadow)


def cap():
    leather = toon("Cap", srgb(0.71, 0.5, 0.25))
    top = loft("CapTop", [
        (2.17, 0.33, 0.35, -0.24), (2.24, 0.37, 0.39, -0.26), (2.31, 0.34, 0.36, -0.28), (2.36, 0.2, 0.22, -0.28), (2.38, 0.03, 0.03, -0.28),
    ], leather)
    peak = loft("CapPeak", [(2.18, 0.28, 0.2, -0.56), (2.21, 0.3, 0.2, -0.58), (2.235, 0.22, 0.14, -0.6)], leather, power=2.4)
    outlined(top)
    outlined(peak)


def tricorn():
    red = toon("Tricorn", srgb(0.65, 0.17, 0.13))
    gold = toon("TricornGold", srgb(1.0, 0.78, 0.18))
    crown = loft("Crown", [(2.14, 0.34, 0.35, -0.24), (2.3, 0.33, 0.33, -0.24), (2.5, 0.25, 0.25, -0.24), (2.54, 0.03, 0.03, -0.24)], red)
    outlined(crown)
    mesh = bpy.data.meshes.new("Brim")
    bm = bmesh.new()
    corners = [(-0.82, -0.14), (0.0, -0.74), (0.82, -0.14), (0.42, 0.42), (-0.42, 0.42)]
    top = [bm.verts.new((x, y - 0.24, 2.42 if i in (0, 2) else 2.2)) for i, (x, y) in enumerate(corners)]
    low = [bm.verts.new((x, y - 0.24, 2.36 if i in (0, 2) else 2.14)) for i, (x, y) in enumerate(corners)]
    bm.faces.new(top)
    bm.faces.new(low[::-1])
    for i in range(5):
        bm.faces.new((low[i], low[(i + 1) % 5], top[(i + 1) % 5], top[i]))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    brim = bpy.data.objects.new("Brim", mesh)
    bpy.context.collection.objects.link(brim)
    mesh.materials.append(red)
    outlined(brim)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.075, location=(0, -0.72, 2.38))
    badge = bpy.context.object
    badge.data.materials.append(gold)
    outlined(badge, 0.015)


def crown():
    gold = toon("Crown", srgb(1.0, 0.78, 0.18))
    ruby = toon("Ruby", srgb(0.88, 0.2, 0.17))
    mesh = bpy.data.meshes.new("CrownRing")
    bm = bmesh.new()
    n, teeth = 40, 5
    low, up = [], []
    for i in range(n):
        a = i * 2 * math.pi / n
        x, y = math.cos(a) * 0.36, math.sin(a) * 0.36 - 0.24
        phase = (i * teeth / n) % 1.0
        height = 0.2 + 0.26 * (1 - abs(phase * 2 - 1))
        low.append(bm.verts.new((x, y, 2.18)))
        up.append(bm.verts.new((x * 1.14 + 0, (y + 0.24) * 1.14 - 0.24, 2.18 + height)))
    for i in range(n):
        bm.faces.new((low[i], low[(i + 1) % n], up[(i + 1) % n], up[i]))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    ring = bpy.data.objects.new("CrownRing", mesh)
    bpy.context.collection.objects.link(ring)
    mesh.materials.append(gold)
    ring.modifiers.new("thick", "SOLIDIFY").thickness = 0.03
    ring.data.materials[0].use_backface_culling = False
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.07, location=(0, -0.645, 2.3))
    stone = bpy.context.object
    stone.data.materials.append(ruby)
    outlined(stone, 0.015)


def horns():
    bone = toon("Horn", srgb(0.93, 0.87, 0.72))
    for side in (-1, 1):
        rings = []
        for i in range(10):
            t = i / 9
            rings.append((2.1 + t * 0.62, 0.11 * (1 - t) + 0.008, 0.11 * (1 - t) + 0.008, -0.24))
        obj = loft("Horn", rings, bone, segments=16)
        for vertex in obj.data.vertices:
            t = (vertex.co.z - 2.1) / 0.62
            vertex.co.x += side * (0.26 + 0.5 * math.sin(t * 1.5))
        outlined(obj, 0.018)


def camera_and_render(path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("w")
    cam = bpy.data.cameras.new("cam")
    cam.lens = 62
    obj = bpy.data.objects.new("cam", cam)
    bpy.context.collection.objects.link(obj)
    obj.location = (0, -6.6, 4.7)
    direction = Vector((0, 0, 1.45)) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = obj
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[zonk_shadow]", path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    folder = argv[argv.index("--render") + 1]
    only_parts = "--parts" in argv
    for name, build in (("bare", None), ("cap", cap), ("tricorn", tricorn), ("crown", crown), ("horns", horns)):
        if only_parts and build is None:
            continue
        clean()
        if not only_parts:
            body()
        if build:
            build()
        camera_and_render(os.path.join(folder, ("acc_%s.png" if only_parts else "shadow_%s.png") % name))


main()
