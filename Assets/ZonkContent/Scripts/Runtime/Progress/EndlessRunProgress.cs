using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Modes;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Progress
{
    /// <summary>Этаж забега, собранный для партии: соперник, кости, ИИ, правила общие и личные, цель.</summary>
    public sealed class RunFloor
    {
        public int Floor;
        public int Target;
        public bool IsGuardian;

        /// <summary>Чьё лицо у врага: имя, портрет, стакан, реплики. Кости и ИИ — забега.</summary>
        public OpponentConfig Opponent;

        /// <summary>Кости врага по слотам, null — обычная.</summary>
        public readonly List<DieConfig> EnemyDice = new List<DieConfig>();

        public AiProfileConfig EnemyAi;

        /// <summary>Сила очков врага (множитель всех его комбинаций).</summary>
        public float EnemyPower;

        /// <summary>Общие правила на обоих: правила забега и правила стража.</summary>
        public readonly List<MatchModifier> SharedRules = new List<MatchModifier>();

        /// <summary>Правило, появившееся на этом этаже (показать крупно), или null.</summary>
        public MatchModifier NewRule;

        /// <summary>Личные правила игрока — его находки.</summary>
        public readonly List<MatchModifier> PlayerModifiers = new List<MatchModifier>();

        /// <summary>Личные правила врага — его сила на этаже.</summary>
        public readonly List<MatchModifier> EnemyModifiers = new List<MatchModifier>();

        /// <summary>Кости игрока в забеге по слотам, null — обычная.</summary>
        public readonly List<DieConfig> PlayerDice = new List<DieConfig>();
    }

    /// <summary>Что дала победа на этаже.</summary>
    public sealed class RunWin
    {
        public int Floor;
        public bool Guardian;
        public bool NewRecord;
        public int HeartsGained;
        public readonly List<Reward> Rewards = new List<Reward>();

        /// <summary>Рубежи, впервые достигнутые этой победой.</summary>
        public readonly List<int> Milestones = new List<int>();
    }

    /// <summary>
    /// Прогресс «Бесконечного забега» без окон: собрать этаж, победа, поражение, находки, продолжение за рекламу.
    /// Всё случайное выводится из сида забега и номера этажа (EndlessRunPlan.FloorRandom): перезапуск игры
    /// не меняет ни соперника, ни правила, ни предложенные находки.
    /// </summary>
    public sealed class EndlessRunProgress
    {
        /// <summary>Категории комбинаций для находок «очки комбинации».</summary>
        public static readonly string[] ComboCategories =
            { ComboCategory.Single, ComboCategory.OfAKind, ComboCategory.Straight, ComboCategory.ThreePairs };

        private readonly ISaveStore _saves;
        private readonly GameConfig _config;
        private readonly ContentDatabase _content;
        private readonly List<int> _ruleBuffer = new List<int>();

        public EndlessRunProgress(ISaveStore saves, GameConfig config, ContentDatabase content)
        {
            _saves = saves;
            _config = config;
            _content = content;
        }

        public EndlessRunConfig Config => _config != null ? _config.EndlessRun : null;
        public bool IsConfigured => Config != null && (Config.Opponents.Count > 0 || Config.Guardians.Count > 0);
        private EndlessRunSave Data => _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun);

        public bool IsActive => Data.Active;
        public int Floor => Data.Floor;
        public int Hearts => Data.Hearts;
        public int Best => Data.Best;
        public bool CanRevive => Config != null && Config.ReviveForAd && Data.Active && !Data.ReviveUsed && Data.Hearts <= 0;
        public IReadOnlyList<RunOfferSave> Offers => Data.Offers;
        public IReadOnlyList<RunComboSave> Combos => Data.Combos;

        /// <summary>Новый забег: кости — набор игрока (dice, null — обычная), сердца, этаж 1. Прежний забег заканчивается.</summary>
        public void StartNew(IReadOnlyList<DieConfig> dice, ulong seed)
        {
            var data = Data;
            data.Active = true;
            data.Floor = 1;
            data.Hearts = Math.Max(1, Config.StartHearts);
            data.ReviveUsed = false;
            data.Seed = unchecked((long)seed);
            data.Combos.Clear();
            data.Offers.Clear();
            data.Dice.Clear();
            for (var i = 0; i < Core.Match.ZonkMatch.DiceCount; i++)
            {
                var die = dice != null && i < dice.Count ? dice[i] : null;
                data.Dice.Add(die != null && die.IsSpecial ? die.Id : string.Empty);
            }

            data.Runs++;
            _saves.RequestSave();
        }

        /// <summary>Собрать текущий этаж для партии.</summary>
        public RunFloor BuildFloor()
        {
            var config = Config;
            var plan = config.Plan;
            var data = Data;
            var seed = unchecked((ulong)data.Seed);
            var floor = Math.Max(1, data.Floor);
            var result = new RunFloor
            {
                Floor = floor,
                Target = plan.Target(floor),
                IsGuardian = plan.IsGuardian(floor) && config.Guardians.Count > 0,
                EnemyPower = plan.EnemyPower(floor),
            };

            // Лицо врага: стражи по кругу, обычные — случайно из списка.
            var random = EndlessRunPlan.FloorRandom(seed, floor, 1);
            if (result.IsGuardian)
                result.Opponent = config.Guardians[(floor / plan.GuardianEvery - 1) % config.Guardians.Count];
            else if (config.Opponents.Count > 0)
                result.Opponent = config.Opponents[(int)(random.NextDouble() * config.Opponents.Count)];
            else
                result.Opponent = config.Guardians[0];

            // Кости врага: особых по плану (у стража на две больше), разные.
            var special = Math.Min(6, plan.EnemySpecialDice(floor) + (result.IsGuardian ? 2 : 0));
            var diceRandom = EndlessRunPlan.FloorRandom(seed, floor, 2);
            var pool = new List<DieConfig>(config.SpecialDice);
            pool.RemoveAll(d => d == null);
            for (var slot = 0; slot < Core.Match.ZonkMatch.DiceCount; slot++)
            {
                DieConfig die = null;
                if (slot < special && pool.Count > 0)
                {
                    var index = (int)(diceRandom.NextDouble() * pool.Count);
                    die = pool[index];
                    pool.RemoveAt(index);
                }

                result.EnemyDice.Add(die);
            }

            var tier = plan.AiTier(floor);
            result.EnemyAi = tier == 0 ? config.AiEarly : tier == 1 ? config.AiMid : config.AiLate;
            if (result.EnemyAi == null)
                result.EnemyAi = result.Opponent != null ? result.Opponent.Ai : null;

            // Общие правила забега на этом этаже и правила стража.
            plan.ActiveRules(seed, floor, config.RulePool.Count, _ruleBuffer);
            foreach (var index in _ruleBuffer)
            {
                var rule = config.RulePool[index];
                if (rule != null)
                    result.SharedRules.Add(rule);
            }

            if (plan.AddsRule(floor) && _ruleBuffer.Count > 0)
                result.NewRule = config.RulePool[_ruleBuffer[_ruleBuffer.Count - 1]];

            if (result.IsGuardian && result.Opponent != null)
            {
                foreach (var rule in result.Opponent.Modifiers)
                {
                    if (rule != null)
                        result.SharedRules.Add(rule);
                }
            }

            // Сила врага и находки игрока — личные правила каждого.
            foreach (var category in ComboCategories)
                result.EnemyModifiers.Add(new ComboMultiplierModifier { Category = category, Multiplier = result.EnemyPower });
            foreach (var combo in data.Combos)
            {
                if (combo != null && Math.Abs(combo.Multiplier - 1f) > 0.001f)
                    result.PlayerModifiers.Add(new ComboMultiplierModifier { Category = combo.Category, Multiplier = combo.Multiplier });
            }

            foreach (var id in data.Dice)
                result.PlayerDice.Add(string.IsNullOrEmpty(id) ? null : _content.Get<DieConfig>(id));

            return result;
        }

        /// <summary>Этаж пройден: награды, сердце за стража, рекорд, рубежи, следующий этаж и находки на выбор.</summary>
        public RunWin OnWin()
        {
            var config = Config;
            var data = Data;
            var floor = Math.Max(1, data.Floor);
            var win = new RunWin { Floor = floor, Guardian = config.Plan.IsGuardian(floor) && config.Guardians.Count > 0 };

            var coins = config.CoinsPerFloor + (win.Guardian ? config.GuardianCoins : 0);
            if (coins > 0 && _config.Coins != null)
                win.Rewards.Add(new CurrencyReward { Currency = _config.Coins, Amount = coins });

            if (win.Guardian && config.HeartsPerGuardian > 0)
            {
                var before = data.Hearts;
                data.Hearts = Math.Min(Math.Max(config.MaxHearts, before), before + config.HeartsPerGuardian);
                win.HeartsGained = data.Hearts - before;
            }

            if (floor > data.Best)
            {
                data.Best = floor;
                win.NewRecord = true;
            }

            foreach (var milestone in config.Milestones)
            {
                if (milestone == null || milestone.Floor != floor || data.Claimed.Contains(milestone.Floor))
                    continue;

                data.Claimed.Add(milestone.Floor);
                win.Milestones.Add(milestone.Floor);
                foreach (var reward in milestone.Rewards)
                {
                    if (reward != null)
                        win.Rewards.Add(reward);
                }
            }

            data.Floor = floor + 1;
            MakeOffers(floor);
            _saves.RequestSave();
            return win;
        }

        /// <summary>Этаж проигран: минус сердце. Возвращает, сколько сердец осталось (0 — забег кончается или продолжение за рекламу).</summary>
        public int OnLoss()
        {
            var data = Data;
            data.Hearts = Math.Max(0, data.Hearts - 1);
            _saves.RequestSave();
            return data.Hearts;
        }

        /// <summary>Продолжить за рекламу: одно сердце, один раз за забег.</summary>
        public void Revive()
        {
            var data = Data;
            if (!CanRevive)
                return;

            data.Hearts = 1;
            data.ReviveUsed = true;
            _saves.RequestSave();
        }

        public void EndRun()
        {
            var data = Data;
            data.Active = false;
            data.Offers.Clear();
            _saves.RequestSave();
        }

        /// <summary>Взять находку из предложенных.</summary>
        public void Choose(int index)
        {
            var config = Config;
            var data = Data;
            if (index < 0 || index >= data.Offers.Count)
                return;

            var offer = data.Offers[index];
            switch (offer.Kind)
            {
                case RunOfferKind.Combo:
                    var combo = data.Combos.Find(c => c.Category == offer.Value);
                    if (combo == null)
                        data.Combos.Add(combo = new RunComboSave { Category = offer.Value, Multiplier = 1f });
                    combo.Multiplier += config.ComboStep;
                    break;
                case RunOfferKind.Die:
                    var slot = data.Dice.FindIndex(string.IsNullOrEmpty);
                    if (slot >= 0 && !data.Dice.Contains(offer.Value))
                        data.Dice[slot] = offer.Value;
                    break;
                case RunOfferKind.Heart:
                    data.Hearts = Math.Min(config.MaxHearts, data.Hearts + 1);
                    break;
            }

            data.Offers.Clear();
            _saves.RequestSave();
        }

        /// <summary>Множитель очков категории у игрока (1 — без находок).</summary>
        public float ComboMultiplier(string category)
        {
            var combo = Data.Combos.Find(c => c.Category == category);
            return combo != null ? combo.Multiplier : 1f;
        }

        /// <summary>Находки на выбор после победы на этаже floor: разные, только те, что можно взять.</summary>
        private void MakeOffers(int floor)
        {
            var config = Config;
            var data = Data;
            data.Offers.Clear();
            var random = EndlessRunPlan.FloorRandom(unchecked((ulong)data.Seed), floor, 3);

            var freeDice = new List<DieConfig>();
            if (data.Dice.Exists(string.IsNullOrEmpty))
            {
                foreach (var die in config.SpecialDice)
                {
                    if (die != null && !data.Dice.Contains(die.Id))
                        freeDice.Add(die);
                }
            }

            for (var attempt = 0; attempt < 30 && data.Offers.Count < Math.Max(1, config.Offers); attempt++)
            {
                var comboWeight = Math.Max(0, config.ComboWeight);
                var dieWeight = freeDice.Count > 0 ? Math.Max(0, config.DieWeight) : 0;
                var heartWeight = data.Hearts < config.MaxHearts ? Math.Max(0, config.HeartWeight) : 0;
                var total = comboWeight + dieWeight + heartWeight;
                if (total <= 0)
                    break;

                var roll = random.NextDouble() * total;
                RunOfferSave offer;
                if (roll < comboWeight)
                {
                    offer = new RunOfferSave
                    {
                        Kind = RunOfferKind.Combo,
                        Value = ComboCategories[(int)(random.NextDouble() * ComboCategories.Length)],
                    };
                }
                else if (roll < comboWeight + dieWeight)
                {
                    offer = new RunOfferSave { Kind = RunOfferKind.Die, Value = freeDice[(int)(random.NextDouble() * freeDice.Count)].Id };
                }
                else
                {
                    offer = new RunOfferSave { Kind = RunOfferKind.Heart };
                }

                if (!data.Offers.Exists(o => o.Kind == offer.Kind && o.Value == offer.Value))
                    data.Offers.Add(offer);
            }
        }
    }
}
