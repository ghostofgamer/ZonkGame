using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Что считает таблица лидеров. Новый показатель — новое значение и его источник в PlayerStats.Value.</summary>
    public enum LeaderboardMetric
    {
        /// <summary>Звёзды за соперников во всей кампании.</summary>
        TotalStars,

        /// <summary>Победы в кампании (включая повторные).</summary>
        CampaignWins,

        /// <summary>Лучший ход: больше всего очков, забранных за один ход, в партиях против соперников.</summary>
        BestTurn,

        /// <summary>«Бесконечный забег»: лучший пройденный этаж.</summary>
        EndlessRunFloor,

        /// <summary>Башня: сколько этажей пройдено.</summary>
        TowerFloor,
    }

    /// <summary>
    /// Таблица лидеров площадки. TechnicalName — техническое имя таблицы в консоли площадки (у Яндекса создаётся
    /// в разделе «Лидерборды»). Новая таблица = новый ассет, окно «Рекорды» покажет её само.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Leaderboard", fileName = "Leaderboard")]
    public sealed class LeaderboardConfig : ContentConfig
    {
        public string TechnicalName;
        public LeaderboardMetric Metric;
        public int Order;
    }
}
