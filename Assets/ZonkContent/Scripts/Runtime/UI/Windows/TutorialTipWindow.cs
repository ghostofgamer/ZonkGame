using TMPro;
using UnityEngine;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Подсказка обучения: табличка сверху с текстом и кнопкой «Понятно». Не модальная: у корня нет картинки,
    /// поэтому нажатия мимо таблички проходят в игру. Открывает и закрывает TutorialDirector.
    /// </summary>
    public sealed class TutorialTipWindow : UiWindow
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private UiButtonView _ok;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text text, UiButtonView ok)
        {
            _text = text;
            _ok = ok;
        }
#endif

        private void Awake()
        {
            _ok.OnClick(RequestClose);
        }

        public void Setup(string text)
        {
            _text.text = text;
            _ok.SetText(T("tutorial.ok"));
        }
    }
}
