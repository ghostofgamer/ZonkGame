# -*- coding: utf-8 -*-
"""
Стаканы для игры «Зонк»: десять разных по форме, одной высоты (0.9), с полостью под кости.

    Cup_Goblet   кубок: чаша на ножке, золото, гравировка, камни
    Cup_Bone     костяной: бугристый, кольца-позвонки, когти-ножки, трещины
    Cup_Clay     глиняный горшок под глазурью: сколы на горле, трещины от сколов, потёки глазури
    Cup_Barrel   бочонок: клёпки-доски разной высоты, железные обручи с заклёпками, отколотая доска
    Cup_Coconut  кокос: скорлупа с неровным срезом, белая мякоть по краю, верёвка
    Cup_Copper   корабельная медная кружка: шов и пояса на заклёпках, вмятины, патина
    Cup_Stone    каменный восьмигранник: руны, отбитые углы и край, мох
    Cup_Horn     изогнутый рог на кованой подставке (кольцо и три ножки), серебряная оковка с гравировкой
    Cup_Wicker   плетёная корзинка из лозы: 12 стоек, ряды «над-под», плетёный обод
    Cup_Crystal  шестигранный аметист: каменное основание, светлые руны, золотой обод, друзы у основания

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_cups.py
    blender --background --factory-startup --python Tools/Blender/zonk_cups.py -- --render <папка>
    blender --background --factory-startup --python Tools/Blender/zonk_cups.py -- --only Cup_Horn,Cup_Wicker,Cup_Crystal
--render ещё рендерит превью стаканов в ряд (с костью для масштаба). --only — собрать и выгрузить только
перечисленные (остальные FBX и PNG не перезаписываются).

Результат: Assets/ZonkContent/Art/Models/Cups/<Имя>.fbx и Art/Textures/Cups/<Имя>.png.
В FBX у стакана два пустых объекта: "Mouth" (горло, верх обода) и "Inside" (внутренность для костей):
высота "Inside" — внутреннее дно, расстояние от оси — свободный радиус внутри.

Оси как в zonk_models.py: Unity = (-bx, bz, -by). Проверено в Blender 3.6.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True   # не оставлять __pycache__ в Tools/Blender
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402  общие функции: сцена, PNG, экспорт, шум

# ---------------------------------------------------------------------------
# Параметры
# ---------------------------------------------------------------------------

PROJECT_ROOT = zm.PROJECT_ROOT
MODELS_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Cups")
TEXTURES_DIR = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures", "Cups")

H = 0.9                   # высота стакана (верх обода)
SEG = 32                  # сегменты по кругу
TEX = 1024                # размер текстуры
SIDE_V0 = 0.26            # боковая полоса UV: от SIDE_V0 до 1 (снаружи, обод, внутри)
DISC_R = 0.115            # радиус дисков дна в UV
DISC_OUT = (0.13, 0.13)   # наружное дно
DISC_IN = (0.38, 0.13)    # внутреннее дно
PATCH_V = 0.12            # плашки однотонных цветов для деталей (камни, заклёпки): справа внизу
PATCH_U0, PATCH_DU, PATCH_HALF = 0.56, 0.09, 0.035
INSIDE_MARGIN = 0.12      # свободный радиус меряется выше дна на это расстояние
PROFILE_TOLERANCE = 0.003   # насколько профиль может отойти от задуманного при упрощении, м
PROFILE_MAX_STEP = 0.09     # ряды по высоте не реже, м (для бугров и вмятин)

# Текстуры под комиксовый шейдер Zonk/Toon: ровные цветовые пятна вместо мелкого шума
COMIC = True
COMIC_BLUR = 2.0          # размытие мелкого шума, пикселей
COMIC_LEVELS = 6          # ступеней яркости
COMIC_POSTER = 0.55       # доля ступенчатого цвета (0 — только размытие)
COMIC_SATURATION = 1.15


# ---------------------------------------------------------------------------
# Профили
# ---------------------------------------------------------------------------

def catmull(points, per_seg=4):
    """Сглаженная кривая через точки (Catmull-Rom), концы сохраняются."""
    pts = [Vector(p) for p in points]
    out = []
    for i in range(len(pts) - 1):
        p0 = pts[i - 1] if i > 0 else pts[i]
        p1, p2 = pts[i], pts[i + 1]
        p3 = pts[i + 2] if i + 2 < len(pts) else pts[i + 1]
        for k in range(per_seg):
            t = k / per_seg
            t2, t3 = t * t, t * t * t
            p = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((p.x, p.y))
    out.append(tuple(pts[-1]))
    return out


def join(*parts):
    """Склейка кусков профиля без повторов на стыках."""
    out = []
    for part in parts:
        for p in part:
            if not out or abs(out[-1][0] - p[0]) > 1e-6 or abs(out[-1][1] - p[1]) > 1e-6:
                out.append(p)
    return out


def simplify(points, tol=PROFILE_TOLERANCE, max_len=PROFILE_MAX_STEP):
    """Убрать лишние ряды профиля: точка остаётся, если без неё форма отклонится больше tol (Дуглас — Пекер),
    и ряды не реже max_len — чтобы бугры и вмятины (dr) было на чём показать."""
    pts = [tuple(p) for p in points]
    if len(pts) < 3:
        return pts

    def dp(a, b):
        (x0, y0), (x1, y1) = pts[a], pts[b]
        dx, dy = x1 - x0, y1 - y0
        seg = math.hypot(dx, dy) or 1e-9
        worst, at = 0.0, -1
        for i in range(a + 1, b):
            x, y = pts[i]
            d = abs(dy * (x - x0) - dx * (y - y0)) / seg
            if d > worst:
                worst, at = d, i
        if worst <= tol:
            return [a, b]
        return dp(a, at)[:-1] + dp(at, b)

    kept = [pts[i] for i in dp(0, len(pts) - 1)]
    out = [kept[0]]
    for p in kept[1:]:
        q = out[-1]
        n = int(math.hypot(p[0] - q[0], p[1] - q[1]) / max_len)
        for k in range(1, n + 1):
            t = k / (n + 1)
            out.append((q[0] + (p[0] - q[0]) * t, q[1] + (p[1] - q[1]) * t))
        out.append(p)
    return out


class Cup:
    """Описание стакана: профиль (снаружи снизу вверх, обод, внутри сверху вниз) и правила формы."""

    def __init__(self, name, outer, rim, inner):
        self.name = name
        outer, inner = simplify(outer), simplify(inner)
        self.profile = join(outer, rim, inner)
        self.rim_index = len(join(outer)) + len(rim) // 2 - 1   # точка на ободе: граница наружной и внутренней части
        self.floor = self.profile[-1][1]
        self.smooth_angle = 45.0
        self.seg = SEG           # сегменты по кругу (у бочонка и многогранника — кратно доскам и граням)
        self.facets = 0          # >0: многогранник (r — апофема грани)
        self.extras = []         # функции (cup, bm) -> добавить детали в bmesh
        self.patches = []        # цвета однотонных плашек
        self.metallic = 0.0
        self.roughness = 0.6
        self.bottom_center = (0.0, 0.0)   # центр наружного донышка (у изогнутого рога смещён)

    # Отклонение радиуса и опускание обода — переопределяются у конкретного стакана.
    def dr(self, theta, z, inner):
        return 0.0

    def rim_drop(self, theta):
        return 0.0

    def facet_scale(self, theta):
        if self.facets <= 0:
            return 1.0
        step = 2.0 * math.pi / self.facets
        d = (theta + step / 2) % step - step / 2
        return 1.0 / math.cos(d)

    def is_inner(self, i):
        return i > self.rim_index

    def outer_radius(self, theta, z):
        """Радиус наружной стенки на высоте z (для посадки деталей)."""
        prof = self.profile[:self.rim_index + 1]
        best = None
        for (r0, z0), (r1, z1) in zip(prof, prof[1:]):
            if min(z0, z1) <= z <= max(z0, z1) and abs(z1 - z0) > 1e-6:
                r = r0 + (r1 - r0) * (z - z0) / (z1 - z0)
                best = r if best is None else max(best, r)
        r = best if best is not None else prof[-1][0]
        return r * self.facet_scale(theta) + self.dr(theta, z, False)

    def inner_free_radius(self):
        """Наименьший радиус внутренней стенки выше дна: столько места у костей."""
        best = 10.0
        for i, (r, z) in enumerate(self.profile):
            if self.is_inner(i) and self.floor + INSIDE_MARGIN <= z <= H - 0.03:
                best = min(best, r)
        return best


def compress_top(z, drop, band=0.09):
    """Опускает верх стенки на drop плавно в полосе band (без вырожденных граней)."""
    if drop <= 0.0 or z <= H - band:
        return z
    return (H - band) + (z - (H - band)) * (band - drop) / band


def chip_drop(theta, chips, jag_seed=0):
    """Сколы на ободе: (центр, полуширина, глубина). Края резкие, дно скола неровное."""
    drop = 0.0
    for k, (c, w, d) in enumerate(chips):
        a = abs((theta - c + math.pi) % (2 * math.pi) - math.pi)
        if a < w:
            edge = zm.smoothstep(w, w * 0.55, a)
            jag = 0.75 + 0.25 * math.sin(theta * 37.0 + k * 5.1 + jag_seed)
            drop = max(drop, float(d * edge * jag))
    return drop


def chip_mask(theta, z, chips):
    """Маска сколотого места на текстуре (numpy)."""
    m = np.zeros_like(theta)
    for c, w, d in chips:
        a = np.abs((theta - c + np.pi) % (2 * np.pi) - np.pi)
        m = np.maximum(m, (1.0 - zm.smoothstep(w * 0.8, w * 1.05, a)) * zm.smoothstep(H - d - 0.03, H - d + 0.005, z))
    return m


# ---------------------------------------------------------------------------
# Меш
# ---------------------------------------------------------------------------

def side_v_of(profile):
    lengths = [0.0]
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        lengths.append(lengths[-1] + math.hypot(r1 - r0, z1 - z0))
    return [SIDE_V0 + (1.0 - SIDE_V0) * L / lengths[-1] for L in lengths]


def build_mesh(cup):
    seg = cup.seg
    prof = cup.profile
    n = len(prof)
    side_v = side_v_of(prof)
    verts = []
    for i, (r, z) in enumerate(prof):
        inner = cup.is_inner(i)
        for j in range(seg):
            a = 2.0 * math.pi * j / seg
            rr = r * cup.facet_scale(a) + cup.dr(a, z, inner)
            zz = compress_top(z, cup.rim_drop(a)) if z > 0.02 else z
            verts.append((rr * math.cos(a), rr * math.sin(a), zz))
    c_bot = len(verts)
    verts.append((cup.bottom_center[0], cup.bottom_center[1], 0.0))
    c_floor = len(verts)
    verts.append((0.0, 0.0, prof[-1][1]))

    def vid(i, j):
        return i * seg + (j % seg)

    faces, uvs = [], []
    for i in range(n - 1):
        for j in range(seg):
            u0, u1 = j / seg, (j + 1) / seg
            faces.append((vid(i, j), vid(i, j + 1), vid(i + 1, j + 1), vid(i + 1, j)))
            uvs.append(((u0, side_v[i]), (u1, side_v[i]), (u1, side_v[i + 1]), (u0, side_v[i + 1])))

    r_disc = max(prof[0][0], prof[-1][0]) * 1.15

    def disc(i, j, center):
        a = 2.0 * math.pi * j / seg
        x, y = verts[vid(i, j)][0], verts[vid(i, j)][1]
        return (center[0] + x / r_disc * DISC_R, center[1] + y / r_disc * DISC_R)

    for j in range(seg):
        faces.append((c_bot, vid(0, j + 1), vid(0, j)))
        uvs.append((DISC_OUT, disc(0, j + 1, DISC_OUT), disc(0, j, DISC_OUT)))
    last = n - 1
    for j in range(seg):
        faces.append((c_floor, vid(last, j), vid(last, j + 1)))
        uvs.append((DISC_IN, disc(last, j, DISC_IN), disc(last, j + 1, DISC_IN)))

    mesh = bpy.data.meshes.new(cup.name)
    mesh.from_pydata(verts, [], faces)
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly, puv in zip(mesh.polygons, uvs):
        for li, uv in zip(poly.loop_indices, puv):
            uv_layer.data[li].uv = uv
    mesh.update()

    # Наружная стенка должна смотреть от оси.
    probe = mesh.polygons[(cup.rim_index // 2) * seg]
    radial = Vector((probe.center.x, probe.center.y, 0)).normalized()
    if probe.normal.dot(radial) < 0:
        mesh.flip_normals()

    # Детали (камни, заклёпки, ножки, верёвка) — в тот же меш, UV на плашку своего цвета.
    if cup.extras:
        bm = bmesh.new()
        bm.from_mesh(mesh)
        uv = bm.loops.layers.uv.active
        for extra in cup.extras:
            before = set(bm.faces)
            patch = extra(cup, bm)
            new_faces = [f for f in bm.faces if f not in before]
            bmesh.ops.recalc_face_normals(bm, faces=new_faces)
            pu = PATCH_U0 + PATCH_DU * patch
            for f in bm.faces:
                if f not in before:
                    for loop in f.loops:
                        loop[uv].uv = (pu, PATCH_V)
        bm.to_mesh(mesh)
        bm.free()

    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.use_auto_smooth = True
    mesh.auto_smooth_angle = math.radians(cup.smooth_angle)
    mesh.validate()
    mesh.update()
    return mesh, side_v


def add_dome(bm, pos, normal, radius, height, segs=8):
    """Полусфера-заклёпка/камень, приплюснутая вдоль нормали."""
    res = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=2, radius=1.0)
    vs = res["verts"]
    rot = Vector((0, 0, 1)).rotation_difference(normal).to_matrix().to_4x4()
    m = Matrix.Translation(pos) @ rot @ Matrix.Diagonal((radius, radius, height, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=vs)


def add_gem(bm, pos, normal, radius, height):
    res = bmesh.ops.create_icosphere(bm, subdivisions=0, radius=1.0)
    rot = Vector((0, 0, 1)).rotation_difference(normal).to_matrix().to_4x4()
    m = Matrix.Translation(pos) @ rot @ Matrix.Diagonal((radius, radius, height, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=res["verts"])


def surface_point(cup, theta, z, lift=0.0):
    r = cup.outer_radius(theta, z) + lift
    return Vector((r * math.cos(theta), r * math.sin(theta), z)), Vector((math.cos(theta), math.sin(theta), 0.0))


def add_tube(bm, path, radii, sides=6, caps=True):
    """Трубка по точкам пути с радиусами (ножки-когти). Концы закрыты (caps)."""
    rings = []
    for k, (p, rad) in enumerate(zip(path, radii)):
        t = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
        a = t.orthogonal().normalized()
        b = t.cross(a).normalized()
        ring = [bm.verts.new(p + (a * math.cos(2 * math.pi * s / sides) + b * math.sin(2 * math.pi * s / sides)) * rad)
                for s in range(sides)]
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for s in range(sides):
            bm.faces.new((r0[s], r0[(s + 1) % sides], r1[(s + 1) % sides], r1[s]))
    if caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])


# ---------------------------------------------------------------------------
# Текстура: поля (угол, высота, радиус, снаружи/внутри) для каждого пикселя
# ---------------------------------------------------------------------------

class Fields:
    def __init__(self, cup, side_v):
        s = TEX
        u = (np.arange(s)[None, :] + 0.5) / s * np.ones((s, 1))
        v = (np.arange(s)[:, None] + 0.5) / s * np.ones((1, s))
        self.u, self.v = u, v
        prof = cup.profile
        zs = np.array([p[1] for p in prof])
        rs = np.array([p[0] for p in prof])
        idx = np.arange(len(prof), dtype=np.float64)
        sv = np.array(side_v)
        self.side = v >= SIDE_V0
        t = np.interp(v, sv, idx)
        self.theta = 2.0 * np.pi * u
        self.z = np.interp(v, sv, zs)
        self.r = np.interp(v, sv, rs)
        self.inner = self.side & (t > cup.rim_index + 0.5)
        self.outer = self.side & ~self.inner
        # «впадина» профиля: темнее в канавках
        conc = np.zeros(len(prof))
        for i in range(1, len(prof) - 1):
            conc[i] = 0.5 * (rs[i - 1] + rs[i + 1]) - rs[i]
        conc = np.convolve(conc, np.ones(3) / 3.0, mode="same")
        self.cavity = np.interp(v, sv, conc) * self.outer
        # глубина внутри: 0 у обода, 1 у дна
        self.depth = np.where(self.inner, np.clip((H - self.z) / max(H - cup.floor, 1e-3), 0, 1), 0.0)

        # диски: наружное дно (z = 0) и внутреннее дно
        r_disc = max(prof[0][0], prof[-1][0]) * 1.15
        for name, c, zval in (("bottom", DISC_OUT, 0.0), ("floor", DISC_IN, cup.floor)):
            du, dv = u - c[0], v - c[1]
            d = np.sqrt(du * du + dv * dv)
            mask = d <= DISC_R * 1.02
            setattr(self, name, mask)
            self.theta = np.where(mask, np.arctan2(dv, du) % (2 * np.pi), self.theta)
            self.r = np.where(mask, d / DISC_R * r_disc, self.r)
            self.z = np.where(mask, zval, self.z)
        self.depth = np.where(self.floor, 1.0, self.depth)

    def v_of(self, cup, side_v, z, inner):
        """V строки, где профиль на высоте z (снаружи или внутри)."""
        best, bv = 1e9, SIDE_V0
        for i, (r, zz) in enumerate(cup.profile):
            if cup.is_inner(i) == inner and abs(zz - z) < best:
                best, bv = abs(zz - z), side_v[i]
        return bv


def lerp(a, b, t):
    t = np.asarray(t)[..., None] if np.ndim(t) else t
    return a * (1.0 - t) + b * t


def col(*c):
    return np.array(c, dtype=np.float32)


def draw_cracks(mask, rng, starts, length=180, branch=0.012, wander=0.13, width=1.2):
    """Трещины: случайное блуждание от точек (x, y, угол) с ветвлением. Бесшовно по X."""
    h, w = mask.shape
    stack = [(x, y, a, length, width) for (x, y, a) in starts]
    while stack:
        x, y, a, steps, wd = stack.pop()
        for k in range(int(steps)):
            a += rng.normal(0, wander)
            x += math.cos(a) * 1.5
            y += math.sin(a) * 1.5
            if y < 0 or y >= h:
                break
            t = 1.0 - k / steps
            rad = max(0.6, wd * t)
            ir = int(math.ceil(rad + 1))
            for dy in range(-ir, ir + 1):
                yy = int(y) + dy
                if 0 <= yy < h:
                    for dx in range(-ir, ir + 1):
                        xx = (int(x) + dx) % w
                        d = math.hypot(dx + int(x) - x, dy + int(y) - y)
                        val = max(0.0, 1.0 - max(0.0, d - rad * 0.5) / (rad * 0.8 + 0.5))
                        if val > mask[yy, xx]:
                            mask[yy, xx] = val
            if rng.random() < branch:
                stack.append((x, y, a + rng.choice((-1, 1)) * rng.uniform(0.5, 1.0), steps * 0.35 * t, wd * 0.6))


def crack_starts_from_chips(cup, side_v, fields, chips, count=1, rng=None):
    """Трещины начинаются у сколов: вниз по наружной стенке и вниз по внутренней."""
    starts = []
    for c, w, d in chips:
        x = (c % (2 * math.pi)) / (2 * math.pi) * TEX
        vo = fields.v_of(cup, side_v, H - d, False) * TEX
        vi = fields.v_of(cup, side_v, H - d, True) * TEX
        for _ in range(count):
            starts.append((x + rng.uniform(-8, 8), vo, -math.pi / 2 + rng.uniform(-0.3, 0.3)))
            starts.append((x + rng.uniform(-8, 8), vi, math.pi / 2 + rng.uniform(-0.3, 0.3)))
    return starts


def comic_flatten(img):
    """Текстура под комиксовый шейдер: убрать мелкий шум, свести цвет к ровным пятнам (постеризация),
    чуть поднять насыщенность. Тёмные линии (трещины, руны, гравировка, швы) остаются чёткими."""
    if not COMIC:
        return img
    h, w, _ = img.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    kernel = np.exp(-2.0 * (np.pi * COMIC_BLUR) ** 2 * (fx * fx + fy * fy))
    blur = np.empty_like(img)
    for c in range(3):
        blur[:, :, c] = np.real(np.fft.ifft2(np.fft.fft2(img[:, :, c]) * kernel))
    weights = np.array((0.3, 0.59, 0.11), dtype=np.float32)
    lum = blur @ weights
    poster = (np.floor(lum * COMIC_LEVELS) + 0.5) / COMIC_LEVELS
    flat = blur * (poster / np.maximum(lum, 1e-4))[..., None]
    out = lerp(blur, flat, COMIC_POSTER)
    grey = (out @ weights)[..., None]
    out = grey + (out - grey) * COMIC_SATURATION
    # линии: заметно темнее размытого — берём из исходника
    lines = zm.smoothstep(0.06, 0.16, lum - img @ weights)
    out = lerp(out, img, lines)
    return np.clip(out, 0.0, 1.0)


def finish_texture(cup, img):
    """Упрощение под комикс, плашки однотонных цветов для деталей и сохранение."""
    img = comic_flatten(img)
    for k, c in enumerate(cup.patches):
        cu = PATCH_U0 + PATCH_DU * k
        x0, x1 = int((cu - PATCH_HALF) * TEX), int((cu + PATCH_HALF) * TEX)
        y0, y1 = int((PATCH_V - PATCH_HALF) * TEX), int((PATCH_V + PATCH_HALF) * TEX)
        img[y0:y1, x0:x1] = c
    return zm.save_png(cup.name, img, os.path.join(TEXTURES_DIR, cup.name + ".png"))


def inside_shade(f, img, strength=0.45):
    """Внутри темнее к дну (запечённое затенение)."""
    k = 1.0 - strength * f.depth
    return img * k[..., None]


# ---------------------------------------------------------------------------
# 1. Кубок
# ---------------------------------------------------------------------------

def make_goblet():
    outer = join(
        [(0.28, 0.0), (0.30, 0.006), (0.302, 0.018), (0.292, 0.03)],
        catmull([(0.292, 0.03), (0.24, 0.045), (0.15, 0.085), (0.085, 0.135), (0.065, 0.175)], 2),
        [(0.064, 0.195), (0.085, 0.212), (0.108, 0.232), (0.112, 0.245), (0.106, 0.262), (0.082, 0.28), (0.062, 0.295), (0.058, 0.312)],
        catmull([(0.058, 0.312), (0.11, 0.348), (0.19, 0.392), (0.255, 0.45), (0.30, 0.53), (0.335, 0.63),
                 (0.355, 0.73), (0.366, 0.82), (0.372, 0.872)], 2),
    )
    rim = [(0.38, 0.884), (0.377, 0.896), (0.366, 0.9), (0.354, 0.895)]
    inner = catmull([(0.35, 0.88), (0.347, 0.80), (0.337, 0.70), (0.316, 0.60), (0.28, 0.52), (0.22, 0.462),
                     (0.13, 0.427), (0.05, 0.414)], 2)
    cup = Cup("Cup_Goblet", outer, rim, inner)
    cup.metallic, cup.roughness = 0.9, 0.3
    cup.patches = [col(0.62, 0.04, 0.08), col(0.05, 0.42, 0.22)]

    def gems(c, bm):
        for k in range(6):
            a = 2 * math.pi * k / 6 + math.pi / 6
            p, nrm = surface_point(c, a, 0.70, 0.004)
            add_gem(bm, p, nrm, 0.03, 0.016)
        return 0

    def gems_green(c, bm):
        for k in range(6):
            a = 2 * math.pi * k / 6
            p, nrm = surface_point(c, a, 0.70, 0.004)
            add_gem(bm, p, nrm, 0.02, 0.012)
        return 1

    cup.extras = [gems, gems_green]

    def texture(f, side_v):
        rng = np.random.default_rng(11)
        brushed = zm.periodic_noise(rng, TEX, TEX, 1.0, scale_v=40.0)
        blot = zm.periodic_noise(rng, TEX, TEX, 60.0)
        gold = col(0.98, 0.76, 0.34)
        dark = col(0.55, 0.36, 0.12)
        img = lerp(gold, dark, np.clip(0.15 + 0.08 * brushed + 0.10 * blot, 0, 1))
        # гравировка: завитки в поясе чаши и на ступне
        band = f.outer & (f.z > 0.6) & (f.z < 0.8)
        scroll = np.abs(np.sin(f.theta * 12 + 4.0 * np.sin(f.z * 60.0)))
        engr = band * (1 - zm.smoothstep(0.0, 0.12, scroll))
        for zb in (0.6, 0.8, 0.06):
            engr = np.maximum(engr, f.outer * (1 - zm.smoothstep(0.002, 0.005, np.abs(f.z - zb))))
        foot = f.outer & (f.z > 0.04) & (f.z < 0.12)
        engr = np.maximum(engr, foot * (1 - zm.smoothstep(0.0, 0.15, np.abs(np.sin(f.theta * 16 + f.z * 40)))))
        img = lerp(img, dark * 0.6, engr * 0.8)
        # канавки и низ чаши темнее, обод светлее
        img = img * (1.0 - np.clip(f.cavity * 30.0, 0, 0.4))[..., None]
        img = lerp(img, col(1.0, 0.9, 0.6), zm.smoothstep(H - 0.03, H, f.z) * f.side * 0.6)
        return inside_shade(f, img, 0.4)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 2. Костяной
# ---------------------------------------------------------------------------

def make_bone():
    # Кость-сустав: широкая «головка» снизу и у горла, гладкое тело, два острых кольца-позвонка.
    outer = join(
        catmull([(0.22, 0.0), (0.27, 0.01), (0.30, 0.04), (0.31, 0.08), (0.295, 0.15), (0.282, 0.24), (0.282, 0.31)], 2),
        [(0.287, 0.335), (0.312, 0.355), (0.318, 0.375), (0.30, 0.392), (0.288, 0.405)],
        catmull([(0.288, 0.405), (0.287, 0.47), (0.29, 0.53)], 2),
        [(0.296, 0.555), (0.324, 0.575), (0.329, 0.597), (0.31, 0.615), (0.301, 0.628)],
        catmull([(0.301, 0.628), (0.312, 0.70), (0.336, 0.78), (0.354, 0.84), (0.36, 0.876)], 2),
    )
    rim = [(0.357, 0.892), (0.346, 0.9), (0.331, 0.898), (0.323, 0.887)]
    inner = catmull([(0.32, 0.87), (0.306, 0.78), (0.272, 0.64), (0.256, 0.47), (0.25, 0.30), (0.244, 0.17),
                     (0.22, 0.095), (0.14, 0.073), (0.06, 0.07)], 2)
    cup = Cup("Cup_Bone", outer, rim, inner)
    cup.roughness = 0.5
    cup.patches = [col(0.83, 0.76, 0.60)]
    chips = [(2.0, 0.30, 0.04), (4.4, 0.16, 0.022)]

    def dr(theta, z, inner):
        # кость не круглая: слегка овальная, с мягкими буграми
        oval = 0.011 * math.cos(2 * theta + 0.5)
        if inner:
            return oval
        return oval + 0.003 * math.sin(3 * theta + 1.3) * math.sin(7 * z + 0.4)

    def rim_drop(theta):
        return 0.006 * (0.5 + 0.5 * math.sin(3 * theta + 0.7)) + chip_drop(theta, chips)

    cup.dr, cup.rim_drop = dr, rim_drop

    def claws(c, bm):
        for k in range(3):
            a = 2 * math.pi * k / 3 + math.pi / 3
            d = Vector((math.cos(a), math.sin(a), 0))
            p0 = d * (c.outer_radius(a, 0.13) - 0.035) + Vector((0, 0, 0.13))
            p1 = d * 0.40 + Vector((0, 0, 0.08))
            p2 = d * 0.46 + Vector((0, 0, 0.006))
            path, radii = [], []
            for s in range(7):
                t = s / 6
                path.append(p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t * t)
                radii.append(0.055 * (1 - t) + 0.01 * t)
            add_tube(bm, path, radii, sides=7)
        return 0

    cup.extras = [claws]

    def texture(f, side_v):
        rng = np.random.default_rng(23)
        ivory = col(0.90, 0.85, 0.72)
        yellow = col(0.76, 0.66, 0.46)
        aged = col(0.50, 0.38, 0.22)
        grime = col(0.30, 0.22, 0.13)
        big = zm.periodic_noise(rng, TEX, TEX, 60.0)
        fib = zm.periodic_noise(rng, TEX, TEX, 1.5, scale_v=40.0)
        img = lerp(ivory, yellow, np.clip(0.25 + 0.18 * big + 0.08 * fib, 0, 1))
        # снизу кость темнее и старее
        img = lerp(img, aged, (1 - zm.smoothstep(0.0, 0.30, f.z)) * f.outer * 0.55)
        # поры
        pores = zm.periodic_noise(rng, TEX, TEX, 1.0)
        img = lerp(img, aged, zm.smoothstep(2.0, 2.7, pores) * 0.7)
        # грязь в канавках у колец
        img = lerp(img, grime, np.clip(f.cavity * 60.0, 0, 0.75))
        # сколы светлее
        cm = chip_mask(f.theta, f.z, chips) * f.side
        img = lerp(img, col(0.97, 0.94, 0.86), cm * 0.8)
        cracks = np.zeros((TEX, TEX), dtype=np.float32)
        starts = crack_starts_from_chips(cup, side_v, f, chips, 1, rng)
        starts.append((rng.uniform(0, TEX), f.v_of(cup, side_v, 0.02, False) * TEX, math.pi / 2))
        draw_cracks(cracks, rng, starts, length=230, width=1.3)
        img = lerp(img, grime, cracks * f.side * 0.8)
        return inside_shade(f, img, 0.5)

    cup.texture = texture
    return cup



# ---------------------------------------------------------------------------
# 3. Глиняный горшок с глазурью, сколами и трещинами
# ---------------------------------------------------------------------------

def make_clay():
    outer = join(
        [(0.20, 0.0), (0.215, 0.005), (0.222, 0.02), (0.216, 0.034), (0.232, 0.05)],
        catmull([(0.232, 0.05), (0.27, 0.10), (0.335, 0.22), (0.365, 0.36), (0.36, 0.48), (0.335, 0.60),
                 (0.31, 0.70), (0.305, 0.78), (0.32, 0.84), (0.345, 0.88)], 2),
    )
    rim = [(0.353, 0.891), (0.346, 0.9), (0.331, 0.899), (0.323, 0.888)]
    inner = catmull([(0.318, 0.87), (0.296, 0.80), (0.29, 0.72), (0.313, 0.60), (0.338, 0.46), (0.333, 0.33),
                     (0.30, 0.20), (0.24, 0.10), (0.16, 0.066), (0.07, 0.056)], 2)
    cup = Cup("Cup_Clay", outer, rim, inner)
    cup.roughness = 0.35
    chips = [(0.6, 0.30, 0.055), (2.9, 0.17, 0.03), (4.6, 0.38, 0.075)]

    def dr(theta, z, inner):
        return 0.004 * math.sin(2 * theta + 3 * z) + 0.003 * math.sin(5 * theta - 2 * z + 1.0)

    cup.dr = dr
    cup.rim_drop = lambda theta: chip_drop(theta, chips, 2.0)

    def texture(f, side_v):
        rng = np.random.default_rng(31)
        terra = col(0.70, 0.38, 0.22)
        terra_dark = col(0.52, 0.26, 0.15)
        glaze = col(0.10, 0.33, 0.36)
        glaze_light = col(0.20, 0.52, 0.52)
        n1 = zm.periodic_noise(rng, TEX, TEX, 30.0)
        n2 = zm.periodic_noise(rng, TEX, TEX, 3.0)
        clay = lerp(terra, terra_dark, np.clip(0.4 + 0.2 * n1 + 0.15 * n2, 0, 1))
        # следы гончарного круга
        clay = clay * (1.0 + 0.04 * np.sin(f.z * 260.0))[..., None]
        # глазурь: сверху снаружи до волнистой границы с потёками, внутри вся
        edge = 0.50 + 0.025 * np.sin(3 * f.theta) + 0.012 * zm.periodic_noise(rng, TEX, TEX, 20.0)
        drips = np.zeros_like(f.theta)
        for _ in range(9):
            c, w, ln = rng.uniform(0, 2 * np.pi), rng.uniform(0.04, 0.09), rng.uniform(0.04, 0.14)
            a = np.abs((f.theta - c + np.pi) % (2 * np.pi) - np.pi)
            prof = np.sqrt(np.clip(1 - (a / w) ** 2, 0, 1))
            drips = np.maximum(drips, prof * ln)
        glazed = np.where(f.outer, zm.smoothstep(edge - drips - 0.006, edge - drips + 0.006, f.z), 1.0)
        glazed = np.where(f.bottom, 0.0, glazed)
        gl = lerp(glaze, glaze_light, np.clip(0.4 + 0.12 * n1 + 0.2 * np.sin(f.z * 30) * 0.5, 0, 1))
        img = lerp(clay, gl, glazed)
        # край глазури толще и темнее
        lip = glazed * (1 - glazed) * 4
        img = lerp(img, glaze * 0.6, lip * 0.5)
        # сколы: глазури нет, свежая глина светлее
        cm = chip_mask(f.theta, f.z, chips) * f.side
        img = lerp(img, col(0.80, 0.52, 0.36), cm)
        cracks = np.zeros((TEX, TEX), dtype=np.float32)
        draw_cracks(cracks, rng, crack_starts_from_chips(cup, side_v, f, chips, 1, rng), length=260, width=2.0)
        img = lerp(img, col(0.05, 0.05, 0.05), cracks * f.side * 0.95)
        return inside_shade(f, img, 0.5)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 4. Бочонок
# ---------------------------------------------------------------------------

def make_barrel():
    staves = 16
    hoops = [(0.10, 0.165), (0.735, 0.80)]
    lift = 0.013

    def body(z):
        return 0.292 + 0.05 * math.sin(math.pi * z / H)

    def on_hoop(z):
        return any(a - 1e-4 <= z <= b + 1e-4 for a, b in hoops)

    zs = sorted(set([0.03 + 0.04 * k for k in range(22)] + [z for a, b in hoops for z in (a - 0.004, a, b, b + 0.004)]))
    zs = [z for z in zs if 0.025 <= z <= 0.88]
    outer = [(0.27, 0.0), (0.285, 0.006), (0.292, 0.02)]
    for z in zs:
        outer.append((body(z) + (lift if on_hoop(z) else 0.0), z))
    wall = 0.03
    top = body(H)
    rim = [(top, 0.893), (top - 0.006, 0.9), (top - wall + 0.006, 0.9), (top - wall, 0.893)]
    inner = [(body(z) - wall, z) for z in (0.87, 0.7, 0.5, 0.3, 0.15)]
    inner += [(body(0.1) - wall - 0.004, 0.09), (body(0.1) - wall - 0.012, 0.075), (0.15, 0.072), (0.06, 0.07)]
    cup = Cup("Cup_Barrel", outer, rim, inner)
    cup.smooth_angle = 35.0
    cup.seg = 32
    cup.roughness = 0.7
    cup.patches = [col(0.30, 0.29, 0.30)]
    rng = np.random.default_rng(5)
    stave_drop = [float(rng.uniform(0.0, 0.014)) for _ in range(staves)]
    stave_drop[5] = 0.055   # отколотая доска

    def stave_of(theta):
        return int((theta % (2 * math.pi)) / (2 * math.pi) * staves + 1e-6) % staves

    def dr(theta, z, inner):
        if on_hoop(z) and not inner:
            return 0.0
        pos = (theta / (2 * math.pi) * staves) % 1.0
        border = min(pos, 1 - pos) < 0.02
        return (-0.004 if inner else -0.007) if border else 0.0

    def rim_drop(theta):
        # граница досок попадает на вершину: берём доску справа, чтобы ступенька была резкой
        return stave_drop[stave_of(theta + 1e-3)]

    cup.dr, cup.rim_drop = dr, rim_drop
    cup.stave_of = stave_of

    def rivets(c, bm):
        for a0, b0 in hoops:
            zc = (a0 + b0) / 2
            for k in range(8):
                a = 2 * math.pi * (k + 0.5) / 8
                p, nrm = surface_point(c, a, zc, -0.002)
                add_dome(bm, p, nrm, 0.012, 0.008, segs=6)
        return 0

    cup.extras = [rivets]

    def texture(f, side_v):
        r = np.random.default_rng(7)
        wood_l = col(0.62, 0.41, 0.22)
        wood_d = col(0.36, 0.21, 0.10)
        st = ((f.theta / (2 * np.pi)) * staves).astype(int) % staves
        tint = np.array([r.uniform(-0.12, 0.12) for _ in range(staves)])[st]
        warp = zm.periodic_noise(r, TEX, TEX, 20.0, scale_v=200.0)
        grain = 0.5 + 0.5 * np.sin(2 * np.pi * ((f.theta / (2 * np.pi)) * staves * 5 + 0.6 * warp))
        grain = grain ** 3
        img = lerp(wood_l, wood_d, np.clip(0.35 * grain + 0.25 + tint, 0, 1))
        pos = (f.theta / (2 * np.pi) * staves) % 1.0
        seam = 1 - zm.smoothstep(0.0, 0.03, np.minimum(pos, 1 - pos))
        img = lerp(img, wood_d * 0.4, seam * f.side * 0.8)
        # обручи: железо с ржавчиной
        hoop = np.zeros_like(f.z)
        for a0, b0 in hoops:
            hoop = np.maximum(hoop, f.outer * zm.smoothstep(a0 - 0.003, a0 + 0.001, f.z) * (1 - zm.smoothstep(b0 - 0.001, b0 + 0.003, f.z)))
        rust_n = zm.periodic_noise(r, TEX, TEX, 8.0)
        iron = lerp(col(0.24, 0.24, 0.26), col(0.46, 0.25, 0.11), zm.smoothstep(0.3, 1.4, rust_n))
        img = lerp(img, iron, hoop)
        img = np.where((f.floor)[..., None], lerp(wood_l, wood_d, np.clip(0.5 + 0.3 * grain, 0, 1)), img)
        return inside_shade(f, img, 0.55)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 5. Кокос
# ---------------------------------------------------------------------------

def make_coconut():
    outer = join(
        [(0.12, 0.0), (0.15, 0.006)],
        catmull([(0.15, 0.006), (0.20, 0.03), (0.27, 0.09), (0.33, 0.20), (0.365, 0.34), (0.372, 0.48),
                 (0.357, 0.62), (0.332, 0.74), (0.306, 0.84), (0.29, 0.884)], 2),
    )
    rim = [(0.285, 0.894), (0.275, 0.9), (0.263, 0.897), (0.258, 0.887)]
    inner = catmull([(0.262, 0.87), (0.30, 0.74), (0.335, 0.60), (0.347, 0.46), (0.337, 0.32), (0.30, 0.20),
                     (0.24, 0.12), (0.16, 0.08), (0.07, 0.066)], 2)
    cup = Cup("Cup_Coconut", outer, rim, inner)
    cup.roughness = 0.85
    cup.patches = [col(0.72, 0.60, 0.40)]

    def dr(theta, z, inner):
        if inner:
            return 0.0
        return 0.007 * math.sin(3 * theta) * math.sin(4 * z + 1.0) + 0.003 * math.sin(9 * theta + 2 * z)

    def rim_drop(theta):
        return 0.022 * (0.5 + 0.5 * math.sin(3 * theta + 1.0)) + 0.008 * (0.5 + 0.5 * math.sin(8 * theta + 2.0))

    cup.dr, cup.rim_drop = dr, rim_drop

    def rope(c, bm):
        zc, seg_a, seg_b, rm = 0.60, 48, 6, 0.017
        rows = []
        for a_i in range(seg_a):
            a = 2 * math.pi * a_i / seg_a
            big = c.outer_radius(a, zc) + rm * 0.6
            d = Vector((math.cos(a), math.sin(a), 0))
            ring = []
            for b_i in range(seg_b):
                b = 2 * math.pi * b_i / seg_b
                rr = rm * (1 + 0.28 * math.sin(3 * b - 18 * a))
                ring.append(bm.verts.new(d * (big + rr * math.cos(b)) + Vector((0, 0, zc + rr * math.sin(b)))))
            rows.append(ring)
        for a_i in range(seg_a):
            r0, r1 = rows[a_i], rows[(a_i + 1) % seg_a]
            for b_i in range(seg_b):
                bm.faces.new((r0[b_i], r1[b_i], r1[(b_i + 1) % seg_b], r0[(b_i + 1) % seg_b]))
        return 0

    cup.extras = [rope]

    def texture(f, side_v):
        r = np.random.default_rng(41)
        brown = col(0.38, 0.23, 0.12)
        brown_d = col(0.22, 0.13, 0.07)
        hair = col(0.58, 0.40, 0.22)
        fib = zm.periodic_noise(r, TEX, TEX, 1.0, scale_v=25.0)
        fib2 = zm.periodic_noise(r, TEX, TEX, 3.0, scale_v=60.0)
        blot = zm.periodic_noise(r, TEX, TEX, 40.0)
        img = lerp(brown, brown_d, np.clip(0.45 + 0.2 * fib2 + 0.15 * blot, 0, 1))
        img = lerp(img, hair, zm.smoothstep(1.2, 2.2, fib) * 0.7)
        # три «глазка» на донышке
        for k in range(3):
            a = 2 * np.pi * k / 3 + 0.4
            ex, ey = DISC_OUT[0] + 0.35 * DISC_R * np.cos(a), DISC_OUT[1] + 0.35 * DISC_R * np.sin(a)
            d = np.sqrt((f.u - ex) ** 2 + (f.v - ey) ** 2)
            img = lerp(img, col(0.09, 0.05, 0.03), (1 - zm.smoothstep(0.008, 0.016, d)) * f.bottom)
        # внутри: белая мякоть у края, глубже коричневая скорлупа
        flesh = col(0.94, 0.92, 0.85)
        shell_in = col(0.46, 0.32, 0.20)
        inner_col = lerp(shell_in, flesh, zm.smoothstep(0.70, 0.80, f.z))
        inner_col = inner_col * (1.0 + 0.05 * fib2)[..., None]
        top = f.side & (f.z > H - 0.03) & ~f.outer
        img = np.where((f.inner | f.floor)[..., None], inner_col, img)
        img = lerp(img, flesh, (f.side & (f.z > H - 0.012)) * 0.9)
        # тонкая белая полоска мякоти по срезу снаружи
        img = lerp(img, flesh, f.outer * zm.smoothstep(H - 0.03, H - 0.012, f.z) * 0.7)
        img = np.where(f.floor[..., None], shell_in, img)
        return inside_shade(f, img, 0.45)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 6. Медная корабельная кружка
# ---------------------------------------------------------------------------

def make_copper():
    bands = [(0.16, 0.20), (0.70, 0.74)]
    lift = 0.006

    def body(z):
        return 0.30 + 0.025 * (z - 0.10) / 0.70

    zs = sorted(set([0.10 + 0.04 * k for k in range(18)] + [z for a, b in bands for z in (a - 0.003, a, b, b + 0.003)]))
    zs = [z for z in zs if 0.10 <= z <= 0.80]
    outer = join(
        [(0.33, 0.0), (0.36, 0.008), (0.362, 0.022), (0.35, 0.032), (0.33, 0.045)],
        catmull([(0.33, 0.045), (0.31, 0.07), (0.30, 0.10)], 3),
        [(body(z) + (lift if any(a - 1e-4 <= z <= b + 1e-4 for a, b in bands) else 0.0), z) for z in zs],
        [(0.33, 0.84)],
    )
    rim = [(0.342, 0.865), (0.348, 0.885), (0.342, 0.9), (0.33, 0.9), (0.322, 0.89)]
    inner = [(0.318, 0.87), (0.31, 0.6), (0.30, 0.35), (0.292, 0.12), (0.285, 0.07), (0.27, 0.055), (0.15, 0.05), (0.06, 0.05)]
    cup = Cup("Cup_Copper", outer, rim, inner)
    cup.metallic, cup.roughness = 0.85, 0.4
    cup.patches = [col(0.80, 0.62, 0.30)]
    dents = [(0.9, 0.45, 0.07, 0.022), (3.6, 0.32, 0.06, 0.018), (4.9, 0.56, 0.05, 0.016), (2.3, 0.64, 0.045, 0.012)]
    cup.dents = dents

    def dent_amount(theta, z):
        total = 0.0
        for c, zc, size, depth in dents:
            a = (theta - c + math.pi) % (2 * math.pi) - math.pi
            d2 = (a * 0.31) ** 2 + (z - zc) ** 2
            total += depth * math.exp(-d2 / (size * size))
        return total

    cup.dr = lambda theta, z, inner: -dent_amount(theta, z) if 0.08 < z < 0.84 else 0.0

    def rivets(c, bm):
        for k in range(8):
            z = 0.25 + 0.055 * k
            if z > 0.66:
                break
            p, nrm = surface_point(c, math.pi, z, -0.001)
            add_dome(bm, p, nrm, 0.011, 0.007, segs=6)
        for a0, b0 in bands:
            for k in range(10):
                a = 2 * math.pi * k / 10 + 0.2
                p, nrm = surface_point(c, a, (a0 + b0) / 2, -0.001)
                add_dome(bm, p, nrm, 0.010, 0.006, segs=6)
        return 0

    cup.extras = [rivets]

    def texture(f, side_v):
        r = np.random.default_rng(53)
        copper = col(0.80, 0.45, 0.26)
        tarnish = col(0.45, 0.24, 0.14)
        patina = col(0.30, 0.62, 0.52)
        n1 = zm.periodic_noise(r, TEX, TEX, 25.0)
        n2 = zm.periodic_noise(r, TEX, TEX, 4.0)
        scratch = zm.periodic_noise(r, TEX, TEX, 0.8, scale_v=12.0)
        img = lerp(copper, tarnish, np.clip(0.30 + 0.15 * n1 + 0.05 * n2, 0, 1))
        img = img * (1.0 + 0.06 * np.clip(scratch, -1, 2))[..., None]
        # вмятины темнее
        dent = np.zeros_like(f.z)
        for c, zc, size, depth in dents:
            a = (f.theta - c + np.pi) % (2 * np.pi) - np.pi
            dent += np.exp(-((a * 0.31) ** 2 + (f.z - zc) ** 2) / (size * size)) * depth / 0.02
        img = lerp(img, tarnish * 0.7, np.clip(dent, 0, 1) * f.outer * 0.6)
        # шов: край листа
        a = np.abs((f.theta - np.pi + np.pi) % (2 * np.pi) - np.pi)
        seam = (1 - zm.smoothstep(0.0, 0.012, np.abs(a - 0.05))) * f.outer * (f.z > 0.1) * (f.z < 0.84)
        img = lerp(img, tarnish * 0.5, seam * 0.8)
        # патина: в швах, у поясов, у дна, пятнами
        near_band = np.zeros_like(f.z)
        for a0, b0 in bands:
            near_band = np.maximum(near_band, np.exp(-((f.z - a0) / 0.012) ** 2) + np.exp(-((f.z - b0) / 0.012) ** 2))
        pm = np.clip(zm.smoothstep(1.6, 2.6, n2 + 0.8 * n1) * 0.7 + near_band * 0.6 + (1 - zm.smoothstep(0.03, 0.1, f.z)) * 0.7 + seam, 0, 1)
        img = lerp(img, patina, pm * f.outer * 0.85)
        # начищенный обод
        img = lerp(img, col(0.95, 0.62, 0.40), zm.smoothstep(H - 0.04, H - 0.01, f.z) * f.side * 0.7)
        return inside_shade(f, img, 0.5)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 7. Каменный восьмигранник с рунами
# ---------------------------------------------------------------------------

def make_stone():
    outer = join(
        [(0.31, 0.0), (0.326, 0.008), (0.33, 0.05), (0.316, 0.07), (0.30, 0.10)],
        [(0.30 + 0.015 * (z - 0.10) / 0.75, z) for z in [0.15 + 0.05 * k for k in range(15)]],
    )
    rim = [(0.312, 0.893), (0.305, 0.9), (0.275, 0.9), (0.268, 0.893)]
    inner = [(0.265, 0.87), (0.258, 0.6), (0.25, 0.30), (0.246, 0.12), (0.236, 0.092), (0.22, 0.082), (0.10, 0.08)]
    cup = Cup("Cup_Stone", outer, rim, inner)
    cup.facets = 8
    cup.seg = 48
    cup.smooth_angle = 30.0
    cup.roughness = 0.9
    step = 2 * math.pi / 8
    chips = [(step * 1.5, 0.30, 0.06), (step * 5.5, 0.20, 0.035)]
    corner_chips = [(step * 3.5, 0.30, 0.42, 0.016), (step * 6.5, 0.55, 0.64, 0.012), (step * 0.5, 0.05, 0.12, 0.012)]

    def dr(theta, z, inner):
        if inner:
            return 0.0
        total = 0.002 * math.sin(5 * theta + 9 * z)
        for c, z0, z1, d in corner_chips:
            a = abs((theta - c + math.pi) % (2 * math.pi) - math.pi)
            if a < 0.14 and z0 <= z <= z1:
                total -= d * (1 - a / 0.14)
        return total

    cup.dr = dr
    cup.rim_drop = lambda theta: chip_drop(theta, chips, 4.0)

    def texture(f, side_v):
        r = np.random.default_rng(67)
        stone = col(0.52, 0.51, 0.48)
        stone_d = col(0.33, 0.32, 0.31)
        moss = col(0.30, 0.40, 0.16)
        n1 = zm.periodic_noise(r, TEX, TEX, 40.0)
        n2 = zm.periodic_noise(r, TEX, TEX, 5.0)
        speck = zm.periodic_noise(r, TEX, TEX, 0.8)
        img = lerp(stone, stone_d, np.clip(0.4 + 0.2 * n1 + 0.15 * n2, 0, 1))
        img = lerp(img, col(0.75, 0.74, 0.70), zm.smoothstep(1.6, 2.4, speck) * 0.6)
        img = lerp(img, col(0.15, 0.15, 0.15), zm.smoothstep(1.8, 2.6, -speck) * 0.6)
        # рёбра граней светлее (потёртые)
        stp = 2 * np.pi / 8
        m = (f.theta - stp / 2) % stp
        dc = np.minimum(m, stp - m)
        img = lerp(img, col(0.66, 0.65, 0.61), (1 - zm.smoothstep(0.0, 0.04, dc)) * f.outer * 0.5)
        # руны: по одной на грань, вырезаны (тёмные бороздки)
        runes = np.zeros((TEX, TEX), dtype=np.float32)
        v0 = f.v_of(cup, side_v, 0.42, False) * TEX
        v1 = f.v_of(cup, side_v, 0.66, False) * TEX
        for k in range(8):
            cx = (k / 8.0) * TEX
            hw = TEX / 8 * 0.22
            pts = [(cx + r.uniform(-hw, hw), r.uniform(v0, v1)) for _ in range(4)]
            strokes = [(pts[0], pts[1]), (pts[1], pts[2]), (pts[0], pts[3])]
            stem_x = cx + r.uniform(-hw * 0.3, hw * 0.3)
            strokes.append(((stem_x, v0), (stem_x, v1)))
            for (x0, y0), (x1, y1) in strokes:
                steps = int(max(abs(x1 - x0), abs(y1 - y0)))
                for s in range(steps + 1):
                    t = s / max(steps, 1)
                    x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                    for dy in range(-2, 3):
                        for dx in range(-2, 3):
                            yy, xx = int(y) + dy, (int(x) + dx) % TEX
                            if 0 <= yy < TEX:
                                runes[yy, xx] = max(runes[yy, xx], 1.0 - math.hypot(dx, dy) / 3.0)
        img = lerp(img, col(0.12, 0.12, 0.13), runes * 0.9)
        # мох снизу и в углублениях
        mm = np.clip((1 - zm.smoothstep(0.05, 0.30, f.z)) * zm.smoothstep(0.0, 1.0, n1 + 0.5 * n2 + 0.3), 0, 1) * f.outer
        img = lerp(img, moss, mm * 0.8)
        cm = chip_mask(f.theta, f.z, chips) * f.side
        img = lerp(img, col(0.68, 0.67, 0.64), cm * 0.8)
        cracks = np.zeros((TEX, TEX), dtype=np.float32)
        starts = crack_starts_from_chips(cup, side_v, f, chips, 1, r)
        draw_cracks(cracks, r, starts, length=220, width=1.6)
        img = lerp(img, col(0.08, 0.08, 0.08), cracks * f.side * 0.9)
        return inside_shade(f, img, 0.5)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 8. Рог на подставке
# ---------------------------------------------------------------------------

def make_horn():
    # Рог изогнут: ось смещается в сторону книзу (s(z)), острый кончик упирается в пол, рог держит
    # кованая подставка — кольцо и три ножки. Полость — только в широкой верхней части.
    def shift(z):
        return 0.27 * max(0.0, 1.0 - z / 0.72) ** 2

    outer = join(
        [(0.025, 0.0), (0.05, 0.05)],
        catmull([(0.05, 0.05), (0.10, 0.14), (0.15, 0.26), (0.21, 0.40), (0.27, 0.55), (0.32, 0.70), (0.355, 0.82),
                 (0.365, 0.875)], 2),
    )
    rim = [(0.372, 0.886), (0.368, 0.898), (0.356, 0.9), (0.346, 0.893)]
    inner = catmull([(0.342, 0.875), (0.325, 0.78), (0.29, 0.68), (0.25, 0.60), (0.19, 0.53), (0.12, 0.497), (0.08, 0.49)], 2)
    cup = Cup("Cup_Horn", outer, rim, inner)
    cup.roughness = 0.4
    cup.patches = [col(0.22, 0.21, 0.23)]
    cup.bottom_center = (shift(0.0), 0.0)
    cup.dr = lambda theta, z, inner: shift(z) * math.cos(theta) + (0.0 if inner else 0.003 * math.sin(z * 70.0))

    def stand(c, bm):
        zr = 0.42
        cx = shift(zr)
        ring_r = c.outer_radius(0.0, zr) - cx + 0.02
        path = [Vector((cx + ring_r * math.cos(2 * math.pi * k / 16), ring_r * math.sin(2 * math.pi * k / 16), zr)) for k in range(17)]
        add_tube(bm, path, [0.016] * len(path), sides=5, caps=False)
        for k in range(3):
            a = 2 * math.pi * k / 3 + math.pi / 3
            d = Vector((math.cos(a), math.sin(a), 0))
            top = Vector((cx, 0, zr)) + d * ring_r
            knee = Vector((cx, 0, 0.18)) + d * (ring_r + 0.07)
            foot = Vector((cx, 0, 0.012)) + d * (ring_r + 0.12)
            pts, radii = [], []
            for s in range(6):
                t = s / 5
                pts.append(top * (1 - t) ** 2 + knee * 2 * t * (1 - t) + foot * t * t)
                radii.append(0.016 - 0.004 * t)
            add_tube(bm, pts, radii, sides=5)
            add_dome(bm, foot + Vector((0, 0, 0.002)), Vector((0, 0, 1)), 0.03, 0.014, segs=6)   # шар-лапка: низ ровно на полу
        return 0

    cup.extras = [stand]

    def texture(f, side_v):
        r = np.random.default_rng(71)
        ivory = col(0.93, 0.86, 0.68)
        amber = col(0.72, 0.48, 0.22)
        dark = col(0.22, 0.13, 0.07)
        n1 = zm.periodic_noise(r, TEX, TEX, 30.0)
        streak = zm.periodic_noise(r, TEX, TEX, 2.0, scale_v=60.0)
        # к кончику темнее: слоновая кость → янтарь → почти чёрный
        t = 1 - zm.smoothstep(0.05, 0.85, f.z)
        img = lerp(ivory, amber, np.clip(t * 1.4 + 0.08 * n1, 0, 1))
        img = lerp(img, dark, zm.smoothstep(0.55, 1.0, t))
        img = img * (1.0 + 0.03 * np.clip(streak, -1, 2))[..., None]
        # кольца роста
        img = lerp(img, amber * 0.75, f.outer * zm.smoothstep(0.9, 0.98, np.abs(np.sin(f.z * 45.0))) * 0.5)
        # серебряная оковка у края с гравировкой
        band = f.outer * zm.smoothstep(0.835, 0.84, f.z)
        silver = lerp(col(0.82, 0.83, 0.86), col(0.5, 0.52, 0.56), np.clip(0.4 + 0.2 * n1, 0, 1))
        img = lerp(img, silver, band)
        wave = np.abs(np.sin(f.theta * 10 + 3.0 * np.sin(f.z * 90.0)))
        img = lerp(img, col(0.3, 0.3, 0.33), band * (1 - zm.smoothstep(0.0, 0.14, wave)) * 0.7)
        img = lerp(img, col(0.3, 0.3, 0.33), f.outer * (1 - zm.smoothstep(0.002, 0.005, np.abs(f.z - 0.84))))
        img = np.where((f.inner | f.floor)[..., None], lerp(amber, dark, np.clip(f.depth * 0.8, 0, 1)), img)
        img = lerp(img, silver, f.side * zm.smoothstep(H - 0.03, H - 0.005, f.z))
        return inside_shade(f, img, 0.4)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 9. Плетёный
# ---------------------------------------------------------------------------

def make_wicker():
    # Корзинка из лозы: 12 стоек, между ними прутья рядами «над-под» (ряды сдвинуты через один),
    # толстый плетёный обод. Рельеф плетения — небольшой, основное — в текстуре.
    stakes, row_h = 12, 0.045

    def body(z):
        return 0.27 + 0.07 * z / H

    zs = [0.04 + row_h * k for k in range(19)]
    outer = [(0.24, 0.0), (0.26, 0.015)] + [(body(z), z) for z in zs]
    rim = [(0.372, 0.868), (0.386, 0.882), (0.378, 0.897), (0.358, 0.9), (0.342, 0.89)]
    inner = [(0.322, 0.87), (0.312, 0.66), (0.296, 0.42), (0.282, 0.20), (0.272, 0.095), (0.25, 0.08), (0.12, 0.075), (0.05, 0.075)]
    cup = Cup("Cup_Wicker", [(r, z) for r, z in outer], rim, inner)
    # simplify убрал бы ряды плетения — профиль наружу задаём без упрощения
    cup.profile = join(outer, rim, simplify(inner))
    cup.rim_index = len(outer) + len(rim) // 2 - 1
    cup.floor = cup.profile[-1][1]
    cup.seg = 36
    cup.smooth_angle = 60.0
    cup.roughness = 0.85

    def dr(theta, z, inner):
        if inner:
            return 0.0
        if z > 0.86:
            return 0.006 * math.sin(24 * theta + z * 200.0)   # коса обода
        if z < 0.03:
            return 0.0
        band = int((z - 0.04) / row_h + 0.5)
        return 0.006 * math.cos(stakes * theta + math.pi * (band % 2))

    cup.dr = dr

    def texture(f, side_v):
        r = np.random.default_rng(83)
        straw = col(0.82, 0.64, 0.34)
        straw_d = col(0.52, 0.36, 0.16)
        gap = col(0.18, 0.11, 0.05)
        n1 = zm.periodic_noise(r, TEX, TEX, 20.0)
        fib = zm.periodic_noise(r, TEX, TEX, 1.0, scale_v=15.0)
        band = np.floor((f.z - 0.04) / row_h + 0.5)
        pos_in_band = ((f.z - 0.04) / row_h + 0.5) % 1.0
        # прут ряда то снаружи (над стойкой), то уходит за стойку; поперёк прута — круглый (светлее посередине)
        over = 0.5 + 0.5 * np.cos(stakes * f.theta + np.pi * (band % 2))
        round_ = np.sin(np.pi * np.clip(pos_in_band, 0, 1)) ** 0.6
        img = lerp(straw_d, straw, np.clip(0.15 + 0.55 * round_ * (0.45 + 0.55 * over) + 0.05 * n1, 0, 1))
        img = img * (1.0 + 0.04 * np.clip(fib, -1, 2))[..., None]
        # там, где прут уходит назад, видна вертикальная стойка
        stake_pos = (f.theta / (2 * np.pi) * stakes) % 1.0
        stake_dist = np.abs(stake_pos - np.where(band % 2 == 0, 0.5, 0.0))
        stake_dist = np.minimum(stake_dist, 1 - stake_dist)
        stake = (1 - zm.smoothstep(0.08, 0.12, stake_dist)) * (1 - over)
        stake_col = lerp(straw_d, straw * 0.95, 0.5 + 0.5 * np.cos(np.pi * stake_dist / 0.12))
        img = lerp(img, stake_col, stake * f.side * (f.z > 0.03) * (f.z < 0.86))
        # тонкие тёмные щели между рядами
        row_gap = 1 - zm.smoothstep(0.0, 0.06, np.minimum(pos_in_band, 1 - pos_in_band))
        img = lerp(img, gap, row_gap * f.side * (f.z > 0.03) * (f.z < 0.86) * 0.7)
        # коса обода: косые полосы
        braid = 0.5 + 0.5 * np.sin(24 * f.theta + f.z * 200.0)
        img = np.where((f.side & (f.z > 0.86))[..., None], lerp(straw_d, straw, braid), img)
        # донышко: спицы и спираль
        spokes = 0.5 + 0.5 * np.cos(f.theta * stakes)
        spiral = 0.5 + 0.5 * np.sin(f.r * 120.0 + f.theta)
        disc = lerp(straw_d, straw, np.clip(0.3 + 0.4 * spiral * spokes + 0.2 * spiral, 0, 1))
        img = np.where((f.bottom | f.floor)[..., None], disc, img)
        return inside_shade(f, img, 0.55)

    cup.texture = texture
    return cup


# ---------------------------------------------------------------------------
# 10. Кристальный
# ---------------------------------------------------------------------------

def make_crystal():
    # Шестигранный аметист: каменное основание, грани расходятся вверх, светлые руны на гранях, золотой обод,
    # у основания — щётка кристаллов-друз.
    outer = join(
        [(0.27, 0.0), (0.30, 0.01), (0.31, 0.05), (0.29, 0.085)],
        [(0.29 + 0.07 * (z - 0.1) / 0.76, z) for z in [0.1 + 0.076 * k for k in range(11)]],
    )
    rim = [(0.37, 0.88), (0.372, 0.894), (0.362, 0.9), (0.33, 0.9), (0.322, 0.892)]
    inner = [(0.318, 0.87), (0.30, 0.6), (0.276, 0.3), (0.26, 0.12), (0.245, 0.095), (0.22, 0.088), (0.08, 0.088)]
    cup = Cup("Cup_Crystal", outer, rim, inner)
    cup.facets = 6
    cup.seg = 36
    cup.smooth_angle = 25.0
    cup.roughness = 0.25
    cup.patches = [col(0.62, 0.36, 0.85), col(0.80, 0.62, 0.98)]

    def druse(c, bm):
        r = np.random.default_rng(97)
        for k in range(9):
            a = 2 * math.pi * k / 9 + float(r.uniform(-0.2, 0.2))
            z0 = float(r.uniform(0.02, 0.12))
            base, nrm = surface_point(c, a, z0 + 0.06, -0.02)
            d = (nrm + Vector((0, 0, float(r.uniform(0.6, 1.2))))).normalized()
            length = float(r.uniform(0.12, 0.22))
            w = float(r.uniform(0.028, 0.04))
            add_tube(bm, [base, base + d * length * 0.75, base + d * length], [w, w * 0.9, 0.002], sides=6)
        return 0

    def druse_light(c, bm):
        r = np.random.default_rng(101)
        for k in range(5):
            a = 2 * math.pi * k / 5 + 0.35
            base, nrm = surface_point(c, a, 0.05, -0.015)
            d = (nrm * 1.2 + Vector((0, 0, 1))).normalized()
            length = float(r.uniform(0.08, 0.13))
            add_tube(bm, [base, base + d * length * 0.7, base + d * length], [0.022, 0.02, 0.002], sides=6)
        return 1

    cup.extras = [druse, druse_light]

    def texture(f, side_v):
        r = np.random.default_rng(89)
        violet = col(0.48, 0.24, 0.70)
        violet_d = col(0.24, 0.10, 0.40)
        lilac = col(0.86, 0.72, 1.0)
        rock = col(0.36, 0.33, 0.32)
        n1 = zm.periodic_noise(r, TEX, TEX, 30.0)
        n2 = zm.periodic_noise(r, TEX, TEX, 4.0)
        img = lerp(violet_d, violet, np.clip(0.3 + 0.5 * zm.smoothstep(0.1, 0.85, f.z) + 0.04 * n1, 0, 1))
        # грани: светлый блик у одного края грани, рёбра светлые
        stp = 2 * np.pi / 6
        m = (f.theta - stp / 2) % stp
        img = lerp(img, lilac, (m / stp) ** 3 * f.outer * 0.45)
        dc = np.minimum(m, stp - m)
        img = lerp(img, lilac, (1 - zm.smoothstep(0.0, 0.035, dc)) * f.outer * 0.7)
        # прожилки
        img = lerp(img, lilac, zm.smoothstep(2.2, 2.6, n2 + 0.5 * n1) * 0.25)
        # каменное основание
        img = lerp(img, rock * (1.0 + 0.1 * n2)[..., None], (1 - zm.smoothstep(0.07, 0.1, f.z)) * f.outer)
        # руны: по одной на грань, светлые
        runes = np.zeros((TEX, TEX), dtype=np.float32)
        v0 = f.v_of(cup, side_v, 0.4, False) * TEX
        v1 = f.v_of(cup, side_v, 0.62, False) * TEX
        for k in range(6):
            cx = ((k + 0.5) / 6.0) * TEX
            hw = TEX / 6 * 0.2
            pts = [(cx + r.uniform(-hw, hw), r.uniform(v0, v1)) for _ in range(4)]
            strokes = [(pts[0], pts[1]), (pts[1], pts[2]), (pts[2], pts[3]), ((cx, v0), (cx, v1))]
            for (x0, y0), (x1, y1) in strokes:
                steps = int(max(abs(x1 - x0), abs(y1 - y0)))
                for s in range(steps + 1):
                    t = s / max(steps, 1)
                    x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                    for dy in range(-2, 3):
                        for dx in range(-2, 3):
                            yy, xx = int(y) + dy, (int(x) + dx) % TEX
                            if 0 <= yy < TEX:
                                runes[yy, xx] = max(runes[yy, xx], 1.0 - math.hypot(dx, dy) / 3.0)
        img = lerp(img, col(0.97, 0.92, 1.0), runes * f.outer * 0.95)
        # золотой обод
        img = lerp(img, col(0.98, 0.78, 0.35), f.side * zm.smoothstep(H - 0.03, H - 0.018, f.z))
        img = np.where(f.inner[..., None], lerp(violet, violet_d, f.depth), img)
        img = np.where(f.floor[..., None], violet_d, img)
        img = np.where(f.bottom[..., None], rock, img)
        return inside_shade(f, img, 0.35)

    cup.texture = texture
    return cup


CUPS = [make_goblet, make_bone, make_clay, make_barrel, make_coconut, make_copper, make_stone,
        make_horn, make_wicker, make_crystal]


# ---------------------------------------------------------------------------
# Сборка, проверка, экспорт
# ---------------------------------------------------------------------------

def build(cup):
    mesh, side_v = build_mesh(cup)
    fields = Fields(cup, side_v)
    img = cup.texture(fields, side_v)
    image = finish_texture(cup, img)
    mat = zm.make_material(cup.name, image)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = cup.metallic
    bsdf.inputs["Roughness"].default_value = cup.roughness
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(cup.name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    mouth = bpy.data.objects.new("Mouth", None)
    mouth.location = (0.0, 0.0, H)
    mouth.parent = obj
    bpy.context.scene.collection.objects.link(mouth)
    # Inside: высота — дно, удаление от оси — свободный радиус (по оси Blender X).
    inside = bpy.data.objects.new("Inside", None)
    inside.location = (cup.inner_free_radius(), 0.0, cup.floor)
    inside.parent = obj
    bpy.context.scene.collection.objects.link(inside)
    return obj


def check(cup, obj):
    """Проверка: высота 0.9, низ на 0, полость открыта (луч сверху по оси доходит до дна)."""
    mesh = obj.data
    zs = [v.co.z for v in mesh.vertices]
    tris = zm.triangle_count(mesh)
    hollow = True
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    hit = tree.ray_cast(Vector((0, 0, 2.0)), Vector((0, 0, -1)))
    hollow = hit[0] is not None and abs(hit[0].z - cup.floor) < 0.02
    ok = abs(max(zs) - H) < 0.01 and abs(min(zs)) < 0.01 and hollow
    width = 2 * max(math.hypot(v.co.x, v.co.y) for v in mesh.vertices)
    print("  %-12s треуг. %5d  высота %.3f  ширина %.2f  дно %.3f  свободный радиус %.3f  %s" % (
        cup.name, tris, max(zs) - min(zs), width, cup.floor, cup.inner_free_radius(),
        "OK" if ok else "ПРОВЕРИТЬ (полость/размер)"))
    return ok


def export(obj):
    path = os.path.join(MODELS_DIR, obj.name + ".fbx")
    zm.export_fbx(obj, path, with_children=True)


def render(out_dir, objs):
    """Превью: все стаканы в ряд, кость рядом с каждым; два ракурса."""
    scene = bpy.context.scene
    die_path = os.path.join(PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models", "Die.fbx")
    spacing = 1.0
    for k, obj in enumerate(objs):
        obj.location = ((k - (len(objs) - 1) / 2) * spacing, 0, 0)
    if os.path.exists(die_path):
        for k in range(len(objs)):
            bpy.ops.import_scene.fbx(filepath=die_path)
            die = bpy.context.selected_objects[0]
            die.location = ((k - (len(objs) - 1) / 2) * spacing + 0.15, -0.55, 0.15)
            die.rotation_euler = (0, 0, 0.4)
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    fo = bpy.data.objects.new("Floor", floor)
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.32, 0.25, 1)
    floor.materials.append(fm)
    scene.collection.objects.link(fo)

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.58, 0.62, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.5
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(sun)

    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_gtao = True
    scene.eevee.use_soft_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 2400, 640
    scene.view_settings.view_transform = "Standard"

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    width = max(len(objs), 6) * spacing   # при --only с парой стаканов камера не подходит вплотную
    for name, elev, dist_k in (("cups_side.png", 8, 1.45), ("cups_top.png", 42, 1.5)):
        e = math.radians(elev)
        dist = width * dist_k
        cam.location = (0, -dist * math.cos(e), 0.45 + dist * math.sin(e))
        target = Vector((0, 0, 0.42))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name)
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None

    zm.reset_scene()
    os.makedirs(MODELS_DIR, exist_ok=True)
    os.makedirs(TEXTURES_DIR, exist_ok=True)
    objs, all_ok = [], True
    print("Стаканы:")
    for make in CUPS:
        cup = make()
        if only is not None and cup.name not in only:
            continue
        obj = build(cup)
        all_ok &= check(cup, obj)
        export(obj)
        for child in obj.children:
            child.name = child.name + "_" + obj.name   # следующий стакан снова получит "Mouth" и "Inside"
        objs.append(obj)
    if render_dir:
        os.makedirs(render_dir, exist_ok=True)
        render(render_dir, objs)
    if not all_ok:
        raise SystemExit("Есть стаканы с ошибками")


if __name__ == "__main__":
    main()
