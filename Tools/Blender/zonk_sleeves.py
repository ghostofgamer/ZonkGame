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
import zonk_wrist as zw  # noqa: E402
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


def fold(t, s, amp):
    """Мягкий рельеф ткани: пологие волны почти вдоль руки, чуть наискось. Цвет не меняет — заломы рисует creases."""
    if amp <= 0:
        return 0.0
    return amp * (0.65 * math.sin(4 * t + 4 * s) + 0.35 * math.sin(7 * t - 6 * s + 1.3))


# Заломы комикса: (s от края, угол, длина вдоль руки, наклон по кругу) — на тыльной стороне и у большого пальца,
# где их видит камера партии. Длина и наклон в долях: штрих идёт от края к локтю и уходит вбок.
CREASES = [(0.03, 62, 0.08, 16), (0.04, 98, 0.07, -12), (0.15, 80, 0.07, -14), (0.2, 112, 0.06, 12),
           (0.1, 40, 0.06, 10)]


def ink(k, path, radius, color, sides=4):
    """Штрих комикса: трубка по точкам, толще посередине, концы сходятся в точку."""
    rings = []
    m = len(path) - 1
    for q, p in enumerate(path):
        d = (path[min(q + 1, m)] - path[max(q - 1, 0)]).normalized()
        u = d.orthogonal().normalized()
        v = d.cross(u).normalized()
        r = radius * max(0.12, math.sin(math.pi * q / m)) ** 0.7
        rings.append([k.vert(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * r)
                      for i in range(sides)])
    k.quads(rings, color, closed=True)
    k.fan(rings[0], path[0], color, flip=True)
    k.fan(rings[-1], path[-1], color)


def body(k, edge, color, grow=1.06, drop=0.0, rows=10, wave=None, edge_color=None, inside=hk.INSIDE, thin=None,
         folds=0.008, crease=None, bulge=None):
    """
    Рукав от края edge до донца у локтя; к локтю чуть шире. thin — широкий рукав тонкой тканью с подкладкой.
    folds — мягкий рельеф ткани; bulge(s) — напуск ткани (над резинкой). crease — устарело: штрихи-заломы
    читались как царапины (отзыв 06.11), больше не рисуются.
    color(r, j): j — номер грани по кругу в счёте 32 граней (узоры писались под 32, граней теперь hk.SEG).
    """
    rings = []
    for r in range(rows + 1):
        q = r / rows
        s = edge + (ARM.back - edge) * q
        w = (lambda t, s=s: fold(t, s, folds) + (wave(t) if wave else 0.0))
        rings.append(dict(s=s, add=surf(s, edge) + (bulge(s) if bulge else 0.0), grow=grow, drop=drop, wave=w))
    scaled = (lambda r, j: color(r, j * 32 // hk.SEG)) if callable(color) else color
    hk.sleeve(k, rings, scaled, edge_color=edge_color, inside=inside, thin=thin)


def T(deg):
    return math.radians(deg)


# ---------------------------------------------------------------------------
# Детали поверх ткани
# ---------------------------------------------------------------------------

def at(k, s, t, edge, grow=1.06, extra=0.0, drop=0.0, folds=0.008, thin=None):
    """
    Оси на поверхности рукава (с рельефом складок): (точка, наружу, к локтю, по кругу). Провис считается как у самой
    ткани (та же высота), а extra — подъём над ней: иначе у свисающего рукава деталь уходит внутрь.
    """
    add = surf(s, edge) + fold(t, s, folds)
    if drop and thin:
        drop = min(drop, ARM.max_drop(s, surf(s, edge) - thin, grow))       # как у ткани с подкладкой (hk.sleeve)
    p, n, along, across = hk.frame_at(k, s, t, add, grow, drop)
    return p + n * extra, n, along, across


class Wrap:
    """
    Всё, что построено внутри блока, ложится на поверхность (как нашивка на рукав): высота вершины над плоскостью детали
    сохраняется, а сама плоскость заменяется поверхностью height(s, t) — высотой над кожей с ростом grow и провисом drop.
    Иначе плоская деталь касается выпуклого рукава одной точкой, а края уходят в ткань (отзыв автора 06.11).
    """
    EPS = 0.0015    # зазор над тканью: без него грань детали мерцает вместе с тканью

    def __init__(self, k, frame, height, grow, drop):
        self.k, self.frame, self.height, self.grow, self.drop = k, frame, height, grow, drop

    def __enter__(self):
        self.start = len(self.k.bm.verts)
        return self.frame

    def __exit__(self, kind, value, trace):
        if kind:
            return False
        p, n = self.frame[0], self.frame[1]
        bm = self.k.bm
        bm.verts.ensure_lookup_table()
        for v in bm.verts[self.start:]:
            h = (v.co - p).dot(n)
            s = (v.co - ARM.palm).dot(ARM.axis)
            c, a, b = ARM.ellipse(s)
            q = v.co - (c - ARM.up * self.drop)
            x, z = q.dot(ARM.side), q.dot(ARM.up)
            t = math.atan2(z / (b * self.grow), x / (a * self.grow))
            add = self.height(s, t)
            t = math.atan2(z / (b * self.grow + add), x / (a * self.grow + add))
            v.co = ARM.point(s, t, self.height(s, t) + self.EPS, self.grow, self.drop) + ARM.normal(s, t) * h
        return False


def on(k, s, t, edge, grow=1.06, extra=0.0, drop=0.0, folds=0.008, thin=None):
    """Деталь на ткани рукава (with on(...) as (p, n, along, across): ...): ложится по рукаву со складками."""
    frame = at(k, s, t, edge, grow, extra, drop, folds, thin)
    if drop and thin:
        drop = min(drop, ARM.max_drop(s, surf(s, edge) - thin, grow))
    return Wrap(k, frame, lambda ss, tt: surf(ss, edge) + fold(tt, ss, folds) + extra, grow, drop)


def on_ring(k, s, t, add, grow=1.0, drop=0.0, wave=None):
    """Деталь на ремешке или манжете (hk.band с верхом add): ложится по их кругу."""
    frame = hk.frame_at(k, s, t, add + (wave(t) if wave else 0.0), grow, drop)
    if drop:
        drop = min(drop, ARM.max_drop(s, add, grow))
    return Wrap(k, frame, lambda ss, tt: add + (wave(tt) if wave else 0.0), grow, drop)


def flat_shape(k, c, n, e1, radius_at, count, h, color, side=None):
    """Плоская фигура (звезда, цветок, луна) толщиной h: radius_at(угол) — контур вокруг центра c, count — точек."""
    e2 = n.cross(e1).normalized()
    lo, hi = [], []
    for q in range(count):
        a = 2 * math.pi * q / count
        r = radius_at(a)
        d = e1 * math.cos(a) + e2 * math.sin(a)
        lo.append(k.vert(c + d * r))
        hi.append(k.vert(c + d * r + n * h))
    side = side or shade(color, 0.75)
    k.fan(hi, c + n * h, color, flip=True)
    k.fan(lo, c, side)
    for q in range(count):
        j = (q + 1) % count
        k.face((lo[q], lo[j], hi[j], hi[q]), side, smooth=False)


def star(k, c, n, e1, r_out, color, points=5, h=0.006, inner=0.45):
    flat_shape(k, c, n, e1, lambda a: r_out if round(a / (math.pi / points)) % 2 == 0 else r_out * inner,
               points * 2, h, color)


def heart(k, c, n, up, size, color, h=0.004):
    """Сердце (черви, бубны — нет): верх к up."""
    e2 = n.cross(up).normalized()
    for x in (-1, 1):
        zw.disc(k, c + up * size * 0.25 + e2 * x * size * 0.26, n, up, size * 0.3, h, color, seg=10)
    flat_shape(k, c - up * size * 0.05, n, -up, lambda a: size * 0.52, 3, h, color)      # низ сердца: вершина вниз


def spade(k, c, n, up, size, color, h=0.004):
    """Пики: перевёрнутое сердце и ножка."""
    heart(k, c, n, -up, size, color, h)
    hk.slab(k, c - up * size * 0.25, -up, n.cross(up).normalized(), n, size * 0.55, size * 0.16, h, color)


def stitched_patch(k, p, n, along, across, w, l, color, stitch):
    """Нашивка со строчкой по краю."""
    zw.chamfer_box(k, p + n * 0.003, across, along, n, w / 2, l / 2, 0.003, 0.003, color, shade(color, 0.8))
    top = p + n * 0.006
    for i in range(6):
        for x, y, d, e in ((-1, -1 + 2 * (i + 0.5) / 6, along, across), (1, -1 + 2 * (i + 0.5) / 6, along, across),
                           (-1 + 2 * (i + 0.5) / 6, -1, across, along), (-1 + 2 * (i + 0.5) / 6, 1, across, along)):
            c = top + across * x * (w / 2 - 0.007) + along * y * (l / 2 - 0.007)
            hk.slab(k, c - d * 0.004, d, e, n, 0.008, 0.003, 0.0015, stitch)


def buckle(k, p, n, along, across, w, l, color):
    """Рамка пряжки поперёк ремня (along — вдоль ремня) и язычок."""
    t = 0.007
    for x in (-1, 1):
        hk.slab(k, p + across * x * (w / 2 - t / 2) - along * l / 2, along, across, n, l, t, 0.008, color)
        hk.slab(k, p + along * x * (l / 2 - t / 2) - across * w / 2, across, along, n, w, t, 0.008, color)
    hk.slab(k, p - along * l / 2, along, across, n, l * 0.9, 0.004, 0.01, shade(color, 0.8))


def strap(k, s, width, edge, grow, color, extra=0.012, buckle_t=None, buckle_color=SILVER):
    """Ремешок вокруг рукава (нарукавная резинка, ремень наруча) с пряжкой на угле buckle_t."""
    lift = surf(s, edge)
    hk.band(k, s - width / 2, s + width / 2, lift - 0.004, lift + extra, color, grow=grow, rows=1)
    if buckle_t is not None:
        with on_ring(k, s, buckle_t, lift + extra, grow) as (p, n, along, across):
            buckle(k, p, n, across, along, width * 1.25, width * 1.1, buckle_color)


# ---------------------------------------------------------------------------
# Рукава
# ---------------------------------------------------------------------------

def make_knit():
    """Свитер: коса-аран полосой по тыльной стороне, полоски и резинка."""
    k = hk.Kit("Sleeve_Knit", ARM)
    green, dark, cream = C(0.2, 0.42, 0.33), C(0.13, 0.29, 0.22), C(0.93, 0.88, 0.74)
    edge = EDGE + 0.09
    body(k, edge, lambda r, j: cream if r in (1, 3) else green, rows=12, folds=0.006)
    ribs = lambda t, r: 0.008 * max(0.0, math.cos(t * 16)) * (1 if r else 0)
    hk.band(k, EDGE - 0.005, EDGE + 0.1, GAP * 0.4, GAP + 0.012,
            lambda r, j: dark if j % 2 else shade(dark, 1.25), grow=1.03, wave=ribs, rows=1)
    # коса-аран после полосок: ровная основа, на ней косые валики с фаской — наклон то влево, то вправо (перекрут)
    s0, s1 = edge + 0.2, ARM.back - 0.06
    mid = (s0 + s1) / 2
    base, twist = shade(green, 0.92), shade(green, 1.3)
    # полосы уже лежат по изгибу рукава (без обёртки); над складками (до 0.006) — с запасом
    hk.strip(k, s0, s1, T(80), T(100), surf(mid, edge) + 0.009, 0.005, base, rows=8, cols=4, grow=1.06)
    for t0 in (T(73), T(104)):
        hk.strip(k, s0, s1, t0, t0 + T(3), surf(mid, edge) + 0.007, 0.008, shade(green, 1.2), rows=8, cols=1, grow=1.06)
    with on(k, mid, T(90), edge, 1.06, 0.013, folds=0.0) as (p, n, along, across):
        steps = int((s1 - s0) / 0.034)
        for q in range(steps):
            c = p + along * (s0 + 0.017 + q * 0.034 - mid) + n * 0.007
            lean = 1 if q % 2 else -1
            e1 = (across * 0.8 * lean + along * 0.6).normalized()
            e2 = n.cross(e1).normalized()
            zw.chamfer_box(k, c, e1, e2, n, 0.03, 0.011, 0.007, 0.005, twist, shade(twist, 0.85))
    k.roughness = 0.9
    return k


def make_shirt():
    """Голубая оксфордская рубашка: манжета с двумя пуговицами, планка, запонка-кость."""
    k = hk.Kit("Sleeve_Shirt", ARM)
    blue, pearl, stitch = C(0.72, 0.83, 0.95), C(0.97, 0.96, 0.92), C(0.55, 0.66, 0.82)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: blue if j % 3 else shade(blue, 1.06), grow=1.07)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.022, blue, rows=1)
    hk.roll(k, EDGE - 0.006, GAP + 0.012, 0.006, shade(blue, 1.08), sides=6)                  # кант манжеты
    for s in (EDGE + 0.008, EDGE + 0.082):
        hk.band(k, s, s + 0.005, GAP + 0.02, GAP + 0.026, stitch, rows=1)
    for t in (T(40), T(62)):                                                                   # две пуговицы
        with on_ring(k, EDGE + 0.045, t, GAP + 0.022) as (p, n, along, across):
            zw.lathe(k, p, n, along, across, [(0, 0), (0.017, 0), (0.017, 0.006), (0.012, 0.008), (0, 0.007)], pearl,
                     seg=10)
            for x in (-1, 1):
                zw.disc(k, p + n * 0.0075 + across * x * 0.005, n, along, 0.0025, 0.001, shade(pearl, 0.6), seg=5)
    hk.strip(k, edge, edge + 0.13, T(150), T(162), surf(edge, edge) + 0.004, 0.006, shade(blue, 0.93), rows=2, cols=1,
             grow=1.07)                                                                         # планка
    with on(k, edge + 0.08, T(156), edge, 1.07, 0.01) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.014, 0.006, pearl, seg=8)
    with on_ring(k, EDGE + 0.045, T(90), GAP + 0.022) as (p, n, along, across):              # запонка-кость
        zw.die(k, p + n * 0.02, n, along, across, 0.04, C(0.95, 0.8, 0.35), C(0.75, 0.55, 0.15), BLACK, [(n, 5)])
    k.roughness = 0.7
    return k


def make_cowboy():
    """Ковбойская клетка: закатанный рукав, бахрома вдоль руки и звезда шерифа."""
    k = hk.Kit("Sleeve_Cowboy", ARM)
    red, dark, cream = C(0.72, 0.13, 0.11), C(0.38, 0.06, 0.06), C(0.92, 0.85, 0.7)
    suede = C(0.66, 0.43, 0.22)

    def plaid(r, j):
        a, b = j % 4 == 0, r % 2 == 0
        if j % 8 == 2:
            return cream
        return shade(dark, 0.8) if a and b else dark if a or b else red

    edge = EDGE + 0.1
    body(k, edge, plaid, rows=14, grow=1.07, crease=shade(dark, 0.6))
    hk.roll(k, edge, GAP + 0.006, 0.042, lambda r, j: plaid(j, r), sides=8)
    for t in (T(65), T(115)):
        hk.cyl_on(k, edge, t, GAP + 0.09, 0.016, 0.008, C(0.95, 0.93, 0.88), seg=8, bevel=0.003)
    # кожаная накладка и бахрома по шву у мизинца
    hk.strip(k, edge + 0.05, ARM.back - 0.05, T(4), T(20), surf(0.4, edge) + 0.002, 0.008, suede, rows=3, cols=2,
             grow=1.07)
    for q in range(16):
        s = edge + 0.07 + q * 0.024
        hk.hang(k, s, T(8), surf(s, edge) + 0.008, 0.014, 0.085 + 0.02 * (q % 2), suede if q % 2 else shade(suede, 0.85),
                thick=0.006, grow=1.07)
    with on(k, edge + 0.12, T(92), edge, 1.07, 0.002) as (p, n, along, across):              # звезда шерифа
        star(k, p, n, -along, 0.068, C(1.0, 0.8, 0.3), h=0.01)
        for q in range(5):
            a = 2 * math.pi * q / 5
            d = -along * math.cos(a) + across * math.sin(a)
            hk.ball(k, p + d * 0.069 + n * 0.008, 0.01, C(1.0, 0.88, 0.5), seg=5, rings=3)
        zw.disc(k, p + n * 0.01, n, along, 0.022, 0.003, C(0.85, 0.6, 0.2), seg=10)
    k.roughness = 0.8
    return k


def make_denim():
    """Джинсовая куртка: нашивки (кость, звезда), значки, строчка, медные пуговицы."""
    k = hk.Kit("Sleeve_Denim", ARM)
    denim, light, stitch, copper = C(0.25, 0.38, 0.62), C(0.36, 0.5, 0.74), C(0.92, 0.62, 0.22), C(0.78, 0.46, 0.22)
    edge = EDGE + 0.06
    body(k, edge, lambda r, j: light if j in (14, 15, 16) else denim, grow=1.1, crease=shade(denim, 0.6))
    for t0 in (T(162), T(170)):
        hk.strip(k, edge + 0.03, ARM.back - 0.04, t0, t0 + T(3), surf(0.5, edge) - 0.002, 0.006, stitch, cols=1, grow=1.1)
    hk.band(k, EDGE - 0.01, EDGE + 0.08, GAP * 0.5, GAP + 0.024, shade(denim, 0.85), rows=1)
    hk.band(k, EDGE + 0.006, EDGE + 0.012, GAP + 0.02, GAP + 0.028, stitch, rows=1)
    hk.band(k, EDGE + 0.058, EDGE + 0.064, GAP + 0.02, GAP + 0.028, stitch, rows=1)
    for s in (EDGE + 0.035,):
        with on_ring(k, s, T(150), GAP + 0.024):
            hk.cyl_on(k, s, T(150), GAP + 0.024, 0.022, 0.012, copper, seg=8, bevel=0.004)
    # нашивка-кость
    with on(k, edge + 0.1, T(98), edge, 1.1) as (p, n, along, across):
        stitched_patch(k, p, n, along, across, 0.14, 0.14, C(0.85, 0.15, 0.15), WHITE)
        zw.pips(k, p + n * 0.0075, n, along, 0.15, 5, WHITE)
    # нашивка-звезда
    with on(k, edge + 0.22, T(62), edge, 1.1) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.07, 0.004, C(0.12, 0.12, 0.14), seg=16)
        star(k, p + n * 0.004, n, -along, 0.06, C(1.0, 0.82, 0.2), h=0.004)
    # значки: «восьмёрка» и сердечко
    with on(k, edge + 0.03, T(70), edge, 1.1) as (p, n, along, across):
        zw.lathe(k, p, n, along, across, [(0, 0), (0.026, 0), (0.026, 0.006), (0.02, 0.01), (0, 0.011)], BLACK, seg=12)
        zw.disc(k, p + n * 0.01, n, along, 0.012, 0.002, WHITE, seg=10)
        zw.glint(k, p + n * 0.011 - along * 0.01 + across * 0.012, n, along, 0.012, 0.004)
    with on(k, edge + 0.2, T(118), edge, 1.1) as (p, n, along, across):
        heart(k, p, n, -along, 0.06, C(0.95, 0.25, 0.4), h=0.008)
    k.roughness = 0.85
    return k


def make_croupier():
    """Рубашка крупье: напуск ткани над широкой резинкой с пряжкой, запонки-фишки."""
    k = hk.Kit("Sleeve_Croupier", ARM)
    edge = EDGE + 0.08
    red = C(0.75, 0.1, 0.12)
    garter = 0.4
    puff = lambda s: 0.03 * math.exp(-((s - (garter - 0.06)) / 0.06) ** 2) - 0.012 * math.exp(-((s - garter) / 0.03) ** 2)
    body(k, edge, lambda r, j: WHITE if j % 2 else shade(WHITE, 0.95), grow=1.12, rows=16, bulge=puff)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.022, WHITE, rows=1)
    hk.roll(k, EDGE - 0.006, GAP + 0.012, 0.006, shade(WHITE, 0.92), sides=6)
    for t in (T(48), T(72)):                                                                   # запонки-фишки
        with on_ring(k, EDGE + 0.045, t, GAP + 0.022) as (p, n, along, across):
            zw.lathe(k, p, n, along, across, [(0, 0), (0.024, 0), (0.024, 0.01), (0, 0.01)],
                     lambda i, q: (WHITE if q % 3 == 0 else red) if i == 1 else red, seg=12)
            zw.disc(k, p + n * 0.01, n, along, 0.013, 0.002, WHITE, seg=10)
    lift = surf(garter, edge) + puff(garter)
    hk.band(k, garter - 0.04, garter + 0.04, lift - 0.006, lift + 0.02,
            lambda r, j: red if r in (0, 2) else BLACK, grow=1.12, rows=3, wave=lambda t, r: 0.004 * math.sin(t * 6))
    with on_ring(k, garter, T(95), lift + 0.022, 1.12, wave=lambda t: 0.004 * math.sin(t * 6)) as (p, n, along, across):
        buckle(k, p, n, across, along, 0.09, 0.07, SILVER)
    k.roughness = 0.6
    return k


def anchor_badge(k, p, n, up, size, color):
    """Якорь-нашивка плашмя: веретено, шток, рога дугой."""
    e = n.cross(up).normalized()
    hk.slab(k, p - up * size * 0.45, up, e, n, size * 0.9, size * 0.12, 0.005, color)
    hk.slab(k, p + up * size * 0.25 - e * size * 0.3, e, up, n, size * 0.6, size * 0.1, 0.005, color)
    hk.link(k, p + up * size * 0.52, up, e, size * 0.1, size * 0.04, color, stretch=1.0, seg=8, sides=4)
    arc = [p - up * size * 0.25 + (-up * math.sin(math.pi * q / 8) * 0.25 + e * math.cos(math.pi * q / 8) * 0.38) * size
           for q in range(9)]
    hk.tube(k, [a + n * 0.003 for a in arc], size * 0.06, color, sides=5)


def make_sailor():
    """Тельняшка, закатанная до локтя, нашивка с якорем."""
    k = hk.Kit("Sleeve_Sailor", ARM)
    navy = C(0.12, 0.2, 0.45)
    edge = 0.27
    body(k, edge + 0.04, lambda r, j: WHITE if r % 2 else navy, rows=18, grow=1.05, folds=0.005)
    hk.roll(k, edge + 0.04, GAP + 0.006, 0.045, lambda r, j: navy if r % 2 else WHITE, sides=8)
    with on(k, 0.45, T(88), edge + 0.04, 1.05, folds=0.005) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.085, 0.004, navy, seg=18)
        zw.lathe(k, p + n * 0.004, n, along, across, [(0.072, 0), (0.08, 0), (0.08, 0.003), (0.072, 0.003)],
                 C(1.0, 0.8, 0.3), seg=18)
        anchor_badge(k, p + n * 0.004, n, -along, 0.11, C(1.0, 0.8, 0.3))
    k.roughness = 0.85
    return k


def make_kimono():
    """Широкий рукав кимоно: однотонное индиго, светлая кайма у края, белый герб-цветок, красная подкладка."""
    k = hk.Kit("Sleeve_Kimono", ARM)
    indigo, sky, red = C(0.16, 0.2, 0.42), C(0.55, 0.68, 0.9), C(0.72, 0.12, 0.12)
    body(k, EDGE, lambda r, j: sky if r == 1 else indigo, grow=1.5, drop=0.14, rows=10, edge_color=WHITE, inside=red,
         thin=0.014, folds=0.006)
    hk.band(k, EDGE + 0.002, EDGE + 0.04, GAP + 0.002, GAP + 0.012, WHITE, grow=1.5, drop=0.14, rows=1)
    with on(k, EDGE + 0.17, T(92), EDGE, 1.5, 0.0, 0.14, folds=0.006, thin=0.014) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.075, 0.004, WHITE, seg=24)
        for q in range(5):
            a = 2 * math.pi * q / 5
            d = -along * math.cos(a) + across * math.sin(a)
            zw.disc(k, p + n * 0.004 + d * 0.03, n, along, 0.025, 0.003, red, seg=12)
        zw.disc(k, p + n * 0.007, n, along, 0.013, 0.002, C(1.0, 0.82, 0.3), seg=10)
    k.roughness = 0.75
    return k


def make_mage():
    """Мантия мага: тёмно-фиолетовый бархат, раструб с золотой каймой и рунами, луна, звёзды, кисточки."""
    k = hk.Kit("Sleeve_Mage", ARM)
    purple, gold, lining = C(0.3, 0.15, 0.46), C(1.0, 0.82, 0.3), C(0.12, 0.1, 0.32)
    edge = EDGE + 0.04
    body(k, edge, purple, grow=1.3, drop=0.06, rows=8, inside=lining, thin=0.014, folds=0.006)
    hk.flare(k, edge + 0.03, edge - 0.09, GAP + 0.05, GAP + 0.12, lambda r, j: gold if r == 2 else shade(purple, 0.8),
             wave=lambda t: 0.012 * math.sin(t * 5), rows=3, thick=0.014, grow=1.25, drop=0.05)
    for q in range(9):                                                                         # руны на кайме
        hk.box_on(k, edge - 0.06, T(42 + q * 12), GAP + 0.098, 0.02, 0.028, 0.008, C(0.4, 0.95, 1.0) if q % 2 else gold,
                  grow=1.25, drop=0.05)
    with on(k, edge + 0.15, T(92), edge, 1.3, 0.0, 0.06, folds=0.006, thin=0.014) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.08, 0.006, gold, seg=24)
        zw.disc(k, p + across * 0.036 - along * 0.016 + n * 0.0005, n, along, 0.069, 0.0068, purple, seg=24)
    for s, t, r in ((0.3, 60, 0.026), (0.44, 118, 0.02), (0.5, 72, 0.018), (0.58, 104, 0.024), (0.36, 135, 0.016),
                    (0.66, 82, 0.018), (0.4, 40, 0.014), (0.62, 128, 0.015)):
        with on(k, s, T(t), edge, 1.3, 0.0, 0.06, folds=0.006, thin=0.014) as (p, n, along, across):
            star(k, p, n, -along, r * 1.4, gold, points=4, h=0.005, inner=0.35)
    for t in (T(70), T(100), T(130)):                                                          # кисточки на краю
        hk.hang(k, edge - 0.085, t, GAP + 0.11, 0.014, 0.07, gold, grow=1.25, drop=0.05)
    k.roughness = 0.7
    return k


def make_biker():
    """Кожаная куртка: молния, шипы на манжете, нашивка «кость с крыльями», ремешок с пряжкой."""
    k = hk.Kit("Sleeve_Biker", ARM)
    leather, shine = C(0.11, 0.11, 0.12), C(0.24, 0.24, 0.27)
    edge = EDGE + 0.07
    body(k, edge, lambda r, j: shine if j in (18, 19) else leather, grow=1.12, crease=shade(shine, 1.6))
    hk.band(k, EDGE - 0.01, EDGE + 0.08, GAP * 0.5, GAP + 0.026, shade(leather, 1.3), rows=1)
    for q in range(10):
        hk.stud(k, EDGE + 0.035, T(20 + q * 15), GAP + 0.026, 0.017, 0.032, SILVER)
    for q in range(14):                                                                        # молния вдоль руки
        s = edge + 0.03 + q * 0.035
        with on(k, s, T(130), edge, 1.12):
            hk.box_on(k, s, T(130), surf(s, edge) + fold(T(130), s, 0.008), 0.02, 0.022, 0.008,
                      SILVER if q % 2 else shade(SILVER, 0.7), grow=1.12)
    with on(k, edge + 0.02, T(130), edge, 1.12, 0.004):
        hk.box_on(k, edge + 0.02, T(130), surf(edge, edge) + fold(T(130), edge + 0.02, 0.008) + 0.004, 0.032, 0.05, 0.014,
                  SILVER, grow=1.12)
    # нашивка: кость с крыльями
    with on(k, edge + 0.13, T(92), edge, 1.12) as (p, n, along, across):
        zw.disc(k, p, n, along, 0.09, 0.004, C(0.75, 0.12, 0.1), seg=18)
        zw.chamfer_box(k, p + n * 0.02, across, along, n, 0.03, 0.03, 0.016, 0.006, WHITE, shade(WHITE, 0.8))
        zw.pips(k, p + n * 0.036, n, along, 0.06, 6, BLACK)
        for x in (-1, 1):
            for q in range(3):
                d = (across * x * math.cos(T(15 + q * 22)) - along * math.sin(T(15 + q * 22))).normalized()
                hk.slab(k, p + n * 0.006 + across * x * 0.028, d, d.cross(n).normalized(), n, 0.07 - q * 0.012, 0.016,
                        0.005, WHITE)
    strap(k, 0.5, 0.05, edge, 1.12, shade(leather, 1.4), buckle_t=T(95))
    k.roughness = 0.45
    return k


def make_mafia():
    """Костюм в тонкую полоску: белая манжета с кантом, запонка с бриллиантом, четыре пуговицы."""
    k = hk.Kit("Sleeve_Mafia", ARM)
    wool, pin = C(0.17, 0.17, 0.2), C(0.6, 0.6, 0.66)
    edge = EDGE + 0.05
    body(k, edge, wool, grow=1.12)
    for q in range(9):                                                                         # тонкие полоски
        t0 = T(-10 + q * 22)
        hk.strip(k, edge + 0.01, ARM.back - 0.02, t0, t0 + T(1.6), surf(0.4, edge) + 0.006, 0.003, pin, rows=6, cols=1,
                 grow=1.12)
    hk.band(k, EDGE - 0.01, edge + 0.02, GAP * 0.5, GAP + 0.018, WHITE, rows=1)
    hk.roll(k, EDGE - 0.006, GAP + 0.01, 0.006, shade(WHITE, 0.92), sides=6)
    hk.roll(k, edge + 0.004, surf(edge, edge) + 0.004, 0.006, shade(wool, 1.5), sides=6, grow=1.12)  # кант рукава
    with on_ring(k, EDGE + 0.02, T(62), GAP + 0.018) as (p, n, along, across):
        zw.lathe(k, p, n, along, across, [(0, 0), (0.032, 0), (0.034, 0.008), (0.027, 0.012), (0, 0.012)],
                 lambda i, q: GOLD if i != 2 else shade(GOLD, 1.2), seg=12)
        zw.gem_cut(k, p + n * 0.012, n, along, 0.021, WHITE, seg=8)
    for q in range(4):
        with on(k, edge + 0.035 + q * 0.032, T(118), edge, 1.12) as (p, n, along, across):
            zw.lathe(k, p, n, along, across, [(0, 0), (0.019, 0), (0.019, 0.007), (0.014, 0.011), (0, 0.011)], BLACK,
                     seg=8)
    k.roughness = 0.6
    return k


def card(k, p, out, across, up, color, suit, suit_color):
    """Карта: белая с каймой, в углу и посередине масть."""
    hk.slab(k, p, out, across, up, 0.24, 0.15, 0.005, color)
    top = p + up * 0.005
    centre = top + out * 0.15
    if suit == "heart":
        heart(k, centre, up, out, 0.06, suit_color, h=0.003)
    else:
        spade(k, centre, up, out, 0.06, suit_color, h=0.003)
    hk.slab(k, top + out * 0.215 - across * 0.05, out, across, up, 0.016, 0.012, 0.003, suit_color)


def make_ace():
    """Шулер: два туза веером из манжеты, красная резинка с пряжкой."""
    k = hk.Kit("Sleeve_Ace", ARM)
    shirt, stripe, red = C(0.95, 0.93, 0.86), C(0.62, 0.72, 0.86), C(0.8, 0.12, 0.12)
    edge = EDGE + 0.08
    body(k, edge, lambda r, j: stripe if j % 4 == 0 else shirt, grow=1.08, crease=shade(stripe, 0.75))
    hk.band(k, EDGE - 0.01, EDGE + 0.09, GAP * 0.5, GAP + 0.018, shirt, rows=1)
    garter = 0.42
    lift = surf(garter, edge)
    hk.band(k, garter - 0.03, garter + 0.03, lift - 0.004, lift + 0.016, red, grow=1.08, rows=1,
            wave=lambda t, r: 0.004 * math.sin(t * 6))
    with on_ring(k, garter, T(95), lift + 0.016, 1.08, wave=lambda t: 0.004 * math.sin(t * 6)) as (p, n, along, across):
        buckle(k, p, n, across, along, 0.07, 0.055, GOLD)
    # Тузы выезжают из манжеты сбоку (у большого пальца) и отходят от руки: часы и браслеты не задевают.
    for t, pitch, suit, col in ((T(38), 0.5, "spade", BLACK), (T(54), 0.4, "heart", red)):
        p, out, across, up = hk.sticking_out(k, EDGE + 0.04, t, GAP + 0.03, pitch)
        card(k, p, out, across, up, WHITE, suit, col)
    k.roughness = 0.7
    return k


def make_pirate():
    """Пиратская рубаха: оборка, красный кушак, кожаный наруч с пряжками и заклёпками."""
    k = hk.Kit("Sleeve_Pirate", ARM)
    linen, frill, lace = C(0.95, 0.92, 0.84), C(0.98, 0.97, 0.92), C(0.45, 0.28, 0.14)
    brown = C(0.42, 0.24, 0.12)
    edge = EDGE + 0.05
    folds = lambda t: 0.016 * math.sin(t * 5)
    body(k, edge, lambda r, j: linen if j % 2 else shade(linen, 0.93), grow=1.2, rows=8, wave=folds, inside=shade(linen, 0.75),
         thin=0.012, folds=0.0)
    hk.band(k, EDGE + 0.02, EDGE + 0.07, GAP + 0.004, GAP + 0.03, C(0.62, 0.1, 0.12), grow=1.08, rows=1)
    hk.flare(k, EDGE + 0.05, EDGE - 0.08, GAP + 0.03, GAP + 0.12, lambda r, j: frill if j % 2 else shade(frill, 0.88),
             wave=lambda t: 0.026 * math.sin(t * 11), rows=3, thick=0.01)
    # наруч: кожа поверх рубахи, два ремешка с пряжками, заклёпки
    s0, s1 = edge + 0.05, edge + 0.19
    hk.band(k, s0, s1, GAP + 0.02, GAP + 0.05, lambda r, j: brown if r != 1 else shade(brown, 1.25), grow=1.25, rows=2)
    for s in (s0 + 0.035, s1 - 0.035):
        hk.band(k, s - 0.012, s + 0.012, GAP + 0.04, GAP + 0.058, shade(brown, 0.6), grow=1.25, rows=1)
        with on_ring(k, s, T(70), GAP + 0.058, 1.25) as (p, n, along, across):
            buckle(k, p, n, across, along, 0.035, 0.032, C(0.85, 0.65, 0.3))
    for q in range(6):
        with on_ring(k, (s0 + s1) / 2, T(95 + q * 12), GAP + 0.05, 1.25) as (p, n, along, across):
            zw.lathe(k, p, n, along, across, [(0, 0), (0.009, 0), (0.007, 0.005), (0, 0.007)], C(0.85, 0.65, 0.3),
                     seg=6)
    k.roughness = 0.85
    return k


def make_fortune():
    """Шаль гадалки: однотонная с золотой вышивкой полосами, монеты, бахрома, глаз с лучами."""
    k = hk.Kit("Sleeve_Fortune", ARM)
    shawl, trim, lining = C(0.55, 0.12, 0.4), C(0.3, 0.06, 0.24), C(0.95, 0.75, 0.3)
    edge = EDGE + 0.04
    body(k, edge, lambda r, j: GOLD if r in (3, 6) else shawl,
         grow=1.28, drop=0.06, rows=10, inside=shade(lining, 0.8), thin=0.012, folds=0.006)
    hk.band(k, edge - 0.02, edge + 0.05, GAP + 0.02, GAP + 0.04, trim, grow=1.3, drop=0.06, rows=1)
    for q in range(16):
        with on_ring(k, edge + 0.015, T(q * 22.5), GAP + 0.04, 1.3, 0.06) as (p, n, along, across):
            zw.lathe(k, p, n, along, across, [(0, 0), (0.021, 0), (0.021, 0.006), (0.014, 0.006), (0.014, 0.008),
                                              (0, 0.008)],
                     lambda i, qq: GOLD if i != 3 else shade(GOLD, 0.75), seg=10)
    for q in range(20):
        t = T(185 + q * 9)
        hk.hang(k, edge - 0.015, t, GAP + 0.03, 0.014, 0.09 + 0.03 * (q % 2), GOLD if q % 3 == 0 else trim,
                grow=1.3, drop=0.06)
    m = 1.45
    with on(k, edge + 0.16, T(92), edge, 1.28, 0.0, 0.06, folds=0.006, thin=0.012) as (p, n, along, across):
        for q in range(10):
            a = math.pi * q / 9
            d = (across * math.cos(a) - along * math.sin(a)).normalized()
            hk.slab(k, p + d * 0.045 * m, d, d.cross(n).normalized(), n, 0.025 * m, 0.007 * m, 0.004, GOLD)
        flat_shape(k, p, n, across,
                   lambda a: 0.048 * m * (1.0 if abs(math.sin(a)) < 0.2 else 0.5 + 0.5 * abs(math.cos(a))),
                   16, 0.004, C(0.97, 0.95, 0.9), side=GOLD)
        zw.disc(k, p + n * 0.004, n, along, 0.02 * m, 0.002, C(0.2, 0.55, 0.9), seg=12)
        zw.disc(k, p + n * 0.006, n, along, 0.009 * m, 0.002, BLACK, seg=8)
        zw.glint(k, p + n * 0.008 - along * 0.006 + across * 0.008, n, along, 0.008, 0.003)
    k.roughness = 0.75
    return k


def flower(k, p, n, along, petal, center, size=0.018):
    """Объёмный цветок: пять лепестков-подушечек и серединка."""
    for q in range(5):
        a = 2 * math.pi * q / 5
        d = along * math.cos(a) + n.cross(along).normalized() * math.sin(a)
        hk.ball(k, p + d * size + n * 0.004, size * 0.75, petal, seg=6, rings=4, squash=(n, 0.5))
    hk.ball(k, p + n * 0.008, size * 0.55, center, seg=6, rings=4, squash=(n, 0.7))


def make_robe():
    """Бабушкин стёганый халат: стёжка ромбами (подушечки и строчка), объёмные цветы, пушистая манжета, бантик."""
    k = hk.Kit("Sleeve_Robe", ARM)
    pink, cream, yellow = C(0.93, 0.6, 0.68), C(0.97, 0.93, 0.85), C(1.0, 0.82, 0.3)
    edge = EDGE + 0.08
    cell_s, cell_t = 0.06, 2 * math.pi / 14
    # подушечки: ткань выпукла в середине ромба и утянута на строчке
    body(k, edge, pink, grow=1.14, rows=16, folds=0.0,
         wave=lambda t: 0.004 * abs(math.sin(t * 7)))
    for d in (-1, 1):                                                                          # строчка ромбами
        for q in range(-6, 8):
            path = []
            for i in range(9):
                s = edge + 0.01 + i * 0.045
                t = T(90) + d * (s - edge) / cell_s * cell_t * 0.5 + q * cell_t
                if not (T(-10) <= t <= T(190)) or s > ARM.back - 0.03:
                    continue
                path.append(ARM.point(s, t, surf(s, edge) + 0.0045 + 0.004 * abs(math.sin(t * 7)), 1.14))   # над волной ткани
            if len(path) >= 3:
                ink(k, path, 0.0045, shade(pink, 0.72), sides=3)
    hk.band(k, EDGE - 0.01, EDGE + 0.1, GAP * 0.5, GAP + 0.04, cream, grow=1.04, rows=2,
            wave=lambda t, r: 0.012 * max(0.0, math.sin(t * 9)) * (r == 1))
    for s, t in ((0.32, 80), (0.42, 120), (0.52, 70), (0.38, 40), (0.6, 105), (0.68, 60), (0.48, 98)):
        with on(k, s, T(t), edge, 1.14, 0.002, folds=0.0) as (p, n, along, across):
            flower(k, p, n, along, WHITE if (s * 100) % 2 < 1 else C(1.0, 0.9, 0.95), yellow, size=0.03)
    p, n, along, across = hk.frame_at(k, EDGE + 0.05, T(95), GAP + 0.045, 1.04)               # бантик на манжете
    red = C(0.85, 0.2, 0.3)
    for x in (-1, 1):
        zw.hull(k, [p + n * h + across * x * w + along * y for h in (0.0, 0.012)
                    for w, y in ((0.006, 0.0), (0.04, -0.018), (0.04, 0.018))], red)
    hk.ball(k, p + n * 0.008, 0.01, shade(red, 0.8), seg=6, rings=4)
    k.roughness = 0.9
    return k


def make_tavern():
    """Трактирщик: закатанный рукав, полотенце через руку, заплатка, кожаный ремешок."""
    k = hk.Kit("Sleeve_Tavern", ARM)
    linen, towel, stripe = C(0.84, 0.77, 0.6), C(0.97, 0.96, 0.92), C(0.72, 0.16, 0.14)
    edge = 0.33
    body(k, edge, lambda r, j: linen if j % 2 else shade(linen, 0.94), grow=1.08, rows=6, folds=0.006)
    hk.roll(k, edge, GAP + 0.006, 0.048, lambda r, j: shade(linen, 0.9) if r % 2 else linen, sides=8)
    hk.strip(k, 0.17, 0.29, T(-20), T(200), GAP + 0.004, 0.014, towel, rows=5,
             top=lambda r, c: stripe if r in (1, 3) else towel)
    for t in (T(-20), T(200)):                                                                 # концы полотенца по бокам
        hk.flap(k, 0.17, 0.29, t, GAP + 0.011, 0.13, towel)
        hk.flap(k, 0.19, 0.205, t, GAP + 0.019, 0.13, stripe, thick=0.004)
        hk.flap(k, 0.255, 0.27, t, GAP + 0.019, 0.13, stripe, thick=0.004)
    with on(k, edge + 0.13, T(96), edge, 1.08, folds=0.006) as (p, n, along, across):          # заплатка
        stitched_patch(k, p, n, along, across, 0.13, 0.12, C(0.55, 0.35, 0.2), C(0.95, 0.85, 0.6))
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
    zw.ARM = ARM
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
        good = tris <= 5000 and inside <= 2                  # 48 граней по кругу (hk.SEG)
        ok &= good
        print("  %-16s треуг. %5d  цветов %2d  вершин в руке %d  %s" % (obj.name, tris, len(kit.colors), inside,
                                                                       "OK" if good else "ПРОВЕРИТЬ"))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        hk.render(render_dir, objs, armature, hand, "sleeves")
    if "--no-export" not in argv:
        for obj in objs:
            hk.export(obj, ARM, MODELS_DIR)
        if not only:
            remove_orphans({obj.name for obj in objs})
    if not ok:
        raise SystemExit("Есть рукава с ошибками")


if __name__ == "__main__":
    main()
