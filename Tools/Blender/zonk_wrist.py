# -*- coding: utf-8 -*-
"""
Запястье руки игрока для «Зонка» (слот «Запястье», 15 вещей; первая — «ничего», без модели): часы и браслеты
в зоне запястья zonk_handkit (s 0…0.15, не выше WRIST_MAX_ADD над кожей) — их не задевает ни один рукав.

    Wrist_Watch      механические часы на кожаном ремешке
    Wrist_RedThread  красная нить на удачу, узелок
    Wrist_WoodBeads  деревянные бусы
    Wrist_DiceBeads  бусы из игральных костей
    Wrist_Studs      кожаный браслет с шипами
    Wrist_Chips      фишки казино на шнурке
    Wrist_Smart      смарт-часы с пульсом на экране
    Wrist_Anchor     морской канат с якорем
    Wrist_GoldWatch  золотые часы на браслете
    Wrist_Chain      толстая золотая цепь
    Wrist_Gems       золотой браслет с камнями
    Wrist_Compass    компас на кожаном ремешке (Сыч)
    Wrist_Horseshoe  железный браслет с подковой (Тихон)
    Wrist_Skull      тёмные бусы с черепом (Крот)

Стиль и координаты — zonk_handkit.py.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_wrist.py
    blender --background --factory-startup --python Tools/Blender/zonk_wrist.py -- --render <папка> [--only Wrist_Chain]

Результат: Assets/ZonkContent/Art/Models/Wrist/<Имя>.fbx и Art/Textures/Wrist/<Имя>.png (палитра 256).
"""

import glob
import math
import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_handkit as hk  # noqa: E402
from zonk_handkit import C, shade  # noqa: E402

MODELS_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Wrist")
TEXTURES_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Wrist")

W = 0.075            # центр запястья (корпуса часов не заходят на манжету рукава: край рукава от 0.16)
TOP = math.pi / 2    # тыльная сторона: циферблаты и подвески смотрят в камеру

GOLD = C(1.0, 0.76, 0.25)
GOLD_DARK = C(0.78, 0.52, 0.12)
SILVER = C(0.8, 0.82, 0.86)
STEEL = C(0.6, 0.62, 0.66)
LEATHER = C(0.42, 0.25, 0.13)
BLACK = C(0.1, 0.1, 0.11)
WHITE = C(0.96, 0.95, 0.9)
RED = C(0.82, 0.12, 0.12)

ARM = None


def T(deg):
    return math.radians(deg)


def strap(k, color, width=0.06, add=0.003, thick=0.02, s=W, stitch=None):
    hk.band(k, s - width / 2, s + width / 2, add, add + thick, color)
    if stitch:
        for e in (-1, 1):
            c = s + e * (width / 2 - 0.009)
            hk.band(k, c - 0.0025, c + 0.0025, add + thick - 0.002, add + thick + 0.003, stitch)


def dial(k, s, t, add, radius, face, marks, hands_color):
    """Циферблат: деления на 12, 3, 6, 9 и две стрелки (на 12 — к пальцам)."""
    p, n, along, across = hk.frame_at(k, s, t, add)
    hk.cyl_on(k, s, t, add, radius, 0.006, face, seg=16)
    top = p + n * 0.006
    for q in range(4):
        a = q * math.pi / 2
        d = across * math.cos(a) - along * math.sin(a)
        hk.slab(k, top + d * radius * 0.7, d, d.cross(n).normalized(), n, radius * 0.2, 0.011, 0.003, marks)
    hk.slab(k, top, -along, across, n, radius * 0.55, 0.011, 0.004, hands_color)
    hk.slab(k, top, across, along, n, radius * 0.75, 0.008, 0.005, hands_color)


def watch(k, case_color, face, marks, hands_color, radius=0.075, add=0.023):
    hk.cyl_on(k, W, TOP, add, radius, 0.03, case_color, seg=16, bevel=0.008)
    dial(k, W, TOP, add + 0.022, radius * 0.78, face, marks, hands_color)
    p, n, along, across = hk.frame_at(k, W, TOP, add + 0.014)
    hk.slab(k, p + across * (radius - 0.004), across, along, n, 0.022, 0.018, 0.012, case_color)   # головка


def charm_anchor(k, s, t, add, color, sc=1.6):
    p, n, along, across = hk.frame_at(k, s, t, add)
    hk.slab(k, p - along * 0.03 * sc, along, across, n, 0.06 * sc, 0.012 * sc, 0.008 * sc, color)    # веретено
    hk.slab(k, p - along * 0.024 * sc - across * 0.011 * sc, across, along, n, 0.022 * sc, 0.008 * sc, 0.008 * sc, color)
    for side in (-1, 1):                                                                             # лапы
        d = (across * side - along * 0.5).normalized()
        hk.slab(k, p + along * 0.026 * sc, d, d.cross(n).normalized(), n, 0.028 * sc, 0.009 * sc, 0.008 * sc, color)
    hk.cyl_on(k, s - 0.038 * sc, t, add, 0.009 * sc, 0.008 * sc, color, seg=8)                     # кольцо


def bead_ring(k, n, radius, color, add=0.003, skip=None):
    for q in range(n):
        t = hk.angle(0) + 2 * math.pi * q / n
        if skip and skip(t):
            continue
        hk.ball(k, ARM.point(W, t, add + radius), radius, color(q) if callable(color) else color, seg=8, rings=5)


# ---------------------------------------------------------------------------
# Вещи
# ---------------------------------------------------------------------------

def make_watch():
    k = hk.Kit("Wrist_Watch", ARM)
    strap(k, LEATHER, stitch=C(0.85, 0.7, 0.45))
    watch(k, STEEL, WHITE, BLACK, BLACK)
    return k


def make_red_thread():
    k = hk.Kit("Wrist_RedThread", ARM)
    for s in (W - 0.016, W + 0.008):
        hk.roll(k, s, 0.002, 0.009, RED, sides=6)
    t = T(25)
    knot = ARM.point(W - 0.004, t, 0.022)
    hk.ball(k, knot, 0.022, shade(RED, 0.85), seg=6, rings=4)
    n = ARM.normal(W, t)
    for d in (-1, 1):                                  # кончики — от руки и вдоль неё, не в руку
        hk.tube(k, [knot, knot + ARM.axis * d * 0.035 + n * 0.02], 0.007, RED, sides=5)
    k.roughness = 0.8
    return k


def make_wood_beads():
    k = hk.Kit("Wrist_WoodBeads", ARM)
    bead_ring(k, 17, 0.03, lambda q: C(0.55, 0.33, 0.16) if q % 2 else C(0.72, 0.48, 0.26))
    k.roughness = 0.7
    return k


def make_dice_beads():
    k = hk.Kit("Wrist_DiceBeads", ARM)
    n = 12
    pips_by = {1: [(0, 0)], 2: [(-1, -1), (1, 1)], 3: [(-1, -1), (0, 0), (1, 1)], 4: [(-1, -1), (1, 1), (-1, 1), (1, -1)],
               5: [(-1, -1), (1, 1), (-1, 1), (1, -1), (0, 0)], 6: [(-1, -1), (1, 1), (-1, 1), (1, -1), (-1, 0), (1, 0)]}
    for q in range(n):
        t = hk.angle(0) + 2 * math.pi * q / n
        if q % 2:
            hk.ball(k, ARM.point(W, t, 0.022), 0.019, BLACK, seg=6, rings=4)
            continue
        p, nrm, along, across = hk.frame_at(k, W, t, 0.003)
        size = 0.048
        hk.slab(k, p - along * size / 2, along, across, nrm, size, size, size, WHITE)
        face = (q // 2) % 6 + 1
        for x, y in pips_by[face]:
            c = p + nrm * size + across * x * 0.013 + along * y * 0.013
            hk.slab(k, c - along * 0.0045, along, across, nrm, 0.009, 0.009, 0.002, RED if face == 1 else BLACK)
    return k


def make_studs():
    k = hk.Kit("Wrist_Studs", ARM)
    strap(k, BLACK, width=0.066, thick=0.02)
    for q in range(10):
        hk.stud(k, W, hk.angle(0) + 2 * math.pi * q / 10, 0.023, 0.02, 0.045, SILVER)
    k.roughness = 0.45
    return k


def make_chips():
    k = hk.Kit("Wrist_Chips", ARM)
    hk.roll(k, W, 0.002, 0.005, BLACK, sides=5)
    colors = [RED, C(0.15, 0.35, 0.75), C(0.12, 0.55, 0.3), BLACK]
    for q in range(7):
        t = T(30 + q * 20)
        color = colors[q % len(colors)]
        hk.cyl_on(k, W, t, 0.012, 0.036, 0.013, color, seg=12)
        p, nrm, along, across = hk.frame_at(k, W, t, 0.012)
        for e in range(4):                                         # белые насечки по краю
            a = e * math.pi / 2 + 0.4
            d = across * math.cos(a) + along * math.sin(a)
            hk.slab(k, p + d * 0.027, d, d.cross(nrm).normalized(), nrm, 0.011, 0.011, 0.0135, WHITE)
        hk.cyl_on(k, W, t, 0.025, 0.016, 0.002, WHITE, seg=10)
    return k


def make_smart():
    k = hk.Kit("Wrist_Smart", ARM)
    body = C(0.14, 0.14, 0.16)
    strap(k, C(0.18, 0.2, 0.24), width=0.056, thick=0.018)
    p, n, along, across = hk.frame_at(k, W, TOP, 0.02)
    hk.slab(k, p - along * 0.056, along, across, n, 0.112, 0.092, 0.026, body)
    top = p + n * 0.026
    hk.slab(k, top - along * 0.046, along, across, n, 0.092, 0.074, 0.002, C(0.05, 0.08, 0.12))
    hk.slab(k, top - along * 0.03 + n * 0.002, along, across, n, 0.018, 0.02, 0.002, C(1.0, 0.25, 0.35))     # сердце
    for q in range(3):                                                                                         # пульс
        hk.slab(k, top + along * 0.006 + across * (q - 1) * 0.02 + n * 0.002, along, across, n, 0.026, 0.011, 0.002,
                C(0.3, 0.95, 0.85))
    hk.slab(k, top + across * 0.05 - along * 0.008 - n * 0.016, across, along, n, 0.01, 0.016, 0.01, body)  # кнопка
    k.roughness = 0.35
    return k


def make_anchor():
    k = hk.Kit("Wrist_Anchor", ARM)
    rope, dark = C(0.86, 0.74, 0.5), C(0.6, 0.48, 0.28)
    for s in (W - 0.016, W + 0.016):
        hk.roll(k, s, 0.002, 0.013, lambda r, j: dark if (j + r) % 3 == 0 else rope, sides=6)
    charm_anchor(k, W + 0.008, TOP, 0.03, GOLD)
    k.roughness = 0.8
    return k


def make_gold_watch():
    k = hk.Kit("Wrist_GoldWatch", ARM)
    hk.band(k, W - 0.03, W + 0.03, 0.003, 0.022, lambda r, j: GOLD if j % 2 else GOLD_DARK)
    watch(k, GOLD, C(0.1, 0.12, 0.16), GOLD, GOLD, radius=0.078, add=0.025)
    k.metallic, k.roughness = 0.6, 0.3
    return k


def make_chain():
    k = hk.Kit("Wrist_Chain", ARM)
    n = 12
    for q in range(n):
        t = hk.angle(0) + 2 * math.pi * (q + 0.5) / n
        nrm = ARM.normal(W, t)
        tangent = nrm.cross(ARM.axis).normalized()
        if q % 2:
            hk.link(k, ARM.point(W, t, 0.016), tangent, ARM.axis, 0.025, 0.01, GOLD)
        else:
            hk.link(k, ARM.point(W, t, 0.034), tangent, nrm, 0.025, 0.01, GOLD_DARK)
    k.metallic, k.roughness = 0.7, 0.3
    return k


def make_gems():
    k = hk.Kit("Wrist_Gems", ARM)
    hk.band(k, W - 0.026, W + 0.026, 0.003, 0.018, GOLD)
    gems = [RED, C(0.15, 0.45, 0.9), C(0.1, 0.7, 0.35), C(0.65, 0.2, 0.85)]
    for q in range(8):
        hk.gem(k, W, T(20 + q * 20), 0.018, 0.024, gems[q % len(gems)])
    k.metallic, k.roughness = 0.4, 0.25
    return k


def make_compass():
    k = hk.Kit("Wrist_Compass", ARM)
    brass, face, ink = C(0.8, 0.6, 0.25), C(0.95, 0.92, 0.8), C(0.2, 0.22, 0.28)
    strap(k, LEATHER, width=0.066, stitch=C(0.85, 0.7, 0.45))
    hk.cyl_on(k, W, TOP, 0.023, 0.078, 0.032, brass, seg=16, bevel=0.009)
    hk.cyl_on(k, W, TOP, 0.046, 0.06, 0.006, face, seg=16)
    p, n, along, across = hk.frame_at(k, W, TOP, 0.052)
    hk.slab(k, p, -along, across, n, 0.054, 0.016, 0.004, RED)          # север — к пальцам
    hk.slab(k, p, along, across, n, 0.046, 0.016, 0.004, ink)
    for q in range(4):
        a = q * math.pi / 2
        d = across * math.cos(a) - along * math.sin(a)
        hk.slab(k, p + d * 0.05, d, d.cross(n).normalized(), n, 0.011, 0.009, 0.003, ink)
    k.roughness = 0.4
    return k


def make_horseshoe():
    k = hk.Kit("Wrist_Horseshoe", ARM)
    iron, dark = C(0.5, 0.52, 0.56), C(0.32, 0.33, 0.36)
    hk.band(k, W - 0.032, W + 0.032, 0.003, 0.016, lambda r, j: iron if j % 4 else dark)
    p, n, along, across = hk.frame_at(k, W, TOP, 0.019)
    r = 0.045
    for q in range(9):                                   # подкова дугой, концами к локтю
        a = math.pi * (q / 8.0)
        c = p + across * math.cos(a) * r - along * math.sin(a) * r
        d = (across * -math.sin(a) - along * math.cos(a)).normalized()
        hk.slab(k, c - d * 0.009, d, d.cross(n).normalized(), n, 0.02, 0.02, 0.014, iron)
        if q in (2, 6):
            hk.slab(k, c + n * 0.014 - d * 0.003, d, d.cross(n).normalized(), n, 0.006, 0.006, 0.002, dark)  # гвоздь
    k.metallic, k.roughness = 0.5, 0.5
    return k


def make_skull():
    k = hk.Kit("Wrist_Skull", ARM)
    bead, bone = C(0.16, 0.13, 0.12), C(0.93, 0.89, 0.78)
    bead_ring(k, 16, 0.024, lambda q: bead if q % 2 else shade(bead, 1.4), skip=lambda t: abs(t - TOP) < 0.35)
    n_ = ARM.normal(W, TOP)
    centre = ARM.point(W, TOP, 0.04)
    hk.ball(k, centre, 0.045, bone, seg=10, rings=6, squash=(n_, 0.75))
    p, nn, along, across = hk.frame_at(k, W - 0.006, TOP, 0.072)
    for x in (-1, 1):
        hk.slab(k, p + across * x * 0.016 - along * 0.008, along, across, nn, 0.016, 0.016, 0.006, BLACK)   # глаза
    hk.slab(k, p + along * 0.014, along, across, nn, 0.01, 0.008, 0.006, BLACK)                             # нос
    hk.slab(k, ARM.point(W + 0.036, TOP, 0.03), along, across, nn, 0.018, 0.042, 0.022, bone)               # челюсть
    k.roughness = 0.6
    return k


WRIST = [make_watch, make_red_thread, make_wood_beads, make_dice_beads, make_studs, make_chips, make_smart, make_anchor,
         make_gold_watch, make_chain, make_gems, make_compass, make_horseshoe, make_skull]


def remove_orphans(names):
    for path in glob.glob(os.path.join(MODELS_DIR, "*.fbx")) + glob.glob(os.path.join(TEXTURES_DIR, "*.png")):
        if os.path.splitext(os.path.basename(path))[0] in names:
            continue
        os.remove(path)
        if os.path.exists(path + ".meta"):
            os.remove(path + ".meta")
        print("  удалён " + os.path.basename(path))


def main():
    global ARM
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

    armature, hand, ARM = hk.load_arm()
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, ok = [], True
    print("Запястье:")
    for make in WRIST:
        kit = make()
        if only and kit.name not in only:
            kit.bm.free()
            continue
        obj = kit.build(TEXTURES_DIR)
        tris = zm.triangle_count(obj.data)
        inside = hk.inside_count(obj, ARM)
        height = max(((v.co - ARM.palm).dot(ARM.axis) for v in obj.data.vertices), default=0)
        good = tris <= 3000 and inside <= 2 and height <= hk.WRIST_TO + 0.01
        ok &= good
        print("  %-16s треуг. %5d  цветов %2d  в руке %d  до s=%.3f  %s" % (obj.name, tris, len(kit.colors), inside, height,
                                                                         "OK" if good else "ПРОВЕРИТЬ"))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        hk.render(render_dir, objs, armature, hand, "wrist")
    for obj in objs:
        hk.export(obj, ARM, MODELS_DIR)
    if not only:
        remove_orphans({obj.name for obj in objs})
    if not ok:
        raise SystemExit("Есть вещи с ошибками")


if __name__ == "__main__":
    main()
