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

        /// <summary>Подложка растёт по тексту внутри. Снизу и справа отступ больше: там тень таблички.</summary>
        private static void FitPlateToText(GameObject plate)
        {
            var layout = plate.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(30, 36, 14, 22);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = plate.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Крупная надпись на подложке: Toast на любом фоне читается. Переиспользуемая деталь.</summary>
        private static UiLabelPlate LabelPlatePart(GameObject plate)
        {
            return Part("LabelPlate", () =>
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(plate);
                root.name = "LabelPlate";
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                FitPlateToText(root);
                var group = root.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                var label = UiText("Label", root.transform, _ui.DisplayFont, 60f, Color.white);
                ComicText(label);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;

                root.AddComponent<UiLabelPlate>().EditorSetup(label, group);
                return root;
            }).GetComponent<UiLabelPlate>();
        }

        /// <summary>Строка соперника: золотая рамка выбранного, как у карточки магазина.</summary>
        private static void UpgradeOpponentRowPart()
        {
            var path = PartsFolder + "/OpponentRow.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Рамка поверх всего в строке: портрет и звёзды её не перекрывают.
                var existing = FindDeep(root.transform, "Selection");
                if (existing != null)
                {
                    if (existing.GetSiblingIndex() != root.transform.childCount - 1)
                    {
                        existing.SetAsLastSibling();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }

                    return;
                }

                var frameRect = UiRect("Selection", root.transform);
                Stretch(frameRect);
                frameRect.SetAsLastSibling();
                var frame = frameRect.gameObject.AddComponent<Image>();
                frame.sprite = ComicSpriteAsset("UI_Plate");
                frame.type = Image.Type.Sliced;
                frame.fillCenter = false;
                frame.pixelsPerUnitMultiplier = 2.5f;
                frame.color = _ui.Palette.Gold;
                frame.raycastTarget = false;
                frameRect.gameObject.SetActive(false);
                root.GetComponent<OpponentRowView>().EditorSetupSelection(frameRect.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Блок «Дальше»: светлая табличка с целью игрока. Переиспользуемая деталь.</summary>
        private static UiLabelPlate NextGoalPart()
        {
            return Part("NextGoal", () =>
            {
                var root = UiRect("NextGoal", null);
                root.sizeDelta = new Vector2(800f, 112f);
                var image = root.gameObject.AddComponent<Image>();
                image.sprite = ComicSpriteAsset("UI_Plate");
                image.type = Image.Type.Sliced;
                image.color = Paper;
                image.raycastTarget = false;
                var group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                Height(root, 112f);

                var label = UiText("Label", root, _ui.BoldFont, 26f, Ink);
                AutoSize(label, 16f, 26f);
                label.fontSharedMaterial = _ui.BoldFont.material;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(22f, 18f);
                label.rectTransform.offsetMax = new Vector2(-26f, -10f);

                root.gameObject.AddComponent<UiLabelPlate>().EditorSetup(label, group);
                return root.gameObject;
            }).GetComponent<UiLabelPlate>();
        }

        /// <summary>Итоги партии: крупный счёт, блок «Дальше», плашка-взрыв поверх окна.</summary>
        private static void UpgradeResults(GameObject burst)
        {
            var path = Prefabs + "/UI/ResultsWindow.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var goalPart = NextGoalPart();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var window = root.GetComponent<Zonk.UI.Windows.ResultsWindow>();
                var title = FindDeep(root.transform, "Title");
                var buttons = FindDeep(root.transform, "Menu");
                if (window == null || title == null || buttons == null)
                    return;

                var column = (RectTransform)title.parent;
                var row = buttons.parent;
                if (FindDeep(root.transform, "Headline") == null)
                {
                    var headline = UiText("Headline", column, _ui.DisplayFont, 86f, Color.white);
                    ComicText(headline, false);
                    Height(headline, 96f);
                    headline.transform.SetSiblingIndex(title.GetSiblingIndex() + 1);

                    var goal = (GameObject)PrefabUtility.InstantiatePrefab(goalPart.gameObject, column);
                    goal.name = "NextGoal";
                    goal.transform.SetSiblingIndex(row.GetSiblingIndex());

                    if (column.parent is RectTransform panel && panel.sizeDelta.y > 0f)
                        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 1000f);

                    var burstObject = Nest(burst, root.transform, "ComicBurst");
                    Place((RectTransform)burstObject.transform, new Vector2(0.5f, 0.62f), new Vector2(1000f, 430f));
                    burstObject.SetActive(false);

                    window.EditorSetupProgress(burstObject.GetComponent<ComicBurstView>(), headline, goal.GetComponent<UiLabelPlate>());
                }

                // Пустое место — над «Дальше», а кнопки всегда внизу окна: при коротких итогах они не висят в середине.
                if (FindDeep(root.transform, "Spacer") == null)
                {
                    var goalRow = FindDeep(root.transform, "NextGoal");
                    var spacer = UiRect("Spacer", column);
                    spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                    spacer.SetSiblingIndex(goalRow != null ? goalRow.GetSiblingIndex() : row.GetSiblingIndex());
                }

                // Полоса опыта крупнее: это главное на экране итогов после счёта.
                var level = FindDeep(root.transform, "PlayerLevel");
                if (level != null)
                {
                    Height(level.GetComponent<RectTransform>(), 92f);
                    var caption = FindDeep(level, "Caption");
                    if (caption != null)
                    {
                        var captionText = caption.GetComponent<TMPro.TMP_Text>();
                        captionText.fontSizeMin = 18f;
                        captionText.fontSizeMax = 30f;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Карточка магазина: иконка главная, выбор — золотая рамка, а не бледная заливка поверх всего.</summary>
        private static void UpgradeShopCardPart()
        {
            var path = PartsFolder + "/ShopCard.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var icon = FindDeep(root.transform, "Icon") as RectTransform;
                var selection = FindDeep(root.transform, "Selection");
                var name = FindDeep(root.transform, "Name") as RectTransform;
                var price = FindDeep(root.transform, "Price") as RectTransform;
                var state = FindDeep(root.transform, "State") as RectTransform;
                if (icon == null || selection == null || name == null || price == null || state == null || Mathf.Approximately(icon.sizeDelta.x, 128f))
                    return;

                Corner(icon, new Vector2(0.5f, 1f), new Vector2(128f, 128f), new Vector2(0f, -8f));
                Anchor(name, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 84f), new Vector2(-28f, 36f));
                Anchor(price, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 56f), new Vector2(-28f, 28f));
                Anchor(state, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 32f), new Vector2(-28f, 26f));
                state.GetComponent<TMPro.TMP_Text>().color = _ui.Palette.Text;

                // Рамка тонкая и под текстом: толстая закрывала «Есть» и «Выбрано».
                var frame = selection.GetComponent<Image>();
                frame.sprite = ComicSpriteAsset("UI_Plate");
                frame.type = Image.Type.Sliced;
                frame.fillCenter = false;
                frame.pixelsPerUnitMultiplier = 2.5f;
                frame.color = _ui.Palette.Gold;
                selection.SetAsFirstSibling();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
                var window = root.GetComponent<Zonk.UI.Windows.MatchHudWindow>();
                var plate = root.transform.Find("TurnPlate") as RectTransform;
                if (window == null || plate == null)
                    return;

                // Ниже таблички счёта: огонь над очками хода её не закрывает.
                var turnOffset = new Vector2(24f, -224f);
                Corner(plate, new Vector2(0f, 1f), new Vector2(300f, 96f), turnOffset);

                if (root.transform.Find("Turn") is RectTransform turn)
                {
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
                }

                // Подсказка на подложке, подложка растёт по тексту.
                GameObject hintPlate = null;
                var existingPlate = root.transform.Find("HintPlate");
                if (existingPlate != null)
                    hintPlate = existingPlate.gameObject;
                else if (root.transform.Find("Hint") is RectTransform hint)
                {
                    hintPlate = Nest(PlatePart(), root.transform, "HintPlate");
                    var hintRect = (RectTransform)hintPlate.transform;
                    hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0f);
                    hintRect.anchoredPosition = new Vector2(0f, 198f);
                    hintPlate.transform.SetSiblingIndex(hint.GetSiblingIndex());
                    FitPlateToText(hintPlate);

                    hint.SetParent(hintRect, false);
                    var hintText = hint.GetComponent<TMP_Text>();
                    hintText.enableAutoSizing = false;
                    hintText.fontSize = 30f;
                    hintText.textWrappingMode = TextWrappingModes.NoWrap;
                    hintText.outlineWidth = 0f;
                    hintText.font = _ui.BoldFont;
                    hintText.fontSharedMaterial = _ui.BoldFont.material;
                }

                window.EditorSetupHudLayout(hintPlate, turnOffset);

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
