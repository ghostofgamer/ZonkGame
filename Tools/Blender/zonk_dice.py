# -*- coding: utf-8 -*-
"""
Модели особых костей «Зонка» (волна 3, 05.10.2026): у каждой особой кости своя идея — аккуратная форма и накладки
другим материалом (оковка, камни, кайма), а не «неровности». Кость узнаётся с первого взгляда и читается на столе.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_dice.py
    ... -- --render <папка>           превью всех костей (с текстурами видов из Art/Textures/DiceLooks)
    ... -- --only Die_Worn,Die_Bone    только перечисленные

Результат: Assets/ZonkContent/Art/Models/Dice/Die_<Имя>.fbx (повторный запуск перезаписывает).

Две части меша (подмеша): 0 — тело (материал вида кости с точками, DieConfig.LookMaterial), 1 — накладки
(DieConfig.LookAccent: золото, серебро, сталь…). Кость без накладок — одна часть.

Соглашение граней как у обычной кости (zonk_models.py, AGENTS): в Unity 1 = +Y, 6 = -Y, 2 = +Z, 5 = -Z, 3 = +X, 4 = -X;
UV-атлас 3×2 (сверху 1 2 3, снизу 4 5 6). Каждый многоугольник проецируется по своей главной оси в клетку этой грани,
поэтому любые текстуры видов и мастерства (точки на местах) подходят к любой модели. У каждой грани остаётся плоская
часть с нормалью строго по оси — её проверяет ContentValidator.ValidateDieMesh.
Точки занимают середину грани до 0.10 от центра (грань — 0.15): накладки только в полосе у рёбер и на углах.
Размер — как у обычной кости (ребро 0.3), начало координат в центре. Коллайдер в игре — куб: модель только вид.
До ~900 треугольников.
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402  (общие: экспорт, оси граней, атлас)

S = zm.DIE_SIZE
H = S * 0.5
DICE_DIR = os.path.join(zm.MODELS_DIR, "Dice")
LOOKS_DIR = os.path.join(zm.TEXTURES_DIR, "DiceLooks")
UV_MARGIN = 0.012      # отступ от края клетки атласа
MAX_TRIS = 900
PIPS_REACH = 0.101     # точки занимают середину грани до этого расстояния от центра

BODY, ACCENT = 0, 1

# Цвета накладок для превью (в Unity — материалы M_DieAccent_<имя>, генератор ZonkSetup.DiceV3)
ACCENT_COLORS = {
    "Emerald": ((0.05, 0.62, 0.32), 0.15, 0.05),
    "Cobalt": ((0.08, 0.2, 0.7), 0.2, 0.0),
    "Silver": ((0.85, 0.87, 0.9), 0.25, 1.0),
    "Gold": ((1.0, 0.74, 0.25), 0.25, 1.0),
    "Iron": ((0.16, 0.15, 0.15), 0.5, 0.8),
    "Steel": ((0.42, 0.44, 0.47), 0.3, 1.0),
    "Worn": ((1.0, 1.0, 1.0), 0.8, 0.0),         # с текстурой потёртости (build_worn_texture)
}

WORN_TEXTURE = os.path.join(LOOKS_DIR, "DieAccent_Worn.png")


# ---------------------------------------------------------------------------
# Общие операции
# ---------------------------------------------------------------------------

def new_cube(half=H, sx=1.0, sy=1.0, sz=1.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0 * half, matrix=Matrix.Diagonal((sx, sy, sz, 1.0)))
    return bm


def bevel_edges(bm, width, segments, profile=0.5, edges=None):
    bmesh.ops.bevel(bm, geom=list(edges if edges is not None else bm.edges), offset=width, offset_type="OFFSET",
                    segments=segments, profile=profile, affect="EDGES", clamp_overlap=True)


def bevel_corners(bm, width):
    """Срезать углы куба плоскостью: на каждом углу треугольная площадка."""
    bmesh.ops.bevel(bm, geom=list(bm.verts), offset=width, offset_type="OFFSET", segments=1, affect="VERTICES",
                    clamp_overlap=True)


def is_axis(n, tol=0.9999):
    return max(abs(n.x), abs(n.y), abs(n.z)) > tol


def axis_faces(bm, tol=0.9999):
    """Плоские грани, смотрящие строго по осям."""
    return [f for f in bm.faces if is_axis(f.normal, tol)]


def corner_faces(bm):
    """Площадки срезанных углов: нормаль смотрит в угол (все три составляющие заметны)."""
    return [f for f in bm.faces if min(abs(f.normal.x), abs(f.normal.y), abs(f.normal.z)) > 0.4]


def largest_per_corner(faces):
    """Из площадок у каждого угла — одна, самая большая (скругление даёт ещё мелкие)."""
    best = {}
    for f in faces:
        c = f.calc_center_median()
        key = (c.x > 0, c.y > 0, c.z > 0)
        if key not in best or f.calc_area() > best[key].calc_area():
            best[key] = f
    return list(best.values())


def chamfer_faces(bm):
    """Фаски рёбер: нормаль между двумя осями."""
    out = []
    for f in bm.faces:
        a = sorted((abs(f.normal.x), abs(f.normal.y), abs(f.normal.z)))
        if a[0] < 0.2 and a[1] > 0.3:
            out.append(f)
    return out


def set_faces(faces, material, smooth):
    for f in faces:
        f.material_index = material
        f.smooth = smooth


def part(bm, make, material=ACCENT, smooth=False):
    """Добавить деталь: всё, что создаст make(), получает материал и сглаживание."""
    before = set(bm.faces)
    make()
    new = [f for f in bm.faces if f not in before]
    set_faces(new, material, smooth)
    return new


def corners():
    return [Vector((x, y, z)) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]


def edges12():
    """12 рёбер куба: (ось ребра, знаки двух других осей)."""
    out = []
    for axis in range(3):
        for s1 in (-1, 1):
            for s2 in (-1, 1):
                out.append((axis, s1, s2))
    return out


def basis(n):
    """Две оси в плоскости с нормалью n."""
    t1 = n.cross(Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))).normalized()
    t2 = n.cross(t1).normalized()
    return t1, t2


def box(bm, center, size, rotation=None):
    m = Matrix.Translation(center)
    if rotation is not None:
        m = m @ rotation
    m = m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def rod(bm, a, b, radius, segments=6, caps=True):
    """Цилиндр от точки a до точки b."""
    d = b - a
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation((a + b) * 0.5) @ rot
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=segments, radius1=radius, radius2=radius,
                          depth=d.length, matrix=m)


def ball(bm, center, radius, u=8, v=5, axis=None):
    m = Matrix.Translation(center)
    if axis is not None:
        m = m @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=radius, matrix=m)


def stud(bm, center, normal, radius, height, segments=6):
    """Заклёпка-усечённый конус на поверхности: основание в center, вершина по нормали."""
    rot = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation(center + normal * (height * 0.5)) @ rot
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=segments, radius1=radius, radius2=radius * 0.45,
                          depth=height, matrix=m)


def prism(bm, center, normal, ring, height, below=0.002):
    """Пластина по контуру ring (список (u, v) по кругу) на плоскости: верх веером от центра (контур может быть вогнутым)."""
    t1, t2 = basis(normal)
    bottom = [bm.verts.new(center + t1 * u + t2 * v - normal * below) for (u, v) in ring]
    top = [bm.verts.new(center + t1 * u + t2 * v + normal * height) for (u, v) in ring]
    middle = bm.verts.new(center + normal * height)
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        bm.faces.new((top[i], top[j], middle))


def strip(bm, face_axis, face_sign, p0, p1, width, height):
    """Тонкая полоса на грани (ось, знак) от p0 до p1 (координаты в плоскости грани): трещина, кайма."""
    others = [i for i in range(3) if i != face_axis]
    a = Vector((0.0, 0.0, 0.0))
    b = Vector((0.0, 0.0, 0.0))
    a[others[0]], a[others[1]] = p0
    b[others[0]], b[others[1]] = p1
    n = Vector((0.0, 0.0, 0.0))
    n[face_axis] = face_sign
    d = b - a
    length = d.length + width            # с нахлёстом: изломы без щелей
    x = d.normalized()
    y = n.cross(x)
    rot = Matrix((x, y, n)).transposed().to_4x4()
    center = (a + b) * 0.5 + n * (H - height * 0.5 + 0.0012)
    box(bm, center, (length, width, height), rot)


def holes_from_cut(bm, point, normal):
    """Срез плоскостью (угол или ребро): часть снаружи убрать, дыру закрыть; вернуть новые грани."""
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=point, plane_no=normal, clear_outer=True)
    cut_edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if not cut_edges:
        return []
    filled = bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)
    return filled["faces"]


# ---------------------------------------------------------------------------
# Модели
# ---------------------------------------------------------------------------

def die_lucky(rng):
    """Счастливая: золотой талисман — мягко скруглённая кость, на срезанных углах изумруды-кабошоны."""
    cut = 0.075                                    # срез угла: до угловых точек грани остаётся зазор
    bm = new_cube()
    bevel_corners(bm, cut)
    bevel_edges(bm, 0.022, 2)
    set_faces(bm.faces, BODY, True)
    set_faces(axis_faces(bm), BODY, False)
    for c in corners():
        n = c.normalized()
        center = c * (H - cut / 3.0) - n * 0.008
        part(bm, lambda: ball(bm, center, 0.026, u=6, v=4, axis=n), smooth=False)   # гранёный камень
        # золотая оправа камня — тот же металл, что и тело: кольцо-ободок вокруг
        part(bm, lambda: bmesh.ops.create_cone(bm, cap_ends=False, segments=10, radius1=0.029, radius2=0.027,
                                               depth=0.007, matrix=Matrix.Translation(center + n * 0.009)
                                               @ n.to_track_quat("Z", "Y").to_matrix().to_4x4()),
             material=BODY, smooth=True)
    return bm, "Emerald"


def die_even(rng):
    """Чётная (фарфор): гладкая кость с кобальтовой каймой-бусиной по всем рёбрам и жемчужинами на углах."""
    bm = new_cube(H - 0.008)
    bevel_edges(bm, 0.02, 3)
    set_faces(bm.faces, BODY, True)
    r = 0.0105
    e = H - r                                     # бусина касается границы куба
    inner = H - 0.024
    for axis, s1, s2 in edges12():
        others = [i for i in range(3) if i != axis]
        a = Vector((0.0, 0.0, 0.0))
        a[others[0]], a[others[1]] = s1 * e, s2 * e
        b = a.copy()
        a[axis], b[axis] = -inner, inner
        part(bm, lambda: rod(bm, a, b, r, segments=6, caps=False), smooth=True)
    for c in corners():
        part(bm, lambda: ball(bm, c * (H - 0.017), 0.017, u=6, v=4, axis=c.normalized()), smooth=True)
    return bm, "Cobalt"


def die_odd(rng):
    """Нечётная (обсидиан): огранённый камень — острые фаски, на срезанных углах серебряные площадки-оправа."""
    bm = new_cube()
    bevel_corners(bm, 0.085)                      # усечённый куб: углы — крупные серебряные площадки
    bevel_edges(bm, 0.012, 1)
    set_faces(bm.faces, BODY, False)
    caps = corner_faces(bm)
    # площадка угла поднята на 7 мм скосом: серебряная пластина-оправа со скошенными краями
    res = bmesh.ops.inset_individual(bm, faces=caps, thickness=0.007, depth=0.006)
    set_faces(caps + res["faces"], ACCENT, False)
    # на каждой площадке — серебряный трёхгранный шип, грани шипа повторяют треугольник площадки
    for f in largest_per_corner(caps):
        c = f.calc_center_median()
        n = f.normal.normalized()
        rot = n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        part(bm, lambda: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=3, radius1=0.021,
                                               radius2=0.0, depth=0.034,
                                               matrix=Matrix.Translation(c + n * 0.016) @ rot))
    return bm, "Silver"


def die_sixes(rng):
    """Шестёрочная (рубин): камень в золотой оправе — широкие золотые фаски и углы, грани-рубины чуть утоплены."""
    bm = new_cube()
    bevel_corners(bm, 0.05)
    bevel_edges(bm, 0.03, 1)
    set_faces(bm.faces, ACCENT, False)
    flats = axis_faces(bm)
    res = bmesh.ops.inset_individual(bm, faces=flats, thickness=0.006, depth=-0.005)
    set_faces(flats, BODY, False)
    set_faces([f for f in res["faces"] if f not in flats], ACCENT, False)
    # на каждой рубиновой грани — второй, внутренний пояс огранки: грань-«площадка» и скос к ней
    inner = bmesh.ops.inset_individual(bm, faces=flats, thickness=0.006, depth=0.0025)
    set_faces(inner["faces"], BODY, False)
    set_faces(flats, BODY, False)
    return bm, "Gold"


def die_fives(rng):
    """Пятёрочная (нефрит): резная канавка-рамка по граням, на срезанных углах золотые цветы о пяти лепестках."""
    bm = new_cube()
    bevel_corners(bm, 0.07)
    bevel_edges(bm, 0.016, 2)
    set_faces(bm.faces, BODY, True)
    flats = axis_faces(bm)
    set_faces(flats, BODY, False)
    # канавка: отступ, вниз, снова отступ и вверх до плоскости
    r1 = bmesh.ops.inset_individual(bm, faces=flats, thickness=0.008, depth=-0.005)
    r2 = bmesh.ops.inset_individual(bm, faces=flats, thickness=0.005, depth=0.005)
    set_faces(r1["faces"] + r2["faces"], BODY, False)
    for f in largest_per_corner(corner_faces(bm)):
        f.smooth = False
        c = f.calc_center_median()
        n = f.normal.normalized()
        ring = []
        for k in range(15):
            t = 2.0 * math.pi * k / 15
            r = 0.01 + 0.014 * abs(math.cos(2.5 * t))
            ring.append((math.cos(t) * r, math.sin(t) * r))
        part(bm, lambda: prism(bm, c, n, ring, 0.005), smooth=False)
    return bm, "Gold"


def die_middle(rng):
    """Серединка (дерево): бочонок — тёсаная кость с двумя железными обручами у верхнего и нижнего края."""
    t = 0.007
    bm = new_cube(H, sx=(H - t) / H, sy=(H - t) / H)
    bevel_edges(bm, 0.014, 2)
    set_faces(bm.faces, BODY, True)
    set_faces(axis_faces(bm), BODY, False)
    band = 0.022
    for z in (0.118, -0.118):                     # обруч от 0.107 до 0.129: выше и ниже точек
        for axis in (0, 1):
            for s in (-1, 1):
                c = Vector((0.0, 0.0, z))
                c[axis] = s * (H - t * 0.5)
                size = [0.0, 0.0, band]
                size[axis] = t
                size[1 - axis] = 2.0 * H
                part(bm, lambda: box(bm, c, size))
                # заклёпка на обруче посередине стороны
                n = Vector((0.0, 0.0, 0.0))
                n[axis] = s
                part(bm, lambda: stud(bm, c + n * (t * 0.5 - 0.001), n, 0.0055, 0.0035, segments=5))
    return bm, "Iron"


def die_edges(rng):
    """Крайняя (мрамор): постамент — карнизы сверху и снизу (грани 1 и 6) с золотым кантом, середина уже."""
    slab = 0.036
    middle = H - 0.018
    bm = bmesh.new()
    # середина: колонна чуть уже
    box(bm, Vector((0, 0, 0)), (2 * middle, 2 * middle, 2 * (H - slab) + 0.004))
    set_faces(bm.faces, BODY, False)
    for s in (-1, 1):
        before = set(bm.faces)
        box(bm, Vector((0, 0, s * (H - slab * 0.5))), (2 * H, 2 * H, slab))
        new_faces = [f for f in bm.faces if f not in before]
        edges = list({e for f in new_faces for e in f.edges})
        bevel_edges(bm, 0.009, 1, edges=edges)
    set_faces([f for f in bm.faces if not is_axis(f.normal)], ACCENT, False)
    for f in axis_faces(bm):
        f.material_index = BODY
    return bm, "Gold"


def die_worn(rng):
    """Потёртая (слоновая кость): старая кость, на углах сколы — под ними грязная, затёртая кость, от сколов трещины."""
    bm = new_cube()
    bevel_edges(bm, 0.018, 2)
    set_faces(bm.faces, BODY, True)
    set_faces(axis_faces(bm), BODY, False)
    gold = []
    # Сколы — ровные срезы углов (глубина по диагонали): до угловых точек граней остаётся зазор.
    # На грани срез проходит по линии s1·u + s2·v = 2H − depth·√3 — от неё и начинаются трещины.
    chips = ((Vector((1, -1, 1)), 0.05), (Vector((-1, 1, -1)), 0.05), (Vector((-1, -1, 1)), 0.038))
    for corner, depth in chips:
        n = corner.normalized()
        gold += holes_from_cut(bm, corner * H - n * depth, n)
    set_faces([f for f in gold if f.is_valid], ACCENT, False)
    # трещины от сколов по краю граней (мимо точек): (ось грани, знак, точки ломаной в плоскости грани)
    cracks = [
        (2, 1, [(0.12, -0.09), (0.113, -0.045), (0.122, 0.0), (0.114, 0.04)]),       # грань 1 (+Z), от скола +X−Y+Z
        (1, -1, [(0.09, 0.12), (0.05, 0.113), (0.01, 0.122)]),                        # грань 2 (−Y), от того же скола
        (0, 1, [(-0.09, 0.12), (-0.045, 0.112), (-0.005, 0.12)]),                     # грань 4 (+X), от того же скола
        (2, -1, [(-0.12, 0.09), (-0.113, 0.04), (-0.121, -0.01)]),                    # грань 6 (−Z), от скола −X+Y−Z
        (0, -1, [(0.09, -0.12), (0.04, -0.113), (0.0, -0.122)]),                      # грань 3 (−X), от того же скола
    ]
    for axis, sign, points in cracks:
        for p0, p1 in zip(points, points[1:]):
            part(bm, lambda: strip(bm, axis, sign, p0, p1, 0.0065, 0.0024))
    return bm, "Worn"


def die_bone(rng):
    """Костяная: как старинная бабка — гладкое тело, на углах круглые мослы-суставы."""
    body = H - 0.009
    bm = new_cube(body)
    bevel_corners(bm, 0.06)
    bevel_edges(bm, 0.022, 2)
    set_faces(bm.faces, BODY, True)
    set_faces(axis_faces(bm), BODY, False)
    r = 0.034                                      # след мосла на грани не задевает угловые точки
    for c in corners():
        part(bm, lambda: ball(bm, c * (H - r), r, u=8, v=5, axis=c.normalized()), material=BODY, smooth=True)
    return bm, None


def die_sharper(rng):
    """Шулерская (бронза): окована по углам стальными уголками на заклёпках, как сундук с секретом."""
    t = 0.006
    bm = new_cube(H - t)
    bevel_edges(bm, 0.008, 1)
    set_faces(bm.faces, BODY, False)
    span = 0.042                                   # уголок от края грани к центру (до точек остаётся зазор)
    for c in corners():
        for axis in range(3):
            n = Vector((0.0, 0.0, 0.0))
            n[axis] = c[axis]
            center = Vector((0.0, 0.0, 0.0))
            size = [0.0, 0.0, 0.0]
            for i in range(3):
                if i == axis:
                    center[i] = c[i] * (H - t * 0.5)
                    size[i] = t
                else:
                    center[i] = c[i] * (H - span * 0.5)
                    size[i] = span
            part(bm, lambda: box(bm, center, size))
            rivet = center.copy()
            for i in range(3):
                if i != axis:
                    rivet[i] = c[i] * (H - span * 0.45)
            part(bm, lambda: stud(bm, rivet + n * (t * 0.5 - 0.0005), n, 0.0055, 0.0035, segments=5))
    return bm, "Steel"


BUILDERS = {
    "Die_Worn": ("worn", die_worn, "Die_Scratched"),
    "Die_Bone": ("bone", die_bone, "Die_Bone"),
    "Die_Lucky": ("lucky", die_lucky, "Die_Gold"),
    "Die_Sharper": ("sharper", die_sharper, "Die_Bronze"),
    "Die_Edges": ("edges", die_edges, "Die_Marble"),
    "Die_Sixes": ("sixes", die_sixes, "Die_Ruby"),
    "Die_Odd": ("odd", die_odd, "Die_Obsidian"),
    "Die_Even": ("even", die_even, "Die_Porcelain"),
    "Die_Fives": ("fives", die_fives, "Die_Jade"),
    "Die_Middle": ("middle", die_middle, "Die_Wood"),
}


# ---------------------------------------------------------------------------
# UV и нормали
# ---------------------------------------------------------------------------

def finish(name, bm):
    """Нормали, UV по главной оси каждого многоугольника, объект в сцене. Сглаживание — флаг грани (f.smooth)."""
    # Размер как у обычной кости: детали не вылезают за куб 0.3 (иначе кость проваливается в стол — коллайдер куб).
    extent0 = max(max(abs(v.co[i]) for v in bm.verts) for i in range(3))
    if extent0 > H:
        bmesh.ops.scale(bm, vec=Vector((H / extent0,) * 3), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    value_by_axis = {}
    for value, n in zm.DIE_FACE_VALUES.items():
        axis = max(range(3), key=lambda i: abs(n[i]))
        value_by_axis[(axis, int(math.copysign(1, n[axis])))] = value

    # Протяжённость по каждой оси: проекция заполняет клетку целиком (с небольшим отступом)
    extent = [max(abs(v.co[i]) for v in mesh.vertices) for i in range(3)]

    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        key = (axis, 1 if n[axis] > 0 else -1)
        value = value_by_axis[key]
        col, row = zm.die_cell(value)
        ua, va = zm._uv_axes(key)
        ia = max(range(3), key=lambda i: abs(ua[i]))
        iv = max(range(3), key=lambda i: abs(va[i]))
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            cu = co.dot(ua) / max(extent[ia], 1e-4)
            cv = co.dot(va) / max(extent[iv], 1e-4)
            u = (col + 0.5 + max(-1.0, min(1.0, cu)) * (0.5 - UV_MARGIN)) / 3.0
            v = (row + 0.5 + max(-1.0, min(1.0, cv)) * (0.5 - UV_MARGIN)) / 2.0
            uv_layer.data[li].uv = (u, v)

    # Нормали: плоские части строго по оси; остальное — гладко (флаг грани) или гранями.
    # Сглаживание только по неплоским многоугольникам одного материала: иначе у вершины на краю плоской грани нормаль
    # почти по оси, а UV у неё от соседней фаски — валидатор (и текстура) видят чужую клетку.
    smooth_flags = [p.use_smooth for p in mesh.polygons]
    vert_normals = {}
    for poly in mesh.polygons:
        n = poly.normal
        if is_axis(n, 0.9995) or not poly.use_smooth:
            continue
        for vi in poly.vertices:
            key = (vi, poly.material_index)
            vert_normals[key] = vert_normals.get(key, Vector((0, 0, 0))) + n * poly.area
    loop_normals = [None] * len(mesh.loops)
    for poly in mesh.polygons:
        n = poly.normal
        flat = is_axis(n, 0.9995)
        for li in poly.loop_indices:
            if flat:
                axis = max(range(3), key=lambda i: abs(n[i]))
                axis_n = Vector((0, 0, 0))
                axis_n[axis] = 1.0 if n[axis] > 0 else -1.0
                loop_normals[li] = axis_n
            elif smooth_flags[poly.index]:
                sum_n = vert_normals.get((mesh.loops[li].vertex_index, poly.material_index))
                loop_normals[li] = (sum_n if sum_n is not None and sum_n.length > 1e-6 else n).normalized()
            else:
                loop_normals[li] = n.normalized()
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.pi
    mesh.normals_split_custom_set([tuple(x) for x in loop_normals])

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def check(obj):
    """Как ContentValidator.ValidateDieMesh: у каждой грани есть плоская часть по оси, и вся она в своей клетке."""
    mesh = obj.data
    mesh.calc_normals_split()
    uv = mesh.uv_layers.active.data
    expected = {1: (0, 1, 0), 6: (0, -1, 0), 2: (0, 0, 1), 5: (0, 0, -1), 3: (1, 0, 0), 4: (-1, 0, 0)}
    found = {k: False for k in expected}
    ok = True
    for loop in mesh.loops:
        n = loop.normal
        unity = Vector((-n.x, n.z, -n.y))
        for value, axis in expected.items():
            if unity.dot(Vector(axis)) < 0.999:
                continue
            found[value] = True
            u, v = uv[loop.index].uv
            col = min(2, max(0, int(u * 3)))
            row = 0 if v >= 0.5 else 1
            if row * 3 + col + 1 != value:
                ok = False
    missing = [k for k, f in found.items() if not f]
    tris = zm.triangle_count(mesh)
    size = [max(v.co[i] for v in mesh.vertices) - min(v.co[i] for v in mesh.vertices) for i in range(3)]
    parts = len({p.material_index for p in mesh.polygons})
    print("  %-12s треуг. %4d  частей %d  размер %.3f×%.3f×%.3f  клетки %s  грани без плоскости %s" % (
        obj.name, tris, parts, size[0], size[1], size[2], "OK" if ok else "ОШИБКА", missing or "нет"))
    return ok and not missing and tris <= MAX_TRIS and max(size) <= S * 1.001


# ---------------------------------------------------------------------------
# Превью
# ---------------------------------------------------------------------------

def look_material(name, texture):
    path = os.path.join(LOOKS_DIR, texture + ".png")
    image = bpy.data.images.load(path) if os.path.exists(path) else None
    if image is None:
        return None
    mat = zm.make_material(name + "_mat", image)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.45
    return mat


def build_worn_texture(path, size=512):
    """
    Потёртость для сколов «Потёртой» (накладки, DieConfig.LookAccent): старая грязная кость в стиле комикса —
    ровные пятна трёх тонов, светлые затёртые полосы, тёмные выбоины и царапины линиями, без мелкого шума.
    Скол на кости занимает малую часть атласа, поэтому пятна и царапины мелкие: в скол попадает несколько штук.
    """
    import numpy as np
    rng = np.random.default_rng(23)
    big = zm.periodic_noise(rng, size, size, 18)
    mid = zm.periodic_noise(rng, size, size, 7)
    streak = zm.periodic_noise(rng, size, size, 26, scale_v=2.5)

    dark = np.array((0.45, 0.37, 0.28), np.float32)
    middle = np.array((0.6, 0.52, 0.4), np.float32)
    light = np.array((0.69, 0.62, 0.5), np.float32)
    scuff = np.array((0.88, 0.83, 0.71), np.float32)
    grime = np.array((0.17, 0.13, 0.1), np.float32)

    # три ровных тона по крупному шуму (резкие, но сглаженные края — пятна, а не каша)
    t = big * 0.75 + mid * 0.25
    to_mid = zm.smoothstep(-0.95, -0.85, t)[..., None]
    to_light = zm.smoothstep(0.75, 0.85, t)[..., None]
    img = dark * (1 - to_mid) + middle * to_mid
    img = img * (1 - to_light) + light * to_light
    # светлые затёртые полосы (кость стёрлась до блеска)
    s = zm.smoothstep(1.7, 1.85, streak)[..., None]
    img = img * (1 - s) + scuff * s
    # тёмные выбоины с грязью
    pit = zm.smoothstep(2.0, 2.15, mid)[..., None]
    img = img * (1 - pit) + grime * pit

    # царапины: тёмные линии по нескольким направлениям, с переносом через край (текстура бесшовная)
    for _ in range(90):
        x0, y0 = rng.uniform(0, size, 2)
        angle = rng.choice((0.35, 0.5, 2.2, 2.6)) + rng.uniform(-0.15, 0.15)
        length = rng.uniform(10, 46)
        strength = rng.uniform(0.55, 0.9)
        for k in np.linspace(0.0, 1.0, int(length * 1.5)):
            x = int(x0 + math.cos(angle) * length * k) % size
            y = int(y0 + math.sin(angle) * length * k) % size
            img[y, x] = img[y, x] * (1 - strength) + grime * strength
    zm.save_png("DieAccent_Worn", img, path)
    return bpy.data.images.get("DieAccent_Worn")


def accent_material(accent):
    color, roughness, metallic = ACCENT_COLORS[accent]
    mat = bpy.data.materials.get("Accent_" + accent)
    if mat is not None:
        return mat
    if accent == "Worn":
        image = build_worn_texture(WORN_TEXTURE)
        mat = zm.make_material("Accent_Worn", image)
        mat.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = roughness
        return mat
    mat = bpy.data.materials.new("Accent_" + accent)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def render(out_dir, objs):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.35, 0.37, 0.4, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world

    plane = bpy.data.meshes.new("Floor")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20)
    bm.to_mesh(plane)
    bm.free()
    floor = bpy.data.objects.new("Floor", plane)
    felt = bpy.data.materials.new("Felt")
    felt.use_nodes = True
    felt.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (0.05, 0.22, 0.12, 1.0)
    plane.materials.append(felt)
    scene.collection.objects.link(floor)

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 4.0
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(45), math.radians(10), math.radians(35))
    scene.collection.objects.link(sun)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0, -6, 4.2)
    cam.rotation_euler = (math.radians(55), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    # Крупно по пять: грань 1 сверху, повёрнуты на угол — видно две боковые грани
    for part_index in range(2):
        group = objs[part_index * 5:(part_index + 1) * 5]
        for obj in objs:
            obj.hide_render = obj not in group
        sp = 0.5
        st = -sp * (len(group) - 1) / 2.0
        for i, obj in enumerate(group):
            obj.location = (st + i * sp, 0, H)
            obj.rotation_euler = (0, 0, math.radians(35))
        cam_data.ortho_scale = sp * len(group) + 0.1
        scene.render.resolution_x, scene.render.resolution_y = 2000, 520
        scene.render.filepath = os.path.join(out_dir, "dice_close_%d.png" % (part_index + 1))
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)

    # Все десять мельче, как на столе: видно, узнаются ли силуэты
    for obj in objs:
        obj.hide_render = False
    sp = 0.42
    st = -sp * (len(objs) - 1) / 2.0
    for i, obj in enumerate(objs):
        obj.location = (st + i * sp, 0, H)
        obj.rotation_euler = (math.radians(-90 if i % 3 == 1 else 0), 0, math.radians(20 + i * 17))
    cam_data.ortho_scale = sp * len(objs) + 0.2
    scene.render.resolution_x, scene.render.resolution_y = 2000, 300
    scene.render.filepath = os.path.join(out_dir, "dice_row.png")
    bpy.ops.render.render(write_still=True)
    print("Превью: " + scene.render.filepath)


# ---------------------------------------------------------------------------

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None

    zm.reset_scene()
    os.makedirs(DICE_DIR, exist_ok=True)
    objs = []
    ok = True
    print("Особые кости:")
    for name, (key, builder, texture) in BUILDERS.items():
        if only and name not in only:
            continue
        rng = random.Random(sum(ord(c) * (i + 1) for i, c in enumerate(name)))
        bm, accent = builder(rng)
        if accent is None:
            for f in bm.faces:
                f.material_index = BODY
        obj = finish(name, bm)
        ok &= check(obj)
        # Слоты материалов: 0 — вид кости, 1 — накладки (в FBX — две части меша, в Unity два подмеша)
        material = look_material(name, texture) or bpy.data.materials.new(name + "_body")
        obj.data.materials.append(material)
        if any(p.material_index == ACCENT for p in obj.data.polygons):
            obj.data.materials.append(accent_material(accent or "Gold"))
        zm.export_fbx(obj, os.path.join(DICE_DIR, name + ".fbx"))
        objs.append(obj)

    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)

    if not ok:
        raise SystemExit("Есть кости с ошибками (см. выше)")


if __name__ == "__main__":
    main()
