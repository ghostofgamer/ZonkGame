using System.Collections.Generic;
using Base.Services.Quality;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zonk.Configs
{
    /// <summary>
    /// Освещение и атмосфера локации: солнце, окружающий свет, туман, фон, сила светильников, постобработка,
    /// эффекты (дождь, молнии…). Локация ссылается на профиль (PrefabPayload.Lighting), при её смене
    /// LightingDirector плавно переводит сцену на новый профиль. Сами источники света (лампа, окно, факел) —
    /// Light в префабах с компонентом SceneLight: профиль только масштабирует их яркость (LampIntensity).
    ///
    /// Производительность (телефоны, браузер): тени только от солнца и с уровня качества SunShadowsFrom,
    /// постобработка только с уровня PostProcessFrom, эффекты сами ужимаются по уровню качества.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Lighting Profile", fileName = "Lighting")]
    public sealed class LightingProfileConfig : ScriptableObject
    {
        [Header("Солнце")]
        public bool Sun = true;
        public Color SunColor = new Color(1f, 0.93f, 0.82f);
        public float SunIntensity = 0.9f;
        public Vector3 SunRotation = new Vector3(50f, -35f, 0f);

        [Tooltip("С какого уровня качества солнце отбрасывает тени (тени дорогие для телефонов)")]
        public QualityTier SunShadowsFrom = QualityTier.Medium;

        [Header("Окружающий свет (тени не чёрные)")]
        public Color AmbientSky = new Color(0.45f, 0.44f, 0.42f);
        public Color AmbientEquator = new Color(0.34f, 0.31f, 0.3f);
        public Color AmbientGround = new Color(0.18f, 0.16f, 0.14f);

        [Header("Туман и фон")]
        public bool Fog;
        public Color FogColor = new Color(0.2f, 0.2f, 0.22f);
        public float FogDensity = 0.03f;
        public Color Background = new Color(0.12f, 0.11f, 0.1f);

        [Header("Светильники (SceneLight в префабах ламп и локаций)")]
        [Tooltip("Множитель яркости: 0 — лампа только для вида, 1 — как в префабе, больше — главный свет сцены")]
        public float LampIntensity = 1f;

        [Header("Постобработка (свечение, виньетка, цвет)")]
        public VolumeProfile PostProcess;

        [Tooltip("С какого уровня качества включать постобработку: на слабых устройствах она дорогая")]
        public QualityTier PostProcessFrom = QualityTier.High;

        [Header("Стиль")]
        [Tooltip("Свой комиксовый стиль локации (цвета теней, точки, контур); пусто — общий GameConfig.ToonStyle")]
        public ToonStyleConfig ToonStyle;

        [Header("Эффекты атмосферы")]
        [SerializeReference, SubclassSelector]
        public List<AtmosphereEffect> Effects = new List<AtmosphereEffect>();

        [Tooltip("За сколько секунд сцена переходит на этот профиль")]
        public float TransitionDuration = 0.6f;
    }
}
