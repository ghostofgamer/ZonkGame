# -*- coding: utf-8 -*-
"""
Карты нормалей для стаканов «Зонка» из уже нарисованных текстур.

Рисунок текстур — комикс: ровные цветовые пятна и тёмные линии (швы, трещины, резьба, щели досок, плетение).
Рельеф берётся оттуда же:
  * тёмные тонкие линии — канавки (линия темнее своего размытого окружения);
  * средний рельеф — разница двух размытий яркости (доски, прутья, обручи чуть выпуклые), слабее канавок.
Мелкий шум не берётся (иначе ступенчатый свет Zonk/Toon пойдёт рябью). Карта — вдвое меньше цвета
(стаканы и столы 512, безделушки 256): рельеф крупный, мелкие мип-уровни ему не нужны, вес небольшой.

Запуск (фоном):
    blender --background --factory-startup --python Tools/Blender/zonk_normals.py
    blender --background --factory-startup --python Tools/Blender/zonk_normals.py -- --only Cup_Wicker,Table_Stone
    blender --background --factory-startup --python Tools/Blender/zonk_normals.py -- --render <папка> --only Cup_Wicker
Результат: Art/Textures/<Cups|Tables|Decor>/<Имя>_N.png рядом с цветом. Касательное пространство OpenGL (+Y — вверх по V),
как в Unity. Подключает генератор (ZonkSetup.NormalMaps): тип Normal Map, Crunch, материал с _BumpMap.
"""

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import zonk_models as zm  # noqa: E402

TEXTURES = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Textures")
MODELS = os.path.join(zm.PROJECT_ROOT, "Assets", "ZonkContent", "Art", "Models")

# Вид предметов: папка и префикс файлов. Сила — наклон стенки канавки (больше — глубже).
# Только стаканы: на столах и безделушках (пробовали 05.10) разницы на глаз нет, а вес растёт.
KINDS = (
    ("Cups", "Cup_", 1.0),
)

GROOVE_BLUR = 3.0      # окружение линии, пикселей цвета 1024
GROOVE_CONTRAST = 0.12  # насколько линия темнее окружения, чтобы стать канавкой во всю глубину
BROAD_FINE = 2.0       # средний рельеф: разница размытий 2 и 10 пикселей
BROAD_COARSE = 10.0
BROAD_AMOUNT = 0.35    # доля среднего рельефа против канавок
STRENGTH = 2.2         # наклон: высота 1 на пиксель карты


def load(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[:, :, :3]


def blur(a, sigma):
    """Гауссово размытие по кругу (FFT): атлас бесшовный по краям не везде, но край атласа на модели не виден."""
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    kernel = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * kernel))


def height_map(rgb):
    """Высота 0 — поверхность, минус — канавки. Масштаб пикселей приведён к цвету 1024."""
    scale = rgb.shape[1] / 1024.0
    lum = rgb @ np.array((0.3, 0.59, 0.11), dtype=np.float32)
    grooves = np.clip((blur(lum, GROOVE_BLUR * scale) - lum) / GROOVE_CONTRAST, 0.0, 1.0)
    grooves = blur(grooves, 0.8 * scale)
    broad = blur(lum, BROAD_FINE * scale) - blur(lum, BROAD_COARSE * scale)
    broad = np.clip(broad / (broad.std() + 1e-5), -2.0, 2.0) * 0.5
    return broad * BROAD_AMOUNT - grooves


def normal_map(height, strength):
    """Нормали по высоте, затем вдвое меньше (среднее 2×2) и в цвет 0..1."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * strength
    n = np.stack((-dx, -dy, np.ones_like(height)), axis=-1)
    h, w, _ = n.shape
    n = n[: h // 2 * 2, : w // 2 * 2].reshape(h // 2, 2, w // 2, 2, 3).mean(axis=(1, 3))
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def targets(only):
    for folder, prefix, strength in KINDS:
        directory = os.path.join(TEXTURES, folder)
        if not os.path.isdir(directory):
            continue
        for file in sorted(os.listdir(directory)):
            if not file.startswith(prefix) or not file.endswith(".png") or file.endswith("_N.png"):
                continue
            name = file[:-4]
            if only is None or name in only:
                yield folder, name, strength


def build(folder, name, strength):
    rgb = load(os.path.join(TEXTURES, folder, name + ".png"))
    normals = normal_map(height_map(rgb), STRENGTH * strength)
    out = os.path.join(TEXTURES, folder, name + "_N.png")
    zm.save_png(name + "_N", normals, out)
    print("  %-18s %4d -> %4d  %s" % (name, rgb.shape[1], normals.shape[1], os.path.relpath(out, zm.PROJECT_ROOT)))


# ---------------------------------------------------------------------------
# Превью: одна модель дважды — слева без карты, справа с картой, свет сбоку, чтобы рельеф был виден
# ---------------------------------------------------------------------------

def material(folder, name, with_normals):
    mat = bpy.data.materials.new(name + ("_N" if with_normals else ""))
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.6
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEXTURES, folder, name + ".png"))
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if with_normals:
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = bpy.data.images.load(os.path.join(TEXTURES, folder, name + "_N.png"))
        ntex.image.colorspace_settings.name = "Non-Color"
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def render(out_dir, items):
    os.makedirs(out_dir, exist_ok=True)
    for folder, name, _ in items:
        zm.reset_scene()
        scene = bpy.context.scene
        path = os.path.join(MODELS, folder, name + ".fbx")
        if not os.path.exists(path):
            continue
        objs = []
        for side, with_normals in ((-1, False), (1, True)):
            bpy.ops.import_scene.fbx(filepath=path)
            obj = [o for o in bpy.context.selected_objects if o.type == "MESH"][0]
            obj.data.materials.clear()
            obj.data.materials.append(material(folder, name, with_normals))
            objs.append((obj, side))
        size = max(objs[0][0].dimensions)
        for obj, side in objs:
            obj.location.x = side * size * 0.6
        center_z = objs[0][0].dimensions.z * 0.5

        world = bpy.data.worlds.new("World")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.3, 0.32, 0.36, 1)
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.4
        scene.world = world
        sun_data = bpy.data.lights.new("Sun", "SUN")
        sun_data.energy = 4.0
        sun = bpy.data.objects.new("Sun", sun_data)
        sun.rotation_euler = (math.radians(70), 0, math.radians(-60))
        scene.collection.objects.link(sun)

        scene.render.engine = "BLENDER_EEVEE"
        scene.eevee.taa_render_samples = 16
        scene.render.resolution_x, scene.render.resolution_y = 1600, 800
        scene.view_settings.view_transform = "Standard"
        cam_data = bpy.data.cameras.new("Cam")
        cam_data.lens = 50
        cam = bpy.data.objects.new("Cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        dist = size * 2.2
        e = math.radians(35)
        cam.location = (0, -dist * math.cos(e), center_z + dist * math.sin(e))
        cam.rotation_euler = (Vector((0, 0, center_z * 0.6)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(out_dir, name + "_normals.png")
        bpy.ops.render.render(write_still=True)
        print("Превью: " + scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    items = list(targets(only))
    print("Карты нормалей:")
    for item in items:
        build(*item)
    if render_dir:
        render(render_dir, items)


if __name__ == "__main__":
    main()
