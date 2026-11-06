# -*- coding: utf-8 -*-
"""
Запястье руки игрока для «Зонка» (слот «Запястье», 15 вещей; первая — «ничего», без модели): часы и браслеты
в зоне запястья zonk_handkit (s 0…0.15, не выше WRIST_MAX_ADD над кожей) — их не задевает ни один рукав.

Вторая версия (06.11.2026, отзыв автора «дёшево и плохо»): вещи крупные (камера партии далеко), лежат вплотную
к граням руки (Arm.hug, без зазора эллипса) и собраны из деталей: ушки и безель с насечкой у часов, звенья браслета,
огранка камней, витая верёвка, фаска у костей. Блик комикса — светлый штрих на стекле и камнях.

    Wrist_Watch      часы дайвера: стальной корпус, безель с насечкой, тёмно-синий циферблат, кожаный ремешок со строчкой
    Wrist_RedThread  витой красный шнурок с золотой монетой-клевером на удачу
    Wrist_WoodBeads  чётки: крупные резные бусины, нефритовая бусина и кисточка
    Wrist_DiceBeads  браслет из костей с фаской (слоновая кость и казино-красные) и золотые проставки
    Wrist_Studs      широкий кожаный напульсник: ряды стальных конусов и шипы посередине
    Wrist_Chips      стопки фишек казино ребром вокруг руки и золотая фишка сверху
    Wrist_Smart      смарт-часы: на экране кость, оранжевый силиконовый ремешок
    Wrist_Anchor     толстый витой канат с золотым якорем
    Wrist_GoldWatch  золотые часы: браслет из звеньев, рифлёный безель, камни вместо меток
    Wrist_Chain      толстая золотая цепь и подвеска-кость
    Wrist_Gems       золотой браслет с огранёнными камнями в оправах
    Wrist_Compass    латунный компас на двойном кожаном ремне (Сыч)
    Wrist_Horseshoe  кованый браслет с заклёпками и подковой (Тихон)
    Wrist_Skull      обсидиановые бусы, костяной череп с рубиновыми глазами и скрещённые кости (Крот)

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

import bmesh

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402
import zonk_handkit as hk  # noqa: E402
from zonk_handkit import C, shade  # noqa: E402

MODELS_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Wrist")
TEXTURES_DIR = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Wrist")

W = 0.068            # центр запястья: корпус часов (радиус до 0.085) не заходит на манжету рукава (край от 0.16)
TOP = math.pi / 2    # тыльная сторона: циферблаты и подвески смотрят в камеру
SEGW = 48            # граней по кругу у браслетов
MAX_TRIS = 3000

GOLD = C(1.0, 0.76, 0.25)
GOLD_LIGHT = C(1.0, 0.9, 0.55)
GOLD_DARK = C(0.72, 0.46, 0.1)
SILVER = C(0.82, 0.84, 0.88)
STEEL = C(0.62, 0.64, 0.68)
STEEL_DARK = C(0.36, 0.38, 0.42)
LEATHER = C(0.45, 0.24, 0.11)
BLACK = C(0.1, 0.1, 0.11)
WHITE = C(0.97, 0.96, 0.92)
RED = C(0.85, 0.12, 0.12)
GLINT = C(1.0, 1.0, 1.0)

ARM = None


def T(deg):
    return math.radians(deg)


# ---------------------------------------------------------------------------
# Оси и формы
# ---------------------------------------------------------------------------

def frame(s, t, add):
    """Точка вплотную к коже и её оси: (точка, наружу, вдоль руки к локтю, по кругу)."""
    p = ARM.hug(s, t, add)
    d = 0.01
    tangent = (ARM.hug(s, t + d, add) - ARM.hug(s, t - d, add)).normalized()
    n = tangent.cross(ARM.axis).normalized()
    if n.dot(p - ARM.centre(s)) < 0:
        n = -n
    along = (ARM.axis - n * ARM.axis.dot(n)).normalized()
    across = n.cross(along).normalized()
    return p, n, along, across


def ring(k, s, add, seg=SEGW):
    return [k.vert(ARM.hug(s, hk.angle(j, seg), add)) for j in range(seg)]


def sweep(k, s, prof, color, seg=SEGW):
    """Кольцо вокруг запястья с сечением prof — замкнутый контур [(смещение вдоль руки, высота над кожей)]; color(ri, j)."""
    rings = [ring(k, s + ds, add, seg) for ds, add in prof]
    k.quads(rings + [rings[0]], color)


def band_prof(width, add, thick, bevel):
    """Сечение ремешка с фаской: 0 — низ, 1 и 5 — бока, 2 и 4 — фаски, 3 — верх."""
    w = width / 2
    return [(-w, add), (w, add), (w, add + thick - bevel), (w - bevel, add + thick), (-w + bevel, add + thick),
            (-w, add + thick - bevel)]


def band_colors(top, bevel=None, side=None):
    bevel = bevel or shade(top, 1.25)
    side = side or shade(top, 0.7)
    return lambda ri, j: top if ri == 3 else bevel if ri in (2, 4) else side


def lathe(k, p, n, e1, e2, prof, color, seg=16, phase=0.0):
    """Тело вращения вокруг оси n от точки p: prof — [(радиус, высота)] снизу вверх, радиус 0 — полюс; color(i, q)."""
    rings = []
    for r, h in prof:
        if r <= 1e-6:
            rings.append(k.vert(p + n * h))
        else:
            rings.append([k.vert(p + n * h + (e1 * math.cos(phase + 2 * math.pi * q / seg) +
                                              e2 * math.sin(phase + 2 * math.pi * q / seg)) * r) for q in range(seg)])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for q in range(seg):
            col = color(i, q) if callable(color) else color
            q1 = (q + 1) % seg
            if isinstance(a, list) and isinstance(b, list):
                k.face((a[q], a[q1], b[q1], b[q]), col)
            elif isinstance(a, list):
                k.face((a[q], a[q1], b), col)
            elif isinstance(b, list):
                k.face((a, b[q1], b[q]), col)


def hull(k, pts, color):
    """Выпуклая оболочка точек: color(нормаль грани) или цвет. Гранёные камни, кости с фаской, корпуса."""
    verts = [k.vert(p) for p in pts]
    res = bmesh.ops.convex_hull(k.bm, input=verts)
    junk = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)})
    if junk:
        bmesh.ops.delete(k.bm, geom=junk, context="VERTS")
    centre = sum(pts, pts[0] * 0) / len(pts)
    for f in res["geom"]:
        if isinstance(f, bmesh.types.BMFace) and f.is_valid:
            f.normal_update()
            nrm = f.normal if f.normal.dot(f.calc_center_median() - centre) >= 0 else -f.normal
            uv = k.cell(color(nrm) if callable(color) else color)
            for loop in f.loops:
                loop[k.uv].uv = uv
            f.smooth = False
            if hasattr(k, "skin_used"):
                f.material_index = 1                                     # кисть на скелете: палитра, не кожа


def chamfer_box(k, c, ex, ey, ez, hx, hy, hz, bevel, color, bevel_color):
    """Брусок с фаской (кость, звено, корпус): оси ex, ey, ez, полуразмеры; грани по осям — color, фаски — bevel_color."""
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                corner = c + ex * sx * hx + ey * sy * hy + ez * sz * hz
                pts += [corner - ex * sx * min(bevel, hx * 0.8), corner - ey * sy * min(bevel, hy * 0.8),
                        corner - ez * sz * min(bevel, hz * 0.8)]
    axes = (ex, ey, ez)
    hull(k, pts, lambda nrm: color if max(abs(nrm.dot(a)) for a in axes) > 0.97 else bevel_color)


def disc(k, p, n, e1, radius, height, color, seg=8, top=None):
    """Плоская шайба: точка кости, метка, кнопка."""
    e2 = n.cross(e1).normalized()
    lathe(k, p, n, e1, e2, [(radius, 0), (radius, height), (0, height)],
          lambda i, q: (top or color) if i == 1 else color, seg=seg)


def glint(k, p, n, e1, length, width=0.006):
    """Блик комикса: светлый косой штрих."""
    e2 = n.cross(e1).normalized()
    d = (e1 + e2 * 0.6).normalized()
    hk.slab(k, p - d * length / 2, d, d.cross(n).normalized(), n, length, width, 0.0015, GLINT)


def loop_tube(k, path, radius, color, sides=6):
    """Замкнутая трубка по точкам (шнур вокруг руки); color(q, i) — по длине и по сечению."""
    rings = []
    m = len(path)
    for q, p in enumerate(path):
        d = (path[(q + 1) % m] - path[q - 1]).normalized()
        u = d.cross(ARM.axis)
        u = u.normalized() if u.length > 1e-6 else d.orthogonal().normalized()
        v = d.cross(u).normalized()
        rings.append([k.vert(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * radius)
                      for i in range(sides)])
    for q in range(m):
        a, b = rings[q], rings[(q + 1) % m]
        for i in range(sides):
            j = (i + 1) % sides
            k.face((a[i], a[j], b[j], b[i]), color(q, i) if callable(color) else color)


def twisted_rope(k, s, add, radius, color, groove, seg=80, sides=9, lobes=3, turns=8):
    """Витая верёвка вокруг руки: сечение из lobes прядей проворачивается turns раз; бороздки между прядями темнее."""
    rings = []
    for q in range(seg):
        t = hk.angle(q, seg)
        c, n, along, _ = frame(s, t, add + radius)
        twist = 2 * math.pi * turns * q / seg
        verts = []
        for i in range(sides):
            phi = 2 * math.pi * i / sides
            r = radius * (0.78 + 0.22 * abs(math.cos(lobes * (phi + twist) / 2)))
            verts.append(k.vert(c + (along * math.cos(phi) + n * math.sin(phi)) * r))
        rings.append(verts)
    for q in range(seg):
        a, b = rings[q], rings[(q + 1) % seg]
        twist = 2 * math.pi * turns * q / seg
        for i in range(sides):
            j = (i + 1) % sides
            phi = 2 * math.pi * (i + 0.5) / sides
            dark = abs(math.cos(lobes * (phi + twist) / 2)) < 0.3
            k.face((a[i], a[j], b[j], b[i]), groove if dark else color)


def bead(k, c, axis, radius, color, seg=10, rings=6, stripes=None):
    """Бусина с отверстием по оси axis (вдоль шнура); stripes(i) — цвет пояска i (резьба), иначе color."""
    e1 = axis.orthogonal().normalized()
    e2 = axis.cross(e1).normalized()
    prof = [(0, -radius)]
    for i in range(1, rings):
        phi = math.pi * i / rings
        prof.append((math.sin(phi) * radius, -math.cos(phi) * radius))
    prof.append((0, radius))
    lathe(k, c, axis, e1, e2, prof, lambda i, q: stripes(i) if stripes else color, seg=seg)


def gem_cut(k, p, n, e1, radius, color, seg=8, height=None):
    """Огранённый камень площадкой наружу: павильон в оправу, коронка гранями двух тонов, площадка светлее, блик."""
    height = height or radius * 0.75
    e2 = n.cross(e1).normalized()
    prof = [(0, -height * 0.5), (radius, 0), (radius, height * 0.18), (radius * 0.6, height * 0.6), (0, height * 0.6)]
    lathe(k, p, n, e1, e2, prof,
          lambda i, q: shade(color, 0.6) if i == 0 else shade(color, 0.8) if i == 1 else
          (color if q % 2 else shade(color, 1.35)) if i == 2 else shade(color, 1.5), seg=seg)
    glint(k, p + n * height * 0.61 + e1 * radius * 0.15 - e2 * radius * 0.2, n, e1, radius * 0.7, radius * 0.18)


def bezel_setting(k, p, n, e1, radius, height, color, seg=12):
    """Оправа камня: ободок с бортиком."""
    e2 = n.cross(e1).normalized()
    lathe(k, p, n, e1, e2, [(0, 0), (radius, 0), (radius * 1.08, height * 0.6), (radius, height), (radius * 0.84, height),
                            (radius * 0.84, height * 0.5), (0, height * 0.5)],
          lambda i, q: shade(color, 1.25) if i == 3 else color if i != 1 else shade(color, 0.8), seg=seg)


def stitches(k, s, add, color, t0=T(-30), t1=T(210), step=2, seg=SEGW):
    """Строчка по ремешку: короткие штрихи на верхней стороне."""
    for j in range(0, seg, step):
        t = hk.angle(j, seg)
        if not (t0 <= t <= t1):
            continue
        p, n, along, across = frame(s, t, add)
        hk.slab(k, p - across * 0.007, across, along, n, 0.014, 0.0045, 0.002, color)


def pips(k, c, n, e1, size, value, color):
    """Точки грани кости value на квадрате size с центром c и нормалью n."""
    e2 = n.cross(e1).normalized()
    layout = {1: [(0, 0)], 2: [(-1, -1), (1, 1)], 3: [(-1, -1), (0, 0), (1, 1)], 4: [(-1, -1), (1, 1), (-1, 1), (1, -1)],
              5: [(-1, -1), (1, 1), (-1, 1), (1, -1), (0, 0)], 6: [(-1, -1), (1, 1), (-1, 1), (1, -1), (-1, 0), (1, 0)]}
    off = size * 0.27
    r = size * (0.13 if value > 1 else 0.17)
    for x, y in layout[value]:
        disc(k, c + e1 * x * off + e2 * y * off - n * 0.001, n, e1, r, 0.0025, color, seg=5)


def die(k, c, n, along, across, size, body, bevel_col, pip, faces, pip_one=None):
    """Кость с фаской: faces — [(направление, значение)] для видимых граней (n наружу, along к локтю, -along к пальцам)."""
    h = size / 2
    chamfer_box(k, c, across, along, n, h, h, h, size * 0.16, body, bevel_col)
    for d, value in faces:
        e1 = along if abs(d.dot(along)) < 0.5 else across
        pips(k, c + d * h, d, e1, size * 0.8, value, pip_one if (pip_one and value == 1) else pip)


def watch_hands(k, top, n, along, across, length, colors, widths, angles_deg):
    """Стрелки: углы от 12 часов (к пальцам) по часовой, если смотреть на циферблат."""
    for L, col, w, a in zip(length, colors, widths, angles_deg):
        a = math.radians(a)
        d = (-along * math.cos(a) + across * math.sin(a)).normalized()
        hk.slab(k, top - d * L * 0.15, d, d.cross(n).normalized(), n, L * 1.15, w, 0.003, col)
        top = top + n * 0.003


# ---------------------------------------------------------------------------
# Часы
# ---------------------------------------------------------------------------

def lugs(k, p, n, along, across, radius, h, color):
    """Ушки корпуса: от края корпуса к ремешку, по два с каждой стороны."""
    for e in (-1, 1):
        for x in (-1, 1):
            o = p + along * e * radius * 0.8 + across * x * radius * 0.48
            hk.slab(k, o - n * 0.004, along * e, across, n, radius * 0.3, radius * 0.16, h, color)


def crown(k, p, n, across, color, dark, radius=0.012, length=0.018):
    e1 = n
    e2 = across.cross(n).normalized()
    lathe(k, p, across, e1, e2, [(0, 0), (radius, 0), (radius, length), (radius * 0.7, length * 1.15), (0, length * 1.15)],
          lambda i, q: (color if q % 2 else dark) if i == 1 else color, seg=10)


def make_watch():
    k = hk.Kit("Wrist_Watch", ARM)
    leather = LEATHER
    sweep(k, W, band_prof(0.1, 0.002, 0.02, 0.007), band_colors(leather))
    for e in (-1, 1):
        stitches(k, W + e * 0.037, 0.0225, C(0.95, 0.82, 0.55))
    R = 0.082
    p, n, along, across = frame(W, TOP, 0.022)
    lugs(k, p, n, along, across, R, 0.02, STEEL)
    # корпус: низ, стенка, ступень под безель
    lathe(k, p, n, along, across, [(0, 0), (R * 0.9, 0), (R, 0.01), (R, 0.026), (R * 0.9, 0.028), (0, 0.028)],
          lambda i, q: STEEL_DARK if i == 0 else STEEL if i == 1 else SILVER, seg=24)
    # безель с насечкой и чёрной вставкой, на 12 — светлый треугольник
    lathe(k, p + n * 0.026, n, along, across,
          [(R * 0.98, 0), (R * 1.02, 0.004), (R * 1.02, 0.014), (R * 0.95, 0.019), (R * 0.76, 0.019), (R * 0.76, 0.012)],
          lambda i, q: (SILVER if q % 2 else STEEL_DARK) if i in (0, 1) else
          (C(0.98, 0.78, 0.2) if q in (11, 12) else BLACK) if i == 3 else STEEL,
          seg=24, phase=-math.pi / 2 + math.pi / 24)
    dial_r = R * 0.76
    top = p + n * 0.038
    disc(k, top - n * 0.004, n, along, dial_r, 0.004, C(0.07, 0.13, 0.32), seg=24)
    for q in range(12):                                             # метки, на 12 — двойная
        a = q * math.pi / 6
        d = -along * math.cos(a) + across * math.sin(a)
        size = 0.014 if q % 3 == 0 else 0.009
        hk.slab(k, top + d * (dial_r * 0.62), d, d.cross(n).normalized(), n, size + 0.006, size * 0.8, 0.0025, WHITE)
    watch_hands(k, top + n * 0.002, n, along, across, [0.032, 0.05, 0.055],
                [WHITE, WHITE, C(1.0, 0.45, 0.1)], [0.011, 0.008, 0.0035], [-60, 60, 160])
    disc(k, top + n * 0.011, n, along, 0.008, 0.003, C(1.0, 0.45, 0.1))
    glint(k, top + n * 0.014 - along * 0.03 + across * 0.02, n, along, 0.03, 0.006)
    crown(k, p + n * 0.014 + across * R * 0.97, n, across, SILVER, STEEL_DARK)
    k.roughness = 0.4
    return k


def make_gold_watch():
    k = hk.Kit("Wrist_GoldWatch", ARM)
    w = 0.05
    prof = [(-w, 0.002), (w, 0.002), (w, 0.015), (w - 0.006, 0.021), (0.019, 0.021), (0.016, 0.026), (-0.016, 0.026),
            (-0.019, 0.021), (-w + 0.006, 0.021), (-w, 0.015)]

    def links(ri, j):
        if j % 3 == 0 and ri in (3, 4, 7, 8):
            return GOLD_DARK                                     # швы между звеньями
        return GOLD_LIGHT if ri in (5, 6) else GOLD if ri in (3, 4, 7, 8, 2, 9) else GOLD_DARK

    sweep(k, W, prof, links, seg=60)
    R = 0.085
    p, n, along, across = frame(W, TOP, 0.022)
    lugs(k, p, n, along, across, R, 0.02, GOLD)
    lathe(k, p, n, along, across, [(0, 0), (R * 0.9, 0), (R, 0.012), (R, 0.026), (0, 0.026)],
          lambda i, q: GOLD_DARK if i == 0 else GOLD, seg=24)
    lathe(k, p + n * 0.026, n, along, across,
          [(R, 0), (R * 1.03, 0.006), (R * 0.94, 0.017), (R * 0.78, 0.017), (R * 0.78, 0.01)],
          lambda i, q: (GOLD_LIGHT if q % 2 else GOLD_DARK) if i in (0, 1) else GOLD, seg=32)
    dial_r = R * 0.78
    top = p + n * 0.036
    disc(k, top - n * 0.004, n, along, dial_r, 0.004, C(0.06, 0.32, 0.22), seg=24)
    for q in range(12):
        a = q * math.pi / 6
        d = -along * math.cos(a) + across * math.sin(a)
        if q % 3 == 0:
            hk.slab(k, top + d * (dial_r * 0.58), d, d.cross(n).normalized(), n, 0.02, 0.01, 0.003, GOLD_LIGHT)
        else:
            gem_cut(k, top + d * (dial_r * 0.7) + n * 0.002, n, d, 0.0075, WHITE, seg=6)
    watch_hands(k, top + n * 0.003, n, along, across, [0.03, 0.048], [GOLD_LIGHT, GOLD_LIGHT], [0.01, 0.007], [-50, 70])
    disc(k, top + n * 0.009, n, along, 0.007, 0.003, GOLD)
    glint(k, top + n * 0.012 - along * 0.03 + across * 0.022, n, along, 0.03, 0.006)
    crown(k, p + n * 0.014 + across * R * 0.97, n, across, GOLD, GOLD_DARK)
    gem_cut(k, p + n * 0.014 + across * (R + 0.02), across, n, 0.007, C(0.2, 0.45, 1.0), seg=6)
    k.metallic, k.roughness = 0.6, 0.3
    return k


def make_smart():
    k = hk.Kit("Wrist_Smart", ARM)
    strap = C(1.0, 0.46, 0.12)
    sweep(k, W, band_prof(0.088, 0.002, 0.018, 0.007), band_colors(strap))
    for q in range(5):                                                 # дырочки ремешка у ладони
        p, n, along, across = frame(W, T(35 - q * 9), 0.0195)
        disc(k, p - n * 0.001, n, along, 0.0075, 0.002, shade(strap, 0.45), seg=6)
    p, n, along, across = frame(W, TOP, 0.022)
    body, hw, hl, h, r = C(0.16, 0.17, 0.2), 0.072, 0.078, 0.03, 0.022
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            cc = p + across * sx * (hw - r) + along * sy * (hl - r)
            for q in range(5):
                a = math.atan2(sy, sx) - math.pi / 4 + math.pi / 2 * q / 4
                off = across * math.cos(a) + along * math.sin(a)
                pts += [cc + off * r - n * 0.004, cc + off * r + n * (h - 0.007), cc + off * (r - 0.006) + n * h]
    hull(k, pts, lambda nrm: shade(body, 1.35) if nrm.dot(n) > 0.95 else body)
    screen = p + n * (h + 0.0005)
    hk.slab(k, screen - along * 0.06, along, across, n, 0.12, 0.112, 0.002, C(0.03, 0.05, 0.09))
    # на экране — кость с пятёркой и полоска шагов
    face = screen + n * 0.002 - along * 0.008
    chamfer_box(k, face + n * 0.002, across, along, n, 0.032, 0.032, 0.0025, 0.008, WHITE, shade(WHITE, 0.8))
    pips(k, face + n * 0.0045, n, along, 0.064, 5, RED)
    for q in range(4):
        hk.slab(k, screen + n * 0.002 + along * 0.034 + across * (q - 1.5) * 0.024, along, across, n, 0.012, 0.016, 0.002,
                C(0.25, 0.95, 0.6) if q < 3 else C(0.2, 0.3, 0.3))
    glint(k, screen + n * 0.006 - along * 0.035 + across * 0.035, n, along, 0.028, 0.005)
    crown(k, p + n * 0.016 + across * hw, n, across, shade(body, 1.6), body, radius=0.009, length=0.012)
    k.roughness = 0.35
    return k


# ---------------------------------------------------------------------------
# Браслеты
# ---------------------------------------------------------------------------

def make_red_thread():
    k = hk.Kit("Wrist_RedThread", ARM)
    twisted_rope(k, W, 0.003, 0.015, RED, shade(RED, 0.55), seg=64, sides=8, lobes=2, turns=9)
    twisted_rope(k, W + 0.043, 0.003, 0.009, C(1.0, 0.8, 0.3), GOLD_DARK, seg=56, lobes=2, turns=9, sides=6)
    # монета на удачу с клевером
    p, n, along, across = frame(W + 0.012, TOP, 0.026)
    lathe(k, p, n, along, across, [(0, 0), (0.05, 0), (0.054, 0.006), (0.05, 0.012), (0.038, 0.012), (0.038, 0.009),
                                   (0, 0.009)],
          lambda i, q: GOLD_DARK if i == 0 else (GOLD if q % 2 else GOLD_LIGHT) if i in (1, 2) else
          GOLD_LIGHT if i == 3 else GOLD, seg=20)
    green = C(0.2, 0.7, 0.3)
    for q in range(4):
        a = q * math.pi / 2 + math.pi / 4
        d = along * math.cos(a) + across * math.sin(a)
        e = d.cross(n).normalized()
        c = p + n * 0.01 + d * 0.014
        for x in (-1, 1):
            bead(k, c + e * x * 0.006, n, 0.009, green, seg=8, rings=4)
    hk.slab(k, p + n * 0.012 + along * 0.002, along, across, n, 0.022, 0.005, 0.003, shade(green, 0.7))   # стебелёк
    glint(k, p + n * 0.016 - along * 0.022 + across * 0.022, n, along, 0.016, 0.005)
    # колечко подвеса
    hk.link(k, p - along * 0.058 + n * 0.006, along, n, 0.009, 0.004, GOLD, stretch=1.0, seg=8, sides=4)
    k.roughness = 0.6
    return k


def make_wood_beads():
    k = hk.Kit("Wrist_WoodBeads", ARM)
    n_beads, r = 17, 0.03
    woods = [C(0.55, 0.32, 0.14), C(0.75, 0.5, 0.26)]
    jade = C(0.25, 0.68, 0.48)
    loop_tube(k, [ARM.hug(W, hk.angle(q, 48), r) for q in range(48)], 0.004, C(0.5, 0.15, 0.1))
    for q in range(n_beads):
        t = hk.angle(0) + 2 * math.pi * (q + 0.5) / n_beads
        p, n, along, across = frame(W, t, r + 0.004)
        big = abs(t - TOP) < math.pi / n_beads
        if big:
            p2, n2, _, _ = frame(W, t, r * 1.3)
            bead(k, p2, across, r * 1.3, jade, seg=12, rings=7,
                 stripes=lambda i: shade(jade, 1.3) if i in (2, 3) else jade)
            continue
        wood = woods[q % 2]
        bead(k, p, across, r, wood, stripes=lambda i, w=wood: shade(w, 0.6) if i == 3 else shade(w, 1.15) if i == 2 else w)
    # кисточка у большого пальца: узел-бусина и пучок нитей вниз, вдоль ладонной стороны
    t = T(-5)
    p, n, along, across = frame(W, t, r)
    tassel = C(0.85, 0.15, 0.12)
    bead(k, p + n * 0.012, n, 0.016, GOLD, seg=8, rings=4)
    base = p + n * 0.03
    down = (-ARM.up + n * 0.5).normalized()
    tip = base + down * 0.08 + n * 0.03
    pts = [base + (along * math.cos(a) + n * math.sin(a)) * 0.012 for a in (q * math.pi / 3 for q in range(6))]
    pts += [tip + (along * math.cos(a) + n * math.sin(a)) * 0.02 for a in (q * math.pi / 3 for q in range(6))]
    hull(k, pts, lambda nrm: tassel if nrm.dot(n) > -0.2 else shade(tassel, 0.7))
    k.roughness = 0.7
    return k


def make_dice_beads():
    k = hk.Kit("Wrist_DiceBeads", ARM)
    size, n_dice = 0.054, 12
    loop_tube(k, [ARM.hug(W, hk.angle(q, 48), size / 2) for q in range(48)], 0.004, BLACK)
    casino = C(0.82, 0.1, 0.12)
    for q in range(n_dice):
        t = hk.angle(0) + 2 * math.pi * (q + 0.5) / n_dice
        p, n, along, across = frame(W, t, size / 2 + 0.008)
        red = q % 2 == 1
        body = casino if red else WHITE
        pip = WHITE if red else BLACK
        top_half = math.sin(t) > 0.2
        value = (q * 5) % 6 + 1
        faces = [(n, value)]
        if top_half:
            side = 7 - ((q * 2) % 6 + 1) if (q * 2) % 6 + 1 not in (value, 7 - value) else (2 if value not in (2, 5) else 3)
            faces += [(-along, side), (along, 7 - side)]
        die(k, p, n, along, across, size, body, shade(body, 0.78), pip, faces, pip_one=None if red else RED)
        # золотая проставка между костями
        t2 = t + math.pi / n_dice
        p2, n2, along2, across2 = frame(W, t2, size / 2)
        lathe(k, p2 - across2 * 0.006, across2, n2, along2, [(0, 0), (0.013, 0), (0.016, 0.006), (0.013, 0.012), (0, 0.012)],
              lambda i, q: GOLD if i != 1 else GOLD_LIGHT, seg=6)
    k.roughness = 0.35
    return k


def make_studs():
    k = hk.Kit("Wrist_Studs", ARM)
    leather = C(0.13, 0.12, 0.13)
    sweep(k, W, band_prof(0.13, 0.002, 0.024, 0.008), band_colors(leather, shade(leather, 2.0), shade(leather, 0.8)))
    for e in (-1, 1):                                                     # окантовка
        sweep(k, W + e * 0.058, [(-0.006, 0.02), (0.006, 0.02), (0.006, 0.03), (-0.006, 0.03)],
              lambda ri, j: C(0.55, 0.12, 0.12) if ri == 2 else C(0.4, 0.08, 0.08))
    cone = [(0, 0), (0.014, 0), (0.014, 0.006), (0.005, 0.024), (0, 0.027)]
    spike = [(0, 0), (0.018, 0), (0.018, 0.006), (0.004, 0.046), (0, 0.048)]
    for row in (-1, 1):
        for j in range(0, 17):
            t = T(-20) + T(220) * j / 16
            p, n, along, across = frame(W + row * 0.032, t, 0.024)
            lathe(k, p, n, along, across, cone, lambda i, q: SILVER if (i == 2 and q % 2) else WHITE if i == 3 else STEEL,
                  seg=6)
    for j in range(0, 9):
        t = T(-5) + T(190) * j / 8
        p, n, along, across = frame(W, t, 0.024)
        lathe(k, p, n, along, across, spike, lambda i, q: SILVER if q % 2 else STEEL if i < 3 else WHITE, seg=6)
    k.metallic, k.roughness = 0.3, 0.45
    return k


def make_chips():
    k = hk.Kit("Wrist_Chips", ARM)
    colors = [RED, C(0.15, 0.35, 0.8), C(0.1, 0.55, 0.3), BLACK, C(0.55, 0.2, 0.7)]
    loop_tube(k, [ARM.hug(W, hk.angle(q, 48), 0.012) for q in range(48)], 0.006, BLACK)
    chip_r, thick = 0.035, 0.017
    arc_from, arc_to, gap = T(-30), T(210), T(17)
    t = arc_from
    q = 0
    while t < arc_to:
        if abs(t - TOP) < gap:
            t += T(2)
            continue
        p, n, along, across = frame(W, t, chip_r + 0.008)
        col = colors[(q // 3) % len(colors)]
        lathe(k, p - across * thick / 2, across, n, along, [(0, 0), (chip_r, 0), (chip_r, thick), (0, thick)],
              lambda i, qq, col=col: (WHITE if qq % 3 == 0 else col) if i == 1 else col, seg=9, phase=q * 0.4)
        ds = ARM.hug(W, t + 0.01, chip_r) - ARM.hug(W, t, chip_r)
        t += 0.01 * (thick * 1.04) / ds.length
        q += 1
    # большая золотая фишка плашмя сверху
    p, n, along, across = frame(W, TOP, 0.035)
    R = 0.062
    lathe(k, p, n, along, across, [(0, 0), (R, 0), (R, 0.016), (R * 0.78, 0.016), (R * 0.78, 0.019), (0, 0.019)],
          lambda i, qq: (WHITE if qq % 4 < 2 else GOLD) if i in (1, 2) else GOLD_LIGHT if i == 3 else GOLD, seg=24)
    hk.link(k, p + n * 0.0185, along, across, R * 0.62, 0.003, WHITE, stretch=1.0, seg=20, sides=4)
    pips(k, p + n * 0.021, n, along, 0.05, 6, C(0.75, 0.1, 0.1))
    glint(k, p + n * 0.022 - along * 0.03 + across * 0.03, n, along, 0.022, 0.005)
    k.roughness = 0.45
    return k


def make_anchor():
    k = hk.Kit("Wrist_Anchor", ARM)
    rope = C(0.92, 0.8, 0.55)
    twisted_rope(k, W, 0.006, 0.024, rope, shade(rope, 0.8), seg=96, turns=7)
    # якорь лёжа сверху, лапы к пальцам
    p, n, along, across = frame(W, TOP, 0.052)
    along, across = across, along                                                                     # поперёк руки: там шире
    gold, dark = GOLD, GOLD_DARK
    m = 1.3                                                                                           # крупнее: камера далеко
    th = 0.016
    hk.slab(k, p - along * 0.065 * m - n * th / 2, along, across, n, 0.12 * m, 0.018 * m, th, gold)   # веретено
    chamfer_box(k, p + along * 0.032 * m, across, along, n, 0.042 * m, 0.008 * m, th / 2, 0.004, gold, dark)  # шток
    for e in (-1, 1):
        bead(k, p + along * 0.032 * m + across * e * 0.046 * m, across, 0.011, gold, seg=8, rings=4)
    hk.link(k, p + along * 0.068 * m, along, n, 0.014 * m, 0.006, gold, stretch=1.0, seg=10, sides=5)  # рым
    arc = []                                                                                          # рога дугой
    for q in range(11):
        a = math.pi * q / 10
        arc.append(p - along * 0.03 * m + (-along * math.sin(a) * 0.035 + across * math.cos(a) * 0.05) * m)
    hk.tube(k, arc, 0.0095 * m, gold, sides=6)
    for e in (-1, 1):                                                                                 # лапы-треугольники
        tip = arc[0] if e > 0 else arc[-1]
        d = across * e
        pts = [tip + along * 0.004, tip + (along * 0.034 - d * 0.004) * m, tip + (-d * 0.022 + along * 0.008) * m,
               tip + d * 0.01 * m]
        pts = [q + n * h for q in pts for h in (-th / 2, th / 2)]
        hull(k, pts, lambda nrm: shade(gold, 1.2) if nrm.dot(n) > 0.7 else dark)
    glint(k, p + n * th - along * 0.04 + across * 0.004, n, along, 0.036, 0.006)
    k.roughness = 0.6
    return k


def make_chain():
    k = hk.Kit("Wrist_Chain", ARM)
    n_links = 26
    for q in range(n_links):
        t = hk.angle(0) + 2 * math.pi * q / n_links
        p, n, along, across = frame(W, t, 0.036)
        tilt = math.radians(50 if q % 2 else -50)
        e2 = (along * math.cos(tilt) + n * math.sin(tilt)).normalized()
        hk.link(k, p, across, e2, 0.024, 0.0125, GOLD if q % 2 else GOLD_LIGHT, stretch=1.3, seg=8, sides=5)
    # подвеска-кость
    p, n, along, across = frame(W + 0.004, TOP, 0.05)
    hk.link(k, p - along * 0.04 - n * 0.012, along, n, 0.01, 0.004, GOLD, stretch=1.0, seg=8, sides=4)
    die(k, p, n, along, across, 0.05, GOLD, GOLD_DARK, BLACK, [(n, 6), (-along, 1), (along, 6)])
    glint(k, p + n * 0.026 - along * 0.012 + across * 0.012, n, along, 0.016, 0.004)
    k.metallic, k.roughness = 0.7, 0.3
    return k


def make_gems():
    k = hk.Kit("Wrist_Gems", ARM)
    sweep(k, W, band_prof(0.12, 0.002, 0.02, 0.006), band_colors(GOLD, GOLD_LIGHT, GOLD_DARK))
    for e in (-1, 1):
        sweep(k, W + e * 0.054, [(-0.007, 0.018), (0.007, 0.018), (0.007, 0.028), (0, 0.032), (-0.007, 0.028)],
              lambda ri, j: GOLD_LIGHT if ri in (2, 3) else GOLD_DARK)
    ruby, sapphire, emerald = C(0.9, 0.08, 0.2), C(0.15, 0.35, 0.95), C(0.08, 0.75, 0.35)
    stones = [(0, 0.032, ruby), (22, 0.022, sapphire), (-22, 0.022, sapphire), (42, 0.017, emerald), (-42, 0.017, emerald),
              (60, 0.013, ruby), (-60, 0.013, ruby)]
    for deg, r, col in stones:
        p, n, along, across = frame(W, TOP + T(deg), 0.021)
        bezel_setting(k, p, n, along, r * 1.18, 0.014, GOLD)
        gem_cut(k, p + n * 0.012, n, along, r, col, seg=8)
    k.metallic, k.roughness = 0.4, 0.25
    return k


def make_compass():
    k = hk.Kit("Wrist_Compass", ARM)
    for e in (-1, 1):
        sweep(k, W + e * 0.034, band_prof(0.042, 0.002, 0.016, 0.005), band_colors(LEATHER))
        stitches(k, W + e * 0.034, 0.0175, C(0.95, 0.82, 0.55), step=3)
    pad = C(0.62, 0.4, 0.2)
    p, n, along, across = frame(W, TOP, 0.016)
    chamfer_box(k, p + n * 0.004, across, along, n, 0.075, 0.08, 0.006, 0.004, pad, shade(pad, 0.75))
    brass, face, ink = C(0.85, 0.63, 0.25), C(0.96, 0.92, 0.78), C(0.2, 0.22, 0.28)
    R = 0.078
    p = p + n * 0.01
    lathe(k, p, n, along, across, [(0, 0), (R, 0), (R * 1.04, 0.012), (R, 0.026), (R * 0.85, 0.028), (R * 0.85, 0.02),
                                   (0, 0.02)],
          lambda i, q: shade(brass, 0.7) if i == 0 else (brass if q % 2 else shade(brass, 1.2)) if i == 1 else
          shade(brass, 1.25) if i in (2, 3) else brass, seg=24)
    top = p + n * 0.02
    disc(k, top, n, along, R * 0.84, 0.002, face, seg=24)
    top = top + n * 0.002
    for q in range(16):                                                 # деления
        a = q * math.pi / 8
        d = -along * math.cos(a) + across * math.sin(a)
        hk.slab(k, top + d * R * 0.66, d, d.cross(n).normalized(), n, 0.012 if q % 4 == 0 else 0.007, 0.004, 0.0015, ink)
    for q in range(8):                                                  # роза ветров
        a = q * math.pi / 4
        d = -along * math.cos(a) + across * math.sin(a)
        e = d.cross(n).normalized()
        L = 0.05 if q % 2 == 0 else 0.03
        col = RED if q == 0 else ink if q % 2 == 0 else C(0.55, 0.55, 0.6)
        pts = [top + d * L, top + e * 0.008, top - e * 0.008, top + n * 0.004 + d * 0.004]
        hull(k, pts, col)
    lathe(k, top, n, along, across, [(0, 0), (0.008, 0), (0.008, 0.006), (0, 0.007)], brass, seg=8)
    glint(k, top + n * 0.008 - along * 0.035 + across * 0.03, n, along, 0.026, 0.005)
    # ушко сверху, как у карманного компаса (к пальцам)
    lathe(k, p + n * 0.012 - along * (R + 0.006), -along, n, across, [(0, 0), (0.009, 0), (0.009, 0.01), (0, 0.01)],
          brass, seg=8)
    hk.link(k, p + n * 0.012 - along * (R + 0.026), along, n, 0.012, 0.004, brass, stretch=1.0, seg=10, sides=4)
    k.roughness = 0.4
    return k


def make_horseshoe():
    k = hk.Kit("Wrist_Horseshoe", ARM)
    iron, dark, light = C(0.3, 0.31, 0.34), C(0.18, 0.18, 0.2), C(0.46, 0.47, 0.5)
    rivet = C(0.8, 0.6, 0.3)
    sweep(k, W, band_prof(0.11, 0.002, 0.02, 0.007),
          lambda ri, j: light if ri in (2, 4) else (iron if (j * 7) % 5 else shade(iron, 0.85)) if ri == 3 else dark)
    for e in (-1, 1):                                                    # заклёпки по краям
        for j in range(0, SEGW, 4):
            t = hk.angle(j, SEGW)
            if not (T(-40) <= t <= T(220)):
                continue
            p, n, along, across = frame(W + e * 0.043, t, 0.021)
            lathe(k, p, n, along, across, [(0, 0), (0.008, 0), (0.007, 0.004), (0.004, 0.007), (0, 0.008)],
                  lambda i, q: shade(rivet, 1.25) if i >= 2 else rivet, seg=6)
    # подкова: дуга из брусков, концами к локтю, с бороздкой и дырками для гвоздей
    p, n, along, across = frame(W, TOP, 0.022)
    shoe, shoe_dark = C(0.9, 0.91, 0.94), C(0.55, 0.57, 0.62)
    r = 0.06
    pts = []
    for q in range(15):
        a = -T(20) + T(220) * q / 14
        pts.append(p + across * math.cos(a) * r - along * math.sin(a) * r)
    for q in range(len(pts) - 1):
        a0, a1 = pts[q], pts[q + 1]
        d = (a1 - a0).normalized()
        out = d.cross(n).normalized()
        if out.dot(a0 - p) < 0:
            out = -out
        quad = [a0 - out * 0.015, a0 + out * 0.015, a1 - out * 0.015, a1 + out * 0.015]
        hull(k, [v + n * h for v in quad for h in (0.0, 0.024)],
             lambda nrm: (shoe if nrm.dot(n) > 0.9 else shoe_dark))
        if q in (2, 4, 6, 8, 10, 12):
            c = (a0 + a1) / 2 + n * 0.0245
            hk.slab(k, c - d * 0.004, d, out, n, 0.008, 0.007, 0.0015, BLACK)
        if q % 2 == 1:
            c = (a0 + a1) / 2 + n * 0.0245
            hk.slab(k, c - d * 0.006, d, out, n, 0.012, 0.0035, 0.0015, shoe_dark)
    glint(k, p + n * 0.026 - along * 0.05 + across * 0.025, n, along, 0.028, 0.006)
    k.metallic, k.roughness = 0.5, 0.5
    return k


def make_skull():
    k = hk.Kit("Wrist_Skull", ARM)
    obsidian, bone = C(0.14, 0.11, 0.16), C(0.94, 0.9, 0.78)
    n_beads, r = 13, 0.036
    loop_tube(k, [ARM.hug(W, hk.angle(q, 48), r) for q in range(48)], 0.004, BLACK)
    for q in range(n_beads):
        t = hk.angle(0) + 2 * math.pi * (q + 0.5) / n_beads
        if abs(t - TOP) < 0.42:
            continue
        p, n, along, across = frame(W, t, r + 0.004)
        bead(k, p, across, r, obsidian, seg=8, rings=5, stripes=lambda i: C(0.45, 0.35, 0.55) if i == 1 else obsidian)
        t2 = t + math.pi / n_beads
        if abs(t2 - TOP) < 0.42:
            continue
        p2, n2, along2, across2 = frame(W, t2, r * 0.8)
        lathe(k, p2 - across2 * 0.004, across2, n2, along2, [(0, 0), (0.012, 0), (0.014, 0.004), (0.012, 0.008), (0, 0.008)],
              SILVER, seg=8)
    # скрещённые кости под черепом
    p, n, along, across = frame(W, TOP, 0.022)
    for e in (-1, 1):
        d = (along + across * e * 1.1).normalized()
        a, b = p - d * 0.06 + n * 0.008, p + d * 0.06 + n * 0.008
        hk.tube(k, [a, b], 0.008, shade(bone, 0.9), sides=6)
        for end in (a, b):
            e2 = d.cross(n).normalized()
            for x in (-1, 1):
                bead(k, end + e2 * x * 0.008, d, 0.009, bone, seg=6, rings=4)
    # череп: свод, скулы и челюсть оболочкой, глазницы с рубинами, нос, зубы
    c = p + n * 0.042
    pts = []
    for q in range(10):
        a = 2 * math.pi * q / 10
        for h, rr in ((0.0, 0.034), (0.016, 0.042), (0.03, 0.036), (0.04, 0.02)):
            pts.append(c - along * 0.012 + n * (h - 0.012) + (across * math.cos(a) + along * math.sin(a) * 1.1) * rr)
    hull(k, pts, lambda nrm: bone if nrm.dot(n) > 0.2 else shade(bone, 0.82))
    jaw = [c + along * 0.026 + across * x * 0.026 + n * h for x in (-1, 1) for h in (-0.03, 0.0)]
    jaw += [c + along * 0.048 + across * x * 0.018 + n * h for x in (-1, 1) for h in (-0.026, -0.004)]
    hull(k, jaw, lambda nrm: shade(bone, 0.95))
    face = c + n * 0.022
    for x in (-1, 1):
        e = c + n * 0.02 + across * x * 0.017 + along * 0.004
        disc(k, e, n, along, 0.012, 0.004, BLACK, seg=8)
        gem_cut(k, e + n * 0.004, n, along, 0.0065, C(1.0, 0.1, 0.12), seg=6)
    hull(k, [face + along * 0.016 + n * 0.0, face + along * 0.026 + across * 0.006, face + along * 0.026 - across * 0.006,
             face + along * 0.021 + n * 0.002], BLACK)
    for x in range(-2, 3):                                                # зубы
        tooth = c + along * 0.044 + across * x * 0.0075 + n * 0.0
        hk.slab(k, tooth - along * 0.004, along, across, n, 0.008, 0.006, 0.004, WHITE)
    glint(k, c + n * 0.03 - along * 0.024 + across * 0.014, n, along, 0.018, 0.005)
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
    no_export = "--no-export" in argv

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
        inside = hk.inside_count(obj, ARM, hugged=True)
        height = max(((v.co - ARM.palm).dot(ARM.axis) for v in obj.data.vertices), default=0)
        good = tris <= MAX_TRIS and inside <= 2 and height <= hk.WRIST_TO + 0.012
        ok &= good
        print("  %-16s треуг. %5d  цветов %2d  в руке %d  до s=%.3f  %s" % (obj.name, tris, len(kit.colors), inside, height,
                                                                         "OK" if good else "ПРОВЕРИТЬ"))
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        hk.render(render_dir, objs, armature, hand, "wrist")
    if not no_export:
        for obj in objs:
            hk.export(obj, ARM, MODELS_DIR)
        if not only:
            remove_orphans({obj.name for obj in objs})
    if not ok:
        raise SystemExit("Есть вещи с ошибками")


if __name__ == "__main__":
    main()
