# -*- coding: utf-8 -*-
"""
Модели особых костей «Зонка»: у каждой особой кости свой силуэт, чтобы её узнавали с первого взгляда.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_dice.py
    ... -- --render <папка>           превью всех костей в ряд (с текстурами видов из Art/Textures/DiceLooks)
    ... -- --only Die_Worn,Die_Bone    только перечисленные

Результат: Assets/ZonkContent/Art/Models/Dice/Die_<Имя>.fbx (повторный запуск перезаписывает).

Соглашение граней как у обычной кости (zonk_models.py, AGENTS): в Unity 1 = +Y, 6 = -Y, 2 = +Z, 5 = -Z, 3 = +X, 4 = -X;
UV-атлас 3×2 (сверху 1 2 3, снизу 4 5 6). Каждый многоугольник проецируется по своей главной оси в клетку этой грани,
поэтому любые текстуры видов и мастерства (точки на местах) подходят к любой модели. У каждой грани остаётся плоская
часть с нормалью строго по оси — её проверяет ContentValidator.ValidateDieMesh.
Размер — как у обычной кости (ребро 0.3), начало координат в центре. Форма коллайдера в игре не меняется: модель —
только внешний вид. До ~600 треугольников.
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
MAX_TRIS = 600


# ---------------------------------------------------------------------------
# Общие операции
# ---------------------------------------------------------------------------

def new_cube():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=S)
    return bm


def bevel(bm, width, segments, profile=0.5):
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=width, offset_type="OFFSET", segments=segments, profile=profile,
                    affect="EDGES", clamp_overlap=True)


def axis_faces(bm, tol=0.9999):
    """Плоские грани, смотрящие строго по осям."""
    return [f for f in bm.faces if max(abs(f.normal.x), abs(f.normal.y), abs(f.normal.z)) > tol]


def inset_extrude(bm, faces, inset, depth):
    """Вдавить (depth < 0) или поднять (depth > 0) середину граней после отступа inset."""
    result = bmesh.ops.inset_individual(bm, faces=faces, thickness=inset, depth=0.0)
    for f in faces:
        n = f.normal.copy()
        for v in f.verts:
            v.co += n * depth
    return result


def cut_corner(bm, corner, depth, tilt, rng):
    """Скол: срезать угол плоскостью (нормаль к углу с наклоном), дыру закрыть."""
    n = Vector(corner).normalized()
    n = (n + Vector((rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt)))).normalized()
    point = Vector(corner) - n * depth
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=point, plane_no=n, clear_outer=True)
    cut_edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    if cut_edges:
        bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)


def add_dome(bm, center, radius, segments=6):
    """Заклёпка-полусфера (для накладок и заклёпок)."""
    res = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=4, radius=radius,
                                    matrix=Matrix.Translation(center))
    return res["verts"]


# ---------------------------------------------------------------------------
# Модели
# ---------------------------------------------------------------------------

def die_worn(rng):
    """Потёртая: старая кость со сколами на углах и рёбрах."""
    bm = new_cube()
    bevel(bm, 0.018, 2)
    corners = [(x, y, z) for x in (-H, H) for y in (-H, H) for z in (-H, H)]
    rng.shuffle(corners)
    for corner in corners[:6]:
        cut_corner(bm, corner, rng.uniform(0.035, 0.06), 0.3, rng)
    # Сколы на середине рёбер: маленькие срезы у ребра
    for _ in range(4):
        axis = rng.randrange(3)
        sx, sy = rng.choice((-1, 1)), rng.choice((-1, 1))
        p = [0.0, 0.0, 0.0]
        others = [i for i in range(3) if i != axis]
        p[others[0]] = sx * H
        p[others[1]] = sy * H
        p[axis] = rng.uniform(-H * 0.6, H * 0.6)
        cut_corner(bm, p, rng.uniform(0.02, 0.032), 0.25, rng)
    return bm, True


def die_bone(rng):
    """Костяная: неровная, оплывшая, края разной толщины."""
    bm = new_cube()
    bevel(bm, 0.055, 3, profile=0.6)
    flat = set(v for f in axis_faces(bm) for v in f.verts)
    for v in bm.verts:
        if v in flat:
            continue
        # неровность только на фасках: плоские части остаются плоскими, края оплывшие, как у старой кости
        k = 1.0 + rng.uniform(-0.09, 0.05)
        v.co *= k
    return bm, True


def die_lucky(rng):
    """Счастливая: круглые углы и золотые заклёпки на каждом углу."""
    bm = new_cube()
    bevel(bm, 0.045, 3)
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                d = Vector((x, y, z)).normalized()
                add_dome(bm, Vector((x, y, z)) * (H - 0.03) + d * 0.012, 0.022)
    return bm, True


def die_sharper(rng):
    """Шулерская: чуть сужена кверху (утяжелённый низ) и окована по нижнему краю."""
    bm = new_cube()
    bevel(bm, 0.02, 2)
    for v in bm.verts:
        t = (v.co.z + H) / S        # 0 внизу, 1 вверху (Blender Z = Unity Y = грань 1)
        k = 1.0 - 0.11 * t
        v.co.x *= k
        v.co.y *= k
    # Оковка: заклёпки по нижнему поясу
    for i in range(8):
        a = i * math.pi / 4.0 + math.pi / 8.0
        r = H * 1.0
        x, y = math.cos(a) * r, math.sin(a) * r
        m = max(abs(x), abs(y))
        p = Vector((x / m * (H - 0.019), y / m * (H - 0.019), -H + 0.04))
        add_dome(bm, p, 0.02, segments=6)
    return bm, True


def die_edges(rng):
    """Гранёная: широкие прямые фаски по рёбрам — восьмиугольный профиль, как огранка мрамора."""
    bm = new_cube()
    bevel(bm, 0.05, 1)
    return bm, False


def die_sixes(rng):
    """Самоцвет: срезаны и рёбра, и углы — кость как ограненный камень."""
    bm = new_cube()
    bmesh.ops.bevel(bm, geom=list(bm.verts), offset=0.07, offset_type="OFFSET", segments=1, affect="VERTICES",
                    clamp_overlap=True)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.018, offset_type="OFFSET", segments=1, affect="EDGES",
                    clamp_overlap=True)
    return bm, False


def die_odd(rng):
    """Обсидиан: острые рёбра, на каждой грани поднятая площадка со скошенными краями — осколок."""
    bm = new_cube()
    faces = axis_faces(bm)
    inset_extrude(bm, faces, 0.04, 0.022)
    return bm, False


def die_even(rng):
    """Фарфоровая: каждая грань в рамке, середина утоплена."""
    bm = new_cube()
    bevel(bm, 0.016, 2)
    faces = axis_faces(bm)
    inset_extrude(bm, faces, 0.026, -0.016)
    return bm, True


def die_fives(rng):
    """Резной нефрит: по каждой грани прорезана канавка — двойная рамка."""
    bm = new_cube()
    bevel(bm, 0.022, 2)
    faces = axis_faces(bm)
    inset_extrude(bm, faces, 0.018, -0.008)
    faces = axis_faces(bm)
    # вторая ступень внутри: середина снова поднимается к исходной плоскости — канавка между ними
    inner = [f for f in faces if f.calc_area() < (S - 0.1) ** 2]
    inset_extrude(bm, inner, 0.008, 0.008)
    return bm, True


def die_middle(rng):
    """Деревянная: старая точёная кость, мягко выпуклые грани, скруглённые края."""
    bm = new_cube()
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=3, use_grid_fill=True)
    # выпуклость: точки на гранях отодвигаются к сфере, центр грани остаётся на месте и смотрит по оси
    for v in bm.verts:
        co = v.co.copy()
        cube_len = max(abs(co.x), abs(co.y), abs(co.z))
        sphere = co.normalized() * cube_len * 1.0
        v.co = co.lerp(sphere, 0.32)
        # края подрезать, чтобы кость не была шаром
    bmesh.ops.scale(bm, vec=Vector((1.0, 1.0, 1.0)), verts=bm.verts)
    return bm, True


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

def finish(name, bm, smooth):
    """Нормали, UV по главной оси каждого многоугольника, объект в сцене."""
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

    # Нормали: плоские части строго по оси; остальное — гладко (smooth) или гранями (огранка)
    # Сглаживание только по неплоским многоугольникам: иначе у вершины на краю плоской грани нормаль почти по оси,
    # а UV у неё от соседней фаски — валидатор (и текстура) видят чужую клетку.
    vert_normals = [Vector((0, 0, 0)) for _ in mesh.vertices]
    for poly in mesh.polygons:
        n = poly.normal
        if max(abs(n.x), abs(n.y), abs(n.z)) > 0.9995:
            continue
        for vi in poly.vertices:
            vert_normals[vi] += n * poly.area
    loop_normals = [None] * len(mesh.loops)
    for poly in mesh.polygons:
        n = poly.normal
        is_flat = max(abs(n.x), abs(n.y), abs(n.z)) > 0.9995
        for li in poly.loop_indices:
            if is_flat:
                axis = max(range(3), key=lambda i: abs(n[i]))
                axis_n = Vector((0, 0, 0))
                axis_n[axis] = 1.0 if n[axis] > 0 else -1.0
                loop_normals[li] = axis_n
            elif smooth:
                loop_normals[li] = (vert_normals[mesh.loops[li].vertex_index] if vert_normals[mesh.loops[li].vertex_index].length > 1e-6 else n).normalized()
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
    print("  %-12s треуг. %4d  размер %.3f×%.3f×%.3f  клетки %s  грани без плоскости %s" % (
        obj.name, tris, size[0], size[1], size[2], "OK" if ok else "ОШИБКА", missing or "нет"))
    return ok and not missing and tris <= MAX_TRIS and max(size) <= S * 1.06


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


def render(out_dir, objs):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 2400, 600
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("World")
    world.color = (0.18, 0.2, 0.22)
    scene.world = world

    spacing = 0.45
    start = -spacing * (len(objs) - 1) / 2.0
    for i, obj in enumerate(objs):
        obj.location = (start + i * spacing, 0, H)
        obj.rotation_euler = (math.radians(-18), math.radians(12), math.radians(32))

    plane = bpy.data.meshes.new("Floor")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20)
    bm.to_mesh(plane)
    bm.free()
    floor = bpy.data.objects.new("Floor", plane)
    scene.collection.objects.link(floor)

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 4.5
    fill_data = bpy.data.lights.new("Fill", type="SUN")
    fill_data.energy = 1.2
    fill = bpy.data.objects.new("Fill", fill_data)
    fill.rotation_euler = (math.radians(60), math.radians(-30), math.radians(-140))
    scene.collection.objects.link(fill)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(30))
    scene.collection.objects.link(sun)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = spacing * len(objs) + 0.3
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0, -6, 2.6)
    cam.rotation_euler = (math.radians(68), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    scene.render.filepath = os.path.join(out_dir, "dice_side.png")
    bpy.ops.render.render(write_still=True)
    print("Превью: " + scene.render.filepath)

    # Крупно по пять, грань 1 сверху
    for part in range(2):
        group = objs[part * 5:(part + 1) * 5]
        for obj in objs:
            obj.hide_render = obj not in group
        sp = 0.5
        st = -sp * (len(group) - 1) / 2.0
        for i, obj in enumerate(group):
            obj.location = (st + i * sp, 0, H)
            obj.rotation_euler = (math.radians(-25), math.radians(20), math.radians(40 + i * 7))
        cam_data.ortho_scale = sp * len(group) + 0.2
        scene.render.resolution_x, scene.render.resolution_y = 2000, 560
        scene.render.filepath = os.path.join(out_dir, "dice_close_%d.png" % (part + 1))
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
        bm, smooth = builder(rng)
        obj = finish(name, bm, smooth)
        ok &= check(obj)
        material = look_material(name, texture)
        if material is not None:
            obj.data.materials.append(material)
        zm.export_fbx(obj, os.path.join(DICE_DIR, name + ".fbx"))
        objs.append(obj)

    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)

    if not ok:
        raise SystemExit("Есть кости с ошибками (см. выше)")


if __name__ == "__main__":
    main()
