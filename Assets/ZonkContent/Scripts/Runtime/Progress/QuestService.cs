using System;
using System.Collections.Generic;
using Base.Services.Monetization;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    /// <summary>Выданное задание: конфиг и прогресс игрока.</summary>
    public sealed class QuestSlot
    {
        internal QuestSlot(QuestConfig quest, QuestEntry entry, QuestPeriod period)
        {
            Quest = quest;
            Entry = entry;
            Period = period;
        }

        public QuestConfig Quest { get; }
        public QuestPeriod Period { get; }
        internal QuestEntry Entry { get; }

        public int Target => Quest.Goal != null ? Math.Max(1, Quest.Goal.Target) : 1;
        public int Progress => Math.Min(Entry.Progress, Target);
        public bool IsDone => Entry.Progress >= Target;
        public bool IsClaimed => Entry.Claimed;
        public bool CanClaim => IsDone && !IsClaimed;
    }

    /// <summary>
    /// Задания дня и недели. Набор выбирается по номеру дня (недели), поэтому не меняется от перезапусков.
    /// Прогресс копится из событий игры (Report), награда забирается кнопкой (Claim). Дневное задание можно
    /// заменить (Reroll) ограниченное число раз в день: рекламу за замену запрашивает окно через IRewardService.
    /// </summary>
    public interface IQuestService
    {
        IReadOnlyList<QuestSlot> Get(QuestPeriod period);
        TimeSpan TimeToReset(QuestPeriod period);

        /// <summary>Есть выполненные задания с незабранной наградой (значок на кнопке меню).</summary>
        bool HasClaimable { get; }

        int RerollsLeft { get; }
        bool CanReroll(QuestSlot slot);
        bool Reroll(QuestSlot slot);

        List<GrantedReward> Claim(QuestSlot slot);

        void Report(QuestEvent e);

        /// <summary>Задания, выполненные с прошлого вызова: для всплывающих сообщений после партии.</summary>
        List<QuestConfig> TakeCompleted();

        event Action Changed;
    }

    public sealed class QuestService : IQuestService
    {
        private const int DailySalt = 1;
        private const int WeeklySalt = 2;
        private const int RerollSalt = 100;

        private static readonly QuestPeriod[] Periods = { QuestPeriod.Daily, QuestPeriod.Weekly };

        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly GameConfig _config;
        private readonly IInventory _inventory;
        private readonly RewardGranter _granter;
        private readonly IGameClock _clock;
        private readonly ICampaignProgress _campaign;
        private readonly IRewardService _rewards;
        private readonly List<QuestConfig> _completed = new List<QuestConfig>();
        private bool _refreshing;

        public QuestService(ISaveStore saves, ContentDatabase content, GameConfig config, IInventory inventory,
            RewardGranter granter, IGameClock clock, ICampaignProgress campaign, IRewardService rewards)
        {
            _saves = saves;
            _content = content;
            _config = config;
            _inventory = inventory;
            _granter = granter;
            _clock = clock;
            _campaign = campaign;
            _rewards = rewards;

            // Любая награда за рекламу (удвоение, энергия, магазин, кнопка в окне заданий) продвигает «посмотреть рекламу».
            _rewards.Completed += (placement, outcome) =>
            {
                if (outcome.IsGranted())
                    Report(new QuestEvent { Kind = QuestEventKind.AdWatched });
            };
        }

        /// <summary>Что знают условия заданий об игроке сейчас.</summary>
        private QuestContext Context => new QuestContext
        {
            Inventory = _inventory,
            Campaign = _campaign,
            AdsAvailable = _rewards.CanOffer,
        };

        public event Action Changed;

        private QuestSave Data => _saves.Get<QuestSave>(SaveKeys.Quests);

        public IReadOnlyList<QuestSlot> Get(QuestPeriod period)
        {
            Refresh();
            return Slots(period);
        }

        public TimeSpan TimeToReset(QuestPeriod period)
        {
            var now = _clock.LocalNow;
            return period == QuestPeriod.Daily ? QuestCalendar.TimeToNextDay(now) : QuestCalendar.TimeToNextWeek(now);
        }

        public bool HasClaimable
        {
            get
            {
                Refresh();
                foreach (var period in Periods)
                {
                    foreach (var slot in Slots(period))
                    {
                        if (slot.CanClaim)
                            return true;
                    }
                }

                return false;
            }
        }

        public int RerollsLeft
        {
            get
            {
                Refresh();
                return Math.Max(0, _config.QuestRerollsPerDay - Data.Rerolls);
            }
        }

        public bool CanReroll(QuestSlot slot)
        {
            return slot != null && slot.Period == QuestPeriod.Daily && !slot.IsDone && !slot.Quest.Guaranteed && RerollsLeft > 0 &&
                   Candidates(QuestPeriod.Daily).Count > Data.Daily.Count;
        }

        public bool Reroll(QuestSlot slot)
        {
            if (!CanReroll(slot))
                return false;

            var data = Data;
            var candidates = Candidates(QuestPeriod.Daily);
            var exclude = new HashSet<int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                foreach (var entry in data.Daily)
                {
                    if (entry.Id == candidates[i].Id)
                        exclude.Add(i);
                }
            }

            // Группы остальных заданий набора заняты: замена не даёт второе похожее задание.
            var usedGroups = new HashSet<string>();
            foreach (var other in Slots(QuestPeriod.Daily))
            {
                if (other.Entry != slot.Entry && !string.IsNullOrEmpty(other.Quest.Group))
                    usedGroups.Add(other.Quest.Group);
            }

            var picked = QuestPicker.Pick(Weights(candidates), 1, QuestPicker.Seed(data.Day, RerollSalt + data.Rerolls), exclude,
                Groups(candidates), usedGroups);
            if (picked.Count == 0)
                return false;

            var index = data.Daily.IndexOf(slot.Entry);
            if (index < 0)
                return false;

            data.Daily[index] = new QuestEntry { Id = candidates[picked[0]].Id };
            data.Rerolls++;
            _saves.RequestSave();
            Changed?.Invoke();
            return true;
        }

        public List<GrantedReward> Claim(QuestSlot slot)
        {
            if (slot == null || !slot.CanClaim)
                return new List<GrantedReward>();

            slot.Entry.Claimed = true;
            var granted = _granter.Grant(slot.Quest.Rewards);
            if (slot.Period == QuestPeriod.Daily)
                Apply(new QuestEvent { Kind = QuestEventKind.DailyQuestClaimed });

            _saves.RequestSave();
            Changed?.Invoke();
            return granted;
        }

        public void Report(QuestEvent e)
        {
            if (e == null)
                return;

            Refresh();
            if (Apply(e))
            {
                _saves.RequestSave();
                Changed?.Invoke();
            }
        }

        public List<QuestConfig> TakeCompleted()
        {
            var result = new List<QuestConfig>(_completed);
            _completed.Clear();
            return result;
        }

        /// <summary>Новый день или неделя: выдать новый набор; первый заход за день продвигает недельное задание.</summary>
        private void Refresh()
        {
            if (_refreshing)
                return;

            _refreshing = true;
            try
            {
                var now = _clock.LocalNow;
                var day = QuestCalendar.DayIndex(now);
                var week = QuestCalendar.WeekIndex(now);
                var data = Data;
                var changed = false;

                // Часы могли уйти назад: задания остаются прежними, новый набор только на следующий день.
                if (day > data.Day)
                {
                    data.Day = day;
                    data.Rerolls = 0;
                    data.Daily = NewSet(QuestPeriod.Daily, _config.DailyQuestCount, QuestPicker.Seed(day, DailySalt));
                    changed = true;
                }

                if (week > data.Week)
                {
                    data.Week = week;
                    data.Weekly = NewSet(QuestPeriod.Weekly, _config.WeeklyQuestCount, QuestPicker.Seed(week, WeeklySalt));
                    changed = true;
                }

                if (day > data.LastVisitDay)
                {
                    data.LastVisitDay = day;
                    Apply(new QuestEvent { Kind = QuestEventKind.DayVisited });
                    changed = true;
                }

                if (changed)
                {
                    _saves.RequestSave();
                    Changed?.Invoke();
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>Продвинуть задания событием. true, если что-то изменилось.</summary>
        private bool Apply(QuestEvent e)
        {
            var changed = false;
            foreach (var period in Periods)
            {
                foreach (var slot in Slots(period))
                {
                    var goal = slot.Quest.Goal;
                    if (goal == null || slot.IsDone || (e.HotSeat && !goal.CountHotSeat))
                        continue;

                    var delta = goal.Progress(e);
                    if (delta <= 0)
                        continue;

                    slot.Entry.Progress = Math.Min(slot.Target, slot.Entry.Progress + delta);
                    changed = true;
                    if (slot.IsDone)
                        _completed.Add(slot.Quest);
                }
            }

            return changed;
        }

        // Выданные задания периода: строятся заново, только когда поменялся список в сохранении (новый день, замена).
        // Report вызывается на каждое отложенное и забранное, поэтому без новых объектов на каждый вызов.
        private readonly List<QuestSlot>[] _slots = { new List<QuestSlot>(), new List<QuestSlot>() };
        private readonly List<QuestEntry>[] _slotsSource = new List<QuestEntry>[2];
        private readonly QuestEntry[][] _slotsEntries = { new QuestEntry[0], new QuestEntry[0] };

        private List<QuestSlot> Slots(QuestPeriod period)
        {
            var data = Data;
            var entries = period == QuestPeriod.Daily ? data.Daily : data.Weekly;
            var index = (int)period;
            if (!IsSameEntries(index, entries))
            {
                var result = _slots[index];
                result.Clear();
                foreach (var entry in entries)
                {
                    var quest = _content.Get<QuestConfig>(entry.Id);
                    if (quest != null)
                        result.Add(new QuestSlot(quest, entry, period));
                }

                _slotsSource[index] = entries;
                _slotsEntries[index] = entries.ToArray();
            }

            return _slots[index];
        }

        private bool IsSameEntries(int index, List<QuestEntry> entries)
        {
            var known = _slotsEntries[index];
            if (!ReferenceEquals(_slotsSource[index], entries) || known.Length != entries.Count)
                return false;

            for (var i = 0; i < known.Length; i++)
            {
                if (!ReferenceEquals(known[i], entries[i]))
                    return false;
            }

            return true;
        }

        /// <summary>Набор периода: обязательные задания (по ID), затем по весу из остальных, по одному из группы.</summary>
        private List<QuestEntry> NewSet(QuestPeriod period, int count, ulong seed)
        {
            var candidates = Candidates(period);
            var guaranteed = candidates.ConvertAll(q => q.Guaranteed);
            var result = new List<QuestEntry>();
            foreach (var index in QuestPicker.PickSet(Weights(candidates), guaranteed, Groups(candidates), count, seed))
                result.Add(new QuestEntry { Id = candidates[index].Id });
            return result;
        }

        private static List<string> Groups(List<QuestConfig> candidates)
        {
            return candidates.ConvertAll(q => q.Group);
        }

        /// <summary>Задания периода в постоянном порядке (по ID): выбор по зерну не зависит от порядка ассетов.</summary>
        private List<QuestConfig> Candidates(QuestPeriod period)
        {
            var result = _content.All<QuestConfig>().FindAll(q => q.Period == period && q.Goal != null && !string.IsNullOrEmpty(q.Id));
            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        /// <summary>Вес кандидата; 0 — задание сейчас выдать нельзя (цель недоступна или условия не выполнены).</summary>
        private double[] Weights(List<QuestConfig> candidates)
        {
            var context = Context;
            var weights = new double[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                var quest = candidates[i];
                if (!quest.IsAvailable(context))
                    continue;

                // Обязательное задание выдаётся и с нулевым весом; в выборку по весу оно не попадает (уже взято).
                weights[i] = quest.Guaranteed ? Math.Max(quest.Weight, 1f) : quest.Weight;
            }

            return weights;
        }
    }
}
