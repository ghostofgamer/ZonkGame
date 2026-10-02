using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class AchievementsSave
    {
        public List<string> Unlocked = new List<string>();
    }

    /// <summary>Открытое достижение и выданные награды: для сообщения после партии.</summary>
    public readonly struct AchievementUnlock
    {
        public AchievementUnlock(AchievementConfig achievement, List<GrantedReward> rewards)
        {
            Achievement = achievement;
            Rewards = rewards;
        }

        public AchievementConfig Achievement { get; }
        public List<GrantedReward> Rewards { get; }
    }

    /// <summary>
    /// Достижения: прогресс считается из статистики, уровня, звёзд, режимов, мастерства и коллекции (AchievementContext).
    /// Check — после партии и при открытии окна: новые открытые выдают награды сразу, сообщения — TakeUnlocked.
    /// </summary>
    public interface IAchievements
    {
        /// <summary>Все достижения по порядку.</summary>
        IReadOnlyList<AchievementConfig> All { get; }

        bool IsUnlocked(AchievementConfig achievement);

        /// <summary>Текущее значение цели (после последней проверки).</summary>
        long ProgressOf(AchievementConfig achievement);

        int UnlockedCount { get; }

        void Check();

        List<AchievementUnlock> TakeUnlocked();
    }

    public sealed class Achievements : IAchievements
    {
        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly RewardGranter _granter;
        private readonly IPlayerRecords _records;
        private readonly IPlayerLevel _level;
        private readonly ICampaignProgress _campaign;
        private readonly IChestService _chests;
        private readonly IDieMastery _mastery;
        private readonly IInventory _inventory;
        private readonly List<AchievementUnlock> _unlocked = new List<AchievementUnlock>();
        private readonly Dictionary<AchievementConfig, long> _progress = new Dictionary<AchievementConfig, long>();
        private List<AchievementConfig> _all;

        public Achievements(ISaveStore saves, ContentDatabase content, RewardGranter granter, IPlayerRecords records, IPlayerLevel level,
            ICampaignProgress campaign, IChestService chests, IDieMastery mastery, IInventory inventory)
        {
            _saves = saves;
            _content = content;
            _granter = granter;
            _records = records;
            _level = level;
            _campaign = campaign;
            _chests = chests;
            _mastery = mastery;
            _inventory = inventory;
        }

        private AchievementsSave Data => _saves.Get<AchievementsSave>(SaveKeys.Achievements);

        public IReadOnlyList<AchievementConfig> All
        {
            get
            {
                if (_all == null)
                {
                    _all = _content.All<AchievementConfig>();
                    _all.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Target.CompareTo(b.Target));
                }

                return _all;
            }
        }

        public bool IsUnlocked(AchievementConfig achievement)
        {
            return achievement != null && Data.Unlocked.Contains(achievement.Id);
        }

        public long ProgressOf(AchievementConfig achievement)
        {
            return achievement != null && _progress.TryGetValue(achievement, out var value) ? value : 0;
        }

        public int UnlockedCount => Data.Unlocked.Count;

        public void Check()
        {
            var context = BuildContext();
            var data = Data;
            var changed = false;
            foreach (var achievement in All)
            {
                var value = achievement.Goal != null ? achievement.Goal.Progress(context) : 0;
                _progress[achievement] = value;
                if (value < achievement.Target || data.Unlocked.Contains(achievement.Id))
                    continue;

                data.Unlocked.Add(achievement.Id);
                _unlocked.Add(new AchievementUnlock(achievement, _granter.Grant(achievement.Rewards)));
                changed = true;
            }

            if (changed)
                _saves.RequestSave();
        }

        public List<AchievementUnlock> TakeUnlocked()
        {
            var result = new List<AchievementUnlock>(_unlocked);
            _unlocked.Clear();
            return result;
        }

        private AchievementContext BuildContext()
        {
            var context = new AchievementContext
            {
                Records = _records.Data,
                Level = _level != null ? _level.Level : 1,
                Stars = _campaign.TotalStars,
                TowerBest = _saves.Get<TowerSave>(SaveKeys.Tower).Best,
                EndlessBest = _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun).Best,
                ChestsOpened = _chests != null ? _chests.Opened : 0,
                OwnedInSlot = CountOwned,
            };

            foreach (var chapter in _campaign.Chapters)
            {
                foreach (var dread in chapter.DreadBosses)
                {
                    if (dread != null && _campaign.GetState(chapter, dread) == OpponentState.Beaten)
                        context.DreadBeaten++;
                }
            }

            foreach (var die in _content.All<DieConfig>())
            {
                var level = die.IsSpecial ? _mastery.GetLevel(die) : 0;
                for (var i = 1; i <= level && i < context.DiceAtMastery.Length; i++)
                    context.DiceAtMastery[i]++;
            }

            return context;
        }

        private int CountOwned(string slotId)
        {
            var count = 0;
            foreach (var item in _content.All<CosmeticItemConfig>())
            {
                if (item.Slot != null && item.Slot.Id == slotId && _inventory.IsOwned(item))
                    count++;
            }

            return count;
        }
    }
}
