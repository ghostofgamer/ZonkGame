# -*- coding: utf-8 -*-
"""
Превью облика руки целиком: тон кожи + кисть + рукав + запястье (как рука собирается в игре из слотов).
Строит вещи теми же функциями, что zonk_sleeves.py, zonk_wrist.py, zonk_hands.py (без выгрузки моделей) и рендерит
наборы по темам (LOOKS) — общий вид и крупно, ракурсом камеры партии (a) и магазина (b).

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_looks_preview.py -- --render <папка>
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_handkit as hk  # noqa: E402
import zonk_sleeves as zs  # noqa: E402
import zonk_wrist as zw  # noqa: E402
import zonk_hands as zh  # noqa: E402

TONES = [(0.98, 0.84, 0.74), (0.93, 0.72, 0.58), (0.84, 0.62, 0.44), (0.7, 0.48, 0.32), (0.5, 0.32, 0.2), (0.33, 0.2, 0.13)]

# (набор, кисть или None, рукав, запястье или None, тон 1..6)
LOOKS = [
    ("Пират", "make_hook", "make_pirate", "make_compass", 2),
    ("Рокер", "make_fingerless", "make_biker", "make_studs", 1),
    ("Мафия", None, "make_mafia", "make_gold_watch", 3),
    ("Маг", None, "make_mage", "make_gems", 4),
    ("Шулер", None, "make_ace", "make_dice_beads", 2),
    ("Моряк", None, "make_sailor", "make_anchor", 5),
    ("Ковбой", "make_leather", "make_cowboy", "make_horseshoe", 3),
    ("Гадалка", None, "make_fortune", "make_chips", 6),
    ("Самурай", None, "make_kimono", "make_red_thread", 4),
    ("Крупье", "make_butler", "make_croupier", "make_watch", 1),
    ("Робот", "make_robot", "make_biker", "make_smart", 2),
    ("Зомби", "make_zombie", "make_robe", "make_skull", 2),
    ("Демон", "make_demon", "make_mage", "make_chain", 2),
    ("Краб", "make_claw", "make_sailor", "make_anchor", 3),
    ("Садовница", "make_gardener", "make_shirt", "make_wood_beads", 1),
]


def tone_material(i):
    name = "Tone_%d" % i
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = TONES[i - 1] + (1.0,)
        mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.7
    return mat


def detach(obj):
    obj.parent = None
    for mod in list(obj.modifiers):
        obj.modifiers.remove(mod)
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[argv.index("--render") + 1]
    os.makedirs(out_dir, exist_ok=True)
    tmp = os.path.join(out_dir, "_textures")
    os.makedirs(tmp, exist_ok=True)

    armature, hand, arm = hk.load_arm()
    zs.ARM = zw.ARM = zh.ARM = arm
    zh.ARMATURE, zh.HAND = armature, hand
    scene = bpy.context.scene
    cols, spacing = 5, 1.35
    groups = []
    for q, (name, hand_fn, sleeve_fn, wrist_fn, tone) in enumerate(LOOKS):
        offset = Vector(((q % cols - (cols - 1) / 2) * spacing, -(q // cols) * spacing * 1.9, 0))
        parts = []
        hides = False
        if hand_fn:
            kit = getattr(zh, hand_fn)()
            hides = kit.name in zh.HIDES_HAND
            obj = detach(kit.build(tmp))
            obj.data = obj.data.copy()
            obj.data.materials[0] = tone_material(tone)
            parts.append(obj)
        if not hides:
            h = detach(hand.copy())
            h.data = hand.data.copy()
            h.data.materials[0] = tone_material(tone)
            scene.collection.objects.link(h)
            parts.append(h)
        parts.append(getattr(zs, sleeve_fn)().build(tmp))
        if wrist_fn:
            parts.append(getattr(zw, wrist_fn)().build(tmp))
        for obj in parts:
            obj.location = offset
        groups.append((name, offset))
        print("  набор %-10s деталей %d" % (name, len(parts)))

    hand.hide_render = True
    armature.hide_render = True
    hk.render(out_dir, [], armature, hand, "looks")
    hand.hide_render = True
    cam = scene.camera
    scene.render.resolution_x, scene.render.resolution_y = 800, 640
    for q, (name, offset) in enumerate(groups):
        target = offset + Vector((0, 0.3, 0.0))
        for view, off in (("a", Vector((0.8, 2.0, 1.8))), ("b", Vector((-1.7, 0.2, 0.9)))):
            cam.location = target + off
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(out_dir, "looks_%02d_%s.png" % (q + 1, view))
            bpy.ops.render.render(write_still=True)
    print("Превью наборов: " + out_dir)


if __name__ == "__main__":
    main()
