using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using Zonk.UI.Transitions;

namespace Zonk.UI
{
    /// <summary>
    /// Окно интерфейса как префаб. Появление и скрытие задаются ассетами UiTransitionConfig (поля в префабе),
    /// без них берутся стили по умолчанию из UiConfig. Окно открывает IUiService: создаёт из префаба при открытии
    /// и удаляет при закрытии, поэтому закрытые окна не занимают память.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UiWindow : MonoBehaviour
    {
        [SerializeField] private UiTransitionConfig _show;
        [SerializeField] private UiTransitionConfig _hide;

        [Tooltip("Модальное окно: использует переходы попапа по умолчанию")]
        [SerializeField] private bool _popup;

        private CanvasGroup _group;
        private UiTransitionTarget _target;
        private UniTaskCompletionSource _closeRequest = new UniTaskCompletionSource();

        /// <summary>Тексты окна: ключи из Texts.csv.</summary>
        protected ILocalization Localization { get; private set; }

        [Inject]
        public void InjectLocalization(ILocalization localization)
        {
            Localization = localization;
        }

        protected string T(string key)
        {
            return Localization != null ? Localization.Get(key) : key;
        }

        protected string T(string key, params object[] args)
        {
            return UiFormat.Format(Localization, key, args);
        }

        protected CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private UiTransitionTarget Target => _target ?? (_target = new UiTransitionTarget((RectTransform)transform, Group));

#if UNITY_EDITOR
        public void EditorSetupTransitions(UiTransitionConfig show, UiTransitionConfig hide, bool popup)
        {
            _show = show;
            _hide = hide;
            _popup = popup;
        }
#endif

        /// <summary>Стили по умолчанию для окна, у которого в префабе переходы не заданы.</summary>
        public void ApplyDefaults(UiConfig config)
        {
            if (config == null)
                return;

            if (_show == null)
                _show = _popup ? config.PopupShow : config.ScreenShow;
            if (_hide == null)
                _hide = _popup ? config.PopupHide : config.ScreenHide;
        }

        public async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            _closeRequest = new UniTaskCompletionSource();
            Group.blocksRaycasts = true;
            Group.interactable = false;
            OnShowing();

            if (_show != null)
                await _show.PlayAsync(Target, ct);
            else
                Target.Reset();

            Group.interactable = true;
            OnShown();
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            Group.interactable = false;
            Group.blocksRaycasts = false;
            OnHiding();

            if (_hide != null)
                await _hide.PlayAsync(Target, ct);

            gameObject.SetActive(false);
            Target.Reset();
        }

        /// <summary>Окно просит закрыть себя (кнопка «Закрыть»). Закрывает тот, кто открыл, через IUiService.</summary>
        public UniTask WaitCloseRequestAsync(CancellationToken ct)
        {
            return _closeRequest.Task.AttachExternalCancellation(ct);
        }

        protected void RequestClose()
        {
            _closeRequest.TrySetResult();
        }

        /// <summary>Вызов перед анимацией показа: заполнить тексты, подписаться.</summary>
        protected virtual void OnShowing()
        {
        }

        protected virtual void OnShown()
        {
        }

        /// <summary>Вызов перед анимацией скрытия: отписаться.</summary>
        protected virtual void OnHiding()
        {
        }
    }
}
