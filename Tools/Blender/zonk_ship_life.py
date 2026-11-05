# -*- coding: utf-8 -*-
"""
Живность корабля: крыса. Бегает по палубе и прячется. Модель со скелетом, кости крутит код (Zonk.Presentation.RatScurry), аниматора нет.
Цвета — общая палитра OppKit, материал Zonk/Toon, как у попугая и краба (zonk_beach_life.py).

Координаты Blender: +Y смотрит вперёд (на игрока), +Z вверх, ноль модели — на палубе под брюхом.
Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_ship_life.py
    blender --background --factory-startup --python Tools/Blender/zonk_ship_life.py -- --preview <папка>
Результат: Art/Models/Environments/Ship_Rat.fbx. Подключает генератор (ZonkSetup.Ship), меню Zonk/Art/Apply Ship Room.
"""

import math
import os
import sys

import bpy
from mathutils import Euler, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_beach_life as life  # noqa: E402
import zonk_oppkit as ok  # noqa: E402

FUR = "#7c8088"
FUR_DARK = "#3a3a44"
BELLY = "#c9ccd2"
PINK = "#e87b8a"
WHITE = "#ffffff"
BLACK = "#16161c"


def rat():
    """
    Крыса, комикс: толстая груша-тело, большая голова с острой мордой, круглые уши, передние зубы, длинный розовый хвост.
    Длина тела около 0.55 м: с камеры партии до неё 10 м, настоящая крыса там была бы точкой.
    """
    body = ok.Kit("Body")
    body.ring_mesh([(0.03, 0.05, 0.05, -0.2), (0.1, 0.14, 0.12, -0.16), (0.19, 0.15, 0.17, -0.09), (0.23, 0.12, 0.19, 0.0), (0.2, 0.085, 0.11, 0.11), (0.13, 0.07, 0.04, 0.14)], FUR, segments=10)
    body.ball((0.0, -0.05, 0.075), 0.11, BELLY, scale=(0.95, 1.5, 0.55), segments=8, rings=4)
    # Гребень шерсти на спине: тёмная полоса ловит силуэт.
    for k in range(5):
        y = -0.24 + k * 0.085
        body.ball((0.0, y, 0.255 - abs(y + 0.07) * 0.32), 0.03, FUR_DARK, scale=(0.5, 1.0, 1.0), segments=5, rings=3)

    head = ok.Kit("Head")
    head.ball((0.0, 0.19, 0.17), 0.1, FUR, scale=(0.95, 1.2, 0.9), segments=9, rings=5)
    head.tube([(0.0, 0.26, 0.155), (0.0, 0.35, 0.125)], [0.07, 0.022], FUR, segments=6)
    head.ball((0.0, 0.365, 0.125), 0.024, PINK, segments=6, rings=3)
    for s in (-1, 1):
        head.ball((s * 0.082, 0.16, 0.27), 0.062, FUR_DARK, scale=(0.85, 0.35, 1.0), segments=7, rings=4)
        head.ball((s * 0.082, 0.175, 0.27), 0.04, PINK, scale=(0.85, 0.3, 1.0), segments=6, rings=3)
        head.ball((s * 0.062, 0.26, 0.215), 0.034, WHITE, segments=7, rings=4)
        head.ball((s * 0.071, 0.286, 0.222), 0.017, BLACK, segments=6, rings=3)
        for dz in (-0.012, 0.016):
            head.stick((s * 0.04, 0.33, 0.135), (s * 0.17, 0.29, 0.135 + dz * 2.2), 0.003, BLACK, segments=3)
    # Передние зубы.
    head.box((0.0, 0.325, 0.075), (0.05, 0.016, 0.045), WHITE)

    tail_a = ok.Kit("TailA")
    tail_a.tube([(0.0, -0.22, 0.07), (0.0, -0.33, 0.045), (0.0, -0.44, 0.04)], [0.024, 0.02, 0.016], PINK, segments=5)
    tail_b = ok.Kit("TailB")
    tail_b.tube([(0.0, -0.44, 0.04), (0.02, -0.56, 0.05), (0.0, -0.68, 0.07), (-0.03, -0.77, 0.09)], [0.016, 0.012, 0.008, 0.002], PINK, segments=5)

    parts = {"Body": body, "Head": head, "TailA": tail_a, "TailB": tail_b}
    bones = [("Root", None, (0, 0, 0)), ("Body", "Root", (0, 0, 0.1)), ("Head", "Body", (0, 0.11, 0.16)), ("TailA", "Body", (0, -0.2, 0.07)), ("TailB", "TailA", (0, -0.44, 0.04))]
    for side, s in (("L", -1), ("R", 1)):
        for end, y, length in (("F", 0.08, 0.1), ("B", -0.13, 0.13)):
            name = "Leg" + end + side
            leg = ok.Kit(name)
            pivot = (s * 0.09, y, 0.1)
            leg.tube([pivot, (s * 0.11, y - 0.01, 0.045), (s * 0.115, y + 0.03, 0.012)], [0.033, 0.024, 0.016], FUR_DARK, segments=5)
            leg.ball((s * 0.115, y + 0.055, 0.014), 0.024, PINK, scale=(1.0, 1.5, 0.45), segments=6, rings=3)
            parts[name] = leg
            bones.append((name, "Body", pivot))
    return life.assemble("Rat", bones, parts)


def preview(folder, rig):
    armature, _ = rig
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1000, 700
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.32, 0.17, 0.09, 1)
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
    shot("rat_front.png", (0.75, 1.5, 0.75), (0, 0.05, 0.12))
    shot("rat_side.png", (1.7, -0.05, 0.55), (0, -0.1, 0.12))
    print(math.pi and "[zonk_ship_life] preview done")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig = rat()
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1], rig)
        return
    life.export(*rig, prefix="Ship_")
    print("[zonk_ship_life] exported to", life.MODELS)


if __name__ == "__main__":
    main()
