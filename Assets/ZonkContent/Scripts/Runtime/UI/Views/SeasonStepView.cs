using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>Состояние награды ступени сезона.</summary>
    public enum SeasonCellState
    {
        /// <summary>Ступень ещё не пройдена.</summary>
        Locked,
        Claimable,
        Claimed,

        /// <summary>Платная: нужна платная дорожка.</summary>
        NeedsPass,

        /// <summary>Платная без покупок на площадке: за рекламу.</summary>
        ForAd,

        /// <summary>Наград нет.</summary>
        Empty,
    }

    /// <summary>
    /// Ступень сезонного пути (деталь Parts/SeasonStep): номер, сверху бесплатная награда, снизу платная.
    /// Ячейка: подложка (цвет по состоянию), картинка награды, подпись. Нажатие — FreeClicked / PremiumClicked.
    /// </summary>
    public sealed class SeasonStepView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _number;

        [SerializeField] private Button _free;
        [SerializeField] private Image _freeBackground;
        [SerializeField] private Image _freeIcon;
        [SerializeField] private TMP_Text _freeText;

        [SerializeField] private Button _premium;
        [SerializeField] private Image _premiumBackground;
        [SerializeField] private Image _premiumIcon;
        [SerializeField] private TMP_Text _premiumText;

        public event Action FreeClicked;
        public event Action PremiumClicked;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text number, Button free, Image freeBackground, Image freeIcon, TMP_Text freeText, Button premium,
            Image premiumBackground, Image premiumIcon, TMP_Text premiumText)
        {
            _number = number;
            _free = free;
            _freeBackground = freeBackground;
            _freeIcon = freeIcon;
            _freeText = freeText;
            _premium = premium;
            _premiumBackground = premiumBackground;
            _premiumIcon = premiumIcon;
            _premiumText = premiumText;
        }
#endif

        private void Awake()
        {
            _free.onClick.AddListener(() => FreeClicked?.Invoke());
            _premium.onClick.AddListener(() => PremiumClicked?.Invoke());
        }

        public void SetNumber(string text, Color color)
        {
            _number.text = text;
            _number.color = color;
        }

        public void SetFree(Sprite icon, string text, SeasonCellState state, Color background)
        {
            Paint(_freeBackground, _freeIcon, _freeText, icon, text, state, background);
        }

        public void SetPremium(Sprite icon, string text, SeasonCellState state, Color background)
        {
            Paint(_premiumBackground, _premiumIcon, _premiumText, icon, text, state, background);
        }

        private static void Paint(Image background, Image iconImage, TMP_Text label, Sprite icon, string text, SeasonCellState state,
            Color color)
        {
            background.color = color;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null && state != SeasonCellState.Empty;
            // Не пройдено или нужна платная дорожка — картинка приглушена.
            iconImage.color = state == SeasonCellState.Locked || state == SeasonCellState.NeedsPass
                ? new Color(1f, 1f, 1f, 0.45f)
                : state == SeasonCellState.Claimed ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
            label.text = text;
        }
    }
}
