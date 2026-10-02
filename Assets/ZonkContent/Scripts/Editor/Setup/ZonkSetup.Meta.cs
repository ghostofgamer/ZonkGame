using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Table;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Долгие цели игрока: сундук за победы (Configs/Game/Chest.asset), сезонный путь (Configs/Seasons), достижения
    /// (Configs/Achievements), их окна и кнопки в главном меню. Картинки сундука и кубка — заглушки, нарисованные кодом.
    /// Уже созданные ассеты не перезаписываются: правки в инспекторе остаются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string ChestSpritePath = Root + "/Art/Sprites/Chest.png";
        private const string TrophySpritePath = Root + "/Art/Sprites/Trophy.png";
        private const string SeasonsFolder = ConfigsFolder + "/Seasons";
        private const string AchievementsFolder = ConfigsFolder + "/Achievements";

        private static void BuildMeta(GameConfig config)
        {
            BuildChest(config);
            BuildSeason(config);
            BuildAchievements(config);
        }

        // ---------- Сундук ----------

        private static void BuildChest(GameConfig config)
        {
            if (!File.Exists(ChestSpritePath))
                SavePng(ChestSpritePath, PaintChest());
            var icon = LookSprite(ChestSpritePath);

            var chest = Asset<ChestConfig>(ConfigsFolder + "/Game/Chest.asset", c =>
            {
                c.Coins = config.Coins;
                c.Icon = icon;
                c.Drops.Add(new ChestDrop { Item = false, Weight = 60f, MinCoins = 50, MaxCoins = 150 });
                c.Drops.Add(new ChestDrop { Item = false, Weight = 15f, MinCoins = 200, MaxCoins = 400 });
                c.Drops.Add(new ChestDrop { Item = true, Rarity = Rarity.Common, Weight = 18f, MinCoins = 100, MaxCoins = 200 });
                c.Drops.Add(new ChestDrop { Item = true, Rarity = Rarity.Rare, Weight = 6f, MinCoins = 200, MaxCoins = 350 });
                c.Drops.Add(new ChestDrop { Item = true, Rarity = Rarity.Legendary, Weight = 1f, MinCoins = 400, MaxCoins = 600 });
            });

            // Вещи «из сундука» (подсказка hint.chest) — в список выпадения, если их там нет.
            var changed = false;
            foreach (var id in ChestItemIds())
            {
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/" + id + ".asset");
                if (item != null && !chest.Items.Contains(item))
                {
                    chest.Items.Add(item);
                    changed = true;
                }
            }

            if (chest.Icon == null)
            {
                chest.Icon = icon;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(chest);

            if (config.Chest == null)
            {
                config.Chest = chest;
                EditorUtility.SetDirty(config);
            }
        }

        private static IEnumerable<string> ChestItemIds()
        {
            foreach (var def in Avatars)
            {
                if (def.Source == FromChest)
                    yield return def.Id;
            }

            foreach (var def in Frames)
            {
                if (def.Source == FromChest)
                    yield return def.Id;
            }
        }

        private static Texture2D PaintChest()
        {
            var size = LookSize;
            var pixels = new Color[size * size];
            var wood = new Color(0.55f, 0.32f, 0.15f);
            var woodDark = new Color(0.32f, 0.17f, 0.07f);
            var band = new Color(1f, 0.78f, 0.28f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var color = Color.clear;
                // Тень, корпус, крышка (полукруг сверху), обручи, замок.
                color = PaintOpaque(color, new Color(0f, 0f, 0f, 0.35f), Ellipse(x, y, 128f, 40f, 104f, 16f) * 0.6f);
                color = PaintOpaque(color, woodDark, Box(x, y, 30f, 44f, 226f, 150f));
                color = PaintOpaque(color, wood, Box(x, y, 36f, 50f, 220f, 144f));
                var lid = Mathf.Max(Box(x, y, 30f, 150f, 226f, 176f), Ellipse(x, y, 128f, 176f, 98f, 54f) * (y >= 176f ? 1f : 0f));
                color = PaintOpaque(color, woodDark, lid);
                color = PaintOpaque(color, wood, Mathf.Max(Box(x, y, 36f, 156f, 220f, 176f),
                    Ellipse(x, y, 128f, 176f, 92f, 48f) * (y >= 176f ? 1f : 0f)));
                // Доски.
                if (Box(x, y, 36f, 50f, 220f, 144f) > 0f && Mathf.Abs(Mathf.Repeat(y - 50f, 31f)) < 2f)
                    color = PaintOpaque(color, woodDark, 0.7f);
                // Обручи.
                color = PaintOpaque(color, band, Box(x, y, 58f, 44f, 76f, 222f) * (lid + Box(x, y, 30f, 44f, 226f, 150f) > 0f ? 1f : 0f));
                color = PaintOpaque(color, band, Box(x, y, 180f, 44f, 198f, 222f) * (lid + Box(x, y, 30f, 44f, 226f, 150f) > 0f ? 1f : 0f));
                color = PaintOpaque(color, band, Box(x, y, 30f, 146f, 226f, 156f));
                // Замок.
                color = PaintOpaque(color, band, Box(x, y, 112f, 116f, 144f, 160f));
                color = PaintOpaque(color, woodDark, Ellipse(x, y, 128f, 140f, 5f, 5f));
                color = PaintOpaque(color, woodDark, Box(x, y, 126f, 124f, 130f, 138f));
                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        /// <summary>Кубок белым: в интерфейсе красится цветом редкости (Image.color).</summary>
        private static Texture2D PaintTrophy()
        {
            var size = 128;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cup = Mathf.Max(Polygon(x, y, new Vector2(28f, 112f), new Vector2(100f, 112f), new Vector2(84f, 64f), new Vector2(44f, 64f)),
                    Ellipse(x, y, 64f, 66f, 22f, 14f));
                var handles = Mathf.Clamp01(Ellipse(x, y, 26f, 92f, 16f, 18f) - Ellipse(x, y, 26f, 92f, 9f, 11f)) +
                              Mathf.Clamp01(Ellipse(x, y, 102f, 92f, 16f, 18f) - Ellipse(x, y, 102f, 92f, 9f, 11f));
                var stem = Box(x, y, 58f, 34f, 70f, 56f);
                var foot = Box(x, y, 40f, 16f, 88f, 34f);
                var shape = Mathf.Clamp01(Mathf.Max(Mathf.Max(cup, handles), Mathf.Max(stem, foot)));
                var shade = Mathf.Lerp(0.75f, 1f, x / (float)size);
                var color = new Color(shade, shade, shade, shape);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, color.a), Ellipse(x, y, 50f, 96f, 6f, 12f) * 0.6f);
                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        // ---------- Сезон ----------

        private static void BuildSeason(GameConfig config)
        {
            EnsureFolder(SeasonsFolder);
            var coins = config.Coins;
            var energy = config.Energy;
            Asset<SeasonConfig>(SeasonsFolder + "/Season_1.asset", s =>
            {
                Identity(s, "season_1", "season.s1.name");
                s.Start = "2026-10-01";
                s.End = "2026-11-12";
                s.PointsPerStep = 600;
                s.PassProductId = "season_1_pass";
                for (var step = 1; step <= 30; step++)
                {
                    var data = new SeasonStep();
                    // Бесплатная дорожка: монеты, энергия каждой третьей, две обычные вещи сезона.
                    data.Free.Add(Gift(coins, step % 5 == 0 ? 250 : 100));
                    if (step % 3 == 0 && energy != null)
                        data.Free.Add(Gift(energy, 2));
                    // Платная: больше монет и все эксклюзивы сезона.
                    data.Premium.Add(Gift(coins, step % 5 == 0 ? 500 : 200));
                    if (step % 4 == 0 && energy != null)
                        data.Premium.Add(Gift(energy, 3));
                    s.Steps.Add(data);
                }

                GiveSeason(s, 8, false, "avatar_s1_star");
                GiveSeason(s, 18, false, "frame_s1_rope");
                GiveSeason(s, 10, true, "avatar_s1_skull");
                GiveSeason(s, 20, true, "frame_s1_sea");
                GiveSeason(s, 25, true, "avatar_s1_moon");
                GiveSeason(s, 30, true, "frame_s1_captain");
            });
        }

        private static void GiveSeason(SeasonConfig season, int step, bool premium, string itemId)
        {
            var item = AssetDatabase.LoadAssetAtPath<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/" + itemId + ".asset");
            if (item == null || step < 1 || step > season.Steps.Count)
                return;
            var list = premium ? season.Steps[step - 1].Premium : season.Steps[step - 1].Free;
            list.Add(new ContentReward { Item = item });
        }

        // ---------- Достижения ----------

        private struct AchievementLine
        {
            public string Id;
            public string Key;
            public AchievementGoal Goal;
            public long[] Tiers;
            public string ItemAtLast;
        }

        private static void BuildAchievements(GameConfig config)
        {
            EnsureFolder(AchievementsFolder);
            if (!File.Exists(TrophySpritePath))
                SavePng(TrophySpritePath, PaintTrophy());
            LookSprite(TrophySpritePath);

            var lines = new[]
            {
                Line("wins", new RecordStatGoal { Stat = RecordStat.Wins }, 1, 10, 50, 100, 250, 500, 1000),
                Line("matches", new RecordStatGoal { Stat = RecordStat.Matches }, 10, 100, 500, 1000, 2500),
                Line("bosses", new RecordStatGoal { Stat = RecordStat.BossWins }, 1, 10, 50, 100),
                LineItem("streak", new RecordStatGoal { Stat = RecordStat.BestStreak }, "frame_ruby", 3, 5, 10),
                LineItem("zonks", new RecordStatGoal { Stat = RecordStat.Zonks }, "avatar_skull", 10, 100, 500, 1000),
                Line("hotdice", new RecordStatGoal { Stat = RecordStat.HotDice }, 1, 10, 50, 100, 500),
                Line("points", new RecordStatGoal { Stat = RecordStat.PointsBanked }, 10000, 100000, 1000000, 5000000),
                Line("bestturn", new RecordStatGoal { Stat = RecordStat.BestTurn }, 1000, 2000, 3000, 5000),
                Line("straight", new RecordStatGoal { Stat = RecordStat.Straights }, 1, 10, 50, 100),
                Line("threepairs", new RecordStatGoal { Stat = RecordStat.ThreePairs }, 1, 10, 50),
                Line("fourkind", new RecordStatGoal { Stat = RecordStat.FourOfAKind }, 1, 10, 50),
                Line("fivekind", new RecordStatGoal { Stat = RecordStat.FiveOfAKind }, 1, 10),
                LineItem("sixkind", new RecordStatGoal { Stat = RecordStat.SixOfAKind }, "avatar_six", 1),
                Line("level", new ProgressGoal { Stat = ProgressStat.Level }, 5, 10, 20, 30, 50, 75, 100),
                Line("stars", new ProgressGoal { Stat = ProgressStat.Stars }, 10, 25, 50, 75, 100),
                Line("tower", new ProgressGoal { Stat = ProgressStat.TowerFloor }, 5, 10, 20, 30),
                Line("endless", new ProgressGoal { Stat = ProgressStat.EndlessFloor }, 5, 10, 20, 30),
                Line("chests", new ProgressGoal { Stat = ProgressStat.ChestsOpened }, 1, 10, 50, 100),
                LineItem("dread", new ProgressGoal { Stat = ProgressStat.DreadBeaten }, "frame_royal", 1, 4),
                Line("mastery", new MasteryDiceGoal { MinLevel = 1 }, 1, 5, 10),
                Line("mastergold", new MasteryDiceGoal { MinLevel = 3 }, 1, 5, 10),
                Line("avatars", new CollectionGoal { SlotId = SlotIds.Avatar }, 5, 10),
                Line("frames", new CollectionGoal { SlotId = SlotIds.Frame }, 5, 10),
                Line("hours", new RecordStatGoal { Stat = RecordStat.HoursPlayed }, 1, 10, 50),
            };

            for (var line = 0; line < lines.Length; line++)
            {
                var def = lines[line];
                for (var tier = 0; tier < def.Tiers.Length; tier++)
                {
                    var target = def.Tiers[tier];
                    var last = tier == def.Tiers.Length - 1;
                    var rarity = def.Tiers.Length > 1 && last ? Rarity.Legendary
                        : def.Tiers.Length > 2 && tier >= def.Tiers.Length - 3 ? Rarity.Rare
                        : Rarity.Common;
                    var id = "ach_" + def.Id + "_" + target;
                    var lineIndex = line;
                    var tierNumber = def.Tiers.Length > 1 ? tier + 1 : 0;
                    Asset<AchievementConfig>(AchievementsFolder + "/" + id + ".asset", a =>
                    {
                        Identity(a, id, "ach." + def.Key);
                        a.DescriptionKey = "ach." + def.Key + ".desc";
                        a.Tier = tierNumber;
                        a.Order = lineIndex * 100 + tier;
                        a.Rarity = rarity;
                        a.Goal = CloneGoal(def.Goal);
                        a.Target = target;
                        var reward = rarity == Rarity.Legendary ? 1000 : rarity == Rarity.Rare ? 400 : 100;
                        if (config.Coins != null)
                            a.Rewards.Add(Gift(config.Coins, reward));
                        if (rarity != Rarity.Common && config.Energy != null)
                            a.Rewards.Add(Gift(config.Energy, rarity == Rarity.Legendary ? 10 : 3));
                        if (last && !string.IsNullOrEmpty(def.ItemAtLast))
                        {
                            var item = AssetDatabase.LoadAssetAtPath<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/" + def.ItemAtLast + ".asset");
                            if (item != null)
                                a.Rewards.Add(new ContentReward { Item = item });
                        }
                    });
                }
            }
        }

        private static AchievementLine Line(string key, AchievementGoal goal, params long[] tiers)
        {
            return new AchievementLine { Id = key, Key = key, Goal = goal, Tiers = tiers };
        }

        private static AchievementLine LineItem(string key, AchievementGoal goal, string item, params long[] tiers)
        {
            return new AchievementLine { Id = key, Key = key, Goal = goal, Tiers = tiers, ItemAtLast = item };
        }

        /// <summary>Своя копия цели в каждом ассете: [SerializeReference] не должен делить объект между ассетами.</summary>
        private static AchievementGoal CloneGoal(AchievementGoal goal)
        {
            return goal == null ? null : (AchievementGoal)JsonUtility.FromJson(JsonUtility.ToJson(goal), goal.GetType());
        }

        // ---------- Детали и окна ----------

        /// <summary>Ступень сезона: номер, бесплатная награда сверху, платная снизу.</summary>
        private static SeasonStepView SeasonStepPart()
        {
            return Part("SeasonStep", () =>
            {
                var root = UiRect("SeasonStep", null);
                root.sizeDelta = new Vector2(170f, 560f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 170f;
                element.minWidth = 170f;
                var column = root.gameObject.AddComponent<VerticalLayoutGroup>();
                column.spacing = 10;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandHeight = false;

                var number = Text("Number", root, _ui.BoldFont, 32, _ui.Palette.TextMuted);
                Height(number, 50);
                var free = SeasonCell("Free", root, out var freeBackground, out var freeIcon, out var freeText);
                var premium = SeasonCell("Premium", root, out var premiumBackground, out var premiumIcon, out var premiumText);

                var view = root.gameObject.AddComponent<SeasonStepView>();
                view.EditorSetup(number, free, freeBackground, freeIcon, freeText, premium, premiumBackground, premiumIcon, premiumText);
                return root.gameObject;
            }).GetComponent<SeasonStepView>();
        }

        private static Button SeasonCell(string name, Transform parent, out Image background, out Image icon, out TMP_Text text)
        {
            var cell = Box(name, parent, _ui.Palette.ButtonMuted);
            Height(cell, 240);
            background = cell.GetComponent<Image>();
            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            icon = UiRect("Icon", cell).gameObject.AddComponent<Image>();
            Corner(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(120f, 120f), new Vector2(0f, -16f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var label = Text("Text", cell, _ui.Font, 22, _ui.Palette.Text);
            AutoSize(label, 14, 22);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            label.rectTransform.offsetMin = new Vector2(8f, 6f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            text = label;
            return button;
        }

        /// <summary>Строка достижения: кубок, название и описание, награда справа, полоса прогресса снизу.</summary>
        private static AchievementRowView AchievementRowPart()
        {
            return Part("AchievementRow", () =>
            {
                var root = Box("AchievementRow", null, new Color(1f, 1f, 1f, 0.05f));
                root.sizeDelta = new Vector2(1200f, 116f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 116f;
                element.preferredHeight = 116f;
                element.flexibleWidth = 1f;

                var icon = UiRect("Icon", root).gameObject.AddComponent<Image>();
                icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TrophySpritePath);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Corner(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(88f, 88f), new Vector2(14f, 0f));

                var title = Text("Title", root, _ui.BoldFont, 28, _ui.Palette.Text);
                title.alignment = TextAlignmentOptions.Left;
                AutoSize(title, 16, 28);
                title.rectTransform.anchorMin = new Vector2(0f, 0.62f);
                title.rectTransform.anchorMax = new Vector2(0.68f, 1f);
                title.rectTransform.offsetMin = new Vector2(116f, 0f);
                title.rectTransform.offsetMax = new Vector2(0f, -6f);

                var description = Text("Description", root, _ui.Font, 22, _ui.Palette.TextMuted);
                description.alignment = TextAlignmentOptions.Left;
                AutoSize(description, 14, 22);
                description.rectTransform.anchorMin = new Vector2(0f, 0.34f);
                description.rectTransform.anchorMax = new Vector2(0.68f, 0.62f);
                description.rectTransform.offsetMin = new Vector2(116f, 0f);
                description.rectTransform.offsetMax = Vector2.zero;

                var reward = Text("Reward", root, _ui.Font, 22, _ui.Palette.Gold);
                reward.alignment = TextAlignmentOptions.Right;
                AutoSize(reward, 14, 22);
                reward.rectTransform.anchorMin = new Vector2(0.68f, 0.34f);
                reward.rectTransform.anchorMax = new Vector2(1f, 1f);
                reward.rectTransform.offsetMin = new Vector2(8f, 0f);
                reward.rectTransform.offsetMax = new Vector2(-16f, -6f);

                var bar = Box("Bar", root, new Color(0f, 0f, 0f, 0.4f));
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0.28f);
                bar.offsetMin = new Vector2(116f, 10f);
                bar.offsetMax = new Vector2(-16f, 0f);
                var fill = Box("Fill", bar, _ui.Palette.Good);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0.3f, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                var progress = Text("Progress", bar, _ui.BoldFont, 18, _ui.Palette.Text);
                Stretch(progress.rectTransform);

                root.gameObject.AddComponent<AchievementRowView>().EditorSetup(root.GetComponent<Image>(), icon, title, description, fill,
                    progress, reward);
                return root.gameObject;
            }).GetComponent<AchievementRowView>();
        }

        /// <summary>Вкладка и страница «Достижения» в профиле: сводка и список.</summary>
        private static void BuildProfileAchievements(ProfileWindow window, RectTransform header, RectTransform pages)
        {
            var tab = ButtonView("AchievementsTab", header, 26, _ui.Palette.ButtonMuted);
            Width(tab, 260, 0);

            var page = UiRect("Achievements", pages);
            Stretch(page);
            var column = Column(page, 10, 0);
            var summary = Text("Summary", column, _ui.BoldFont, 28, _ui.Palette.Gold);
            summary.alignment = TextAlignmentOptions.Left;
            Height(summary, 44);
            var list = VerticalList("List", column, 8, out var content);
            Height(list, 620);
            page.gameObject.SetActive(false);

            window.EditorSetupAchievements(tab, page.gameObject, summary, content, AchievementRowPart());
        }

        private static ChestWindow BuildChestWindow(RectTransform root)
        {
            Dim(root, 0.6f);
            var panel = CenterPanel(root, new Vector2(1000f, 900f));
            var column = Column(panel, 12, 28);
            var title = Text("Title", column, _ui.BoldFont, 48, _ui.Palette.Gold);
            Height(title, 72);

            var stage = UiRect("Stage", column);
            Height(stage, 440);
            var glow = UiRect("Glow", stage).gameObject.AddComponent<Image>();
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(420f, 420f));
            glow.color = Color.clear;
            glow.raycastTarget = false;
            var chest = UiRect("Chest", stage).gameObject.AddComponent<Image>();
            Place(chest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(320f, 320f));
            chest.preserveAspect = true;
            chest.raycastTarget = false;
            var prize = UiRect("Prize", stage).gameObject.AddComponent<Image>();
            Place(prize.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(240f, 240f));
            prize.preserveAspect = true;
            prize.raycastTarget = false;

            var result = Text("Result", column, _ui.BoldFont, 34, _ui.Palette.Gold);
            AutoSize(result, 20, 34);
            Height(result, 90);
            var count = Text("Count", column, _ui.Font, 26, _ui.Palette.TextMuted);
            Height(count, 44);

            var buttons = Row(column, 24);
            Height(buttons, 84);
            var open = ButtonView("Open", buttons, 32, _ui.Palette.ButtonAccent);
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);

            var window = root.gameObject.AddComponent<ChestWindow>();
            window.EditorSetup(title, chest, glow, prize, result, count, open, back);
            return window;
        }

        private static SeasonWindow BuildSeasonWindow(RectTransform root)
        {
            Dim(root, 0.6f);
            var panel = CenterPanel(root, new Vector2(1720f, 980f));
            var column = Column(panel, 12, 28);

            var header = Row(column, 16);
            Height(header, 72);
            var title = Text("Title", header, _ui.BoldFont, 44, _ui.Palette.Gold);
            title.alignment = TextAlignmentOptions.Left;
            Width(title, -1, 1);
            var timer = Text("Timer", header, _ui.Font, 26, _ui.Palette.TextMuted);
            timer.alignment = TextAlignmentOptions.Right;
            Width(timer, 420, 0);

            var progressRow = Row(column, 16);
            Height(progressRow, 44);
            var stepText = Text("Step", progressRow, _ui.BoldFont, 26, _ui.Palette.Text);
            stepText.alignment = TextAlignmentOptions.Left;
            Width(stepText, 320, 0);
            var bar = Box("Bar", progressRow, new Color(0f, 0f, 0f, 0.4f));
            Width(bar, -1, 1);
            var fill = Box("Fill", bar, _ui.Palette.Gold);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.3f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            var trackRow = Row(column, 12);
            Height(trackRow, 600);
            trackRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var labels = UiRect("Labels", trackRow);
            Width(labels, 150, 0);
            var freeLabel = Text("FreeLabel", labels, _ui.BoldFont, 26, _ui.Palette.Good);
            freeLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            freeLabel.rectTransform.anchorMax = new Vector2(1f, 0.9f);
            freeLabel.rectTransform.offsetMin = freeLabel.rectTransform.offsetMax = Vector2.zero;
            var premiumLabel = Text("PremiumLabel", labels, _ui.BoldFont, 26, _ui.Palette.RarityLegendary);
            premiumLabel.rectTransform.anchorMin = new Vector2(0f, 0.08f);
            premiumLabel.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            premiumLabel.rectTransform.offsetMin = premiumLabel.rectTransform.offsetMax = Vector2.zero;
            var track = HorizontalList("Track", trackRow, 12, out var trackContent);
            Width(track, -1, 1);
            var step = SeasonStepPart();

            var bottom = Row(column, 20);
            Height(bottom, 84);
            var passInfo = Text("PassInfo", bottom, _ui.Font, 24, _ui.Palette.Text);
            passInfo.alignment = TextAlignmentOptions.Left;
            AutoSize(passInfo, 14, 24);
            Width(passInfo, -1, 1);
            var buy = ButtonView("Buy", bottom, 28, _ui.Palette.ButtonAccent);
            Width(buy, 420, 0);
            var back = ButtonView("Back", bottom, 28, _ui.Palette.ButtonMuted);
            Width(back, 280, 0);

            var window = root.gameObject.AddComponent<SeasonWindow>();
            window.EditorSetup(title, timer, stepText, fill, freeLabel, premiumLabel, trackContent, step, passInfo, buy, back);
            return window;
        }

        /// <summary>Кнопки «Сундук» и «Сезон» в правом нижнем углу главного меню, если их там нет.</summary>
        private static void AddMetaButtonsToMainMenu()
        {
            EditWindowPrefab<MainMenuWindow>("MainMenuWindow", "_chest", (root, window) =>
            {
                var button = ButtonView("Chest", root, 28, _ui.Palette.Bank);
                Corner((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(400f, 84f), new Vector2(-24f, 24f));
                return button;
            });

            EditWindowPrefab<MainMenuWindow>("MainMenuWindow", "_season", (root, window) =>
            {
                var button = ButtonView("Season", root, 28, _ui.Palette.ButtonAccent);
                Corner((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(400f, 84f), new Vector2(-24f, 120f));
                return button;
            });
        }
    }
}
