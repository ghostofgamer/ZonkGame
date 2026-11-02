using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// «Мои кости» (05.10.2026): окно наборов костей на столе (как магазин: камера на лотке, нижняя панель) и входы в него —
    /// кнопка в колонке главного меню, «Изменить» в кампании. У игрока 1 игры вдвоём — источник костей (набор или свои)
    /// и «Все обычные». В уже созданные префабы элементы дописываются (заблокированные не трогаются): в окне они необязательны.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static DiceWindow BuildDiceWindow(RectTransform root)
        {
            var title = Text("Title", root, _ui.BoldFont, 40, _ui.Palette.Gold);
            Corner(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 80f), new Vector2(0f, -24f));
            AutoSize(title, 24, 40);

            // Нижняя панель на всю ширину с отступами по краям — как у магазина: стол с костями остаётся виден.
            var panel = PanelAt(root);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(-48f, 380f);
            panel.anchoredPosition = new Vector2(0f, 16f);
            var column = Column(panel, 10, 14);

            var tabs = Row(column, 8);
            Height(tabs, 64);
            var presetTemplate = ButtonView("PresetTemplate", tabs, 24, _ui.Palette.ButtonMuted);
            var allStandard = ButtonView("AllStandard", tabs, 22, _ui.Palette.Button);
            var back = ButtonView("Back", tabs, 24, _ui.Palette.Button);

            var body = Row(column, 16);
            Height(body, 270);
            var items = HorizontalList("Items", body, 10, out var itemsContent);
            Width(items, -1, 3);

            var details = Column(body, 6, 8);
            Width(details, 560, 0);
            var slotName = Text("SlotName", details, _ui.BoldFont, 30, _ui.Palette.Gold);
            slotName.alignment = TextAlignmentOptions.Left;
            AutoSize(slotName, 18, 30);
            Height(slotName, 44);
            var description = Text("Description", details, _ui.Font, 22, _ui.Palette.Text);
            description.alignment = TextAlignmentOptions.TopLeft;
            AutoSize(description, 14, 22);
            Height(description, 110);
            var specials = Text("Specials", details, _ui.Font, 22, _ui.Palette.TextMuted);
            specials.alignment = TextAlignmentOptions.Left;
            AutoSize(specials, 14, 22);
            Height(specials, 34);
            var hint = Text("Hint", details, _ui.Font, 18, _ui.Palette.TextMuted);
            hint.alignment = TextAlignmentOptions.TopLeft;
            AutoSize(hint, 12, 18);
            Height(hint, 52);

            var window = root.gameObject.AddComponent<DiceWindow>();
            window.EditorSetup(title, tabs, presetTemplate, allStandard, back, itemsContent, ShopCardPart(), slotName, description,
                specials, hint);
            return window;
        }

        /// <summary>Входы в «Мои кости» в уже созданных окнах: дописываются один раз, пустое поле — признак старого префаба.</summary>
        private static void AddDiceEditing()
        {
            // Главное меню: кнопка в колонке сразу под «Магазином».
            EditWindowPrefab<MainMenuWindow>("MainMenuWindow", "_dice", (root, window) =>
            {
                var shop = FindDeep(root, "Shop");
                if (shop == null)
                    return null;

                var button = ButtonView("Dice", shop.parent, 34, _ui.Palette.Button);
                Height(button, 62);
                button.transform.SetSiblingIndex(shop.GetSiblingIndex() + 1);
                return button;
            });

            // Кампания: «Изменить» в строке «Мои кости» рядом с наборами.
            EditWindowPrefab<CampaignWindow>("CampaignWindow", "_editDice", (root, window) =>
            {
                var label = FindDeep(root, "MyDiceLabel");
                if (label == null || !(label.parent is RectTransform header))
                    return null;

                var button = ButtonView("EditDice", header, 20, _ui.Palette.ButtonAccent);
                Width(button, 200, 0);
                return button;
            });

            AddHotSeatDiceSource();
        }

        /// <summary>
        /// Игра вдвоём: в панели игрока строка подписи костей получает кнопку источника (набор аккаунта или свои),
        /// строка скина — «Все обычные». Высота панели прежняя: кнопки встают в существующие строки.
        /// </summary>
        private static void AddHotSeatDiceSource()
        {
            var path = Prefabs + "/UI/HotSeatSetupWindow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed = false;
                foreach (var panel in root.GetComponentsInChildren<PlayerSetupPanel>(true))
                {
                    if (panel.HasDiceSource)
                        continue;

                    var label = FindDeep(panel.transform, "DiceLabel");
                    var skin = FindDeep(panel.transform, "Skin");
                    if (label == null || skin == null || !(label.parent is RectTransform column))
                        continue;

                    var labelRow = Row(column, 12);
                    labelRow.name = "DiceLabelRow";
                    labelRow.SetSiblingIndex(label.GetSiblingIndex());
                    Height(labelRow, 52);
                    label.SetParent(labelRow, false);
                    Width(label.GetComponent<RectTransform>(), -1, 1);
                    var source = ButtonView("DiceSource", labelRow, 20, _ui.Palette.ButtonAccent);
                    Width(source, 360, 0);

                    var skinRow = Row(column, 12);
                    skinRow.name = "SkinRow";
                    skinRow.SetSiblingIndex(skin.GetSiblingIndex());
                    Height(skinRow, 64);
                    skin.SetParent(skinRow, false);
                    Width(skin.GetComponent<RectTransform>(), -1, 1);
                    var allStandard = ButtonView("AllStandard", skinRow, 20, _ui.Palette.ButtonMuted);
                    Width(allStandard, 260, 0);

                    panel.EditorSetupDiceSource(source, allStandard);
                    EditorUtility.SetDirty(panel);
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Zonk] HotSeatSetupWindow: dice source added");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
