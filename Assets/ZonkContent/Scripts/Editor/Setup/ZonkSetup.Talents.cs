using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Дерево талантов (Configs/Talents): три ветки — Купец (выгода), Мастер (кости, опыт, рогалики), Коллекционер (облик,
    /// магазин); значки веток — заглушки кодом (Art/Sprites/Talents). Очки за трудные достижения (AchievementConfig.TalentPoints:
    /// редкие 1, легендарные 2, самые трудные 3) — проставляются, пока у редкого/легендарного 0. Подсказке хода — ИИ «эксперт».
    /// Вкладка «Таланты» и кнопка подсказки в настройках добавляются в готовые окна (EditWindowPrefab), значок свободных очков —
    /// в деталь Parts/PlayerLevel. Уже созданные ассеты не перезаписываются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string TalentsFolder = ConfigsFolder + "/Talents";
        private const string TalentIconsFolder = Root + "/Art/Sprites/Talents";

        private struct TalentDef
        {
            public string Id;
            public TalentBranch Branch;
            public int Row;
            public int Order;
            public string Requires;
            public int BranchPoints;
            public int MaxRank;
            public int Cost;
            public TalentEffect Effect;
            public float Value;
            public bool Capstone;
        }

        private static TalentDef Tal(string id, TalentBranch branch, int row, int order, TalentEffect effect, float value, int maxRank,
            int branchPoints = 0, string requires = null, int cost = 1, bool capstone = false)
        {
            return new TalentDef
            {
                Id = id, Branch = branch, Row = row, Order = order, Effect = effect, Value = value, MaxRank = maxRank,
                BranchPoints = branchPoints, Requires = requires, Cost = cost, Capstone = capstone,
            };
        }

        /// <summary>Узлы дерева. Числа «Купца» — черновые: подкрутить при пересчёте экономики.</summary>
        private static readonly TalentDef[] TalentDefs =
        {
            // Купец: выгода.
            Tal("talent_coins", TalentBranch.Merchant, 0, 0, TalentEffect.CoinsOnWinPercent, 5f, 5),
            Tal("talent_energy", TalentBranch.Merchant, 0, 1, TalentEffect.EnergyMax, 1f, 2),
            Tal("talent_chest", TalentBranch.Merchant, 1, 0, TalentEffect.ChestWinsMinus, 1f, 1, 3, "talent_coins"),
            Tal("talent_regen", TalentBranch.Merchant, 1, 1, TalentEffect.EnergyRegenPercent, 10f, 3, 3, "talent_energy"),
            Tal("talent_stakes", TalentBranch.Merchant, 2, 0, TalentEffect.StakeOptionsExtra, 1f, 2, 5),
            Tal("talent_refund", TalentBranch.Merchant, 2, 1, TalentEffect.StakeRefundPercent, 10f, 3, 5, "talent_stakes"),
            Tal("talent_daily", TalentBranch.Merchant, 3, 0, TalentEffect.DailyQuestsExtra, 1f, 1, 8),
            Tal("talent_reroll", TalentBranch.Merchant, 3, 1, TalentEffect.QuestRerollsExtra, 1f, 2, 8),
            Tal("talent_lucky_day", TalentBranch.Merchant, 4, 0, TalentEffect.FirstWinDoubleCoins, 1f, 1, 14, cost: 3, capstone: true),

            // Мастер: кости, опыт, рогалики.
            Tal("talent_xp", TalentBranch.Master, 0, 0, TalentEffect.XpPercent, 5f, 4),
            Tal("talent_mastery", TalentBranch.Master, 0, 1, TalentEffect.MasteryPercent, 10f, 3),
            Tal("talent_presets", TalentBranch.Master, 1, 0, TalentEffect.DicePresetsExtra, 1f, 2, 3),
            Tal("talent_hint", TalentBranch.Master, 1, 1, TalentEffect.BestMoveHint, 1f, 1, 3),
            Tal("talent_run_hearts", TalentBranch.Master, 2, 0, TalentEffect.RunStartHearts, 1f, 2, 5),
            Tal("talent_tower_hearts", TalentBranch.Master, 2, 1, TalentEffect.TowerStartHearts, 1f, 2, 5),
            Tal("talent_run_offers", TalentBranch.Master, 3, 0, TalentEffect.RunPerkOffersExtra, 1f, 1, 9, "talent_run_hearts"),
            Tal("talent_run_reroll", TalentBranch.Master, 3, 1, TalentEffect.RunFreeRerolls, 1f, 2, 9, "talent_run_hearts"),
            Tal("talent_tower_revive", TalentBranch.Master, 3, 2, TalentEffect.TowerFreeRevives, 1f, 1, 9, "talent_tower_hearts"),
            Tal("talent_veteran", TalentBranch.Master, 4, 0, TalentEffect.RunStartPerk, 1f, 1, 15, cost: 3, capstone: true),

            // Коллекционер: облик и магазин.
            Tal("talent_discount", TalentBranch.Collector, 0, 0, TalentEffect.ShopCoinDiscountPercent, 3f, 5),
            Tal("talent_title", TalentBranch.Collector, 0, 1, TalentEffect.Title, 1f, 1),
            Tal("talent_celebrate", TalentBranch.Collector, 1, 0, TalentEffect.WinCelebration, 1f, 1, 3),
            Tal("talent_decor", TalentBranch.Collector, 2, 0, TalentEffect.DecorSpotsExtra, 1f, 3, 5),
            Tal("talent_patron", TalentBranch.Collector, 3, 0, TalentEffect.ShopCoinDiscountPercent, 10f, 1, 10, cost: 3, capstone: true),
        };

        /// <summary>Самые трудные достижения: 3 очка (остальные легендарные — 2, редкие — 1).</summary>
        private static readonly string[] HardestAchievements = { "ach_wins_1000", "ach_dread_4", "ach_sixkind_1", "ach_level_100" };

        private static void BuildTalents(GameConfig config)
        {
            EnsureFolder(TalentsFolder);
            EnsureFolder(TalentIconsFolder);
            var icons = TalentIcons();

            // Сначала все узлы, потом связи «требует» (узел может ссылаться на ещё не созданный).
            foreach (var def in TalentDefs)
            {
                var d = def;
                Asset<TalentConfig>(TalentsFolder + "/" + d.Id + ".asset", t =>
                {
                    Identity(t, d.Id, "talent." + d.Id.Substring("talent_".Length));
                    t.DescriptionKey = t.NameKey + ".desc";
                    t.Branch = d.Branch;
                    t.Row = d.Row;
                    t.Order = d.Order;
                    t.BranchPointsRequired = d.BranchPoints;
                    t.MaxRank = d.MaxRank;
                    t.CostPerRank = d.Cost;
                    t.Effect = d.Effect;
                    t.ValuePerRank = d.Value;
                    t.Capstone = d.Capstone;
                    t.Icon = icons[(int)d.Branch];
                });
            }

            foreach (var def in TalentDefs)
            {
                if (string.IsNullOrEmpty(def.Requires))
                    continue;
                var talent = AssetDatabase.LoadAssetAtPath<TalentConfig>(TalentsFolder + "/" + def.Id + ".asset");
                if (talent != null && talent.Requires == null)
                {
                    talent.Requires = AssetDatabase.LoadAssetAtPath<TalentConfig>(TalentsFolder + "/" + def.Requires + ".asset");
                    EditorUtility.SetDirty(talent);
                }
            }

            EnsureAchievementTalentPoints();

            if (config.HintAi == null)
            {
                config.HintAi = AssetDatabase.LoadAssetAtPath<AiProfileConfig>(ConfigsFolder + "/Ai/Ai_expert.asset");
                EditorUtility.SetDirty(config);
            }
        }

        private static void EnsureAchievementTalentPoints()
        {
            foreach (var achievement in FindAll<AchievementConfig>(ConfigsFolder))
            {
                if (achievement.TalentPoints > 0 || achievement.Rarity == Rarity.Common)
                    continue;

                achievement.TalentPoints = System.Array.IndexOf(HardestAchievements, achievement.Id) >= 0 ? 3
                    : achievement.Rarity == Rarity.Legendary ? 2
                    : 1;
                EditorUtility.SetDirty(achievement);
            }
        }

        // ---------- Значки ----------

        /// <summary>Значки веток белым по прозрачному (окно красит их цветом состояния): монета, кость, самоцвет.</summary>
        private static Sprite[] TalentIcons()
        {
            var names = new[] { "Talent_Merchant", "Talent_Master", "Talent_Collector" };
            var sprites = new Sprite[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var path = TalentIconsFolder + "/" + names[i] + ".png";
                if (!File.Exists(path))
                    SavePng(path, PaintTalentIcon(i));
                sprites[i] = LookSprite(path);
            }

            return sprites;
        }

        private static Texture2D PaintTalentIcon(int branch)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            var white = Color.white;
            var shade = new Color(0.7f, 0.7f, 0.7f, 1f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var color = Color.clear;
                switch (branch)
                {
                    case 0:
                        // Монета с ободком и знаком.
                        color = PaintOpaque(color, shade, Ellipse(x, y, 64f, 64f, 52f, 52f));
                        color = PaintOpaque(color, white, Ellipse(x, y, 64f, 64f, 44f, 44f));
                        color = PaintOpaque(color, shade, Box(x, y, 58f, 34f, 70f, 94f));
                        color = PaintOpaque(color, shade, Mathf.Clamp01(Ellipse(x, y, 64f, 74f, 20f, 13f) - Ellipse(x, y, 64f, 74f, 12f, 6f)));
                        color = PaintOpaque(color, shade, Mathf.Clamp01(Ellipse(x, y, 64f, 54f, 20f, 13f) - Ellipse(x, y, 64f, 54f, 12f, 6f)));
                        break;
                    case 1:
                        // Кость: пятёрка.
                        color = PaintOpaque(color, white, RoundSquare(x, y, 64f, 64f, 48f));
                        color = PaintOpaque(color, shade, Ellipse(x, y, 64f, 64f, 9f, 9f));
                        color = PaintOpaque(color, shade, Ellipse(x, y, 40f, 88f, 9f, 9f));
                        color = PaintOpaque(color, shade, Ellipse(x, y, 88f, 88f, 9f, 9f));
                        color = PaintOpaque(color, shade, Ellipse(x, y, 40f, 40f, 9f, 9f));
                        color = PaintOpaque(color, shade, Ellipse(x, y, 88f, 40f, 9f, 9f));
                        break;
                    default:
                        // Самоцвет с гранью.
                        color = PaintOpaque(color, white, Polygon(x, y, new Vector2(64f, 114f), new Vector2(110f, 70f), new Vector2(64f, 14f),
                            new Vector2(18f, 70f)));
                        color = PaintOpaque(color, shade, Polygon(x, y, new Vector2(64f, 114f), new Vector2(110f, 70f), new Vector2(64f, 70f)));
                        break;
                }

                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        // ---------- Детали и окна ----------

        /// <summary>Узел таланта: подложка-кнопка, значок слева, название, ранг справа, рамка выбора.</summary>
        private static TalentNodeView TalentNodePart()
        {
            return Part("TalentNode", () =>
            {
                var root = Box("TalentNode", null, _ui.Palette.ButtonMuted);
                root.sizeDelta = new Vector2(400f, 76f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 76f;
                element.preferredHeight = 76f;
                element.flexibleWidth = 1f;
                var background = root.GetComponent<Image>();
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = background;

                var selected = Box("Selected", root, _ui.Palette.Gold);
                Stretch(selected);
                selected.offsetMin = new Vector2(-4f, -4f);
                selected.offsetMax = new Vector2(4f, 4f);
                selected.SetAsFirstSibling();
                selected.GetComponent<Image>().raycastTarget = false;
                selected.gameObject.SetActive(false);

                var icon = UiRect("Icon", root).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Corner(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(10f, 0f));

                var name = Text("Name", root, _ui.Font, 22, _ui.Palette.Text);
                name.alignment = TextAlignmentOptions.Left;
                AutoSize(name, 14, 22);
                name.rectTransform.anchorMin = Vector2.zero;
                name.rectTransform.anchorMax = new Vector2(0.78f, 1f);
                name.rectTransform.offsetMin = new Vector2(74f, 4f);
                name.rectTransform.offsetMax = new Vector2(0f, -4f);

                var rank = Text("Rank", root, _ui.BoldFont, 22, _ui.Palette.Text);
                rank.alignment = TextAlignmentOptions.Right;
                AutoSize(rank, 14, 22);
                rank.rectTransform.anchorMin = new Vector2(0.78f, 0f);
                rank.rectTransform.anchorMax = Vector2.one;
                rank.rectTransform.offsetMin = Vector2.zero;
                rank.rectTransform.offsetMax = new Vector2(-12f, 0f);

                var view = root.gameObject.AddComponent<TalentNodeView>();
                view.EditorSetup(background, icon, name, rank, selected.gameObject, button);
                return root.gameObject;
            }).GetComponent<TalentNodeView>();
        }

        /// <summary>
        /// Вкладка «Таланты»: кнопка в заголовке, страница поверх остальных: очки, три столбца-ветки (прокрутка),
        /// снизу подробности узла и кнопки «Изучить» / «Сбросить». Возвращает кнопку вкладки.
        /// </summary>
        private static UiButtonView BuildProfileTalents(ProfileWindow window, RectTransform header, RectTransform pages)
        {
            var tab = ButtonView("TalentsTab", header, 26, _ui.Palette.ButtonMuted);
            Width(tab, 240, 0);

            var page = UiRect("Talents", pages);
            Stretch(page);
            var column = Column(page, 8, 0);

            var points = Text("Points", column, _ui.BoldFont, 28, _ui.Palette.Gold);
            points.alignment = TextAlignmentOptions.Left;
            Height(points, 40);

            var branches = Row(column, 14);
            Height(branches, 470);
            var titles = new TMP_Text[3];
            var lists = new RectTransform[3];
            for (var i = 0; i < 3; i++)
            {
                var branch = Column(branches, 6, 0);
                Width(branch, -1, 1);
                titles[i] = Text("Title", branch, _ui.BoldFont, 26, _ui.Palette.Gold);
                Height(titles[i], 36);
                var list = VerticalList("Nodes", branch, 6, out lists[i]);
                Height(list, 424);
            }

            var bottom = Row(column, 16);
            Height(bottom, 120);
            bottom.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var info = Text("Info", bottom, _ui.Font, 22, _ui.Palette.Text);
            info.alignment = TextAlignmentOptions.TopLeft;
            AutoSize(info, 14, 22);
            Width(info, -1, 1);
            var learn = ButtonView("Learn", bottom, 26, _ui.Palette.ButtonAccent);
            Width(learn, 260, 0);
            var reset = ButtonView("Reset", bottom, 22, _ui.Palette.ButtonMuted);
            Width(reset, 260, 0);
            page.gameObject.SetActive(false);

            window.EditorSetupTalents(tab, page.gameObject, points, titles, lists, TalentNodePart(), info, learn, reset, TalentIcons());
            return tab;
        }

        /// <summary>Вкладка талантов в окне профиля, кнопка подсказки в настройках, значок очков в детали уровня.</summary>
        private static void AddTalentsToWindows()
        {
            EditWindowPrefab<ProfileWindow>("ProfileWindow", "_talentsTab", (root, window) =>
            {
                var pages = FindDeep(root, "Pages") as RectTransform;
                var title = FindDeep(root, "Title");
                var header = title != null ? title.parent as RectTransform : null;
                return pages != null && header != null ? BuildProfileTalents(window, header, pages) : null;
            });

            EditWindowPrefab<SettingsWindow>("SettingsWindow", "_hint", (root, window) =>
            {
                var back = FindDeep(root, "Back");
                if (back == null)
                    return null;

                var hint = ButtonView("Hint", back.parent, 28, _ui.Palette.Button);
                Height(hint, 72);
                hint.transform.SetSiblingIndex(back.GetSiblingIndex());
                // Окно выше на кнопку (не больше 1000 по правилам вёрстки).
                var panel = back.parent.parent as RectTransform;
                if (panel != null && panel.sizeDelta.y > 0f && panel.sizeDelta.y < 860f)
                    panel.sizeDelta = new Vector2(panel.sizeDelta.x, 860f);
                window.EditorSetupHint(hint);
                return hint;
            });

            AddTalentBadgeToLevelPart();
        }

        /// <summary>Значок «есть свободные очки» в правом верхнем углу значка уровня (деталь Parts/PlayerLevel).</summary>
        private static void AddTalentBadgeToLevelPart()
        {
            var path = PartsFolder + "/PlayerLevel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<PlayerLevelView>();
                if (view == null)
                    return;
                var serialized = new SerializedObject(view);
                var field = serialized.FindProperty("_talentBadge");
                if (field == null || field.objectReferenceValue != null)
                    return;

                var badge = Box("TalentBadge", root.transform, _ui.Palette.Bad);
                Corner(badge, new Vector2(0f, 1f), new Vector2(26f, 26f), new Vector2(44f, 6f));
                badge.GetComponent<Image>().raycastTarget = false;
                var mark = Text("Mark", badge, _ui.BoldFont, 20, _ui.Palette.Text);
                mark.text = "+";
                Stretch(mark.rectTransform);
                badge.gameObject.SetActive(false);

                field.objectReferenceValue = badge.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
