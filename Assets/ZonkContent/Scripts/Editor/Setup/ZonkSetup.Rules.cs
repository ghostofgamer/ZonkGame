using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI;
using Zonk.UI.Rules;
using Zonk.UI.Transitions;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Книга правил: картинки граней костей для UI (вырезаются из атласа кости), иконка книжки для меню,
    /// ассет RulesBook (страницы и примеры комбинаций) и префаб окна RulesWindow.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string Sprites = Root + "/Art/Sprites";

        private static void BuildRules(UiConfig ui, Configs.RuleSetConfig rules)
        {
            EnsureFolder(Sprites);

            var faces = DieFaceSprites();
            var icon = RulesIcon();

            var book = Asset<RulesBookConfig>(ConfigsFolder + "/Ui/RulesBook.asset", b =>
            {
                b.Rules = rules;
                b.Combos = new List<ComboExample>
                {
                    Combo("combo.one", 1),
                    Combo("combo.five", 5),
                    Combo("combo.threeOnes", 1, 1, 1),
                    Combo("combo.threeTwos", 2, 2, 2),
                    Combo("combo.threeThrees", 3, 3, 3),
                    Combo("combo.threeFours", 4, 4, 4),
                    Combo("combo.threeFives", 5, 5, 5),
                    Combo("combo.threeSixes", 6, 6, 6),
                    Combo("combo.fourKind", 3, 3, 3, 3),
                    Combo("combo.fiveKind", 3, 3, 3, 3, 3),
                    Combo("combo.sixKind", 3, 3, 3, 3, 3, 3),
                    Combo("combo.straight", 1, 2, 3, 4, 5, 6),
                    Combo("combo.straightLow", 1, 2, 3, 4, 5),
                    Combo("combo.straightHigh", 2, 3, 4, 5, 6),
                    Combo("combo.threePairs", 2, 2, 4, 4, 6, 6),
                };
                for (var i = 1; i <= 7; i++)
                    b.Pages.Add(new RulesPage { TitleKey = $"rules.p{i}.title", TextKey = $"rules.p{i}.text" });
            });

            var popupIn = Transition("Popup_In", 0.3f, Ease.OutBack,
                new FadeEffect { DurationScale = 0.6f, OverrideEase = true, Ease = Ease.OutCubic },
                new ScaleEffect { TargetPath = "Panel", From = new Vector3(0.85f, 0.85f, 1f) });
            var popupOut = Transition("Popup_Out", 0.2f, Ease.InBack,
                new FadeEffect { From = 1f, To = 0f, OverrideEase = true, Ease = Ease.InCubic },
                new ScaleEffect { TargetPath = "Panel", From = Vector3.one, To = new Vector3(0.9f, 0.9f, 1f) });
            var pageOut = Transition("Page_Out", 0.15f, Ease.InCubic,
                new FadeEffect { From = 1f, To = 0f }, new MoveEffect { Offset = new Vector2(-50f, 0f), Outgoing = true });
            var pageIn = Transition("Page_In", 0.2f, Ease.OutCubic,
                new FadeEffect(), new MoveEffect { Offset = new Vector2(50f, 0f) });

            var window = BuildRulesWindow(ui, popupIn, popupOut, pageOut, pageIn);

            if (ui.RulesBook == null)
                ui.RulesBook = book;
            if (ui.RulesIcon == null)
                ui.RulesIcon = icon;
            if (ui.DieFaces == null || ui.DieFaces.Length < 6 || ui.DieFaces[0] == null)
                ui.DieFaces = faces;
            var windowComponent = window.GetComponent<RulesWindow>();
            if (!ui.Windows.Contains(windowComponent))
                ui.Windows.Add(windowComponent);
            EditorUtility.SetDirty(ui);
        }

        private static ComboExample Combo(string nameKey, params int[] faces)
        {
            return new ComboExample { NameKey = nameKey, Faces = faces };
        }

        private static UiTransitionConfig Transition(string name, float duration, Ease ease, params UiEffect[] effects)
        {
            return Asset<UiTransitionConfig>(TransitionsFolder + "/" + name + ".asset", t =>
            {
                t.Duration = duration;
                t.Ease = ease;
                t.Effects = new List<UiEffect>(effects);
            });
        }

        /// <summary>Шесть спрайтов граней из атласа кости (3×2: сверху 1 2 3, снизу 4 5 6).</summary>
        private static Sprite[] DieFaceSprites()
        {
            var sprites = new Sprite[6];
            byte[] bytes = null;
            Texture2D atlas = null;

            for (var face = 1; face <= 6; face++)
            {
                var path = Sprites + "/DieFace_" + face + ".png";
                if (!File.Exists(path))
                {
                    if (atlas == null)
                    {
                        bytes = File.ReadAllBytes(Textures + "/Die_Classic.png");
                        atlas = new Texture2D(2, 2);
                        atlas.LoadImage(bytes);
                    }

                    var cellWidth = atlas.width / 3;
                    var cellHeight = atlas.height / 2;
                    var column = (face - 1) % 3;
                    var row = (face - 1) / 3;
                    var pixels = atlas.GetPixels(column * cellWidth, row == 0 ? cellHeight : 0, cellWidth, cellHeight);
                    var cell = TextureFrom(cellWidth, cellHeight, pixels);
                    SavePng(path, cell);
                }

                sprites[face - 1] = SpriteAt(path);
            }

            if (atlas != null)
                Object.DestroyImmediate(atlas);
            return sprites;
        }

        /// <summary>Иконка книжки: обложка, корешок, обрез страниц. Знак вопроса рисует кнопка текстом.</summary>
        private static Sprite RulesIcon()
        {
            var path = Sprites + "/Icon_RulesBook.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var clear = new Color(0f, 0f, 0f, 0f);
                var cover = new Color(0.62f, 0.2f, 0.14f);
                var spine = new Color(0.42f, 0.12f, 0.08f);
                var pages = new Color(0.97f, 0.92f, 0.8f);
                var trim = new Color(1f, 0.82f, 0.35f);

                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var color = clear;
                    if (x >= 60 && x <= 214 && y >= 30 && y <= 226)
                        color = x <= 84 ? spine : cover;
                    else if (x > 214 && x <= 228 && y >= 40 && y <= 218)
                        color = (y / 6) % 2 == 0 ? pages : new Color(0.85f, 0.8f, 0.7f);

                    // Золотая рамка на обложке.
                    if (x > 84 && x <= 214 && y >= 30 && y <= 226 &&
                        (x < 96 || x > 202 || y < 42 || y > 214) && (x >= 92 && x <= 206 && y >= 38 && y <= 218))
                        color = trim;

                    texture.SetPixel(x, y, color);
                }

                texture.Apply();
                SavePng(path, texture);
            }

            return SpriteAt(path);
        }

        private static Sprite SpriteAt(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Префаб окна правил: затемнение, панель; слева прокручиваемый список комбинаций (строка-шаблон),
        /// справа страница (заголовок, картинка, текст) и листание. Вёрстку можно менять в префабе.
        /// </summary>
        private static GameObject BuildRulesWindow(UiConfig ui, UiTransitionConfig show, UiTransitionConfig hide,
            UiTransitionConfig pageOut, UiTransitionConfig pageIn)
        {
            return Prefab(Prefabs + "/UI/RulesWindow.prefab", () =>
            {
                var palette = ui.Palette;
                var root = UiRect("RulesWindow", null);
                Stretch(root);
                root.gameObject.AddComponent<CanvasGroup>();
                root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

                var panel = UiRect("Panel", root);
                Place(panel, new Vector2(0.5f, 0.5f), new Vector2(1680, 940));
                panel.gameObject.AddComponent<Image>().color = palette.Panel;

                var title = UiText("Title", panel, ui.BoldFont, 54, palette.Gold);
                Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(-240f, 90f));

                var close = TextButton("Close", panel, ui.BoldFont, "×", 60, palette.ButtonMuted, palette.Text);
                Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -55f), new Vector2(84f, 84f));

                // Слева: комбинации.
                var left = UiRect("Combos", panel);
                Anchor(left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(420f, -40f), new Vector2(760f, -160f));
                var combosTitle = UiText("CombosTitle", left, ui.BoldFont, 34, palette.Text);
                Anchor(combosTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 50f));

                var scroll = UiRect("Scroll", left);
                Anchor(scroll, Vector2.zero, Vector2.one, new Vector2(0f, -30f), new Vector2(0f, -60f));
                scroll.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
                var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
                var viewport = UiRect("Viewport", scroll);
                Stretch(viewport);
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = UiRect("Content", viewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = Vector2.zero;
                var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                contentLayout.spacing = 6;
                contentLayout.padding = new RectOffset(12, 12, 12, 12);
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scrollRect.content = content;
                scrollRect.viewport = viewport;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.scrollSensitivity = 40f;

                var row = BuildComboRow(content, ui);

                // Справа: страница правил.
                var right = UiRect("Page", panel);
                Anchor(right, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(410f, -40f), new Vector2(-900f, -160f));
                var pageContent = UiRect("PageContent", right);
                Anchor(pageContent, Vector2.zero, Vector2.one, new Vector2(0f, 40f), new Vector2(0f, -100f));
                pageContent.gameObject.AddComponent<CanvasGroup>();
                var pageTitle = UiText("PageTitle", pageContent, ui.BoldFont, 40, palette.Gold);
                pageTitle.alignment = TextAlignmentOptions.Left;
                Anchor(pageTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(-40f, 60f));
                var illustration = UiRect("Illustration", pageContent).gameObject.AddComponent<Image>();
                illustration.preserveAspect = true;
                Anchor(illustration.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -150f), new Vector2(200f, 200f));
                illustration.gameObject.SetActive(false);
                var pageText = UiText("PageText", pageContent, ui.Font, 31, palette.Text);
                pageText.alignment = TextAlignmentOptions.TopLeft;
                Anchor(pageText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -40f), new Vector2(-40f, -90f));

                var previous = TextButton("Previous", right, ui.BoldFont, "<", 30, palette.Button, palette.Text);
                Anchor((RectTransform)previous.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(120f, 40f), new Vector2(220f, 72f));
                var next = TextButton("Next", right, ui.BoldFont, ">", 30, palette.ButtonAccent, palette.Text);
                Anchor((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 40f), new Vector2(220f, 72f));
                var counter = UiText("Counter", right, ui.Font, 30, palette.TextMuted);
                Anchor(counter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-15f, 40f), new Vector2(240f, 60f));

                var window = root.gameObject.AddComponent<RulesWindow>();
                window.EditorSetup(title, combosTitle, pageTitle, pageText, counter, illustration, content, row, pageContent,
                    previous, next, close, pageOut, pageIn);
                window.EditorSetupTransitions(show, hide, true);
                return root.gameObject;
            });
        }

        private static RulesComboRow BuildComboRow(RectTransform parent, UiConfig ui)
        {
            var row = UiRect("ComboRowTemplate", parent);
            row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(10, 14, 6, 6);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            // Строка растёт по высоте, если название переносится на вторую строку.
            row.gameObject.AddComponent<LayoutElement>().minHeight = 56;

            var dice = UiRect("Dice", row);
            var diceLayout = dice.gameObject.AddComponent<HorizontalLayoutGroup>();
            diceLayout.spacing = 3;
            diceLayout.childAlignment = TextAnchor.MiddleLeft;
            diceLayout.childControlWidth = true;
            diceLayout.childControlHeight = true;
            diceLayout.childForceExpandWidth = false;
            diceLayout.childForceExpandHeight = false;
            var diceElement = dice.gameObject.AddComponent<LayoutElement>();
            diceElement.preferredWidth = 230;
            diceElement.minWidth = 230;

            var die = UiRect("Die", dice).gameObject.AddComponent<Image>();
            die.preserveAspect = true;
            var dieElement = die.gameObject.AddComponent<LayoutElement>();
            dieElement.preferredWidth = dieElement.preferredHeight = 36;

            var name = UiText("Name", row, ui.Font, 26, ui.Palette.Text);
            name.alignment = TextAlignmentOptions.Left;
            // Длинное название само уменьшается до 18 и переносится, а не вылезает за строку.
            AutoSize(name, 18, 26);
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var points = UiText("Points", row, ui.BoldFont, 28, ui.Palette.Gold);
            points.alignment = TextAlignmentOptions.Right;
            points.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;

            var view = row.gameObject.AddComponent<RulesComboRow>();
            view.EditorSetup(dice, die, name, points);
            return view;
        }

        private static Button TextButton(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
            Color textColor)
        {
            var rect = UiRect(name, parent);
            rect.gameObject.AddComponent<Image>().color = color;
            var button = rect.gameObject.AddComponent<Button>();
            var label = UiText("Label", rect, font, size, textColor);
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>Якоря, позиция и размер одним вызовом (pivot по центру).</summary>
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
