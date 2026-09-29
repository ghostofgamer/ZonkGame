using TMPro;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.Utils;

namespace Zonk.UI
{
    /// <summary>Крупная надпись по центру: «ЗОНК!», «Горячие кости!», «+350».</summary>
    public static class Toast
    {
        public static async UniTask ShowAsync(UiKit kit, Transform parent, string text, Color color, float duration,
            CancellationToken ct)
        {
            var label = kit.Label(parent, text, 84, TextAnchor.MiddleCenter, color, true);
            UiKit.Outline(label, new Color(0f, 0f, 0f, 0.8f), 0.25f);
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1200, 160));

            try
            {
                var rect = label.rectTransform;
                rect.localScale = Vector3.one * 0.4f;
                await Animate.ScaleAsync(rect, Vector3.one, 0.25f, ct, AnimateEase.OutBack);
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
                await Animate.RunAsync(0.25f, t => label.color = new Color(color.r, color.g, color.b, 1f - t), ct);
            }
            finally
            {
                if (label != null)
                    UnityEngine.Object.Destroy(label.gameObject);
            }
        }
    }

    /// <summary>Облачко реплики над местом игрока. Следит за точкой в мире, пока показано.</summary>
    public static class SpeechBubble
    {
        public static async UniTask ShowAsync(UiKit kit, RectTransform canvasRoot, Camera camera, Transform anchor,
            string text, float duration, CancellationToken ct)
        {
            var panel = kit.Panel("Bubble", canvasRoot, new Color(1f, 0.97f, 0.9f, 0.95f));
            var rect = panel.rectTransform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(520, 110);
            var label = kit.Label(panel.transform, text, 28, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f));
            UiKit.Stretch(label.rectTransform, 16, 16, 8, 8);

            try
            {
                var time = 0f;
                while (time < duration)
                {
                    Follow(canvasRoot, camera, anchor, rect);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    time += Time.deltaTime;
                }
            }
            finally
            {
                if (panel != null)
                    UnityEngine.Object.Destroy(panel.gameObject);
            }
        }

        private static void Follow(RectTransform canvasRoot, Camera camera, Transform anchor, RectTransform rect)
        {
            if (anchor == null || camera == null)
                return;

            var screen = camera.WorldToScreenPoint(anchor.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, null, out var local))
                rect.anchoredPosition = local + canvasRoot.rect.size * 0.5f;
        }
    }
}
