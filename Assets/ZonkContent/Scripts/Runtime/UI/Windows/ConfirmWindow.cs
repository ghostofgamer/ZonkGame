using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>Вопрос с двумя ответами. Удобный вызов: ConfirmWindow.AskAsync(ui, text, ct).</summary>
    public sealed class ConfirmWindow : UiWindow
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private UiButtonView _yes;
        [SerializeField] private UiButtonView _no;

        private readonly Choice<bool> _answer = new Choice<bool>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text text, UiButtonView yes, UiButtonView no)
        {
            _text = text;
            _yes = yes;
            _no = no;
        }
#endif

        private void Awake()
        {
            _yes.OnClick(() => _answer.Set(true));
            _no.OnClick(() => _answer.Set(false));
        }

        public void Setup(string text)
        {
            _text.text = text;
            _yes.SetText(T("ui.yes"));
            _no.SetText(T("ui.no"));
        }

        public UniTask<bool> WaitAnswerAsync(CancellationToken ct)
        {
            return _answer.WaitAsync(ct);
        }

        public static async UniTask<bool> AskAsync(IUiService ui, string text, CancellationToken ct)
        {
            var window = await ui.OpenAsync<ConfirmWindow>(ct, w => w.Setup(text));
            try
            {
                return await window.WaitAnswerAsync(ct);
            }
            finally
            {
                await ui.CloseAsync(window, CancellationToken.None);
            }
        }
    }
}
