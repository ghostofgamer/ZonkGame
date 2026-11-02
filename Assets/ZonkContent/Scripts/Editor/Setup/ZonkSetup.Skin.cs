using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Комикс-оформление интерфейса. Текстуры рисует Tools/Blender/zonk_ui.py, здесь: настройка импорта, шрифты,
    /// палитра, переиспользуемые детали Parts (Plate, Ribbon, Flame, FlameRow, FlameRing, ComicBurst, Toast, Popup)
    /// и проход по всем префабам Prefabs/UI. Повторный запуск ничего не портит, заблокированные префабы не трогает.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string ComicSprites = Root + "/Art/Sprites";
        private const string DisplayFontName = "FiraSansExtraCondensed-BlackItalic";
        private const int FlameFrames = 8;
        private const float FlameFps = 12f;

        // Жирный текст такого размера и крупнее становится заголовочным шрифтом.
        private const float DisplayMinSize = 40f;

        // Надпись на кнопке мельче этого — тонкий контур без тени, иначе буквы забиваются.
        private const float DisplaySmallSize = 32f;

        // Кнопка под палец: надпись не мельче.
        private const float ButtonMinFont = 28f;

        private static readonly Color Ink = new Color(0.082f, 0.063f, 0.047f, 1f);
        private static readonly Color Paper = new Color(0.953f, 0.89f, 0.749f, 1f);

        [MenuItem("Zonk/Setup/Apply Comic UI", priority = 4)]
        public static void ApplyComicUiMenu()
        {
            _ui = AssetDatabase.LoadAssetAtPath<UiConfig>(ConfigsFolder + "/Ui/UiConfig.asset");
            if (_ui == null)
            {
                Debug.LogError("[Zonk] UiConfig not found: run Zonk/Setup/Build Everything first");
                return;
            }

            ImportComicSprites();
            BuildSpriteAtlases();
            ApplyComicSkin();
            UiWindowRegistry.Rebuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Comic UI applied");
        }

        // ---------- Текстуры ----------

        /// <summary>Настройка импорта. Границы 9-slice совпадают с рисунком в zonk_ui.py.</summary>
        public static void ImportComicSprites()
        {
            AssetDatabase.Refresh();
            ComicSprite("UI_Button", 40, 150f, 128);
            ComicSprite("UI_Panel", 64, 100f, 192);
            ComicSprite("UI_Plate", 32, 150f, 96);
            ComicSprite("UI_Ribbon", new Vector4(72, 0, 72, 0), 100f, 256);
            ComicSprite("UI_Spark", 0, 100f, 32);
            ComicSprite("Comic/UI_Burst_A", 0, 100f, 1024);
            ComicSprite("Comic/UI_Burst_B", 0, 100f, 1024);
            for (var i = 0; i < FlameFrames; i++)
                ComicSprite("UI_Flame_" + i, 0, 100f, 128);
        }

        private static void ComicSprite(string name, float border, float pixelsPerUnit, int maxSize)
        {
            ComicSprite(name, new Vector4(border, border, border, border), pixelsPerUnit, maxSize);
        }

        private static void ComicSprite(string name, Vector4 border, float pixelsPerUnit, int maxSize)
        {
            var path = ComicSprites + "/" + name + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogError("[Zonk] No texture " + path + ": run Tools/Blender/zonk_ui.py");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;

            // Полный прямоугольник: иначе Tiled и Sliced работают с искажениями.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Sprite ComicSpriteAsset(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ComicSprites + "/" + name + ".png");
        }

        // ---------- Шрифты и палитра ----------

        private static Material DisplayMaterial(TMP_FontAsset font)
        {
            var path = Fonts + "/" + DisplayFontName + " Comic.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(font.material) { name = DisplayFontName + " Comic" };
            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, Ink);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Шрифты и палитра ставятся один раз: дальше UiConfig правится руками, повторный запуск не затирает.</summary>
        private static void SetupComicConfig()
        {
            var firstRun = _ui.DisplayFont == null;
            _ui.Font = FontAsset("Rubik-Medium");
            _ui.BoldFont = FontAsset("Rubik-Bold");
            _ui.DisplayFont = FontAsset(DisplayFontName);
            _ui.DisplayMaterial = DisplayMaterial(_ui.DisplayFont);
            _ui.DisplaySmallMaterial = DisplaySmallMaterial(_ui.DisplayFont);

            if (firstRun)
            {
                var palette = _ui.Palette;
                palette.Panel = new Color(0.141f, 0.102f, 0.075f, 1f);
                palette.PanelLight = new Color(0.2f, 0.149f, 0.106f, 1f);
                palette.Button = new Color(0.184f, 0.498f, 0.839f, 1f);
                palette.ButtonAccent = new Color(0.961f, 0.502f, 0.102f, 1f);
                palette.ButtonMuted = new Color(0.416f, 0.353f, 0.29f, 1f);
                palette.Text = Paper;
                palette.TextMuted = new Color(0.765f, 0.682f, 0.557f, 1f);
                palette.Good = new Color(0.184f, 0.639f, 0.353f, 1f);
                palette.Bad = new Color(0.878f, 0.212f, 0.173f, 1f);
                palette.Gold = new Color(1f, 0.776f, 0.18f, 1f);
                palette.Bank = new Color(0.184f, 0.639f, 0.353f, 1f);
            }

            EditorUtility.SetDirty(_ui);
        }

        /// <summary>Старые цвета палитры в префабах заменяются новыми (значения по умолчанию из UiPalette, какими их записал генератор).</summary>
        private static List<KeyValuePair<Color, Color>> ComicColorMap()
        {
            var old = new UiPalette();
            var now = _ui.Palette;
            return new List<KeyValuePair<Color, Color>>
            {
                Pair(old.Panel, now.Panel),
                Pair(old.PanelLight, now.PanelLight),
                Pair(old.Button, now.Button),
                Pair(old.ButtonAccent, now.ButtonAccent),
                Pair(old.ButtonMuted, now.ButtonMuted),
                Pair(old.Text, now.Text),
                Pair(old.TextMuted, now.TextMuted),
                Pair(old.Good, now.Good),
                Pair(old.Bad, now.Bad),
                Pair(old.Gold, now.Gold),
                Pair(old.Bank, now.Bank),
            };
        }

        private static KeyValuePair<Color, Color> Pair(Color from, Color to) => new KeyValuePair<Color, Color>(from, to);

        private static bool Remap(List<KeyValuePair<Color, Color>> map, Color color, out Color result)
        {
            foreach (var pair in map)
            {
                var from = pair.Key;
                if (Mathf.Abs(from.r - color.r) < 0.015f && Mathf.Abs(from.g - color.g) < 0.015f &&
                    Mathf.Abs(from.b - color.b) < 0.015f && Mathf.Abs(from.a - color.a) < 0.015f && pair.Value != color)
                {
                    result = pair.Value;
                    return true;
                }
            }

            result = color;
            return false;
        }

        // ---------- Детали ----------

        private static void ComicText(TMP_Text text, bool upper = true)
        {
            text.font = _ui.DisplayFont;
            text.fontSharedMaterial = _ui.DisplayMaterial;
            if (upper)
                text.fontStyle |= FontStyles.UpperCase;
        }

        /// <summary>Табличка: тянется без искажений, цвет задаётся Image.color.</summary>
        private static GameObject PlatePart()
        {
            return Part("Plate", () =>
            {
                var root = UiRect("Plate", null);
                root.sizeDelta = new Vector2(320f, 96f);
                var image = root.gameObject.AddComponent<Image>();
                image.sprite = ComicSpriteAsset("UI_Plate");
                image.type = Image.Type.Sliced;
                image.color = _ui.Palette.Panel;
                image.raycastTarget = false;
                return root.gameObject;
            });
        }

        /// <summary>Лента заголовка: скошенные концы, надпись внутри.</summary>
        private static GameObject RibbonPart()
        {
            return Part("Ribbon", () =>
            {
                var root = UiRect("Ribbon", null);
                root.sizeDelta = new Vector2(520f, 96f);
                root.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
                var image = root.gameObject.AddComponent<Image>();
                image.sprite = ComicSpriteAsset("UI_Ribbon");
                image.type = Image.Type.Sliced;
                image.color = _ui.Palette.Bad;
                image.raycastTarget = false;

                var label = UiText("Label", root, _ui.DisplayFont, 44, Color.white);
                ComicText(label);
                AutoSize(label, 20f, 44f);
                Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(-2f, 4f), new Vector2(-96f, -24f));
                return root.gameObject;
            });
        }

        private static Sprite[] FlameSprites()
        {
            var frames = new Sprite[FlameFrames];
            for (var i = 0; i < frames.Length; i++)
                frames[i] = ComicSpriteAsset("UI_Flame_" + i);
            return frames;
        }

        /// <summary>Один огонёк: кадры по очереди и лёгкое качание. Основание внизу, ставится на край чего угодно.</summary>
        private static GameObject FlamePart()
        {
            return Part("Flame", () =>
            {
                var root = UiRect("Flame", null);
                root.sizeDelta = new Vector2(72f, 90f);
                root.pivot = new Vector2(0.5f, 0f);
                var image = root.gameObject.AddComponent<Image>();
                var frames = FlameSprites();
                image.sprite = frames[0];
                image.raycastTarget = false;
                image.preserveAspect = true;
                root.gameObject.AddComponent<SpriteFlipbook>().EditorSetup(image, frames, FlameFps, 0);
                root.gameObject.AddComponent<UiSway>().EditorSetup(3f, 0f, 0.06f, 1.7f, 0f);
                return root.gameObject;
            });
        }

        private static void NestFlame(GameObject flame, RectTransform parent, int index)
        {
            var copy = Nest(flame, parent, "Flame" + (index + 1));
            copy.GetComponent<SpriteFlipbook>().EditorSetOffset(index * 3 % FlameFrames);
            copy.GetComponent<UiSway>().EditorSetup(3f, 0f, 0.06f, 1.7f, index * 0.37f % 1f);
        }

        /// <summary>Ряд огня над верхним краем родителя: растягивается по его ширине.</summary>
        private static GameObject FlameRowPart(GameObject flame)
        {
            return Part("FlameRow", () =>
            {
                var root = UiRect("FlameRow", null);
                root.anchorMin = new Vector2(0f, 1f);
                root.anchorMax = new Vector2(1f, 1f);
                root.pivot = new Vector2(0.5f, 0f);
                root.sizeDelta = new Vector2(0f, 72f);
                root.anchoredPosition = new Vector2(0f, -6f);
                const int count = 6;
                for (var i = 0; i < count; i++)
                {
                    NestFlame(flame, root, i);
                    var rect = (RectTransform)root.GetChild(i);
                    rect.anchorMin = rect.anchorMax = new Vector2((i + 0.5f) / count, 0f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(58f, 72f);
                }

                return root.gameObject;
            });
        }

        /// <summary>Кольцо огня по эллипсу вокруг родителя: огоньки смотрят наружу, позиции в долях родителя.</summary>
        private static GameObject FlameRingPart(GameObject flame)
        {
            return Part("FlameRing", () =>
            {
                var root = UiRect("FlameRing", null);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                const int count = 14;
                for (var i = 0; i < count; i++)
                {
                    NestFlame(flame, root, i);
                    var rect = (RectTransform)root.GetChild(i);
                    var angle = i * Mathf.PI * 2f / count;
                    var anchor = new Vector2(0.5f + Mathf.Cos(angle) * 0.42f, 0.5f + Mathf.Sin(angle) * 0.34f);
                    rect.anchorMin = rect.anchorMax = anchor;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(150f, 188f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                }

                return root.gameObject;
            });
        }

        /// <summary>Плашка-взрыв: кольцо огня, плашка, слово, подпись.</summary>
        private static GameObject ComicBurstPart(GameObject ring)
        {
            return Part("ComicBurst", () =>
            {
                var root = UiRect("ComicBurst", null);
                root.sizeDelta = new Vector2(1000f, 430f);
                root.localRotation = Quaternion.Euler(0f, 0f, 4f);
                var group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                var fire = Nest(ring, root, "Fire");

                var shapeRect = UiRect("Shape", root);
                Stretch(shapeRect);
                var shape = shapeRect.gameObject.AddComponent<Image>();
                shape.sprite = ComicSpriteAsset("Comic/UI_Burst_A");
                shape.raycastTarget = false;

                var word = UiText("Word", root, _ui.DisplayFont, 120, Color.white);
                ComicText(word);
                AutoSize(word, 44f, 120f);
                word.rectTransform.anchorMin = new Vector2(0.16f, 0.3f);
                word.rectTransform.anchorMax = new Vector2(0.84f, 0.86f);
                word.rectTransform.offsetMin = word.rectTransform.offsetMax = Vector2.zero;
                word.gameObject.AddComponent<UiSway>().EditorSetup(1.2f, 0f, 0.025f, 2.2f, 0f);

                var caption = UiText("Caption", root, _ui.BoldFont, 34, _ui.Palette.Gold);
                AutoSize(caption, 18f, 34f);
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                ComicText(caption, false);
                caption.rectTransform.anchorMin = new Vector2(0.2f, 0.17f);
                caption.rectTransform.anchorMax = new Vector2(0.8f, 0.31f);
                caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;

                root.gameObject.AddComponent<ComicBurstView>().EditorSetup(group, shape, word, caption, fire);
                return root.gameObject;
            });
        }

        private static GameObject ComicLabelPart(string name, float size, Vector2 rectSize, bool wrap)
        {
            return Part(name, () =>
            {
                var text = UiText(name, null, _ui.DisplayFont, size, Color.white);
                ComicText(text, false);
                text.rectTransform.sizeDelta = rectSize;
                text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                return text.gameObject;
            });
        }

        // ---------- Применение к окнам ----------

        public static void ApplyComicSkin()
        {
            if (_ui == null)
                _ui = AssetDatabase.LoadAssetAtPath<UiConfig>(ConfigsFolder + "/Ui/UiConfig.asset");
            if (_ui == null)
                return;

            SetupComicConfig();

            var plate = PlatePart();
            RibbonPart();
            var flame = FlamePart();
            var row = FlameRowPart(flame);
            var ring = FlameRingPart(flame);
            var burst = ComicBurstPart(ring);
            _ui.ToastPrefab = ComicLabelPart("Toast", 84f, new Vector2(1200f, 160f), true).GetComponent<TMP_Text>();
            _ui.PopupPrefab = ComicLabelPart("Popup", 64f, new Vector2(700f, 120f), false).GetComponent<TMP_Text>();
            _ui.ToastPlatePrefab = LabelPlatePart(plate);
            EditorUtility.SetDirty(_ui);
            AssetDatabase.SaveAssets();

            var map = ComicColorMap();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs + "/UI" }))
                SkinPrefab(AssetDatabase.GUIDToAssetPath(guid), map);

            AddComicToHud(plate, row, burst);
            UpgradeHudPanelPart();
            UpgradeShopCardPart();
            UpgradeHudLayout();
            UpgradeMainMenu();
        }

        private static void SkinPrefab(string path, List<KeyValuePair<Color, Color>> map)
        {
            if (IsLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var isPart = path.Replace('\\', '/').StartsWith(PartsFolder + "/");
                var changed = false;

                foreach (var image in root.GetComponentsInChildren<Image>(true))
                    changed |= SkinImage(image, root, isPart, map);

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    changed |= SkinText(text, map);

                changed |= FixStretchingRows(root);

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Деталь с картинкой, которую подставляет код или которая сама себе картинка: подложка не нужна.
        private static readonly string[] NoSkinNames =
        {
            "Portrait", "Avatar", "Icon", "Fill", "Bar", "Frame", "Flag", "Selection", "Selected", "Highlight", "Star", "Coin", "Dim", "Flame", "Shape",
        };

        private static bool SkinImage(Image image, GameObject root, bool isPart, List<KeyValuePair<Color, Color>> map)
        {
            var changed = false;
            if (Remap(map, image.color, out var color))
            {
                image.color = color;
                changed = true;
            }

            // Вид вложенной детали задаёт её собственный префаб: здесь только цвет копии.
            if (image.sprite != null || PrefabUtility.IsPartOfPrefabInstance(image))
                return changed;

            var view = image.GetComponent<UiButtonView>();
            var selectable = image.GetComponent<Selectable>();
            var isButton = view != null && view.Background == image;
            if (!isButton)
            {
                foreach (var name in NoSkinNames)
                {
                    if (image.name.Contains(name))
                        return changed;
                }
            }

            var isSelectable = selectable != null && selectable.targetGraphic == image;
            var isPanel = image.name.Contains("Panel") && image.GetComponent<HudPlayerPanel>() == null;
            var isPartRoot = isPart && image.gameObject == root;

            if (isButton)
            {
                image.sprite = ComicSpriteAsset("UI_Button");
                image.type = Image.Type.Tiled;
                MoveButtonLabel(view);
            }
            else if (isPanel)
            {
                image.sprite = ComicSpriteAsset("UI_Panel");
                image.type = Image.Type.Sliced;
            }
            else if (isSelectable || isPartRoot)
            {
                image.sprite = ComicSpriteAsset("UI_Plate");
                image.type = Image.Type.Sliced;
            }
            else
            {
                return changed;
            }

            image.fillCenter = true;
            return true;
        }

        /// <summary>У кнопки снизу толстая тень: надпись чуть выше, чтобы стояла по центру заливки.</summary>
        private static void MoveButtonLabel(UiButtonView view)
        {
            if (view.Label != null)
                view.Label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        }

        private static bool SkinText(TMP_Text text, List<KeyValuePair<Color, Color>> map)
        {
            var changed = false;
            if (Remap(map, text.color, out var color))
            {
                text.color = color;
                changed = true;
            }

            var inButton = text.GetComponentInParent<UiButtonView>(true) != null;
            var size = text.enableAutoSizing ? text.fontSizeMax : text.fontSize;
            if (inButton)
            {
                if (size < ButtonMinFont)
                {
                    size = ButtonMinFont;
                    text.fontSize = size;
                    text.fontSizeMax = size;
                    changed = true;
                }

                var buttonMaterial = size < DisplaySmallSize ? _ui.DisplaySmallMaterial : _ui.DisplayMaterial;
                if (text.font == _ui.DisplayFont && text.fontSharedMaterial != buttonMaterial)
                {
                    text.fontSharedMaterial = buttonMaterial;
                    changed = true;
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(text))
                return changed;

            var bold = text.font == _ui.BoldFont || text.font == _ui.DisplayFont ||
                       (text.font != null && text.font.name.Contains("Bold"));
            var display = inButton || (bold && size >= DisplayMinSize);

            var font = display ? _ui.DisplayFont : bold ? _ui.BoldFont : _ui.Font;
            // Тёмный текст на светлой табличке: чернильный контур сделал бы из него пятно.
            var dark = text.color.r + text.color.g + text.color.b < 0.9f;
            var material = !display || dark ? font.material : size < DisplaySmallSize ? _ui.DisplaySmallMaterial : _ui.DisplayMaterial;

            if (text.font != font)
            {
                text.font = font;
                changed = true;
            }

            if (text.fontSharedMaterial != material)
            {
                text.fontSharedMaterial = material;
                changed = true;
            }

            if (display && !dark && (inButton || size >= 40f) && (text.fontStyle & FontStyles.UpperCase) == 0)
            {
                text.fontStyle |= FontStyles.UpperCase;
                changed = true;
            }

            return changed;
        }

        /// <summary>В окно партии: табличка очков хода, огонь на ней и на кнопке броска, плашка-взрыв.</summary>
        private static void AddComicToHud(GameObject plate, GameObject row, GameObject burst)
        {
            var path = Prefabs + "/UI/MatchHudWindow.prefab";
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.transform.Find("ComicBurst") != null)
                    return;

                var window = root.GetComponent<MatchHudWindow>();
                var turn = (RectTransform)root.transform.Find("Turn");
                Transform roll = null;
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Roll")
                        roll = child;
                }

                if (window == null || turn == null || roll == null)
                {
                    Debug.LogError("[Zonk] MatchHudWindow: Turn or Roll not found, comic parts not added");
                    return;
                }

                // Табличка под строкой очков, текст на ней тёмный.
                var plateObject = Nest(plate, root.transform, "TurnPlate");
                var plateRect = (RectTransform)plateObject.transform;
                Corner(plateRect, new Vector2(0.5f, 1f), new Vector2(440f, 112f), new Vector2(0f, -56f));
                plateObject.transform.SetSiblingIndex(turn.GetSiblingIndex());
                var plateImage = plateObject.GetComponent<Image>();
                plateImage.color = Paper;
                Corner(turn, new Vector2(0.5f, 1f), new Vector2(400f, 100f), new Vector2(0f, -60f));
                var turnText = turn.GetComponent<TMP_Text>();
                turnText.color = Ink;
                turnText.outlineWidth = 0f;
                turnText.fontSharedMaterial = turnText.font.material;

                var turnFire = Nest(row, plateRect, "Fire");
                turnFire.SetActive(false);
                var rollFire = Nest(row, roll, "Fire");
                rollFire.SetActive(false);

                var burstObject = Nest(burst, root.transform, "ComicBurst");
                Place((RectTransform)burstObject.transform, new Vector2(0.5f, 0.56f), new Vector2(1000f, 430f));
                burstObject.SetActive(false);

                window.EditorSetupComic(burstObject.GetComponent<ComicBurstView>(), plateImage, turnFire, rollFire);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
