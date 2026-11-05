# -*- coding: utf-8 -*-
"""
Живность чердака: рыжий кот на сундуке Тихона. Спит, дышит, дёргает ухом и хвостом, иногда поднимает голову и оглядывается.
Модель со скелетом, кости крутит код (Zonk.Presentation.CatNap), аниматора нет. Цвета — палитра OppKit, материал Zonk/Toon.

Координаты Blender: +Y смотрит вперёд (на игрока), +Z вверх, ноль модели — под брюхом.
Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_attic_life.py
Результат: Art/Models/Environments/Attic_Cat.fbx. Подключает генератор (ZonkSetup.Attic), меню Zonk/Art/Apply Attic Room.
"""

import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_beach_life as life  # noqa: E402
import zonk_oppkit as ok  # noqa: E402

ORANGE = "#e0632a"
STRIPE = "#a82d24"
CREAM = "#f3e8cf"
PINK = "#e87b8a"
BLACK = "#16161c"
WHITE = "#ffffff"


def cat():
    """
    Кот, свернулся на боку: круглая спина, большая голова на лапах, хвост вокруг. Полоски пятнами, глаза закрыты дугами,
    открытые спрятаны под веками (кость Lids: масштаб открывает их). Длина около 0.85 м: с камеры партии до него 10 м.
    """
    body = ok.Kit("Body")
    body.ball((-0.08, -0.03, 0.21), 0.3, ORANGE, scale=(1.28, 0.92, 0.72), segments=12, rings=6)
    for x, z in ((-0.3, 0.32), (-0.14, 0.4), (0.02, 0.4), (0.16, 0.32)):
        body.ball((x, -0.02, z), 0.05, STRIPE, scale=(0.62, 2.4, 0.3), segments=6, rings=3)
    body.ball((0.2, 0.17, 0.08), 0.1, CREAM, scale=(1.25, 0.85, 0.65), segments=7, rings=4)
    body.ball((-0.2, 0.2, 0.08), 0.1, CREAM, scale=(1.1, 0.75, 0.6), segments=7, rings=4)

    head = ok.Kit("Head")
    hx, hy, hz = 0.3, 0.12, 0.2
    head.ball((hx, hy, hz), 0.165, ORANGE, scale=(1.1, 0.95, 0.92), segments=10, rings=6)
    head.ball((hx, hy + 0.115, hz - 0.045), 0.078, CREAM, scale=(1.22, 0.65, 0.75), segments=7, rings=4)
    head.ball((hx, hy + 0.17, hz - 0.018), 0.018, PINK, segments=5, rings=3)
    for x in (-0.05, 0.05):
        head.ball((hx + x, hy + 0.05, hz + 0.13), 0.022, STRIPE, scale=(0.5, 1.7, 0.35), segments=5, rings=3)
    parts = {"Body": body, "Head": head}
    for side, s in (("L", -1), ("R", 1)):
        ear = ok.Kit("Ear" + side)
        ex, ez = hx + s * 0.1, hz + 0.135
        ear.ring_mesh([(ez, 0.058, 0.03, hy, ex), (ez + 0.1, 0.008, 0.006, hy - 0.005, ex + s * 0.015)], ORANGE, segments=5)
        ear.ring_mesh([(ez + 0.005, 0.038, 0.012, hy + 0.024, ex), (ez + 0.075, 0.006, 0.004, hy + 0.024, ex + s * 0.012)], PINK, segments=4)
        parts["Ear" + side] = ear
    # Глаза: закрытые — дуги на морде, всегда видны. Открытые — на кости Lids, в покое сжаты в ноль.
    for s in (-1, 1):
        ex = hx + s * 0.075
        head.tube([(ex - 0.03, hy + 0.135, hz + 0.022), (ex, hy + 0.15, hz + 0.008), (ex + 0.03, hy + 0.135, hz + 0.022)], [0.006, 0.007, 0.006], BLACK, segments=4)
    eyes = ok.Kit("Lids")
    for s in (-1, 1):
        ex = hx + s * 0.075
        eyes.ball((ex, hy + 0.135, hz + 0.02), 0.042, WHITE, scale=(1, 0.6, 1), segments=7, rings=4)
        eyes.ball((ex, hy + 0.165, hz + 0.02), 0.024, BLACK, scale=(0.6, 0.5, 1), segments=6, rings=3)
    parts["Lids"] = eyes

    # Хвост: три кости, обнимает кота спереди, кончик тёмный.
    points = [(-0.42, -0.03, 0.09), (-0.5, 0.16, 0.07), (-0.34, 0.3, 0.06), (-0.1, 0.33, 0.06), (0.1, 0.32, 0.06)]
    for name, a, b, r0, r1, color in (("TailA", 0, 1, 0.056, 0.05, ORANGE), ("TailB", 1, 2, 0.05, 0.046, ORANGE), ("TailC", 2, 3, 0.046, 0.04, ORANGE), ("TailD", 3, 4, 0.04, 0.03, STRIPE)):
        kit = ok.Kit(name)
        kit.tube([points[a], points[b]], [r0, r1], color, segments=6)
        kit.ball(points[b], r1, color, segments=6, rings=3)
        parts[name] = kit
    bones = [("Root", None, (0, 0, 0)), ("Body", "Root", (0, 0, 0.12)), ("Head", "Body", (hx - 0.1, hy - 0.04, hz - 0.06)), ("Lids", "Head", (hx, hy + 0.14, hz + 0.02)),
             ("EarL", "Head", (hx - 0.1, hy, hz + 0.135)), ("EarR", "Head", (hx + 0.1, hy, hz + 0.135)),
             ("TailA", "Body", points[0]), ("TailB", "TailA", points[1]), ("TailC", "TailB", points[2]), ("TailD", "TailC", points[3])]
    return life.assemble("Cat", bones, parts)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    armature, mesh = cat()
    life.export(armature, mesh, prefix="Attic_")
    print("[zonk_attic_life] exported to", life.MODELS)


if __name__ == "__main__":
    main()
