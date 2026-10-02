using System.Collections.Generic;
using System.Threading;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Progress;
using Object = UnityEngine.Object;

namespace Zonk.Tests
{
    /// <summary>Логика «Бесконечного забега» и башни: этажи, сердца, находки, рубежи, награды, сохранение.</summary>
    public sealed class ModeProgressTests
    {
        private sealed class MemorySaves : ISaveStore
        {
            private readonly Dictionary<string, object> _sections = new Dictionary<string, object>();

            public bool IsLoaded => true;
            public bool IsCloudWriteBlocked => false;
            public UniTask LoadAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask WaitLoadedAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

            public T Get<T>(string key) where T : class, new()
            {
                if (!_sections.TryGetValue(key, out var section))
                    _sections[key] = section = new T();
                return (T)section;
            }

            public int GetVersion(string key) => 1;
            public void RequestSave() { }
            public UniTask SaveNowAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        }

        private readonly List<Object> _created = new List<Object>();
        private MemorySaves _saves;
        private GameConfig _config;
        private ContentDatabase _database;
        private EndlessRunConfig _run;
        private TowerConfig _tower;
        private DieConfig _lucky;
        private DieConfig _sixes;
        private OpponentConfig _regular;
        private OpponentConfig _guardian;

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _created.Add(asset);
            return asset;
        }

        private DieConfig Die(string id, bool special)
        {
            var die = Create<DieConfig>(id);
            die.Id = id;
            die.Weights = special ? new[] { 1.3f, 1f, 1f, 1f, 0.6f, 1f } : new[] { 1f, 1f, 1f, 1f, 1f, 1f };
            // Особая кость отличается меткой (DieConfig.IsSpecial).
            die.MarkerColor = special ? Color.red : Color.clear;
            return die;
        }

        [SetUp]
        public void SetUp()
        {
            _saves = new MemorySaves();
            _config = Create<GameConfig>("GameConfig");
            _config.Coins = Create<CurrencyConfig>("Coins");
            _config.StandardDie = Die("standard", false);
            _lucky = Die("lucky", true);
            _sixes = Die("sixes", true);

            _regular = Create<OpponentConfig>("regular");
            _regular.Id = "regular";
            _guardian = Create<OpponentConfig>("guardian");
            _guardian.Id = "guardian";
            _guardian.IsBoss = true;
            _guardian.Modifiers.Add(new MinBankModifier { MinBankScore = 350 });

            _run = Create<EndlessRunConfig>("EndlessRun");
            _run.Opponents.Add(_regular);
            _run.Guardians.Add(_guardian);
            _run.SpecialDice.Add(_lucky);
            _run.SpecialDice.Add(_sixes);
            _run.RulePool.Add(new ZonkPenaltyModifier { Penalty = 200 });
            _run.RulePool.Add(new ComboMultiplierModifier { Category = ComboCategory.Straight, Multiplier = 2f });
            _run.Milestones.Add(new RunMilestone { Floor = 2, Rewards = new List<Reward> { new CurrencyReward { Currency = _config.Coins, Amount = 100 } } });
            _config.EndlessRun = _run;

            _tower = Create<TowerConfig>("Tower");
            _tower.CheckpointEvery = 2;
            _tower.HeartsPerAttempt = 2;
            for (var i = 0; i < 4; i++)
            {
                _tower.Floors.Add(new TowerFloor
                {
                    Opponent = _regular,
                    Target = 2000 + i * 100,
                    FirstClearRewards = new List<Reward> { new CurrencyReward { Currency = _config.Coins, Amount = 50 } },
                });
            }

            _config.Tower = _tower;

            _database = Create<ContentDatabase>("Database");
            _database.EditorSetItems(new List<ContentConfig> { _lucky, _sixes, _config.StandardDie, _regular, _guardian });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        private EndlessRunProgress NewRun() => new EndlessRunProgress(_saves, _config, _database);

        [Test]
        public void RunStartsWithHeartsAndOwnSpecialDice()
        {
            var run = NewRun();
            run.StartNew(new[] { _lucky, null, null, _config.StandardDie, null, null }, 42);

            Assert.IsTrue(run.IsActive);
            Assert.AreEqual(1, run.Floor);
            Assert.AreEqual(_run.StartHearts, run.Hearts);
            var floor = run.BuildFloor();
            Assert.AreEqual(_lucky, floor.PlayerDice[0], "own special die stays");
            Assert.IsNull(floor.PlayerDice[3], "standard die is stored as empty slot");
        }

        [Test]
        public void FloorIsTheSameAfterRestart()
        {
            var run = NewRun();
            run.StartNew(null, 7);
            for (var i = 0; i < 4; i++)
                run.OnWin();

            var first = run.BuildFloor();
            var again = NewRun().BuildFloor();
            Assert.AreEqual(first.Floor, again.Floor);
            Assert.AreEqual(first.Opponent, again.Opponent);
            CollectionAssert.AreEqual(first.EnemyDice, again.EnemyDice);
            CollectionAssert.AreEqual(first.SharedRules, again.SharedRules);
            Assert.AreEqual(1, first.SharedRules.Count, "floor 5 brings the first rule");
            Assert.AreSame(first.NewRule, first.SharedRules[0]);
        }

        [Test]
        public void GuardianFloorAddsItsRulesAndHeart()
        {
            var run = NewRun();
            run.StartNew(null, 1);
            for (var i = 0; i < 9; i++)
                run.OnWin();

            var floor = run.BuildFloor();
            Assert.AreEqual(10, floor.Floor);
            Assert.IsTrue(floor.IsGuardian);
            Assert.AreEqual(_guardian, floor.Opponent);
            Assert.IsTrue(floor.SharedRules.Exists(r => r is MinBankModifier), "guardian's own rule applies");

            var heartsBefore = run.Hearts;
            var win = run.OnWin();
            Assert.IsTrue(win.Guardian);
            Assert.AreEqual(heartsBefore + 1, run.Hearts);
        }

        [Test]
        public void WinGivesCoinsRecordMilestoneOnceAndOffers()
        {
            var run = NewRun();
            run.StartNew(null, 3);
            var first = run.OnWin();
            Assert.IsTrue(first.NewRecord);
            Assert.AreEqual(2, run.Floor);
            Assert.That(run.Offers.Count, Is.InRange(1, _run.Offers));

            var second = run.OnWin();
            Assert.Contains(2, second.Milestones, "milestone on floor 2");

            // Новый забег: рубеж 2 уже получен.
            run.StartNew(null, 4);
            run.OnWin();
            var again = run.OnWin();
            Assert.AreEqual(0, again.Milestones.Count, "milestone reward only once");
            Assert.IsFalse(again.NewRecord, "floor 2 is not above the record");
        }

        [Test]
        public void OffersAreDistinctAndSurviveRestart()
        {
            var run = NewRun();
            run.StartNew(null, 11);
            run.OnWin();
            var offers = new List<string>();
            foreach (var offer in run.Offers)
                offers.Add(offer.Kind + ":" + offer.Value);
            CollectionAssert.AllItemsAreUnique(offers);

            var restored = NewRun();
            Assert.AreEqual(run.Offers.Count, restored.Offers.Count, "offers are in the save");
        }

        [Test]
        public void ChoosingPerksChangesTheRun()
        {
            var run = NewRun();
            run.StartNew(null, 5);
            var saves = _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun);

            saves.Offers.Add(new RunOfferSave { Kind = RunOfferKind.Combo, Value = ComboCategory.OfAKind });
            run.Choose(0);
            Assert.AreEqual(1f + _run.ComboStep, run.ComboMultiplier(ComboCategory.OfAKind), 0.0001f);
            Assert.AreEqual(0, run.Offers.Count, "offers are cleared after the choice");

            saves.Offers.Add(new RunOfferSave { Kind = RunOfferKind.Die, Value = "sixes" });
            run.Choose(0);
            Assert.AreEqual(_sixes, run.BuildFloor().PlayerDice[0], "special die goes into the first standard slot");

            var hearts = run.Hearts;
            saves.Offers.Add(new RunOfferSave { Kind = RunOfferKind.Heart });
            run.Choose(0);
            Assert.AreEqual(hearts + 1, run.Hearts);

            var floor = run.BuildFloor();
            Assert.IsTrue(floor.PlayerModifiers.Exists(m => m is ComboMultiplierModifier c && c.Category == ComboCategory.OfAKind));
            Assert.AreEqual(4, floor.EnemyModifiers.Count, "enemy power for every combo category");
        }

        [Test]
        public void HeartsAndReviveOnce()
        {
            var run = NewRun();
            run.StartNew(null, 9);
            for (var i = 0; i < _run.StartHearts - 1; i++)
                Assert.AreEqual(RunLoss.HeartLost, run.OnLoss());

            Assert.IsFalse(run.CanRevive, "hearts left — no revive yet");
            Assert.AreEqual(RunLoss.Out, run.OnLoss());
            Assert.IsTrue(run.CanRevive);
            run.Revive();
            Assert.AreEqual(1, run.Hearts);
            Assert.AreEqual(RunLoss.Out, run.OnLoss());
            Assert.IsFalse(run.CanRevive, "second revive only from the deep floor");
        }

        // ---------- Забег 2.0 ----------

        private RunPerkEntry Entry(string id, RunPerk perk, Rarity rarity = Rarity.Common, int max = 99)
        {
            var entry = new RunPerkEntry { Id = id, Perk = perk, Rarity = rarity, Weight = 10, MaxStacks = max };
            _run.Perks.Add(entry);
            return entry;
        }

        private void Offer(string id, string value = null)
        {
            _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun).Offers.Add(new RunOfferSave { PerkId = id, Value = value });
        }

        [Test]
        public void ShieldTakesTheHitAndSecondWindSavesOnce()
        {
            Entry("shield", new ShieldPerk());
            Entry("second_wind", new SecondWindPerk(), Rarity.Legendary, 1);
            var run = NewRun();
            run.StartNew(null, 2);

            Offer("shield");
            run.Choose(0);
            Assert.AreEqual(1, run.Shields);
            var hearts = run.Hearts;
            Assert.AreEqual(RunLoss.Shielded, run.OnLoss());
            Assert.AreEqual(hearts, run.Hearts, "shield keeps the heart");
            Assert.AreEqual(0, run.Shields);

            Offer("second_wind");
            run.Choose(0);
            while (run.Hearts > 1)
                run.OnLoss();
            Assert.AreEqual(RunLoss.SecondWind, run.OnLoss());
            Assert.AreEqual(1, run.Hearts);
            Assert.AreEqual(RunLoss.Out, run.OnLoss(), "second wind works once");
        }

        [Test]
        public void StacksMakePerksStrongerOnTheFloor()
        {
            Entry("head_start", new HeadStartPerk { Points = 150 }, max: 3);
            Entry("insurance", new InsurancePerk { PercentPerStack = 15 }, Rarity.Rare, 3);
            Entry("relief", new ReliefPerk { PercentPerStack = 10 }, Rarity.Rare, 3);
            var run = NewRun();
            run.StartNew(null, 2);
            var plain = run.BuildFloor();

            for (var i = 0; i < 2; i++)
            {
                Offer("head_start");
                run.Choose(0);
                Offer("insurance");
                run.Choose(0);
            }

            Offer("relief");
            run.Choose(0);
            var floor = run.BuildFloor();
            Assert.AreEqual(300, floor.PlayerStartScore, "two stacks of head start");
            Assert.IsTrue(floor.PlayerModifiers.Exists(m => m is ZonkInsuranceModifier z && z.KeepPercent == 30));
            Assert.Less(floor.Target, plain.Target, "relief lowers the target");
        }

        [Test]
        public void ZonkSavesAreSpentFromThePoolAndCharmIsFree()
        {
            Entry("zonk_save", new ZonkSavePerk { Charges = 2 }, Rarity.Rare);
            Entry("lucky_charm", new LuckyCharmPerk(), Rarity.Legendary, 1);
            var run = NewRun();
            run.StartNew(null, 2);
            Offer("zonk_save");
            run.Choose(0);
            Offer("lucky_charm");
            run.Choose(0);

            var floor = run.BuildFloor();
            Assert.AreEqual(3, floor.PlayerZonkSaves, "pool of two plus one free");
            run.OnMatchPlayed(floor, 1);
            Assert.AreEqual(1, run.ZonkSaves, "two used: the free one first, then one from the pool");
        }

        [Test]
        public void LoadedDieUpgradesInTheSameSlot()
        {
            var oneFive = Die("loaded15", true);
            var five = Die("loaded5", true);
            var one = Die("loaded1", true);
            _database.EditorSetItems(new List<ContentConfig> { _lucky, _sixes, _config.StandardDie, _regular, _guardian, oneFive, five, one });
            Entry("loaded", new LoadedDiePerk { Steps = new List<DieConfig> { oneFive, five, one } }, Rarity.Rare, 3);
            var run = NewRun();
            run.StartNew(new[] { _lucky, null, null, null, null, null }, 2);

            Offer("loaded");
            run.Choose(0);
            Assert.AreEqual(oneFive, run.BuildFloor().PlayerDice[1]);
            Offer("loaded");
            run.Choose(0);
            Assert.AreEqual(five, run.BuildFloor().PlayerDice[1], "second stack replaces the die in the same slot");
            Offer("loaded");
            run.Choose(0);
            Assert.AreEqual(one, run.BuildFloor().PlayerDice[1]);
            Assert.AreEqual(_lucky, run.BuildFloor().PlayerDice[0]);
        }

        [Test]
        public void EliteFloorIsHarderAndGivesRarePerk()
        {
            Entry("heart", new HeartPerk());
            Entry("shield", new ShieldPerk(), Rarity.Rare);
            var run = NewRun();
            run.StartNew(null, 2);
            run.OnWin();
            Assert.IsTrue(run.NeedsPath);
            run.ChoosePath(RunPath.Normal);
            var normal = run.BuildFloor();
            run.ChoosePath(RunPath.Elite);
            var elite = run.BuildFloor();
            Assert.IsTrue(elite.IsElite);
            Assert.Greater(elite.EnemyPower, normal.EnemyPower);
            Assert.Greater(elite.Target, normal.Target);

            var win = run.OnWin();
            Assert.IsTrue(win.Elite);
            Assert.AreEqual("shield", run.Offers[0].PerkId, "elite win always offers a rare perk first");
        }

        [Test]
        public void ShopSellsPerksForTokens()
        {
            Entry("shield", new ShieldPerk(), Rarity.Rare);
            Entry("purse", new TokenPursePerk { Tokens = 5 });
            var run = NewRun();
            run.StartNew(null, 2);
            var save = _saves.Get<EndlessRunSave>(SaveKeys.EndlessRun);
            save.Tokens = 6;
            save.Floor = 4;
            run.ChoosePath(RunPath.Shop);
            Assert.IsTrue(run.IsShopOpen);
            var index = save.Offers.FindIndex(o => o.PerkId == "shield");
            Assert.GreaterOrEqual(index, 0);
            Assert.IsTrue(run.Buy(index));
            Assert.AreEqual(0, run.Tokens);
            Assert.AreEqual(1, run.Shields);
            run.LeaveShop();
            Assert.IsFalse(run.IsShopOpen);
            Assert.AreEqual(RunPath.Normal, run.CurrentPath);
        }

        [Test]
        public void ExtraPickForAdKeepsTheRestOfTheOffers()
        {
            Entry("a", new ShieldPerk());
            Entry("b", new TokenPursePerk());
            Entry("c", new HeadStartPerk(), max: 3);
            var run = NewRun();
            run.StartNew(null, 2);
            run.OnWin();
            Assert.AreEqual(3, run.Offers.Count);
            Assert.IsTrue(run.CanTakeExtra);
            run.GrantExtraPick();
            run.Choose(0);
            Assert.AreEqual(2, run.Offers.Count, "after the ad one more of the rest");
            Assert.IsFalse(run.CanTakeExtra);
            run.Choose(0);
            Assert.AreEqual(0, run.Offers.Count);
        }

        [Test]
        public void TowerCheckpointsAndFirstClearRewards()
        {
            var tower = new TowerProgress(_saves, _config);
            Assert.AreEqual(0, tower.FloorIndex);

            tower.StartAttempt();
            var first = tower.OnWin();
            Assert.IsTrue(first.FirstClear);
            Assert.IsFalse(first.CheckpointReached);
            var second = tower.OnWin();
            Assert.IsTrue(second.CheckpointReached, "every 2 floors");
            Assert.AreEqual(2, tower.Checkpoint);

            // Проиграл все сердца на третьем этаже — следующая попытка с рубежа.
            tower.OnLoss();
            tower.OnLoss();
            tower.EndAttempt();
            Assert.AreEqual(2, tower.FloorIndex);

            tower.StartAttempt();
            Assert.AreEqual(2, tower.FloorIndex);
            Assert.AreEqual(_tower.HeartsPerAttempt, tower.Hearts);
        }

        [Test]
        public void TowerCompletesAndReplayGivesOnlyRepeatCoins()
        {
            var tower = new TowerProgress(_saves, _config);
            tower.StartAttempt();
            TowerWin last = null;
            for (var i = 0; i < 4; i++)
                last = tower.OnWin();

            Assert.IsTrue(last.TowerCompleted);
            Assert.IsTrue(tower.IsComplete);
            Assert.IsFalse(tower.IsActive);

            tower.StartAttempt();
            Assert.AreEqual(0, tower.FloorIndex, "after completion the climb starts again from the bottom");
            var replay = tower.OnWin();
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(1, replay.Rewards.Count);
            Assert.AreEqual(_tower.RepeatCoins, ((CurrencyReward)replay.Rewards[0]).Amount);
        }

        [Test]
        public void TowerContinuesWhenFloorsAreAdded()
        {
            var tower = new TowerProgress(_saves, _config);
            tower.StartAttempt();
            for (var i = 0; i < 4; i++)
                tower.OnWin();
            Assert.IsTrue(tower.IsComplete);

            // Обновление игры: ещё два этажа.
            _tower.Floors.Add(new TowerFloor { Opponent = _guardian, Target = 5000 });
            _tower.Floors.Add(new TowerFloor { Opponent = _guardian, Target = 6000 });
            Assert.IsFalse(tower.IsComplete);
            Assert.AreEqual(4, tower.FloorIndex, "the next attempt starts from the new floors");
        }
    }
}
