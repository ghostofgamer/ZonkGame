# -*- coding: utf-8 -*-
"""
Рукава руки игрока для «Зонка» (слот «Рукав», 15 штук): одежда на предплечье, закрывает срез руки (zonk_arms.py).
Часы и браслеты — отдельный слот «Запястье» (zonk_wrist.py), поэтому край рукава не ближе SLEEVE_MIN_EDGE к запястью.

    Sleeve_Knit      свитер с полосками и резинкой (по умолчанию)
    Sleeve_Shirt     белая рубашка: манжета с пуговицами, шов
    Sleeve_Cowboy    ковбойская клетка, закатанный рукав с кнопками
    Sleeve_Denim     джинсовая куртка: строчка, манжета с медной пуговицей
    Sleeve_Croupier  рубашка крупье с чёрной нарукавной резинкой и запонкой
    Sleeve_Sailor    тельняшка, закатанная до локтя
    Sleeve_Kimono    широкий свисающий рукав кимоно, красная подкладка
    Sleeve_Mage      мантия мага: раструб, золотая кайма с рунами, звёзды
    Sleeve_Biker     кожаная куртка: молния, шипы на манжете
    Sleeve_Mafia     костюм в полоску, белая манжета, золотая запонка
    Sleeve_Ace       шулер: туз в рукаве, красная резинка
    Sleeve_Pirate    пиратская рубаха: кружевная оборка, шнуровка
    Sleeve_Fortune   шаль гадалки: монеты и бахрома
    Sleeve_Robe      бабушкин стёганый халат в цветочек, пушистая манжета
    Sleeve_Tavern    закатанный рукав трактирщика и полотенце на руке

Стиль и координаты — zonk_handkit.py (гранёные формы, плоские цвета палитры, детали объёмом).

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_sleeves.py
    blender --background --factory-startup --python Tools/Blender/zonk_sleeves.py -- --render <папка> [--only Sleeve_Knit,Sleeve_Ace]

Результат: Assets/ZonkContent/Art/Models/Sleeves/<Имя>.fbx и Art/Textures/Sleeves/<Имя>.png (палитра 256).
Без --only файлы рукавов, которых больше нет в списке, удаляются (у пересобранных .meta остаётся — GUID тот же).
"""

import glob
import math
import os
import sys

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_handkit as hk  # noqa: E402
from zonk_handkit import C, shade, GAP  # noqa: E402

MODELS_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Sleeves")
TEXTURES_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Sleeves")

EDGE = 0.17          # край обычного рукава
BACK_FLARE = 0.03    # к локтю рукав чуть шире

GOLD = C(1.0, 0.76, 0.25)
SILVER = C(0.78, 0.8, 0.84)
WHITE = C(0.95, 0.95, 0.92)
BLACK = C(0.1, 0.1, 0.11)

ARM = None


def surf(s, edge):
    """Зазор ткани рукава (body с краем edge) на расстоянии s: к локтю рукав шире."""
    q = min(max((s - edge) / (ARM.back - edge), 0.0), 1.0)
    return GAP + BACK_FLARE * q * q


def body(k, edge, color, grow=1.06, drop=0.0, rows=8, wave=None, edge_color=None, inside=hk.INSIDE, thin=None):
    """Рукав от края edge до донца у локтя; к локтю чуть шире. thin — широкий рукав тонкой тканью с подкладкой."""
    rings = []
    for r in range(rows + 1):
        q = r / rows
        s = edge + (ARM.back - edge) * q
        rings.append(dict(s=s, add=surf(s, edge), grow=grow, drop=drop, wave=wave))
    hk.sleeve(k, rings, color, edge_color=edge_color, inside=inside, thin=thin)


def T(deg):
    return math.radians(deg)


# ---------------------------------------------------------------------------
# Рукава
# ---------------------------------------------------------------------------

def make_knit():
    k = hk.Kit("Sleeve_Knit", ARM)
    green, dark, cream = C(0.2, 0.42, 0.33), C(0.13, 0.29, 0.22), C(0.93, 0.88, 0.74)
    body(k, EDGE + 0.09, lambda r, j: cream if r in (1, 3) else (green if j % 2 else shade(green, 0.92)), rows=12)
    ribs = lambda t, r: 0.008 * max(0.0, math.cos(t * 16)) * (1 if r else 0)
    hk.band(k, EDGE - 0.005, EDGE + 0.1, GAP * 0.4, GAP + 0.012,
            lambda r, j: dark if j % 2 else shade(dark, 1.25), grow=1.03, wave=ribs, rows=1)
    k.roughness = 0.9
    return k


def make_shirt():
    k = hk.Kit("Sleeve_Shirt", ARM)
    seam, pearl, stitch = C(0.8, 0.82, 0.85), C(0.9, 0.88, 0.8), C(0.72, 0.76, 0.82)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: WHITE if j % 4 else shade(WHITE, 0.96), grow=1.07)
    hk.strip(k, edge + 0.03, ARM.back - 0.03, T(200), T(208), surf(0.5, edge) - 0.004, 0.006, seam, cols=1, grow=1.07)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.02, WHITE, rows=1)
    hk.band(k, EDGE + 0.004, EDGE + 0.01, GAP + 0.016, GAP + 0.023, stitch, rows=1)
    for s in (EDGE + 0.03, EDGE + 0.07):
        hk.cyl_on(k, s, T(160), GAP + 0.02, 0.02, 0.01, pearl, seg=8, bevel=0.003)
    k.roughness = 0.7
    return k


def make_cowboy():
    k = hk.Kit("Sleeve_Cowboy", ARM)
    red, dark, cream = C(0.72, 0.13, 0.11), C(0.38, 0.06, 0.06), C(0.92, 0.85, 0.7)

    def plaid(r, j):
        a, b = j % 4 == 0, r % 2 == 0
        if j % 8 == 2:
            return cream
        return shade(dark, 0.8) if a and b else dark if a or b else red

    edge = EDGE + 0.1
    body(k, edge, plaid, rows=14, grow=1.07)
    hk.roll(k, edge, GAP + 0.006, 0.042, lambda r, j: plaid(j, r), sides=8)
    for t in (T(65), T(115)):
        hk.cyl_on(k, edge, t, GAP + 0.09, 0.016, 0.008, C(0.95, 0.93, 0.88), seg=8, bevel=0.003)
    k.roughness = 0.8
    return k


def make_denim():
    k = hk.Kit("Sleeve_Denim", ARM)
    denim, light, stitch, copper = C(0.25, 0.38, 0.62), C(0.36, 0.5, 0.74), C(0.92, 0.62, 0.22), C(0.78, 0.46, 0.22)
    edge = EDGE + 0.06
    body(k, edge, lambda r, j: light if j in (14, 15, 16) else denim, grow=1.1)
    for t0 in (T(112), T(126)):
        hk.strip(k, edge + 0.03, ARM.back - 0.04, t0, t0 + T(3), surf(0.5, edge) - 0.004, 0.006, stitch, cols=1, grow=1.1)
    hk.band(k, EDGE - 0.01, EDGE + 0.08, GAP * 0.5, GAP + 0.024, shade(denim, 0.85), rows=1)
    hk.band(k, EDGE + 0.006, EDGE + 0.012, GAP + 0.02, GAP + 0.028, stitch, rows=1)
    hk.band(k, EDGE + 0.058, EDGE + 0.064, GAP + 0.02, GAP + 0.028, stitch, rows=1)
    hk.cyl_on(k, EDGE + 0.035, T(165), GAP + 0.024, 0.022, 0.012, copper, seg=8, bevel=0.004)
    k.roughness = 0.85
    return k


def make_croupier():
    k = hk.Kit("Sleeve_Croupier", ARM)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: WHITE if j % 4 else shade(WHITE, 0.96), grow=1.12)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.02, WHITE, rows=1)
    hk.box_on(k, EDGE + 0.045, T(165), GAP + 0.02, 0.04, 0.034, 0.014, BLACK, top=SILVER)
    garter = 0.42
    lift = surf(garter, edge)
    hk.band(k, garter - 0.035, garter + 0.035, lift - 0.004, lift + 0.016, BLACK, grow=1.12, rows=1,
            wave=lambda t, r: 0.004 * math.sin(t * 6))
    hk.box_on(k, garter, T(95), lift + 0.016, 0.06, 0.04, 0.012, SILVER, grow=1.12)
    k.roughness = 0.6
    return k


def make_sailor():
    k = hk.Kit("Sleeve_Sailor", ARM)
    navy = C(0.12, 0.2, 0.45)
    edge = 0.27
    body(k, edge + 0.04, lambda r, j: WHITE if r % 2 else navy, rows=18, grow=1.05)
    hk.roll(k, edge + 0.04, GAP + 0.006, 0.045, lambda r, j: navy if r % 2 else WHITE, sides=8)
    k.roughness = 0.85
    return k


def make_kimono():
    k = hk.Kit("Sleeve_Kimono", ARM)
    indigo, wave_c, red = C(0.16, 0.2, 0.42), C(0.3, 0.38, 0.62), C(0.72, 0.12, 0.12)
    pattern = lambda r, j: wave_c if (j + 2 * r) % 6 == 0 or (j - 2 * r) % 6 == 0 else indigo
    body(k, EDGE, pattern, grow=1.5, drop=0.14, rows=8, edge_color=WHITE, inside=red, thin=0.014)
    hk.band(k, EDGE + 0.002, EDGE + 0.04, GAP + 0.002, GAP + 0.012, WHITE, grow=1.5, drop=0.14, rows=1)
    k.roughness = 0.75
    return k


def make_mage():
    k = hk.Kit("Sleeve_Mage", ARM)
    purple, star, lining = C(0.32, 0.16, 0.48), C(1.0, 0.86, 0.35), C(0.12, 0.1, 0.32)
    edge = EDGE + 0.04
    body(k, edge, lambda r, j: purple if (j + r) % 7 else shade(purple, 1.2), grow=1.3, drop=0.06, rows=8,
         inside=lining, thin=0.014)
    hk.flare(k, edge + 0.03, edge - 0.09, GAP + 0.05, GAP + 0.12, lambda r, j: GOLD if r == 2 else shade(purple, 0.8),
             wave=lambda t: 0.012 * math.sin(t * 5), rows=3, thick=0.014, grow=1.25, drop=0.05)
    for q in range(9):                                          # руны на кайме
        hk.box_on(k, edge - 0.06, T(42 + q * 12), GAP + 0.098, 0.02, 0.028, 0.008, star, grow=1.25, drop=0.05)
    for s, t in ((0.32, 70), (0.4, 100), (0.48, 80), (0.56, 115), (0.36, 125), (0.62, 55), (0.7, 95), (0.52, 140)):
        hk.box_on(k, s, T(t), surf(s, edge), 0.026, 0.026, 0.008, star, tilt=math.pi / 4, grow=1.3, drop=0.06)
    k.roughness = 0.7
    return k


def make_biker():
    k = hk.Kit("Sleeve_Biker", ARM)
    leather, shine = C(0.11, 0.11, 0.12), C(0.24, 0.24, 0.27)
    edge = EDGE + 0.07
    body(k, edge, lambda r, j: shine if j in (18, 19) else leather, grow=1.12)
    hk.band(k, EDGE - 0.01, EDGE + 0.08, GAP * 0.5, GAP + 0.026, shade(leather, 1.3), rows=1)
    for q in range(10):
        hk.stud(k, EDGE + 0.035, T(20 + q * 15), GAP + 0.026, 0.017, 0.032, SILVER)
    for q in range(14):                                         # молния вдоль руки
        s = edge + 0.03 + q * 0.035
        hk.box_on(k, s, T(200), surf(s, edge), 0.02, 0.022, 0.008, SILVER if q % 2 else shade(SILVER, 0.7), grow=1.12)
    hk.box_on(k, edge + 0.02, T(200), surf(edge, edge) + 0.006, 0.032, 0.05, 0.014, SILVER, grow=1.12)
    k.roughness = 0.45
    return k


def make_mafia():
    k = hk.Kit("Sleeve_Mafia", ARM)
    wool, pin = C(0.17, 0.17, 0.2), C(0.42, 0.42, 0.46)
    edge = EDGE + 0.05
    body(k, edge, lambda r, j: pin if j % 4 == 0 else wool, grow=1.12)
    hk.band(k, EDGE - 0.01, edge + 0.02, GAP * 0.5, GAP + 0.018, WHITE, rows=1)
    hk.box_on(k, EDGE + 0.015, T(160), GAP + 0.018, 0.042, 0.032, 0.014, GOLD, top=shade(GOLD, 1.15))
    for q in range(3):
        hk.cyl_on(k, edge + 0.035 + q * 0.04, T(200), surf(edge + 0.04, edge), 0.017, 0.008, BLACK, seg=8, grow=1.12)
    k.roughness = 0.6
    return k


def make_ace():
    k = hk.Kit("Sleeve_Ace", ARM)
    shirt, stripe, red = C(0.95, 0.93, 0.86), C(0.62, 0.72, 0.86), C(0.8, 0.12, 0.12)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: stripe if j % 4 == 0 else shirt, grow=1.08)
    hk.band(k, EDGE - 0.01, EDGE + 0.09, GAP * 0.5, GAP + 0.018, shirt, rows=1)
    garter = 0.42
    lift = surf(garter, edge)
    hk.band(k, garter - 0.03, garter + 0.03, lift - 0.004, lift + 0.016, red, grow=1.08, rows=1,
            wave=lambda t, r: 0.004 * math.sin(t * 6))
    # Туз выезжает из манжеты сбоку (у большого пальца) и отходит от руки: часы на тыльной стороне и браслеты не задевает.
    for t, pitch, color in ((T(40), 0.46, C(0.86, 0.86, 0.92)), (T(54), 0.4, WHITE)):           # выше браслетов
        p, out, across, up = hk.sticking_out(k, EDGE + 0.04, t, GAP + 0.03, pitch)
        hk.slab(k, p, out, across, up, 0.24, 0.13, 0.005, color)
    tip = p + out * 0.18 + up * 0.006                          # масть на верхней карте: ромб
    hk.slab(k, tip - across * 0.0, out, across, up, 0.05, 0.035, 0.003, red)
    hk.slab(k, tip + out * 0.012, out, across, up, 0.026, 0.06, 0.003, red)
    k.roughness = 0.7
    return k


def make_pirate():
    k = hk.Kit("Sleeve_Pirate", ARM)
    linen, frill, lace = C(0.95, 0.92, 0.84), C(0.98, 0.97, 0.92), C(0.45, 0.28, 0.14)
    edge = EDGE + 0.05
    folds = lambda t: 0.016 * math.sin(t * 5)
    body(k, edge, lambda r, j: linen if j % 2 else shade(linen, 0.93), grow=1.2, wave=folds, inside=shade(linen, 0.75),
         thin=0.012)
    hk.band(k, EDGE + 0.02, EDGE + 0.07, GAP + 0.004, GAP + 0.03, C(0.62, 0.1, 0.12), grow=1.08, rows=1)
    hk.flare(k, EDGE + 0.05, EDGE - 0.08, GAP + 0.03, GAP + 0.12, lambda r, j: frill if j % 2 else shade(frill, 0.88),
             wave=lambda t: 0.026 * math.sin(t * 11), rows=3, thick=0.01)
    for q in range(4):                                          # шнуровка на тыльной стороне
        s0, s1 = edge + 0.02 + q * 0.05, edge + 0.07 + q * 0.05
        lift = surf(s0, edge) + 0.02
        for a, b in ((T(78), T(102)), (T(102), T(78))):
            path = [ARM.point(s0 + (s1 - s0) * i / 4, a + (b - a) * i / 4, lift, 1.2) for i in range(5)]
            hk.tube(k, path, 0.008, lace, sides=5)
    k.roughness = 0.85
    return k


def make_fortune():
    k = hk.Kit("Sleeve_Fortune", ARM)
    shawl, trim, lining = C(0.55, 0.12, 0.4), C(0.3, 0.06, 0.24), C(0.95, 0.75, 0.3)
    edge = EDGE + 0.04
    body(k, edge, lambda r, j: shawl if (j + r) % 5 else shade(shawl, 1.25), grow=1.28, drop=0.06, rows=8,
         inside=shade(lining, 0.8), thin=0.012)
    hk.band(k, edge - 0.02, edge + 0.05, GAP + 0.02, GAP + 0.04, trim, grow=1.3, drop=0.06, rows=1)
    for q in range(16):
        hk.cyl_on(k, edge + 0.015, T(q * 22.5), GAP + 0.04, 0.02, 0.006, GOLD, seg=8, grow=1.3, drop=0.06)
    for q in range(20):
        t = T(185 + q * 9)
        hk.hang(k, edge - 0.015, t, GAP + 0.03, 0.014, 0.09 + 0.03 * (q % 2), GOLD if q % 3 == 0 else trim,
                grow=1.3, drop=0.06)
    k.roughness = 0.75
    return k


def make_robe():
    k = hk.Kit("Sleeve_Robe", ARM)
    pink, quilt, cream, yellow = C(0.93, 0.6, 0.68), C(0.8, 0.45, 0.55), C(0.97, 0.93, 0.85), C(1.0, 0.82, 0.3)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: quilt if (r + j) % 4 == 0 or (j - r) % 4 == 0 else pink, grow=1.14, rows=12)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.04, cream, grow=1.04, rows=2,
            wave=lambda t, r: 0.012 * max(0.0, math.sin(t * 9)) * (r == 1))
    for s, t in ((0.32, 80), (0.42, 120), (0.52, 70), (0.38, 40), (0.6, 105), (0.68, 60)):
        lift = surf(s, edge)
        hk.cyl_on(k, s, T(t), lift, 0.016, 0.012, yellow, seg=6, grow=1.14)
        for q in range(5):
            a = q * 2 * math.pi / 5
            hk.cyl_on(k, s + 0.03 * math.cos(a), T(t) + 0.16 * math.sin(a), lift, 0.018, 0.008, WHITE, seg=6, grow=1.14)
    k.roughness = 0.9
    return k


def make_tavern():
    k = hk.Kit("Sleeve_Tavern", ARM)
    linen, towel, stripe = C(0.84, 0.77, 0.6), C(0.97, 0.96, 0.92), C(0.72, 0.16, 0.14)
    edge = 0.33
    body(k, edge, lambda r, j: linen if j % 2 else shade(linen, 0.94), grow=1.08, rows=6)
    hk.roll(k, edge, GAP + 0.006, 0.048, lambda r, j: shade(linen, 0.9) if r % 2 else linen, sides=8)
    hk.strip(k, 0.17, 0.29, T(-20), T(200), GAP + 0.004, 0.014, towel, rows=5,
             top=lambda r, c: stripe if r in (1, 3) else towel)
    for t in (T(-20), T(200)):                                  # концы полотенца свисают по бокам
        hk.flap(k, 0.17, 0.29, t, GAP + 0.011, 0.13, towel)
        hk.flap(k, 0.19, 0.205, t, GAP + 0.019, 0.13, stripe, thick=0.004)
        hk.flap(k, 0.255, 0.27, t, GAP + 0.019, 0.13, stripe, thick=0.004)
    k.roughness = 0.85
    return k


def remove_orphans(names):
    """Файлы рукавов, которых больше нет в списке (переименованы, убраны). У оставшихся .meta не трогается: GUID тот же."""
    for path in glob.glob(os.path.join(MODELS_DIR, "*.fbx")) + glob.glob(os.path.join(TEXTURES_DIR, "*.png")):
        if os.path.splitext(os.path.basename(path))[0] in names:
            continue
        os.remove(path)
        if os.path.exists(path + ".meta"):
            os.remove(path + ".meta")
        print("  удалён " + os.path.basename(path))


SLEEVES = [make_knit, make_shirt, make_cowboy, make_denim, make_croupier, make_sailor, make_kimono, make_mage,
           make_biker, make_mafia, make_ace, make_pirate, make_fortune, make_robe, make_tavern]


def main():
    global ARM
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

    armature, hand, ARM = hk.load_arm()
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, ok = [], True
    print("Рукава:")
    for make in SLEEVES:
        kit = make()
        if only and kit.name not in only:
            kit.bm.free()
            continue
        obj = kit.build(TEXTURES_DIR)
        tris = zm.triangle_count(obj.data)
        inside = hk.inside_count(obj, ARM)
        good = tris <= 4000 and inside <= 2
        ok &= good
        print("  %-16s треуг. %5d  цветов %2d  вершин в руке %d  %s" % (obj.name, tris, len(kit.colors), inside,
                                                                       "OK" if good else "ПРОВЕРИТЬ"))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        hk.render(render_dir, objs, armature, hand, "sleeves")
    for obj in objs:
        hk.export(obj, ARM, MODELS_DIR)
    if not only:
        remove_orphans({obj.name for obj in objs})
    if not ok:
        raise SystemExit("Есть рукава с ошибками")


if __name__ == "__main__":
    main()
