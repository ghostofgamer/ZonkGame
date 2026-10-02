using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Что открывается с уровнем игрока.</summary>
    public enum GameFeature
    {
        Tower,
        EndlessRun,
    }

    [Serializable]
    public sealed class FeatureUnlock
    {
        public GameFeature Feature;

        [Min(1)] public int Level = 1;
    }

    /// <summary>Рубеж уровня: награда сверх обычной за уровень.</summary>
    [Serializable]
    public sealed class PlayerLevelMilestone
    {
        [Min(2)] public int Level = 5;

        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();
    }

    /// <summary>
    /// Уровень игрока: опыт за каждую партию против соперника (за проигрыш тоже, меньше), награды за уровни,
    /// что открывается на каком уровне. Партии вдвоём опыт не дают. Кривая: опыт до следующего уровня =
    /// FirstLevelXp + LevelXpGrowth × (уровень − 1) (Zonk.Core.Progress.PlayerLevelMath).
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Player Level", fileName = "PlayerLevel")]
    public sealed class PlayerLevelConfig : ScriptableObject
    {
        [Header("Кривая уровней")]
        [Tooltip("Опыт с 1-го уровня до 2-го")]
        [Min(1)] public int FirstLevelXp = 200;

        [Tooltip("На сколько больше опыта нужен каждый следующий уровень")]
        [Min(0)] public int LevelXpGrowth = 60;

        [Tooltip("Наибольший уровень (0 — без предела)")]
        [Min(0)] public int MaxLevel = 0;

        [Header("Опыт за партию")]
        public int WinXp = 50;
        public int LossXp = 20;

        [Tooltip("За сдачу: 0, чтобы опыт не набирали быстрыми сдачами")]
        public int SurrenderXp = 0;

        [Tooltip("Опыт за каждую 1000 очков игрока в партии")]
        public int XpPer1000Points = 10;

        [Tooltip("Предел опыта за очки в одной партии")]
        public int MaxScoreXp = 100;

        [Tooltip("Сверху за победу над боссом")]
        public int BossWinXp = 30;

        [Tooltip("Сверху за первую победу за день: причина зайти завтра")]
        public int FirstWinOfDayXp = 100;

        [Header("Награды")]
        [Tooltip("Награда за каждый новый уровень")]
        [SerializeReference, SubclassSelector]
        public List<Reward> EveryLevelRewards = new List<Reward>();

        [Tooltip("Рубежи: награда сверх обычной на определённых уровнях")]
        public List<PlayerLevelMilestone> Milestones = new List<PlayerLevelMilestone>();

        [Header("Что открывается")]
        public List<FeatureUnlock> Unlocks = new List<FeatureUnlock>();

        /// <summary>Опыт за партию без бонуса первой победы дня.</summary>
        public int MatchXp(bool won, bool surrendered, int score, bool vsBoss)
        {
            if (surrendered)
                return Mathf.Max(0, SurrenderXp);

            var xp = won ? WinXp : LossXp;
            xp += Mathf.Clamp(Mathf.Max(0, score) / 1000 * XpPer1000Points, 0, Mathf.Max(0, MaxScoreXp));
            if (won && vsBoss)
                xp += BossWinXp;
            return Mathf.Max(0, xp);
        }

        /// <summary>С какого уровня открыто; 1 — сразу.</summary>
        public int UnlockLevel(GameFeature feature)
        {
            foreach (var unlock in Unlocks)
            {
                if (unlock != null && unlock.Feature == feature)
                    return Mathf.Max(1, unlock.Level);
            }

            return 1;
        }

        public PlayerLevelMilestone MilestoneAt(int level)
        {
            foreach (var milestone in Milestones)
            {
                if (milestone != null && milestone.Level == level)
                    return milestone;
            }

            return null;
        }
    }
}
