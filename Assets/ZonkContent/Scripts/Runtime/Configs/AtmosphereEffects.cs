using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Наклон лампы слота (ID слота, наклон по X и Z в градусах, высота точки подвеса над якорем): лампа качается
        /// вокруг подвеса, прожектор над столом — вместе с ней. Ноль — на место.
        /// </summary>
        public Action<string, float, float, float> SwayLamp;

        /// <summary>Источники света в предметах на столе (лампа, локация); список живой — меняется со сменой предметов.</summary>
        public IReadOnlyList<SceneLight> Lights;

        /// <summary>Множитель яркости прожектора над столом (мерцание лампы). 1 — как в профиле.</summary>
        public Action<float> FlickerTableLight;
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

    /// <summary>
    /// Качание лампы над столом (сквозняк, качка корабля): лампа медленно ходит вокруг точки подвеса, а с ней свет
    /// и прожектор — ступени света гуляют по столу. Две синусоиды с несоизмеримыми периодами по двум осям: движение
    /// не повторяется явно. Слабо (1–2°), чтобы кости на столе читались; на корабле сильнее.
    /// Каждый кадр — только математика и поворот одного объекта, без выделения памяти.
    /// </summary>
    [Serializable]
    public sealed class LampSwayEffect : AtmosphereEffect
    {
        [Tooltip("ID слота лампы (CosmeticSlotConfig.Id)")]
        public string LampSlot = "lamp";

        [Tooltip("Наибольший наклон, градусы: по X (к игроку и от него) и по Z (влево-вправо)")]
        public Vector2 Amplitude = new Vector2(1.5f, 1f);

        [Tooltip("Период качания, секунды. Вторая ось и вторая волна — с несоизмеримыми периодами, чтобы движение не повторялось")]
        [Min(0.5f)] public float Period = 5f;

        [Tooltip("Доля второй, быстрой волны (дрожь поверх плавного качания): 0 — только плавно")]
        [Range(0f, 1f)] public float Jitter = 0.25f;

        [Tooltip("Высота точки подвеса над якорем лампы, м: чем выше, тем больше лампа сдвигается вбок")]
        [Min(0f)] public float PivotAbove = 1.2f;

        public override async UniTask RunAsync(AtmosphereContext context, CancellationToken ct)
        {
            if (context.SwayLamp == null)
                return;

            var phase = Random.value * 100f;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var t = Time.time + phase;
                    var w = 2f * Mathf.PI / Mathf.Max(Period, 0.5f);
                    var x = Mathf.Sin(t * w) * (1f - Jitter) + Mathf.Sin(t * w * 2.71f + 1.3f) * Jitter;
                    var z = Mathf.Sin(t * w * 0.77f + 2.1f) * (1f - Jitter) + Mathf.Sin(t * w * 3.13f) * Jitter;
                    context.SwayLamp(LampSlot, x * Amplitude.x, z * Amplitude.y, PivotAbove);
                    await UniTask.Yield();
                }
            }
            finally
            {
                context.SwayLamp(LampSlot, 0f, 0f, PivotAbove);
            }
        }
    }

    /// <summary>
    /// Мерцание огня: яркость ламп и свечей (SceneLight) и прожектора над столом слегка гуляет — живое пламя
    /// или дрожащая лампочка. Шум Перлина, у каждого источника своя фаза; насколько дрожит источник — его
    /// SceneLight.FlickerWeight (электрическая лампа может не мерцать). Без выделения памяти.
    /// </summary>
    [Serializable]
    public sealed class LightFlickerEffect : AtmosphereEffect
    {
        [Tooltip("Насколько гуляет яркость, доля: 0.08 — ±8%")]
        [Range(0f, 0.5f)] public float Amount = 0.08f;

        [Tooltip("Скорость мерцания: 1 — спокойное масляное пламя, 3 — трепещущая свеча")]
        [Min(0.05f)] public float Speed = 1.5f;

        [Tooltip("Мерцает ли прожектор над столом (свет лампы на столе) вместе с лампами")]
        public bool TableLight = true;

        public override async UniTask RunAsync(AtmosphereContext context, CancellationToken ct)
        {
            var seed = Random.value * 100f;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var t = Time.time * Speed;
                    var lights = context.Lights;
                    if (lights != null)
                    {
                        for (var i = 0; i < lights.Count; i++)
                        {
                            if (lights[i] != null)
                                lights[i].SetFlicker(1f + Amount * Noise(t, seed + i * 7.31f));
                        }
                    }

                    if (TableLight && context.FlickerTableLight != null)
                        context.FlickerTableLight(1f + Amount * Noise(t, seed));
                    await UniTask.Yield();
                }
            }
            finally
            {
                var lights = context.Lights;
                if (lights != null)
                {
                    for (var i = 0; i < lights.Count; i++)
                    {
                        if (lights[i] != null)
                            lights[i].SetFlicker(1f);
                    }
                }

                context.FlickerTableLight?.Invoke(1f);
            }
        }

        /// <summary>Шум −1..1: плавная волна и быстрая дрожь поверх.</summary>
        private static float Noise(float t, float seed)
        {
            var slow = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            var fast = Mathf.PerlinNoise(t * 4.3f, seed + 31.7f) * 2f - 1f;
            return slow * 0.7f + fast * 0.3f;
        }
    }
}
