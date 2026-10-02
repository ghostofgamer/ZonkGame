using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace Zonk.Utils
{
    /// <summary>
    /// Ожидание твина DOTween через UniTask. Отмена токена убивает твин, ожидание завершается отменой.
    /// Подписка идёт через OnKill: он срабатывает и при обычном завершении (autoKill), и при Kill.
    /// </summary>
    public static class TweenAwait
    {
        public static async UniTask AwaitAsync(this Tween tween, CancellationToken ct)
        {
            if (tween == null || !tween.IsActive())
                return;

            var source = new UniTaskCompletionSource();
            // Метод-расширение вместо лямбды: один делегат без лишнего объекта замыкания.
            tween.OnKill(source.SetDone);

            using (ct.Register(KillTween, tween))
            {
                await source.Task;
            }

            ct.ThrowIfCancellationRequested();
        }

        /// <summary>Готовый делегат отмены: твин приходит состоянием, без замыкания на каждое ожидание.</summary>
        private static readonly Action<object> KillTween = state =>
        {
            var tween = (Tween)state;
            if (tween.IsActive())
                tween.Kill();
        };

        private static void SetDone(this UniTaskCompletionSource source)
        {
            source.TrySetResult();
        }
    }
}
