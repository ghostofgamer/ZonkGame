using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Окна режимов-испытаний: ChallengeWindow (лобби режима и этаж перед партией) и PerkChoiceWindow (выбор находки).
    /// Вёрстка по образцу RulesWindow: панель по центру не больше 1760×1000, всё на якорях, длинный текст прокручивается.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static ChallengeWindow BuildChallenge(RectTransform root)
        {
            Dim(root, 0.6f);
            var panel = CenterPanel(root, new Vector2(1400f, 880f));

            // Заголовок растянут по верхнему краю панели.
            var title = Text("Title", panel, _ui.BoldFont, 50, _ui.Palette.Gold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 28;
            title.fontSizeMax = 50;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-80f, 90f));

            // Портрет соперника — в левом верхнем углу (без портрета текст занимает всю ширину).
            var portrait = UiRect("Portrait", panel).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            Corner(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 300f), new Vector2(40f, -130f));
            portrait.gameObject.SetActive(false);

            // Текст: этаж, цель, правила, сердца, находки — прокручивается, если длинный.
            var list = VerticalList("Body", panel, 8, out var content);
            list.anchorMin = Vector2.zero;
            list.anchorMax = Vector2.one;
            list.offsetMin = new Vector2(370f, 150f);
            list.offsetMax = new Vector2(-40f, -120f);
            var body = Text("Text", content, _ui.Font, 32, _ui.Palette.Text);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;

            // Кнопки — снизу по центру, растянуты по ширине панели.
            var row = Row(panel, 24);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(-80f, 90f);
            row.anchoredPosition = new Vector2(0f, 36f);
            var primary = ButtonView("Primary", row, 32, _ui.Palette.ButtonAccent);
            var secondary = ButtonView("Secondary", row, 30, _ui.Palette.Button);
            var back = ButtonView("Back", row, 30, _ui.Palette.ButtonMuted);

            var window = root.gameObject.AddComponent<ChallengeWindow>();
            window.EditorSetup(title, portrait, body, list.GetComponent<ScrollRect>(), primary, secondary, back);
            return window;
        }

        private static PerkChoiceWindow BuildPerkChoice(RectTransform root)
        {
            Dim(root, 0.65f);
            var panel = CenterPanel(root, new Vector2(1500f, 760f));

            var title = Text("Title", panel, _ui.BoldFont, 50, _ui.Palette.Gold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 28;
            title.fontSizeMax = 50;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-80f, 100f));

            // Находки — большими кнопками в ряд: название и что даёт (две строки, автоподбор размера).
            var options = Row(panel, 30);
            options.anchorMin = Vector2.zero;
            options.anchorMax = Vector2.one;
            options.offsetMin = new Vector2(50f, 60f);
            options.offsetMax = new Vector2(-50f, -150f);
            var template = ButtonView("Option", options, 34, _ui.Palette.Button);

            var window = root.gameObject.AddComponent<PerkChoiceWindow>();
            window.EditorSetup(title, options, template);
            return window;
        }
    }
}
