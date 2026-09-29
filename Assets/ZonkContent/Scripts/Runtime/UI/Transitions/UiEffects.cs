using System;
using DG.Tweening;
using UnityEngine;

namespace Zonk.UI.Transitions
{
    public enum UiEdge
    {
        Left,
        Right,
        Top,
        Bottom,
    }

    /// <summary>
    /// Один эффект перехода окна. Эффекты одного UiTransitionConfig играют одновременно, у каждого можно
    /// сдвинуть начало (Delay), растянуть длительность (DurationScale) и задать свою кривую.
    /// Новый эффект = новый наследник, он появится в списке конфига перехода.
    /// Твины собираются из ядра DOTween (DOTween.To и расширения Transform): модули DOTween из asmdef не видны.
    /// </summary>
    [Serializable]
    public abstract class UiEffect
    {
        [Tooltip("Путь к дочернему элементу окна (как в Transform.Find). Пусто = само окно")]
        public string TargetPath;

        [Tooltip("Задержка от начала перехода, секунды")]
        public float Delay;

        [Tooltip("Множитель общей длительности перехода")]
        public float DurationScale = 1f;

        [Tooltip("Своя кривая вместо кривой перехода")]
        public bool OverrideEase;

        public Ease Ease = Ease.OutCubic;

        public void AddTo(Sequence sequence, UiTransitionTarget target, float duration, Ease ease)
        {
            target = target.ForChild(TargetPath);
            if (target == null)
                return;

            var tween = Create(target, Mathf.Max(0.01f, duration * DurationScale));
            if (tween == null)
                return;

            tween.SetEase(OverrideEase ? Ease : ease);
            sequence.Insert(Mathf.Max(0f, Delay), tween);
        }

        /// <summary>Выставить начальное значение и вернуть твин к конечному.</summary>
        protected abstract Tween Create(UiTransitionTarget target, float duration);
    }

    /// <summary>Прозрачность окна (CanvasGroup).</summary>
    [Serializable]
    public sealed class FadeEffect : UiEffect
    {
        [Range(0f, 1f)] public float From;
        [Range(0f, 1f)] public float To = 1f;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var group = target.Group;
            if (group == null)
                return null;

            group.alpha = From;
            return DOTween.To(() => group.alpha, value => group.alpha = value, To, duration);
        }
    }

    /// <summary>Прилёт из-за края или улёт за край. Distance — доля размера экрана (1 = целиком).</summary>
    [Serializable]
    public sealed class SlideEffect : UiEffect
    {
        public UiEdge Edge = UiEdge.Bottom;

        [Range(0f, 1.5f)] public float Distance = 1f;

        [Tooltip("Выключено: прилёт с края на место. Включено: улёт с места за край")]
        public bool Outgoing;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var size = target.ParentSize;
            Vector2 direction;
            switch (Edge)
            {
                case UiEdge.Left: direction = new Vector2(-size.x, 0f); break;
                case UiEdge.Right: direction = new Vector2(size.x, 0f); break;
                case UiEdge.Top: direction = new Vector2(0f, size.y); break;
                default: direction = new Vector2(0f, -size.y); break;
            }

            var away = target.BasePosition + direction * Distance;
            var rect = target.Rect;
            var from = Outgoing ? target.BasePosition : away;
            var to = Outgoing ? away : target.BasePosition;
            rect.anchoredPosition = from;
            return DOTween.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value, to, duration);
        }
    }

    /// <summary>Сдвиг на заданное число пикселей (небольшой «подъезд» к месту).</summary>
    [Serializable]
    public sealed class MoveEffect : UiEffect
    {
        public Vector2 Offset = new Vector2(0f, -60f);
        public bool Outgoing;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var rect = target.Rect;
            var from = Outgoing ? target.BasePosition : target.BasePosition + Offset;
            var to = Outgoing ? target.BasePosition + Offset : target.BasePosition;
            rect.anchoredPosition = from;
            return DOTween.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value, to, duration);
        }
    }

    /// <summary>Масштаб относительно исходного: 0.8 → 1 «выпрыгивание», 1 → 0 «схлопывание».</summary>
    [Serializable]
    public sealed class ScaleEffect : UiEffect
    {
        public Vector3 From = new Vector3(0.8f, 0.8f, 1f);
        public Vector3 To = Vector3.one;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var rect = target.Rect;
            rect.localScale = Vector3.Scale(target.BaseScale, From);
            return rect.DOScale(Vector3.Scale(target.BaseScale, To), duration);
        }
    }

    /// <summary>Поворот относительно исходного, градусы. Поворот по X даёт «переворот карточки».</summary>
    [Serializable]
    public sealed class RotateEffect : UiEffect
    {
        public Vector3 From = new Vector3(90f, 0f, 0f);
        public Vector3 To = Vector3.zero;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var rect = target.Rect;
            rect.localEulerAngles = target.BaseRotation + From;
            return rect.DOLocalRotate(target.BaseRotation + To, duration);
        }
    }

    /// <summary>Толчок масштаба: окно «вздрагивает». Для акцента, а не для появления.</summary>
    [Serializable]
    public sealed class PunchScaleEffect : UiEffect
    {
        public Vector3 Punch = new Vector3(0.1f, 0.1f, 0f);
        public int Vibrato = 8;

        [Range(0f, 1f)] public float Elasticity = 0.8f;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            target.Rect.localScale = target.BaseScale;
            return target.Rect.DOPunchScale(Punch, duration, Vibrato, Elasticity);
        }
    }

    /// <summary>Тряска по экрану в пикселях: ошибка, «не хватает монет».</summary>
    [Serializable]
    public sealed class ShakeEffect : UiEffect
    {
        public float Strength = 20f;
        public int Vibrato = 20;

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var rect = target.Rect;
            rect.anchoredPosition = target.BasePosition;
            var basePosition = (Vector3)target.BasePosition;
            var offset = Vector3.zero;
            return DOTween.Shake(() => offset, value =>
            {
                offset = value;
                rect.anchoredPosition = basePosition + value;
            }, duration, Strength, Vibrato, 90f, true, true);
        }
    }

    /// <summary>
    /// Дочерние элементы окна появляются по очереди: прозрачность и масштаб каждого с шагом Step.
    /// Для меню и списков. Позиции детей не трогает: они могут стоять в LayoutGroup.
    /// </summary>
    [Serializable]
    public sealed class StaggerChildrenEffect : UiEffect
    {
        [Tooltip("Путь к контейнеру внутри окна (пусто = само окно)")]
        public string ContainerPath;

        public float Step = 0.05f;
        public Vector3 ScaleFrom = new Vector3(0.9f, 0.9f, 1f);

        protected override Tween Create(UiTransitionTarget target, float duration)
        {
            var container = string.IsNullOrEmpty(ContainerPath) ? target.Rect : target.Rect.Find(ContainerPath);
            if (container == null)
                return null;

            var sequence = DOTween.Sequence();
            var index = 0;
            foreach (Transform child in container)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                if (!child.TryGetComponent<CanvasGroup>(out var group))
                    group = child.gameObject.AddComponent<CanvasGroup>();

                var baseScale = child.localScale;
                group.alpha = 0f;
                child.localScale = Vector3.Scale(baseScale, ScaleFrom);

                var at = index++ * Step;
                sequence.Insert(at, DOTween.To(() => group.alpha, value => group.alpha = value, 1f, duration));
                sequence.Insert(at, child.DOScale(baseScale, duration));
            }

            return sequence;
        }
    }
}
