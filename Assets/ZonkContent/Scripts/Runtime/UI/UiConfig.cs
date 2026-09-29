using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zonk.UI.Transitions;

namespace Zonk.UI
{
    /// <summary>Палитра интерфейса. Меняется в UiConfig, а не в коде.</summary>
    [System.Serializable]
    public sealed class UiPalette
    {
        public Color Panel = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        public Color PanelLight = new Color(0.16f, 0.13f, 0.1f, 0.92f);
        public Color Button = new Color(0.55f, 0.36f, 0.16f, 1f);
        public Color ButtonAccent = new Color(0.85f, 0.6f, 0.2f, 1f);
        public Color ButtonMuted = new Color(0.3f, 0.27f, 0.24f, 1f);
        public Color Text = new Color(0.97f, 0.93f, 0.85f, 1f);
        public Color TextMuted = new Color(0.75f, 0.7f, 0.62f, 1f);
        public Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);
        public Color Bad = new Color(0.95f, 0.4f, 0.35f, 1f);
        public Color Gold = new Color(1f, 0.82f, 0.35f, 1f);

        [Tooltip("Кнопка «Забрать очки»: должна явно отличаться от «Бросить»")]
        public Color Bank = new Color(0.2f, 0.62f, 0.34f, 1f);
    }

    /// <summary>
    /// Настройки интерфейса: шрифты, палитра, стили появления окон по умолчанию, окна-префабы, подсказки загрузки.
    /// Окно со своими переходами в префабе использует их, без своих — берёт отсюда.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/UI/Ui Config", fileName = "UiConfig")]
    public sealed class UiConfig : ScriptableObject
    {
        [Header("Шрифты (TextMeshPro, Noto Sans: латиница, кириллица, турецкий)")]
        public TMP_FontAsset Font;
        public TMP_FontAsset BoldFont;

        public UiPalette Palette = new UiPalette();

        [Header("Переходы по умолчанию")]
        public UiTransitionConfig ScreenShow;
        public UiTransitionConfig ScreenHide;
        public UiTransitionConfig PopupShow;
        public UiTransitionConfig PopupHide;

        [Tooltip("Акцент на ошибке: «не хватает монет»")]
        public UiTransitionConfig ErrorShake;

        [Header("Картинки")]
        [Tooltip("Грани костей для интерфейса: 1..6 по порядку")]
        public Sprite[] DieFaces = new Sprite[6];

        [Tooltip("Иконка кнопки правил в меню: книжка с вопросом")]
        public Sprite RulesIcon;

        [Tooltip("Монетка, которая летит в кошелёк при получении монет")]
        public Sprite CoinSprite;

        [Header("Языки")]
        [Tooltip("Флаг каждого языка для кнопки языка в настройках и окна выбора. Нет флага — только название")]
        public System.Collections.Generic.List<LanguageFlag> LanguageFlags = new System.Collections.Generic.List<LanguageFlag>();

        [Header("Звёзды")]
        [Tooltip("Полученная звезда (картинки звёзд в префабах Parts/Stars, OpponentRow, StarConditionRow)")]
        public Sprite StarGold;

        [Tooltip("Ещё не полученная звезда")]
        public Sprite StarGray;

        public Sprite FlagOf(string language)
        {
            foreach (var flag in LanguageFlags)
            {
                if (flag != null && flag.Code == language)
                    return flag.Flag;
            }

            return null;
        }

        [Tooltip("Значки внутри текста (★ звезда и др.): белые, красятся цветом текста. Нужны, потому что в шрифте таких символов нет")]
        public TMPro.TMP_SpriteAsset Icons;

        [Header("Книга правил")]
        public Rules.RulesBookConfig RulesBook;

        [Header("Окна-префабы: IUiService находит окно по типу компонента")]
        public List<UiWindow> Windows = new List<UiWindow>();

        [Header("Экран загрузки")]
        [Tooltip("Ключи подсказок в таблице локализации")]
        public List<string> TipKeys = new List<string>();

        [Tooltip("Как часто меняется подсказка, секунды")]
        public float TipInterval = 4f;

        public T FindWindow<T>() where T : UiWindow
        {
            foreach (var window in Windows)
            {
                if (window is T typed)
                    return typed;
            }

            return null;
        }
    }
}
