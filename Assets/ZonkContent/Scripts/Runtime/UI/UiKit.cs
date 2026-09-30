using System;
using Base.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zonk.Configs;

namespace Zonk.UI
{
    /// <summary>
    /// Цвета интерфейса. Значения берутся из UiConfig.Palette при создании UiKit, здесь только запасные.
    /// </summary>
    public static class UiColors
    {
        public static Color Panel = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        public static Color PanelLight = new Color(0.16f, 0.13f, 0.1f, 0.92f);
        public static Color Button = new Color(0.55f, 0.36f, 0.16f, 1f);
        public static Color ButtonAccent = new Color(0.85f, 0.6f, 0.2f, 1f);
        public static Color ButtonMuted = new Color(0.3f, 0.27f, 0.24f, 1f);
        public static Color Text = new Color(0.97f, 0.93f, 0.85f, 1f);
        public static Color TextMuted = new Color(0.75f, 0.7f, 0.62f, 1f);
        public static Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static Color Bad = new Color(0.95f, 0.4f, 0.35f, 1f);
        public static Color Gold = new Color(1f, 0.82f, 0.35f, 1f);
        public static Color Bank = new Color(0.2f, 0.62f, 0.34f, 1f);

        public static void Apply(UiPalette palette)
        {
            if (palette == null)
                return;

            Panel = palette.Panel;
            PanelLight = palette.PanelLight;
            Button = palette.Button;
            ButtonAccent = palette.ButtonAccent;
            ButtonMuted = palette.ButtonMuted;
            Text = palette.Text;
            TextMuted = palette.TextMuted;
            Good = palette.Good;
            Bad = palette.Bad;
            Gold = palette.Gold;
            Bank = palette.Bank;
        }
    }

    /// <summary>Кнопка с подписью: подпись можно менять, кнопку выключать.</summary>
    public sealed class UiButton
    {
        public UiButton(Button button, TMP_Text label, Image background)
        {
            Button = button;
            Label = label;
            Background = background;
        }

        public Button Button { get; }
        public TMP_Text Label { get; }
        public Image Background { get; }
        public GameObject GameObject => Button.gameObject;

        public bool Interactable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }

        public void SetText(string text)
        {
            Label.text = text;
        }

        public void SetColor(Color color)
        {
            Background.color = color;
        }

        public void SetVisible(bool visible)
        {
            Button.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Построение uGUI из кода для экранов, которые ещё не переведены в префабы (UiWindow).
    /// Текст — TextMeshPro со шрифтом из UiConfig (Noto Sans: латиница, кириллица, турецкий).
    /// Строки берутся из ILocalization по ключу.
    /// </summary>
    public sealed class UiKit
    {
        private readonly ILocalization _localization;
        private readonly System.Collections.Generic.Stack<TMP_Text> _toasts = new System.Collections.Generic.Stack<TMP_Text>();
        private readonly System.Collections.Generic.Stack<TMP_Text> _popups = new System.Collections.Generic.Stack<TMP_Text>();
        private readonly System.Collections.Generic.Stack<Image> _bubbles =new System.Collections.Generic.Stack<Image>();
        private readonly System.Collections.Generic.Stack<Image> _coins = new System.Collections.Generic.Stack<Image>();
        private Transform _poolRoot;

        public UiKit(ILocalization localization, GameConfig config)
        {
            _localization = localization;
            Config = config != null ? config.Ui : null;
            if (Config != null)
                UiColors.Apply(Config.Palette);
        }

        public UiConfig Config { get; }

        public TMP_FontAsset Font => Config != null && Config.Font != null ? Config.Font : TMP_Settings.defaultFontAsset;

        public TMP_FontAsset BoldFont => Config != null && Config.BoldFont != null ? Config.BoldFont : Font;

        public string T(string key)
        {
            return _localization.Get(key);
        }

        public string T(string key, params object[] args)
        {
            var format = _localization.Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Растянуть на всего родителя с отступами.</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Прямоугольник заданного размера, привязанный к точке родителя (0..1).</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        public Image Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public TMP_Text Label(Transform parent, string text, int size = 28, TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null, bool bold = false)
        {
            var rect = Rect("Label", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bold ? BoldFont : Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = ToAlignment(align);
            label.color = color ?? UiColors.Text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Надпись для Toast из пула: создаётся один раз (TextMeshPro с контуром — это меш и свой материал), дальше
        /// только переносится в parent. Вернуть — ReturnToast.
        /// </summary>
        public TMP_Text RentToast(Transform parent)
        {
            TMP_Text label = null;
            while (label == null && _toasts.Count > 0)
                label = _toasts.Pop();

            if (label == null)
            {
                label = Label(parent, string.Empty, 84, TextAnchor.MiddleCenter, UiColors.Text, true);
                label.name = "Toast";
                Outline(label, new Color(0f, 0f, 0f, 0.8f), 0.25f);
            }

            label.transform.SetParent(parent, false);
            label.transform.SetAsLastSibling();
            label.gameObject.SetActive(true);
            return label;
        }

        public void ReturnToast(TMP_Text label)
        {
            if (label == null)
                return;

            label.gameObject.SetActive(false);
            label.transform.SetParent(PoolRoot, false);
            _toasts.Push(label);
        }

        /// <summary>
        /// Всплывающая надпись над костями («+350», «Стрит») из пула, отдельного от Toast: у неё свой размер шрифта.
        /// Вернуть — ReturnPopup.
        /// </summary>
        public TMP_Text RentPopup(Transform parent, float fontSize)
        {
            TMP_Text label = null;
            while (label == null && _popups.Count > 0)
                label = _popups.Pop();

            if (label == null)
            {
                label = Label(parent, string.Empty, 64, TextAnchor.MiddleCenter, UiColors.Text, true);
                label.name = "Popup";
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.sizeDelta = new Vector2(700f, 120f);
                Outline(label, new Color(0f, 0f, 0f, 0.85f), 0.28f);
            }

            label.fontSize = fontSize;
            label.transform.SetParent(parent, false);
            label.transform.SetAsLastSibling();
            label.gameObject.SetActive(true);
            return label;
        }

        public void ReturnPopup(TMP_Text label)
        {
            if (label == null)
                return;

            label.gameObject.SetActive(false);
            label.transform.SetParent(PoolRoot, false);
            _popups.Push(label);
        }

        /// <summary>Облачко реплики из пула (панель с надписью внутри). Вернуть — ReturnBubble.</summary>
        public Image RentBubble(Transform parent, out TMP_Text label)
        {
            Image panel = null;
            while (panel == null && _bubbles.Count > 0)
                panel = _bubbles.Pop();

            if (panel == null)
            {
                panel = Panel("Bubble", parent, new Color(1f, 0.97f, 0.9f, 0.95f));
                var text = Label(panel.transform, string.Empty, 28, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f));
                Stretch(text.rectTransform, 16, 16, 8, 8);
            }

            panel.transform.SetParent(parent, false);
            panel.transform.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            label = panel.GetComponentInChildren<TMP_Text>(true);
            return panel;
        }

        public void ReturnBubble(Image panel)
        {
            if (panel == null)
                return;

            panel.gameObject.SetActive(false);
            panel.transform.SetParent(PoolRoot, false);
            _bubbles.Push(panel);
        }

        /// <summary>Летящая монетка из пула (картинка UiConfig.CoinSprite). Вернуть — ReturnCoin.</summary>
        public Image RentCoin(Transform parent)
        {
            Image coin = null;
            while (coin == null && _coins.Count > 0)
                coin = _coins.Pop();

            if (coin == null)
            {
                coin = Panel("Coin", parent, Color.white);
                coin.sprite = Config != null ? Config.CoinSprite : null;
                coin.preserveAspect = true;
                coin.raycastTarget = false;
                coin.rectTransform.sizeDelta = new Vector2(56f, 56f);
                if (coin.sprite == null)
                    coin.color = UiColors.Gold;
            }

            coin.transform.SetParent(parent, false);
            coin.transform.SetAsLastSibling();
            coin.gameObject.SetActive(true);
            return coin;
        }

        public void ReturnCoin(Image coin)
        {
            if (coin == null)
                return;

            coin.gameObject.SetActive(false);
            coin.transform.SetParent(PoolRoot, false);
            _coins.Push(coin);
        }

        /// <summary>Выключенный объект сцены, где ждут надписи из пулов (уничтожается вместе со сценой).</summary>
        private Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var root = new GameObject("UiPool");
                    root.SetActive(false);
                    _poolRoot = root.transform;
                }

                return _poolRoot;
            }
        }

        /// <summary>Контур вокруг текста для надписей поверх сцены.</summary>
        public static void Outline(TMP_Text label, Color color, float width = 0.2f)
        {
            label.outlineColor = color;
            label.outlineWidth = width;
        }

        public UiButton Button(Transform parent, string text, UnityAction onClick, Color? color = null, int fontSize = 28)
        {
            var rect = Rect("Button", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color ?? UiColors.Button;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var label = Label(rect, text, fontSize, TextAnchor.MiddleCenter, null, true);
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(16, fontSize);
            label.fontSizeMax = fontSize;
            Stretch(label.rectTransform, 8, 8, 4, 4);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 64;
            layout.preferredHeight = 72;
            return new UiButton(button, label, image);
        }

        /// <summary>Кнопка-иконка. Без картинки показывает подпись fallbackText.</summary>
        public UiButton IconButton(Transform parent, Sprite icon, string fallbackText, UnityAction onClick)
        {
            var rect = Rect("IconButton", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.color = icon != null ? Color.white : UiColors.ButtonAccent;
            var button = rect.gameObject.AddComponent<Button>();
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var label = Label(rect, icon != null ? string.Empty : fallbackText, 48, TextAnchor.MiddleCenter, null, true);
            Stretch(label.rectTransform);
            return new UiButton(button, label, image);
        }

        /// <summary>
        /// Горизонтальный список с прокруткой и маской: сколько бы ни было элементов, за края он не вылезает.
        /// Элементы добавляются в content, их ширину задаёт LayoutElement.
        /// </summary>
        public ScrollRect HorizontalList(Transform parent, float spacing, out RectTransform content)
        {
            var root = Rect("List", parent);
            root.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        public VerticalLayoutGroup Column(Transform parent, float spacing = 12, int padding = 16, TextAnchor align = TextAnchor.UpperCenter)
        {
            var rect = Rect("Column", parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public HorizontalLayoutGroup Row(Transform parent, float spacing = 12, int padding = 0, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rect = Rect("Row", parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static LayoutElement Size(Component component, float preferredWidth = -1, float preferredHeight = -1,
            float flexibleWidth = -1)
        {
            if (!component.TryGetComponent<LayoutElement>(out var layout))
                layout = component.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0)
            {
                layout.preferredWidth = preferredWidth;
                layout.minWidth = preferredWidth;
            }

            if (preferredHeight >= 0)
            {
                layout.preferredHeight = preferredHeight;
                layout.minHeight = preferredHeight;
            }

            if (flexibleWidth >= 0)
                layout.flexibleWidth = flexibleWidth;
            return layout;
        }

        /// <summary>Поле ввода имени (TextMeshPro).</summary>
        public TMP_InputField Input(Transform parent, string value, int characterLimit = 16)
        {
            var background = Panel("Input", parent, new Color(1f, 1f, 1f, 0.12f));
            var area = Rect("TextArea", background.transform);
            Stretch(area, 12, 12, 4, 4);
            area.gameObject.AddComponent<RectMask2D>();

            var text = Label(area, value, 26, TextAnchor.MiddleLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;
            Stretch(text.rectTransform);

            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.fontAsset = Font;
            input.characterLimit = characterLimit;
            input.text = value;
            Size(background, -1, 56);
            return input;
        }

        /// <summary>Затемнение на весь экран, закрывает клики по столу.</summary>
        public Image Blocker(Transform parent, float alpha = 0.55f)
        {
            var image = Panel("Blocker", parent, new Color(0f, 0f, 0f, alpha));
            Stretch(image.rectTransform);
            return image;
        }

        private static TextAlignmentOptions ToAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }
    }
}
