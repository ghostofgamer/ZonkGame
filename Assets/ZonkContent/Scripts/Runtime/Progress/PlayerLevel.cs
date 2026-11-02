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

        /// <summary>Наибольший уровень (0 — без предела).</summary>
        int MaxLevel { get; }

        /// <summary>Опыт, набранный на текущем уровне, и сколько нужно до следующего.</summary>
        void GetProgress(long xp, out int level, out long intoLevel, out long toNext);

        /// <summary>Начислить опыт за партию. Награды за новые уровни выдаются сразу, сообщения — TakeLevelUps.</summary>
        XpGain AddMatch(bool won, bool surrendered, int score, bool vsBoss);

        /// <summary>Начислить опыт напрямую (проверка в редакторе, будущие награды опытом). Награды уровней — как обычно.</summary>
        void AddXp(long xp);

        List<PlayerLevelUp> TakeLevelUps();

        /// <summary>
        /// Вещи пройденных уровней, которых у игрока нет: рубеж добавили или передвинули обновлением. Выдаёт и
        /// возвращает выданное (пусто — ничего). Монеты задним числом не выдаются: только вещи.
        /// </summary>
        List<GrantedReward> GrantMissedItems();

        /// <summary>На каком уровне выдаётся вещь (рубеж конфига); 0 — не за уровень.</summary>
        int RewardLevelOf(ContentConfig item);

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

        private readonly ITalents _talents;

        public PlayerLevel(ISaveStore saves, GameConfig config, RewardGranter granter, IGameClock clock, ITalents talents = null)
        {
            _talents = talents;
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

        public int MaxLevel => _config != null ? _config.MaxLevel : 0;

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

        public void AddXp(long xp)
        {
            if (_config == null || xp <= 0)
                return;

            var data = Data;
            data.Xp += xp;
            Cap(data);
            GrantNewLevels(data);
            _saves.RequestSave();
            Changed?.Invoke();
        }

        public XpGain AddMatch(bool won, bool surrendered, int score, bool vsBoss)
        {
            var data = Data;
            if (_config == null)
                return new XpGain(0, 0, data.Xp, data.Xp);

            var matchXp = _config.MatchXp(won, surrendered, score, vsBoss);
            if (_talents != null && matchXp > 0)
                matchXp = (int)Math.Round(matchXp * (1.0 + Math.Max(0f, _talents.Value(TalentEffect.XpPercent)) / 100.0));
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
            Cap(data);
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

        public List<GrantedReward> GrantMissedItems()
        {
            var granted = new List<GrantedReward>();
            if (_config == null)
                return granted;

            var reached = Math.Min(Data.RewardedLevel, Level);
            foreach (var milestone in _config.Milestones)
            {
                if (milestone == null || milestone.Level > reached)
                    continue;

                foreach (var reward in milestone.Rewards)
                {
                    if (reward is ContentReward content && content.Item != null)
                        granted.AddRange(_granter.Grant(new Reward[] { reward }));
                }
            }

            if (granted.Count > 0)
            {
                _saves.RequestSave();
                Changed?.Invoke();
            }

            return granted;
        }

        public int RewardLevelOf(ContentConfig item)
        {
            return _config != null ? _config.LevelOf(item) : 0;
        }

        public int UnlockLevel(GameFeature feature)
        {
            return _config != null ? _config.UnlockLevel(feature) : 1;
        }

        public bool IsUnlocked(GameFeature feature)
        {
            if (DebugCheats.UnlockAll)
                return true;

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

        /// <summary>На пределе опыт не копится: после поднятия предела игрок идёт со следующего уровня, а не прыгает через несколько.</summary>
        private void Cap(PlayerLevelSave data)
        {
            if (_config.MaxLevel <= 0)
                return;

            var cap = PlayerLevelMath.TotalXpFor(_config.MaxLevel, _config.FirstLevelXp, _config.LevelXpGrowth);
            if (data.Xp > cap)
                data.Xp = cap;
        }

        private int LevelFor(long xp)
        {
            return _config != null ? PlayerLevelMath.LevelFor(xp, _config.FirstLevelXp, _config.LevelXpGrowth, _config.MaxLevel) : 1;
        }
    }
}
