using System;
using Zonk.Progress;

namespace Zonk.Configs
{
    /// <summary>Что знает о игроке цель достижения. Собирается при проверке (редко: после партии, при открытии окна).</summary>
    public sealed class AchievementContext
    {
        public RecordsSave Records;
        public int Level;
        public int Stars;
        public int TowerBest;
        public int EndlessBest;
        public int ChestsOpened;
        public int DreadBeaten;

        /// <summary>Сколько особых костей достигли уровня мастерства (индекс — уровень, 1..).</summary>
        public int[] DiceAtMastery = new int[8];

        /// <summary>Сколько предметов слота у игрока (ID слота).</summary>
        public Func<string, int> OwnedInSlot = _ => 0;
    }

    /// <summary>Цель достижения: текущее значение, которое сравнивается с порогом. Новый вид — наследник.</summary>
    [Serializable]
    public abstract class AchievementGoal
    {
        public abstract long Progress(AchievementContext context);
    }

    /// <summary>Счётчик статистики игрока.</summary>
    public enum RecordStat
    {
        Matches,
        Wins,
        BossWins,
        BestStreak,
        Zonks,
        HotDice,
        PointsBanked,
        BestTurn,
        Rolls,
        Straights,
        ThreePairs,
        FourOfAKind,
        FiveOfAKind,
        SixOfAKind,
        HoursPlayed,
    }

    /// <summary>Значение из статистики (IPlayerRecords).</summary>
    [Serializable]
    public sealed class RecordStatGoal : AchievementGoal
    {
        public RecordStat Stat;

        public override long Progress(AchievementContext context)
        {
            var r = context.Records;
            if (r == null)
                return 0;

            switch (Stat)
            {
                case RecordStat.Matches: return r.Matches;
                case RecordStat.Wins: return r.Wins;
                case RecordStat.BossWins: return r.BossWins;
                case RecordStat.BestStreak: return r.BestStreak;
                case RecordStat.Zonks: return r.Zonks;
                case RecordStat.HotDice: return r.HotDice;
                case RecordStat.PointsBanked: return r.PointsBanked;
                case RecordStat.BestTurn: return r.BestTurn;
                case RecordStat.Rolls: return r.Rolls;
                case RecordStat.Straights: return Combo(r, RecordedCombo.Straight);
                case RecordStat.ThreePairs: return Combo(r, RecordedCombo.ThreePairs);
                case RecordStat.FourOfAKind: return Combo(r, RecordedCombo.FourOfAKind);
                case RecordStat.FiveOfAKind: return Combo(r, RecordedCombo.FiveOfAKind);
                case RecordStat.SixOfAKind: return Combo(r, RecordedCombo.SixOfAKind);
                case RecordStat.HoursPlayed: return r.SecondsPlayed / 3600;
                default: return 0;
            }
        }

        private static long Combo(RecordsSave records, RecordedCombo combo)
        {
            var index = (int)combo;
            return records.Combos != null && index < records.Combos.Length ? records.Combos[index] : 0;
        }
    }

    /// <summary>Прогресс игрока вне статистики.</summary>
    public enum ProgressStat
    {
        Level,
        Stars,
        TowerFloor,
        EndlessFloor,
        ChestsOpened,
        DreadBeaten,
    }

    [Serializable]
    public sealed class ProgressGoal : AchievementGoal
    {
        public ProgressStat Stat;

        public override long Progress(AchievementContext context)
        {
            switch (Stat)
            {
                case ProgressStat.Level: return context.Level;
                case ProgressStat.Stars: return context.Stars;
                case ProgressStat.TowerFloor: return context.TowerBest;
                case ProgressStat.EndlessFloor: return context.EndlessBest;
                case ProgressStat.ChestsOpened: return context.ChestsOpened;
                case ProgressStat.DreadBeaten: return context.DreadBeaten;
                default: return 0;
            }
        }
    }

    /// <summary>Сколько особых костей достигли уровня мастерства MinLevel.</summary>
    [Serializable]
    public sealed class MasteryDiceGoal : AchievementGoal
    {
        public int MinLevel = 1;

        public override long Progress(AchievementContext context)
        {
            var levels = context.DiceAtMastery;
            return levels != null && MinLevel >= 0 && MinLevel < levels.Length ? levels[MinLevel] : 0;
        }
    }

    /// <summary>Сколько предметов слота (аватары, рамки…) у игрока.</summary>
    [Serializable]
    public sealed class CollectionGoal : AchievementGoal
    {
        public string SlotId;

        public override long Progress(AchievementContext context)
        {
            return context.OwnedInSlot != null ? context.OwnedInSlot(SlotId) : 0;
        }
    }
}
