using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    public enum ChallengeChoice
    {
        Primary,
        Secondary,
        Back,
    }

    /// <summary>
    /// Окно режима-испытания (башня, «Бесконечный забег»): заголовок, портрет соперника, текст (этаж, цель, правила,
    /// сердца, находки, рекорд — длинный текст прокручивается) и до трёх кнопок. Одно окно на лобби режима и на
    /// представление этажа перед партией: что писать, решает состояние режима.
    /// </summary>
    public sealed class ChallengeWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private UiButtonView _primary;
        [SerializeField] private UiButtonView _secondary;
        [SerializeField] private UiButtonView _back;

        private readonly Choice<ChallengeChoice> _choice = new Choice<ChallengeChoice>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, Image portrait, TMP_Text body, ScrollRect scroll, UiButtonView primary,
            UiButtonView secondary, UiButtonView back)
        {
            _title = title;
            _portrait = portrait;
            _body = body;
            _scroll = scroll;
            _primary = primary;
            _secondary = secondary;
            _back = back;
        }
#endif

        private float _bodyLeft = -1f;

        private void Awake()
        {
            if (_scroll != null)
                _bodyLeft = ((RectTransform)_scroll.transform).offsetMin.x;
            _primary.OnClick(() => _choice.Set(ChallengeChoice.Primary));
            if (_secondary != null)
                _secondary.OnClick(() => _choice.Set(ChallengeChoice.Secondary));
            _back.OnClick(() => _choice.Set(ChallengeChoice.Back));
        }

        /// <summary>secondary пустой — кнопки нет; primaryEnabled — можно ли нажать главную кнопку.</summary>
        public void Setup(string title, Sprite portrait, string body, string primary, string secondary, string back,
            bool primaryEnabled = true)
        {
            _title.text = title;
            _body.text = body;
            if (_portrait != null)
            {
                _portrait.sprite = portrait;
                _portrait.gameObject.SetActive(portrait != null);
            }

            _primary.SetText(primary);
            _primary.Interactable = primaryEnabled;
            if (_secondary != null)
            {
                _secondary.SetVisible(!string.IsNullOrEmpty(secondary));
                if (!string.IsNullOrEmpty(secondary))
                    _secondary.SetText(secondary);
            }

            _back.SetText(back);
            if (_scroll != null)
            {
                // Без портрета текст занимает всю ширину панели (отступ как справа).
                var rect = (RectTransform)_scroll.transform;
                var right = -rect.offsetMax.x;
                rect.offsetMin = new Vector2(portrait != null || _bodyLeft < 0f ? _bodyLeft : right, rect.offsetMin.y);
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        public UniTask<ChallengeChoice> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }
}
