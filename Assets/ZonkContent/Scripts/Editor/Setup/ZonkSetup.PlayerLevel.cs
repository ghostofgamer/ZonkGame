using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.UI;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Уровень игрока: конфиг (Configs/Game/PlayerLevel.asset) с наградами и открытием режимов, деталь Parts/PlayerLevel
    /// и её копии в главном меню и итогах партии. Уже созданные окна не пересоздаются: деталь вкладывается в префаб,
    /// если её там нет (заблокированные префабы не трогаются — тогда добавить руками и проставить поле _playerLevel).
    /// </summary>
    public static partial class ZonkSetup
    {
        private static void BuildPlayerLevel(GameConfig config)
        {
            var coins = config.Coins;
            var energy = config.Energy;
            var level = Asset<PlayerLevelConfig>(ConfigsFolder + "/Game/PlayerLevel.asset", l =>
            {
                if (coins != null)
                    l.EveryLevelRewards.Add(Gift(coins, 50));

                // Каждый пятый уровень — заметная награда: монеты и энергия, каждый десятый — больше.
                for (var number = 5; number <= 100; number += 5)
                {
                    var milestone = new PlayerLevelMilestone { Level = number };
                    if (coins != null)
                        milestone.Rewards.Add(Gift(coins, number % 10 == 0 ? 500 : 200));
                    if (energy != null)
                        milestone.Rewards.Add(Gift(energy, number % 10 == 0 ? 5 : 3));
                    l.Milestones.Add(milestone);
                }

                l.Unlocks.Add(new FeatureUnlock { Feature = GameFeature.Tower, Level = 3 });
                l.Unlocks.Add(new FeatureUnlock { Feature = GameFeature.EndlessRun, Level = 5 });
            });

            if (config.PlayerLevel == null)
            {
                config.PlayerLevel = level;
                EditorUtility.SetDirty(config);
            }
        }

        /// <summary>Номер уровня в значке, полоса опыта, подпись над полосой.</summary>
        private static GameObject PlayerLevelPart()
        {
            return Part("PlayerLevel", () =>
            {
                var root = Box("PlayerLevel", null, _ui.Palette.Panel);
                root.sizeDelta = new Vector2(540f, 64f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 64f;
                element.flexibleWidth = 1f;

                var badge = Box("Badge", root, _ui.Palette.Gold);
                Corner(badge, new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(4f, 0f));
                var number = Text("Level", badge, _ui.BoldFont, 30, _ui.Palette.Panel);
                AutoSize(number, 16, 30);
                Stretch(number.rectTransform);

                var caption = Text("Caption", root, _ui.Font, 22, _ui.Palette.Text);
                caption.alignment = TextAlignmentOptions.Left;
                AutoSize(caption, 14, 22);
                caption.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                caption.rectTransform.anchorMax = new Vector2(1f, 1f);
                caption.rectTransform.offsetMin = new Vector2(72f, 0f);
                caption.rectTransform.offsetMax = new Vector2(-12f, -2f);

                var bar = Box("Bar", root, new Color(0f, 0f, 0f, 0.4f));
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0.4f);
                bar.offsetMin = new Vector2(72f, 8f);
                bar.offsetMax = new Vector2(-12f, 0f);

                var fill = Box("Fill", bar, _ui.Palette.Good);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0.3f, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;

                // В меню нажатие на уровень открывает профиль (статистику).
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = root.GetComponent<Image>();
                root.gameObject.AddComponent<PlayerLevelView>().EditorSetup(number, fill, caption);
                return root.gameObject;
            });
        }

        /// <summary>Строка статистики: название слева, значение справа.</summary>
        private static StatRowView StatRowPart()
        {
            return Part("StatRow", () =>
            {
                var root = Box("StatRow", null, new Color(1f, 1f, 1f, 0.04f));
                root.sizeDelta = new Vector2(1200f, 52f);
                var element = root.gameObject.AddComponent<LayoutElement>();
                element.minHeight = 52f;
                element.preferredHeight = 52f;
                element.flexibleWidth = 1f;

                var label = Text("Label", root, _ui.Font, 26, _ui.Palette.TextMuted);
                label.alignment = TextAlignmentOptions.Left;
                AutoSize(label, 16, 26);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = new Vector2(0.6f, 1f);
                label.rectTransform.offsetMin = new Vector2(20f, 0f);
                label.rectTransform.offsetMax = new Vector2(-8f, 0f);

                var value = Text("Value", root, _ui.BoldFont, 26, _ui.Palette.Text);
                value.alignment = TextAlignmentOptions.Right;
                AutoSize(value, 16, 26);
                value.rectTransform.anchorMin = new Vector2(0.6f, 0f);
                value.rectTransform.anchorMax = Vector2.one;
                value.rectTransform.offsetMin = new Vector2(8f, 0f);
                value.rectTransform.offsetMax = new Vector2(-20f, 0f);

                root.gameObject.AddComponent<StatRowView>().EditorSetup(label, value);
                return root.gameObject;
            }).GetComponent<StatRowView>();
        }

        /// <summary>Профиль: заголовок и вкладки, страницы «Статистика» (список строк) и «Облик» (аватары и рамки), «Назад».</summary>
        private static ProfileWindow BuildProfile(RectTransform root)
        {
            Dim(root, 0.5f);
            var panel = CenterPanel(root, new Vector2(1400f, 940f));
            var column = Column(panel, 12, 28);

            var header = Row(column, 16);
            Height(header, 72);
            var title = Text("Title", header, _ui.BoldFont, 44, _ui.Palette.Gold);
            title.alignment = TextAlignmentOptions.Left;
            Width(title, -1, 1);

            // Страницы лежат друг на друге, видна одна.
            var pages = UiRect("Pages", column);
            Height(pages, 680);
            var list = VerticalList("Stats", pages, 6, out var listContent);
            Stretch(list);
            var row = StatRowPart();

            var buttons = Row(column, 24);
            Height(buttons, 80);
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);
            Width(back, 360, 0);
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var window = root.gameObject.AddComponent<ProfileWindow>();
            window.EditorSetup(title, listContent, row, back);
            BuildProfileLook(window, header, pages, list.gameObject);
            BuildProfileAchievements(window, header, pages);
            return window;
        }

        /// <summary>Деталь уровня в главном меню (над колонкой кнопок) и в итогах (под звёздами), если её там нет.</summary>
        private static void AddPlayerLevelToWindows()
        {
            var part = PlayerLevelPart();

            EditWindowPrefab<MainMenuWindow>("MainMenuWindow", "_playerLevel", (root, window) =>
            {
                var level = (RectTransform)Nest(part, root, "PlayerLevel").transform;
                Corner(level, new Vector2(0f, 1f), new Vector2(540f, 64f), new Vector2(48f, -12f));
                return level.GetComponent<PlayerLevelView>();
            });

            EditWindowPrefab<ResultsWindow>("ResultsWindow", "_playerLevel", (root, window) =>
            {
                var stars = root.Find("Panel/Column/Stars") ?? FindDeep(root, "Stars");
                var column = stars != null ? stars.parent : root;
                var level = Nest(part, column, "PlayerLevel");
                if (stars != null)
                    level.transform.SetSiblingIndex(stars.GetSiblingIndex() + 1);

                // Панель выше на строку уровня (не больше 1000 по правилам вёрстки).
                var panel = column.parent as RectTransform;
                if (panel != null && panel.sizeDelta.y > 0f && panel.sizeDelta.y < 880f)
                    panel.sizeDelta = new Vector2(panel.sizeDelta.x, 880f);
                return level.GetComponent<PlayerLevelView>();
            });
        }

        /// <summary>Вложить деталь в готовый префаб окна, если поле field пусто и префаб не заблокирован.</summary>
        private static void EditWindowPrefab<T>(string name, string field, System.Func<Transform, T, Component> add) where T : UiWindow
        {
            var path = Prefabs + "/UI/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var window = root.GetComponent<T>();
                if (window == null)
                    return;

                var serialized = new SerializedObject(window);
                var property = serialized.FindProperty(field);
                if (property == null || property.objectReferenceValue != null)
                    return;

                property.objectReferenceValue = add(root.transform, window);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Zonk] " + name + ": " + field + " added");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }
    }
}
