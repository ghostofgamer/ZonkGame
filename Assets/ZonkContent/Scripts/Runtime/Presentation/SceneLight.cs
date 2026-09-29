using Base.Services.Quality;
using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Источник света в префабе лампы или локации (лампа, окно, факел). Яркость из префаба — базовая, профиль
    /// освещения локации её масштабирует (LightingProfileConfig.LampIntensity): одна и та же лампа в подвале — главный
    /// свет, на пляже — почти только для вида. На слабых устройствах лишние источники выключаются (MinTier).
    /// Тени у таких источников выключены: для телефонов и браузера они слишком дорогие; узор (решётка) — cookie.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class SceneLight : MonoBehaviour
    {
        [Tooltip("С какого уровня качества источник включён. Главный свет сцены — Low (всегда)")]
        [SerializeField] private QualityTier _minTier = QualityTier.Low;

        private Light _light;
        private float _baseIntensity = -1f;

        public void Apply(float multiplier, QualityTier tier)
        {
            if (_light == null)
            {
                _light = GetComponent<Light>();
                _baseIntensity = _light.intensity;
                _light.shadows = LightShadows.None;
            }

            var on = tier >= _minTier && multiplier > 0.001f;
            if (_light.enabled != on)
                _light.enabled = on;
            if (on)
                _light.intensity = _baseIntensity * multiplier;
        }
    }
}
