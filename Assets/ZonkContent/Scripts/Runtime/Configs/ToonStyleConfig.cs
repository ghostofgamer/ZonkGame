using System;
using Base.Services.Quality;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Комиксовый стиль игры для шейдера Zonk/Toon: ступени света, растр точками в тенях, кромка, блик, контур.
    /// Общий стиль — GameConfig.ToonStyle; локация может заменить его своим (LightingProfileConfig.ToonStyle).
    /// Значения выставляет ToonStyleService глобально для всех материалов; в редакторе правка видна сразу.
    ///
    /// Производительность: всё считается в материале, без постобработки. Самое дорогое — контур (второй проход
    /// по вершинам объекта), поэтому у него свой уровень качества.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Toon Style", fileName = "ToonStyle")]
    public sealed class ToonStyleConfig : ScriptableObject
    {
        [Tooltip("1 — комиксовый стиль, 0 — обычный плавный свет (для сравнения). Промежуточные значения смешивают")]
        [Range(0f, 1f)] public float Blend = 1f;

        [Tooltip("Генератор (Zonk/Setup/Build Everything) переводит материалы игры на шейдер Zonk/Toon. " +
                 "Снять — меню Zonk/Art/Materials: URP Lit вернёт обычный шейдер")]
        public bool ToonMaterials = true;

        [Header("Ступени света")]
        [Tooltip("Освещение в тени: доля света солнца и её оттенок")]
        public Color ShadowTint = new Color(0.42f, 0.4f, 0.5f);

        [Tooltip("Освещение в полутени")]
        public Color MidTint = new Color(0.74f, 0.72f, 0.78f);

        [Tooltip("Свет ниже этого — тень")]
        [Range(0f, 1f)] public float ShadowThreshold = 0.12f;

        [Tooltip("Свет выше этого — полный свет")]
        [Range(0f, 1f)] public float LightThreshold = 0.5f;

        [Tooltip("Мягкость границы ступеней: меньше — резче")]
        [Range(0.001f, 0.2f)] public float Softness = 0.02f;

        [Header("Растр точками в тенях")]
        public Color DotColor = new Color(0.25f, 0.18f, 0.3f);

        [Tooltip("Насколько точки темнят цвет")]
        [Range(0f, 1f)] public float DotStrength = 0.75f;

        [Tooltip("Шаг сетки точек в пикселях при высоте экрана 1080")]
        [Min(2f)] public float DotCell = 7f;

        [Tooltip("Наибольший радиус точки в глубокой тени (доля ячейки, 0.5 — точки касаются)")]
        [Range(0f, 0.5f)] public float DotMaxRadius = 0.45f;

        [Tooltip("Точки появляются, когда свет ниже этого")]
        [Range(0f, 1f)] public float DotStart = 0.45f;

        [Tooltip("Наклон сетки, градусы")]
        public float DotAngle = 45f;

        [Tooltip("С какого уровня качества рисовать точки")]
        public QualityTier DotsFrom = QualityTier.Low;

        [Header("Кромка по силуэту")]
        public Color RimColor = new Color(1f, 0.95f, 0.85f);

        [Tooltip("Ширина кромки: больше — уже")]
        [Range(0f, 1f)] public float RimThreshold = 0.72f;

        [Range(0f, 1f)] public float RimStrength = 0.3f;

        [Tooltip("Доля кромки на теневой стороне")]
        [Range(0f, 1f)] public float RimInShadow = 0.3f;

        [Header("Блик")]
        [Tooltip("Блик ярче этого — пятно, тусклее — нет")]
        [Range(0f, 1f)] public float SpecularThreshold = 0.5f;

        [Range(0f, 2f)] public float SpecularStrength = 0.8f;

        [Header("Контур")]
        public Color OutlineColor = new Color(0.06f, 0.04f, 0.06f);

        [Tooltip("Толщина в пикселях при высоте экрана 1080; одна и та же на любом расстоянии")]
        [Range(0f, 6f)] public float OutlineWidth = 2f;

        [Tooltip("С какого уровня качества рисовать контур")]
        public QualityTier OutlineFrom = QualityTier.Low;

#if UNITY_EDITOR
        /// <summary>Правка в инспекторе: применить сразу (в игре и в окне сцены).</summary>
        public static event Action<ToonStyleConfig> EditorChanged;

        private void OnValidate()
        {
            EditorChanged?.Invoke(this);
        }
#endif
    }
}
