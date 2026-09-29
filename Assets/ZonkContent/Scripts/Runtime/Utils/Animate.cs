using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zonk.Utils
{
    /// <summary>Функции сглаживания для Tween: t от 0 до 1.</summary>
    public static class AnimateEase
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    /// <summary>
    /// Простые анимации на UniTask без сторонних пакетов и без корутин.
    /// Отмена через CancellationToken оставляет объект там, где его застала отмена.
    /// </summary>
    public static class Animate
    {
        /// <summary>Вызывает step(t) каждый кадр, t от 0 до 1 после сглаживания. Последний вызов всегда с t = 1.</summary>
        public static async UniTask RunAsync(float duration, Action<float> step, CancellationToken ct,
            Func<float, float> ease = null)
        {
            ease = ease ?? AnimateEase.OutCubic;

            if (duration <= 0f)
            {
                step(1f);
                return;
            }

            var time = 0f;
            while (time < duration)
            {
                step(ease(time / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                time += Time.deltaTime;
            }

            step(1f);
        }

        public static UniTask MoveAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            var from = target.position;
            return RunAsync(duration, t => target.position = Vector3.LerpUnclamped(from, to, t), ct, ease);
        }

        public static UniTask MoveLocalAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            var from = target.localPosition;
            return RunAsync(duration, t => target.localPosition = Vector3.LerpUnclamped(from, to, t), ct, ease);
        }

        public static UniTask RotateAsync(Transform target, Quaternion to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            var from = target.rotation;
            return RunAsync(duration, t => target.rotation = Quaternion.SlerpUnclamped(from, to, t), ct, ease);
        }

        public static UniTask RotateLocalAsync(Transform target, Quaternion to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            var from = target.localRotation;
            return RunAsync(duration, t => target.localRotation = Quaternion.SlerpUnclamped(from, to, t), ct, ease);
        }

        public static UniTask ScaleAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            var from = target.localScale;
            return RunAsync(duration, t => target.localScale = Vector3.LerpUnclamped(from, to, t), ct, ease);
        }

        /// <summary>Перемещение и поворот одновременно, в мировых координатах.</summary>
        public static UniTask PoseAsync(Transform target, Vector3 position, Quaternion rotation, float duration,
            CancellationToken ct, Func<float, float> ease = null)
        {
            var fromPosition = target.position;
            var fromRotation = target.rotation;
            return RunAsync(duration, t =>
            {
                target.position = Vector3.LerpUnclamped(fromPosition, position, t);
                target.rotation = Quaternion.SlerpUnclamped(fromRotation, rotation, t);
            }, ct, ease);
        }

        /// <summary>Перекат по дуге с поворотом: кость перепрыгивает на новое место и ложится на грань.</summary>
        public static UniTask JumpPoseAsync(Transform target, Vector3 to, Quaternion rotation, float height, float duration,
            CancellationToken ct)
        {
            var from = target.position;
            var fromRotation = target.rotation;
            return RunAsync(duration, t =>
            {
                var position = Vector3.Lerp(from, to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * height;
                target.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, rotation, t));
            }, ct, AnimateEase.InOutCubic);
        }

        /// <summary>Прыжок по дуге: для костей, которые откладываются в сторону.</summary>
        public static UniTask JumpAsync(Transform target, Vector3 to, float height, float duration, CancellationToken ct)
        {
            var from = target.position;
            return RunAsync(duration, t =>
            {
                var position = Vector3.Lerp(from, to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * height;
                target.position = position;
            }, ct, AnimateEase.InOutCubic);
        }
    }
}
