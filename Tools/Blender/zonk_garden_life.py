# -*- coding: utf-8 -*-
"""
Живность сада: три бабочки и лягушка, плюс плоскость воды в прудике. Бабочки и лягушка — модели со скелетом, кости крутит код
(Zonk.Presentation.ButterflyFlutter, FrogHop), аниматора нет. Цвета — общая палитра OppKit, материал Zonk/Toon.

Координаты Blender: +Y смотрит вперёд (на игрока), +Z вверх, ноль модели — под брюхом.
Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_garden_life.py
    blender --background --factory-startup --python Tools/Blender/zonk_garden_life.py -- --preview <папка>
Результат: Art/Models/Environments/Garden_ButterflyA..C.fbx, Garden_Frog.fbx, Garden_Pond.fbx. Подключает генератор (ZonkSetup.Garden).
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_beach_life as life  # noqa: E402
import zonk_oppkit as ok  # noqa: E402

GREEN = "#2f7a4c"
GREEN_DARK = "#1f5235"
BELLY = "#ece1c1"
WHITE = "#ffffff"
BLACK = "#16161c"
YELLOW = "#ffc62e"
ORANGE = "#e0632a"
BLUE = "#2f5f9c"
PINK = "#e87b8a"
PURPLE = "#6a3a7c"


def wing_shape(kit, side, points, color, lift=0.0):
    """Плоская часть крыла: веер из центра, с небольшой толщиной лица и изнанки. side — с какой стороны."""
    verts = [kit.bm.verts.new((side * x, y, z + lift)) for x, y, z in points]
    faces = []
    for k in range(1, len(verts) - 1):
        faces.append(kit.bm.faces.new((verts[0], verts[k], verts[k + 1])))
    kit._finish(None, faces, color, True)


def butterfly(letter, main, trim, spot):
    """
    Бабочка, комикс: большие яркие крылья с каймой и пятном. Размах около 0.42 м: с камеры до неё 8–10 м, настоящая там не видна.
    Крылья — отдельные кости на сгибе у тела, тело само лёгкое.
    """
    body = ok.Kit("Body")
    body.tube([(0, -0.1, 0.0), (0, 0.0, 0.012), (0, 0.08, 0.018)], [0.012, 0.024, 0.018], BLACK, segments=5)
    body.ball((0, 0.095, 0.022), 0.024, BLACK, segments=6, rings=3)
    for s in (-1, 1):
        body.tube([(s * 0.01, 0.11, 0.035), (s * 0.04, 0.16, 0.065), (s * 0.055, 0.2, 0.06)], [0.004, 0.003, 0.005], BLACK, segments=3)
    parts = {"Body": body}
    for side_name, s in (("WingL", -1), ("WingR", 1)):
        wing = ok.Kit(side_name)
        # Верхняя большая лопасть, нижняя поменьше: кайма снизу, цвет поверх, пятно сверху.
        upper = [(0.01, 0.03, 0.0), (0.075, 0.13, 0.0), (0.16, 0.17, 0.0), (0.235, 0.13, 0.0), (0.225, 0.04, 0.0), (0.12, -0.01, 0.0)]
        lower = [(0.01, -0.01, 0.0), (0.12, -0.02, 0.0), (0.18, -0.08, 0.0), (0.135, -0.16, 0.0), (0.06, -0.14, 0.0), (0.015, -0.07, 0.0)]
        wing_shape(wing, s, upper, trim)
        wing_shape(wing, s, lower, trim)
        wing_shape(wing, s, [(x * 0.82 + 0.005, y * 0.82 + 0.006, 0) for x, y, _ in upper], main, 0.003)
        wing_shape(wing, s, [(x * 0.8 + 0.005, y * 0.8 - 0.002, 0) for x, y, _ in lower], main, 0.003)
        wing_shape(wing, s, [(0.13 + math.cos(a) * 0.035, 0.09 + math.sin(a) * 0.035, 0) for a in (k * 2 * math.pi / 7 for k in range(7))], spot, 0.006)
        wing_shape(wing, s, [(0.085 + math.cos(a) * 0.022, -0.075 + math.sin(a) * 0.022, 0) for a in (k * 2 * math.pi / 6 for k in range(6))], spot, 0.006)
        parts[side_name] = wing
    bones = [("Root", None, (0, 0, 0)), ("Body", "Root", (0, 0, 0.02)), ("WingL", "Body", (-0.012, 0.0, 0.012)), ("WingR", "Body", (0.012, 0.0, 0.012))]
    armature, mesh = life.assemble("Butterfly" + letter, bones, parts)
    return armature, mesh


def frog():
    """Лягушка: приземистое тело, большие глаза сверху, широкий рот, горло дышит, веки моргают, длинные задние лапы."""
    body = ok.Kit("Body")
    body.ring_mesh([(0.03, 0.1, 0.12, -0.03), (0.09, 0.17, 0.2, 0.0), (0.16, 0.16, 0.19, 0.03), (0.215, 0.1, 0.12, 0.07), (0.235, 0.04, 0.05, 0.09)], GREEN, segments=10)
    body.ball((0, 0.13, 0.13), 0.13, GREEN, scale=(1.05, 0.95, 0.7), segments=9, rings=5)
    body.tube([(-0.11, 0.225, 0.11), (0.0, 0.25, 0.1), (0.11, 0.225, 0.11)], [0.006, 0.008, 0.006], GREEN_DARK, segments=4)
    for x, y, z, r in ((-0.06, -0.08, 0.19, 0.022), (0.055, -0.03, 0.215, 0.018), (0.1, -0.1, 0.15, 0.016)):
        body.ball((x, y, z), r, GREEN_DARK, scale=(1, 1, 0.45), segments=5, rings=3)
    for s in (-1, 1):
        body.ball((s * 0.075, 0.13, 0.215), 0.052, GREEN, segments=7, rings=4)
        body.ball((s * 0.075, 0.155, 0.225), 0.044, WHITE, segments=7, rings=4)
        body.ball((s * 0.08, 0.19, 0.228), 0.022, BLACK, segments=6, rings=3)
    throat = ok.Kit("Throat")
    throat.ball((0, 0.12, 0.065), 0.085, BELLY, scale=(1.1, 1.0, 0.6), segments=8, rings=4)
    lids = ok.Kit("Lids")
    for s in (-1, 1):
        lids.ball((s * 0.075, 0.157, 0.24), 0.047, GREEN, scale=(1.05, 1.05, 0.55), segments=7, rings=3)
    parts = {"Body": body, "Throat": throat, "Lids": lids}
    bones = [("Root", None, (0, 0, 0)), ("Body", "Root", (0, 0, 0.05)), ("Throat", "Body", (0, 0.12, 0.06)), ("Lids", "Body", (0, 0.15, 0.26))]
    for side, s in (("L", -1), ("R", 1)):
        back = ok.Kit("LegB" + side)
        pivot = (s * 0.13, -0.09, 0.09)
        back.tube([pivot, (s * 0.25, -0.02, 0.13), (s * 0.27, -0.14, 0.02), (s * 0.3, 0.02, 0.012)], [0.05, 0.036, 0.026, 0.018], GREEN, segments=5)
        back.ball((s * 0.31, 0.06, 0.012), 0.045, GREEN_DARK, scale=(1.05, 1.3, 0.25), segments=6, rings=3)
        front = ok.Kit("LegF" + side)
        hand = (s * 0.14, 0.09, 0.08)
        front.tube([hand, (s * 0.2, 0.15, 0.04), (s * 0.19, 0.2, 0.012)], [0.03, 0.022, 0.016], GREEN, segments=5)
        front.ball((s * 0.19, 0.225, 0.012), 0.032, GREEN_DARK, scale=(1.1, 1.2, 0.25), segments=6, rings=3)
        parts["LegB" + side] = back
        parts["LegF" + side] = front
        bones.append(("LegB" + side, "Body", pivot))
        bones.append(("LegF" + side, "Body", hand))
    return life.assemble("Frog", bones, parts)


def export_pond():
    """Вода прудика: простой эллипс в нуле, ставит на место генератор. Эллипс симметричен, поэтому оси выгрузки не важны."""
    rx, ry = 1.75, 1.2
    mesh = bpy.data.meshes.new("Garden_Pond")
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((math.cos(k * 2 * math.pi / 26) * rx, math.sin(k * 2 * math.pi / 26) * ry, 0)) for k in range(26)])
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Garden_Pond", mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(ok.palette_material())
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(life.MODELS, "Garden_Pond.fbx"), use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="OFF", bake_anim=False)


def preview(folder, frog_rig):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1000, 700
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.3, 0.55, 0.2, 1)
    scene.world = world
    camera = bpy.data.cameras.new("c")
    camera.lens = 50
    obj = bpy.data.objects.new("c", camera)
    bpy.context.collection.objects.link(obj)
    scene.camera = obj

    def shot(name, position, target):
        obj.location = position
        obj.rotation_euler = (Vector(target) - Vector(position)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, name)
        bpy.ops.render.render(write_still=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    shot("garden_life.png", (0.7, 2.6, 1.35), (0.45, 0, 0.1))
    print("[zonk_garden_life] preview done")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rigs = [butterfly("A", ORANGE, BLACK, YELLOW), butterfly("B", BLUE, BLACK, WHITE), butterfly("C", PINK, PURPLE, YELLOW)]
    if "--preview" in argv:
        for k, (armature, _) in enumerate(rigs):
            armature.location = (-0.7 + k * 0.72, 0.9, 0.55)
        frog_rig = frog()
        frog_rig[0].location = (0.45, 0, 0)
        preview(argv[argv.index("--preview") + 1], frog_rig)
        return
    frog_rig = frog()
    for armature, mesh in rigs + [frog_rig]:
        life.export(armature, mesh, prefix="Garden_")
    export_pond()
    print("[zonk_garden_life] exported to", life.MODELS)


if __name__ == "__main__":
    main()
