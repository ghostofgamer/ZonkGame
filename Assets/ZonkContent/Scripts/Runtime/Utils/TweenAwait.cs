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
            tween.OnKill(() => source.TrySetResult());

            using (ct.Register(() =>
                   {
                       if (tween.IsActive())
                           tween.Kill();
                   }))
            {
                await source.Task;
            }

            ct.ThrowIfCancellationRequested();
        }
    }
}
