using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zenject;
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

        /// <summary>Сильный соперник (выбран на развилке): труднее, за победу — редкая находка и больше наград.</summary>
        public bool IsElite;

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

        /// <summary>Фора игрока (находки): очки на счету с начала партии.</summary>
        public int PlayerStartScore;

        /// <summary>Заряды спасения от Зонка на эту партию: общий запас забега и бесплатные (талисман).</summary>
        public int PlayerZonkSaves;

        /// <summary>Из них бесплатные на эту партию (не копятся, тратятся первыми).</summary>
        public int FreeZonkSaves;
    }

    /// <summary>Что дала победа на этаже.</summary>
    public sealed class RunWin
    {
        public int Floor;
        public bool Guardian;
        public bool Elite;
        public bool NewRecord;
        public int HeartsGained;
        public int Tokens;
        public readonly List<Reward> Rewards = new List<Reward>();

        /// <summary>Рубежи, впервые достигнутые этой победой.</summary>
        public readonly List<int> Milestones = new List<int>();

        /// <summary>Награды рубежей (их можно удвоить за рекламу).</summary>
        public readonly List<Reward> MilestoneRewards = new List<Reward>();
    }

    /// <summary>Как кончился проигрыш на этаже.</summary>
    public enum RunLoss
    {
        /// <summary>Минус сердце, сердца ещё есть.</summary>
        HeartLost,

        /// <summary>Щит принял удар: сердце цело.</summary>
        Shielded,

        /// <summary>Сердца кончились, «Второе дыхание» вернуло одно.</summary>
        SecondWind,

        /// <summary>Сердец нет: продолжить за рекламу или конец.</summary>
        Out,
    }

    /// <summary>Путь на развилке перед этажом.</summary>
    public enum RunPath
    {
        Normal = 0,
        Elite = 1,
        Rest = 2,
        Shop = 3,
    }

    /// <summary>
    /// Прогресс «Бесконечного забега» без окон: собрать этаж, победа, поражение, находки, развилки, лавка, продолжение
    /// за рекламу. Всё случайное выводится из сида забега и номера этажа (EndlessRunPlan.FloorRandom): перезапуск игры
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
        private readonly ITalents _talents;
        private readonly List<int> _ruleBuffer = new List<int>();
        private readonly RunPerkContext _context = new RunPerkContext();
        private readonly List<RunPerkEntry> _candidates = new List<RunPerkEntry>();
        private List<RunPerkEntry> _legacyEntries;

        public EndlessRunProgress(ISaveStore saves, GameConfig config, ContentDatabase content, [InjectOptional] ITalents talents = null)
        {
            _saves = saves;
            _config = config;
            _content = content;
            _talents = talents;
        }

        public EndlessRunConfig Config => _config != null ? _config.EndlessRun : null;
        public bool IsConfigured => Config != null && (Config.Opponents.Count > 0 || Config.Guardians.Count > 0);
        private EndlessRunSave Data => _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun);

        public bool IsActive => Data.Active;
        public int Floor => Data.Floor;
        public int Hearts => Data.Hearts;
        public int Best => Data.Best;
        public int Shields => Data.Shields;
        public int ZonkSaves => Data.ZonkSaves;
        public int Tokens => Data.Tokens;
        public int FreeRerolls => Data.FreeRerolls;
        public IReadOnlyList<RunOfferSave> Offers => Data.Offers;
        public IReadOnlyList<RunComboSave> Combos => Data.Combos;
        public IReadOnlyList<RunPerkStack> Perks => Data.Perks;

        /// <summary>Сейчас открыта лавка: предложения с ценой.</summary>
        public bool IsShopOpen => Data.Offers.Count > 0 && Data.Offers[0].Price > 0;

        /// <summary>Сколько раз уже продолжали за рекламу (старое сохранение: ReviveUsed — как 1).</summary>
        private int RevivesUsed => Math.Max(Data.RevivesUsed, Data.ReviveUsed ? 1 : 0);

        public bool CanRevive
        {
            get
            {
                var config = Config;
                var data = Data;
                if (config == null || !config.ReviveForAd || !data.Active || data.Hearts > 0)
                    return false;
                var used = RevivesUsed;
                return used < Math.Max(1, config.MaxRevives) && (used == 0 || data.Floor >= config.DeepReviveFloor);
            }
        }

        public int MaxHearts => Config != null ? Config.MaxHearts + Talent(TalentEffect.RunStartHearts) : 5;

        /// <summary>ID особых костей игрока в забеге по слотам, пустая строка — обычная.</summary>
        public IReadOnlyList<string> DiceIds => Data.Dice;

        /// <summary>Пул находок: из конфига, а если он пуст — старые три (очки, кость, сердце).</summary>
        public IReadOnlyList<RunPerkEntry> Entries
        {
            get
            {
                var config = Config;
                if (config.Perks.Count > 0)
                    return config.Perks;
                return _legacyEntries ?? (_legacyEntries = new List<RunPerkEntry>
                {
                    new RunPerkEntry { Id = "combo", Weight = config.ComboWeight, MaxStacks = 99, Perk = new ComboPerk { Step = config.ComboStep } },
                    new RunPerkEntry { Id = "die", Weight = config.DieWeight, MaxStacks = 6, Tactic = RunTactic.Dice, Perk = new SpecialDiePerk() },
                    new RunPerkEntry { Id = "heart", Weight = config.HeartWeight, MaxStacks = 99, Perk = new HeartPerk() },
                });
            }
        }

        public RunPerkEntry FindEntry(string id)
        {
            foreach (var entry in Entries)
            {
                if (entry != null && entry.Id == id)
                    return entry;
            }

            return null;
        }

        /// <summary>Находка предложения: по PerkId, у старых сохранений — по Kind.</summary>
        public RunPerkEntry EntryOf(RunOfferSave offer)
        {
            if (!string.IsNullOrEmpty(offer.PerkId))
                return FindEntry(offer.PerkId);
            var legacy = offer.Kind == RunOfferKind.Combo ? typeof(ComboPerk) : offer.Kind == RunOfferKind.Die ? typeof(SpecialDiePerk) : typeof(HeartPerk);
            foreach (var entry in Entries)
            {
                if (entry != null && entry.Perk != null && entry.Perk.GetType() == legacy)
                    return entry;
            }

            return null;
        }

        public RunPerkContext Context
        {
            get
            {
                _context.Data = Data;
                _context.Config = Config;
                _context.Content = _content;
                _context.MaxHearts = MaxHearts;
                return _context;
            }
        }

        public int StacksOf(RunPerkEntry entry) => entry != null ? Context.Stacks(entry.Id) : 0;

        /// <summary>Новый забег: кости — набор игрока (dice, null — обычная), сердца, этаж 1. Прежний забег заканчивается.</summary>
        public void StartNew(IReadOnlyList<DieConfig> dice, ulong seed)
        {
            var data = Data;
            data.Active = true;
            data.Floor = 1;
            data.Hearts = Math.Max(1, Config.StartHearts + Talent(TalentEffect.RunStartHearts));
            data.ReviveUsed = false;
            data.RevivesUsed = 0;
            data.Seed = unchecked((long)seed);
            data.Combos.Clear();
            data.Offers.Clear();
            data.Dice.Clear();
            data.Perks.Clear();
            data.Shields = 0;
            data.ZonkSaves = 0;
            data.Tokens = 0;
            data.SecondWindUsed = false;
            data.PathFloor = 0;
            data.Path = 0;
            data.FreeRerolls = Talent(TalentEffect.RunFreeRerolls);
            ResetOfferFlags(data);
            for (var i = 0; i < Core.Match.ZonkMatch.DiceCount; i++)
            {
                var die = dice != null && i < dice.Count ? dice[i] : null;
                data.Dice.Add(die != null && die.IsSpecial ? die.Id : string.Empty);
            }

            data.Runs++;

            // Талант «стартовая находка»: выбор до первого этажа.
            if (Talent(TalentEffect.RunStartPerk) > 0)
                MakeOffers(0, false);
            _saves.RequestSave();
        }

        // ---------- Развилки ----------

        /// <summary>Перед этим этажом нужно выбрать путь (со второго этажа, один раз на этаж).</summary>
        public bool NeedsPath => Data.Active && Data.Floor > 1 && Data.PathFloor != Data.Floor;

        public RunPath CurrentPath => Data.PathFloor == Data.Floor ? (RunPath)Data.Path : RunPath.Normal;

        /// <summary>Пути на выбор: обычный и сильный всегда, привал или лавка — каждые CampEvery этажей по очереди.</summary>
        public void PathOptions(List<RunPath> result)
        {
            result.Clear();
            result.Add(RunPath.Normal);
            result.Add(RunPath.Elite);
            var config = Config;
            var step = Data.Floor - 1;
            if (config.CampEvery <= 0 || step <= 0 || step % config.CampEvery != 0)
                return;

            var shop = (step / config.CampEvery) % 2 == 0 || Data.Hearts >= MaxHearts;
            result.Add(shop ? RunPath.Shop : RunPath.Rest);
        }

        /// <summary>Выбрать путь. Привал — сердце сразу, лавка — предложения с ценой (EndlessRunState открывает).</summary>
        public void ChoosePath(RunPath path)
        {
            var data = Data;
            data.PathFloor = data.Floor;
            data.Path = (int)path;
            if (path == RunPath.Rest)
                data.Hearts = Math.Min(MaxHearts, data.Hearts + 1);
            else if (path == RunPath.Shop)
                MakeShopOffers();
            _saves.RequestSave();
        }

        // ---------- Этаж ----------

        /// <summary>Собрать текущий этаж для партии.</summary>
        public RunFloor BuildFloor()
        {
            var config = Config;
            var plan = config.Plan;
            var data = Data;
            var seed = unchecked((ulong)data.Seed);
            var floor = Math.Max(1, data.Floor);
            var elite = CurrentPath == RunPath.Elite;
            var result = new RunFloor
            {
                Floor = floor,
                Target = plan.Target(floor),
                IsGuardian = plan.IsGuardian(floor) && config.Guardians.Count > 0,
                IsElite = elite,
                EnemyPower = plan.EnemyPower(floor) + (elite ? config.EliteEnemyPower : 0f),
            };
            if (elite)
                result.Target = (int)Math.Round(result.Target * (100 + config.EliteTargetPercent) / 100.0 / 50.0) * 50;

            // Лицо врага: стражи по кругу, обычные — случайно из списка.
            var random = EndlessRunPlan.FloorRandom(seed, floor, 1);
            if (result.IsGuardian)
                result.Opponent = config.Guardians[(floor / plan.GuardianEvery - 1) % config.Guardians.Count];
            else if (config.Opponents.Count > 0)
                result.Opponent = config.Opponents[(int)(random.NextDouble() * config.Opponents.Count)];
            else
                result.Opponent = config.Guardians[0];

            // Кости врага: особых по плану (у стража на две больше, у сильного — EliteExtraDice), разные.
            var special = Math.Min(6, plan.EnemySpecialDice(floor) + (result.IsGuardian ? 2 : 0) + (elite ? config.EliteExtraDice : 0));
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

            var context = Context;
            foreach (var stack in data.Perks)
            {
                var entry = FindEntry(stack.Id);
                if (entry != null && entry.Perk != null && stack.Stacks > 0)
                    entry.Perk.ApplyToFloor(context, result, stack.Stacks);
            }

            foreach (var stack in data.Perks)
            {
                var entry = FindEntry(stack.Id);
                if (entry != null && entry.Perk != null && stack.Stacks > 0)
                    result.FreeZonkSaves += entry.Perk.FreeZonkSavesPerMatch(stack.Stacks);
            }

            result.PlayerZonkSaves = data.ZonkSaves + result.FreeZonkSaves;

            foreach (var id in data.Dice)
                result.PlayerDice.Add(string.IsNullOrEmpty(id) ? null : _content.Get<DieConfig>(id));

            return result;
        }

        /// <summary>
        /// Партия сыграна: сколько зарядов спасения осталось у игрока. Сначала тратятся бесплатные (талисман), сверх них —
        /// общий запас забега.
        /// </summary>
        public void OnMatchPlayed(RunFloor floor, int zonkSavesLeft)
        {
            var data = Data;
            var used = Math.Max(0, floor.PlayerZonkSaves - zonkSavesLeft);
            data.ZonkSaves = Math.Max(0, data.ZonkSaves - Math.Max(0, used - floor.FreeZonkSaves));
            _saves.RequestSave();
        }

        /// <summary>Этаж пройден: награды, жетоны, сердце за стража, рекорд, рубежи, следующий этаж и находки на выбор.</summary>
        public RunWin OnWin()
        {
            var config = Config;
            var data = Data;
            var floor = Math.Max(1, data.Floor);
            var elite = CurrentPath == RunPath.Elite;
            var win = new RunWin
            {
                Floor = floor,
                Guardian = config.Plan.IsGuardian(floor) && config.Guardians.Count > 0,
                Elite = elite,
            };

            var coins = config.CoinsPerFloor + config.CoinsGrowthPerFloor * floor + (win.Guardian ? config.GuardianCoins : 0);
            if (elite)
                coins *= (100 + config.EliteCoinsPercent) / 100f;
            coins *= Multiplier(p => p.CoinMultiplier);
            var amount = (int)Math.Round(coins);
            if (amount > 0 && _config.Coins != null)
                win.Rewards.Add(new CurrencyReward { Currency = _config.Coins, Amount = amount });

            var tokens = (config.TokensPerFloor + floor / 10 + (elite ? config.EliteTokens : 0)) * Multiplier(p => p.TokenMultiplier);
            win.Tokens = (int)Math.Round(tokens);
            data.Tokens += win.Tokens;

            if (win.Guardian && config.HeartsPerGuardian > 0)
            {
                var before = data.Hearts;
                data.Hearts = Math.Min(Math.Max(MaxHearts, before), before + config.HeartsPerGuardian);
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
                    if (reward == null)
                        continue;
                    win.Rewards.Add(reward);
                    win.MilestoneRewards.Add(reward);
                }
            }

            data.Floor = floor + 1;
            MakeOffers(floor, elite);
            _saves.RequestSave();
            return win;
        }

        /// <summary>Этаж проигран: щит, минус сердце или «второе дыхание».</summary>
        public RunLoss OnLoss()
        {
            var data = Data;
            RunLoss loss;
            if (data.Shields > 0)
            {
                data.Shields--;
                loss = RunLoss.Shielded;
            }
            else
            {
                data.Hearts = Math.Max(0, data.Hearts - 1);
                loss = data.Hearts > 0 ? RunLoss.HeartLost : RunLoss.Out;
                if (loss == RunLoss.Out && !data.SecondWindUsed && HasSecondWind())
                {
                    data.SecondWindUsed = true;
                    data.Hearts = 1;
                    loss = RunLoss.SecondWind;
                }
            }

            _saves.RequestSave();
            return loss;
        }

        /// <summary>Продолжить за рекламу: одно сердце; до MaxRevives раз, второе и дальше — с DeepReviveFloor.</summary>
        public void Revive()
        {
            var data = Data;
            if (!CanRevive)
                return;

            data.RevivesUsed = RevivesUsed + 1;
            data.ReviveUsed = true;
            data.Hearts = 1;
            _saves.RequestSave();
        }

        public void EndRun()
        {
            var data = Data;
            data.Active = false;
            data.Offers.Clear();
            ResetOfferFlags(data);
            _saves.RequestSave();
        }

        // ---------- Находки ----------

        /// <summary>Взять находку из предложенных (после победы). За рекламу можно взять ещё одну из оставшихся.</summary>
        public void Choose(int index)
        {
            var data = Data;
            if (index < 0 || index >= data.Offers.Count)
                return;

            var offer = data.Offers[index];
            Take(offer);
            if (data.OffersExtraPending)
            {
                data.OffersExtraPending = false;
                data.Offers.RemoveAt(index);
                RemoveUnavailableOffers();
            }
            else
            {
                data.Offers.Clear();
                ResetOfferFlags(data);
            }

            _saves.RequestSave();
        }

        /// <summary>Купить в лавке за жетоны. false — не хватает жетонов.</summary>
        public bool Buy(int index)
        {
            var data = Data;
            if (index < 0 || index >= data.Offers.Count || data.Offers[index].Price <= 0 || data.Tokens < data.Offers[index].Price)
                return false;

            var offer = data.Offers[index];
            data.Tokens -= offer.Price;
            Take(offer);
            data.Offers.RemoveAt(index);
            RemoveUnavailableOffers();
            _saves.RequestSave();
            return true;
        }

        public void LeaveShop()
        {
            var data = Data;
            data.Offers.Clear();
            data.Path = (int)RunPath.Normal;
            _saves.RequestSave();
        }

        /// <summary>Можно взять ещё одну находку за рекламу (раз на выбор, пока больше одной на выбор).</summary>
        public bool CanTakeExtra => !Data.OffersExtraUsed && !IsShopOpen && Data.Offers.Count > 1;

        /// <summary>Реклама просмотрена: после выбора можно взять ещё одну из оставшихся.</summary>
        public void GrantExtraPick()
        {
            var data = Data;
            data.OffersExtraUsed = true;
            data.OffersExtraPending = true;
            _saves.RequestSave();
        }

        /// <summary>Поменять можно: бесплатно (таланты) или раз на выбор за рекламу.</summary>
        public bool CanRerollForAd => !Data.OffersRerolled && !IsShopOpen && Data.Offers.Count > 0;

        public bool CanRerollFree => Data.FreeRerolls > 0 && !IsShopOpen && Data.Offers.Count > 0;

        /// <summary>Новые находки на выбор. free — бесплатный переброс (талант), иначе за рекламу (раз на выбор).</summary>
        public void RerollOffers(bool free)
        {
            var data = Data;
            if (free)
                data.FreeRerolls = Math.Max(0, data.FreeRerolls - 1);
            else
                data.OffersRerolled = true;
            data.OfferSalt++;
            var pending = data.OffersExtraPending;
            MakeOffers(data.Floor - 1, false, keepFlags: true);
            data.OffersExtraPending = pending;
            _saves.RequestSave();
        }

        /// <summary>Множитель очков категории у игрока (1 — без находок).</summary>
        public float ComboMultiplier(string category)
        {
            var combo = Data.Combos.Find(c => c.Category == category);
            return combo != null ? combo.Multiplier : 1f;
        }

        private void Take(RunOfferSave offer)
        {
            var entry = EntryOf(offer);
            if (entry == null || entry.Perk == null)
                return;

            var data = Data;
            var stack = data.Perks.Find(p => p.Id == entry.Id);
            if (stack == null)
                data.Perks.Add(stack = new RunPerkStack { Id = entry.Id });
            stack.Stacks++;
            entry.Perk.OnTaken(Context, offer.Value, stack.Stacks);
        }

        /// <summary>Находки на выбор после победы на этаже floor (0 — стартовая): разные, только доступные.</summary>
        private void MakeOffers(int floor, bool elite, bool keepFlags = false)
        {
            var data = Data;
            if (!keepFlags)
            {
                ResetOfferFlags(data);
                data.OfferSalt = 0;
            }

            var count = Math.Max(1, Config.Offers) + Talent(TalentEffect.RunPerkOffersExtra);
            foreach (var stack in data.Perks)
            {
                var entry = FindEntry(stack.Id);
                if (entry != null && entry.Perk != null)
                    count += entry.Perk.ExtraOffers(stack.Stacks);
            }

            var random = EndlessRunPlan.FloorRandom(unchecked((ulong)data.Seed), floor, 3 + data.OfferSalt * 101);
            FillOffers(data.Offers, count, random, elite ? Rarity.Rare : Rarity.Common, 0);
        }

        /// <summary>Лавка: три находки с ценой по редкости.</summary>
        private void MakeShopOffers()
        {
            var data = Data;
            var random = EndlessRunPlan.FloorRandom(unchecked((ulong)data.Seed), data.Floor, 9);
            FillOffers(data.Offers, 3, random, Rarity.Common, 1);
        }

        /// <summary>
        /// Случайные разные находки по весам; первая — не ниже firstRarity (сильный соперник — редкая и лучше).
        /// priced: 1 — с ценой лавки.
        /// </summary>
        private void FillOffers(List<RunOfferSave> offers, int count, Core.Dice.IRandom random, Rarity firstRarity, int priced)
        {
            offers.Clear();
            var context = Context;
            for (var pick = 0; pick < count; pick++)
            {
                _candidates.Clear();
                var total = 0;
                foreach (var entry in Entries)
                {
                    if (entry == null || entry.Perk == null || entry.Weight <= 0)
                        continue;
                    if (pick == 0 && entry.Rarity < firstRarity)
                        continue;
                    if (context.Stacks(entry.Id) >= entry.MaxStacks || !entry.Perk.IsAvailable(context))
                        continue;
                    if (offers.Exists(o => o.PerkId == entry.Id))
                        continue;
                    _candidates.Add(entry);
                    total += entry.Weight;
                }

                if (total <= 0)
                {
                    if (pick == 0 && firstRarity > Rarity.Common)
                    {
                        firstRarity = Rarity.Common;
                        pick--;
                        continue;
                    }

                    break;
                }

                var roll = random.NextDouble() * total;
                var chosen = _candidates[_candidates.Count - 1];
                foreach (var candidate in _candidates)
                {
                    if (roll < candidate.Weight)
                    {
                        chosen = candidate;
                        break;
                    }

                    roll -= candidate.Weight;
                }

                var prices = Config.ShopPrices;
                var rarity = (int)chosen.Rarity;
                offers.Add(new RunOfferSave
                {
                    PerkId = chosen.Id,
                    Kind = LegacyKind(chosen),
                    Value = chosen.Perk.PickValue(context, random),
                    Price = priced > 0 ? (prices != null && rarity < prices.Length ? Math.Max(1, prices[rarity]) : 5) : 0,
                });
            }
        }

        private void RemoveUnavailableOffers()
        {
            var context = Context;
            Data.Offers.RemoveAll(o =>
            {
                var entry = EntryOf(o);
                return entry == null || entry.Perk == null || context.Stacks(entry.Id) >= entry.MaxStacks || !entry.Perk.IsAvailable(context);
            });
        }

        private static RunOfferKind LegacyKind(RunPerkEntry entry)
        {
            return entry.Perk is SpecialDiePerk ? RunOfferKind.Die : entry.Perk is HeartPerk ? RunOfferKind.Heart : RunOfferKind.Combo;
        }

        private static void ResetOfferFlags(EndlessRunSave data)
        {
            data.OffersRerolled = false;
            data.OffersExtraUsed = false;
            data.OffersExtraPending = false;
        }

        private bool HasSecondWind()
        {
            foreach (var stack in Data.Perks)
            {
                var entry = FindEntry(stack.Id);
                if (entry != null && entry.Perk != null && stack.Stacks > 0 && entry.Perk.SecondWind)
                    return true;
            }

            return false;
        }

        private float Multiplier(Func<RunPerk, Func<int, float>> pick)
        {
            var result = 1f;
            foreach (var stack in Data.Perks)
            {
                var entry = FindEntry(stack.Id);
                if (entry != null && entry.Perk != null && stack.Stacks > 0)
                    result *= pick(entry.Perk)(stack.Stacks);
            }

            return result;
        }

        private int Talent(TalentEffect effect)
        {
            return _talents != null ? (int)Math.Round(_talents.Value(effect)) : 0;
        }
    }
}
