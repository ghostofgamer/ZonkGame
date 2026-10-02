using System;
using System.Collections.Generic;
using Base.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Utils;

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

    /// <summary>
    /// Текст по ключу с подстановкой («Монеты: {0}»). Ошибка формата в переводе не роняет окно: показывается шаблон.
    /// Для редкого (открытие окна, нажатие): string.Format выделяет память. Частые числа — NumberTemplate.
    /// </summary>
    public static class UiFormat
    {
        public static string Format(ILocalization localization, string key, params object[] args)
        {
            var format = localization != null ? localization.Get(key) : key;
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }

    /// <summary>
    /// Шаблон с одним числом («Монеты: {0}», «Забрать {0}») для часто меняющегося текста: шаблон режется на префикс
    /// и суффикс один раз (и заново после смены языка), число пишется NumberText без выделения памяти.
    /// Текст перезаписывается, только если изменились число или шаблон.
    /// </summary>
    public sealed class NumberTemplate
    {
        private string _template;
        private string _prefix;
        private string _suffix;
        private int _shown;

        public void Set(TMP_Text text, string template, int value)
        {
            if (text == null)
                return;

            if (!ReferenceEquals(template, _template))
            {
                _template = template;
                Split(template, out _prefix, out _suffix);
            }
            else if (value == _shown)
            {
                return;
            }

            _shown = value;
            NumberText.Set(text, _prefix, value, _suffix);
        }

        /// <summary>«Очки хода: {0}» → «Очки хода: » и «». Без {0} число дописывается через пробел.</summary>
        public static void Split(string template, out string prefix, out string suffix)
        {
            template = template ?? string.Empty;
            var at = template.IndexOf("{0}", StringComparison.Ordinal);
            prefix = at >= 0 ? template.Substring(0, at) : template + " ";
            suffix = at >= 0 ? template.Substring(at + 3) : null;
        }
    }

    /// <summary>
    /// Мимолётные надписи из кода (Toast, всплывающие очки, облачко реплики, летящая монетка) — из пулов:
    /// TextMeshPro с контуром создаётся один раз и дальше только переносится. Окна — префабы (UiWindow), не здесь.
    /// Строки берутся из ILocalization по ключу.
    /// </summary>
    public sealed class UiKit
    {
        private readonly ILocalization _localization;
        private readonly Stack<TMP_Text> _toasts = new Stack<TMP_Text>();
        private readonly Stack<TMP_Text> _popups = new Stack<TMP_Text>();
        private readonly Stack<Image> _bubbles = new Stack<Image>();
        private readonly Stack<Image> _coins = new Stack<Image>();
        private Transform _poolRoot;

        public UiKit(ILocalization localization, GameConfig config)
        {
            _localization = localization;
            Config = config != null ? config.Ui : null;
            if (Config != null)
                UiColors.Apply(Config.Palette);
        }

        public UiConfig Config { get; }

        private TMP_FontAsset Font => Config != null && Config.Font != null ? Config.Font : TMP_Settings.defaultFontAsset;

        private TMP_FontAsset BoldFont => Config != null && Config.BoldFont != null ? Config.BoldFont : Font;

        public string T(string key)
        {
            return _localization.Get(key);
        }

        public string T(string key, params object[] args)
        {
            return UiFormat.Format(_localization, key, args);
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

        /// <summary>Надпись для Toast из пула. Вернуть — ReturnToast.</summary>
        public TMP_Text RentToast(Transform parent)
        {
            var label = Pop(_toasts);
            if (label == null)
            {
                label = Label(parent, 84, UiColors.Text);
                label.name = "Toast";
                Outline(label, new Color(0f, 0f, 0f, 0.8f), 0.25f);
            }

            return Show(label, parent);
        }

        public void ReturnToast(TMP_Text label)
        {
            Push(_toasts, label);
        }

        /// <summary>
        /// Всплывающая надпись над костями («+350», «Стрит») из пула, отдельного от Toast: у неё свой размер шрифта.
        /// Вернуть — ReturnPopup.
        /// </summary>
        public TMP_Text RentPopup(Transform parent, float fontSize)
        {
            var label = Pop(_popups);
            if (label == null)
            {
                label = Label(parent, 64, UiColors.Text);
                label.name = "Popup";
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.sizeDelta = new Vector2(700f, 120f);
                Outline(label, new Color(0f, 0f, 0f, 0.85f), 0.28f);
            }

            label.fontSize = fontSize;
            return Show(label, parent);
        }

        public void ReturnPopup(TMP_Text label)
        {
            Push(_popups, label);
        }

        /// <summary>Облачко реплики из пула (панель с надписью внутри). Вернуть — ReturnBubble.</summary>
        public Image RentBubble(Transform parent, out TMP_Text label)
        {
            var panel = Pop(_bubbles);
            if (panel == null)
            {
                panel = Panel("Bubble", parent, new Color(1f, 0.97f, 0.9f, 0.95f));
                var text = Label(panel.transform, 28, new Color(0.15f, 0.1f, 0.05f), false);
                Stretch(text.rectTransform, 16, 16, 8, 8);
            }

            label = panel.GetComponentInChildren<TMP_Text>(true);
            return Show(panel, parent);
        }

        public void ReturnBubble(Image panel)
        {
            Push(_bubbles, panel);
        }

        /// <summary>Летящая монетка из пула (картинка UiConfig.CoinSprite). Вернуть — ReturnCoin.</summary>
        public Image RentCoin(Transform parent)
        {
            var coin = Pop(_coins);
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

            return Show(coin, parent);
        }

        public void ReturnCoin(Image coin)
        {
            Push(_coins, coin);
        }

        /// <summary>Свободный из пула; уничтоженные вместе со сценой пропускаются.</summary>
        private static T Pop<T>(Stack<T> pool) where T : Component
        {
            while (pool.Count > 0)
            {
                var item = pool.Pop();
                if (item != null)
                    return item;
            }

            return null;
        }

        private void Push<T>(Stack<T> pool, T item) where T : Component
        {
            // Окно могли закрыть посреди показа: вместе с ним уничтожен и объект, в пул его не вернуть.
            if (item == null)
                return;

            item.gameObject.SetActive(false);
            item.transform.SetParent(PoolRoot, false);
            pool.Push(item);
        }

        private static T Show<T>(T item, Transform parent) where T : Component
        {
            item.transform.SetParent(parent, false);
            item.transform.SetAsLastSibling();
            item.gameObject.SetActive(true);
            return item;
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

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Растянуть на всего родителя с отступами.</summary>
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Надпись по центру шрифтом из UiConfig (Noto Sans: латиница, кириллица, турецкий).</summary>
        private TMP_Text Label(Transform parent, int size, Color color, bool bold = true)
        {
            var label = Rect("Label", parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bold ? BoldFont : Font;
            label.text = string.Empty;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Контур вокруг текста для надписей поверх сцены.</summary>
        private static void Outline(TMP_Text label, Color color, float width)
        {
            label.outlineColor = color;
            label.outlineWidth = width;
        }
    }
}
