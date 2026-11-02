# -*- coding: utf-8 -*-
"""
Рука игрока для «Зонка» из модели автора (Tools/Blender/Source/Arms.fbx: две руки от плеча, T-поза, скелет с пальцами).

Что делает скрипт:
    1. Берёт правую руку, левую и лишние кости (Root, плечо, IK) удаляет.
    2. Режет предплечье: от запястья остаётся FOREARM_KEEP его длины — место под часы, браслет, платок.
       Срез закрыт тёмной заглушкой (второй материал); снаружи его закрывает рукав (zonk_sleeves.py, слот «Рукав»).
    3. Чинит веса: ладонь за костяшками принадлежит кости Palm (в исходнике первая фаланга указательного
       тянула весь передний ряд ладони).
    4. Ставит руку ладонью вниз, пальцами вперёд, начало координат — центр ладони; масштаб SCALE под стакан 0.9.
    5. Добавляет якорь "Wrist" (на кости Forearm) — место для часов и браслетов.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_arms.py
    blender --background --factory-startup --python Tools/Blender/zonk_arms.py -- --render <папка>
--render ещё рендерит превью: рука открытая, обхват стаканов двух радиусов и кулак — тем же расчётом хвата, что в игре
(HandView.SolveGrip): каждая фаланга поворачивается так, чтобы её конец лёг на окружность стакана.

Результат: Assets/ZonkContent/Art/Models/Hands/Hand.fbx.
Оси: FBX без bake_space_transform (скелет), Unity сам переводит оси (bakeAxisConversion в генераторе);
Unity = (-bx, bz, -by). В Unity рука: пальцы +Z, ладонь −Y, большой палец −X (правая рука), центр ладони в нуле.
Кости: Forearm (корень) → Palm → Index1..3, Middle1..3, Ring1..3, Pinky1..3, Thumb1..3.
Материалы по порядку: Hand_Skin (кожа), Hand_Cuff (заглушка среза под рукавом).

Проверено в Blender 3.6.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCE = os.path.join(PROJECT_ROOT, "Tools", "Blender", "Source", "Arms.fbx")
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Hands")

SCALE = 4.0              # ладонь ~0.42 в ширину, кисть ~0.72: стакан 0.9 при радиусе 0.33–0.45
FOREARM_KEEP = 0.4       # доля предплечья от запястья, которая остаётся
SLEEVE_GAP = 0.004       # зазор рукава над кожей (в исходном масштабе): zonk_sleeves.py

# Превью хвата: те же числа, что в HandConfig по умолчанию.
GRIP_PALM_OFFSET = 0.034 * SCALE  # низ ладони под её центром
GRIP_PAD_FORWARD = 0.0465 * SCALE  # подушка ладони у костяшек, где её касается стакан
GRIP_TILT = 35.0          # наклон кисти, градусы
GRIP_FINGER_PAD = 0.018 * SCALE  # половина толщины пальца
GRIP_MAX_BEND = 95.0
GRIP_MAX_OPEN = 15.0
GRIP_THUMB_BEND = 30.0
GRIP_THUMB_OPEN = 45.0
FINGERS = ("Index", "Middle", "Ring", "Pinky", "Thumb")
SKIN_COLOR = (0.93, 0.72, 0.58, 1.0)
CUFF_COLOR = (0.12, 0.1, 0.1, 1.0)   # заглушка среза, изнутри рукава


def new_name(old):
    """Forearm.Right → Forearm, Index1.R → Index1."""
    for suffix in (".Right", ".R"):
        if old.endswith(suffix):
            return old[: -len(suffix)]
    return None


def is_right(name):
    return name.endswith(".Right") or name.endswith(".R")


def load_source():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE)
    armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    bpy.ops.object.select_all(action="DESELECT")
    for o in (armature, mesh):
        o.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return armature, mesh


def keep_right_bones(armature):
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    bones = armature.data.edit_bones
    for bone in list(bones):
        name = new_name(bone.name)
        keep = name is not None and not name.startswith("IK") and not name.startswith("Biceps") and not bone.name.endswith("_end")
        if not keep:
            bones.remove(bone)
    for bone in bones:
        bone.name = new_name(bone.name)
    bones["Forearm"].parent = None
    bpy.ops.object.mode_set(mode="OBJECT")


def fix_weights(mesh, armature):
    """Левую руку долой; веса плеча — предплечью; ладонь за костяшками — кости Palm."""
    bones = armature.data.bones
    # Рука вытянута в −X: «за костяшками» — x больше линии костяшек (с небольшим запасом).
    knuckle_x = max(bones[f + "1"].head_local.x for f in ("Index", "Middle", "Ring", "Pinky")) + 0.006

    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x > 0], context="VERTS")
    bm.to_mesh(mesh.data)
    bm.free()

    # Группы оставшихся костей Blender уже переименовал вместе с костями; левые — долой, Biceps.Right — в Biceps.
    for g in list(mesh.vertex_groups):
        if g.name.endswith(".Left") or g.name.endswith(".L"):
            mesh.vertex_groups.remove(g)
        else:
            g.name = new_name(g.name) or g.name
    groups = {g.index: g.name for g in mesh.vertex_groups}
    forearm = mesh.vertex_groups["Forearm"]
    palm = mesh.vertex_groups["Palm"]
    biceps = mesh.vertex_groups.get("Biceps")

    for v in mesh.data.vertices:
        weights = {groups[g.group]: g.weight for g in v.groups}
        moved_forearm = weights.get("Biceps", 0.0)
        moved_palm = 0.0
        if v.co.x > knuckle_x:
            for name, w in weights.items():
                if name[:-1] in ("Index", "Middle", "Ring", "Pinky"):
                    moved_palm += w
                    mesh.vertex_groups[name].remove([v.index])
        if moved_forearm > 0:
            forearm.add([v.index], weights.get("Forearm", 0.0) + moved_forearm, "REPLACE")
        if moved_palm > 0:
            palm.add([v.index], weights.get("Palm", 0.0) + moved_palm, "REPLACE")
    if biceps is not None:
        mesh.vertex_groups.remove(biceps)


def cut_forearm(mesh, armature):
    """Срез предплечья плоскостью поперёк руки, срез закрыт заглушкой."""
    bones = armature.data.bones
    wrist = bones["Palm"].head_local.copy()
    elbow = bones["Forearm"].head_local.copy()
    axis = (wrist - elbow).normalized()            # от локтя к кисти
    cut = wrist + (elbow - wrist) * FOREARM_KEEP

    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    result = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=cut, plane_no=axis, clear_inner=True)
    loop = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    loop_verts = list({v for e in loop for v in e.verts})
    centre = sum((v.co for v in loop_verts), Vector()) / len(loop_verts)

    # Упорядочить кольцо среза по углу вокруг оси руки.
    ref = (loop_verts[0].co - centre).normalized()
    side = axis.cross(ref)
    loop_verts.sort(key=lambda v: math.atan2((v.co - centre).dot(side), (v.co - centre).dot(ref)))

    # Срез закрыт тёмной заглушкой (второй материал): снаружи её закрывает рукав (zonk_sleeves.py).
    cap = [bm.verts.new(v.co.copy()) for v in loop_verts]
    cap_face = bm.faces.new(list(reversed(cap)))
    cap_face.material_index = 1
    bmesh.ops.recalc_face_normals(bm, faces=[cap_face])
    if cap_face.normal.dot(axis) > 0:                # смотрит к локтю, не в руку
        cap_face.normal_flip()
    new_verts = cap
    bm.to_mesh(mesh.data)
    bm.free()

    forearm = mesh.vertex_groups["Forearm"]
    first_new = len(mesh.data.vertices) - len(new_verts)
    indices = list(range(first_new, len(mesh.data.vertices)))
    for g in mesh.vertex_groups:
        g.remove(indices)
    forearm.add(indices, 1.0, "REPLACE")


def setup_materials(mesh):
    mesh.data.materials.clear()
    for name, color in (("Hand_Skin", SKIN_COLOR), ("Hand_Cuff", CUFF_COLOR)):
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color = color
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        for node in list(mat.node_tree.nodes):
            if node.type == "TEX_IMAGE":
                mat.node_tree.nodes.remove(node)
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Roughness"].default_value = 0.7
        mesh.data.materials.append(mat)


def place(armature, mesh):
    """Центр ладони в ноль, пальцы в −Y Blender (+Z Unity), масштаб SCALE."""
    bones = armature.data.bones
    palm_centre = (bones["Palm"].head_local + bones["Middle1"].head_local) * 0.5
    zs = [v.co.z for v in mesh.data.vertices if (v.co - palm_centre).length < 0.05]
    palm_centre.z = (min(zs) + max(zs)) * 0.5
    ys = [v.co.y for v in mesh.data.vertices if v.co.x < bones["Palm"].head_local.x]
    palm_centre.y = (min(ys) + max(ys)) * 0.5
    transform = Matrix.Scale(SCALE, 4) @ Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Translation(-palm_centre)
    for o in (armature, mesh):
        o.matrix_world = transform @ o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    for o in (armature, mesh):
        o.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    armature.name = "Hand"
    armature.data.name = "Hand"
    mesh.name = "HandMesh"
    mesh.data.name = "HandMesh"

    zs = [v.co.z for v in mesh.data.vertices if v.co.length < 0.045 * SCALE]
    print("[zonk_arms] palm half thickness: %.3f, bottom %.3f" % ((max(zs) - min(zs)) * 0.5, min(zs)))


def add_anchor(armature, name, bone, location):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(empty)
    empty.parent = armature
    empty.parent_type = "BONE"
    empty.parent_bone = bone
    empty.matrix_world = Matrix.Translation(location)
    return empty


def export(armature, mesh, anchors, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in [armature, mesh] + anchors:
        o.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        mesh_smooth_type="OFF",
        use_mesh_modifiers=False,
        use_tspace=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )


# ---------------------------------------------------------------------------
# Превью: тот же расчёт хвата, что HandView.SolveGrip в игре
# ---------------------------------------------------------------------------

def finger_chains(armature):
    bones = armature.data.bones
    return [[bones[f + str(i)] for i in (1, 2, 3)] for f in FINGERS]


def grip_centre(cup_radius, palm_offset, pad_forward, tilt):
    """
    Центр стакана в осях руки (высота, вперёд): стакан касается подушки ладони у костяшек
    (palm_offset под ладонью, pad_forward вперёд), кисть наклонена на tilt — пальцы ближе к стакану, ладонь у запястья
    отходит от стенки.
    """
    return (-palm_offset - cup_radius * math.cos(tilt), pad_forward + cup_radius * math.sin(tilt))


ITERATIONS = 14


def crossing(outside, lo, hi):
    """Угол в [lo, hi], где outside(угол) меняется; на краях одинаково — тот край, к которому шли."""
    start = outside(lo)
    if outside(hi) == start:
        return hi
    for _ in range(ITERATIONS):
        mid = (lo + hi) * 0.5
        if outside(mid) == start:
            lo = mid
        else:
            hi = mid
    return (lo + hi) * 0.5


def segments_of(chain):
    """Фаланги в плоскости поперёк оси стакана: начало, (угол над «вперёд», длина) каждой."""
    start = (chain[0].head_local.z, -chain[0].head_local.y)
    segments = []
    for bone in chain:
        d = (bone.tail_local.z - bone.head_local.z, -(bone.tail_local.y - bone.head_local.y))
        segments.append((math.atan2(d[0], d[1]), math.hypot(*d)))
    return start, segments


def bend_finger(start, segments, centre, radius, max_bend, max_open):
    """
    Палец обхватывает стакан (окружность radius): суставы по очереди от костяшки. Фаланга, которая не дотянется
    до стенки, гнётся только до касательной (не ныряет к стакану); дотянется — гнётся, пока конец не ляжет на стенку;
    уже упирается в стенку — чуть разгибается (до max_open).
    """
    angles = []
    p, total = start, 0.0
    for base, length in segments:
        a0 = base - total

        def end(theta):
            return (p[0] + math.sin(a0 - theta) * length, p[1] + math.cos(a0 - theta) * length)

        def outside(theta):
            e = end(theta)
            return math.hypot(e[0] - centre[0], e[1] - centre[1]) > radius

        v = (centre[0] - p[0], centre[1] - p[1])
        d = math.hypot(*v)
        theta = None
        if d > radius:
            tangent = math.atan2(v[0], v[1]) + math.asin(radius / d)
            to_tangent = math.atan2(math.sin(a0 - tangent), math.cos(a0 - tangent))
            if to_tangent >= 0 and length <= math.sqrt(d * d - radius * radius):
                theta = min(to_tangent, max_bend)
        if theta is None:
            theta = crossing(outside, 0.0, max_bend) if outside(0.0) else -crossing(lambda t: outside(-t), 0.0, max_open)
        angles.append(theta)
        p = end(theta)
        total += theta
    return angles


def bend_thumb(start, segments, centre, radius, max_bend, max_open):
    """Большой палец — один угол на все суставы: кончик на стенку; в стакане — отводится наружу."""
    def outside(theta):
        p = start
        for i, (base, length) in enumerate(segments):
            a = base - theta * (i + 1)
            p = (p[0] + math.sin(a) * length, p[1] + math.cos(a) * length)
        return math.hypot(p[0] - centre[0], p[1] - centre[1]) > radius

    theta = crossing(outside, 0.0, max_bend) if outside(0.0) else -crossing(lambda t: outside(-t), 0.0, max_open)
    return [theta] * len(segments)


def solve_grip(armature, centre, cup_radius, finger_pad, max_bend, max_open, thumb_bend, thumb_open):
    """Углы сгиба (HandView.SolveGrip): ось стакана — X руки, плоскость (высота, вперёд) = Blender (z, −y)."""
    radius = cup_radius + finger_pad
    angles = {}
    for chain in finger_chains(armature):
        start, segments = segments_of(chain)
        if chain[0].name.startswith("Thumb"):
            values = bend_thumb(start, segments, centre, radius, thumb_bend, thumb_open)
        else:
            values = bend_finger(start, segments, centre, radius, max_bend, max_open)
        for bone, theta in zip(chain, values):
            angles[bone.name] = theta
    return angles


def pose(armature, angles):
    """Поворот каждой кости вокруг оси X руки (в Blender +X) на свой угол: пальцы к ладони."""
    for pb in armature.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    for name, theta in angles.items():
        pb = armature.pose.bones[name]
        axis_world = Vector((1, 0, 0))
        axis_local = (pb.bone.matrix_local.to_3x3().inverted() @ axis_world).normalized()
        pb.rotation_quaternion = Matrix.Rotation(theta, 4, axis_local).to_quaternion()
    bpy.context.view_layer.update()


def render_previews(armature, out_dir):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    world = bpy.data.worlds.new("w")
    world.color = (0.32, 0.32, 0.35)
    scene.world = world
    bpy.ops.object.light_add(type="SUN", rotation=(0.8, 0.3, 0.6))
    bpy.context.object.data.energy = 4

    max_angle, max_open = math.radians(GRIP_MAX_BEND), math.radians(GRIP_MAX_OPEN)
    fist = {"1": 90, "2": 95, "3": 70}
    cup = None
    for name, radius in (("open", None), ("grip_034", 0.34), ("grip_042", 0.42), ("fist", 0.0)):
        if cup is not None:
            bpy.data.objects.remove(cup)
            cup = None
        if radius is None:
            angles = {b.name: math.radians(8) for chain in finger_chains(armature) for b in chain}
        elif radius == 0.0:
            angles = {b.name: math.radians(fist[b.name[-1]] * (0.4 if b.name.startswith("Thumb") else 1.0))
                      for chain in finger_chains(armature) for b in chain}
        else:
            centre = grip_centre(radius, GRIP_PALM_OFFSET, GRIP_PAD_FORWARD, math.radians(GRIP_TILT))
            angles = solve_grip(armature, centre, radius, GRIP_FINGER_PAD, max_angle, max_open,
                                math.radians(GRIP_THUMB_BEND), math.radians(GRIP_THUMB_OPEN))
            print("[zonk_arms] %s: " % name + ", ".join("%s %.0f" % (k, math.degrees(v)) for k, v in angles.items()))
            # Стакан: ось вдоль X руки (в игре вертикаль); (высота, вперёд) → Blender (z, −y).
            bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=0.9, location=(0, -centre[1], centre[0]),
                                                rotation=(0, math.radians(90), 0), vertices=32)
            cup = bpy.context.object
            mat = bpy.data.materials.new("cup")
            mat.diffuse_color = (0.45, 0.3, 0.2, 1)
            cup.data.materials.append(mat)
        pose(armature, angles)
        for view, loc in (("side", (SCALE, -0.09 * SCALE, -0.09 * SCALE)), ("persp", (0.55 * SCALE, -0.6 * SCALE, 0.35 * SCALE))):
            bpy.ops.object.camera_add(location=loc)
            cam = bpy.context.object
            target = Vector((0, -0.09 * SCALE, -0.09 * SCALE))
            cam.rotation_euler = (target - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
            cam.data.lens = 50
            scene.camera = cam
            scene.render.filepath = os.path.join(out_dir, "hand_%s_%s.png" % (name, view))
            bpy.ops.render.render(write_still=True)
            bpy.data.objects.remove(cam)
    pose(armature, {})


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None

    armature, mesh = load_source()
    keep_right_bones(armature)
    fix_weights(mesh, armature)
    setup_materials(mesh)
    cut_forearm(mesh, armature)
    place(armature, mesh)

    bones = armature.data.bones
    wrist = bones["Palm"].head_local.lerp(bones["Forearm"].head_local, 0.15)
    anchors = [add_anchor(armature, "Wrist", "Forearm", wrist)]

    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    print("[zonk_arms] triangles: %d, bones: %d" % (tris, len(bones)))

    os.makedirs(OUT_DIR, exist_ok=True)
    export(armature, mesh, anchors, os.path.join(OUT_DIR, "Hand.fbx"))
    print("[zonk_arms] exported " + os.path.join(OUT_DIR, "Hand.fbx"))

    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render_previews(armature, render_dir)


if __name__ == "__main__":
    main()
