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

        [Tooltip("Прожектор над столом (профиль локации: TableLight). Пусто — без пятна")]
        [SerializeField] private Light _tableLight;

        private readonly List<SceneLight> _lights = new List<SceneLight>();
        private GameConfig _config;
        private IQualityService _quality;
        private LightingProfileConfig _profile;
        private CancellationTokenSource _transitionCts;
        private CancellationTokenSource _effectsCts;

        // Качание лампы (LampSwayEffect) и мерцание прожектора (LightFlickerEffect): исходные места запоминаются
        // при первом обращении, наклон и множитель — поверх них.
        private Transform _lamp;
        private string _lampSlot;
        private Vector3 _lampBasePosition;
        private Quaternion _lampBaseRotation;
        private Vector2 _tableLightBase;
        private bool _tableLightBaseSet;
        private float _tiltX;
        private float _tiltZ;
        private float _pivotAbove;
        private float _tableFlicker = 1f;

        // Что выставлено сейчас (цель перехода): от этих значений считаются вспышки молний.
        private Color _sunColor;
        private float _sunIntensity;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;

        // Затемнение запечённых комнат (Zonk/Baked): глобально, поэтому работает и для модели, которая догрузится позже.
        // Хранится как «сколько убрать»: пока значение не задано (снимок иконки, редактор), комната как запекли.
        private static readonly int BakedDarkenId = Shader.PropertyToID("_ZonkBakedDarken");
        private Color _bakedDarken;

#if UNITY_EDITOR
        public void EditorSetup(Light sun, Camera camera, Volume volume, CosmeticStage stage, SoundPlayer sound, Light tableLight = null)
        {
            _tableLight = tableLight;
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
            var context = new AtmosphereContext
            {
                Camera = _camera, Tier = tier, Sound = _sound, Flash = FlashAsync,
                SwayLamp = SwayLamp, Lights = _lights, FlickerTableLight = FlickerTableLight,
            };
            foreach (var effect in profile.Effects)
            {
                if (effect != null)
                    effect.RunAsync(context, _effectsCts.Token).SuppressCancellationThrow().Forget();
            }
        }

        /// <summary>Пятно света на стол по профилю: в тёмных локациях стол освещён из темноты. Без теней — дёшево.</summary>
        private void ApplyTableLight()
        {
            if (_tableLight == null)
                return;

            var power = _profile != null ? _profile.TableLight : 0f;
            var on = power > 0.001f;
            if (_tableLight.enabled != on)
                _tableLight.enabled = on;
            if (!on)
                return;

            // Геометрия от освещаемого круга: прожектор над центром стола на высоте профиля, полный свет до радиуса,
            // дальше мягкое затухание. Свет слабеет с квадратом расстояния — сила домножается на высоту², чтобы
            // TableLight был яркостью на сукне при любой высоте. Дальность с запасом: у края дальности URP гасит свет.
            var height = _profile.TableLightHeight;
            var radius = _profile.TableLightRadius;
            var outer = radius + _profile.TableLightFade;
            ApplyTableLightPose();
            _tableLight.intensity = power * height * height * _tableFlicker;
            _tableLight.color = _profile.TableLightColor;
            _tableLight.spotAngle = 2f * Mathf.Atan2(outer, height) * Mathf.Rad2Deg;
            _tableLight.innerSpotAngle = 2f * Mathf.Atan2(radius, height) * Mathf.Rad2Deg;
            _tableLight.range = 2.5f * Mathf.Sqrt(height * height + outer * outer);
        }

        /// <summary>Место прожектора: над центром стола на высоте профиля; лампа качается — он качается с ней вокруг подвеса.</summary>
        private void ApplyTableLightPose()
        {
            if (!_tableLightBaseSet)
            {
                var start = _tableLight.transform.position;
                _tableLightBase = new Vector2(start.x, start.z);
                _tableLightBaseSet = true;
            }

            var position = new Vector3(_tableLightBase.x, _profile.TableLightHeight, _tableLightBase.y);
            var down = Quaternion.Euler(90f, 0f, 0f);
            if (_lamp == null || (_tiltX == 0f && _tiltZ == 0f))
            {
                _tableLight.transform.SetPositionAndRotation(position, down);
                return;
            }

            var tilt = Quaternion.Euler(_tiltX, 0f, _tiltZ);
            var pivot = _lampBasePosition + Vector3.up * _pivotAbove;
            _tableLight.transform.SetPositionAndRotation(pivot + tilt * (position - pivot), tilt * down);
        }

        /// <summary>Наклон лампы слота вокруг точки подвеса (LampSwayEffect, каждый кадр). Ноль — лампа на месте.</summary>
        private void SwayLamp(string slotId, float tiltX, float tiltZ, float pivotAbove)
        {
            if (_lamp == null || _lampSlot != slotId)
            {
                RestoreLamp();
                _lamp = _stage != null ? _stage.SpotAnchor(slotId, 0) : null;
                _lampSlot = slotId;
                if (_lamp == null)
                    return;
                _lampBasePosition = _lamp.position;
                _lampBaseRotation = _lamp.rotation;
            }

            _tiltX = tiltX;
            _tiltZ = tiltZ;
            _pivotAbove = pivotAbove;
            var tilt = Quaternion.Euler(tiltX, 0f, tiltZ);
            var pivot = _lampBasePosition + Vector3.up * pivotAbove;
            _lamp.SetPositionAndRotation(pivot + tilt * (_lampBasePosition - pivot), tilt * _lampBaseRotation);
            if (_tableLight != null && _tableLight.enabled && _profile != null)
                ApplyTableLightPose();
        }

        private void RestoreLamp()
        {
            if (_lamp != null)
                _lamp.SetPositionAndRotation(_lampBasePosition, _lampBaseRotation);
            _tiltX = 0f;
            _tiltZ = 0f;
        }

        /// <summary>Мерцание прожектора над столом (LightFlickerEffect, каждый кадр): множитель около 1.</summary>
        private void FlickerTableLight(float factor)
        {
            _tableFlicker = factor;
            if (_tableLight != null && _tableLight.enabled && _profile != null)
            {
                var height = _profile.TableLightHeight;
                _tableLight.intensity = _profile.TableLight * height * height * factor;
            }
        }

#if UNITY_EDITOR
        // Только в редакторе: правка профиля локации во время игры сразу видна на столе — свет подбирается вживую.
        private void Update()
        {
            if (_profile != null)
                ApplyTableLight();
        }
#endif

        private void ApplyLights()
        {
            ApplyTableLight();
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
            var fromDarken = _bakedDarken;
            var toDarken = Color.white - profile.BakedTint;

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

                _bakedDarken = Color.Lerp(fromDarken, toDarken, t);
                Shader.SetGlobalColor(BakedDarkenId, _bakedDarken);

                if (t >= 1f)
                    return;

                // Отмена — проверкой токена: SuppressCancellationThrow на каждом кадре создавал бы объект.
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (ct.IsCancellationRequested)
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

                // Запечённая комната тоже вспыхивает: отрицательное затемнение осветляет текстуру.
                Shader.SetGlobalColor(BakedDarkenId, _bakedDarken - color * (intensity * 0.4f * k));
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (ct.IsCancellationRequested)
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
            Shader.SetGlobalColor(BakedDarkenId, _bakedDarken);
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
