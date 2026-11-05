# -*- coding: utf-8 -*-
"""
Живность пляжа: попугай и краб. Двигаются, поэтому не запекаются: отдельные модели со скелетом, кости крутит код
(Zonk.Presentation.ParrotPerch, CrabScuttle), аниматора нет. Цвета — общая палитра OppKit, материал Zonk/Toon: контур и свет дают стиль.

Координаты Blender: +Y смотрит на игрока, +Z вверх, ноль модели — опора (лапы на жёрдочке, брюхо краба над песком).
Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_beach_life.py
    blender --background --factory-startup --python Tools/Blender/zonk_beach_life.py -- --preview <папка>
Результат: Art/Models/Environments/Beach_Parrot.fbx, Beach_Crab.fbx. Подключает генератор (ZonkSetup.Water), меню Zonk/Art/Apply Beach Room.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_oppkit as ok  # noqa: E402

MODELS = os.path.join(ok.ART, "Models", "Environments")

RED = "#a82d24"
RED_DARK = "#7a2320"
ORANGE = "#e0632a"
BLUE = "#2f5f9c"
BLUE_DARK = "#1d3a66"
YELLOW = "#ffc62e"
WHITE = "#ffffff"
CREAM = "#f3e8cf"
BLACK = "#16161c"
BROWN = "#2b1d14"
GREY = "#7c8088"


def blade(k, points, widths, wide, thick, color, segments=6):
    """Перо или плоский лист: сечение-эллипс через точки, ширина по вектору wide, толщина thick постоянная."""
    wide = Vector(wide).normalized()
    points = [Vector(p) for p in points]
    axis = (points[-1] - points[0]).normalized()
    flat = axis.cross(wide).normalized()
    loops = []
    for point, width in zip(points, widths):
        loops.append([k.bm.verts.new(point + wide * math.cos(i * 2 * math.pi / segments) * width + flat * math.sin(i * 2 * math.pi / segments) * thick) for i in range(segments)])
    faces = []
    for a, b in zip(loops, loops[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            faces.append(k.bm.faces.new((a[i], a[j], b[j], b[i])))
    faces.append(k.bm.faces.new(loops[0][::-1]))
    faces.append(k.bm.faces.new(loops[-1]))
    k._finish(None, faces, color, True)


def assemble(name, bones, parts):
    """Куски (кость -> Kit) в одну сетку: каждая вершина целиком на своей кости. Скелет без анимации."""
    armature_data = bpy.data.armatures.new(name)
    armature = bpy.data.objects.new(name, armature_data)
    bpy.context.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    for bone_name, parent, head in bones:
        bone = armature_data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = (head[0], head[1], head[2] + 0.04)
        if parent:
            bone.parent = armature_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")

    objects = []
    for bone_name, kit in parts.items():
        bmesh.ops.recalc_face_normals(kit.bm, faces=kit.bm.faces)
        mesh = bpy.data.meshes.new(bone_name)
        kit.bm.to_mesh(mesh)
        kit.bm.free()
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        obj = bpy.data.objects.new(bone_name, mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(ok.palette_material())
        group = obj.vertex_groups.new(name=bone_name)
        group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
        objects.append(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    merged = bpy.context.object
    merged.name = name + "Mesh"
    merged.parent = armature
    merged.modifiers.new("Armature", "ARMATURE").object = armature
    merged.data.calc_loop_triangles()
    print("[zonk_beach_life]", name, "triangles", len(merged.data.loop_triangles), "bones", len(bones))
    return armature, merged


def export(armature, mesh, prefix="Beach_"):
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODELS, prefix + armature.name + ".fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=False,
        use_tspace=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )


# ---------- Попугай ----------

def parrot():
    """
    Ара, комикс: крупная голова, белая щека вокруг глаза, большой крючковатый клюв, крыло в три яруса, длинный хвост.
    Высота с головой около 0.65 м: с камеры партии до него 8 м, мелкая птица там не читается.
    """
    feet = ok.Kit("Feet")
    for s in (-1, 1):
        feet.stick((s * 0.05, 0.0, 0.1), (s * 0.05, 0.015, 0.035), 0.016, GREY, segments=5)
        for dx in (-0.024, 0.024):
            feet.tube([(s * 0.05 + dx, -0.03, 0.03), (s * 0.05 + dx, 0.02, 0.05), (s * 0.05 + dx, 0.05, 0.015), (s * 0.05 + dx, 0.035, -0.025)], [0.012, 0.014, 0.012, 0.005], GREY, segments=5)

    body = ok.Kit("Body")
    body.ring_mesh([(0.06, 0.05, 0.05, -0.04), (0.12, 0.115, 0.12, -0.02), (0.22, 0.14, 0.145, 0.0), (0.32, 0.125, 0.125, 0.005), (0.41, 0.08, 0.08, 0.02)], RED, segments=10)

    head = ok.Kit("Head")
    head.ball((0.0, 0.03, 0.51), 0.125, RED, scale=(1, 1.02, 1.0), segments=10, rings=6)
    head.ball((0.0, -0.05, 0.6), 0.035, RED_DARK, scale=(0.5, 1.5, 1.0), segments=6, rings=3)
    for s in (-1, 1):
        head.ball((s * 0.075, 0.09, 0.525), 0.062, WHITE, scale=(0.72, 0.8, 0.95), segments=8, rings=4)
        head.ball((s * 0.103, 0.105, 0.54), 0.024, BLACK, segments=6, rings=4)
        head.ball((s * 0.116, 0.113, 0.55), 0.008, WHITE, segments=5, rings=3)
    head.tube([(0.0, 0.125, 0.555), (0.0, 0.205, 0.545), (0.0, 0.235, 0.485), (0.0, 0.205, 0.425)], [0.056, 0.05, 0.026, 0.006], CREAM, segments=7)
    head.tube([(0.0, 0.125, 0.465), (0.0, 0.165, 0.45), (0.0, 0.178, 0.425)], [0.036, 0.026, 0.006], BROWN, segments=6)

    wing_left = ok.Kit("WingL")
    wing_right = ok.Kit("WingR")
    for s, wing in ((-1, wing_left), (1, wing_right)):
        wide = (0, 1, 0)
        wing.ball((s * 0.14, 0.0, 0.335), 0.07, RED, scale=(0.42, 1.0, 1.05), segments=8, rings=4)
        for i, (y, length, width) in enumerate(((0.035, 0.27, 0.042), (-0.03, 0.3, 0.042))):
            blade(wing, [(s * 0.155, y, 0.33), (s * 0.165, y - 0.02, 0.33 - length * 0.55), (s * 0.155, y - 0.045, 0.33 - length)], [0.02, width, 0.008], wide, 0.011, YELLOW)
        for y, drop, tip in ((0.03, 0.38, -0.11), (-0.015, 0.43, -0.15), (-0.06, 0.47, -0.2)):
            blade(wing, [(s * 0.15, y, 0.24), (s * 0.16, y - 0.05, 0.24 - drop * 0.5), (s * 0.135, y + tip, 0.24 - drop)], [0.02, 0.038, 0.006], wide, 0.009, BLUE if tip > -0.18 else BLUE_DARK)

    tail = ok.Kit("Tail")
    wide = (1, 0, 0)
    blade(tail, [(0.0, -0.12, 0.13), (0.0, -0.3, -0.05), (0.0, -0.46, -0.24)], [0.03, 0.05, 0.006], wide, 0.011, RED)
    for s in (-1, 1):
        blade(tail, [(s * 0.025, -0.11, 0.13), (s * 0.05, -0.27, -0.03), (s * 0.075, -0.39, -0.18)], [0.024, 0.04, 0.005], wide, 0.01, BLUE)

    bones = [("Root", None, (0, 0, 0)), ("Feet", "Root", (0, 0, 0.01)), ("Body", "Root", (0, 0, 0.05)), ("Head", "Body", (0, 0.015, 0.41)),
             ("WingL", "Body", (-0.135, 0.0, 0.36)), ("WingR", "Body", (0.135, 0.0, 0.36)), ("Tail", "Body", (0, -0.1, 0.13))]
    return assemble("Parrot", bones, {"Feet": feet, "Body": body, "Head": head, "WingL": wing_left, "WingR": wing_right, "Tail": tail})


# ---------- Краб ----------

def crab():
    """Краб, комикс: широкий панцирь, глаза на стебельках, две большие клешни с подвижной нижней челюстью, по три ноги с боков."""
    body = ok.Kit("Body")
    body.ring_mesh([(0.07, 0.1, 0.08), (0.1, 0.175, 0.13), (0.14, 0.2, 0.15), (0.18, 0.17, 0.125), (0.205, 0.09, 0.065)], ORANGE, segments=12)
    for x, y, r in ((-0.07, -0.02, 0.022), (0.05, -0.05, 0.018), (0.085, 0.035, 0.016)):
        body.ball((x, y, 0.2 - abs(x) * 0.28), r, CREAM, scale=(1, 1, 0.5), segments=6, rings=3)
    body.tube([(-0.05, 0.145, 0.12), (0.0, 0.165, 0.105), (0.05, 0.145, 0.12)], [0.008, 0.01, 0.008], RED_DARK, segments=4)
    for s in (-1, 1):
        body.stick((s * 0.055, 0.1, 0.17), (s * 0.075, 0.12, 0.28), 0.013, RED, segments=5)
        body.ball((s * 0.075, 0.125, 0.3), 0.042, WHITE, segments=8, rings=5)
        body.ball((s * 0.08, 0.158, 0.305), 0.021, BLACK, segments=6, rings=4)

    parts = {"Body": body}
    bones = [("Root", None, (0, 0, 0)), ("Body", "Root", (0, 0, 0.1))]

    for s, side in ((-1, "L"), (1, "R")):
        claw = ok.Kit("Claw" + side)
        claw.tube([(s * 0.16, 0.07, 0.12), (s * 0.28, 0.13, 0.17), (s * 0.31, 0.22, 0.175)], [0.03, 0.03, 0.034], RED, segments=6)
        claw.ball((s * 0.315, 0.28, 0.18), 0.08, ORANGE, scale=(0.85, 1.1, 0.72), segments=8, rings=5)
        claw.tube([(s * 0.315, 0.33, 0.2), (s * 0.31, 0.39, 0.205), (s * 0.285, 0.455, 0.185)], [0.05, 0.036, 0.006], ORANGE, segments=6)
        pincer = ok.Kit("Pincer" + side)
        pincer.tube([(s * 0.315, 0.32, 0.15), (s * 0.31, 0.37, 0.14), (s * 0.29, 0.425, 0.155)], [0.036, 0.024, 0.005], CREAM, segments=6)
        parts["Claw" + side] = claw
        parts["Pincer" + side] = pincer
        bones.append(("Claw" + side, "Body", (s * 0.16, 0.07, 0.12)))
        bones.append(("Pincer" + side, "Claw" + side, (s * 0.315, 0.3, 0.155)))
        for i, y in enumerate((0.06, -0.01, -0.08)):
            leg = ok.Kit("Leg" + side + str(i))
            pivot = (s * 0.15, y, 0.11)
            leg.tube([pivot, (s * 0.31, y * 1.5, 0.165), (s * 0.43, y * 1.9, 0.0)], [0.022, 0.021, 0.006], RED, segments=5)
            parts["Leg" + side + str(i)] = leg
            bones.append(("Leg" + side + str(i), "Body", pivot))
    return assemble("Crab", bones, parts)


# ---------- Проверка ----------

def pose(armature, angles):
    for name, (x, y, z) in angles.items():
        bone = armature.pose.bones[name]
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = Euler((math.radians(x), math.radians(y), math.radians(z)))


def preview(folder, parrot_set, crab_set):
    """Лист: попугай и краб спереди и сверху-спереди, как их видят камеры игры; в покое и в движении."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1000, 700
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.62, 0.38, 1)
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

    parrot_arm, parrot_mesh = parrot_set
    crab_arm, crab_mesh = crab_set
    bpy.ops.object.mode_set(mode="OBJECT")
    crab_arm.location = (3, 0, 0)
    for o in (parrot_arm, crab_arm):
        o.select_set(False)

    shot("parrot_front.png", (0.8, 2.0, 0.85), (0, 0, 0.26))
    shot("crab_front.png", (3.3, 1.9, 1.5), (3, 0, 0.12))
    pose(parrot_arm, {"WingL": (0, 65, 0), "WingR": (0, -65, 0), "Head": (0, 20, 35)})
    pose(crab_arm, {"ClawL": (0, 25, 0), "ClawR": (0, -25, 0), "PincerL": (-22, 0, 0), "LegL0": (0, 25, 0), "LegR1": (0, -25, 0), "LegL2": (0, 25, 0)})
    bpy.context.view_layer.update()
    shot("parrot_flap.png", (0.8, 2.0, 0.85), (0, 0, 0.26))
    shot("crab_run.png", (3.3, 1.9, 1.5), (3, 0, 0.12))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parrot_set = parrot()
    crab_set = crab()
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1], parrot_set, crab_set)
        return
    export(*parrot_set)
    export(*crab_set)
    print("[zonk_beach_life] exported to", MODELS)


if __name__ == "__main__":
    main()
