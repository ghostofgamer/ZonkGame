using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class TalentRank
    {
        public string Id;
        public int Rank;
    }

    [Serializable]
    public sealed class TalentsSave
    {
        public List<TalentRank> Ranks = new List<TalentRank>();
    }

    /// <summary>
    /// Таланты: очки = (уровень − 1) × GameConfig.TalentPointsPerLevel + очки открытых достижений (AchievementConfig.TalentPoints).
    /// Уровень и достижения читаются из их разделов сохранения напрямую (без IPlayerLevel и IAchievements: те сами читают
    /// таланты — опыт, монеты, и зависимость была бы по кругу). Value — из кэша по эффектам, без выделений.
    /// </summary>
    public sealed class Talents : ITalents
    {
        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly GameConfig _config;
        private readonly float[] _values = new float[Enum.GetValues(typeof(TalentEffect)).Length];
        private List<TalentConfig> _all;
        private List<AchievementConfig> _pointAchievements;
        private TalentsSave _cachedFor;

        public Talents(ISaveStore saves, ContentDatabase content, GameConfig config)
        {
            _saves = saves;
            _content = content;
            _config = config;
        }

        public event Action Changed;

        private TalentsSave Data => _saves.Get<TalentsSave>(SaveKeys.Talents);

        public IReadOnlyList<TalentConfig> All
        {
            get
            {
                if (_all == null)
                {
                    _all = _content != null ? _content.All<TalentConfig>() : new List<TalentConfig>();
                    _all.Sort((a, b) =>
                        a.Branch != b.Branch ? a.Branch.CompareTo(b.Branch)
                        : a.Row != b.Row ? a.Row.CompareTo(b.Row)
                        : a.Order.CompareTo(b.Order));
                }

                return _all;
            }
        }

        public float Value(TalentEffect effect)
        {
            // Сохранение загружается позже первых обращений: кэш пересобирается, когда сменился объект раздела.
            var data = Data;
            if (!ReferenceEquals(data, _cachedFor))
                Rebuild(data);

            var index = (int)effect;
            return index >= 0 && index < _values.Length ? _values[index] : 0f;
        }

        public int TotalPoints => LevelPoints() + AchievementPoints();

        public int SpentPoints
        {
            get
            {
                var spent = 0;
                foreach (var talent in All)
                    spent += RankOf(talent) * Math.Max(1, talent.CostPerRank);
                return spent;
            }
        }

        public int FreePoints => Math.Max(0, TotalPoints - SpentPoints);

        public int RankOf(TalentConfig talent)
        {
            var entry = Entry(talent, false);
            return entry != null ? Math.Min(entry.Rank, talent.MaxRank) : 0;
        }

        public int PointsIn(TalentBranch branch)
        {
            var points = 0;
            foreach (var talent in All)
            {
                if (talent.Branch == branch)
                    points += RankOf(talent) * Math.Max(1, talent.CostPerRank);
            }

            return points;
        }

        public bool IsOpen(TalentConfig talent)
        {
            if (talent == null)
                return false;
            if (talent.Requires != null && RankOf(talent.Requires) <= 0)
                return false;
            return PointsIn(talent.Branch) >= talent.BranchPointsRequired;
        }

        public bool CanRankUp(TalentConfig talent)
        {
            return talent != null && IsOpen(talent) && RankOf(talent) < talent.MaxRank &&
                   FreePoints >= Math.Max(1, talent.CostPerRank);
        }

        public bool TryRankUp(TalentConfig talent)
        {
            if (!CanRankUp(talent))
                return false;

            Entry(talent, true).Rank++;
            _saves.RequestSave();
            Rebuild(Data);
            Changed?.Invoke();
            return true;
        }

        public void Reset()
        {
            Data.Ranks.Clear();
            _saves.RequestSave();
            Rebuild(Data);
            Changed?.Invoke();
        }

        private void Rebuild(TalentsSave data)
        {
            _cachedFor = data;
            Array.Clear(_values, 0, _values.Length);
            foreach (var talent in All)
            {
                var rank = RankOf(talent);
                var index = (int)talent.Effect;
                if (rank > 0 && index >= 0 && index < _values.Length)
                    _values[index] += rank * talent.ValuePerRank;
            }
        }

        private TalentRank Entry(TalentConfig talent, bool create)
        {
            if (talent == null || string.IsNullOrEmpty(talent.Id))
                return null;

            var data = Data;
            foreach (var entry in data.Ranks)
            {
                if (entry.Id == talent.Id)
                    return entry;
            }

            if (!create)
                return null;

            var created = new TalentRank { Id = talent.Id };
            data.Ranks.Add(created);
            return created;
        }

        private int LevelPoints()
        {
            var levels = _config != null ? _config.PlayerLevel : null;
            if (levels == null)
                return 0;

            var xp = _saves.Get<PlayerLevelSave>(SaveKeys.PlayerLevel).Xp;
            var level = PlayerLevelMath.LevelFor(xp, levels.FirstLevelXp, levels.LevelXpGrowth, levels.MaxLevel);
            return Math.Max(0, level - 1) * Math.Max(0, _config.TalentPointsPerLevel);
        }

        private int AchievementPoints()
        {
            if (_pointAchievements == null)
            {
                _pointAchievements = new List<AchievementConfig>();
                if (_content != null)
                {
                    foreach (var achievement in _content.All<AchievementConfig>())
                    {
                        if (achievement.TalentPoints > 0)
                            _pointAchievements.Add(achievement);
                    }
                }
            }

            var unlocked = _saves.Get<AchievementsSave>(SaveKeys.Achievements).Unlocked;
            var points = 0;
            foreach (var achievement in _pointAchievements)
            {
                if (unlocked.Contains(achievement.Id))
                    points += achievement.TalentPoints;
            }

            return points;
        }
    }
}
