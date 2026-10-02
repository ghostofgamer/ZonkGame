using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Table;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Облик игрока: слоты «Аватар» и «Рамка» (в сцене ничего не меняют, в магазине не показываются — выбираются в профиле),
    /// 12 аватаров и 10 рамок разной редкости. Картинки — заглушки, нарисованные кодом (Art/Sprites/Avatars, Frames):
    /// настоящий арт подставляется в SpritePayload предмета без кода. Где взять: награда за уровень (рубежи
    /// PlayerLevelConfig), сундук и достижения (подсказка ProgressPriceOption.HintKey). Базовые аватар и рамка бесплатные.
    /// Уже созданные предметы, картинки и награды не перезаписываются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string AvatarsFolder = Root + "/Art/Sprites/Avatars";
        private const string FramesFolder = Root + "/Art/Sprites/Frames";
        private const int LookSize = 256;

        private enum Glyph
        {
            Die,
            Star,
            Moon,
            Clover,
            Heart,
            Diamond,
            Bolt,
            Eye,
            Skull,
            Crown,
            Sun,
            Six,
        }

        private enum FrameStyle
        {
            Plain,
            Rope,
            Dots,
            Double,
            Gems,
            Gold,
            Fire,
            Royal,
        }

        /// <summary>Где взять: 0 — бесплатно, больше 0 — уровень, -1 — сундук, -2 — достижение.</summary>
        private const int FromChest = -1;
        private const int FromAchievement = -2;
        private const int FromSeason = -3;

        private struct AvatarDef
        {
            public string Id;
            public Rarity Rarity;
            public Glyph Glyph;
            public Color Top;
            public Color Bottom;
            public Color Ink;
            public int Source;
        }

        private struct FrameDef
        {
            public string Id;
            public Rarity Rarity;
            public FrameStyle Style;
            public Color Main;
            public Color Accent;
            public int Source;
        }

        private static readonly AvatarDef[] Avatars =
        {
            Av("avatar_die", Rarity.Common, Glyph.Die, C(0.62f, 0.2f, 0.16f), C(0.36f, 0.1f, 0.08f), C(0.98f, 0.95f, 0.88f), 0),
            Av("avatar_star", Rarity.Common, Glyph.Star, C(0.2f, 0.36f, 0.7f), C(0.1f, 0.16f, 0.4f), C(1f, 0.84f, 0.3f), 5),
            Av("avatar_moon", Rarity.Common, Glyph.Moon, C(0.12f, 0.14f, 0.32f), C(0.04f, 0.05f, 0.14f), C(0.95f, 0.92f, 0.7f), 15),
            Av("avatar_clover", Rarity.Common, Glyph.Clover, C(0.55f, 0.78f, 0.45f), C(0.24f, 0.45f, 0.22f), C(0.1f, 0.38f, 0.14f), FromChest),
            Av("avatar_heart", Rarity.Rare, Glyph.Heart, C(0.98f, 0.7f, 0.76f), C(0.75f, 0.35f, 0.45f), C(0.85f, 0.1f, 0.2f), 20),
            Av("avatar_diamond", Rarity.Rare, Glyph.Diamond, C(0.15f, 0.45f, 0.5f), C(0.05f, 0.2f, 0.26f), C(0.55f, 0.95f, 1f), 30),
            Av("avatar_bolt", Rarity.Rare, Glyph.Bolt, C(0.45f, 0.22f, 0.65f), C(0.2f, 0.08f, 0.35f), C(1f, 0.88f, 0.2f), FromChest),
            Av("avatar_eye", Rarity.Rare, Glyph.Eye, C(0.3f, 0.55f, 0.5f), C(0.12f, 0.25f, 0.24f), C(0.25f, 0.65f, 0.35f), FromChest),
            Av("avatar_skull", Rarity.Rare, Glyph.Skull, C(0.25f, 0.25f, 0.28f), C(0.06f, 0.06f, 0.08f), C(0.94f, 0.92f, 0.85f), FromAchievement),
            Av("avatar_crown", Rarity.Legendary, Glyph.Crown, C(0.75f, 0.12f, 0.15f), C(0.32f, 0.03f, 0.06f), C(1f, 0.78f, 0.2f), 50),
            Av("avatar_sun", Rarity.Legendary, Glyph.Sun, C(1f, 0.6f, 0.2f), C(0.75f, 0.22f, 0.08f), C(1f, 0.95f, 0.55f), FromChest),
            Av("avatar_six", Rarity.Legendary, Glyph.Six, C(0.14f, 0.12f, 0.1f), C(0.02f, 0.02f, 0.02f), C(1f, 0.78f, 0.25f), FromAchievement),
            // Сезон 1 «Пиратский»: только на сезонном пути.
            Av("avatar_s1_skull", Rarity.Rare, Glyph.Skull, C(0.75f, 0.12f, 0.12f), C(0.3f, 0.03f, 0.03f), C(0.96f, 0.93f, 0.85f), FromSeason),
            Av("avatar_s1_star", Rarity.Common, Glyph.Star, C(0.1f, 0.4f, 0.55f), C(0.03f, 0.15f, 0.25f), C(1f, 0.55f, 0.35f), FromSeason),
            Av("avatar_s1_moon", Rarity.Legendary, Glyph.Moon, C(0.05f, 0.25f, 0.3f), C(0.01f, 0.06f, 0.1f), C(1f, 0.8f, 0.3f), FromSeason),
        };

        private static readonly FrameDef[] Frames =
        {
            Fr("frame_wood", Rarity.Common, FrameStyle.Plain, C(0.5f, 0.32f, 0.16f), C(0.3f, 0.18f, 0.08f), 0),
            Fr("frame_silver", Rarity.Common, FrameStyle.Plain, C(0.85f, 0.87f, 0.92f), C(0.55f, 0.58f, 0.65f), 10),
            Fr("frame_rope", Rarity.Common, FrameStyle.Rope, C(0.82f, 0.68f, 0.42f), C(0.55f, 0.42f, 0.22f), FromChest),
            Fr("frame_dots", Rarity.Rare, FrameStyle.Dots, C(0.25f, 0.45f, 0.85f), C(0.9f, 0.95f, 1f), 25),
            Fr("frame_double", Rarity.Rare, FrameStyle.Double, C(0.3f, 0.75f, 0.55f), C(0.12f, 0.35f, 0.25f), FromChest),
            Fr("frame_emerald", Rarity.Rare, FrameStyle.Gems, C(0.75f, 0.78f, 0.82f), C(0.15f, 0.8f, 0.4f), 40),
            Fr("frame_ruby", Rarity.Rare, FrameStyle.Gems, C(0.35f, 0.3f, 0.3f), C(0.9f, 0.12f, 0.2f), FromAchievement),
            Fr("frame_gold", Rarity.Legendary, FrameStyle.Gold, C(1f, 0.8f, 0.3f), C(0.3f, 0.6f, 1f), 75),
            Fr("frame_fire", Rarity.Legendary, FrameStyle.Fire, C(1f, 0.45f, 0.1f), C(1f, 0.85f, 0.25f), FromChest),
            Fr("frame_royal", Rarity.Legendary, FrameStyle.Royal, C(0.45f, 0.15f, 0.6f), C(1f, 0.8f, 0.3f), FromAchievement),
            Fr("frame_s1_rope", Rarity.Common, FrameStyle.Rope, C(0.45f, 0.32f, 0.2f), C(0.25f, 0.16f, 0.08f), FromSeason),
            Fr("frame_s1_sea", Rarity.Rare, FrameStyle.Dots, C(0.1f, 0.45f, 0.6f), C(0.85f, 0.95f, 1f), FromSeason),
            Fr("frame_s1_captain", Rarity.Legendary, FrameStyle.Gold, C(0.9f, 0.7f, 0.25f), C(0.85f, 0.12f, 0.15f), FromSeason),
        };

        private static AvatarDef Av(string id, Rarity rarity, Glyph glyph, Color top, Color bottom, Color ink, int source)
        {
            return new AvatarDef { Id = id, Rarity = rarity, Glyph = glyph, Top = top, Bottom = bottom, Ink = ink, Source = source };
        }

        private static FrameDef Fr(string id, Rarity rarity, FrameStyle style, Color main, Color accent, int source)
        {
            return new FrameDef { Id = id, Rarity = rarity, Style = style, Main = main, Accent = accent, Source = source };
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static void BuildAppearance(GameConfig config)
        {
            EnsureFolder(AvatarsFolder);
            EnsureFolder(FramesFolder);

            var avatarSlot = LookSlot(SlotIds.Avatar, "slot.avatar", 100);
            var frameSlot = LookSlot(SlotIds.Frame, "slot.frame", 101);

            for (var i = 0; i < Avatars.Length; i++)
            {
                var def = Avatars[i];
                var path = AvatarsFolder + "/" + def.Id + ".png";
                if (!File.Exists(path))
                    SavePng(path, PaintAvatar(def));
                var item = LookItem(def.Id, avatarSlot, i, def.Rarity, LookSprite(path), def.Source);
                if (def.Source == 0)
                    SetDefault(avatarSlot, item);
                GiveAtLevel(config, def.Source, item);
            }

            for (var i = 0; i < Frames.Length; i++)
            {
                var def = Frames[i];
                var path = FramesFolder + "/" + def.Id + ".png";
                if (!File.Exists(path))
                    SavePng(path, PaintFrame(def));
                var item = LookItem(def.Id, frameSlot, i, def.Rarity, LookSprite(path), def.Source);
                if (def.Source == 0)
                    SetDefault(frameSlot, item);
                GiveAtLevel(config, def.Source, item);
            }
        }

        private static CosmeticSlotConfig LookSlot(string id, string nameKey, int order)
        {
            var slot = Slot(id, nameKey, order, CameraShots.Menu, new NoSceneApplier());
            if (slot.ShowInShop)
            {
                slot.ShowInShop = false;
                EditorUtility.SetDirty(slot);
            }

            return slot;
        }

        private static CosmeticItemConfig LookItem(string id, CosmeticSlotConfig slot, int order, Rarity rarity, Sprite sprite, int source)
        {
            var price = source == 0 ? new PriceOption[0]
                : new PriceOption[] { new ProgressPriceOption { HintKey = source > 0 ? "hint.level" + source : source == FromChest ? "hint.chest" : source == FromSeason ? "hint.season" : "hint.achievement" } };
            var item = Item(id, slot, order, new SpritePayload { Sprite = sprite }, price);
            var changed = false;
            if (item.Rarity != rarity && item.Rarity == Rarity.Common)
            {
                item.Rarity = rarity;
                changed = true;
            }

            if (item.Payload is SpritePayload payload && payload.Sprite == null)
            {
                payload.Sprite = sprite;
                changed = true;
            }

            if (item.Icon == null)
            {
                item.Icon = sprite;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>Предмет в награду за уровень (рубеж PlayerLevelConfig), если его там ещё нет.</summary>
        private static void GiveAtLevel(GameConfig config, int level, CosmeticItemConfig item)
        {
            var levels = config != null ? config.PlayerLevel : null;
            if (levels == null || level <= 0)
                return;

            var milestone = levels.MilestoneAt(level);
            if (milestone == null)
            {
                milestone = new PlayerLevelMilestone { Level = level };
                levels.Milestones.Add(milestone);
            }

            foreach (var reward in milestone.Rewards)
            {
                if (reward is ContentReward content && content.Item == item)
                    return;
            }

            milestone.Rewards.Add(new ContentReward { Item = item });
            EditorUtility.SetDirty(levels);
        }

        private static Sprite LookSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.maxTextureSize != LookSize)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = LookSize;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- Рисование ----------

        private static Texture2D PaintAvatar(AvatarDef def)
        {
            var size = LookSize;
            var pixels = new Color[size * size];
            var center = size * 0.5f;
            var legendary = def.Rarity == Rarity.Legendary;
            var shade = Color.Lerp(def.Ink, Color.black, 0.45f);

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var disc = Ellipse(x, y, center, center, 120f, 120f);
                if (disc <= 0f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                // Фон: градиент сверху вниз с мягким светом в центре.
                var color = Color.Lerp(def.Bottom, def.Top, y / (float)size);
                var dx = (x - center) / center;
                var dy = (y - center) / center;
                color = Color.Lerp(color, Color.white, Mathf.Clamp01(0.18f - (dx * dx + dy * dy) * 0.18f));

                color = PaintGlyph(color, def.Glyph, x, y, def.Ink, shade);

                // Легендарный: искры по краю.
                if (legendary)
                {
                    color = Paint(color, Color.white, Sparkle(x, y, 60f, 200f, 10f));
                    color = Paint(color, Color.white, Sparkle(x, y, 205f, 70f, 8f));
                    color = Paint(color, Color.white, Sparkle(x, y, 190f, 196f, 6f));
                }

                color.a = disc;
                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        private static Color PaintGlyph(Color color, Glyph glyph, float x, float y, Color ink, Color shade)
        {
            switch (glyph)
            {
                case Glyph.Die:
                case Glyph.Six:
                {
                    var face = glyph == Glyph.Six ? new Color(0.08f, 0.07f, 0.06f) : ink;
                    var pip = glyph == Glyph.Six ? ink : new Color(0.15f, 0.08f, 0.06f);
                    color = Paint(color, Color.black * 0.5f, RoundSquare(x + 6f, y - 6f, 128f, 128f, 78f));
                    color = Paint(color, face, RoundSquare(x, y, 128f, 128f, 78f));
                    if (glyph == Glyph.Six)
                    {
                        color = Paint(color, ink, Mathf.Clamp01(RoundSquare(x, y, 128f, 128f, 78f) - RoundSquare(x, y, 128f, 128f, 72f)));
                        for (var row = -1; row <= 1; row++)
                        {
                            color = Paint(color, pip, Ellipse(x, y, 98f, 128f + row * 34f, 13f, 13f));
                            color = Paint(color, pip, Ellipse(x, y, 158f, 128f + row * 34f, 13f, 13f));
                        }
                    }
                    else
                    {
                        color = Paint(color, pip, Ellipse(x, y, 128f, 128f, 14f, 14f));
                        color = Paint(color, pip, Ellipse(x, y, 92f, 164f, 14f, 14f));
                        color = Paint(color, pip, Ellipse(x, y, 164f, 164f, 14f, 14f));
                        color = Paint(color, pip, Ellipse(x, y, 92f, 92f, 14f, 14f));
                        color = Paint(color, pip, Ellipse(x, y, 164f, 92f, 14f, 14f));
                    }

                    return color;
                }
                case Glyph.Star:
                    color = Paint(color, shade, StarCoverage(x - 134f, y - 122f, 92f, 38f));
                    return Paint(color, ink, StarCoverage(x - 128f, y - 128f, 92f, 38f));
                case Glyph.Moon:
                    return Paint(color, ink, Mathf.Clamp01(Ellipse(x, y, 118f, 128f, 78f, 78f) - Ellipse(x, y, 150f, 146f, 68f, 68f)));
                case Glyph.Clover:
                    color = Paint(color, ink, Box(x, y, 122f, 50f, 134f, 120f));
                    color = Paint(color, ink, Ellipse(x, y, 128f, 168f, 34f, 34f));
                    color = Paint(color, ink, Ellipse(x, y, 94f, 128f, 34f, 34f));
                    color = Paint(color, ink, Ellipse(x, y, 162f, 128f, 34f, 34f));
                    return Paint(color, Color.Lerp(ink, Color.white, 0.25f), Ellipse(x, y, 128f, 136f, 14f, 14f));
                case Glyph.Heart:
                {
                    var heart = Mathf.Max(Mathf.Max(Ellipse(x, y, 98f, 150f, 42f, 42f), Ellipse(x, y, 158f, 150f, 42f, 42f)),
                        Polygon(x, y, new Vector2(58f, 140f), new Vector2(198f, 140f), new Vector2(128f, 50f)));
                    color = Paint(color, ink, heart);
                    return Paint(color, Color.white, Ellipse(x, y, 92f, 160f, 12f, 8f) * 0.7f);
                }
                case Glyph.Diamond:
                    color = Paint(color, ink, Polygon(x, y, new Vector2(128f, 214f), new Vector2(204f, 140f), new Vector2(128f, 44f), new Vector2(52f, 140f)));
                    return Paint(color, Color.Lerp(ink, Color.white, 0.55f),
                        Polygon(x, y, new Vector2(128f, 214f), new Vector2(204f, 140f), new Vector2(128f, 140f)));
                case Glyph.Bolt:
                    return Paint(color, ink, Polygon(x, y, new Vector2(150f, 222f), new Vector2(78f, 118f), new Vector2(122f, 118f),
                        new Vector2(100f, 34f), new Vector2(180f, 148f), new Vector2(134f, 148f)));
                case Glyph.Eye:
                    color = Paint(color, new Color(0.97f, 0.96f, 0.92f), Ellipse(x, y, 128f, 128f, 96f, 52f));
                    color = Paint(color, ink, Ellipse(x, y, 128f, 128f, 36f, 36f));
                    color = Paint(color, Color.black, Ellipse(x, y, 128f, 128f, 16f, 16f));
                    return Paint(color, Color.white, Ellipse(x, y, 140f, 140f, 7f, 7f));
                case Glyph.Skull:
                {
                    var dark = new Color(0.08f, 0.07f, 0.07f);
                    color = Paint(color, ink, Ellipse(x, y, 128f, 146f, 72f, 70f));
                    color = Paint(color, ink, Box(x, y, 94f, 66f, 162f, 112f));
                    color = Paint(color, dark, Ellipse(x, y, 100f, 142f, 19f, 22f));
                    color = Paint(color, dark, Ellipse(x, y, 156f, 142f, 19f, 22f));
                    color = Paint(color, dark, Polygon(x, y, new Vector2(128f, 122f), new Vector2(118f, 102f), new Vector2(138f, 102f)));
                    for (var tooth = 0; tooth < 4; tooth++)
                        color = Paint(color, dark, Box(x, y, 104f + tooth * 16f, 66f, 106f + tooth * 16f, 88f));
                    return color;
                }
                case Glyph.Crown:
                {
                    var crown = Mathf.Max(Box(x, y, 60f, 70f, 196f, 112f),
                        Mathf.Max(Polygon(x, y, new Vector2(60f, 110f), new Vector2(60f, 190f), new Vector2(104f, 110f)),
                            Mathf.Max(Polygon(x, y, new Vector2(92f, 110f), new Vector2(128f, 200f), new Vector2(164f, 110f)),
                                Polygon(x, y, new Vector2(152f, 110f), new Vector2(196f, 190f), new Vector2(196f, 110f)))));
                    color = Paint(color, shade, crown * Mathf.Clamp01((140f - y) / 80f) * 0.5f);
                    color = Paint(color, ink, crown);
                    color = Paint(color, new Color(0.85f, 0.1f, 0.2f), Ellipse(x, y, 128f, 91f, 11f, 11f));
                    color = Paint(color, new Color(0.2f, 0.5f, 0.95f), Ellipse(x, y, 88f, 91f, 9f, 9f));
                    color = Paint(color, new Color(0.2f, 0.8f, 0.4f), Ellipse(x, y, 168f, 91f, 9f, 9f));
                    color = Paint(color, ink, Ellipse(x, y, 60f, 192f, 9f, 9f));
                    color = Paint(color, ink, Ellipse(x, y, 128f, 202f, 9f, 9f));
                    return Paint(color, ink, Ellipse(x, y, 196f, 192f, 9f, 9f));
                }
                case Glyph.Sun:
                {
                    var dx = x - 128f;
                    var dy = y - 128f;
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    var angle = Mathf.Atan2(dy, dx);
                    var ray = Mathf.Cos(angle * 12f) > 0.35f && radius > 50f && radius < 100f - (1f - Mathf.Cos(angle * 12f)) * 30f ? 1f : 0f;
                    color = Paint(color, ink, ray);
                    color = Paint(color, ink, Ellipse(x, y, 128f, 128f, 50f, 50f));
                    return Paint(color, Color.white, Ellipse(x, y, 116f, 140f, 16f, 12f) * 0.6f);
                }
                default:
                    return color;
            }
        }

        private static Texture2D PaintFrame(FrameDef def)
        {
            var size = LookSize;
            var pixels = new Color[size * size];
            var center = size * 0.5f;
            var dark = Color.Lerp(def.Main, Color.black, 0.5f);

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var radius = Mathf.Sqrt(dx * dx + dy * dy);
                var angle = Mathf.Atan2(dy, dx);
                var color = Color.clear;

                switch (def.Style)
                {
                    case FrameStyle.Plain:
                        color = RingPaint(color, radius, 110f, 124f, def.Main, def.Accent);
                        break;
                    case FrameStyle.Rope:
                        color = RingPaint(color, radius, 110f, 124f, def.Main, def.Accent);
                        if (Ring(radius, 110f, 124f) > 0f && Mathf.Sin(angle * 28f + radius * 0.35f) > 0.3f)
                            color = Paint(color, def.Accent, 0.8f);
                        break;
                    case FrameStyle.Dots:
                        color = RingPaint(color, radius, 110f, 122f, def.Main, dark);
                        for (var i = 0; i < 12; i++)
                        {
                            var a = i * Mathf.PI * 2f / 12f;
                            color = Paint(color, def.Accent, Ellipse(x, y, center + Mathf.Cos(a) * 116f, center + Mathf.Sin(a) * 116f, 6f, 6f));
                        }

                        break;
                    case FrameStyle.Double:
                        color = RingPaint(color, radius, 108f, 114f, def.Main, dark);
                        color = RingPaint(color, radius, 118f, 126f, def.Main, dark);
                        break;
                    case FrameStyle.Gems:
                        color = RingPaint(color, radius, 108f, 122f, def.Main, dark);
                        color = Gems(color, x, y, center, 115f, 4, 13f, def.Accent);
                        break;
                    case FrameStyle.Gold:
                        color = RingPaint(color, radius, 104f, 126f, def.Main, Color.Lerp(def.Main, new Color(0.5f, 0.3f, 0.05f), 0.6f));
                        if (Ring(radius, 104f, 126f) > 0f && Mathf.Abs(Mathf.Sin(angle * 20f)) > 0.92f)
                            color = Paint(color, Color.white, 0.5f);
                        color = Gems(color, x, y, center, 115f, 8, 9f, def.Accent);
                        color = Paint(color, Color.white, Sparkle(x, y, 40f, 210f, 12f));
                        color = Paint(color, Color.white, Sparkle(x, y, 222f, 52f, 10f));
                        break;
                    case FrameStyle.Fire:
                    {
                        var flame = 112f + Mathf.Max(0f, Mathf.Sin(angle * 9f)) * 14f + Mathf.Max(0f, Mathf.Sin(angle * 23f + 1f)) * 5f;
                        if (radius > 104f && radius < flame)
                        {
                            var t = Mathf.InverseLerp(104f, flame, radius);
                            color = Paint(color, Color.Lerp(def.Accent, def.Main, t), 1f);
                        }

                        color = RingPaint(color, radius, 104f, 110f, def.Accent, def.Main);
                        break;
                    }
                    case FrameStyle.Royal:
                        color = RingPaint(color, radius, 104f, 124f, def.Main, Color.Lerp(def.Main, Color.black, 0.5f));
                        color = RingPaint(color, radius, 104f, 108f, def.Accent, def.Accent);
                        color = RingPaint(color, radius, 120f, 124f, def.Accent, def.Accent);
                        color = Paint(color, def.Accent, Polygon(x, y, new Vector2(96f, 222f), new Vector2(96f, 252f), new Vector2(112f, 234f),
                            new Vector2(128f, 254f), new Vector2(144f, 234f), new Vector2(160f, 252f), new Vector2(160f, 222f)));
                        break;
                }

                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        /// <summary>Кольцо с объёмом: светлее у внешнего края, темнее у внутреннего; поверх прозрачного — непрозрачное.</summary>
        private static Color RingPaint(Color under, float radius, float inner, float outer, Color light, Color dark)
        {
            var amount = Ring(radius, inner, outer);
            if (amount <= 0f)
                return under;

            var t = Mathf.InverseLerp(inner, outer, radius);
            var ring = Color.Lerp(dark, light, Mathf.Sin(t * Mathf.PI) * 0.6f + 0.4f);
            ring.a = 1f;
            var result = Color.Lerp(under, ring, amount);
            result.a = Mathf.Max(under.a, amount);
            return result;
        }

        private static float Ring(float radius, float inner, float outer)
        {
            return Mathf.Clamp01(Mathf.Min(radius - inner, outer - radius) + 0.5f);
        }

        private static Color Gems(Color color, float x, float y, float center, float radius, int count, float size, Color gem)
        {
            for (var i = 0; i < count; i++)
            {
                var a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / count;
                var gx = center + Mathf.Cos(a) * radius;
                var gy = center + Mathf.Sin(a) * radius;
                color = PaintOpaque(color, Color.Lerp(gem, Color.black, 0.4f), Ellipse(x, y, gx, gy, size + 3f, size + 3f));
                color = PaintOpaque(color, gem, Ellipse(x, y, gx, gy, size, size));
                color = PaintOpaque(color, Color.white, Ellipse(x, y, gx - size * 0.3f, gy + size * 0.3f, size * 0.3f, size * 0.3f));
            }

            return color;
        }

        private static Color PaintOpaque(Color under, Color over, float amount)
        {
            if (amount <= 0f)
                return under;
            var result = Color.Lerp(under, over, amount);
            result.a = Mathf.Max(under.a, amount);
            return result;
        }

        /// <summary>Четырёхлучевая искра.</summary>
        private static float Sparkle(float x, float y, float cx, float cy, float size)
        {
            var dx = Mathf.Abs(x - cx);
            var dy = Mathf.Abs(y - cy);
            var arm = Mathf.Max(Mathf.Clamp01(1.5f - dx) * Mathf.Clamp01(1f - dy / size), Mathf.Clamp01(1.5f - dy) * Mathf.Clamp01(1f - dx / size));
            return Mathf.Max(arm, Ellipse(x, y, cx, cy, size * 0.18f, size * 0.18f));
        }

        /// <summary>Квадрат со скруглёнными углами (суперэллипс).</summary>
        private static float RoundSquare(float x, float y, float cx, float cy, float half)
        {
            var nx = Mathf.Abs(x - cx) / half;
            var ny = Mathf.Abs(y - cy) / half;
            var value = Mathf.Pow(Mathf.Pow(nx, 6f) + Mathf.Pow(ny, 6f), 1f / 6f);
            return Mathf.Clamp01((1f - value) * half / 1.5f + 0.5f);
        }

        /// <summary>Покрытие многоугольником (2×2 подвыборки для мягкого края).</summary>
        private static float Polygon(float x, float y, params Vector2[] points)
        {
            var hits = 0;
            for (var sy = 0; sy < 2; sy++)
            for (var sx = 0; sx < 2; sx++)
            {
                if (Inside(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f, points))
                    hits++;
            }

            return hits / 4f;
        }

        private static bool Inside(float x, float y, Vector2[] points)
        {
            var inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            return inside;
        }

        // ---------- Детали ----------

        /// <summary>Аватар с рамкой поверх; нажатие (кнопка на корне) — Clicked.</summary>
        private static GameObject PlayerAvatarPart()
        {
            return Part("PlayerAvatar", () =>
            {
                var root = UiRect("PlayerAvatar", null);
                root.sizeDelta = new Vector2(160f, 160f);
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 160f;
                element.preferredHeight = 160f;

                var avatar = UiRect("Avatar", root).gameObject.AddComponent<Image>();
                Stretch(avatar.rectTransform);
                avatar.preserveAspect = true;
                avatar.raycastTarget = false;
                var frame = UiRect("Frame", root).gameObject.AddComponent<Image>();
                Stretch(frame.rectTransform);
                frame.preserveAspect = true;
                frame.raycastTarget = false;

                root.gameObject.AddComponent<PlayerAvatarView>().EditorSetup(avatar, frame);
                return root.gameObject;
            });
        }

        /// <summary>Ячейка облика: подложка цветом редкости, картинка, замок у закрытого, отметка у надетого.</summary>
        private static AppearanceCellView AppearanceCellPart()
        {
            return Part("AppearanceCell", () =>
            {
                var root = Box("AppearanceCell", null, _ui.Palette.RarityCommon);
                root.sizeDelta = new Vector2(140f, 140f);
                var background = root.GetComponent<Image>();
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = background;

                var picture = UiRect("Picture", root).gameObject.AddComponent<Image>();
                Stretch(picture.rectTransform);
                picture.rectTransform.offsetMin = new Vector2(10f, 10f);
                picture.rectTransform.offsetMax = new Vector2(-10f, -10f);
                picture.preserveAspect = true;
                picture.raycastTarget = false;

                var locked = Box("Locked", root, new Color(0f, 0f, 0f, 0.45f));
                Stretch(locked);
                locked.GetComponent<Image>().raycastTarget = false;
                var mark = Text("Mark", locked, _ui.BoldFont, 54, new Color(1f, 1f, 1f, 0.85f));
                mark.text = "?";
                Stretch(mark.rectTransform);

                var equipped = Box("Equipped", root, _ui.Palette.Good);
                Corner(equipped, new Vector2(1f, 0f), new Vector2(34f, 34f), new Vector2(-6f, 6f));
                equipped.GetComponent<Image>().raycastTarget = false;
                equipped.gameObject.SetActive(false);

                var view = root.gameObject.AddComponent<AppearanceCellView>();
                view.EditorSetup(background, picture, locked.gameObject, equipped.gameObject, button);
                return root.gameObject;
            }).GetComponent<AppearanceCellView>();
        }

        /// <summary>Сетка ячеек облика: шесть в ряд, растёт вниз.</summary>
        private static RectTransform LookGrid(string name, RectTransform parent)
        {
            var grid = UiRect(name, parent);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(120f, 120f);
            layout.spacing = new Vector2(12f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            return grid;
        }

        /// <summary>Вкладки и страница «Облик» в окне профиля: слева большой аватар и подсказка, справа сетки.</summary>
        private static void BuildProfileLook(ProfileWindow window, RectTransform header, RectTransform pages, GameObject statsPage)
        {
            var statsTab = ButtonView("StatsTab", header, 26, _ui.Palette.ButtonAccent);
            var lookTab = ButtonView("LookTab", header, 26, _ui.Palette.ButtonMuted);
            Width(statsTab, 260, 0);
            Width(lookTab, 260, 0);

            var look = UiRect("Look", pages);
            Stretch(look);
            var row = Row(look, 24);
            Stretch(row);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var left = Column(row, 16, 0);
            Width(left, 360, 0);
            var preview = Nest(PlayerAvatarPart(), left, "Preview").GetComponent<PlayerAvatarView>();
            Height(preview, 300);
            var info = Text("Info", left, _ui.Font, 26, _ui.Palette.Text);
            AutoSize(info, 16, 26);
            Height(info, 220);

            var list = VerticalList("LookList", row, 10, out var content);
            Width(list, -1, 1);
            var avatarsTitle = Text("AvatarsTitle", content, _ui.BoldFont, 30, _ui.Palette.Gold);
            avatarsTitle.alignment = TextAlignmentOptions.Left;
            Height(avatarsTitle, 44);
            var avatars = LookGrid("Avatars", content);
            var framesTitle = Text("FramesTitle", content, _ui.BoldFont, 30, _ui.Palette.Gold);
            framesTitle.alignment = TextAlignmentOptions.Left;
            Height(framesTitle, 44);
            var frames = LookGrid("Frames", content);
            look.gameObject.SetActive(false);

            window.EditorSetupLook(statsTab, lookTab, statsPage, look.gameObject, preview, info, avatarsTitle, avatars, framesTitle,
                frames, AppearanceCellPart());
        }

        /// <summary>Аватар в главном меню: в левом верхнем углу, уровень — правее него.</summary>
        private static void AddAvatarToMainMenu()
        {
            var part = PlayerAvatarPart();
            EditWindowPrefab<MainMenuWindow>("MainMenuWindow", "_avatar", (root, window) =>
            {
                var avatar = (RectTransform)Nest(part, root, "Avatar").transform;
                Corner(avatar, new Vector2(0f, 1f), new Vector2(84f, 84f), new Vector2(44f, -8f));
                var level = root.Find("PlayerLevel") as RectTransform;
                if (level != null)
                    Corner(level, new Vector2(0f, 1f), new Vector2(448f, 64f), new Vector2(140f, -18f));
                return avatar.GetComponent<PlayerAvatarView>();
            });
        }
    }
}
