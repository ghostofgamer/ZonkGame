# -*- coding: utf-8 -*-
"""
Живность подвала: крот в круглых очках, хозяин норы в полу клуба. Время от времени вылезает, оглядывается, шевелит носом и прячется.
Модель со скелетом, кости крутит код (Zonk.Presentation.MolePeek), аниматора нет. Цвета — палитра OppKit, материал Zonk/Toon.

Координаты Blender: +Y смотрит вперёд (на игрока), +Z вверх, ноль модели — на уровне пола, когда крот вылез.
Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_basement_life.py
Результат: Art/Models/Environments/Basement_Mole.fbx. Подключает генератор (ZonkSetup.Basement), меню Zonk/Art/Apply Basement Room.
"""

import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_beach_life as life  # noqa: E402
import zonk_oppkit as ok  # noqa: E402

FUR = "#3a3a44"
FUR_DARK = "#16161c"
PINK = "#e87b8a"
WHITE = "#ffffff"
GOLD = "#ffc62e"
BELLY = "#7c8088"


def mole():
    """Крот-комикс: толстое тело столбиком, большие розовые лапы-лопаты, длинный нос, маленькие круглые очки в золотой оправе."""
    body = ok.Kit("Body")
    body.ring_mesh([(-0.5, 0.2, 0.2), (-0.1, 0.25, 0.24), (0.14, 0.24, 0.23), (0.32, 0.19, 0.18), (0.42, 0.12, 0.12)], FUR, segments=12)
    body.ball((0, 0.13, 0.04), 0.15, BELLY, scale=(1.0, 0.72, 1.3), segments=9, rings=5)

    head = ok.Kit("Head")
    head.ball((0, 0.03, 0.51), 0.165, FUR, scale=(1.05, 1.05, 0.95), segments=11, rings=6)
    head.tube([(0, 0.15, 0.49), (0, 0.27, 0.46)], [0.085, 0.045], FUR, segments=7)
    head.ball((0, 0.29, 0.46), 0.05, PINK, segments=7, rings=4)
    # Очки: две линзы с оправой и перемычка, глаза-точки за стеклом.
    for s in (-1, 1):
        ex, ey, ez = s * 0.075, 0.155, 0.55
        head.ball((ex, ey, ez), 0.052, WHITE, scale=(1, 0.3, 1), segments=8, rings=4)
        head.ball((ex, ey + 0.017, ez), 0.021, FUR_DARK, scale=(1, 0.5, 1), segments=6, rings=3)
        ring = [(ex + 0.058 * math.cos(k * 0.785), ey + 0.012, ez + 0.058 * math.sin(k * 0.785)) for k in range(9)]
        for a, b in zip(ring, ring[1:]):
            head.stick(a, b, 0.008, GOLD, segments=4)
        head.stick((ex + s * 0.058, ey + 0.01, ez), (s * 0.15, 0.03, ez + 0.01), 0.006, GOLD, segments=3)
    head.stick((-0.017, 0.167, 0.555), (0.017, 0.167, 0.555), 0.007, GOLD, segments=3)

    parts = {"Body": body, "Head": head}
    bones = [("Root", None, (0, 0, -0.5)), ("Body", "Root", (0, 0, -0.1)), ("Head", "Body", (0, 0.0, 0.38))]
    for side, s in (("L", -1), ("R", 1)):
        paw = ok.Kit("Paw" + side)
        pivot = (s * 0.21, 0.05, 0.2)
        paw.tube([pivot, (s * 0.28, 0.17, 0.07)], [0.07, 0.06], FUR, segments=6)
        paw.ball((s * 0.3, 0.21, 0.03), 0.095, PINK, scale=(1.15, 0.7, 0.95), segments=8, rings=4)
        for k in (-1, 0, 1):
            paw.tube([(s * (0.3 + k * 0.055), 0.26, -0.03), (s * (0.31 + k * 0.07), 0.29, -0.09)], [0.02, 0.004], WHITE, segments=4)
        parts["Paw" + side] = paw
        bones.append(("Paw" + side, "Body", pivot))
    return life.assemble("Mole", bones, parts)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    armature, mesh = mole()
    life.export(armature, mesh, prefix="Basement_")
    print("[zonk_basement_life] exported to", life.MODELS)


if __name__ == "__main__":
    main()
