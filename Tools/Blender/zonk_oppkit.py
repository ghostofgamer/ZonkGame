# -*- coding: utf-8 -*-
"""
Набор деталей соперника-тени: голова, грудь, предмет. Профессию показывают они, дым у всех общий.

Правила набора:
  * читается силуэтом: деталь торчит за контур и узнаётся в маленьком размере;
  * плоские цвета из одной палитры (Art/Textures/OppKit_Palette.png), одна текстура и один материал на все детали;
  * мало треугольников: контур и свет делает Zonk/Toon;
  * ориентир — голова радиуса 0.47: детали головы ставятся в точку над её центром;
  * предмет лежит на столе и не заходит на лоток: settle_on_table сама доворачивает его от рамки.

Координаты Blender: +Y смотрит на игрока, +Z вверх, X зеркален относительно Unity (Unity = (-x, z, -y)).
Голова: центр на (0, 0, -0.425) относительно точки крепления AccessoryAnchor.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_oppkit.py
    blender --background --factory-startup --python Tools/Blender/zonk_oppkit.py -- --preview <папка>
Результат: Art/Models/OppKit/<Имя>.fbx. Подключает генератор (ZonkSetup.OpponentKit).
"""

import math
import os
import re
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.geometry import convex_hull_2d

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "ZonkContent", "Art")
OUT = os.path.join(ART, "Models", "OppKit")

# Палитра: цвет -> клетка. Новый цвет только дописывать в конец: иначе поедут готовые модели.
PALETTE = [
    "#b5803f", "#8a5a2b", "#5b3a1e", "#2b1d14", "#f3e8cf", "#d9c9a3", "#a82d24", "#7a2320",
    "#2f5f9c", "#1d3a66", "#2f7a4c", "#1f5235", "#ffc62e", "#c9982f", "#c9ccd2", "#7c8088",
    "#6a3a7c", "#e87b8a", "#3a3a44", "#16161c", "#e0632a", "#ffffff", "#7fb7c9", "#ece1c1",
    "#e2c36b", "#9be0ff",
]
SIZE = 8
HEAD_Z = -0.425
HEAD_R = 0.47


def uv_of(color):
    index = PALETTE.index(color)
    return ((index % SIZE + 0.5) / SIZE, 1.0 - (index // SIZE + 0.5) / SIZE)


def save_palette():
    cell = 8
    size = SIZE * cell
    image = np.zeros((size, size, 4), dtype=np.float32)
    image[..., 3] = 1.0
    for i, value in enumerate(PALETTE):
        row, column = i // SIZE, i % SIZE
        rgb = [int(value[k:k + 2], 16) / 255 for k in (1, 3, 5)]
        image[row * cell:(row + 1) * cell, column * cell:(column + 1) * cell, :3] = rgb
    bitmap = bpy.data.images.new("OppKit_Palette", size, size, alpha=True)
    bitmap.colorspace_settings.name = "Non-Color"
    bitmap.pixels.foreach_set(np.flipud(image).ravel())
    bitmap.filepath_raw = os.path.join(ART, "Textures", "OppKit_Palette.png")
    bitmap.file_format = "PNG"
    bitmap.save()
    bpy.data.images.remove(bitmap)


class Kit:
    """Одна деталь: куски складываются в общую сетку, у каждого свой цвет."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.smooth = self.bm.faces.layers.int.new("smooth")

    def _finish(self, verts_before, faces, color, smooth):
        u, v = uv_of(color)
        for face in faces:
            face.smooth = smooth
            for loop in face.loops:
                loop[self.uv].uv = (u, v)

    def ring_mesh(self, rings, color, segments=14, smooth=True, cap_top=True, cap_bottom=True):
        """Тело вращения: кольца (z, rx, ry, сдвиг y, сдвиг x)."""
        loops = []
        for ring in rings:
            z, rx, ry = ring[0], ring[1], ring[2]
            dy = ring[3] if len(ring) > 3 else 0.0
            dx = ring[4] if len(ring) > 4 else 0.0
            loop = [self.bm.verts.new((math.cos(i * 2 * math.pi / segments) * rx + dx, math.sin(i * 2 * math.pi / segments) * ry + dy, z)) for i in range(segments)]
            loops.append(loop)
        faces = []
        for a, b in zip(loops, loops[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        if cap_bottom:
            faces.append(self.bm.faces.new(loops[0][::-1]))
        if cap_top:
            faces.append(self.bm.faces.new(loops[-1]))
        self._finish(None, faces, color, smooth)

    def box(self, center, size, color, rotation=None):
        cx, cy, cz = center
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        points = [(x, y, z) for x in (-sx, sx) for y in (-sy, sy) for z in (-sz, sz)]
        matrix = rotation if rotation is not None else Matrix.Identity(3)
        verts = [self.bm.verts.new(matrix @ Vector(p) + Vector((cx, cy, cz))) for p in points]
        order = ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3))
        faces = [self.bm.faces.new([verts[i] for i in face]) for face in order]
        self._finish(None, faces, color, False)

    def ball(self, center, radius, color, scale=(1, 1, 1), segments=12, rings=7):
        cx, cy, cz = center
        steps = []
        for r in range(rings + 1):
            a = -math.pi / 2 + math.pi * r / rings
            k = max(0.01, math.cos(a))
            steps.append((cz + math.sin(a) * radius * scale[2], radius * k * scale[0], radius * k * scale[1], cy, cx))
        self.ring_mesh(steps, color, segments)

    def stick(self, a, b, radius, color, segments=7):
        """Палка между двумя точками."""
        a, b = Vector(a), Vector(b)
        axis = (b - a).normalized()
        side = axis.orthogonal().normalized()
        other = axis.cross(side)
        loops = []
        for point in (a, b):
            loops.append([self.bm.verts.new(point + (side * math.cos(i * 2 * math.pi / segments) + other * math.sin(i * 2 * math.pi / segments)) * radius) for i in range(segments)])
        faces = []
        for i in range(segments):
            j = (i + 1) % segments
            faces.append(self.bm.faces.new((loops[0][i], loops[0][j], loops[1][j], loops[1][i])))
        faces.append(self.bm.faces.new(loops[0][::-1]))
        faces.append(self.bm.faces.new(loops[1]))
        self._finish(None, faces, color, True)

    def cut(self, inside):
        """Убрать грани, центр которых попал в условие: вырез под лицо."""
        doomed = [face for face in self.bm.faces if inside(face.calc_center_median())]
        bmesh.ops.delete(self.bm, geom=doomed, context="FACES")

    def tube(self, points, radii, color, segments=8, flat=1.0):
        """Труба через точки с разной толщиной; flat сплющивает её по вертикали (лента, лезвие)."""
        points = [Vector(p) for p in points]
        axis = (points[-1] - points[0]).normalized()
        up = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((0, 1, 0))
        side = axis.cross(up).normalized()
        other = side.cross(axis).normalized()
        loops = []
        for point, radius in zip(points, radii):
            loops.append([self.bm.verts.new(point + side * math.cos(i * 2 * math.pi / segments) * radius
                                            + other * math.sin(i * 2 * math.pi / segments) * radius * flat)
                          for i in range(segments)])
        faces = []
        for a, b in zip(loops, loops[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        faces.append(self.bm.faces.new(loops[0][::-1]))
        faces.append(self.bm.faces.new(loops[-1]))
        self._finish(None, faces, color, True)

    def lobed(self, rings, color, segments=24, cap=True):
        """Тело вращения с радиусом от угла: (z, функция угла) — треуголка, двууголка."""
        loops = []
        for z, radius in rings:
            loops.append([self.bm.verts.new((math.cos(i * 2 * math.pi / segments) * radius(i * 2 * math.pi / segments),
                                             math.sin(i * 2 * math.pi / segments) * radius(i * 2 * math.pi / segments), z))
                          for i in range(segments)])
        faces = []
        for a, b in zip(loops, loops[1:]):
            for i in range(segments):
                j = (i + 1) % segments
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        if cap:
            faces.append(self.bm.faces.new(loops[0][::-1]))
            faces.append(self.bm.faces.new(loops[-1]))
        self._finish(None, faces, color, False)

    def lat_ring(self, radius, tilt, color, z=0.0, segments=16, thick=0.012):
        """Нитка по шару: кольцо, наклонённое на tilt градусов (клубок)."""
        rot = Matrix.Rotation(math.radians(tilt), 3, "X")
        pts = [rot @ Vector((math.cos(i * 2 * math.pi / segments) * radius, math.sin(i * 2 * math.pi / segments) * radius, 0.0)) + Vector((0, 0, z)) for i in range(segments + 1)]
        for a, b in zip(pts, pts[1:]):
            self.stick(a, b, thick, color, segments=4)

    def yaw(self, degrees):
        """Повернуть готовую деталь вокруг вертикали: на столе лежит наискосок."""
        rot = Matrix.Rotation(math.radians(degrees), 4, "Z")
        bmesh.ops.transform(self.bm, matrix=rot, verts=self.bm.verts)
        return self

    def shift(self, x, y):
        """Сдвинуть готовую деталь по столу."""
        bmesh.ops.translate(self.bm, vec=(x, y, 0.0), verts=self.bm.verts)
        return self

    def build(self):
        check_eyes(self)
        settle_on_table(self)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(palette_material())
        return obj


def palette_material():
    material = bpy.data.materials.get("OppKit")
    if material:
        return material
    material = bpy.data.materials.new("OppKit")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    tex = nodes.new("ShaderNodeTexImage")
    path = os.path.join(ART, "Textures", "OppKit_Palette.png")
    tex.image = bpy.data.images.load(path)
    tex.interpolation = "Closest"
    emit = nodes.new("ShaderNodeEmission")
    out = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(tex.outputs[0], emit.inputs[0])
    material.node_tree.links.new(emit.outputs[0], out.inputs[0])
    return material


# ---------- Голова ----------

def head_flat_cap():
    """Кепка: мягкий верх, козырёк вперёд."""
    k = Kit("Head_FlatCap")
    z = HEAD_Z + 0.26
    k.ring_mesh([(z, 0.47, 0.49), (z + 0.07, 0.56, 0.58, 0.03), (z + 0.15, 0.54, 0.57, 0.05), (z + 0.21, 0.33, 0.36, 0.04), (z + 0.235, 0.05, 0.05, 0.03)], "#b5803f")
    k.ring_mesh([(z - 0.01, 0.36, 0.17, 0.5), (z + 0.03, 0.4, 0.19, 0.53), (z + 0.05, 0.28, 0.1, 0.6)], "#8a5a2b", cap_bottom=True)
    return k


def head_scarf(name="Head_Scarf", main="#a82d24", dark="#7a2320"):
    """Платок: облегает голову, узел под подбородком с хвостами. Спереди вырез: глаза тени лежат за деталью."""
    k = Kit(name)
    c = HEAD_Z
    r = HEAD_R
    k.ring_mesh([(c - 0.32, r * 0.84, r * 0.84, -0.05), (c - 0.05, r * 1.06, r * 1.06, -0.05), (c + 0.22, r * 1.03, r * 1.03, -0.04), (c + 0.42, r * 0.66, r * 0.66, -0.03), (c + 0.5, 0.05, 0.05, -0.02)], main, cap_bottom=False, segments=20)
    # Вырез под лицо.
    k.cut(lambda p: p.y > 0.08 and p.z < c + 0.27)
    k.ball((0.0, 0.36, c - 0.42), 0.09, dark)
    k.ring_mesh([(c - 0.75, 0.13, 0.04, 0.36, 0.13), (c - 0.45, 0.04, 0.04, 0.36, 0.03)], dark, segments=6)
    k.ring_mesh([(c - 0.75, 0.13, 0.04, 0.36, -0.13), (c - 0.45, 0.04, 0.04, 0.36, -0.03)], dark, segments=6)
    return k


def head_scarf_dark():
    return head_scarf("Head_ScarfDark", "#6a3a7c", "#3a3a44")


def head_ushanka():
    """Ушанка: меховой верх и уши в стороны."""
    k = Kit("Head_Ushanka")
    z = HEAD_Z + 0.2
    k.ring_mesh([(z, 0.51, 0.51), (z + 0.13, 0.56, 0.56), (z + 0.3, 0.49, 0.49), (z + 0.37, 0.05, 0.05)], "#5b3a1e")
    k.ring_mesh([(z - 0.01, 0.53, 0.2, 0.42), (z + 0.15, 0.58, 0.2, 0.46), (z + 0.21, 0.48, 0.14, 0.47)], "#d9c9a3")
    for side in (-1, 1):
        k.ball((side * 0.56, 0.0, z - 0.16), 0.22, "#d9c9a3", scale=(0.48, 0.92, 1.05))
    return k


def head_postman():
    """Фуражка почтальона: круглый верх, чёрный козырёк, золотая кокарда."""
    k = Kit("Head_Postman")
    z = HEAD_Z + 0.27
    k.ring_mesh([(z, 0.47, 0.47), (z + 0.1, 0.48, 0.48), (z + 0.15, 0.62, 0.6, 0.05), (z + 0.22, 0.62, 0.6, 0.05), (z + 0.25, 0.08, 0.08, 0.05)], "#2f5f9c")
    k.ring_mesh([(z - 0.02, 0.38, 0.2, 0.44), (z + 0.02, 0.4, 0.2, 0.46), (z + 0.03, 0.3, 0.11, 0.55)], "#16161c")
    k.ball((0.0, 0.5, z + 0.12), 0.08, "#ffc62e", scale=(1, 0.4, 1))
    return k


# ---------- Общие заготовки ----------

STRAW, CRYSTAL = "#e2c36b", "#9be0ff"
# Низ детали головы: ниже точка спереди закроет глаза (камера партии смотрит сверху на 41°). Считает check_eyes.
EYE_LINE = -0.6


def hat_base(k, z, r, h, color, top=0.8, dy=0.0):
    """Тулья: от обода до скруглённого верха."""
    k.ring_mesh([(z, r, r, dy), (z + h * 0.75, r * 0.98, r * 0.98, dy), (z + h, r * top, r * top, dy), (z + h * 1.03, 0.04, 0.04, dy)], color)


def hat_brim(k, z, r, color, dy=0.0, ry=None):
    """Поля шляпы: плоское кольцо с толщиной."""
    ry = ry or r
    k.ring_mesh([(z, r * 0.9, ry * 0.9, dy), (z + 0.015, r, ry, dy), (z + 0.045, r, ry, dy), (z + 0.06, r * 0.9, ry * 0.9, dy)], color, segments=16)


def hat_band(k, z, r, h, color, dy=0.0):
    k.ring_mesh([(z, r * 1.015, r * 1.015, dy), (z + h, r * 1.015, r * 1.015, dy)], color, cap_top=False, cap_bottom=False)


def head_ring(k, z, h, color, r=0.5):
    """Повязка вокруг головы."""
    # Без крышки: сверху виден дым, а не диск. Внутренняя стенка, чтобы дальняя сторона не пропадала.
    k.ring_mesh([(z, r, r), (z + h * 0.5, r * 1.03, r * 1.03), (z + h, r * 0.98, r * 0.98), (z + h, r * 0.86, r * 0.86), (z, r * 0.86, r * 0.86), (z, r, r)],
                color, cap_top=False, cap_bottom=False)


# ---------- Голова: новые ----------

def head_chef():
    k = Kit("Head_Chef")
    z = -0.14
    hat_band(k, z, 0.5, 0.14, "#d9c9a3")
    k.ring_mesh([(z + 0.13, 0.5, 0.5), (z + 0.26, 0.58, 0.58), (z + 0.46, 0.66, 0.66), (z + 0.62, 0.58, 0.58), (z + 0.7, 0.28, 0.28)], "#ffffff")
    return k


def head_straw(name="Head_StrawHat", band="#a82d24", main=STRAW):
    k = Kit(name)
    hat_brim(k, 0.05, 0.76, main, dy=-0.08)
    hat_base(k, 0.08, 0.42, 0.27, main, dy=-0.1)
    hat_band(k, 0.09, 0.42, 0.08, band, dy=-0.1)
    return k


def head_sun_hat():
    k = head_straw("Head_SunHat", "#e87b8a", "#ece1c1")
    k.ball((0.3, 0.2, 0.16), 0.1, "#e87b8a", segments=8, rings=4)
    return k


def head_bee_hat():
    k = head_straw("Head_BeeHat", "#2b1d14")
    # Сетка по бокам и сзади, спереди открыта.
    k.ring_mesh([(-0.75, 0.62, 0.62, -0.1), (0.05, 0.72, 0.72, -0.1)], "#c9ccd2", segments=16, cap_top=False, cap_bottom=False)
    k.cut(lambda p: p.y > -0.05 and p.z < 0.04 and abs(p.x) < 0.75)
    return k


def head_bowler():
    k = Kit("Head_Bowler")
    hat_brim(k, -0.07, 0.62, "#16161c", dy=-0.08)
    k.ring_mesh([(-0.05, 0.46, 0.46, -0.08), (0.08, 0.46, 0.46, -0.08), (0.24, 0.38, 0.38, -0.08), (0.32, 0.12, 0.12, -0.08)], "#2b1d14")
    hat_band(k, -0.04, 0.46, 0.06, "#7c8088", dy=-0.08)
    return k


def head_top_hat(name="Head_TopHat", main="#16161c", band="#a82d24"):
    k = Kit(name)
    hat_brim(k, -0.05, 0.66, main, dy=-0.1)
    k.ring_mesh([(-0.03, 0.4, 0.4, -0.1), (0.3, 0.37, 0.37, -0.1), (0.62, 0.44, 0.44, -0.1), (0.64, 0.38, 0.38, -0.1)], main)
    hat_band(k, -0.01, 0.405, 0.12, band, dy=-0.1)
    return k


def head_magic_hat():
    k = head_top_hat("Head_MagicHat", "#6a3a7c", "#ffc62e")
    for x, z in ((-0.24, 0.3), (0.2, 0.42), (0.05, 0.22)):
        k.ball((x, 0.3, z), 0.035, "#ffc62e", segments=6, rings=3)
    return k


def head_fedora():
    k = Kit("Head_Fedora")
    hat_brim(k, -0.03, 0.72, "#3a3a44", dy=-0.1)
    k.ring_mesh([(0.0, 0.43, 0.46, -0.1), (0.18, 0.41, 0.44, -0.1), (0.3, 0.3, 0.36, -0.1), (0.33, 0.04, 0.2, -0.1)], "#3a3a44")
    hat_band(k, 0.0, 0.44, 0.08, "#a82d24", dy=-0.1)
    return k


def bandana(name, main, dark, skull=False):
    k = Kit(name)
    head_ring(k, -0.2, 0.26, main, 0.5)
    k.ring_mesh([(0.05, 0.48, 0.48), (0.08, 0.4, 0.4), (0.1, 0.06, 0.06)], main)
    k.ball((0.0, -0.5, -0.1), 0.09, dark, segments=8, rings=4)
    for side in (-1, 1):
        k.tube([(side * 0.05, -0.52, -0.1), (side * 0.32, -0.64, -0.3)], [0.05, 0.09], dark, flat=0.35)
    if skull:
        k.ball((0.0, 0.5, -0.06), 0.1, "#f3e8cf", scale=(1, 0.3, 1), segments=8, rings=4)
    return k


def head_bandana():
    return bandana("Head_Bandana", "#a82d24", "#7a2320")


def head_bandana_dark():
    return bandana("Head_BandanaDark", "#3a3a44", "#16161c")


def head_pirate():
    return bandana("Head_PirateBandana", "#a82d24", "#7a2320", True)


def head_eye_patch():
    """Одноглазый: чёрная повязка закрывает глаз нарочно (исключение в check_eyes)."""
    k = bandana("Head_EyePatch", "#5b3a1e", "#2b1d14")
    k.ball((0.2, 0.5, -0.27), 0.14, "#16161c", scale=(1, 0.3, 1), segments=10, rings=4)
    k.tube([(0.02, 0.5, -0.12), (0.34, 0.48, -0.38)], [0.022, 0.022], "#16161c")
    return k


def head_beret(name="Head_Beret", main="#a82d24", feather=None):
    k = Kit(name)
    head_ring(k, -0.13, 0.08, "#2b1d14", 0.49)
    k.ring_mesh([(-0.06, 0.5, 0.5, -0.02, 0.1), (0.04, 0.68, 0.64, -0.03, 0.14), (0.13, 0.62, 0.58, -0.04, 0.14), (0.17, 0.2, 0.2, -0.04, 0.1)], main)
    if feather:
        k.tube([(-0.3, 0.1, 0.1), (-0.55, -0.1, 0.5), (-0.68, -0.28, 0.8)], [0.04, 0.09, 0.015], feather, flat=0.3)
    return k


def head_bard():
    return head_beret("Head_BardBeret", "#2f7a4c", "#ffc62e")


def tricorn(name, main, trim, r=0.62, lift=0.26):
    k = Kit(name)
    def edge(a):
        return r + 0.24 * math.cos(3 * (a - math.pi / 2))
    k.lobed([(-0.03, lambda a: 0.45), (0.03, lambda a: 0.48), (0.04 + lift * 0.6, edge), (0.06 + lift * 0.7, edge)], main)
    k.lobed([(0.06 + lift * 0.7, edge), (0.075 + lift * 0.7, lambda a: edge(a) * 1.03)], trim, cap=False)
    hat_base(k, 0.0, 0.42, 0.33, main, top=0.72)
    return k


def head_tricorn():
    return tricorn("Head_Tricorn", "#3a3a44", "#c9ccd2", r=0.56)


def head_captain():
    k = tricorn("Head_Captain", "#16161c", "#ffc62e", r=0.68, lift=0.34)
    k.ball((0.0, 0.42, 0.2), 0.1, "#f3e8cf", scale=(1, 0.4, 1), segments=8, rings=4)
    return k


def head_bicorn():
    k = Kit("Head_Bicorn")
    def edge(a):
        return 0.5 + 0.34 * abs(math.cos(a)) ** 1.5
    k.lobed([(-0.03, lambda a: 0.45), (0.05, lambda a: 0.5), (0.22, edge), (0.26, edge)], "#1d3a66")
    hat_base(k, 0.0, 0.42, 0.3, "#1d3a66", top=0.72)
    k.ball((0.0, 0.44, 0.15), 0.07, "#ffc62e", scale=(1, 0.4, 1), segments=8, rings=4)
    return k


def head_sailor():
    k = Kit("Head_SailorCap")
    hat_band(k, -0.1, 0.48, 0.12, "#1d3a66")
    k.ring_mesh([(0.01, 0.48, 0.48), (0.11, 0.62, 0.62), (0.15, 0.6, 0.6), (0.17, 0.3, 0.3)], "#ffffff")
    for side in (-1, 1):
        k.tube([(side * 0.08, -0.5, -0.08), (side * 0.24, -0.6, -0.45)], [0.045, 0.06], "#1d3a66", flat=0.3)
    return k


def peaked(name, main, band, badge="#ffc62e"):
    k = Kit(name)
    z = -0.13
    k.ring_mesh([(z, 0.47, 0.47), (z + 0.1, 0.48, 0.48), (z + 0.2, 0.64, 0.62, -0.03), (z + 0.27, 0.64, 0.62, -0.03), (z + 0.3, 0.2, 0.2, -0.03)], main)
    hat_band(k, z, 0.47, 0.08, band)
    k.tube([(-0.38, 0.32, z + 0.04), (0.0, 0.56, z + 0.1), (0.38, 0.32, z + 0.04)], [0.05, 0.1, 0.05], "#16161c", flat=0.3)
    k.ball((0.0, 0.5, z + 0.17), 0.07, badge, scale=(1, 0.4, 1), segments=8, rings=4)
    return k


def head_customs():
    return peaked("Head_Customs", "#1f5235", "#a82d24")


def head_officer():
    return peaked("Head_Officer", "#1d3a66", "#ffc62e", "#ffffff")


def head_visor():
    """Спасатель: красный козырёк на резинке."""
    k = Kit("Head_Visor")
    head_ring(k, -0.17, 0.12, "#a82d24", 0.49)
    k.tube([(-0.42, 0.28, -0.12), (0.0, 0.66, -0.0), (0.42, 0.28, -0.12)], [0.05, 0.16, 0.05], "#ffffff", flat=0.22)
    return k


def head_surfer():
    k = bandana("Head_SurfBand", "#7fb7c9", "#2f5f9c")
    for x in (-0.26, 0.0, 0.26):
        k.ball((x, 0.46, -0.1), 0.05, "#ffc62e", scale=(1, 0.4, 1), segments=6, rings=3)
    return k


def head_bucket():
    """Панама рыбака, с блесной на полях."""
    k = Kit("Head_Bucket")
    k.ring_mesh([(0.02, 0.72, 0.7, -0.08), (0.08, 0.5, 0.5, -0.08), (0.27, 0.42, 0.42, -0.08), (0.3, 0.08, 0.08, -0.08)], "#2f7a4c")
    k.ball((0.42, 0.2, 0.07), 0.05, "#c9ccd2", scale=(1, 0.5, 1.5), segments=6, rings=3)
    return k


def tiara(name, band, gem, spikes=7, tall=0.2):
    k = Kit(name)
    head_ring(k, -0.1, 0.08, band, 0.48)
    for i in range(spikes):
        a = math.pi * (i + 0.5) / spikes
        x, y = math.cos(a) * 0.47, math.sin(a) * 0.47
        h = tall * (1.3 if i == spikes // 2 else 0.9)
        k.tube([(x, y, -0.04), (x, y + 0.02, -0.04 + h)], [0.06, 0.01], band, segments=5)
        k.ball((x, y + 0.02, -0.04 + h * 0.5), 0.035, gem, segments=6, rings=3)
    return k


def head_crown():
    return tiara("Head_Crown", "#ffc62e", "#a82d24")


def head_rose_tiara():
    k = tiara("Head_RoseTiara", "#2f7a4c", "#2f7a4c", 5, 0.12)
    for x, y, z, r in ((-0.26, 0.4, 0.0, 0.1), (0.0, 0.47, 0.06, 0.13), (0.26, 0.4, 0.0, 0.1)):
        k.ball((x, y, z), r, "#a82d24", segments=8, rings=4)
    return k


def head_crab():
    """Мадам Клешня: шляпка и две клешни вверх."""
    k = Kit("Head_Crab")
    head_ring(k, -0.1, 0.1, "#e0632a", 0.48)
    for side in (-1, 1):
        k.tube([(side * 0.34, 0.0, 0.0), (side * 0.55, 0.0, 0.3)], [0.08, 0.06], "#e0632a")
        k.ball((side * 0.6, 0.0, 0.42), 0.15, "#a82d24", scale=(0.8, 0.6, 1.1), segments=8, rings=5)
        k.tube([(side * 0.68, 0.0, 0.5), (side * 0.74, 0.0, 0.72)], [0.06, 0.012], "#a82d24", segments=5)
        k.tube([(side * 0.52, 0.0, 0.5), (side * 0.5, 0.0, 0.7)], [0.06, 0.012], "#a82d24", segments=5)
    return k


def head_cat():
    k = Kit("Head_CatEars")
    for side in (-1, 1):
        k.tube([(side * 0.33, 0.0, -0.05), (side * 0.42, 0.0, 0.42)], [0.2, 0.01], "#e0632a", segments=4, flat=0.5)
        k.tube([(side * 0.34, 0.09, -0.02), (side * 0.41, 0.09, 0.27)], [0.11, 0.01], "#e87b8a", segments=4, flat=0.3)
    return k


def head_raven():
    """Клюв намеренно ниже глаз (исключение в check_eyes) и хохолок."""
    k = Kit("Head_Raven")
    k.tube([(0.0, 0.42, -0.5), (0.0, 0.92, -0.72)], [0.15, 0.01], "#3a3a44", segments=6)
    for x, t in ((-0.12, 0.28), (0.0, 0.42), (0.12, 0.28)):
        k.tube([(x, -0.08, -0.03), (x * 1.5, -0.3, t)], [0.08, 0.01], "#3a3a44", segments=5, flat=0.35)
    return k


def head_ghost():
    """Чепец привидения: ажурный край и боковые ленты, лицо открыто."""
    k = Kit("Head_Ghost")
    k.ring_mesh([(-0.12, 0.5, 0.5, -0.06), (0.04, 0.54, 0.54, -0.08), (0.18, 0.42, 0.42, -0.1), (0.24, 0.1, 0.1, -0.1)], "#c9ccd2")
    for i in range(9):
        a = math.pi * i / 8
        k.ball((math.cos(a) * 0.51, math.sin(a) * 0.45 - 0.06, -0.12), 0.05, "#ffffff", segments=6, rings=3)
    for side in (-1, 1):
        k.tube([(side * 0.5, -0.04, -0.14), (side * 0.56, -0.02, -0.7)], [0.09, 0.13], "#c9ccd2", flat=0.25)
    return k


def head_loupe():
    k = Kit("Head_Loupe")
    head_ring(k, -0.16, 0.1, "#5b3a1e", 0.49)
    k.tube([(0.2, 0.42, -0.08), (0.24, 0.62, 0.02)], [0.1, 0.13], "#ffc62e")
    k.tube([(0.24, 0.63, 0.025), (0.245, 0.65, 0.033)], [0.1, 0.1], CRYSTAL)
    return k


def head_turban():
    k = Kit("Head_Turban")
    k.ring_mesh([(-0.14, 0.5, 0.5), (0.0, 0.6, 0.6), (0.12, 0.56, 0.56, -0.03), (0.26, 0.44, 0.44, -0.04), (0.38, 0.2, 0.2, -0.04), (0.41, 0.04, 0.04, -0.04)], "#6a3a7c")
    k.tube([(-0.56, 0.1, -0.04), (0.0, 0.52, 0.2), (0.48, 0.14, 0.29)], [0.06, 0.08, 0.05], "#e87b8a")
    k.ball((0.0, 0.54, 0.1), 0.09, "#ffc62e", scale=(1, 0.5, 1.2), segments=8, rings=4)
    return k


def head_miner():
    k = Kit("Head_Miner")
    hat_brim(k, -0.07, 0.58, "#c9982f", dy=-0.04)
    k.ring_mesh([(-0.03, 0.48, 0.48), (0.1, 0.47, 0.47), (0.25, 0.36, 0.36), (0.33, 0.12, 0.12)], "#ffc62e")
    k.tube([(0.0, 0.4, 0.09), (0.0, 0.52, 0.11)], [0.09, 0.12], "#3a3a44")
    k.tube([(0.0, 0.53, 0.112), (0.0, 0.55, 0.115)], [0.1, 0.1], "#ffffff")
    return k


def head_jester():
    k = Kit("Head_Jester")
    head_ring(k, -0.14, 0.14, "#ffc62e", 0.5)
    k.ring_mesh([(-0.02, 0.48, 0.48), (0.1, 0.4, 0.4), (0.16, 0.08, 0.08)], "#a82d24")
    for x, color in ((-1, "#a82d24"), (1, "#2f5f9c")):
        k.tube([(x * 0.2, 0.0, 0.08), (x * 0.5, 0.0, 0.42), (x * 0.8, 0.0, 0.42)], [0.2, 0.11, 0.03], color)
        k.ball((x * 0.85, 0.0, 0.38), 0.075, "#ffc62e", segments=8, rings=4)
    k.tube([(0.0, -0.08, 0.1), (0.0, -0.18, 0.62)], [0.17, 0.03], "#2f7a4c")
    k.ball((0.0, -0.18, 0.68), 0.075, "#ffc62e", segments=8, rings=4)
    return k


def head_bun():
    """Бухгалтер: тугой пучок и карандаш в нём."""
    k = Kit("Head_Bun")
    k.ring_mesh([(-0.12, 0.5, 0.5, -0.04), (0.0, 0.5, 0.5, -0.05), (0.08, 0.36, 0.36, -0.06), (0.11, 0.05, 0.05, -0.06)], "#7c8088")
    k.ball((0.0, -0.08, 0.2), 0.2, "#7c8088", segments=10, rings=5)
    k.tube([(-0.3, -0.06, 0.1), (0.32, -0.06, 0.32)], [0.025, 0.025], "#ffc62e")
    return k


def head_worn_hat():
    """Старьёвщик: мятая шляпа с заплатой."""
    k = Kit("Head_WornHat")
    k.ring_mesh([(-0.03, 0.66, 0.6, -0.08, 0.02), (0.02, 0.48, 0.47, -0.08), (0.2, 0.42, 0.4, -0.09, 0.06), (0.28, 0.2, 0.22, -0.1, 0.12), (0.31, 0.03, 0.03, -0.1, 0.12)], "#5b3a1e")
    k.box((-0.2, 0.3, 0.14), (0.16, 0.07, 0.13), "#b5803f")
    return k


def head_merchant():
    """Купец: шапка с меховой оторочкой — родня ушанки."""
    k = Kit("Head_Merchant")
    k.ring_mesh([(0.0, 0.46, 0.46), (0.22, 0.54, 0.54), (0.34, 0.46, 0.46), (0.38, 0.08, 0.08)], "#a82d24")
    k.ring_mesh([(-0.12, 0.49, 0.49), (-0.04, 0.58, 0.58), (0.07, 0.58, 0.58), (0.13, 0.48, 0.48)], "#d9c9a3")
    k.ball((0.0, 0.5, 0.22), 0.06, "#ffc62e", segments=8, rings=4)
    return k


def head_watch():
    """Ночной сторож: ушанка потемнее, уши завязаны наверху."""
    k = Kit("Head_WatchCap")
    z = -0.225
    k.ring_mesh([(z, 0.51, 0.51), (z + 0.13, 0.56, 0.56), (z + 0.3, 0.49, 0.49), (z + 0.37, 0.05, 0.05)], "#1d3a66")
    k.ring_mesh([(z - 0.01, 0.53, 0.2, 0.42), (z + 0.15, 0.58, 0.2, 0.46), (z + 0.21, 0.48, 0.14, 0.47)], "#7c8088")
    for side in (-1, 1):
        k.ball((side * 0.48, -0.02, z + 0.3), 0.2, "#7c8088", scale=(0.7, 0.9, 0.5))
    return k


def head_twins():
    """Близнецы: две одинаковые кепочки рядом."""
    k = Kit("Head_TwinCaps")
    for side in (-1, 1):
        x = side * 0.34
        k.ring_mesh([(-0.03, 0.33, 0.33, 0.0, x), (0.1, 0.36, 0.36, 0.0, x), (0.2, 0.23, 0.23, 0.0, x), (0.23, 0.04, 0.04, 0.0, x)], "#2f5f9c" if side < 0 else "#a82d24")
        k.tube([(x - 0.2, 0.26, 0.0), (x, 0.44, 0.03), (x + 0.2, 0.26, 0.0)], [0.04, 0.09, 0.04], "#ffffff", flat=0.25)
    return k


def head_midnight():
    """Мадам Полночь: шляпка-вуаль с месяцем."""
    k = Kit("Head_Midnight")
    hat_brim(k, 0.0, 0.6, "#16161c", dy=-0.08)
    k.ring_mesh([(0.03, 0.34, 0.34, -0.08, 0.1), (0.2, 0.3, 0.3, -0.08, 0.12), (0.25, 0.1, 0.1, -0.08, 0.12)], "#6a3a7c")
    k.tube([(-0.28, 0.28, 0.14), (-0.42, 0.28, 0.38), (-0.22, 0.28, 0.54)], [0.015, 0.075, 0.015], "#ffc62e", flat=0.4)
    return k


def head_fortune():
    """Фортуна: лавровый венок из золота."""
    k = Kit("Head_Laurel")
    for side in (-1, 1):
        for i in range(6):
            a = math.pi * (i + 0.6) / 13
            x, y = side * math.cos(a) * 0.5, math.sin(a) * 0.5 - 0.03
            z = -0.08 + i * 0.012
            k.ball((x, y, z + 0.05), 0.09, "#ffc62e", scale=(1, 0.5, 1.6), segments=6, rings=3)
    k.ball((0.0, 0.49, -0.04), 0.075, CRYSTAL, scale=(1, 0.5, 1), segments=8, rings=4)
    return k


# ---------- Грудь: всё в полосе 0.12…−0.40, ниже её закрывает стол ----------

def chest_apron(name="Chest_Apron", main="#ece1c1", trim="#d9c9a3"):
    """Фартук: нагрудник сразу под шеей."""
    k = Kit(name)
    k.box((0.0, 0.42, -0.2), (0.62, 0.03, 0.44), main)
    k.box((0.0, 0.44, -0.3), (0.34, 0.03, 0.14), trim)
    for side in (-1, 1):
        k.box((side * 0.25, 0.38, 0.06), (0.08, 0.03, 0.14), main)
    return k


def chest_strap():
    """Ремень сумки через плечо с пряжкой."""
    k = Kit("Chest_Strap")
    k.box((0.0, 0.42, -0.14), (1.1, 0.03, 0.11), "#5b3a1e", Matrix.Rotation(math.radians(-22), 3, "Y"))
    k.box((0.0, 0.45, -0.14), (0.14, 0.03, 0.15), "#ffc62e", Matrix.Rotation(math.radians(-22), 3, "Y"))
    return k


def chest_key_chain():
    """Цепочка с ключом: хранительница сундука."""
    k = Kit("Chest_KeyChain")
    links = 11
    for i in range(links):
        a = math.pi * i / (links - 1)
        k.ball((math.cos(a) * 0.28, 0.38, 0.1 - math.sin(a) * 0.22), 0.036, "#c9982f", segments=6, rings=3)
    k.ring_mesh([(-0.2, 0.08, 0.03, 0.42), (-0.14, 0.1, 0.03, 0.42), (-0.08, 0.08, 0.03, 0.42)], "#ffc62e", segments=8)
    k.stick((0.0, 0.42, -0.2), (0.0, 0.42, -0.38), 0.028, "#ffc62e")
    k.box((0.05, 0.42, -0.36), (0.09, 0.03, 0.035), "#ffc62e")
    return k


def chest_bow_tie(name="Chest_BowTie", color="#a82d24"):
    k = Kit(name)
    for side in (-1, 1):
        k.tube([(side * 0.03, 0.42, -0.03), (side * 0.28, 0.42, -0.03)], [0.05, 0.14], color, flat=0.3)
    k.ball((0.0, 0.44, -0.03), 0.06, "#7a2320" if color == "#a82d24" else "#3a3a44", segments=8, rings=4)
    return k


def chest_bow_black():
    k = chest_bow_tie("Chest_BowBlack", "#ffffff")
    return k


def chest_tie():
    k = Kit("Chest_Tie")
    k.ring_mesh([(0.0, 0.3, 0.1, 0.34), (0.09, 0.3, 0.12, 0.34)], "#ffffff", segments=10)
    k.tube([(0.0, 0.44, 0.0), (0.0, 0.46, -0.3), (0.0, 0.46, -0.4)], [0.05, 0.1, 0.015], "#a82d24", flat=0.25)
    return k


def chest_medal():
    k = Kit("Chest_Medal")
    k.box((0.0, 0.41, -0.05), (0.16, 0.03, 0.2), "#a82d24")
    k.ball((0.0, 0.43, -0.22), 0.11, "#ffc62e", scale=(1, 0.3, 1), segments=10, rings=4)
    k.ball((0.0, 0.46, -0.22), 0.05, "#c9982f", scale=(1, 0.3, 1), segments=8, rings=3)
    return k


def beads(name, color, big=None, r=0.28, count=13, size=0.042):
    k = Kit(name)
    for i in range(count):
        a = math.pi * i / (count - 1)
        k.ball((math.cos(a) * r, 0.38, 0.1 - math.sin(a) * r * 1.05), size, color, segments=8, rings=4)
    if big:
        k.ball((0.0, 0.42, 0.1 - r * 1.05 - 0.08), 0.09, big, segments=8, rings=4)
    return k


def chest_pearls():
    return beads("Chest_Pearls", "#ffffff", "#e87b8a")


def chest_beads():
    return beads("Chest_Beads", "#2f7a4c", "#ffc62e", count=11, size=0.05)


def chest_shells():
    return beads("Chest_Shells", "#7fb7c9", "#ece1c1", count=9, size=0.05)


def chest_bell():
    k = beads("Chest_Bell", "#a82d24", None, count=15, size=0.04)
    k.ball((0.0, 0.43, -0.28), 0.1, "#ffc62e", segments=10, rings=5)
    return k


def chest_whistle():
    k = beads("Chest_Whistle", "#2f5f9c", None, count=13, size=0.02)
    k.tube([(-0.09, 0.42, -0.24), (0.08, 0.42, -0.24)], [0.06, 0.06], "#c9ccd2")
    k.box((0.13, 0.42, -0.24), (0.1, 0.05, 0.06), "#c9ccd2")
    return k


def chest_sailor():
    k = Kit("Chest_SailorCollar")
    for side in (-1, 1):
        k.box((side * 0.2, 0.41, -0.1), (0.36, 0.03, 0.3), "#1d3a66", Matrix.Rotation(math.radians(side * 30), 3, "Y"))
        k.box((side * 0.2, 0.43, -0.13), (0.3, 0.03, 0.03), "#ffffff", Matrix.Rotation(math.radians(side * 30), 3, "Y"))
    k.ball((0.0, 0.45, -0.28), 0.07, "#a82d24", scale=(1, 0.5, 1), segments=8, rings=4)
    return k


def chest_bandolier():
    k = Kit("Chest_Bandolier")
    rot = Matrix.Rotation(math.radians(26), 3, "Y")
    k.box((0.0, 0.42, -0.13), (1.1, 0.03, 0.14), "#5b3a1e", rot)
    for i in range(-3, 4):
        p = rot @ Vector((i * 0.13, 0.0, 0.0))
        k.box((p.x, 0.45, -0.13 + p.z), (0.05, 0.03, 0.1), "#ffc62e", rot)
    return k


def chest_watch_chain():
    k = beads("Chest_WatchChain", "#ffc62e", None, r=0.3, count=15, size=0.02)
    k.ball((0.0, 0.43, -0.27), 0.12, "#ffc62e", scale=(1, 0.3, 1), segments=10, rings=4)
    k.ball((0.0, 0.46, -0.27), 0.085, "#ffffff", scale=(1, 0.2, 1), segments=10, rings=4)
    return k


def chest_jester():
    k = Kit("Chest_JesterCollar")
    for i in range(6):
        x = -0.4 + i * 0.16
        k.tube([(x, 0.42, 0.03), (x, 0.46, -0.24)], [0.1, 0.01], "#a82d24" if i % 2 else "#2f5f9c", segments=4, flat=0.25)
        k.ball((x, 0.47, -0.25), 0.04, "#ffc62e", segments=6, rings=3)
    return k


def chest_cross_ribbon():
    k = Kit("Chest_Ribbon")
    k.box((0.0, 0.42, -0.13), (1.1, 0.03, 0.13), "#2f5f9c", Matrix.Rotation(math.radians(-24), 3, "Y"))
    k.ball((-0.1, 0.45, -0.17), 0.1, "#ffc62e", scale=(1, 0.3, 1), segments=8, rings=4)
    return k


# ---------- Предметы: лежат на столе, начало координат — на столешнице ----------

def prop_die():
    k = Kit("Prop_Die")
    k.box((0.0, 0.0, 0.15), (0.3, 0.3, 0.3), "#f3e8cf")
    for dx, dz in ((-0.08, 0.23), (0.0, 0.15), (0.08, 0.07)):
        k.ball((dx, 0.155, dz), 0.028, "#a82d24", scale=(1, 0.3, 1), segments=6, rings=3)
    k.ball((0.0, 0.0, 0.305), 0.03, "#a82d24", scale=(1, 1, 0.3), segments=6, rings=3)
    return k.yaw(30)


def prop_rolling_pin():
    k = Kit("Prop_RollingPin")
    k.tube([(-0.4, 0.0, 0.1), (-0.37, 0.0, 0.1), (0.37, 0.0, 0.1), (0.4, 0.0, 0.1)], [0.085, 0.1, 0.1, 0.085], "#b5803f", segments=10)
    k.stick((-0.62, 0, 0.1), (-0.4, 0, 0.1), 0.038, "#8a5a2b")
    k.stick((0.4, 0, 0.1), (0.62, 0, 0.1), 0.038, "#8a5a2b")
    return k.yaw(25)


def prop_mug():
    k = Kit("Prop_Mug")
    k.ring_mesh([(0.0, 0.17, 0.17), (0.36, 0.19, 0.19)], "#c9982f", segments=12)
    k.ring_mesh([(0.36, 0.195, 0.195), (0.45, 0.18, 0.18), (0.47, 0.03, 0.03)], "#f3e8cf", segments=12)
    k.tube([(0.17, 0.0, 0.3), (0.3, 0.0, 0.28), (0.3, 0.0, 0.12), (0.17, 0.0, 0.08)], [0.03] * 4, "#8a5a2b", segments=6)
    return k


def prop_letter():
    k = Kit("Prop_Letter")
    k.box((0.0, 0.0, 0.02), (0.6, 0.42, 0.04), "#f3e8cf")
    k.box((-0.15, 0.08, 0.045), (0.38, 0.03, 0.015), "#d9c9a3", Matrix.Rotation(math.radians(30), 3, "Z"))
    k.box((0.15, 0.08, 0.045), (0.38, 0.03, 0.015), "#d9c9a3", Matrix.Rotation(math.radians(-30), 3, "Z"))
    k.ball((0.0, 0.0, 0.05), 0.065, "#a82d24", scale=(1, 1, 0.4), segments=8, rings=3)
    return k.yaw(20)


def prop_big_key():
    k = Kit("Prop_BigKey")
    k.tube([(-0.44, 0.0, 0.05), (0.2, 0.0, 0.05)], [0.05, 0.05], "#ffc62e")
    k.ring_mesh([(0.0, 0.17, 0.17, 0.0, 0.34), (0.1, 0.17, 0.17, 0.0, 0.34)], "#ffc62e", segments=10)
    k.ring_mesh([(0.09, 0.09, 0.09, 0.0, 0.34), (0.105, 0.09, 0.09, 0.0, 0.34)], "#5b3a1e", segments=10)
    k.box((-0.38, 0.1, 0.05), (0.05, 0.2, 0.05), "#ffc62e")
    k.box((-0.28, 0.08, 0.05), (0.05, 0.16, 0.05), "#ffc62e")
    return k.yaw(25)


def prop_coins():
    k = Kit("Prop_Coins")
    for i, (x, y) in enumerate(((0, 0), (0.01, 0.005), (-0.01, 0.0), (0.012, -0.008), (0.0, 0.0))):
        z = i * 0.055
        k.ring_mesh([(z, 0.16, 0.16, y, x), (z + 0.05, 0.16, 0.16, y, x)], "#ffc62e" if i % 2 else "#c9982f", segments=12)
    k.ring_mesh([(0.0, 0.16, 0.16, 0.2, 0.3), (0.05, 0.16, 0.16, 0.2, 0.3)], "#ffc62e", segments=12)
    return k


def prop_club():
    k = Kit("Prop_Club")
    k.tube([(-0.55, 0.0, 0.08), (0.0, 0.0, 0.1), (0.5, 0.0, 0.14)], [0.05, 0.08, 0.14], "#8a5a2b")
    k.tube([(-0.55, 0.0, 0.08), (-0.4, 0.0, 0.08)], [0.06, 0.06], "#2b1d14")
    return k.yaw(30)


def prop_cards():
    k = Kit("Prop_Cards")
    for i, a in enumerate((-28, -8, 12, 32)):
        rot = Matrix.Rotation(math.radians(a), 3, "Z")
        p = rot @ Vector((0.0, 0.14, 0.0))
        k.box((p.x, p.y, 0.012 + i * 0.012), (0.3, 0.44, 0.012), "#ffffff", rot)
        p2 = rot @ Vector((0.0, 0.14, 0.0))
        k.ball((p2.x, p2.y, 0.024 + i * 0.012), 0.05, "#a82d24" if i % 2 else "#16161c", scale=(1, 1.3, 0.2), segments=4, rings=3)
    return k


def prop_dagger():
    k = Kit("Prop_Dagger")
    k.tube([(-0.05, 0.0, 0.04), (0.56, 0.0, 0.04)], [0.08, 0.008], "#c9ccd2", segments=4, flat=0.3)
    k.box((-0.06, 0.0, 0.04), (0.04, 0.26, 0.05), "#ffc62e")
    k.tube([(-0.3, 0.0, 0.04), (-0.08, 0.0, 0.04)], [0.04, 0.04], "#5b3a1e")
    k.ball((-0.32, 0.0, 0.04), 0.05, "#ffc62e", segments=6, rings=3)
    return k.yaw(30)


def prop_lute():
    k = Kit("Prop_Lute")
    k.ball((0.15, 0.0, 0.1), 0.28, "#b5803f", scale=(1.15, 0.92, 0.36), segments=12, rings=6)
    k.ring_mesh([(0.2, 0.08, 0.08, 0.0, 0.15), (0.21, 0.08, 0.08, 0.0, 0.15)], "#2b1d14", segments=10)
    k.box((-0.34, 0.0, 0.14), (0.6, 0.08, 0.03), "#5b3a1e")
    k.box((-0.68, 0.0, 0.17), (0.12, 0.11, 0.04), "#8a5a2b", Matrix.Rotation(math.radians(20), 3, "Y"))
    return k.yaw(35)


def prop_fish():
    k = Kit("Prop_Fish")
    k.ball((0.0, 0.0, 0.1), 0.32, "#7fb7c9", scale=(1.25, 0.4, 0.34), segments=10, rings=6)
    k.tube([(-0.38, 0.0, 0.1), (-0.62, 0.0, 0.1)], [0.03, 0.14], "#2f5f9c", flat=0.25)
    k.ball((0.27, 0.075, 0.13), 0.035, "#16161c", segments=6, rings=3)
    k.tube([(0.0, 0.0, 0.2), (-0.12, 0.0, 0.3)], [0.1, 0.01], "#2f5f9c", segments=4, flat=0.2)
    return k.yaw(20)


def prop_surfboard():
    k = Kit("Prop_Surfboard")
    k.ball((0.0, 0.0, 0.05), 0.65, "#7fb7c9", scale=(1.0, 0.27, 0.07), segments=14, rings=6)
    k.box((0.0, 0.0, 0.092), (0.9, 0.06, 0.012), "#ffffff")
    k.box((0.0, 0.0, 0.094), (0.05, 0.28, 0.012), "#e0632a")
    return k.yaw(28)


def prop_corn():
    k = Kit("Prop_Corn")
    k.tube([(-0.32, 0.0, 0.11), (0.0, 0.0, 0.12), (0.36, 0.0, 0.11)], [0.09, 0.12, 0.03], "#ffc62e", segments=9)
    for side in (-1, 1):
        k.tube([(-0.42, 0.0, 0.08), (-0.1, side * 0.13, 0.06), (0.14, side * 0.2, 0.05)], [0.04, 0.09, 0.008], "#2f7a4c", flat=0.25)
    return k.yaw(25)


def prop_lifebuoy():
    k = Kit("Prop_Lifebuoy")
    for i in range(16):
        a0, a1 = i * math.pi / 8, (i + 1) * math.pi / 8
        k.tube([(math.cos(a0) * 0.26, math.sin(a0) * 0.26, 0.08), (math.cos(a1) * 0.26, math.sin(a1) * 0.26, 0.08)], [0.08, 0.08], "#a82d24" if (i // 2) % 2 else "#ffffff", segments=6)
    return k


def prop_teacup():
    k = Kit("Prop_Teacup")
    k.ring_mesh([(0.0, 0.26, 0.26), (0.03, 0.26, 0.26), (0.04, 0.13, 0.13)], "#ffffff", segments=14)
    k.ring_mesh([(0.04, 0.09, 0.09), (0.2, 0.17, 0.17)], "#ffffff", segments=12)
    k.ring_mesh([(0.2, 0.15, 0.15), (0.205, 0.15, 0.15)], "#8a5a2b", segments=12)
    k.ring_mesh([(0.17, 0.172, 0.172), (0.19, 0.172, 0.172)], "#e87b8a", segments=12, cap_top=False, cap_bottom=False)
    k.tube([(0.15, 0.0, 0.17), (0.24, 0.0, 0.15), (0.2, 0.0, 0.08)], [0.018] * 3, "#ffffff", segments=5)
    return k


def prop_claw():
    k = Kit("Prop_Claw")
    k.ball((0.0, 0.0, 0.09), 0.25, "#a82d24", scale=(1.3, 0.8, 0.38), segments=10, rings=5)
    k.tube([(0.26, 0.09, 0.09), (0.62, 0.15, 0.09)], [0.13, 0.015], "#e0632a", segments=5, flat=0.5)
    k.tube([(0.26, -0.09, 0.09), (0.56, -0.15, 0.09)], [0.1, 0.015], "#a82d24", segments=5, flat=0.5)
    k.tube([(-0.3, 0.0, 0.08), (-0.52, 0.0, 0.07)], [0.08, 0.05], "#e0632a")
    return k.yaw(25)


def prop_mop():
    k = Kit("Prop_Mop")
    k.stick((-0.65, 0, 0.04), (0.35, 0, 0.05), 0.03, "#b5803f")
    for i in range(5):
        y = -0.16 + i * 0.08
        k.tube([(0.33, y * 0.4, 0.05), (0.68, y * 1.3, 0.03)], [0.035, 0.05], "#ece1c1", segments=5)
    return k.yaw(28)


def prop_ladle():
    k = Kit("Prop_Ladle")
    k.ring_mesh([(0.0, 0.05, 0.05, 0.0, 0.42), (0.04, 0.15, 0.15, 0.0, 0.42), (0.1, 0.17, 0.17, 0.0, 0.42)], "#c9ccd2", segments=12, cap_top=False)
    k.ring_mesh([(0.09, 0.15, 0.15, 0.0, 0.42), (0.095, 0.15, 0.15, 0.0, 0.42)], "#e0632a", segments=12)
    k.tube([(-0.55, 0.0, 0.05), (0.27, 0.0, 0.1)], [0.025, 0.025], "#7c8088")
    return k.yaw(25)


def prop_compass():
    k = Kit("Prop_Compass")
    k.ring_mesh([(0.0, 0.26, 0.26), (0.07, 0.26, 0.26), (0.08, 0.24, 0.24)], "#c9982f", segments=16)
    k.ring_mesh([(0.08, 0.215, 0.215), (0.085, 0.215, 0.215)], "#f3e8cf", segments=16)
    k.tube([(0.0, -0.17, 0.095), (0.0, 0.17, 0.095)], [0.04, 0.005], "#a82d24", segments=4, flat=0.15)
    return k.yaw(25)


def prop_cutlass():
    k = Kit("Prop_Cutlass")
    k.tube([(-0.1, 0.0, 0.04), (0.3, 0.05, 0.04), (0.7, 0.0, 0.04)], [0.06, 0.08, 0.005], "#c9ccd2", segments=4, flat=0.25)
    k.tube([(-0.3, 0.0, 0.04), (-0.1, 0.0, 0.04)], [0.035, 0.035], "#2b1d14")
    k.tube([(-0.32, 0.0, 0.04), (-0.2, -0.15, 0.04), (-0.08, -0.12, 0.04), (-0.08, 0.1, 0.04)], [0.02] * 4, "#ffc62e", segments=5)
    return k.yaw(25)


def prop_hook():
    k = Kit("Prop_Hook")
    k.ring_mesh([(0.0, 0.17, 0.17), (0.11, 0.19, 0.19)], "#5b3a1e", segments=12)
    k.tube([(0.0, 0.0, 0.1), (0.3, 0.0, 0.08), (0.48, 0.0, 0.08), (0.56, 0.12, 0.08), (0.48, 0.22, 0.08), (0.38, 0.17, 0.08)], [0.035, 0.035, 0.035, 0.035, 0.03, 0.01], "#c9ccd2", segments=6)
    return k.yaw(30)


def prop_parrot():
    """Попугай капитана сидит на столе и смотрит на игрока."""
    k = Kit("Prop_Parrot")
    k.ball((0.0, 0.0, 0.3), 0.2, "#a82d24", scale=(0.85, 0.85, 1.2), segments=10, rings=5)
    k.ball((0.0, 0.03, 0.56), 0.13, "#a82d24", segments=10, rings=5)
    k.tube([(0.0, 0.14, 0.56), (0.0, 0.25, 0.49)], [0.06, 0.012], "#ffc62e", segments=5)
    for side in (-1, 1):
        k.ball((side * 0.17, -0.02, 0.28), 0.14, "#2f5f9c", scale=(0.35, 0.7, 1.25), segments=8, rings=4)
        k.ball((side * 0.06, 0.12, 0.6), 0.03, "#ffffff", segments=6, rings=3)
        k.box((side * 0.07, 0.04, 0.04), (0.08, 0.12, 0.08), "#ffc62e")
    k.tube([(0.0, -0.14, 0.2), (0.0, -0.46, 0.04)], [0.09, 0.02], "#ffc62e", flat=0.3)
    return k


def prop_trowel():
    k = Kit("Prop_Trowel")
    k.tube([(0.02, 0.0, 0.04), (0.42, 0.0, 0.04)], [0.14, 0.015], "#7c8088", segments=4, flat=0.2)
    k.tube([(-0.4, 0.0, 0.05), (-0.12, 0.0, 0.05)], [0.05, 0.04], "#2f7a4c")
    k.stick((-0.12, 0, 0.05), (0.03, 0, 0.04), 0.02, "#c9ccd2")
    return k.yaw(30)


def prop_honey():
    k = Kit("Prop_Honey")
    k.ring_mesh([(0.0, 0.14, 0.14), (0.05, 0.2, 0.2), (0.26, 0.2, 0.2), (0.33, 0.15, 0.15)], "#ffc62e", segments=12)
    k.ring_mesh([(0.33, 0.17, 0.17), (0.41, 0.17, 0.17), (0.43, 0.1, 0.1)], "#ece1c1", segments=12)
    k.box((0.0, 0.19, 0.17), (0.2, 0.03, 0.12), "#f3e8cf")
    return k


def prop_clock():
    k = Kit("Prop_Clock")
    k.ring_mesh([(0.0, 0.3, 0.3), (0.05, 0.3, 0.3), (0.06, 0.26, 0.26)], "#c9982f", segments=16)
    k.ring_mesh([(0.06, 0.255, 0.255), (0.065, 0.255, 0.255)], "#ffffff", segments=16)
    k.box((0.0, 0.08, 0.075), (0.025, 0.17, 0.012), "#16161c")
    k.box((0.06, 0.0, 0.075), (0.13, 0.025, 0.012), "#16161c")
    k.ball((0.0, 0.34, 0.03), 0.055, "#ffc62e", segments=8, rings=3)
    return k.yaw(25)


def prop_tray():
    k = Kit("Prop_Tray")
    k.ring_mesh([(0.0, 0.30, 0.30), (0.02, 0.335, 0.335), (0.035, 0.335, 0.335), (0.04, 0.29, 0.29)], "#c9ccd2", segments=16)
    k.box((-0.08, 0.04, 0.1), (0.13, 0.13, 0.13), "#f3e8cf")
    k.box((0.12, -0.08, 0.1), (0.13, 0.13, 0.13), "#f3e8cf")
    return k


def prop_rose():
    k = Kit("Prop_Rose")
    k.ball((0.42, 0.0, 0.11), 0.12, "#a82d24", segments=10, rings=5)
    k.ball((0.42, 0.0, 0.17), 0.075, "#7a2320", segments=8, rings=4)
    k.tube([(-0.5, 0.0, 0.03), (0.34, 0.0, 0.08)], [0.018, 0.022], "#2f7a4c")
    for x, side in ((-0.15, 1), (0.1, -1)):
        k.ball((x, side * 0.1, 0.05), 0.09, "#2f7a4c", scale=(1.2, 0.55, 0.2), segments=6, rings=3)
    return k.yaw(25)


def prop_yarn():
    k = Kit("Prop_Yarn")
    k.ball((0.0, 0.0, 0.2), 0.2, "#e87b8a", segments=12, rings=6)
    for a in (-30, 15, 60):
        k.lat_ring(0.205, a, "#a82d24", z=0.2)
    k.tube([(0.12, 0.14, 0.04), (0.34, 0.24, 0.015), (0.5, 0.06, 0.015)], [0.014] * 3, "#e87b8a", segments=5)
    return k


def prop_wand():
    k = Kit("Prop_Wand")
    k.stick((-0.42, 0, 0.03), (0.42, 0, 0.03), 0.028, "#16161c")
    k.stick((-0.42, 0, 0.03), (-0.28, 0, 0.03), 0.03, "#ffffff")
    k.stick((0.28, 0, 0.03), (0.42, 0, 0.03), 0.03, "#ffffff")
    for x, y, r in ((0.5, 0.1, 0.03), (0.42, 0.18, 0.02), (0.56, -0.02, 0.018)):
        k.ball((x, y, r), r, "#ffc62e", segments=6, rings=3)
    return k.yaw(30)


def prop_candle():
    k = Kit("Prop_Candle")
    k.ring_mesh([(0.0, 0.2, 0.2), (0.03, 0.2, 0.2), (0.05, 0.07, 0.07)], "#c9982f", segments=12)
    k.ring_mesh([(0.03, 0.06, 0.06), (0.42, 0.06, 0.06)], "#ffffff", segments=10)
    k.ball((0.0, 0.0, 0.48), 0.04, "#ffc62e", scale=(1, 1, 1.7), segments=6, rings=4)
    return k


def prop_puppet():
    k = Kit("Prop_Puppet")
    k.box((0.0, 0.0, 0.03), (0.68, 0.06, 0.05), "#8a5a2b")
    k.box((0.0, 0.0, 0.05), (0.06, 0.5, 0.05), "#8a5a2b")
    for x, y in ((-0.3, 0.0), (0.3, 0.0), (0.0, 0.22)):
        k.tube([(x, y, 0.06), (x * 0.7, y - 0.26, 0.02)], [0.006, 0.006], "#ffffff", segments=4)
    return k.yaw(25)


def prop_oil_lamp():
    k = Kit("Prop_OilLamp")
    k.ball((0.0, 0.0, 0.09), 0.2, "#c9982f", scale=(1.35, 0.85, 0.5), segments=10, rings=5)
    k.tube([(0.2, 0.0, 0.1), (0.44, 0.0, 0.13)], [0.09, 0.03], "#c9982f")
    k.tube([(-0.2, 0.0, 0.1), (-0.36, 0.0, 0.19), (-0.46, 0.0, 0.1)], [0.02] * 3, "#ffc62e", segments=5)
    k.ball((0.0, 0.0, 0.2), 0.05, "#ffc62e", segments=6, rings=3)
    return k.yaw(25)


def prop_lantern():
    k = Kit("Prop_Lantern")
    k.ring_mesh([(0.0, 0.17, 0.17), (0.05, 0.17, 0.17)], "#3a3a44", segments=8)
    k.ring_mesh([(0.05, 0.12, 0.12), (0.32, 0.12, 0.12)], "#ffc62e", segments=8)
    k.ring_mesh([(0.32, 0.17, 0.17), (0.38, 0.17, 0.17), (0.45, 0.04, 0.04)], "#3a3a44", segments=8)
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 8
        x, y = math.cos(a) * 0.125, math.sin(a) * 0.125
        k.stick((x, y, 0.05), (x, y, 0.32), 0.012, "#3a3a44", segments=4)
    return k


def prop_stamp():
    k = Kit("Prop_Stamp")
    k.ring_mesh([(0.0, 0.2, 0.14), (0.06, 0.2, 0.14)], "#a82d24", segments=10)
    k.ring_mesh([(0.06, 0.05, 0.05), (0.2, 0.04, 0.04)], "#5b3a1e", segments=8)
    k.ball((0.0, 0.0, 0.26), 0.09, "#5b3a1e", segments=8, rings=4)
    k.box((0.4, 0.05, 0.01), (0.34, 0.26, 0.02), "#f3e8cf")
    k.ball((0.42, 0.05, 0.021), 0.07, "#a82d24", scale=(1, 1, 0.1), segments=8, rings=3)
    return k


def prop_shell():
    k = Kit("Prop_Shell")
    k.ball((0.0, 0.0, 0.06), 0.28, "#e87b8a", scale=(1, 0.85, 0.2), segments=12, rings=5)
    k.ball((0.0, 0.0, 0.1), 0.095, "#ffffff", segments=10, rings=5)
    k.ball((0.0, -0.16, 0.2), 0.26, "#ece1c1", scale=(1, 0.3, 0.75), segments=12, rings=5)
    return k


def prop_feather():
    k = Kit("Prop_Feather")
    k.tube([(-0.42, 0.0, 0.02), (0.0, 0.0, 0.03), (0.5, 0.05, 0.03)], [0.02, 0.13, 0.01], "#3a3a44", flat=0.15)
    k.tube([(-0.5, 0.0, 0.02), (0.45, 0.04, 0.05)], [0.01, 0.004], "#c9ccd2", segments=4)
    return k.yaw(30)


def prop_crate():
    k = Kit("Prop_Crate")
    k.box((0.0, 0.0, 0.17), (0.4, 0.34, 0.34), "#8a5a2b")
    for z in (0.03, 0.31):
        k.box((0.0, 0.175, z), (0.42, 0.02, 0.05), "#5b3a1e")
    k.box((0.0, 0.18, 0.17), (0.5, 0.02, 0.05), "#5b3a1e", Matrix.Rotation(math.radians(40), 3, "Y"))
    return k.yaw(25)


def prop_pickaxe():
    k = Kit("Prop_Pickaxe")
    k.stick((-0.56, 0, 0.04), (0.38, 0, 0.05), 0.03, "#8a5a2b")
    k.tube([(0.38, -0.34, 0.05), (0.42, 0.0, 0.05), (0.38, 0.34, 0.05)], [0.012, 0.05, 0.012], "#7c8088", segments=5)
    return k.yaw(28)


def prop_pipe():
    k = Kit("Prop_Pipe")
    k.ring_mesh([(0.03, 0.08, 0.08, 0.0, 0.3), (0.17, 0.11, 0.11, 0.0, 0.3)], "#5b3a1e", segments=10)
    k.ring_mesh([(0.17, 0.075, 0.075, 0.0, 0.3), (0.175, 0.075, 0.075, 0.0, 0.3)], "#e0632a", segments=10)
    k.tube([(-0.4, 0.0, 0.03), (0.0, 0.0, 0.04), (0.26, 0.0, 0.07)], [0.018, 0.022, 0.04], "#16161c", segments=6)
    return k.yaw(30)


def prop_abacus():
    k = Kit("Prop_Abacus")
    k.box((0.0, 0.22, 0.03), (0.72, 0.05, 0.06), "#5b3a1e")
    k.box((0.0, -0.22, 0.03), (0.72, 0.05, 0.06), "#5b3a1e")
    for x in (-0.36, 0.36):
        k.box((x, 0.0, 0.03), (0.05, 0.48, 0.06), "#5b3a1e")
    for row, y in enumerate((-0.12, 0.0, 0.12)):
        k.stick((-0.34, y, 0.04), (0.34, y, 0.04), 0.008, "#c9ccd2", segments=4)
        for i in range(4):
            x = -0.26 + i * 0.09 + (0.18 if row == 1 else 0.0)
            k.ball((x, y, 0.04), 0.044, "#a82d24" if i % 2 else "#ffc62e", segments=6, rings=3)
    return k.yaw(25)


def prop_crystal():
    k = Kit("Prop_Crystal")
    k.ring_mesh([(0.0, 0.2, 0.2), (0.05, 0.17, 0.17), (0.1, 0.12, 0.12)], "#6a3a7c", segments=8)
    k.ball((0.0, 0.0, 0.3), 0.22, CRYSTAL, segments=14, rings=7)
    k.ball((0.08, 0.13, 0.38), 0.05, "#ffffff", segments=6, rings=3)
    return k


def prop_fan():
    k = Kit("Prop_Fan")
    for i in range(9):
        a = math.radians(30 + i * 15)
        rot = Matrix.Rotation(a - math.pi / 2, 3, "Z")
        p = rot @ Vector((0.0, 0.24, 0.0))
        k.box((p.x, p.y, 0.012 + (i % 2) * 0.006), (0.09, 0.45, 0.012), "#6a3a7c" if i % 2 else "#16161c", rot)
    k.ball((0.0, 0.0, 0.02), 0.04, "#ffc62e", scale=(1, 1, 0.5), segments=6, rings=3)
    return k.shift(0.0, -0.23)


def prop_gold_die():
    k = Kit("Prop_GoldDie")
    k.box((0.0, 0.0, 0.17), (0.34, 0.34, 0.34), "#ffc62e")
    for dx, dz in ((-0.09, 0.26), (0.09, 0.08), (0.09, 0.26), (-0.09, 0.08), (0.0, 0.17)):
        k.ball((dx, 0.175, dz), 0.03, "#16161c", scale=(1, 0.3, 1), segments=6, rings=3)
    k.ball((0.0, 0.0, 0.345), 0.035, "#16161c", scale=(1, 1, 0.3), segments=6, rings=3)
    return k.yaw(30)


PARTS = [
    head_flat_cap, head_scarf, head_scarf_dark, head_ushanka, head_postman,
    head_chef, head_straw, head_sun_hat, head_bee_hat, head_bowler, head_top_hat, head_magic_hat, head_fedora,
    head_bandana, head_bandana_dark, head_pirate, head_eye_patch, head_beret, head_bard, head_tricorn, head_captain,
    head_bicorn, head_sailor, head_customs, head_officer, head_visor, head_surfer, head_bucket, head_crown,
    head_rose_tiara, head_crab, head_cat, head_raven, head_ghost, head_loupe, head_turban, head_miner, head_jester,
    head_bun, head_worn_hat, head_merchant, head_watch, head_twins, head_midnight, head_fortune,
    chest_apron, chest_strap, chest_key_chain, chest_bow_tie, chest_bow_black, chest_tie, chest_medal, chest_pearls,
    chest_beads, chest_shells, chest_bell, chest_whistle, chest_sailor, chest_bandolier, chest_watch_chain,
    chest_jester, chest_cross_ribbon,
    prop_die, prop_rolling_pin, prop_mug, prop_letter, prop_big_key, prop_coins, prop_club, prop_cards, prop_dagger,
    prop_lute, prop_fish, prop_surfboard, prop_corn, prop_lifebuoy, prop_teacup, prop_claw, prop_mop, prop_ladle,
    prop_compass, prop_cutlass, prop_hook, prop_parrot, prop_trowel, prop_honey, prop_clock, prop_tray, prop_rose,
    prop_yarn, prop_wand, prop_candle, prop_puppet, prop_oil_lamp, prop_lantern, prop_stamp, prop_shell, prop_feather,
    prop_crate, prop_pickaxe, prop_pipe, prop_abacus, prop_crystal, prop_fan, prop_gold_die,
]

# Намеренно перед лицом: повязка на глаз, клюв; у платка узел под подбородком, лицо вырезано.
EYE_EXCEPTIONS = {"Head_EyePatch", "Head_Raven", "Head_Scarf", "Head_ScarfDark"}


def check_eyes(k):
    """Деталь головы не должна спускаться на глаза при взгляде камеры партии сверху (41°)."""
    if not k.name.startswith("Head_") or k.name in EYE_EXCEPTIONS:
        return
    tilt = math.radians(41)
    low = min((v.co.z * math.cos(tilt) - v.co.y * math.sin(tilt) for v in k.bm.verts if abs(v.co.x) < 0.32 and v.co.y > 0.0), default=1.0)
    if low < EYE_LINE:
        print("[zonk_oppkit] WARN eyes", k.name, round(low, 3))


# Предмет на столе: PropAnchor соперника в ZonkSetup.Scenes, в осях Unity (x, z). Модель лежит вокруг этой точки.
PROP_ANCHOR = (2.15, 1.25)
RIM_CLEAR = (1.80, 1.20)   # рамка лотка наружу до 1.75 × 1.15 (zonk_rims.py) плюс зазор
TABLE_DIAG = 3.95          # |x| + |z| плоско у всех столов (PLAY_DIAG в zonk_tables.py)
SETTLE_STEP = 5


def table_footprint(points, degrees):
    """Контур предмета на столе после поворота, в осях Unity."""
    turn = Matrix.Rotation(math.radians(degrees), 2)
    return [(PROP_ANCHOR[0] - q.x, PROP_ANCHOR[1] - q.y) for q in (turn @ p for p in points)]


def touches_rim(footprint):
    """Разделяющая ось между выпуклым контуром предмета и прямоугольником рамки."""
    hull = [footprint[i] for i in convex_hull_2d(footprint)]
    axes = [(1.0, 0.0), (0.0, 1.0)]
    axes += [(hull[i][1] - hull[i - 1][1], hull[i - 1][0] - hull[i][0]) for i in range(len(hull))]
    rx, rz = RIM_CLEAR
    for ax, az in axes:
        values = [x * ax + z * az for x, z in hull]
        reach = abs(ax) * rx + abs(az) * rz
        if min(values) > reach or max(values) < -reach:
            return False
    return True


def fits_table(footprint):
    return not touches_rim(footprint) and max(x + z for x, z in footprint) <= TABLE_DIAG


def settle_on_table(k):
    """Наименьший поворот, при котором предмет не заходит на лоток и лежит на плоской части любого стола."""
    if not k.name.startswith("Prop_"):
        return
    points = [Vector((v.co.x, v.co.y)) for v in k.bm.verts]
    for step in range(0, 180 // SETTLE_STEP + 1):
        for degrees in (step * SETTLE_STEP, -step * SETTLE_STEP):
            if fits_table(table_footprint(points, degrees)):
                if degrees != 0:
                    k.yaw(degrees)
                    print("[zonk_oppkit] settle", k.name, degrees)
                return
    print("[zonk_oppkit] WARN table", k.name, "does not fit next to the tray")


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)


def export(obj):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, obj.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             bake_space_transform=True, mesh_smooth_type="OFF", bake_anim=False)


def preview(folder):
    """Листы набора: чёрная голова с деталью головы или груди и предметы, вид камеры партии."""
    def sheet(makes, name, with_body):
        clear()
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE"
        scene.render.resolution_x, scene.render.resolution_y = 1800, 1100
        scene.view_settings.view_transform = "Standard"
        world = bpy.data.worlds.new("w")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.32, 0.17, 0.09, 1)
        scene.world = world
        black = bpy.data.materials.new("Black")
        black.use_nodes = True
        nodes = black.node_tree.nodes
        nodes.clear()
        emit = nodes.new("ShaderNodeEmission")
        emit.inputs[0].default_value = (0.008, 0.006, 0.012, 1)
        black.node_tree.links.new(emit.outputs[0], nodes.new("ShaderNodeOutputMaterial").inputs[0])
        columns = 9
        rows = (len(makes) + columns - 1) // columns
        for i, make in enumerate(makes):
            x, y = (i % columns - (columns - 1) / 2) * 2.0, -(i // columns) * 2.3
            if with_body:
                bpy.ops.mesh.primitive_uv_sphere_add(radius=HEAD_R, location=(x, y, HEAD_Z))
                bpy.context.object.data.materials.append(black)
            obj = make().build()
            obj.location = (x, y, 0.0 if with_body else -0.3)
        cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
        bpy.context.collection.objects.link(cam)
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = 19.0
        centre = Vector((0.0, -(rows - 1) * 1.15, -0.2))
        tilt = math.radians(41)
        cam.location = centre + Vector((0.0, math.cos(tilt), math.sin(tilt))) * 20.0
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera = cam
        scene.render.filepath = os.path.join(folder, name)
        bpy.ops.render.render(write_still=True)

    heads = [m for m in PARTS if m.__name__.startswith("head_")]
    chests = [m for m in PARTS if m.__name__.startswith("chest_")]
    props = [m for m in PARTS if m.__name__.startswith("prop_")]
    sheet(heads, "oppkit_heads.png", True)
    sheet(chests, "oppkit_chests.png", False)
    sheet(props, "oppkit_props.png", False)


def sheet_scene(width, height):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.32, 0.17, 0.09, 1)
    scene.world = world
    return scene


def flat_material(name, color):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    emit = nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = color
    material.node_tree.links.new(emit.outputs[0], nodes.new("ShaderNodeOutputMaterial").inputs[0])
    return material


def sheet_camera(scene, centre, scale, path):
    """Камера партии: 41° сверху, со стороны игрока (+Y)."""
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    bpy.context.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = scale
    tilt = math.radians(41)
    cam.location = centre + Vector((0.0, math.cos(tilt), math.sin(tilt))) * 30.0
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sheet_label(text, location):
    curve = bpy.data.curves.new("t", "FONT")
    curve.body = text
    curve.size = 0.3
    curve.materials.append(flat_material("Label", (1, 1, 1, 1)))
    obj = bpy.data.objects.new("t", curve)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (math.radians(49), 0, math.pi)


def read_kit_table():
    """Таблица соперников из генератора: для листов проверки, без второго списка. Грозные версии одинаковы с обычными."""
    path = os.path.join(ROOT, "Assets", "ZonkContent", "Scripts", "Editor", "Setup", "ZonkSetup.OpponentKit.cs")
    with open(path, encoding="utf-8") as source:
        rows = re.findall(r'\("Opp_(\w+)", "(\w*)", "(\w*)", "(\w*)"\)', source.read())
    return [row for row in rows if not row[0].endswith("_dread")]


def opponent_sheets(folder):
    """Листы проверки: каждый соперник целиком (голова, грудь, предмет), детали на своих местах относительно головы."""
    makers = {}
    for make in PARTS:
        probe = make()
        makers[probe.name] = make
        probe.bm.free()
    rows = read_kit_table()
    per_sheet, columns = 20, 5
    for page in range(0, len(rows), per_sheet):
        scene = sheet_scene(2000, 1500)
        black = flat_material("Black", (0.008, 0.006, 0.012, 1))
        for i, (name, head, chest, prop) in enumerate(rows[page:page + per_sheet]):
            base = Vector(((i % columns - (columns - 1) / 2) * 3.8, 0.0, -(i // columns) * 3.1))
            for radius, offset in ((HEAD_R * 0.85, (0, 0, HEAD_Z * 0.85)), (0.55, (0, 0.1, -1.15))):
                bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=base + Vector(offset))
                bpy.context.object.data.materials.append(black)
            if head:
                obj = makers[head]().build()
                obj.location = base
                obj.scale = (0.85, 0.85, 0.85)
            if chest:
                makers[chest]().build().location = base + Vector((0, 0.32, -0.975))
            if prop:
                makers[prop]().build().location = base + Vector((1.2, 0, -1.3))
            sheet_label(name, base + Vector((1.6, 0, 0.55)))
        sheet_camera(scene, Vector((0.0, 0.0, -5.0)), 19.5, os.path.join(folder, "oppkit_opponents_%d.png" % (page // per_sheet + 1)))


def table_sheet(folder):
    """Лист проверки: все предметы на своём месте у угла лотка, вид со стороны игрока."""
    scene = sheet_scene(2000, 1500)
    rim = flat_material("Rim", (0.05, 0.05, 0.06, 1))
    props = [m for m in PARTS if m.__name__.startswith("prop_")]
    columns = 8
    for i, make in enumerate(props):
        base = Vector(((i % columns - (columns - 1) / 2) * 2.3, (i // columns) * 2.5, 0.0))
        corners = [(0.95, 0.3), (1.75, 0.3), (1.75, 1.15), (0.95, 1.15)]
        mesh = bpy.data.meshes.new("rim")
        mesh.from_pydata([(PROP_ANCHOR[0] - x, PROP_ANCHOR[1] - z, -0.01) for x, z in corners], [], [[0, 1, 2, 3]])
        plate = bpy.data.objects.new("rim", mesh)
        bpy.context.collection.objects.link(plate)
        plate.data.materials.append(rim)
        plate.location = base
        make().build().location = base
    rows = (len(props) + columns - 1) // columns
    sheet_camera(scene, Vector((0.0, (rows - 1) * 1.25, 0.0)), 19.5, os.path.join(folder, "oppkit_table.png"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    save_palette()
    if "--preview" in argv:
        folder = argv[argv.index("--preview") + 1]
        preview(folder)
        table_sheet(folder)
        opponent_sheets(folder)
        return
    for make in PARTS:
        clear()
        export(make().build())
        print("[zonk_oppkit]", make.__name__)


main()
