using System;
using System.Threading;
using Base.Services.Quality;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Presentation;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Zonk.Configs
{
    /// <summary>Что эффекту атмосферы доступно в сцене. Заполняет LightingDirector.</summary>
    public sealed class AtmosphereContext
    {
        public Camera Camera;
        public QualityTier Tier;
        public SoundPlayer Sound;

        /// <summary>Вспышка света поверх профиля: цвет, сила, длительность (сек).</summary>
        public Func<Color, float, float, CancellationToken, UniTask> Flash;
    }

    /// <summary>
    /// Эффект атмосферы локации (LightingProfileConfig.Effects): работает, пока профиль активен, и убирает
    /// за собой при отмене. Новый эффект (снег, пыль, светлячки) = новый наследник, в профиле он появится в списке.
    /// Эффект сам ужимается по уровню качества: на телефоне меньше частиц.
    /// </summary>
    [Serializable]
    public abstract class AtmosphereEffect
    {
        public abstract UniTask RunAsync(AtmosphereContext context, CancellationToken ct);
    }

    /// <summary>
    /// Дождь: префаб системы частиц у камеры (капли всегда в кадре при любом ракурсе). Число капель — по уровню
    /// качества: одна система частиц без столкновений, это дёшево и для браузера.
    /// </summary>
    [Serializable]
    public sealed class RainEffect : AtmosphereEffect
    {
        [Tooltip("Префаб с ParticleSystem (капли летят вниз)")]
        public GameObject Prefab;

        [Tooltip("Сколько капель одновременно на уровнях Low / Medium / High")]
        public int[] MaxDrops = { 150, 350, 700 };

        public Vector3 CameraOffset = new Vector3(0f, 1.5f, 2.5f);

        public override async UniTask RunAsync(AtmosphereContext context, CancellationToken ct)
        {
            if (Prefab == null || context.Camera == null)
                return;

            var instance = Object.Instantiate(Prefab, context.Camera.transform, false);
            instance.transform.localPosition = CameraOffset;
            var drops = MaxDrops != null && MaxDrops.Length > 0 ? MaxDrops[Mathf.Clamp((int)context.Tier, 0, MaxDrops.Length - 1)] : 300;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>())
            {
                var main = system.main;
                var scale = main.maxParticles > 0 ? (float)drops / main.maxParticles : 1f;
                main.maxParticles = drops;
                var emission = system.emission;
                emission.rateOverTimeMultiplier *= scale;
            }

            try
            {
                await UniTask.WaitUntilCanceled(ct);
            }
            finally
            {
                if (instance != null)
                    Object.Destroy(instance);
            }
        }
    }

    /// <summary>
    /// Гроза: время от времени небо вспыхивает (свет сцены на мгновение ярче, иногда двойная вспышка), следом гром.
    /// Между вспышками ничего не считает.
    /// </summary>
    [Serializable]
    public sealed class LightningEffect : AtmosphereEffect
    {
        [Tooltip("Пауза между вспышками, секунды (от и до)")]
        public Vector2 Interval = new Vector2(7f, 16f);

        public Color FlashColor = new Color(0.8f, 0.85f, 1f);
        public float FlashIntensity = 2.5f;
        public float FlashDuration = 0.18f;

        [Tooltip("Доля вспышек с повтором (двойная молния)")]
        [Range(0f, 1f)] public float DoubleChance = 0.4f;

        [Tooltip("Пауза перед повторной вспышкой, секунды")]
        public float DoubleGap = 0.08f;

        [Tooltip("Сила и длительность повторной вспышки относительно первой")]
        [Range(0f, 1f)] public float DoubleStrength = 0.7f;

        [Tooltip("Задержка грома после вспышки, секунды (от и до)")]
        public Vector2 ThunderDelay = new Vector2(0.4f, 1.6f);

        public override async UniTask RunAsync(AtmosphereContext context, CancellationToken ct)
        {
            if (context.Flash == null)
                return;

            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(Interval.x, Interval.y)), cancellationToken: ct)
                        .SuppressCancellationThrow())
                    return;

                await context.Flash(FlashColor, FlashIntensity, FlashDuration, ct);
                if (Random.value < DoubleChance)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(DoubleGap), cancellationToken: ct).SuppressCancellationThrow())
                        return;
                    await context.Flash(FlashColor, FlashIntensity * DoubleStrength, FlashDuration * DoubleStrength, ct);
                }

                if (context.Sound == null)
                    continue;

                if (await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(ThunderDelay.x, ThunderDelay.y)), cancellationToken: ct)
                        .SuppressCancellationThrow())
                    return;
                context.Sound.Play(Sfx.Thunder, 0.8f);
            }
        }
    }
}
