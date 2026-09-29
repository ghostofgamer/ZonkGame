using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zonk.Utils
{
    /// <summary>
    /// Функции сглаживания для анимаций: t от 0 до 1. Это готовые делегаты (поля), а не методы: передача метода
    /// как Func создавала бы новый объект на каждый вызов анимации.
    /// </summary>
    public static class AnimateEase
    {
        public static readonly Func<float, float> Linear = t => t;
        public static readonly Func<float, float> OutCubic = t => 1f - (1f - t) * (1f - t) * (1f - t);
        public static readonly Func<float, float> InCubic = t => t * t * t;

        public static readonly Func<float, float> InOutCubic = t =>
        {
            if (t < 0.5f)
                return 4f * t * t * t;
            var u = -2f * t + 2f;
            return 1f - u * u * u / 2f;
        };

        public static readonly Func<float, float> OutBack = t =>
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        };
    }

    /// <summary>
    /// Простые анимации на UniTask без корутин. Отмена через CancellationToken оставляет объект там, где его застала
    /// отмена. Готовые анимации (Move, Rotate, Scale, Pose, Jump) — собственные циклы без замыканий: на телефоне
    /// и в браузере они не оставляют мусора для сборщика. RunAsync с делегатом — для редких особых случаев.
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

        private enum Channel
        {
            Position,
            LocalPosition,
            Rotation,
            LocalRotation,
            Scale,
            Pose,
            Jump,
            JumpPose,
        }

        public static UniTask MoveAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            return PlayAsync(Channel.Position, target, target.position, to, default, default, 0f, duration, ease, ct);
        }

        public static UniTask MoveLocalAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            return PlayAsync(Channel.LocalPosition, target, target.localPosition, to, default, default, 0f, duration, ease, ct);
        }

        public static UniTask RotateAsync(Transform target, Quaternion to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            return PlayAsync(Channel.Rotation, target, default, default, target.rotation, to, 0f, duration, ease, ct);
        }

        public static UniTask RotateLocalAsync(Transform target, Quaternion to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            return PlayAsync(Channel.LocalRotation, target, default, default, target.localRotation, to, 0f, duration, ease, ct);
        }

        public static UniTask ScaleAsync(Transform target, Vector3 to, float duration, CancellationToken ct,
            Func<float, float> ease = null)
        {
            return PlayAsync(Channel.Scale, target, target.localScale, to, default, default, 0f, duration, ease, ct);
        }

        /// <summary>Перемещение и поворот одновременно, в мировых координатах.</summary>
        public static UniTask PoseAsync(Transform target, Vector3 position, Quaternion rotation, float duration,
            CancellationToken ct, Func<float, float> ease = null)
        {
            return PlayAsync(Channel.Pose, target, target.position, position, target.rotation, rotation, 0f, duration, ease, ct);
        }

        /// <summary>Перекат по дуге с поворотом: кость перепрыгивает на новое место и ложится на грань.</summary>
        public static UniTask JumpPoseAsync(Transform target, Vector3 to, Quaternion rotation, float height, float duration,
            CancellationToken ct)
        {
            return PlayAsync(Channel.JumpPose, target, target.position, to, target.rotation, rotation, height, duration,
                AnimateEase.InOutCubic, ct);
        }

        /// <summary>Прыжок по дуге: для костей, которые откладываются в сторону.</summary>
        public static UniTask JumpAsync(Transform target, Vector3 to, float height, float duration, CancellationToken ct)
        {
            return PlayAsync(Channel.Jump, target, target.position, to, default, default, height, duration,
                AnimateEase.InOutCubic, ct);
        }

        /// <summary>Общий цикл всех готовых анимаций: все данные в параметрах, поэтому без замыканий.</summary>
        private static async UniTask PlayAsync(Channel channel, Transform target, Vector3 fromVector, Vector3 toVector,
            Quaternion fromRotation, Quaternion toRotation, float height, float duration, Func<float, float> ease,
            CancellationToken ct)
        {
            ease = ease ?? AnimateEase.OutCubic;
            var time = 0f;
            while (duration > 0f && time < duration)
            {
                Apply(channel, target, fromVector, toVector, fromRotation, toRotation, height, ease(time / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                time += Time.deltaTime;
            }

            Apply(channel, target, fromVector, toVector, fromRotation, toRotation, height, 1f);
        }

        private static void Apply(Channel channel, Transform target, Vector3 fromVector, Vector3 toVector,
            Quaternion fromRotation, Quaternion toRotation, float height, float t)
        {
            if (target == null)
                return;

            switch (channel)
            {
                case Channel.Position:
                    target.position = Vector3.LerpUnclamped(fromVector, toVector, t);
                    break;
                case Channel.LocalPosition:
                    target.localPosition = Vector3.LerpUnclamped(fromVector, toVector, t);
                    break;
                case Channel.Rotation:
                    target.rotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, t);
                    break;
                case Channel.LocalRotation:
                    target.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, t);
                    break;
                case Channel.Scale:
                    target.localScale = Vector3.LerpUnclamped(fromVector, toVector, t);
                    break;
                case Channel.Pose:
                    target.SetPositionAndRotation(Vector3.LerpUnclamped(fromVector, toVector, t),
                        Quaternion.SlerpUnclamped(fromRotation, toRotation, t));
                    break;
                case Channel.Jump:
                {
                    var position = Vector3.Lerp(fromVector, toVector, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * height;
                    target.position = position;
                    break;
                }
                case Channel.JumpPose:
                {
                    var position = Vector3.Lerp(fromVector, toVector, t);
                    position.y += Mathf.Sin(t * Mathf.PI) * height;
                    target.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, toRotation, t));
                    break;
                }
            }
        }
    }
}
