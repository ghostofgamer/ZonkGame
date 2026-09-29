# -*- coding: utf-8 -*-
"""
Модели для игры «Зонк»: игральная кость и два стакана (кожаный и деревянный).

Запуск (фоном, без GUI, чистые настройки Blender):
    blender --background --factory-startup --python Tools/Blender/zonk_models.py

Результат (повторный запуск перезаписывает):
    Assets/ZonkContent/Art/Models/Die.fbx, Cup_Leather.fbx, Cup_Wood.fbx
    Assets/ZonkContent/Art/Textures/Die_Classic.png, Cup_Leather.png, Cup_Wood.png

Оси. FBX пишется с axis_forward='-Z', axis_up='Y', bake_space_transform=True.
После импорта в Unity координаты: Unity = (-bx, bz, -by), где (bx, by, bz) координаты Blender.
Поэтому грани кости в Blender: 1 = +Z, 6 = -Z, 2 = -Y, 5 = +Y, 3 = -X, 4 = +X,
что в Unity даёт 1 = +Y, 6 = -Y, 2 = +Z, 5 = -Z, 3 = +X, 4 = -X.

Проверено в Blender 3.6.
"""

import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Vector

# ---------------------------------------------------------------------------
# Параметры
# ---------------------------------------------------------------------------

# Корень проекта Unity: две папки вверх от этого файла (Tools/Blender/..)
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures")

RANDOM_SEED = 7

# Кость
DIE_SIZE = 0.3               # ребро куба, м
DIE_BEVEL_WIDTH = 0.03       # ширина фаски
DIE_BEVEL_SEGMENTS = 3       # сегменты фаски
DIE_UV_MARGIN = 0.06         # отступ плоской грани от края ячейки атласа (доля ячейки)
DIE_CELL_PX = 256            # размер ячейки атласа, пикс. Атлас 3x2 ячейки
DIE_BODY_COLOR = (0.96, 0.94, 0.88)   # слоновая кость, почти белый: тонируется материалом в Unity
DIE_PIP_COLOR = (0.05, 0.05, 0.06)    # точки
DIE_PIP_RADIUS = 0.085       # радиус точки, доля ячейки
DIE_PIP_OFFSET = 0.24        # смещение крайних точек от центра ячейки, доля ячейки
DIE_PIP_SHADOW = 0.18        # сила тени-обводки вокруг точки (0 = нет)

# Значение грани -> направление нормали в координатах Blender.
# Ячейка атласа: верхняя строка 1,2,3, нижняя 4,5,6 (слева направо).
DIE_FACE_VALUES = {
    1: Vector((0, 0, 1)),
    6: Vector((0, 0, -1)),
    2: Vector((0, -1, 0)),
    5: Vector((0, 1, 0)),
    3: Vector((-1, 0, 0)),
    4: Vector((1, 0, 0)),
}

# Стаканы
CUP_SEGMENTS = 32            # сегменты тела вращения
CUP_HEIGHT = 0.9             # высота до верха обода
CUP_WALL = 0.025             # толщина стенки
CUP_FLOOR = 0.04             # толщина дна (высота внутреннего дна)
CUP_TEXTURE_PX = 512
CUP_SMOOTH_ANGLE = 40.0      # угол auto smooth, градусы: острее этого рёбра жёсткие

# Раскладка UV стакана: боковая полоса (снаружи, обод, внутри) по V от SIDE_V0 до 1,
# внизу два диска: наружное дно и внутреннее дно.
CUP_SIDE_V0 = 0.26
CUP_DISC_RADIUS_UV = 0.115
CUP_DISC_OUTER_CENTER = (0.13, 0.13)
CUP_DISC_INNER_CENTER = (0.38, 0.13)


# ---------------------------------------------------------------------------
# Общее
# ---------------------------------------------------------------------------

def reset_scene():
    """Пустая сцена без объектов и неиспользуемых данных."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.objects):
        for block in list(coll):
            coll.remove(block)


def ensure_dirs():
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)


def save_png(name, rgb, path):
    """Сохраняет массив (H, W, 3) в PNG. Строка 0 массива = низ картинки (V = 0)."""
    h, w, _ = rgb.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb, 0.0, 1.0)
    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def make_material(name, image):
    """Материал с текстурой (для превью; в Unity будут свои материалы)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nodes.active = tex
    return mat


def export_fbx(obj, path, with_children=False):
    """Экспорт одного объекта (и детей) в FBX с осями под Unity."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    if with_children:
        for child in obj.children:
            child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        # 'OFF' пишет только нормали вершин-углов (custom split normals), без групп сглаживания:
        # Unity берёт их как есть.
        mesh_smooth_type="OFF",
        use_mesh_modifiers=True,
        use_tspace=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )


def triangle_count(mesh):
    return sum(len(p.vertices) - 2 for p in mesh.polygons)


# ---------------------------------------------------------------------------
# Периодический шум (для бесшовных по U текстур)
# ---------------------------------------------------------------------------

def periodic_noise(rng, h, w, scale, power=2.0, scale_v=None):
    """Бесшовный шум: белый шум, отфильтрованный в частотной области.
    scale = характерный размер пятна в пикселях по U, scale_v по V (по умолчанию как по U)."""
    white = rng.standard_normal((h, w))
    sv = scale if scale_v is None else scale_v
    fy = np.fft.fftfreq(h)[:, None] * max(sv, 1.0)
    fx = np.fft.fftfreq(w)[None, :] * max(scale, 1.0)
    f = np.sqrt(fx * fx + fy * fy)
    spectrum = np.exp(-f ** power)
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * spectrum))
    out -= out.mean()
    out /= (out.std() + 1e-8)
    return out


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------
# Кость
# ---------------------------------------------------------------------------

PIP_LAYOUTS = {
    # координаты в долях от центра ячейки: -1, 0, +1 (умножаются на DIE_PIP_OFFSET)
    1: [(0, 0)],
    2: [(-1, 1), (1, -1)],
    3: [(-1, 1), (0, 0), (1, -1)],
    4: [(-1, -1), (-1, 1), (1, -1), (1, 1)],
    5: [(-1, -1), (-1, 1), (0, 0), (1, -1), (1, 1)],
    6: [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)],
}


def die_cell(value):
    """Ячейка атласа (столбец, строка снизу) для значения грани."""
    if value <= 3:
        return value - 1, 1
    return value - 4, 0


def build_die_texture(path):
    cell = DIE_CELL_PX
    w, h = cell * 3, cell * 2
    body = np.array(DIE_BODY_COLOR, dtype=np.float32)
    pip = np.array(DIE_PIP_COLOR, dtype=np.float32)
    img = np.empty((h, w, 3), dtype=np.float32)
    img[:] = body

    # координаты центров пикселей внутри ячейки, 0..1
    t = (np.arange(cell) + 0.5) / cell
    cu, cv = np.meshgrid(t, t)  # cu по столбцам (U), cv по строкам (V)
    aa = 1.5 / cell  # ширина сглаживания края, в долях ячейки

    for value, layout in PIP_LAYOUTS.items():
        col, row = die_cell(value)
        # расстояние до ближайшей точки и тень
        dist = np.full((cell, cell), 10.0, dtype=np.float32)
        for (px, py) in layout:
            cx = 0.5 + px * DIE_PIP_OFFSET
            cy = 0.5 + py * DIE_PIP_OFFSET
            dist = np.minimum(dist, np.sqrt((cu - cx) ** 2 + (cv - cy) ** 2))
        # маска точки со сглаженным краем
        mask = 1.0 - smoothstep(DIE_PIP_RADIUS - aa, DIE_PIP_RADIUS + aa, dist)
        # мягкая тень-обводка снаружи точки
        ring = np.exp(-((dist - DIE_PIP_RADIUS) / 0.018) ** 2) * (dist > DIE_PIP_RADIUS) * DIE_PIP_SHADOW
        # внутри точки лёгкий градиент к краю: вдавленность
        inner = 1.0 + 0.6 * smoothstep(0.0, DIE_PIP_RADIUS, dist)
        tile = body[None, None, :] * (1.0 - ring[..., None])
        pip_col = np.clip(pip[None, None, :] * inner[..., None], 0, 1)
        tile = tile * (1.0 - mask[..., None]) + pip_col * mask[..., None]
        img[row * cell:(row + 1) * cell, col * cell:(col + 1) * cell] = tile

    return save_png("Die_Classic", img, path)


# Оси проекции UV для грани: (ось U, ось V), U x V = нормаль (картинка не зеркалится снаружи)
def _uv_axes(normal_axis):
    x, y, z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    table = {
        (2, 1): (x, y),
        (2, -1): (x, -y),
        (0, 1): (y, z),
        (0, -1): (-y, z),
        (1, 1): (-x, z),
        (1, -1): (x, z),
    }
    return table[normal_axis]


def _die_map_coord(c):
    """Координата на грани (-S/2..S/2) -> доля ячейки 0..1.
    Плоская часть (|c| <= S/2 - bevel) линейно в [margin, 1 - margin], фаска сжимается в поле отступа."""
    half = DIE_SIZE * 0.5
    flat = half - DIE_BEVEL_WIDTH
    m = DIE_UV_MARGIN
    a = abs(c)
    if a <= flat:
        r = a / flat * (0.5 - m)
    else:
        # фаска: от края плоской части до края ячейки с небольшим запасом 0.5 %
        r = (0.5 - m) + (a - flat) / (half - flat) * (m - 0.005)
    return 0.5 + math.copysign(r, c)


def build_die(image):
    mesh = bpy.data.meshes.new("Die")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=DIE_SIZE)
    bmesh.ops.bevel(
        bm,
        geom=list(bm.edges) + list(bm.verts),
        offset=DIE_BEVEL_WIDTH,
        offset_type="OFFSET",
        segments=DIE_BEVEL_SEGMENTS,
        profile=0.5,
        affect="EDGES",
        clamp_overlap=True,
    )
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    value_by_axis = {}
    for value, n in DIE_FACE_VALUES.items():
        axis = max(range(3), key=lambda i: abs(n[i]))
        value_by_axis[(axis, int(math.copysign(1, n[axis])))] = value

    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        key = (axis, 1 if n[axis] > 0 else -1)
        value = value_by_axis[key]
        col, row = die_cell(value)
        ua, va = _uv_axes(key)
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            u = (col + _die_map_coord(co.dot(ua))) / 3.0
            v = (row + _die_map_coord(co.dot(va))) / 2.0
            uv_layer.data[li].uv = (u, v)

    # Нормали: у плоских граней строго по оси, на фасках гладкие (средняя по соседним граням с весом площади)
    vert_normals = [Vector((0, 0, 0)) for _ in mesh.vertices]
    for poly in mesh.polygons:
        for vi in poly.vertices:
            vert_normals[vi] += poly.normal * poly.area
    loop_normals = [None] * len(mesh.loops)
    for poly in mesh.polygons:
        n = poly.normal
        is_flat = max(abs(n.x), abs(n.y), abs(n.z)) > 0.9999
        for li in poly.loop_indices:
            if is_flat:
                axis = max(range(3), key=lambda i: abs(n[i]))
                axis_n = Vector((0, 0, 0))
                axis_n[axis] = 1.0 if n[axis] > 0 else -1.0
                loop_normals[li] = axis_n
            else:
                loop_normals[li] = vert_normals[mesh.loops[li].vertex_index].normalized()
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.pi
    mesh.normals_split_custom_set([tuple(n) for n in loop_normals])

    mesh.materials.append(make_material("Die", image))
    obj = bpy.data.objects.new("Die", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def check_die(obj):
    """Для каждой плоской грани: ячейка атласа по UV и направление в Unity по формуле (-bx, bz, -by)."""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    expected_unity = {1: (0, 1, 0), 6: (0, -1, 0), 2: (0, 0, 1), 5: (0, 0, -1), 3: (1, 0, 0), 4: (-1, 0, 0)}
    ok = True
    print("Проверка граней кости (Blender -> Unity):")
    for poly in mesh.polygons:
        n = poly.normal
        if max(abs(n.x), abs(n.y), abs(n.z)) < 0.9999:
            continue
        us = [uv[li].uv[0] for li in poly.loop_indices]
        vs = [uv[li].uv[1] for li in poly.loop_indices]
        col = int(sum(us) / len(us) * 3)
        row = int(sum(vs) / len(vs) * 2)
        value = col + 1 if row == 1 else col + 4
        unity = (round(-n.x), round(n.z), round(-n.y))
        match = unity == expected_unity[value]
        ok &= match
        print("  blender n=(%+d,%+d,%+d)  ячейка col=%d row=%d -> значение %d  unity=(%+d,%+d,%+d)  %s" % (
            round(n.x), round(n.y), round(n.z), col, row, value, unity[0], unity[1], unity[2],
            "OK" if match else "ОШИБКА"))
    print("Грани кости: %s" % ("всё совпадает" if ok else "ЕСТЬ ОШИБКИ"))
    return ok


# ---------------------------------------------------------------------------
# Стаканы
# ---------------------------------------------------------------------------

def _lerp(a, b, t):
    return a + (b - a) * t


def leather_profile():
    """Профиль кожаного стакана (r, z) от центра наружного дна до центра внутреннего дна.
    Возвращает точки и индекс точки, на которой кончается наружная стенка (для строчки)."""
    wall = CUP_WALL
    pts = []
    # наружное дно и кант у дна
    pts += [(0.292, 0.0), (0.306, 0.006), (0.311, 0.022), (0.311, 0.048), (0.303, 0.064)]
    # стенка с лёгкой талией: 0.30 внизу -> 0.34 у горла
    z0, z1 = 0.072, 0.828
    n = 8
    for i in range(n):
        t = i / (n - 1)
        z = _lerp(z0, z1, t)
        r = _lerp(0.300, 0.338, t) - 0.010 * math.sin(math.pi * t)
        pts.append((r, z))
    stitch_index = len(pts) - 2
    # валик-обод
    pts += [(0.346, 0.842), (0.356, 0.858), (0.359, 0.876), (0.352, 0.892), (0.338, 0.9), (0.322, 0.896)]
    # внутренняя стенка вниз
    zi0, zi1 = 0.878, CUP_FLOOR + 0.03
    n = 6
    for i in range(n):
        t = i / (n - 1)
        z = _lerp(zi0, zi1, t)
        r = _lerp(0.338, 0.300, t) - 0.010 * math.sin(math.pi * t) - wall
        pts.append((r, z))
    # скругление к внутреннему дну
    r_last = pts[-1][0]
    pts += [(r_last - 0.008, CUP_FLOOR + 0.008), (r_last - 0.022, CUP_FLOOR)]
    return pts, stitch_index


def wood_profile():
    """Профиль деревянного стакана: почти прямой, три проточенные канавки."""
    wall = CUP_WALL
    r_bot, r_top = 0.305, 0.335
    grooves = [0.12, 0.46, 0.78]
    depth, half_w = 0.007, 0.012

    def r_at(z):
        return _lerp(r_bot, r_top, z / CUP_HEIGHT)

    pts = [(r_bot - 0.012, 0.0), (r_bot - 0.002, 0.004), (r_at(0.018), 0.018)]
    # стенка: опорные точки + канавки (V-образные, 3 точки)
    z_marks = [0.07]
    for g in grooves:
        z_marks += [g - half_w, ("g", g), g + half_w]
    z_marks += [0.62, 0.86]
    z_marks.sort(key=lambda m: m[1] if isinstance(m, tuple) else m)
    for m in z_marks:
        if isinstance(m, tuple):
            z = m[1]
            pts.append((r_at(z) - depth, z))
        else:
            pts.append((r_at(m), m))
    # верх обода, скруглённый
    pts += [(r_top - 0.002, CUP_HEIGHT - 0.006), (r_top - 0.010, CUP_HEIGHT), (r_top - wall + 0.006, CUP_HEIGHT - 0.002)]
    # внутренняя стенка
    for z in (CUP_HEIGHT - 0.012, 0.6, 0.3, CUP_FLOOR + 0.03):
        pts.append((r_at(z) - wall, z))
    r_last = pts[-1][0]
    pts += [(r_last - 0.008, CUP_FLOOR + 0.008), (r_last - 0.022, CUP_FLOOR)]
    return pts, None


def build_cup(name, profile, image, rim_z):
    """Тело вращения по профилю. Центры дна (r = 0) закрываются веерами треугольников."""
    seg = CUP_SEGMENTS
    n = len(profile)

    # длины дуг профиля для V боковой полосы
    lengths = [0.0]
    for i in range(1, n):
        (r0, z0), (r1, z1) = profile[i - 1], profile[i]
        lengths.append(lengths[-1] + math.hypot(r1 - r0, z1 - z0))
    total = lengths[-1]
    side_v = [CUP_SIDE_V0 + (1.0 - CUP_SIDE_V0) * (L / total) for L in lengths]

    verts = []
    for (r, z) in profile:
        for j in range(seg):
            a = 2.0 * math.pi * j / seg
            verts.append((r * math.cos(a), r * math.sin(a), z))
    center_bottom = len(verts)
    verts.append((0.0, 0.0, 0.0))
    center_floor = len(verts)
    verts.append((0.0, 0.0, profile[-1][1]))

    faces, face_uvs = [], []

    def vid(i, j):
        return i * seg + (j % seg)

    # боковая поверхность: нормали наружу у наружной стенки, внутрь стакана у внутренней
    # (порядок обхода задаётся направлением профиля: снизу вверх снаружи, сверху вниз внутри)
    for i in range(n - 1):
        for j in range(seg):
            u0, u1 = j / seg, (j + 1) / seg
            faces.append((vid(i, j), vid(i, j + 1), vid(i + 1, j + 1), vid(i + 1, j)))
            face_uvs.append(((u0, side_v[i]), (u1, side_v[i]), (u1, side_v[i + 1]), (u0, side_v[i + 1])))

    def disc_uv(i, j, center, r_max):
        a = 2.0 * math.pi * j / seg
        r = profile[i][0] / r_max * CUP_DISC_RADIUS_UV
        return (center[0] + r * math.cos(a), center[1] + r * math.sin(a))

    r_disc = max(profile[0][0], profile[-1][0])
    # наружное дно: смотрит вниз
    for j in range(seg):
        faces.append((center_bottom, vid(0, j + 1), vid(0, j)))
        face_uvs.append((CUP_DISC_OUTER_CENTER,
                         disc_uv(0, j + 1, CUP_DISC_OUTER_CENTER, r_disc),
                         disc_uv(0, j, CUP_DISC_OUTER_CENTER, r_disc)))
    # внутреннее дно: смотрит вверх
    last = n - 1
    for j in range(seg):
        faces.append((center_floor, vid(last, j), vid(last, j + 1)))
        face_uvs.append((CUP_DISC_INNER_CENTER,
                         disc_uv(last, j, CUP_DISC_INNER_CENTER, r_disc),
                         disc_uv(last, j + 1, CUP_DISC_INNER_CENTER, r_disc)))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly, uvs in zip(mesh.polygons, face_uvs):
        for li, uv in zip(poly.loop_indices, uvs):
            uv_layer.data[li].uv = uv

    # Проверка направления нормалей: наружная стенка должна смотреть от оси
    mesh.update()
    probe = mesh.polygons[seg * 4]  # полоса на наружной стенке
    radial = Vector((probe.center.x, probe.center.y, 0)).normalized()
    if probe.normal.dot(radial) < 0:
        mesh.flip_normals()

    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.radians(CUP_SMOOTH_ANGLE)
    mesh.validate()
    mesh.update()

    mesh.materials.append(make_material(name, image))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    mouth = bpy.data.objects.new("Mouth", None)
    mouth.empty_display_type = "PLAIN_AXES"
    mouth.empty_display_size = 0.1
    mouth.location = (0.0, 0.0, rim_z)
    mouth.parent = obj
    bpy.context.scene.collection.objects.link(mouth)
    return obj, side_v


def build_leather_texture(path, stitch_v, rim_v0):
    """Кожа: коричневая, крупное и мелкое зерно, строчка под ободом, обод темнее."""
    rng = np.random.default_rng(RANDOM_SEED)
    s = CUP_TEXTURE_PX
    base = np.array((0.36, 0.20, 0.11), dtype=np.float32)
    big = periodic_noise(rng, s, s, 40.0)
    mid = periodic_noise(rng, s, s, 7.0)
    fine = periodic_noise(rng, s, s, 1.6)
    # «пупырчатое» зерно кожи: гребни мелкого шума
    pebble = 1.0 - np.abs(periodic_noise(rng, s, s, 3.0))
    shade = 1.0 + 0.10 * big + 0.05 * mid + 0.04 * fine - 0.07 * smoothstep(0.3, 1.0, pebble)
    img = base[None, None, :] * shade[..., None]

    v = (np.arange(s)[:, None] + 0.5) / s
    u = (np.arange(s)[None, :] + 0.5) / s
    # обод (валик) и внутренняя часть чуть темнее, потёртость по верхнему краю
    rim_mask = smoothstep(rim_v0 - 0.004, rim_v0 + 0.004, v) * np.ones_like(u)
    img *= (1.0 - 0.12 * rim_mask)[..., None]

    # строчка: пунктир светлой нитки вдоль U, бесшовный по кругу (целое число стежков)
    stitches = 96
    phase = (u * stitches) % 1.0
    dash = smoothstep(0.12, 0.2, phase) * (1.0 - smoothstep(0.62, 0.7, phase))
    line_w = 2.2 / s
    for sv in stitch_v:
        band = 1.0 - smoothstep(line_w * 0.5, line_w * 1.2, np.abs(v - sv))
        groove = 1.0 - smoothstep(line_w * 1.2, line_w * 3.0, np.abs(v - sv))
        img *= (1.0 - 0.25 * groove * (1.0 - dash))[..., None]
        thread = np.array((0.86, 0.74, 0.52), dtype=np.float32)
        a = (band * dash)[..., None]
        img = img * (1.0 - a) + thread[None, None, :] * a * (0.9 + 0.1 * fine[..., None])

    return save_png("Cup_Leather", img, path)


def build_wood_texture(path):
    """Дерево: волокна вдоль высоты стакана (V), бесшовно по U."""
    rng = np.random.default_rng(RANDOM_SEED + 1)
    s = CUP_TEXTURE_PX
    u = (np.arange(s)[None, :] + 0.5) / s
    v = (np.arange(s)[:, None] + 0.5) / s
    # искажение волокон: пятна сильно вытянуты вдоль V, поэтому волокна идут вдоль высоты стакана
    warp_big = periodic_noise(rng, s, s, 40.0, scale_v=260.0)
    warp_small = periodic_noise(rng, s, s, 6.0, scale_v=60.0)
    fine = periodic_noise(rng, s, s, 1.0, scale_v=6.0)
    tone = periodic_noise(rng, s, s, 30.0, scale_v=120.0)
    # волокна: полосы по U; число полос целое, поэтому шов по U незаметен
    rings = 36
    x = u * rings + 0.35 * warp_big + 0.06 * warp_small + 0.0 * v
    grain = 0.5 + 0.5 * np.sin(2.0 * math.pi * x)
    grain = grain ** 4  # узкие тёмные прожилки на светлом
    light = np.array((0.64, 0.44, 0.25), dtype=np.float32)
    dark = np.array((0.40, 0.24, 0.12), dtype=np.float32)
    t = np.clip(0.55 * grain + 0.10 * tone + 0.06 * fine + 0.1, 0.0, 1.0)
    img = light[None, None, :] * (1.0 - t[..., None]) + dark[None, None, :] * t[..., None]
    return save_png("Cup_Wood", img, path)


# ---------------------------------------------------------------------------
# Сборка всего
# ---------------------------------------------------------------------------

def main():
    reset_scene()
    ensure_dirs()
    report = []

    # --- кость
    die_tex = build_die_texture(os.path.join(TEXTURES_DIR, "Die_Classic.png"))
    die = build_die(die_tex)
    die_ok = check_die(die)
    export_fbx(die, os.path.join(MODELS_DIR, "Die.fbx"))
    report.append(("Die", triangle_count(die.data)))

    # --- кожаный стакан
    profile, stitch_idx = leather_profile()
    # V строчек: вычисляется так же, как в build_cup
    lengths = [0.0]
    for i in range(1, len(profile)):
        lengths.append(lengths[-1] + math.hypot(profile[i][0] - profile[i - 1][0], profile[i][1] - profile[i - 1][1]))
    side_v = [CUP_SIDE_V0 + (1.0 - CUP_SIDE_V0) * (L / lengths[-1]) for L in lengths]
    # строчка под ободом (между двумя верхними точками стенки) и над кантом у дна
    stitch_v = [0.5 * (side_v[stitch_idx] + side_v[stitch_idx + 1]), 0.5 * (side_v[5] + side_v[6])]
    rim_v0 = side_v[stitch_idx + 2]
    leather_tex = build_leather_texture(os.path.join(TEXTURES_DIR, "Cup_Leather.png"), stitch_v, rim_v0)
    cup_l, _ = build_cup("Cup_Leather", profile, leather_tex, rim_z=CUP_HEIGHT)
    export_fbx(cup_l, os.path.join(MODELS_DIR, "Cup_Leather.fbx"), with_children=True)
    report.append(("Cup_Leather", triangle_count(cup_l.data)))    # убрать кожаный стакан из сцены, чтобы Empty следующего стакана получил имя "Mouth", а не "Mouth.001"
    for ob in list(cup_l.children) + [cup_l]:
        bpy.data.objects.remove(ob, do_unlink=True)

    # --- деревянный стакан
    wood_tex = build_wood_texture(os.path.join(TEXTURES_DIR, "Cup_Wood.png"))
    cup_w, _ = build_cup("Cup_Wood", wood_profile()[0], wood_tex, rim_z=CUP_HEIGHT)
    export_fbx(cup_w, os.path.join(MODELS_DIR, "Cup_Wood.fbx"), with_children=True)
    report.append(("Cup_Wood", triangle_count(cup_w.data)))

    print("Готово:")
    for name, tris in report:
        print("  %-12s треугольников: %d" % (name, tris))
    if not die_ok:
        raise SystemExit("Ориентация граней кости не совпала с таблицей")


if __name__ == "__main__":
    main()
