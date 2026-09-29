using System.Collections.Generic;
using UnityEngine;

namespace Zonk.UI.Transitions
{
    /// <summary>
    /// Что анимирует переход: прямоугольник окна, его CanvasGroup и исходная поза.
    /// Поза запоминается один раз, чтобы повторные показы начинались с одного и того же места.
    /// Эффект может целиться в дочерний элемент окна (ForChild): его поза тоже запоминается один раз.
    /// </summary>
    public sealed class UiTransitionTarget
    {
        private readonly Dictionary<string, UiTransitionTarget> _children = new Dictionary<string, UiTransitionTarget>();

        public UiTransitionTarget(RectTransform rect, CanvasGroup group)
        {
            Rect = rect;
            Group = group;
            BasePosition = rect.anchoredPosition;
            BaseScale = rect.localScale;
            BaseRotation = rect.localEulerAngles;
        }

        public RectTransform Rect { get; }
        public CanvasGroup Group { get; }
        public Vector2 BasePosition { get; }
        public Vector3 BaseScale { get; }
        public Vector3 BaseRotation { get; }

        /// <summary>Размер области, в которой лежит элемент: от него считаются сдвиги «из-за края экрана».</summary>
        public Vector2 ParentSize
        {
            get
            {
                var parent = Rect.parent as RectTransform;
                return parent != null ? parent.rect.size : new Vector2(Screen.width, Screen.height);
            }
        }

        /// <summary>Дочерний элемент по пути (как в Transform.Find). null, если такого нет.</summary>
        public UiTransitionTarget ForChild(string path)
        {
            if (string.IsNullOrEmpty(path))
                return this;

            if (_children.TryGetValue(path, out var cached))
                return cached;

            var child = Rect.Find(path) as RectTransform;
            if (child == null)
                return null;

            if (!child.TryGetComponent<CanvasGroup>(out var group))
                group = child.gameObject.AddComponent<CanvasGroup>();

            var target = new UiTransitionTarget(child, group);
            _children[path] = target;
            return target;
        }

        /// <summary>Вернуть окно и затронутые дочерние элементы в исходную позу без анимации.</summary>
        public void Reset()
        {
            Rect.anchoredPosition = BasePosition;
            Rect.localScale = BaseScale;
            Rect.localEulerAngles = BaseRotation;
            if (Group != null)
                Group.alpha = 1f;

            foreach (var child in _children.Values)
                child.Reset();
        }
    }
}
