using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Quality;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zenject;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Свет и атмосфера сцены стола по профилю локации (LightingProfileConfig). Когда стол надевает предмет слота,
    /// который задаёт освещение (CosmeticSlotConfig.DrivesLighting, у локаций), директор плавно переводит солнце,
    /// окружающий свет, туман и фон на профиль локации, масштабирует светильники (SceneLight) и запускает эффекты.
    ///
    /// Производительность: работа только во время перехода (доли секунды) и вспышек молний; тени солнца
    /// и постобработка — по уровню качества; в остальное время директор ничего не считает.
    /// </summary>
    public sealed class LightingDirector : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Camera _camera;
        [SerializeField] private Volume _volume;
        [SerializeField] private CosmeticStage _stage;
        [SerializeField] private SoundPlayer _sound;

        private readonly List<SceneLight> _lights = new List<SceneLight>();
        private GameConfig _config;
        private IQualityService _quality;
        private LightingProfileConfig _profile;
        private CancellationTokenSource _transitionCts;
        private CancellationTokenSource _effectsCts;

        // Что выставлено сейчас (цель перехода): от этих значений считаются вспышки молний.
        private Color _sunColor;
        private float _sunIntensity;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;

#if UNITY_EDITOR
        public void EditorSetup(Light sun, Camera camera, Volume volume, CosmeticStage stage, SoundPlayer sound)
        {
            _sun = sun;
            _camera = camera;
            _volume = volume;
            _stage = stage;
            _sound = sound;
        }
#endif

        private bool _tierSubscribed;

        private ToonStyleService _toon;

        [Inject]
        public void Construct(GameConfig config, IQualityService quality, ToonStyleService toon)
        {
            _config = config;
            _quality = quality;
            _toon = toon;
            // Зависимости сцены могут прийти и до, и после OnEnable: подписка в обоих местах, но один раз.
            if (isActiveAndEnabled)
                SubscribeTier();
        }

        private void OnEnable()
        {
            if (_stage != null)
            {
                _stage.ItemApplied += OnItemApplied;
                _stage.InstancesChanged += OnInstancesChanged;
            }
            SubscribeTier();
        }

        private void SubscribeTier()
        {
            if (_quality == null || _tierSubscribed)
                return;

            _quality.TierChanged += OnTierChanged;
            _tierSubscribed = true;
        }

        private void OnDisable()
        {
            if (_stage != null)
            {
                _stage.ItemApplied -= OnItemApplied;
                _stage.InstancesChanged -= OnInstancesChanged;
            }
            if (_quality != null && _tierSubscribed)
                _quality.TierChanged -= OnTierChanged;
            _tierSubscribed = false;
            Cancel(ref _transitionCts);
            Cancel(ref _effectsCts);
        }

        /// <summary>Модели на столе догрузились или сменились: лампы и свет локации собираются заново.</summary>
        private void OnInstancesChanged()
        {
            _stage.CollectLights(_lights);
            ApplyLights();
        }

        private QualityTier Tier => _quality != null ? _quality.Tier : QualityTier.High;

        private void OnItemApplied(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null)
                return;

            // Светильники могли смениться с любым предметом (лампа, локация): собрать и выставить заново.
            _stage.CollectLights(_lights);
            if (!item.Slot.DrivesLighting)
            {
                ApplyLights();
                return;
            }

            var profile = item.Payload is PrefabPayload prefab && prefab.Lighting != null
                ? prefab.Lighting
                : _config != null ? _config.DefaultLighting : null;
            Apply(profile);
        }

        private void OnTierChanged()
        {
            var profile = _profile;
            _profile = null;
            Apply(profile);
        }

        /// <summary>Перейти на профиль (тот же — ничего не делать).</summary>
        public void Apply(LightingProfileConfig profile)
        {
            if (profile == null || profile == _profile)
            {
                ApplyLights();
                return;
            }

            var instant = _profile == null;
            _profile = profile;
            var tier = Tier;
            _toon?.SetLocationStyle(profile.ToonStyle);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = profile.Fog;
            RenderSettings.fogMode = FogMode.Exponential;

            if (_sun != null)
            {
                _sun.enabled = profile.Sun;
                _sun.shadows = profile.Sun && tier >= profile.SunShadowsFrom ? LightShadows.Soft : LightShadows.None;
            }

            ApplyPostProcess(profile, tier);
            ApplyLights();

            Cancel(ref _transitionCts);
            _transitionCts = new CancellationTokenSource();
            TransitionAsync(profile, instant ? 0f : profile.TransitionDuration, _transitionCts.Token).Forget();

            Cancel(ref _effectsCts);
            _effectsCts = new CancellationTokenSource();
            var context = new AtmosphereContext { Camera = _camera, Tier = tier, Sound = _sound, Flash = FlashAsync };
            foreach (var effect in profile.Effects)
            {
                if (effect != null)
                    effect.RunAsync(context, _effectsCts.Token).SuppressCancellationThrow().Forget();
            }
        }

        private void ApplyLights()
        {
            var multiplier = _profile != null ? _profile.LampIntensity : 1f;
            var tier = Tier;
            foreach (var sceneLight in _lights)
            {
                if (sceneLight != null)
                    sceneLight.Apply(multiplier, tier);
            }
        }

        /// <summary>Постобработка только с нужного уровня качества; иначе камера её не считает вовсе.</summary>
        private void ApplyPostProcess(LightingProfileConfig profile, QualityTier tier)
        {
            var on = profile.PostProcess != null && tier >= profile.PostProcessFrom;
            if (_volume != null)
            {
                _volume.sharedProfile = on ? profile.PostProcess : null;
                _volume.enabled = on;
            }

            if (_camera != null && _camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data.renderPostProcessing = on;
        }

        private async UniTaskVoid TransitionAsync(LightingProfileConfig profile, float duration, CancellationToken ct)
        {
            var fromSunColor = _sun != null ? _sun.color : profile.SunColor;
            var fromSunIntensity = _sun != null ? _sun.intensity : profile.SunIntensity;
            var fromSunRotation = _sun != null ? _sun.transform.rotation : Quaternion.Euler(profile.SunRotation);
            var fromSky = RenderSettings.ambientSkyColor;
            var fromEquator = RenderSettings.ambientEquatorColor;
            var fromGround = RenderSettings.ambientGroundColor;
            var fromFog = RenderSettings.fogColor;
            var fromDensity = RenderSettings.fogDensity;
            var fromBackground = _camera != null ? _camera.backgroundColor : profile.Background;

            _sunColor = profile.SunColor;
            _sunIntensity = profile.Sun ? profile.SunIntensity : 0f;
            _ambientSky = profile.AmbientSky;
            _ambientEquator = profile.AmbientEquator;
            _ambientGround = profile.AmbientGround;

            var time = 0f;
            while (true)
            {
                var t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
                if (_sun != null)
                {
                    _sun.color = Color.Lerp(fromSunColor, _sunColor, t);
                    _sun.intensity = Mathf.Lerp(fromSunIntensity, _sunIntensity, t);
                    _sun.transform.rotation = Quaternion.Slerp(fromSunRotation, Quaternion.Euler(profile.SunRotation), t);
                }

                RenderSettings.ambientSkyColor = Color.Lerp(fromSky, _ambientSky, t);
                RenderSettings.ambientEquatorColor = Color.Lerp(fromEquator, _ambientEquator, t);
                RenderSettings.ambientGroundColor = Color.Lerp(fromGround, _ambientGround, t);
                RenderSettings.fogColor = Color.Lerp(fromFog, profile.FogColor, t);
                RenderSettings.fogDensity = Mathf.Lerp(fromDensity, profile.FogDensity, t);
                if (_camera != null)
                    _camera.backgroundColor = Color.Lerp(fromBackground, profile.Background, t);

                if (t >= 1f || await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
                time += Time.unscaledDeltaTime;
            }
        }

        /// <summary>Вспышка молнии: свет сцены на мгновение ярче и холоднее, затем обратно к профилю.</summary>
        private async UniTask FlashAsync(Color color, float intensity, float duration, CancellationToken ct)
        {
            var time = 0f;
            while (time < duration)
            {
                // Резкий пик и спад: вспышка, а не плавное свечение.
                var k = 1f - time / duration;
                k *= k;
                if (_sun != null)
                {
                    _sun.enabled = true;
                    _sun.color = Color.Lerp(_sunColor, color, k);
                    _sun.intensity = _sunIntensity + intensity * k;
                }

                RenderSettings.ambientSkyColor = _ambientSky + color * (intensity * 0.35f * k);
                RenderSettings.ambientEquatorColor = _ambientEquator + color * (intensity * 0.25f * k);
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    break;
                time += Time.unscaledDeltaTime;
            }

            if (_sun != null)
            {
                _sun.color = _sunColor;
                _sun.intensity = _sunIntensity;
                _sun.enabled = _profile == null || _profile.Sun;
            }

            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
        }

        private static void Cancel(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;

            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
}
