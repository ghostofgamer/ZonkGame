using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Ступень сезонного пути: бесплатная награда и награда платной дорожки.</summary>
    [Serializable]
    public sealed class SeasonStep
    {
        [SerializeReference, SubclassSelector]
        public List<Reward> Free = new List<Reward>();

        [SerializeReference, SubclassSelector]
        public List<Reward> Premium = new List<Reward>();
    }

    /// <summary>
    /// Сезон: даты (UTC), ступени с наградами двух дорожек, товар платной дорожки. Очки сезона — тот же опыт, что за
    /// уровень игрока (каждая партия против соперника). Новый сезон — новый ассет с новыми датами и новым PassProductId
    /// (завести товар в консолях площадок). Платная дорожка — только внешний вид, не сила.
    /// Без покупок на площадке платные награды открываются рекламой или прогрессом (PremiumForAd, PremiumFreeAfterSteps).
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Season", fileName = "Season")]
    public sealed class SeasonConfig : ContentConfig
    {
        [Tooltip("Начало, UTC, yyyy-MM-dd")]
        public string Start = "2026-11-01";

        [Tooltip("Конец (не включительно), UTC, yyyy-MM-dd")]
        public string End = "2026-12-13";

        [Tooltip("Очков (опыта) на ступень")]
        [Min(1)] public int PointsPerStep = 300;

        public List<SeasonStep> Steps = new List<SeasonStep>();

        [Tooltip("Товар платной дорожки (постоянный), ID как в консоли площадки. Пусто — платной дорожки нет")]
        public string PassProductId;

        [Header("Без покупок на площадке")]
        [Tooltip("Платную награду можно забрать за просмотр рекламы")]
        public bool PremiumForAd = true;

        [Tooltip("Платная награда ступени открывается даром, когда игрок ушёл на столько ступеней дальше (0 — нет)")]
        [Min(0)] public int PremiumFreeAfterSteps = 8;

        public DateTime StartUtc => Parse(Start, DateTime.MaxValue);
        public DateTime EndUtc => Parse(End, DateTime.MinValue);

        public bool IsActive(DateTime utcNow)
        {
            return utcNow >= StartUtc && utcNow < EndUtc;
        }

        private static DateTime Parse(string value, DateTime fallback)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date
                : fallback;
        }
    }
}
