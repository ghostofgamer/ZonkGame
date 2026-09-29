using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.UI.Transitions;

namespace Zonk.UI
{
    /// <summary>
    /// Экран интерфейса, построенный кодом (UiKit). Строится при открытии и удаляется при закрытии (Dispose).
    /// Появление и скрытие — стили из UiConfig (экран или попап). Экраны постепенно переходят в префабы UiWindow,
    /// логика выбора (RunAsync) при этом сохраняется.
    /// </summary>
    public abstract class UiScreen : IDisposable
    {
        private readonly CanvasGroup _group;
        private readonly UiTransitionTarget _target;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        protected UiScreen(UiKit kit, RectTransform parent)
        {
            Kit = kit;
            Root = kit.Rect(GetType().Name, parent);
            UiKit.Stretch(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _target = new UiTransitionTarget(Root, _group);
            _group.alpha = 0f;
            ShowNextFrameAsync(_lifetime.Token).Forget();
        }

        protected UiKit Kit { get; }
        protected RectTransform Root { get; }

        /// <summary>Попап (модальное окно поверх экрана): другие стили появления.</summary>
        protected virtual bool IsPopup => false;

        /// <summary>Токен, отменяемый при закрытии экрана: для фоновых обновлений (таймеры, анимации).</summary>
        protected CancellationToken LifetimeToken => _lifetime.Token;

        /// <summary>Закрыть: клики сразу отключаются, экран уходит своей анимацией и удаляется.</summary>
        public virtual void Dispose()
        {
            if (_lifetime.IsCancellationRequested)
                return;

            _lifetime.Cancel();
            if (Root == null)
                return;

            _group.interactable = false;
            _group.blocksRaycasts = false;
            HideAndDestroyAsync().Forget();
        }

        /// <summary>Стиль появления ждёт кадр: к этому моменту наследник достроил содержимое экрана.</summary>
        private async UniTaskVoid ShowNextFrameAsync(CancellationToken ct)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                return;

            var show = Kit.Config != null ? (IsPopup ? Kit.Config.PopupShow : Kit.Config.ScreenShow) : null;
            if (show == null)
            {
                _group.alpha = 1f;
                return;
            }

            await show.PlayAsync(_target, ct).SuppressCancellationThrow();
        }

        private async UniTaskVoid HideAndDestroyAsync()
        {
            var hide = Kit.Config != null ? (IsPopup ? Kit.Config.PopupHide : Kit.Config.ScreenHide) : null;
            var ct = Root.gameObject.GetCancellationTokenOnDestroy();
            if (hide != null)
                await hide.PlayAsync(_target, ct).SuppressCancellationThrow();

            if (Root != null)
                UnityEngine.Object.Destroy(Root.gameObject);
        }
    }

    /// <summary>Ожидание одного выбора из кнопок экрана.</summary>
    public sealed class Choice<T>
    {
        private UniTaskCompletionSource<T> _source = new UniTaskCompletionSource<T>();

        public void Set(T value)
        {
            _source.TrySetResult(value);
        }

        public async UniTask<T> WaitAsync(CancellationToken ct)
        {
            var result = await _source.Task.AttachExternalCancellation(ct);
            _source = new UniTaskCompletionSource<T>();
            return result;
        }
    }
}
