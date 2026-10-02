using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Волна 2, кости (02.10.2026, по отзыву «вид костей — просто цвет», «особые кости уникальнее»):
    /// - у каждой особой кости своя модель (Tools/Blender/zonk_dice.py → Art/Models/Dice): DieConfig.LookMesh;
    /// - скины костей — свои текстуры (тело с узором и точки), а не перекраска одной текстуры; ID и цены прежние;
    /// - новые скины со своими точками: черепа, звёзды, руны вместо кружков.
    /// Текстуры рисуются кодом по раскладке атласа 3×2 (точки на местах обычной кости, ZonkSetup.DieLooks), 512 + Crunch.
    /// Уже нарисованный файл не перерисовывается; уже заданная модель кости не заменяется.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string DiceModelsFolder = Models + "/Dice";
        private const string DieSkinsFolder = Textures + "/DiceSkins";
        private const int SkinCell = 256;

        /// <summary>Особая кость → модель.</summary>
        private static readonly (string DieId, string Model)[] SpecialDiceModels =
        {
            ("die_worn", "Die_Worn"), ("die_bone", "Die_Bone"), ("die_lucky", "Die_Lucky"), ("die_sharper", "Die_Sharper"),
            ("die_edges", "Die_Edges"), ("die_sixes", "Die_Sixes"), ("die_odd", "Die_Odd"), ("die_even", "Die_Even"),
            ("die_fives", "Die_Fives"), ("die_middle", "Die_Middle"),
        };

        private enum PipGlyph
        {
            Round,
            Skull,
            Star,
            Rune,
        }

        /// <summary>Вид скина: узор тела (x, y — пиксели атласа; lx, ly — 0..1 внутри грани), точки, блеск.</summary>
        private sealed class SkinLook
        {
            public string Name;
            public Func<float, float, float, float, Func<float, float, float, float>, Color> Body;
            public Color Pips;
            public PipGlyph Glyph = PipGlyph.Round;
            public Color? Frame;
            public float Smoothness = 0.6f;
            public float Metallic;
        }

        private static readonly (string ItemId, string Look)[] SkinRemap =
        {
            ("skin_sapphire", "Sapphire"), ("skin_ruby", "Ruby"), ("skin_jade", "Jade"), ("skin_obsidian", "ObsidianGold"),
            ("skin_gold", "GoldEngraved"), ("skin_pearl", "Pearl"), ("skin_emerald", "Emerald"),
        };

        private static readonly (string ItemId, string Look, int Order)[] NewSkins =
        {
            ("skin_skulls", "Skulls", 20), ("skin_stars", "Stars", 21), ("skin_runes", "Runes", 22),
        };

        /// <summary>Цены новых скинов (волна 2).</summary>
        private static readonly (string id, Get how, int value)[] DiceLayoutV6 =
        {
            ("skin_stars", Get.Coins, 3000),
            ("skin_runes", Get.Ads, 6),
            ("skin_skulls", Get.Play, FromChest),
        };

        static partial void BuildDiceV2(GameConfig config)
        {
            AssignSpecialDiceModels();

            var skinSlot = SlotAt(Zonk.Table.SlotIds.DiceSkin);
            if (skinSlot == null)
                return;

            EnsureFolder(DieSkinsFolder);
            var looks = new Dictionary<string, SkinLook>();
            foreach (var look in SkinLookDefinitions())
                looks[look.Name] = look;

            // Перекраски → текстурные виды: тот же предмет, та же цена, другой материал.
            foreach (var (itemId, lookName) in SkinRemap)
            {
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/" + itemId + ".asset");
                if (item == null || !(item.Payload is MeshMaterialPayload payload) || !looks.TryGetValue(lookName, out var look))
                    continue;

                var material = SkinMaterial(look);
                if (payload.Material == material)
                    continue;

                // Свой материал, назначенный руками (не M_Die_* генератора), не трогаем.
                if (payload.Material != null && !payload.Material.name.StartsWith("M_Die_"))
                    continue;

                payload.Material = material;
                EditorUtility.SetDirty(item);
            }

            foreach (var (itemId, lookName, order) in NewSkins)
            {
                if (looks.TryGetValue(lookName, out var look))
                    Item(itemId, skinSlot, order, new MeshMaterialPayload { Material = SkinMaterial(look) });
            }

            ApplyShopLayoutV6(DiceLayoutV6, config);

            // Виды особых костей и мастерства перерисованы (BuildDieLooks, BuildMasteryLook идут раньше): метка, чтобы не повторять.
            if (RedrawDieLooks)
                File.WriteAllText(DieLooksRedrawMarker, "die looks v2 (smoothstep fix)");
        }

        /// <summary>Модели особых костей: импорт с чтением (валидатор проверяет грани) и ссылка в DieConfig.LookMesh.</summary>
        private static void AssignSpecialDiceModels()
        {
            foreach (var (dieId, model) in SpecialDiceModels)
            {
                var path = DiceModelsFolder + "/" + model + ".fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter))
                    continue;

                ConfigureModel(path, true);
                var die = FindDie(dieId);
                if (die == null || die.LookMesh != null)
                    continue;

                die.LookMesh = MeshOf(path);
                EditorUtility.SetDirty(die);
            }
        }

        private static DieConfig FindDie(string id)
        {
            foreach (var die in FindAll<DieConfig>(ConfigsFolder))
            {
                if (die.Id == id)
                    return die;
            }

            return null;
        }

        private static Material SkinMaterial(SkinLook look)
        {
            var path = DieSkinsFolder + "/DieSkin_" + look.Name + ".png";
            if (!File.Exists(path))
                SavePng(path, PaintSkin(look));
            ConfigureTexture(path, DieLookTextureSize);
            return Mat(null, "DieSkin_" + look.Name, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(path), look.Smoothness,
                look.Metallic, Color.black);
        }

        // ---------- Виды ----------

        private static SkinLook[] SkinLookDefinitions()
        {
            return new[]
            {
                new SkinLook
                {
                    // Сапфировое стекло: глубокий синий, светлый блик полосой по диагонали грани, тёмный край.
                    Name = "Sapphire", Pips = new Color(0.95f, 0.97f, 1f), Smoothness = 0.92f,
                    Body = (x, y, lx, ly, n) => Glass(lx, ly, new Color(0.06f, 0.16f, 0.5f), new Color(0.3f, 0.55f, 0.95f)),
                },
                new SkinLook
                {
                    // Рубин: огранка — грань поделена на треугольники разной яркости.
                    Name = "Ruby", Pips = new Color(1f, 0.85f, 0.4f), Smoothness = 0.9f, Metallic = 0.1f,
                    Body = (x, y, lx, ly, n) => Facets(lx, ly, new Color(0.5f, 0.03f, 0.08f), new Color(0.9f, 0.15f, 0.2f)),
                },
                new SkinLook
                {
                    // Резной нефрит: облачный узор тёмными завитками.
                    Name = "Jade", Pips = new Color(0.97f, 0.98f, 0.92f), Smoothness = 0.7f,
                    Body = (x, y, lx, ly, n) =>
                    {
                        var body = Color.Lerp(new Color(0.18f, 0.5f, 0.32f), new Color(0.45f, 0.78f, 0.55f), n(x, y, 22f));
                        var swirl = Mathf.Abs(Mathf.Sin((lx * 7f + Mathf.Sin(ly * 9f) * 1.2f) * Mathf.PI));
                        return Color.Lerp(body, new Color(0.08f, 0.3f, 0.18f), Smooth01(0.93f, 0.99f, swirl) * 0.8f);
                    },
                },
                new SkinLook
                {
                    // Обсидиан с золотом: чёрное стекло, золотые точки и тонкие золотые трещины.
                    Name = "ObsidianGold", Pips = new Color(1f, 0.78f, 0.25f), Smoothness = 0.95f, Metallic = 0.2f,
                    Body = (x, y, lx, ly, n) =>
                    {
                        var body = Color.Lerp(new Color(0.03f, 0.02f, 0.05f), new Color(0.16f, 0.12f, 0.22f),
                            Mathf.Pow(n(x, y, 30f), 3f));
                        var vein = Mathf.Abs(n(x, y, 14f) - 0.5f);
                        return Color.Lerp(new Color(0.95f, 0.72f, 0.2f), body, Smooth01(0.004f, 0.02f, vein));
                    },
                },
                new SkinLook
                {
                    // Гравированное золото: металл, узор-завитки и рамка по краю грани.
                    Name = "GoldEngraved", Pips = new Color(0.3f, 0.15f, 0.03f), Smoothness = 0.85f, Metallic = 0.85f,
                    Frame = new Color(0.65f, 0.42f, 0.1f),
                    Body = (x, y, lx, ly, n) =>
                    {
                        var body = Color.Lerp(new Color(0.92f, 0.7f, 0.25f), new Color(1f, 0.88f, 0.5f), n(x, y, 35f));
                        var dx = lx - 0.5f;
                        var dy = ly - 0.5f;
                        var curl = Mathf.Abs(Mathf.Sin(Mathf.Sqrt(dx * dx + dy * dy) * 40f + Mathf.Atan2(dy, dx) * 3f));
                        return Color.Lerp(body, new Color(0.72f, 0.5f, 0.15f), Smooth01(0.9f, 0.98f, curl) * 0.7f);
                    },
                },
                new SkinLook
                {
                    // Перламутр: переливы розового, голубого и сливочного полосами.
                    Name = "Pearl", Pips = new Color(0.35f, 0.3f, 0.45f), Smoothness = 0.92f, Metallic = 0.15f,
                    Body = (x, y, lx, ly, n) =>
                    {
                        var t = Mathf.Sin((lx + ly) * 9f + n(x, y, 25f) * 6f) * 0.5f + 0.5f;
                        var a = Color.Lerp(new Color(1f, 0.9f, 0.93f), new Color(0.85f, 0.93f, 1f), t);
                        return Color.Lerp(a, new Color(1f, 0.98f, 0.9f), n(x, y, 60f) * 0.5f);
                    },
                },
                new SkinLook
                {
                    // Изумруд: огранка и золотая рамка грани.
                    Name = "Emerald", Pips = new Color(1f, 0.9f, 0.5f), Smoothness = 0.9f, Metallic = 0.1f,
                    Frame = new Color(0.95f, 0.75f, 0.25f),
                    Body = (x, y, lx, ly, n) => Facets(lx, ly, new Color(0.03f, 0.35f, 0.18f), new Color(0.2f, 0.75f, 0.42f)),
                },
                new SkinLook
                {
                    // Черепа: старая кость, вместо точек — черепа.
                    Name = "Skulls", Pips = new Color(0.1f, 0.07f, 0.06f), Glyph = PipGlyph.Skull, Smoothness = 0.35f,
                    Body = (x, y, lx, ly, n) =>
                        Color.Lerp(new Color(0.85f, 0.79f, 0.62f), new Color(0.96f, 0.92f, 0.8f), n(x, y, 16f)),
                },
                new SkinLook
                {
                    // Звездочёт: ночное небо, вместо точек — золотые звёзды, мелкие искры.
                    Name = "Stars", Pips = new Color(1f, 0.82f, 0.3f), Glyph = PipGlyph.Star, Smoothness = 0.75f,
                    Body = (x, y, lx, ly, n) =>
                    {
                        var sky = Color.Lerp(new Color(0.05f, 0.07f, 0.2f), new Color(0.15f, 0.12f, 0.35f), ly);
                        var sparkle = Smooth01(0.94f, 0.99f, n(x, y, 4f)) * 0.8f;
                        return Color.Lerp(sky, new Color(0.85f, 0.9f, 1f), sparkle);
                    },
                },
                new SkinLook
                {
                    // Руны: серый камень, вместо точек — светящиеся голубые руны.
                    Name = "Runes", Pips = new Color(0.4f, 0.85f, 1f), Glyph = PipGlyph.Rune, Smoothness = 0.2f,
                    Body = (x, y, lx, ly, n) =>
                    {
                        var stone = Color.Lerp(new Color(0.35f, 0.36f, 0.38f), new Color(0.55f, 0.56f, 0.58f), n(x, y, 18f));
                        var crack = Mathf.Abs(n(x, y, 11f) - 0.5f);
                        return Color.Lerp(new Color(0.2f, 0.2f, 0.22f), stone, Smooth01(0.006f, 0.03f, crack));
                    },
                },
            };
        }

        private static Color Glass(float lx, float ly, Color deep, Color light)
        {
            var edge = Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly));
            // Стекло: свет по краям грани, глубина в середине.
            var body = Color.Lerp(light, deep, Smooth01(0f, 0.22f, edge));
            var streak = Mathf.Abs((lx + ly) - 1.1f);
            body = Color.Lerp(body, Color.white, (1f - Smooth01(0.04f, 0.09f, streak)) * 0.55f);
            var streak2 = Mathf.Abs((lx + ly) - 1.32f);
            return Color.Lerp(body, Color.white, (1f - Smooth01(0.01f, 0.03f, streak2)) * 0.4f);
        }

        /// <summary>Огранка: грань делится диагоналями на 4 треугольника и ступени к центру, каждая — своя яркость.</summary>
        private static Color Facets(float lx, float ly, Color dark, Color light)
        {
            var dx = lx - 0.5f;
            var dy = ly - 0.5f;
            var quadrant = Mathf.Abs(dx) > Mathf.Abs(dy) ? (dx > 0 ? 0 : 2) : (dy > 0 ? 1 : 3);
            var ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) < 0.28f ? 1 : 0;
            var shade = ring == 1 ? 0.75f : new[] { 0.35f, 1f, 0.15f, 0.55f }[quadrant];
            return Color.Lerp(dark, light, shade);
        }

        // ---------- Рисование атласа ----------

        /// <summary>Точки граней: смещения от центра клетки (как PIP_LAYOUTS в zonk_models.py), доля клетки.</summary>
        private static readonly Vector2[][] PipLayouts =
        {
            null,
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, 1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1), new Vector2(1, 1) },
            new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) },
        };

        private const float PipOffset = 0.24f;
        private const float PipRadius = 0.085f;

        private static Texture2D PaintSkin(SkinLook look)
        {
            var width = SkinCell * 3;
            var height = SkinCell * 2;
            var pixels = new Color[width * height];
            var seed = look.Name.GetHashCode();
            float Noise(float x, float y, float scale) => ValueNoise(x / scale, y / scale, seed);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var col = x / SkinCell;
                var row = y / SkinCell;
                // Верхний ряд — грани 1 2 3, нижний — 4 5 6.
                var face = row == 1 ? col + 1 : col + 4;
                var lx = (x - col * SkinCell + 0.5f) / SkinCell;
                var ly = (y - row * SkinCell + 0.5f) / SkinCell;

                var color = look.Body(x, y, lx, ly, Noise);
                if (look.Frame.HasValue)
                {
                    var edge = Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly));
                    color = Color.Lerp(color, look.Frame.Value, 1f - Smooth01(0.035f, 0.05f, edge));
                }

                var pip = 0f;
                // Знаки крупнее круглых точек: черепа и руны должны читаться на маленькой кости.
                var scale = look.Glyph == PipGlyph.Round ? 1f : look.Glyph == PipGlyph.Rune ? 1.1f : 1.3f;
                var glow = 0f;
                foreach (var offset in PipLayouts[face])
                {
                    var px = (lx - (0.5f + offset.x * PipOffset)) / (PipRadius * scale);
                    var py = (ly - (0.5f + offset.y * PipOffset)) / (PipRadius * scale);
                    pip = Mathf.Max(pip, GlyphCoverage(look.Glyph, px, py));
                    if (look.Glyph == PipGlyph.Rune)
                        glow = Mathf.Max(glow, 1f - Smooth01(0.4f, 1.6f, Mathf.Sqrt(px * px + py * py)));
                }

                // Руны светятся: мягкий ореол цветом знака.
                color = Color.Lerp(color, look.Pips, glow * 0.35f);

                // Тень вокруг точки: вдавленность, как у классической кости.
                color = Color.Lerp(color, look.Pips, pip);
                color.a = 1f;
                pixels[y * width + x] = color;
            }

            return TextureFrom(width, height, pixels);
        }

        /// <summary>Покрытие знака точки в координатах точки (радиус точки = 1, центр в 0).</summary>
        private static float GlyphCoverage(PipGlyph glyph, float x, float y)
        {
            const float soft = 0.08f;
            switch (glyph)
            {
                case PipGlyph.Skull:
                {
                    // Голова, челюсть, глазницы и нос вырезаны.
                    var head = Circle(x, y - 0.15f, 0.85f, soft);
                    var jaw = Rect(x, y + 0.72f, 0.48f, 0.3f, soft);
                    var shape = Mathf.Max(head, jaw);
                    var eyes = Mathf.Max(Circle(x - 0.33f, y - 0.15f, 0.26f, soft), Circle(x + 0.33f, y - 0.15f, 0.26f, soft));
                    var nose = Circle(x, y + 0.28f, 0.11f, soft);
                    var teeth = Mathf.Abs(Mathf.Sin(x * 11f)) > 0.75f ? Rect(x, y + 0.82f, 0.45f, 0.16f, soft) : 0f;
                    return Mathf.Clamp01(shape - Mathf.Max(Mathf.Max(eyes, nose), teeth));
                }
                case PipGlyph.Star:
                    return StarCoverage(x * 24f, y * 24f, 30f, 12f);
                case PipGlyph.Rune:
                {
                    // Руна: вертикальная черта и две косые (похоже на «ᚠ»), толщина 0.22.
                    var stem = Segment(x, y, -0.3f, -1f, -0.3f, 1f, 0.36f, soft);
                    var arm1 = Segment(x, y, -0.3f, 0.5f, 0.65f, 1f, 0.36f, soft);
                    var arm2 = Segment(x, y, -0.3f, -0.05f, 0.65f, 0.45f, 0.36f, soft);
                    return Mathf.Max(stem, Mathf.Max(arm1, arm2));
                }
                default:
                    return Circle(x, y, 1f, soft);
            }
        }

        private static float Circle(float x, float y, float r, float soft)
        {
            return 1f - Smooth01(r - soft, r + soft, Mathf.Sqrt(x * x + y * y));
        }

        private static float Rect(float x, float y, float halfW, float halfH, float soft)
        {
            var d = Mathf.Max(Mathf.Abs(x) - halfW, Mathf.Abs(y) - halfH);
            return 1f - Smooth01(-soft, soft, d);
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float width, float soft)
        {
            var px = x - ax;
            var py = y - ay;
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            var ex = px - dx * t;
            var ey = py - dy * t;
            return 1f - Smooth01(width * 0.5f - soft, width * 0.5f + soft, Mathf.Sqrt(ex * ex + ey * ey));
        }
    }
}
