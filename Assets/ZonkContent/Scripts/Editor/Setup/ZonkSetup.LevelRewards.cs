using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Награды уровней (05.10): предел 50, вещи почти на каждом втором уровне. Новые вещи только за уровень: 5 аватаров
    /// и 4 рамки (ZonkSetup.Appearance, рисуются здесь), 3 стиля броска (вкладка «Броски» магазина). Раскладка
    /// применяется к готовому PlayerLevel.asset один раз (PlayerLevelConfig.RewardsLayoutVersion): предел, рубежи выше
    /// предела убираются, золотая рамка с 75-го переезжает на 45-й. Вкладка «Уровни» в профиле — путь наград.
    /// Расширение в обновлении: поднять MaxLevel, добавить рубежи и вещи (GiveAtLevel) — игроки на пределе пойдут дальше,
    /// а вещи, поставленные на уже пройденные уровни, придут сами при входе в меню (IPlayerLevel.GrantMissedItems).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const int LevelCap = 50;
        private const int LevelRewardsLayout = 1;

        private static void BuildLevelRewards(GameConfig config)
        {
            var levels = config.PlayerLevel;
            var rollSlot = SlotAt("roll_style");
            if (levels == null || rollSlot == null)
                return;

            // «Ветеран»: спокойная уверенная рука — короткая тряска, мягкий замах, кости катятся, а не летят.
            var veteran = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Veteran.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.4f, 0.55f);
                s.ShakeAmplitude = new Vector2(0.03f, 0.05f);
                s.ShakeFrequency = new Vector2(14f, 18f);
                s.ShakeTilt = new Vector2(6f, 10f);
                s.PourAngle = new Vector2(90f, 105f);
                s.DirectionJitter = 6f;
                s.ThrowSpeed = new Vector2(1.9f, 2.3f);
                s.SpinSpeed = new Vector2(6f, 10f);
            });
            TuneThrow(veteran, 0.25f, 0.35f, 10f, 16f, 0.24f, 0.3f, 0.02f, 0.05f, 0.15f, 0.19f, 0.08f, 0.12f);
            GiveAtLevel(config, 4, LevelItem("roll_veteran", rollSlot, 9, new RollStylePayload { Style = veteran }));

            // «Фокусник»: долгая пауза-прицел наверху, лёгкий рывок, кости закручены сильнее всех.
            var trick = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Trick.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.7f, 1f);
                s.ShakeAmplitude = new Vector2(0.05f, 0.08f);
                s.ShakeFrequency = new Vector2(22f, 28f);
                s.ShakeTilt = new Vector2(20f, 32f);
                s.PourAngle = new Vector2(110f, 130f);
                s.DirectionJitter = 12f;
                s.ThrowSpeed = new Vector2(2.4f, 2.9f);
                s.SpinSpeed = new Vector2(28f, 36f);
            });
            TuneThrow(trick, 0.4f, 0.5f, 28f, 38f, 0.3f, 0.38f, 0.25f, 0.4f, 0.12f, 0.16f, 0.18f, 0.25f);
            GiveAtLevel(config, 12, LevelItem("roll_trick", rollSlot, 10, new RollStylePayload { Style = trick }));

            // «Мастер»: размашистый широкий замах и резкий бросок через весь лоток, кости ложатся веером.
            var master = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Master.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.5f, 0.7f);
                s.ShakeAmplitude = new Vector2(0.08f, 0.12f);
                s.ShakeFrequency = new Vector2(24f, 30f);
                s.ShakeTilt = new Vector2(14f, 24f);
                s.PourAngle = new Vector2(120f, 140f);
                s.DirectionJitter = 20f;
                s.ThrowSpeed = new Vector2(3.2f, 3.8f);
                s.SpinSpeed = new Vector2(16f, 24f);
                s.Spread = new Vector2(0.8f, 1.1f);
            });
            TuneThrow(master, 0.6f, 0.8f, 38f, 48f, 0.22f, 0.28f, 0.08f, 0.14f, 0.09f, 0.12f, 0.3f, 0.42f);
            GiveAtLevel(config, 37, LevelItem("roll_master", rollSlot, 11, new RollStylePayload { Style = master }));

            ApplyLevelRewardsLayout(levels);
        }

        private static CosmeticItemConfig LevelItem(string id, CosmeticSlotConfig slot, int order, CosmeticPayload payload)
        {
            return Item(id, slot, order, payload, new ProgressPriceOption { HintKey = "hint.level" });
        }

        /// <summary>Раскладка v1 к готовому ассету: предел 50, рубежи выше предела убрать, золотая рамка — на 45-й.</summary>
        private static void ApplyLevelRewardsLayout(PlayerLevelConfig levels)
        {
            if (levels.RewardsLayoutVersion >= LevelRewardsLayout)
                return;

            levels.MaxLevel = LevelCap;
            levels.Milestones.RemoveAll(m => m == null || m.Level > LevelCap);

            var gold = AssetDatabase.LoadAssetAtPath<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/frame_gold.asset");
            if (gold != null && levels.LevelOf(gold) == 0)
            {
                var milestone = levels.MilestoneAt(45);
                if (milestone == null)
                {
                    milestone = new PlayerLevelMilestone { Level = 45 };
                    levels.Milestones.Add(milestone);
                }

                milestone.Rewards.Add(new ContentReward { Item = gold });
            }

            levels.Milestones.Sort((a, b) => a.Level.CompareTo(b.Level));
            levels.RewardsLayoutVersion = LevelRewardsLayout;
            EditorUtility.SetDirty(levels);
            Debug.Log("[Zonk] Player level: cap " + LevelCap + ", rewards layout v" + LevelRewardsLayout);
        }

        // ---------- Рисование новых аватаров и рамок ----------

        private static Color PaintLevelGlyph(Color color, Glyph glyph, float x, float y, Color ink, Color shade)
        {
            var dark = Color.Lerp(ink, Color.black, 0.6f);
            switch (glyph)
            {
                case Glyph.Horseshoe:
                {
                    // Подкова дугой вниз, концы вверх, гвозди по дуге.
                    var arc = Mathf.Min(Ellipse(x, y, 128f, 128f, 80f, 80f), 1f - Ellipse(x, y, 128f, 128f, 46f, 46f)) * (y <= 128f ? 1f : 0f);
                    var legs = Mathf.Max(Box(x, y, 48f, 128f, 82f, 196f), Box(x, y, 174f, 128f, 208f, 196f));
                    var shoe = Mathf.Max(arc, legs);
                    color = Paint(color, Color.black * 0.5f, Mathf.Max(
                        Mathf.Min(Ellipse(x - 5f, y + 5f, 128f, 128f, 80f, 80f), 1f - Ellipse(x - 5f, y + 5f, 128f, 128f, 46f, 46f)) * (y + 5f <= 128f ? 1f : 0f),
                        Mathf.Max(Box(x - 5f, y + 5f, 48f, 128f, 82f, 196f), Box(x - 5f, y + 5f, 174f, 128f, 208f, 196f))) * 0.6f);
                    color = Paint(color, ink, shoe);
                    color = Paint(color, Color.white, Mathf.Min(arc, 1f - Ellipse(x, y, 128f, 128f, 74f, 74f)) * 0.5f);
                    color = Paint(color, Color.Lerp(ink, Color.white, 0.6f), Box(x, y, 52f, 192f, 78f, 200f) + Box(x, y, 178f, 192f, 204f, 200f));
                    for (var i = 0; i < 6; i++)
                    {
                        var a = Mathf.PI * (1.08f + i * 0.168f);
                        color = Paint(color, dark, Ellipse(x, y, 128f + Mathf.Cos(a) * 63f, 128f + Mathf.Sin(a) * 63f, 5f, 5f));
                    }

                    color = Paint(color, dark, Ellipse(x, y, 65f, 160f, 5f, 5f));
                    return Paint(color, dark, Ellipse(x, y, 191f, 160f, 5f, 5f));
                }
                case Glyph.Anchor:
                {
                    var ring = Mathf.Min(Ellipse(x, y, 128f, 198f, 24f, 24f), 1f - Ellipse(x, y, 128f, 198f, 13f, 13f));
                    var shaft = Box(x, y, 119f, 56f, 137f, 176f);
                    var bar = Box(x, y, 86f, 158f, 170f, 172f);
                    var hook = Mathf.Min(Ellipse(x, y, 128f, 112f, 72f, 72f), 1f - Ellipse(x, y, 128f, 112f, 58f, 58f)) * (y <= 112f ? 1f : 0f);
                    var flukes = Mathf.Max(Polygon(x, y, new Vector2(42f, 104f), new Vector2(76f, 128f), new Vector2(72f, 96f)),
                        Polygon(x, y, new Vector2(214f, 104f), new Vector2(180f, 128f), new Vector2(184f, 96f)));
                    var anchor = Mathf.Max(Mathf.Max(ring, shaft), Mathf.Max(Mathf.Max(bar, hook), flukes));
                    color = Paint(color, shade, anchor * 0.4f * (x > 128f ? 1f : 0f));
                    color = Paint(color, ink, anchor);
                    color = Paint(color, dark, Ellipse(x, y, 104f, 165f, 4f, 4f));
                    return Paint(color, dark, Ellipse(x, y, 152f, 165f, 4f, 4f));
                }
                case Glyph.Cup:
                {
                    // Кожаный стакан для костей, из горла выпрыгивает кость.
                    var cup = Polygon(x, y, new Vector2(82f, 186f), new Vector2(174f, 186f), new Vector2(160f, 54f), new Vector2(96f, 54f));
                    color = Paint(color, Color.black * 0.5f, Polygon(x - 6f, y + 6f, new Vector2(82f, 186f), new Vector2(174f, 186f),
                        new Vector2(160f, 54f), new Vector2(96f, 54f)) * 0.6f);
                    color = Paint(color, ink, cup);
                    color = Paint(color, Color.Lerp(ink, Color.white, 0.25f), cup * Mathf.Clamp01((118f - x) / 30f) * 0.6f);
                    color = Paint(color, dark, Mathf.Min(cup, Box(x, y, 0f, 150f, 256f, 160f) + Box(x, y, 0f, 80f, 256f, 90f)));
                    color = Paint(color, dark, Ellipse(x, y, 128f, 186f, 46f, 9f));
                    color = Paint(color, Color.black, Ellipse(x, y, 128f, 187f, 38f, 6f));
                    var die = RoundSquare(x, y, 170f, 214f, 22f);
                    color = Paint(color, new Color(0.98f, 0.95f, 0.88f), die);
                    color = Paint(color, new Color(0.7f, 0.1f, 0.1f), Ellipse(x, y, 162f, 222f, 4.5f, 4.5f));
                    color = Paint(color, new Color(0.7f, 0.1f, 0.1f), Ellipse(x, y, 170f, 214f, 4.5f, 4.5f));
                    return Paint(color, new Color(0.7f, 0.1f, 0.1f), Ellipse(x, y, 178f, 206f, 4.5f, 4.5f));
                }
                case Glyph.Key:
                {
                    // Старинный ключ наискосок: кольцо-бородка сверху слева, стержень и бородка внизу справа.
                    var u = (x - 128f) * 0.7071f + (y - 128f) * 0.7071f;
                    var v = -(x - 128f) * 0.7071f + (y - 128f) * 0.7071f;
                    var bow = Mathf.Min(Ellipse(u, v, -58f, 0f, 38f, 38f), 1f - Ellipse(u, v, -58f, 0f, 18f, 18f));
                    var shaft = Box(u, v, -22f, -8f, 86f, 8f);
                    var bit = Mathf.Max(Box(u, v, 56f, -36f, 68f, -8f), Box(u, v, 74f, -30f, 86f, -8f));
                    var collar = Box(u, v, -24f, -13f, -14f, 13f);
                    var key = Mathf.Max(Mathf.Max(bow, shaft), Mathf.Max(bit, collar));
                    color = Paint(color, Color.black * 0.5f, key * 0.4f);
                    color = Paint(color, ink, key);
                    return Paint(color, Color.white, Mathf.Min(bow, Ellipse(u, v, -66f, 14f, 12f, 8f)) * 0.6f);
                }
                case Glyph.Flame:
                {
                    var outer = Mathf.Max(Ellipse(x, y, 128f, 104f, 58f, 58f),
                        Polygon(x, y, new Vector2(72f, 112f), new Vector2(110f, 170f), new Vector2(140f, 236f), new Vector2(176f, 160f), new Vector2(184f, 112f)));
                    var tongue = Polygon(x, y, new Vector2(150f, 150f), new Vector2(194f, 190f), new Vector2(172f, 120f));
                    var inner = Mathf.Max(Ellipse(x, y, 128f, 92f, 32f, 32f),
                        Polygon(x, y, new Vector2(97f, 98f), new Vector2(124f, 168f), new Vector2(158f, 98f)));
                    color = Paint(color, Color.Lerp(ink, new Color(0.7f, 0.05f, 0.05f), 0.5f), Mathf.Max(outer, tongue));
                    color = Paint(color, ink, Mathf.Max(Ellipse(x, y, 128f, 100f, 50f, 50f),
                        Polygon(x, y, new Vector2(80f, 108f), new Vector2(116f, 160f), new Vector2(138f, 220f), new Vector2(168f, 156f), new Vector2(176f, 108f))));
                    color = Paint(color, new Color(1f, 0.85f, 0.3f), inner);
                    return Paint(color, new Color(1f, 0.98f, 0.85f), Ellipse(x, y, 128f, 84f, 16f, 18f));
                }
                default:
                    return color;
            }
        }

        private static Color PaintLevelFrame(Color color, FrameDef def, float x, float y, float radius, float angle, float center, Color dark)
        {
            switch (def.Style)
            {
                case FrameStyle.Chain:
                {
                    // Звенья: круглые и продолговатые по очереди, каждое — кольцо со светом сверху.
                    const int links = 18;
                    for (var i = 0; i < links; i++)
                    {
                        var a = i * Mathf.PI * 2f / links;
                        var cx = center + Mathf.Cos(a) * 115f;
                        var cy = center + Mathf.Sin(a) * 115f;
                        var lu = (x - cx) * -Mathf.Sin(a) + (y - cy) * Mathf.Cos(a);
                        var lv = (x - cx) * Mathf.Cos(a) + (y - cy) * Mathf.Sin(a);
                        var wide = i % 2 == 0;
                        var rx = wide ? 22f : 18f;
                        var ry = wide ? 12f : 7f;
                        var link = Mathf.Min(Ellipse(lu, lv, 0f, 0f, rx, ry), 1f - Ellipse(lu, lv, 0f, 0f, rx - 7f, Mathf.Max(1f, ry - 6f)));
                        if (!wide)
                            link = Ellipse(lu, lv, 0f, 0f, rx, ry);
                        color = PaintOpaque(color, Color.Lerp(def.Accent, def.Main, lv > 0f ? 1f : 0.55f), link);
                    }

                    return color;
                }
                case FrameStyle.Pips:
                {
                    // Кольцо слоновой кости, по нему шесть маленьких граней кости: 1…6 точек.
                    color = RingPaint(color, radius, 106f, 124f, def.Main, Color.Lerp(def.Main, new Color(0.5f, 0.4f, 0.3f), 0.6f));
                    for (var face = 1; face <= 6; face++)
                    {
                        var a = Mathf.PI * 0.5f - (face - 1) * Mathf.PI * 2f / 6f;
                        var fx = center + Mathf.Cos(a) * 115f;
                        var fy = center + Mathf.Sin(a) * 115f;
                        color = PaintOpaque(color, Color.Lerp(def.Main, Color.black, 0.35f), RoundSquare(x, y, fx, fy, 19f));
                        color = PaintOpaque(color, def.Main, RoundSquare(x, y, fx, fy, 16f));
                        color = Pips(color, x, y, fx, fy, face, def.Accent);
                    }

                    return color;
                }
                case FrameStyle.Laurel:
                {
                    // Венок: тонкий золотой обод, листья в два ряда, сверху разрыв, снизу золотая лента.
                    color = RingPaint(color, radius, 113f, 117f, def.Accent, Color.Lerp(def.Accent, Color.black, 0.4f));
                    const int leaves = 13;
                    for (var side = -1; side <= 1; side += 2)
                    for (var i = 0; i < leaves; i++)
                    {
                        var a = -Mathf.PI * 0.5f + side * (0.22f + i * 0.2f);
                        for (var row = 0; row < 2; row++)
                        {
                            var r = row == 0 ? 106f : 124f;
                            var lx = center + Mathf.Cos(a) * r;
                            var ly = center + Mathf.Sin(a) * r;
                            var tilt = a + side * (Mathf.PI * 0.5f) + (row == 0 ? -0.5f : 0.5f) * side;
                            var leaf = Rotated(x, y, lx, ly, 13f, 6f, tilt);
                            var vein = Rotated(x, y, lx, ly, 11f, 1.2f, tilt);
                            color = PaintOpaque(color, Color.Lerp(def.Main, Color.black, row == 0 ? 0.25f : 0f), leaf);
                            color = PaintOpaque(color, Color.Lerp(def.Main, Color.black, 0.45f), Mathf.Min(leaf, vein));
                        }
                    }

                    color = PaintOpaque(color, def.Accent, Polygon(x, y, new Vector2(104f, 4f), new Vector2(116f, 22f), new Vector2(140f, 22f),
                        new Vector2(152f, 4f), new Vector2(128f, 14f)));
                    return PaintOpaque(color, Color.Lerp(def.Accent, Color.black, 0.2f), Ellipse(x, y, 128f, 20f, 10f, 8f));
                }
                case FrameStyle.Stars:
                {
                    // Ночное небо кольцом: тёмно-синий обод с золотым кантом, восемь звёзд и искры.
                    color = RingPaint(color, radius, 102f, 127f, def.Main, Color.Lerp(def.Main, Color.black, 0.5f));
                    color = RingPaint(color, radius, 102f, 105f, def.Accent, def.Accent);
                    color = RingPaint(color, radius, 124f, 127f, def.Accent, def.Accent);
                    for (var i = 0; i < 8; i++)
                    {
                        var a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 8f;
                        var sx = center + Mathf.Cos(a) * 114.5f;
                        var sy = center + Mathf.Sin(a) * 114.5f;
                        var big = i % 2 == 0;
                        color = PaintOpaque(color, def.Accent, StarCoverage(x - sx, y - sy, big ? 13f : 9f, big ? 5.5f : 4f));
                    }

                    color = Paint(color, Color.white, Sparkle(x, y, 36f, 196f, 12f));
                    color = Paint(color, Color.white, Sparkle(x, y, 214f, 64f, 10f));
                    return Paint(color, Color.white, Sparkle(x, y, 196f, 224f, 7f));
                }
                default:
                    return color;
            }
        }

        /// <summary>Точки грани кости 1…6 в квадрате с центром (cx, cy).</summary>
        private static Color Pips(Color color, float x, float y, float cx, float cy, int face, Color pip)
        {
            const float step = 7.5f;
            const float size = 3.4f;
            var center = face % 2 == 1;
            if (center)
                color = PaintOpaque(color, pip, Ellipse(x, y, cx, cy, size, size));
            if (face >= 2)
            {
                color = PaintOpaque(color, pip, Ellipse(x, y, cx - step, cy + step, size, size));
                color = PaintOpaque(color, pip, Ellipse(x, y, cx + step, cy - step, size, size));
            }

            if (face >= 4)
            {
                color = PaintOpaque(color, pip, Ellipse(x, y, cx + step, cy + step, size, size));
                color = PaintOpaque(color, pip, Ellipse(x, y, cx - step, cy - step, size, size));
            }

            if (face == 6)
            {
                color = PaintOpaque(color, pip, Ellipse(x, y, cx - step, cy, size, size));
                color = PaintOpaque(color, pip, Ellipse(x, y, cx + step, cy, size, size));
            }

            return color;
        }

        /// <summary>Эллипс, повёрнутый на angle (радианы) вокруг своего центра.</summary>
        private static float Rotated(float x, float y, float cx, float cy, float rx, float ry, float angle)
        {
            var dx = x - cx;
            var dy = y - cy;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            return Ellipse(dx * cos + dy * sin, -dx * sin + dy * cos, 0f, 0f, rx, ry);
        }

        // ---------- Вкладка «Уровни» в профиле ----------

        /// <summary>Строка пути уровней: значок с номером, значок вещи, награды, состояние справа.</summary>
        private static LevelRewardRowView LevelRewardRowPart()
        {
            return Part("LevelRewardRow", () =>
            {
                var root = Box("LevelRewardRow", null, new Color(1f, 1f, 1f, 0.04f));
                root.sizeDelta = new Vector2(1200f, 88f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 88f;
                element.preferredHeight = 88f;
                element.flexibleWidth = 1f;

                var badge = Box("Badge", root, _ui.Palette.ButtonMuted);
                Corner(badge, new Vector2(0f, 0.5f), new Vector2(68f, 68f), new Vector2(12f, 0f));
                badge.GetComponent<Image>().raycastTarget = false;
                var number = Text("Number", badge, _ui.BoldFont, 32, _ui.Palette.Text);
                AutoSize(number, 16, 32);
                Stretch(number.rectTransform);

                var icon = UiRect("Icon", root).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Corner(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(92f, 0f));

                var rewards = Text("Rewards", root, _ui.Font, 26, _ui.Palette.Text);
                rewards.alignment = TextAlignmentOptions.Left;
                AutoSize(rewards, 16, 26);
                rewards.rectTransform.anchorMin = Vector2.zero;
                rewards.rectTransform.anchorMax = new Vector2(0.74f, 1f);
                rewards.rectTransform.offsetMin = new Vector2(180f, 6f);
                rewards.rectTransform.offsetMax = new Vector2(0f, -6f);

                var state = Text("State", root, _ui.Font, 22, _ui.Palette.TextMuted);
                state.alignment = TextAlignmentOptions.Right;
                AutoSize(state, 14, 22);
                state.rectTransform.anchorMin = new Vector2(0.74f, 0f);
                state.rectTransform.anchorMax = Vector2.one;
                state.rectTransform.offsetMin = new Vector2(8f, 6f);
                state.rectTransform.offsetMax = new Vector2(-16f, -6f);

                root.gameObject.AddComponent<LevelRewardRowView>().EditorSetup(root.GetComponent<Image>(), badge.GetComponent<Image>(), number,
                    icon, rewards, state);
                return root.gameObject;
            }).GetComponent<LevelRewardRowView>();
        }

        /// <summary>
        /// Вкладка «Уровни» в готовом окне профиля (если её нет): сводка и список. Вкладки в шапке сужаются, чтобы пятая
        /// поместилась, заголовок подбирает размер сам.
        /// </summary>
        private static void AddLevelsToProfile()
        {
            EditWindowPrefab<ProfileWindow>("ProfileWindow", "_levelsTab", (root, window) =>
            {
                var pages = FindDeep(root, "Pages") as RectTransform;
                var title = FindDeep(root, "Title");
                var header = title != null ? title.parent as RectTransform : null;
                if (pages == null || header == null)
                    return null;

                foreach (var name in new[] { "StatsTab", "LookTab", "AchievementsTab", "TalentsTab" })
                {
                    var existing = header.Find(name);
                    if (existing != null)
                        Width(existing, 214, 0);
                }

                if (title.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI titleText)
                    AutoSize(titleText, 24, titleText.fontSize);

                var tab = ButtonView("LevelsTab", header, 26, _ui.Palette.ButtonMuted);
                Width(tab, 214, 0);
                var titleIndex = title.GetSiblingIndex();
                // Уровни — сразу после «Статистики»: рядом с тем, что открывает нажатие на уровень.
                var stats = header.Find("StatsTab");
                tab.transform.SetSiblingIndex(stats != null ? stats.GetSiblingIndex() + 1 : titleIndex + 1);

                var page = UiRect("Levels", pages);
                Stretch(page);
                var column = Column(page, 10, 0);
                var summary = Text("Summary", column, _ui.BoldFont, 28, _ui.Palette.Gold);
                summary.alignment = TextAlignmentOptions.Left;
                AutoSize(summary, 16, 28);
                Height(summary, 44);
                var list = VerticalList("List", column, 6, out var content);
                Height(list, 620);
                page.gameObject.SetActive(false);

                window.EditorSetupLevels(tab, page.gameObject, summary, content, LevelRewardRowPart());
                return tab;
            });
        }
    }
}
