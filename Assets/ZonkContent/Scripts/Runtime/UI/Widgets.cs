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
        private const float FadeDuration = 0.25f;

        private static async UniTask ShowPlateAsync(UiKit kit, Zonk.UI.Views.UiLabelPlate plate, string text, Color color, float duration,
            CancellationToken ct)
        {
            plate.Label.text = text;
            plate.Label.color = color;
            plate.Group.alpha = 1f;
            var rect = plate.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.62f);
            rect.anchoredPosition = Vector2.zero;

            try
            {
                rect.localScale = Vector3.one * 0.4f;
                await Animate.ScaleAsync(rect, Vector3.one, 0.25f, ct, AnimateEase.OutBack);
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);

                var time = 0f;
                while (time < FadeDuration)
                {
                    plate.Group.alpha = 1f - time / FadeDuration;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    time += Time.deltaTime;
                }
            }
            finally
            {
                if (plate != null)
                    kit.ReturnToastPlate(plate);
            }
        }

        public static async UniTask ShowAsync(UiKit kit, Transform parent, string text, Color color, float duration,
            CancellationToken ct)
        {
            // Надпись на подложке: читается на любом фоне. Деталь из пула, не создаётся на каждое сообщение.
            var plate = kit.RentToastPlate(parent);
            if (plate != null)
            {
                await ShowPlateAsync(kit, plate, text, color, duration, ct);
                return;
            }

            // Без детали в UiConfig — старая надпись из пула.
            var label = kit.RentToast(parent);
            label.text = text;
            label.color = color;
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1200, 160));

            try
            {
                var rect = label.rectTransform;
                rect.localScale = Vector3.one * 0.4f;
                await Animate.ScaleAsync(rect, Vector3.one, 0.25f, ct, AnimateEase.OutBack);
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);

                var time = 0f;
                while (time < FadeDuration)
                {
                    label.color = new Color(color.r, color.g, color.b, 1f - time / FadeDuration);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    time += Time.deltaTime;
                }
            }
            finally
            {
                // Окно могли закрыть посреди показа: вместе с ним уничтожена и надпись, в пул её не вернуть.
                if (label != null)
                    kit.ReturnToast(label);
            }
        }
    }

    /// <summary>Облачко реплики над местом игрока. Следит за точкой в мире, пока показано.</summary>
    public static class SpeechBubble
    {
        public static async UniTask ShowAsync(UiKit kit, RectTransform canvasRoot, Camera camera, Transform anchor,
            string text, float duration, CancellationToken ct)
        {
            var panel = kit.RentBubble(canvasRoot, out var label);
            var rect = panel.rectTransform;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(520, 110);
            label.text = text;

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
                    kit.ReturnBubble(panel);
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
