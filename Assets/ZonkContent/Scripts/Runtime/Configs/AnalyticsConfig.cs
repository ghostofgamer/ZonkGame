using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Настройки аналитики игры (что отправлять). Куда — решает площадка (RuStore: AppMetrica).</summary>
    [CreateAssetMenu(menuName = "Zonk/Analytics", fileName = "Analytics")]
    public sealed class AnalyticsConfig : ScriptableObject
    {
        [Tooltip("Слать тестовые события в папку test: проверить, что аналитика доходит. Перед выпуском выключить, " +
                 "чтобы в отчётах были только настоящие события")]
        public bool SendTestEvents = true;

        private static AnalyticsConfig _fallback;

        /// <summary>Значения по умолчанию, если ассет не задан.</summary>
        public static AnalyticsConfig Fallback => _fallback != null ? _fallback : _fallback = CreateInstance<AnalyticsConfig>();
    }
}
