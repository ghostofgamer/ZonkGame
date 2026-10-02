using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Внешний вид особых костей: текстуры рисуются поверх атласа Die_Classic, точки остаются на своих местах
    /// (маска по яркости атласа), меняются тело кости, узор и цвет точек. Заглушки до настоящих текстур художника:
    /// ассет материала один и тот же, текстуру можно заменить в нём, не трогая код и конфиги.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string DieLooksFolder = Textures + "/DiceLooks";
        private const int DieLookTextureSize = 512;

        /// <summary>Узор тела: (u, v в пикселях атласа, шум) → цвет тела.</summary>
        private delegate Color BodyPattern(float x, float y, Func<float, float, float, float> noise);

        private sealed class DieLook
        {
            public string Name;
            public BodyPattern Body;
            public Color Pips;
            public float Smoothness;
            public float Metallic;
        }

        /// <summary>Стили особых костей: тело, точки, блеск.</summary>
        private static DieLook[] DieLookDefinitions()
        {
            return new[]
            {
                new DieLook
                {
                    Name = "Gold", Pips = new Color(0.25f, 0.12f, 0.03f), Smoothness = 0.8f, Metallic = 0.85f,
                    Body = (x, y, n) => Color.Lerp(new Color(0.95f, 0.72f, 0.25f), new Color(1f, 0.88f, 0.5f), n(x, y, 40f)),
                },
                new DieLook
                {
                    Name = "Porcelain", Pips = new Color(0.1f, 0.25f, 0.7f), Smoothness = 0.9f,
                    Body = (x, y, n) => Color.Lerp(new Color(0.93f, 0.94f, 0.97f), Color.white, n(x, y, 60f)),
                },
                new DieLook
                {
                    Name = "Obsidian", Pips = new Color(0.92f, 0.92f, 0.95f), Smoothness = 0.92f, Metallic = 0.1f,
                    Body = (x, y, n) => Color.Lerp(new Color(0.05f, 0.04f, 0.07f), new Color(0.2f, 0.15f, 0.28f),
                        Mathf.Pow(n(x, y, 25f), 3f)),
                },
                new DieLook
                {
                    Name = "Ruby", Pips = new Color(1f, 0.82f, 0.35f), Smoothness = 0.85f, Metallic = 0.1f,
                    Body = (x, y, n) => Color.Lerp(new Color(0.45f, 0.02f, 0.06f), new Color(0.8f, 0.08f, 0.12f), n(x, y, 30f)),
                },
                new DieLook
                {
                    Name = "Jade", Pips = new Color(0.95f, 0.97f, 0.9f), Smoothness = 0.75f,
                    Body = (x, y, n) => Color.Lerp(new Color(0.15f, 0.45f, 0.3f), new Color(0.45f, 0.75f, 0.55f),
                        n(x, y, 20f) * 0.7f + n(x, y, 80f) * 0.3f),
                },
                new DieLook
                {
                    // Потёртая: слоновая кость с грязью и тонкими царапинами.
                    Name = "Scratched", Pips = new Color(0.1f, 0.08f, 0.07f), Smoothness = 0.3f,
                    Body = (x, y, n) =>
                    {
                        var body = Color.Lerp(new Color(0.78f, 0.74f, 0.66f), new Color(0.92f, 0.89f, 0.8f), n(x, y, 12f));
                        var scratch = Mathf.Abs(Mathf.Sin((x * 0.9f + y * 0.35f) * 0.21f + n(x, y, 6f) * 9f));
                        var scratch2 = Mathf.Abs(Mathf.Sin((x * -0.4f + y * 0.95f) * 0.17f + n(y, x, 7f) * 7f));
                        var mark = Mathf.Max(Mathf.SmoothStep(0.985f, 1f, scratch), Mathf.SmoothStep(0.99f, 1f, scratch2));
                        return Color.Lerp(body, new Color(0.45f, 0.4f, 0.35f), mark * 0.8f);
                    },
                },
                new DieLook
                {
                    // Деревянная: кольца волокон.
                    Name = "Wood", Pips = new Color(0.12f, 0.06f, 0.02f), Smoothness = 0.25f,
                    Body = (x, y, n) =>
                    {
                        var rings = Mathf.Sin((y * 0.12f + n(x, y, 30f) * 6f) * 3.1f) * 0.5f + 0.5f;
                        return Color.Lerp(new Color(0.45f, 0.26f, 0.12f), new Color(0.68f, 0.45f, 0.24f), rings);
                    },
                },
                new DieLook
                {
                    // Мраморная: белая с серыми прожилками.
                    Name = "Marble", Pips = new Color(0.08f, 0.08f, 0.1f), Smoothness = 0.85f,
                    Body = (x, y, n) =>
                    {
                        var vein = Mathf.Abs(Mathf.Sin((x + y) * 0.03f + n(x, y, 18f) * 8f));
                        return Color.Lerp(new Color(0.55f, 0.55f, 0.6f), new Color(0.96f, 0.96f, 0.95f), Mathf.Pow(vein, 0.35f));
                    },
                },
                new DieLook
                {
                    // Костяная: желтоватая кость с трещинами.
                    Name = "Bone", Pips = new Color(0.25f, 0.12f, 0.05f), Smoothness = 0.35f,
                    Body = (x, y, n) =>
                    {
                        var body = Color.Lerp(new Color(0.82f, 0.76f, 0.58f), new Color(0.95f, 0.9f, 0.76f), n(x, y, 15f));
                        var crack = Mathf.Abs(n(x, y, 9f) - 0.5f);
                        return Color.Lerp(new Color(0.4f, 0.32f, 0.2f), body, Mathf.SmoothStep(0.005f, 0.03f, crack));
                    },
                },
                new DieLook
                {
                    // Бронзовая: металл с пятнами ржавчины.
                    Name = "Bronze", Pips = new Color(0.05f, 0.04f, 0.03f), Smoothness = 0.45f, Metallic = 0.65f,
                    Body = (x, y, n) =>
                    {
                        var metal = Color.Lerp(new Color(0.55f, 0.36f, 0.18f), new Color(0.75f, 0.52f, 0.28f), n(x, y, 25f));
                        var rust = Mathf.SmoothStep(0.55f, 0.75f, n(x, y, 10f));
                        return Color.Lerp(metal, new Color(0.45f, 0.2f, 0.1f), rust * 0.8f);
                    },
                },
            };
        }

        /// <summary>Создаёт текстуры и материалы стилей; возвращает материал по имени стиля.</summary>
        private static System.Collections.Generic.Dictionary<string, Material> BuildDieLooks()
        {
            EnsureFolder(DieLooksFolder);
            var looks = DieLookDefinitions();
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            Texture2D atlas = null;
            foreach (var look in looks)
            {
                var texturePath = DieLooksFolder + "/Die_" + look.Name + ".png";
                if (!File.Exists(texturePath))
                {
                    if (atlas == null)
                    {
                        atlas = new Texture2D(2, 2);
                        atlas.LoadImage(File.ReadAllBytes(Textures + "/Die_Classic.png"));
                    }

                    SavePng(texturePath, Paint(atlas, look));
                }

                // Кость на экране маленькая: 512 и Crunch, иначе виды костей — несколько мегабайт стартовой загрузки браузера.
                ConfigureTexture(texturePath, DieLookTextureSize);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                materials[look.Name] = Mat(null, "DieLook_" + look.Name, Color.white, texture, look.Smoothness, look.Metallic,
                    Color.black);
            }

            if (atlas != null)
                UnityEngine.Object.DestroyImmediate(atlas);
            return materials;
        }

        /// <summary>
        /// Вид особой кости на уровне мастерства: тот же узор, точки цветом уровня, со второго уровня металлическая
        /// рамка по краю граней. Текстура вдвое меньше основной: кость на экране маленькая, а сборка для браузера лёгкая.
        /// Уже созданный файл не перерисовывается: его можно заменить своей текстурой.
        /// </summary>
        private static Material BuildMasteryLook(string lookName, int level, Color levelColor)
        {
            DieLook look = null;
            foreach (var candidate in DieLookDefinitions())
            {
                if (candidate.Name == lookName)
                    look = candidate;
            }

            if (look == null)
                return null;

            EnsureFolder(DieLooksFolder + "/Mastery");
            var texturePath = DieLooksFolder + "/Mastery/Die_" + lookName + "_M" + level + ".png";
            if (!File.Exists(texturePath))
            {
                var atlas = new Texture2D(2, 2);
                atlas.LoadImage(File.ReadAllBytes(Textures + "/Die_Classic.png"));
                // Точки цветом уровня должны читаться на теле: на светлом или золотом теле они темнее.
                var body = look.Body(40f, 40f, (x, y, s) => 0.5f);
                var pips = Mathf.Abs(body.grayscale - levelColor.grayscale) < 0.3f ? levelColor * 0.35f : levelColor;
                pips.a = 1f;
                var painted = Paint(atlas, look, pips, level >= 2 ? levelColor : (Color?)null, 2);
                UnityEngine.Object.DestroyImmediate(atlas);
                SavePng(texturePath, painted);
            }

            ConfigureTexture(texturePath, DieLookTextureSize);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            return Mat(null, "DieLook_" + lookName + "_M" + level, Color.white, texture,
                Mathf.Min(1f, look.Smoothness + 0.05f * level), look.Metallic, Color.black);
        }

        /// <summary>
        /// Точки по маске яркости атласа (тёмное = точка), тело по узору стиля. pips — свой цвет точек (мастерство),
        /// border — металлическая рамка по краю каждой грани, step — уменьшение разрешения (2 = вдвое меньше).
        /// </summary>
        private static Texture2D Paint(Texture2D atlas, DieLook look, Color? pips = null, Color? border = null, int step = 1)
        {
            var sourceWidth = atlas.width;
            var source = atlas.GetPixels();
            var width = atlas.width / step;
            var height = atlas.height / step;
            var result = new Color[width * height];
            var seed = look.Name.GetHashCode();
            var pipColor = pips ?? look.Pips;

            // Атлас 3×2 грани: рамка считается от краёв клетки грани.
            var cellWidth = atlas.width / 3f;
            var cellHeight = atlas.height / 2f;
            var borderWidth = cellWidth * 0.045f;

            float Noise(float x, float y, float scale) => ValueNoise(x / scale, y / scale, seed);

            for (var oy = 0; oy < height; oy++)
            for (var ox = 0; ox < width; ox++)
            {
                var x = ox * step;
                var y = oy * step;
                var luminance = source[y * sourceWidth + x].grayscale;
                var pip = 1f - Mathf.SmoothStep(0.3f, 0.6f, luminance);
                var color = Color.Lerp(look.Body(x, y, Noise), pipColor, pip);

                if (border.HasValue)
                {
                    var lx = x % cellWidth;
                    var ly = y % cellHeight;
                    var edge = Mathf.Min(Mathf.Min(lx, cellWidth - lx), Mathf.Min(ly, cellHeight - ly));
                    var amount = 1f - Mathf.SmoothStep(borderWidth * 0.7f, borderWidth, edge);
                    var shine = 0.85f + 0.3f * Noise(x, y, 6f);
                    color = Color.Lerp(color, border.Value * shine, amount);
                }

                color.a = 1f;
                result[oy * width + ox] = color;
            }

            return TextureFrom(width, height, result);
        }

        /// <summary>Плавный шум 0..1 по решётке с хешем: одинаковый при каждом запуске генератора.</summary>
        private static float ValueNoise(float x, float y, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float Hash(int ix, int iy)
            {
                unchecked
                {
                    var h = ix * 374761393 + iy * 668265263 + seed * 144269504;
                    h = (h ^ (h >> 13)) * 1274126177;
                    return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
                }
            }

            var a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx);
            var b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }
    }
}
