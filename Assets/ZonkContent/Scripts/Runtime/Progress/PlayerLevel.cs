using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class PlayerLevelSave
    {
        public long Xp;

        /// <summary>До какого уровня награды уже выданы (0 в новом сохранении — как 1).</summary>
        public int RewardedLevel;

        /// <summary>День последней победы (yyyy-MM-dd, местное время): бонус первой победы дня.</summary>
        public string LastWinDay;
    }

    /// <summary>Опыт за партию для экрана итогов: было, стало, из чего сложилось.</summary>
    public readonly struct XpGain
    {
        public XpGain(int matchXp, int firstWinXp, long xpBefore, long xpAfter)
        {
            MatchXp = matchXp;
            FirstWinXp = firstWinXp;
            XpBefore = xpBefore;
            XpAfter = xpAfter;
        }

        public int MatchXp { get; }
        public int FirstWinXp { get; }
        public long XpBefore { get; }
        public long XpAfter { get; }
        public int Total => MatchXp + FirstWinXp;
    }

    /// <summary>Новый уровень игрока и выданные за него награды: для сообщения после партии.</summary>
    public readonly struct PlayerLevelUp
    {
        public PlayerLevelUp(int level, List<GrantedReward> rewards)
        {
            Level = level;
            Rewards = rewards;
        }

        public int Level { get; }
        public List<GrantedReward> Rewards { get; }
    }

    /// <summary>
    /// Уровень игрока: опыт копится за каждую партию против соперника (кампания, башня, забег), за проигрыш тоже.
    /// Настройки и награды — PlayerLevelConfig (GameConfig.PlayerLevel). Прогресс есть и в неудачный день.
    /// </summary>
    public interface IPlayerLevel
    {
        int Level { get; }
        long Xp { get; }

        /// <summary>Опыт, набранный на текущем уровне, и сколько нужно до следующего.</summary>
        void GetProgress(long xp, out int level, out long intoLevel, out long toNext);

        /// <summary>Начислить опыт за партию. Награды за новые уровни выдаются сразу, сообщения — TakeLevelUps.</summary>
        XpGain AddMatch(bool won, bool surrendered, int score, bool vsBoss);

        List<PlayerLevelUp> TakeLevelUps();

        int UnlockLevel(GameFeature feature);
        bool IsUnlocked(GameFeature feature);

        /// <summary>Достигнут новый уровень (номер уровня).</summary>
        event Action<int> LevelReached;

        event Action Changed;
    }

    public sealed class PlayerLevel : IPlayerLevel
    {
        private readonly ISaveStore _saves;
        private readonly PlayerLevelConfig _config;
        private readonly RewardGranter _granter;
        private readonly IGameClock _clock;
        private readonly List<PlayerLevelUp> _levelUps = new List<PlayerLevelUp>();

        public PlayerLevel(ISaveStore saves, GameConfig config, RewardGranter granter, IGameClock clock)
        {
            _saves = saves;
            _config = config != null ? config.PlayerLevel : null;
            _granter = granter;
            _clock = clock;
        }

        public event Action<int> LevelReached;
        public event Action Changed;

        private PlayerLevelSave Data => _saves.Get<PlayerLevelSave>(SaveKeys.PlayerLevel);

        public long Xp => Data.Xp;

        public int Level => LevelFor(Data.Xp);

        public void GetProgress(long xp, out int level, out long intoLevel, out long toNext)
        {
            level = LevelFor(xp);
            if (_config == null)
            {
                intoLevel = 0;
                toNext = 1;
                return;
            }

            intoLevel = xp - PlayerLevelMath.TotalXpFor(level, _config.FirstLevelXp, _config.LevelXpGrowth);
            toNext = PlayerLevelMath.XpToNext(level, _config.FirstLevelXp, _config.LevelXpGrowth);
            if (_config.MaxLevel > 0 && level >= _config.MaxLevel)
                intoLevel = toNext;
        }

        public XpGain AddMatch(bool won, bool surrendered, int score, bool vsBoss)
        {
            var data = Data;
            if (_config == null)
                return new XpGain(0, 0, data.Xp, data.Xp);

            var matchXp = _config.MatchXp(won, surrendered, score, vsBoss);
            var firstWinXp = 0;
            if (won && !surrendered)
            {
                var today = _clock.LocalNow.ToString("yyyy-MM-dd");
                if (data.LastWinDay != today)
                {
                    data.LastWinDay = today;
                    firstWinXp = Math.Max(0, _config.FirstWinOfDayXp);
                }
            }

            var before = data.Xp;
            data.Xp = before + matchXp + firstWinXp;
            GrantNewLevels(data);
            _saves.RequestSave();
            Changed?.Invoke();
            return new XpGain(matchXp, firstWinXp, before, data.Xp);
        }

        public List<PlayerLevelUp> TakeLevelUps()
        {
            var result = new List<PlayerLevelUp>(_levelUps);
            _levelUps.Clear();
            return result;
        }

        public int UnlockLevel(GameFeature feature)
        {
            return _config != null ? _config.UnlockLevel(feature) : 1;
        }

        public bool IsUnlocked(GameFeature feature)
        {
            return Level >= UnlockLevel(feature);
        }

        private void GrantNewLevels(PlayerLevelSave data)
        {
            var level = LevelFor(data.Xp);
            if (data.RewardedLevel < 1)
                data.RewardedLevel = 1;

            while (data.RewardedLevel < level)
            {
                data.RewardedLevel++;
                var granted = _granter.Grant(_config.EveryLevelRewards);
                var milestone = _config.MilestoneAt(data.RewardedLevel);
                if (milestone != null)
                    granted.AddRange(_granter.Grant(milestone.Rewards));
                _levelUps.Add(new PlayerLevelUp(data.RewardedLevel, granted));
                LevelReached?.Invoke(data.RewardedLevel);
            }
        }

        private int LevelFor(long xp)
        {
            return _config != null ? PlayerLevelMath.LevelFor(xp, _config.FirstLevelXp, _config.LevelXpGrowth, _config.MaxLevel) : 1;
        }
    }
}
