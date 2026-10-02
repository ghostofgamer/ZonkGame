using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Zonk.UI;
using Zonk.UI.Transitions;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Интерфейс: шрифтовые ассеты TextMeshPro (Noto Sans, динамический атлас: буквы всех языков добавляются сами),
    /// библиотека стилей появления и скрытия окон, UiConfig и префаб экрана загрузки.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string Fonts = Root + "/Art/Fonts";
        private const string TransitionsFolder = ConfigsFolder + "/Ui/Transitions";
        private const int TipCount = 12;

        public static UiConfig BuildUi(out GameObject loadingScreen)
        {
            EnsureFolder(TransitionsFolder);
            EnsureFolder(Prefabs + "/UI");

            var regular = FontAsset("NotoSans-Regular");
            var bold = FontAsset("NotoSans-Bold");
            var transitions = BuildTransitions();

            var config = Asset<UiConfig>(ConfigsFolder + "/Ui/UiConfig.asset", c =>
            {
                c.Font = regular;
                c.BoldFont = bold;
                c.ScreenShow = transitions["Rise_In"];
                c.ScreenHide = transitions["Fade_Out"];
                c.PopupShow = transitions["Pop_In"];
                c.PopupHide = transitions["Pop_Out"];
                c.ErrorShake = transitions["Shake"];
                for (var i = 1; i <= TipCount; i++)
                    c.TipKeys.Add("tip." + i);
            });

            loadingScreen = BuildLoadingScreen(config, transitions["Fade_Out"]);
            return config;
        }

        /// <summary>Динамический шрифтовой ассет TMP: атлас заполняется символами по мере появления в текстах.</summary>
        private static TMP_FontAsset FontAsset(string name)
        {
            var path = Fonts + "/" + name + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "/" + name + ".ttf");
            if (font == null)
            {
                Debug.LogError("[Zonk] Font not found: " + Fonts + "/" + name + ".ttf");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, path);

            // Атлас и материал живут внутри ассета шрифта, иначе они потеряются при сохранении.
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Библиотека стилей переходов. Любой можно поправить в инспекторе или собрать свой.</summary>
        private static Dictionary<string, UiTransitionConfig> BuildTransitions()
        {
            var result = new Dictionary<string, UiTransitionConfig>();

            void Add(string name, float duration, Ease ease, params UiEffect[] effects)
            {
                result[name] = Asset<UiTransitionConfig>(TransitionsFolder + "/" + name + ".asset", t =>
                {
                    t.Duration = duration;
                    t.Ease = ease;
                    t.Effects = new List<UiEffect>(effects);
                });
            }

            // Появление.
            Add("Fade_In", 0.25f, Ease.OutCubic, new FadeEffect());
            Add("Pop_In", 0.3f, Ease.OutBack, new ScaleEffect(), new FadeEffect { DurationScale = 0.5f });
            Add("Bounce_In", 0.6f, Ease.OutElastic, new ScaleEffect { From = new Vector3(0f, 0f, 1f) },
                new FadeEffect { DurationScale = 0.3f, OverrideEase = true, Ease = Ease.Linear });
            Add("Zoom_In", 0.3f, Ease.OutCubic, new ScaleEffect { From = new Vector3(1.15f, 1.15f, 1f) }, new FadeEffect());
            Add("Rise_In", 0.3f, Ease.OutCubic, new MoveEffect(), new FadeEffect());
            Add("Flip_In", 0.4f, Ease.OutBack, new RotateEffect(), new FadeEffect { DurationScale = 0.5f });
            Add("Drop_In", 0.6f, Ease.OutBounce, new SlideEffect { Edge = UiEdge.Top });
            Add("Slide_In_Bottom", 0.4f, Ease.OutCubic, new SlideEffect { Edge = UiEdge.Bottom }, new FadeEffect());
            Add("Slide_In_Top", 0.4f, Ease.OutCubic, new SlideEffect { Edge = UiEdge.Top }, new FadeEffect());
            Add("Slide_In_Left", 0.4f, Ease.OutCubic, new SlideEffect { Edge = UiEdge.Left }, new FadeEffect());
            Add("Slide_In_Right", 0.4f, Ease.OutCubic, new SlideEffect { Edge = UiEdge.Right }, new FadeEffect());
            Add("Stagger_In", 0.3f, Ease.OutBack, new FadeEffect { DurationScale = 0.5f }, new StaggerChildrenEffect());

            // Скрытие.
            Add("Fade_Out", 0.2f, Ease.InCubic, new FadeEffect { From = 1f, To = 0f });
            Add("Pop_Out", 0.2f, Ease.InBack, new ScaleEffect { From = Vector3.one, To = new Vector3(0.85f, 0.85f, 1f) },
                new FadeEffect { From = 1f, To = 0f });
            Add("Shrink_Out", 0.25f, Ease.InBack, new ScaleEffect { From = Vector3.one, To = new Vector3(0f, 0f, 1f) },
                new FadeEffect { From = 1f, To = 0f, DurationScale = 0.8f });
            Add("Zoom_Out", 0.25f, Ease.InCubic, new ScaleEffect { From = Vector3.one, To = new Vector3(1.15f, 1.15f, 1f) },
                new FadeEffect { From = 1f, To = 0f });
            Add("Sink_Out", 0.25f, Ease.InCubic, new MoveEffect { Outgoing = true }, new FadeEffect { From = 1f, To = 0f });
            Add("Slide_Out_Bottom", 0.35f, Ease.InCubic, new SlideEffect { Edge = UiEdge.Bottom, Outgoing = true },
                new FadeEffect { From = 1f, To = 0f });
            Add("Slide_Out_Top", 0.35f, Ease.InCubic, new SlideEffect { Edge = UiEdge.Top, Outgoing = true },
                new FadeEffect { From = 1f, To = 0f });
            Add("Slide_Out_Left", 0.35f, Ease.InCubic, new SlideEffect { Edge = UiEdge.Left, Outgoing = true },
                new FadeEffect { From = 1f, To = 0f });
            Add("Slide_Out_Right", 0.35f, Ease.InCubic, new SlideEffect { Edge = UiEdge.Right, Outgoing = true },
                new FadeEffect { From = 1f, To = 0f });

            // Акценты.
            Add("Shake", 0.4f, Ease.Linear, new ShakeEffect());
            Add("Punch", 0.3f, Ease.Linear, new PunchScaleEffect());
            return result;
        }

        /// <summary>
        /// Экран загрузки: свой Canvas поверх всего, фон, название, полоса прогресса, подсказка.
        /// Префаб можно перерисовать как угодно, ссылки в компоненте LoadingScreen.
        /// </summary>
        private static GameObject BuildLoadingScreen(UiConfig config, UiTransitionConfig hide)
        {
            return Prefab(Prefabs + "/UI/LoadingScreen.prefab", () =>
            {
                var root = new GameObject("LoadingScreen");
                AddScaledCanvas(root).sortingOrder = 100;

                var window = UiRect("Window", root.transform);
                Stretch(window);
                window.gameObject.AddComponent<CanvasGroup>();
                window.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.07f, 0.05f, 1f);

                var title = UiText("Title", window, config.BoldFont, 110, config.Palette.Gold);
                Place(title.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(1400, 200));

                var bar = UiRect("Bar", window);
                Place(bar, new Vector2(0.5f, 0.4f), new Vector2(760, 20));
                bar.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
                var fill = UiRect("Fill", bar);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0.05f, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                fill.gameObject.AddComponent<Image>().color = config.Palette.Gold;

                var tip = UiText("Tip", window, config.Font, 34, config.Palette.TextMuted);
                Place(tip.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(1300, 160));

                var screen = window.gameObject.AddComponent<LoadingScreen>();
                screen.EditorSetup(fill, tip, title);
                screen.EditorSetupTransitions(null, hide, false);
                return root;
            });
        }

        private static RectTransform UiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI UiText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var text = UiRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Автоподбор размера текста в пределах min…max: длинные строки на других языках не вылезают.</summary>
        private static void AutoSize(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        /// <summary>Тёмная обводка текста поверх 3D-сцены.</summary>
        private static void DarkOutline(TextMeshProUGUI text)
        {
            text.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            text.outlineWidth = 0.2f;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
