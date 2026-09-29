using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;
using Object = UnityEngine.Object;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Детали интерфейса — префабы в Prefabs/UI/Parts, из которых собираются окна (вложенные префабы): кнопка,
    /// подложка окна, кошелёк, табличка игрока, звёзды, строка соперника, условие звезды, карточка товара, строка
    /// задания, строка рекордов. Детали — «исходники» вида: генератор создаёт их один раз и больше не трогает
    /// (ни Build Everything, ни Rebuild UI Prefabs). Поменял деталь — поменялись все окна, где она стоит.
    /// Пересоздать деталь из генератора: удалить её файл и запустить Build Everything.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string PartsFolder = Prefabs + "/UI/Parts";

        private static GameObject Part(string name, Func<GameObject> build)
        {
            return Prefab(PartsFolder + "/" + name + ".prefab", build);
        }

        /// <summary>Копия детали в окне: вложенный префаб (правки детали приходят во все окна).</summary>
        private static GameObject Nest(GameObject part, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(part, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return instance;
        }

        // ---------- Детали ----------

        private static GameObject ButtonPart()
        {
            return Part("Button", () => CreateButton("Button", null, 28, _ui.Palette.Button).gameObject);
        }

        private static GameObject WindowPanelPart()
        {
            return Part("WindowPanel", () =>
            {
                var panel = UiRect("WindowPanel", null);
                panel.gameObject.AddComponent<Image>().color = _ui.Palette.Panel;
                panel.sizeDelta = new Vector2(800f, 600f);
                return panel.gameObject;
            });
        }

        private static GameObject WalletPart()
        {
            return Part("Wallet", () => CreateWallet(null).gameObject);
        }

        private static GameObject HudPlayerPanelPart()
        {
            return Part("HudPlayerPanel", () => CreateHudPanel(null).gameObject);
        }

        private static GameObject QuestRowGameObjectPart()
        {
            return Part("QuestRow", () =>
            {
                var holder = UiRect("Holder", null);
                var row = QuestRow(holder);
                row.transform.SetParent(null, false);
                row.gameObject.SetActive(true);
                Object.DestroyImmediate(holder.gameObject);
                return row.gameObject;
            });
        }

        private static QuestRowView QuestRowPart() => QuestRowGameObjectPart().GetComponent<QuestRowView>();

        /// <summary>Три звезды картинками (спрайты — UiConfig.StarGold / StarGray).</summary>
        private static GameObject StarsGameObjectPart()
        {
            return Part("Stars", () =>
            {
                var root = UiRect("Stars", null);
                root.sizeDelta = new Vector2(132f, 40f);
                var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var stars = new Image[3];
                for (var i = 0; i < stars.Length; i++)
                {
                    stars[i] = UiRect("Star" + (i + 1), root).gameObject.AddComponent<Image>();
                    stars[i].sprite = _ui.StarGray;
                    stars[i].preserveAspect = true;
                    stars[i].raycastTarget = false;
                    stars[i].rectTransform.sizeDelta = new Vector2(40f, 40f);
                }

                var element = root.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 132f;
                element.preferredHeight = 40f;
                root.gameObject.AddComponent<StarsView>().EditorSetup(stars);
                return root.gameObject;
            });
        }

        private static StarsView StarsPart() => StarsGameObjectPart().GetComponent<StarsView>();

        /// <summary>Строка соперника: фон-кнопка, портрет, имя, пометка (босс, закрыт), звёзды.</summary>
        private static OpponentRowView OpponentRowPart()
        {
            return Part("OpponentRow", () =>
            {
                var root = UiRect("OpponentRow", null);
                root.sizeDelta = new Vector2(700f, 76f);
                var background = root.gameObject.AddComponent<Image>();
                background.color = _ui.Palette.Button;
                var button = root.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                button.colors = colors;
                Height(root, 76);

                var portrait = UiRect("Portrait", root).gameObject.AddComponent<Image>();
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                Corner(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(8f, 0f));

                var name = Text("Name", root, _ui.BoldFont, 26, _ui.Palette.Text);
                name.alignment = TextAlignmentOptions.Left;
                name.enableAutoSizing = true;
                name.fontSizeMin = 16;
                name.fontSizeMax = 26;
                Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(-69f, -2f), new Vector2(-322f, -6f));

                var status = Text("Status", root, _ui.Font, 18, _ui.Palette.TextMuted);
                status.alignment = TextAlignmentOptions.Left;
                Anchor(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(-69f, 2f), new Vector2(-322f, -6f));

                var stars = Nest(StarsGameObjectPart(), root, "Stars");
                Corner((RectTransform)stars.transform, new Vector2(1f, 0.5f), new Vector2(132f, 40f), new Vector2(-12f, 0f));

                var view = root.gameObject.AddComponent<OpponentRowView>();
                view.EditorSetup(button, background, portrait, name, status, stars.GetComponent<StarsView>());
                return root.gameObject;
            }).GetComponent<OpponentRowView>();
        }

        /// <summary>Условие звезды: картинка звезды и текст.</summary>
        private static StarConditionRowView StarConditionRowPart()
        {
            return Part("StarConditionRow", () =>
            {
                var root = UiRect("StarConditionRow", null);
                root.sizeDelta = new Vector2(600f, 38f);
                Height(root, 38);
                var star = UiRect("Star", root).gameObject.AddComponent<Image>();
                star.sprite = _ui.StarGray;
                star.preserveAspect = true;
                star.raycastTarget = false;
                Corner(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(0f, 0f));
                var text = Text("Text", root, _ui.Font, 22, _ui.Palette.TextMuted);
                text.alignment = TextAlignmentOptions.Left;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14;
                text.fontSizeMax = 22;
                Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-44f, 0f));

                var view = root.gameObject.AddComponent<StarConditionRowView>();
                view.EditorSetup(star, text);
                return root.gameObject;
            }).GetComponent<StarConditionRowView>();
        }

        /// <summary>Карточка товара: иконка сверху, название, цена, состояние, рамка выбора.</summary>
        private static ShopCardView ShopCardPart()
        {
            return Part("ShopCard", () =>
            {
                var root = UiRect("ShopCard", null);
                root.sizeDelta = new Vector2(210f, 240f);
                var background = root.gameObject.AddComponent<Image>();
                background.color = _ui.Palette.Button;
                var button = root.gameObject.AddComponent<Button>();
                Width(root, 210, 0);

                var selection = UiRect("Selection", root).gameObject.AddComponent<Image>();
                selection.color = new Color(1f, 0.82f, 0.35f, 0.35f);
                selection.raycastTarget = false;
                Stretch(selection.rectTransform);
                selection.gameObject.SetActive(false);

                var icon = UiRect("Icon", root).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Corner(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(96f, 96f), new Vector2(0f, -10f));

                var name = Text("Name", root, _ui.BoldFont, 22, _ui.Palette.Text);
                name.enableAutoSizing = true;
                name.fontSizeMin = 14;
                name.fontSizeMax = 22;
                Anchor(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 96f), new Vector2(-16f, 52f));

                var price = Text("Price", root, _ui.BoldFont, 20, _ui.Palette.Gold);
                price.enableAutoSizing = true;
                price.fontSizeMin = 13;
                price.fontSizeMax = 20;
                Anchor(price.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 56f), new Vector2(-16f, 32f));

                var state = Text("State", root, _ui.Font, 18, _ui.Palette.TextMuted);
                state.enableAutoSizing = true;
                state.fontSizeMin = 12;
                state.fontSizeMax = 18;
                Anchor(state.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-16f, 30f));

                var view = root.gameObject.AddComponent<ShopCardView>();
                view.EditorSetup(button, background, icon, name, price, state, selection.gameObject);
                return root.gameObject;
            }).GetComponent<ShopCardView>();
        }

        /// <summary>Строка рекордов: место, имя, результат.</summary>
        private static LeaderboardRowView LeaderboardRowPart()
        {
            return Part("LeaderboardRow", () =>
            {
                var root = UiRect("LeaderboardRow", null);
                root.sizeDelta = new Vector2(900f, 48f);
                var background = root.gameObject.AddComponent<Image>();
                background.color = new Color(1f, 1f, 1f, 0.04f);
                background.raycastTarget = false;
                Height(root, 48);
                var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.padding = new RectOffset(16, 16, 4, 4);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;

                var rank = Text("Rank", root, _ui.BoldFont, 26, _ui.Palette.Gold);
                Width(rank, 80, 0);
                var name = Text("Name", root, _ui.Font, 26, _ui.Palette.Text);
                name.alignment = TextAlignmentOptions.Left;
                name.enableAutoSizing = true;
                name.fontSizeMin = 16;
                name.fontSizeMax = 26;
                Width(name, -1, 1);
                var score = Text("Score", root, _ui.BoldFont, 26, _ui.Palette.Text);
                score.alignment = TextAlignmentOptions.Right;
                Width(score, 200, 0);

                var view = root.gameObject.AddComponent<LeaderboardRowView>();
                view.EditorSetup(background, rank, name, score);
                return root.gameObject;
            }).GetComponent<LeaderboardRowView>();
        }

        /// <summary>Кнопка языка: флаг слева, название языка, отметка выбранного.</summary>
        private static GameObject LanguageButtonGameObjectPart()
        {
            return Part("LanguageButton", () =>
            {
                var root = UiRect("LanguageButton", null);
                root.sizeDelta = new Vector2(290f, 90f);
                var background = root.gameObject.AddComponent<Image>();
                background.color = _ui.Palette.Button;
                var button = root.gameObject.AddComponent<Button>();

                var selected = UiRect("Selected", root).gameObject.AddComponent<Image>();
                selected.color = new Color(1f, 0.82f, 0.35f, 0.35f);
                selected.raycastTarget = false;
                Stretch(selected.rectTransform);
                selected.gameObject.SetActive(false);

                var flag = UiRect("Flag", root).gameObject.AddComponent<Image>();
                flag.preserveAspect = true;
                flag.raycastTarget = false;
                Corner(flag.rectTransform, new Vector2(0f, 0.5f), new Vector2(84f, 56f), new Vector2(14f, 0f));

                var name = Text("Name", root, _ui.BoldFont, 28, _ui.Palette.Text);
                name.alignment = TextAlignmentOptions.Left;
                name.enableAutoSizing = true;
                name.fontSizeMin = 16;
                name.fontSizeMax = 28;
                Anchor(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(52f, 0f), new Vector2(-124f, -12f));

                var view = root.gameObject.AddComponent<LanguageButtonView>();
                view.EditorSetup(button, flag, name, selected.gameObject);
                return root.gameObject;
            });
        }

        /// <summary>Строки флагов для всех языков игры (картинки ставятся потом в UiConfig).</summary>
        private static void EnsureLanguageFlags(Zonk.UI.UiConfig ui)
        {
            if (ui == null || ui.LanguageFlags.Count > 0)
                return;

            foreach (var code in new[] { "ru", "en", "tr", "es", "pt", "de", "fr" })
                ui.LanguageFlags.Add(new Zonk.UI.LanguageFlag { Code = code });
            EditorUtility.SetDirty(ui);
        }

        // ---------- Копии деталей в окнах ----------

        /// <summary>Кнопка окна: копия Parts/Button с размером шрифта и цветом этой кнопки.</summary>
        private static UiButtonView ButtonView(string name, Transform parent, float fontSize, Color color)
        {
            var view = Nest(ButtonPart(), parent, name).GetComponent<UiButtonView>();
            view.Background.color = color;
            view.Label.fontSize = fontSize;
            view.Label.fontSizeMax = fontSize;
            view.Label.fontSizeMin = Mathf.Min(16f, fontSize);
            return view;
        }

        /// <summary>Подложка окна: копия Parts/WindowPanel.</summary>
        private static RectTransform PanelAt(Transform parent)
        {
            return (RectTransform)Nest(WindowPanelPart(), parent, "Panel").transform;
        }

        /// <summary>Кошелёк в правом верхнем углу окна: копия Parts/Wallet.</summary>
        private static void Wallet(RectTransform root)
        {
            var wallet = (RectTransform)Nest(WalletPart(), root, "Wallet").transform;
            Corner(wallet, new Vector2(1f, 1f), new Vector2(480f, 64f), new Vector2(-24f, -24f));
        }

        private static HudPlayerPanel HudPanel(string name, RectTransform root, Vector2 anchor, Vector2 offset)
        {
            var panel = Nest(HudPlayerPanelPart(), root, name);
            Corner((RectTransform)panel.transform, anchor, new Vector2(480f, 120f), offset);
            return panel.GetComponent<HudPlayerPanel>();
        }

        /// <summary>Картинки звёзд: золотая и серая (белую звезду Star.png перекрашивает генератор).</summary>
        private static void BuildStarSprites(Zonk.UI.UiConfig ui)
        {
            if (ui == null)
                return;

            var folder = Root + "/Art/Sprites";
            EnsureFolder(folder);
            if (ui.StarGold == null)
                ui.StarGold = StarSprite(folder + "/Star_Gold.png", new Color(1f, 0.8f, 0.25f), new Color(0.7f, 0.45f, 0.08f));
            if (ui.StarGray == null)
                ui.StarGray = StarSprite(folder + "/Star_Gray.png", new Color(0.55f, 0.53f, 0.5f), new Color(0.3f, 0.29f, 0.27f));
            EditorUtility.SetDirty(ui);
        }

        private static Sprite StarSprite(string path, Color fill, Color edge)
        {
            if (!File.Exists(path))
            {
                const int size = 64;
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var cx = x + 0.5f - size * 0.5f;
                    var cy = y + 0.5f - size * 0.5f;
                    var outer = StarCoverage(cx, cy, size * 0.48f, size * 0.2f);
                    var inner = StarCoverage(cx, cy, size * 0.38f, size * 0.15f);
                    var color = Color.Lerp(edge, fill, inner);
                    color = Color.Lerp(color, Color.white, Mathf.Clamp01((cy - 4f) / size) * 0.35f * inner);
                    color.a = outer;
                    pixels[y * size + x] = color;
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
