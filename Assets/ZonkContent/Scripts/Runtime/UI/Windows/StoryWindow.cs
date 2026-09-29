using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>Сюжетные карточки: портрет (если есть), говорящий и текст, «Дальше» и «Пропустить».</summary>
    public sealed class StoryWindow : UiWindow
    {
        [SerializeField] private TMP_Text _speaker;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Image _portrait;
        [SerializeField] private UiButtonView _skip;
        [SerializeField] private UiButtonView _next;

        private readonly Choice<bool> _nextPressed = new Choice<bool>();
        private bool _skipped;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text speaker, TMP_Text text, Image portrait, UiButtonView skip, UiButtonView next)
        {
            _speaker = speaker;
            _text = text;
            _portrait = portrait;
            _skip = skip;
            _next = next;
        }
#endif

        private void Awake()
        {
            _skip.OnClick(() =>
            {
                _skipped = true;
                _nextPressed.Set(true);
            });
            _next.OnClick(() => _nextPressed.Set(true));
        }

        protected override void OnShowing()
        {
            _skip.SetText(T("story.skip"));
            _next.SetText(T("story.next"));
        }

        /// <summary>Открыть окно, показать сюжет и закрыть.</summary>
        public static async UniTask ShowAsync(IUiService ui, IReadOnlyList<StoryLine> lines, CancellationToken ct)
        {
            var window = await ui.OpenAsync<StoryWindow>(ct);
            try
            {
                await window.PlayAsync(lines, ct);
            }
            finally
            {
                await ui.CloseAsync(window, CancellationToken.None);
            }
        }

        public async UniTask PlayAsync(IReadOnlyList<StoryLine> lines, CancellationToken ct)
        {
            foreach (var line in lines)
            {
                _speaker.text = T(line.SpeakerKey);
                _text.text = T(line.TextKey);
                if (_portrait != null)
                {
                    _portrait.sprite = line.Portrait;
                    _portrait.gameObject.SetActive(line.Portrait != null);
                }

                await _nextPressed.WaitAsync(ct);
                if (_skipped)
                    return;
            }
        }
    }
}
