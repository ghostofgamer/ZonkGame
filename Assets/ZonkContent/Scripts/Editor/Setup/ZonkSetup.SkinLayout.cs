using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Вёрстка под комикс-оформление и палец: мелкий материал, табло партии, меню. Правит готовые префабы, поэтому
    /// работает и у окон из снимков. Повторный запуск ничего не меняет, заблокированные префабы не трогает.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static Material DisplaySmallMaterial(TMP_FontAsset font)
        {
            var path = Fonts + "/" + DisplayFontName + " Comic Small.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(font.material) { name = DisplayFontName + " Comic Small" };
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.14f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Строка с фиксированной высотой не должна расти: горизонтальная раскладка сообщает гибкую высоту (кнопки итогов выросли на всё окно).</summary>
        private static bool FixStretchingRows(GameObject root)
        {
            var changed = false;
            foreach (var layout in root.GetComponentsInChildren<LayoutElement>(true))
            {
                if (layout.preferredHeight > 0f && layout.flexibleHeight < 0f && layout.GetComponent<HorizontalLayoutGroup>() != null)
                {
                    layout.flexibleHeight = 0f;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Табличка игрока: крупный счёт. Деталь генератор не пересоздаёт, поэтому правится здесь.</summary>
        private static void UpgradeHudPanelPart()
        {
            var path = PartsFolder + "/HudPlayerPanel.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var score = FindDeep(root.transform, "Score");
                var playerName = FindDeep(root.transform, "Name");
                if (score == null || playerName == null || Mathf.Approximately(score.GetComponent<TMP_Text>().fontSize, 60f))
                    return;

                var scoreText = score.GetComponent<TMP_Text>();
                scoreText.fontSize = 60f;
                scoreText.font = _ui.DisplayFont;
                scoreText.fontSharedMaterial = _ui.DisplayMaterial;
                Height(scoreText, 66f);
                var nameText = playerName.GetComponent<TMP_Text>();
                nameText.fontSizeMax = 26f;
                Height(nameText, 34f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Партия: очки хода — на табличке под тем, кто ходит (верх экрана свободен для соперника и его аксессуаров),
        /// подсказка выше огня на кнопке, «Фразы» и «Сдаться» под палец.
        /// </summary>
        private static void UpgradeHudLayout()
        {
            var path = Prefabs + "/UI/MatchHudWindow.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plate = root.transform.Find("TurnPlate") as RectTransform;
                var turn = root.transform.Find("Turn") as RectTransform;
                if (plate == null || turn == null)
                    return;

                Corner(plate, new Vector2(0f, 1f), new Vector2(300f, 96f), new Vector2(24f, -150f));
                turn.SetParent(plate, false);
                turn.anchorMin = Vector2.zero;
                turn.anchorMax = Vector2.one;
                turn.pivot = new Vector2(0.5f, 0.5f);
                turn.offsetMin = new Vector2(14f, 12f);
                turn.offsetMax = new Vector2(-18f, -8f);
                var turnText = turn.GetComponent<TMP_Text>();
                turnText.enableAutoSizing = true;
                turnText.fontSizeMin = 18f;
                turnText.fontSizeMax = 52f;

                if (root.transform.Find("Hint") is RectTransform hint)
                    Corner(hint, new Vector2(0.5f, 0f), new Vector2(1100f, 60f), new Vector2(0f, 200f));

                if (root.transform.Find("Surrender") is RectTransform surrender)
                    Corner(surrender, new Vector2(1f, 0f), new Vector2(260f, 88f), new Vector2(-24f, 24f));
                if (root.transform.Find("Phrases") is RectTransform phrases)
                    Corner(phrases, new Vector2(0f, 0f), new Vector2(260f, 88f), new Vector2(24f, 24f));

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Меню под палец: шире, большая главная кнопка, остальное парами. Ссылки окна на кнопки не меняются.</summary>
        private static void UpgradeMainMenu()
        {
            var path = Prefabs + "/UI/MainMenuWindow.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var campaign = FindDeep(root.transform, "Campaign");
                if (campaign == null || FindDeep(root.transform, "MenuRow1") != null)
                    return;

                var column = (RectTransform)campaign.parent;
                Corner((RectTransform)column.parent, new Vector2(0f, 0.5f), new Vector2(780f, 860f), new Vector2(48f, -30f));

                var title = FindDeep(root.transform, "Title");
                if (title != null)
                {
                    var titleText = title.GetComponent<TMP_Text>();
                    Height(titleText, 110f);
                    // Справа значок правил: заголовок на него не наезжает.
                    titleText.margin = new Vector4(0f, 0f, 110f, 0f);
                    titleText.enableAutoSizing = true;
                    titleText.fontSizeMin = 30f;
                    titleText.fontSizeMax = 64f;
                }

                var main = campaign.GetComponent<UiButtonView>();
                Height(main, 132f);
                if (main.Label != null)
                {
                    main.Label.enableAutoSizing = true;
                    main.Label.fontSizeMax = 60f;
                    main.Label.fontSharedMaterial = _ui.DisplayMaterial;
                }

                var pairs = new[]
                {
                    new[] { "EndlessRun", "Tower" },
                    new[] { "Shop", "Dice" },
                    new[] { "Quests", "HotSeat" },
                    new[] { "Leaderboards", "Settings" },
                };
                for (var i = 0; i < pairs.Length; i++)
                {
                    var row = Row(column, 14f);
                    row.name = "MenuRow" + (i + 1);
                    Height(row, 102f);
                    foreach (var name in pairs[i])
                    {
                        var button = FindDeep(root.transform, name);
                        if (button == null)
                            continue;

                        button.SetParent(row, false);
                        var view = button.GetComponent<UiButtonView>();
                        if (view != null && view.Label != null)
                        {
                            view.Label.enableAutoSizing = true;
                            view.Label.fontSizeMin = 20f;
                            view.Label.fontSizeMax = 38f;
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
