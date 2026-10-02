using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    /// <summary>Новый уровень мастерства кости: для сообщения после партии.</summary>
    public readonly struct MasteryLevelUp
    {
        public MasteryLevelUp(DieConfig die, int level, List<GrantedReward> rewards)
        {
            Die = die;
            Level = level;
            Rewards = rewards;
        }

        public DieConfig Die { get; }
        public int Level { get; }
        public List<GrantedReward> Rewards { get; }
    }

    /// <summary>
    /// Мастерство особых костей: очки копятся за забранные комбинации с костью в партиях против соперников.
    /// Уровни и награды — GameConfig.MasteryLevels, вид кости на уровне — DieConfig.MasteryLooks.
    /// </summary>
    public interface IDieMastery
    {
        int LevelCount { get; }
        int GetPoints(DieConfig die);

        /// <summary>0 — уровня ещё нет.</summary>
        int GetLevel(DieConfig die);

        /// <summary>Описание уровня 1..LevelCount, иначе null.</summary>
        MasteryLevel GetLevelInfo(int level);

        void AddPoints(DieConfig die, int points);

        List<MasteryLevelUp> TakeLevelUps();

        event Action Changed;
    }

    public sealed class DieMastery : IDieMastery
    {
        private readonly ISaveStore _saves;
        private readonly GameConfig _config;
        private readonly RewardGranter _granter;
        private readonly List<MasteryLevelUp> _levelUps = new List<MasteryLevelUp>();

        private readonly ITalents _talents;

        public DieMastery(ISaveStore saves, GameConfig config, RewardGranter granter, ITalents talents = null)
        {
            _talents = talents;
            _saves = saves;
            _config = config;
            _granter = granter;
        }

        public event Action Changed;

        private MasterySave Data => _saves.Get<MasterySave>(SaveKeys.Mastery);

        public int LevelCount => _config.MasteryLevels.Count;

        public int GetPoints(DieConfig die)
        {
            var entry = Entry(die, false);
            return entry != null ? entry.Points : 0;
        }

        public int GetLevel(DieConfig die)
        {
            return die != null && die.IsSpecial ? MasteryMath.LevelFor(GetPoints(die), Thresholds()) : 0;
        }

        public MasteryLevel GetLevelInfo(int level)
        {
            return level >= 1 && level <= _config.MasteryLevels.Count ? _config.MasteryLevels[level - 1] : null;
        }

        public void AddPoints(DieConfig die, int points)
        {
            if (die == null || !die.IsSpecial || points <= 0)
                return;

            // Талант «Мастер»: очки мастерства быстрее.
            if (_talents != null)
                points = (int)Math.Min(int.MaxValue, Math.Round(points * (1.0 + Math.Max(0f, _talents.Value(TalentEffect.MasteryPercent)) / 100.0)));

            var entry = Entry(die, true);
            entry.Points = (int)Math.Min(int.MaxValue, (long)entry.Points + points);
            var level = MasteryMath.LevelFor(entry.Points, Thresholds());
            while (entry.RewardedLevel < level)
            {
                entry.RewardedLevel++;
                var info = GetLevelInfo(entry.RewardedLevel);
                var granted = info != null ? _granter.Grant(info.Rewards) : new List<GrantedReward>();
                _levelUps.Add(new MasteryLevelUp(die, entry.RewardedLevel, granted));
            }

            _saves.RequestSave();
            Changed?.Invoke();
        }

        public List<MasteryLevelUp> TakeLevelUps()
        {
            var result = new List<MasteryLevelUp>(_levelUps);
            _levelUps.Clear();
            return result;
        }

        private int[] _thresholds;

        /// <summary>Пороги уровней: собираются один раз (AddPoints вызывается на каждую кость каждого хода).</summary>
        private int[] Thresholds()
        {
            var levels = _config.MasteryLevels;
            if (_thresholds != null && _thresholds.Length == levels.Count)
                return _thresholds;

            _thresholds = new int[levels.Count];
            for (var i = 0; i < levels.Count; i++)
                _thresholds[i] = levels[i] != null ? levels[i].Points : int.MaxValue;
            return _thresholds;
        }

        private MasteryEntry Entry(DieConfig die, bool create)
        {
            if (die == null || string.IsNullOrEmpty(die.Id))
                return null;

            var data = Data;
            foreach (var entry in data.Dice)
            {
                if (entry.Id == die.Id)
                    return entry;
            }

            if (!create)
                return null;

            var created = new MasteryEntry { Id = die.Id };
            data.Dice.Add(created);
            return created;
        }
    }
}
